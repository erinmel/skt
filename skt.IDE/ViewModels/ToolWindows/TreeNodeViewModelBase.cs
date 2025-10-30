using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace skt.IDE.ViewModels.ToolWindows;

public interface ITreeNodeViewModel
{
    string StableId { get; }
    string NodePath { get; }
    bool IsExpanded { get; set; }
    bool HasChildren { get; }
    bool ChildrenLoaded { get; }
}

public abstract partial class TreeNodeViewModelBase<TNode> : ObservableObject, ITreeNodeViewModel
{
    protected TNode _node;
    protected bool _childrenLoaded;

    [ObservableProperty]
    private bool _isExpanded;

    public abstract ObservableCollection<TreeNodeViewModelBase<TNode>> Children { get; }
    public bool ChildrenLoaded => _childrenLoaded;
    public abstract bool HasChildren { get; }
    public abstract string StableId { get; }
    public abstract string NodePath { get; }

    protected TreeNodeViewModelBase(TNode node)
    {
        _node = node;
    }

    protected abstract void LoadChildren();
    protected abstract void UpdateDisplayProperties();

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_childrenLoaded && HasChildren)
        {
            LoadChildren();
        }
    }
}
