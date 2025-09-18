namespace skt.IDE.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

public partial class TextInputDialog : Window
{
    private TextBlock? _messageText;
    private TextBox? _inputBox;
    private Button? _okButton;
    private Button? _cancelButton;

    public TextInputDialog()
    {
        InitializeComponent();

        _messageText = this.FindControl<TextBlock>("MessageText");
        _inputBox = this.FindControl<TextBox>("InputBox");
        _okButton = this.FindControl<Button>("OkButton");
        _cancelButton = this.FindControl<Button>("CancelButton");

        if (_okButton is not null)
        {
            _okButton.Click += (_, __) => Close(_inputBox?.Text?.Trim());
        }
        if (_cancelButton is not null)
        {
            _cancelButton.Click += (_, __) => Close(null);
        }

        this.Opened += (_, __) =>
        {
            if (_inputBox is not null)
            {
                _inputBox.Focus();
                _inputBox.CaretIndex = _inputBox.Text?.Length ?? 0;
            }
        };

        if (_inputBox is not null)
        {
            _inputBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Close(_inputBox.Text?.Trim());
                }
            };
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void Configure(string title, string message, string defaultText)
    {
        Title = title;
        if (_messageText is null)
        {
            _messageText = this.FindControl<TextBlock>("MessageText");
        }
        if (_inputBox is null)
        {
            _inputBox = this.FindControl<TextBox>("InputBox");
        }
        if (_messageText is not null)
        {
            _messageText.Text = message;
        }
        if (_inputBox is not null)
        {
            _inputBox.Text = defaultText;
        }
    }

    public Task<string?> ShowDialogAsync(Window owner)
    {
        return ShowDialog<string?>(owner);
    }
}