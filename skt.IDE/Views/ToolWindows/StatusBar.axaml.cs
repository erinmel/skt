using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using skt.IDE.ViewModels;
using skt.IDE.Services.Buss;
using skt.IDE;

namespace skt.IDE.Views.ToolWindows;

public partial class StatusBar : UserControl
{
    private CancellationTokenSource? _messageCts;

    public StatusBar()
    {
        InitializeComponent();

        // Subscribe to StatusBarBus events
        App.EventBus.Subscribe<StatusBarMessageEvent>(OnStatusBarMessage);
        App.EventBus.Subscribe<CursorPositionEvent>(OnCursorPosition);
        App.EventBus.Subscribe<FileEncodingChangedEvent>(OnFileEncodingChanged);

        // Unsubscribe when unloaded
        Unloaded += (_, __) =>
        {
            App.EventBus.Unsubscribe<StatusBarMessageEvent>(OnStatusBarMessage);
            App.EventBus.Unsubscribe<CursorPositionEvent>(OnCursorPosition);
            App.EventBus.Unsubscribe<FileEncodingChangedEvent>(OnFileEncodingChanged);
        };
    }

    private void OnStatusBarMessage(StatusBarMessageEvent e)
    {
        // Marshal to UI thread
        Dispatcher.UIThread.Post(async () =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.StatusMessage = e.Message ?? string.Empty;

                // Cancel any existing scheduled clear
                _messageCts?.Cancel();
                _messageCts?.Dispose();
                _messageCts = null;

                // If duration is null or negative -> infinite, do nothing
                if (e.DurationMs.HasValue && e.DurationMs.Value >= 0)
                {
                    var cts = new CancellationTokenSource();
                    _messageCts = cts;
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(e.DurationMs.Value), cts.Token);
                        // Only clear if message hasn't changed since
                        if (DataContext is MainWindowViewModel vm2 && vm2.StatusMessage == e.Message)
                        {
                            vm2.StatusMessage = "Ready";
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // ignore
                    }
                    finally
                    {
                        if (_messageCts == cts)
                        {
                            _messageCts?.Dispose();
                            _messageCts = null;
                        }
                    }
                }
            }
        });
    }

    private void OnCursorPosition(CursorPositionEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.CurrentLine = e.Line;
                vm.CurrentColumn = e.Column;
            }
        });
    }

    private void OnFileEncodingChanged(FileEncodingChangedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.FileEncoding = e.EncodingName;
            }
        });
    }

    // Example placeholder for a future click handler - leave as TODO
    private void OnStatusClick(object? sender, RoutedEventArgs e)
    {
        // TODO: forward click to MainWindow or publish an event
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StatusMessage = "TODO: Status click forwarded to main";
        }
    }
}
