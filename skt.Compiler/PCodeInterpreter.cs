using skt.Shared;
using System.Text;

namespace skt.Compiler;

/// <summary>
/// P-Code Stack Machine Interpreter
/// Executes P-Code programs with full support for strings, integers, floats, and booleans
/// Uses long[] stack to store both ints and doubles (via BitConverter)
/// </summary>
public class PCodeInterpreter
{
  private readonly long[] _stack;
  private int _sp; // Stack pointer
  private int _pc; // Program counter
  private readonly PCodeProgram _program;
  private bool _running;
  private readonly StringBuilder _output;
  private Queue<string>? _inputQueue;
  
  private const int STACK_SIZE = 10000;
  
  public string Output => _output.ToString();
  public bool IsRunning => _running;
  
  // Events for real-time output/error reporting
  public event Action<string>? OnOutput;
  public event Action<string>? OnError;
  
  // Event for requesting input from UI
  public event Func<Task<string?>>? OnInputRequest;
  
  public PCodeInterpreter(PCodeProgram program)
  {
    _program = program;
    _stack = new long[STACK_SIZE];
    _sp = 0;
    _pc = 0;
    _running = false;
    _output = new StringBuilder();
  }

  public PCodeInterpreter()
  {
    _program = new PCodeProgram();
    _stack = new long[STACK_SIZE];
    _sp = 0;
    _pc = 0;
    _running = false;
    _output = new StringBuilder();
  }
  
  /// <summary>
  /// Executes the P-Code program
  /// </summary>
  public async Task ExecuteAsync(PCodeProgram program, string[]? inputs = null, System.Threading.CancellationToken cancellationToken = default)
  {
    await ExecuteAsync(program.Instructions, program.StringTable, program.DataSize, inputs, cancellationToken);
  }
  
  /// <summary>
  /// Executes the P-Code program (synchronous version for backwards compatibility)
  /// </summary>
  public void Execute(PCodeProgram program, string[]? inputs = null)
  {
    ExecuteAsync(program.Instructions, program.StringTable, program.DataSize, inputs).GetAwaiter().GetResult();
  }

  /// <summary>
  /// Executes the P-Code program (legacy method for backward compatibility)
  /// </summary>
  public void Execute(string[]? inputs = null)
  {
    ExecuteAsync(_program.Instructions, _program.StringTable, _program.DataSize, inputs).GetAwaiter().GetResult();
  }

  private async Task ExecuteAsync(List<PCodeInstruction> instructions, List<string> stringTable, int dataSize, string[]? inputs = null, System.Threading.CancellationToken cancellationToken = default)
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
    _sp = dataSize;
    
    // Create a temporary program for execution context
    var execProgram = new PCodeProgram
    {
      Instructions = new List<PCodeInstruction>(instructions),
      StringTable = new List<string>(stringTable),
      DataSize = dataSize
    };
    
