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

public partial class SemanticTreeView : UserControl
{
    private SemanticTreeViewModel? ViewModel => DataContext as SemanticTreeViewModel;

    public SemanticTreeView()
    {
        InitializeComponent();

        App.Messenger.Register<SemanticAnalysisCompletedEvent>(this, (r, m) => OnSemanticAnalysisCompleted(m));
        App.Messenger.Register<SemanticAnalysisFailedEvent>(this, (r, m) => OnSemanticAnalysisFailed(m));

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


            vm.NotifyTreeDataGridToExpandAll -= ExpandAllRowsInTreeDataGrid;
            vm.NotifyTreeDataGridToExpandAll += ExpandAllRowsInTreeDataGrid;
            vm.NotifyTreeDataGridToCollapseAll -= CollapseAllRowsInTreeDataGrid;
            vm.NotifyTreeDataGridToCollapseAll += CollapseAllRowsInTreeDataGrid;
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
        Dispatcher.UIThread.Post(() => _ = TreeViewHelpers.RestoreVisualStateAsync<AnnotatedAstNodeViewModel>(
            SemanticTreeGrid,
            ViewModel?.RootNodes ?? new ObservableCollection<AnnotatedAstNodeViewModel>(),
            nodePath,
            verticalOffset));
    }

    private void ExpandAllRowsInTreeDataGrid()
    {
        if (ViewModel?.RootNodes == null) return;

        ExpandAllNodesRecursively(ViewModel.RootNodes);

        // Ensure the TreeDataGrid's internal source sees the expansion by toggling rows in the source
        ForceExpandInSourceRecursively(ViewModel.RootNodes);

        // Retry a few times to handle virtualization/materialization delays by toggling the view-model TreeSource
        Dispatcher.UIThread.Post(() =>
        {
            _ = ExpandRetryAsync(ViewModel.RootNodes, SemanticTreeGrid);
        });
    }

    private async System.Threading.Tasks.Task ExpandRetryAsync(IEnumerable<AnnotatedAstNodeViewModel> nodes, TreeDataGrid grid)
    {
        int attempts = 4;
        var currentOffset = grid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.Offset.Y ?? 0;
        var vmSrc = ViewModel?.TreeSource;
        for (int i = 0; i < attempts; i++)
        {
            await System.Threading.Tasks.Task.Delay(50 * (i + 1));

            if (ViewModel != null)
            {
                ViewModel.TreeSource = null;
                ViewModel.TreeSource = vmSrc;
            }

            ForceExpandInSourceRecursively(nodes);

            TreeViewHelpers.SetScrollOffset(grid, currentOffset);
        }
    }

    private void ForceExpandInSourceRecursively(IEnumerable<AnnotatedAstNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
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

        // Force TreeDataGrid to refresh visuals so the collapsed state is reflected by toggling the view-model TreeSource
        Dispatcher.UIThread.Post(() =>
        {
            var src = ViewModel.TreeSource;
            ViewModel.TreeSource = null;
            ViewModel.TreeSource = src;
        });
    }

    private void ExpandAllNodesRecursively(IEnumerable<AnnotatedAstNodeViewModel> nodes)
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

    private void CollapseAllNodesRecursively(IEnumerable<AnnotatedAstNodeViewModel> nodes)
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