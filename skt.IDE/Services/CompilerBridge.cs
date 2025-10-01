using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using skt.Compiler;
using skt.IDE.Services.Buss;
using skt.Shared;

namespace skt.IDE.Services;

public class CompilerBridge
{
    private readonly IEventBus _bus;
    private readonly LexicalAnalyzer _lexical = new();

    public CompilerBridge(IEventBus bus)
    {
        _bus = bus;
        _bus.Subscribe<TokenizeFileRequestEvent>(OnTokenizeFileRequest);
        _bus.Subscribe<TokenizeBufferRequestEvent>(OnTokenizeBufferRequest);
        _bus.Subscribe<FileOpenedEvent>(OnFileOpened);
    }

    private void PrintTokens(string? sourceId, List<Token> tokens, List<ErrorToken> errors, bool fromBuffer)
    {
        Console.WriteLine($"==== Lexical Analysis {(fromBuffer ? "(buffer)" : "(file)")} {sourceId ?? "<memory>"} ====\nTokens={tokens.Count} Errors={errors.Count}");
#if DEBUG
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            Console.WriteLine($"[TOK {i:D4}] {t.Type,-18} '{t.Value}' @ {t.Line}:{t.Column}-{t.EndLine}:{t.EndColumn}");
        }
        if (errors.Count > 0)
        {
            Console.WriteLine("-- Errors --");
            for (int i = 0; i < errors.Count; i++)
            {
                var e = errors[i];
                Console.WriteLine($"[ERR {i:D4}] Expected {e.Expected} Found '{e.Value}' @ {e.Line}:{e.Column}-{e.EndLine}:{e.EndColumn}");
            }
        }
#endif
        Console.WriteLine("==== End ====\n");
    }

    private void AnalyzeFileInMemory(string filePath, string origin)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _bus.Publish(new LexicalAnalysisFailedEvent(filePath, "File not found", false));
                return;
            }
            var code = File.ReadAllText(filePath);
            var (tokens, errors) = _lexical.Tokenize(code); // in-memory only
            Console.WriteLine($"[Lexical:{origin}] {filePath} tokens={tokens.Count} errors={errors.Count}");
            PrintTokens(filePath, tokens, errors, false);
            _bus.Publish(new LexicalAnalysisCompletedEvent(filePath, tokens, errors, false));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lexical:{origin}][Error] {ex.Message}");
            _bus.Publish(new LexicalAnalysisFailedEvent(filePath, ex.Message, false));
        }
    }

    private void AnalyzeBuffer(string content, string? filePath)
    {
        try
        {
            var (tokens, errors) = _lexical.Tokenize(content);
            PrintTokens(filePath, tokens, errors, true);
            _bus.Publish(new LexicalAnalysisCompletedEvent(filePath, tokens, errors, true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lexical:buffer][Error] {ex.Message}");
            _bus.Publish(new LexicalAnalysisFailedEvent(filePath, ex.Message, true));
        }
    }

    private void OnFileOpened(FileOpenedEvent e) => AnalyzeFileInMemory(e.FilePath, "open");
    private void OnTokenizeFileRequest(TokenizeFileRequestEvent e) => AnalyzeFileInMemory(e.FilePath, "request");
    private void OnTokenizeBufferRequest(TokenizeBufferRequestEvent e) => AnalyzeBuffer(e.Content, e.FilePath);
}
