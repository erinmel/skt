using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using skt.IDE.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;

namespace skt.IDE.Views;

public partial class MainWindow : Window
{
    private enum ToolWindowType
    {
        FileExplorer,
        Tokens,
        SyntaxTree,
        PhaseOutput
    }

    private enum TerminalPanelType
    {
        Terminal,
        Errors,
        Syntax,
        Other
    }

    private const string SelectedCssClass = "selected";

    private static class ToolWindowTitles
    {
        public const string FileExplorer = "File Explorer";
        public const string Tokens = "Tokens";
        public const string SyntaxTree = "Syntax Tree";
        public const string PhaseOutput = "Phase Output";
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
    private ToolWindowType _selectedToolWindow = ToolWindowType.FileExplorer;
    private TerminalPanelType _selectedTerminalPanel = TerminalPanelType.Terminal;
    private bool _isTerminalPanelVisible;

    public MainWindow()
    {
        InitializeComponent();
        InitializeUi();

        // Keep ViewModel in sync when the WindowState changes (toolbar may change it directly)
        PropertyChanged += OnWindowPropertyChanged;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty && ViewModel != null)
        {
            ViewModel.UpdateWindowState(WindowState);
        }
    }

    private void InitializeUi()
    {
        UpdateToolWindowVisibility();
        UpdateTerminalPanelVisibility();
        UpdateToolWindowSelection();

        // Initialize window state in ViewModel
        if (ViewModel != null)
        {
            ViewModel.UpdateWindowState(WindowState);
        }
    }

    #region Custom Toolbar Window Control Events

    private void Minimize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Restore_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Title Bar and Window Control Events

