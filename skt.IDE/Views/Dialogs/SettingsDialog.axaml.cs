using System;
using System.Linq;
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

            // Populate font size ComboBoxes
            PopulateFontSizeComboBoxes();

            ApplyThemeButton.Click += OnApplyThemeClicked;
            OkButton.Click += OnOkClicked;
            CancelButton.Click += (_, _) => Close(false);
        }

        private void PopulateFontSizeComboBoxes()
        {
            // App font sizes: 10-24 (reasonable range for UI)
            for (int i = 10; i <= 24; i++)
            {
                AppFontSizeCombo.Items.Add(new ComboBoxItem { Content = i.ToString() });
            }

            // Editor font sizes: 8-28 (wider range for code editing)
            for (int i = 8; i <= 28; i++)
            {
                EditorFontSizeCombo.Items.Add(new ComboBoxItem { Content = i.ToString() });
            }
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            // Load saved settings
            var settings = SettingsManager.Load();
            
            // Set theme combo
            ThemeCombo.SelectedIndex = settings.Theme == "Dark" ? 0 : 1;
            
            // Set font sizes - find matching items
            var appFontItem = AppFontSizeCombo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Content?.ToString() == ((int)settings.AppFontSize).ToString());
            if (appFontItem != null)
                AppFontSizeCombo.SelectedItem = appFontItem;
            else
                AppFontSizeCombo.SelectedIndex = 4; // Default to 14

            var editorFontItem = EditorFontSizeCombo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Content?.ToString() == ((int)settings.EditorFontSize).ToString());
            if (editorFontItem != null)
                EditorFontSizeCombo.SelectedItem = editorFontItem;
            else
                EditorFontSizeCombo.SelectedIndex = 6; // Default to 14 (8,9,10,11,12,13,14)
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
            // Get font sizes from ComboBoxes
            var appFontItem = AppFontSizeCombo.SelectedItem as ComboBoxItem;
            var editorFontItem = EditorFontSizeCombo.SelectedItem as ComboBoxItem;
            
            double appSize = 14.0;
            double editorSize = 14.0;

            if (appFontItem?.Content != null && double.TryParse(appFontItem.Content.ToString(), out var parsedAppSize))
                appSize = parsedAppSize;

            if (editorFontItem?.Content != null && double.TryParse(editorFontItem.Content.ToString(), out var parsedEditorSize))
                editorSize = parsedEditorSize;

            // Apply font tokens immediately
            ThemeManager.UpdateFontTokens(appSize, editorSize);

            // Save all settings
            var themeItem = ThemeCombo.SelectedItem as ComboBoxItem;
            var theme = themeItem?.Content?.ToString() == "Dark" ? "Dark" : "Light";
            
            var settings = new UserSettings
            {
                Theme = theme,
                AppFontSize = appSize,
                EditorFontSize = editorSize
            };
            SettingsManager.Save(settings);


            Close(true);
        }

        // Provide an async ShowAsync wrapper so callers can await the dialog like other dialogs in the project
        public Task<bool?> ShowAsync(Window owner) => ShowDialog<bool?>(owner);
    }
}
