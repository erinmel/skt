using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using skt.IDE.Services;
using System.Threading.Tasks;

namespace skt.IDE.Views.Dialogs
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
            this.Opened += OnOpened;

            ApplyThemeButton.Click += OnApplyThemeClicked;
            OkButton.Click += OnOkClicked;
            CancelButton.Click += (_, _) => Close(false);
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            if (Application.Current?.Resources is { } res)
            {
                if (res.TryGetValue("AppFontSize", out var appFs))
                {
                    var text = appFs switch
                    {
                        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        _ => "12"
                    };
                    AppFontSizeBox.Text = text;
                }

                if (res.TryGetValue("EditorFontSize", out var edFs))
                {
                    var text = edFs switch
                    {
                        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        _ => "12"
                    };
                    EditorFontSizeBox.Text = text;
                }

                var cur = Application.Current.RequestedThemeVariant;
                ThemeCombo.SelectedIndex = cur == Avalonia.Styling.ThemeVariant.Dark ? 0 : 1;
            }
        }

        private void OnApplyThemeClicked(object? sender, RoutedEventArgs e)
        {
            var item = ThemeCombo.SelectedItem as ComboBoxItem;
            if (item?.Content?.ToString() == "Dark")
                ThemeManager.ApplyTheme(AppThemeVariant.Dark);
            else
                ThemeManager.ApplyTheme(AppThemeVariant.Light);
        }

        private void OnOkClicked(object? sender, RoutedEventArgs e)
        {
            if (!double.TryParse(AppFontSizeBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var appSize))
                appSize = 12.0;
            if (!double.TryParse(EditorFontSizeBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var editorSize))
                editorSize = 12.0;

            ThemeManager.UpdateFontTokens(appSize, editorSize);

            OnApplyThemeClicked(sender, e);

            Close(true);
        }

        // Provide an async ShowAsync wrapper so callers can await the dialog like other dialogs in the project
        public Task<bool?> ShowAsync(Window owner) => ShowDialog<bool?>(owner);
    }
}