    private void MaximizeRestore_Click(object? sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }
    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    }

    #endregion

    #region Tool Window Toggle Methods

    private async void ToolWindowToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || string.IsNullOrEmpty(button.Name))
            return;

        var toolWindow = GetToolWindowFromButtonName(button.Name);
        await SwitchToolWindow(toolWindow);
    }

    private ToolWindowType GetToolWindowFromButtonName(string buttonName)
    {
        return buttonName switch
        {
            nameof(FileExplorerToggle) => ToolWindowType.FileExplorer,
            nameof(TokensToggle) => ToolWindowType.Tokens,
            nameof(SyntaxTreeToggle) => ToolWindowType.SyntaxTree,
            nameof(PhaseOutputToggle) => ToolWindowType.PhaseOutput,
            _ => ToolWindowType.FileExplorer
        };
    }

    private async Task SwitchToolWindow(ToolWindowType toolWindow)
    {
        _selectedToolWindow = toolWindow;
        UpdateToolWindowVisibility();
        UpdateToolWindowSelection();

        var viewModel = ViewModel;
        if (viewModel is not null)
        {
            viewModel.SelectedToolWindowTitle = GetToolWindowTitle(toolWindow);

            // Trigger compilation or analysis if needed for Tokens/SyntaxTree views
            if (toolWindow is ToolWindowType.Tokens or ToolWindowType.SyntaxTree)
            {
                await RefreshAnalysisData();
            }
        }
    }

    private static string GetToolWindowTitle(ToolWindowType toolWindow)
    {
        return toolWindow switch
        {
            ToolWindowType.FileExplorer => ToolWindowTitles.FileExplorer,
            ToolWindowType.Tokens => ToolWindowTitles.Tokens,
            ToolWindowType.SyntaxTree => ToolWindowTitles.SyntaxTree,
            ToolWindowType.PhaseOutput => ToolWindowTitles.PhaseOutput,
            _ => ToolWindowTitles.FileExplorer
        };
    }

    private void UpdateToolWindowVisibility()
    {
        FileExplorerContent.IsVisible = _selectedToolWindow == ToolWindowType.FileExplorer;
        TokensContent.IsVisible = _selectedToolWindow == ToolWindowType.Tokens;
        SyntaxTreeContent.IsVisible = _selectedToolWindow == ToolWindowType.SyntaxTree;
        PhaseOutputContent.IsVisible = _selectedToolWindow == ToolWindowType.PhaseOutput;
    }

    private void UpdateToolWindowSelection()
    {
        // Remove selected class from all tool window buttons
        RemoveSelectionFromToolWindowButtons();

        // Add selected class to current tool window button
        var selectedButton = GetSelectedToolWindowButton();
        selectedButton?.Classes.Add(SelectedCssClass);
    }

    private void RemoveSelectionFromToolWindowButtons()
    {
        FileExplorerToggle.Classes.Remove(SelectedCssClass);
        TokensToggle.Classes.Remove(SelectedCssClass);
        SyntaxTreeToggle.Classes.Remove(SelectedCssClass);
        PhaseOutputToggle.Classes.Remove(SelectedCssClass);
    }

    private Button? GetSelectedToolWindowButton()
    {
        return _selectedToolWindow switch
        {
            ToolWindowType.FileExplorer => FileExplorerToggle,
            ToolWindowType.Tokens => TokensToggle,
            ToolWindowType.SyntaxTree => SyntaxTreeToggle,
            ToolWindowType.PhaseOutput => PhaseOutputToggle,
            _ => FileExplorerToggle
        };
    }

    #endregion

    #region Terminal Panel Toggle Methods

    private void ToolPanelToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || string.IsNullOrEmpty(button.Name))
            return;

        var panel = GetTerminalPanelFromButtonName(button.Name);
        ToggleTerminalPanel(panel);
    }

    private TerminalPanelType GetTerminalPanelFromButtonName(string buttonName)
    {
        return buttonName switch
        {
            nameof(TerminalToggle) => TerminalPanelType.Terminal,
            nameof(OutputToggle) => TerminalPanelType.Errors, // Output maps to general errors
            nameof(ErrorsToggle) => TerminalPanelType.Errors,
            nameof(BuildToggle) => TerminalPanelType.Other, // Build output maps to other
            _ => TerminalPanelType.Terminal
        };
    }

    private void ToggleTerminalPanel(TerminalPanelType panelType)
    {
        // If the same panel is clicked and panel is visible, hide it
        if (_selectedTerminalPanel == panelType && _isTerminalPanelVisible)
        {
            _isTerminalPanelVisible = false;
        }
        else
        {
            // Show panel and switch to the selected type
            _selectedTerminalPanel = panelType;
            _isTerminalPanelVisible = true;
        }

        UpdateTerminalPanelVisibility();
        UpdateTerminalPanelSelection();
        SwitchTerminalTab();
    }

    private void UpdateTerminalPanelVisibility()
    {
        var terminalRow = RootGrid.RowDefinitions[2]; // Terminal/Errors row (corrected index - now row 2)

        terminalRow.Height = _isTerminalPanelVisible
            ? new GridLength(200, GridUnitType.Pixel) // Show with 200px height
            : new GridLength(0, GridUnitType.Pixel);   // Hide
    }

    private void UpdateTerminalPanelSelection()
    {
        // Remove selected class from all panel buttons
        RemoveSelectionFromPanelButtons();

        if (_isTerminalPanelVisible)
        {
            // Add selected class to current panel button
            var selectedButton = GetSelectedPanelButton();
            selectedButton?.Classes.Add(SelectedCssClass);
        }
    }

    private void RemoveSelectionFromPanelButtons()
    {
        TerminalToggle.Classes.Remove(SelectedCssClass);
        OutputToggle.Classes.Remove(SelectedCssClass);
        ErrorsToggle.Classes.Remove(SelectedCssClass);
        BuildToggle.Classes.Remove(SelectedCssClass);
    }

    private Button? GetSelectedPanelButton()
    {
        return _selectedTerminalPanel switch
        {
            TerminalPanelType.Terminal => TerminalToggle,
            TerminalPanelType.Errors => ErrorsToggle,
            TerminalPanelType.Syntax => ErrorsToggle, // Syntax errors use same button
            TerminalPanelType.Other => BuildToggle,
            _ => TerminalToggle
        };
    }

    private void SwitchTerminalTab()
    {
        if (!_isTerminalPanelVisible)
            return;

        var tabIndex = GetTabIndexForPanel(_selectedTerminalPanel);
        TerminalTabView.SelectedIndex = tabIndex;
    }

    private static int GetTabIndexForPanel(TerminalPanelType panelType)
    {
        return panelType switch
        {
            TerminalPanelType.Terminal => 0,
            TerminalPanelType.Errors => 1,   // Lexical Errors
            TerminalPanelType.Syntax => 2,   // Syntax Errors
            TerminalPanelType.Other => 3,    // Other Errors
            _ => 0
        };
    }

    #endregion

    #region Toolbar Event Handlers

    private void NewProjectButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is not null)
        {
            viewModel.StatusMessage = "New project not implemented yet";
        }
    }

    private async void OpenFileButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null) return;

        var storageProvider = StorageProvider;
        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var file = result[0];
            var filePath = file.Path.LocalPath;
            viewModel.EditorContent = await System.IO.File.ReadAllTextAsync(filePath);
            viewModel.CurrentFilePath = filePath;
            viewModel.StatusMessage = $"Opened: {System.IO.Path.GetFileName(filePath)}";
        }
    }

    private void CloseFileButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is not null)
        {
            viewModel.EditorContent = string.Empty;
            viewModel.CurrentFilePath = string.Empty;
            viewModel.StatusMessage = "File closed";
        }
    }

    private async void OpenProjectButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storageProvider = StorageProvider;

            var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Project Folder",
                AllowMultiple = false
            });

            if (result.Count > 0)
            {
                var selectedFolder = result[0];
                var folderPath = selectedFolder.Path.LocalPath;

                var viewModel = ViewModel;
                if (viewModel is not null)
                {
                    // Publish the selected folder path on the EventBus so other components can react
                    App.EventBus.Publish(new skt.IDE.Services.ProjectFolderSelectedEvent(folderPath));
                    await viewModel.OpenProject(folderPath);
                    await SwitchToolWindow(ToolWindowType.FileExplorer);
                }
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "Error opening project");
        }
    }

    private void NewFileButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null) return;
        viewModel.CreateNewFile();
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return;

        try
        {
            if (!string.IsNullOrEmpty(viewModel.CurrentFilePath))
            {
                await viewModel.SaveFile();
            }
            else
            {
                SaveAsButton_Click(sender, e);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "Error saving file");
        }
    }

    private async void SaveAsButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return;

        try
        {
            await viewModel.SaveAsFile();
        }
        catch (Exception ex)
        {
            HandleException(ex, "Error saving file as");
        }
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is not null)
        {
            viewModel.StatusMessage = "Settings dialog not implemented yet";
        }
    }

    #endregion

    #region Editor Event Handlers

    private void EditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var viewModel = ViewModel;
        if (viewModel is null)
            return;

        UpdateCursorPosition(textBox, viewModel);

        // TODO: Trigger real-time analysis if needed
        // This could update tokens, syntax tree, and error information
    }

    private static void UpdateCursorPosition(TextBox textBox, MainWindowViewModel viewModel)
    {
        var caretIndex = textBox.CaretIndex;
        var text = textBox.Text ?? string.Empty;

        var textUpToCaret = text.Substring(0, Math.Min(caretIndex, text.Length));
        var lines = textUpToCaret.Split('\n');

        viewModel.CurrentLine = lines.Length;
        viewModel.CurrentColumn = (lines.LastOrDefault()?.Length ?? 0) + 1;
    }

    #endregion

    #region Helper Methods

    private async Task RefreshAnalysisData()
    {
        var viewModel = ViewModel;
        if (viewModel is null || string.IsNullOrEmpty(viewModel.EditorContent))
            return;

        try
        {
            // TODO: Integrate with your compiler for real analysis
            // For now, just update the status
            viewModel.StatusMessage = "Analysis refreshed";

            // Placeholder for actual lexical/syntax analysis
            UpdateAnalysisOutput(viewModel);
        }
        catch (Exception ex)
        {
            HandleException(ex, "Analysis error");
        }

        await Task.CompletedTask;
    }

    private void UpdateAnalysisOutput(MainWindowViewModel viewModel)
    {
        switch (_selectedToolWindow)
        {
            case ToolWindowType.Tokens:
                viewModel.TokensOutput = "Tokens analysis would appear here...";
                break;
            case ToolWindowType.SyntaxTree:
                viewModel.SyntaxTreeOutput = "Syntax tree would appear here...";
                break;
        }
    }

    private void HandleException(Exception ex, string context)
    {
        var viewModel = ViewModel;
        if (viewModel is not null)
        {
            viewModel.StatusMessage = $"{context}: {ex.Message}";
        }
    }

    #endregion
}
