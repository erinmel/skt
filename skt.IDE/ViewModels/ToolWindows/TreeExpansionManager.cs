using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace skt.IDE.ViewModels.ToolWindows;

public enum TreeExpansionMode
{
    Collapsed,
    FirstLevelOnly,
    FullyExpanded
}

public interface ITreeExpansionManager<TNodeViewModel> where TNodeViewModel : ITreeNodeViewModel
{
    TreeExpansionMode ExpansionMode { get; set; }
    void ApplyExpansionMode(IEnumerable<TNodeViewModel> rootNodes);
    void ExpandAll(IEnumerable<TNodeViewModel> nodes);
    void CollapseAll(IEnumerable<TNodeViewModel> nodes);
    void ExpandFirstLevel(IEnumerable<TNodeViewModel> nodes);
}

public class TreeExpansionManager<TNodeViewModel> : ITreeExpansionManager<TNodeViewModel>
    where TNodeViewModel : class, ITreeNodeViewModel
{
    private TreeExpansionMode _expansionMode = TreeExpansionMode.FullyExpanded;

    public TreeExpansionMode ExpansionMode
    {
        get => _expansionMode;
        set => _expansionMode = value;
    }

    public void ApplyExpansionMode(IEnumerable<TNodeViewModel> rootNodes)
    {
        switch (_expansionMode)
        {
            case TreeExpansionMode.Collapsed:
                CollapseAll(rootNodes);
                break;
            case TreeExpansionMode.FirstLevelOnly:
                ExpandFirstLevel(rootNodes);
                break;
            case TreeExpansionMode.FullyExpanded:
                ExpandAll(rootNodes);
                break;
        }
    }

    public void ExpandAll(IEnumerable<TNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsExpanded)
            {
                node.IsExpanded = true;
            }

            if (node.HasChildren)
            {
                var children = GetChildrenFromNode(node);
                if (children != null)
                {
                    ExpandAll(children);
                }
            }
        }
    }

    public void CollapseAll(IEnumerable<TNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.HasChildren && node.ChildrenLoaded)
            {
                var children = GetChildrenFromNode(node);
                if (children != null)
                {
                    CollapseAll(children);
                }
            }
            node.IsExpanded = false;
        }
    }

    public void ExpandFirstLevel(IEnumerable<TNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = true;
            if (node.HasChildren && node.ChildrenLoaded)
            {
                var children = GetChildrenFromNode(node);
                if (children != null)
                {
                    CollapseAll(children);
                }
            }
        }
    }

    private IEnumerable<TNodeViewModel>? GetChildrenFromNode(TNodeViewModel node)
    {
        var childrenProperty = node.GetType().GetProperty("Children");
        return childrenProperty?.GetValue(node) as IEnumerable<TNodeViewModel>;
    }
}

public static class TreeViewHelpers
{
    public static TNodeViewModel? FindNodeByPath<TNodeViewModel>(IEnumerable<TNodeViewModel> nodes, string path)
        where TNodeViewModel : class, ITreeNodeViewModel
    {
        var nodesList = nodes as TNodeViewModel[] ?? nodes.ToArray();
        foreach (var node in nodesList)
        {
            if (string.Equals(node.StableId, path, StringComparison.OrdinalIgnoreCase))
                return node;

            if (node.HasChildren)
            {
                var childrenProperty = node.GetType().GetProperty("Children");
                var children = childrenProperty?.GetValue(node) as IEnumerable<TNodeViewModel>;
                if (children != null)
                {
                    var found = FindNodeByPath(children, path);
                    if (found != null)
                        return found;
                }
            }
        }
        return null;
    }

    public static void ExpandAncestorsForPath<TNodeViewModel>(IEnumerable<TNodeViewModel> nodes, string path)
        where TNodeViewModel : class, ITreeNodeViewModel
    {
        var nodesList = nodes as TNodeViewModel[] ?? nodes.ToArray();
        var matchingNodes = nodesList
            .Where(n => path.StartsWith(n.StableId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var node in matchingNodes)
        {
            node.IsExpanded = true;

            if (string.Equals(node.StableId, path, StringComparison.OrdinalIgnoreCase))
                return;

            if (node.HasChildren)
            {
                var childrenProperty = node.GetType().GetProperty("Children");
                var children = childrenProperty?.GetValue(node) as IEnumerable<TNodeViewModel>;
                if (children != null)
                {
                    ExpandAncestorsForPath(children, path);
                }
            }
        }
    }

    public static void TryExpandInSource<TNodeViewModel>(TNodeViewModel target)
        where TNodeViewModel : class, ITreeNodeViewModel
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

    public static void SetScrollOffset(TreeDataGrid treeGrid, double verticalOffset)
    {
        var scrollViewer = treeGrid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer != null)
        {
            scrollViewer.Offset = new Avalonia.Vector(scrollViewer.Offset.X, verticalOffset);
        }
    }

    public static async System.Threading.Tasks.Task RestoreVisualStateAsync<TNodeViewModel>(
        TreeDataGrid treeGrid,
        IEnumerable<TNodeViewModel> rootNodes,
        string? nodePath,
        double verticalOffset)
        where TNodeViewModel : class, ITreeNodeViewModel
    {
        try
        {
            if (string.IsNullOrEmpty(nodePath))
            {
                SetScrollOffset(treeGrid, verticalOffset);
                return;
            }

            var rootNodesList = rootNodes as TNodeViewModel[] ?? rootNodes.ToArray();
            var target = FindNodeByPath(rootNodesList, nodePath);
            if (target == null)
            {
                SetScrollOffset(treeGrid, verticalOffset);
                return;
            }

            ExpandAncestorsForPath(rootNodesList, nodePath);
            TryExpandInSource(target);

            await System.Threading.Tasks.Task.Delay(50);

            try
            {
                var row = treeGrid.GetVisualDescendants()
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

            SetScrollOffset(treeGrid, verticalOffset);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestoreVisualState failed: {ex}");
        }
    }
}
