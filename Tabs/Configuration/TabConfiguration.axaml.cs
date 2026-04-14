using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Handlers.DataHandling;
using System;
using System.Diagnostics;
using System.IO;

namespace CRT
{
    public partial class TabConfiguration : UserControl
    {
        private bool thisSuppressCheckDataOnLaunchChanged;

        public TabConfiguration()
        {
            this.InitializeComponent();

            this.ThemeVariantComboBox.SelectedIndex = UserSettings.ThemeVariant switch
            {
                "Dark" => 1,
                "UserPreference" => 2,
                _ => 0
            };

            this.MultipleInstancesForComponentPopupToggleSwitch.IsChecked = UserSettings.MultipleInstancesForComponentPopup;

            bool isInteractiveCadTraceHoverHoldShiftMode =
                string.Equals(UserSettings.InteractiveCadTraceHoverMode, "HoldShift", StringComparison.Ordinal);

            this.InteractiveCadTraceHoverAlwaysRadioButton.IsChecked = !isInteractiveCadTraceHoverHoldShiftMode;
            this.InteractiveCadTraceHoverHoldShiftRadioButton.IsChecked = isInteractiveCadTraceHoverHoldShiftMode;

            // Initialize configuration checkboxes — subscribe after setting initial values
            // to avoid triggering redundant saves during startup
            this.CheckVersionOnLaunchCheckBox.IsChecked = UserSettings.CheckVersionOnLaunch;
            this.CheckDataOnLaunchCheckBox.IsChecked = UserSettings.CheckDataOnLaunch;
            this.ShowDevelopmentVersionNotificationCheckBox.IsChecked = UserSettings.ShowDevelopmentVersionNotification;
            this.ValidateDataOnLaunchCheckBox.IsChecked = UserSettings.ValidateDataOnLaunch;
            this.DebugLoggingCheckBox.IsChecked = UserSettings.DebugLogging;

            this.ThemeVariantComboBox.SelectionChanged += this.OnThemeVariantSelectionChanged;
            this.MultipleInstancesForComponentPopupToggleSwitch.IsCheckedChanged += this.OnMultipleInstancesForComponentPopupChanged;
            this.InteractiveCadTraceHoverAlwaysRadioButton.IsCheckedChanged += this.OnInteractiveCadTraceHoverModeChanged;
            this.InteractiveCadTraceHoverHoldShiftRadioButton.IsCheckedChanged += this.OnInteractiveCadTraceHoverModeChanged;
            this.CheckVersionOnLaunchCheckBox.IsCheckedChanged += this.OnCheckVersionOnLaunchChanged;
            this.CheckDataOnLaunchCheckBox.IsCheckedChanged += this.OnCheckDataOnLaunchChanged;
            this.ShowDevelopmentVersionNotificationCheckBox.IsCheckedChanged += this.OnShowDevelopmentVersionNotificationChanged;
            this.ValidateDataOnLaunchCheckBox.IsCheckedChanged += this.OnValidateDataOnLaunchChanged;
            this.DebugLoggingCheckBox.IsCheckedChanged += this.OnDebugLoggingChanged;
        }

        // ###########################################################################################
        // Persists the global interactive CAD trace hover mode selected in configuration.
        // ###########################################################################################
        private void OnInteractiveCadTraceHoverModeChanged(object? sender, RoutedEventArgs e)
        {
            if (this.InteractiveCadTraceHoverAlwaysRadioButton.IsChecked == true)
            {
                UserSettings.InteractiveCadTraceHoverMode = "Always";
                return;
            }

            if (this.InteractiveCadTraceHoverHoldShiftRadioButton.IsChecked == true)
            {
                UserSettings.InteractiveCadTraceHoverMode = "HoldShift";
            }
        }

        // ###########################################################################################
        // Applies and persists the selected application theme from the drop-down list.
        // ###########################################################################################
        private void OnThemeVariantSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var themeVariant = this.ThemeVariantComboBox.SelectedIndex switch
            {
                1 => "Dark",
                2 => "UserPreference",
                _ => "Light"
            };

