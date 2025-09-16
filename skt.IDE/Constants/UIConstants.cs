namespace skt.IDE.Constants;

public static class UIConstants
{
    public const string SelectedCssClass = "selected";

    public static class TabNames
    {
        public const string Terminal = "Terminal";
        public const string Errors = "Errors";
        public const string Syntax = "Syntax";
        public const string Other = "Other";
    }

    public static class ToolWindowTitles
    {
        public const string FileExplorer = "File Explorer";
        public const string Tokens = "Tokens";
        public const string SyntaxTree = "Syntax Tree";
        public const string PhaseOutput = "Phase Output";
    }
}

public enum ToolWindowType
{
    FileExplorer,
    Tokens,
    SyntaxTree,
    PhaseOutput
}

public enum TerminalPanelType
{
    Terminal,
    Errors,
    Syntax,
    Other
}
