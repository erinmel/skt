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

public partial class SyntaxTreeViewModel : ObservableObject, IDisposable
{
    private ObservableCollection<AstNodeViewModel> _rootNodesInternal = new();
    private AstNodeViewModel? _programNode;
    private readonly TreeExpansionManager<AstNodeViewModel> _expansionManager = new();

    [ObservableProperty]
    private string _statusMessage = "No syntax tree to display. Open a file and compile to see syntax analysis.";

    [ObservableProperty]
    private HierarchicalTreeDataGridSource<AstNodeViewModel>? _treeSource;

    public ObservableCollection<AstNodeViewModel> RootNodes => _rootNodesInternal;

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

    public SyntaxTreeViewModel()
    {
        _expansionManager.ExpansionMode = TreeExpansionMode.FullyExpanded;
        InitializeTreeSource();
    }

    private void InitializeTreeSource()
    {
        TreeSource?.Dispose();

        TreeSource = new HierarchicalTreeDataGridSource<AstNodeViewModel>(_rootNodesInternal)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<AstNodeViewModel>(
                    new TextColumn<AstNodeViewModel, string>("Node", x => x.DisplayName),
                    x => x.Children,
                    x => x.HasChildren,
                    x => x.IsExpanded),
                new TextColumn<AstNodeViewModel, string>("Token Type", x => x.TokenType),
                new TextColumn<AstNodeViewModel, int>("Ln", x => x.Line),
                new TextColumn<AstNodeViewModel, int>("Col", x => x.Column)
            }
        };
    }

    public void UpdateTree(AstNode? rootNode, List<ParseError>? errors)
    {
        if (rootNode == null)
        {
            _rootNodesInternal.Clear();
            _programNode = null;
            StatusMessage = errors?.Count > 0
                ? $"Syntax analysis failed with {errors.Count} error(s)"
                : "No syntax tree to display";
            return;
        }

        var isFirstLoad = _programNode == null;

        if (_programNode != null && rootNode.Rule == "program")
        {
            _programNode.UpdateFromAstNode(rootNode);
        }
        else
        {
            _rootNodesInternal.Clear();
            _programNode = null;

            if (rootNode.Rule == "program")
            {
                var programViewModel = new AstNodeViewModel(rootNode);
                _programNode = programViewModel;
                _rootNodesInternal.Add(programViewModel);
            }
            else
            {
                _rootNodesInternal.Add(new AstNodeViewModel(rootNode));
            }
        }

        var errorCount = errors?.Count ?? 0;
        StatusMessage = errorCount > 0
            ? $"Syntax tree generated with {errorCount} error(s)"
            : "Syntax tree generated successfully";

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
        StatusMessage = "No syntax tree to display";
    }

    public void Dispose()
    {
        TreeSource?.Dispose();
        TreeSource = null;
    }

    public event Action? NotifyTreeDataGridToExpandAll;

    private void ExpandAllNodesRecursively(IEnumerable<AstNodeViewModel> nodes)
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

    private void CollapseAllNodesRecursively(IEnumerable<AstNodeViewModel> nodes)
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

public partial class AstNodeViewModel : ObservableObject, ITreeNodeViewModel
{
    private AstNode _astNode;
    private bool _childrenLoaded;

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _tokenType = "";

    [ObservableProperty]
    private int _line;

    [ObservableProperty]
    private int _column;

    [ObservableProperty]
    private bool _isExpanded;

    private ObservableCollection<AstNodeViewModel> _children = new();

    public ObservableCollection<AstNodeViewModel> Children
    {
        get
        {
            // Children are now eagerly loaded in constructor
            return _children;
        }
    }

    public bool ChildrenLoaded => _childrenLoaded;
    public AstNode AstNode => _astNode;

    public AstNodeViewModel(AstNode astNode)
    {
        _astNode = astNode;
        UpdateDisplayProperties();

        // Eagerly load children for tree structure
        if (_astNode.Children.Count > 0)
        {
            LoadChildren();
        }
    }

    private void UpdateDisplayProperties()
    {
        if (_astNode.Token != null && !string.IsNullOrEmpty(_astNode.Token.Value))
        {
            DisplayName = _astNode.Token.Value;
            TokenType = _astNode.Token.Type.ToString();
            Line = _astNode.Token.Line;
            Column = _astNode.Token.Column;
        }
        else
        {
            DisplayName = _astNode.Rule;
            TokenType = "";
            Line = _astNode.Line;
            Column = _astNode.Column;
        }
    }

    private void LoadChildren()
    {
        _childrenLoaded = true;
        _children.Clear();

        foreach (var childAstNode in _astNode.Children)
        {
            _children.Add(new AstNodeViewModel(childAstNode));
        }
    }

    public void UpdateFromAstNode(AstNode newAstNode)
    {
        var wasExpanded = IsExpanded;
        _astNode = newAstNode;
        UpdateDisplayProperties();

        if (_childrenLoaded)
        {
            UpdateChildren(newAstNode.Children, wasExpanded);
        }
        else if (wasExpanded && HasChildren)
        {
            LoadChildren();
            IsExpanded = true;
        }
    }

    private void UpdateChildren(List<AstNode> newChildren, bool preserveExpansion = true)
    {
        var oldViewModels = new List<AstNodeViewModel>(_children);
        var oldExpansionStates = preserveExpansion
            ? oldViewModels.ToDictionary(vm => vm.GeneratePathId(), vm => vm.IsExpanded)
            : new Dictionary<string, bool>();

        _children.Clear();

        for (int i = 0; i < newChildren.Count; i++)
        {
            var newChildAst = newChildren[i];

            if (i < oldViewModels.Count && AreNodesCompatible(oldViewModels[i].AstNode, newChildAst))
            {
                var existingViewModel = oldViewModels[i];
                existingViewModel.UpdateFromAstNode(newChildAst);
                _children.Add(existingViewModel);
            }
            else
            {
                var newViewModel = new AstNodeViewModel(newChildAst);
                if (preserveExpansion && oldExpansionStates.TryGetValue(newViewModel.GeneratePathId(), out var wasExpanded))
                {
                    newViewModel.IsExpanded = wasExpanded;
                }
                _children.Add(newViewModel);
            }
        }
    }

    private bool AreNodesCompatible(AstNode oldNode, AstNode newNode)
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

    public bool IsTerminal => _astNode.Token != null;
    public bool HasChildren => _astNode.Children.Count > 0;

    public string StableId => GeneratePathId();
    public string NodePath => GeneratePathId();

    private string GeneratePathId()
    {
        if (_astNode.Token != null && !string.IsNullOrEmpty(_astNode.Token.Value))
        {
            return $"{_astNode.Token.Type}:{_astNode.Token.Value}@{_astNode.Line}:{_astNode.Column}";
        }
        return $"{_astNode.Rule}@{_astNode.Line}:{_astNode.Column}";
    }
}
