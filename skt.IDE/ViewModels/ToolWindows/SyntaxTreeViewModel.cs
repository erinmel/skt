using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.Shared;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class SyntaxTreeViewModel : ObservableObject
{
    private ObservableCollection<AstNodeViewModel> _rootNodesInternal = new();

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
        _rootNodesInternal.Clear();

        if (rootNode == null)
        {
            StatusMessage = errors?.Count > 0
                ? $"Syntax analysis failed with {errors.Count} error(s)"
                : "No syntax tree to display";
            return;
        }

        var viewModel = new AstNodeViewModel(rootNode);
        _rootNodesInternal.Add(viewModel);

        var errorCount = errors?.Count ?? 0;
        StatusMessage = errorCount > 0
            ? $"Syntax tree generated with {errorCount} error(s)"
            : "Syntax tree generated successfully";
    }

    public void Clear()
    {
        _rootNodesInternal.Clear();
        StatusMessage = "No syntax tree to display";
    }
}

public partial class AstNodeViewModel : ObservableObject
{
    private readonly AstNode _node;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _tokenType = "";

    [ObservableProperty]
    private string _lexeme = "";

    [ObservableProperty]
    private int _line;

    [ObservableProperty]
    private int _column;

    [ObservableProperty]
    private ObservableCollection<AstNodeViewModel> _children = new();

    public AstNodeViewModel(AstNode node)
    {
        _node = node;

        // If there's a token with a lexeme, show just the lexeme
        // Otherwise show the rule name
        if (node.Token != null && !string.IsNullOrEmpty(node.Token.Value))
        {
            _displayName = node.Token.Value;
            _lexeme = node.Token.Value;
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

        if (node.Children != null && node.Children.Count > 0)
        {
            foreach (var child in node.Children)
            {
                Children.Add(new AstNodeViewModel(child));
            }
        }
    }

    public bool IsTerminal => _node.Token != null;
    public bool HasChildren => Children.Count > 0;
}
