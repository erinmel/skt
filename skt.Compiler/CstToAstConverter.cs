using skt.Shared;

namespace skt.Compiler;

public class CstToAstConverter
{
    private readonly HashSet<string> _ignoreSymbols = new() { ",", ";", "<<", ">>", "main" };
    
    private readonly Dictionary<string, int> _operators = new()
    {
        ["^"] = 1,
        ["*"] = 2,
        ["/"] = 2,
        ["%"] = 2,
        ["+"] = 3,
        ["-"] = 3,
        ["<"] = 4,
        ["<="] = 4,
        [">"] = 4,
        [">="] = 4,
        ["=="] = 5,
        ["!="] = 5,
        ["&&"] = 6,
        ["||"] = 7,
        ["!"] = 0,
        ["="] = 8,
        ["+="] = 8,
        ["-="] = 8,
        ["*="] = 8,
        ["/="] = 8,
        ["%="] = 8,
        ["^="] = 8,
        ["++"] = 0,
        ["--"] = 0
    };

    private readonly HashSet<string> _dataTypes = new() { "int", "float", "bool", "string" };

    public AstNode? Convert(AstNode? cstRoot)
    {
        if (cstRoot == null) return null;

        var program = new AstNode("program", new List<AstNode>());
        ProcessProgramContent(cstRoot, program);
        return program;
    }

    private bool IsErrorNode(AstNode node)
    {
        return node.Rule.StartsWith("Error(") || node.Rule.StartsWith("ERROR");
    }

    private AstNode CreateErrorPlaceholder(AstNode errorNode, string expectedRule)
    {
        return new AstNode(
            rule: $"ERROR_PLACEHOLDER({expectedRule})",
            children: new List<AstNode>(),
            token: null,
            line: errorNode.Line,
            column: errorNode.Column,
            endLine: errorNode.EndLine,
            endColumn: errorNode.EndColumn
        );
    }

    private void ProcessProgramContent(AstNode node, AstNode astParent)
    {
        if (node.Children == null || node.Children.Count == 0) return;

        foreach (var child in node.Children)
        {
            if (IsErrorNode(child))
            {
                var errorPlaceholder = CreateErrorPlaceholder(child, "statement");
                astParent.Children.Add(errorPlaceholder);
                continue;
            }

            try
            {
                AstNode? astNode = child.Rule switch
                {
                    "vdecl" => ConvertDeclaration(child),
                    "asgn" => ConvertAssignment(child),
                    "ifstmt" => ConvertIfStatement(child),
                    "whilestmt" => ConvertWhileStatement(child),
                    "dostmt" => ConvertDoWhileStatement(child),
                    "cinstmt" => ConvertCinStatement(child),
                    "coutstmt" => ConvertCoutStatement(child),
                    _ => null
                };

                if (astNode != null)
                {
                    astParent.Children.Add(astNode);
                }
                else
                {
                    ProcessProgramContent(child, astParent);
                }
            }
            catch
            {
                var errorPlaceholder = CreateErrorPlaceholder(child, child.Rule);
                astParent.Children.Add(errorPlaceholder);
            }
        }
    }

    private AstNode? ConvertDeclaration(AstNode vdeclNode)
    {
        try
        {
            var typeToken = FindTokenByValues(vdeclNode, _dataTypes);
            if (typeToken == null)
                return CreateErrorPlaceholder(vdeclNode, "declaration");

            var declNode = new AstNode(
                rule: typeToken.Value,
                children: new List<AstNode>(),
                token: typeToken,
                line: typeToken.Line,
                column: typeToken.Column,
                endLine: typeToken.EndLine,
                endColumn: typeToken.EndColumn
            );

            var identifiers = FindAllTokensByType(vdeclNode, TokenType.Identifier);
            foreach (var idToken in identifiers)
            {
                var idNode = new AstNode(
                    rule: "ID",
                    children: new List<AstNode>(),
                    token: idToken,
                    line: idToken.Line,
                    column: idToken.Column,
                    endLine: idToken.EndLine,
                    endColumn: idToken.EndColumn
                );
                declNode.Children.Add(idNode);
            }

            return declNode;
        }
        catch
        {
            return CreateErrorPlaceholder(vdeclNode, "declaration");
        }
    }

