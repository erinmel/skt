using Avalonia.Controls;
using skt.IDE.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using skt.IDE.Services.Buss;
using CommunityToolkit.Mvvm.Messaging;

namespace skt.IDE.Views.Shell;

public partial class MainWindow : Window
{
    private enum ToolWindowType
    {
        FileExplorer,
        Tokens,
        PCode,
        SyntaxTree,
        SemanticTree
    }

    private enum TerminalPanelType
    {
        Terminal,
        TokenErrors,
        SyntaxErrors,
        SemanticErrors,
        SymbolTable
    }

    private static class ToolWindowTitles
    {
        public const string FileExplorer = "File Explorer";
        public const string Tokens = "Tokens";
        public const string SyntaxTree = "Syntax Tree";
        public const string SemanticTree = "Semantic Tree";
        public const string PCode = "P-Code";
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
    private ToolWindowType _selectedToolWindow = ToolWindowType.FileExplorer;
    private TerminalPanelType _selectedTerminalPanel = TerminalPanelType.Terminal;
    private bool _isTerminalPanelVisible;
    private double _previousTerminalPanelHeight = 200.0; // Remember last height, default 200px

    public MainWindow()
    {
        InitializeComponent();
        CenterAndSizeWindowToScreen();
        InitializeUi();

        // Keep ViewModel in sync when the WindowState changes (toolbar may change it directly)
        PropertyChanged += OnWindowPropertyChanged;

        // Wire up decoupled ToolWindowStrip events
        ToolWindowStripControl.ToolWindowButtonClicked += OnToolWindowStrip_ToolWindowButtonClicked;
        ToolWindowStripControl.ToolPanelButtonClicked += OnToolWindowStrip_ToolPanelButtonClicked;

        // Subscribe to global requests to show tool windows or terminal tabs
        App.Messenger.Register<ShowToolWindowRequestEvent>(this, (r, m) => OnShowToolWindowRequest(m));
        App.Messenger.Register<ShowTerminalTabRequestEvent>(this, (r, m) => OnShowTerminalTabRequest(m));
        App.Messenger.Register<ProjectLoadedEvent>(this, (r, m) => OnProjectLoaded(m));

        // Subscribe to error events to update icons
        App.Messenger.Register<LexicalAnalysisCompletedEvent>(this, (r, m) => UpdateErrorIcons());
        App.Messenger.Register<LexicalAnalysisFailedEvent>(this, (r, m) => UpdateErrorIcons());
        App.Messenger.Register<SyntaxAnalysisCompletedEvent>(this, (r, m) => UpdateErrorIcons());
        App.Messenger.Register<SyntaxAnalysisFailedEvent>(this, (r, m) => UpdateErrorIcons());
        App.Messenger.Register<SemanticAnalysisCompletedEvent>(this, (r, m) => UpdateErrorIcons());
        App.Messenger.Register<SemanticAnalysisFailedEvent>(this, (r, m) => UpdateErrorIcons());
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
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ShowTerminalTabRequest received for tab {e.TabIndex}");
        
        // Map incoming tab index to internal TerminalPanelType and ensure panel is visible
        var panelType = e.TabIndex switch
        {
            0 => TerminalPanelType.Terminal,
            1 => TerminalPanelType.TokenErrors,
            2 => TerminalPanelType.SyntaxErrors,
            3 => TerminalPanelType.SemanticErrors,
            4 => TerminalPanelType.SymbolTable,
            _ => TerminalPanelType.Terminal
        };

        Dispatcher.UIThread.Post(() =>
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting terminal panel to {panelType}, visible = true");
            _selectedTerminalPanel = panelType;
            _isTerminalPanelVisible = true;
            UpdateTerminalPanelVisibility();
            UpdateTerminalPanelSelection();
            SwitchTerminalTab();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Terminal panel should now be visible");
        });
    }

