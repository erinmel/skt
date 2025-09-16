using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using skt.IDE.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace skt.IDE.Views;

public partial class MainWindow : Window
{
    enum ToolWindowType
    {
        FileExplorer,
        Tokens,
        SyntaxTree,
        PhaseOutput
    }

    enum TerminalPanelType
    {
        Terminal,
        Errors,
        Syntax,
        Other
    }

    const string SelectedCssClass = "selected";

    static class ToolWindowTitles
    {
        public const string FileExplorer = "File Explorer";
        public const string Tokens = "Tokens";
        public const string SyntaxTree = "Syntax Tree";
        public const string PhaseOutput = "Phase Output";
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
    private ToolWindowType _selectedToolWindow = ToolWindowType.FileExplorer;
    private TerminalPanelType _selectedTerminalPanel = TerminalPanelType.Terminal;
    private bool _isTerminalPanelVisible = false;

    public MainWindow()
    {
        InitializeComponent();
        UpdateToolWindowVisibility();
        UpdateTerminalPanelVisibility();
        UpdateToolWindowSelection(); // Initialize selection state
    }

    #region Tool Window Toggle Methods

    private async void ToolWindowToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var toolWindow = button.Name switch
        {
            nameof(FileExplorerToggle) => ToolWindowType.FileExplorer,
            nameof(TokensToggle) => ToolWindowType.Tokens,
            nameof(SyntaxTreeToggle) => ToolWindowType.SyntaxTree,
            nameof(PhaseOutputToggle) => ToolWindowType.PhaseOutput,
            _ => ToolWindowType.FileExplorer
        };

        await SwitchToolWindow(toolWindow);
    }

    private async Task SwitchToolWindow(ToolWindowType toolWindow)
    {
        _selectedToolWindow = toolWindow;
        UpdateToolWindowVisibility();
        UpdateToolWindowSelection();

        if (ViewModel != null)
        {
            ViewModel.SelectedToolWindowTitle = toolWindow switch
            {
                ToolWindowType.FileExplorer => ToolWindowTitles.FileExplorer,
                ToolWindowType.Tokens => ToolWindowTitles.Tokens,
                ToolWindowType.SyntaxTree => ToolWindowTitles.SyntaxTree,
                ToolWindowType.PhaseOutput => ToolWindowTitles.PhaseOutput,
                _ => ToolWindowTitles.FileExplorer
            };

            // TODO: Trigger compilation or analysis if needed for Tokens/SyntaxTree views
            if (toolWindow == ToolWindowType.Tokens || toolWindow == ToolWindowType.SyntaxTree)
            {
                await RefreshAnalysisData();
            }
        }
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
        FileExplorerToggle.Classes.Remove(SelectedCssClass);
        TokensToggle.Classes.Remove(SelectedCssClass);
        SyntaxTreeToggle.Classes.Remove(SelectedCssClass);
        PhaseOutputToggle.Classes.Remove(SelectedCssClass);

        // Add selected class to current tool window button
        var selectedButton = _selectedToolWindow switch
        {
            ToolWindowType.FileExplorer => FileExplorerToggle,
            ToolWindowType.Tokens => TokensToggle,
            ToolWindowType.SyntaxTree => SyntaxTreeToggle,
            ToolWindowType.PhaseOutput => PhaseOutputToggle,
            _ => FileExplorerToggle
        };
        selectedButton.Classes.Add(SelectedCssClass);
    }

    #endregion

    #region Terminal Panel Toggle Methods

    private void ToolPanelToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var panel = button.Name switch
        {
            nameof(TerminalToggle) => TerminalPanelType.Terminal,
            nameof(OutputToggle) => TerminalPanelType.Errors, // Output maps to general errors
            nameof(ErrorsToggle) => TerminalPanelType.Errors,
            nameof(BuildToggle) => TerminalPanelType.Other, // Build output maps to other
            _ => TerminalPanelType.Terminal
        };

        ToggleTerminalPanel(panel);
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
        var terminalRow = RootGrid.RowDefinitions[2]; // Terminal/Errors row

