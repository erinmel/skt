using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using skt.IDE.Services.Buss;

namespace skt.IDE.Views.ToolWindows.Terminal;

public partial class TerminalPanel : UserControl
{
    public TerminalPanel()
    {
        InitializeComponent();
        
        // Subscribe to terminal events
        App.Messenger.Register<PCodeExecutionOutputEvent>(this, (_, m) => OnExecutionOutput(m));
        App.Messenger.Register<ClearTerminalRequestEvent>(this, (_, m) => OnClearTerminal(m));
        
        // Clean up on unload
        Unloaded += (_, _) => App.Messenger.UnregisterAll(this);
    }

    public void SetSelectedTab(int index)
    {
        TerminalTabView.SelectedIndex = index;
    }

    private void OnExecutionOutput(PCodeExecutionOutputEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var textBox = this.FindControl<TextBox>("TerminalTextBox");
            if (textBox != null)
            {
                textBox.Text += e.Output;
                
                // Scroll to end
                var scrollViewer = this.FindControl<ScrollViewer>("TerminalScrollViewer");
                scrollViewer?.ScrollToEnd();
            }
        });
    }

    private void OnClearTerminal(ClearTerminalRequestEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var textBox = this.FindControl<TextBox>("TerminalTextBox");
            if (textBox != null)
            {
                textBox.Text = string.Empty;
            }
        });
    }
}
