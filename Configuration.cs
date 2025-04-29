using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
//using System.Windows.Shapes;

namespace Commodore_Repair_Toolbox
{
    class Configuration
    {
        private static readonly string filePath = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.cfg");
        private static Dictionary<string, string> settings = new Dictionary<string, string>();

        public static void LoadConfig()
        {
            if (File.Exists(filePath))
            {
                settings = File.ReadAllLines(filePath)
                    .Select(line => line.Split(new[] { '=' }, 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(
                        parts => parts[0].Trim(),  // remove trailing space
                        parts => parts[1].Trim()
                    );
            }

            Debug.WriteLine("---[Configuration file keys loaded]---");
            foreach (var kv in settings)
            {
                Debug.WriteLine($"'{kv.Key}' => '{kv.Value}'");
            }
            Debug.WriteLine("--------------------------------------");
        }

        public static void SaveConfig()
        {
            // Save lines alphabetically sorted
            var sortedSettings = settings.OrderBy(kv => kv.Key).ToList();
            File.WriteAllLines(filePath, sortedSettings.Select(kv => $"{kv.Key}={kv.Value}"));
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
