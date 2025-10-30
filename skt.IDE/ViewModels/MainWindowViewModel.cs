using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.ViewModels.ToolWindows;
using Avalonia.Controls;

namespace skt.IDE.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _greeting = "Welcome to Avalonia!";

    // StatusMessage removed; status bar is driven via App.EventBus (StatusBarMessageEvent)

    [ObservableProperty] private int _currentLine = 1;

    [ObservableProperty] private int _currentColumn = 1;

    [ObservableProperty] private string _fileEncoding = "UTF-8";

    [ObservableProperty] private string _editorContent = "";

    [ObservableProperty]
    private string _tokensOutput = "No tokens to display. Open a file and compile to see lexical analysis.";

    [ObservableProperty] private string _syntaxTreeOutput =
        "No syntax tree to display. Open a file and compile to see syntax analysis.";

    [ObservableProperty] private string _lexicalErrors = "";

    [ObservableProperty] private string _syntaxErrors = "";

    [ObservableProperty] private string _otherErrors = "";

    [ObservableProperty] private int _selectedToolWindowIndex;

    [ObservableProperty] private string _currentProjectPath = "";

    [ObservableProperty] private string _currentFilePath = "";

    [ObservableProperty] private string _selectedToolWindowTitle = "File Explorer";

    [ObservableProperty] private bool _isProjectOpen;

    [ObservableProperty] private bool _isFileOpen;

    [ObservableProperty] private WindowState _currentWindowState = WindowState.Normal;

    [ObservableProperty] private string _windowStateButtonText = "🗖";

    [ObservableProperty] private string _windowStateButtonTooltip = "Maximize";

    public string WindowStateIconKey
    {
        get
        {
            return CurrentWindowState switch
            {
                WindowState.Maximized => "Icon.Restore",
                WindowState.Minimized => "Icon.Minimize",
                _ => "Icon.Maximize"
            };
        }
    }

    public FileExplorerViewModel FileExplorer { get; } = new();
    public PhaseOutputViewModel PhaseOutput { get; } = new();
    public TokensViewModel Tokens { get; } = new();

    // Errors view model (lexical/syntax/other grouped errors)
    public ErrorsViewModel Errors { get; } = new();

    // Syntax Tree view model
    public SyntaxTreeViewModel SyntaxTree { get; } = new();

    // Semantic Tree view model
    public SemanticTreeViewModel SemanticTree { get; } = new();

    // Symbol Table view model
    public SymbolTableViewModel SymbolTable { get; } = new();

    // Expose TabbedEditorViewModel for MainWindow binding
    public TabbedEditorViewModel TabbedEditorViewModel { get; } = new();

    public void UpdateWindowState(WindowState newState)
    {
        CurrentWindowState = newState;
        UpdateWindowStateButton();
    }

    private void UpdateWindowStateButton()
    {
        switch (CurrentWindowState)
        {
            case WindowState.Maximized:
                WindowStateButtonTooltip = "Restore";
                break;
            case WindowState.Normal:
            case WindowState.Minimized:
                WindowStateButtonTooltip = "Maximize";
                break;
        }

        OnPropertyChanged(nameof(WindowStateIconKey));
    }
}
