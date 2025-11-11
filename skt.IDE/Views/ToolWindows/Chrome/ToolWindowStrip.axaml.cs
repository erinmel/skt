using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using skt.IDE.Views.Shell;

namespace skt.IDE.Views.ToolWindows.Chrome;

public partial class ToolWindowStrip : UserControl
{
    private const string SelectedCssClass = "selected";

    public event Action<string>? ToolWindowButtonClicked;
    public event Action<string>? ToolPanelButtonClicked;

    public ToolWindowStrip()
    {
        InitializeComponent();

        // Wire up button clicks programmatically (XAML Click handlers were removed)
        try
        {
            var fe = this.FindControl<Button>("FileExplorerToggle");
            var tokens = this.FindControl<Button>("TokensToggle");
            var syntax = this.FindControl<Button>("SyntaxTreeToggle");
            var semantic = this.FindControl<Button>("SemanticTreeToggle");

            var terminal = this.FindControl<Button>("TerminalToggle");
            var tokenErrors = this.FindControl<Button>("TokenErrorsToggle");
            var syntaxErrors = this.FindControl<Button>("SyntaxErrorsToggle");
            var semanticErrors = this.FindControl<Button>("SemanticErrorsToggle");
            var symbolTablePanel = this.FindControl<Button>("SymbolTablePanelToggle");

            if (fe is not null) fe.Click += ToolWindowToggle_Click;
            if (tokens is not null) tokens.Click += ToolWindowToggle_Click;
            if (syntax is not null) syntax.Click += ToolWindowToggle_Click;
            if (semantic is not null) semantic.Click += ToolWindowToggle_Click;

            if (terminal is not null) terminal.Click += ToolPanelToggle_Click;
            if (tokenErrors is not null) tokenErrors.Click += ToolPanelToggle_Click;
            if (syntaxErrors is not null) syntaxErrors.Click += ToolPanelToggle_Click;
            if (semanticErrors is not null) semanticErrors.Click += ToolPanelToggle_Click;
            if (symbolTablePanel is not null) symbolTablePanel.Click += ToolPanelToggle_Click;
        }
        catch(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error wiring up ToolWindowStrip buttons: {ex.Message}");
        }
    }

    private void ToolWindowToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || string.IsNullOrEmpty(button.Name))
            return;

        OnToolWindowButtonClicked(button.Name);
    }

    private void ToolPanelToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || string.IsNullOrEmpty(button.Name))
            return;

        OnToolPanelButtonClicked(button.Name);
    }

    // Standard protected raiser methods so static analyzers can detect the event being raised
    protected virtual void OnToolWindowButtonClicked(string buttonName)
    {
        ToolWindowButtonClicked?.Invoke(buttonName);
    }

    protected virtual void OnToolPanelButtonClicked(string buttonName)
    {
        ToolPanelButtonClicked?.Invoke(buttonName);
    }

    private void ClearToolWindowSelection()
    {
        FileExplorerToggle.Classes.Remove(SelectedCssClass);
        TokensToggle.Classes.Remove(SelectedCssClass);
        SyntaxTreeToggle.Classes.Remove(SelectedCssClass);
        SemanticTreeToggle.Classes.Remove(SelectedCssClass);
    }

    public void SetSelectedToolWindow(string buttonName)
    {
        ClearToolWindowSelection();

        switch (buttonName)
        {
            case nameof(FileExplorerToggle):
                FileExplorerToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(TokensToggle):
                TokensToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(SyntaxTreeToggle):
                SyntaxTreeToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(SemanticTreeToggle):
                SemanticTreeToggle.Classes.Add(SelectedCssClass);
                break;
        }
    }

    public void ClearPanelSelection()
    {
        TerminalToggle.Classes.Remove(SelectedCssClass);
        TokenErrorsToggle.Classes.Remove(SelectedCssClass);
        SyntaxErrorsToggle.Classes.Remove(SelectedCssClass);
        SemanticErrorsToggle.Classes.Remove(SelectedCssClass);
        SymbolTablePanelToggle.Classes.Remove(SelectedCssClass);
    }

    public void SetSelectedPanel(string buttonName)
    {
        ClearPanelSelection();

        switch (buttonName)
        {
            case nameof(TerminalToggle):
                TerminalToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(TokenErrorsToggle):
                TokenErrorsToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(SyntaxErrorsToggle):
                SyntaxErrorsToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(SemanticErrorsToggle):
                SemanticErrorsToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(SymbolTablePanelToggle):
                SymbolTablePanelToggle.Classes.Add(SelectedCssClass);
                break;
        }
    }

    public void SetTokenErrorsIconAlert(bool hasErrors)
    {
        var icon = this.FindControl<SktIcon>("TokenErrorsIcon");
        if (icon != null)
        {
            icon.IconKey = hasErrors ? "Icon.TokenizationErrorsAlert" : "Icon.TokenizationErrors";
        }
    }

    public void SetSyntaxErrorsIconAlert(bool hasErrors)
    {
        var icon = this.FindControl<SktIcon>("SyntaxErrorsIcon");
        if (icon != null)
        {
            icon.IconKey = hasErrors ? "Icon.SyntaxErrorsAlert" : "Icon.SyntaxErrors";
        }
    }

    public void SetSemanticErrorsIconAlert(bool hasErrors)
    {
        var icon = this.FindControl<SktIcon>("SemanticErrorsIcon");
        if (icon != null)
        {
            icon.IconKey = hasErrors ? "Icon.SemanticErrorsAlert" : "Icon.SemanticErrors";
        }
    }
}

