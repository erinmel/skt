using System.Collections.Generic;
using skt.Shared;

namespace skt.IDE.Services.Buss;

public class TokenizeFileRequestEvent
{
    public string FilePath { get; }
    public bool WriteTokenFile { get; }
    public TokenizeFileRequestEvent(string filePath, bool writeTokenFile = false)
    {
        FilePath = filePath;
        WriteTokenFile = writeTokenFile;
    }
}

public class TokenizeBufferRequestEvent
{
    public string? FilePath { get; }
    public string Content { get; }
    public TokenizeBufferRequestEvent(string content, string? filePath)
    {
        Content = content;
        FilePath = filePath;
    }
}

public class LexicalAnalysisCompletedEvent
{
    public string? FilePath { get; }
    public int TokenCount { get; }
    public int ErrorCount { get; }
    public List<Token> Tokens { get; }
    public List<ErrorToken> Errors { get; }
    public bool FromBuffer { get; }

    public LexicalAnalysisCompletedEvent(string? filePath, List<Token> tokens, List<ErrorToken> errors, bool fromBuffer = false)
    {
        FilePath = filePath;
        Tokens = tokens;
        Errors = errors;
        TokenCount = tokens.Count;
        ErrorCount = errors.Count;
        FromBuffer = fromBuffer;
    }
}

public class LexicalAnalysisFailedEvent
{
    public string? FilePath { get; }
    public string Message { get; }
    public bool FromBuffer { get; }
    public LexicalAnalysisFailedEvent(string? filePath, string message, bool fromBuffer)
    {
        FilePath = filePath;
        Message = message;
        FromBuffer = fromBuffer;
    }
}

public class ParseFileRequestEvent
{
    public string FilePath { get; }

    public ParseFileRequestEvent(string filePath)
    {
        FilePath = filePath;
    }
}

public class ParseBufferRequestEvent
{
    public string? FilePath { get; }
    public List<Token> Tokens { get; }

    public ParseBufferRequestEvent(List<Token> tokens, string? filePath)
    {
        Tokens = tokens;
        FilePath = filePath;
    }
}

public class SyntaxAnalysisCompletedEvent
{
    public string? FilePath { get; }
    public AstNode? Ast { get; }
    public List<ParseError> Errors { get; }
    public bool FromBuffer { get; }

    public SyntaxAnalysisCompletedEvent(string? filePath, AstNode? ast, List<ParseError> errors, bool fromBuffer = false)
    {
        FilePath = filePath;
        Ast = ast;
        Errors = errors;
        FromBuffer = fromBuffer;
    }
}

public class SyntaxAnalysisFailedEvent
{
    public string FilePath { get; }
    public string Message { get; }

    public SyntaxAnalysisFailedEvent(string filePath, string message)
    {
        FilePath = filePath;
        Message = message;
    }
}

public class SemanticAnalysisRequestEvent
{
    public string? FilePath { get; }
    public AstNode Ast { get; }
    public bool FromBuffer { get; }

    public SemanticAnalysisRequestEvent(AstNode ast, string? filePath, bool fromBuffer = false)
    {
        Ast = ast;
        FilePath = filePath;
        FromBuffer = fromBuffer;
    }
}

public class SemanticAnalysisCompletedEvent
{
    public string? FilePath { get; }
    public AnnotatedAstNode? AnnotatedAst { get; }
    public SymbolTable SymbolTable { get; }
    public List<SemanticError> Errors { get; }
    public bool FromBuffer { get; }

    public SemanticAnalysisCompletedEvent(
        string? filePath,
        AnnotatedAstNode? annotatedAst,
        SymbolTable symbolTable,
        List<SemanticError> errors,
        bool fromBuffer = false)
    {
        FilePath = filePath;
        AnnotatedAst = annotatedAst;
        SymbolTable = symbolTable;
        Errors = errors;
        FromBuffer = fromBuffer;
    }
}

public class SemanticAnalysisFailedEvent
{
    public string? FilePath { get; }
    public string Message { get; }
    public bool FromBuffer { get; }

    public SemanticAnalysisFailedEvent(string? filePath, string message, bool fromBuffer = false)
    {
        FilePath = filePath;
        Message = message;
        FromBuffer = fromBuffer;
    }
}
