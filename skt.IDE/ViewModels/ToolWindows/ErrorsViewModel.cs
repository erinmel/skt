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
    private readonly ObservableCollection<FileErrorGroup> _groups = new();

    public ObservableCollection<FileErrorGroup> Groups => _groups;

    public ErrorsViewModel()
    {
        App.EventBus.Subscribe<LexicalAnalysisCompletedEvent>(OnLexicalCompleted);
        App.EventBus.Subscribe<LexicalAnalysisFailedEvent>(OnLexicalFailed);
        App.EventBus.Subscribe<FileClosedEvent>(OnFileClosed);
    }

    private void OnFileClosed(FileClosedEvent e)
    {
        if (string.IsNullOrEmpty(e.FilePath)) return;
        var existing = _groups.FirstOrDefault(g => string.Equals(g.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => _groups.Remove(existing));
                return;
            }
            _groups.Remove(existing);
        }
    }

    private void OnLexicalFailed(LexicalAnalysisFailedEvent e)
    {
        var path = e.FilePath ?? string.Empty;
        var message = e.Message;
        Dispatcher.UIThread.Post(() => AddOrUpdateGroupWithMessages(path, new[] { (message, 0, 0) }));
    }

    private void OnLexicalCompleted(LexicalAnalysisCompletedEvent e)
    {
        var path = e.FilePath ?? string.Empty;
        var errors = e.Errors;
        var messages = errors.Select(err => (FormatError(err), err.Line, err.Column)).ToArray();
        Dispatcher.UIThread.Post(() => AddOrUpdateGroupWithMessages(path, messages));
    }

    private static string FormatError(ErrorToken err)
    {
        if (!string.IsNullOrEmpty(err.Expected))
            return $"Expected '{err.Expected}' but found '{err.Value}'";
        return $"{err.Type}: {err.Value}";
    }

    private void AddOrUpdateGroupWithMessages(string filePath, (string msg, int line, int col)[] messages)
    {
        var group = _groups.FirstOrDefault(g => string.Equals(g.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (group == null && messages.Length > 0)
        {
            group = new FileErrorGroup(filePath);
            _groups.Add(group);
        }

        if (group != null)
        {
            group.Errors.Clear();
            foreach (var (msg, line, col) in messages)
            {
                group.Errors.Add(new ErrorItem(filePath, msg, line, col));
            }

            // If no errors, remove the group
            if (group.Errors.Count == 0)
            {
                _groups.Remove(group);
            }
        }
    }
}
