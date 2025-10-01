namespace skt.IDE.ViewModels.ToolWindows;

public class TokenRow
{
    public string Type { get; }
    public string Value { get; }
    public int Line { get; }
    public int Column { get; }
    public int EndLine { get; }
    public int EndColumn { get; }

    public TokenRow(string type, string value, int line, int column, int endLine, int endColumn)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    public string StartPos => $"{Line}:{Column}";
    public string EndPos => $"{EndLine}:{EndColumn}";
}
