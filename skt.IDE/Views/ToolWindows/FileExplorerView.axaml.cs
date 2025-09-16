using Avalonia.Controls;
using Avalonia.Input;
using skt.IDE.Models;
using skt.IDE.ViewModels.ToolWindows;
using System;
using Avalonia.VisualTree;

namespace skt.IDE.Views.ToolWindows;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is FileExplorerViewModel viewModel)
        {
            // Subscribe to file selection events
            viewModel.FileSelected += OnFileSelected;
        }

        // Add double-click handler to the TreeView
        if (FileTreeView != null)
        {
            FileTreeView.DoubleTapped += FileTreeView_DoubleTapped;
        }
    }

    private void FileTreeView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (FileTreeView.SelectedItem is FileNode selectedNode && DataContext is FileExplorerViewModel viewModel)
        {
            if (!selectedNode.IsDirectory)
            {
                // File double-clicked - open it
                viewModel.SelectFileCommand.Execute(selectedNode);
            }
            else
            {
                // Directory double-clicked - expand/collapse it
                selectedNode.IsExpanded = !selectedNode.IsExpanded;
            }
        }
    }

    private void OnFileSelected(string filePath)
    {
        // Find the parent MainWindow using the visual tree
        var mainWindow = this.FindAncestorOfType<MainWindow>();
        if (mainWindow?.DataContext is ViewModels.MainWindowViewModel mainViewModel)
        {
            _ = mainViewModel.OpenFile(filePath);
        }
    }
}
