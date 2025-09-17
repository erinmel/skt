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
        FileTreeView.PointerPressed += FileTreeView_PointerPressed;
        FileTreeView.DoubleTapped += FileTreeView_DoubleTapped;
    }

    private void FileTreeView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pointProps = e.GetCurrentPoint(this).Properties;
        if (!pointProps.IsLeftButtonPressed)
            return;

        // Single-click only
        if (e.ClickCount != 1)
            return;

        if (e.Source is Control control)
        {
            var tvi = control.FindAncestorOfType<TreeViewItem>();
            if (tvi?.DataContext is FileNode { IsDirectory: true } node)
            {
                node.IsExpanded = !node.IsExpanded;
                e.Handled = true;
            }
        }
    }

    private void FileTreeView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (FileTreeView.SelectedItem is FileNode selectedNode && DataContext is FileExplorerViewModel viewModel && !selectedNode.IsDirectory)
        {
            // File double-clicked - open it
            viewModel.NotifyFileSelected(selectedNode.FullPath);

            // Find the parent MainWindow and open the file
            var mainWindow = this.FindAncestorOfType<MainWindow>();
            if (mainWindow?.DataContext is ViewModels.MainWindowViewModel mainViewModel)
            {
                _ = mainViewModel.OpenFile(selectedNode.FullPath);
            }
            e.Handled = true;
        }
    }
}
