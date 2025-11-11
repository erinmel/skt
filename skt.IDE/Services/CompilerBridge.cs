using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using skt.Compiler;
using skt.IDE.Services.Buss;
using skt.Shared;
using CommunityToolkit.Mvvm.Messaging;

namespace skt.IDE.Services;

public class CompilerBridge
{
    private readonly IMessenger _messenger;

    public CompilerBridge(IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.Register<TokenizeFileRequestEvent>(this, (r, m) => OnTokenizeFileRequest(m));
        _messenger.Register<TokenizeBufferRequestEvent>(this, (r, m) => OnTokenizeBufferRequest(m));
        _messenger.Register<FileOpenedEvent>(this, (r, m) => OnFileOpened(m));
        _messenger.Register<ParseFileRequestEvent>(this, (r, m) => OnParseFileRequest(m));
        _messenger.Register<ParseBufferRequestEvent>(this, (r, m) => OnParseBufferRequest(m));
        _messenger.Register<SemanticAnalysisRequestEvent>(this, (r, m) => OnSemanticAnalysisRequest(m));
    }

    private async Task AnalyzeFileInMemory(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _messenger.Send(new LexicalAnalysisFailedEvent(filePath, "File not found", false));
                return;
            }

            var code = await File.ReadAllTextAsync(filePath);
            await Task.Run(() =>
            {
                var lexical = new LexicalAnalyzer();
                var (tokens, errors) = lexical.Tokenize(code);
                _messenger.Send(new LexicalAnalysisCompletedEvent(filePath, tokens, errors, false));
            });
        }
        catch (Exception ex)
        {
            _messenger.Send(new LexicalAnalysisFailedEvent(filePath, ex.Message, false));
        }
    }

    private async void AnalyzeBuffer(string content, string? filePath)
    {
        try
        {
            var (tokens, errors) = await Task.Run(() =>
            {
                var lexical = new LexicalAnalyzer();
                return lexical.Tokenize(content);
            });

            _messenger.Send(new LexicalAnalysisCompletedEvent(filePath, tokens, errors, true));

            await ParseBuffer(tokens, filePath);
        }
        catch (Exception ex)
        {
            _messenger.Send(new LexicalAnalysisFailedEvent(filePath, ex.Message, true));
        }
    }

    private async void PerformSyntaxAnalysis(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _messenger.Send(new SyntaxAnalysisFailedEvent(filePath, "File not found"));
                return;
            }

            var code = await File.ReadAllTextAsync(filePath);

            await Task.Run(() =>
            {
                var lexical = new LexicalAnalyzer();
                var (tokens, lexErrors) = lexical.TokenizeToFile(code, filePath);
                _messenger.Send(new LexicalAnalysisCompletedEvent(filePath, tokens, lexErrors, false));

                if (lexErrors.Count > 0)
                {
                    return;
                }

                var syntax = new SyntaxAnalyzer();
                var (ast, errors) = syntax.Parse(filePath);
                _messenger.Send(new SyntaxAnalysisCompletedEvent(filePath, ast, errors));
            });
        }
        catch (Exception ex)
        {
            _messenger.Send(new SyntaxAnalysisFailedEvent(filePath, ex.Message));
        }
    }

    private async Task ParseBuffer(List<Token> tokens, string? filePath)
    {
        try
        {
            var (ast, errors) = await Task.Run(() =>
            {
                var syntax = new SyntaxAnalyzer();
                return syntax.ParseFromTokens(tokens);
            });

            _messenger.Send(new SyntaxAnalysisCompletedEvent(filePath, ast, errors, true));

            if (ast != null)
            {
                await PerformSemanticAnalysisOnBuffer(ast, filePath);
            }
        }
        catch (Exception ex)
        {
            _messenger.Send(new SyntaxAnalysisFailedEvent(filePath ?? "unnamed", ex.Message));
        }
    }

    private async void PerformSemanticAnalysis(SemanticAnalysisRequestEvent request)
    {
        try
        {
            var (annotatedAst, symbolTable, errors) = await Task.Run(() =>
            {
                var semantic = new SemanticAnalyzer();
                return semantic.Analyze(request.Ast);
            });

            _messenger.Send(new SemanticAnalysisCompletedEvent(
                request.FilePath,
                annotatedAst,
                symbolTable,
                errors,
                request.FromBuffer
            ));
        }
        catch (Exception ex)
        {
            _messenger.Send(new SemanticAnalysisFailedEvent(request.FilePath, ex.Message, request.FromBuffer));
        }
    }

    private async Task PerformSemanticAnalysisOnBuffer(AstNode ast, string? filePath)
    {
        try
        {
            var (annotatedAst, symbolTable, errors) = await Task.Run(() =>
            {
                var semantic = new SemanticAnalyzer();
                return semantic.Analyze(ast);
            });

            _messenger.Send(new SemanticAnalysisCompletedEvent(
                filePath,
                annotatedAst,
                symbolTable,
                errors,
                true
            ));
        }
        catch (Exception ex)
        {
            _messenger.Send(new SemanticAnalysisFailedEvent(filePath ?? "unnamed", ex.Message, true));
        }
    }

    private void OnFileOpened(FileOpenedEvent e) => AnalyzeFileInMemory(e.FilePath);
    private void OnTokenizeFileRequest(TokenizeFileRequestEvent e) => AnalyzeFileInMemory(e.FilePath);
    private void OnTokenizeBufferRequest(TokenizeBufferRequestEvent e) => AnalyzeBuffer(e.Content, e.FilePath);
    private void OnParseFileRequest(ParseFileRequestEvent e) => PerformSyntaxAnalysis(e.FilePath);
    private void OnParseBufferRequest(ParseBufferRequestEvent e) => _ = ParseBuffer(e.Tokens, e.FilePath);
    private void OnSemanticAnalysisRequest(SemanticAnalysisRequestEvent e) => PerformSemanticAnalysis(e);
}
