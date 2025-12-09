using skt.Shared;

namespace skt.Compiler;

/// <summary>
/// Generates P-Code (stack-based bytecode) from annotated AST
/// </summary>
public class PCodeGenerator
{
  private PCodeProgram _program = new();
  private readonly Dictionary<string, int> _variableOffsets = new();
  private readonly Dictionary<string, string> _variableTypes = new(); // Track variable data types
  private int _nextOffset = 0;
  private int _labelCounter = 0;
  
  public PCodeProgram Generate(AnnotatedAstNode? ast)
  {
    _program = new PCodeProgram();
    _variableOffsets.Clear();
    _variableTypes.Clear();
    _nextOffset = 0;
    _labelCounter = 0;
    
    if (ast == null)
      return _program;
    
    // Generate code
    GenerateNode(ast);
    
    // Add HALT at the end
    Emit(PCodeOperation.HLT, 0, 0, "end of program");
    
    _program.DataSize = _nextOffset;
    return _program;
  }
  
  private void GenerateNode(AnnotatedAstNode node)
  {
    if (node.IsError)
      return;
    
    switch (node.Rule)
    {
      case "program":
        GenerateProgram(node);
        break;
      
      case "int":
      case "float":
      case "bool":
        GenerateDeclaration(node);
        break;
      
      case "string" when node.Children.Count > 0:
        // string type declaration (has children - the IDs)
        GenerateDeclaration(node);
        break;
      
      case "=":
        GenerateAssignment(node);
        break;
      
      case "+=":
      case "-=":
      case "*=":
      case "/=":
      case "%=":
      case "^=":
        GenerateCompoundAssignment(node);
        break;
      
      case "+":
      case "-":
        // Check if unary or binary based on number of children
        if (node.Children.Count == 1)
          GenerateUnaryOp(node);
        else
          GenerateBinaryOp(node);
        break;
      
      case "*":
      case "/":
      case "%":
      case "^":
        GenerateBinaryOp(node);
        break;
      
      case "<":
      case "<=":
      case ">":
      case ">=":
      case "==":
      case "!=":
        GenerateRelationalOp(node);
        break;
      
      case "&&":
      case "||":
        GenerateLogicalOp(node);
        break;
      
      case "!":
        GenerateNotOp(node);
        break;
      
      case "++":
      case "--":
        GenerateIncrementDecrement(node);
        break;
      
      case "branch":
        GenerateBranch(node);
        break;
      
      case "while":
        GenerateWhile(node);
        break;
      
      case "do":
        GenerateDoWhile(node);
        break;
      
      case "cin":
        GenerateCin(node);
        break;
      
      case "cout":
        GenerateCout(node);
        break;
      
      case ">>":
        GenerateCinOperator(node);
        break;
      
      case "<<":
        GenerateCoutOperator(node);
        break;
      
      case "literal":
        GenerateLiteral(node);
        break;
      
      case "string":
        GenerateString(node);
        break;
      
      case "boolean":
        GenerateBoolean(node);
        break;
      
      case "ID":
        GenerateIdentifier(node);
        break;
      
      case "body":
        GenerateBody(node);
        break;
      
      default:
        // Process children
        foreach (var child in node.Children)
          GenerateNode(child);
        break;
    }
  }
  
  private void GenerateProgram(AnnotatedAstNode node)
  {
    EmitComment("Program start");
    
    foreach (var child in node.Children)
      GenerateNode(child);
    
    EmitComment("Program end");
  }
  
  private void GenerateDeclaration(AnnotatedAstNode node)
  {
    string dataType = node.Rule;

    foreach (var child in node.Children)
    {
      if (child.Rule == "ID" && child.Token != null)
      {
        string varName = child.Token.Value;
        
        // Allocate space for variable
        if (!_variableOffsets.ContainsKey(varName))
        {
          _variableOffsets[varName] = _nextOffset++;
          _variableTypes[varName] = dataType; // Store the type
          EmitComment($"Declare {dataType} {varName} at offset {_variableOffsets[varName]}");
        }
        
        // Check if there's an initialization
        if (child.Children.Count > 0)
        {
          // Generate code for initialization expression
          GenerateNode(child.Children[0]);
          
          // Store in variable
          int offset = _variableOffsets[varName];
          Emit(PCodeOperation.STO, 0, offset, $"store {varName}");
        }
      }
    }
  }
  
