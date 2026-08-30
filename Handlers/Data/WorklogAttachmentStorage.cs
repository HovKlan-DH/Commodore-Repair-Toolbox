using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Handlers.DataHandling
{
    // ###########################################################################################
    // Naming and validation for worklog entry photo/file attachments - the decisions made when a
    // user picks or drops a file, kept out of WorklogEntryEditorWindow so they can be tested
    // without a window, a file dialog or a drop event.
    //
    // Only the naming and vetting lives here; the actual byte copy is CopyAttachmentIntoFolder,
    // which is the single method in this class that touches the disk.
    // ###########################################################################################
    public static class WorklogAttachmentStorage
    {
        // ###########################################################################################
        // Why an attachment was refused. Distinguishing these lets the caller say what is actually
        // wrong instead of a single unhelpful "could not add photo".
        // ###########################################################################################
        public enum AttachmentProblem
        {
            None,
            NoFileSelected,
            FileNotFound,
            NotDisplayableImage
        }

        // ###########################################################################################
        // Vets a path the user picked or dropped, before anything is copied. requireDisplayableImage
        // is set for the Photos section, which draws its attachments with Avalonia's Bitmap and so
        // cannot accept a format Bitmap will not decode - reusing ContributionPackaging's set rather
        // than keeping a second copy of it (see IsDisplayableImageFile for why it is narrower than
        // the ExternalTargetLauncher allowlist).
        //
        // The file picker's own filter is only a suggestion - a name typed into its file box gets
        // straight past it, and a drag-and-drop never consults it at all - so the format is checked
        // here rather than trusted, exactly as ComponentContribution's picker does.
        // ###########################################################################################
        public static AttachmentProblem ValidateSourceFile(string? sourcePath, bool requireDisplayableImage)
        {
            string trimmed = sourcePath?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return AttachmentProblem.NoFileSelected;
            }

            if (requireDisplayableImage && !ContributionPackaging.IsDisplayableImageFile(trimmed))
            {
                return AttachmentProblem.NotDisplayableImage;
            }

            try
            {
                if (!File.Exists(trimmed))
                {
                    return AttachmentProblem.FileNotFound;
                }
            }
            catch
            {
                // A malformed path (illegal characters, too long) throws rather than returning
                // false, and is just as unusable as a missing one.
                return AttachmentProblem.FileNotFound;
            }

            return AttachmentProblem.None;
        }

        // ###########################################################################################
        // The message shown for a refused attachment. Kept beside the enum so a new problem cannot
        // be added without a matching sentence.
        // ###########################################################################################
        public static string DescribeProblem(AttachmentProblem problem) => problem switch
        {
            AttachmentProblem.NoFileSelected => "Select a file first.",
            AttachmentProblem.FileNotFound => "That file could no longer be found.",
            AttachmentProblem.NotDisplayableImage =>
                "That file is not an image the application can display. Use PNG, JPG, GIF, BMP or WEBP.",
            _ => string.Empty
        };

        // ###########################################################################################
        // The name an attachment is stored under: its owning record's id, an underscore, then the
        // original file name - "3_IMG_1234.jpg" for photo #3.
        //
        // Attachments share one folder per entry, so two photos picked from different folders that
        // are both "IMG_1234.jpg" would otherwise collide and the second would overwrite the first
        // one's bytes while both rows pointed at the same file. Prefixing with the record id makes
        // the name unique by construction - ids are unique within their list - instead of hunting
        // for a free " (2)" variant, and it makes the file's owner readable straight off the name,
        // which is what lets a deleted photo's file be found and removed with confidence.
        //
        // Photos and Files number their ids independently, so photo #3 and file #3 would both want
        // "3_..."; the caller passes a prefix that separates them (see PhotoFilePrefix).
        //
        // The original name is kept rather than replaced by the id alone so the folder stays
        // readable to someone looking at it outside the app.
        // ###########################################################################################
        public static string BuildStoredFileName(string? sourcePath, string ownerPrefix, int recordId)
        {
            string candidate;

            try
            {
                candidate = Path.GetFileName(sourcePath?.Trim() ?? string.Empty);
            }
            catch
            {
                candidate = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = "attachment";
            }

            candidate = SanitizeFileName(candidate);

            return $"{ownerPrefix}{recordId.ToString(CultureInfo.InvariantCulture)}_{candidate}";
        }

        // ###########################################################################################
        // Prefixes keeping the two attachment lists' ids from colliding in the folder they share.
        //
        // Photos take no prefix - "3_board.png" - since they are the common case and the bare id
        // reads cleanly. Files keep one, because the two lists number their ids independently: photo
        // #3 and file #3 both exist, and without something to tell them apart both would want
        // "3_board.png" and one would overwrite the other. Nothing writes a file attachment yet
        // (the Files section's Add is still a no-op), so this only matters when that is implemented.
        // ###########################################################################################
        public const string PhotoFilePrefix = "";
        public const string FileFilePrefix = "file";

        // ###########################################################################################
        // Strips anything the filesystem will not accept in a name. A dropped file's name comes from
        // wherever it was dragged from, so it is not assumed to be a legal name on this platform -
        // and the stored name is later combined into a path, so a name carrying a directory
        // separator would otherwise write outside the attachments folder.
        // ###########################################################################################
        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new System.Text.StringBuilder(fileName.Length);

            foreach (char character in fileName)
            {
                builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
            }

            string sanitized = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "attachment" : sanitized;
        }

        // ###########################################################################################
        // Moves one attachment a single step through the display order - the Files section's up/down
        // buttons. A step that would fall off either end is ignored rather than clamped, so the
        // button is simply inert at the ends.
        //
        // Shares ReorderAttachment's dense renumbering rather than repeating it; this used to be a
        // private copy inside WorklogEntryEditorWindow, where no test could reach it.
        // ###########################################################################################
        public static void StepAttachment(List<WorklogAttachmentRecord> attachments, int id, int direction)
        {
            if (attachments == null || attachments.Count < 2)
            {
                return;
            }

            var ordered = attachments.OrderBy(a => a.DisplayOrder).ToList();

            int index = ordered.FindIndex(a => a.Id == id);
            if (index < 0)
            {
                return;
            }

            int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= ordered.Count)
            {
                return;
            }

            ReorderAttachment(attachments, id, targetIndex);
        }

        // ###########################################################################################
        // Renumbers DisplayOrder densely from 0, preserving the order the attachments already sort
        // into. Called when a list is loaded so that duplicate or gapped values - written by an
        // older build whose add path started at 1 while its reorder renumbered from 0, or by a
        // hand-edited entries.json - cannot leave two rows sharing an order and sorting arbitrarily.
        //
        // Returns true when anything actually changed, so the caller can avoid a pointless save.
        // ###########################################################################################
        public static bool NormalizeDisplayOrder(List<WorklogAttachmentRecord> attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return false;
            }

            var ordered = attachments.OrderBy(a => a.DisplayOrder).ToList();
            bool changed = false;

            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].DisplayOrder != i)
                {
                    ordered[i].DisplayOrder = i;
                    changed = true;
                }
            }

            return changed;
        }

        // ###########################################################################################
        // Reorders attachments by moving the dragged one to sit at targetIndex in display order,
        // renumbering DisplayOrder from 0 so the result is a dense, gap-free sequence.
        //
        // targetIndex is the position the row should end up at in the list as the user sees it. It
        // is clamped rather than rejected, because a drag released past the end of the list means
        // "put it last", which is what the user is asking for and not an error.
        //
        // targetIndex is interpreted as the final position in the reordered list, so it is used
        // directly after the removal rather than adjusted: dragging row 0 to index 2 of four leaves
        // [1,2,3], and inserting at 2 puts the row third, which is where it was dropped.
        // ###########################################################################################
        public static void ReorderAttachment(List<WorklogAttachmentRecord> attachments, int id, int targetIndex)
        {
            if (attachments == null || attachments.Count < 2)
            {
                return;
            }

            var ordered = attachments.OrderBy(a => a.DisplayOrder).ToList();

            int currentIndex = ordered.FindIndex(a => a.Id == id);
            if (currentIndex < 0)
            {
                return;
            }

            int clampedTarget = Math.Clamp(targetIndex, 0, ordered.Count - 1);
            if (clampedTarget == currentIndex)
            {
                return;
            }

            var moved = ordered[currentIndex];
            ordered.RemoveAt(currentIndex);
            ordered.Insert(Math.Clamp(clampedTarget, 0, ordered.Count), moved);

            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].DisplayOrder = i;
            }
        }

        // ###########################################################################################
        // Deletes one attachment's bytes, reporting whether the folder no longer holds that file.
        // An already-missing file counts as success: the caller's goal is that it is gone, and a
        // photo whose file was removed outside the app must still be deletable from the list.
        // ###########################################################################################
        public static bool DeleteAttachmentFile(string? attachmentsFolder, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(attachmentsFolder) || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            try
            {
                string path = Path.Combine(attachmentsFolder, fileName);

                // Guards against a stored name that escapes the folder (a "..\" that predates the
                // sanitizing above, or a hand-edited entries.json) - deleting outside the
                // attachments folder is never intended here.
                string fullPath = Path.GetFullPath(path);
                string fullFolder = Path.GetFullPath(attachmentsFolder);

                if (!fullPath.StartsWith(fullFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warning($"Refused to delete worklog attachment outside its folder: [{fileName}]");
                    return false;
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to delete worklog attachment [{fileName}]: {ex.Message}");
                return false;
            }
        }

        // ###########################################################################################
        // Copies a vetted source file into the entry's attachments folder under storedFileName, and
        // reports whether the bytes actually landed. Returns false rather than throwing when the
        // copy fails (source vanished between picking and copying, destination not writable, disk
        // full), so the caller can refuse to add a row that would point at a file which is not
        // there - a row whose image can never load is worse than a refused add.
        //
        // Overwrites deliberately: stored names are built from the owning record's id, so replacing
        // a photo's image reuses that photo's existing name on purpose and the old bytes are meant
        // to go. There is no accidental-collision case left for a refusal to protect against.
        // ###########################################################################################
        public static bool CopyAttachmentIntoFolder(string sourcePath, string attachmentsFolder, string storedFileName)
        {
            try
            {
                Directory.CreateDirectory(attachmentsFolder);
                File.Copy(sourcePath, Path.Combine(attachmentsFolder, storedFileName), overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to copy worklog attachment [{storedFileName}]: {ex.Message}");
                return false;
            }
        }

        // ###########################################################################################
        // Swaps an attachment's file for a newly chosen one, leaving exactly one file behind, and
        // returns the name to store on the record (unchanged when the copy failed).
        //
        // The two cases look the same to the user but are not on disk. Stored names are built from
        // the record's id, so re-picking a file with the same name and extension produces the same
        // stored name and the copy overwrites in place - deleting "the old file" afterwards would
        // delete the one just written. Change the extension or pick a differently-named file and the
        // new name differs, so the previous file has to be removed or it sits there orphaned,
        // invisible to the app but taking up space forever.
        //
        // Ordering matters: the old file is only removed once the replacement has actually landed.
        // A failed copy leaves the original untouched and the record still pointing at it, rather
        // than a row whose image can never load again.
        // ###########################################################################################
        public static bool TryReplaceAttachmentFile(
            string sourcePath,
            string attachmentsFolder,
            string previousFileName,
            string newStoredFileName,
            out string storedFileName)
        {
            storedFileName = previousFileName;

            if (!CopyAttachmentIntoFolder(sourcePath, attachmentsFolder, newStoredFileName))
            {
                return false;
            }

            storedFileName = newStoredFileName;

            if (!string.Equals(previousFileName, newStoredFileName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteAttachmentFile(attachmentsFolder, previousFileName);
            }

            return true;
        }
    }
}
