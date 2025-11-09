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
            // Load saved settings
            var settings = SettingsManager.Load();
            
            // Set theme combo
            ThemeCombo.SelectedIndex = settings.Theme == "Dark" ? 0 : 1;
            
            // Set font sizes
            AppFontSizeBox.Text = settings.AppFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EditorFontSizeBox.Text = settings.EditorFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void OnApplyThemeClicked(object? sender, RoutedEventArgs e)
        {
            var item = ThemeCombo.SelectedItem as ComboBoxItem;
            var theme = item?.Content?.ToString() == "Dark" ? "Dark" : "Light";
            
            if (theme == "Dark")
                ThemeManager.ApplyTheme(AppThemeVariant.Dark);
            else
                ThemeManager.ApplyTheme(AppThemeVariant.Light);

            // Save theme preference
            var settings = SettingsManager.Load();
            settings.Theme = theme;
            SettingsManager.Save(settings);
        }

        private void OnOkClicked(object? sender, RoutedEventArgs e)
        {
            if (!double.TryParse(AppFontSizeBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var appSize))
                appSize = 14.0;
            if (!double.TryParse(EditorFontSizeBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var editorSize))
                editorSize = 14.0;

            ThemeManager.UpdateFontTokens(appSize, editorSize);

            // Save all settings
            var item = ThemeCombo.SelectedItem as ComboBoxItem;
            var theme = item?.Content?.ToString() == "Dark" ? "Dark" : "Light";
            
            var settings = new UserSettings
            {
                Theme = theme,
                AppFontSize = appSize,
                EditorFontSize = editorSize
            };
            SettingsManager.Save(settings);

            OnApplyThemeClicked(sender, e);

            Close(true);
        }

        // Provide an async ShowAsync wrapper so callers can await the dialog like other dialogs in the project
        public Task<bool?> ShowAsync(Window owner) => ShowDialog<bool?>(owner);
    }
}
