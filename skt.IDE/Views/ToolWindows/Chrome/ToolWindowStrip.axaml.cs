using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

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
            var phase = this.FindControl<Button>("PhaseOutputToggle");

            var terminal = this.FindControl<Button>("TerminalToggle");
            var output = this.FindControl<Button>("OutputToggle");
            var errors = this.FindControl<Button>("ErrorsToggle");
            var build = this.FindControl<Button>("BuildToggle");

            if (fe is not null) fe.Click += ToolWindowToggle_Click;
            if (tokens is not null) tokens.Click += ToolWindowToggle_Click;
            if (syntax is not null) syntax.Click += ToolWindowToggle_Click;
            if (semantic is not null) semantic.Click += ToolWindowToggle_Click;
            if (phase is not null) phase.Click += ToolWindowToggle_Click;

            if (terminal is not null) terminal.Click += ToolPanelToggle_Click;
            if (output is not null) output.Click += ToolPanelToggle_Click;
            if (errors is not null) errors.Click += ToolPanelToggle_Click;
            if (build is not null) build.Click += ToolPanelToggle_Click;
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
        PhaseOutputToggle.Classes.Remove(SelectedCssClass);
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
            case nameof(PhaseOutputToggle):
                PhaseOutputToggle.Classes.Add(SelectedCssClass);
                break;
        }
    }

    public void ClearPanelSelection()
    {
        TerminalToggle.Classes.Remove(SelectedCssClass);
        OutputToggle.Classes.Remove(SelectedCssClass);
        ErrorsToggle.Classes.Remove(SelectedCssClass);
        BuildToggle.Classes.Remove(SelectedCssClass);
    }

    public void SetSelectedPanel(string buttonName)
    {
        ClearPanelSelection();

        switch (buttonName)
        {
            case nameof(TerminalToggle):
                TerminalToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(OutputToggle):
                OutputToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(ErrorsToggle):
                ErrorsToggle.Classes.Add(SelectedCssClass);
                break;
            case nameof(BuildToggle):
                BuildToggle.Classes.Add(SelectedCssClass);
                break;
        }
    }
}
