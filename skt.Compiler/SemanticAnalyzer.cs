using skt.Shared;

namespace skt.Compiler;

/// <summary>
/// Performs semantic analysis on the AST, checking types, declarations, and building symbol table
/// </summary>
public class SemanticAnalyzer
{
  private readonly SymbolTable _symbolTable = new();
  private readonly List<SemanticError> _errors = [];
  private string _currentScope = "global";
  private int _scopeCounter = 0;

  /// <summary>
  /// Analyzes the AST and returns an annotated AST with semantic information
  /// </summary>
  public (AnnotatedAstNode? annotatedAst, SymbolTable symbolTable, List<SemanticError> errors) Analyze(AstNode? ast)
  {
    _errors.Clear();
    _symbolTable.Clear();
    _currentScope = "global";

    if (ast == null)
    {
      return (null, _symbolTable, _errors);
    }

    var annotatedAst = AnnotatedAstNode.FromAstNode(ast);
    AnalyzeNode(annotatedAst);

    return (annotatedAst, _symbolTable, _errors);
  }

  /// <summary>
  /// Main analysis method that processes each node recursively
  /// </summary>
  private void AnalyzeNode(AnnotatedAstNode node)
  {
    if (node.IsError)
    {
      return; // Skip error nodes from parsing phase
    }

    node.Scope = _currentScope;

    // Process based on rule type
    switch (node.Rule)
    {
      case "program":
        AnalyzeProgram(node);
        break;
      case "int":
      case "float":
      case "bool":
      case "string":
        AnalyzeDeclaration(node);
        break;
      case "literal":
      case "boolean":
        AnalyzeLiteral(node);
        break;
      case "=":
      case "+=":
      case "-=":
      case "*=":
      case "/=":
      case "%=":
      case "^=":
        AnalyzeAssignment(node);
        break;
      case "+":
      case "-":
      case "*":
      case "/":
      case "%":
      case "^":
        AnalyzeBinaryArithmeticOp(node);
        break;
      case "<":
      case "<=":
      case ">":
      case ">=":
      case "==":
      case "!=":
        AnalyzeRelationalOp(node);
        break;
      case "&&":
      case "||":
      case "!":
        AnalyzeLogicalOp(node);
        break;
      case "++":
      case "--":
        AnalyzeIncrementDecrement(node);
        break;
      case "if":
        AnalyzeIfStatement(node);
        break;
      case "while":
      case "do":
        AnalyzeWhileStatement(node);
        break;
      case "cin":
        AnalyzeCinStatement(node);
        break;
      case "cout":
        AnalyzeCoutStatement(node);
        break;
      case "ID":
        AnalyzeIdentifier(node);
        break;
      default:
        // For other nodes, just recursively analyze children
        foreach (var child in node.Children)
        {
          AnalyzeNode(child);
        }
        break;
    }
  }

  private void AnalyzeProgram(AnnotatedAstNode node)
  {
    // Process all children (statements) in the global scope
    foreach (var child in node.Children)
    {
      AnalyzeNode(child);
    }
  }

  private void AnalyzeDeclaration(AnnotatedAstNode node)
  {
    string dataType = node.Rule; // int, float, bool, string

    // Process each identifier being declared
    foreach (var child in node.Children)
    {
      if (child.Rule == "ID" && child.Token != null)
      {
        string varName = child.Token.Value;

        // Try to add to symbol table
        if (!_symbolTable.AddSymbol(varName, dataType, _currentScope, child.Line, child.Column))
        {
          // Duplicate declaration
          ReportError(
              SemanticErrorType.DuplicateDeclaration,
              $"Variable '{varName}' is already declared in this scope",
              child.Line, child.Column, child.EndLine, child.EndColumn,
              varName
          );
        }

        // Set type attribute
        child.DataType = dataType;
      }
    }

    node.DataType = dataType;
  }

  private void AnalyzeAssignment(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
    {
      return;
    }

    var leftNode = node.Children[0];
    var rightNode = node.Children[1];

    // Analyze left side (should be an identifier)
    AnalyzeNode(leftNode);

    // Analyze right side (expression)
    AnalyzeNode(rightNode);

    // Check if left side is declared
    if (leftNode.Rule == "ID" && leftNode.Token != null)
    {
      string varName = leftNode.Token.Value;

      if (!_symbolTable.IsDeclared(varName, _currentScope))
      {
        ReportError(
            SemanticErrorType.UndeclaredVariable,
            $"Variable '{varName}' is used before declaration",
            leftNode.Line, leftNode.Column, leftNode.EndLine, leftNode.EndColumn,
            varName
        );
        return;
      }

      // Get the type of the variable
      string? varType = _symbolTable.GetSymbolType(varName, _currentScope);
      leftNode.DataType = varType;

      // Check type compatibility
      if (varType != null && rightNode.DataType != null)
      {
        if (!AreTypesCompatible(varType, rightNode.DataType))
        {
          ReportError(
              SemanticErrorType.TypeIncompatibility,
              $"Cannot assign '{rightNode.DataType}' to '{varType}' variable '{varName}'",
              node.Line, node.Column, node.EndLine, node.EndColumn,
              varName,
              varType,
              rightNode.DataType
          );
        }
      }

      node.DataType = varType;
    }
  }

