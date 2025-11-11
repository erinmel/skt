using System;
using System.IO;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using skt.IDE.Services.Buss;
using skt.IDE.ViewModels.ToolWindows;
using Avalonia;
using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics;

namespace skt.IDE.Views.ToolWindows.CompilerOutput;

public partial class ErrorsView : UserControl
{
    public static readonly StyledProperty<IEnumerable<FileErrorGroup>?> GroupsSourceProperty =
        AvaloniaProperty.Register<ErrorsView, IEnumerable<FileErrorGroup>?>(nameof(GroupsSource));

    public IEnumerable<FileErrorGroup>? GroupsSource
    {
        get => GetValue(GroupsSourceProperty);
        set => SetValue(GroupsSourceProperty, value);
    }

    public static readonly StyledProperty<int> PanelTabIndexProperty =
        AvaloniaProperty.Register<ErrorsView, int>(nameof(PanelTabIndex), 1);

    /// <summary>
    /// Index of the terminal tab this ErrorsView represents (0=Terminal,1=Lexical,2=Syntax,3=Other)
    /// </summary>
    public int PanelTabIndex
    {
        get => GetValue(PanelTabIndexProperty);
        set => SetValue(PanelTabIndexProperty, value);
    }

    public ErrorsView()
    {
        InitializeComponent();
    }

    private static void FocusMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
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

        App.Messenger.Send(new OpenFileRequestEvent(filePath));

        // Use multiple delayed attempts to ensure caret is positioned after tab switch
        for (int i = 0; i < 3; i++)
        {
            var delay = (i + 1) * 50; // 50ms, 100ms, 150ms
            var line = item.Line;
            var column = item.Column;
            var path = filePath;

            DispatcherTimer.RunOnce(() =>
            {
                App.Messenger.Send(new SetCaretLineColumnRequestEvent(path, line, column));
            }, TimeSpan.FromMilliseconds(delay));
        }
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
        catch (Exception ex)
        {
            Debug.WriteLine($"ErrorsView.ComputeOffsetFromLineColumn: {ex}");
            return 0;
        }
    }
}