    private void OnProjectLoaded(ProjectLoadedEvent e)
    {
        if (e.Success)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await SwitchToolWindow(ToolWindowType.FileExplorer);
            });
        }
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
            "SemanticTreeToggle" => ToolWindowType.SemanticTree,
            "PCodeToggle" => ToolWindowType.PCode,
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
            ToolWindowType.SemanticTree => ToolWindowTitles.SemanticTree,
            ToolWindowType.PCode => ToolWindowTitles.PCode,
            _ => ToolWindowTitles.FileExplorer
        };
    }

    private void UpdateToolWindowVisibility()
    {
        // Find the dynamic content controls by name at runtime to avoid relying on generated fields
        var fileExplorer = this.FindControl<Control>("FileExplorerContent");
        var tokens = this.FindControl<Control>("TokensContent");
        var pcode = this.FindControl<Control>("PCodeContent");
        var syntax = this.FindControl<Control>("SyntaxTreeContent");
        var semantic = this.FindControl<Control>("SemanticTreeContent");

        if (fileExplorer is not null) fileExplorer.IsVisible = _selectedToolWindow == ToolWindowType.FileExplorer;
        if (tokens is not null) tokens.IsVisible = _selectedToolWindow == ToolWindowType.Tokens;
        if (pcode is not null) pcode.IsVisible = _selectedToolWindow == ToolWindowType.PCode;
        if (syntax is not null) syntax.IsVisible = _selectedToolWindow == ToolWindowType.SyntaxTree;
        if (semantic is not null) semantic.IsVisible = _selectedToolWindow == ToolWindowType.SemanticTree;
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
            ToolWindowType.PCode => "PCodeToggle",
            ToolWindowType.SyntaxTree => "SyntaxTreeToggle",
            ToolWindowType.SemanticTree => "SemanticTreeToggle",
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
            "TokenErrorsToggle" => TerminalPanelType.TokenErrors,
            "SyntaxErrorsToggle" => TerminalPanelType.SyntaxErrors,
            "SemanticErrorsToggle" => TerminalPanelType.SemanticErrors,
            "SymbolTablePanelToggle" => TerminalPanelType.SymbolTable,
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
        var currentHeight = terminalRow.Height.Value;

        if (_isTerminalPanelVisible)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Making panel visible:");
            System.Diagnostics.Debug.WriteLine($"  Current height: {currentHeight}px");
            System.Diagnostics.Debug.WriteLine($"  Saved height: {_previousTerminalPanelHeight}px");
            
            // Only restore saved height if panel is currently hidden (0)
            // Otherwise keep current height (user may have just resized)
            if (currentHeight == 0)
            {
                terminalRow.Height = new GridLength(_previousTerminalPanelHeight, GridUnitType.Pixel);
                System.Diagnostics.Debug.WriteLine($"  Applied saved height: {_previousTerminalPanelHeight}px");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"  Keeping current height: {currentHeight}px");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Hiding panel:");
            System.Diagnostics.Debug.WriteLine($"  Current height: {currentHeight}px");
            
            // Save current height before hiding (if not already 0)
            if (currentHeight > 0)
            {
                _previousTerminalPanelHeight = currentHeight;
                System.Diagnostics.Debug.WriteLine($"  Saved height: {_previousTerminalPanelHeight}px");
            }
            
            // Hide panel
            terminalRow.Height = new GridLength(0, GridUnitType.Pixel);
            System.Diagnostics.Debug.WriteLine($"  Panel hidden");
        }
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
            TerminalPanelType.TokenErrors => "TokenErrorsToggle",
            TerminalPanelType.SyntaxErrors => "SyntaxErrorsToggle",
            TerminalPanelType.SemanticErrors => "SemanticErrorsToggle",
            TerminalPanelType.SymbolTable => "SymbolTablePanelToggle",
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
            TerminalPanelType.TokenErrors => 1,   // Lexical/Token Errors tab
            TerminalPanelType.SyntaxErrors => 2,   // Syntax Errors tab
            TerminalPanelType.SemanticErrors => 3,   // Semantic Errors tab
            TerminalPanelType.SymbolTable => 4,    // Symbol Table tab
            _ => 0
        };
    }

    private void UpdateErrorIcons()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = ViewModel;
            if (vm?.Errors == null) return;

            // Update icon based on whether there are errors in each category
            var hasTokenErrors = vm.Errors.LexicalGroups.Any(g => g.Errors.Count > 0);
            var hasSyntaxErrors = vm.Errors.SyntaxGroups.Any(g => g.Errors.Count > 0);
            var hasSemanticErrors = vm.Errors.SemanticGroups.Any(g => g.Errors.Count > 0);

            ToolWindowStripControl.SetTokenErrorsIconAlert(hasTokenErrors);
            ToolWindowStripControl.SetSyntaxErrorsIconAlert(hasSyntaxErrors);
            ToolWindowStripControl.SetSemanticErrorsIconAlert(hasSemanticErrors);
        });
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

    private void CenterAndSizeWindowToScreen()
    {
        var screen = Screens.Primary;
        if (screen?.WorkingArea == null) return;

        var workingArea = screen.WorkingArea;

        // Calculate width as 80% of screen width
        var targetWidth = workingArea.Width * 0.8;

        // Calculate height as 66% of the calculated width (maintaining aspect ratio)
        var targetHeight = targetWidth * 0.66;

        // Ensure the height doesn't exceed screen boundaries
        if (targetHeight > workingArea.Height * 0.9)
        {
            targetHeight = workingArea.Height * 0.9;
            // Recalculate width to maintain aspect ratio if height was clamped
            targetWidth = targetHeight / 0.66;
        }

        Width = targetWidth;
        Height = targetHeight;

        // Center the window on screen
        Position = new PixelPoint(
            (int)(workingArea.X + (workingArea.Width - targetWidth) / 2),
            (int)(workingArea.Y + (workingArea.Height - targetHeight) / 2)
        );
    }
}
