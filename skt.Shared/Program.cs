// See https://aka.ms/new-console-template for more information
using System.Text.Json.Serialization;

namespace skt.Shared;

public enum TokenType
{
    INTEGER,
    REAL,
    BOOLEAN,
    STRING,
    IDENTIFIER,
    RESERVED_WORD,
    COMMENT,
    ARITHMETIC_OPERATOR,
    RELATIONAL_OPERATOR,
    LOGICAL_OPERATOR,
    ASSIGNMENT_OPERATOR,
    SHIFT_OPERATOR,
    SYMBOL,
    ERROR
}

[Serializable]
public record Token(
    TokenType Type,
    string Value,
    int Line,
    int Column,
    int EndLine,
    int EndColumn
);

[Serializable]
public record ErrorToken(
    TokenType Type,
    string Expected,
    string Value,
    int Line,
    int Column,
    int EndLine,
    int EndColumn
);

public static class TokenConstants
{
    public static readonly HashSet<string> Keywords = new()
    {
        "if", "else", "do", "while", "switch", "case",
        "int", "float", "bool", "string", "main",
        "cin", "cout", "true", "false"
    };

    public static readonly Dictionary<string, TokenType> OperatorsMap = new()
    {
        { "+", TokenType.ARITHMETIC_OPERATOR },
        { "-", TokenType.ARITHMETIC_OPERATOR },
        { "*", TokenType.ARITHMETIC_OPERATOR },
        { "/", TokenType.ARITHMETIC_OPERATOR },
        { "%", TokenType.ARITHMETIC_OPERATOR },
        { "^", TokenType.ARITHMETIC_OPERATOR },
        { "<", TokenType.RELATIONAL_OPERATOR },
        { ">", TokenType.RELATIONAL_OPERATOR },
        { "==", TokenType.RELATIONAL_OPERATOR },
        { "<=", TokenType.RELATIONAL_OPERATOR },
        { ">=", TokenType.RELATIONAL_OPERATOR },
        { "!=", TokenType.RELATIONAL_OPERATOR },
        { "&&", TokenType.LOGICAL_OPERATOR },
        { "||", TokenType.LOGICAL_OPERATOR },
        { "!", TokenType.LOGICAL_OPERATOR },
        { "=", TokenType.ASSIGNMENT_OPERATOR },
        { "+=", TokenType.ASSIGNMENT_OPERATOR },
        { "-=", TokenType.ASSIGNMENT_OPERATOR },
        { "*=", TokenType.ASSIGNMENT_OPERATOR },
        { "/=", TokenType.ASSIGNMENT_OPERATOR },
        { "%=", TokenType.ASSIGNMENT_OPERATOR },
        { "^=", TokenType.ASSIGNMENT_OPERATOR },
        { "++", TokenType.ASSIGNMENT_OPERATOR },
        { "--", TokenType.ASSIGNMENT_OPERATOR },
        { "(", TokenType.SYMBOL },
        { ")", TokenType.SYMBOL },
        { "{", TokenType.SYMBOL },
        { "}", TokenType.SYMBOL },
        { ",", TokenType.SYMBOL },
        { ";", TokenType.SYMBOL }
    };

    public static readonly Dictionary<string, TokenType> MultiCharOperators = new()
    {
        { "++", TokenType.ASSIGNMENT_OPERATOR },
        { "+=", TokenType.ASSIGNMENT_OPERATOR },
        { "--", TokenType.ASSIGNMENT_OPERATOR },
        { "-=", TokenType.ASSIGNMENT_OPERATOR },
        { "*=", TokenType.ASSIGNMENT_OPERATOR },
        { "/=", TokenType.ASSIGNMENT_OPERATOR },
        { "%=", TokenType.ASSIGNMENT_OPERATOR },
        { "^=", TokenType.ASSIGNMENT_OPERATOR },
        { "==", TokenType.RELATIONAL_OPERATOR },
        { "!=", TokenType.RELATIONAL_OPERATOR },
        { "<=", TokenType.RELATIONAL_OPERATOR },
        { ">=", TokenType.RELATIONAL_OPERATOR },
        { ">>", TokenType.SHIFT_OPERATOR },
        { "<<", TokenType.SHIFT_OPERATOR },
        { "&&", TokenType.LOGICAL_OPERATOR },
        { "||", TokenType.LOGICAL_OPERATOR }
    };

    public static readonly HashSet<char> MultiCharFirst = new()
    {
        '+', '-', '*', '/', '%', '^', '=', '!', '<', '>', '&', '|'
    };
}

// AST Node classes
[Serializable]
public class ASTNode
{
    public string Rule { get; set; }
    public List<ASTNode> Children { get; set; }
    public Token? Token { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }

    public ASTNode(string rule, List<ASTNode>? children = null, Token? token = null,
                   int line = 0, int column = 0, int endLine = 0, int endColumn = 0)
    {
        Rule = rule;
        Children = children ?? new List<ASTNode>();
        Token = token;
        Line = line;
        Column = column;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    public bool IsTerminal => Token != null;
    public bool IsError => Rule.StartsWith("ERROR");
}

// Production rule for grammar
public record Production(string LeftSide, List<string> RightSide, int Index);

// Parser error with position information
[Serializable]
public record ParseError(
    string Message,
    int Line,
    int Column,
    int EndLine,
    int EndColumn,
    string? ExpectedToken = null,
    string? FoundToken = null
);
