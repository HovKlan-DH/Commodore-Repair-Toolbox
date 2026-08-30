using Avalonia;
using CRT;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Handlers.DataHandling
{
    // ###########################################################################################
    // One repair job a user is tracking against a board. Persisted as its own subfolder's
    // index.json inside the "Workbook" folder - never synced and never part of the online "Data"
    // folder. Everything that ever belongs to this workbook (entries, photos, files) lives inside
    // that same subfolder, so deleting the folder deletes the workbook entirely.
    // ###########################################################################################
    public sealed class WorkbookRecord
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("boardKey")] public string BoardKey { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("note")] public string Note { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; set; } = "Open";
        [JsonPropertyName("startDate")] public DateTime StartDate { get; set; }
        [JsonPropertyName("entryCount")] public int EntryCount { get; set; }
    }

    // ###########################################################################################
    // One "New fault" card save: the drawn area on a schematic, its headline/comment, category
    // (Note/Cosmetic/Issue), resolution state (Open/Closed) and the board labels of the
    // components marked in scope. Persisted as one entry inside its workbook's own entries.json -
    // see the WorklogManager class header for why entries live beside their workbook's index.json
    // rather than in any central file.
    //
    // Title is the entry's one-line headline and Description its longer comment. Both are written
    // by the quick card and both stay editable in the full editor - neither is legacy, and dropping
    // either as "redundant" would discard real user data.
    // Links/Comments/WorkDoneItems/Photos/Files are the full editor's own sub-lists - see
    // their own record types below. Photo/file bytes themselves are not stored here, only their
    // metadata; the files live in the entry's own "entry-<id>-files" subfolder under the workbook.
    // ###########################################################################################
    public sealed class WorklogEntryRecord
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("schematicName")] public string SchematicName { get; set; } = string.Empty;
        [JsonPropertyName("areaX")] public double AreaX { get; set; }
        [JsonPropertyName("areaY")] public double AreaY { get; set; }
        [JsonPropertyName("areaWidth")] public double AreaWidth { get; set; }
        [JsonPropertyName("areaHeight")] public double AreaHeight { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("category")] public string Category { get; set; } = "Note";
        [JsonPropertyName("state")] public string State { get; set; } = "Open";
        [JsonPropertyName("componentLabels")] public List<string> ComponentLabels { get; set; } = new();
        [JsonPropertyName("createdDate")] public DateTime CreatedDate { get; set; }

        [JsonPropertyName("links")] public List<WorklogLinkRecord> Links { get; set; } = new();
        [JsonPropertyName("comments")] public List<WorklogCommentRecord> Comments { get; set; } = new();
        [JsonPropertyName("workDoneItems")] public List<WorklogWorkDoneRecord> WorkDoneItems { get; set; } = new();
        [JsonPropertyName("photos")] public List<WorklogAttachmentRecord> Photos { get; set; } = new();
        [JsonPropertyName("files")] public List<WorklogAttachmentRecord> Files { get; set; } = new();
    }

    // ###########################################################################################
    // One row in an entry's "Links of interest" list: a free-text headline plus the URL it points
    // to.
    // ###########################################################################################
    public sealed class WorklogLinkRecord
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("headline")] public string Headline { get; set; } = string.Empty;
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    }

    // ###########################################################################################
    // One row in an entry's "Comments" list. Every comment is user-added; the entry itself starts
    // with none.
    // ###########################################################################################
    public sealed class WorklogCommentRecord
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("date")] public DateTime Date { get; set; }
    }

    // ###########################################################################################
    // One row in an entry's "Work done" list: a dated note plus the time spent and cost, so the
    // editor can show a running total across every row.
    // ###########################################################################################
    public sealed class WorklogWorkDoneRecord
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("date")] public DateTime Date { get; set; }
        [JsonPropertyName("hoursSpent")] public double HoursSpent { get; set; }
        [JsonPropertyName("cost")] public double Cost { get; set; }
    }

    // ###########################################################################################
    // One row in an entry's "Photos/images" or "Files" list: the attached file's own name (as
    // stored in the entry's "entry-<id>-files" subfolder) plus a user comment. Photos and Files
    // use the same shape - only which list they sit in tells them apart. DisplayOrder lets photos
    // be dragged into a different order without renaming the files on disk.
    // ###########################################################################################
    public sealed class WorklogAttachmentRecord
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("fileName")] public string FileName { get; set; } = string.Empty;
        [JsonPropertyName("comment")] public string Comment { get; set; } = string.Empty;
        [JsonPropertyName("displayOrder")] public int DisplayOrder { get; set; }
    }

    // ###########################################################################################
    // Reads and writes workbooks under the local "Workbook" folder - one subfolder per workbook,
    // named after its id, each holding its own index.json. There is deliberately no central index:
    // every query scans the subfolders on disk, so there is no bookkeeping file to keep in sync or
    // go stale.
    //
    // The app has no delete-workbook feature at all: nothing here removes a folder, so the only way
    // to delete one today is by hand in the file manager.
    //
    // One consequence of having no persisted id counter: the next id is the highest numbered
    // subfolder currently on disk, plus one. So hand-deleting workbook #3 (the highest) lets the
    // next workbook created take #3 again - nothing remembers #3 was ever used. That is not
    // harmless: entry attachments live in "entry-<id>-files" folders keyed on the same reused ids
    // (see GetEntryAttachmentsFolder), so a recreated workbook can inherit a deleted one's files.
    // Worth a persisted counter in index.json before attachments actually ship.
    //
    // Purely local: this folder sits beside the settings/log files, never inside the synced "Data"
    // folder. Call Load() once at startup before any other member is used.
    // ###########################################################################################
    public static class WorklogManager
    {
        private static string _workbookRootPath = string.Empty;

        // ###########################################################################################
        // Resolves the "Workbook" folder in the user's AppData folder and points the manager at it.
        // Falls back to an unusable (empty) root silently on any failure.
        // ###########################################################################################
        public static void Load()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var directory = Path.Combine(appData, AppConfig.AppFolderName, AppConfig.WorklogFolderName);
                LoadFrom(directory);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load worklog: [{ex.Message}] - using defaults");
            }
        }

        // ###########################################################################################
        // Points the manager at an explicit workbook root folder, creating it if missing. Load()
        // resolves the real AppData location and calls this; splitting the two lets the test suite
        // point at a temporary folder instead of the user's real one.
        // Falls back to an unusable (empty) root silently on any failure.
        // ###########################################################################################
        internal static void LoadFrom(string workbookRootPath)
        {
            try
            {
                _workbookRootPath = workbookRootPath;
                Directory.CreateDirectory(_workbookRootPath);

                Logger.Info($"Worklog loaded: [{ReadAllWorkbooks().Count} workbooks] from [{_workbookRootPath}]");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load worklog: [{ex.Message}] - using defaults");
                _workbookRootPath = string.Empty;
            }
        }

        // ###########################################################################################
        // Reads every workbook subfolder's index.json under the workbook root. A subfolder with no
        // (or an unreadable) index.json is skipped rather than failing the whole read - it may be a
        // workbook that is mid-delete, or debris left behind by hand.
        // ###########################################################################################
        private static List<WorkbookRecord> ReadAllWorkbooks()
        {
            var results = new List<WorkbookRecord>();

            if (string.IsNullOrEmpty(_workbookRootPath) || !Directory.Exists(_workbookRootPath))
            {
                return results;
            }

            foreach (var folder in Directory.GetDirectories(_workbookRootPath))
            {
                string indexPath = Path.Combine(folder, AppConfig.WorklogIndexFileName);
                if (!File.Exists(indexPath))
                {
                    continue;
                }

                try
                {
                    var json = File.ReadAllText(indexPath);
                    var record = JsonSerializer.Deserialize<WorkbookRecord>(json);
                    if (record != null)
                    {
                        results.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to load workbook: [{folder}] [{ex.Message}] - skipped");
                }
            }

            return results;
        }

        // ###########################################################################################
        // Returns the id that CreateWorkbook will hand out next, for display before the workbook
        // actually exists (the "#1" preview in the create dialog).
        // ###########################################################################################
        public static int PeekNextId() => NextIdFromExistingFolders();

        // ###########################################################################################
        // The next id is one past the highest numbered subfolder currently on disk - see the class
        // header for why that means a deleted highest-id workbook's number can be reused.
        // ###########################################################################################
        private static int NextIdFromExistingFolders()
        {
            if (string.IsNullOrEmpty(_workbookRootPath) || !Directory.Exists(_workbookRootPath))
            {
                return 1;
            }

            int maxId = 0;

            foreach (var folder in Directory.GetDirectories(_workbookRootPath))
            {
                if (int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out int id) &&
                    id > maxId)
                {
                    maxId = id;
                }
            }

            return maxId + 1;
        }

        // ###########################################################################################
        // Returns the id AddEntry will hand out next for the given workbook, for display before the
        // entry actually exists (the "New fault" card's on-board "#N" badge). Same
        // highest-plus-one scheme as AddEntry itself - see its own comment - so this must stay in
        // sync with it. A workbook with no folder or no entries yet previews "#1".
        // ###########################################################################################
        public static int PeekNextEntryId(int workbookId)
        {
            string? folder = GetWorkbookFolder(workbookId);
            if (folder == null)
            {
                return 1;
            }

            var entries = ReadEntries(folder);
            return entries.Count == 0 ? 1 : entries.Max(e => e.Id) + 1;
        }

        // ###########################################################################################
        // Returns the most recently created still-open workbook for the given board key, or null
        // when that board has no open workbook (including when it has none at all, or its folder
        // was deleted). A board can accumulate several closed workbooks over time; only the
        // highest-id open one counts as "active".
        // ###########################################################################################
        public static WorkbookRecord? GetActiveWorkbookForBoard(string boardKey)
        {
            if (string.IsNullOrWhiteSpace(boardKey))
            {
                return null;
            }

            return ReadAllWorkbooks()
                .Where(w => string.Equals(w.BoardKey, boardKey, StringComparison.Ordinal) &&
                            string.Equals(w.Status, "Open", StringComparison.Ordinal))
                .OrderByDescending(w => w.Id)
                .FirstOrDefault();
        }

        // ###########################################################################################
        // Returns the most recently created workbook for the given board key regardless of its
        // status, or null when that board has none at all.
        //
        // This is the *display* lookup, deliberately separate from GetActiveWorkbookForBoard above,
        // which stays Open-only for callers that specifically need an open workbook. Resolving a
        // workbook's last outstanding entry closes it, and when the bar only ever asked for an open
        // workbook that made the whole workbook vanish from the UI the moment it was finished - it
        // looked like data loss even though everything was still on disk. The bar now shows a closed
        // workbook too, with its Closed status dot; adding a still-Open entry to it reopens it
        // through the normal RecomputeWorkbookStatus rule.
        // ###########################################################################################
        public static WorkbookRecord? GetLatestWorkbookForBoard(string boardKey)
        {
            if (string.IsNullOrWhiteSpace(boardKey))
            {
                return null;
            }

            return ReadAllWorkbooks()
                .Where(w => string.Equals(w.BoardKey, boardKey, StringComparison.Ordinal))
                .OrderByDescending(w => w.Id)
                .FirstOrDefault();
        }

        // ###########################################################################################
        // Creates a new open workbook for the given board: allocates the next id, creates its own
        // subfolder under the workbook root, and writes that workbook's index.json into it.
        // Returns null when the workbook root is unusable or the folder cannot be written.
        //
        // The empty-root guard matters as much here as in the readers: LoadFrom blanks the root on
        // any failure, and Path.Combine("", "1") yields the *relative* path "1", so without it this
        // silently created workbook folders next to the executable. Those looked fine in the bar but
        // were invisible to GetWorkbookFolder, which does guard - so every entry the user then
        // recorded was discarded with nothing but a log line.
        // ###########################################################################################
        public static WorkbookRecord? CreateWorkbook(string boardKey, string title, string note)
        {
            if (string.IsNullOrEmpty(_workbookRootPath))
            {
                Logger.Warning("Failed to create workbook: no usable workbook root folder");
                return null;
            }

            int id = NextIdFromExistingFolders();
            string folder = Path.Combine(_workbookRootPath, id.ToString(CultureInfo.InvariantCulture));

            var record = new WorkbookRecord
            {
                Id = id,
                BoardKey = boardKey,
                Title = title.Trim(),
                Note = note?.Trim() ?? string.Empty,
                Status = "Open",
                StartDate = DateTime.Now.Date,
                EntryCount = 0
            };

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to create workbook folder: [{folder}] [{ex.Message}]");
                return null;
            }

            if (!SaveWorkbook(folder, record))
            {
                return null;
            }

            Logger.Info($"Setting changed: [Worklog] created workbook [#{id}] [{record.Title}] for board [{boardKey}]");

            return record;
        }

        // ###########################################################################################
        // Resolution states that count as "closed" for a single entry. A workbook auto-closes once
        // every one of its entries is in one of these states - see RecomputeWorkbookStatus.
        //
        // An entry has just two states now, Open and Closed, matching the workbook's own vocabulary.
        // A set (rather than a plain equality check) is kept because the auto-close rule is about
        // "which states mean finished", which is the thing likeliest to grow again.
        // ###########################################################################################
        private static readonly HashSet<string> ResolvedEntryStates = new(StringComparer.Ordinal) { "Closed" };

        // ###########################################################################################
        // Reads the given workbook's entries.json (empty list when the workbook has none yet, or its
        // folder cannot be found).
        // ###########################################################################################
        public static List<WorklogEntryRecord> GetEntries(int workbookId)
        {
            string? folder = GetWorkbookFolder(workbookId);
            if (folder == null)
            {
                return new List<WorklogEntryRecord>();
            }

            return ReadEntries(folder);
        }

        private static List<WorklogEntryRecord> ReadEntries(string folder)
        {
            string entriesPath = Path.Combine(folder, AppConfig.WorklogEntriesFileName);
            if (!File.Exists(entriesPath))
            {
                return new List<WorklogEntryRecord>();
            }

            try
            {
                var json = File.ReadAllText(entriesPath);
                var entries = JsonSerializer.Deserialize<List<WorklogEntryRecord>>(json) ?? new List<WorklogEntryRecord>();

                foreach (var entry in entries)
                {
                    NormalizeEntryCollections(entry);
                    entry.State = MigrateEntryState(entry.State);
                }

                return entries;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load worklog entries: [{folder}] [{ex.Message}] - skipped");
                return new List<WorklogEntryRecord>();
            }
        }

        // ###########################################################################################
        // Maps the retired entry states onto the two that replaced them.
        //
        // Entries used to carry Pending/RuledOut/Fixed; they now carry Open/Closed, matching the
        // vocabulary a workbook already used. Without this mapping every entry saved by an older
        // build would be silently misreported: "Fixed" and "RuledOut" both counted as resolved, so
        // dropping them from ResolvedEntryStates reopens workbooks the user had finished, the
        // editor renders NEITHER state pill as selected (it only knows the new two), and saving
        // writes the retired value straight back.
        //
        // Ruled out maps to Closed rather than Open on purpose: it counted as resolved under the
        // old rule, so mapping it anywhere else would change every affected workbook's status
        // rather than preserve it. The mapping runs on read, so it costs nothing once the value has
        // been written back by any later save, and an unrecognised value is left alone for
        // RecomputeWorkbookStatus to treat as unresolved (the safe direction - a workbook stays
        // open rather than silently closing).
        // ###########################################################################################
        internal static string MigrateEntryState(string? state)
        {
            string trimmed = state?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(trimmed))
            {
                return "Open";
            }

            if (string.Equals(trimmed, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return "Open";
            }

            if (string.Equals(trimmed, "Fixed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "RuledOut", StringComparison.OrdinalIgnoreCase))
            {
                return "Closed";
            }

            return trimmed;
        }

        // ###########################################################################################
        // Replaces any null collection on a freshly deserialized entry with an empty one.
        //
        // System.Text.Json assigns straight over a property's "= new()" initializer when the JSON
        // carries an explicit null, so a hand-edited or partially-written entries.json yields an
        // entry whose ComponentLabels/Links/Comments/WorkDoneItems/Photos/Files are null despite
        // their initializers. Every consumer then dereferences them unguarded - the editor's
        // CloneEntry did so six times on the path that opens a saved entry. Fixing it here means no
        // caller has to know, and none can forget.
        // ###########################################################################################
        private static void NormalizeEntryCollections(WorklogEntryRecord entry)
        {
            entry.ComponentLabels ??= new List<string>();
            entry.Links ??= new List<WorklogLinkRecord>();
            entry.Comments ??= new List<WorklogCommentRecord>();
            entry.WorkDoneItems ??= new List<WorklogWorkDoneRecord>();
            entry.Photos ??= new List<WorklogAttachmentRecord>();
            entry.Files ??= new List<WorklogAttachmentRecord>();
        }

        // ###########################################################################################
        // Finds the id'd workbook's own subfolder by its name (the id itself - see the class header
        // for why there is no other lookup), returning null when it does not exist.
        // ###########################################################################################
        private static string? GetWorkbookFolder(int workbookId)
        {
            if (string.IsNullOrEmpty(_workbookRootPath))
            {
                return null;
            }

            string folder = Path.Combine(_workbookRootPath, workbookId.ToString(CultureInfo.InvariantCulture));
            return Directory.Exists(folder) ? folder : null;
        }

        // ###########################################################################################
        // Appends a new entry to the given workbook: allocates the next entry id (same
        // highest-plus-one scheme as workbook ids, scoped to this workbook's own entries),
        // saves entries.json, then recomputes and saves the workbook's own status/entryCount -
        // see RecomputeWorkbookStatus for the Open/Closed rule.
        // Returns the saved entry, or null when the workbook folder cannot be found or the write
        // itself failed (a full or locked disk) - in both cases nothing was persisted, so the
        // caller must not treat it as saved.
        // ###########################################################################################
        public static WorklogEntryRecord? AddEntry(
            int workbookId,
            string schematicName,
            Rect area,
            string title,
            string description,
            string category,
            string state,
            IEnumerable<string> componentLabels)
        {
            string? folder = GetWorkbookFolder(workbookId);
            if (folder == null)
            {
                Logger.Warning($"Failed to add worklog entry: workbook [#{workbookId}] folder not found");
                return null;
            }

            var entries = ReadEntries(folder);
            int nextEntryId = entries.Count == 0 ? 1 : entries.Max(e => e.Id) + 1;

            var entry = new WorklogEntryRecord
            {
                Id = nextEntryId,
                SchematicName = schematicName ?? string.Empty,
                AreaX = area.X,
                AreaY = area.Y,
                AreaWidth = area.Width,
                AreaHeight = area.Height,
                Title = title?.Trim() ?? string.Empty,
                Description = description?.Trim() ?? string.Empty,
                Category = string.IsNullOrWhiteSpace(category) ? "Note" : category,
                State = string.IsNullOrWhiteSpace(state) ? "Open" : state,
                ComponentLabels = componentLabels?.ToList() ?? new List<string>(),
                CreatedDate = DateTime.Now
            };

            entries.Add(entry);

            if (!SaveEntries(folder, entries))
            {
                // Nothing reached disk - returning the entry would tell the caller it was saved.
                return null;
            }

            RecomputeWorkbookStatus(folder, workbookId, entries);

            Logger.Info($"Setting changed: [Worklog] added entry [#{nextEntryId}] to workbook [#{workbookId}] [{entry.Category}/{entry.State}]");

            return entry;
        }

        // ###########################################################################################
        // Overwrites an existing entry in place (matched by Id) with everything the full editor can
        // change: title, description, category, state, and the Links/Comments/WorkDoneItems/Photos/
        // Files sub-lists. Then recomputes the workbook's status the same way AddEntry does, since
        // editing an entry's State is exactly how the user resolves (or reopens) it from the editor.
        // Returns false when the workbook or the entry itself cannot be found.
        // ###########################################################################################
        public static bool UpdateEntry(int workbookId, WorklogEntryRecord updatedEntry)
        {
            string? folder = GetWorkbookFolder(workbookId);
            if (folder == null)
            {
                Logger.Warning($"Failed to update worklog entry: workbook [#{workbookId}] folder not found");
                return false;
            }

            var entries = ReadEntries(folder);
            int index = entries.FindIndex(e => e.Id == updatedEntry.Id);
            if (index < 0)
            {
                Logger.Warning($"Failed to update worklog entry: entry [#{updatedEntry.Id}] not found in workbook [#{workbookId}]");
                return false;
            }

            entries[index] = updatedEntry;

            if (!SaveEntries(folder, entries))
            {
                // Nothing reached disk - reporting true here is what made the editor close looking
                // saved while the user's edits reverted on the next refresh.
                return false;
            }

            RecomputeWorkbookStatus(folder, workbookId, entries);

            Logger.Info($"Setting changed: [Worklog] updated entry [#{updatedEntry.Id}] in workbook [#{workbookId}] [{updatedEntry.Category}/{updatedEntry.State}]");

            return true;
        }

        // ###########################################################################################
        // Resolves (creating if missing) the subfolder that holds one entry's photo/file attachment
        // bytes - "entry-<id>-files" inside the entry's own workbook folder. Photo/file metadata
        // (comment, display order) lives in entries.json via WorklogAttachmentRecord; only the
        // actual bytes live here, named by WorklogAttachmentRecord.FileName.
        // Returns null when the workbook folder cannot be found.
        // ###########################################################################################
        public static string? GetEntryAttachmentsFolder(int workbookId, int entryId)
        {
            string? folder = GetWorkbookFolder(workbookId);
            if (folder == null)
            {
                return null;
            }

            string attachmentsFolder = Path.Combine(folder, $"entry-{entryId.ToString(CultureInfo.InvariantCulture)}-files");
            Directory.CreateDirectory(attachmentsFolder);
            return attachmentsFolder;
        }

        // ###########################################################################################
        // Recomputes and saves a workbook's entryCount and Open/Closed status from its current
        // entries: Closed once there is at least one entry and every one of them is Closed, Open
        // otherwise (including the no-entries case). Called after every entry is added, so a still
        // Open entry keeps (or reopens) the workbook, and closing the last outstanding entry closes
        // it.
        // ###########################################################################################
        private static void RecomputeWorkbookStatus(string folder, int workbookId, List<WorklogEntryRecord> entries)
        {
            string indexPath = Path.Combine(folder, AppConfig.WorklogIndexFileName);
            if (!File.Exists(indexPath))
            {
                return;
            }

            WorkbookRecord? record;
            try
            {
                var json = File.ReadAllText(indexPath);
                record = JsonSerializer.Deserialize<WorkbookRecord>(json);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to recompute workbook status [{folder}]: [{ex.Message}]");
                return;
            }

            if (record == null)
            {
                return;
            }

            record.EntryCount = entries.Count;
            record.Status = entries.Count > 0 && entries.All(e => ResolvedEntryStates.Contains(e.State))
                ? "Closed"
                : "Open";

            SaveWorkbook(folder, record);

            Logger.Info($"Setting changed: [Worklog] workbook [#{workbookId}] status set to [{record.Status}] ({entries.Count} entries)");
        }

        private static readonly JsonSerializerOptions SaveJsonOptions = new() { WriteIndented = true };

        // ###########################################################################################
        // Serializes a workbook's entries and writes them to its own entries.json, returning false
        // when nothing reached disk. Callers MUST propagate that: AddEntry and UpdateEntry used to
        // report success regardless, so a locked or full disk closed the editor looking saved and
        // the user then watched their edits revert on the next refresh.
        // ###########################################################################################
        private static bool SaveEntries(string folder, List<WorklogEntryRecord> entries)
        {
            string entriesPath = Path.Combine(folder, AppConfig.WorklogEntriesFileName);
            return AtomicJsonFile.Write(entriesPath, entries, SaveJsonOptions, "worklog entries");
        }

        // ###########################################################################################
        // Serializes one workbook and writes it to its own index.json, returning false when nothing
        // reached disk.
        // ###########################################################################################
        private static bool SaveWorkbook(string folder, WorkbookRecord record)
        {
            string indexPath = Path.Combine(folder, AppConfig.WorklogIndexFileName);
            return AtomicJsonFile.Write(indexPath, record, SaveJsonOptions, "workbook");
        }
    }
}
