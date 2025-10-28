using Avalonia.Controls;
using skt.IDE.ViewModels;
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using skt.IDE.Services.Buss;

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

        // Wire up decoupled ToolWindowStrip events
        ToolWindowStripControl.ToolWindowButtonClicked += OnToolWindowStrip_ToolWindowButtonClicked;
        ToolWindowStripControl.ToolPanelButtonClicked += OnToolWindowStrip_ToolPanelButtonClicked;

        // Subscribe to global requests to show tool windows or terminal tabs
        App.EventBus.Subscribe<ShowToolWindowRequestEvent>(OnShowToolWindowRequest);
        App.EventBus.Subscribe<ShowTerminalTabRequestEvent>(OnShowTerminalTabRequest);
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

    private void OnToolWindowStrip_ToolWindowButtonClicked(string buttonName)
    {
        // Fire-and-forget switch (original UI handler was async); swallow returned task
        _ = OnToolWindowStrip_ToolWindowButtonClickedAsync(buttonName);
    }

    private async Task OnToolWindowStrip_ToolWindowButtonClickedAsync(string buttonName)
    {
        var toolWindow = GetToolWindowFromButtonName(buttonName);
        await SwitchToolWindow(toolWindow);
    }

    private void OnToolWindowStrip_ToolPanelButtonClicked(string buttonName)
    {
        var panel = GetTerminalPanelFromButtonName(buttonName);
        ToggleTerminalPanel(panel);
    }

    private void OnShowToolWindowRequest(ShowToolWindowRequestEvent e)
    {
        if (e == null || string.IsNullOrEmpty(e.ButtonName)) return;
        // Reuse existing async handler to switch tool window
        _ = OnToolWindowStrip_ToolWindowButtonClickedAsync(e.ButtonName);
    }

    private void OnShowTerminalTabRequest(ShowTerminalTabRequestEvent e)
    {
        // Map incoming tab index to internal TerminalPanelType and ensure panel is visible
        var panelType = e.TabIndex switch
        {
            0 => TerminalPanelType.Terminal,
            1 => TerminalPanelType.Errors,
            2 => TerminalPanelType.Syntax,
            3 => TerminalPanelType.Other,
            _ => TerminalPanelType.Terminal
        };

        Dispatcher.UIThread.Post(() =>
        {
            _selectedTerminalPanel = panelType;
            _isTerminalPanelVisible = true;
            UpdateTerminalPanelVisibility();
            UpdateTerminalPanelSelection();
            SwitchTerminalTab();
        });
    }

    #region Tool Window Toggle Methods

    private ToolWindowType GetToolWindowFromButtonName(string buttonName)
    {
        // Map the button name strings from ToolWindowStrip to the enum
        return buttonName switch
        {
            "FileExplorerToggle" => ToolWindowType.FileExplorer,
            "TokensToggle" => ToolWindowType.Tokens,
            "SyntaxTreeToggle" => ToolWindowType.SyntaxTree,
            "PhaseOutputToggle" => ToolWindowType.PhaseOutput,
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
        // Find the dynamic content controls by name at runtime to avoid relying on generated fields
        var fileExplorer = this.FindControl<Control>("FileExplorerContent");
        var tokens = this.FindControl<Control>("TokensContent");
        var syntax = this.FindControl<Control>("SyntaxTreeContent");
        var phaseOutput = this.FindControl<Control>("PhaseOutputContent");

        if (fileExplorer is not null) fileExplorer.IsVisible = _selectedToolWindow == ToolWindowType.FileExplorer;
        if (tokens is not null) tokens.IsVisible = _selectedToolWindow == ToolWindowType.Tokens;
        if (syntax is not null) syntax.IsVisible = _selectedToolWindow == ToolWindowType.SyntaxTree;
        if (phaseOutput is not null) phaseOutput.IsVisible = _selectedToolWindow == ToolWindowType.PhaseOutput;
    }

    private void UpdateToolWindowSelection()
    {
        // Delegate selection styling to the ToolWindowStrip control
        ToolWindowStripControl.SetSelectedToolWindow(GetButtonNameForToolWindow(_selectedToolWindow));
    }

    private static string GetButtonNameForToolWindow(ToolWindowType toolWindow)
    {
        return toolWindow switch
        {
            ToolWindowType.FileExplorer => "FileExplorerToggle",
            ToolWindowType.Tokens => "TokensToggle",
            ToolWindowType.SyntaxTree => "SyntaxTreeToggle",
            ToolWindowType.PhaseOutput => "PhaseOutputToggle",
            _ => "FileExplorerToggle"
        };
    }

    #endregion

    #region Terminal Panel Toggle Methods

    private TerminalPanelType GetTerminalPanelFromButtonName(string buttonName)
    {
        return buttonName switch
        {
            "TerminalToggle" => TerminalPanelType.Terminal,
            // Map OutputToggle -> Lexical Errors panel, ErrorsToggle -> Syntax Errors panel
            "OutputToggle" => TerminalPanelType.Errors,
            "ErrorsToggle" => TerminalPanelType.Syntax,
            "BuildToggle" => TerminalPanelType.Other,
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
        // Delegate panel selection styling to the ToolWindowStrip control
        ToolWindowStripControl.ClearPanelSelection();
        if (_isTerminalPanelVisible)
        {
            ToolWindowStripControl.SetSelectedPanel(GetButtonNameForPanel(_selectedTerminalPanel));
        }
    }

    private static string GetButtonNameForPanel(TerminalPanelType panel)
    {
        return panel switch
        {
            TerminalPanelType.Terminal => "TerminalToggle",
            // Use OutputToggle for lexical/errors panel, ErrorsToggle for syntax panel
            TerminalPanelType.Errors => "OutputToggle",
            TerminalPanelType.Syntax => "ErrorsToggle",
            TerminalPanelType.Other => "BuildToggle",
            _ => "TerminalToggle"
        };
    }

    private void SwitchTerminalTab()
    {
        if (!_isTerminalPanelVisible)
            return;

        var tabIndex = GetTabIndexForPanel(_selectedTerminalPanel);
        // Delegate tab selection to the TerminalPanel control
        TerminalPanelControl?.SetSelectedTab(tabIndex);
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

    // Toolbar actions (OpenProject, NewFile, Save, SaveAs, Settings, window controls) moved to `Toolbar` control. Removed from MainWindow.
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
            System.Diagnostics.Debug.WriteLine("Analysis refreshed (no status event published by MainWindow)");

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

    private void HandleException(Exception ex, string context) {
        var viewModel = ViewModel;
        if (viewModel is not null)
        {
            System.Diagnostics.Debug.WriteLine($"{context}: {ex.Message}");
            // Append to OtherErrors so it appears in the UI terminal/other errors tab
            viewModel.OtherErrors = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {context}: {ex.Message}\n" + viewModel.OtherErrors;
        }
    }

    #endregion
}
