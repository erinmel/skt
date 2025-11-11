using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using skt.IDE.Services.Buss;
using skt.IDE.ViewModels;

namespace skt.IDE.Services;

public class ActiveEditorService
{
    private TextEditorViewModel? _activeEditor;
    private readonly List<TextEditorViewModel> _allEditors = new();

    public TextEditorViewModel? ActiveEditor => _activeEditor;

    public IReadOnlyList<TextEditorViewModel> AllEditors => _allEditors.AsReadOnly();

    public ActiveEditorService()
    {
        App.Messenger.Register<ActiveEditorChangedEvent>(this, (_, e) => OnActiveEditorChanged(e));
        App.Messenger.Register<LexicalAnalysisCompletedEvent>(this, (_, e) => OnLexicalAnalysisCompleted(e));
        App.Messenger.Register<SyntaxAnalysisCompletedEvent>(this, (_, e) => OnSyntaxAnalysisCompleted(e));
        App.Messenger.Register<SemanticAnalysisCompletedEvent>(this, (_, e) => OnSemanticAnalysisCompleted(e));
    }

    private void OnActiveEditorChanged(ActiveEditorChangedEvent e)
    {
        _activeEditor = e.ActiveEditor;

        if (e.ActiveEditor != null && !_allEditors.Contains(e.ActiveEditor))
        {
            _allEditors.Add(e.ActiveEditor);
        }
    }

    private void OnLexicalAnalysisCompleted(LexicalAnalysisCompletedEvent e)
    {
        var editor = FindEditorByFilePath(e.FilePath);
        editor?.UpdateLexicalAnalysis(e.Tokens, e.Errors);
    }

    private void OnSyntaxAnalysisCompleted(SyntaxAnalysisCompletedEvent e)
    {
        var editor = FindEditorByFilePath(e.FilePath);
        editor?.UpdateSyntaxAnalysis(e.Ast, e.Errors);
    }

    private void OnSemanticAnalysisCompleted(SemanticAnalysisCompletedEvent e)
    {
        var editor = FindEditorByFilePath(e.FilePath);
        editor?.UpdateSemanticAnalysis(e.AnnotatedAst, e.SymbolTable, e.Errors);
    }

    private TextEditorViewModel? FindEditorByFilePath(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        return _allEditors.FirstOrDefault(e =>
            string.Equals(e.FilePath, filePath, System.StringComparison.OrdinalIgnoreCase));
    }

    public void RegisterEditor(TextEditorViewModel editor)
    {
        if (!_allEditors.Contains(editor))
        {
            _allEditors.Add(editor);
        }
    }

    public void UnregisterEditor(TextEditorViewModel editor)
    {
        _allEditors.Remove(editor);

        if (_activeEditor == editor)
        {
            _activeEditor = null;
        }
    }
}
