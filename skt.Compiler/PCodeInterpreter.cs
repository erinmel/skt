using skt.Shared;
using System.Text;

namespace skt.Compiler;

/// <summary>
/// P-Code Stack Machine Interpreter
/// Executes P-Code programs with full support for strings, integers, and booleans
/// </summary>
public class PCodeInterpreter
{
  private readonly int[] _stack;
  private int _sp; // Stack pointer
  private int _pc; // Program counter
  private readonly PCodeProgram _program;
  private bool _running;
  private readonly StringBuilder _output;
  private Queue<string>? _inputQueue;
  
  private const int STACK_SIZE = 10000;
  
  public string Output => _output.ToString();
  public bool IsRunning => _running;
  
  public PCodeInterpreter(PCodeProgram program)
  {
    _program = program;
    _stack = new int[STACK_SIZE];
    _sp = 0;
    _pc = 0;
    _running = false;
    _output = new StringBuilder();
  }
  
  /// <summary>
  /// Executes the P-Code program
  /// </summary>
  public void Execute(string[]? inputs = null)
  {
    if (inputs != null)
    {
      _inputQueue = new Queue<string>(inputs);
    }
    
    _sp = 0;
    _pc = 0;
    _running = true;
    _output.Clear();
    
    // Allocate space for variables
    _sp = _program.DataSize;
    
    while (_running && _pc < _program.Instructions.Count)
    {
      var instruction = _program.Instructions[_pc];
      ExecuteInstruction(instruction);
    }
  }
  
