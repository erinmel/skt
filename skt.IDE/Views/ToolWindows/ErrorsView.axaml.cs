using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using skt.IDE.Services.Buss;
using skt.IDE.ViewModels.ToolWindows;

namespace skt.IDE.Views.ToolWindows;

public partial class ErrorsView : UserControl
{
    public ErrorsView()
    {
        InitializeComponent();
    }

    private static void FocusMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var win = desktop.MainWindow;
            if (win != null)
            {
                win.Activate();
                win.Focus();
            }
        }
    }

    private void OnErrorDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var filePath = btn.Tag as string ?? string.Empty;
        if (btn.DataContext is not ErrorItem item) return;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        FocusMainWindow();

        App.EventBus.Publish(new OpenFileRequestEvent(filePath));
        // Use line/column event instead of computing raw offset
        Dispatcher.UIThread.Post(() =>
        {
            App.EventBus.Publish(new SetCaretLineColumnRequestEvent(filePath, item.Line, item.Column));
        }, DispatcherPriority.Background);
    }

    private static int ComputeOffsetFromLineColumn(string filePath, int line, int column)
    {
        // No longer used; retained temporarily in case of fallback needs
        try
        {
            var text = File.ReadAllText(filePath);
            if (text.Length == 0) return 0;
            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            int targetLine = Math.Clamp(line, 1, lines.Length);
            int offset = 0;
            for (int i = 0; i < targetLine - 1; i++)
                offset += lines[i].Length + 1;
            int col = Math.Clamp(column, 1, lines[targetLine - 1].Length + 1);
            offset += col - 1;
            return Math.Clamp(offset, 0, text.Length);
        }
        catch { return 0; }
    }
}
