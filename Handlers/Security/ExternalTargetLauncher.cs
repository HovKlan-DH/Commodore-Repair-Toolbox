using Handlers.DataHandling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CRT
{
    public static class ExternalTargetLauncher
    {
        // ###########################################################################################
        // File extensions the launcher will hand to the OS shell. TryStart uses ShellExecute, and
        // the shell RUNS executables, scripts and shortcuts rather than displaying them - so a
        // *.exe/*.bat/*.lnk inside the (network-synced, community-contributed) data root must never
        // become code execution just because a workbook cell references it. Only the document,
        // image and data formats that board data actually contains are openable; anything else,
        // including a file with no extension, is rejected (fail closed). Extend this set when board
        // data legitimately gains a new non-executable file type.
        // ###########################################################################################
        private static readonly HashSet<string> AllowedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg",
            // Documents
            ".pdf", ".txt", ".md", ".html", ".htm", ".csv",
            // Data
            ".json", ".xml", ".xlsx", ".xls",
            // Domain-specific files shipped with board data (scope captures, CAD schematics)
            ".fsc", ".sch", ".kicad_pcb", ".kicad_sch"
        };

        // ###########################################################################################
        // The same set, exposed so callers that ATTACH a file can refuse an unopenable one up front
        // rather than storing it and failing at open time - the worklog's Files section builds its
        // picker filter and its validation from this.
        //
        // Deliberately derived from the launcher's own set rather than a second hand-kept list: a
        // caller keeping its own would drift, and the drift would show up as a file that attaches
        // happily and then cannot be opened. Extending AllowedFileExtensions extends this too.
        //
        // Returns a COPY. IReadOnlyCollection is only a compile-time promise - the backing HashSet
        // implements it, so handing back the instance itself would let any caller cast it and
        // Add(".exe"), permanently defeating the executable/script/shortcut rejection this class
        // exists to enforce. The set is tiny and callers use it once to build a picker filter, so
        // copying costs nothing next to leaving a security allowlist writable.
        // ###########################################################################################
        public static IReadOnlyCollection<string> OpenableFileExtensions => AllowedFileExtensions.ToArray();

        // ###########################################################################################
        // True when the file's extension is one the launcher will hand to the OS shell. Blank input
        // and a name with no extension are false (fail closed), matching TryOpen's own behaviour.
        //
        // The extension is read from the NORMALIZED full path, exactly as HasAllowedFileExtension
        // does for the open path. Reading it from the raw string instead lets the two disagree
        // about the same file: Windows quirks like a trailing dot or an alternate data stream
        // ("notes.txt:evil.exe") change what the OS finally resolves, so a file could pass the
        // attach-time check here and be refused - or worse, resolve differently - at open time.
        // Since the whole point of exposing this is that the two checks cannot drift, they have to
        // examine the same string.
        // ###########################################################################################
        public static bool IsOpenableFile(string? pathValue)
        {
            string trimmed = pathValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return false;
            }

            try
            {
                // GetFullPath resolves the trailing-dot and ADS forms the raw string hides. It
                // throws on genuinely malformed input, which the catch below turns into a refusal.
                return ExternalTargetLauncher.HasAllowedFileExtension(Path.GetFullPath(trimmed));
            }
            catch
            {
                return false;
            }
        }

        // ###########################################################################################
        // Opens a validated external target. Allowed URI schemes are HTTP/HTTPS/mailto, and local
        // files must resolve inside the configured data-root boundary and carry an extension from
        // the document/image/data allowlist above - never an executable, script or shortcut.
        // ###########################################################################################
        public static bool TryOpen(string target, string? dataRootOverride = null)
        {
            if (string.IsNullOrWhiteSpace(target))
                return false;

            if (ExternalTargetLauncher.TryCreateAllowedUri(target, out Uri allowedUri))
            {
                return ExternalTargetLauncher.TryStart(allowedUri.AbsoluteUri, $"URI [{allowedUri.AbsoluteUri}]");
            }

            string dataRoot = !string.IsNullOrWhiteSpace(dataRootOverride)
                ? dataRootOverride
                : DataManager.DataRoot;

            if (ExternalTargetLauncher.TryResolveDataRootScopedFilePath(target, dataRoot, out string localPath))
            {
                return ExternalTargetLauncher.TryStart(localPath, $"local file [{localPath}]");
            }

            // Logger, not Debug.WriteLine: the latter carries an implicit [Conditional("DEBUG")] and
            // is erased from RELEASE builds, which is precisely where a refused link needs to leave a
            // trace - a user reporting "the link does nothing" has only the log to send.
            Logger.Warning($"Rejected external target outside allowed scope: [{target}]");
            return false;
        }

        // ###########################################################################################
        // Validates that a target string is an allowed absolute URI.
        // ###########################################################################################
        private static bool TryCreateAllowedUri(string target, out Uri uri)
        {
            uri = null!;

            if (!Uri.TryCreate(target.Trim(), UriKind.Absolute, out Uri? candidateUri))
                return false;

            if (!string.Equals(candidateUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(candidateUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(candidateUri.Scheme, "mailto", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            uri = candidateUri;
            return true;
        }

        // ###########################################################################################
        // Resolves a local file path and rejects anything outside the configured data-root, plus
        // any file whose extension is not on the openable-document allowlist.
        // Relative paths are resolved against data-root; absolute paths must still stay inside it.
        // ###########################################################################################
        private static bool TryResolveDataRootScopedFilePath(string target, string dataRoot, out string localPath)
        {
            localPath = string.Empty;

            if (string.IsNullOrWhiteSpace(dataRoot) || string.IsNullOrWhiteSpace(target))
                return false;

            try
            {
                string normalizedDataRoot = Path.GetFullPath(dataRoot);
                string normalizedTargetInput = target.Trim().Replace('/', Path.DirectorySeparatorChar);

                string normalizedTarget = Path.IsPathRooted(normalizedTargetInput)
                    ? Path.GetFullPath(normalizedTargetInput)
                    : Path.GetFullPath(Path.Combine(normalizedDataRoot, normalizedTargetInput));

                string normalizedDataRootWithSeparator = ExternalTargetLauncher.AppendDirectorySeparator(normalizedDataRoot);
                StringComparison pathComparison = ExternalTargetLauncher.GetPathComparison();

                if (string.Equals(normalizedTarget, normalizedDataRoot, pathComparison))
                    return false;

                if (!normalizedTarget.StartsWith(normalizedDataRootWithSeparator, pathComparison))
                    return false;

                if (!ExternalTargetLauncher.HasAllowedFileExtension(normalizedTarget))
                    return false;

                if (!File.Exists(normalizedTarget))
                    return false;

                localPath = normalizedTarget;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ###########################################################################################
        // Returns whether the normalized path carries an extension from the openable allowlist.
        // The extension is taken from the already-normalized full path, so Windows quirks like
        // trailing dots or alternate data streams cannot smuggle a second, executable extension.
        // ###########################################################################################
        private static bool HasAllowedFileExtension(string normalizedPath)
        {
            string extension = Path.GetExtension(normalizedPath);

            return !string.IsNullOrEmpty(extension) &&
                   ExternalTargetLauncher.AllowedFileExtensions.Contains(extension);
        }

        // ###########################################################################################
        // Starts a validated target through the operating system shell.
        // ###########################################################################################
        private static bool TryStart(string fileName, string description)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = true
                });

                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to open {description} - [{ex.Message}]");
                return false;
            }
        }

        // ###########################################################################################
        // Appends a trailing directory separator when missing so StartsWith path checks stay safe.
        // ###########################################################################################
        private static string AppendDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Path.DirectorySeparatorChar.ToString();

            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        // ###########################################################################################
        // Returns the correct filesystem path comparison for the current operating system.
        // ###########################################################################################
        private static StringComparison GetPathComparison()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }
    }
}