using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    // ###########################################################################################
    // Downloads a new application executable and orchestrates a self-replacing update via a
    // platform-appropriate script (batch on Windows, bash on Linux/Wine).
    // ###########################################################################################
    public static class AutoUpdater
    {
        // Minimum plausible size for a .NET executable (bytes)
        private const long MinExeSize = 50 * 1024;

        // The same log file that Main.DebugOutput() writes to
        private static string AppLogFile => Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.log");

        // ###########################################################################################
        // Downloads the new executable from the given URL, writes an updater script, launches it,
        // and exits the application. Returns false if any step fails before the script is launched.
        // ###########################################################################################
        public static bool PerformUpdate(string downloadUrl, string restartParameter)
        {
            Log("=== AutoUpdater started ===");
            Log("Download URL: " + downloadUrl);
            Log("Restart parameter: " + restartParameter);

            string currentExePath = Application.ExecutablePath;

            // Detect platform early so the correct temp directory can be chosen
            bool isWine = IsRunningUnderWine();
            bool isUnix = Environment.OSVersion.Platform == PlatformID.Unix
                       || Environment.OSVersion.Platform == PlatformID.MacOSX;

            // Under Wine, Path.GetTempPath() returns a C:\ path (e.g. C:\users\dh\AppData\Local\Temp)
            // that Wine can write to internally, but native bash cannot address.
            // Wine maps Z:\ to / on the host filesystem, so Z:\tmp becomes /tmp.
            string tempDir;
            if (isWine && Directory.Exists(@"Z:\"))
            {
                tempDir = @"Z:\tmp";
                Log("Using Wine Z: drive for temp: " + tempDir);
            }
            else
            {
                tempDir = Path.GetTempPath();
                if (isWine)
                {
                    Log("WARNING: Wine Z: drive not found, falling back to: " + tempDir);
                }
            }

            string tempExePath = Path.Combine(tempDir, "crt_update_" + Path.GetFileName(currentExePath));

            // Step 1: Validate the URL
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                ShowError("Update URL is not configured.");
                return false;
            }

            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http"))
            {
                ShowError("Invalid update URL:\n" + downloadUrl);
                return false;
            }

            // Step 2: Verify the application directory is writable
            string appDir = Path.GetDirectoryName(currentExePath);
            if (!IsDirectoryWritable(appDir))
            {
                ShowError(
                    "The application directory is not writable:\n\n" + appDir +
                    "\n\nTry running the application as administrator, or move it to a writable location."
                );
                return false;
            }

            // Step 3: Download new executable to temp
            Log("Downloading to: " + tempExePath);
            try
            {
                // Remove stale temp file from a previous failed attempt
                if (File.Exists(tempExePath))
                {
                    File.Delete(tempExePath);
                }

                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

                using (var client = new WebClient())
                {
                    client.Headers.Add("user-agent", "CRT AutoUpdater");
                    client.DownloadFile(downloadUrl, tempExePath);
                }
            }
            catch (WebException ex)
            {
                Log("EXCEPTION (download): " + ex);
                Cleanup(tempExePath);
                ShowError(
                    "Failed to download the update.\n\n" +
                    "URL: " + downloadUrl + "\n\n" +
                    "Error: " + ex.Message +
                    (ex.Response is HttpWebResponse resp
                        ? "\nHTTP status: " + (int)resp.StatusCode + " " + resp.StatusDescription
                        : "")
                );
                return false;
            }
            catch (Exception ex)
            {
                Log("EXCEPTION (download): " + ex);
                Cleanup(tempExePath);
                ShowError("Failed to download the update:\n\n" + ex.Message);
                return false;
            }

            // Step 4: Validate the downloaded file
            try
            {
                var fileInfo = new FileInfo(tempExePath);
                if (!fileInfo.Exists)
                {
                    Log("ERROR: Downloaded file does not exist at " + tempExePath);
                    ShowError("Downloaded file is missing.\n\nExpected at: " + tempExePath);
                    return false;
                }

                if (fileInfo.Length == 0)
                {
                    Log("ERROR: Downloaded file is 0 bytes");
                    Cleanup(tempExePath);
                    ShowError("Downloaded file is empty (0 bytes).\n\nThe server may be unreachable or the URL is incorrect.");
                    return false;
                }

                if (fileInfo.Length < MinExeSize)
                {
                    Log("WARNING: Downloaded file is suspiciously small (" + fileInfo.Length + " bytes) - checking content");
                    string head = ReadFileHead(tempExePath, 512);
                    if (head.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        head.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Log("ERROR: Downloaded file appears to be an HTML page, not an executable");
                        Cleanup(tempExePath);
                        ShowError(
                            "The downloaded file appears to be a web page, not an executable.\n\n" +
                            "The update URL may be incorrect or the server returned an error page.\n\n" +
                            "URL: " + downloadUrl
                        );
                        return false;
                    }
                    Log("File is small but not HTML - proceeding");
                }

                Log("Download OK: " + fileInfo.Length + " bytes");
            }
            catch (Exception ex)
            {
                Log("EXCEPTION (validation): " + ex);
                Cleanup(tempExePath);
                ShowError("Failed to validate the downloaded file:\n\n" + ex.Message);
                return false;
            }

            // Step 5: Launch the right updater script for the platform
            Log("Platform: isWine=" + isWine + " isUnix=" + isUnix);

            try
            {
                if (isWine || isUnix)
                {
                    LaunchBashUpdater(currentExePath, tempExePath, restartParameter, isWine);
                }
                else
                {
                    LaunchBatchUpdater(currentExePath, tempExePath, restartParameter);
                }
            }
            catch (Exception ex)
            {
                Log("EXCEPTION (script launch): " + ex);
                Cleanup(tempExePath);
                ShowError(
                    "Failed to launch the updater script:\n\n" + ex.Message +
                    "\n\nThe downloaded file has been removed. Please try again."
                );
                return false;
            }

            // Step 6: Exit only after the script has been successfully launched
            Log("Updater script launched. Exiting application.");
            Application.Exit();
            return true;
        }

        // ###########################################################################################
        // Windows-native: writes a .bat script that polls the PID, replaces the exe, and relaunches.
        // Errors are appended to the application log and surfaced via a visible console window.
        // ###########################################################################################
        private static void LaunchBatchUpdater(string currentExePath, string tempExePath, string restartParameter)
        {
            string batchPath = Path.Combine(Path.GetTempPath(), "crt_updater.bat");
            int pid = Process.GetCurrentProcess().Id;
            string logEscaped = AppLogFile.Replace("/", "\\");

            string content =
                "@echo off\r\n" +
                "echo [%DATE% %TIME%] Updater script started >> \"" + logEscaped + "\"\r\n" +
                "echo [%DATE% %TIME%] Waiting for PID " + pid + " to exit >> \"" + logEscaped + "\"\r\n" +
                ":waitloop\r\n" +
                "tasklist /FI \"PID eq " + pid + "\" 2>NUL | find /I \"" + pid + "\" >NUL\r\n" +
                "if not errorlevel 1 (\r\n" +
                "    timeout /t 1 /nobreak >NUL\r\n" +
                "    goto waitloop\r\n" +
                ")\r\n" +
                "echo [%DATE% %TIME%] Process exited. Replacing executable >> \"" + logEscaped + "\"\r\n" +
                "copy /Y \"" + tempExePath + "\" \"" + currentExePath + "\"\r\n" +
                "if errorlevel 1 (\r\n" +
                "    echo [%DATE% %TIME%] ERROR: Failed to replace executable >> \"" + logEscaped + "\"\r\n" +
                "    echo.\r\n" +
                "    echo ============================================\r\n" +
                "    echo   ERROR: Could not replace the executable.\r\n" +
                "    echo   The folder may be write-protected.\r\n" +
                "    echo   Check the log: \"" + logEscaped + "\"\r\n" +
                "    echo ============================================\r\n" +
                "    echo.\r\n" +
                "    pause\r\n" +
                "    goto cleanup\r\n" +
                ")\r\n" +
                "echo [%DATE% %TIME%] Executable replaced. Launching >> \"" + logEscaped + "\"\r\n" +
                "start \"\" \"" + currentExePath + "\" " + restartParameter + "\r\n" +
                "if errorlevel 1 (\r\n" +
                "    echo [%DATE% %TIME%] ERROR: Failed to launch the updated executable >> \"" + logEscaped + "\"\r\n" +
                "    echo.\r\n" +
                "    echo ============================================\r\n" +
                "    echo   ERROR: Could not start the application.\r\n" +
                "    echo   Check the log: \"" + logEscaped + "\"\r\n" +
                "    echo ============================================\r\n" +
                "    echo.\r\n" +
                "    pause\r\n" +
                "    goto cleanup\r\n" +
                ")\r\n" +
                "echo [%DATE% %TIME%] Update completed successfully >> \"" + logEscaped + "\"\r\n" +
                ":cleanup\r\n" +
                "del \"" + tempExePath + "\" >NUL 2>&1\r\n" +
                "del \"%~f0\" >NUL 2>&1\r\n";

            File.WriteAllText(batchPath, content);

            if (!File.Exists(batchPath))
            {
                throw new IOException("Failed to write updater script to: " + batchPath);
            }

            Log("Launching batch script: " + batchPath);

            // Hidden by default; the script reveals its own window only on error via "pause"
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (proc == null)
            {
                throw new InvalidOperationException("Process.Start returned null for batch script.");
            }

            Log("Batch script process started (PID " + proc.Id + ")");
        }

        // ###########################################################################################
        // Linux/Wine: writes a bash script that waits, replaces the exe, and relaunches via
        // "wine" (when under Wine) or "mono" (when native Linux). Uses nohup to survive parent exit.
        // Errors are appended to the application log file.
        // ###########################################################################################
        private static void LaunchBashUpdater(string currentExePath, string tempExePath, string restartParameter, bool isWine)
        {
            string unixCurrentExe = ToUnixPath(currentExePath);
            string unixTempExe = ToUnixPath(tempExePath);

            // Use Z:\tmp for the script when under Wine (maps to /tmp on host)
            string scriptDir = (isWine && Directory.Exists(@"Z:\")) ? @"Z:\tmp" : Path.GetTempPath();
            string scriptFile = Path.Combine(scriptDir, "crt_updater.sh");
            string unixScript = ToUnixPath(scriptFile);
            string unixLog = ToUnixPath(AppLogFile);

            Log("Resolved Unix paths: exe=" + unixCurrentExe + " temp=" + unixTempExe + " script=" + unixScript + " log=" + unixLog);

            // Wine relaunches via "wine", native Linux via "mono"
            string launchCmd = isWine
                ? "wine '" + unixCurrentExe + "' " + restartParameter
                : "mono '" + unixCurrentExe + "' " + restartParameter;

            string content =
                "#!/bin/bash\n" +
                "LOG='" + unixLog + "'\n" +
                "echo \"[$(date '+%Y-%m-%d %H:%M:%S')] [AutoUpdater] Updater script started\" >> \"$LOG\"\n" +
                "sleep 3\n" +
                "echo \"[$(date '+%Y-%m-%d %H:%M:%S')] [AutoUpdater] Replacing executable\" >> \"$LOG\"\n" +
                "cp -f '" + unixTempExe + "' '" + unixCurrentExe + "'\n" +
                "if [ $? -ne 0 ]; then\n" +
                "    echo \"[$(date '+%Y-%m-%d %H:%M:%S')] [AutoUpdater] ERROR: Failed to replace executable\" >> \"$LOG\"\n" +
                "    rm -f '" + unixTempExe + "'\n" +
                "    rm -f '" + unixScript + "'\n" +
                "    exit 1\n" +
                "fi\n" +
                "chmod +x '" + unixCurrentExe + "'\n" +
                "rm -f '" + unixTempExe + "'\n" +
                "echo \"[$(date '+%Y-%m-%d %H:%M:%S')] [AutoUpdater] Launching updated executable\" >> \"$LOG\"\n" +
                launchCmd + " &\n" +
                "if [ $? -ne 0 ]; then\n" +
                "    echo \"[$(date '+%Y-%m-%d %H:%M:%S')] [AutoUpdater] ERROR: Failed to launch executable\" >> \"$LOG\"\n" +
                "else\n" +
                "    echo \"[$(date '+%Y-%m-%d %H:%M:%S')] [AutoUpdater] Update completed successfully\" >> \"$LOG\"\n" +
                "fi\n" +
                "rm -f '" + unixScript + "'\n";

            File.WriteAllText(scriptFile, content);

            if (!File.Exists(scriptFile))
            {
                throw new IOException("Failed to write updater script to: " + scriptFile);
            }

            Log("Launching bash script: " + unixScript);

            string escaped = unixScript.Replace("'", "'\\''");
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"nohup /bin/bash '" + escaped + "' > /dev/null 2>&1 &\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (proc == null)
            {
                throw new InvalidOperationException("Process.Start returned null for bash script.");
            }

            Log("Bash script process started (PID " + proc.Id + ")");
        }

        // ###########################################################################################
        // Converts a Wine/Windows path to a Unix path.
        // Z:\ is mapped to the host root filesystem /.
        // Other drive letters (e.g. C:\) are resolved via the WINEPREFIX environment variable
        // (defaulting to $HOME/.wine), since Wine maps C: to $WINEPREFIX/drive_c/.
        // Non-drive paths simply get backslashes replaced with forward slashes.
        // ###########################################################################################
        private static string ToUnixPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            // Z:\ maps to root filesystem / in Wine
            if (path.Length >= 3 &&
                (path[0] == 'Z' || path[0] == 'z') &&
                path[1] == ':' &&
                (path[2] == '\\' || path[2] == '/'))
            {
                return path.Substring(2).Replace('\\', '/');
            }

            // For other Wine drive letters (e.g. C:\), resolve via WINEPREFIX
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
            {
                string prefix = Environment.GetEnvironmentVariable("WINEPREFIX");
                if (string.IsNullOrEmpty(prefix))
                {
                    // Default Wine prefix is ~/.wine; HOME is passed through from the host
                    string home = Environment.GetEnvironmentVariable("HOME");
                    if (!string.IsNullOrEmpty(home))
                    {
                        prefix = home + "/.wine";
                    }
                }

                if (!string.IsNullOrEmpty(prefix))
                {
                    char driveLetter = char.ToLower(path[0]);
                    string rest = path.Substring(2).Replace('\\', '/');
                    return prefix + "/drive_" + driveLetter + rest;
                }
            }

            return path.Replace('\\', '/');
        }

        // ###########################################################################################
        // Detects if the application is running under Wine (same logic as Main.IsRunningUnderWine).
        // ###########################################################################################
        private static bool IsRunningUnderWine()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINELOADERNOEXEC"))) return true;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINEPREFIX"))) return true;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINESERVER"))) return true;

            try
            {
                IntPtr ntdll = GetModuleHandle("ntdll.dll");
                if (ntdll != IntPtr.Zero)
                {
                    IntPtr wineVer = GetProcAddress(ntdll, "wine_get_version");
                    if (wineVer != IntPtr.Zero) return true;
                }
            }
            catch { }

            return false;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        // ###########################################################################################
        // Checks whether the target directory is writable by attempting to create a temp file.
        // ###########################################################################################
        private static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                string testFile = Path.Combine(dirPath, ".crt_write_test_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ###########################################################################################
        // Reads the first N bytes of a file as a string for content-type sniffing.
        // ###########################################################################################
        private static string ReadFileHead(string path, int maxBytes)
        {
            try
            {
                byte[] buffer = new byte[maxBytes];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int read = fs.Read(buffer, 0, buffer.Length);
                    return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                }
            }
            catch
            {
                return "";
            }
        }

        // ###########################################################################################
        // Forwards a prefixed message to the shared application log via Main.DebugOutput().
        // ###########################################################################################
        private static void Log(string message)
        {
            Main.DebugOutput("[AutoUpdater] " + message);
        }

        // ###########################################################################################
        // Shows a user-visible error dialog and logs the message. Tells the user which log to check.
        // ###########################################################################################
        private static void ShowError(string message)
        {
            Log("ERROR: " + message.Replace("\n", " "));
            MessageBox.Show(
                message + "\n\nCheck the log for details:\n" + AppLogFile,
                "Auto-Update Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        // ###########################################################################################
        // Removes the temp download file if it exists. Swallows exceptions.
        // ###########################################################################################
        private static void Cleanup(string tempExePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(tempExePath) && File.Exists(tempExePath))
                {
                    File.Delete(tempExePath);
                    Log("Cleaned up temp file: " + tempExePath);
                }
            }
            catch (Exception ex)
            {
                Log("WARNING: Failed to clean up temp file: " + ex.Message);
            }
        }
    }
}