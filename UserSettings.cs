using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRT
{
    // ###########################################################################################
    // Persisted user preferences model. Defaults to enabled for all online features.
    // ###########################################################################################
    internal sealed class UserSettingsData
    {
        [JsonPropertyName("checkVersionOnLaunch")] public bool CheckVersionOnLaunch { get; set; } = true;
        [JsonPropertyName("checkDataOnLaunch")] public bool CheckDataOnLaunch { get; set; } = true;
        [JsonPropertyName("leftPanelWidth")] public double LeftPanelWidth { get; set; } = 200.0;
        [JsonPropertyName("schematicsSplitterRatios")] public Dictionary<string, double> SchematicsSplitterRatios { get; set; } = new();
        [JsonPropertyName("selectedCategoriesByBoard")] public Dictionary<string, List<string>> SelectedCategoriesByBoard { get; set; } = new();

        [JsonPropertyName("hasWindowPlacement")] public bool HasWindowPlacement { get; set; } = false;
        [JsonPropertyName("windowState")] public string WindowState { get; set; } = "Normal";
        [JsonPropertyName("windowWidth")] public double WindowWidth { get; set; } = 1024.0;
        [JsonPropertyName("windowHeight")] public double WindowHeight { get; set; } = 768.0;
        [JsonPropertyName("windowX")] public int WindowX { get; set; } = 0;
        [JsonPropertyName("windowY")] public int WindowY { get; set; } = 0;
        [JsonPropertyName("windowScreenX")] public int WindowScreenX { get; set; } = 0;
        [JsonPropertyName("windowScreenY")] public int WindowScreenY { get; set; } = 0;
        [JsonPropertyName("windowScreenWidth")] public int WindowScreenWidth { get; set; } = 1920;
        [JsonPropertyName("windowScreenHeight")] public int WindowScreenHeight { get; set; } = 1080;
        [JsonPropertyName("windowScreenScaling")] public double WindowScreenScaling { get; set; } = 1.0;
    }

    // ###########################################################################################
    // Loads and saves user preferences to a JSON file in the same folder as the log file.
    // Call Load() once at startup before any settings are read.
    // ###########################################################################################
    public static class UserSettings
    {
        private static UserSettingsData _data = new();
        private static string _settingsFilePath = string.Empty;

        public static bool CheckVersionOnLaunch
        {
            get => _data.CheckVersionOnLaunch;
            set
            {
                _data.CheckVersionOnLaunch = value;
                Logger.Info($"Setting changed - [CheckVersionOnLaunch] [{value}]");
                Save();
            }
        }

        public static bool CheckDataOnLaunch
        {
            get => _data.CheckDataOnLaunch;
            set
            {
                _data.CheckDataOnLaunch = value;
                Logger.Info($"Setting changed - [CheckDataOnLaunch] [{value}]");
                Save();
            }
        }

        public static double LeftPanelWidth
        {
            get => _data.LeftPanelWidth;
            set
            {
                _data.LeftPanelWidth = value;
                Logger.Info($"Setting changed - [LeftPanelWidth] [{value:F1}]");
                Save();
            }
        }

        // Window placement — read-only; written atomically via SaveWindowPlacement
        public static bool HasWindowPlacement => _data.HasWindowPlacement;
        public static string WindowState => _data.WindowState;
        public static double WindowWidth => _data.WindowWidth;
        public static double WindowHeight => _data.WindowHeight;
        public static int WindowX => _data.WindowX;
        public static int WindowY => _data.WindowY;
        public static int WindowScreenX => _data.WindowScreenX;
        public static int WindowScreenY => _data.WindowScreenY;
        public static int WindowScreenWidth => _data.WindowScreenWidth;
        public static int WindowScreenHeight => _data.WindowScreenHeight;
        public static double WindowScreenScaling => _data.WindowScreenScaling;

        // ###########################################################################################
        // Returns the saved schematics splitter ratio for the given board key.
        // Defaults to 0.5 (equal split) when no saved value exists.
        // ###########################################################################################
        public static double GetSchematicsSplitterRatio(string boardKey)
            => _data.SchematicsSplitterRatios.TryGetValue(boardKey, out var ratio) ? ratio : 0.5;

        // ###########################################################################################
        // Persists the schematics splitter ratio for the given board key.
        // ###########################################################################################
        public static void SetSchematicsSplitterRatio(string boardKey, double ratio)
        {
            _data.SchematicsSplitterRatios[boardKey] = ratio;
            Logger.Info($"Setting changed - [SchematicsSplitterRatio] [{boardKey}] [{ratio:F3}]");
            Save();
        }

        // ###########################################################################################
        // Returns the saved selected categories for the given board key.
        // Returns null when no selection has been saved yet (caller should default to all selected).
        // ###########################################################################################
        public static List<string>? GetSelectedCategories(string boardKey)
            => _data.SelectedCategoriesByBoard.TryGetValue(boardKey, out var categories) ? categories : null;

        // ###########################################################################################
        // Persists the selected category list for the given board key.
        // ###########################################################################################
        public static void SetSelectedCategories(string boardKey, List<string> categories)
        {
            _data.SelectedCategoriesByBoard[boardKey] = categories;
            Logger.Info($"Setting changed - [SelectedCategories] [{boardKey}] [{categories.Count} selected]");
            Save();
        }

        // ###########################################################################################
        // Saves all window placement values atomically in a single disk write.
        // ###########################################################################################
        public static void SaveWindowPlacement(string state, double width, double height, int x, int y, int screenX, int screenY, int screenWidth, int screenHeight, double screenScaling)
        {
            _data.HasWindowPlacement = true;
            _data.WindowState = state;
            _data.WindowWidth = width;
            _data.WindowHeight = height;
            _data.WindowX = x;
            _data.WindowY = y;
            _data.WindowScreenX = screenX;
            _data.WindowScreenY = screenY;
            _data.WindowScreenWidth = screenWidth;
            _data.WindowScreenHeight = screenHeight;
            _data.WindowScreenScaling = screenScaling;
            Logger.Info($"Setting changed - [WindowPlacement] [{state}] [{width:F0}x{height:F0}] [Pos: {x},{y}] [Screen: {screenX},{screenY} {screenWidth}x{screenHeight} @{screenScaling:F2}x]");
            Save();
        }

        // ###########################################################################################
        // Resolves the settings file path and loads persisted values.
        // Falls back to defaults silently on any failure.
        // ###########################################################################################
        public static void Load()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var directory = Path.Combine(appData, AppConfig.AppFolderName);
                Directory.CreateDirectory(directory);
                _settingsFilePath = Path.Combine(directory, AppConfig.SettingsFileName);

                if (!File.Exists(_settingsFilePath))
                {
                    Logger.Info("Settings file not found - using defaults");
                    return;
                }

                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<UserSettingsData>(json);
                if (loaded != null)
                {
                    _data = loaded;
                    Logger.Info($"Settings loaded - [CheckVersionOnLaunch] [{_data.CheckVersionOnLaunch}] [CheckDataOnLaunch] [{_data.CheckDataOnLaunch}] [LeftPanelWidth] [{_data.LeftPanelWidth:F1}] [SchematicsSplitterRatios] [{_data.SchematicsSplitterRatios.Count} entries] [SelectedCategoriesByBoard] [{_data.SelectedCategoriesByBoard.Count} entries] [WindowPlacement] [{_data.WindowState}] [{_data.WindowWidth:F0}x{_data.WindowHeight:F0}]");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load settings - [{ex.Message}] - using defaults");
            }
        }

        // ###########################################################################################
        // Serializes current settings and writes them to the JSON file.
        // ###########################################################################################
        private static void Save()
        {
            if (string.IsNullOrEmpty(_settingsFilePath))
                return;

            try
            {
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to save settings - [{ex.Message}]");
            }
        }
    }
}