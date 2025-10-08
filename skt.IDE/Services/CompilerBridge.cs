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
    private readonly SyntaxAnalyzer _syntax = new();

    public CompilerBridge(IEventBus bus)
    {
        _bus = bus;
        _bus.Subscribe<TokenizeFileRequestEvent>(OnTokenizeFileRequest);
        _bus.Subscribe<TokenizeBufferRequestEvent>(OnTokenizeBufferRequest);
        _bus.Subscribe<FileOpenedEvent>(OnFileOpened);
        _bus.Subscribe<ParseFileRequestEvent>(OnParseFileRequest);
        _bus.Subscribe<ParseBufferRequestEvent>(OnParseBufferRequest);
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
            var (tokens, errors) = _lexical.Tokenize(code);
            _bus.Publish(new LexicalAnalysisCompletedEvent(filePath, tokens, errors, false));

            // Also perform syntax analysis when file is analyzed
            PerformSyntaxAnalysis(filePath);
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
            _bus.Publish(new LexicalAnalysisCompletedEvent(filePath, tokens, errors, true));

            // Also trigger syntax analysis for the buffer
            ParseBuffer(content, tokens, filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lexical:buffer][Error] {ex.Message}");
            _bus.Publish(new LexicalAnalysisFailedEvent(filePath, ex.Message, true));
        }
    }

    private void PerformSyntaxAnalysis(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _bus.Publish(new SyntaxAnalysisFailedEvent(filePath, "File not found"));
                return;
            }

            var code = File.ReadAllText(filePath);

            // First tokenize to file so syntax analyzer can read it
            _lexical.TokenizeToFile(code, filePath);

            // Then parse
            var (ast, errors) = _syntax.Parse(filePath);

            Console.WriteLine($"[Syntax] {filePath} ast={(ast != null ? "OK" : "null")} errors={errors.Count}");
            _bus.Publish(new SyntaxAnalysisCompletedEvent(filePath, ast, errors));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Syntax][Error] {ex.Message}");
            _bus.Publish(new SyntaxAnalysisFailedEvent(filePath, ex.Message));
        }
    }

    private void ParseBuffer(string content, List<Token> tokens, string? filePath)
    {
        try
        {
            // Parse directly from tokens in memory without writing to file
            var (ast, errors) = _syntax.ParseFromTokens(tokens);

            Console.WriteLine($"[Syntax:buffer] {filePath ?? "unnamed"} ast={(ast != null ? "OK" : "null")} errors={errors.Count}");
            _bus.Publish(new SyntaxAnalysisCompletedEvent(filePath, ast, errors, true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Syntax:buffer][Error] {ex.Message}");
            _bus.Publish(new SyntaxAnalysisFailedEvent(filePath ?? "unnamed", ex.Message));
        }
    }

    private void OnFileOpened(FileOpenedEvent e) => AnalyzeFileInMemory(e.FilePath, "open");
    private void OnTokenizeFileRequest(TokenizeFileRequestEvent e) => AnalyzeFileInMemory(e.FilePath, "request");
    private void OnTokenizeBufferRequest(TokenizeBufferRequestEvent e) => AnalyzeBuffer(e.Content, e.FilePath);
    private void OnParseFileRequest(ParseFileRequestEvent e) => PerformSyntaxAnalysis(e.FilePath);
    private void OnParseBufferRequest(ParseBufferRequestEvent e) => ParseBuffer(e.Content, e.Tokens, e.FilePath);
}
