namespace skt.Shared;

/// <summary>
/// Represents an entry in the symbol table
/// </summary>
[Serializable]
public class SymbolTableEntry(string name, string dataType, string scope,
                       int declarationLine, int declarationColumn, int memoryOffset = 0)
{
  public string Name { get; set; } = name;
  public string DataType { get; set; } = dataType;
  public string Scope { get; set; } = scope;
  public int DeclarationLine { get; set; } = declarationLine;
  public int DeclarationColumn { get; set; } = declarationColumn;
  public int MemoryOffset { get; set; } = memoryOffset;
  public object? Value { get; set; } = null;  // Current value of the variable
  public List<(int Line, int Column)> References { get; set; } = [];

  public void AddReference(int line, int column)
  {
    References.Add((line, column));
  }

  public void SetValue(object? value)
  {
    Value = value;
  }
}

/// <summary>
/// Symbol table for managing variable declarations and scopes
/// </summary>
[Serializable]
public class SymbolTable
{
  private readonly List<SymbolTableEntry> _entries = [];
  private int _currentOffset = 0;

  public IReadOnlyList<SymbolTableEntry> Entries => _entries.AsReadOnly();

  /// <summary>
  /// Adds a new symbol to the table
  /// </summary>
  public bool AddSymbol(string name, string dataType, string scope, int line, int column)
  {
    // Check for duplicate in the same scope
    if (_entries.Any(e => e.Name == name && e.Scope == scope))
    {
      return false; // Duplicate declaration
    }

    var entry = new SymbolTableEntry(name, dataType, scope, line, column, _currentOffset);
    _entries.Add(entry);

    // Update memory offset based on data type
    _currentOffset += GetTypeSize(dataType);

    return true;
  }

  /// <summary>
  /// Looks up a symbol in the table, searching through scopes
  /// </summary>
  public SymbolTableEntry? Lookup(string name, string currentScope)
  {
    // First, try to find in current scope
    var entry = _entries.FirstOrDefault(e => e.Name == name && e.Scope == currentScope);

    // If not found and not in global scope, try global scope
    if (entry == null && currentScope != "global")
    {
      entry = _entries.FirstOrDefault(e => e.Name == name && e.Scope == "global");
    }

    return entry;
  }

  /// <summary>
  /// Gets the data type of a symbol
  /// </summary>
  public string? GetSymbolType(string name, string scope)
  {
    return Lookup(name, scope)?.DataType;
  }

  /// <summary>
  /// Checks if a symbol is declared
  /// </summary>
  public bool IsDeclared(string name, string scope)
  {
    return Lookup(name, scope) != null;
  }

  /// <summary>
  /// Sets the value of a symbol
  /// </summary>
  public void SetSymbolValue(string name, string scope, object? value)
  {
    var entry = Lookup(name, scope);
    if (entry != null)
    {
      entry.SetValue(value);
    }
  }

  /// <summary>
  /// Gets the value of a symbol
  /// </summary>
  public object? GetSymbolValue(string name, string scope)
  {
    return Lookup(name, scope)?.Value;
  }

  /// <summary>
  /// Gets the memory size for a data type
  /// </summary>
  private static int GetTypeSize(string dataType)
  {
    return dataType switch
    {
      "int" => 4,
      "float" => 4,
      "bool" => 1,
      "string" => 8, // pointer size
      _ => 4 // default
    };
  }

  public void Clear()
  {
    _entries.Clear();
    _currentOffset = 0;
  }

  /// <summary>
  /// Copies entries from another symbol table
  /// </summary>
  public void CopyFrom(SymbolTable other)
  {
    _entries.Clear();
    _entries.AddRange(other.Entries);
    _currentOffset = other._currentOffset;
  }
}
