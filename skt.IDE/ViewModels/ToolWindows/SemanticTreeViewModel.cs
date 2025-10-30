using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using skt.Shared;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class SemanticTreeViewModel : ObservableObject, IDisposable
{
    private ObservableCollection<AnnotatedAstNodeViewModel> _rootNodesInternal = new();
    private AnnotatedAstNodeViewModel? _programNode;
    private readonly TreeExpansionManager<AnnotatedAstNodeViewModel> _expansionManager = new();

    [ObservableProperty]
    private string _statusMessage = "No semantic tree to display. Open a file and compile to see semantic analysis.";

    [ObservableProperty]
    private HierarchicalTreeDataGridSource<AnnotatedAstNodeViewModel>? _treeSource;

    public ObservableCollection<AnnotatedAstNodeViewModel> RootNodes => _rootNodesInternal;

    public Func<(string? selectedPath, double verticalOffset)>? RequestVisualState { get; set; }

    public TreeExpansionMode ExpansionMode
    {
        get => _expansionManager.ExpansionMode;
        set
        {
            _expansionManager.ExpansionMode = value;
            _expansionManager.ApplyExpansionMode(_rootNodesInternal);
        }
    }

    public SemanticTreeViewModel()
    {
        _expansionManager.ExpansionMode = TreeExpansionMode.FullyExpanded;
        InitializeTreeSource();
    }

    private void InitializeTreeSource()
    {
        TreeSource?.Dispose();

        TreeSource = new HierarchicalTreeDataGridSource<AnnotatedAstNodeViewModel>(_rootNodesInternal)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<AnnotatedAstNodeViewModel>(
                    new TextColumn<AnnotatedAstNodeViewModel, string>("Rule/Token", x => x.DisplayName),
                    x => x.Children,
                    x => x.HasChildren,
                    x => x.IsExpanded),
                new TextColumn<AnnotatedAstNodeViewModel, string>("Type", x => x.TypeValue),
                new TextColumn<AnnotatedAstNodeViewModel, string>("Type Prop", x => x.TypePropagation),
                new TextColumn<AnnotatedAstNodeViewModel, string>("Value", x => x.ValueValue),
                new TextColumn<AnnotatedAstNodeViewModel, string>("Val Prop", x => x.ValuePropagation),
                new TextColumn<AnnotatedAstNodeViewModel, int>("Line", x => x.Line),
                new TextColumn<AnnotatedAstNodeViewModel, int>("Col", x => x.Column)
            }
        };
    }

    public void UpdateTree(AnnotatedAstNode? rootNode, List<SemanticError>? errors)
    {
        if (rootNode == null)
        {
            _rootNodesInternal.Clear();
            _programNode = null;
            StatusMessage = errors?.Count > 0
                ? $"Semantic analysis failed with {errors.Count} error(s)"
                : "No semantic tree to display";
            return;
        }

        var isFirstLoad = _programNode == null;

        if (_programNode != null && rootNode.Rule == "program")
        {
            _programNode.UpdateFromAnnotatedNode(rootNode);
        }
        else
        {
            _rootNodesInternal.Clear();
            _programNode = null;

            if (rootNode.Rule == "program")
            {
                var programViewModel = new AnnotatedAstNodeViewModel(rootNode);
                _programNode = programViewModel;
                _rootNodesInternal.Add(programViewModel);
            }
            else
            {
                _rootNodesInternal.Add(new AnnotatedAstNodeViewModel(rootNode));
            }
        }

        var errorCount = errors?.Count ?? 0;
        StatusMessage = errorCount > 0
            ? $"Semantic tree generated with {errorCount} error(s)"
            : "Semantic tree generated successfully";

        if (isFirstLoad)
        {
            // On first load, expand all nodes after a brief delay for UI to settle
            System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    System.Diagnostics.Debug.WriteLine("First load - expanding all nodes");
                    NotifyTreeDataGridToExpandAll?.Invoke();
                });
            });
        }
    }

    [RelayCommand]
    public void ExpandAll()
    {
        if (_rootNodesInternal.Count == 0) return;
        ExpandAllNodesRecursively(_rootNodesInternal);
    }

    [RelayCommand]
    public void CollapseAll()
    {
        if (_rootNodesInternal.Count == 0) return;
        CollapseAllNodesRecursively(_rootNodesInternal);
        NotifyTreeDataGridToCollapseAll?.Invoke();
    }

    [RelayCommand]
    public void ClearTree()
    {
        Clear();
    }

    public void Clear()
    {
        _rootNodesInternal.Clear();
        _programNode = null;
        StatusMessage = "No semantic tree to display";
    }

    public void Dispose()
    {
        TreeSource?.Dispose();
        TreeSource = null;
    }

    public event Action? NotifyTreeDataGridToExpandAll;
    public event Action? NotifyTreeDataGridToCollapseAll;

    private void ExpandAllNodesRecursively(IEnumerable<AnnotatedAstNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.HasChildren)
            {
                node.IsExpanded = true;
                if (node.Children.Count > 0)
                {
                    ExpandAllNodesRecursively(node.Children);
                }
            }
        }
    }

    private void CollapseAllNodesRecursively(IEnumerable<AnnotatedAstNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.HasChildren && node.Children.Count > 0)
            {
                CollapseAllNodesRecursively(node.Children);
                node.IsExpanded = false;
            }
        }
    }
}

