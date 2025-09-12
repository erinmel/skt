using System.Security.Cryptography;
using System.Text;
using skt.Shared;

namespace skt.Compiler;

public class SyntaxAnalyzer
{
    private readonly Dictionary<string, List<List<string>>> _grammar;
    private readonly HashSet<string> _nonTerminals;
    private readonly HashSet<string> _terminals;
    private readonly Dictionary<string, HashSet<string>> _first;
    private readonly Dictionary<string, HashSet<string>> _follow;
    private readonly Dictionary<(string, string), (List<string>, int)> _parsingTable;
    private readonly List<ParseError> _errors;
    private readonly HashSet<(int, int, string)> _errorPositions;
    private readonly HashSet<string> _syncTokens;

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

        _syncTokens = new HashSet<string>
        {
            ";", "}", "{", "if", "while", "do", "cin", "cout",
            "int", "float", "bool", "string", "$"
        };

        BuildParser();
    }

    private Dictionary<string, List<List<string>>> DefineGrammar()
    {
        return new Dictionary<string, List<List<string>>>
        {
            ["prog"] = new() { new() { "main", "{", "block", "}" } },
            ["block"] = new() { new() { "elem", "block" }, new() { "EPSILON" } },
            ["elem"] = new() { new() { "vdecl" }, new() { "stmt" } },
            ["vdecl"] = new() { new() { "type", "ids", ";" } },
            ["ids"] = new() { new() { "ID", "ids_t" } },
            ["ids_t"] = new() { new() { ",", "ID", "ids_t" }, new() { "EPSILON" } },
            ["type"] = new() { new() { "int" }, new() { "float" }, new() { "bool" }, new() { "string" } },
            ["stmt"] = new()
            {
                new() { "ifstmt" }, new() { "whilestmt" }, new() { "dostmt" },
                new() { "cinstmt" }, new() { "coutstmt" }, new() { "asgn" }
            },
            ["ifstmt"] = new() { new() { "if", "expr", "{", "stmts", "}", "op_else" } },
            ["whilestmt"] = new() { new() { "while", "expr", "{", "stmts", "}" } },
            ["dostmt"] = new() { new() { "do", "{", "stmts", "}", "while", "expr", ";" } },
            ["cinstmt"] = new() { new() { "cin", ">>", "ID", ";" } },
            ["coutstmt"] = new() { new() { "cout", "<<", "out", ";" } },
            ["op_else"] = new() { new() { "else", "{", "stmts", "}" }, new() { "EPSILON" } },
            ["out"] = new() { new() { "expr", "out_t" } },
            ["out_t"] = new() { new() { "<<", "expr", "out_t" }, new() { "EPSILON" } },
            // Modified: stmts now allows both variable declarations and statements
            ["stmts"] = new() { new() { "elem", "stmts" }, new() { "EPSILON" } },
            ["asgn"] = new()
            {
                new() { "ID", "asgn_op", "expr", ";" },
                new() { "ID", "inc_op", ";" }
            },
            ["asgn_op"] = new()
            {
                new() { "=" }, new() { "+=" }, new() { "-=" }, new() { "*=" },
                new() { "/=" }, new() { "%=" }, new() { "^=" }
            },
            ["inc_op"] = new() { new() { "++" }, new() { "--" } },
            ["expr"] = new() { new() { "logic" } },
            ["logic"] = new() { new() { "not", "logic_t" } },
            ["logic_t"] = new() { new() { "log_op", "not", "logic_t" }, new() { "EPSILON" } },
            ["log_op"] = new() { new() { "||" }, new() { "&&" } },
            ["not"] = new() { new() { "!", "not" }, new() { "rel" } },
            ["rel"] = new() { new() { "arit", "rel_t" } },
            ["rel_t"] = new() { new() { "rel_op", "arit" }, new() { "EPSILON" } },
            ["rel_op"] = new()
            {
                new() { "<" }, new() { "<=" }, new() { ">" }, new() { ">=" },
                new() { "==" }, new() { "!=" }
            },
            ["arit"] = new() { new() { "term", "arit_t" } },
            ["arit_t"] = new() { new() { "sum_op", "term", "arit_t" }, new() { "EPSILON" } },
            ["sum_op"] = new() { new() { "+" }, new() { "-" } },
            ["term"] = new() { new() { "pow", "term_t" } },
            ["term_t"] = new() { new() { "mul_op", "pow", "term_t" }, new() { "EPSILON" } },
            ["mul_op"] = new() { new() { "*" }, new() { "/" }, new() { "%" } },
            ["pow"] = new() { new() { "fact", "pow_t" } },
            ["pow_t"] = new() { new() { "^", "fact", "pow_t" }, new() { "EPSILON" } },
            ["fact"] = new()
            {
                new() { "(", "expr", ")" }, new() { "ID" }, new() { "sig_num" },
                new() { "bool_lit" }, new() { "STRING_LITERAL" }
            },
            ["sig_num"] = new() { new() { "sign", "num" } },
            ["sign"] = new() { new() { "+" }, new() { "-" }, new() { "EPSILON" } },
            ["num"] = new() { new() { "ENTERO" }, new() { "REAL" } },
            ["bool_lit"] = new() { new() { "true" }, new() { "false" } }
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

        foreach (var productions in _grammar.Values)
        {
            foreach (var production in productions)
            {
                foreach (var symbol in production)
                {
                    if (!_nonTerminals.Contains(symbol) && symbol != "EPSILON")
                    {
                        _terminals.Add(symbol);
                    }
                }
            }
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
        if (production.Count == 1 && production[0] == "EPSILON")
        {
            _first[nonTerminal].Add("EPSILON");
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
                _first[nonTerminal].UnionWith(_first[symbol].Except(new[] { "EPSILON" }));
                if (!_first[symbol].Contains("EPSILON"))
                {
                    allHaveEpsilon = false;
                    break;
                }
            }
        }

        if (allHaveEpsilon)
        {
            _first[nonTerminal].Add("EPSILON");
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
            var followBefore = _follow.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Count
            );

            foreach (var nt in _nonTerminals)
            {
                foreach (var production in _grammar[nt])
                {
                    AddFollowOfProduction(nt, production);
                }
            }

            var followAfter = _follow.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Count
            );

            if (!followBefore.SequenceEqual(followAfter))
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
                    _follow[symbol].UnionWith(firstBeta.Except(new[] { "EPSILON" }));
                    if (firstBeta.Contains("EPSILON"))
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
                result.UnionWith(_first[symbol].Except(new[] { "EPSILON" }));
                if (!_first[symbol].Contains("EPSILON"))
                {
                    allHaveEpsilon = false;
                    break;
                }
            }
        }

        if (allHaveEpsilon)
        {
            result.Add("EPSILON");
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

                // For each terminal in FIRST of the production
                foreach (var terminal in firstProd.Except(new[] { "EPSILON" }))
                {
                    if (!_parsingTable.ContainsKey((nt, terminal)))
                    {
                        _parsingTable[(nt, terminal)] = (production, i);
                    }
                }

                // If EPSILON is in FIRST, add for FOLLOW
                if (firstProd.Contains("EPSILON"))
                {
                    foreach (var terminal in _follow[nt])
                    {
                        if (!_parsingTable.ContainsKey((nt, terminal)))
                        {
                            _parsingTable[(nt, terminal)] = (production, i);
                        }
                    }
                }
            }
        }
    }

    private string MapTokenToTerminal(Token? token)
    {
        if (token == null) return "$";

        var tokenMapping = new Dictionary<TokenType, string>
        {
            [TokenType.IDENTIFIER] = "ID",
            [TokenType.INTEGER] = "ENTERO",
            [TokenType.REAL] = "REAL",
            [TokenType.STRING] = "STRING_LITERAL"
        };

        return tokenMapping.GetValueOrDefault(token.Type, token.Value);
    }

    private Token? CurrentToken => _position < _tokens.Count ? _tokens[_position] : null;
    private bool AtEnd => _position >= _tokens.Count;

    public (ASTNode? ast, List<ParseError> errors) Parse(string filePath)
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
            _tokens = _tokens.Where(t => t.Type != TokenType.COMMENT).ToList();

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
        return Convert.ToHexString(hash)[..16].ToLower(); // Use first 16 chars
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
        return matchingFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
    }

    private ASTNode? ParseNonTerminal(string nonTerminal)
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
            if (new[] { "vdecl", "asgn", "stmt" }.Contains(nonTerminal))
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

    private ASTNode? ParseStandard(string nonTerminal)
    {
        var currentToken = CurrentToken;

        if (AtEnd)
        {
            if (_parsingTable.ContainsKey((nonTerminal, "$")))
            {
                var (production, _) = _parsingTable[(nonTerminal, "$")];
                return CreateNode(nonTerminal, new List<ASTNode>(), currentToken);
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
                var (production, _) = _parsingTable[(nonTerminal, "$")];
                return CreateNode(nonTerminal, new List<ASTNode>(), currentToken);
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

    private ASTNode ParseProduction(string nonTerminal, List<string> production)
    {
        var currentToken = CurrentToken;
        var node = CreateNode(nonTerminal, new List<ASTNode>(), currentToken);

        foreach (var symbol in production)
        {
            if (symbol == "EPSILON")
            {
                continue;
            }
            else if (_terminals.Contains(symbol))
            {
                var child = ParseTerminal(symbol);
                if (child != null)
                {
                    node.Children.Add(child);
                    UpdateNodePosition(node, child);
                }
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

    private ASTNode? ParseTerminal(string expectedSymbol)
    {
        var currentToken = CurrentToken;

        if (AtEnd)
        {
            AddError($"EOF inesperado, esperando '{expectedSymbol}'");
            return CreateVirtualNode(expectedSymbol);
        }

        string currentTerminal = MapTokenToTerminal(currentToken);

        if ((expectedSymbol == "STRING_LITERAL" && currentToken!.Type == TokenType.STRING) ||
            currentTerminal == expectedSymbol)
        {
            _position++;
            return CreateNode(expectedSymbol, new List<ASTNode>(), currentToken, currentToken);
        }
        else
        {
            AddError($"Esperaba '{expectedSymbol}', encontró '{currentToken?.Value}'");
            // For critical tokens, insert virtual token
            if (new[] { ";", ")", "}", "{", "(" }.Contains(expectedSymbol))
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

    private ASTNode? ParseWithRecovery(string nonTerminal)
    {
        return nonTerminal switch
        {
            "vdecl" => ParseVdeclRecovery(),
            "asgn" => ParseAsgnRecovery(),
            "stmt" => ParseStmtRecovery(),
            _ => ParseStandard(nonTerminal)
        };
    }

    private ASTNode ParseVdeclRecovery()
    {
        var currentToken = CurrentToken;
        var node = CreateNode("vdecl", new List<ASTNode>(), currentToken);

        // type
        var typeNode = ParseNonTerminal("type");
        if (typeNode != null)
        {
            node.Children.Add(typeNode);
        }

        // ids with comma recovery
        var idsNode = ParseIdsRecovery();
        if (idsNode != null)
        {
            node.Children.Add(idsNode);
        }

        // ;
        var semicolon = ExpectToken(";", "Falta punto y coma en declaración");
        node.Children.Add(semicolon);

        return node;
    }

    private ASTNode ParseAsgnRecovery()
    {
        var currentToken = CurrentToken;
        var node = CreateNode("asgn", new List<ASTNode>(), currentToken);

        // ID
        var idNode = ExpectToken("ID", "Identificador esperado en asignación");
        node.Children.Add(idNode);

        // Operator
        currentToken = CurrentToken;
        if (currentToken != null)
        {
            string terminal = MapTokenToTerminal(currentToken);
            if (new[] { "=", "+=", "-=", "*=", "/=", "%=", "^=" }.Contains(terminal))
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
                    SynchronizeTo(new[] { ";" });
                }
            }
            else if (new[] { "++", "--" }.Contains(terminal))
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
                SynchronizeTo(new[] { ";" });
            }
        }

        // ;
        var semicolon = ExpectToken(";", "Falta punto y coma en asignación");
        node.Children.Add(semicolon);

        return node;
    }

    private ASTNode? ParseStmtRecovery()
    {
        var currentToken = CurrentToken;
        string terminal = currentToken != null ? MapTokenToTerminal(currentToken) : "$";

        if (new[] { "cin", "cout" }.Contains(terminal))
        {
            return ParseIoStatement(terminal);
        }
        else
        {
            return ParseStandard("stmt");
        }
    }

    private ASTNode ParseIdsRecovery()
    {
        var currentToken = CurrentToken;
        var node = CreateNode("ids", new List<ASTNode>(), currentToken);

        // First ID
        var idNode = ExpectToken("ID", "Identificador esperado");
        node.Children.Add(idNode);

        // ids_t with recovery
        var idsTNode = ParseIdsTRecovery();
        if (idsTNode != null)
        {
            node.Children.Add(idsTNode);
        }

        return node;
    }

    private ASTNode ParseIdsTRecovery()
    {
        var currentToken = CurrentToken;
        var node = CreateNode("ids_t", new List<ASTNode>(), currentToken);

        // Check if there's ID without comma (int x y;)
        if (currentToken?.Type == TokenType.IDENTIFIER)
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
            if (idsTNode != null)
            {
                node.Children.Add(idsTNode);
            }
        }
        else if (currentToken != null && MapTokenToTerminal(currentToken) == ",")
        {
            // Normal case with comma
            var commaNode = ExpectToken(",");
            node.Children.Add(commaNode);

            var idNode = ExpectToken("ID", "Identificador esperado después de coma");
            node.Children.Add(idNode);

            var idsTNode = ParseIdsTRecovery();
            if (idsTNode != null)
            {
                node.Children.Add(idsTNode);
            }
        }

        return node;
    }

    private ASTNode ParseIoStatement(string ioType)
    {
        var currentToken = CurrentToken;
        var node = CreateNode("stmt", new List<ASTNode>(), currentToken);

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
                SynchronizeTo(new[] { ";" });
            }
        }

        // ;
        var semicolon = ExpectToken(";", $"Falta punto y coma en sentencia {ioType}");
        node.Children.Add(semicolon);

        return node;
    }

    private ASTNode ExpectToken(string expectedTerminal, string? errorMessage = null)
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
            return CreateNode(expectedTerminal, new List<ASTNode>(), currentToken, currentToken);
        }
        else
        {
            errorMessage ??= $"Esperaba '{expectedTerminal}', encontró '{currentToken?.Value}'";
            AddError(errorMessage);

            // For critical tokens, insert virtual; for others, consume and create virtual
            if (new[] { ";", ")", "}", "{", "(" }.Contains(expectedTerminal))
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

    private ASTNode CreateNode(string rule, List<ASTNode> children, Token? token = null, Token? endToken = null)
    {
        if (token != null)
        {
            // Only assign token to terminal nodes (if it's in the set of terminals)
            Token? tokenToAssign = null;
            if (_terminals.Contains(rule))
            {
                tokenToAssign = endToken ?? token;
            }

            return new ASTNode(
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
            return new ASTNode(rule: rule, children: children, line: 0, column: 0);
        }
    }

    private ASTNode CreateVirtualNode(string terminal)
    {
        var currentToken = CurrentToken;
        var virtualToken = new Token(
            Type: new[] { ";", "(", ")", "{", "}", "," }.Contains(terminal)
                ? TokenType.SYMBOL
                : TokenType.IDENTIFIER,
            Value: terminal,
            Line: currentToken?.Line ?? 1,
            Column: currentToken?.Column ?? 1,
            EndLine: currentToken?.Line ?? 1,
            EndColumn: currentToken?.Column ?? 1
        );
        return CreateNode(terminal, new List<ASTNode>(), virtualToken, virtualToken);
    }

    private ASTNode CreateErrorNode(string expectedSymbol)
    {
        var currentToken = CurrentToken;
        // Error nodes don't have tokens because they're not terminals
        return new ASTNode(
            rule: $"ERROR({expectedSymbol})",
            children: new List<ASTNode>(),
            token: null,
            line: currentToken?.Line ?? 0,
            column: currentToken?.Column ?? 0,
            endLine: currentToken?.EndLine ?? 0,
            endColumn: currentToken?.EndColumn ?? 0
        );
    }

    private void UpdateNodePosition(ASTNode parent, ASTNode child)
    {
        parent.EndLine = child.EndLine;
        parent.EndColumn = child.EndColumn;
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
        if (_errorPositions.Contains(errorKey))
        {
            return;
        }

        _errorPositions.Add(errorKey);

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

    public List<ParseError> GetErrors() => new(_errors);
    public bool HasErrors => _errors.Count > 0;
    public void ResetErrorTracking() => _errorPositions.Clear();
}