  private void GenerateAssignment(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
      return;
    
    var leftChild = node.Children[0];
    var rightChild = node.Children[1];
    
    if (leftChild.Rule == "ID" && leftChild.Token != null)
    {
      string varName = leftChild.Token.Value;
      
      // Generate code for right side (pushes value onto stack)
      GenerateNode(rightChild);
      
      // Store in variable
      if (_variableOffsets.TryGetValue(varName, out int offset))
      {
        Emit(PCodeOperation.STO, 0, offset, $"store {varName}");
      }
    }
  }
  
  private void GenerateCompoundAssignment(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
      return;
    
    var leftChild = node.Children[0];
    var rightChild = node.Children[1];
    
    if (leftChild.Rule == "ID" && leftChild.Token != null)
    {
      string varName = leftChild.Token.Value;
      
      if (_variableOffsets.TryGetValue(varName, out int offset))
      {
        // Load current value
        Emit(PCodeOperation.LOD, 0, offset, $"load {varName}");
        
        // Generate right side
        GenerateNode(rightChild);
        
        // Perform operation
        var op = node.Rule switch
        {
          "+=" => PCodeOperation.ADD,
          "-=" => PCodeOperation.SUB,
          "*=" => PCodeOperation.MUL,
          "/=" => PCodeOperation.DIV,
          "%=" => PCodeOperation.MOD,
          _ => PCodeOperation.ADD
        };
        
        Emit(op, 0, 0, $"op {node.Rule}");
        
        // Store result
        Emit(PCodeOperation.STO, 0, offset, $"store {varName}");
      }
    }
  }
  
  private void GenerateBinaryOp(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
      return;
    
    var leftChild = node.Children[0];
    var rightChild = node.Children[1];
    
    // Check if this is string concatenation
    if (node.DataType == "string" && node.Rule == "+")
    {
      // Generate left operand (pushes to stack)
      GenerateNode(leftChild);
      
      // Convert left operand to string if needed
      if (leftChild.DataType != "string")
      {
        EmitTypeToStringConversion(leftChild.DataType);
      }
      
      // Generate right operand (pushes to stack)
      GenerateNode(rightChild);
      
      // Convert right operand to string if needed
      if (rightChild.DataType != "string")
      {
        EmitTypeToStringConversion(rightChild.DataType);
      }
      
      // Concatenate strings
      Emit(PCodeOperation.CONCAT, 0, 0, "concatenate strings");
      return;
    }
    
    // Generate left operand (pushes to stack)
    GenerateNode(leftChild);
    
    // Convert left operand if needed (int to float)
    if (node.DataType == "float" && leftChild.DataType == "int")
    {
      Emit(PCodeOperation.I2F, 0, 0, "convert int to float");
    }
    
    // Generate right operand (pushes to stack)
    GenerateNode(rightChild);
    
    // Convert right operand if needed (int to float)
    if (node.DataType == "float" && rightChild.DataType == "int")
    {
      Emit(PCodeOperation.I2F, 0, 0, "convert int to float");
    }
    
    // Determine if we're doing float or int operation
    bool isFloatOp = node.DataType == "float" || leftChild.DataType == "float" || rightChild.DataType == "float";
    
    // Perform operation (pops two, pushes result)
    PCodeOperation op;
    if (isFloatOp)
    {
      // Use float operations
      op = node.Rule switch
      {
        "+" => PCodeOperation.FADD,
        "-" => PCodeOperation.FSUB,
        "*" => PCodeOperation.FMUL,
        "/" => PCodeOperation.FDIV,
        "^" => PCodeOperation.FPOW,
        _ => PCodeOperation.FADD
      };
    }
    else
    {
      // Use integer operations
      op = node.Rule switch
      {
        "+" => PCodeOperation.ADD,
        "-" => PCodeOperation.SUB,
        "*" => PCodeOperation.MUL,
        "/" => PCodeOperation.DIV,
        "%" => PCodeOperation.MOD,
        "^" => PCodeOperation.POW,
        _ => PCodeOperation.ADD
      };
    }
    
    Emit(op, 0, 0, $"op {node.Rule}");
  }
  
  private void EmitTypeToStringConversion(string? dataType)
  {
    switch (dataType)
    {
      case "int":
        Emit(PCodeOperation.I2S, 0, 0, "convert int to string");
        break;
      case "float":
        Emit(PCodeOperation.F2S, 0, 0, "convert float to string");
        break;
      case "bool":
        Emit(PCodeOperation.B2S, 0, 0, "convert bool to string");
        break;
      // string doesn't need conversion
    }
  }
  
