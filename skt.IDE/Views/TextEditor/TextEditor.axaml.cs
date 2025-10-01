using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System.Linq;
using System.Text;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using skt.IDE.Services.Buss;

namespace skt.IDE.Views.TextEditor
{
    public partial class TextEditor : UserControl
    {
        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<TextEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private ScrollViewer? _mainScroll;
        private ScrollViewer? _lineNumbersScroll;
        private TextBox? _mainEditor;
        private TextBox? _lineNumbers;
        private bool _isSyncingScroll;
        private bool _isSubscribed;
        private string _lastLexSnapshot = string.Empty; // snapshot for duplicate suppression

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
            _mainEditor = this.FindControl<TextBox>("MainEditorTextBox");
            _lineNumbers = this.FindControl<TextBox>("LineNumbersTextBox");

            if (_mainEditor is null || _lineNumbers is null)
                return;

            // Initial setup
            UpdateLineNumbers();
            SyncFontProperties();

            _mainEditor.PropertyChanged += MainEditor_PropertyChanged;

            if (!_isSubscribed)
            {
                App.EventBus.Subscribe<SetCaretPositionRequestEvent>(OnSetCaretRequest);
                _isSubscribed = true;
            }

            Dispatcher.UIThread.Post(() =>
            {
                SetupScrollSynchronization();
                PublishCursorAndSelection();
                PublishBufferLexical(); // tokenize immediately after attach (file just opened or created)
            }, DispatcherPriority.Loaded);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_isSubscribed)
            {
                App.EventBus.Unsubscribe<SetCaretPositionRequestEvent>(OnSetCaretRequest);
                _isSubscribed = false;
            }
        }

        private void OnSetCaretRequest(SetCaretPositionRequestEvent e)
        {
            if (_mainEditor == null) return;
            if (DataContext is not ViewModels.DocumentViewModel doc || string.IsNullOrEmpty(doc.FilePath)) return;
            if (!string.Equals(doc.FilePath, e.FilePath, StringComparison.OrdinalIgnoreCase)) return;

            var length = _mainEditor.Text?.Length ?? 0;
            var caret = Math.Clamp(e.CaretIndex, 0, length);
            Dispatcher.UIThread.Post(() =>
            {
                _mainEditor.CaretIndex = caret;
                _mainEditor.Focus();
                PublishCursorAndSelection();
            });
        }

        private void SetupScrollSynchronization()
        {
            // Find the ScrollViewers inside the TextBoxes
            _mainScroll = _mainEditor?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            _lineNumbersScroll = _lineNumbers?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

            if (_mainScroll != null)
            {
                _mainScroll.ScrollChanged += MainScroll_ScrollChanged;
            }

            if (_lineNumbersScroll != null)
            {
                _lineNumbersScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                _lineNumbersScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
        }

        private void MainEditor_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextBox.TextProperty)
            {
                // Update the Text property binding
                if (_mainEditor != null && GetValue(TextProperty) != _mainEditor.Text)
                {
                    SetValue(TextProperty, _mainEditor.Text);
                }
                UpdateLineNumbers();
                PublishBufferLexical(); // tokenize on every text mutation
            }

            if (e.Property == TextBox.FontSizeProperty ||
                e.Property == TextBox.FontFamilyProperty ||
                e.Property == TextBox.FontStyleProperty ||
                e.Property == TextBox.FontWeightProperty ||
                e.Property == TextBox.FontStretchProperty ||
                e.Property == TextBox.PaddingProperty ||
                e.Property == TextBox.LineHeightProperty)
            {
                SyncFontProperties();
                UpdateLineNumbers();
            }

            if (e.Property == TextBox.CaretIndexProperty ||
                e.Property == TextBox.SelectionStartProperty ||
                e.Property == TextBox.SelectionEndProperty)
            {
                PublishCursorAndSelection();
            }
        }

        private void OnTextPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextProperty && _mainEditor != null)
            {
                var newText = GetValue(TextProperty);
                _mainEditor.Text = newText;
                UpdateLineNumbers();
                PublishCursorAndSelection();
                PublishBufferLexical(); // tokenize when external binding updates (e.g. file load)
            }
        }

        private void SyncFontProperties()
        {
            if (_lineNumbers == null || _mainEditor == null)
                return;

            _lineNumbers.FontSize = _mainEditor.FontSize;
            _lineNumbers.FontFamily = _mainEditor.FontFamily;
            _lineNumbers.FontStyle = _mainEditor.FontStyle;
            _lineNumbers.FontWeight = _mainEditor.FontWeight;
            _lineNumbers.FontStretch = _mainEditor.FontStretch;
            _lineNumbers.Padding = _mainEditor.Padding;
            _lineNumbers.LineHeight = _mainEditor.LineHeight;

            // Ensure same text rendering properties
            _lineNumbers.TextAlignment = TextAlignment.Right;
            _lineNumbers.TextWrapping = TextWrapping.NoWrap;
            _mainEditor.TextWrapping = TextWrapping.NoWrap;
        }

        private void UpdateLineNumbers()
        {
            if (_lineNumbers == null)
                return;

            var content = Text ?? _mainEditor?.Text ?? string.Empty;

            // Always ensure at least one line
            if (string.IsNullOrEmpty(content))
                content = " ";

            var lines = content.Split('\n');
            var sb = new StringBuilder(lines.Length * 5);

            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append((i + 1).ToString());
                if (i < lines.Length - 1)
                    sb.AppendLine();
            }

            var newText = sb.ToString();
            _lineNumbers.Text = newText;

            // After updating line numbers, sync scroll position
            SyncScrollPosition();
        }

        private void MainScroll_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll)
                return;

            SyncScrollPosition();
        }

        private void SyncScrollPosition()
        {
            if (_mainScroll == null || _lineNumbersScroll == null || _isSyncingScroll)
                return;

            _isSyncingScroll = true;
            try
            {
                // Sync vertical offset
                var targetOffset = new Vector(_lineNumbersScroll.Offset.X, _mainScroll.Offset.Y);

                // Only update if different to avoid infinite loops
                if (Math.Abs(_lineNumbersScroll.Offset.Y - _mainScroll.Offset.Y) > 0.01)
                {
                    _lineNumbersScroll.Offset = targetOffset;

                    // Force immediate update
                    _lineNumbersScroll.InvalidateArrange();
                    _lineNumbersScroll.InvalidateMeasure();
                }
            }
            finally
            {
                _isSyncingScroll = false;
            }
        }

        private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // This is called from the XAML binding
            if (!_isSyncingScroll)
            {
                SyncScrollPosition();
            }
        }

        private void PublishCursorAndSelection()
        {
            if (_mainEditor == null) return;
            var text = _mainEditor.Text ?? string.Empty;
            var caret = Math.Clamp(_mainEditor.CaretIndex, 0, text.Length);

            var (line, col) = GetLineAndColumn(text, caret);
            App.EventBus.Publish(new CursorPositionEvent(line, col));

            var selStart = Math.Clamp(_mainEditor.SelectionStart, 0, text.Length);
            var selEnd = Math.Clamp(_mainEditor.SelectionEnd, 0, text.Length);
            if (selEnd < selStart)
            {
                var tmp = selStart;
                selStart = selEnd;
                selEnd = tmp;
            }

            var charCount = selEnd - selStart;
            if (charCount > 0)
            {
                var (startLine, startCol) = GetLineAndColumn(text, selStart);
                var (endLine, endCol) = GetLineAndColumn(text, selEnd);
                var lineBreaks = CountLineBreaks(text, selStart, selEnd);
                App.EventBus.Publish(new SelectionInfoEvent(startLine, startCol, endLine, endCol, charCount, lineBreaks));
            }
            else
            {
                // Clear selection info
                App.EventBus.Publish(new SelectionInfoEvent(line, col, line, col, 0, 0));
            }
        }

        private static (int line, int col) GetLineAndColumn(string text, int index)
        {
            index = Math.Clamp(index, 0, text.Length);
            int line = 1;
            int lastNlPos = -1;
            for (int i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    lastNlPos = i;
                }
            }
            int col = index - lastNlPos;
            return (line, col);
        }

        private static int CountLineBreaks(string text, int start, int end)
        {
            int count = 0;
            for (int i = Math.Max(0, start); i < Math.Min(text.Length, end); i++)
            {
                if (text[i] == '\n') count++;
            }
            return count;
        }

        private void PublishBufferLexical()
        {
            if (_mainEditor == null) return;
            var text = _mainEditor.Text ?? string.Empty;
            if (text == _lastLexSnapshot) return; // avoid duplicate publish
            _lastLexSnapshot = text;
            string? filePath = null;
            if (DataContext is ViewModels.DocumentViewModel doc)
                filePath = doc.FilePath;
            App.EventBus.Publish(new TokenizeBufferRequestEvent(text, filePath));
        }
    }
}
