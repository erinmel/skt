using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Services.Buss;
using skt.Shared;

namespace skt.IDE.ViewModels.ToolWindows;

public class ErrorItem
{
    public string FilePath { get; }
    public string Message { get; }
    public string StartPos { get; }
    public int Line { get; }
    public int Column { get; }

    public ErrorItem(string filePath, string message, int line, int column)
    {
        FilePath = filePath;
        Message = message;
        Line = line;
        Column = column;
        StartPos = $"{line}:{column}";
    }
}

public class FileErrorGroup
{
    public string FilePath { get; }
    public string DisplayName => string.IsNullOrEmpty(FilePath) ? "<buffer>" : Path.GetFileName(FilePath);
    public ObservableCollection<ErrorItem> Errors { get; } = new();
    public int ErrorCount => Errors.Count;

    public FileErrorGroup(string filePath)
    {
        FilePath = filePath ?? string.Empty;
    }
}

public class ErrorsViewModel : ObservableObject
{
    private readonly ObservableCollection<FileErrorGroup> _lexicalGroups = new();
    private readonly ObservableCollection<FileErrorGroup> _syntaxGroups = new();

    public ObservableCollection<FileErrorGroup> LexicalGroups => _lexicalGroups;
    public ObservableCollection<FileErrorGroup> SyntaxGroups => _syntaxGroups;

    public ErrorsViewModel()
    {
        App.EventBus.Subscribe<LexicalAnalysisCompletedEvent>(OnLexicalCompleted);
        App.EventBus.Subscribe<LexicalAnalysisFailedEvent>(OnLexicalFailed);
        App.EventBus.Subscribe<SyntaxAnalysisCompletedEvent>(OnSyntaxCompleted);
        App.EventBus.Subscribe<SyntaxAnalysisFailedEvent>(OnSyntaxFailed);
        App.EventBus.Subscribe<FileClosedEvent>(OnFileClosed);
    }

    private void OnFileClosed(FileClosedEvent e)
    {
        if (string.IsNullOrEmpty(e.FilePath)) return;
        RemoveGroupIfExists(_lexicalGroups, e.FilePath);
        RemoveGroupIfExists(_syntaxGroups, e.FilePath);
    }

    private void RemoveGroupIfExists(ObservableCollection<FileErrorGroup> collection, string filePath)
    {
        var existing = collection.FirstOrDefault(g => string.Equals(g.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => collection.Remove(existing));
                return;
            }
            collection.Remove(existing);
        }
    }

    private void OnLexicalFailed(LexicalAnalysisFailedEvent e)
    {
        var path = e.FilePath ?? string.Empty;
        var message = e.Message;
        Dispatcher.UIThread.Post(() => AddOrUpdateGroupWithMessages(_lexicalGroups, path, new[] { (message, 0, 0) }));
    }

    private void OnLexicalCompleted(LexicalAnalysisCompletedEvent e)
    {
        var path = e.FilePath ?? string.Empty;
        var errors = e.Errors;
        var messages = errors.Select(err => (FormatError(err), err.Line, err.Column)).ToArray();
        Dispatcher.UIThread.Post(() => AddOrUpdateGroupWithMessages(_lexicalGroups, path, messages));
    }

    private void OnSyntaxFailed(SyntaxAnalysisFailedEvent e)
    {
        var path = e.FilePath ?? string.Empty;
        var message = e.Message;
        Dispatcher.UIThread.Post(() => AddOrUpdateGroupWithMessages(_syntaxGroups, path, new[] { (message, 0, 0) }));
    }

    private void OnSyntaxCompleted(SyntaxAnalysisCompletedEvent e)
    {
        var path = e.FilePath ?? string.Empty;
        var errors = e.Errors ?? new List<ParseError>();
        var messages = errors.Select(err => (FormatParseError(err), err.Line, err.Column)).ToArray();
        Dispatcher.UIThread.Post(() => AddOrUpdateGroupWithMessages(_syntaxGroups, path, messages));
    }

    private string FormatParseError(ParseError err)
    {
        if (!string.IsNullOrEmpty(err.ExpectedToken) || !string.IsNullOrEmpty(err.FoundToken))
        {
            var expected = string.IsNullOrEmpty(err.ExpectedToken) ? "" : $"Expected '{err.ExpectedToken}' ";
            var found = string.IsNullOrEmpty(err.FoundToken) ? "" : $"but found '{err.FoundToken}'";
            return string.IsNullOrWhiteSpace(expected + found) ? err.Message : $"{expected}{found} ({err.Message})";
        }
        return err.Message;
    }

    private static string FormatError(ErrorToken err)
    {
        if (!string.IsNullOrEmpty(err.Expected))
            return $"Expected '{err.Expected}' but found '{err.Value}'";
        return $"{err.Type}: {err.Value}";
    }

    private static readonly object _groupLock = new();

    private void AddOrUpdateGroupWithMessages(ObservableCollection<FileErrorGroup> target, string filePath, (string msg, int line, int col)[] messages)
    {
        lock (_groupLock)
        {
            var group = target.FirstOrDefault(g => string.Equals(g.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (group == null && messages.Length > 0)
            {
                group = new FileErrorGroup(filePath);
                target.Add(group);
            }

            if (group != null)
            {
                group.Errors.Clear();
                foreach (var (msg, line, col) in messages)
                {
                    group.Errors.Add(new ErrorItem(filePath, msg, line, col));
                }

                if (group.Errors.Count == 0)
                {
                    target.Remove(group);
                }
            }
        }
    }
}