        if (_isTerminalPanelVisible)
        {
            terminalRow.Height = new GridLength(200, GridUnitType.Pixel); // Show with 200px height
        }
        else
        {
            terminalRow.Height = new GridLength(0, GridUnitType.Pixel); // Hide
        }
    }

    private void UpdateTerminalPanelSelection()
    {
        // Remove selected class from all panel buttons
        TerminalToggle.Classes.Remove(SelectedCssClass);
        OutputToggle.Classes.Remove(SelectedCssClass);
        ErrorsToggle.Classes.Remove(SelectedCssClass);
        BuildToggle.Classes.Remove(SelectedCssClass);

        if (_isTerminalPanelVisible)
        {
            // Add selected class to current panel button
            var selectedButton = _selectedTerminalPanel switch
            {
                TerminalPanelType.Terminal => TerminalToggle,
                TerminalPanelType.Errors => ErrorsToggle,
                TerminalPanelType.Syntax => ErrorsToggle, // Syntax errors use same button
                TerminalPanelType.Other => BuildToggle,
                _ => TerminalToggle
            };
            selectedButton.Classes.Add(SelectedCssClass);
        }
    }

    private void SwitchTerminalTab()
    {
        if (!_isTerminalPanelVisible) return;

        var tabIndex = _selectedTerminalPanel switch
        {
            TerminalPanelType.Terminal => 0,
            TerminalPanelType.Errors => 1, // Lexical Errors
            TerminalPanelType.Syntax => 2, // Syntax Errors
            TerminalPanelType.Other => 3, // Other Errors
            _ => 0
        };

        TerminalTabView.SelectedIndex = tabIndex;
    }

    #endregion

    #region Toolbar Event Handlers

    private void FileMenuButton_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Implement file menu dropdown
        // This could show options like New, Open, Save, Recent Files, etc.
        if (ViewModel != null)
        {
            ViewModel.StatusMessage = "File menu not implemented yet";
        }
    }

    private async void OpenProjectButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storageProvider = StorageProvider;
            if (storageProvider == null) return;

            var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Project Folder",
                AllowMultiple = false
            });

            if (result.Count > 0)
            {
                var selectedFolder = result[0];
                var folderPath = selectedFolder.Path.LocalPath;

                if (ViewModel != null)
                {
                    await ViewModel.OpenProject(folderPath);
                    await SwitchToolWindow(ToolWindowType.FileExplorer); // Switch to file explorer
                }
            }
        }
        catch (Exception ex)
        {
            if (ViewModel != null)
            {
                ViewModel.StatusMessage = $"Error opening project: {ex.Message}";
            }
        }
    }

    private void NewFileButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.EditorContent = "";
            ViewModel.CurrentFilePath = "";
            ViewModel.StatusMessage = "New file created";
            ViewModel.CurrentLine = 1;
            ViewModel.CurrentColumn = 1;
        }
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null && !string.IsNullOrEmpty(ViewModel.CurrentFilePath))
        {
            try
            {
                await System.IO.File.WriteAllTextAsync(ViewModel.CurrentFilePath, ViewModel.EditorContent);
                ViewModel.StatusMessage = $"Saved: {System.IO.Path.GetFileName(ViewModel.CurrentFilePath)}";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error saving file: {ex.Message}";
            }
        }
        else
        {
            // If no current file, trigger Save As
            SaveAsButton_Click(sender, e);
        }
    }

    private async void SaveAsButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storageProvider = StorageProvider;
            if (storageProvider == null || ViewModel == null) return;

            var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save File As",
                SuggestedFileName = "Untitled.skt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("SKT Files") { Patterns = new[] { "*.skt" } },
                    new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (result != null)
            {
                var filePath = result.Path.LocalPath;
                await System.IO.File.WriteAllTextAsync(filePath, ViewModel.EditorContent);
                ViewModel.CurrentFilePath = filePath;
                ViewModel.StatusMessage = $"Saved as: {System.IO.Path.GetFileName(filePath)}";
            }
        }
        catch (Exception ex)
        {
            if (ViewModel != null)
            {
                ViewModel.StatusMessage = $"Error saving file: {ex.Message}";
            }
        }
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.StatusMessage = "Settings dialog not implemented yet";
        }
    }

    #endregion

    #region Editor Event Handlers

    private void EditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || ViewModel == null) return;

        // Update cursor position (simplified - you might want more sophisticated tracking)
        var caretIndex = textBox.CaretIndex;
        var text = textBox.Text ?? "";

        var lines = text.Substring(0, Math.Min(caretIndex, text.Length)).Split('\n');
        ViewModel.CurrentLine = lines.Length;
        ViewModel.CurrentColumn = lines.LastOrDefault()?.Length + 1 ?? 1;

        // TODO: Trigger real-time analysis if needed
        // This could update tokens, syntax tree, and error information
    }

    #endregion

    #region Helper Methods

    private async Task RefreshAnalysisData()
    {
        if (ViewModel == null || string.IsNullOrEmpty(ViewModel.EditorContent)) return;

        try
        {
            // TODO: Integrate with your compiler for real analysis
            // For now, just update the status
            ViewModel.StatusMessage = "Analysis refreshed";

            // Placeholder for actual lexical/syntax analysis
            if (_selectedToolWindow == ToolWindowType.Tokens)
            {
                ViewModel.TokensOutput = "Tokens analysis would appear here...";
            }
            else if (_selectedToolWindow == ToolWindowType.SyntaxTree)
            {
                ViewModel.SyntaxTreeOutput = "Syntax tree would appear here...";
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Analysis error: {ex.Message}";
        }
    }

    #endregion
}
