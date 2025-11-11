using System;
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
    private readonly SemanticAnalyzer _semantic = new();

    public CompilerBridge(IEventBus bus)
    {
        _bus = bus;
        _bus.Subscribe<TokenizeFileRequestEvent>(OnTokenizeFileRequest);
        _bus.Subscribe<TokenizeBufferRequestEvent>(OnTokenizeBufferRequest);
        _bus.Subscribe<FileOpenedEvent>(OnFileOpened);
        _bus.Subscribe<ParseFileRequestEvent>(OnParseFileRequest);
        _bus.Subscribe<ParseBufferRequestEvent>(OnParseBufferRequest);
        _bus.Subscribe<SemanticAnalysisRequestEvent>(OnSemanticAnalysisRequest);
    }

    private void AnalyzeFileInMemory(string filePath)
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
        }
        catch (Exception ex)
        {
            _bus.Publish(new LexicalAnalysisFailedEvent(filePath, ex.Message, false));
        }
    }

    private void AnalyzeBuffer(string content, string? filePath)
    {
        try
        {
            var (tokens, errors) = _lexical.Tokenize(content);
            _bus.Publish(new LexicalAnalysisCompletedEvent(filePath, tokens, errors, true));

            ParseBuffer(tokens, filePath);
        }
        catch (Exception ex)
        {
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

            // Perform lexical analysis and write tokens to file (cascade)
            var (tokens, lexErrors) = _lexical.TokenizeToFile(code, filePath);
            _bus.Publish(new LexicalAnalysisCompletedEvent(filePath, tokens, lexErrors, false));

            // Only continue with syntax if lexical analysis succeeded
            if (lexErrors.Count > 0)
            {
                return;
            }

            // Parse using the token file
            var (ast, errors) = _syntax.Parse(filePath);
            _bus.Publish(new SyntaxAnalysisCompletedEvent(filePath, ast, errors));
        }
        catch (Exception ex)
        {
            _bus.Publish(new SyntaxAnalysisFailedEvent(filePath, ex.Message));
        }
    }

    private void ParseBuffer(List<Token> tokens, string? filePath)
    {
        try
        {
            var (ast, errors) = _syntax.ParseFromTokens(tokens);
            _bus.Publish(new SyntaxAnalysisCompletedEvent(filePath, ast, errors, true));

            if (ast != null)
            {
                PerformSemanticAnalysisOnBuffer(ast, filePath);
            }
        }
        catch (Exception ex)
        {
            _bus.Publish(new SyntaxAnalysisFailedEvent(filePath ?? "unnamed", ex.Message));
        }
    }

    private void PerformSemanticAnalysis(SemanticAnalysisRequestEvent request)
    {
        try
        {
            var (annotatedAst, symbolTable, errors) = _semantic.Analyze(request.Ast);
            _bus.Publish(new SemanticAnalysisCompletedEvent(
                request.FilePath,
                annotatedAst,
                symbolTable,
                errors,
                request.FromBuffer
            ));
        }
        catch (Exception ex)
        {
            _bus.Publish(new SemanticAnalysisFailedEvent(request.FilePath, ex.Message, request.FromBuffer));
        }
    }

    private void PerformSemanticAnalysisOnBuffer(AstNode ast, string? filePath)
    {
        try
        {
            var (annotatedAst, symbolTable, errors) = _semantic.Analyze(ast);
            _bus.Publish(new SemanticAnalysisCompletedEvent(
                filePath,
                annotatedAst,
                symbolTable,
                errors,
                true
            ));
        }
        catch (Exception ex)
        {
            _bus.Publish(new SemanticAnalysisFailedEvent(filePath ?? "unnamed", ex.Message, true));
        }
    }

    private void OnFileOpened(FileOpenedEvent e) => AnalyzeFileInMemory(e.FilePath);
    private void OnTokenizeFileRequest(TokenizeFileRequestEvent e) => AnalyzeFileInMemory(e.FilePath);
    private void OnTokenizeBufferRequest(TokenizeBufferRequestEvent e) => AnalyzeBuffer(e.Content, e.FilePath);
    private void OnParseFileRequest(ParseFileRequestEvent e) => PerformSyntaxAnalysis(e.FilePath);
    private void OnParseBufferRequest(ParseBufferRequestEvent e) => ParseBuffer(e.Tokens, e.FilePath);
    private void OnSemanticAnalysisRequest(SemanticAnalysisRequestEvent e) => PerformSemanticAnalysis(e);
}
