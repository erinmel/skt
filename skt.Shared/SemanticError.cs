namespace skt.Shared;

/// <summary>
/// Types of semantic errors that can be detected during semantic analysis
/// </summary>
public enum SemanticErrorType
{
  UndeclaredVariable,
  DuplicateDeclaration,
  TypeIncompatibility,
  InvalidOperator,
}

/// <summary>
/// Represents a semantic error found during semantic analysis
/// </summary>
[Serializable]
public record SemanticError(
    SemanticErrorType ErrorType,
    string Message,
    int Line,
    int Column,
    int EndLine,
    int EndColumn,
    string? VariableName = null,
    string? ExpectedType = null,
    string? ActualType = null
);
