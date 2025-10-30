using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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

    // Cache last successful lexical results per file
    private readonly Dictionary<string, (List<Token> tokens, List<ErrorToken> errors)> _tokenCache = new();

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

        App.Messenger.Register<LexicalAnalysisCompletedEvent>(this, (r, m) => OnLexicalCompleted(m));
        App.Messenger.Register<LexicalAnalysisFailedEvent>(this, (r, m) => OnLexicalFailed(m));
        App.Messenger.Register<FileOpenedEvent>(this, (r, m) => OnFileOpened(m));
        App.Messenger.Register<FileClosedEvent>(this, (r, m) => OnFileClosed(m));
        App.Messenger.Register<FileRenamedEvent>(this, (r, m) => OnFileRenamed(m));
        App.Messenger.Register<SelectedDocumentChangedEvent>(this, (r, m) => OnSelectedDocumentChanged(m));
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

    private void OnSelectedDocumentChanged(SelectedDocumentChangedEvent e)
    {
        if (string.IsNullOrEmpty(e.FilePath))
        {
            CurrentFile = string.Empty;
            Clear();
            return;
        }

        if (CurrentFile != e.FilePath)
        {
            CurrentFile = e.FilePath;
            if (_tokenCache.TryGetValue(e.FilePath, out var cached))
            {
                LoadTokens(cached.tokens, cached.errors);
            }
            else
            {
                Clear();
                App.Messenger.Send(new TokenizeFileRequestEvent(e.FilePath));
            }
        }
        else if (e.IsDirty)
        {
            App.Messenger.Send(new TokenizeFileRequestEvent(e.FilePath));
        }
    }

    private void OnFileOpened(FileOpenedEvent e)
    {
        CurrentFile = e.FilePath;
        // Tokenization will be triggered by compiler bridge subscription already
    }

    private void OnFileClosed(FileClosedEvent e)
    {
        _tokenCache.Remove(e.FilePath);
        if (!string.Equals(CurrentFile, e.FilePath, StringComparison.OrdinalIgnoreCase)) return;
        CurrentFile = string.Empty;
        Clear();
    }

    private void OnFileRenamed(FileRenamedEvent e)
    {
        if (_tokenCache.Remove(e.OldPath, out var data))
        {
            _tokenCache[e.NewPath] = data;
        }

        if (!string.Equals(CurrentFile, e.OldPath, StringComparison.OrdinalIgnoreCase)) return;
        CurrentFile = e.NewPath;
        if (_tokenCache.TryGetValue(e.NewPath, out var cached))
        {
            LoadTokens(cached.tokens, cached.errors);
        }
    }

    private void OnLexicalFailed(LexicalAnalysisFailedEvent e)
    {
        if (!string.IsNullOrEmpty(e.FilePath))
            CurrentFile = e.FilePath;
        if (e.FilePath == CurrentFile)
        {
            Clear();
        }
    }

    private void OnLexicalCompleted(LexicalAnalysisCompletedEvent e)
    {
        var path = e.FilePath ?? CurrentFile;
        if (string.IsNullOrEmpty(path))
            return;

        _tokenCache[path] = (e.Tokens, e.Errors);

        if (path == CurrentFile)
        {
            LoadTokens(e.Tokens, e.Errors);
        }
    }

    private void LoadTokens(List<Token> tokens, List<ErrorToken> errors)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => LoadTokens(tokens, errors));
            return;
        }
        _rows.Clear();
        foreach (var t in tokens)
        {
            _rows.Add(new TokenRow( t.Type.ToString(), t.Value, t.Line, t.Column, t.EndLine, t.EndColumn));
        }
        TokenCount = tokens.Count;
        ErrorCount = errors.Count;
        Source = CreateSource(_rows); // force refresh
    }
    private void Clear()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Clear);
            return;
        }
        _rows.Clear();
        TokenCount = 0;
        ErrorCount = 0;
        Source = CreateSource(_rows); // ensure grid shows empty state
    }

    public void Dispose()
    {
        Source?.Dispose();
    }
}
