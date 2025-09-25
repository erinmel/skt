using Avalonia.Controls;
using Avalonia.Input;
using skt.IDE.Models;
using skt.IDE.ViewModels.ToolWindows;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using skt.IDE.Services.Buss;

namespace skt.IDE.Views.ToolWindows;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        // Removed auto expansion toggle on selection change to prevent Up/Down expanding folders
        FileTreeView.DoubleTapped += FileTreeView_DoubleTapped;
        FileTreeView.KeyDown += FileTreeView_KeyDown;
        DataContextChanged += FileExplorerView_DataContextChanged;
        AttachContextMenuHandlers();
    }

    private void FileExplorerView_DataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is FileExplorerViewModel vm)
        {
            vm.InlineRenameStarted -= FocusInlineEditor;
            vm.InlineRenameStarted += FocusInlineEditor;

            // provide the viewmodel with a way to query current visual state
            vm.RequestVisualState = () =>
            {
                var selectedNode = FileTreeView.SelectedItem as FileNode;
                double offset = 0;
                var scroll = FileTreeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (scroll != null)
                {
                    offset = scroll.Offset.Y;
                }
                return (selectedNode?.FullPath, offset);
            };

            // subscribe to restore requests
            vm.RestoreVisualStateRequested -= RestoreVisualState;
            vm.RestoreVisualStateRequested += RestoreVisualState;
        }
    }

    private void FileTreeView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (FileTreeView.SelectedItem is FileNode node && DataContext is FileExplorerViewModel vm)
        {
            if (e.Key == Key.F2)
            {
                if (vm.RenameResourceCommand.CanExecute(node))
                {
                    vm.RenameResourceCommand.Execute(node);
                    e.Handled = true;
                }
                return;
            }
            if (e.Key == Key.Right && node.IsDirectory && !node.IsExpanded)
            {
                node.IsExpanded = true;
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Left && node.IsDirectory && node.IsExpanded)
            {
                node.IsExpanded = false;
                e.Handled = true;
                return;
            }
        }
    }

    private void FileTreeView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Open file or toggle folder on double click
        if (FileTreeView.SelectedItem is not FileNode selectedNode) return;

        // Ignore placeholder nodes
        if (selectedNode.IsPlaceholder) return;

        // Directory: toggle expansion
        if (selectedNode.IsDirectory)
        {
            selectedNode.IsExpanded = !selectedNode.IsExpanded;
            e.Handled = true;
            return;
        }

        // File: request to open
        if (File.Exists(selectedNode.FullPath))
        {
            App.EventBus.Publish(new OpenFileRequestEvent(selectedNode.FullPath));
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

    private void InlineEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not FileNode node || DataContext is not FileExplorerViewModel vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.CommitInlineRename(node);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelInlineRename(node);
            e.Handled = true;
        }
    }

    private void InlineEditor_LostFocus(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not FileNode node || DataContext is not FileExplorerViewModel vm)
            return;
        if (node.IsEditing)
        {
            vm.CommitInlineRename(node);
        }
    }

    private void FocusInlineEditor(FileNode node)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var editor = FileTreeView.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(t => t.Name == "InlineEditor" && t.DataContext == node);
            if (editor != null)
            {
                editor.Focus();
                try
                {
                    var text = editor.Text ?? string.Empty;
                    if (!node.IsDirectory)
                    {
                        var ext = Path.GetExtension(text);
                        var length = text.Length - ext.Length;
                        if (length < 0) length = text.Length;
                        editor.SelectionStart = 0;
                        editor.SelectionEnd = length;
                    }
                    else
                    {
                        editor.SelectionStart = 0;
                        editor.SelectionEnd = text.Length;
                    }
                }
                catch (Exception)
                {
                }
            }
        });
    }

    private void RestoreVisualState(string? selectedPath, double verticalOffset)
    {
        // run after layout pass to ensure TreeViewItems are generated
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                if (string.IsNullOrEmpty(selectedPath))
                {
                    // still restore scroll if possible
                    var scrollViewer = FileTreeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                    if (scrollViewer != null)
                    {
                        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, verticalOffset);
                    }
                    return;
                }

                if (DataContext is not FileExplorerViewModel vm) return;

                // Find the FileNode by path in the VM's tree
                var rootNodes = vm.RootNodes;
                var target = FindNodeByPath(rootNodes, selectedPath);
                if (target == null)
                {
                    // couldn't find, still try to restore scroll
                    var scroll2 = FileTreeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                    if (scroll2 != null)
                    {
                        scroll2.Offset = new Vector(scroll2.Offset.X, verticalOffset);
                    }
                    return;
                }

                // Expand ancestors
                ExpandAncestorsForPath(rootNodes, selectedPath);

                // Wait a tick for visuals to be realized
                await Task.Delay(50);

                // Find corresponding TreeViewItem and select it
                var tvi = FileTreeView.GetVisualDescendants().OfType<TreeViewItem>().FirstOrDefault(tv => tv.DataContext == target);
                if (tvi != null)
                {
                    tvi.IsSelected = true;
                    tvi.BringIntoView();
                }

                // Restore scroll offset
                var scroll = FileTreeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (scroll != null)
                {
                    scroll.Offset = new Vector(scroll.Offset.X, verticalOffset);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreVisualState failed: {ex}");
            }
        });
    }

    private FileNode? FindNodeByPath(IEnumerable<FileNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase)) return node;
            if (node.Children?.Count > 0)
            {
                var found = FindNodeByPath(node.Children, path);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void ExpandAncestorsForPath(IEnumerable<FileNode> nodes, string path)
    {
        // recursively expand a node's ancestors by comparing path prefixes
        foreach (var node in nodes)
        {
            if (path.StartsWith(node.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                node.IsExpanded = true;
                // if exact match we can stop after expanding current
                if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                ExpandAncestorsForPath(node.Children, path);
            }
        }
    }
}