  private void GenerateRelationalOp(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
      return;
    
    var leftChild = node.Children[0];
    var rightChild = node.Children[1];
    
    // Generate left operand
    GenerateNode(leftChild);
    
    // Convert left operand if needed (int to float)
    if (rightChild.DataType == "float" && leftChild.DataType == "int")
    {
      Emit(PCodeOperation.I2F, 0, 0, "convert int to float");
    }
    
    // Generate right operand
    GenerateNode(rightChild);
    
    // Convert right operand if needed (int to float)
    if (leftChild.DataType == "float" && rightChild.DataType == "int")
    {
      Emit(PCodeOperation.I2F, 0, 0, "convert int to float");
    }
    
    // Determine if we're doing float or int comparison
    bool isFloatOp = leftChild.DataType == "float" || rightChild.DataType == "float";
    
    // Perform comparison
    PCodeOperation op;
    if (isFloatOp)
    {
      op = node.Rule switch
      {
        "<" => PCodeOperation.FLSS,
        "<=" => PCodeOperation.FLEQ,
        ">" => PCodeOperation.FGTR,
        ">=" => PCodeOperation.FGEQ,
        "==" => PCodeOperation.FEQL,
        "!=" => PCodeOperation.FNEQ,
        _ => PCodeOperation.FEQL
      };
    }
    else
    {
      op = node.Rule switch
      {
        "<" => PCodeOperation.LSS,
        "<=" => PCodeOperation.LEQ,
        ">" => PCodeOperation.GTR,
        ">=" => PCodeOperation.GEQ,
        "==" => PCodeOperation.EQL,
        "!=" => PCodeOperation.NEQ,
        _ => PCodeOperation.EQL
      };
    }
    
    Emit(op, 0, 0, $"compare {node.Rule}");
  }
  
  private void GenerateLogicalOp(AnnotatedAstNode node)
  {
    if (node.Children.Count < 2)
      return;
    
    GenerateNode(node.Children[0]);
    GenerateNode(node.Children[1]);
    
    var op = node.Rule switch
    {
      "&&" => PCodeOperation.AND,
      "||" => PCodeOperation.OR,
      _ => PCodeOperation.AND
    };
    
    Emit(op, 0, 0, $"logical {node.Rule}");
  }
  
  private void GenerateNotOp(AnnotatedAstNode node)
  {
    if (node.Children.Count < 1)
      return;
    
    GenerateNode(node.Children[0]);
    Emit(PCodeOperation.NOT, 0, 0, "logical not");
  }
  
  private void GenerateUnaryOp(AnnotatedAstNode node)
  {
    if (node.Children.Count < 1)
      return;
    
    var child = node.Children[0];
    
    // Generate the operand
    GenerateNode(child);
    
    // Apply unary operation
    if (node.Rule == "-")
    {
      // Use FNEG for float, NEG for int
      bool isFloat = node.DataType == "float" || child.DataType == "float";
      if (isFloat)
      {
        Emit(PCodeOperation.FNEG, 0, 0, "unary negate float");
      }
      else
      {
        Emit(PCodeOperation.NEG, 0, 0, "unary negate int");
      }
    }
    // Unary + does nothing
  }
  
  private void GenerateIncrementDecrement(AnnotatedAstNode node)
  {
    if (node.Children.Count < 1)
      return;
    
    var child = node.Children[0];
    if (child.Rule == "ID" && child.Token != null)
    {
      string varName = child.Token.Value;
      
      if (_variableOffsets.TryGetValue(varName, out int offset))
      {
        // Load variable
        Emit(PCodeOperation.LOD, 0, offset, $"load {varName}");
        
        // Push 1
        Emit(PCodeOperation.LIT, 0, 1, "push 1");
        
        // Add or subtract
        if (node.Rule == "++")
          Emit(PCodeOperation.ADD, 0, 0, "increment");
        else
          Emit(PCodeOperation.SUB, 0, 0, "decrement");
        
        // Store back
        Emit(PCodeOperation.STO, 0, offset, $"store {varName}");
      }
    }
  }
  
