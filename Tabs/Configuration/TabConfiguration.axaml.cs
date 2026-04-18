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

            // Initialize configuration checkboxes — subscribe after setting initial values
            // to avoid triggering redundant saves during startup
            this.CheckVersionOnLaunchCheckBox.IsChecked = UserSettings.CheckVersionOnLaunch;
            this.CheckDataOnLaunchCheckBox.IsChecked = UserSettings.CheckDataOnLaunch;
            this.ShowDevelopmentVersionNotificationCheckBox.IsChecked = UserSettings.ShowDevelopmentVersionNotification;
            this.MultipleInstancesForComponentPopupCheckBox.IsChecked = UserSettings.MultipleInstancesForComponentPopup;

            this.ThemeVariantComboBox.SelectionChanged += this.OnThemeVariantSelectionChanged;
            this.CheckVersionOnLaunchCheckBox.IsCheckedChanged += this.OnCheckVersionOnLaunchChanged;
            this.CheckDataOnLaunchCheckBox.IsCheckedChanged += this.OnCheckDataOnLaunchChanged;
            this.ShowDevelopmentVersionNotificationCheckBox.IsCheckedChanged += this.OnShowDevelopmentVersionNotificationChanged;
            this.MultipleInstancesForComponentPopupCheckBox.IsCheckedChanged += this.OnMultipleInstancesForComponentPopupChanged;
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
        // Persists the "Check for new version at launch" preference when the checkbox is toggled.
        // Enabling it also performs the check immediately.
        // ###########################################################################################
        private async void OnCheckVersionOnLaunchChanged(object? sender, RoutedEventArgs e)
        {
            bool isEnabled = this.CheckVersionOnLaunchCheckBox.IsChecked == true;
            UserSettings.CheckVersionOnLaunch = isEnabled;

            if (!isEnabled)
            {
                return;
            }

            if (TopLevel.GetTopLevel(this) is Main mainWindow)
            {
                await mainWindow.CheckForAppUpdateNowAsync();
            }
        }

        // ###########################################################################################
        // Persists the "Check for new or updated data at launch" preference when the checkbox is
        // toggled. Enabling it also performs the check immediately.
        // ###########################################################################################
        private async void OnCheckDataOnLaunchChanged(object? sender, RoutedEventArgs e)
        {
            if (this.thisSuppressCheckDataOnLaunchChanged)
            {
                return;
            }

            bool isEnabled = this.CheckDataOnLaunchCheckBox.IsChecked == true;
            UserSettings.CheckDataOnLaunch = isEnabled;

            if (!isEnabled)
            {
                return;
            }

            if (TopLevel.GetTopLevel(this) is Main mainWindow)
            {
                await mainWindow.CheckForDataUpdatesNowAsync();
            }
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
        // Opens the persistent AppData folder that contains the log and settings files.
        // ###########################################################################################
        private void OnOpenAppDataFolderClick(object? sender, RoutedEventArgs e)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var directory = Path.Combine(appData, AppConfig.AppFolderName);

            try
            {
                Directory.CreateDirectory(directory);

                var psi = new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to open AppData folder: [{directory}] - [{ex.Message}]");
            }
        }

        // ###########################################################################################
        // Reloads user-preference theme colors from settings and reapplies the configured theme.
        // ###########################################################################################
        private void OnReloadUserPreferenceThemeClick(object? sender, RoutedEventArgs e)
        {
            UserSettings.ReloadUserThemeColors();

            if (Application.Current is App app)
            {
                app.ApplyConfiguredTheme();
            }
        }

        // ###########################################################################################
        // Persists the "Open multiple windows for popup" preference when the checkbox is toggled.
        // ###########################################################################################
        private void OnMultipleInstancesForComponentPopupChanged(object? sender, RoutedEventArgs e)
        {
            UserSettings.MultipleInstancesForComponentPopup =
                this.MultipleInstancesForComponentPopupCheckBox.IsChecked == true;
        }





    }
}