  private void AnalyzeBinaryArithmeticOp(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
    {
      // Process all children anyway
      foreach (var child in node.Children)
      {
        AnalyzeNode(child);
      }
      return;
    }

    var leftNode = node.Children[0];
    var rightNode = node.Children[1];

    AnalyzeNode(leftNode);
    AnalyzeNode(rightNode);

    // Determine result type based on operand types
    string? resultType = InferArithmeticResultType(leftNode.DataType, rightNode.DataType);

    if (resultType == null && leftNode.DataType != null && rightNode.DataType != null)
    {
      ReportError(
          SemanticErrorType.InvalidOperator,
          $"Invalid operand types for arithmetic operator '{node.Rule}': '{leftNode.DataType}' and '{rightNode.DataType}'",
          node.Line, node.Column, node.EndLine, node.EndColumn
      );
      node.DataType = "int"; // Default to avoid cascading errors
    }
    else
    {
      node.DataType = resultType ?? "int";
    }

    // Check for constant folding
    if (leftNode.IsConstant && rightNode.IsConstant)
    {
      node.IsConstant = true;
      // Could calculate value here for optimization
    }
  }

  private void AnalyzeRelationalOp(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
    {
      return;
    }

    var leftNode = node.Children[0];
    var rightNode = node.Children[1];

    AnalyzeNode(leftNode);
    AnalyzeNode(rightNode);

    // Relational operators always return bool
    node.DataType = "bool";

    // Check if operands are comparable
    if (leftNode.DataType != null && rightNode.DataType != null)
    {
      if (!AreTypesComparable(leftNode.DataType, rightNode.DataType))
      {
        ReportError(
            SemanticErrorType.InvalidOperator,
            $"Cannot compare '{leftNode.DataType}' with '{rightNode.DataType}'",
            node.Line, node.Column, node.EndLine, node.EndColumn
        );
      }
    }
  }

  private void AnalyzeLogicalOp(AnnotatedAstNode node)
  {
    foreach (var child in node.Children)
    {
      AnalyzeNode(child);

      if (child.DataType != "bool" && child.DataType != null)
      {
        ReportError(
            SemanticErrorType.TypeIncompatibility,
            $"Logical operator requires boolean operands, found '{child.DataType}'",
            child.Line, child.Column, child.EndLine, child.EndColumn
        );
      }
    }

    node.DataType = "bool";
  }

  private void AnalyzeIncrementDecrement(AnnotatedAstNode node)
  {
    if (node.Children.Count > 0)
    {
      var operand = node.Children[0];
      AnalyzeNode(operand);

      if (operand.Rule == "ID" && operand.Token != null)
      {
        string varName = operand.Token.Value;

        if (!_symbolTable.IsDeclared(varName, _currentScope))
        {
          ReportError(
              SemanticErrorType.UndeclaredVariable,
              $"Variable '{varName}' is used before declaration",
              operand.Line, operand.Column, operand.EndLine, operand.EndColumn,
              varName
          );
          return;
        }

        string? varType = _symbolTable.GetSymbolType(varName, _currentScope);

        if (varType != "int" && varType != "float")
        {
          ReportError(
              SemanticErrorType.InvalidOperator,
              $"Increment/decrement operators require numeric types, found '{varType}'",
              node.Line, node.Column, node.EndLine, node.EndColumn
          );
        }

        node.DataType = varType;
      }
    }
  }

  private void AnalyzeIfStatement(AnnotatedAstNode node)
  {
    AnalyzeWithScope(node, "if");
  }

  private void AnalyzeWhileStatement(AnnotatedAstNode node)
  {
    AnalyzeWithScope(node, "while");
  }

  /// <summary>
  /// Analyzes a node within a new scope
  /// </summary>
  private void AnalyzeWithScope(AnnotatedAstNode node, string scopePrefix)
  {
    string previousScope = _currentScope;
    _currentScope = $"{scopePrefix}_{_scopeCounter++}";

    foreach (var child in node.Children)
    {
      AnalyzeNode(child);
    }

    _currentScope = previousScope;
  }