    private AstNode? ConvertAssignment(AstNode asgnNode)
    {
        try
        {
            var idToken = FindFirstTokenByType(asgnNode, TokenType.Identifier);
            if (idToken == null)
                return CreateErrorPlaceholder(asgnNode, "assignment");

            var opToken = FindTokenByValues(asgnNode, _operators.Keys);
            if (opToken == null)
                return CreateErrorPlaceholder(asgnNode, "assignment");

            return opToken.Value switch
            {
                "++" or "--" => TransformIncrementDecrement(idToken, opToken),
                "+=" or "-=" or "*=" or "/=" or "%=" or "^=" => TransformCompoundAssignment(asgnNode, idToken, opToken),
                _ => CreateSimpleAssignment(asgnNode, idToken, opToken)
            };
        }
        catch
        {
            return CreateErrorPlaceholder(asgnNode, "assignment");
        }
    }

    private AstNode TransformIncrementDecrement(Token idToken, Token opToken)
    {
        var assignToken = new Token(
            Type: TokenType.AssignmentOperator,
            Value: "=",
            Line: opToken.Line,
            Column: opToken.Column,
            EndLine: opToken.EndLine,
            EndColumn: opToken.EndColumn
        );

        var assignNode = new AstNode(
            rule: "=",
            children: new List<AstNode>(),
            token: assignToken,
            line: opToken.Line,
            column: opToken.Column,
            endLine: opToken.EndLine,
            endColumn: opToken.EndColumn
        );

        var idNode = new AstNode(
            rule: "ID",
            children: new List<AstNode>(),
            token: idToken,
            line: idToken.Line,
            column: idToken.Column,
            endLine: idToken.EndLine,
            endColumn: idToken.EndColumn
        );
        assignNode.Children.Add(idNode);

        var operatorValue = opToken.Value == "++" ? "+" : "-";
        var arithOpToken = new Token(
            Type: TokenType.ArithmeticOperator,
            Value: operatorValue,
            Line: opToken.Line,
            Column: opToken.Column,
            EndLine: opToken.EndLine,
            EndColumn: opToken.EndColumn
        );

        var opNode = new AstNode(
            rule: operatorValue,
            children: new List<AstNode>(),
            token: arithOpToken,
            line: opToken.Line,
            column: opToken.Column,
            endLine: opToken.EndLine,
            endColumn: opToken.EndColumn
        );

        var leftIdNode = new AstNode(
            rule: "ID",
            children: new List<AstNode>(),
            token: idToken,
            line: idToken.Line,
            column: idToken.Column,
            endLine: idToken.EndLine,
            endColumn: idToken.EndColumn
        );
        opNode.Children.Add(leftIdNode);

        var oneToken = new Token(
            Type: TokenType.Integer,
            Value: "1",
            Line: opToken.Line,
            Column: opToken.Column,
            EndLine: opToken.EndLine,
            EndColumn: opToken.EndColumn
        );

        var oneNode = new AstNode(
            rule: "literal",
            children: new List<AstNode>(),
            token: oneToken,
            line: oneToken.Line,
            column: oneToken.Column,
            endLine: oneToken.EndLine,
            endColumn: oneToken.EndColumn
        );
        opNode.Children.Add(oneNode);

        assignNode.Children.Add(opNode);
        return assignNode;
    }

