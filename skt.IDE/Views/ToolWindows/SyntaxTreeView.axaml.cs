using Avalonia.Controls;
using skt.IDE.ViewModels.ToolWindows;
using skt.IDE.Services.Buss;
using System;

namespace skt.IDE.Views.ToolWindows;

public partial class SyntaxTreeView : UserControl
{
    private SyntaxTreeViewModel? ViewModel => DataContext as SyntaxTreeViewModel;

    public SyntaxTreeView()
    {
        InitializeComponent();

        // Subscribe to syntax analysis events
        var bus = App.EventBus;
        bus.Subscribe<SyntaxAnalysisCompletedEvent>(OnSyntaxAnalysisCompleted);
        bus.Subscribe<SyntaxAnalysisFailedEvent>(OnSyntaxAnalysisFailed);
    }

    private void OnSyntaxAnalysisCompleted(SyntaxAnalysisCompletedEvent e)
    {
        if (ViewModel != null)
        {
            ViewModel.UpdateTree(e.Ast, e.Errors);
        }
    }

    private void OnSyntaxAnalysisFailed(SyntaxAnalysisFailedEvent e)
    {
        if (ViewModel != null)
        {
            ViewModel.Clear();
        }
    }
}
