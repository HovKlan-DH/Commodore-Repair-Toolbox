using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRT
{
    internal sealed class DataFileEntry
    {
        [JsonPropertyName("file")] public string File { get; init; } = string.Empty;
        [JsonPropertyName("checksum")] public string Checksum { get; init; } = string.Empty;
        [JsonPropertyName("url")] public string Url { get; init; } = string.Empty;
    }

    public static class OnlineServices
    {
        private const string ChecksumsUrl = "https://commodore-repair-toolbox.dk/auto-data/dataChecksums.json";
        private const string CheckVersionUrl = "https://commodore-repair-toolbox.dk/auto-update/";

        // ###########################################################################################
        // Asks server for newest version.
        // Reports the app version and OS details. Runs silently - failures are only logged.
        // ###########################################################################################
        public static async Task CheckVersionAsync()
        {
            try
            {
                var assemblyVersion = typeof(OnlineServices).Assembly.GetName().Version;
                var versionString = assemblyVersion != null
                    ? $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
                    : "0.0.0";

                var osHighLevel = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) ? "FreeBSD"
                    : "Unknown";

                var osVersion = RuntimeInformation.OSDescription;

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"VRT {versionString}");

                var payload = new List<KeyValuePair<string, string>>
                {
                    new("control", "VRT"),
                    new("osHighlevel", osHighLevel),
                    new("osVersion", osVersion),
                };

                using var response = await http.PostAsync(CheckVersionUrl, new FormUrlEncodedContent(payload));
                var responseBody = await response.Content.ReadAsStringAsync();
                Logger.Info($"Online version check completed - [{(int)response.StatusCode}] [{osHighLevel}] [{osVersion}]");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Online version check failed - [{ex.Message}]");
            }
        }

        // ###########################################################################################
        // Compares every local file against the online checksum manifest and downloads anything
        // that is missing or has changed. Runs in two phases: verify checksums, then download.
        // onStatus:  optional callback for general progress messages.
        // onFile:    optional callback fired with each file path as it is being downloaded.
        // ###########################################################################################
        public static async Task SyncDataAsync(string dataRoot, Action<string>? onStatus = null, Action<string>? onFile = null)
        {
            using var http = new HttpClient();

            string json;
            try
            {
                json = await http.GetStringAsync(ChecksumsUrl);
            }
            catch (Exception ex)
            {
                Logger.Critical($"Failed to fetch checksum manifest: {ex.Message}");
                onStatus?.Invoke("Sync failed - see log");
                return;
            }

            List<DataFileEntry>? entries;
            try
            {
                entries = JsonSerializer.Deserialize<List<DataFileEntry>>(json);
            }
            catch (Exception ex)
            {
                Logger.Critical($"Failed to parse checksum manifest: {ex.Message}");
                onStatus?.Invoke("Sync failed - see log");
                return;
            }

            if (entries is null || entries.Count == 0)
            {
                Logger.Warning("Checksum manifest is empty");
                onStatus?.Invoke("No files in manifest");
                return;
            }

            Logger.Info($"Online source checksum manifest fetched - [{entries.Count}] files available online");

            // Phase 1: Compare local checksums against the manifest — no file callback here,
            // the splash only shows filenames during actual downloads in Phase 2
            var toDownload = new List<(DataFileEntry Entry, bool IsNew)>();

            foreach (var entry in entries)
            {
                var relativePath = entry.File.Replace('/', Path.DirectorySeparatorChar);
                var localPath = Path.Combine(dataRoot, relativePath);

                if (!File.Exists(localPath))
                {
                    toDownload.Add((entry, true));
                }
                else
                {
                    var localChecksum = await ComputeChecksumAsync(localPath);
                    if (localChecksum != entry.Checksum.ToLowerInvariant())
                        toDownload.Add((entry, false));
                }
            }

            Logger.Info($"Check complete - [{toDownload.Count}] of [{entries.Count}] files need updating");

            // Phase 2: Download files that are new or changed
            if (toDownload.Count == 0)
            {
                Logger.Info($"All [{entries.Count}] files are up to date");
                onStatus?.Invoke("All files are up to date");
                onFile?.Invoke(string.Empty);
                return;
            }

            Logger.Info("Individual file sync status:");

            int newCount = 0, updatedCount = 0, failedCount = 0;
            int downloadIndex = 0;

            foreach (var (entry, isNew) in toDownload)
            {
                downloadIndex++;
                onStatus?.Invoke($"Downloading file [{downloadIndex}] of [{toDownload.Count}] from online source:");
                onFile?.Invoke(entry.File);
                if (await DownloadFileAsync(http, entry, dataRoot, isNew))
                {
                    if (isNew) newCount++;
                    else updatedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            int upToDateCount = entries.Count - toDownload.Count;
            Logger.Info($"Sync completed - [{newCount}] new, [{updatedCount}] updated, [{failedCount}] failed, [{upToDateCount}] up-to-date");
            onStatus?.Invoke($"Sync complete ({newCount} new, {updatedCount} updated, {failedCount} failed)");
            onFile?.Invoke(string.Empty);
        }

        // ###########################################################################################
        // Computes the SHA-256 checksum of a local file and returns it as a lowercase hex string.
        // ###########################################################################################
        private static async Task<string> ComputeChecksumAsync(string filePath)
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var hash = await SHA256.HashDataAsync(stream);
            return Convert.ToHexStringLower(hash);
        }

        // ###########################################################################################
        // Downloads a single manifest entry, logs the HTTP status code with New/Updated context,
        // and saves it to the correct local path. Returns true on success, false otherwise.
        // ###########################################################################################
        private static async Task<bool> DownloadFileAsync(HttpClient http, DataFileEntry entry, string dataRoot, bool isNew)
        {
            var relativePath = entry.File.Replace('/', Path.DirectorySeparatorChar);
            var localPath = Path.Combine(dataRoot, relativePath);
            var directory = Path.GetDirectoryName(localPath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var decodedUrl = Uri.UnescapeDataString(entry.Url);

            try
            {
                using var response = await http.GetAsync(decodedUrl);
                var statusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localPath, data);
                    Logger.Info($"[{entry.File}] [{statusCode}] [{(isNew ? "New" : "Updated")}]");
                    return true;
                }

                Logger.Warning($"[{entry.File}] [{statusCode}]");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[{entry.File}] [Exception] [{ex.Message}]");
                return false;
            }
        }
    }
}