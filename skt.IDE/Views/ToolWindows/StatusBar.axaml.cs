using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using skt.IDE.ViewModels;
using skt.IDE.Services.Buss;

namespace skt.IDE.Views.ToolWindows;

public partial class StatusBar : UserControl
{
    private CancellationTokenSource? _messageCts;
    private TextBlock? _statusTextBlock;
    private TextBlock? _lineTextBlock;
    private TextBlock? _colTextBlock;
    private TextBlock? _encodingTextBlock;

    public StatusBar()
    {
        InitializeComponent();

        // Cache named controls so the status bar owns its UI state
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        _lineTextBlock = this.FindControl<TextBlock>("LineTextBlock");
        _colTextBlock = this.FindControl<TextBlock>("ColTextBlock");
        _encodingTextBlock = this.FindControl<TextBlock>("EncodingTextBlock");

        // Subscribe to status events on the global bus
        App.EventBus.Subscribe<StatusBarMessageEvent>(OnStatusBarMessage);
        App.EventBus.Subscribe<CursorPositionEvent>(OnCursorPosition);
        App.EventBus.Subscribe<FileEncodingChangedEvent>(OnFileEncodingChanged);

        // Unsubscribe when unloaded
        Unloaded += (s, _) =>
        {
            App.EventBus.Unsubscribe<StatusBarMessageEvent>(OnStatusBarMessage);
            App.EventBus.Unsubscribe<CursorPositionEvent>(OnCursorPosition);
            App.EventBus.Unsubscribe<FileEncodingChangedEvent>(OnFileEncodingChanged);
        };
    }

    private void OnStatusBarMessage(StatusBarMessageEvent e)
    {
        // Kick off the async handler on the UI thread but avoid async-void lambdas
        Dispatcher.UIThread.Post(() => _ = HandleStatusBarMessageAsync(e));
    }

    private async Task HandleStatusBarMessageAsync(StatusBarMessageEvent e)
    {
        try
        {
            // Update the status text directly so the StatusBar is the single owner of UI state
            if (_statusTextBlock != null)
            {
                _statusTextBlock.Text = e.Message ?? string.Empty;

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
                        if (_statusTextBlock != null && _statusTextBlock.Text == e.Message)
                        {
                            _statusTextBlock.Text = "Ready";
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // expected when a new message replaces the old one
                    }
                    catch (Exception ex)
                    {
                        // Log unexpected exceptions without crashing the UI
                        System.Diagnostics.Debug.WriteLine($"Status bar handler error: {ex}");
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled error in HandleStatusBarMessageAsync: {ex}");
        }
    }

    private void OnCursorPosition(CursorPositionEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_lineTextBlock != null)
                _lineTextBlock.Text = $"Line: {e.Line}";
            if (_colTextBlock != null)
                _colTextBlock.Text = $"Col: {e.Column}";
        });
    }

    private void OnFileEncodingChanged(FileEncodingChangedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_encodingTextBlock != null)
                _encodingTextBlock.Text = $"Encoding: {e.EncodingName}";
        });
    }

    // Example placeholder for a future click handler - publish a status message instead
    private void OnStatusClick(object? sender, RoutedEventArgs e)
    {
        App.EventBus.Publish(new StatusBarMessageEvent("TODO: Status click forwarded to main", 3000));
    }
}