            UserSettings.ThemeVariant = themeVariant;

            if (Application.Current is App app)
            {
                app.ApplyConfiguredTheme();
            }
        }

        // ###########################################################################################
        // Persists the "Multiple instances for component popup" preference when the toggle is changed.
        // ###########################################################################################
        private void OnMultipleInstancesForComponentPopupChanged(object? sender, RoutedEventArgs e)
        {
            UserSettings.MultipleInstancesForComponentPopup = this.MultipleInstancesForComponentPopupToggleSwitch.IsChecked == true;
        }

        // ###########################################################################################
        // Persists the "Check for new version at launch" preference when the checkbox is toggled.
        // ###########################################################################################
        private void OnCheckVersionOnLaunchChanged(object? sender, RoutedEventArgs e)
        {
            UserSettings.CheckVersionOnLaunch = this.CheckVersionOnLaunchCheckBox.IsChecked == true;
        }

        // ###########################################################################################
        // Persists the "Check for new or updated data at launch" preference when the checkbox is toggled.
        // ###########################################################################################
        private void OnCheckDataOnLaunchChanged(object? sender, RoutedEventArgs e)
        {
            if (this.thisSuppressCheckDataOnLaunchChanged)
            {
                return;
            }

            UserSettings.CheckDataOnLaunch = this.CheckDataOnLaunchCheckBox.IsChecked == true;
        }

        // ###########################################################################################
        // Updates the "Check for new or updated data at launch" checkbox from outside this tab
        // without triggering its persistence handler a second time.
        // ###########################################################################################
        public void SetCheckDataOnLaunchCheckBoxValue(bool isChecked)
        {
            this.thisSuppressCheckDataOnLaunchChanged = true;
            this.CheckDataOnLaunchCheckBox.IsChecked = isChecked;
            this.thisSuppressCheckDataOnLaunchChanged = false;
        }

        // ###########################################################################################
        // Persists the "Show notification for DEVELOPMENT versions" preference when the checkbox is toggled.
        // ###########################################################################################
        private void OnShowDevelopmentVersionNotificationChanged(object? sender, RoutedEventArgs e)
        {
            UserSettings.ShowDevelopmentVersionNotification = this.ShowDevelopmentVersionNotificationCheckBox.IsChecked == true;
        }

        // ###########################################################################################
        // Persists the "Validate data at application launch" preference when the checkbox is toggled.
        // ###########################################################################################
        private void OnValidateDataOnLaunchChanged(object? sender, RoutedEventArgs e)
        {
            UserSettings.ValidateDataOnLaunch = this.ValidateDataOnLaunchCheckBox.IsChecked == true;
        }

        // ###########################################################################################
        // Persists the "Enable debug logging" preference when the checkbox is toggled.
        // ###########################################################################################
        private void OnDebugLoggingChanged(object? sender, RoutedEventArgs e)
        {
            UserSettings.DebugLogging = this.DebugLoggingCheckBox.IsChecked == true;
        }

        // ###########################################################################################
        // Opens the persistent AppData folder that contains the log and settings files.
        // ###########################################################################################
        private void OnOpenAppDataFolderClick(object? sender, RoutedEventArgs e)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var directory = Path.Combine(appData, AppConfig.AppFolderName);

            try
            {
                Directory.CreateDirectory(directory);

                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"")
                    {
                        UseShellExecute = true
                    });
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", directory);
                }
                else
                {
                    Process.Start("xdg-open", directory);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to open app data folder - [{directory}] - [{ex.Message}]");
            }
        }

        // ###########################################################################################
        // Reloads user preference colors from the settings file and reapplies the current theme.
        // ###########################################################################################
        private void OnReloadUserPreferenceThemeClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current is App app)
            {
                app.ApplyConfiguredTheme();
            }
        }










    }
}