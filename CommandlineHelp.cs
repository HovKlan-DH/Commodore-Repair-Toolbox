using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    internal static class CommandlineHelp
    {
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        public static bool ShouldShowHelp(string[] args)
        {
            if (args == null || args.Length <= 1) return false;

            return args.Skip(1).Any(a =>
                string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/?", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/h", StringComparison.OrdinalIgnoreCase));
        }

        public static void ShowAndExit()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Usage:");
            sb.AppendLine("  Commodore-Repair-Toolbox.exe [option]");
            sb.AppendLine();
            sb.AppendLine("Options:");
            sb.AppendLine("  --data-root=<path>");
            sb.AppendLine("  --data-root=\"<path with spaces>\"");
            sb.AppendLine("      Specifies the data folder containing:");
            sb.AppendLine("        - Commodore-Repair-Toolbox.xlsx");
            sb.AppendLine("        - All referenced board data files and images etc.");
            sb.AppendLine();
            sb.AppendLine("  --help | /?");
            sb.AppendLine("      Show this help and exit.");
            sb.AppendLine();
            sb.AppendLine("Launching file with no options will startup as default with \"Data\" folder in same directory as the executable file.");
            sb.AppendLine();
            sb.AppendLine("Examples:");
            sb.AppendLine("  Commodore-Repair-Toolbox.exe");
            sb.AppendLine("  Commodore-Repair-Toolbox.exe --data-root=D:\\MyFolder\\Data");
            sb.AppendLine("  Commodore-Repair-Toolbox.exe --data-root=\"D:\\My Folder With Spaces\\Data\"");
            sb.AppendLine("  Commodore-Repair-Toolbox.exe --help");
            sb.AppendLine("  Commodore-Repair-Toolbox.exe /?");
            sb.AppendLine();

            string text = sb.ToString();

            // Try to attach to parent console; if success write there, else message box.
            if (AttachConsole(ATTACH_PARENT_PROCESS))
            {
                try
                {
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.WriteLine(text);
                }
                catch
                {
                    MessageBox.Show(text, "CRT Commandline Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show(text, "CRT Commandline Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Environment.Exit(0);
        }
    }
}