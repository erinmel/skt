using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Messaging;
using skt.Compiler;
using skt.IDE.Services.Buss;
using skt.IDE.ViewModels.ToolWindows;
using skt.Shared;

namespace skt.IDE.ViewModels;

public class TextEditorViewModel : INotifyPropertyChanged
{
    private string _content = "";
    private string? _filePath;
    private bool _isDirty;
    private bool _isSelected;
    private int _caretPosition;
    private double _scrollPosition;

    public string? FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public string FileName => string.IsNullOrEmpty(FilePath) ? "Untitled" : Path.GetFileName(FilePath);

    public string DisplayTitle => _isDirty ? $"{FileName}*" : FileName;

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
            {
                IsDirty = true;
                RequestAnalysis();
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public int CaretPosition
    {
        get => _caretPosition;
        set => SetProperty(ref _caretPosition, value);
    }

    public double ScrollPosition
    {
        get => _scrollPosition;
        set => SetProperty(ref _scrollPosition, value);
    }

    public List<Token> Tokens { get; } = new();
    public List<ErrorToken> LexicalErrors { get; } = new();

    public AstNode? SyntaxTree { get; private set; }
    public List<ParseError> SyntaxErrors { get; } = new();

    public AnnotatedAstNode? SemanticTree { get; private set; }
    public SymbolTable SymbolTable { get; } = new();
    public List<SemanticError> SemanticErrors { get; } = new();

    public Dictionary<string, bool> SyntaxTreeExpansionState { get; } = new();
    public Dictionary<string, bool> SemanticTreeExpansionState { get; } = new();

    public TreeExpansionMode SyntaxTreeExpansionMode { get; set; } = TreeExpansionMode.FullyExpanded;
    public TreeExpansionMode SemanticTreeExpansionMode { get; set; } = TreeExpansionMode.FullyExpanded;

    public DateTime LastAnalyzed { get; private set; } = DateTime.MinValue;

    public bool HasLexicalAnalysis => Tokens.Count > 0 || LexicalErrors.Count > 0;
    public bool HasSyntaxAnalysis => SyntaxTree != null;
    public bool HasSemanticAnalysis => SemanticTree != null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TextEditorViewModel(string? filePath = null, string content = "")
    {
        _filePath = filePath;
        _content = content;
        _isDirty = string.IsNullOrEmpty(filePath);
    }

    public void SetContentFromFile(string content)
    {
        _content = content;
        _isDirty = false;
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(DisplayTitle));
        RequestAnalysis();
    }

    public void MarkAsSaved()
    {
        IsDirty = false;
    }

    private void RequestAnalysis()
    {
        if (!string.IsNullOrWhiteSpace(FilePath))
        {
            App.Messenger.Send(new TokenizeBufferRequestEvent(Content, FilePath));
        }
    }

    public void UpdateLexicalAnalysis(IEnumerable<Token> tokens, IEnumerable<ErrorToken> errors)
    {
        Tokens.Clear();
        Tokens.AddRange(tokens);

        LexicalErrors.Clear();
        LexicalErrors.AddRange(errors);

        LastAnalyzed = DateTime.Now;
    }

    public void UpdateSyntaxAnalysis(AstNode? ast, IEnumerable<ParseError> errors)
    {
        SyntaxTree = ast;

        SyntaxErrors.Clear();
        SyntaxErrors.AddRange(errors);

        LastAnalyzed = DateTime.Now;
    }

    public void UpdateSemanticAnalysis(AnnotatedAstNode? annotatedAst, SymbolTable symbolTable, IEnumerable<SemanticError> errors)
    {
        SemanticTree = annotatedAst;

        SymbolTable.Clear();
        SymbolTable.CopyFrom(symbolTable);

        SemanticErrors.Clear();
        SemanticErrors.AddRange(errors);

        LastAnalyzed = DateTime.Now;
    }

    public void ClearAnalysisResults()
    {
        Tokens.Clear();
        LexicalErrors.Clear();
        SyntaxTree = null;
        SyntaxErrors.Clear();
        SemanticTree = null;
        SymbolTable.Clear();
        SemanticErrors.Clear();
        SyntaxTreeExpansionState.Clear();
        SemanticTreeExpansionState.Clear();
    }

    public void ClearContent()
    {
        _content = string.Empty;
        _isDirty = false;
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(DisplayTitle));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
