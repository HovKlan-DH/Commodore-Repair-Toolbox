using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    public static class DataPaths
    {
        public static string DataRoot { get; private set; }
        public static string MainExcelPath => Path.Combine(DataRoot, MainExcelFileName);
        public const string MainExcelFileName = "Commodore-Repair-Toolbox.xlsx";

        public static string SourceDescription { get; private set; }
        public static bool IsDevOverride { get; private set; }

        private static bool _initialized;

        public static void Initialize(string[] args)
        {
            if (_initialized) return;
            _initialized = true;

            string exeDir = Application.StartupPath;
            string defaultExeData = Path.Combine(exeDir, "Data");

            string cmdValue = GetArgValue(args, "data-root");
            string cfgValue = Configuration.GetSetting("DataRoot", null);

            // 1) Command line
            if (!string.IsNullOrWhiteSpace(cmdValue))
            {
                if (Directory.Exists(cmdValue))
                {
                    DataRoot = cmdValue;
                    SourceDescription = "Commandline option";
                    Persist(cmdValue);
                    FinalValidate();
                    return;
                }
                Warn($"Data folder specified on command line does not exist:\r\n{cmdValue}");
            }

            // 2) Config
            if (!string.IsNullOrWhiteSpace(cfgValue))
            {
                if (Directory.Exists(cfgValue))
                {
                    DataRoot = cfgValue;
                    SourceDescription = "Configuration file";
                    FinalValidate();
                    return;
                }
                Warn($"Configured DataRoot does not exist:\r\n{cfgValue}");
            }

            // 3) <exe>\\Data (only if it already exists)
            if (Directory.Exists(defaultExeData))
            {
                DataRoot = defaultExeData;
                SourceDescription = "<executable file>\\Data";
                FinalValidate();
                return;
            }

            // 4) Visual Studio dev override (../../../Data) LAST precedence
            if (TryResolveDevOverride(exeDir, out string devRoot))
            {
                DataRoot = devRoot;
                IsDevOverride = true;
                SourceDescription = "Visual Studio development override";
                FinalValidate();
                return;
            }

            CriticalExit(
                "Could not resolve a valid data-root.\r\n\r\n" +
                "Checked in order:\r\n" +
                "  1) Commandline --data-root\r\n" +
                "  2) Config key DataRoot\r\n" +
                "  3) <exe>\\Data (should exist)\r\n" +
                "  4) ../../../Data (only when debugging in Visual Studio)\r\n\r\n" +
                "Please specify --data-root or create an appropriate \"Data\" folder.");
        }

        private static void FinalValidate()
        {
            if (string.IsNullOrWhiteSpace(DataRoot) || !Directory.Exists(DataRoot))
            {
                CriticalExit("Internal error: resolved DataRoot is invalid.");
            }

            string excel = MainExcelPath;
            if (!File.Exists(excel))
            {
                CriticalExit(
                    $"Main Excel data file not found:\r\n{excel}\r\n\r\n" +
                    $"Data root source: {SourceDescription}\r\n\r\n" +
                    $"Ensure '{MainExcelFileName}' exists in the resolved data folder or specify another with --data-root.");
            }

            Main.DebugOutput($"INFO: Data-root resolves to [{DataRoot}]");
            Main.DebugOutput($"INFO: Data-root originates from [{SourceDescription}]");
        }

        private static bool TryResolveDevOverride(string exeDir, out string devRoot)
        {
            devRoot = null;
            if (!Debugger.IsAttached) return false;

            // bin/x64/Debug -> ../../Data
            string candidate = Path.GetFullPath(Path.Combine(exeDir, @"..\..\Data"));
            if (!Directory.Exists(candidate)) return false;

            string excel = Path.Combine(candidate, MainExcelFileName);
            if (!File.Exists(excel)) return false;

            devRoot = candidate;
            return true;
        }

        private static void Warn(string message)
        {
            MessageBox.Show(
                message + "\r\n\r\nContinuing with lower precedence options.",
                "Data path warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void CriticalExit(string message)
        {
            MessageBox.Show(message, "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(-1);
        }

        private static void Persist(string value)
        {
            try { Configuration.SaveSetting("DataRoot", value); } catch { /* non-fatal */ }
        }

        public static string Resolve(string relativeOrAbsolute)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
                return relativeOrAbsolute;
            if (Path.IsPathRooted(relativeOrAbsolute))
                return relativeOrAbsolute;
            return Path.Combine(DataRoot, relativeOrAbsolute);
        }

        public static string GetRelativeToRoot(string fullPath)
        {
            try
            {
                string rootFull = Path.GetFullPath(DataRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(fullPath);
                if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(rootFull.Length).Replace('\\', '/');
            }
            catch { }
            return fullPath;
        }

        private static string GetArgValue(string[] args, string key)
        {
            // Support:
            // --key=value | --key:"v" | --key value
            // /key=value  | /key:"v"  | /key value | /key:value
            for (int i = 1; i < args.Length; i++)
            {
                var a = args[i];
                if (!(a.StartsWith("--", StringComparison.OrdinalIgnoreCase) ||
                      a.StartsWith("/", StringComparison.OrdinalIgnoreCase)))
                    continue;

                string body = a.StartsWith("--", StringComparison.OrdinalIgnoreCase)
                    ? a.Substring(2)
                    : a.Substring(1);

                int sepIdx = body.IndexOf('=');
                if (sepIdx < 0) sepIdx = body.IndexOf(':');

                if (sepIdx > 0)
                {
                    string k = body.Substring(0, sepIdx);
                    if (!k.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                    string v = body.Substring(sepIdx + 1).Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(v))
                        return v;
                }
                else
                {
                    if (body.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Length)
                        {
                            string next = args[i + 1];
                            if (!(next.StartsWith("--") || next.StartsWith("/")))
                                return next.Trim().Trim('"');
                        }
                    }
                }
            }
            return null;
        }
    }
}