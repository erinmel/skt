using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Services.Buss;
using skt.Shared;
using CommunityToolkit.Mvvm.Messaging;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class TokensViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<TokenRow> _rows = new();
    private readonly Services.ActiveEditorService? _activeEditorService;

    [ObservableProperty]
    private FlatTreeDataGridSource<TokenRow> _source;

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private int _tokenCount;

    [ObservableProperty]
    private int _errorCount;

    public TokensViewModel()
    {
        _source = CreateSource(_rows);
        _activeEditorService = App.Services?.GetService(typeof(Services.ActiveEditorService)) as Services.ActiveEditorService;

        App.Messenger.Register<ActiveEditorChangedEvent>(this, (_, m) => OnActiveEditorChanged(m));
        App.Messenger.Register<LexicalAnalysisCompletedEvent>(this, (_, m) => OnLexicalCompleted(m));
        App.Messenger.Register<FileClosedEvent>(this, (_, m) => OnFileClosed(m));
    }

    private FlatTreeDataGridSource<TokenRow> CreateSource(IList<TokenRow> items) => new(items)
    {
        Columns =
        {
            new TextColumn<TokenRow, string>("Type", x => x.Type),
            new TextColumn<TokenRow, string>("Value", x => x.Value),
            new TextColumn<TokenRow, string>("Start", x => x.StartPos),
            new TextColumn<TokenRow, string>("End", x => x.EndPos)
        }
    };

    private void OnActiveEditorChanged(ActiveEditorChangedEvent e)
    {
        if (e.ActiveEditor == null)
        {
            CurrentFile = string.Empty;
            Clear();
            return;
        }

        CurrentFile = e.ActiveEditor.FilePath ?? string.Empty;
        LoadTokens(e.ActiveEditor.Tokens, e.ActiveEditor.LexicalErrors);
    }

    private void OnLexicalCompleted(LexicalAnalysisCompletedEvent e)
    {
        // Only update if this is for the currently active file
        if (!string.Equals(CurrentFile, e.FilePath, StringComparison.OrdinalIgnoreCase))
            return;

        LoadTokens(e.Tokens, e.Errors);
    }

    private void OnFileClosed(FileClosedEvent e)
    {
        if (!string.Equals(CurrentFile, e.FilePath, StringComparison.OrdinalIgnoreCase)) return;
        CurrentFile = string.Empty;
        Clear();
    }

    private void LoadTokens(IEnumerable<Token> tokens, IEnumerable<ErrorToken> errors)
    {
        var tokenList = tokens.ToList();
        var errorList = errors.ToList();

        Dispatcher.UIThread.Post(() =>
        {
            _rows.Clear();

            foreach (var token in tokenList)
            {
                _rows.Add(new TokenRow(
                    token.Type.ToString(),
                    token.Value,
                    token.Line,
                    token.Column,
                    token.EndLine,
                    token.EndColumn
                ));
            }

            foreach (var error in errorList)
            {
                _rows.Add(new TokenRow(
                    "ERROR",
                    error.Value,
                    error.Line,
                    error.Column,
                    error.EndLine,
                    error.EndColumn
                ));
            }

            TokenCount = tokenList.Count;
            ErrorCount = errorList.Count;
        });
    }

    private void Clear()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _rows.Clear();
            TokenCount = 0;
            ErrorCount = 0;
        });
    }

    public void Dispose()
    {
        App.Messenger.UnregisterAll(this);
    }
}