public partial class AnnotatedAstNodeViewModel : ObservableObject, ITreeNodeViewModel
{
    private AnnotatedAstNode _annotatedNode;
    private bool _childrenLoaded;

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _dataType = "";

    [ObservableProperty]
    private string _scope = "";

    [ObservableProperty]
    private string _value = "";

    [ObservableProperty]
    private string _isConstant = "";

    [ObservableProperty]
    private int _line;

    [ObservableProperty]
    private int _column;

    [ObservableProperty]
    private bool _isExpanded;

    private ObservableCollection<AnnotatedAstNodeViewModel> _children = new();

    public ObservableCollection<AnnotatedAstNodeViewModel> Children
    {
        get
        {
            // Children are now eagerly loaded in constructor
            return _children;
        }
    }

    public bool ChildrenLoaded => _childrenLoaded;
    public AnnotatedAstNode AnnotatedNode => _annotatedNode;

    public AnnotatedAstNodeViewModel(AnnotatedAstNode annotatedNode)
    {
        _annotatedNode = annotatedNode;
        UpdateDisplayProperties();

        // Eagerly load children for tree structure
        if (_annotatedNode.Children.Count > 0)
        {
            LoadChildren();
        }
    }

    private void UpdateDisplayProperties()
    {
        if (_annotatedNode.Token != null && !string.IsNullOrEmpty(_annotatedNode.Token.Value))
        {
            DisplayName = _annotatedNode.Token.Value;
            Line = _annotatedNode.Token.Line;
            Column = _annotatedNode.Token.Column;
        }
        else
        {
            DisplayName = _annotatedNode.Rule;
            Line = _annotatedNode.Line;
            Column = _annotatedNode.Column;
        }

        DataType = _annotatedNode.DataType ?? "";
        Scope = _annotatedNode.Scope ?? "";
        Value = ValueFormatter.FormatValue(_annotatedNode.Value);
        IsConstant = _annotatedNode.IsConstant ? "Yes" : "No";
    }

    public string TypeValue
    {
        get
        {
            if (string.IsNullOrEmpty(_annotatedNode.DataType))
                return "";

            return _annotatedNode.DataType;
        }
    }

    public string ValueValue
    {
        get
        {
            return ValueFormatter.FormatValue(_annotatedNode.Value);
        }
    }

    public string TypePropagation
    {
        get
        {
            var propagation = _annotatedNode.TypeAttribute.Propagation;
            var source = _annotatedNode.TypeAttribute.SourceNode;

            if (propagation == AttributePropagation.None)
                return "";

            var result = FormatPropagationWithSymbol(propagation);
            if (!string.IsNullOrEmpty(source))
                result += $" {source}";

            return result;
        }
    }

