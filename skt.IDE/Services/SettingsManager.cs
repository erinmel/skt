using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace skt.IDE.Services
{
    /// <summary>
    /// Stores user preferences such as theme, font sizes, etc.
    /// </summary>
    public class UserSettings
    {
        public string Theme { get; set; } = "Dark";
        public double AppFontSize { get; set; } = 14.0;
        public double EditorFontSize { get; set; } = 14.0;
    }

    /// <summary>
    /// Manages loading and saving user settings to a JSON file.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SktIDE",
            "settings.json"
        );

        /// <summary>
        /// Loads user settings from disk. Returns default settings if file doesn't exist.
        /// </summary>
        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    if (settings != null)
                    {
                        // Removed verbose startup logging to keep console quiet.
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SettingsManager: Error loading settings: {ex}");
            }

            // Return defaults if load failed or file doesn't exist
            return new UserSettings();
        }

        /// <summary>
        /// Saves user settings to disk.
        /// </summary>
        public static void Save(UserSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(SettingsPath, json);
                // Removed noisy confirmation log
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SettingsManager: Error saving settings: {ex}");
            }
        }

        /// <summary>
        /// Applies the loaded settings to the application theme and fonts.
        /// </summary>
        public static void ApplySettings(UserSettings settings)
        {
            // Apply theme
            var variant = settings.Theme == "Dark" ? AppThemeVariant.Dark : AppThemeVariant.Light;
            ThemeManager.ApplyTheme(variant);

            // Apply font sizes
            ThemeManager.UpdateFontTokens(settings.AppFontSize, settings.EditorFontSize);
        }
    }
}
