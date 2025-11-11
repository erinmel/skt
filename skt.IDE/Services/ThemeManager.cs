using System;
using System.Linq;
using System.Diagnostics;
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

        // Define sensible min/max bounds for fonts. These can be tuned later or made configurable.
        private const double AppFontSizeMin = 10.0;
        private const double AppFontSizeMax = 22.0;
        private const double EditorFontSizeMin = 8.0;
        private const double EditorFontSizeMax = 28.0;

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
                Debug.WriteLine($"ThemeManager: failed to set theme tokens: {ex}");
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
                Debug.WriteLine($"ThemeManager: failed to reload syntax token resources: {ex}");
            }

            // Apply base Tok.* keys mapped to the active theme (Tok.<Name> -> Tok.<Name>.<Dark|Light>)
            try
            {
                ApplyActiveSyntaxTokenKeys(app, variant);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeManager: failed to apply active syntax token keys: {ex}");
            }

            // Notify subscribers that the theme was applied and resources reloaded.
            try
            {
                ThemeApplied?.Invoke(variant);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeManager: error while invoking ThemeApplied handlers: {ex}");
            }

            // As a fallback, ask TextEditor to update all registered instances (avoids visual-tree traversal).
            try
            {
                skt.IDE.Views.Editor.TextEditor.ApplyThemeToAll(variant);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeManager: failed to notify TextEditor instances: {ex}");
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
                    // mark the loaded theme resources so we can detect/refresh them later when font tokens change
                    try
                    {
                        themeRes["IsThemeResources"] = true;
                    }
                    catch
                    {
                        // ignore if the dictionary is read-only for some reason
                    }
                    app.Resources.MergedDictionaries.Add(themeRes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeManager: failed to load ThemeResources.axaml: {ex}");
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

            // Clamp incoming font sizes to sensible bounds so UI remains usable and responsive.
            var clampedAppFontSize = Math.Clamp(appFontSize, AppFontSizeMin, AppFontSizeMax);
            var clampedEditorFontSize = Math.Clamp(editorFontSize, EditorFontSizeMin, EditorFontSizeMax);

            app.Resources["AppFontSize"] = clampedAppFontSize;
            app.Resources["EditorFontSize"] = clampedEditorFontSize;

            if (!string.IsNullOrEmpty(appFontFamily))
                app.Resources["AppFontFamily"] = appFontFamily;

            if (!string.IsNullOrEmpty(editorFontFamily))
                app.Resources["EditorFontFamily"] = editorFontFamily;

            // Derived tokens for responsive design:
            // - Line height is typically 1.2x font size but can be adjusted; expose as explicit tokens.
            // - Small variants for use in denser UI (tabs, headers) computed as scale of main font.
            // - IconScaleDefault helps icons size relative to AppFontSize when UseAppFontSize is true
            try
            {
                double appLineHeight = Math.Round(clampedAppFontSize * 1.25, 2); // a bit more than 1.2 for legibility
                double editorLineHeight = Math.Round(clampedEditorFontSize * 1.4, 2); // editors usually need slightly larger line spacing
                double appFontSizeSmall = Math.Round(clampedAppFontSize * 0.9, 2);
                double editorFontSizeSmall = Math.Round(clampedEditorFontSize * 0.9, 2);
                double iconDefaultScale = 1.0; // icons will typically multiply AppFontSize by IconScale when UseAppFontSize is true

                app.Resources["AppLineHeight"] = appLineHeight;
                app.Resources["EditorLineHeight"] = editorLineHeight;
                app.Resources["AppFontSizeSmall"] = appFontSizeSmall;
                app.Resources["EditorFontSizeSmall"] = editorFontSizeSmall;
                app.Resources["IconDefaultScale"] = iconDefaultScale;

                // Derived layout tokens based on font size to keep UI proportions consistent.
                // These are conservative multipliers chosen to keep elements usable across font ranges.
                double toolbarHeight = Math.Round(clampedAppFontSize * 2.6); // e.g. 14 -> 36
                double tabHeaderHeight = Math.Round(clampedAppFontSize * 2.2); // e.g. 14 -> 31
                double tabItemHeight = Math.Round(clampedAppFontSize * 2.5); // e.g. 14 -> 35
                double tabMinWidth = Math.Round(clampedAppFontSize * 8.5); // e.g. 14 -> 119
                double tabMaxWidth = Math.Round(clampedAppFontSize * 14.0); // e.g. 14 -> 196
                double toolStripWidth = Math.Round(clampedAppFontSize * 3.2); // e.g. 14 -> 45
                double splitterThickness = Math.Max(6, Math.Round(clampedAppFontSize * 0.75)); // minimum 6
                double statusBarHeight = Math.Round(clampedAppFontSize * 1.8); // e.g. 14 -> 25

                // GridLength expected by RowDefinition/ColumnDefinition in XAML
                var glToolbar = new GridLength(toolbarHeight, GridUnitType.Pixel);
                var glTabHeader = new GridLength(tabHeaderHeight, GridUnitType.Pixel);
                app.Resources["ToolbarHeight"] = glToolbar;
                app.Resources["TabHeaderHeight"] = glTabHeader;
                app.Resources["TabItemHeight"] = tabItemHeight;
                app.Resources["TabMinWidth"] = tabMinWidth;
                app.Resources["TabMaxWidth"] = tabMaxWidth;
                var glToolStrip = new GridLength(toolStripWidth, GridUnitType.Pixel);
                var glStatusBar = new GridLength(statusBarHeight, GridUnitType.Pixel);
                app.Resources["ToolStripWidth"] = glToolStrip;
                app.Resources["ToolStripWidthValue"] = toolStripWidth; // Double for MinWidth/MaxWidth
                app.Resources["SplitterThickness"] = splitterThickness; // splitter thickness is a double property
                app.Resources["StatusBarHeight"] = glStatusBar;

                // Window/dialog defaults scaled slightly by font size
                app.Resources["WindowDefaultWidth"] = Math.Max(800, Math.Round(clampedAppFontSize * 85));
                app.Resources["WindowDefaultHeight"] = Math.Max(600, Math.Round(clampedAppFontSize * 57));
                app.Resources["DialogDefaultWidth"] = Math.Round(clampedAppFontSize * 30);
                app.Resources["DialogDefaultHeight"] = Math.Round(clampedAppFontSize * 14);

                // Spacing and padding derived from font size for consistent responsive design
                double paddingH = Math.Max(6, Math.Round(clampedAppFontSize * 0.7));
                double paddingV = Math.Max(3, Math.Round(clampedAppFontSize * 0.35));
                double paddingCompactH = Math.Max(4, Math.Round(clampedAppFontSize * 0.45));
                double paddingCompactV = Math.Max(2, Math.Round(clampedAppFontSize * 0.28));
                double controlPaddingH = Math.Max(6, Math.Round(clampedAppFontSize * 0.57));
                double controlPaddingV = Math.Max(3, Math.Round(clampedAppFontSize * 0.28));
                double controlPaddingSmallH = Math.Max(3, Math.Round(clampedAppFontSize * 0.28));
                double controlPaddingSmallV = Math.Max(1, Math.Round(clampedAppFontSize * 0.14));

                app.Resources["ButtonPadding"] = new Thickness(paddingH, paddingV);
                app.Resources["ButtonPaddingCompact"] = new Thickness(paddingCompactH, paddingCompactV);
                app.Resources["ControlPadding"] = new Thickness(controlPaddingH, controlPaddingV);
                app.Resources["ControlPaddingSmall"] = new Thickness(controlPaddingSmallH, controlPaddingSmallV);

                // Icon sizes scaled from app font size
                app.Resources["IconSizeToolbar"] = Math.Round(clampedAppFontSize * 1.4); // e.g. 14 -> 20
                app.Resources["IconSizeMenu"] = Math.Round(clampedAppFontSize * 1.15); // e.g. 14 -> 16
                app.Resources["IconSizeLogo"] = Math.Round(clampedAppFontSize * 2.1); // e.g. 14 -> 29
                app.Resources["SplitterVisualLineThickness"] = Math.Max(1, Math.Round(clampedAppFontSize * 0.14));

                // Also expose min/max so consumers (or settings UI) can show limits.
                app.Resources["AppFontSizeMin"] = AppFontSizeMin;
                app.Resources["AppFontSizeMax"] = AppFontSizeMax;
                app.Resources["EditorFontSizeMin"] = EditorFontSizeMin;
                app.Resources["EditorFontSizeMax"] = EditorFontSizeMax;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeManager: failed to refresh theme/icon resources after font update: {ex}");
            }

            // Notify subscribers that font tokens changed so controls that size based on AppFontSize can update.
            try
            {
                var current = app.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? AppThemeVariant.Dark : AppThemeVariant.Light;
                ThemeApplied?.Invoke(current);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeManager: error while invoking ThemeApplied after UpdateFontTokens: {ex}");
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
                Debug.WriteLine($"ThemeManager: failed to load SyntaxColors.axaml: {ex}");
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
