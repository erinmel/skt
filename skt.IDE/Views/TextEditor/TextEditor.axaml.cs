using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System.Linq;
using Avalonia.Media.TextFormatting;
using System.Text;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

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

        public TextEditor()
        {
            InitializeComponent();
            AttachedToVisualTree += OnAttached;
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

            // We need to wait for the template to be applied to get the ScrollViewers
            Dispatcher.UIThread.Post(() =>
            {
                SetupScrollSynchronization();
            }, DispatcherPriority.Loaded);
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
        }

        private void OnTextPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextProperty && _mainEditor != null)
            {
                var newText = GetValue(TextProperty);
                if (_mainEditor.Text != newText)
                {
                    _mainEditor.Text = newText;
                }
                UpdateLineNumbers();
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
            if (_lineNumbers.Text != newText)
            {
                _lineNumbers.Text = newText;
            }

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
    }
}