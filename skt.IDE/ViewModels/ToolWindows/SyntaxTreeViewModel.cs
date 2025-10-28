using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.Shared;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class SyntaxTreeViewModel : ObservableObject
{
    private ObservableCollection<AstNodeViewModel> _rootNodesInternal = new();
    private Dictionary<string, bool> _expansionState = new();
    private AstNode? _lastRootNode;

    [ObservableProperty]
    private string _statusMessage = "No syntax tree to display. Open a file and compile to see syntax analysis.";

    [ObservableProperty]
    private HierarchicalTreeDataGridSource<AstNodeViewModel>? _treeSource;

    public SyntaxTreeViewModel()
    {
        InitializeTreeSource();
    }

    private void InitializeTreeSource()
    {
        TreeSource = new HierarchicalTreeDataGridSource<AstNodeViewModel>(_rootNodesInternal)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<AstNodeViewModel>(
                    new TextColumn<AstNodeViewModel, string>("Node", x => x.DisplayName),
                    x => x.Children,
                    x => x.HasChildren),
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
            _lastRootNode = null;
            _expansionState.Clear();
            StatusMessage = errors?.Count > 0
                ? $"Syntax analysis failed with {errors.Count} error(s)"
                : "No syntax tree to display";
            return;
        }

        // Save expansion state before update
        SaveExpansionState(_rootNodesInternal);

        // Check if we can do a smart update instead of full rebuild
        bool needsFullRebuild = _lastRootNode == null ||
                                _rootNodesInternal.Count == 0 ||
                                !AreSimilarTrees(_lastRootNode, rootNode);

        if (needsFullRebuild)
        {
            _rootNodesInternal.Clear();
            // Use an explicit path for the root so expansion state keys line up with saved keys
            var rootPath = "[0]";
            var viewModel = new AstNodeViewModel(rootNode, _expansionState, rootPath);
            _rootNodesInternal.Add(viewModel);
        }
        else
        {
            // Smart update: reuse existing nodes where possible
            if (_rootNodesInternal.Count > 0)
            {
                // Ensure we pass the same root path used when saving state
                _rootNodesInternal[0].UpdateFrom(rootNode, _expansionState, "[0]");
            }
        }

        _lastRootNode = rootNode;

        var errorCount = errors?.Count ?? 0;
        StatusMessage = errorCount > 0
            ? $"Syntax tree generated with {errorCount} error(s)"
            : "Syntax tree generated successfully";
    }

    private void SaveExpansionState(ObservableCollection<AstNodeViewModel> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var rootPath = $"[{i}]";
            SaveNodeExpansionState(node, rootPath);
        }
    }

    private void SaveNodeExpansionState(AstNodeViewModel node, string path)
    {
        _expansionState[path] = node.IsExpanded;

        if (node.ChildrenLoaded)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                var childPath = $"{path}[{i}]";
                SaveNodeExpansionState(child, childPath);
            }
        }
    }

    private bool AreSimilarTrees(AstNode node1, AstNode node2)
    {
        // Quick check: if root rules match and child counts match, trees are similar
        if (node1.Rule != node2.Rule)
            return false;

        var count1 = node1.Children?.Count ?? 0;
        var count2 = node2.Children?.Count ?? 0;

        return count1 == count2;
    }

    public void Clear()
    {
        _rootNodesInternal.Clear();
        _expansionState.Clear();
        _lastRootNode = null;
        StatusMessage = "No syntax tree to display";
    }
}

public partial class AstNodeViewModel : ObservableObject
{
    private readonly AstNode _node;
    private bool _childrenLoaded;
    private List<AstNode>? _childNodes;
    private Dictionary<string, bool>? _expansionState;
    private string _nodePath;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _tokenType = "";

    [ObservableProperty]
    private int _line;

    [ObservableProperty]
    private int _column;

    [ObservableProperty]
    private bool _isExpanded;

    private ObservableCollection<AstNodeViewModel>? _children;

    public ObservableCollection<AstNodeViewModel> Children
    {
        get
        {
            if (_children == null)
            {
                _children = new ObservableCollection<AstNodeViewModel>();
                LoadChildrenIfNeeded();
            }
            return _children;
        }
    }

    public bool ChildrenLoaded => _childrenLoaded;

    public AstNodeViewModel(AstNode node, Dictionary<string, bool>? expansionState = null, string parentPath = "")
    {
        _node = node;
        _expansionState = expansionState;

        // If there's a token with a lexeme, show just the lexeme
        // Otherwise show the rule name
        if (node.Token != null && !string.IsNullOrEmpty(node.Token.Value))
        {
            _displayName = node.Token.Value;
            TokenType = node.Token.Type.ToString();
            Line = node.Token.Line;
            Column = node.Token.Column;
        }
        else
        {
            _displayName = node.Rule;
            Line = node.Line;
            Column = node.Column;
        }

        // Cache child nodes but don't create ViewModels yet (lazy loading)
        _childNodes = node.Children?.Count > 0 ? node.Children : null;

        _nodePath = parentPath;

        if (expansionState != null && expansionState.TryGetValue(_nodePath, out bool wasExpanded))
        {
            _isExpanded = wasExpanded;
        }
    }

    private void LoadChildrenIfNeeded()
    {
        if (_childrenLoaded || _childNodes == null || _children == null)
            return;

        _childrenLoaded = true;
        for (int i = 0; i < _childNodes.Count; i++)
        {
            var childNode = _childNodes[i];
            var childPath = $"{_nodePath}[{i}]";
            _children.Add(new AstNodeViewModel(childNode, _expansionState, childPath));
        }
    }

    public void UpdateFrom(AstNode newNode, Dictionary<string, bool> expansionState, string parentPath = "")
    {
        _expansionState = expansionState;

        // Update basic properties
        if (newNode.Token != null && !string.IsNullOrEmpty(newNode.Token.Value))
        {
            DisplayName = newNode.Token.Value;
            TokenType = newNode.Token.Type.ToString();
            Line = newNode.Token.Line;
            Column = newNode.Token.Column;
        }
        else
        {
            DisplayName = newNode.Rule;
            Line = newNode.Line;
            Column = newNode.Column;
        }

        // Update child nodes cache
        _childNodes = newNode.Children?.Count > 0 ? newNode.Children : null;

        _nodePath = parentPath;

        if (expansionState.TryGetValue(_nodePath, out bool wasExpanded))
        {
            IsExpanded = wasExpanded;
        }

        // If children were already loaded, update them
        if (_childrenLoaded && _children != null)
        {
            var newChildCount = _childNodes?.Count ?? 0;
            var oldChildCount = _children.Count;

            // Reuse existing children where possible
            for (int i = 0; i < Math.Min(newChildCount, oldChildCount); i++)
            {
                var childPath = $"{_nodePath}[{i}]";
                _children[i].UpdateFrom(_childNodes![i], expansionState, childPath);
            }

            // Add new children if needed
            if (newChildCount > oldChildCount)
            {
                for (int i = oldChildCount; i < newChildCount; i++)
                {
                    var childPath = $"{_nodePath}[{i}]";
                    _children.Add(new AstNodeViewModel(_childNodes![i], expansionState, childPath));
                }
            }
            // Remove extra children if needed
            else if (newChildCount < oldChildCount)
            {
                for (int i = oldChildCount - 1; i >= newChildCount; i--)
                {
                    _children.RemoveAt(i);
                }
            }
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_childrenLoaded)
        {
            LoadChildrenIfNeeded();
        }
    }

    public bool IsTerminal => _node.Token != null;
    public bool HasChildren => _childNodes != null && _childNodes.Count > 0;
}
