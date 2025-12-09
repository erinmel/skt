using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using skt.IDE.Services;
using skt.IDE.Services.Buss;
using skt.Shared;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Styling;
using System.Diagnostics;

namespace skt.IDE.Views.Editor;

public partial class TextEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TextEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private AvaloniaEdit.TextEditor? _editor;
    private bool _isSubscribed;
    private string _lastLexSnapshot = string.Empty;
    private string? _currentDocumentPath;
    private SemanticColorizer? _colorizer;

    private static readonly ConcurrentDictionary<string, int> PendingCarets = new();
    private static readonly ConcurrentDictionary<TextEditor, byte> s_instances = new();

    // Called by ThemeManager to forward theme changes to all registered editors.
    public static void ApplyThemeToAll(AppThemeVariant variant)
    {
        try
        {
            foreach (var editor in s_instances.Keys)
            {
                var ed = editor;
                Dispatcher.UIThread.Post(() =>
                {
                    try { ed.ApplyTheme(variant); } catch (Exception ex) { Debug.WriteLine($"ApplyThemeToAll: {ex}"); }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyThemeToAll: outer error applying theme to all editors: {ex}");
        }
    }

    private static void FocusMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var win = desktop.MainWindow;
            if (win != null)
            {
                win.Activate();
                win.Focus();
            }
        }
    }

    private void EnsureFocusWithRetries(int attempts = 4, int delayMs = 35)
    {
        if (_editor == null) return;
        int remaining = attempts;
        void TryFocus()
        {
            if (_editor == null) return;
            if (_editor.IsFocused || _editor.TextArea.IsFocused)
                return;
            _editor.Focus();
            _editor.TextArea.Focus();
            _editor.TextArea.Caret.BringCaretToView();
            remaining--;
            if (remaining > 0)
            {
                DispatcherTimer.RunOnce(TryFocus, TimeSpan.FromMilliseconds(delayMs));
            }
        }
        // Kick off
        DispatcherTimer.RunOnce(TryFocus, TimeSpan.FromMilliseconds(delayMs));
    }

    private void ApplyCaretAndFocus(int caret)
    {
        if (_editor == null) return;
        var docText = _editor.Document?.Text ?? string.Empty;
        caret = Math.Clamp(caret, 0, docText.Length);
        _editor.CaretOffset = caret;
        if (_editor.Document != null)
        {
            var loc = _editor.Document.GetLocation(caret);
            // Scroll with a small vertical margin so the caret isn't placed at the very top or bottom.
            const int verticalMarginLines = 3;
            int scrollLine = Math.Clamp(loc.Line - verticalMarginLines, 1, _editor.Document.LineCount);
            _editor.ScrollTo(scrollLine, loc.Column);
        }
        FocusMainWindow();
        _editor.Focus();
        _editor.TextArea.Focus();
        _editor.TextArea.Caret.BringCaretToView();
        PublishCursor();
        PublishSelection();
        EnsureFocusWithRetries();
        Dispatcher.UIThread.Post(() =>
        {
            if (_editor.Document != null)
            {
                var loc2 = _editor.Document.GetLocation(_editor.CaretOffset);
                int scrollLine2 = Math.Clamp(loc2.Line - 2, 1, _editor.Document.LineCount);
                _editor.ScrollTo(scrollLine2, loc2.Column);
            }
            _editor.Focus();
            _editor.TextArea.Focus();
            _editor.TextArea.Caret.BringCaretToView();
        }, DispatcherPriority.Input);
        Dispatcher.UIThread.Post(() =>
        {
            _editor.TextArea.Caret.BringCaretToView();
        }, DispatcherPriority.Render);
    }

    public TextEditor()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        PropertyChanged += OnTextPropertyChanged;
        DataContextChanged += OnDataContextChanged;

        // Subscribe to theme changes so this editor updates its brushes and colorizer
        ThemeManager.ThemeApplied += OnThemeApplied;
    }

    // Internal handler subscribed to ThemeManager.ThemeApplied. Forwards to ApplyTheme on UI thread.
    private void OnThemeApplied(AppThemeVariant variant)
    {
        Dispatcher.UIThread.Post(() => ApplyTheme(variant));
    }

    // Called by containers (e.g. TabbedEditor) to apply a new theme variant to this editor.
    public void ApplyTheme(AppThemeVariant variant)
    {
        Debug.WriteLine($"  TextEditor.ApplyTheme: Applying {variant} theme to editor (document: {_currentDocumentPath ?? "no document"})");
        bool isDark = variant == AppThemeVariant.Dark;

        // Update semantic colorizer to change suffix/brush lookups and invalidate cached pens
        Debug.WriteLine($"    - Updating colorizer isDark={isDark}");
        _colorizer?.ApplyTheme(isDark);

        // Force re-attach the colorizer to the TextView to ensure it re-processes lines
        try
        {
            if (_editor?.TextArea?.TextView != null && _colorizer != null)
            {
                var tv = _editor.TextArea.TextView;
                if (tv.LineTransformers.Contains(_colorizer))
                {
                    tv.LineTransformers.Remove(_colorizer);
                    tv.LineTransformers.Add(_colorizer);
                }
                if (tv.BackgroundRenderers.Contains(_colorizer))
                {
                    tv.BackgroundRenderers.Remove(_colorizer);
                    tv.BackgroundRenderers.Add(_colorizer);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TextEditor.ApplyTheme: error updating colorizer: {ex}");
        }

        // Also refresh syntax colors from current ViewModel tokens to force a re-colorize pass
        try
        {
            Debug.WriteLine($"    - Refreshing syntax colors");
            RefreshSyntaxColors(variant);
            Debug.WriteLine($"    - Syntax colors refreshed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TextEditor.ApplyTheme: failed refreshing syntax colors: {ex}");
        }

        // Update editor-level resources (font size, foreground/background) from application resources
        var app = Application.Current;
        if (app?.Resources != null && _editor != null)
        {
            if (app.Resources.TryGetValue("EditorFontSize", out var efs) && efs is double ef)
                _editor.FontSize = ef;

            if (app.Resources.TryGetValue("AppForegroundHighBrush", out var fg) && fg is IBrush fb)
                _editor.Foreground = fb;

            if (app.Resources.TryGetValue("AppBackgroundBrush", out var bg) && bg is IBrush bb)
                _editor.Background = bb;
        }

        // Force redraw so token brushes are re-resolved
        _editor?.TextArea?.TextView?.Redraw();
    }

    // Force the colorizer to re-run using current ViewModel tokens (useful after resources reload).
    public void RefreshSyntaxColors(AppThemeVariant variant)
    {
        if (_colorizer == null || _editor == null) return;
        if (DataContext is ViewModels.TextEditorViewModel vm)
        {
            bool isDark = variant == AppThemeVariant.Dark;
            _colorizer.Update(vm.Tokens, vm.LexicalErrors, isDark);
            _editor.TextArea.TextView.Redraw();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_editor == null) return;

        if (DataContext is ViewModels.TextEditorViewModel doc && !string.IsNullOrEmpty(doc.FilePath))
        {
            _currentDocumentPath = doc.FilePath;

            // Restore syntax highlighting from active editor
            bool isDark = (Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark) != ThemeVariant.Light;
            _colorizer?.Update(doc.Tokens, doc.LexicalErrors, isDark);
            _editor.TextArea.TextView.Redraw();

            // Restore caret position
            if (doc.CaretPosition > 0)
            {
                _editor.CaretOffset = Math.Clamp(doc.CaretPosition, 0, _editor.Document?.TextLength ?? 0);
            }

            // Handle pending caret position
            if (PendingCarets.TryRemove(doc.FilePath, out var pendingOffset))
            {
                Dispatcher.UIThread.Post(() => ApplyCaretAndFocus(pendingOffset), DispatcherPriority.Background);
            }

            // Notify that document changed
            App.Messenger.Send(new SelectedDocumentChangedEvent(doc.FilePath, true, doc.IsDirty));
        }
        else
        {
            _currentDocumentPath = null;
            _colorizer?.Update(Array.Empty<Token>(), Array.Empty<ErrorToken>(), false);
            _editor?.TextArea.TextView.Redraw();
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Register instance for global theme notifications
        s_instances.TryAdd(this, 0);
        _editor = this.FindControl<AvaloniaEdit.TextEditor>("Editor");
        if (_editor == null) return;

        if (_editor.Document == null)
            _editor.Document = new TextDocument(Text ?? string.Empty);
        else if (Text != _editor.Document.Text)
            _editor.Document.Text = Text ?? string.Empty;

        // Configure editor options for smart indentation with tabs converted to spaces
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.IndentationSize = 4;
        _editor.Options.ShowTabs = true;
        _editor.Options.ShowSpaces = false;

        // Apply font size from theme manager
        var app = Application.Current;
        if (app?.Resources != null)
        {
            if (app.Resources.TryGetValue("EditorFontSize", out var efs) && efs is double ef)
                _editor.FontSize = ef;

            if (app.Resources.TryGetValue("AppForegroundHighBrush", out var fg) && fg is IBrush fb)
                _editor.Foreground = fb;

            if (app.Resources.TryGetValue("AppBackgroundBrush", out var bg) && bg is IBrush bb)
                _editor.Background = bb;
        }

        _colorizer = new SemanticColorizer();
        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_colorizer);

        _editor.TextChanged += OnEditorTextChanged;
        _editor.TextArea.Caret.PositionChanged += OnCaretChanged;
        _editor.TextArea.SelectionChanged += OnSelectionChanged;

        if (!_isSubscribed)
        {
            App.Messenger.Register<SetCaretPositionRequestEvent>(this, (_, m) => OnSetCaretRequest(m));
            App.Messenger.Register<SetCaretLineColumnRequestEvent>(this, (_, m) => OnSetCaretLineColumnRequest(m));
            App.Messenger.Register<LexicalAnalysisCompletedEvent>(this, (_, m) => OnLexicalCompleted(m));
            App.Messenger.Register<LexicalAnalysisFailedEvent>(this, (_, m) => OnLexicalFailed(m));
            App.Messenger.Register<SemanticAnalysisCompletedEvent>(this, (_, m) => OnSemanticCompleted(m));
            App.Messenger.Register<SemanticAnalysisFailedEvent>(this, (_, m) => OnSemanticFailed(m));
            _isSubscribed = true;
        }

        // Set current document path and restore state
        if (DataContext is ViewModels.TextEditorViewModel doc && !string.IsNullOrEmpty(doc.FilePath))
        {
            _currentDocumentPath = doc.FilePath;
            bool isDark = (Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark) != ThemeVariant.Light;
            _colorizer.Update(doc.Tokens, doc.LexicalErrors, isDark);
            _editor.TextArea.TextView.Redraw();
        }

        Dispatcher.UIThread.Post(() =>
        {
            PublishCursor();
            PublishSelection();
            PublishBufferLexical();
        }, DispatcherPriority.Loaded);

        if (DataContext is not ViewModels.TextEditorViewModel docForCaret
            || string.IsNullOrEmpty(docForCaret.FilePath)
            || !PendingCarets.TryRemove(docForCaret.FilePath, out var pendingOffset)) return;
        Dispatcher.UIThread.Post(() =>
        {
            ApplyCaretAndFocus(pendingOffset);
            EnsureFocusWithRetries();
        }, DispatcherPriority.Background);
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Unregister instance
        s_instances.TryRemove(this, out _);
        if (_editor != null)
        {
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.TextArea.Caret.PositionChanged -= OnCaretChanged;
            _editor.TextArea.SelectionChanged -= OnSelectionChanged;
        }
        if (_isSubscribed)
        {
            App.Messenger.UnregisterAll(this);
            _isSubscribed = false;
        }

        // Unsubscribe theme handler to avoid leaks
        ThemeManager.ThemeApplied -= OnThemeApplied;
    }

    private void OnSetCaretRequest(SetCaretPositionRequestEvent e)
    {
        if (DataContext is not ViewModels.TextEditorViewModel doc || string.IsNullOrEmpty(doc.FilePath))
        {
            PendingCarets.AddOrUpdate(e.FilePath, e.CaretIndex, (_, _) => e.CaretIndex);
            return;
        }
        if (!string.Equals(doc.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            PendingCarets.AddOrUpdate(e.FilePath, e.CaretIndex, (_, _) => e.CaretIndex);
            return;
        }
        if (_editor == null)
        {
            PendingCarets.AddOrUpdate(e.FilePath, e.CaretIndex, (_, _) => e.CaretIndex);
            return;
        }
        var docText = _editor.Document?.Text ?? string.Empty;
        var caret = Math.Clamp(e.CaretIndex, 0, docText.Length);
        Dispatcher.UIThread.Post(() =>
        {
            ApplyCaretAndFocus(caret);
        }, DispatcherPriority.Background);
    }

    private void OnSetCaretLineColumnRequest(SetCaretLineColumnRequestEvent e)
    {
        if (DataContext is not ViewModels.TextEditorViewModel doc || string.IsNullOrEmpty(doc.FilePath))
        {
            return;
        }

        if (!string.Equals(doc.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_editor?.Document == null) return;
        int offset = MapLineColumnToOffset(_editor.Document, e.Line, e.Column);
        Dispatcher.UIThread.Post(() => ApplyCaretAndFocus(offset), DispatcherPriority.Background);
    }

    private static int MapLineColumnToOffset(TextDocument doc, int line, int column, int tabWidth = 4)
    {
        line = Math.Clamp(line, 1, doc.LineCount);
        var docLine = doc.GetLineByNumber(line);
        int lineStart = docLine.Offset;
        int lineEnd = docLine.EndOffset; // excludes line break
        int visualCol = 1;
        for (int i = lineStart; i < lineEnd; i++)
        {
            if (visualCol >= column) return i;
            char ch = doc.GetCharAt(i);
            if (ch == '\t')
                visualCol += tabWidth; // matches tokenizer fixed width tab advance
            else
                visualCol++;
        }
        // If requested column is beyond line content, place at end
        return lineEnd;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_editor == null) return;
        var docText = _editor.Document?.Text ?? string.Empty;
        if (GetValue(TextProperty) != docText)
            SetValue(TextProperty, docText);
        PublishBufferLexical();
    }

    private void OnCaretChanged(object? sender, EventArgs e)
    {
        PublishCursor();
        PublishSelection();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        PublishSelection();
    }

    private void OnTextPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextProperty && _editor?.Document != null)
        {
            var newText = GetValue(TextProperty) ?? string.Empty;
            if (_editor.Document.Text != newText)
            {
                _editor.Document.Text = newText;
                PublishCursor();
                PublishSelection();
                PublishBufferLexical();
            }
        }
    }

    private void PublishBufferLexical()
    {
        if (_editor?.Document == null) return;
        var text = _editor.Document.Text;
        if (text == _lastLexSnapshot) return;
        _lastLexSnapshot = text;
        string? filePath = null;
        if (DataContext is ViewModels.TextEditorViewModel doc)
        {
            filePath = doc.FilePath;
            _currentDocumentPath = filePath;
        }
        App.Messenger.Send(new TokenizeBufferRequestEvent(text, filePath));
    }

    private void OnLexicalCompleted(LexicalAnalysisCompletedEvent e)
    {
        if (_editor == null || _colorizer == null) return;

        var eventPath = e.FilePath ?? string.Empty;
        var docPath = _currentDocumentPath ?? string.Empty;

        // Update state manager (it's already updated by DocumentStateManager itself)

        // Only update colorizer if this event is for the current document
        var isForCurrentDocument = string.Equals(docPath, eventPath, StringComparison.OrdinalIgnoreCase);

        if (isForCurrentDocument)
        {
            bool isDark = (Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark) != ThemeVariant.Light;
            _colorizer.Update(e.Tokens, e.Errors, isDark);
            _editor.TextArea.TextView.Redraw();
        }
    }

    private void OnLexicalFailed(LexicalAnalysisFailedEvent e)
    {
        if (_editor == null || _colorizer == null) return;

        var eventPath = e.FilePath ?? string.Empty;
        var docPath = _currentDocumentPath ?? string.Empty;

        var isForCurrentDocument = string.Equals(docPath, eventPath, StringComparison.OrdinalIgnoreCase);

        if (isForCurrentDocument)
        {
            bool isDark = (Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark) != ThemeVariant.Light;
            _colorizer.Update(Array.Empty<Token>(), Array.Empty<ErrorToken>(), isDark);
            _editor.TextArea.TextView.Redraw();
        }
    }

    private void OnSemanticCompleted(SemanticAnalysisCompletedEvent e)
    {
        if (_editor == null || _colorizer == null) return;

        var eventPath = e.FilePath ?? string.Empty;
        var docPath = _currentDocumentPath ?? string.Empty;

        var isForCurrentDocument = string.Equals(docPath, eventPath, StringComparison.OrdinalIgnoreCase);

        if (isForCurrentDocument)
        {
            _colorizer.UpdateSemanticErrors(e.Errors);
            _editor.TextArea.TextView.Redraw();
        }
    }

    private void OnSemanticFailed(SemanticAnalysisFailedEvent e)
    {
        if (_editor == null || _colorizer == null) return;

        var eventPath = e.FilePath ?? string.Empty;
        var docPath = _currentDocumentPath ?? string.Empty;

        var isForCurrentDocument = string.Equals(docPath, eventPath, StringComparison.OrdinalIgnoreCase);

        if (isForCurrentDocument)
        {
            _colorizer.UpdateSemanticErrors(Array.Empty<SemanticError>());
            _editor.TextArea.TextView.Redraw();
        }
    }

    private void PublishCursor()
    {
        if (_editor?.Document == null) return;
        var offset = _editor.CaretOffset;
        offset = Math.Clamp(offset, 0, _editor.Document.TextLength);
        var loc = _editor.Document.GetLocation(offset);
        App.Messenger.Send(new CursorPositionEvent(loc.Line, loc.Column));
    }

    private void PublishSelection()
    {
        if (_editor?.Document == null) return;
        int start = _editor.SelectionStart;
        int length = _editor.SelectionLength;
        int end = start + length;
        start = Math.Clamp(start, 0, _editor.Document.TextLength);
        end = Math.Clamp(end, 0, _editor.Document.TextLength);
        if (length <= 0)
        {
            var loc = _editor.Document.GetLocation(_editor.CaretOffset);
            App.Messenger.Send(new SelectionInfoEvent(loc.Line, loc.Column, loc.Line, loc.Column, 0, 0));
            return;
        }
        var startLoc = _editor.Document.GetLocation(start);
        var endLoc = _editor.Document.GetLocation(end);
        int lineBreaks = CountLineBreaks(_editor.Document, start, end);
        App.Messenger.Send(new SelectionInfoEvent(startLoc.Line, startLoc.Column, endLoc.Line, endLoc.Column, length, lineBreaks));
    }

    private static int CountLineBreaks(TextDocument doc, int start, int end)
    {
        int count = 0;
        for (int i = start; i < end && i < doc.TextLength; i++)
        {
            if (doc.GetCharAt(i) == '\n') count++;
        }
        return count;
    }

    public class SemanticColorizer : DocumentColorizingTransformer, IBackgroundRenderer
    {
        private bool _isDark;
        private readonly Dictionary<int, List<SpanPart>> _tokenPartsByLine = new();
        private readonly Dictionary<int, List<SpanPart>> _errorPartsByLine = new();
        private readonly Dictionary<int, List<SpanPart>> _semanticErrorPartsByLine = new();
        private readonly List<int> _sortedErrorLines = new();
        private readonly List<int> _sortedSemanticErrorLines = new();
        private Pen? _errorPen;
        private Pen? _semanticErrorPen;
        private readonly Dictionary<string, IBrush> _brushCache = new(); // Cache for brush lookups

        private readonly struct SpanPart
        {
            public readonly int StartColumn; // 1-based inclusive
            public readonly int EndColumnExclusive; // exclusive, int.MaxValue means to end of line
            public readonly TokenType Type;
            public SpanPart(int start, int endExclusive, TokenType type)
            {
                StartColumn = start;
                EndColumnExclusive = endExclusive;
                Type = type;
            }
        }

        private static readonly Dictionary<TokenType, (Color dark, Color light)> Fallback = new()
        {
            { TokenType.Integer, (Color.FromRgb(0x4F,0xC1,0xFF), Color.FromRgb(0x00,0x55,0x99)) },
            { TokenType.Real, (Color.FromRgb(0x4F,0xC1,0xFF), Color.FromRgb(0x00,0x55,0x99)) },
            { TokenType.Boolean, (Color.FromRgb(0xC5,0xAE,0xFF), Color.FromRgb(0x64,0x2C,0x99)) },
            { TokenType.String, (Color.FromRgb(0xCE,0x91,0x78), Color.FromRgb(0x8B,0x2F,0x00)) },
            { TokenType.Identifier, (Color.FromRgb(0xE0,0xE0,0xE0), Color.FromRgb(0x20,0x20,0x20)) },
            { TokenType.ReservedWord, (Color.FromRgb(0xB4,0x8E,0xF0), Color.FromRgb(0x53,0x19,0x95)) },
            { TokenType.Comment, (Color.FromRgb(0x57,0xA6,0x4A), Color.FromRgb(0x2F,0x63,0x26)) },
            { TokenType.ArithmeticOperator, (Color.FromRgb(0xFF,0xC1,0x6B), Color.FromRgb(0xB0,0x42,0x00)) },
            { TokenType.RelationalOperator, (Color.FromRgb(0xFF,0xC1,0x6B), Color.FromRgb(0xB0,0x42,0x00)) },
            { TokenType.LogicalOperator, (Color.FromRgb(0xFF,0xC1,0x6B), Color.FromRgb(0xB0,0x42,0x00)) },
            { TokenType.AssignmentOperator, (Color.FromRgb(0xFF,0xC1,0x6B), Color.FromRgb(0xB0,0x42,0x00)) },
            { TokenType.ShiftOperator, (Color.FromRgb(0xFF,0xC1,0x6B), Color.FromRgb(0xB0,0x42,0x00)) },
            { TokenType.Symbol, (Color.FromRgb(0xA9,0xA9,0xA9), Color.FromRgb(0x44,0x44,0x44)) },
            { TokenType.Error, (Color.FromRgb(0xFF,0x55,0x55), Color.FromRgb(0xC5,0x00,0x00)) }
        };

        public void Update(IEnumerable<Token> tokens, IEnumerable<ErrorToken> errors, bool isDark)
        {
            // Clear cache if theme changed
            if (_isDark != isDark)
            {
                _brushCache.Clear();
            }
            
            _isDark = isDark;
            _tokenPartsByLine.Clear();
            _errorPartsByLine.Clear();
            _semanticErrorPartsByLine.Clear();
            foreach (var t in tokens)
                ExplodeMultiLine(t.Line, t.EndLine, t.Column, t.EndColumn, t.Type, _tokenPartsByLine);
            foreach (var e in errors)
                ExplodeMultiLine(e.Line, e.EndLine, e.Column, e.EndColumn, TokenType.Error, _errorPartsByLine);
            _sortedErrorLines.Clear();
            _sortedErrorLines.AddRange(_errorPartsByLine.Keys);
            _sortedErrorLines.Sort();
            _sortedSemanticErrorLines.Clear();
            _errorPen = null;
            _semanticErrorPen = null;
        }

        public void UpdateSemanticErrors(IEnumerable<SemanticError> semanticErrors)
        {
            _semanticErrorPartsByLine.Clear();
            foreach (var e in semanticErrors)
                ExplodeMultiLine(e.Line, e.EndLine, e.Column, e.EndColumn, TokenType.Error, _semanticErrorPartsByLine);
            _sortedSemanticErrorLines.Clear();
            _sortedSemanticErrorLines.AddRange(_semanticErrorPartsByLine.Keys);
            _sortedSemanticErrorLines.Sort();
            _semanticErrorPen = null;
        }

        // New: allow theme changes to be applied without re-supplying tokens (keeps current token map)
        public void ApplyTheme(bool isDark)
        {
            _isDark = isDark;
            // Invalidate cached pens so they're recreated with the new brush
            _errorPen = null;
            _semanticErrorPen = null;
        }

        private static void ExplodeMultiLine(int startLine, int endLine, int startCol, int endCol, TokenType type, Dictionary<int, List<SpanPart>> target)
        {
            if (endLine < startLine) return;
            for (int line = startLine; line <= endLine; line++)
            {
                int sc = line == startLine ? startCol : 1;
                int ec = line == endLine ? endCol : int.MaxValue;
                AddSpan(target, line, new SpanPart(sc, ec, type));
            }
        }

        private static void AddSpan(Dictionary<int, List<SpanPart>> map, int line, SpanPart part)
        {
            if (!map.TryGetValue(line, out var list))
            {
                list = new List<SpanPart>();
                map[line] = list;
            }
            list.Add(part);
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (line.IsDeleted) return;
            int n = line.LineNumber;
            if (_tokenPartsByLine.TryGetValue(n, out var tokenParts))
                foreach (var p in tokenParts)
                    ApplySpan(line, p.StartColumn, p.EndColumnExclusive, p.Type);
            if (_errorPartsByLine.TryGetValue(n, out var errorParts))
                foreach (var p in errorParts)
                    ApplySpan(line, p.StartColumn, p.EndColumnExclusive, p.Type);
            // Note: Semantic errors are NOT colorized here - they only get yellow squiggles via Draw()
        }

        private void ApplySpan(DocumentLine line, int startCol, int endColExclusive, TokenType type)
        {
            if (startCol <= 0) startCol = 1;
            if (endColExclusive <= startCol) return;
            var doc = CurrentContext?.Document;
            if (doc == null) return;
            int lineStartOffset = line.Offset;
            int lineEndOffset = line.EndOffset;
            int spanStartOffset = lineStartOffset + startCol - 1;
            int spanEndOffset = endColExclusive == int.MaxValue ? lineEndOffset : lineStartOffset + endColExclusive - 1;
            spanStartOffset = Math.Clamp(spanStartOffset, lineStartOffset, lineEndOffset);
            spanEndOffset = Math.Clamp(spanEndOffset, spanStartOffset, lineEndOffset);
            if (spanEndOffset <= spanStartOffset) return;
            var brush = GetBrush(type);
            ChangeLinePart(spanStartOffset, spanEndOffset, el => el.TextRunProperties.SetForegroundBrush(brush));
        }

        private IBrush GetBrush(TokenType type)
        {
            string suffix = _isDark ? "Dark" : "Light";
            string key = type switch
            {
                TokenType.Integer => $"Tok.Integer.{suffix}",
                TokenType.Real => $"Tok.Real.{suffix}",
                TokenType.Boolean => $"Tok.Boolean.{suffix}",
                TokenType.String => $"Tok.String.{suffix}",
                TokenType.Identifier => $"Tok.Identifier.{suffix}",
                TokenType.ReservedWord => $"Tok.Reserved.{suffix}",
                TokenType.Comment => $"Tok.Comment.{suffix}",
                TokenType.ArithmeticOperator => $"Tok.Operator.{suffix}",
                TokenType.RelationalOperator => $"Tok.Operator.{suffix}",
                TokenType.LogicalOperator => $"Tok.Operator.{suffix}",
                TokenType.AssignmentOperator => $"Tok.Operator.{suffix}",
                TokenType.ShiftOperator => $"Tok.Operator.{suffix}",
                TokenType.Symbol => $"Tok.Symbol.{suffix}",
                TokenType.Error => $"Tok.Error.{suffix}",
                _ => string.Empty
            };
            
            // Check cache first - massive performance improvement
            if (_brushCache.TryGetValue(key, out var cachedBrush))
            {
                return cachedBrush;
            }
            
            // First try the base key (Tok.<Name>) which ThemeManager now maps to the active themed brush.
            string baseKey = type switch
            {
                TokenType.Integer => "Tok.Integer",
                TokenType.Real => "Tok.Real",
                TokenType.Boolean => "Tok.Boolean",
                TokenType.String => "Tok.String",
                TokenType.Identifier => "Tok.Identifier",
                TokenType.ReservedWord => "Tok.Reserved",
                TokenType.Comment => "Tok.Comment",
                TokenType.ArithmeticOperator => "Tok.Operator",
                TokenType.RelationalOperator => "Tok.Operator",
                TokenType.LogicalOperator => "Tok.Operator",
                TokenType.AssignmentOperator => "Tok.Operator",
                TokenType.ShiftOperator => "Tok.Operator",
                TokenType.Symbol => "Tok.Symbol",
                TokenType.Error => "Tok.Error",
                _ => string.Empty
            };

            IBrush? resultBrush = null;
            
            // Prefer the themed keys (Tok.*.Dark / Tok.*.Light) from the merged SyntaxColors dictionary.
            if (!string.IsNullOrEmpty(key) && Application.Current != null && Application.Current.TryFindResource(key, out var themedRes) && themedRes is IBrush themedBrush)
            {
                resultBrush = themedBrush;
            }
            // Fallback to base key mapping applied by ThemeManager (Tok.<Name>)
            else if (!string.IsNullOrEmpty(baseKey) && Application.Current != null && Application.Current.TryFindResource(baseKey, out var baseRes2) && baseRes2 is IBrush baseBrush2)
            {
                resultBrush = baseBrush2;
            }
            // Final fallback to hardcoded colors
            else if (Fallback.TryGetValue(type, out var fb))
            {
                resultBrush = new SolidColorBrush(_isDark ? fb.dark : fb.light);
            }
            else
            {
                resultBrush = _isDark ? Brushes.White : Brushes.Black;
            }
            
            // Cache the result for future use
            _brushCache[key] = resultBrush;
            
            return resultBrush;
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var doc = textView.Document;
            var visualLines = textView.VisualLines;
            if (doc == null || visualLines == null || visualLines.Count == 0) return;
            int first = visualLines[0].FirstDocumentLine.LineNumber;
            int last = visualLines[^1].LastDocumentLine.LineNumber;

            // Draw red squiggles for lexical errors
            if (_sortedErrorLines.Count > 0)
            {
                _errorPen ??= new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55)), 1.0);
                foreach (var lineNum in GetVisibleErrorLines(first, last, _sortedErrorLines))
                    DrawErrorWavesForLine(textView, drawingContext, doc, lineNum, _errorPen, _errorPartsByLine);
            }

            // Draw yellow squiggles for semantic errors (thicker for better visibility)
            if (_sortedSemanticErrorLines.Count > 0)
            {
                _semanticErrorPen ??= new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)), 1.5);
                foreach (var lineNum in GetVisibleErrorLines(first, last, _sortedSemanticErrorLines))
                    DrawErrorWavesForLine(textView, drawingContext, doc, lineNum, _semanticErrorPen, _semanticErrorPartsByLine);
            }
        }

        private IEnumerable<int> GetVisibleErrorLines(int first, int last, List<int> sortedLines)
        {
            if (sortedLines.Count == 0) yield break;
            int lo = 0, hi = sortedLines.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (sortedLines[mid] < first) lo = mid + 1; else hi = mid - 1;
            }
            for (int i = lo; i < sortedLines.Count; i++)
            {
                int line = sortedLines[i];
                if (line > last) yield break;
                yield return line;
            }
        }

        private void DrawErrorWavesForLine(TextView textView, DrawingContext ctx, TextDocument doc, int lineNum, Pen pen, Dictionary<int, List<SpanPart>> errorParts)
        {
            if (!errorParts.TryGetValue(lineNum, out var parts) || parts.Count == 0) return;
            if (lineNum < 1 || lineNum > doc.LineCount) return;
            var docLine = doc.GetLineByNumber(lineNum);
            int lineContentColumns = (docLine.EndOffset - docLine.Offset) + 1;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                int startCol = p.StartColumn > 0 ? p.StartColumn : 1;
                int endCol = p.EndColumnExclusive == int.MaxValue ? lineContentColumns : p.EndColumnExclusive;
                if (endCol <= startCol) continue;
                int startOffset = docLine.Offset + startCol - 1;
                int endOffset = docLine.Offset + endCol - 1;
                DrawWave(textView, ctx, doc, startOffset, endOffset, pen);
            }
        }

        private static void DrawWave(TextView view, DrawingContext ctx, TextDocument doc, int startOffset, int endOffset, Pen pen)
        {
            // Validate offsets before attempting to get location
            if (startOffset < 0 || startOffset > doc.TextLength || endOffset < 0 || endOffset > doc.TextLength)
                return;
            
            var start = view.GetVisualPosition(new TextViewPosition(doc.GetLocation(startOffset)), VisualYPosition.TextBottom);
            var end = view.GetVisualPosition(new TextViewPosition(doc.GetLocation(endOffset)), VisualYPosition.TextBottom);
            if (double.IsNaN(start.X) || double.IsNaN(end.X)) return;

            try
            {
                var viewType = view.GetType();
                var scrollProp = viewType.GetProperty("ScrollOffset");
                if (scrollProp != null)
                {
                    var scrollVal = scrollProp.GetValue(view);
                    if (scrollVal != null)
                    {
                        var sxProp = scrollVal.GetType().GetProperty("X");
                        var syProp = scrollVal.GetType().GetProperty("Y");
                        if (sxProp != null && syProp != null)
                        {
                            double sx = Convert.ToDouble(sxProp.GetValue(scrollVal));
                            double sy = Convert.ToDouble(syProp.GetValue(scrollVal));
                            start = new Point(start.X - sx, start.Y - sy);
                            end = new Point(end.X - sx, end.Y - sy);
                        }
                    }
                }
            }
            catch
            {
                // If reflection fails, fall back to original positions (best-effort).
            }

            double y = start.Y - 1;
            double x = start.X;
            double xEnd = end.X;
            const double step = 4;
            bool up = true;
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(new Point(x, y), false);
                while (x < xEnd)
                {
                    x += step / 2;
                    g.LineTo(new Point(x, y + (up ? -2 : 0)));
                    up = !up;
                }
            }
            ctx.DrawGeometry(null, pen, geo);
        }
    }
}
