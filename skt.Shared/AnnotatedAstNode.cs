namespace skt.Shared;

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
}
