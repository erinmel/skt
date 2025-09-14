using System.Collections.Immutable;

namespace skt.Shared;

public enum TokenType
{
    Integer,
    Real,
    Boolean,
    String,
    Identifier,
    ReservedWord,
    Comment,
    ArithmeticOperator,
    RelationalOperator,
    LogicalOperator,
    AssignmentOperator,
    ShiftOperator,
    Symbol,
    Error
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
    public static readonly ImmutableHashSet<string> Keywords =
        ImmutableHashSet.Create(
            "if", "else", "do", "while", "switch", "case",
            "int", "float", "bool", "string", "main",
            "cin", "cout", "true", "false"
        );

    public static readonly ImmutableDictionary<string, TokenType> OperatorsMap =
        new Dictionary<string, TokenType>
        {
            { "+", TokenType.ArithmeticOperator },
            { "-", TokenType.ArithmeticOperator },
            { "*", TokenType.ArithmeticOperator },
            { "/", TokenType.ArithmeticOperator },
            { "%", TokenType.ArithmeticOperator },
            { "^", TokenType.ArithmeticOperator },
            { "<", TokenType.RelationalOperator },
            { ">", TokenType.RelationalOperator },
            { "==", TokenType.RelationalOperator },
            { "<=", TokenType.RelationalOperator },
            { ">=", TokenType.RelationalOperator },
            { "!=", TokenType.RelationalOperator },
            { "&&", TokenType.LogicalOperator },
            { "||", TokenType.LogicalOperator },
            { "!", TokenType.LogicalOperator },
            { "=", TokenType.AssignmentOperator },
            { "+=", TokenType.AssignmentOperator },
            { "-=", TokenType.AssignmentOperator },
            { "*=", TokenType.AssignmentOperator },
            { "/=", TokenType.AssignmentOperator },
            { "%=", TokenType.AssignmentOperator },
            { "^=", TokenType.AssignmentOperator },
            { "++", TokenType.AssignmentOperator },
            { "--", TokenType.AssignmentOperator },
            { "(", TokenType.Symbol },
            { ")", TokenType.Symbol },
            { "{", TokenType.Symbol },
            { "}", TokenType.Symbol },
            { ",", TokenType.Symbol },
            { ";", TokenType.Symbol }
        }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, TokenType> MultiCharOperators =
        new Dictionary<string, TokenType>
        {
            { "++", TokenType.AssignmentOperator },
            { "+=", TokenType.AssignmentOperator },
            { "--", TokenType.AssignmentOperator },
            { "-=", TokenType.AssignmentOperator },
            { "*=", TokenType.AssignmentOperator },
            { "/=", TokenType.AssignmentOperator },
            { "%=", TokenType.AssignmentOperator },
            { "^=", TokenType.AssignmentOperator },
            { "==", TokenType.RelationalOperator },
            { "!=", TokenType.RelationalOperator },
            { "<=", TokenType.RelationalOperator },
            { ">=", TokenType.RelationalOperator },
            { ">>", TokenType.ShiftOperator },
            { "<<", TokenType.ShiftOperator },
            { "&&", TokenType.LogicalOperator },
            { "||", TokenType.LogicalOperator }
        }.ToImmutableDictionary();

    public static readonly ImmutableHashSet<char> MultiCharFirst =
        ImmutableHashSet.Create('+', '-', '*', '/', '^', '%', '=', '!', '<', '>', '&', '|');
}

// AST Node classes
[Serializable]
public class AstNode
{
    public string Rule { get; set; }
    public List<AstNode> Children { get; set; }
    public Token? Token { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }

    public AstNode(string rule, List<AstNode>? children = null, Token? token = null,
                   int line = 0, int column = 0, int endLine = 0, int endColumn = 0)
    {
        Rule = rule;
        Children = children ?? new List<AstNode>();
        Token = token;
        Line = line;
        Column = column;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    public bool IsTerminal => Token != null;
    public bool IsError => Rule.StartsWith("Error");
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
