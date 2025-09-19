using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.ViewModels.ToolWindows;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;
using skt.IDE.Services;

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

    public DrawingImage? WindowStateIcon
    {
        get
        {
            var key = CurrentWindowState switch
            {
                WindowState.Maximized => "Icon.Restore",
                WindowState.Minimized => "Icon.Minimize",
                _ => "Icon.Maximize"
            };

            return Application.Current?.FindResource(key) as DrawingImage;
        }
    }

    // Computed properties for button enablement
    public bool CanSave => TabbedEditorViewModel.SelectedDocument != null && TabbedEditorViewModel.SelectedDocument.IsDirty;
    public bool CanSaveAs => TabbedEditorViewModel.SelectedDocument != null;
    public bool CanCreateNewFile => IsProjectOpen;

    public FileExplorerViewModel FileExplorer { get; } = new();
    public PhaseOutputViewModel PhaseOutput { get; } = new();

    // Expose TabbedEditorViewModel for MainWindow binding
    public TabbedEditorViewModel TabbedEditorViewModel { get; } = new();

    public MainWindowViewModel()
    {
        // Subscribe to TabbedEditor selection changes to keep save button states in sync
        TabbedEditorViewModel.PropertyChanged += OnTabbedEditorPropertyChanged;

        App.EventBus.Subscribe<FileDirtyStateChangedEvent>(OnFileDirtyStateChanged);
        App.EventBus.Subscribe<FileOpenedEvent>(OnFileOpenedOrClosed);
        App.EventBus.Subscribe<FileClosedEvent>(OnFileOpenedOrClosed);
    }

    private void OnFileDirtyStateChanged(FileDirtyStateChangedEvent e)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveAs));
    }

    private void OnFileOpenedOrClosed(object e)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanSaveAs));
    }

    private void OnTabbedEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e?.PropertyName == nameof(TabbedEditorViewModel.SelectedDocument))
        {
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(CanSaveAs));
        }
    }

    // CreateNewFile moved to toolbar/event flow (Publish CreateFileRequestEvent) - method removed from MainWindowViewModel.

    // Save and SaveAs moved to Toolbar -> TabbedEditorViewModel calls; methods removed from MainWindowViewModel.


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

        OnPropertyChanged(nameof(WindowStateIcon));
    }

    partial void OnEditorContentChanged(string value)
    {
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
