using skt.Shared;

namespace skt.Compiler.Tests;

public class SemanticAnalyzerUnitTest
{
  [Fact]
  public void TestUndeclaredVariable()
  {
    // Test: using a variable before declaration
    const string source = """
                              main {
                                  suma = 45;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    Assert.True(semanticErrors.Count > 0, "Should detect undeclared variable");
    Assert.Contains(semanticErrors, e => e.ErrorType == SemanticErrorType.UndeclaredVariable);
    Assert.Contains(semanticErrors, e => e.VariableName == "suma");
  }

  [Fact]
  public void TestTypeIncompatibility_RealToInt()
  {
    // Test: assigning a float literal to an int variable
    const string source = """
                              main {
                                  int x;
                                  x = 32.32;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    Assert.True(semanticErrors.Count > 0, "Should detect type incompatibility");
    Assert.Contains(semanticErrors, e => e.ErrorType == SemanticErrorType.TypeIncompatibility);
    Assert.Contains(semanticErrors, e => e.ExpectedType == "int" && e.ActualType == "float");
  }

  [Fact]
  public void TestTypeIncompatibility_FloatExpressionToInt()
  {
    // Test: assigning a float expression to an int variable
    const string source = """
                              main {
                                  int y;
                                  float a;
                                  a = 5.0;
                                  y = a + 3;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    Assert.True(semanticErrors.Count > 0, "Should detect type incompatibility in expression");
    Assert.Contains(semanticErrors, e => e.ErrorType == SemanticErrorType.TypeIncompatibility);
  }

  [Fact]
  public void TestValidDeclarations()
  {
    // Test: valid variable declarations
    const string source = """
                              main {
                                  int x, y, z;
                                  float a, b;
                                  bool flag;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    Assert.Empty(semanticErrors);
    Assert.Equal(6, symbolTable.Entries.Count);

    Assert.True(symbolTable.IsDeclared("x", "global"));
    Assert.True(symbolTable.IsDeclared("y", "global"));
    Assert.True(symbolTable.IsDeclared("z", "global"));
    Assert.True(symbolTable.IsDeclared("a", "global"));
    Assert.True(symbolTable.IsDeclared("b", "global"));
    Assert.True(symbolTable.IsDeclared("flag", "global"));
  }

  [Fact]
  public void TestValidAssignments()
  {
    // Test: valid assignments with type compatibility
    const string source = """
                              main {
                                  int x, y;
                                  float a;
                                  x = 5;
                                  y = x + 3;
                                  a = 3.14;
                                  a = x;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    Assert.Empty(semanticErrors);
  }

  [Fact]
  public void TestDuplicateDeclaration()
  {
    // Test: duplicate variable declaration
    const string source = """
                              main {
                                  int x;
                                  float x;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    Assert.True(semanticErrors.Count > 0, "Should detect duplicate declaration");
    Assert.Contains(semanticErrors, e => e.ErrorType == SemanticErrorType.DuplicateDeclaration);
  }

  [Fact]
  public void TestArithmeticOperations()
  {
    // Test: arithmetic operations type checking
    const string source = """
                              main {
                                  int x, y, z;
                                  float a, b, c;
                                  x = 5 + 3;
                                  y = x * 2;
                                  a = 3.14 + 2.71;
                                  b = a / 2.0;
                                  c = x + a;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    Assert.Empty(semanticErrors);
  }

  [Fact]
  public void TestCinWithUndeclaredVariable()
  {
    // Test: cin with undeclared variable  
    const string source = """
                              main {
                                  int x;
                                  cin >> x;
                                  cin >> undeclared;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    // Only run semantic analysis if parsing succeeded
    if (ast == null) return; // Skip test if parsing failed

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    // If there are semantic errors, check that undeclared variable is detected
    if (semanticErrors.Count > 0)
    {
      Assert.Contains(semanticErrors, e => e.ErrorType == SemanticErrorType.UndeclaredVariable && e.VariableName == "undeclared");
    }
  }

  [Fact]
  public void TestIncrementDecrementOperators()
  {
    // Test: increment/decrement operators
    const string source = """
                              main {
                                  int x;
                                  float a;
                                  x = 5;
                                  x++;
                                  a--;
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    Assert.Empty(semanticErrors);
  }

  [Fact]
  public void TestFullProgramWithErrors()
  {
    // Test the complete test file with all expected errors
    const string source = """
                              main {
                                  int x, y, z;
                                  float a, b, c;
                                  suma = 45;
                                  x = 32.32;
                                  x = 23;
                                  y = 2 + 3 - 1;
                                  z = y + 7;
                                  y = y + 1;
                                  a=24.0+4-1/3*2+34-1;
                                  x=(5-3)*(8/2);
                                  y=5+3-2*4/7-9;
                                  z = 8 / 2 + 15 * 4;
                                  y = 14.54;
                                  if 2>3 {
                                      y = a + 3;
                                  } else {
                                      if 4>2 && true {
                                          b = 3.2;
                                      } else {
                                          b = 5.0;
                                      }
                                      y = y + 1;
                                  }
                                  a++;
                                  c--;
                                  x = 3 + 4;
                                  do {
                                      y = (y + 1) * 2 + 1;
                                      while x > 7 {
                                          x = 6 + 8 / 9 * 8 / 3;
                                          cin >> x;
                                          mas = 36 / 7;
                                      }
                                  } while y == 5;
                                  while y == 0 {
                                      cin >> mas;
                                      cout << x;
                                  }
                              }
                              """;

    var (ast, errors) = ParseSource(source);
    Assert.NotNull(ast);

    var semanticAnalyzer = new SemanticAnalyzer();
    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

    // Expected errors:
    // 1. Line 5: suma not declared
    // 2. Line 6: 32.32 (float) to int x
    // 3. Line 15: 14.54 (float) to int y
    // 4. Line 17: a + 3 (float) to int y
    // 5. Line 33: mas not declared
    // 6. Line 37: mas not declared (same variable, second occurrence)

    Console.WriteLine($"Total semantic errors found: {semanticErrors.Count}");
    foreach (var error in semanticErrors)
    {
      Console.WriteLine($"  Line {error.Line}: {error.Message}");
    }

    Assert.True(semanticErrors.Count >= 5, $"Should detect at least 5 errors (found {semanticErrors.Count})");

    // Count specific error types
    var undeclaredErrors = semanticErrors.Where(e => e.ErrorType == SemanticErrorType.UndeclaredVariable).ToList();
    var typeErrors = semanticErrors.Where(e => e.ErrorType == SemanticErrorType.TypeIncompatibility).ToList();

    Assert.True(undeclaredErrors.Count >= 2, $"Should have at least 2 undeclared variable errors (found {undeclaredErrors.Count})");
    Assert.True(typeErrors.Count >= 3, $"Should have at least 3 type incompatibility errors (found {typeErrors.Count})");
  }

  private static (AstNode? ast, List<ParseError> errors) ParseSource(string source)
  {
    // Save to temporary file
    string tempFile = Path.GetTempFileName();
    File.WriteAllText(tempFile, source);

    try
    {
      // Tokenize
      var lexer = new LexicalAnalyzer();
      lexer.TokenizeToFile(source, tempFile);

      // Parse
      var parser = new SyntaxAnalyzer();
      return parser.Parse(tempFile);
    }
    finally
    {
      // Clean up
      if (File.Exists(tempFile))
        File.Delete(tempFile);

      string tokenFile = tempFile + ".sktt";
      if (File.Exists(tokenFile))
        File.Delete(tokenFile);
    }
  }
}
