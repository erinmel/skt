using System;
using Avalonia.Controls;
using skt.IDE.ViewModels.ToolWindows;
using skt.IDE.Services.Buss;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;

namespace skt.IDE.Views.ToolWindows;

public partial class SemanticTreeView : UserControl
{
    private SemanticTreeViewModel? ViewModel => DataContext as SemanticTreeViewModel;

    public SemanticTreeView()
    {
        InitializeComponent();

        var bus = App.EventBus;
        bus.Subscribe<SemanticAnalysisCompletedEvent>(OnSemanticAnalysisCompleted);
        bus.Subscribe<SemanticAnalysisFailedEvent>(OnSemanticAnalysisFailed);

        DataContextChanged += SemanticTreeView_DataContextChanged;
    }

    private void SemanticTreeView_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SemanticTreeViewModel vm)
        {
            vm.RequestVisualState = () =>
            {
                var focusedRow = SemanticTreeGrid.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(c => c.IsFocused && c.DataContext is AnnotatedAstNodeViewModel);

                var selectedPath = (focusedRow?.DataContext as AnnotatedAstNodeViewModel)?.NodePath;
                double offset = 0;
                var scroll = SemanticTreeGrid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
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

    private void OnSemanticAnalysisCompleted(SemanticAnalysisCompletedEvent e)
    {
        if (ViewModel != null)
        {
            ViewModel.UpdateTree(e.AnnotatedAst, e.Errors);
        }
    }

    private void OnSemanticAnalysisFailed(SemanticAnalysisFailedEvent e)
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

            if (DataContext is not SemanticTreeViewModel vm)
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

            try
            {
                var row = SemanticTreeGrid.GetVisualDescendants()
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

    private void TryExpandInSource(AnnotatedAstNodeViewModel target)
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
        var scrollViewer = SemanticTreeGrid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer != null)
        {
            scrollViewer.Offset = new Avalonia.Vector(scrollViewer.Offset.X, verticalOffset);
        }
    }

    private AnnotatedAstNodeViewModel? FindNodeByPath(System.Collections.Generic.IEnumerable<AnnotatedAstNodeViewModel> nodes, string path)
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

    private void ExpandAncestorsForPath(System.Collections.Generic.IEnumerable<AnnotatedAstNodeViewModel> nodes, string path)
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