using System;
using System.Collections.Generic;
using skt.Compiler;
using skt.Shared;

namespace skt.IDE.Services;

public class DocumentCompilationState
{
    public string FilePath { get; }
    public string Content { get; set; }

    public List<Token> Tokens { get; }
    public List<ErrorToken> LexicalErrors { get; }

    public AstNode? SyntaxTree { get; set; }
    public List<ParseError> SyntaxErrors { get; }

    public AnnotatedAstNode? SemanticTree { get; set; }
    public SymbolTable SymbolTable { get; }
    public List<SemanticError> SemanticErrors { get; }

    public Dictionary<string, bool> SyntaxTreeExpansionState { get; }
    public Dictionary<string, bool> SemanticTreeExpansionState { get; }

    public int CaretPosition { get; set; }
    public double ScrollPosition { get; set; }

    public DateTime LastAnalyzed { get; set; }
    public bool HasLexicalAnalysis => Tokens.Count > 0 || LexicalErrors.Count > 0;
    public bool HasSyntaxAnalysis => SyntaxTree != null;
    public bool HasSemanticAnalysis => SemanticTree != null;

    public DocumentCompilationState(string filePath, string content = "")
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Content = content;

        Tokens = new List<Token>();
        LexicalErrors = new List<ErrorToken>();
        SyntaxErrors = new List<ParseError>();
        SemanticErrors = new List<SemanticError>();
        SymbolTable = new SymbolTable();

        SyntaxTreeExpansionState = new Dictionary<string, bool>();
        SemanticTreeExpansionState = new Dictionary<string, bool>();

        LastAnalyzed = DateTime.MinValue;
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

    public void Clear()
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
}
