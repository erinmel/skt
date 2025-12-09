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
        // Check if unary or binary
        if (node.Children.Count == 1)
          AnalyzeUnaryArithmeticOp(node);
        else
          AnalyzeBinaryArithmeticOp(node);
        break;
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
      case "branch":
        AnalyzeBranchStatement(node);
        break;
      case "while":
      case "do":
        AnalyzeWhileStatement(node);
        break;
      case ">>":
        AnalyzeCinOperator(node);
        break;
      case "<<":
        AnalyzeCoutOperator(node);
        break;
      case "cin":
        AnalyzeCinKeyword(node);
        break;
      case "cout":
        AnalyzeCoutKeyword(node);
        break;
      case "body":
        AnalyzeBody(node);
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
        else
        {
          // Record the declaration as a reference to the variable
          var symbol = _symbolTable.Lookup(varName, _currentScope);
          if (symbol != null)
          {
            symbol.AddReference(child.Line, child.Column);
          }
        }

        // Set type attribute (inherited from declaration)
        child.SetTypeAttribute(dataType, AttributePropagation.Inherited, node.Rule);
      }
    }

    node.SetTypeAttribute(dataType, AttributePropagation.None, "declaration");
  }

  private void AnalyzeAssignment(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
    {
      return;
    }

    var leftNode = node.Children[0];
    var rightNode = node.Children[1];

    // Analyze right side first (expression)
    AnalyzeNode(rightNode);

    // Check if left side is declared
    if (leftNode.Rule == "ID" && leftNode.Token != null)
    {
      string varName = leftNode.Token.Value;

      if (!_symbolTable.IsDeclared(varName, _currentScope))
      {
        // Variable not declared - set type to "error"
        ReportError(
            SemanticErrorType.UndeclaredVariable,
            $"Variable '{varName}' is used before declaration",
            leftNode.Line, leftNode.Column, leftNode.EndLine, leftNode.EndColumn,
            varName
        );
        leftNode.SetTypeAttribute("error", AttributePropagation.None, "undeclared");
        leftNode.SetValueAttribute("error", AttributePropagation.None, "undeclared");
        node.SetTypeAttribute("error", AttributePropagation.None, "undeclared");
        // Set "error" as value for undeclared variable assignment
        node.SetValueAttribute("error", AttributePropagation.Synthesized, "undeclared");
        return;
      }

      // Get the type and current value of the variable BEFORE assignment
      string? varType = _symbolTable.GetSymbolType(varName, _currentScope);
      object? oldValue = _symbolTable.GetSymbolValue(varName, _currentScope);

      // Record this as a reference to the variable
      var symbol = _symbolTable.Lookup(varName, _currentScope);
      if (symbol != null)
      {
        symbol.AddReference(leftNode.Line, leftNode.Column);
      }

      // Set the type of the left node (inherited from symbol table)
      leftNode.SetTypeAttribute(varType, AttributePropagation.Inherited, "symbol_table");

      // IMPORTANT: Show the value BEFORE assignment (not after)
      // If the variable had a value before, show it; otherwise, don't set a value attribute
      if (oldValue != null)
      {
        leftNode.SetValueAttribute(oldValue, AttributePropagation.Inherited, "symbol_table");
      }

      // Check type compatibility
      bool typesCompatible = false;
      if (varType != null && rightNode.DataType != null)
      {
        typesCompatible = AreTypesCompatible(varType, rightNode.DataType);
        if (!typesCompatible)
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

      node.SetTypeAttribute(varType, AttributePropagation.Sibling, $"{leftNode.Rule}");

      // Only update the value in the symbol table if types are compatible
      if (typesCompatible && rightNode.Value != null)
      {
        object? valueToStore = rightNode.Value;

        // Convert value to correct type if needed
        if (varType == "float" && rightNode.Value is int intVal)
        {
          // Type promotion: int to float
          valueToStore = (float)intVal;
        }

        // Update the variable's value in the symbol table
        _symbolTable.SetSymbolValue(varName, _currentScope, valueToStore);

        // Set the value on the assignment node (=) to indicate successful assignment
        node.SetValueAttribute(valueToStore, AttributePropagation.Synthesized, "assignment");
      }
      else if (!typesCompatible)
      {
        // Set "error" as value when assignment fails due to type incompatibility
        node.SetValueAttribute("error", AttributePropagation.Synthesized, "type_error");
      }
    }
    else
    {
      // Analyze left side if it's not an identifier
      AnalyzeNode(leftNode);
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

    // Check if this is string concatenation (only valid for + operator)
    bool isStringOperation = leftNode.DataType == "string" || rightNode.DataType == "string";
    
    if (isStringOperation)
    {
      if (node.Rule != "+")
      {
        ReportError(
            SemanticErrorType.InvalidOperator,
            $"String operands only valid with '+' operator, not '{node.Rule}'",
            node.Line, node.Column, node.EndLine, node.EndColumn
        );
        node.SetTypeAttribute("string", AttributePropagation.None, "error_recovery");
      }
      else
      {
        // Valid string concatenation - result is always string
        node.SetTypeAttribute("string", AttributePropagation.Synthesized, "children");
        
        // Check for constant folding with strings
        if (leftNode.IsConstant && rightNode.IsConstant && leftNode.Value != null && rightNode.Value != null)
        {
          string leftStr = leftNode.Value.ToString() ?? "";
          string rightStr = rightNode.Value.ToString() ?? "";
          node.SetValueAttribute(leftStr + rightStr, AttributePropagation.Synthesized, "constant_folding");
        }
      }
      return;
    }

    // Determine result type based on operand types (synthesized from children)
    string? resultType = InferArithmeticResultType(leftNode.DataType, rightNode.DataType);

    if (resultType == null && leftNode.DataType != null && rightNode.DataType != null)
    {
      ReportError(
          SemanticErrorType.InvalidOperator,
          $"Invalid operand types for arithmetic operator '{node.Rule}': '{leftNode.DataType}' and '{rightNode.DataType}'",
          node.Line, node.Column, node.EndLine, node.EndColumn
      );
      node.SetTypeAttribute("int", AttributePropagation.None, "error_recovery");
    }
    else
    {
      node.SetTypeAttribute(resultType ?? "int", AttributePropagation.Synthesized, "children");
    }

    // Check for constant folding - compute value if both operands are constants
    if (leftNode.IsConstant && rightNode.IsConstant && leftNode.Value != null && rightNode.Value != null)
    {
      object? computedValue = ComputeArithmeticValue(node.Rule, leftNode.Value, rightNode.Value);
      if (computedValue != null)
      {
        node.SetValueAttribute(computedValue, AttributePropagation.Synthesized, "constant_folding");
      }
    }
  }

  private void AnalyzeUnaryArithmeticOp(AnnotatedAstNode node)
  {
    if (node.Children.Count < 1)
    {
      return;
    }

    var operandNode = node.Children[0];
    AnalyzeNode(operandNode);

    // Unary +/- preserves the type of the operand
    string? resultType = operandNode.DataType;

    if (resultType != null && !IsNumericType(resultType))
    {
      ReportError(
          SemanticErrorType.InvalidOperator,
          $"Invalid operand type for unary operator '{node.Rule}': '{resultType}' (expected numeric type)",
          node.Line, node.Column, node.EndLine, node.EndColumn
      );
      node.SetTypeAttribute("int", AttributePropagation.None, "error_recovery");
    }
    else
    {
      node.SetTypeAttribute(resultType ?? "int", AttributePropagation.Synthesized, "child");
    }

    // Check for constant folding
    if (operandNode.IsConstant && operandNode.Value != null)
    {
      object? computedValue = ComputeUnaryArithmeticValue(node.Rule, operandNode.Value);
      if (computedValue != null)
      {
        node.SetValueAttribute(computedValue, AttributePropagation.Synthesized, "constant_folding");
      }
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

    // Relational operators always return bool (synthesized)
    node.SetTypeAttribute("bool", AttributePropagation.Synthesized, "relational_op");

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

    // Constant folding for relational operations
    if (leftNode.IsConstant && rightNode.IsConstant && leftNode.Value != null && rightNode.Value != null)
    {
      object? computedValue = ComputeRelationalValue(node.Rule, leftNode.Value, rightNode.Value);
      if (computedValue != null)
      {
        node.SetValueAttribute(computedValue, AttributePropagation.Synthesized, "constant_folding");
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

    node.SetTypeAttribute("bool", AttributePropagation.Synthesized, "logical_op");

    // Constant folding for logical operations
    if (node.Rule == "!" && node.Children.Count == 1)
    {
      // Unary NOT operator
      var operand = node.Children[0];
      if (operand.IsConstant && operand.Value is bool boolValue)
      {
        node.SetValueAttribute(!boolValue, AttributePropagation.Synthesized, "constant_folding");
      }
    }
    else if (node.Children.Count == 2)
    {
      // Binary logical operators (&&, ||)
      var leftNode = node.Children[0];
      var rightNode = node.Children[1];

      if (leftNode.IsConstant && rightNode.IsConstant &&
          leftNode.Value is bool leftBool && rightNode.Value is bool rightBool)
      {
        bool result = node.Rule switch
        {
          "&&" => leftBool && rightBool,
          "||" => leftBool || rightBool,
          _ => false
        };
        node.SetValueAttribute(result, AttributePropagation.Synthesized, "constant_folding");
      }
    }
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

        // Record this as a reference to the variable (++ and -- count as 2 references)
        var symbol = _symbolTable.Lookup(varName, _currentScope);
        if (symbol != null)
        {
          symbol.AddReference(operand.Line, operand.Column);  // First reference (read)
          symbol.AddReference(operand.Line, operand.Column);  // Second reference (write)
        }

        if (varType != "int" && varType != "float")
        {
          ReportError(
              SemanticErrorType.InvalidOperator,
              $"Increment/decrement operators require numeric types, found '{varType}'",
              node.Line, node.Column, node.EndLine, node.EndColumn
          );
        }

        node.SetTypeAttribute(varType, AttributePropagation.Synthesized, operand.Rule);
      }
    }
  }

  private void AnalyzeBranchStatement(AnnotatedAstNode node)
  {
    // Branch statements may affect control flow, analyze with caution
    foreach (var child in node.Children)
    {
      AnalyzeNode(child);
    }
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

  private void AnalyzeCinOperator(AnnotatedAstNode node)
  {
    // Analyze as a function call with cin as the callee
    foreach (var child in node.Children)
    {
      AnalyzeNode(child);

      // Check if this is an undeclared variable being used with cin
      if (child.Rule == "ID" && child.Token != null)
      {
        string varName = child.Token.Value;
        if (!_symbolTable.IsDeclared(varName, _currentScope))
        {
          // Variable not declared - set type and value to "error"
          child.SetTypeAttribute("error", AttributePropagation.None, "undeclared");
          child.SetValueAttribute("error", AttributePropagation.None, "undeclared");
        }
        // Note: AddReference is already called by AnalyzeIdentifier when AnalyzeNode(child) is called above
      }
    }

    // Set return type to void for cin operator
    node.SetTypeAttribute("void", AttributePropagation.Synthesized, "cin_operator");
  }

  private void AnalyzeCoutOperator(AnnotatedAstNode node)
  {
    // Analyze as a function call with cout as the callee
    foreach (var child in node.Children)
    {
      AnalyzeNode(child);
    }

    // Set return type to void for cout operator
    node.SetTypeAttribute("void", AttributePropagation.Synthesized, "cout_operator");
  }

  private void AnalyzeCinKeyword(AnnotatedAstNode node)
  {
    // Handles the 'cin' keyword node and performs variable validation.
    // Unlike AnalyzeCinOperator (which handles the '>>' operator node and does not validate variables),
    // this method checks that each variable used with 'cin' is declared.
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
          // Set type and value to "error" for undeclared variables
          child.SetTypeAttribute("error", AttributePropagation.None, "undeclared");
          child.SetValueAttribute("error", AttributePropagation.None, "undeclared");
        }
        else
        {
          // Use SetTypeAttribute for consistency
          string? varType = _symbolTable.GetSymbolType(varName, _currentScope);
          child.SetTypeAttribute(varType, AttributePropagation.Inherited, "symbol_table");

          // Record this as a reference to the variable
          var symbol = _symbolTable.Lookup(varName, _currentScope);
          if (symbol != null)
          {
            symbol.AddReference(child.Line, child.Column);
          }

          // Get and set the current value if it exists
          object? value = _symbolTable.GetSymbolValue(varName, _currentScope);
          if (value != null)
          {
            child.SetValueAttribute(value, AttributePropagation.Inherited, "symbol_table");
          }
        }
      }
      else
      {
        AnalyzeNode(child);
      }
    }
  }

  private void AnalyzeCoutKeyword(AnnotatedAstNode node)
  {
    // Similar to AnalyzeCoutOperator, but for the 'cout' keyword usage
    foreach (var child in node.Children)
    {
      AnalyzeNode(child);
    }
  }

  private void AnalyzeBody(AnnotatedAstNode node)
  {
    // Body nodes are usually blocks of statements, analyze all children
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
      node.SetTypeAttribute(symbol.DataType, AttributePropagation.Inherited, "symbol_table");

      // Record this as a reference to the variable
      symbol.AddReference(node.Line, node.Column);

      // If the variable has a value stored, propagate it
      if (symbol.Value != null)
      {
        node.SetValueAttribute(symbol.Value, AttributePropagation.Inherited, "symbol_table");
      }
    }
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

    // Check for float literals first (before int, to prioritize float detection)
    if (node.Token.Type == TokenType.Real || (value.Contains('.') && float.TryParse(value, out _)))
    {
      if (float.TryParse(value, out float floatValue))
      {
        node.SetTypeAttribute("float", AttributePropagation.None, "literal");
        node.SetValueAttribute(floatValue, AttributePropagation.None, "literal");
      }
      return true;
    }

    // Check for integer literals
    if (node.Token.Type == TokenType.Integer || int.TryParse(value, out _))
    {
      if (int.TryParse(value, out int intValue))
      {
        node.SetTypeAttribute("int", AttributePropagation.None, "literal");
        node.SetValueAttribute(intValue, AttributePropagation.None, "literal");
      }
      return true;
    }

    // Check for string literals
    if (node.Token.Type == TokenType.String)
    {
      node.SetTypeAttribute("string", AttributePropagation.None, "literal");
      node.SetValueAttribute(value, AttributePropagation.None, "literal");
      return true;
    }

    // Check for boolean literals
    if (node.Token.Type == TokenType.Boolean || value == "true" || value == "false")
    {
      node.SetTypeAttribute("bool", AttributePropagation.None, "literal");
      node.SetValueAttribute(value == "true", AttributePropagation.None, "literal");
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

  private static object? ComputeArithmeticValue(string op, object? left, object? right)
  {
    // For now, only supports int and float, and basic ops
    if (left is int leftInt && right is int rightInt)
    {
      return op switch
      {
        "+" => leftInt + rightInt,
        "-" => leftInt - rightInt,
        "*" => leftInt * rightInt,
        "/" => leftInt / rightInt,
        "%" => leftInt % rightInt,
        "^" => (int)Math.Pow(leftInt, rightInt),
        _ => null
      };
    }

    if (left is float leftFloat && right is float rightFloat)
    {
      return op switch
      {
        "+" => leftFloat + rightFloat,
        "-" => leftFloat - rightFloat,
        "*" => leftFloat * rightFloat,
        "/" => leftFloat / rightFloat,
        "%" => leftFloat % rightFloat,
        "^" => (float)Math.Pow(leftFloat, rightFloat),
        _ => null
      };
    }

    if (left is int intValue && right is float floatValue)
    {
      return op switch
      {
        "+" => intValue + floatValue,
        "-" => intValue - floatValue,
        "*" => intValue * floatValue,
        "/" => intValue / floatValue,
        "%" => intValue % floatValue,
        "^" => (float)Math.Pow(intValue, floatValue),
        _ => null
      };
    }

    if (left is float floatValue2 && right is int intValue2)
    {
      return op switch
      {
        "+" => floatValue2 + intValue2,
        "-" => floatValue2 - intValue2,
        "*" => floatValue2 * intValue2,
        "/" => floatValue2 / intValue2,
        "%" => floatValue2 % intValue2,
        "^" => (float)Math.Pow(floatValue2, intValue2),
        _ => null
      };
    }

    return null;
  }

  private static object? ComputeUnaryArithmeticValue(string op, object? operand)
  {
    if (op == "-")
    {
      if (operand is int intValue)
        return -intValue;
      if (operand is float floatValue)
        return -floatValue;
    }
    else if (op == "+")
    {
      // Unary + does nothing
      return operand;
    }

    return null;
  }

  private static object? ComputeRelationalValue(string op, object? left, object? right)
  {
    if (left is null || right is null)
      return null;

    // For now, only supports int and float, and basic relational ops
    if (left is int leftInt && right is int rightInt)
    {
      return op switch
      {
        "<" => leftInt < rightInt,
        "<=" => leftInt <= rightInt,
        ">" => leftInt > rightInt,
        ">=" => leftInt >= rightInt,
        "==" => leftInt == rightInt,
        "!=" => leftInt != rightInt,
        _ => null
      };
    }

    if (left is float leftFloat && right is float rightFloat)
    {
      return op switch
      {
        "<" => leftFloat < rightFloat,
        "<=" => leftFloat <= rightFloat,
        ">" => leftFloat > rightFloat,
        ">=" => leftFloat >= rightFloat,
        "==" => leftFloat == rightFloat,
        "!=" => leftFloat != rightFloat,
        _ => null
      };
    }

    if (left is int intValue && right is float floatValue)
    {
      return op switch
      {
        "<" => intValue < floatValue,
        "<=" => intValue <= floatValue,
        ">" => intValue > floatValue,
        ">=" => intValue >= floatValue,
        "==" => intValue == floatValue,
        "!=" => intValue != floatValue,
        _ => null
      };
    }

    if (left is float floatValue2 && right is int intValue2)
    {
      return op switch
      {
        "<" => floatValue2 < intValue2,
        "<=" => floatValue2 <= intValue2,
        ">" => floatValue2 > intValue2,
        ">=" => floatValue2 >= intValue2,
        "==" => floatValue2 == intValue2,
        "!=" => floatValue2 != intValue2,
        _ => null
      };
    }

    return null;
  }
}