    public string ValuePropagation
    {
        get
        {
            var propagation = _annotatedNode.ValueAttribute.Propagation;
            var source = _annotatedNode.ValueAttribute.SourceNode;

            if (propagation == AttributePropagation.None)
                return "";

            var result = FormatPropagationWithSymbol(propagation);
            if (!string.IsNullOrEmpty(source))
                result += $" {source}";

            return result;
        }
    }

    private static string FormatPropagationWithSymbol(AttributePropagation propagation)
    {
        return propagation switch
        {
            AttributePropagation.Synthesized => "⭡ Synth",
            AttributePropagation.Inherited => "⭣ Inher",
            AttributePropagation.Sibling => "⭤ Sibl",
            _ => ""
        };
    }

    private static string FormatPropagation(AttributePropagation propagation)
    {
        return propagation switch
        {
            AttributePropagation.Synthesized => "↑",
            AttributePropagation.Inherited => "↓",
            AttributePropagation.Sibling => "↔",
            _ => ""
        };
    }

    private void LoadChildren()
    {
        _childrenLoaded = true;
        _children.Clear();

        foreach (var childNode in _annotatedNode.Children)
        {
            _children.Add(new AnnotatedAstNodeViewModel(childNode));
        }
    }

    public void UpdateFromAnnotatedNode(AnnotatedAstNode newNode)
    {
        var wasExpanded = IsExpanded;
        _annotatedNode = newNode;
        UpdateDisplayProperties();

        if (_childrenLoaded)
        {
            UpdateChildren(newNode.Children, wasExpanded);
        }
        else if (wasExpanded && HasChildren)
        {
            LoadChildren();
            IsExpanded = true;
        }
    }

    private void UpdateChildren(List<AnnotatedAstNode> newChildren, bool preserveExpansion = true)
    {
        var oldViewModels = new List<AnnotatedAstNodeViewModel>(_children);
        var oldExpansionStates = preserveExpansion
            ? oldViewModels.ToDictionary(vm => vm.GeneratePathId(), vm => vm.IsExpanded)
            : new Dictionary<string, bool>();

        _children.Clear();

        for (int i = 0; i < newChildren.Count; i++)
        {
            var newChildNode = newChildren[i];

            if (i < oldViewModels.Count && AreNodesCompatible(oldViewModels[i].AnnotatedNode, newChildNode))
            {
                var existingViewModel = oldViewModels[i];
                existingViewModel.UpdateFromAnnotatedNode(newChildNode);
                _children.Add(existingViewModel);
            }
            else
            {
                var newViewModel = new AnnotatedAstNodeViewModel(newChildNode);
                if (preserveExpansion && oldExpansionStates.TryGetValue(newViewModel.GeneratePathId(), out var wasExpanded))
                {
                    newViewModel.IsExpanded = wasExpanded;
                }
                _children.Add(newViewModel);
            }
        }
    }

    private bool AreNodesCompatible(AnnotatedAstNode oldNode, AnnotatedAstNode newNode)
    {
        if (oldNode.Rule != newNode.Rule)
            return false;

        if (oldNode.Token != null && newNode.Token != null)
        {
            return oldNode.Token.Type == newNode.Token.Type;
        }

        return oldNode.Token == null && newNode.Token == null;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        // Children are now eagerly loaded, no need for lazy loading
    }

    public bool IsTerminal => _annotatedNode.Token != null;
    public bool HasChildren => _annotatedNode.Children.Count > 0;

    public string StableId => GeneratePathId();
    public string NodePath => GeneratePathId();

    private string GeneratePathId()
    {
        if (_annotatedNode.Token != null && !string.IsNullOrEmpty(_annotatedNode.Token.Value))
        {
            return $"{_annotatedNode.Token.Type}:{_annotatedNode.Token.Value}@{_annotatedNode.Line}:{_annotatedNode.Column}";
        }
        return $"{_annotatedNode.Rule}@{_annotatedNode.Line}:{_annotatedNode.Column}";
    }
}
