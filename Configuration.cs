using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    class Configuration
    {
        private static readonly string filePath = Path.Combine(Application.StartupPath, "config.txt");
        private static Dictionary<string, string> settings = new Dictionary<string, string>();

        public static void LoadConfig()
        {
            if (File.Exists(filePath))
            {
                settings = File.ReadAllLines(filePath)
                    .Select(line => line.Split('='))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0], parts => parts[1]);
            }
        }

        public static void SaveConfig()
        {
            File.WriteAllLines(filePath, settings.Select(kv => $"{kv.Key}={kv.Value}"));
        }

        public static string GetSetting(string key, string defaultValue = "")
        {
            return settings.ContainsKey(key) ? settings[key] : defaultValue;
        }

        public static void SaveSetting(string key, string value)
        {
            settings[key] = value;
            SaveConfig();
        }
    }
}
