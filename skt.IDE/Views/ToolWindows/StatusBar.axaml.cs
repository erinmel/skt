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
    private CancellationTokenSource? _timeAgoCts;
    private readonly TextBlock? _lineColumCountTextBlock;
    private readonly TextBlock? _encodingTextBlock;
    private StatusBarMessageEvent? _currentEvent;
    private long _messageToken;

    public StatusBar()
    {
        InitializeComponent();

        _lineColumCountTextBlock = this.FindControl<TextBlock>("LineColumCountTextBlock");
        _encodingTextBlock = this.FindControl<TextBlock>("EncodingTextBlock");

        App.EventBus.Subscribe<StatusBarMessageEvent>(OnStatusBarMessage);
        App.EventBus.Subscribe<CursorPositionEvent>(OnCursorPosition);
        App.EventBus.Subscribe<FileEncodingChangedEvent>(OnFileEncodingChanged);
        App.EventBus.Subscribe<SelectionInfoEvent>(OnSelectionInfo);

        Unloaded += (s, _) =>
        {
            App.EventBus.Unsubscribe<StatusBarMessageEvent>(OnStatusBarMessage);
            App.EventBus.Unsubscribe<CursorPositionEvent>(OnCursorPosition);
            App.EventBus.Unsubscribe<FileEncodingChangedEvent>(OnFileEncodingChanged);
            App.EventBus.Unsubscribe<SelectionInfoEvent>(OnSelectionInfo);
        };
    }

    private void OnStatusBarMessage(StatusBarMessageEvent e)
    {
        // Increment token to represent a newer message; any in-flight handlers become stale
        var token = Interlocked.Increment(ref _messageToken);

        try
        {
            _messageCts?.Cancel();
            _timeAgoCts?.Cancel();
        }
        catch (Exception)
        {
            // Ignored: cancellation may throw if already disposed concurrently
        }

        Dispatcher.UIThread.Post(() => _ = HandleStatusBarMessageAsync(e, token));
    }

    private async Task HandleStatusBarMessageAsync(StatusBarMessageEvent e, long token)
    {
        try
        {
            if (_lineColumCountTextBlock == null) return;
            if (Interlocked.Read(ref _messageToken) != token) return;

            _currentEvent = e;
            _lineColumCountTextBlock.Text = e.ShowTimeAgo ? FormatTimeAgo(e.Message, 0) : (e.Message ?? string.Empty);

            await CancelAndClearMessageCtsAsync();
            await CancelAndClearTimeAgoCtsAsync();

            StartTimeAgoIfNeeded(e, token);

            await StartDurationHandlerAsync(e, token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled error in HandleStatusBarMessageAsync: {ex}");
        }
    }

    private async Task CancelAndClearMessageCtsAsync()
    {
        var cts = _messageCts;
        if (cts == null) return;
        try
        {
            await cts.CancelAsync();
        }
        catch
        {
            // Ignored: cancellation may throw if already disposed concurrently
        }
        try
        {
            cts.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Status bar handler error: {ex}");
        }
        if (ReferenceEquals(_messageCts, cts))
            _messageCts = null;
    }

    private async Task CancelAndClearTimeAgoCtsAsync()
    {
        var cts = _timeAgoCts;
        if (cts == null) return;
        try
        {
            await cts.CancelAsync();
        }
        catch
        {
            // Ignored
        }
        try
        {
            cts.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Status bar handler error: {ex}");
        }
        if (ReferenceEquals(_timeAgoCts, cts))
            _timeAgoCts = null;
    }

    private void StartTimeAgoIfNeeded(StatusBarMessageEvent e, long token)
    {
        if (!e.ShowTimeAgo) return;
        var timeAgoCts = new CancellationTokenSource();
        _timeAgoCts = timeAgoCts;
        _ = UpdateTimeAgoAsync(e, token, timeAgoCts.Token);
    }

    private async Task StartDurationHandlerAsync(StatusBarMessageEvent e, long token)
    {
        if (e.DurationMs is not >= 0) return;

        var cts = new CancellationTokenSource();
        _messageCts = cts;
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(e.DurationMs.Value), cts.Token);

            if (_lineColumCountTextBlock != null && Interlocked.Read(ref _messageToken) == token)
            {
                _lineColumCountTextBlock.Text = string.Empty;
                _currentEvent = null;
                await CancelAndClearTimeAgoCtsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Status bar handler error: {ex}");
        }
        finally
        {
            if (_messageCts == cts)
            {
                try
                {
                    _messageCts?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Status bar handler error: {ex}");
                }
                _messageCts = null;
            }
        }
    }


    private async Task UpdateTimeAgoAsync(StatusBarMessageEvent e, long token, CancellationToken ct)
    {
        int minutes = 1;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
                if (ct.IsCancellationRequested) break;

                // Stop if a newer message arrived
                if (Interlocked.Read(ref _messageToken) != token) break;

                if (_lineColumCountTextBlock != null && _currentEvent == e)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_lineColumCountTextBlock != null && _currentEvent == e && Interlocked.Read(ref _messageToken) == token)
                        {
                            _lineColumCountTextBlock.Text = FormatTimeAgo(e.Message, minutes);
                        }
                    });
                    minutes++;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Time-ago updater error: {ex}");
        }
    }

    private void OnCursorPosition(CursorPositionEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_lineColumCountTextBlock != null)
                _lineColumCountTextBlock.Text = $"Line: {e.Line} Col: {e.Column}";
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

    private void OnSelectionInfo(SelectionInfoEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_lineColumCountTextBlock == null) return;
            if (e.CharCount <= 0)
            {
                _lineColumCountTextBlock.Text = string.Empty;
                return;
            }
            _lineColumCountTextBlock.Text = $"{e.StartLine}:{e.StartColumn} - {e.EndLine}:{e.EndColumn} ({e.CharCount} chars, {e.LineBreakCount} line breaks)";
        });
    }

    private static string FormatTimeAgo(string? message, int minutes)
    {
        var baseText = message ?? string.Empty;
        var unit = minutes == 1 ? "minute" : "minutes";
        return $"{baseText} ({minutes} {unit} ago)";
    }
}
