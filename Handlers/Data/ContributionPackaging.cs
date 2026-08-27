using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Handlers.DataHandling
{
    // ###########################################################################################
    // One file-backed contribution row that may carry an attachable source file.
    // SectionFolder is the zip sub-folder for the row's section (e.g. "ComponentImages"), and
    // ResolvedSourcePath is the verified on-disk path of the file, or null when the row's file
    // could not be resolved (nothing gets attached for it, and its zip entry stays empty).
    // ###########################################################################################
    public sealed class ContributionFileReference
    {
        public string SectionFolder { get; init; } = string.Empty;
        public string? ResolvedSourcePath { get; init; }
    }

    // ###########################################################################################
    // The result of planning the contribution zip: per input row the zip entry name its file
    // lives under (empty string when it has no attachable file), plus the distinct list of
    // files to actually write into the archive. Two rows referencing the same source file share
    // one zip entry, so Attachments can be shorter than EntryNames.
    // ###########################################################################################
    public sealed class ContributionZipPlan
    {
        public IReadOnlyList<string> EntryNames { get; init; } = Array.Empty<string>();
        public IReadOnlyList<ContributionAttachment> Attachments { get; init; } = Array.Empty<ContributionAttachment>();
    }

    public sealed class ContributionAttachment
    {
        public string SourcePath { get; init; } = string.Empty;
        public string ZipEntryName { get; init; } = string.Empty;
    }

    // ###########################################################################################
    // Pure packaging logic for the component contribution upload: resolving referenced files,
    // assigning deterministic zip entry names that the payload records per row (so the server
    // can locate each submitted file exactly instead of guessing by file name), and building
    // the plain-text summary sent alongside the zip.
    // ###########################################################################################
    public static class ContributionPackaging
    {
        public const string ReferencedFilesRootFolder = "ReferencedFiles";

        // Marker the contribution endpoint (Assets/Webserver/app-contribution/api/index.php) puts
        // in its response body when it rejects a submission because the application is older than
        // the newest released version. The token is followed by that newest version number.
        public const string OutdatedVersionToken = "OUTDATED_VERSION";

        // ###########################################################################################
        // Detects the server's "application too old" rejection in a contribution upload response.
        // Returns true when the token is present; newestVersion carries the version number the
        // server named right after the token, or an empty string when none could be read.
        // ###########################################################################################
        public static bool TryParseOutdatedVersionResponse(string? responseBody, out string newestVersion)
        {
            newestVersion = string.Empty;

            string body = responseBody?.Trim() ?? string.Empty;
            int tokenIndex = body.IndexOf(OutdatedVersionToken, StringComparison.OrdinalIgnoreCase);
            if (tokenIndex < 0)
            {
                return false;
            }

            string remainder = body.Substring(tokenIndex + OutdatedVersionToken.Length).Trim();
            string[] parts = remainder.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0 && parts[0].Length > 0 && char.IsDigit(parts[0][0]))
            {
                newestVersion = parts[0];
            }

            return true;
        }

        // ###########################################################################################
        // Assigns a zip entry name to every reference that has a resolved source file.
        // Entry names are "ReferencedFiles/<SectionFolder>/<NNN>_<filename>" with a single global
        // running number, and references to the same source path (case-insensitive) reuse the
        // first entry instead of packing the file twice.
        // ###########################################################################################
        public static ContributionZipPlan AssignZipEntries(IReadOnlyList<ContributionFileReference> references)
        {
            var entryNames = new List<string>(references.Count);
            var attachments = new List<ContributionAttachment>();
            var entryBySourcePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var reference in references)
            {
                string sourcePath = reference.ResolvedSourcePath?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    entryNames.Add(string.Empty);
                    continue;
                }

                if (entryBySourcePath.TryGetValue(sourcePath, out string? existingEntry))
                {
                    entryNames.Add(existingEntry);
                    continue;
                }

                int number = attachments.Count + 1;
                string entryName = $"{ReferencedFilesRootFolder}/{reference.SectionFolder}/{number:D3}_{Path.GetFileName(sourcePath)}";

                entryBySourcePath[sourcePath] = entryName;
                attachments.Add(new ContributionAttachment
                {
                    SourcePath = sourcePath,
                    ZipEntryName = entryName
                });
                entryNames.Add(entryName);
            }

            return new ContributionZipPlan
            {
                EntryNames = entryNames,
                Attachments = attachments
            };
        }

        // ###########################################################################################
        // Resolves an edited file path so it can be verified for existence and attached.
        // Accepts both relative paths (resolved against the data root) and external absolute
        // paths chosen through the file picker. Returns null when the file does not exist.
        // ###########################################################################################
        public static string? ResolveExistingFilePath(string dataRoot, string? pathValue)
        {
            string trimmed = pathValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            try
            {
                string normalizedInput = trimmed.Replace('/', Path.DirectorySeparatorChar);

                // 1. If the user selected an absolute path via the file picker anywhere on their PC, allow it!
                if (Path.IsPathRooted(normalizedInput))
                {
                    string fullPath = Path.GetFullPath(normalizedInput);
                    return File.Exists(fullPath) ? fullPath : null;
                }

                // 2. If it's a relative path, assume it lives strictly inside the current data-root
                if (!string.IsNullOrWhiteSpace(dataRoot))
                {
                    string normalizedDataRoot = Path.GetFullPath(dataRoot);
                    string combinedPath = Path.GetFullPath(Path.Combine(normalizedDataRoot, normalizedInput));
                    return File.Exists(combinedPath) ? combinedPath : null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // ###########################################################################################
        // Builds the plain-text summary sent alongside the zipped JSON payload. The server relies
        // on the "Mandatory change comment:" marker line when formatting the notification email,
        // so that exact wording is part of the upload contract.
        // ###########################################################################################
        public static string BuildFeedbackText(
            string hardwareName,
            string boardName,
            string componentDisplayText,
            string componentUuidV4,
            string region,
            string comment)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Hardware: {hardwareName}");
            builder.AppendLine($"Board: {boardName}");
            builder.AppendLine($"Component: {componentDisplayText}");
            builder.AppendLine($"Component UUID v4: {componentUuidV4}");
            builder.AppendLine($"Region context: {region}");
            builder.AppendLine();
            builder.AppendLine("Mandatory change comment:");
            builder.AppendLine(comment);

            return builder.ToString();
        }
    }
}
