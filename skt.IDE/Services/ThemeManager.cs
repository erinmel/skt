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
        public static event Action<AppThemeVariant>? ThemeApplied;

        public static void ApplyTheme(AppThemeVariant variant)
        {
            var app = Application.Current;
            if (app == null) return;

            app.RequestedThemeVariant = variant == AppThemeVariant.Dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;

            EnsureThemeResourcesLoaded(app);

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

            // Reload icon resources: remove any previously-merged icon dictionaries so consumers
            // re-resolve resources. Then ensure the single default icon resource dictionary is present.
            ReloadIconResources(app);

            // Reload syntax token colors so Tok.*.Dark and Tok.*.Light entries are reloaded
            // (this allows editing or replacing SyntaxColors.axaml at runtime to take effect).
            try
            {
                ReloadSyntaxTokenResources(app);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: failed to reload syntax token resources: {ex.Message}");
            }

            // Apply base Tok.* keys mapped to the active theme (Tok.<Name> -> Tok.<Name>.<Dark|Light>)
            try
            {
                ApplyActiveSyntaxTokenKeys(app, variant);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: failed to apply active syntax token keys: {ex.Message}");
            }

            // Notify subscribers that the theme was applied and resources reloaded.
            try
            {
                ThemeApplied?.Invoke(variant);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: error while invoking ThemeApplied handlers: {ex.Message}");
            }

            // As a fallback, ask TextEditor to update all registered instances (avoids visual-tree traversal).
            try
            {
                skt.IDE.Views.Editor.TextEditor.ApplyThemeToAll(variant);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: failed to notify TextEditor instances: {ex.Message}");
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

            // Notify subscribers that font tokens changed so controls that size based on AppFontSize can update.
            try
            {
                var current = app.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? AppThemeVariant.Dark : AppThemeVariant.Light;
                ThemeApplied?.Invoke(current);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: error while invoking ThemeApplied after UpdateFontTokens: {ex.Message}");
            }
        }

        public static void ToggleTheme()
        {
            var app = Application.Current;
            if (app == null) return;
            var current = app.RequestedThemeVariant;
            ApplyTheme(current == Avalonia.Styling.ThemeVariant.Dark ? AppThemeVariant.Light : AppThemeVariant.Dark);
        }

        // Reload the syntax color tokens resource dictionary so brushes are freshly reloaded
        // (useful when changing theme or editing the axaml file on disk).
        private static void ReloadSyntaxTokenResources(Application app)
        {
            if (app.Resources == null) return;

            // Remove any existing merged dictionaries that look like the syntax colors dictionary
            var existing = app.Resources.MergedDictionaries.OfType<ResourceDictionary>()
                .Where(rd => rd.ContainsKey("Tok.Integer.Dark") || rd.ContainsKey("Tok.Integer.Light") || (rd.ContainsKey("IsSyntaxColors") && rd["IsSyntaxColors"] is bool b && b))
                .ToList();
            foreach (var e in existing)
                app.Resources.MergedDictionaries.Remove(e);

            // Load the syntax colors resource dictionary anew
            try
            {
                var syntaxUri = new Uri("avares://skt.IDE/Assets/Colors/SyntaxColors.axaml");
                var syntaxDict = (ResourceDictionary)AvaloniaXamlLoader.Load(syntaxUri);
                // mark it so future reloads can detect it
                syntaxDict["IsSyntaxColors"] = true;
                app.Resources.MergedDictionaries.Add(syntaxDict);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ThemeManager: failed to load SyntaxColors.axaml: {ex.Message}");
            }
        }

        // After reloading the syntax token dictionaries, set base Tok.<Name> resource keys to
        // point to the currently active themed brushes (Tok.<Name> -> Tok.<Name>.<Dark|Light>).
        private static void ApplyActiveSyntaxTokenKeys(Application app, AppThemeVariant variant)
        {
            if (app.Resources == null) return;
            string suffix = variant == AppThemeVariant.Dark ? "Dark" : "Light";

            var tokenNames = new[] { "Integer", "Real", "Boolean", "String", "Identifier", "Reserved", "Comment", "Operator", "Symbol", "Error" };
            foreach (var name in tokenNames)
            {
                var themedKey = $"Tok.{name}.{suffix}";
                var baseKey = $"Tok.{name}";
                
                // Use TryFindResource to search in merged dictionaries
                if (app.TryFindResource(themedKey, out var value))
                {
                    app.Resources[baseKey] = value;
                }
                else
                {
                    // If no themed key exists, remove any base mapping so GetResource falls back to other mechanisms.
                    if (app.Resources.ContainsKey(baseKey))
                    {
                        app.Resources.Remove(baseKey);
                    }
                }
            }
        }
    }
}