  private void AnalyzeCinStatement(AnnotatedAstNode node)
  {
    foreach (var child in node.Children)
    {
      if (child.Rule == "ID" && child.Token != null)
      {
        string varName = child.Token.Value;

        if (!_symbolTable.IsDeclared(varName, _currentScope))
        {
          ReportError(
              SemanticErrorType.UndeclaredVariable,
              $"Variable '{varName}' is used before declaration",
              child.Line, child.Column, child.EndLine, child.EndColumn,
              varName
          );
        }
        else
        {
          child.DataType = _symbolTable.GetSymbolType(varName, _currentScope);
        }
      }
      else
      {
        AnalyzeNode(child);
      }
    }
  }

  private void AnalyzeCoutStatement(AnnotatedAstNode node)
  {
    foreach (var child in node.Children)
    {
      AnalyzeNode(child);
    }
  }

  private void AnalyzeIdentifier(AnnotatedAstNode node)
  {
    if (node.Token == null) return;

    string varName = node.Token.Value;

    // Try to process as literal first
    if (TryProcessLiteral(node, varName))
    {
      return;
    }

    // Look up in symbol table for identifiers
    var symbol = _symbolTable.Lookup(varName, _currentScope);
    if (symbol != null)
    {
      node.DataType = symbol.DataType;
    }
    // If not found and not a literal, it will be caught by assignment/usage analysis
  }

  private static void AnalyzeLiteral(AnnotatedAstNode node)
  {
    if (node.Token == null) return;

    TryProcessLiteral(node, node.Token.Value);
  }

  /// <summary>
  /// Attempts to process a node as a literal value
  /// </summary>
  /// <returns>True if the node was processed as a literal, false otherwise</returns>
  private static bool TryProcessLiteral(AnnotatedAstNode node, string value)
  {
    if (node.Token == null) return false;

    // Check for integer literals
    if (node.Token.Type == TokenType.Integer || int.TryParse(value, out _))
    {
      node.DataType = "int";
      node.IsConstant = true;
      if (int.TryParse(value, out int intValue))
      {
        node.Value = intValue;
      }
      return true;
    }

    // Check for float literals
    if (node.Token.Type == TokenType.Real || float.TryParse(value, out _))
    {
      node.DataType = "float";
      node.IsConstant = true;
      if (float.TryParse(value, out float floatValue))
      {
        node.Value = floatValue;
      }
      return true;
    }

    // Check for string literals
    if (node.Token.Type == TokenType.String)
    {
      node.DataType = "string";
      node.IsConstant = true;
      node.Value = value;
      return true;
    }

    // Check for boolean literals
    if (node.Token.Type == TokenType.Boolean || value == "true" || value == "false")
    {
      node.DataType = "bool";
      node.IsConstant = true;
      node.Value = value == "true";
      return true;
    }

    return false;
  }

  /// <summary>
  /// Checks if two types are compatible for assignment
  /// </summary>
  private static bool AreTypesCompatible(string targetType, string sourceType)
  {
    // Exact match
    if (targetType == sourceType)
      return true;

    // int can be assigned to float (promotion)
    if (targetType == "float" && sourceType == "int")
      return true;

    // No other implicit conversions allowed
    return false;
  }

  /// <summary>
  /// Checks if two types can be compared
  /// </summary>
  private static bool AreTypesComparable(string type1, string type2)
  {
    // Same types can always be compared
    if (type1 == type2)
      return true;

    // Numeric types can be compared with each other
    if (IsNumericType(type1) && IsNumericType(type2))
      return true;

    return false;
  }

  /// <summary>
  /// Infers the result type of an arithmetic operation
  /// </summary>
  private static string? InferArithmeticResultType(string? type1, string? type2)
  {
    if (type1 == null || type2 == null)
      return null;

    // Both must be numeric
    if (!IsNumericType(type1) || !IsNumericType(type2))
      return null;

    // If either is float, result is float
    if (type1 == "float" || type2 == "float")
      return "float";

    // Both are int
    return "int";
  }

  private static bool IsNumericType(string type)
  {
    return type == "int" || type == "float";
  }

  /// <summary>
  /// Reports a semantic error
  /// </summary>
  private void ReportError(SemanticErrorType errorType, string message,
                          int line, int column, int endLine, int endColumn,
                          string? variableName = null, string? expectedType = null, string? actualType = null)
  {
    var error = new SemanticError(
        errorType,
        message,
        line,
        column,
        endLine,
        endColumn,
        variableName,
        expectedType,
        actualType
    );

    _errors.Add(error);
  }
}
