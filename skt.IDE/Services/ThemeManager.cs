using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using Avalonia.Media;

namespace skt.IDE.Services
{
    public enum AppThemeVariant
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        public static void ApplyTheme(AppThemeVariant variant)
        {
            var app = Application.Current;
            if (app == null) return;

            app.RequestedThemeVariant = variant == AppThemeVariant.Dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;

            EnsureThemeResourcesLoaded(app);

            // Ensure high/low foreground tokens reflect the chosen theme (dark => white text)
            try
            {
                if (variant == AppThemeVariant.Dark)
                {
                    app.Resources["AppForegroundHighBrush"] = Brushes.White;
                    app.Resources["AppForegroundLowBrush"] = Brushes.LightGray;
                    app.Resources["AppBackgroundBrush"] = Brushes.Transparent; // let theme/background determine real background
                }
                else
                {
                    app.Resources["AppForegroundHighBrush"] = Brushes.Black;
                    app.Resources["AppForegroundLowBrush"] = Brushes.Gray;
                    app.Resources["AppBackgroundBrush"] = Brushes.Transparent;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: failed to set theme tokens: {ex.Message}");
            }

            ReloadIconResources(app, variant);
        }

        private static void EnsureThemeResourcesLoaded(Application app)
        {
            if (app.Resources == null) return;

            // If AppFontSize is not present in the top-level resources, load the ThemeResources dictionary.
            try
            {
                if (!app.Resources.TryGetValue("AppFontSize", out _))
                {
                    var themeRes = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://skt.IDE/Assets/Styles/ThemeResources.axaml"));
                    app.Resources.MergedDictionaries.Add(themeRes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: failed to load ThemeResources.axaml: {ex.Message}");
            }
        }

        private static void ReloadIconResources(Application app, AppThemeVariant variant)
        {
            if (app.Resources == null) return;

            var fileName = variant == AppThemeVariant.Dark ? "ToolIconResources.Dark.axaml" : "ToolIconResources.Light.axaml";
            var uri = new Uri($"avares://skt.IDE/Assets/Icons/{fileName}");
            try
            {
                var dict = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
                dict["IsIconResource"] = true;
                app.Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: themed icons not found ({fileName}): {ex.Message}");
                try
                {
                    var defaultUri = new Uri("avares://skt.IDE/Assets/Icons/ToolIconResources.axaml");
                    var defaultDict = (ResourceDictionary)AvaloniaXamlLoader.Load(defaultUri);
                    defaultDict["IsIconResource"] = true;
                    app.Resources.MergedDictionaries.Add(defaultDict);
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"ThemeManager: failed to load default icons: {ex2.Message}");
                }
            }
        }

        public static void UpdateFontTokens(double appFontSize, double editorFontSize, string? appFontFamily = null, string? editorFontFamily = null)
        {
            var app = Application.Current;
            if (app == null) return;

            app.Resources["AppFontSize"] = appFontSize;
            app.Resources["EditorFontSize"] = editorFontSize;

            if (!string.IsNullOrEmpty(appFontFamily))
                app.Resources["AppFontFamily"] = appFontFamily;

            if (!string.IsNullOrEmpty(editorFontFamily))
                app.Resources["EditorFontFamily"] = editorFontFamily;
        }

        public static void ToggleTheme()
        {
            var app = Application.Current;
            if (app == null) return;
            var current = app.RequestedThemeVariant;
            ApplyTheme(current == Avalonia.Styling.ThemeVariant.Dark ? AppThemeVariant.Light : AppThemeVariant.Dark);
        }
    }
}
