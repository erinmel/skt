using Avalonia.Controls;
using Avalonia.Input;
using skt.IDE.Models;
using skt.IDE.ViewModels.ToolWindows;
using skt.IDE.Services.Buss;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace skt.IDE.Views.ToolWindows;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        FileTreeView.SelectionChanged += FileTreeView_SelectionChanged;
        FileTreeView.DoubleTapped += FileTreeView_DoubleTapped;
        AttachContextMenuHandlers();
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
                // File double-clicked - publish an open request so TabbedEditor handles it
                // Notify local listeners and show a brief status message while the editor opens
                viewModel.NotifyFileSelected(selectedNode.FullPath);
                App.EventBus.Publish(new StatusBarMessageEvent($"Opening: {Path.GetFileName(selectedNode.FullPath)}", 1500));
                App.EventBus.Publish(new OpenFileRequestEvent(selectedNode.FullPath));
            }
            else
            {
                // Double-click on folder - also toggle (for redundancy)
                selectedNode.IsExpanded = !selectedNode.IsExpanded;
            }
            e.Handled = true;
        }
    }

    private void AttachContextMenuHandlers()
    {
        if (FileTreeView?.ContextMenu is not { } ctx)
            return;

        ctx.Opened += (_, _) => HandleContextMenuOpened(ctx);
    }

    private void HandleContextMenuOpened(ContextMenu ctx)
    {
        var selectedNode = FileTreeView.SelectedItem as FileNode;

        foreach (var item in ctx.Items.OfType<MenuItem>())
        {
            SetCommandParameterIfBound(item, selectedNode);
            WireCommandParameterICommandClick(item);
        }
    }

    private void SetCommandParameterIfBound(MenuItem item, FileNode? selectedNode)
    {
        if (item.Command is not null)
        {
            item.CommandParameter = selectedNode;
        }
    }

    private void WireCommandParameterICommandClick(MenuItem item)
    {
        if (item.CommandParameter is ICommand && item.Tag as string != "wired")
        {
            item.Click += (_, _) => ExecuteMenuItemCommand(item);
            item.Tag = "wired";
        }
    }

    private void ExecuteMenuItemCommand(MenuItem item)
    {
        if (item.CommandParameter is ICommand cmd)
        {
            var param = FileTreeView.SelectedItem as FileNode;
            if (cmd.CanExecute(param))
            {
                cmd.Execute(param);
            }
        }
    }
}
