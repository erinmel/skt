using System.Collections.Generic;
using skt.Shared;

namespace skt.IDE.Services.Buss;

public class TokenizeFileRequestEvent
{
    public string FilePath { get; }
    public bool WriteTokenFile { get; }
    public TokenizeFileRequestEvent(string filePath, bool writeTokenFile = false)
    {
        FilePath = filePath;
        WriteTokenFile = writeTokenFile;
    }
}

public class TokenizeBufferRequestEvent
{
    public string? FilePath { get; }
    public string Content { get; }
    public TokenizeBufferRequestEvent(string content, string? filePath)
    {
        Content = content;
        FilePath = filePath;
    }
}

public class LexicalAnalysisCompletedEvent
{
    public string? FilePath { get; }
    public int TokenCount { get; }
    public int ErrorCount { get; }
    public List<Token> Tokens { get; }
    public List<ErrorToken> Errors { get; }
    public bool FromBuffer { get; }

    public LexicalAnalysisCompletedEvent(string? filePath, List<Token> tokens, List<ErrorToken> errors, bool fromBuffer)
    {
        FilePath = filePath;
        Tokens = tokens;
        Errors = errors;
        TokenCount = tokens.Count;
        ErrorCount = errors.Count;
        FromBuffer = fromBuffer;
    }
}

public class LexicalAnalysisFailedEvent
{
    public string? FilePath { get; }
    public string Message { get; }
    public bool FromBuffer { get; }
    public LexicalAnalysisFailedEvent(string? filePath, string message, bool fromBuffer)
    {
        FilePath = filePath;
        Message = message;
        FromBuffer = fromBuffer;
    }
}
