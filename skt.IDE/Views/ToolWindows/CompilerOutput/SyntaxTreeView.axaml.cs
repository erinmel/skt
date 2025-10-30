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

        App.Messenger.Register<SyntaxAnalysisCompletedEvent>(this, (r, m) => OnSyntaxAnalysisCompleted(m));
        App.Messenger.Register<SyntaxAnalysisFailedEvent>(this, (r, m) => OnSyntaxAnalysisFailed(m));

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
        Dispatcher.UIThread.Post(() => _ = TreeViewHelpers.RestoreVisualStateAsync<AstNodeViewModel>(
            SyntaxTreeGrid,
            ViewModel?.RootNodes ?? new ObservableCollection<AstNodeViewModel>(),
            nodePath,
            verticalOffset));
    }

    private void ExpandAllRowsInTreeDataGrid()
    {
        System.Diagnostics.Debug.WriteLine("ExpandAllRowsInTreeDataGrid called");
        if (ViewModel?.RootNodes == null) return;

        ExpandAllNodesRecursively(ViewModel.RootNodes);
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
}