    private AstNode TransformCompoundAssignment(AstNode asgnNode, Token idToken, Token opToken)
    {
        var assignToken = new Token(
            Type: TokenType.AssignmentOperator,
            Value: "=",
            Line: opToken.Line,
            Column: opToken.Column,
            EndLine: opToken.EndLine,
            EndColumn: opToken.EndColumn
        );

        var assignNode = new AstNode(
            rule: "=",
            children: new List<AstNode>(),
            token: assignToken,
            line: opToken.Line,
            column: opToken.Column,
            endLine: opToken.EndLine,
            endColumn: opToken.EndColumn
        );

        var idNode = new AstNode(
            rule: "ID",
            children: new List<AstNode>(),
            token: idToken,
            line: idToken.Line,
            column: idToken.Column,
            endLine: idToken.EndLine,
            endColumn: idToken.EndColumn
        );
        assignNode.Children.Add(idNode);

        var baseOperator = opToken.Value[..^1]; // Remove the '='
        var baseOpToken = new Token(
            Type: TokenType.ArithmeticOperator,
            Value: baseOperator,
            Line: opToken.Line,
            Column: opToken.Column,
            EndLine: opToken.EndLine,
            EndColumn: opToken.EndColumn
        );

        var exprNode = FindChildByRule(asgnNode, "expr");
        var exprAst = exprNode != null ? ConvertExpression(exprNode) : CreateErrorPlaceholder(asgnNode, "expression");

        var opNode = new AstNode(
            rule: baseOperator,
            children: new List<AstNode>(),
            token: baseOpToken,
            line: opToken.Line,
            column: opToken.Column,
            endLine: opToken.EndLine,
            endColumn: opToken.EndColumn
        );

        var leftIdNode = new AstNode(
            rule: "ID",
            children: new List<AstNode>(),
            token: idToken,
            line: idToken.Line,
            column: idToken.Column,
            endLine: idToken.EndLine,
            endColumn: idToken.EndColumn
        );
        opNode.Children.Add(leftIdNode);

        if (exprAst != null)
        {
            opNode.Children.Add(exprAst);
        }

        assignNode.Children.Add(opNode);
        return assignNode;
    }

    private AstNode CreateSimpleAssignment(AstNode asgnNode, Token idToken, Token opToken)
    {
        var assignNode = new AstNode(
            rule: opToken.Value,
            children: new List<AstNode>(),
            token: opToken,
            line: opToken.Line,
            column: opToken.Column,
            endLine: opToken.EndLine,
            endColumn: opToken.EndColumn
        );

        var idNode = new AstNode(
            rule: "ID",
            children: new List<AstNode>(),
            token: idToken,
            line: idToken.Line,
            column: idToken.Column,
            endLine: idToken.EndLine,
            endColumn: idToken.EndColumn
        );
        assignNode.Children.Add(idNode);

        var exprNode = FindChildByRule(asgnNode, "expr");
        if (exprNode != null)
        {
            var exprAst = ConvertExpression(exprNode);
            if (exprAst != null)
            {
                assignNode.Children.Add(exprAst);
            }
        }

        return assignNode;
    }

    private AstNode? ConvertExpression(AstNode exprNode)
    {
        try
        {
            var tokens = new List<Token>();
            CollectExpressionTokens(exprNode, tokens);

            if (tokens.Count == 0)
                return CreateErrorPlaceholder(exprNode, "expression");

            return BuildExpressionTreeWithParentheses(tokens);
        }
        catch
        {
            return CreateErrorPlaceholder(exprNode, "expression");
        }
    }