  private void ExecuteInstruction(PCodeInstruction instruction)
  {
    switch (instruction.Op)
    {
      case PCodeOperation.LIT:
        // Push literal value onto stack
        Push(instruction.Operand);
        _pc++;
        break;
      
      case PCodeOperation.LOD:
        // Load variable from data area
        {
          int value = _stack[instruction.Operand];
          Push(value);
          _pc++;
        }
        break;
      
      case PCodeOperation.STO:
        // Store top of stack in variable
        {
          int value = Pop();
          _stack[instruction.Operand] = value;
          _pc++;
        }
        break;
      
      case PCodeOperation.ADD:
        {
          int b = Pop();
          int a = Pop();
          Push(a + b);
          _pc++;
        }
        break;
      
      case PCodeOperation.SUB:
        {
          int b = Pop();
          int a = Pop();
          Push(a - b);
          _pc++;
        }
        break;
      
      case PCodeOperation.MUL:
        {
          int b = Pop();
          int a = Pop();
          Push(a * b);
          _pc++;
        }
        break;
      
      case PCodeOperation.DIV:
        {
          int b = Pop();
          int a = Pop();
          if (b == 0)
          {
            _output.AppendLine("Runtime Error: Division by zero");
            _running = false;
          }
          else
          {
            Push(a / b);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.MOD:
        {
          int b = Pop();
          int a = Pop();
          if (b == 0)
          {
            _output.AppendLine("Runtime Error: Modulo by zero");
            _running = false;
          }
          else
          {
            Push(a % b);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.NEG:
        {
          int a = Pop();
          Push(-a);
          _pc++;
        }
        break;
      
      case PCodeOperation.EQL:
        {
          int b = Pop();
          int a = Pop();
          Push(a == b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.NEQ:
        {
          int b = Pop();
          int a = Pop();
          Push(a != b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.LSS:
        {
          int b = Pop();
          int a = Pop();
          Push(a < b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.LEQ:
        {
          int b = Pop();
          int a = Pop();
          Push(a <= b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.GTR:
        {
          int b = Pop();
          int a = Pop();
          Push(a > b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.GEQ:
        {
          int b = Pop();
          int a = Pop();
          Push(a >= b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.AND:
        {
          int b = Pop();
          int a = Pop();
          Push((a != 0 && b != 0) ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.OR:
        {
          int b = Pop();
          int a = Pop();
          Push((a != 0 || b != 0) ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.NOT:
        {
          int a = Pop();
          Push(a == 0 ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.JMP:
        // Unconditional jump
        _pc = instruction.Operand;
        break;
      
      case PCodeOperation.JPC:
        // Jump if false (top of stack is 0)
        {
          int condition = Pop();
          if (condition == 0)
            _pc = instruction.Operand;
          else
            _pc++;
        }
        break;
      
      case PCodeOperation.RED:
        // Read integer
        {
          // Flush console to ensure all cout appears before reading
          Console.Out.Flush();
          
          string? input = ReadInput();
          if (input != null && int.TryParse(input, out int value))
          {
            Push(value);
          }
          else
          {
            Console.WriteLine($"Invalid integer input: {input ?? "null"}");
            Push(0);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.RDB:
        // Read boolean (accepts: true, false, 1, 0 - case insensitive)
        {
          Console.Out.Flush();
          
          string? input = ReadInput();
          int boolValue = 0;
          
          if (input != null)
          {
            string trimmed = input.Trim().ToLower();
            if (trimmed == "true" || trimmed == "1")
            {
              boolValue = 1;
            }
            else if (trimmed == "false" || trimmed == "0")
            {
              boolValue = 0;
            }
            else
            {
              Console.WriteLine($"Invalid boolean input: {input} (expected: true/false/1/0)");
              boolValue = 0;
            }
          }
          
          Push(boolValue);
          _pc++;
        }
        break;
      
      case PCodeOperation.RDS:
        // Read string (allows spaces)
        {
          Console.Out.Flush();
          
          string? input = ReadInput();
          if (input != null)
          {
            // Add string to string table if not already there
            int strIndex = _program.StringTable.IndexOf(input);
            if (strIndex < 0)
            {
              _program.StringTable.Add(input);
              strIndex = _program.StringTable.Count - 1;
            }
            Push(strIndex);
          }
          else
          {
            Push(0); // Empty string index
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.WRT:
        // Write integer
        {
          int value = Pop();
          Console.Write(value);
          _output.Append(value);
          _pc++;
        }
        break;
      
      case PCodeOperation.WRS:
        // Write string
        {
          int strIndex = Pop();
          if (strIndex >= 0 && strIndex < _program.StringTable.Count)
          {
            string str = _program.StringTable[strIndex];
            Console.Write(str);
            _output.Append(str);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.WRL:
        // Write line
        _output.AppendLine();
        _pc++;
        break;
      
      case PCodeOperation.HLT:
        // Halt execution
        _running = false;
        break;
      
      case PCodeOperation.NOP:
        // No operation - skip silently (for backwards compatibility)
        _pc++;
        break;
      
      default:
        _output.AppendLine($"Unknown instruction: {instruction.Op}");
        _running = false;
        break;
    }
  }
  
  private void Push(int value)
  {
    if (_sp >= STACK_SIZE)
    {
      _output.AppendLine("Runtime Error: Stack overflow");
      _running = false;
      return;
    }
    _stack[_sp++] = value;
  }
  
  private int Pop()
  {
    if (_sp <= 0)
    {
      _output.AppendLine("Runtime Error: Stack underflow");
      _running = false;
      return 0;
    }
    return _stack[--_sp];
  }
  
  private string? ReadInput()
  {
    if (_inputQueue != null && _inputQueue.Count > 0)
    {
      return _inputQueue.Dequeue();
    }
    
    // If no input queue, read from console silently
    // The programmer's cout should have already prompted the user
    return Console.ReadLine();
  }
  
  /// <summary>
  /// Executes with interactive input
  /// </summary>
  public void ExecuteInteractive()
  {
    _inputQueue = null;
    Execute();
  }
  
  /// <summary>
  /// Returns execution trace for debugging
  /// </summary>
  public string GetExecutionTrace()
  {
    var trace = new StringBuilder();
    trace.AppendLine("Execution Trace:");
    trace.AppendLine(new string('=', 60));
    
    _sp = _program.DataSize;
    _pc = 0;
    _running = true;
    
    int steps = 0;
    const int MAX_STEPS = 10000;
    
    while (_running && _pc < _program.Instructions.Count && steps < MAX_STEPS)
    {
      var instruction = _program.Instructions[_pc];
      
      trace.AppendLine($"PC={_pc,4} SP={_sp,4} : {instruction}");
      
      ExecuteInstruction(instruction);
      steps++;
    }
    
    if (steps >= MAX_STEPS)
    {
      trace.AppendLine("Warning: Maximum steps reached (possible infinite loop)");
    }
    
    trace.AppendLine();
    trace.AppendLine("Final Stack:");
    for (int i = 0; i < Math.Min(_sp, 10); i++)
    {
      trace.AppendLine($"  [{i}] = {_stack[i]}");
    }
    
    return trace.ToString();
  }
}

