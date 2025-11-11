using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using skt.IDE.Services;
using skt.IDE.Services.Buss;
using skt.Shared;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class SyntaxTreeViewModel : ObservableObject, IDisposable
{
    private ObservableCollection<AstNodeViewModel> _rootNodesInternal = new();
    private AstNodeViewModel? _programNode;
    private readonly TreeExpansionManager<AstNodeViewModel> _expansionManager = new();
    private CancellationTokenSource? _expansionCts;
    private readonly Services.ActiveEditorService? _activeEditorService;
    private ViewModels.TextEditorViewModel? _currentEditor;

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
            _ = ApplyExpansionModeAsync();
        }
    }

    public SyntaxTreeViewModel()
    {
        _expansionManager.ExpansionMode = TreeExpansionMode.FullyExpanded;
        _activeEditorService = App.Services?.GetService(typeof(Services.ActiveEditorService)) as Services.ActiveEditorService;
        InitializeTreeSource();

        App.Messenger.Register<ActiveEditorChangedEvent>(this, (_, e) => OnActiveEditorChanged(e));
        App.Messenger.Register<SyntaxAnalysisCompletedEvent>(this, (_, e) => OnSyntaxAnalysisCompleted(e));
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
        var filePathContext = _currentEditor?.FilePath;

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
                var programViewModel = new AstNodeViewModel(rootNode, 0, filePathContext);
                _programNode = programViewModel;
                _rootNodesInternal.Add(programViewModel);
            }
            else
            {
                _rootNodesInternal.Add(new AstNodeViewModel(rootNode, 0, filePathContext));
            }
        }

        var errorCount = errors?.Count ?? 0;
        StatusMessage = errorCount > 0
            ? $"Syntax tree generated with {errorCount} error(s)"
            : "Syntax tree generated successfully";

        if (isFirstLoad)
        {
            _ = Task.Delay(50).ContinueWith(async _ =>
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("First load - expanding all nodes");
                    await ExpandAllAsync();
                });
            });
        }
    }

    [RelayCommand]
    private async Task ExpandAllAsync()
    {
        if (_rootNodesInternal.Count == 0) return;

        _expansionCts?.Cancel();
        _expansionCts = new CancellationTokenSource();

        try
        {
            await _expansionManager.ExpandAllAsync(_rootNodesInternal, _expansionCts.Token);
            NotifyTreeDataGridToExpandAll?.Invoke();
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Expand all operation cancelled");
        }
    }

    [RelayCommand]
    private async Task CollapseAllAsync()
    {
        if (_rootNodesInternal.Count == 0) return;

        _expansionCts?.Cancel();
        _expansionCts = new CancellationTokenSource();

        try
        {
            await _expansionManager.CollapseAllAsync(_rootNodesInternal, _expansionCts.Token);
            NotifyTreeDataGridToCollapseAll?.Invoke();
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Collapse all operation cancelled");
        }
    }

    private async Task ApplyExpansionModeAsync()
    {
        if (_rootNodesInternal.Count == 0) return;

        _expansionCts?.Cancel();
        _expansionCts = new CancellationTokenSource();

        try
        {
            await _expansionManager.ApplyExpansionModeAsync(_rootNodesInternal, _expansionCts.Token);
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("Apply expansion mode cancelled");
        }
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
        _expansionCts?.Cancel();
        _expansionCts?.Dispose();
        TreeSource?.Dispose();
        TreeSource = null;
    }

    public void RefreshSource()
    {
        InitializeTreeSource();
    }

    public event Action? NotifyTreeDataGridToExpandAll;
    public event Action? NotifyTreeDataGridToCollapseAll;

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
            if (node.HasChildren)
            {
                // Recurse only when there are actual children in the collection
                if (node.Children.Count > 0)
                {
                    CollapseAllNodesRecursively(node.Children);
                }

                // Always collapse the node itself, even if its children were not yet materialized
                node.IsExpanded = false;
            }
        }
    }

    private void OnActiveEditorChanged(ActiveEditorChangedEvent e)
    {
        SaveCurrentEditorExpansionState();
        SaveCurrentEditorExpansionMode();
        _currentEditor = e.ActiveEditor;
        LoadCurrentEditorTree();
        LoadCurrentEditorExpansionMode();
    }

    private void SaveCurrentEditorExpansionState()
    {
        if (_currentEditor == null) return;

        _currentEditor.SyntaxTreeExpansionState.Clear();
        SaveExpansionState(_rootNodesInternal, _currentEditor.SyntaxTreeExpansionState);
    }

    private void SaveCurrentEditorExpansionMode()
    {
        if (_currentEditor == null) return;
        _currentEditor.SyntaxTreeExpansionMode = _expansionManager.ExpansionMode;
    }

    private void LoadCurrentEditorExpansionMode()
    {
        if (_currentEditor == null) return;
        _expansionManager.ExpansionMode = _currentEditor.SyntaxTreeExpansionMode;
        OnPropertyChanged(nameof(ExpansionMode));
    }

    private void SaveExpansionState(IEnumerable<AstNodeViewModel> nodes, Dictionary<string, bool> state)
    {
        foreach (var node in nodes)
        {
            if (node.HasChildren)
            {
                state[node.NodePath] = node.IsExpanded;
                if (node.Children.Count > 0)
                {
                    SaveExpansionState(node.Children, state);
                }
            }
        }
    }

    private void LoadCurrentEditorTree()
    {
        if (_currentEditor == null)
        {
            _rootNodesInternal.Clear();
            _programNode = null;
            StatusMessage = "No syntax tree to display. Open a file and compile to see syntax analysis.";
            return;
        }

        UpdateTree(_currentEditor.SyntaxTree, _currentEditor.SyntaxErrors.ToList());

        if (_currentEditor.SyntaxTreeExpansionState.Count > 0)
        {
            RestoreExpansionState(_rootNodesInternal, _currentEditor.SyntaxTreeExpansionState);
        }
    }

    private void RestoreExpansionState(IEnumerable<AstNodeViewModel> nodes, Dictionary<string, bool> state)
    {
        foreach (var node in nodes)
        {
            if (state.TryGetValue(node.NodePath, out var isExpanded))
            {
                node.IsExpanded = isExpanded;

                if (node.Children.Count > 0)
                {
                    RestoreExpansionState(node.Children, state);
                }
            }
        }
    }

    private void OnSelectedDocumentChanged(SelectedDocumentChangedEvent e)
    {
        // Deprecated - using ActiveEditorChangedEvent instead
    }

    private void OnSyntaxAnalysisCompleted(SyntaxAnalysisCompletedEvent e)
    {
        // Only update if this is for the currently active editor
        if (_currentEditor != null && string.Equals(_currentEditor.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            UpdateTree(e.Ast, e.Errors);
        }
    }
}

