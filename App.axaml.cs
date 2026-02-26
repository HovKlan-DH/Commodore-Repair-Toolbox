using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace CRT
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // ###########################################################################################
        // Shows the splash screen, initializes data (syncing with online source), then opens the main window.
        // ###########################################################################################
        public override async void OnFrameworkInitializationCompleted()
        {
            Logger.Initialize();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Logger.Info(version != null
                ? $"Commodore Repair Toolbox version [{version.Major}.{version.Minor}.{version.Build}] launched"
                : "Commodore Repair Toolbox launched");

            _ = OnlineServices.CheckVersionAsync();

            var os = RuntimeInformation.OSDescription;
//            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.OSVersion.Version.Build >= 22000)
//                os = os.Replace("Windows 10", "Windows 11");
            Logger.Info($"Operating system is [{os}]");

            var archDescription = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "64-bit",
                Architecture.X86 => "32-bit",
                Architecture.Arm64 => "ARM 64-bit",
                Architecture.Arm => "ARM 32-bit",
                var a => a.ToString()
            };
            Logger.Info($"CPU architecture is [{archDescription}]");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var splash = new Splash();
                desktop.MainWindow = splash;
                splash.Show();

                await DataManager.InitializeAsync(desktop.Args ?? []);

                var main = new Main();
                desktop.MainWindow = main;
                main.Show();
                splash.Close();

                Logger.Info("Main window opened");
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}