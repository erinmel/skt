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
using skt.IDE.Services.Buss;
using skt.Shared;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class SemanticTreeViewModel : ObservableObject, IDisposable
{
    private ObservableCollection<AnnotatedAstNodeViewModel> _rootNodesInternal = new();
    private AnnotatedAstNodeViewModel? _programNode;
    private readonly TreeExpansionManager<AnnotatedAstNodeViewModel> _expansionManager = new();
    private CancellationTokenSource? _expansionCts;
    private readonly Services.ActiveEditorService? _activeEditorService;
    private ViewModels.TextEditorViewModel? _currentEditor;

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
            _ = ApplyExpansionModeAsync();
        }
    }

    public SemanticTreeViewModel()
    {
        _expansionManager.ExpansionMode = TreeExpansionMode.FullyExpanded;
        _activeEditorService = App.Services?.GetService(typeof(Services.ActiveEditorService)) as Services.ActiveEditorService;
        InitializeTreeSource();

        App.Messenger.Register<ActiveEditorChangedEvent>(this, (_, e) => OnActiveEditorChanged(e));
        App.Messenger.Register<SemanticAnalysisCompletedEvent>(this, (_, e) => OnSemanticAnalysisCompleted(e));
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

        _currentEditor.SemanticTreeExpansionState.Clear();
        SaveExpansionState(_rootNodesInternal, _currentEditor.SemanticTreeExpansionState);
    }

    private void SaveCurrentEditorExpansionMode()
    {
        if (_currentEditor == null) return;
        _currentEditor.SemanticTreeExpansionMode = _expansionManager.ExpansionMode;
    }

    private void LoadCurrentEditorExpansionMode()
    {
        if (_currentEditor == null) return;
        _expansionManager.ExpansionMode = _currentEditor.SemanticTreeExpansionMode;
        OnPropertyChanged(nameof(ExpansionMode));
    }

    private void SaveExpansionState(IEnumerable<AnnotatedAstNodeViewModel> nodes, Dictionary<string, bool> state)
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
            StatusMessage = "No semantic tree to display. Open a file and compile to see semantic analysis.";
            return;
        }

        UpdateTree(_currentEditor.SemanticTree, _currentEditor.SemanticErrors.ToList());

        if (_currentEditor.SemanticTreeExpansionState.Count > 0)
        {
            RestoreExpansionState(_rootNodesInternal, _currentEditor.SemanticTreeExpansionState);
        }
    }

    private void RestoreExpansionState(IEnumerable<AnnotatedAstNodeViewModel> nodes, Dictionary<string, bool> state)
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

    private void OnSemanticAnalysisCompleted(SemanticAnalysisCompletedEvent e)
    {
        // Only update if this is for the currently active editor
        if (_currentEditor != null && string.Equals(_currentEditor.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            UpdateTree(e.AnnotatedAst, e.Errors);
        }
    }

    private void SaveExpansionState(string filePath)
    {
        var state = _activeEditorService?.ActiveEditor?.SemanticTreeExpansionState;
        if (state == null) return;

        state.Clear();
        SaveExpansionStateRecursive(_rootNodesInternal, state);
    }

    private void SaveExpansionStateRecursive(IEnumerable<AnnotatedAstNodeViewModel> nodes, Dictionary<string, bool> expansionState)
    {
        foreach (var node in nodes)
        {
            if (node.HasChildren)
            {
                expansionState[node.NodePath] = node.IsExpanded;

                if (node.Children.Count > 0)
                {
                    SaveExpansionStateRecursive(node.Children, expansionState);
                }
            }
        }
    }

    private void RestoreExpansionState(string filePath)
    {
        var state = _activeEditorService?.ActiveEditor?.SemanticTreeExpansionState;
        if (state == null || state.Count == 0) return;

        RestoreExpansionStateRecursive(_rootNodesInternal, state);
    }

    private void RestoreExpansionStateRecursive(IEnumerable<AnnotatedAstNodeViewModel> nodes, Dictionary<string, bool> expansionState)
    {
        foreach (var node in nodes)
        {
            if (expansionState.TryGetValue(node.NodePath, out var isExpanded))
            {
                node.IsExpanded = isExpanded;

                if (isExpanded && node.HasChildren && node.ChildrenLoaded && node.Children.Count > 0)
                {
                    RestoreExpansionStateRecursive(node.Children, expansionState);
                }
            }
        }
    }

    private void InitializeTreeSource()
    {
        TreeSource?.Dispose();

        TreeSource = new HierarchicalTreeDataGridSource<AnnotatedAstNodeViewModel>(_rootNodesInternal)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<AnnotatedAstNodeViewModel>(
                    new TextColumn<AnnotatedAstNodeViewModel, string>("Token/Lexeme", x => x.DisplayName),
                    x => x.Children,
                    x => x.HasChildren,
                    x => x.IsExpanded),
                new TextColumn<AnnotatedAstNodeViewModel, string>("Value", x => x.ValueValue),
                new TextColumn<AnnotatedAstNodeViewModel, string>("Type", x => x.TypeValue),
                new TextColumn<AnnotatedAstNodeViewModel, int>("Line", x => x.Line),
                new TextColumn<AnnotatedAstNodeViewModel, int>("Column", x => x.Column)
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
        var filePathContext = _currentEditor?.FilePath;

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
                var programViewModel = new AnnotatedAstNodeViewModel(rootNode, 0, filePathContext);
                _programNode = programViewModel;
                _rootNodesInternal.Add(programViewModel);
            }
            else
            {
                _rootNodesInternal.Add(new AnnotatedAstNodeViewModel(rootNode, 0, filePathContext));
            }
        }

        var errorCount = errors?.Count ?? 0;
        StatusMessage = errorCount > 0
            ? $"Semantic tree generated with {errorCount} error(s)"
            : "Semantic tree generated successfully";

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
    private string? _cachedNodePath;
    private int _indexInParent;
    private string? _fileContext;

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

    private readonly ObservableCollection<AnnotatedAstNodeViewModel> _children = new();

    public ObservableCollection<AnnotatedAstNodeViewModel> Children => _children;
    public bool ChildrenLoaded => true;
    public AnnotatedAstNode AnnotatedNode => _annotatedNode;

    public AnnotatedAstNodeViewModel(AnnotatedAstNode annotatedNode, int indexInParent = 0, string? fileContext = null)
    {
        _annotatedNode = annotatedNode;
        _indexInParent = indexInParent;
        _fileContext = fileContext;
        UpdateDisplayProperties();
        LoadChildren();
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

    private void LoadChildren()
    {
        _children.Clear();

        for (int i = 0; i < _annotatedNode.Children.Count; i++)
        {
            _children.Add(new AnnotatedAstNodeViewModel(_annotatedNode.Children[i], i, _fileContext));
        }
    }

    public void UpdateFromAnnotatedNode(AnnotatedAstNode newNode)
    {
        _annotatedNode = newNode;
        _cachedNodePath = null;
        UpdateDisplayProperties();
        UpdateChildren(newNode.Children);
    }

    private void UpdateChildren(List<AnnotatedAstNode> newChildren)
    {
        var oldViewModels = new List<AnnotatedAstNodeViewModel>(_children);
        var oldExpansionStates = oldViewModels.ToDictionary(vm => vm.NodePath, vm => vm.IsExpanded);

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
                var newViewModel = new AnnotatedAstNodeViewModel(newChildNode, i, _fileContext);
                if (oldExpansionStates.TryGetValue(newViewModel.NodePath, out var isExpanded))
                {
                    newViewModel.IsExpanded = isExpanded;
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

    public bool IsTerminal => _annotatedNode.Token != null;
    public bool HasChildren => _annotatedNode.Children.Count > 0;

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

        if (_annotatedNode.Token != null && !string.IsNullOrEmpty(_annotatedNode.Token.Value))
        {
            return $"{filePrefix}{_annotatedNode.Token.Type}:{_annotatedNode.Token.Value}[{_indexInParent}]@{_annotatedNode.Line}:{_annotatedNode.Column}";
        }
        return $"{filePrefix}{_annotatedNode.Rule}[{_indexInParent}]@{_annotatedNode.Line}:{_annotatedNode.Column}";
    }
}
