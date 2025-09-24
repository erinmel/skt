using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using skt.IDE.Services.Buss;

namespace skt.IDE.Views.ToolWindows;

public partial class StatusBar : UserControl
{
    private CancellationTokenSource? _messageCts;
    private CancellationTokenSource? _timeAgoCts;
    private Task? _durationTask;
    private Task? _timeAgoTask;
    private readonly TextBlock? _lineColumCountTextBlock;
    private readonly TextBlock? _encodingTextBlock;
    private readonly TextBlock? _statusTextBlock;
    private long _messageToken;

    public StatusBar()
    {
        InitializeComponent();

        _lineColumCountTextBlock = this.FindControl<TextBlock>("LineColumCountTextBlock");
        _encodingTextBlock = this.FindControl<TextBlock>("EncodingTextBlock");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");

        App.EventBus.Subscribe<StatusBarMessageEvent>(OnStatusBarMessage);
        App.EventBus.Subscribe<CursorPositionEvent>(OnCursorPosition);
        App.EventBus.Subscribe<FileEncodingChangedEvent>(OnFileEncodingChanged);
        App.EventBus.Subscribe<SelectionInfoEvent>(OnSelectionInfo);

        Unloaded += (_, _) =>
        {
            App.EventBus.Unsubscribe<StatusBarMessageEvent>(OnStatusBarMessage);
            App.EventBus.Unsubscribe<CursorPositionEvent>(OnCursorPosition);
            App.EventBus.Unsubscribe<FileEncodingChangedEvent>(OnFileEncodingChanged);
            App.EventBus.Unsubscribe<SelectionInfoEvent>(OnSelectionInfo);
        };
    }

    private async void OnStatusBarMessage(StatusBarMessageEvent e)
    {
        // Increment token to represent a newer message; any in-flight handlers become stale
        var token = Interlocked.Increment(ref _messageToken);

        // Ensure any previous handlers are cancelled and disposed before starting a new one
        await CancelAndClearMessageCtsAsync();
        await CancelAndClearTimeAgoCtsAsync();

        var receivedAt = DateTimeOffset.UtcNow;
        _ = HandleStatusBarMessageAsync(e, token, receivedAt);
    }

    private Task HandleStatusBarMessageAsync(StatusBarMessageEvent e, long token, DateTimeOffset receivedAt)
    {
        try
        {
            if (_statusTextBlock == null || Interlocked.Read(ref _messageToken) != token)
            {
                return Task.CompletedTask;
            }
            _statusTextBlock.Text = e.ShowTimeAgo ? FormatTimeAgo(e.Message, 0) : (e.Message);

            if (e.ShowTimeAgo)
            {
                _timeAgoTask = StartTimeAgoHandlerAsync(e, token, receivedAt);
            }

            _durationTask = StartDurationHandlerAsync(e, token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled error in HandleStatusBarMessageAsync: {ex}");
        }

        return Task.CompletedTask;
    }

    private async Task CancelAndClearMessageCtsAsync()
    {
        try
        {
            var cts = _messageCts;
            if (cts == null)
            {
                var dt = _durationTask;
                if (dt == null) return;
                await dt;
                _durationTask = null;
                return;
            }

            await cts.CancelAsync();

            var running = _durationTask;
            if (running != null)
            {
                 await running;
                _durationTask = null;
            }

            cts.Dispose();
            if (ReferenceEquals(_messageCts, cts))
                _messageCts = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Status bar handler error: {ex}");
        }
    }

    private async Task CancelAndClearTimeAgoCtsAsync()
    {
        var cts = _timeAgoCts;
        var running = _timeAgoTask;
        if (cts == null && running == null)
            return;

        try
        {
            if (cts != null)
                await cts.CancelAsync();

            if (running != null)
                await running;

            _timeAgoTask = null;

            if (cts != null)
            {
                cts.Dispose();
                if (ReferenceEquals(_timeAgoCts, cts))
                    _timeAgoCts = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Status bar handler error: {ex}");
        }
    }

    private async Task StartDurationHandlerAsync(StatusBarMessageEvent e, long token)
    {
        if (e.DurationMs is not >= 0) return;

        var cts = new CancellationTokenSource();
        _messageCts = cts;
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(e.DurationMs.Value), cts.Token);

            if (Interlocked.Read(ref _messageToken) == token)
            {
                // Clear the status text area when the message duration completes
                if (_statusTextBlock != null)
                    _statusTextBlock.Text = string.Empty;
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

    private async Task StartTimeAgoHandlerAsync(StatusBarMessageEvent e, long token, DateTimeOffset receivedAt)
    {
        var cts = new CancellationTokenSource();
        _timeAgoCts = cts;

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (Interlocked.Read(ref _messageToken) != token) break;

                    TryUpdateTimeAgo(e, token, receivedAt);

                    await Task.Delay(TimeSpan.FromSeconds(60), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Status bar time-ago handler error: {ex}");
        }
        finally
        {
            if (_timeAgoCts == cts)
            {
                try
                {
                    _timeAgoCts?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Status bar handler error: {ex}");
                }
                _timeAgoCts = null;
            }
        }
    }

    private void TryUpdateTimeAgo(StatusBarMessageEvent e, long token, DateTimeOffset receivedAt)
    {
        var minutes = (int)Math.Floor((DateTimeOffset.UtcNow - receivedAt).TotalMinutes);
        if (minutes < 0) minutes = 0;

        if (Interlocked.Read(ref _messageToken) != token) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (Interlocked.Read(ref _messageToken) != token) return;
            if (_statusTextBlock == null) return;

            _statusTextBlock.Text = FormatTimeAgo(e.Message, minutes);
        });
    }

    private void OnCursorPosition(CursorPositionEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_lineColumCountTextBlock != null)
                _lineColumCountTextBlock.Text = $"{e.Line}:{e.Column}";
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
                // Show caret position when there's no selection instead of clearing the text
                _lineColumCountTextBlock.Text = $"{e.StartLine}:{e.StartColumn}";
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
