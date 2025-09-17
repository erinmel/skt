using Avalonia.Controls;
using Avalonia.Input;
using skt.IDE.Models;
using skt.IDE.ViewModels.ToolWindows;
using Avalonia.VisualTree;

namespace skt.IDE.Views.ToolWindows;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        FileTreeView.SelectionChanged += FileTreeView_SelectionChanged;
        FileTreeView.DoubleTapped += FileTreeView_DoubleTapped;
    }

    private void FileTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Handle single-click selection for folders
        if (FileTreeView.SelectedItem is FileNode { IsDirectory: true } selectedNode)
        {
            selectedNode.IsExpanded = !selectedNode.IsExpanded;
        }
    }

    private void FileTreeView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (FileTreeView.SelectedItem is FileNode selectedNode && DataContext is FileExplorerViewModel viewModel)
        {
            if (!selectedNode.IsDirectory)
            {
                // File double-clicked - open it
                viewModel.NotifyFileSelected(selectedNode.FullPath);

                // Find the parent MainWindow and open the file
                var mainWindow = this.FindAncestorOfType<MainWindow>();
                if (mainWindow?.DataContext is ViewModels.MainWindowViewModel mainViewModel)
                {
                    _ = mainViewModel.OpenFile(selectedNode.FullPath);
                }
            }
            else
            {
                // Double-click on folder - also toggle (for redundancy)
                selectedNode.IsExpanded = !selectedNode.IsExpanded;
            }
            e.Handled = true;
        }
    }
}
