using System;

namespace skt.IDE.Services.Buss
{
    // Message to show on the status bar. DurationMs: milliseconds to display; null or negative -> infinite.
    public class StatusBarMessageEvent
    {
        public string Message { get; }
        public long? DurationMs { get; }
        public DateTimeOffset Timestamp { get; }
        public string Id { get; }
        public bool ShowTimeAgo { get; }

        // Primary constructor
        public StatusBarMessageEvent(string message, long? durationMs = null, bool showTimeAgo = false)
        {
            Message = message ?? string.Empty;
            DurationMs = durationMs;
            Timestamp = DateTimeOffset.UtcNow;
            Id = Guid.NewGuid().ToString();
            ShowTimeAgo = showTimeAgo;
        }

        // Convenience overload used in some call sites: (message, showTimeAgo)
        public StatusBarMessageEvent(string message, bool showTimeAgo) : this(message, null, showTimeAgo) { }

        // Convenience overload used in older call sites: (message, durationMs)
        public StatusBarMessageEvent(string message, long durationMs) : this(message, (long?)durationMs, false) { }
    }

    // Cursor position updates for status bar (line and column are 1-based)
    public class CursorPositionEvent
    {
        public int Line { get; }
        public int Column { get; }

        public CursorPositionEvent(int line, int column)
        {
            Line = Math.Max(1, line);
            Column = Math.Max(1, column);
        }
    }

    // File encoding change event for status bar display
    public class FileEncodingChangedEvent
    {
        public string EncodingName { get; }

        public FileEncodingChangedEvent(string encodingName)
        {
            EncodingName = encodingName ?? "UTF-8";
        }
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
}
