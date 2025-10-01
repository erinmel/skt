using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using skt.IDE.Services.Buss;
using skt.Shared;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace skt.IDE.Views.TextEditor;

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
    private readonly List<Token> _currentTokens = new();
    private readonly List<ErrorToken> _currentErrors = new();
    private SemanticColorizer? _colorizer;

    public TextEditor()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        PropertyChanged += OnTextPropertyChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _editor = this.FindControl<AvaloniaEdit.TextEditor>("Editor");
        if (_editor == null) return;

        if (_editor.Document == null)
            _editor.Document = new TextDocument(Text ?? string.Empty);
        else if (Text != _editor.Document.Text)
            _editor.Document.Text = Text ?? string.Empty;

        _colorizer = new SemanticColorizer();
        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_colorizer);

        _editor.TextChanged += OnEditorTextChanged;
        _editor.TextArea.Caret.PositionChanged += OnCaretChanged;
        _editor.TextArea.SelectionChanged += OnSelectionChanged;

        if (!_isSubscribed)
        {
            App.EventBus.Subscribe<SetCaretPositionRequestEvent>(OnSetCaretRequest);
            App.EventBus.Subscribe<LexicalAnalysisCompletedEvent>(OnLexicalCompleted);
            App.EventBus.Subscribe<LexicalAnalysisFailedEvent>(OnLexicalFailed);
            _isSubscribed = true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            PublishCursor();
            PublishSelection();
            PublishBufferLexical();
        }, DispatcherPriority.Loaded);
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_editor != null)
        {
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.TextArea.Caret.PositionChanged -= OnCaretChanged;
            _editor.TextArea.SelectionChanged -= OnSelectionChanged;
        }
        if (_isSubscribed)
        {
            App.EventBus.Unsubscribe<SetCaretPositionRequestEvent>(OnSetCaretRequest);
            App.EventBus.Unsubscribe<LexicalAnalysisCompletedEvent>(OnLexicalCompleted);
            App.EventBus.Unsubscribe<LexicalAnalysisFailedEvent>(OnLexicalFailed);
            _isSubscribed = false;
        }
    }

    private void OnSetCaretRequest(SetCaretPositionRequestEvent e)
    {
        if (_editor == null) return;
        if (DataContext is not ViewModels.DocumentViewModel doc || string.IsNullOrEmpty(doc.FilePath)) return;
        if (!string.Equals(doc.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase)) return;
        var docText = _editor.Document?.Text ?? string.Empty;
        var caret = Math.Clamp(e.CaretIndex, 0, docText.Length);
        Dispatcher.UIThread.Post(() =>
        {
            _editor.CaretOffset = caret;
            _editor.Focus();
            PublishCursor();
            PublishSelection();
        });
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
        if (DataContext is ViewModels.DocumentViewModel doc) filePath = doc.FilePath;
        App.EventBus.Publish(new TokenizeBufferRequestEvent(text, filePath));
    }

    private void OnLexicalCompleted(LexicalAnalysisCompletedEvent e)
    {
        if (_editor == null || _colorizer == null) return;
        if (DataContext is ViewModels.DocumentViewModel doc)
        {
            if (!string.Equals(doc.FilePath ?? string.Empty, e.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) && !e.FromBuffer) return;
        }
        _currentTokens.Clear();
        _currentTokens.AddRange(e.Tokens);
        _currentErrors.Clear();
        _currentErrors.AddRange(e.Errors);
        bool isDark = (Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark) != ThemeVariant.Light;
        _colorizer.Update(_currentTokens, _currentErrors, isDark);
        _editor.TextArea.TextView.Redraw();
    }

    private void OnLexicalFailed(LexicalAnalysisFailedEvent e)
    {
    }

    private void PublishCursor()
    {
        if (_editor?.Document == null) return;
        var offset = _editor.CaretOffset;
        offset = Math.Clamp(offset, 0, _editor.Document.TextLength);
        var loc = _editor.Document.GetLocation(offset);
        App.EventBus.Publish(new CursorPositionEvent(loc.Line, loc.Column));
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
            App.EventBus.Publish(new SelectionInfoEvent(loc.Line, loc.Column, loc.Line, loc.Column, 0, 0));
            return;
        }
        var startLoc = _editor.Document.GetLocation(start);
        var endLoc = _editor.Document.GetLocation(end);
        int lineBreaks = CountLineBreaks(_editor.Document, start, end);
        App.EventBus.Publish(new SelectionInfoEvent(startLoc.Line, startLoc.Column, endLoc.Line, endLoc.Column, length, lineBreaks));
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

    private class SemanticColorizer : DocumentColorizingTransformer, IBackgroundRenderer
    {
        private readonly List<Token> _tokens = new();
        private readonly List<ErrorToken> _errors = new();
        private bool _isDark;
        // Fallback colors if resource lookup fails
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
            _tokens.Clear();
            _tokens.AddRange(tokens);
            _errors.Clear();
            _errors.AddRange(errors);
            _isDark = isDark;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (line.IsDeleted) return;
            int lineNumber = line.LineNumber;
            foreach (var t in _tokens)
            {
                if (t.EndLine < lineNumber || t.Line > lineNumber) continue;
                int startCol = (t.Line == lineNumber) ? t.Column : 1;
                int endColExclusive = (t.EndLine == lineNumber) ? t.EndColumn : int.MaxValue;
                ApplySpan(line, startCol, endColExclusive, t.Type, false);
            }
            foreach (var e in _errors)
            {
                if (e.EndLine < lineNumber || e.Line > lineNumber) continue;
                int startCol = (e.Line == lineNumber) ? e.Column : 1;
                int endColExclusive = (e.EndLine == lineNumber) ? e.EndColumn : int.MaxValue;
                ApplySpan(line, startCol, endColExclusive, TokenType.Error, true);
            }
        }

        private void ApplySpan(DocumentLine line, int startCol, int endColExclusive, TokenType type, bool error)
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
            ChangeLinePart(spanStartOffset, spanEndOffset, el => { el.TextRunProperties.SetForegroundBrush(brush); });
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
            if (!string.IsNullOrEmpty(key) && Application.Current != null && Application.Current.TryFindResource(key, out var res) && res is IBrush b)
                return b;
            if (Fallback.TryGetValue(type, out var fb))
                return new SolidColorBrush(_isDark ? fb.dark : fb.light);
            return _isDark ? Brushes.White : Brushes.Black;
        }

        public KnownLayer Layer => KnownLayer.Selection; // draw over selection for underline

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_errors.Count == 0) return;
            var doc = textView.Document;
            if (doc == null) return;
            foreach (var e in _errors)
            {
                for (int line = e.Line; line <= e.EndLine; line++)
                {
                    var docLine = doc.GetLineByNumber(Math.Clamp(line, 1, doc.LineCount));
                    int startCol = (line == e.Line) ? e.Column : 1;
                    int endCol = (line == e.EndLine) ? e.EndColumn : (docLine.EndOffset - docLine.Offset) + 1;
                    if (startCol <= 0) startCol = 1;
                    if (endCol <= startCol) continue;
                    int startOffset = docLine.Offset + startCol - 1;
                    int endOffset = docLine.Offset + endCol - 1;
                    var geoBuilder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 0 }; // not used for wave, but keep
                    var start = textView.GetVisualPosition(new TextViewPosition(doc.GetLocation(startOffset)), VisualYPosition.TextBottom);
                    var end = textView.GetVisualPosition(new TextViewPosition(doc.GetLocation(endOffset)), VisualYPosition.TextBottom);
                    if (double.IsNaN(start.X) || double.IsNaN(end.X)) continue;
                    double y = start.Y - 1;
                    double x = start.X;
                    double xEnd = end.X;
                    var brush = GetBrush(TokenType.Error);
                    var pen = new Pen(brush, 1);
                    bool up = true;
                    double step = 4;
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        ctx.BeginFigure(new Avalonia.Point(x, y), false);
                        while (x < xEnd)
                        {
                            x += step / 2;
                            ctx.LineTo(new Avalonia.Point(x, y + (up ? -2 : 0)));
                            up = !up;
                        }
                    }
                    drawingContext.DrawGeometry(null, pen, geo);
                }
            }
        }
    }
}
