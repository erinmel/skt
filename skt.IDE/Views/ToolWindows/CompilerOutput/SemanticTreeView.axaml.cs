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
        System.Diagnostics.Debug.WriteLine("ExpandAllRowsInTreeDataGrid called");
        if (ViewModel?.RootNodes == null) return;

        ExpandAllNodesRecursively(ViewModel.RootNodes);
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
}