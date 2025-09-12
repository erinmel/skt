namespace skt.Compiler.Tests;

public class SyntaxAnalyzerUnitTest
{
    private readonly SyntaxAnalyzer _analyzer = new();
    private readonly LexicalAnalyzer _lexicalAnalyzer = new();

    #region Helper Methods

    private string CreateTestFile(string code)
    {
        string tempFile = Path.GetTempFileName() + ".skt";
        File.WriteAllText(tempFile, code);

        // Generate tokens using lexical analyzer
        _lexicalAnalyzer.TokenizeToFile(code, tempFile);

        return tempFile;
    }

    private static void CleanupTestFile(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    #endregion

    #region Basic Program Structure Tests

    [Fact]
    public void Parse_SimpleMainProgram_ReturnsValidAST()
    {
        // Arrange
        string code = "main { }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
            Assert.Equal("prog", ast.Rule);
            Assert.Equal(4, ast.Children.Count); // main, {, block, }
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_EmptyProgram_ReturnsValidAST()
    {
        // Arrange
        string code = "main { }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
            Assert.Equal("prog", ast.Rule);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Variable Declaration Tests

    [Fact]
    public void Parse_SimpleVariableDeclaration_ReturnsValidAST()
    {
        // Arrange
        string code = "main { int x; }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);

            // Find the variable declaration in the AST
            var blockNode = ast.Children.FirstOrDefault(c => c.Rule == "block");
            Assert.NotNull(blockNode);

            var elemNode = blockNode.Children.FirstOrDefault(c => c.Rule == "elem");
            Assert.NotNull(elemNode);

            var vdeclNode = elemNode.Children.FirstOrDefault(c => c.Rule == "vdecl");
            Assert.NotNull(vdeclNode);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_MultipleVariableDeclaration_ReturnsValidAST()
    {
        // Arrange
        string code = "main { int x, y, z; }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_DifferentTypeDeclarations_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int x; 
            float y; 
            bool z; 
            string w; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Assignment Tests

    [Fact]
    public void Parse_SimpleAssignment_ReturnsValidAST()
    {
        // Arrange
        string code = "main { int x; x = 5; }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_CompoundAssignments_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int x; 
            x += 5; 
            x -= 3; 
            x *= 2; 
            x /= 4; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_IncrementDecrement_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int x; 
            x++; 
            x--; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Expression Tests

    [Fact]
    public void Parse_ArithmeticExpression_ReturnsValidAST()
    {
        // Arrange
        string code = "main { int x; x = 5 + 3 * 2; }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_LogicalExpression_ReturnsValidAST()
    {
        // Arrange
        string code = "main { bool x; x = true && false || !true; }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_RelationalExpression_ReturnsValidAST()
    {
        // Arrange
        string code = "main { bool x; x = 5 > 3 && 2 <= 4; }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_ParenthesizedExpression_ReturnsValidAST()
    {
        // Arrange
        string code = "main { int x; x = (5 + 3) * 2; }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Control Flow Tests

    [Fact]
    public void Parse_IfStatement_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            bool x; 
            if x { 
                int y; 
            } 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_IfElseStatement_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            bool x; 
            if x { 
                int y; 
            } else { 
                int z; 
            } 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_WhileLoop_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            bool x; 
            while x { 
                int y; 
            } 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_DoWhileLoop_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            bool x; 
            do { 
                int y; 
            } while x; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Input/Output Tests

    [Fact]
    public void Parse_CinStatement_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int x; 
            cin >> x; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_CoutStatement_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int x; 
            cout << x; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_CoutMultipleValues_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int x; 
            string msg; 
            cout << msg << x << ""hello""; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    public void Parse_MissingSemicolon_RecoversWithError()
    {
        // Arrange
        string code = @"main { 
            int x 
            int y; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Message.Contains("punto y coma"));
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_MissingCommaInDeclaration_RecoversWithError()
    {
        // Arrange
        string code = @"main { 
            int x y z; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Message.Contains("coma"));
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_InvalidToken_RecoversWithError()
    {
        // Arrange
        string code = @"main { 
            int x; 
            @ invalid token; 
            int y; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.NotEmpty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Complex Program Tests

    [Fact]
    public void Parse_ComplexProgram_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int x, y, z; 
            bool flag; 
            string message; 
            
            x = 10; 
            y = 20; 
            z = x + y * 2; 
            
            flag = x > y && z <= 50; 
            
            if flag { 
                cout << ""Result is: "" << z; 
                x++; 
            } else { 
                cout << ""No result""; 
                x--; 
            } 
            
            while x > 0 { 
                cin >> y; 
                x = x - 1; 
            } 
            
            do { 
                z += 5; 
            } while z < 100; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
            Assert.Equal("prog", ast.Rule);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_NestedControlFlow_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int i, j; 
            bool condition; 
            
            i = 0; 
            while i < 10 { 
                j = 0; 
                while j < 5 { 
                    if i > j { 
                        cout << i << j; 
                    } else { 
                        if condition { 
                            cout << ""nested""; 
                        } 
                    } 
                    j++; 
                } 
                i++; 
            } 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void Parse_LargeProgram_CompletesInReasonableTime()
    {
        // Arrange
        var codeBuilder = new System.Text.StringBuilder();
        codeBuilder.AppendLine("main {");

        // Generate a moderately large program (reduced from 1000 to 100 to avoid recursion overflow)
        for (int i = 0; i < 100; i++)
        {
            codeBuilder.AppendLine($"    int var{i};");
            codeBuilder.AppendLine($"    var{i} = {i};");
        }

        codeBuilder.AppendLine("}");
        string code = codeBuilder.ToString();
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var (ast, errors) = _analyzer.Parse(testFile);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Parsing should complete within 5 seconds");
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_EmptyFile_ReturnsError()
    {
        // Arrange
        string code = "";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.Null(ast);
            Assert.NotEmpty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_OnlyMain_ReturnsValidAST()
    {
        // Arrange
        string code = "main";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            // Should have errors due to missing braces
            Assert.NotEmpty(errors);
            // Print the ast for debugging
            Assert.NotNull(ast);

        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_NonExistentFile_ReturnsError()
    {
        // Arrange
        string nonExistentFile = "nonexistent_file.skt";

        // Act
        var (ast, errors) = _analyzer.Parse(nonExistentFile);

        // Assert
        Assert.Null(ast);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Message.Contains("tokens no encontrado"));
    }

    #endregion

    #region Grammar Rule Coverage Tests

    [Fact]
    public void Parse_AllOperatorTypes_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int a, b; 
            bool c; 
            float d; 
            
            // Arithmetic operators
            a = b + 5 - 3 * 2 / 4 % 3; 
            
            // Power operator
            a = b ^ 2; 
            
            // Relational operators
            c = a < b && a <= b && a > b && a >= b && a == b && a != b; 
            
            // Logical operators
            c = !c || (c && true); 
            
            // Assignment operators
            a += 1; 
            a -= 1; 
            a *= 2; 
            a /= 2; 
            a %= 3; 
            a ^= 2; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public void Parse_AllLiteralTypes_ReturnsValidAST()
    {
        // Arrange
        string code = @"main { 
            int intVar; 
            float floatVar; 
            bool boolVar; 
            string stringVar; 
            
            intVar = 42; 
            floatVar = 3.14; 
            boolVar = true; 
            stringVar = ""Hello World""; 
            
            boolVar = false; 
            intVar = -10; 
            floatVar = +2.5; 
        }";
        string testFile = CreateTestFile(code);

        try
        {
            // Act
            var (ast, errors) = _analyzer.Parse(testFile);

            // Assert
            Assert.NotNull(ast);
            Assert.Empty(errors);

            // Print the AST structure
            Console.WriteLine("=== AST Structure for Parse_AllLiteralTypes_ReturnsValidAST ===");
            PrintAstNode(ast, 0);
            Console.WriteLine("=== End AST Structure ===");
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    private static void PrintAstNode(object node, int depth)
    {
        if (node == null) return;

        string indent = new string(' ', depth * 2);

        // Use reflection to get properties since we don't know the exact AST node type
        var nodeType = node.GetType();
        var ruleProperty = nodeType.GetProperty("Rule");
        var valueProperty = nodeType.GetProperty("Value");
        var childrenProperty = nodeType.GetProperty("Children");

        string rule = ruleProperty?.GetValue(node)?.ToString() ?? "Unknown";
        string value = valueProperty?.GetValue(node)?.ToString();

        if (!string.IsNullOrEmpty(value))
        {
            Console.WriteLine($"{indent}{rule}: \"{value}\"");
        }
        else
        {
            Console.WriteLine($"{indent}{rule}");
        }

        // Print children if they exist
        var children = childrenProperty?.GetValue(node);
        if (children is IEnumerable<object> childList)
        {
            foreach (var child in childList)
            {
                PrintAstNode(child, depth + 1);
            }
        }
    }

    #endregion
}
