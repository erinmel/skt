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
    private System.Threading.CancellationTokenSource? _executionCancellation;
    private bool _isExecuting = false;

    public CompilerBridge(IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.Register<TokenizeFileRequestEvent>(this, (r, m) => OnTokenizeFileRequest(m));
        _messenger.Register<TokenizeBufferRequestEvent>(this, (r, m) => OnTokenizeBufferRequest(m));
        _messenger.Register<FileOpenedEvent>(this, (r, m) => OnFileOpened(m));
        _messenger.Register<ParseFileRequestEvent>(this, (r, m) => OnParseFileRequest(m));
        _messenger.Register<ParseBufferRequestEvent>(this, (r, m) => OnParseBufferRequest(m));
        _messenger.Register<SemanticAnalysisRequestEvent>(this, (r, m) => OnSemanticAnalysisRequest(m));
        _messenger.Register<PCodeGenerationRequestEvent>(this, (r, m) => OnPCodeGenerationRequest(m));
        _messenger.Register<PCodeExecutionRequestEvent>(this, (r, m) => OnPCodeExecutionRequest(m));
        _messenger.Register<StopExecutionRequestEvent>(this, (r, m) => OnStopExecutionRequest(m));
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

            // Generate P-Code if semantic analysis succeeded
            if (annotatedAst != null && errors.Count == 0)
            {
                await GeneratePCodeFromBuffer(annotatedAst, filePath);
            }
        }
        catch (Exception ex)
        {
            _messenger.Send(new SemanticAnalysisFailedEvent(filePath ?? "unnamed", ex.Message, true));
        }
    }

    private async Task GeneratePCodeFromBuffer(AnnotatedAstNode annotatedAst, string? filePath)
    {
        try
        {
            var program = await Task.Run(() =>
            {
                var generator = new PCodeGenerator();
                return generator.Generate(annotatedAst);
            });

            _messenger.Send(new PCodeGenerationCompletedEvent(filePath, program, true));
        }
        catch (Exception ex)
        {
            _messenger.Send(new PCodeGenerationFailedEvent(filePath ?? "unnamed", ex.Message, true));
        }
    }

    private async void OnPCodeGenerationRequest(PCodeGenerationRequestEvent request)
    {
        try
        {
            var program = await Task.Run(() =>
            {
                var generator = new PCodeGenerator();
                return generator.Generate(request.AnnotatedAst);
            });

            _messenger.Send(new PCodeGenerationCompletedEvent(request.FilePath, program, request.FromBuffer));
        }
        catch (Exception ex)
        {
            _messenger.Send(new PCodeGenerationFailedEvent(request.FilePath, ex.Message, request.FromBuffer));
        }
    }

    private void OnStopExecutionRequest(StopExecutionRequestEvent request)
    {
        System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Stop execution requested");
        
        if (_isExecuting && _executionCancellation != null)
        {
            _executionCancellation.Cancel();
            _messenger.Send(new PCodeExecutionOutputEvent("\n\nProgram execution stopped by user.\n", true));
            System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Execution cancelled");
            
            // Send completion event to update UI
            _messenger.Send(new PCodeExecutionCompletedEvent(null, false, "Stopped by user"));
            
            // Clean up state
            _isExecuting = false;
            _executionCancellation?.Dispose();
            _executionCancellation = null;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[CompilerBridge] No execution running to stop");
        }
    }

    private async void OnPCodeExecutionRequest(PCodeExecutionRequestEvent request)
    {
        try
        {
            // Cancel any previous execution
            if (_isExecuting && _executionCancellation != null)
            {
                System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Cancelling previous execution");
                _executionCancellation.Cancel();
                await Task.Delay(100); // Give time for cancellation to complete
            }
            
            // Create new cancellation token for this execution
            _executionCancellation = new System.Threading.CancellationTokenSource();
            _isExecuting = true;
            
            // Notify that execution started
            _messenger.Send(new PCodeExecutionStartedEvent(request.FilePath));
            
            System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Starting P-Code execution");
            System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Program has {request.Program?.Instructions?.Count ?? 0} instructions");
            
            if (request.Program == null || request.Program.Instructions == null || request.Program.Instructions.Count == 0)
            {
                _messenger.Send(new PCodeExecutionOutputEvent("Error: No P-Code instructions to execute\n", true));
                _messenger.Send(new PCodeExecutionCompletedEvent(request.FilePath, false, "No instructions"));
                _isExecuting = false;
                return;
            }
            
            var interpreter = new PCodeInterpreter();
            
            // Subscribe to output events
            interpreter.OnOutput += (output) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Interpreter] Output: {output}");
                _messenger.Send(new PCodeExecutionOutputEvent(output, false));
            };
            
            interpreter.OnError += (error) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Interpreter] Error: {error}");
                _messenger.Send(new PCodeExecutionOutputEvent(error, true));
            };
            
            // Subscribe to input request events
            interpreter.OnInputRequest += async () =>
            {
                System.Diagnostics.Debug.WriteLine($"[Interpreter] Requesting input");
                
                // Check if execution was already cancelled
                if (_executionCancellation?.IsCancellationRequested == true)
                {
                    System.Diagnostics.Debug.WriteLine($"[Interpreter] Execution cancelled, throwing OperationCanceledException");
                    throw new OperationCanceledException();
                }
                
                // Create TaskCompletionSource for waiting on response
                var tcs = new TaskCompletionSource<string?>();
                
                // Register temporary handler for input response - needs a unique recipient
                var recipient = new object();
                _messenger.Register<PCodeInputResponseEvent>(recipient, (r, m) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Interpreter] Received input: {m.Input}");
                    
                    // Check if cancelled before accepting input
                    if (_executionCancellation?.IsCancellationRequested == true)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Interpreter] Execution cancelled, ignoring input");
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        tcs.TrySetResult(m.Input);
                    }
                    _messenger.Unregister<PCodeInputResponseEvent>(recipient);
                });
                
                // Request input from the terminal
                _messenger.Send(new PCodeInputRequestEvent());
                
                // Wait for response with timeout to prevent deadlock
                try
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(5));
                    var timeoutTask = Task.Delay(System.Threading.Timeout.Infinite, cts.Token);
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                    
                    if (completedTask == tcs.Task)
                    {
                        var result = await tcs.Task;
                        
                        // Final check before returning
                        if (_executionCancellation?.IsCancellationRequested == true)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Interpreter] Execution cancelled after receiving input");
                            throw new OperationCanceledException();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[Interpreter] Returning input to interpreter: {result}");
                        return result;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Interpreter] ERROR: Input timeout after 5 minutes");
                        _messenger.Unregister<PCodeInputResponseEvent>(recipient);
                        return "0"; // Return default value on timeout
                    }
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[Interpreter] Input cancelled");
                    _messenger.Unregister<PCodeInputResponseEvent>(recipient);
                    throw; // Re-throw to stop interpreter
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Interpreter] ERROR: Exception waiting for input: {ex.Message}");
                    _messenger.Unregister<PCodeInputResponseEvent>(recipient);
                    return "0";
                }
            };

            // Execute the program asynchronously with cancellation support
            System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Calling ExecuteAsync with cancellation token");
            await interpreter.ExecuteAsync(request.Program, null, _executionCancellation.Token);
            
            // If we reach here without exception, execution completed successfully
            System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Execution completed successfully");
            // Send completion message
            _messenger.Send(new PCodeExecutionOutputEvent("\nProgram exited successfully.\n", false));
            _messenger.Send(new PCodeExecutionCompletedEvent(request.FilePath, true));
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Execution cancelled via exception");
            _messenger.Send(new PCodeExecutionCompletedEvent(request.FilePath, false, "Cancelled"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CompilerBridge] Exception: {ex}");
            _messenger.Send(new PCodeExecutionOutputEvent($"\nRuntime Error: {ex.Message}\n", true));
            _messenger.Send(new PCodeExecutionOutputEvent("Program exited with errors.\n", true));
            _messenger.Send(new PCodeExecutionCompletedEvent(request.FilePath, false, ex.Message));
        }
        finally
        {
            _isExecuting = false;
            _executionCancellation?.Dispose();
            _executionCancellation = null;
        }
    }

    private void OnFileOpened(FileOpenedEvent e) => _ = AnalyzeFileInMemory(e.FilePath);
    private void OnTokenizeFileRequest(TokenizeFileRequestEvent e) => _ = AnalyzeFileInMemory(e.FilePath);
    private void OnTokenizeBufferRequest(TokenizeBufferRequestEvent e) => AnalyzeBuffer(e.Content, e.FilePath);
    private void OnParseFileRequest(ParseFileRequestEvent e) => PerformSyntaxAnalysis(e.FilePath);
    private void OnParseBufferRequest(ParseBufferRequestEvent e) => _ = ParseBuffer(e.Tokens, e.FilePath);
    private void OnSemanticAnalysisRequest(SemanticAnalysisRequestEvent e) => PerformSemanticAnalysis(e);
}
