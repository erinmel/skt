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
        // Note: this ThemeManager intentionally does NOT try to load per-theme icon resource files
        // (ToolIconResources.Dark.axaml / ToolIconResources.Light.axaml). Icons in this project use brushes
        // (DynamicResource) and are resolved from the single `ToolIconResources.axaml`. UI controls should
        // re-resolve icon resources when theme changes by listening to ThemeApplied (SktIcon already does this).

        // Event raised after a theme is applied and resources (including icon resources) are reloaded.
        // Subscribers should update any cached resources they hold (for example, SktIcon instances).
        public static event Action<AppThemeVariant>? ThemeApplied;

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

            // Reload icon resources: remove any previously-merged icon dictionaries so that consumers
            // re-resolve resources. Then ensure the single default icon resource dictionary is present.
            ReloadIconResources(app);

            // Notify subscribers that the theme was applied and resources reloaded.
            try
            {
                ThemeApplied?.Invoke(variant);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: error while invoking ThemeApplied handlers: {ex.Message}");
            }
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

        // Simplified icon reload: remove any merged icon dictionaries so controls re-resolve resources,
        // then ensure the single default icons resource dictionary is present. This intentionally does
        // not attempt to load per-theme icon files.
        private static void ReloadIconResources(Application app)
        {
            if (app.Resources == null) return;

            // Remove existing icon resource dictionaries so consumers will pick up the current resources.
            var existing = app.Resources.MergedDictionaries.OfType<ResourceDictionary>()
                .Where(rd => rd.ContainsKey("IsIconResource") && rd["IsIconResource"] is bool b && b)
                .ToList();
            foreach (var e in existing)
                app.Resources.MergedDictionaries.Remove(e);

            // Ensure default icons resource is present (App.axaml often already includes it).
            try
            {
                EnsureDefaultIconResourceLoaded(app);
            }
            catch
            {
                // Silent fail: icon resources may be part of the app package; avoid noisy logs at runtime.
            }
        }

        // Ensure there is at least one icon resource dictionary merged into app resources.
        private static void EnsureDefaultIconResourceLoaded(Application app)
        {
            // If there's already an icon resource present, nothing to do.
            if (app.Resources.MergedDictionaries.OfType<ResourceDictionary>().Any(rd => rd.ContainsKey("IsIconResource") && rd["IsIconResource"] is bool b && b))
                return;

            // If the app's top-level resources already contain icon keys (e.g. from App.axaml ResourceInclude), don't load again.
            if (app.Resources.ContainsKey("Icon.Skt"))
                return;

            var defaultUri = new Uri("avares://skt.IDE/Assets/Icons/ToolIconResources.axaml");
            var defaultDict = (ResourceDictionary)AvaloniaXamlLoader.Load(defaultUri);
            defaultDict["IsIconResource"] = true;
            app.Resources.MergedDictionaries.Add(defaultDict);
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
