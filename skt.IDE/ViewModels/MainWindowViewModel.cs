using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.ViewModels.ToolWindows;
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace skt.IDE.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string _greeting = "Welcome to Avalonia!";

    [ObservableProperty] private string _statusMessage = "Ready";

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

    // Computed properties for button enablement
    public bool CanSave => IsFileOpen && !string.IsNullOrEmpty(CurrentFilePath);
    public bool CanSaveAs => IsFileOpen && !string.IsNullOrEmpty(EditorContent);
    public bool CanCreateNewFile => IsProjectOpen;

    public FileExplorerViewModel FileExplorer { get; } = new();
    public PhaseOutputViewModel PhaseOutput { get; } = new();

    public async Task OpenProject(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                CurrentProjectPath = folderPath;
                IsProjectOpen = true;
                StatusMessage = $"Opened project: {Path.GetFileName(folderPath)}";

                // Update the file explorer with the new project
                await FileExplorer.LoadProject(folderPath);

                // Switch to file explorer tab
                SelectedToolWindowIndex = 0;

                // Notify computed properties changed
                OnPropertyChanged(nameof(CanCreateNewFile));
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
                IsFileOpen = true;
                EditorContent = await File.ReadAllTextAsync(filePath);
                StatusMessage = $"Opened: {Path.GetFileName(filePath)}";

                // Reset cursor position
                CurrentLine = 1;
                CurrentColumn = 1;

                // Detect encoding (simplified - assumes UTF-8 for now)
                FileEncoding = "UTF-8";

                // Notify computed properties changed
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(CanSaveAs));
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
                WindowStateButtonText = "🗗";
                WindowStateButtonTooltip = "Restore";
                break;
            case WindowState.Normal:
            case WindowState.Minimized:
                WindowStateButtonText = "🗖";
                WindowStateButtonTooltip = "Maximize";
                break;
        }
    }

    partial void OnEditorContentChanged(string value)
    {
        // When editor content changes, we could trigger compilation here
        // For now, just update status
        if (string.IsNullOrEmpty(value))
        {
            StatusMessage = "Ready";
        }
        else if (string.IsNullOrEmpty(CurrentFilePath))
        {
            StatusMessage = "Modified";
        }
        else
        {
            StatusMessage = $"Modified: {Path.GetFileName(CurrentFilePath)}";
        }

        // Update computed properties
        OnPropertyChanged(nameof(CanSaveAs));
    }

    partial void OnCurrentFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnIsFileOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveAs));
    }

    partial void OnIsProjectOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCreateNewFile));
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