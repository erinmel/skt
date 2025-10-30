using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    Task ApplyExpansionModeAsync(IEnumerable<TNodeViewModel> rootNodes, CancellationToken cancellationToken = default);
    Task ExpandAllAsync(IEnumerable<TNodeViewModel> nodes, CancellationToken cancellationToken = default);
    Task CollapseAllAsync(IEnumerable<TNodeViewModel> nodes, CancellationToken cancellationToken = default);
    Task ExpandFirstLevelAsync(IEnumerable<TNodeViewModel> nodes, CancellationToken cancellationToken = default);
}

public class TreeExpansionManager<TNodeViewModel> : ITreeExpansionManager<TNodeViewModel>
    where TNodeViewModel : class, ITreeNodeViewModel
{
    private TreeExpansionMode _expansionMode = TreeExpansionMode.FullyExpanded;
    private const int BatchSize = 20;
    private CancellationTokenSource? _currentOperationCts;
    private static readonly Dictionary<Type, System.Reflection.PropertyInfo?> _propertyCache = new();

    public TreeExpansionMode ExpansionMode
    {
        get => _expansionMode;
        set => _expansionMode = value;
    }

    public async Task ApplyExpansionModeAsync(IEnumerable<TNodeViewModel> rootNodes, CancellationToken cancellationToken = default)
    {
        _currentOperationCts?.Cancel();
        _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            switch (_expansionMode)
            {
                case TreeExpansionMode.Collapsed:
                    await CollapseAllAsync(rootNodes, _currentOperationCts.Token);
                    break;
                case TreeExpansionMode.FirstLevelOnly:
                    await ExpandFirstLevelAsync(rootNodes, _currentOperationCts.Token);
                    break;
                case TreeExpansionMode.FullyExpanded:
                    await ExpandAllAsync(rootNodes, _currentOperationCts.Token);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, this is expected
        }
    }

    public async Task ExpandAllAsync(IEnumerable<TNodeViewModel> nodes, CancellationToken cancellationToken = default)
    {
        var nodesList = nodes.ToList();
        await ExpandNodesRecursivelyAsync(nodesList, cancellationToken);
    }

    public async Task CollapseAllAsync(IEnumerable<TNodeViewModel> nodes, CancellationToken cancellationToken = default)
    {
        var nodesList = nodes.ToList();
        await CollapseNodesRecursivelyAsync(nodesList, cancellationToken);
    }

    public async Task ExpandFirstLevelAsync(IEnumerable<TNodeViewModel> nodes, CancellationToken cancellationToken = default)
    {
        var nodesList = nodes.ToList();
        int processedCount = 0;

        foreach (var node in nodesList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            node.IsExpanded = true;

            if (node.HasChildren && node.ChildrenLoaded)
            {
                var children = GetChildrenFromNode(node);
                if (children != null)
                {
                    await CollapseAllAsync(children, cancellationToken);
                }
            }

            processedCount++;
            if (processedCount % BatchSize == 0)
            {
                await Task.Delay(1, cancellationToken);
            }
        }
    }

    private async Task ExpandNodesRecursivelyAsync(List<TNodeViewModel> nodes, CancellationToken cancellationToken)
    {
        var queue = new Queue<TNodeViewModel>(nodes);
        int processedCount = 0;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var node = queue.Dequeue();

            if (!node.IsExpanded)
            {
                node.IsExpanded = true;
            }

            if (node.HasChildren)
            {
                var children = GetChildrenFromNode(node);
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            processedCount++;
            if (processedCount % BatchSize == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Task.Delay(1, cancellationToken);
            }
        }
    }

    private async Task CollapseNodesRecursivelyAsync(List<TNodeViewModel> nodes, CancellationToken cancellationToken)
    {
        var stack = new Stack<TNodeViewModel>(nodes.Reverse<TNodeViewModel>());
        int processedCount = 0;

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var node = stack.Pop();

            if (node.HasChildren && node.ChildrenLoaded)
            {
                var children = GetChildrenFromNode(node);
                if (children != null)
                {
                    foreach (var child in children.Reverse())
                    {
                        stack.Push(child);
                    }
                }
            }

            node.IsExpanded = false;

            processedCount++;
            if (processedCount % BatchSize == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Task.Delay(1, cancellationToken);
            }
        }
    }

    private IEnumerable<TNodeViewModel>? GetChildrenFromNode(TNodeViewModel node)
    {
        var nodeType = node.GetType();

        if (!_propertyCache.TryGetValue(nodeType, out var childrenProperty))
        {
            childrenProperty = nodeType.GetProperty("Children");
            _propertyCache[nodeType] = childrenProperty;
        }

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
