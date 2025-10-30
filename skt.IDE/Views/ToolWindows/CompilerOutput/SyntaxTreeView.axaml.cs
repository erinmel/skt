using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using skt.IDE.ViewModels.ToolWindows;
using skt.IDE.Services.Buss;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;

namespace skt.IDE.Views.ToolWindows.CompilerOutput;

public partial class SyntaxTreeView : UserControl
{
    private SyntaxTreeViewModel? ViewModel => DataContext as SyntaxTreeViewModel;

    public SyntaxTreeView()
    {
        InitializeComponent();

        App.Messenger.Register<SyntaxAnalysisCompletedEvent>(this, (_, m) => OnSyntaxAnalysisCompleted(m));
        App.Messenger.Register<SyntaxAnalysisFailedEvent>(this, (_, m) => OnSyntaxAnalysisFailed(m));

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

            vm.NotifyTreeDataGridToExpandAll -= ExpandAllRowsInTreeDataGrid;
            vm.NotifyTreeDataGridToExpandAll += ExpandAllRowsInTreeDataGrid;
            vm.NotifyTreeDataGridToCollapseAll -= CollapseAllRowsInTreeDataGrid;
            vm.NotifyTreeDataGridToCollapseAll += CollapseAllRowsInTreeDataGrid;
        }
    }

    private void OnSyntaxAnalysisCompleted(SyntaxAnalysisCompletedEvent e)
    {
        if (ViewModel != null)
        {
            var state = ViewModel.RequestVisualState?.Invoke();
            ViewModel.UpdateTree(e.Ast, e.Errors);
            RestoreVisualState(state?.selectedPath, state?.verticalOffset ?? 0);
        }
    }

    private void OnSyntaxAnalysisFailed(SyntaxAnalysisFailedEvent e)
    {
        // Use the parameter so analyzers don't warn about an unused parameter.
        _ = e;

        if (ViewModel != null)
        {
            ViewModel.Clear();
        }
    }

    private void RestoreVisualState(string? nodePath, double verticalOffset)
    {
        Dispatcher.UIThread.Post(() => _ = TreeViewHelpers.RestoreVisualStateAsync(
            SyntaxTreeGrid,
            ViewModel?.RootNodes ?? new ObservableCollection<AstNodeViewModel>(),
            nodePath,
            verticalOffset));
    }

    private void ExpandAllRowsInTreeDataGrid()
    {
         if (ViewModel?.RootNodes == null) return;

         ExpandAllNodesRecursively(ViewModel.RootNodes);

         // Ensure the TreeDataGrid's internal source sees the expansion by toggling rows in the source
         ForceExpandInSourceRecursively(ViewModel.RootNodes);

         // Retry a few times: refresh source and re-apply TryExpand to handle virtualization/materialization delays
         Dispatcher.UIThread.Post(() =>
         {
             _ = ExpandRetryAsync(ViewModel.RootNodes, SyntaxTreeGrid);
         });
    }

    private async System.Threading.Tasks.Task ExpandRetryAsync(IEnumerable<AstNodeViewModel> nodes, TreeDataGrid grid)
    {
        int attempts = 4;
        var currentOffset = grid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.Offset.Y ?? 0;
        for (int i = 0; i < attempts; i++)
        {
            await System.Threading.Tasks.Task.Delay(50 * (i + 1));

            var src = grid.Source;
            grid.Source = null;
            grid.Source = src;

            // Re-apply try-expand to encourage the source to mark rows expanded
            ForceExpandInSourceRecursively(nodes);

            // Reapply scroll offset to further nudge layout and materialization
            TreeViewHelpers.SetScrollOffset(grid, currentOffset);
        }
    }

    private void ForceExpandInSourceRecursively(IEnumerable<AstNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            // TryExpandInSource toggles IsExpanded to ensure the TreeDataGrid's source becomes aware of the change
            TreeViewHelpers.TryExpandInSource(node);

            var children = node.Children;
            if (children.Count > 0)
            {
                ForceExpandInSourceRecursively(children);
            }
        }
    }

    private void CollapseAllRowsInTreeDataGrid()
    {
        if (ViewModel?.RootNodes == null) return;

        CollapseAllNodesRecursively(ViewModel.RootNodes);

        // Force TreeDataGrid to refresh visuals so the collapsed state is reflected
        Dispatcher.UIThread.Post(() =>
        {
            var src = SyntaxTreeGrid.Source;
            SyntaxTreeGrid.Source = null;
            SyntaxTreeGrid.Source = src;
        });
    }

    private void ExpandAllNodesRecursively(IEnumerable<AstNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.HasChildren)
            {
                node.IsExpanded = true;
                var children = node.Children;
                if (children.Count > 0)
                {
                    ExpandAllNodesRecursively(children);
                }
            }
        }
    }

    private void CollapseAllNodesRecursively(IEnumerable<AstNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.HasChildren)
            {
                node.IsExpanded = false;
                var children = node.Children;
                if (children.Count > 0)
                {
                    CollapseAllNodesRecursively(children);
                }
            }
        }
    }
}
