namespace skt.Shared;

/// <summary>
/// P-Code instruction types for stack-based virtual machine
/// </summary>
public enum PCodeOperation
{
  // Stack operations
  LIT,    // Load literal: LIT 0, value - push value onto stack
  LOD,    // Load variable: LOD level, offset - push variable value
  STO,    // Store: STO level, offset - pop and store in variable
  
  // Arithmetic operations
  ADD,    // Add: pop two values, push sum
  SUB,    // Subtract: pop two values, push difference
  MUL,    // Multiply: pop two values, push product
  DIV,    // Divide: pop two values, push quotient
  MOD,    // Modulo: pop two values, push remainder
  NEG,    // Negate: pop value, push -value
  
  // Comparison operations
  EQL,    // Equal: pop two values, push 1 if equal, 0 otherwise
  NEQ,    // Not equal
  LSS,    // Less than
  LEQ,    // Less or equal
  GTR,    // Greater than
  GEQ,    // Greater or equal
  
  // Logical operations
  AND,    // Logical AND
  OR,     // Logical OR
  NOT,    // Logical NOT
  
  // Control flow
  JMP,    // Unconditional jump: JMP 0, address
  JPC,    // Jump if false: JPC 0, address - pop value, jump if 0
  
  // Procedure/Function operations
  CAL,    // Call procedure: CAL level, address
  INT,    // Increment stack pointer: INT 0, size
  RET,    // Return from procedure
  
  // I/O operations
  RED,    // Read integer: pop address, read and store
  RDB,    // Read boolean: read true/false/1/0 and store
  RDS,    // Read string: read string and store string index
  WRT,    // Write integer: pop and write value
  WRS,    // Write string: pop string reference and write
  WRL,    // Write line: write newline
  
  // Special operations
  HLT,    // Halt execution
  NOP     // No operation
}

/// <summary>
/// Represents a single P-Code instruction
/// </summary>
[Serializable]
public class PCodeInstruction
{
  public PCodeOperation Op { get; set; }
  public int Level { get; set; }      // Lexical level (usually 0 for global)
  public int Operand { get; set; }    // Address, offset, or value
  public string? Comment { get; set; } // Optional comment
  public int SourceLine { get; set; }  // Original source line
  
  public PCodeInstruction(PCodeOperation op, int level, int operand, string? comment = null)
  {
    Op = op;
    Level = level;
    Operand = operand;
    Comment = comment;
  }
  
  public override string ToString()
  {
    var instruction = $"{Op,-6} {Level,2} {Operand,6}";
    if (!string.IsNullOrEmpty(Comment))
      instruction += $"  ; {Comment}";
    return instruction;
  }
  
  public string ToCompactString()
  {
    return $"{Op}|{Level}|{Operand}|{SourceLine}|{Comment ?? ""}";
  }
  
  public static PCodeInstruction FromCompactString(string line)
  {
    var parts = line.Split('|');
    if (parts.Length < 4)
      throw new FormatException($"Invalid P-Code format: {line}");
    
    var instruction = new PCodeInstruction(
      Enum.Parse<PCodeOperation>(parts[0]),
      int.Parse(parts[1]),
      int.Parse(parts[2]),
      parts.Length > 4 ? parts[4] : null
    );
    
    if (parts.Length > 3)
      instruction.SourceLine = int.Parse(parts[3]);
    
    return instruction;
  }
}

/// <summary>
/// P-Code program with string table
/// </summary>
[Serializable]
public class PCodeProgram
{
  public List<PCodeInstruction> Instructions { get; set; }
  public List<string> StringTable { get; set; }  // String literals
  public Dictionary<string, int> Labels { get; set; }
  public int DataSize { get; set; }  // Size of data area needed
  
  public PCodeProgram()
  {
    Instructions = [];
    StringTable = [];
    Labels = new Dictionary<string, int>();
    DataSize = 0;
  }
  
  public void AddInstruction(PCodeInstruction instruction)
  {
    Instructions.Add(instruction);
  }
  
  public void AddLabel(string label)
  {
    Labels[label] = Instructions.Count;
  }
  
  public int AddString(string str)
  {
    // Check if string already exists
    int index = StringTable.IndexOf(str);
    if (index >= 0)
      return index;
    
    StringTable.Add(str);
    return StringTable.Count - 1;
  }
  
  public override string ToString()
  {
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("P-Code Program:");
    sb.AppendLine(new string('=', 60));
    sb.AppendLine();
    
    if (StringTable.Count > 0)
    {
      sb.AppendLine("String Table:");
      for (int i = 0; i < StringTable.Count; i++)
      {
        sb.AppendLine($"  [{i}] = {StringTable[i]}");
      }
      sb.AppendLine();
    }
    
    sb.AppendLine($"Data Size: {DataSize}");
    sb.AppendLine();
    sb.AppendLine("Instructions:");
    
    for (int i = 0; i < Instructions.Count; i++)
    {
      // Show label if exists
      var labelName = Labels.FirstOrDefault(x => x.Value == i).Key;
      if (labelName != null)
        sb.AppendLine($"{labelName}:");
      
      sb.AppendLine($"{i,4}: {Instructions[i]}");
    }
    
    return sb.ToString();
  }
  
  /// <summary>
  /// Saves P-Code to an efficient binary file
  /// </summary>
  public void SaveToFile(string filePath)
  {
    using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
    using var writer = new BinaryWriter(stream);
    
    // Write magic number for validation
    writer.Write("PCODE".ToCharArray());
    writer.Write((byte)1); // Version
    
    // Write metadata
    writer.Write(DataSize);
    writer.Write(StringTable.Count);
    writer.Write(Instructions.Count);
    
    // Write string table
    foreach (var str in StringTable)
    {
      writer.Write(str);
    }
    
    // Write instructions
    foreach (var instruction in Instructions)
    {
      writer.Write((byte)instruction.Op);
      writer.Write(instruction.Level);
      writer.Write(instruction.Operand);
      writer.Write(instruction.SourceLine);
    }
  }
  
  /// <summary>
  /// Loads P-Code from a binary file
  /// </summary>
  public static PCodeProgram LoadFromFile(string filePath)
  {
    var program = new PCodeProgram();
    
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
    using var reader = new BinaryReader(stream);
    
    // Validate magic number
    char[] magic = reader.ReadChars(5);
    if (new string(magic) != "PCODE")
      throw new FormatException("Invalid P-Code file: wrong magic number");
    
    byte version = reader.ReadByte();
    if (version != 1)
      throw new FormatException($"Unsupported P-Code version: {version}");
    
    // Read metadata
    program.DataSize = reader.ReadInt32();
    int stringCount = reader.ReadInt32();
    int instructionCount = reader.ReadInt32();
    
    // Read string table
    for (int i = 0; i < stringCount; i++)
    {
      program.StringTable.Add(reader.ReadString());
    }
    
    // Read instructions
    for (int i = 0; i < instructionCount; i++)
    {
      var op = (PCodeOperation)reader.ReadByte();
      int level = reader.ReadInt32();
      int operand = reader.ReadInt32();
      int sourceLine = reader.ReadInt32();
      
      var instruction = new PCodeInstruction(op, level, operand)
      {
        SourceLine = sourceLine
      };
      
      program.AddInstruction(instruction);
    }
    
    return program;
  }
  
  public void SaveToReadableFile(string filePath)
  {
    File.WriteAllText(filePath, ToString());
  }
}

