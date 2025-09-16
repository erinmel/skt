using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.ViewModels.ToolWindows;
using System;
using System.IO;
using System.Threading.Tasks;

namespace skt.IDE.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _currentLine = 1;

    [ObservableProperty]
    private int _currentColumn = 1;

    [ObservableProperty]
    private string _fileEncoding = "UTF-8";

    [ObservableProperty]
    private string _editorContent = "";

    [ObservableProperty]
    private string _tokensOutput = "No tokens to display. Open a file and compile to see lexical analysis.";

    [ObservableProperty]
    private string _syntaxTreeOutput = "No syntax tree to display. Open a file and compile to see syntax analysis.";

    [ObservableProperty]
    private string _lexicalErrors = "";

    [ObservableProperty]
    private string _syntaxErrors = "";

    [ObservableProperty]
    private string _otherErrors = "";

    [ObservableProperty]
    private int _selectedToolWindowIndex = 0;

    [ObservableProperty]
    private string _currentProjectPath = "";

    [ObservableProperty]
    private string _currentFilePath = "";

    [ObservableProperty]
    private string _selectedToolWindowTitle = "File Explorer";

    public FileExplorerViewModel FileExplorer { get; } = new();
    public PhaseOutputViewModel PhaseOutput { get; } = new();

    public async Task OpenProject(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                CurrentProjectPath = folderPath;
                StatusMessage = $"Opened project: {Path.GetFileName(folderPath)}";

                // Update the file explorer with the new project
                await FileExplorer.LoadProject(folderPath);

                // Switch to file explorer tab
                SelectedToolWindowIndex = 0;
            }
            else
            {
                StatusMessage = "Selected folder does not exist.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening project: {ex.Message}";
        }
    }

    public async Task OpenFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                CurrentFilePath = filePath;
                EditorContent = await File.ReadAllTextAsync(filePath);
                StatusMessage = $"Opened: {Path.GetFileName(filePath)}";

                // Reset cursor position
                CurrentLine = 1;
                CurrentColumn = 1;

                // Detect encoding (simplified - assumes UTF-8 for now)
                FileEncoding = "UTF-8";
            }
            else
            {
                StatusMessage = "Selected file does not exist.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening file: {ex.Message}";
        }
    }

    partial void OnEditorContentChanged(string value)
    {
        // When editor content changes, we could trigger compilation here
        // For now, just update status
        StatusMessage = string.IsNullOrEmpty(value) ? "Ready" :
                       string.IsNullOrEmpty(CurrentFilePath) ? "Modified" :
                       $"Modified: {Path.GetFileName(CurrentFilePath)}";
    }

    partial void OnSelectedToolWindowIndexChanged(int value)
    {
        SelectedToolWindowTitle = value switch
        {
            0 => "File Explorer",
            1 => "Tokens",
            2 => "Syntax Tree",
            3 => "Phase Output",
            _ => "File Explorer"
        };
    }
}