public partial class AstNodeViewModel : ObservableObject, ITreeNodeViewModel
{
    private AstNode _astNode;
    private string? _cachedNodePath;
    private int _indexInParent;
    private string? _fileContext;

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

    private readonly ObservableCollection<AstNodeViewModel> _children = new();

    public ObservableCollection<AstNodeViewModel> Children => _children;
    public bool ChildrenLoaded => true;
    public AstNode AstNode => _astNode;

    public AstNodeViewModel(AstNode astNode, int indexInParent = 0, string? fileContext = null)
    {
        _astNode = astNode;
        _indexInParent = indexInParent;
        _fileContext = fileContext;
        UpdateDisplayProperties();
        LoadChildren();
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
        _children.Clear();

        for (int i = 0; i < _astNode.Children.Count; i++)
        {
            _children.Add(new AstNodeViewModel(_astNode.Children[i], i, _fileContext));
        }
    }

    public void UpdateFromAstNode(AstNode newAstNode)
    {
        _astNode = newAstNode;
        _cachedNodePath = null;
        UpdateDisplayProperties();
        UpdateChildren(newAstNode.Children);
    }

    private void UpdateChildren(List<AstNode> newChildren)
    {
        var oldViewModels = new List<AstNodeViewModel>(_children);
        var oldExpansionStates = oldViewModels.ToDictionary(vm => vm.NodePath, vm => vm.IsExpanded);

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
                var newViewModel = new AstNodeViewModel(newChildAst, i, _fileContext);
                if (oldExpansionStates.TryGetValue(newViewModel.NodePath, out var isExpanded))
                {
                    newViewModel.IsExpanded = isExpanded;
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

    public bool IsTerminal => _astNode.Token != null;
    public bool HasChildren => _astNode.Children.Count > 0;

    public string StableId => NodePath;
    public string NodePath
    {
        get
        {
            if (_cachedNodePath == null)
            {
                _cachedNodePath = GeneratePathId();
            }
            return _cachedNodePath;
        }
    }

    private string GeneratePathId()
    {
        var filePrefix = string.IsNullOrEmpty(_fileContext) ? "" : $"{_fileContext}::";

        if (_astNode.Token != null && !string.IsNullOrEmpty(_astNode.Token.Value))
        {
            return $"{filePrefix}{_astNode.Token.Type}:{_astNode.Token.Value}[{_indexInParent}]@{_astNode.Line}:{_astNode.Column}";
        }
        return $"{filePrefix}{_astNode.Rule}[{_indexInParent}]@{_astNode.Line}:{_astNode.Column}";
    }
}
