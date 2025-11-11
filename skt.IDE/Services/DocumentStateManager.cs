using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using skt.Compiler;
using skt.IDE.Services.Buss;
using skt.IDE.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace skt.IDE.Services;

public class DocumentStateManager
{
    private readonly SemaphoreSlim _compilationLock = new(1, 1);
    private readonly IMessenger _messenger;
    private TextEditorViewModel? _activeEditor;

    public DocumentStateManager(IMessenger messenger)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        _messenger.Register<ActiveEditorChangedEvent>(this, (_, e) => OnActiveEditorChanged(e));
        _messenger.Register<TokenizeBufferRequestEvent>(this, (_, e) => OnTokenizeRequest(e));
    }

    private void OnActiveEditorChanged(ActiveEditorChangedEvent e)
    {
        _activeEditor = e.ActiveEditor;
    }

    private void OnTokenizeRequest(TokenizeBufferRequestEvent e)
    {
        if (!string.IsNullOrWhiteSpace(e.FilePath))
        {
            _ = AnalyzeDocumentAsync(e.FilePath, e.Content);
        }
    }

    private async Task AnalyzeDocumentAsync(string filePath, string content)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        await _compilationLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                var lexer = new LexicalAnalyzer();
                var (tokens, errors) = lexer.Tokenize(content);

                Dispatcher.UIThread.Post(() =>
                {
                    _messenger.Send(new LexicalAnalysisCompletedEvent(filePath, tokens, errors));
                });

                var parser = new SyntaxAnalyzer();
                var (ast, parseErrors) = parser.ParseFromTokens(tokens);

                Dispatcher.UIThread.Post(() =>
                {
                    _messenger.Send(new SyntaxAnalysisCompletedEvent(filePath, ast, parseErrors));
                });

                if (ast != null)
                {
                    var semanticAnalyzer = new SemanticAnalyzer();
                    var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

                    Dispatcher.UIThread.Post(() =>
                    {
                        _messenger.Send(new SemanticAnalysisCompletedEvent(
                            filePath,
                            annotatedAst,
                            symbolTable,
                            semanticErrors));
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DocumentStateManager: Error analyzing document {filePath}: {ex}");
        }
        finally
        {
            _compilationLock.Release();
        }
    }
}
