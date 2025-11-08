namespace skt.Shared;

/// <summary>
/// Defines how a semantic attribute is propagated through the AST
/// </summary>
public enum AttributePropagation
{
  None,           // Attribute not set or not applicable
  Synthesized,    // Computed from children (bottom-up)
  Inherited,      // Passed from parent (top-down)
  Sibling          // Propagated from sibling nodes
}

/// <summary>
/// Represents a semantic attribute with its value and propagation information
/// </summary>
[Serializable]
public class SemanticAttribute
{
  public string? Value { get; set; }
  public AttributePropagation Propagation { get; set; }
  public string? SourceNode { get; set; } // For debugging: where did this attribute come from?

  public SemanticAttribute()
  {
    Propagation = AttributePropagation.None;
  }

  public SemanticAttribute(string? value, AttributePropagation propagation, string? sourceNode = null)
  {
    Value = value;
    Propagation = propagation;
    SourceNode = sourceNode;
  }
}

/// <summary>
/// AST Node with semantic attributes (inherited and synthesized attributes)
/// </summary>
[Serializable]
public class AnnotatedAstNode
{
  // Base AST information
  public string Rule { get; set; }
  public List<AnnotatedAstNode> Children { get; set; }
  public Token? Token { get; set; }
  public int Line { get; set; }
  public int Column { get; set; }
  public int EndLine { get; set; }
  public int EndColumn { get; set; }

  // Semantic attributes (synthesized)
  public string? DataType { get; set; }           // Type of expression/variable
  public object? Value { get; set; }              // Constant value (if applicable)
  public bool IsConstant { get; set; }            // True if value is a compile-time constant

  // Semantic attributes (inherited)
  public string? Scope { get; set; }              // Current scope

  // Enhanced attribute tracking
  public SemanticAttribute TypeAttribute { get; set; }    // Tracks how type is propagated
  public SemanticAttribute ValueAttribute { get; set; }   // Tracks how value is propagated

  public AnnotatedAstNode(AstNode node)
  {
    Rule = node.Rule;
    Token = node.Token;
    Line = node.Line;
    Column = node.Column;
    EndLine = node.EndLine;
    EndColumn = node.EndColumn;
    Children = [];
    Scope = "global";
    TypeAttribute = new SemanticAttribute();
    ValueAttribute = new SemanticAttribute();
  }

  public bool IsError => Rule.StartsWith("Error") || Rule.StartsWith("ERROR");

  /// <summary>
  /// Creates an annotated AST from a regular AST
  /// </summary>
  public static AnnotatedAstNode FromAstNode(AstNode node)
  {
    var annotated = new AnnotatedAstNode(node);

    if (node.Children != null)
    {
      foreach (var child in node.Children)
      {
        annotated.Children.Add(FromAstNode(child));
      }
    }

    return annotated;
  }

  /// <summary>
  /// Converts back to a regular AST node (useful for display)
  /// </summary>
  public AstNode ToAstNode()
  {
    var astNode = new AstNode(Rule, null, Token, Line, Column, EndLine, EndColumn);

    foreach (var child in Children)
    {
      astNode.Children.Add(child.ToAstNode());
    }

    return astNode;
  }

  /// <summary>
  /// Sets the type attribute with propagation information
  /// </summary>
  public void SetTypeAttribute(string? type, AttributePropagation propagation, string? source = null)
  {
    DataType = type;
    TypeAttribute = new SemanticAttribute(type, propagation, source);
  }

  /// <summary>
  /// Sets the value attribute with propagation information
  /// </summary>
  public void SetValueAttribute(object? value, AttributePropagation propagation, string? source = null)
  {
    Value = value;
    IsConstant = value != null;

    string? valueString = value != null ? ValueFormatter.FormatValue(value) : null;
    ValueAttribute = new SemanticAttribute(valueString, propagation, source);
  }

  /// <summary>
  /// Gets a summary of all semantic attributes for debugging/visualization
  /// </summary>
  public string GetAttributeSummary()
  {
    var parts = new List<string>();

    if (!string.IsNullOrEmpty(DataType))
    {
      var typeInfo = $"Type: {DataType} [{TypeAttribute.Propagation}]";
      if (!string.IsNullOrEmpty(TypeAttribute.SourceNode))
        typeInfo += $" from {TypeAttribute.SourceNode}";
      parts.Add(typeInfo);
    }

    if (Value != null)
    {
      string formattedValue = ValueFormatter.FormatValue(Value);
      var valueInfo = $"Value: {formattedValue} [{ValueAttribute.Propagation}]";
      if (!string.IsNullOrEmpty(ValueAttribute.SourceNode))
        valueInfo += $" from {ValueAttribute.SourceNode}";
      parts.Add(valueInfo);
    }

    if (!string.IsNullOrEmpty(Scope) && Scope != "global")
    {
      parts.Add($"Scope: {Scope}");
    }

    return parts.Count > 0 ? string.Join(", ", parts) : "No attributes";
  }
}
