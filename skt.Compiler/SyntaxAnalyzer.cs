using System.Security.Cryptography;
using System.Text;
using skt.Shared;

namespace skt.Compiler;

public class SyntaxAnalyzer
{
    private const string Epsilon = "EPSILON";
    private const string RuleVdecl = "vdecl";
    private const string RuleIdsT = "ids_t";
    private const string RuleStmts = "stmts";

    // Shared token sets
    private static readonly string[] CriticalTokens = [";", ")", "}", "{", "("];
    private static readonly string[] IoTerms = ["cin", "cout"];
    private static readonly string[] IncOps = ["++", "--"];
    private static readonly string[] AsgnOps = ["=", "+=", "-=", "*=", "/=", "%=", "^="];

    private readonly Dictionary<string, List<List<string>>> _grammar;
    private readonly HashSet<string> _nonTerminals;
    private readonly HashSet<string> _terminals;
    private readonly Dictionary<string, HashSet<string>> _first;
    private readonly Dictionary<string, HashSet<string>> _follow;
    private readonly Dictionary<(string, string), (List<string>, int)> _parsingTable;
    private readonly List<ParseError> _errors;
    private readonly HashSet<(int, int, string)> _errorPositions;

    private List<Token> _tokens;
    private int _position;
    private int _recursionDepth;
    private const int MaxRecursionDepth = 300;

    public SyntaxAnalyzer()
    {
        _grammar = DefineGrammar();
        _nonTerminals = new HashSet<string>();
        _terminals = new HashSet<string>();
        _first = new Dictionary<string, HashSet<string>>();
        _follow = new Dictionary<string, HashSet<string>>();
        _parsingTable = new Dictionary<(string, string), (List<string>, int)>();
        _errors = new List<ParseError>();
        _errorPositions = new HashSet<(int, int, string)>();
        _tokens = new List<Token>();
        _position = 0;
        _recursionDepth = 0;

        BuildParser();
    }

    private static Dictionary<string, List<List<string>>> DefineGrammar()
    {
        return new Dictionary<string, List<List<string>>>
        {
            ["prog"] = [["main", "{", "block", "}"]],
            ["block"] = [["elem", "block"], [Epsilon]],
            ["elem"] = [[RuleVdecl], ["stmt"]],
            [RuleVdecl] = [["type", "ids", ";"]],
            ["ids"] = [["ID", RuleIdsT]],
            [RuleIdsT] = [[",", "ID", RuleIdsT], [Epsilon]],
            ["type"] = [["int"], ["float"], ["bool"], ["string"]],
            ["stmt"] = [["ifstmt"], ["whilestmt"], ["dostmt"], ["cinstmt"], ["coutstmt"], ["asgn"]],
            ["ifstmt"] = [["if", "expr", "{", RuleStmts, "}", "op_else"]],
            ["whilestmt"] = [["while", "expr", "{", RuleStmts, "}"]],
            ["dostmt"] = [["do", "{", RuleStmts, "}", "while", "expr", ";"]],
            ["cinstmt"] = [["cin", ">>", "ID", ";"]],
            ["coutstmt"] = [["cout", "<<", "out", ";"]],
            ["op_else"] = [["else", "{", RuleStmts, "}"], [Epsilon]],
            ["out"] = [["expr", "out_t"]],
            ["out_t"] = [["<<", "expr", "out_t"], [Epsilon]],
            // Modified: stmts now allows both variable declarations and statements
            [RuleStmts] = [["elem", RuleStmts], [Epsilon]],
            ["asgn"] = [["ID", "asgn_op", "expr", ";"], ["ID", "inc_op", ";"]],
            ["asgn_op"] = [["="], ["+="], ["-="], ["*="], ["/="], ["%="], ["^="]],
            ["inc_op"] = [["++"], ["--"]],
            ["expr"] = [["logic"]],
            ["logic"] = [["not", "logic_t"]],
            ["logic_t"] = [["log_op", "not", "logic_t"], [Epsilon]],
            ["log_op"] = [["||"], ["&&"]],
            ["not"] = [["!", "not"], ["rel"]],
            ["rel"] = [["arit", "rel_t"]],
            ["rel_t"] = [["rel_op", "arit"], [Epsilon]],
            ["rel_op"] = [["<"], ["<="], [">"], [">="], ["=="], ["!="]],
            ["arit"] = [["term", "arit_t"]],
            ["arit_t"] = [["sum_op", "term", "arit_t"], [Epsilon]],
            ["sum_op"] = [["+"], ["-"]],
            ["term"] = [["pow", "term_t"]],
            ["term_t"] = [["mul_op", "pow", "term_t"], [Epsilon]],
            ["mul_op"] = [["*"], ["/"], ["%"]],
            ["pow"] = [["fact", "pow_t"]],
            ["pow_t"] = [["^", "fact", "pow_t"], [Epsilon]],
            ["fact"] = [["(", "expr", ")"], ["ID"], ["sig_num"], ["bool_lit"], ["STRING_LITERAL"]],
            ["sig_num"] = [["sign", "num"]],
            ["sign"] = [["+"], ["-"], [Epsilon]],
            ["num"] = [["ENTERO"], ["REAL"]],
            ["bool_lit"] = [["true"], ["false"]]
        };
    }