    private void CollectExpressionTokens(AstNode node, List<Token> tokens)
    {
        if (node.Token != null && !_ignoreSymbols.Contains(node.Token.Value))
        {
            tokens.Add(node.Token);
            return;
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                CollectExpressionTokens(child, tokens);
            }
        }
    }

    private AstNode? BuildExpressionTreeWithParentheses(List<Token> tokens)
    {
        if (tokens.Count == 0) return null;

        var processedTokens = ProcessParentheses(tokens);
        return BuildExpressionTree(processedTokens);
    }

    private List<object> ProcessParentheses(List<Token> tokens)
    {
        var result = new List<object>();
        int i = 0;

        while (i < tokens.Count)
        {
            var token = tokens[i];

            if (token.Value == "(")
            {
                int parenEnd = FindMatchingParenthesis(tokens, i);

                if (parenEnd != -1)
                {
                    var innerTokens = tokens.GetRange(i + 1, parenEnd - i - 1);

                    if (innerTokens.Count > 0)
                    {
                        var innerAst = BuildExpressionTreeWithParentheses(innerTokens);
                        if (innerAst != null)
                        {
                            result.Add(innerAst);
                        }
                    }

                    i = parenEnd + 1;
                }
                else
                {
                    result.Add(token);
                    i++;
                }
            }
            else
            {
                result.Add(token);
                i++;
            }
        }

        return result;
    }

    private int FindMatchingParenthesis(List<Token> tokens, int startIdx)
    {
        if (startIdx >= tokens.Count || tokens[startIdx].Value != "(")
            return -1;

        int parenCount = 1;
        int i = startIdx + 1;

        while (i < tokens.Count && parenCount > 0)
        {
            if (tokens[i].Value == "(")
                parenCount++;
            else if (tokens[i].Value == ")")
                parenCount--;
            i++;
        }

        return parenCount == 0 ? i - 1 : -1;
    }

    private AstNode? BuildExpressionTree(List<object> tokens)
    {
        if (tokens.Count == 0) return null;

        if (tokens.Count == 1)
        {
            if (tokens[0] is AstNode astNode)
                return astNode;
            else if (tokens[0] is Token token)
                return CreateLeafFromToken(token);
        }

        int mainOpIdx = FindMainOperatorIndex(tokens);

        if (mainOpIdx == -1)
        {
            if (tokens[0] is AstNode astNode)
                return astNode;
            else if (tokens[0] is Token token)
                return CreateLeafFromToken(token);
            return null;
        }

        var opToken = (Token)tokens[mainOpIdx];

        var opNode = new AstNode(
            rule: opToken.Value,
            children: new List<AstNode>(),
            token: opToken,
            line: opToken.Line,
            column: opToken.Column,
            endLine: opToken.EndLine,
            endColumn: opToken.EndColumn
        );

        var leftTokens = tokens.GetRange(0, mainOpIdx);
        if (leftTokens.Count > 0)
        {
            var leftAst = BuildExpressionTree(leftTokens);
            if (leftAst != null)
            {
                opNode.Children.Add(leftAst);
            }
        }

        var rightTokens = tokens.GetRange(mainOpIdx + 1, tokens.Count - mainOpIdx - 1);
        if (rightTokens.Count > 0)
        {
            var rightAst = BuildExpressionTree(rightTokens);
            if (rightAst != null)
            {
                opNode.Children.Add(rightAst);
            }
        }

        return opNode;
    }

    private int FindMainOperatorIndex(List<object> tokens)
    {
        int mainIdx = -1;
        int minPrecedence = -1;

        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is Token token && _operators.ContainsKey(token.Value))
            {
                int precedence = _operators[token.Value];
                if (precedence >= minPrecedence)
                {
                    minPrecedence = precedence;
                    mainIdx = i;
                }
            }
        }

        return mainIdx;
    }

    private AstNode CreateLeafFromToken(Token token)
    {
        string rule = token.Type switch
        {
            TokenType.Identifier => "ID",
            TokenType.Integer or TokenType.Real => "literal",
            TokenType.Boolean => "boolean",
            TokenType.String => "string",
            _ => "value"
        };

        return new AstNode(
            rule: rule,
            children: new List<AstNode>(),
            token: token,
            line: token.Line,
            column: token.Column,
            endLine: token.EndLine,
            endColumn: token.EndColumn
        );
    }

    private AstNode? ConvertIfStatement(AstNode ifNode)
    {
        try
        {
            if (IsErrorNode(ifNode))
                return CreateErrorPlaceholder(ifNode, "if");

            var branchNode = new AstNode("branch", new List<AstNode>());

            // Condition
            var exprNode = FindChildByRule(ifNode, "expr");
            if (exprNode != null)
            {
                var condition = ConvertExpression(exprNode);
                if (condition != null)
                {
                    branchNode.Children.Add(condition);
                }
            }

            // If body
            var stmtsNodes = FindAllChildrenByRule(ifNode, "stmts");
            if (stmtsNodes.Count > 0)
            {
                var ifBody = new AstNode("body", new List<AstNode>());
                ProcessStatements(stmtsNodes[0], ifBody);
                branchNode.Children.Add(ifBody);
            }

            // Else body (optional)
            var elseNode = FindChildByRule(ifNode, "op_else");
            if (elseNode != null)
            {
                var elseStmts = FindChildByRule(elseNode, "stmts");
                if (elseStmts != null)
                {
                    var elseBody = new AstNode("body", new List<AstNode>());
                    ProcessStatements(elseStmts, elseBody);
                    branchNode.Children.Add(elseBody);
                }
            }

            return branchNode;
        }
        catch
        {
            return CreateErrorPlaceholder(ifNode, "if");
        }
    }

    private AstNode? ConvertWhileStatement(AstNode whileNode)
    {
        try
        {
            if (IsErrorNode(whileNode))
                return CreateErrorPlaceholder(whileNode, "while");

            var whileAst = new AstNode("while", new List<AstNode>());

            // Condition
            var exprNode = FindChildByRule(whileNode, "expr");
            if (exprNode != null)
            {
                var condition = ConvertExpression(exprNode);
                if (condition != null)
                {
                    whileAst.Children.Add(condition);
                }
            }

            // Body
            var stmtsNode = FindChildByRule(whileNode, "stmts");
            if (stmtsNode != null)
            {
                var body = new AstNode("body", new List<AstNode>());
                ProcessStatements(stmtsNode, body);
                whileAst.Children.Add(body);
            }

            return whileAst;
        }
        catch
        {
            return CreateErrorPlaceholder(whileNode, "while");
        }
    }

    private AstNode? ConvertDoWhileStatement(AstNode doNode)
    {
        try
        {
            if (IsErrorNode(doNode))
                return CreateErrorPlaceholder(doNode, "do-while");

            var doAst = new AstNode("do", new List<AstNode>());

            // Body
            var stmtsNode = FindChildByRule(doNode, "stmts");
            if (stmtsNode != null)
            {
                var body = new AstNode("body", new List<AstNode>());
                ProcessStatements(stmtsNode, body);
                doAst.Children.Add(body);
            }

            // Condition
            var exprNode = FindChildByRule(doNode, "expr");
            if (exprNode != null)
            {
                var condition = ConvertExpression(exprNode);
                if (condition != null)
                {
                    doAst.Children.Add(condition);
                }
            }

            return doAst;
        }
        catch
        {
            return CreateErrorPlaceholder(doNode, "do-while");
        }
    }

    private AstNode? ConvertCinStatement(AstNode cinNode)
    {
        try
        {
            if (IsErrorNode(cinNode))
                return CreateErrorPlaceholder(cinNode, "cin");

            var cinAst = new AstNode("cin", new List<AstNode>());

            var idToken = FindFirstTokenByType(cinNode, TokenType.Identifier);
            if (idToken != null)
            {
                var idNode = new AstNode(
                    rule: "ID",
                    children: new List<AstNode>(),
                    token: idToken,
                    line: idToken.Line,
                    column: idToken.Column,
                    endLine: idToken.EndLine,
                    endColumn: idToken.EndColumn
                );
                cinAst.Children.Add(idNode);
            }

            return cinAst.Children.Count > 0 ? cinAst : null;
        }
        catch
        {
            return CreateErrorPlaceholder(cinNode, "cin");
        }
    }

    private AstNode? ConvertCoutStatement(AstNode coutNode)
    {
        try
        {
            if (IsErrorNode(coutNode))
                return CreateErrorPlaceholder(coutNode, "cout");

            var coutAst = new AstNode("cout", new List<AstNode>());

            var outNode = FindChildByRule(coutNode, "out");
            if (outNode != null)
            {
                var coutChain = ConvertCoutChain(outNode);
                if (coutChain != null)
                {
                    coutAst.Children.Add(coutChain);
                }
            }
            else
            {
                var exprNode = FindChildByRule(coutNode, "expr");
                if (exprNode != null)
                {
                    var exprAst = ConvertExpression(exprNode);
                    if (exprAst != null)
                    {
                        coutAst.Children.Add(exprAst);
                    }
                }
            }

            return coutAst.Children.Count > 0 ? coutAst : null;
        }
        catch
        {
            return CreateErrorPlaceholder(coutNode, "cout");
        }
    }

    private AstNode? ConvertCoutChain(AstNode outNode)
    {
        var exprNode = FindChildByRule(outNode, "expr");
        return exprNode != null ? ConvertExpression(exprNode) : null;
    }

    private void ProcessStatements(AstNode stmtsNode, AstNode bodyAst)
    {
        if (stmtsNode.Children == null) return;

        foreach (var child in stmtsNode.Children)
        {
            if (IsErrorNode(child))
            {
                var errorPlaceholder = CreateErrorPlaceholder(child, "statement");
                bodyAst.Children.Add(errorPlaceholder);
                continue;
            }

            try
            {
                if (child.Rule == "stmt")
                {
                    ProcessSingleStatement(child, bodyAst);
                }
                else if (child.Rule == "stmts")
                {
                    ProcessStatements(child, bodyAst);
                }
                else if (child.Rule == "elem")
                {
                    ProcessProgramContent(child, bodyAst);
                }
            }
            catch
            {
                var errorPlaceholder = CreateErrorPlaceholder(child, child.Rule);
                bodyAst.Children.Add(errorPlaceholder);
            }
        }
    }

    private void ProcessSingleStatement(AstNode stmtNode, AstNode bodyAst)
    {
        if (IsErrorNode(stmtNode))
        {
            var errorPlaceholder = CreateErrorPlaceholder(stmtNode, "statement");
            bodyAst.Children.Add(errorPlaceholder);
            return;
        }

        foreach (var child in stmtNode.Children)
        {
            AstNode? astNode = child.Rule switch
            {
                "asgn" => ConvertAssignment(child),
                "ifstmt" => ConvertIfStatement(child),
                "whilestmt" => ConvertWhileStatement(child),
                "dostmt" => ConvertDoWhileStatement(child),
                "cinstmt" => ConvertCinStatement(child),
                "coutstmt" => ConvertCoutStatement(child),
                _ => null
            };

            if (astNode != null)
            {
                bodyAst.Children.Add(astNode);
            }
        }
    }

    // Helper methods
    private Token? FindTokenByValues(AstNode node, IEnumerable<string> values)
    {
        if (node.Token != null && values.Contains(node.Token.Value))
            return node.Token;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                var result = FindTokenByValues(child, values);
                if (result != null) return result;
            }
        }

        return null;
    }

    private Token? FindFirstTokenByType(AstNode node, TokenType tokenType)
    {
        if (node.Token != null && node.Token.Type == tokenType)
            return node.Token;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                var result = FindFirstTokenByType(child, tokenType);
                if (result != null) return result;
            }
        }

        return null;
    }

    private List<Token> FindAllTokensByType(AstNode node, TokenType tokenType)
    {
        var tokens = new List<Token>();
        CollectTokensByType(node, tokenType, tokens);
        return tokens;
    }

    private void CollectTokensByType(AstNode node, TokenType tokenType, List<Token> tokens)
    {
        if (node.Token != null && node.Token.Type == tokenType)
        {
            tokens.Add(node.Token);
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                CollectTokensByType(child, tokenType, tokens);
            }
        }
    }

    private AstNode? FindChildByRule(AstNode node, string rule)
    {
        if (node.Children == null) return null;

        foreach (var child in node.Children)
        {
            if (child.Rule == rule)
                return child;

            var result = FindChildByRule(child, rule);
            if (result != null) return result;
        }

        return null;
    }

    private List<AstNode> FindAllChildrenByRule(AstNode node, string rule)
    {
        var results = new List<AstNode>();
        CollectChildrenByRule(node, rule, results);
        return results;
    }

    private void CollectChildrenByRule(AstNode node, string rule, List<AstNode> results)
    {
        if (node.Children == null) return;

        foreach (var child in node.Children)
        {
            if (child.Rule == rule)
            {
                results.Add(child);
            }

            CollectChildrenByRule(child, rule, results);
        }
    }
}

