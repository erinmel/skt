using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using skt.IDE.ViewModels;
using skt.IDE.Constants;
using System;
using System.Linq;

namespace skt.IDE.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
    private ToolWindowType _selectedToolWindow = ToolWindowType.FileExplorer;

    public MainWindow()
    {
        InitializeComponent();
        UpdateToolWindowButtons();

        // Subscribe to theme changes to update icons
        if (Application.Current != null)
        {
            Application.Current.ActualThemeVariantChanged += OnThemeChanged;
        }

        // Set initial tool window
        ShowToolWindow(ToolWindowType.FileExplorer);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Update all SVG icons when theme changes
        UpdateAllIcons();
    }

    private void UpdateAllIcons()
    {
        // This method will be called when theme changes to refresh all icons
        var currentTheme = Application.Current?.ActualThemeVariant?.Key;
        // Force a refresh of the visual tree to update icon sources
        InvalidateVisual();
    }

    private async void OpenProjectButton_Click(object? sender, RoutedEventArgs e)
    {
        var folderOptions = new FolderPickerOpenOptions
        {
            Title = "Select Project Folder",
            AllowMultiple = false
        };

        var result = await StorageProvider.OpenFolderPickerAsync(folderOptions);

        if (result.Any())
        {
            var selectedFolder = result[0];
            if (ViewModel != null)
            {
                await ViewModel.OpenProject(selectedFolder.Path.LocalPath);
            }
        }
    }

    private void FileMenuButton_Click(object? sender, RoutedEventArgs e)
    {
        var contextMenu = new ContextMenu();

        var openProjectItem = new MenuItem { Header = "Open Project" };
        openProjectItem.Click += OpenProjectButton_Click;

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (s, args) => Close();

        contextMenu.Items.Add(openProjectItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(exitItem);

        contextMenu.Open(sender as Control);
    }

    private void EditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (ViewModel != null && sender is TextBox textBox)
        {
            ViewModel.EditorContent = textBox.Text ?? string.Empty;
        }
    }

    // Tool Window Toggle Methods
    private void FileExplorerToggle_Click(object? sender, RoutedEventArgs e)
    {
        ShowToolWindow(ToolWindowType.FileExplorer);
    }

    private void TokensToggle_Click(object? sender, RoutedEventArgs e)
    {
        ShowToolWindow(ToolWindowType.Tokens);
    }

    private void SyntaxTreeToggle_Click(object? sender, RoutedEventArgs e)
    {
        ShowToolWindow(ToolWindowType.SyntaxTree);
    }

    private void PhaseOutputToggle_Click(object? sender, RoutedEventArgs e)
    {
        ShowToolWindow(ToolWindowType.PhaseOutput);
    }

    // Terminal Panel Toggle Methods
    private void TerminalToggle_Click(object? sender, RoutedEventArgs e)
    {
        ToggleTerminalPanel(TerminalPanelType.Terminal);
    }

    private void OutputToggle_Click(object? sender, RoutedEventArgs e)
    {
        ToggleTerminalPanel(TerminalPanelType.Other);
    }

    private void ErrorsToggle_Click(object? sender, RoutedEventArgs e)
    {
        ToggleTerminalPanel(TerminalPanelType.Errors);
    }

    private void BuildToggle_Click(object? sender, RoutedEventArgs e)
    {
        ToggleTerminalPanel(TerminalPanelType.Other);
    }

    private void ShowToolWindow(ToolWindowType toolWindow)
    {
        _selectedToolWindow = toolWindow;

        // Hide all content panels
        FileExplorerContent.IsVisible = false;
        TokensContent.IsVisible = false;
        SyntaxTreeContent.IsVisible = false;
        PhaseOutputContent.IsVisible = false;

        // Show selected panel
        switch (toolWindow)
        {
            case ToolWindowType.FileExplorer:
                FileExplorerContent.IsVisible = true;
                if (ViewModel != null) ViewModel.SelectedToolWindowTitle = UIConstants.ToolWindowTitles.FileExplorer;
                break;
            case ToolWindowType.Tokens:
                TokensContent.IsVisible = true;
                if (ViewModel != null) ViewModel.SelectedToolWindowTitle = UIConstants.ToolWindowTitles.Tokens;
                break;
            case ToolWindowType.SyntaxTree:
                SyntaxTreeContent.IsVisible = true;
                if (ViewModel != null) ViewModel.SelectedToolWindowTitle = UIConstants.ToolWindowTitles.SyntaxTree;
                break;
            case ToolWindowType.PhaseOutput:
                PhaseOutputContent.IsVisible = true;
                if (ViewModel != null) ViewModel.SelectedToolWindowTitle = UIConstants.ToolWindowTitles.PhaseOutput;
                break;
        }

        UpdateToolWindowButtons();
    }

    private void ToggleTerminalPanel(TerminalPanelType panel)
    {
        var terminalRow = RootGrid.RowDefinitions[2];

        if (terminalRow.Height.Value == 0)
        {
            // Show terminal panel
            terminalRow.Height = new GridLength(200, GridUnitType.Pixel);
        }
        else
        {
            // Hide terminal panel
            terminalRow.Height = new GridLength(0);
        }

        // Switch to the requested tab
        var tabIndex = panel switch
        {
            TerminalPanelType.Terminal => 0,
            TerminalPanelType.Errors => 1,
            TerminalPanelType.Syntax => 2,
            TerminalPanelType.Other => 3,
            _ => 0
        };

        TerminalTabView.SelectedIndex = tabIndex;
    }

    private void UpdateToolWindowButtons()
    {
        // Remove selected class from all buttons
        FileExplorerToggle.Classes.Remove(UIConstants.SelectedCssClass);
        TokensToggle.Classes.Remove(UIConstants.SelectedCssClass);
        SyntaxTreeToggle.Classes.Remove(UIConstants.SelectedCssClass);
        PhaseOutputToggle.Classes.Remove(UIConstants.SelectedCssClass);

        // Add selected class to current button
        switch (_selectedToolWindow)
        {
            case ToolWindowType.FileExplorer:
                FileExplorerToggle.Classes.Add(UIConstants.SelectedCssClass);
                break;
            case ToolWindowType.Tokens:
                TokensToggle.Classes.Add(UIConstants.SelectedCssClass);
                break;
            case ToolWindowType.SyntaxTree:
                SyntaxTreeToggle.Classes.Add(UIConstants.SelectedCssClass);
                break;
            case ToolWindowType.PhaseOutput:
                PhaseOutputToggle.Classes.Add(UIConstants.SelectedCssClass);
                break;
        }
    }
}