  private void GenerateBranch(AnnotatedAstNode node)
  {
    // if condition then_part [else_part]
    if (node.Children.Count < 2)
      return;
    
    string elseLabel = NewLabel();
    string endLabel = NewLabel();
    
    // Generate condition (pushes 0 or 1)
    GenerateNode(node.Children[0]);
    
    // Jump to else if false
    Emit(PCodeOperation.JPC, 0, 0, $"jump to {elseLabel} if false");
    int elseJumpAddr = _program.Instructions.Count - 1;
    
    // Generate then part
    GenerateNode(node.Children[1]);
    
    // Jump to end
    Emit(PCodeOperation.JMP, 0, 0, $"jump to {endLabel}");
    int endJumpAddr = _program.Instructions.Count - 1;
    
    // Else label
    _program.Instructions[elseJumpAddr].Operand = _program.Instructions.Count;
    
    // Generate else part if exists
    if (node.Children.Count > 2)
    {
      GenerateNode(node.Children[2]);
    }
    
    // End label
    _program.Instructions[endJumpAddr].Operand = _program.Instructions.Count;
  }
  
  private void GenerateWhile(AnnotatedAstNode node)
  {
    // while condition body
    if (node.Children.Count < 2)
      return;
    
    string startLabel = NewLabel();
    string endLabel = NewLabel();
    
    // Start of loop
    int loopStart = _program.Instructions.Count;
    
    // Generate condition
    GenerateNode(node.Children[0]);
    
    // Jump to end if false
    Emit(PCodeOperation.JPC, 0, 0, $"exit loop {endLabel}");
    int exitJumpAddr = _program.Instructions.Count - 1;
    
    // Generate body
    GenerateNode(node.Children[1]);
    
    // Jump back to start
    Emit(PCodeOperation.JMP, 0, loopStart, $"loop back to {startLabel}");
    
    // Fix exit jump
    _program.Instructions[exitJumpAddr].Operand = _program.Instructions.Count;
  }
  
  private void GenerateDoWhile(AnnotatedAstNode node)
  {
    // do body while condition
    if (node.Children.Count < 2)
      return;
    
    string startLabel = NewLabel();
    
    // Start of loop
    int loopStart = _program.Instructions.Count;
    
    // Generate body
    GenerateNode(node.Children[0]);
    
    // Generate condition (evaluates to 1 if true, 0 if false)
    GenerateNode(node.Children[1]);
    
    // JPC jumps if top of stack is 0 (false)
    // So we jump to exit if condition is false
    Emit(PCodeOperation.JPC, 0, 0, $"exit loop if false");
    int exitJumpAddr = _program.Instructions.Count - 1;
    
    // Condition is true, jump back to start
    Emit(PCodeOperation.JMP, 0, loopStart, $"loop back to {startLabel}");
    
    // Fix exit jump address
    _program.Instructions[exitJumpAddr].Operand = _program.Instructions.Count;
  }
  
  private void GenerateCin(AnnotatedAstNode node)
  {
    foreach (var child in node.Children)
    {
      if (child.Rule == "ID" && child.Token != null)
      {
        string varName = child.Token.Value;
        
        if (_variableOffsets.TryGetValue(varName, out int offset))
        {
          // Get variable type to determine which read instruction to use
          string varType = _variableTypes.GetValueOrDefault(varName, "int");
          
          // Generate appropriate read instruction based on type
          PCodeOperation readOp = varType switch
          {
            "bool" => PCodeOperation.RDB,
            "float" => PCodeOperation.RDF,
            "string" => PCodeOperation.RDS,
            _ => PCodeOperation.RED // int
          };
          
          Emit(readOp, 0, 0, $"read {varType} {varName}");
          
          // Store in variable
          Emit(PCodeOperation.STO, 0, offset, $"store {varName}");
        }
      }
      else if (child.Rule == ">>" || child.Rule == "cin")
      {
        GenerateNode(child);
      }
    }
  }
  
