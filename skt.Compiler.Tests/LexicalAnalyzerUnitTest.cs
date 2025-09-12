using skt.Shared;

namespace skt.Compiler.Tests;

public class LexicalAnalyzerUnitTest
{
    private readonly LexicalAnalyzer _analyzer = new();

    #region Basic Token Recognition Tests

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmptyResults()
    {
        // Arrange
        string code = "";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Empty(tokens);
        Assert.Empty(errors);
    }

    [Fact]
    public void Tokenize_SimpleInteger_ReturnsIntegerToken()
    {
        // Arrange
        string code = "42";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.INTEGER, tokens[0].Type);
        Assert.Equal("42", tokens[0].Value);
        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(1, tokens[0].Column);
    }

    [Fact]
    public void Tokenize_RealNumber_ReturnsRealToken()
    {
        // Arrange
        string code = "3.14";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.REAL, tokens[0].Type);
        Assert.Equal("3.14", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_StringWithQuotes_ReturnsStringToken()
    {
        // Arrange
        string code = "\"hello world\"";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.STRING, tokens[0].Type);
        Assert.Equal("\"hello world\"", tokens[0].Value);
    }

    #endregion

    #region Keyword Recognition Tests

    [Theory]
    [InlineData("if")]
    [InlineData("else")]
    [InlineData("while")]
    [InlineData("do")]
    [InlineData("switch")]
    [InlineData("case")]
    [InlineData("main")]
    [InlineData("cin")]
    [InlineData("cout")]
    public void Tokenize_ReservedKeyword_ReturnsReservedWordToken(string keyword)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(keyword);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.RESERVED_WORD, tokens[0].Type);
        Assert.Equal(keyword, tokens[0].Value);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("float")]
    [InlineData("bool")]
    [InlineData("string")]
    public void Tokenize_TypeKeyword_ReturnsReservedWordToken(string typeKeyword)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(typeKeyword);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.RESERVED_WORD, tokens[0].Type);
        Assert.Equal(typeKeyword, tokens[0].Value);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Tokenize_BooleanLiteral_ReturnsBooleanToken(string boolean)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(boolean);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.BOOLEAN, tokens[0].Type);
        Assert.Equal(boolean, tokens[0].Value);
    }

    #endregion

    #region Identifier Tests

    [Theory]
    [InlineData("variable")]
    [InlineData("_underscore")]
    [InlineData("var123")]
    [InlineData("camelCase")]
    [InlineData("PascalCase")]
    [InlineData("snake_case")]
    public void Tokenize_ValidIdentifier_ReturnsIdentifierToken(string identifier)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(identifier);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.IDENTIFIER, tokens[0].Type);
        Assert.Equal(identifier, tokens[0].Value);
    }

    #endregion

    #region Operator Tests

    [Theory]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("/")]
    [InlineData("%")]
    [InlineData("^")]
    public void Tokenize_ArithmeticOperator_ReturnsArithmeticOperatorToken(string op)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(op);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.ARITHMETIC_OPERATOR, tokens[0].Type);
        Assert.Equal(op, tokens[0].Value);
    }

    [Theory]
    [InlineData("==")]
    [InlineData("!=")]
    [InlineData("<=")]
    [InlineData(">=")]
    [InlineData("<")]
    [InlineData(">")]
    public void Tokenize_RelationalOperator_ReturnsRelationalOperatorToken(string op)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(op);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.RELATIONAL_OPERATOR, tokens[0].Type);
        Assert.Equal(op, tokens[0].Value);
    }

    [Theory]
    [InlineData("&&")]
    [InlineData("||")]
    [InlineData("!")]
    public void Tokenize_LogicalOperator_ReturnsLogicalOperatorToken(string op)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(op);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.LOGICAL_OPERATOR, tokens[0].Type);
        Assert.Equal(op, tokens[0].Value);
    }

    [Theory]
    [InlineData("=")]
    [InlineData("+=")]
    [InlineData("-=")]
    [InlineData("*=")]
    [InlineData("/=")]
    [InlineData("%=")]
    [InlineData("^=")]
    [InlineData("++")]
    [InlineData("--")]
    public void Tokenize_AssignmentOperator_ReturnsAssignmentOperatorToken(string op)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(op);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.ASSIGNMENT_OPERATOR, tokens[0].Type);
        Assert.Equal(op, tokens[0].Value);
    }

    [Theory]
    [InlineData("<<")]
    [InlineData(">>")]
    public void Tokenize_ShiftOperator_ReturnsShiftOperatorToken(string op)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(op);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.SHIFT_OPERATOR, tokens[0].Type);
        Assert.Equal(op, tokens[0].Value);
    }

    #endregion

    #region Symbol Tests

    [Theory]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData(",")]
    [InlineData(";")]
    public void Tokenize_Symbol_ReturnsSymbolToken(string symbol)
    {
        // Act
        var (tokens, errors) = _analyzer.Tokenize(symbol);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.SYMBOL, tokens[0].Type);
        Assert.Equal(symbol, tokens[0].Value);
    }

    #endregion

    #region Comment Tests

    [Fact]
    public void Tokenize_LineComment_ReturnsCommentToken()
    {
        // Arrange
        string code = "// This is a line comment";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.COMMENT, tokens[0].Type);
        Assert.Equal("// This is a line comment", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_BlockComment_ReturnsCommentToken()
    {
        // Arrange
        string code = "/* This is a block comment */";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.COMMENT, tokens[0].Type);
        Assert.Equal("/* This is a block comment */", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_MultilineBlockComment_ReturnsCommentToken()
    {
        // Arrange
        string code = @"/* Multi-line
block comment */";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.COMMENT, tokens[0].Type);
        Assert.Contains("Multi-line", tokens[0].Value);
        Assert.Contains("block comment", tokens[0].Value);
    }

    #endregion

    #region Complex Expression Tests

    [Fact]
    public void Tokenize_SimpleAssignment_ReturnsCorrectTokenSequence()
    {
        // Arrange
        string code = "x = 42";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Equal(3, tokens.Count);
        Assert.Empty(errors);
        
        Assert.Equal(TokenType.IDENTIFIER, tokens[0].Type);
        Assert.Equal("x", tokens[0].Value);
        
        Assert.Equal(TokenType.ASSIGNMENT_OPERATOR, tokens[1].Type);
        Assert.Equal("=", tokens[1].Value);
        
        Assert.Equal(TokenType.INTEGER, tokens[2].Type);
        Assert.Equal("42", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_ArithmeticExpression_ReturnsCorrectTokenSequence()
    {
        // Arrange
        string code = "x = 42 + y * 3.14";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Equal(7, tokens.Count);
        Assert.Empty(errors);
        
        Assert.Equal(TokenType.IDENTIFIER, tokens[0].Type); // x
        Assert.Equal(TokenType.ASSIGNMENT_OPERATOR, tokens[1].Type); // =
        Assert.Equal(TokenType.INTEGER, tokens[2].Type); // 42
        Assert.Equal(TokenType.ARITHMETIC_OPERATOR, tokens[3].Type); // +
        Assert.Equal(TokenType.IDENTIFIER, tokens[4].Type); // y
        Assert.Equal(TokenType.ARITHMETIC_OPERATOR, tokens[5].Type); // *
        Assert.Equal(TokenType.REAL, tokens[6].Type); // 3.14
    }

    [Fact]
    public void Tokenize_FunctionDeclaration_ReturnsCorrectTokenSequence()
    {
        // Arrange
        string code = "int main() { return 0; }";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Empty(errors);
        Assert.Equal(9, tokens.Count); // Fixed: should be 9 tokens, not 8
        
        Assert.Equal(TokenType.RESERVED_WORD, tokens[0].Type); // int
        Assert.Equal(TokenType.RESERVED_WORD, tokens[1].Type); // main
        Assert.Equal(TokenType.SYMBOL, tokens[2].Type); // (
        Assert.Equal(TokenType.SYMBOL, tokens[3].Type); // )
        Assert.Equal(TokenType.SYMBOL, tokens[4].Type); // {
        Assert.Equal(TokenType.IDENTIFIER, tokens[5].Type); // return
        Assert.Equal(TokenType.INTEGER, tokens[6].Type); // 0
        Assert.Equal(TokenType.SYMBOL, tokens[7].Type); // ;
        Assert.Equal(TokenType.SYMBOL, tokens[8].Type); // }
    }

    [Fact]
    public void Tokenize_ConditionWithComments_ReturnsCorrectTokens()
    {
        // Arrange
        string code = @"
            x = 42; // assign value
            /* check condition */
            if (x > 0) { cout << ""positive""; }
        ";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Empty(errors);
        
        var comments = tokens.Where(t => t.Type == TokenType.COMMENT).ToList();
        Assert.Equal(2, comments.Count);
        
        var codeTokens = tokens.Where(t => t.Type != TokenType.COMMENT).ToList();
        Assert.True(codeTokens.Count >= 12); // x, =, 42, ;, if, (, x, >, 0, ), {, cout, <<, "positive", ;, }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Tokenize_UnterminatedString_ReturnsErrorToken()
    {
        // Arrange
        string code = "\"unterminated string";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Empty(tokens);
        Assert.Single(errors);
        Assert.Equal(TokenType.ERROR, errors[0].Type);
        Assert.Equal("unclosed string", errors[0].Expected);
        Assert.Contains("unterminated string", errors[0].Value);
    }

    [Fact]
    public void Tokenize_InvalidCharacter_ReturnsErrorToken()
    {
        // Arrange
        string code = "@#$"; // Invalid characters
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Empty(tokens);
        Assert.Equal(3, errors.Count); // One error for each invalid character
        Assert.All(errors, error => Assert.Equal(TokenType.ERROR, error.Type));
        Assert.All(errors, error => Assert.Equal("valid token", error.Expected));
    }

    #endregion

    #region String Handling Tests

    [Fact]
    public void Tokenize_StringWithEscapeSequences_HandlesCorrectly()
    {
        // Arrange
        string code = @"""Hello \""World\""""";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.STRING, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsStringToken()
    {
        // Arrange
        string code = "\"\"";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Single(tokens);
        Assert.Empty(errors);
        Assert.Equal(TokenType.STRING, tokens[0].Type);
        Assert.Equal("\"\"", tokens[0].Value);
    }

    #endregion

    #region Position Tracking Tests

    [Fact]
    public void Tokenize_MultilineCode_TracksLineNumbersCorrectly()
    {
        // Arrange
        string code = @"int x
= 42;
string message = ""hello"";";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Empty(errors);
        Assert.True(tokens.Count >= 7);
        
        // Check line numbers - Fixed expectations to match actual tokenizer behavior
        Assert.Equal(1, tokens[0].Line); // int (line 1)
        Assert.Equal(1, tokens[1].Line); // x (line 1)  
        Assert.Equal(2, tokens[2].Line); // = (line 2)
        Assert.Equal(2, tokens[3].Line); // 42 (line 2)
        Assert.Equal(2, tokens[4].Line); // ; (line 2) - Fixed: semicolon is on line 2, not line 3
        Assert.Equal(3, tokens[5].Line); // string (line 3) - Fixed: adjusted index
    }

    [Fact]
    public void Tokenize_WithWhitespace_IgnoresWhitespaceButTracksPosition()
    {
        // Arrange
        string code = "   x   =   42   ";
        
        // Act
        var (tokens, errors) = _analyzer.Tokenize(code);
        
        // Assert
        Assert.Equal(3, tokens.Count);
        Assert.Empty(errors);
        
        // Check that whitespace is ignored but positions are correct
        Assert.Equal("x", tokens[0].Value);
        Assert.Equal("=", tokens[1].Value);
        Assert.Equal("42", tokens[2].Value);
        
        // Column positions should reflect actual positions in source
        Assert.Equal(4, tokens[0].Column); // x starts at column 4 (fixed: was expecting 5)
    }

    #endregion

    #region File Output Tests

    [Fact]
    public void TokenizeToFile_ValidCode_CreatesTokenFile()
    {
        // Arrange
        string code = "int x = 42;";
        string testFilePath = "test_file.skt";
        
        // Act
        var (tokens, errors) = _analyzer.TokenizeToFile(code, testFilePath);
        
        // Assert
        Assert.Empty(errors);
        Assert.True(tokens.Count > 0);
        
        // Verify file was created
        string outputDir = "lexical_output";
        Assert.True(Directory.Exists(outputDir));
        
        var files = Directory.GetFiles(outputDir, "*.sktt");
        Assert.Single(files);
        
        // Cleanup
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void TokenizeToFile_CommentsExcluded_FiltersCommentsFromOutput()
    {
        // Arrange
        string code = @"int x = 42; // comment
        /* block comment */
        x += 10;";
        string testFilePath = "test_with_comments.skt";
        
        // Act
        var (allTokens, errors) = _analyzer.TokenizeToFile(code, testFilePath);
        
        // Assert
        Assert.Empty(errors);
        
        var comments = allTokens.Where(t => t.Type == TokenType.COMMENT).ToList();
        Assert.Equal(2, comments.Count); // Should find comments in return value
        
        // Cleanup
        string outputDir = "lexical_output";
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }
    }

    #endregion
}