    private void BuildParser()
    {
        ExtractSymbols();
        ComputeFirstSets();
        ComputeFollowSets();
        BuildParsingTable();
    }

    private void ExtractSymbols()
    {
        _nonTerminals.UnionWith(_grammar.Keys);

        foreach (var symbol in _grammar.Values
                     .SelectMany(productions => productions)
                     .SelectMany(production => production)
                     .Where(symbol => !_nonTerminals.Contains(symbol) && symbol != Epsilon))
        {
            _terminals.Add(symbol);
        }
        _terminals.Add("$");
    }

    private void ComputeFirstSets()
    {
        // Initialize FIRST sets
        foreach (var symbol in _nonTerminals.Union(_terminals))
        {
            _first[symbol] = new HashSet<string>();
        }

        // Terminals
        foreach (var terminal in _terminals)
        {
            _first[terminal].Add(terminal);
        }

        // Non-terminals
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var nt in _nonTerminals)
            {
                foreach (var production in _grammar[nt])
                {
                    var firstBefore = _first[nt].Count;
                    AddFirstOfProduction(nt, production);
                    if (_first[nt].Count > firstBefore)
                    {
                        changed = true;
                    }
                }
            }
        }
    }

    private void AddFirstOfProduction(string nonTerminal, List<string> production)
    {
        if (production is [Epsilon])
        {
            _first[nonTerminal].Add(Epsilon);
            return;
        }

        bool allHaveEpsilon = true;
        foreach (var symbol in production)
        {
            if (_terminals.Contains(symbol))
            {
                _first[nonTerminal].Add(symbol);
                allHaveEpsilon = false;
                break;
            }
            else if (_nonTerminals.Contains(symbol))
            {
                _first[nonTerminal].UnionWith(_first[symbol].Except([Epsilon]));
                if (!_first[symbol].Contains(Epsilon))
                {
                    allHaveEpsilon = false;
                    break;
                }
            }
        }

        if (allHaveEpsilon)
        {
            _first[nonTerminal].Add(Epsilon);
        }
    }

    private void ComputeFollowSets()
    {
        foreach (var nt in _nonTerminals)
        {
            _follow[nt] = new HashSet<string>();
        }

        _follow["prog"].Add("$");

        bool changed = true;
        while (changed)
        {
            changed = false;
            var countsBefore = _follow.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);

            foreach (var nt in _nonTerminals)
            {
                foreach (var production in _grammar[nt])
                {
                    AddFollowOfProduction(nt, production);
                }
            }

            // Detect changes without relying on dictionary enumeration order
            if (_nonTerminals.Any(nt => _follow[nt].Count != countsBefore[nt]))
            {
                changed = true;
            }
        }
    }

    private void AddFollowOfProduction(string nonTerminal, List<string> production)
    {
        for (int i = 0; i < production.Count; i++)
        {
            var symbol = production[i];
            if (_nonTerminals.Contains(symbol))
            {
                var beta = production.Skip(i + 1).ToList();
                if (beta.Count > 0)
                {
                    var firstBeta = FirstOfString(beta);
                    _follow[symbol].UnionWith(firstBeta.Except([Epsilon]));
                    if (firstBeta.Contains(Epsilon))
                    {
                        _follow[symbol].UnionWith(_follow[nonTerminal]);
                    }
                }
                else
                {
                    _follow[symbol].UnionWith(_follow[nonTerminal]);
                }
            }
        }
    }

    private HashSet<string> FirstOfString(List<string> symbols)
    {
        var result = new HashSet<string>();
        bool allHaveEpsilon = true;

        foreach (var symbol in symbols)
        {
            if (_terminals.Contains(symbol))
            {
                result.Add(symbol);
                allHaveEpsilon = false;
                break;
            }
            else if (_nonTerminals.Contains(symbol))
            {
                result.UnionWith(_first[symbol].Except([Epsilon]));
                if (!_first[symbol].Contains(Epsilon))
                {
                    allHaveEpsilon = false;
                    break;
                }
            }
        }

        if (allHaveEpsilon)
        {
            result.Add(Epsilon);
        }
        return result;
    }

    private void BuildParsingTable()
    {
        foreach (var nt in _nonTerminals)
        {
            for (int i = 0; i < _grammar[nt].Count; i++)
            {
                var production = _grammar[nt][i];
                var firstProd = FirstOfString(production);

                foreach (var terminal in firstProd.Where(t => t != Epsilon))
                {
                    _parsingTable.TryAdd((nt, terminal), (production, i));
                }

                if (firstProd.Contains(Epsilon))
                {
                    foreach (var terminal in _follow[nt])
                    {
                        _parsingTable.TryAdd((nt, terminal), (production, i));
                    }
                }
            }
        }
    }

    private static string MapTokenToTerminal(Token? token)
    {
        if (token == null) return "$";

        var tokenMapping = new Dictionary<TokenType, string>
        {
            [TokenType.Identifier] = "ID",
            [TokenType.Integer] = "ENTERO",
            [TokenType.Real] = "REAL",
            [TokenType.String] = "STRING_LITERAL"
        };

        return tokenMapping.GetValueOrDefault(token.Type, token.Value);
    }

    private Token? CurrentToken => _position < _tokens.Count ? _tokens[_position] : null;
    private bool AtEnd => _position >= _tokens.Count;

    public (AstNode? ast, List<ParseError> errors) Parse(string filePath)
    {
        try
        {
            // Load tokens from hashed file - use fallback strategy to find the file
            string hashPrefix = CreateHashPrefix(filePath);
            string? file = FindTokenFile(hashPrefix);

            if (file == null)
            {
                AddError("Archivo de tokens no encontrado");
                return (null, _errors);
            }

            // Use binary deserialization instead of JSON
            _tokens = LexicalAnalyzer.ReadBinaryTokens(file);

            // Filter out comments (though they should already be excluded)
            _tokens = _tokens.Where(t => t.Type != TokenType.Comment).ToList();

            _position = 0;
            _errors.Clear();
            _errorPositions.Clear();

            var ast = ParseNonTerminal("prog");

            if (ast != null && !AtEnd)
            {
                int remaining = _tokens.Count - _position;
                AddError($"Tokens extra después del final ({remaining} tokens restantes)");
            }

            return (ast, _errors);
        }
        catch (Exception e)
        {
            AddError($"Error interno del parser: {e.Message}");
            return (null, _errors);
        }
    }

    private static string CreateHashPrefix(string filePath)
    {
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath));
        return Convert.ToHexString(hash).ToLower(); // Use full hash for collision resistance
    }

    private static string? FindTokenFile(string hashPrefix)
    {
        string outputDir = "lexical_output";
        if (!Directory.Exists(outputDir))
        {
            return null;
        }

        // Find files with the same hash prefix
        var matchingFiles = Directory.GetFiles(outputDir, $"{hashPrefix}_*.sktt");

        if (matchingFiles.Length == 0)
        {
            return null;
        }

        // Return the most recent file
        return matchingFiles.OrderByDescending(File.GetLastWriteTime).First();
    }

    private AstNode? ParseNonTerminal(string nonTerminal)
    {
        // Recursion control
        _recursionDepth++;
        if (_recursionDepth > MaxRecursionDepth)
        {
            _recursionDepth--;
            AddError($"Máxima profundidad de recursión excedida en {nonTerminal}");
            return CreateErrorNode(nonTerminal);
        }

        try
        {
            // Special cases with error recovery
            if (new[] { RuleVdecl, "asgn", "stmt" }.Contains(nonTerminal))
            {
                return ParseWithRecovery(nonTerminal);
            }
            else
            {
                return ParseStandard(nonTerminal);
            }
        }
        finally
        {
            _recursionDepth--;
        }
    }

    private AstNode? ParseStandard(string nonTerminal)
    {
        var currentToken = CurrentToken;

        if (AtEnd)
        {
            if (_parsingTable.ContainsKey((nonTerminal, "$")))
            {
                return CreateNode(nonTerminal, new List<AstNode>(), currentToken);
            }
            else
            {
                AddError($"EOF inesperado, esperando {nonTerminal}");
                return null;
            }
        }

        string terminal = MapTokenToTerminal(currentToken);

        if (!_parsingTable.ContainsKey((nonTerminal, terminal)))
        {
            if (_parsingTable.ContainsKey((nonTerminal, "$")))
            {
                return CreateNode(nonTerminal, new List<AstNode>(), currentToken);
            }
            else
            {
                AddError($"No hay regla para {nonTerminal} con '{currentToken?.Value}'");
                if (!AtEnd)
                {
                    _position++;
                }
                return CreateErrorNode(nonTerminal);
            }
        }

        var (targetProduction, _) = _parsingTable[(nonTerminal, terminal)];
        return ParseProduction(nonTerminal, targetProduction);
    }

    private AstNode ParseProduction(string nonTerminal, List<string> production)
    {
        var currentToken = CurrentToken;
        var node = CreateNode(nonTerminal, new List<AstNode>(), currentToken);

        foreach (var symbol in production.Where(s => s != Epsilon))
        {
            if (_terminals.Contains(symbol))
            {
                var child = ParseTerminal(symbol);
                node.Children.Add(child);
                UpdateNodePosition(node, child);
            }
            else if (_nonTerminals.Contains(symbol))
            {
                var child = ParseNonTerminal(symbol);
                if (child != null)
                {
                    node.Children.Add(child);
                    UpdateNodePosition(node, child);
                }
                else
                {
                    var errorNode = CreateErrorNode(symbol);
                    node.Children.Add(errorNode);
                }
            }
        }

        return node;
    }

    private AstNode ParseTerminal(string expectedSymbol)
    {
        var currentToken = CurrentToken;

        if (AtEnd)
        {
            AddError($"EOF inesperado, esperando '{expectedSymbol}'");
            return CreateVirtualNode(expectedSymbol);
        }

        string currentTerminal = MapTokenToTerminal(currentToken);

        if ((expectedSymbol == "STRING_LITERAL" && currentToken!.Type == TokenType.String) ||
            currentTerminal == expectedSymbol)
        {
            _position++;
            return CreateNode(expectedSymbol, new List<AstNode>(), currentToken, currentToken);
        }
        else
        {
            AddError($"Esperaba '{expectedSymbol}', encontró '{currentToken?.Value}'");
            // For critical tokens, insert virtual token
            if (CriticalTokens.Contains(expectedSymbol))
            {
                return CreateVirtualNode(expectedSymbol);
            }
            else
            {
                _position++;
                return CreateVirtualNode(expectedSymbol);
            }
        }
    }

    private AstNode? ParseWithRecovery(string nonTerminal)
    {
        return nonTerminal switch
        {
            RuleVdecl => ParseVdeclRecovery(),
            "asgn" => ParseAsgnRecovery(),
            "stmt" => ParseStmtRecovery(),
            _ => ParseStandard(nonTerminal)
        };
    }

    private AstNode ParseVdeclRecovery()
    {
        var currentToken = CurrentToken;
        var node = CreateNode(RuleVdecl, new List<AstNode>(), currentToken);

        // type
        var typeNode = ParseNonTerminal("type");
        if (typeNode != null)
        {
            node.Children.Add(typeNode);
        }

        // ids with comma recovery
        var idsNode = ParseIdsRecovery();
        node.Children.Add(idsNode);

        // ;
        var semicolon = ExpectToken(";", "Falta punto y coma en declaración");
        node.Children.Add(semicolon);

        return node;
    }

    private AstNode ParseAsgnRecovery()
    {
        var currentToken = CurrentToken;
        var node = CreateNode("asgn", new List<AstNode>(), currentToken);

        // ID
        var idNode = ExpectToken("ID", "Identificador esperado en asignación");
        node.Children.Add(idNode);

        // Operator
        currentToken = CurrentToken;
        if (currentToken != null)
        {
            string terminal = MapTokenToTerminal(currentToken);
            if (AsgnOps.Contains(terminal))
            {
                var opNode = ParseNonTerminal("asgn_op");
                if (opNode != null)
                {
                    node.Children.Add(opNode);
                }

                // Expression
                var exprNode = ParseNonTerminal("expr");
                if (exprNode != null)
                {
                    node.Children.Add(exprNode);
                }
                else
                {
                    AddError("Expresión esperada después del operador");
                    SynchronizeTo([";"]);
                }
            }
            else if (IncOps.Contains(terminal))
            {
                var incNode = ParseNonTerminal("inc_op");
                if (incNode != null)
                {
                    node.Children.Add(incNode);
                }
            }
            else
            {
                AddError("Operador de asignación esperado");
                SynchronizeTo([";"]);
            }
        }

        // ;
        var semicolon = ExpectToken(";", "Falta punto y coma en asignación");
        node.Children.Add(semicolon);

        return node;
    }

    private AstNode? ParseStmtRecovery()
    {
        var currentToken = CurrentToken;
        string terminal = currentToken != null ? MapTokenToTerminal(currentToken) : "$";

        if (IoTerms.Contains(terminal))
        {
            return ParseIoStatement(terminal);
        }
        else
        {
            return ParseStandard("stmt");
        }
    }

    private AstNode ParseIdsRecovery()
    {
        var currentToken = CurrentToken;
        var node = CreateNode("ids", new List<AstNode>(), currentToken);

        // First ID
        var idNode = ExpectToken("ID", "Identificador esperado");
        node.Children.Add(idNode);

        // ids_t with recovery
        var idsTNode = ParseIdsTRecovery();
        node.Children.Add(idsTNode);

        return node;
    }

    private AstNode ParseIdsTRecovery()
    {
        var currentToken = CurrentToken;
        var node = CreateNode(RuleIdsT, new List<AstNode>(), currentToken);

        // Check if there's ID without comma (int x y;)
        if (currentToken?.Type == TokenType.Identifier)
        {
            AddError("Falta coma entre identificadores");
            // Insert virtual comma
            var commaNode = CreateVirtualNode(",");
            node.Children.Add(commaNode);

            // Parse ID
            var idNode = ExpectToken("ID");
            node.Children.Add(idNode);

            // Recursion
            var idsTNode = ParseIdsTRecovery();
            node.Children.Add(idsTNode);
        }
        else if (currentToken != null && MapTokenToTerminal(currentToken) == ",")
        {
            // Normal case with comma
            var commaNode = ExpectToken(",");
            node.Children.Add(commaNode);

            var idNode = ExpectToken("ID", "Identificador esperado después de coma");
            node.Children.Add(idNode);

            var idsTNode = ParseIdsTRecovery();
            node.Children.Add(idsTNode);
        }

        return node;
    }

    private AstNode ParseIoStatement(string ioType)
    {
        var currentToken = CurrentToken;
        var node = CreateNode("stmt", new List<AstNode>(), currentToken);

        // cin/cout
        var ioNode = ExpectToken(ioType);
        node.Children.Add(ioNode);

        // >> or <<
        string op = ioType == "cin" ? ">>" : "<<";
        var opNode = ExpectToken(op, $"Operador '{op}' esperado después de '{ioType}'");
        node.Children.Add(opNode);

        if (ioType == "cin")
        {
            // ID for cin
            var idNode = ExpectToken("ID", "Identificador esperado después de '>>'");
            node.Children.Add(idNode);
        }
        else
        {
            // out for cout
            var outNode = ParseNonTerminal("out");
            if (outNode != null)
            {
                node.Children.Add(outNode);
            }
            else
            {
                AddError("Expresión esperada después de '<<'");
                SynchronizeTo([";"]);
            }
        }

        // ;
        var semicolon = ExpectToken(";", $"Falta punto y coma en sentencia {ioType}");
        node.Children.Add(semicolon);

        return node;
    }

    private AstNode ExpectToken(string expectedTerminal, string? errorMessage = null)
    {
        var currentToken = CurrentToken;

        if (AtEnd)
        {
            errorMessage ??= $"EOF inesperado, esperando '{expectedTerminal}'";
            AddError(errorMessage);
            return CreateVirtualNode(expectedTerminal);
        }

        string currentTerminal = MapTokenToTerminal(currentToken);

        if (currentTerminal == expectedTerminal)
        {
            _position++;
            return CreateNode(expectedTerminal, new List<AstNode>(), currentToken, currentToken);
        }
        else
        {
            errorMessage ??= $"Esperaba '{expectedTerminal}', encontró '{currentToken?.Value}'";
            AddError(errorMessage);

            // For critical tokens, insert virtual; for others, consume and create virtual
            if (CriticalTokens.Contains(expectedTerminal))
            {
                return CreateVirtualNode(expectedTerminal);
            }
            else
            {
                _position++;
                return CreateVirtualNode(expectedTerminal);
            }
        }
    }

    private AstNode CreateVirtualNode(string terminal)
    {
        var currentToken = CurrentToken;
        var virtualToken = new Token(
            Type: CriticalTokens.Contains(terminal) ? TokenType.Symbol : TokenType.Identifier,
            Value: terminal,
            Line: currentToken?.Line ?? 1,
            Column: currentToken?.Column ?? 1,
            EndLine: currentToken?.Line ?? 1,
            EndColumn: currentToken?.Column ?? 1
        );
        return CreateNode(terminal, new List<AstNode>(), virtualToken, virtualToken);
    }

    // Re-added: create a node with optional token bounds, only assigning token for terminals
    private AstNode CreateNode(string rule, List<AstNode> children, Token? token = null, Token? endToken = null)
    {
        if (token != null)
        {
            Token? tokenToAssign = _terminals.Contains(rule) ? (endToken ?? token) : null;
            return new AstNode(
                rule: rule,
                children: children,
                token: tokenToAssign,
                line: token.Line,
                column: token.Column,
                endLine: (endToken ?? token).EndLine,
                endColumn: (endToken ?? token).EndColumn
            );
        }
        else
        {
            return new AstNode(rule: rule, children: children, line: 0, column: 0);
        }
    }

    private AstNode CreateErrorNode(string expectedSymbol)
    {
        var currentToken = CurrentToken;
        // Error nodes don't have tokens because they're not terminals
        return new AstNode(
            rule: $"Error({expectedSymbol})",
            children: new List<AstNode>(),
            token: null,
            line: currentToken?.Line ?? 0,
            column: currentToken?.Column ?? 0,
            endLine: currentToken?.EndLine ?? 0,
            endColumn: currentToken?.EndColumn ?? 0
        );
    }

    private static void UpdateNodePosition(AstNode parent, AstNode child)
    {
        parent.EndLine = child.EndLine;
        parent.EndColumn = child.EndColumn;
    }

    // Re-added: advance position until a token from the synchronization set is found
    private void SynchronizeTo(string[] syncSet)
    {
        while (!AtEnd)
        {
            var currentToken = CurrentToken;
            if (currentToken != null && syncSet.Contains(MapTokenToTerminal(currentToken)))
            {
                break;
            }
            _position++;
        }
    }

    private void AddError(string message)
    {
        var currentToken = CurrentToken;

        // Create unique identifier for error position
        (int, int, string) errorKey;
        if (currentToken != null)
        {
            errorKey = (currentToken.Line, currentToken.Column, message[..Math.Min(50, message.Length)]);
        }
        else
        {
            if (_position > 0)
            {
                var lastToken = _tokens[_position - 1];
                errorKey = (lastToken.Line, lastToken.EndColumn, message[..Math.Min(50, message.Length)]);
            }
            else
            {
                errorKey = (1, 1, message[..Math.Min(50, message.Length)]);
            }
        }

        // Only add if we haven't already reported an error at this position with this message
        if (!_errorPositions.Add(errorKey))
        {
            return;
        }

        ParseError error;
        if (currentToken != null)
        {
            error = new ParseError(
                Message: message,
                Line: currentToken.Line,
                Column: currentToken.Column,
                EndLine: currentToken.EndLine,
                EndColumn: currentToken.EndColumn,
                FoundToken: currentToken.Value
            );
        }
        else
        {
            // EOF case
            if (_position > 0)
            {
                var lastToken = _tokens[_position - 1];
                error = new ParseError(
                    Message: message,
                    Line: lastToken.Line,
                    Column: lastToken.EndColumn,
                    EndLine: lastToken.Line,
                    EndColumn: lastToken.EndColumn,
                    FoundToken: "EOF"
                );
            }
            else
            {
                error = new ParseError(
                    Message: message,
                    Line: 1,
                    Column: 1,
                    EndLine: 1,
                    EndColumn: 1,
                    FoundToken: "EOF"
                );
            }
        }
        _errors.Add(error);
    }

    public List<ParseError> GetErrors() => [.._errors];
    public bool HasErrors => _errors.Count > 0;
    public void ResetErrorTracking() => _errorPositions.Clear();
}
