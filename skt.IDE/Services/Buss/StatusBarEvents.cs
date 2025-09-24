using System;

namespace skt.IDE.Services.Buss;

public class StatusBarMessageEvent(string? message, long? durationMs = null, bool showTimeAgo = false)
{
    public string Message { get; } = message ?? string.Empty;
    public long? DurationMs { get; } = durationMs;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Id { get; } = Guid.NewGuid().ToString();
    public bool ShowTimeAgo { get; } = showTimeAgo;
    public StatusBarMessageEvent(string message, bool showTimeAgo) : this(message, null, showTimeAgo) { }

    public StatusBarMessageEvent(string message, long durationMs) : this(message, durationMs, false) { }
}

// Cursor position updates for status bar (line and column are 1-based)
public class CursorPositionEvent(int line, int column)
{
    public int Line { get; } = Math.Max(1, line);
    public int Column { get; } = Math.Max(1, column);
}

// File encoding change event for status bar display
public abstract class FileEncodingChangedEvent(string? encodingName)
{
    public string EncodingName { get; } = encodingName ?? "UTF-8";
}

// Selection information for status bar display
public class SelectionInfoEvent
{
    public int StartLine { get; }
    public int StartColumn { get; }
    public int EndLine { get; }
    public int EndColumn { get; }
    public int CharCount { get; }
    public int LineBreakCount { get; }

    public SelectionInfoEvent(int startLine, int startColumn, int endLine, int endColumn, int charCount, int lineBreakCount)
    {
        StartLine = Math.Max(1, startLine);
        StartColumn = Math.Max(1, startColumn);
        EndLine = Math.Max(1, endLine);
        EndColumn = Math.Max(1, endColumn);
        CharCount = Math.Max(0, charCount);
        LineBreakCount = Math.Max(0, lineBreakCount);
    }
}