  private void GenerateCout(AnnotatedAstNode node)
  {
    foreach (var child in node.Children)
    {
      if (child.Rule == "<<" || child.Rule == "cout")
      {
        GenerateNode(child);
      }
      else if (child.Rule == "string" && child.Token != null)
      {
        // String literal
        string str = child.Token.Value.Trim('"');
        
        // Handle escape sequences
        str = str.Replace("\\n", "\n")
                 .Replace("\\t", "\t")
                 .Replace("\\r", "\r")
                 .Replace("\\\"", "\"");
        
        // Add to string table
        int strIndex = _program.AddString(str);
        
        // Push string index and write
        Emit(PCodeOperation.LIT, 0, strIndex, "push string index");
        Emit(PCodeOperation.WRS, 0, 0, $"write string");
      }
      else
      {
        // Check variable type for correct write instruction
        string? varType = null;
        if (child.Rule == "ID" && child.Token != null)
        {
          string varName = child.Token.Value;
          _variableTypes.TryGetValue(varName, out varType);
        }
        
        // If not a variable, check the expression's DataType
        if (varType == null)
        {
          varType = child.DataType ?? "int";
        }
        
        // Expression - generate code (pushes value)
        GenerateNode(child);
        
        // Write value - use appropriate instruction based on type
        if (varType == "string")
        {
          Emit(PCodeOperation.WRS, 0, 0, "write string");
        }
        else if (varType == "float")
        {
          Emit(PCodeOperation.WRTF, 0, 0, "write float");
        }
        else
        {
          Emit(PCodeOperation.WRT, 0, 0, "write int");
        }
      }
    }
  }
  
  private void GenerateCinOperator(AnnotatedAstNode node)
  {
    // >> operator - process children for reading
    foreach (var child in node.Children)
    {
      if (child.Rule == "ID" && child.Token != null)
      {
        string varName = child.Token.Value;
        
        if (_variableOffsets.TryGetValue(varName, out int offset))
        {
          // Get variable type
          string varType = _variableTypes.GetValueOrDefault(varName, "int");
          
          // Generate appropriate read instruction
          PCodeOperation readOp = varType switch
          {
            "bool" => PCodeOperation.RDB,
            "float" => PCodeOperation.RDF,
            "string" => PCodeOperation.RDS,
            _ => PCodeOperation.RED
          };
          
          Emit(readOp, 0, 0, $"read {varType} {varName}");
          Emit(PCodeOperation.STO, 0, offset, $"store {varName}");
        }
      }
      else if (child.Rule == ">>" || child.Rule == "cin")
      {
        GenerateNode(child);
      }
    }
  }
  
  private void GenerateCoutOperator(AnnotatedAstNode node)
  {
    GenerateCout(node);
  }
  
  private void GenerateLiteral(AnnotatedAstNode node)
  {
    if (node.Token == null)
      return;
    
    string value = node.Token.Value;
    
    if (int.TryParse(value, out int intValue))
    {
      Emit(PCodeOperation.LIT, 0, intValue, $"push {intValue}");
    }
    else if (double.TryParse(value, out double doubleValue))
    {
      // Store float value in string table and use LITF instruction
      int strIndex = _program.AddString(value);
      Emit(PCodeOperation.LITF, 0, strIndex, $"push {doubleValue}");
    }
    else
    {
      Emit(PCodeOperation.LIT, 0, 0, $"push {value}");
    }
  }
  
  private void GenerateString(AnnotatedAstNode node)
  {
    if (node.Token == null)
      return;
    
    string str = node.Token.Value.Trim('"');
    str = str.Replace("\\n", "\n")
             .Replace("\\t", "\t")
             .Replace("\\r", "\r")
             .Replace("\\\"", "\"");
    
    int strIndex = _program.AddString(str);
    Emit(PCodeOperation.LIT, 0, strIndex, $"push string index");
  }
  
  private void GenerateBoolean(AnnotatedAstNode node)
  {
    if (node.Token == null)
      return;
    
    int value = node.Token.Value.ToLower() == "true" ? 1 : 0;
    Emit(PCodeOperation.LIT, 0, value, $"push {node.Token.Value}");
  }
  
  private void GenerateIdentifier(AnnotatedAstNode node)
  {
    if (node.Token == null)
      return;
    
    string varName = node.Token.Value;
    
    if (_variableOffsets.TryGetValue(varName, out int offset))
    {
      Emit(PCodeOperation.LOD, 0, offset, $"load {varName}");
    }
  }
  
  private void GenerateBody(AnnotatedAstNode node)
  {
    foreach (var child in node.Children)
      GenerateNode(child);
  }
  
  private void Emit(PCodeOperation op, int level, int operand, string? comment = null)
  {
    var instruction = new PCodeInstruction(op, level, operand, comment);
    _program.AddInstruction(instruction);
  }
  
  private void EmitComment(string comment)
  {
    // Comments are not emitted as instructions, only used for debugging
    // They will appear in the readable output via instruction comments
  }
  
  private string NewLabel()
  {
    return $"L{_labelCounter++}";
  }
}

