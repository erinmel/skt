using System;

namespace skt.IDE.Services.Buss
{
    // Message to show on the status bar. DurationMs: milliseconds to display; null or negative -> infinite.
    public class StatusBarMessageEvent
    {
        public string Message { get; }
        public long? DurationMs { get; }
        public DateTimeOffset Timestamp { get; }

        public StatusBarMessageEvent(string message, long? durationMs = null)
        {
            Message = message ?? string.Empty;
            DurationMs = durationMs;
            Timestamp = DateTimeOffset.UtcNow;
        }
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
}

