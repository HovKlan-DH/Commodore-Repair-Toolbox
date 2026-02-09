using System;
using System.Windows.Forms;

using MainForm = Commodore_Repair_Toolbox.Main;  // Alias to avoid clash with Program.Main()

namespace Commodore_Repair_Toolbox
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            var args = Environment.GetCommandLineArgs();

            // Ensure log exists BEFORE any other initialization (DataPaths, help, etc.)
            MainForm.InitializeLogging();

            if (CommandlineHelp.ShouldShowHelp(args))
            {
                CommandlineHelp.ShowAndExit();
                return;
            }

            // Load configuration before DataPaths.Initialize() can persist DataRoot
            Splashscreen.Current?.UpdateStatus("Loading configuration file");
            Configuration.LoadConfig();

            DataPaths.Initialize(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Show splash screen while the main form is loading
            using (var splash = new Splashscreen())
            {
                splash.Show();
                splash.Refresh();

                var mainForm = new MainForm();
                mainForm.Shown += (s, e) => splash.Close();

                Application.Run(mainForm);
            }
        }
    }
}