    while (_running && _pc < instructions.Count)
    {
      // Check for cancellation
      if (cancellationToken.IsCancellationRequested)
      {
        _running = false;
        throw new System.OperationCanceledException("Program execution was cancelled", cancellationToken);
      }
      
      var instruction = instructions[_pc];
      await ExecuteInstructionAsync(instruction, execProgram);
    }
  }
  
  private async Task ExecuteInstructionAsync(PCodeInstruction instruction, PCodeProgram program)
  {
    switch (instruction.Op)
    {
      case PCodeOperation.LIT:
        // Push integer literal value onto stack
        PushInt(instruction.Operand);
        _pc++;
        break;
      
      case PCodeOperation.LITF:
        // Push float literal - operand is index to string table containing the float value
        {
          string floatStr = program.StringTable[instruction.Operand];
          if (double.TryParse(floatStr, out double floatValue))
          {
            PushDouble(floatValue);
          }
          else
          {
            PushDouble(0.0);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.LOD:
        // Load variable from data area
        {
          long value = _stack[instruction.Operand];
          Push(value);
          _pc++;
        }
        break;
      
      case PCodeOperation.STO:
        // Store top of stack in variable
        {
          long value = Pop();
          _stack[instruction.Operand] = value;
          _pc++;
        }
        break;
      
      case PCodeOperation.ADD:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a + b);
          _pc++;
        }
        break;
      
      case PCodeOperation.SUB:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a - b);
          _pc++;
        }
        break;
      
      case PCodeOperation.MUL:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a * b);
          _pc++;
        }
        break;
      
      case PCodeOperation.FADD:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushDouble(a + b);
          _pc++;
        }
        break;
      
      case PCodeOperation.FSUB:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushDouble(a - b);
          _pc++;
        }
        break;
      
      case PCodeOperation.FMUL:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushDouble(a * b);
          _pc++;
        }
        break;
      
      case PCodeOperation.DIV:
        {
          int b = PopInt();
          int a = PopInt();
          if (b == 0)
          {
            _output.AppendLine("Runtime Error: Division by zero");
            _running = false;
          }
          else
          {
            PushInt(a / b);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.FDIV:
        {
          double b = PopDouble();
          double a = PopDouble();
          if (Math.Abs(b) < double.Epsilon)
          {
            _output.AppendLine("Runtime Error: Division by zero");
            _running = false;
          }
          else
          {
            PushDouble(a / b);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.FPOW:
        {
          double exponent = PopDouble();
          double baseValue = PopDouble();
          double result = Math.Pow(baseValue, exponent);
          PushDouble(result);
          _pc++;
        }
        break;
      
      case PCodeOperation.MOD:
        {
          int b = PopInt();
          int a = PopInt();
          if (b == 0)
          {
            _output.AppendLine("Runtime Error: Modulo by zero");
            _running = false;
          }
          else
          {
            PushInt(a % b);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.POW:
        {
          int exponent = PopInt();
          int baseValue = PopInt();
          int result = (int)Math.Pow(baseValue, exponent);
          PushInt(result);
          _pc++;
        }
        break;
      
      case PCodeOperation.NEG:
        {
          int a = PopInt();
          PushInt(-a);
          _pc++;
        }
        break;
      
      case PCodeOperation.FNEG:
        {
          double a = PopDouble();
          PushDouble(-a);
          _pc++;
        }
        break;
      
      case PCodeOperation.I2F:
        {
          // Convert int to float
          int a = PopInt();
          PushDouble((double)a);
          _pc++;
        }
        break;
      
      case PCodeOperation.F2I:
        {
          // Convert float to int (truncate)
          double a = PopDouble();
          PushInt((int)a);
          _pc++;
        }
        break;
      
      case PCodeOperation.EQL:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a == b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.NEQ:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a != b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.LSS:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a < b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.LEQ:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a <= b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.GTR:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a > b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.GEQ:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt(a >= b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.FEQL:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushInt(Math.Abs(a - b) < double.Epsilon ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.FNEQ:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushInt(Math.Abs(a - b) >= double.Epsilon ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.FLSS:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushInt(a < b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.FLEQ:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushInt(a <= b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.FGTR:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushInt(a > b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.FGEQ:
        {
          double b = PopDouble();
          double a = PopDouble();
          PushInt(a >= b ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.AND:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt((a != 0 && b != 0) ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.OR:
        {
          int b = PopInt();
          int a = PopInt();
          PushInt((a != 0 || b != 0) ? 1 : 0);
          _pc++;
        }
        break;
      
      case PCodeOperation.NOT:
        {
          int a = PopInt();
          PushInt(a == 0 ? 1 : 0);
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
          int condition = PopInt();
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
          
          string? input = await ReadInputAsync();
          if (input != null && int.TryParse(input, out int value))
          {
            PushInt(value);
          }
          else
          {
            string error = $"Invalid integer input: {input ?? "null"}\n";
            Console.Write(error);
            _output.Append(error);
            OnOutput?.Invoke(error);
            PushInt(0);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.RDF:
        // Read float
        {
          Console.Out.Flush();
          
          string? input = await ReadInputAsync();
          if (input != null && double.TryParse(input, out double value))
          {
            PushDouble(value);
          }
          else
          {
            string error = $"Invalid float input: {input ?? "null"}\n";
            Console.Write(error);
            _output.Append(error);
            OnOutput?.Invoke(error);
            PushDouble(0.0);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.RDB:
        // Read boolean (accepts: true, false, 1, 0 - case insensitive)
        {
          Console.Out.Flush();
          
          string? input = await ReadInputAsync();
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
              string error = $"Invalid boolean input: {input} (expected: true/false/1/0)\n";
              Console.Write(error);
              _output.Append(error);
              OnOutput?.Invoke(error);
              boolValue = 0;
            }
          }
          
          PushInt(boolValue);
          _pc++;
        }
        break;
      
      case PCodeOperation.RDS:
        // Read string (allows spaces)
        {
          Console.Out.Flush();
          
          string? input = await ReadInputAsync();
          if (input != null)
          {
            // Add string to string table if not already there
            int strIndex = program.StringTable.IndexOf(input);
            if (strIndex < 0)
            {
              program.StringTable.Add(input);
              strIndex = program.StringTable.Count - 1;
            }
            PushInt(strIndex);
          }
          else
          {
            PushInt(0); // Empty string index
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.WRT:
        // Write integer
        {
          int value = PopInt();
          string output = value.ToString();
          Console.Write(output);
          _output.Append(output);
          OnOutput?.Invoke(output);
          _pc++;
        }
        break;
      
      case PCodeOperation.WRTF:
        // Write float
        {
          double floatValue = PopDouble();
          string output = floatValue.ToString("0.###########");
          Console.Write(output);
          _output.Append(output);
          OnOutput?.Invoke(output);
          _pc++;
        }
        break;
      
      case PCodeOperation.WRS:
        // Write string
        {
          int strIndex = PopInt();
          if (strIndex >= 0 && strIndex < program.StringTable.Count)
          {
            string str = program.StringTable[strIndex];
            Console.Write(str);
            _output.Append(str);
            OnOutput?.Invoke(str);
          }
          _pc++;
        }
        break;
      
      case PCodeOperation.WRL:
        // Write line
        _output.AppendLine();
        OnOutput?.Invoke(Environment.NewLine);
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
        string errorMsg = $"Unknown instruction: {instruction.Op}{Environment.NewLine}";
        _output.Append(errorMsg);
        OnError?.Invoke(errorMsg);
        _running = false;
        break;
    }
  }
  
  private void Push(long value)
  {
    if (_sp >= STACK_SIZE)
    {
      _output.AppendLine("Runtime Error: Stack overflow");
      _running = false;
      return;
    }
    _stack[_sp++] = value;
  }
  
  private long Pop()
  {
    if (_sp <= 0)
    {
      _output.AppendLine("Runtime Error: Stack underflow");
      _running = false;
      return 0;
    }
    return _stack[--_sp];
  }
  
  private void PushInt(int value)
  {
    Push(value);
  }
  
  private int PopInt()
  {
    return (int)Pop();
  }
  
  private void PushDouble(double value)
  {
    Push(BitConverter.DoubleToInt64Bits(value));
  }
  
  private double PopDouble()
  {
    return BitConverter.Int64BitsToDouble(Pop());
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
  
  private async Task<string?> ReadInputAsync()
  {
    if (_inputQueue != null && _inputQueue.Count > 0)
    {
      return _inputQueue.Dequeue();
    }
    
    // If OnInputRequest event is subscribed, use it for UI input
    if (OnInputRequest != null)
    {
      return await OnInputRequest.Invoke();
    }
    
    // Fallback to console if no UI input handler
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
      
      ExecuteInstructionAsync(instruction, _program).GetAwaiter().GetResult();
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

