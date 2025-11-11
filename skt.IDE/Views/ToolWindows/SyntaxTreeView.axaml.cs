using System;
using Avalonia.Controls;
using skt.IDE.ViewModels.ToolWindows;
using skt.IDE.Services.Buss;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;

namespace skt.IDE.Views.ToolWindows;

public partial class SyntaxTreeView : UserControl
{
    private SyntaxTreeViewModel? ViewModel => DataContext as SyntaxTreeViewModel;

    public SyntaxTreeView()
    {
        InitializeComponent();

        // Subscribe to syntax analysis events
        var bus = App.EventBus;
        bus.Subscribe<SyntaxAnalysisCompletedEvent>(OnSyntaxAnalysisCompleted);
        bus.Subscribe<SyntaxAnalysisFailedEvent>(OnSyntaxAnalysisFailed);

        DataContextChanged += SyntaxTreeView_DataContextChanged;
    }

    private void SyntaxTreeView_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SyntaxTreeViewModel vm)
        {
            vm.RequestVisualState = () =>
            {
                // Try to capture the currently focused row's DataContext (an AstNodeViewModel)
                var focusedRow = SyntaxTreeGrid.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(c => c.IsFocused && c.DataContext is AstNodeViewModel);

                var selectedPath = (focusedRow?.DataContext as AstNodeViewModel)?.NodePath;
                double offset = 0;
                var scroll = SyntaxTreeGrid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (scroll != null)
                {
                    offset = scroll.Offset.Y;
                }
                return (selectedPath, offset);
            };

            vm.RestoreVisualStateRequested -= RestoreVisualState;
            vm.RestoreVisualStateRequested += RestoreVisualState;
        }
    }

    private void OnSyntaxAnalysisCompleted(SyntaxAnalysisCompletedEvent e)
    {
        if (ViewModel != null)
        {
            ViewModel.UpdateTree(e.Ast, e.Errors);
        }
    }

    private void OnSyntaxAnalysisFailed(SyntaxAnalysisFailedEvent e)
    {
        if (ViewModel != null)
        {
            ViewModel.Clear();
        }
    }

    private void RestoreVisualState(string? nodePath, double verticalOffset)
    {
        Dispatcher.UIThread.Post(() => _ = RestoreVisualStateAsync(nodePath, verticalOffset));
    }

    private async System.Threading.Tasks.Task RestoreVisualStateAsync(string? nodePath, double verticalOffset)
    {
        try
        {
            if (string.IsNullOrEmpty(nodePath))
            {
                SetScrollOffset(verticalOffset);
                return;
            }

            if (DataContext is not SyntaxTreeViewModel vm)
                return;

            var rootNodes = vm.RootNodes;
            var target = FindNodeByPath(rootNodes, nodePath);
            if (target == null)
            {
                SetScrollOffset(verticalOffset);
                return;
            }

            ExpandAncestorsForPath(rootNodes, nodePath);

            TryExpandInSource(target);

            await System.Threading.Tasks.Task.Delay(50);

            // Try to select via control if possible, otherwise set SelectedItem
            try
            {
                var row = SyntaxTreeGrid.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(c => c.DataContext == target);

                if (row != null)
                {
                    row.Focus();
                    row.BringIntoView();
                }
            }
            catch
            {
                // Best-effort: ignore
            }

            SetScrollOffset(verticalOffset);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestoreVisualState failed: {ex}");
        }
    }

    private void TryExpandInSource(AstNodeViewModel target)
    {
        if (!target.IsExpanded)
        {
            target.IsExpanded = true;
        }
        else
        {
            target.IsExpanded = false;
            target.IsExpanded = true;
        }
    }

    private void SetScrollOffset(double verticalOffset)
    {
        var scrollViewer = SyntaxTreeGrid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer != null)
        {
            scrollViewer.Offset = new Avalonia.Vector(scrollViewer.Offset.X, verticalOffset);
        }
    }

    private AstNodeViewModel? FindNodeByPath(System.Collections.Generic.IEnumerable<AstNodeViewModel> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.StableId, path, StringComparison.OrdinalIgnoreCase)) return node;
            if (node.Children.Count > 0)
            {
                var found = FindNodeByPath(node.Children, path);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void ExpandAncestorsForPath(System.Collections.Generic.IEnumerable<AstNodeViewModel> nodes, string path)
    {
        var matchingNodes = nodes
            .Where(n => path.StartsWith(n.StableId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var node in matchingNodes)
        {
            node.IsExpanded = true;
            TryExpandInSource(node);
            if (string.Equals(node.StableId, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            ExpandAncestorsForPath(node.Children, path);
        }
    }
}
