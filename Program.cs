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

            DataPaths.Initialize(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}