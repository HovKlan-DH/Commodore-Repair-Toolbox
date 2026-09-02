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

        // Whether the entry's coloured area is drawn on the Schematics tab. Defaults to true, which
        // is also what an entry written by an older build gets: the property is simply absent from
        // its JSON, so the initialiser stands and every existing worklog keeps showing its area.
        // A default of false would silently blank the board for anyone upgrading.
        //
        // An entry with this off still shows its "#N" pill - parked in the top-right corner of the
        // schematic panel rather than on the board (see ParkedBadgeGeometry). Hiding the pill too
        // would make the entry invisible and unreachable from the board.
        [JsonPropertyName("showMarkedArea")] public bool ShowMarkedArea { get; set; } = true;

        // Which of the in-scope components the user has ticked off as done - the "Mark components
        // completed" checklist, for tracking progress through a job like "replace every capacitor".
        //
        // Always a SUBSET of ComponentLabels: the completed list offers exactly the components that
        // are in scope, so a label dropped from the scope is dropped from here too rather than
        // lingering as a completed component the entry no longer covers. Empty by default, and for
        // every entry written before this field existed - a new component is work still to do, so
        // "not started" is the only honest starting point.
        [JsonPropertyName("completedComponentLabels")] public List<string> CompletedComponentLabels { get; set; } = new();

        // Which of the editor's list sections (Links, Work done, Comments, Components in scope,
        // Components completed, Photos, Files) the user has folded away, keyed by section name.
        //
        // A map of only the COLLAPSED sections rather than a flag per list: sections are expanded by
        // default, so an absent key means "open" and an entry written before this existed opens with
        // everything showing. It also means adding an eighth list later needs no schema change and
        // no migration - the new section simply has no key yet.
        [JsonPropertyName("collapsedSections")] public List<string> CollapsedSections { get; set; } = new();
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
        // Returns EVERY workbook recorded for the given board key, newest first, or an empty list
        // when the board has none.
        //
        // The two lookups below each collapse a board's history to a single workbook - the active
        // one, or the latest one - because the worklog bar has room for exactly one. The Workbooks
        // tab lists the whole history instead, so it needs the unreduced set. Ordering matches
        // theirs (descending id, so newest first) rather than by date: the id is what the user sees
        // on each card as "#12", and a list numbered 12, 11, 9 that was not in that order would
        // look sorted by nothing at all.
        //
        // An empty or unknown board key yields an empty list rather than every workbook on disk.
        // Returning everything would show one board's repairs under another board's name, which is
        // worse than showing nothing.
        // ###########################################################################################
        public static List<WorkbookRecord> GetWorkbooksForBoard(string boardKey)
        {
            if (string.IsNullOrWhiteSpace(boardKey))
            {
                return new List<WorkbookRecord>();
            }

            return ReadAllWorkbooks()
                .Where(w => string.Equals(w.BoardKey, boardKey, StringComparison.Ordinal))
                .OrderByDescending(w => w.Id)
                .ToList();
        }

        // ###########################################################################################
        // Returns EVERY workbook on disk, for EVERY board, newest first - the one place a caller
        // genuinely wants workbooks that are not scoped to "the current board" (the worklog bar's
        // picker, so a workbook for a different board can be selected without a trip to that board
        // first). Everywhere else in the app - the Workbooks tab, GetActiveWorkbookForBoard,
        // ResolveActiveWorkbook - deliberately stays board-scoped; see GetWorkbooksForBoard's own
        // header for why an unscoped list is usually the wrong answer.
        // ###########################################################################################
        public static List<WorkbookRecord> GetAllWorkbooks() =>
            ReadAllWorkbooks().OrderByDescending(w => w.Id).ToList();

        // ###########################################################################################
        // Returns the most recently created still-open workbook for the given board key, or null
        // when that board has no open workbook (including when it has none at all, or its folder
        // was deleted). A board can accumulate several closed workbooks over time; only the
        // highest-id open one counts as "active".
        //
        // NOT the lookup any UI should use, despite the name. "Which workbook is active" is
        // ResolveActiveWorkbook above: it honours the workbook the user activated on the Workbooks
        // tab, and it is status-blind, so a finished workbook stays visible instead of vanishing
        // from the UI the moment its last entry is resolved. Nothing in the app calls this any
        // more - it is kept as a query the WorklogManager tests use to assert the OPEN/CLOSED
        // bookkeeping RecomputeWorkbookStatus does. Calling it from a new worklog feature would
        // silently reintroduce "newest open one, ignore what the user activated".
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
        // Status-blind, deliberately unlike GetActiveWorkbookForBoard above: resolving a workbook's
        // last outstanding entry closes it, and when the bar only ever asked for an OPEN workbook
        // that made the whole workbook vanish from the UI the moment it was finished - it looked
        // like data loss even though everything was still on disk.
        //
        // NOT the lookup any UI should use either, and for the opposite reason to the one above:
        // "newest wins, always" is exactly what activating an older or closed workbook on the
        // Workbooks tab is meant to override. Every former caller (the worklog bar, "Show worklogs",
        // "Add worklog") now goes through ResolveActiveWorkbook. Kept only as the query the
        // WorklogManager tests use to read a board's newest workbook back off disk; calling it from
        // a new worklog feature would silently ignore what the user activated.
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
        // Picks the ONE workbook every worklog-facing surface acts on for a board: the worklog bar,
        // "Show worklogs", "Add worklog", and the Workbooks tab's highlighted card and board pane.
        //
        // The saved id (UserSettings.ActiveWorkbookIdByBoard, set when the user clicks a card in the
        // Workbooks tab) wins whenever it still names a workbook this board actually has; otherwise
        // the board's newest, which is what everything defaulted to before workbooks could be
        // activated at all. The saved id is validated on every call rather than trusted: a workbook
        // folder can be deleted by hand, and an unvalidated id would leave the bar quietly showing
        // nothing instead of falling back.
        //
        // Pure, and takes its two inputs rather than reading them: Main and TabWorkbooks each had
        // their own copy of this rule with different shapes (one returning the record, one the id),
        // which is precisely the disagreement between the highlighted card and the bar that the
        // design was supposed to prevent. One implementation, unit-testable, called from both.
        //
        // "workbooks" is expected newest-first, which is GetWorkbooksForBoard's own order.
        // ###########################################################################################
        public static WorkbookRecord? ResolveActiveWorkbook(IReadOnlyList<WorkbookRecord> workbooks, int? savedActiveId)
        {
            if (workbooks == null || workbooks.Count == 0)
            {
                return null;
            }

            if (savedActiveId.HasValue)
            {
                var saved = workbooks.FirstOrDefault(w => w.Id == savedActiveId.Value);
                if (saved != null)
                {
                    return saved;
                }
            }

            return workbooks[0];
        }

        // ###########################################################################################
        // Whether an entry state counts as finished. The ONE place that question is answered, so a
        // second resolved state added to ResolvedEntryStates cannot auto-close a workbook while
        // pills, padlocks and colours elsewhere still draw its entries as open.
        //
        // Case-insensitive, unlike the set it consults: the set decides the auto-close rule against
        // states this app wrote itself, while this is asked about states read back off disk, which
        // can carry "closed" from a hand edit or an older build. Falling through to "open" there
        // renders a red padlock on a pill whose own label reads "closed", and is indistinguishable
        // from the intended default - so it would never be noticed.
        // ###########################################################################################
        public static bool IsResolvedState(string? state) =>
            !string.IsNullOrWhiteSpace(state) &&
            ResolvedEntryStates.Any(resolved => string.Equals(resolved, state.Trim(), StringComparison.OrdinalIgnoreCase));

        // ###########################################################################################
        // Whether a WORKBOOK's status counts as open - the other axis, the one RecomputeWorkbookStatus
        // writes. Separate from IsResolvedState above because they answer different questions about
        // different fields, even though both read "Open"/"Closed" to the user.
        //
        // Anything that is not recognisably "Closed" reads as open, matching how every status pill in
        // the app already falls back, and case-insensitively for the same reason IsResolvedState is:
        // a status read back off disk can carry other casing.
        // ###########################################################################################
        public static bool IsWorkbookStatusOpen(string? status) =>
            !string.Equals(status?.Trim(), "Closed", StringComparison.OrdinalIgnoreCase);

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
        // Overwrites an existing workbook's title/note in place (its id, board key, status, start
        // date and entry count are untouched - this only edits the two fields the create dialog
        // collects, reused for "Edit workbook" via CreateWorkbookWindow.InitializeForEdit).
        //
        // Returns the SAVED record, or null when the workbook cannot be found or the write itself
        // failed - the same shape CreateWorkbook returns, and for the same reason: the caller needs
        // the record that actually reached disk. Returning a bool instead left the caller patching up
        // its own in-memory copy field by field, re-applying the trim rules a second time, so the two
        // could drift apart the moment either side's trimming changed.
        // ###########################################################################################
        public static WorkbookRecord? UpdateWorkbook(int workbookId, string title, string note)
        {
            string? folder = GetWorkbookFolder(workbookId);
            if (folder == null)
            {
                Logger.Warning($"Failed to update workbook: [#{workbookId}] folder not found");
                return null;
            }

            string indexPath = Path.Combine(folder, AppConfig.WorklogIndexFileName);
            WorkbookRecord? record;
            try
            {
                var json = File.ReadAllText(indexPath);
                record = JsonSerializer.Deserialize<WorkbookRecord>(json);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to update workbook [#{workbookId}]: [{ex.Message}]");
                return null;
            }

            if (record == null)
            {
                return null;
            }

            record.Title = title?.Trim() ?? string.Empty;
            record.Note = note?.Trim() ?? string.Empty;

            if (!SaveWorkbook(folder, record))
            {
                return null;
            }

            Logger.Info($"Setting changed: [Worklog] updated workbook [#{workbookId}] [{record.Title}]");

            return record;
        }

        // ###########################################################################################
        // Deletes a workbook entirely: its index.json, entries.json and every entry's attachment
        // subfolder all live inside its own folder (see the class header), so removing that one
        // folder removes the whole workbook - there is no separate bookkeeping entry anywhere else
        // that could be left dangling. Returns false when the workbook cannot be found or the folder
        // could not be removed (e.g. a file inside it is locked open elsewhere).
        // ###########################################################################################
        public static bool DeleteWorkbook(int workbookId)
        {
            string? folder = GetWorkbookFolder(workbookId);
            if (folder == null)
            {
                Logger.Warning($"Failed to delete workbook: [#{workbookId}] folder not found");
                return false;
            }

            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to delete workbook [#{workbookId}]: [{ex.Message}]");
                return false;
            }

            Logger.Info($"Setting changed: [Worklog] deleted workbook [#{workbookId}]");

            return true;
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
        // The automatic comments the app writes into an entry's own Comments list when something
        // worth recording happens to it: it was created, its state was flipped, or its category was
        // changed. They read as an audit trail beside the user's own comments, so a worklog explains
        // its own history rather than only showing its current state.
        //
        // The wording lives here rather than at the two call sites (the quick create card and the
        // full editor) so the phrasing cannot drift apart between them, and so the tests assert the
        // same strings the app writes.
        // ###########################################################################################
        public const string CreatedCommentText = "Worklog created";

        public const string OpenedCommentText = "Worklog opened";

        public const string ClosedCommentText = "Worklog closed";

        // The category is quoted, deliberately: a bare Worklog changed to Note reads as a sentence
        // that has lost a word, and the quotes mark the value as the literal category name.
        public static string BuildCategoryChangedCommentText(string category) =>
            $"Worklog changed to \"{category}\"";

        // The state comment for a state value - null for anything that is neither Open nor Closed,
        // so an unrecognised state (a value from a future build, say) records nothing rather than
        // claiming the worklog was opened.
        public static string? BuildStateChangedCommentText(string state)
        {
            if (string.Equals(state, "Open", StringComparison.Ordinal))
                return OpenedCommentText;

            if (string.Equals(state, "Closed", StringComparison.Ordinal))
                return ClosedCommentText;

            return null;
        }

        // ###########################################################################################
        // Appends an automatic comment to the given list, allocating the next free id the same way
        // the editor's own Add-comment does. Returns the record it added so a caller can show it.
        //
        // Blank text adds nothing: BuildStateChangedCommentText can decline to describe a state, and
        // an empty comment row in the list would be worse than no row at all.
        // ###########################################################################################
        public static WorklogCommentRecord? AppendAutomaticComment(List<WorklogCommentRecord> comments, string? text)
        {
            if (comments == null || string.IsNullOrWhiteSpace(text))
                return null;

            int nextId = comments.Count == 0 ? 1 : comments.Max(c => c.Id) + 1;

            var comment = new WorklogCommentRecord
            {
                Id = nextId,
                Text = text.Trim(),
                Date = DateTime.Now
            };

            comments.Add(comment);
            return comment;
        }

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
            IEnumerable<string> componentLabels,
            bool showMarkedArea = true)
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
                ShowMarkedArea = showMarkedArea,
                CreatedDate = DateTime.Now
            };

            // Every worklog starts its own history with the fact that it was created, so the
            // Comments list is an audit trail from the first entry rather than starting empty and
            // only recording what happened later.
            AppendAutomaticComment(entry.Comments, CreatedCommentText);

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
            record.Status = entries.Count > 0 && entries.All(e => IsResolvedState(e.State))
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
