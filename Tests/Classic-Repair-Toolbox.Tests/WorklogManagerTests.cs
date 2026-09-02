using System.Text.Json;
using Avalonia;
using Handlers.DataHandling;

namespace ClassicRepairToolbox.Tests;

// Characterisation tests for WorklogManager - the local, never-synced "Workbook" folder that backs
// the worklog bar. Same shape as UserSettingsTests: a static singleton whose LoadFrom() seam lets
// a test point it at a temporary folder instead of the user's real AppData folder. NOTHING here
// calls Load().
//
// The storage model has no central index: each workbook is its own subfolder (named after its id)
// holding its own index.json, and every query scans those subfolders fresh. That is deliberate -
// deleting a workbook is just deleting its folder, with no separate bookkeeping file that could go
// stale. One consequence, pinned down below: with no persisted id counter, the next id is simply
// the highest numbered subfolder on disk plus one, so deleting the highest-id workbook lets its
// number be reused by the next one created.
//
// The class is global mutable state, so this whole file is one xUnit collection: the tests run
// sequentially and each one re-loads a fresh workbook folder first.
[Collection("Worklog")]
public sealed class WorklogManagerTests : IDisposable
{
    private readonly TempWorkspace thisWorkspace = new();

    public void Dispose()
    {
        // Detach from the temp folder so nothing written later can reach the user's real one.
        this.LoadWorklog();
        this.thisWorkspace.Dispose();
    }

    /// <summary>Points WorklogManager at a fresh, uniquely-named workbook folder under the workspace.</summary>
    private string LoadWorklog()
    {
        string root = this.thisWorkspace.Path_("Workbook-" + Guid.NewGuid().ToString("N"));
        WorklogManager.LoadFrom(root);
        return root;
    }

    /// <summary>
    /// Creates a workbook and asserts it succeeded. CreateWorkbook returns null when the root is
    /// unusable or the write fails, which no test here sets up - so a null is a real failure worth
    /// failing loudly on, rather than something to silence with "!" at every call site.
    /// </summary>
    private static WorkbookRecord CreateWorkbook(string boardKey, string title, string note = "")
    {
        var record = WorklogManager.CreateWorkbook(boardKey, title, note);
        Assert.NotNull(record);
        return record!;
    }

    [Fact]
    public void An_empty_workbook_folder_starts_with_no_workbooks_and_id_one()
    {
        this.LoadWorklog();

        Assert.Equal(1, WorklogManager.PeekNextId());
        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));
    }

    [Fact]
    public void Creating_a_workbook_allocates_id_one_and_makes_it_the_active_workbook_for_its_board()
    {
        this.LoadWorklog();

        var record = CreateWorkbook("Commodore 64|250469", "Mr. Jensens C64", "Bought at auction");

        Assert.Equal(1, record.Id);
        Assert.Equal("Open", record.Status);
        Assert.Equal(0, record.EntryCount);
        Assert.Equal("Mr. Jensens C64", record.Title);
        Assert.Equal("Bought at auction", record.Note);

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(active);
        Assert.Equal(1, active!.Id);
    }

    [Fact]
    public void The_next_id_advances_after_each_workbook_is_created()
    {
        this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "First job", "");
        Assert.Equal(2, WorklogManager.PeekNextId());

        var second = CreateWorkbook("Commodore 64|250469", "Second job", "");
        Assert.Equal(2, second.Id);
        Assert.Equal(3, WorklogManager.PeekNextId());
    }

    [Fact]
    public void The_most_recently_created_open_workbook_is_the_active_one_for_a_board()
    {
        // A board can accumulate several workbooks over time - the highest id wins.
        this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "Older job", "");
        var newer = CreateWorkbook("Commodore 64|250469", "Newer job", "");

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");

        Assert.Equal(newer.Id, active!.Id);
        Assert.Equal("Newer job", active.Title);
    }

    [Fact]
    public void Workbooks_are_kept_apart_per_board()
    {
        this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "C64 job", "");
        CreateWorkbook("Amiga 500|A500", "Amiga job", "");

        Assert.Equal("C64 job", WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469")!.Title);
        Assert.Equal("Amiga job", WorklogManager.GetActiveWorkbookForBoard("Amiga 500|A500")!.Title);
    }

    [Fact]
    public void A_blank_board_key_yields_no_active_workbook_instead_of_throwing()
    {
        this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "C64 job", "");

        Assert.Null(WorklogManager.GetActiveWorkbookForBoard(""));
        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("   "));
    }

    [Fact]
    public void The_title_and_note_are_trimmed()
    {
        this.LoadWorklog();

        var record = CreateWorkbook("Commodore 64|250469", "  Mr. Jensens C64  ", "  Some note  ");

        Assert.Equal("Mr. Jensens C64", record.Title);
        Assert.Equal("Some note", record.Note);
    }

    [Fact]
    public void Creating_a_workbook_writes_its_own_index_file_inside_its_own_folder_named_after_its_id()
    {
        string root = this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "C64 job", "");

        string ownIndexPath = Path.Combine(root, "1", "index.json");
        Assert.True(File.Exists(ownIndexPath));
        Assert.False(File.Exists(Path.Combine(root, "index.json")), "there must be no central index file");
    }

    [Fact]
    public void A_workbook_survives_a_reload_by_being_read_back_from_its_own_folder()
    {
        string root = this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "C64 job", "Some note");

        WorklogManager.LoadFrom(root);

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(active);
        Assert.Equal("C64 job", active!.Title);
        Assert.Equal("Some note", active.Note);
        Assert.Equal(2, WorklogManager.PeekNextId());
    }

    [Fact]
    public void Deleting_a_workbook_folder_removes_it_from_the_active_lookup()
    {
        // This is the whole point of the per-workbook-folder model: there is no in-app "delete
        // workbook" feature, and none is needed - removing the folder from disk is the delete.
        string root = this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "C64 job", "");
        Assert.NotNull(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));

        Directory.Delete(Path.Combine(root, "1"), recursive: true);

        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));
    }

    [Fact]
    public void Deleting_the_highest_id_workbook_lets_its_id_be_reused()
    {
        // CURRENT BEHAVIOUR, and a deliberate consequence of having no persisted id counter: the
        // next id is only ever "highest folder on disk, plus one". Nothing remembers that #1 was
        // already used once it is gone.
        string root = this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "First job", "");
        Directory.Delete(Path.Combine(root, "1"), recursive: true);

        Assert.Equal(1, WorklogManager.PeekNextId());

        var reused = CreateWorkbook("Commodore 64|250469", "Second job", "");
        Assert.Equal(1, reused.Id);
    }

    // ---------------------------------------------------------- an unusable workbook root

    // Regression: CreateWorkbook was the one member with no empty-root guard. Path.Combine("", "1")
    // yields the RELATIVE path "1", so it used to create a workbook folder next to the executable
    // and hand back a record that looked fine in the bar - while GetWorkbookFolder, which does
    // guard, could never find it again, so every entry recorded afterwards was silently discarded.
    [Fact]
    public void Creating_a_workbook_fails_cleanly_when_the_workbook_root_is_unusable()
    {
        WorklogManager.LoadFrom(string.Empty);

        string cwdBefore = Directory.GetCurrentDirectory();

        var record = WorklogManager.CreateWorkbook("Commodore 64|250469", "C64 job", "");

        Assert.Null(record);
        Assert.False(Directory.Exists(Path.Combine(cwdBefore, "1")), "must not create a workbook folder relative to the working directory");
    }

    [Fact]
    public void The_next_id_skips_gaps_left_by_a_deleted_workbook_that_was_not_the_highest()
    {
        // Folders "1" and "3" exist (2 was deleted) - the next id must be 4, the highest plus one,
        // not 2: filling gaps would risk two different workbooks briefly sharing a folder name.
        string root = this.LoadWorklog();

        Directory.CreateDirectory(Path.Combine(root, "1"));
        Directory.CreateDirectory(Path.Combine(root, "3"));

        Assert.Equal(4, WorklogManager.PeekNextId());
    }

    [Fact]
    public void A_subfolder_with_no_index_file_is_skipped_instead_of_throwing()
    {
        // Debris, or a workbook folder created but never finished - either way it must not break
        // the board lookup for every other workbook.
        string root = this.LoadWorklog();

        Directory.CreateDirectory(Path.Combine(root, "1"));
        CreateWorkbook("Commodore 64|250469", "Real job", "");

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");

        Assert.NotNull(active);
        Assert.Equal("Real job", active!.Title);
    }

    [Fact]
    public void A_malformed_index_file_is_skipped_instead_of_throwing()
    {
        string root = this.LoadWorklog();
        Directory.CreateDirectory(Path.Combine(root, "1"));
        File.WriteAllText(Path.Combine(root, "1", "index.json"), "{ this is not json");

        Exception? thrown = Record.Exception(() =>
            WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));

        Assert.True(thrown is null);
        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));
    }

    // Regression: System.Text.Json assigns straight over a property's "= new()" initializer when
    // the JSON carries an explicit null, so these arrive null despite their initializers. Every
    // consumer dereferences them unguarded - the editor's CloneEntry did so six times, on the path
    // that opens a saved entry, before the window was ever shown.
    [Fact]
    public void Explicit_nulls_in_an_entrys_collections_are_read_back_as_empty_lists()
    {
        string root = this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job");
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>());

        string entriesPath = Path.Combine(root, workbook.Id.ToString(), "entries.json");
        File.WriteAllText(entriesPath, """
        [
          {
            "id": 1,
            "schematicName": "Sch",
            "title": "Bad cap",
            "category": "Issue",
            "state": "Open",
            "componentLabels": null,
            "links": null,
            "comments": null,
            "workDoneItems": null,
            "photos": null,
            "files": null
          }
        ]
        """);

        var entry = WorklogManager.GetEntries(workbook.Id).Single();

        Assert.NotNull(entry.ComponentLabels);
        Assert.NotNull(entry.Links);
        Assert.NotNull(entry.Comments);
        Assert.NotNull(entry.WorkDoneItems);
        Assert.NotNull(entry.Photos);
        Assert.NotNull(entry.Files);
        Assert.Empty(entry.Comments);
    }

    [Fact]
    public void The_written_index_file_is_valid_indented_json()
    {
        string root = this.LoadWorklog();

        CreateWorkbook("Commodore 64|250469", "C64 job", "");

        string json = File.ReadAllText(Path.Combine(root, "1", "index.json"));
        Assert.Contains("\n", json);
        Exception? thrown = Record.Exception(() => JsonDocument.Parse(json));
        Assert.True(thrown is null, "the index file must stay parseable");
    }

    [Fact]
    public void A_missing_workbook_root_folder_is_created_on_load()
    {
        string root = this.thisWorkspace.Path_("Workbook-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(root));

        WorklogManager.LoadFrom(root);

        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void Adding_an_entry_writes_it_to_the_workbooks_own_entries_file()
    {
        string root = this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var entry = WorklogManager.AddEntry(
            workbook.Id,
            "Schematic 1",
            new Rect(10, 20, 30, 40),
            "  Bad cap  ",
            "  Leaking electrolyte  ",
            "Issue",
            "Open",
            new[] { "C12", "R4" });

        Assert.NotNull(entry);
        Assert.Equal(1, entry!.Id);
        Assert.Equal("Schematic 1", entry.SchematicName);
        Assert.Equal(10, entry.AreaX);
        Assert.Equal(20, entry.AreaY);
        Assert.Equal(30, entry.AreaWidth);
        Assert.Equal(40, entry.AreaHeight);
        Assert.Equal("Bad cap", entry.Title);
        Assert.Equal("Leaking electrolyte", entry.Description);
        Assert.Equal(new[] { "C12", "R4" }, entry.ComponentLabels);

        Assert.True(File.Exists(Path.Combine(root, workbook.Id.ToString(), "entries.json")));

        var storedEntries = WorklogManager.GetEntries(workbook.Id);
        Assert.Single(storedEntries);
        Assert.Equal("Bad cap", storedEntries[0].Title);
    }

    [Fact]
    public void Entry_ids_increment_independently_per_workbook()
    {
        this.LoadWorklog();
        var first = CreateWorkbook("Commodore 64|250469", "First job", "");
        var second = CreateWorkbook("Amiga 500|A500", "Second job", "");

        WorklogManager.AddEntry(first.Id, "Sch", new Rect(0, 0, 1, 1), "A", "", "Note", "Open", Array.Empty<string>());
        var secondEntryInFirst = WorklogManager.AddEntry(first.Id, "Sch", new Rect(0, 0, 1, 1), "B", "", "Note", "Open", Array.Empty<string>());
        var firstEntryInSecond = WorklogManager.AddEntry(second.Id, "Sch", new Rect(0, 0, 1, 1), "C", "", "Note", "Open", Array.Empty<string>());

        Assert.Equal(2, secondEntryInFirst!.Id);
        Assert.Equal(1, firstEntryInSecond!.Id);
    }

    // ---------------------------------------------------------- the "New fault" card's preview badge

    [Fact]
    public void Peek_next_entry_id_previews_1_for_a_workbook_with_no_entries_yet()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        Assert.Equal(1, WorklogManager.PeekNextEntryId(workbook.Id));
    }

    [Fact]
    public void Peek_next_entry_id_matches_what_AddEntry_actually_assigns_next()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "First", "", "Note", "Open", Array.Empty<string>());

        int previewed = WorklogManager.PeekNextEntryId(workbook.Id);
        var secondEntry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Second", "", "Note", "Open", Array.Empty<string>());

        Assert.Equal(2, previewed);
        Assert.Equal(previewed, secondEntry!.Id);
    }

    [Fact]
    public void Peek_next_entry_id_is_scoped_to_its_own_workbook_not_the_workbooks_own_id()
    {
        this.LoadWorklog();
        var first = CreateWorkbook("Commodore 64|250469", "First job", "");
        var second = CreateWorkbook("Amiga 500|A500", "Second job", "");
        WorklogManager.AddEntry(first.Id, "Sch", new Rect(0, 0, 1, 1), "A", "", "Note", "Open", Array.Empty<string>());

        // Regression: the "New fault" card's badge used to display the workbook's own id instead of
        // the entry's, so every entry added to workbook #1 previewed as "#1" no matter how many
        // entries it already had.
        Assert.Equal(2, WorklogManager.PeekNextEntryId(first.Id));
        Assert.Equal(1, WorklogManager.PeekNextEntryId(second.Id));
    }

    [Fact]
    public void Peek_next_entry_id_previews_1_for_a_workbook_that_does_not_exist()
    {
        this.LoadWorklog();

        Assert.Equal(1, WorklogManager.PeekNextEntryId(999));
    }

    [Fact]
    public void Adding_an_open_entry_keeps_the_workbook_open()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Issue", "Open", Array.Empty<string>());

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(active);
        Assert.Equal("Open", active!.Status);
        Assert.Equal(1, active.EntryCount);
    }

    [Fact]
    public void A_workbook_auto_closes_once_its_only_entry_is_closed()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Issue", "Closed", Array.Empty<string>());

        // A Closed workbook is no longer "active" - GetActiveWorkbookForBoard only returns Open ones.
        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));

        var entries = WorklogManager.GetEntries(workbook.Id);
        Assert.Single(entries);
    }

    // An entry state the app never writes and cannot migrate must not silently count as resolved:
    // closing a workbook the user still considers open is the one direction of this rule that
    // loses information. The retired states are NOT in this list - they have a defined mapping and
    // are covered by the migration tests below.
    [Theory]
    [InlineData("Whatever")]
    [InlineData("half-done")]
    public void An_unrecognised_entry_state_does_not_close_a_workbook(string state)
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Issue", state, Array.Empty<string>());

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(active);
        Assert.Equal("Open", active!.Status);
    }

    // ---------------------------------------------------------------- retired state migration

    // Entries written by an older build carry Pending/RuledOut/Fixed. They are mapped on read, so
    // an upgraded user sees the state they left behind rather than a blank pill and a workbook
    // that silently reopened.
    //
    // RuledOut maps to Closed, not Open: it counted as resolved under the old rule, so any other
    // mapping would CHANGE a workbook's status instead of preserving it.
    [Theory]
    [InlineData("Pending", "Open")]
    [InlineData("Fixed", "Closed")]
    [InlineData("RuledOut", "Closed")]
    [InlineData("pending", "Open")]
    [InlineData("ruledout", "Closed")]
    public void A_retired_entry_state_is_migrated_to_its_replacement(string stored, string expected)
    {
        Assert.Equal(expected, WorklogManager.MigrateEntryState(stored));
    }

    // The current values and anything unknown pass through untouched - the migration must not
    // rewrite a state it does not own.
    [Theory]
    [InlineData("Open")]
    [InlineData("Closed")]
    [InlineData("Whatever")]
    public void A_current_or_unknown_entry_state_is_left_alone(string state)
    {
        Assert.Equal(state, WorklogManager.MigrateEntryState(state));
    }

    // A blank state has always meant Open (AddEntry defaults it), so the migration agrees.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_blank_entry_state_migrates_to_open(string? state)
    {
        Assert.Equal("Open", WorklogManager.MigrateEntryState(state));
    }

    // The end-to-end promise: a workbook closed under the old vocabulary stays closed after the
    // upgrade. This is what the migration exists for - without it RecomputeWorkbookStatus reopens
    // it the next time anything in that workbook is saved.
    [Fact]
    public void A_workbook_whose_entries_were_all_fixed_stays_closed_after_upgrading()
    {
        string root = this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Closed", Array.Empty<string>());

        // Rewrite entries.json the way the previous build would have left it.
        string entriesPath = Path.Combine(root, workbook.Id.ToString(), "entries.json");
        File.WriteAllText(entriesPath, File.ReadAllText(entriesPath).Replace("\"Closed\"", "\"Fixed\""));

        var migrated = WorklogManager.GetEntries(workbook.Id);
        Assert.Equal("Closed", Assert.Single(migrated).State);

        // And the recomputed status still reports the workbook as finished.
        WorklogManager.UpdateEntry(workbook.Id, migrated[0]);
        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));
    }

    [Fact]
    public void A_workbook_stays_open_while_any_entry_is_still_open()
    {
        // Two entries: one closed, one still open - the workbook must not close just because
        // *an* entry was closed, only once *every* entry is.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Closed one", "", "Issue", "Closed", Array.Empty<string>());
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Still open", "", "Issue", "Open", Array.Empty<string>());

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(active);
        Assert.Equal("Open", active!.Status);
        Assert.Equal(2, active.EntryCount);
    }

    [Fact]
    public void A_workbook_closes_once_its_last_outstanding_entry_is_resolved()
    {
        // Closing the first entry alone must not close the workbook (a second is still open);
        // closing the second one too must close it.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "First", "", "Issue", "Closed", Array.Empty<string>());
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Second", "", "Issue", "Closed", Array.Empty<string>());

        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));
    }

    // ---------------------------------------------------------- the bar's display-only lookup

    [Fact]
    public void The_latest_workbook_lookup_still_finds_a_workbook_that_auto_closed()
    {
        // This is the whole reason GetLatestWorkbookForBoard exists alongside the Open-only
        // GetActiveWorkbookForBoard. Resolving a workbook's last outstanding entry closes it, and
        // when the worklog bar asked only for an *open* workbook, a finished one vanished from the
        // UI completely - it read as data loss even though everything was still on disk.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Note", "Closed", Array.Empty<string>());

        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));

        var latest = WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(latest);
        Assert.Equal(workbook.Id, latest!.Id);
        Assert.Equal("Closed", latest.Status);
        Assert.Equal(1, latest.EntryCount);
    }

    [Fact]
    public void The_latest_workbook_lookup_returns_the_highest_id_regardless_of_status()
    {
        // Highest id wins even when an older workbook is the one still open - the bar shows the
        // most recent job, not the most recent *open* job.
        this.LoadWorklog();
        var older = CreateWorkbook("Commodore 64|250469", "Older job", "");
        var newer = CreateWorkbook("Commodore 64|250469", "Newer job", "");

        WorklogManager.AddEntry(older.Id, "Sch", new Rect(0, 0, 1, 1), "Still open", "", "Issue", "Open", Array.Empty<string>());
        WorklogManager.AddEntry(newer.Id, "Sch", new Rect(0, 0, 1, 1), "Done", "", "Issue", "Closed", Array.Empty<string>());

        Assert.Equal(older.Id, WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469")!.Id);
        Assert.Equal(newer.Id, WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469")!.Id);
    }

    [Fact]
    public void Adding_an_open_entry_to_a_closed_workbook_reopens_it()
    {
        // Why the worklog bar needs no separate "Reopen" affordance: "Add worklog" stays available
        // on a closed workbook, and the entry it adds is Open, which RecomputeWorkbookStatus turns
        // straight back into Open.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Done", "", "Issue", "Closed", Array.Empty<string>());
        Assert.Equal("Closed", WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469")!.Status);

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "New fault", "", "Issue", "Open", Array.Empty<string>());

        var reopened = WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469");
        Assert.Equal("Open", reopened!.Status);
        Assert.Equal(2, reopened.EntryCount);
        Assert.Equal(workbook.Id, WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469")!.Id);
    }

    [Fact]
    public void The_latest_workbook_lookup_is_scoped_to_its_board_and_rejects_a_blank_key()
    {
        this.LoadWorklog();
        CreateWorkbook("Commodore 64|250469", "C64 job", "");

        Assert.Null(WorklogManager.GetLatestWorkbookForBoard("Amiga 500|A500"));
        Assert.Null(WorklogManager.GetLatestWorkbookForBoard(""));
        Assert.Null(WorklogManager.GetLatestWorkbookForBoard("   "));
    }

    // ---------------------------------------------------------------------------------------
    // GetWorkbooksForBoard - the Workbooks tab's list. Unlike the two lookups above it does not
    // reduce a board's history to one workbook, so these pin down that it returns the whole set,
    // in the order the tab renders it.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void A_board_with_no_workbooks_lists_an_empty_set_rather_than_null()
    {
        this.LoadWorklog();

        // The tab does a .Count on this straight away to build its "N workbooks" heading, so a
        // null here would be a crash on the most ordinary case there is - a board nobody has
        // worked on yet.
        var workbooks = WorklogManager.GetWorkbooksForBoard("Commodore 64|250469");

        Assert.NotNull(workbooks);
        Assert.Empty(workbooks);
    }

    [Fact]
    public void Every_workbook_for_a_board_is_listed_newest_first()
    {
        this.LoadWorklog();

        var first = CreateWorkbook("Commodore 64|250469", "Full recap", "");
        var second = CreateWorkbook("Commodore 64|250469", "Dead PLA", "");
        var third = CreateWorkbook("Commodore 64|250469", "No picture", "");

        var workbooks = WorklogManager.GetWorkbooksForBoard("Commodore 64|250469");

        // Descending id, matching the two single-workbook lookups. The id is what the user sees
        // on each card as "#N", so a list that was not in id order would look sorted by nothing.
        Assert.Equal(
            new[] { third.Id, second.Id, first.Id },
            workbooks.Select(w => w.Id).ToArray());
    }

    [Fact]
    public void The_workbook_list_includes_closed_workbooks_not_just_open_ones()
    {
        this.LoadWorklog();

        var open = CreateWorkbook("Commodore 64|250469", "Still going", "");
        var closed = CreateWorkbook("Commodore 64|250469", "Finished", "");

        // Status is derived, not assigned: a workbook closes when every one of its entries is
        // resolved, so this closes it the way the app does rather than writing the field.
        WorklogManager.AddEntry(open.Id, "Sch", new Rect(0, 0, 1, 1), "Still looking", "", "Issue", "Open", Array.Empty<string>());
        WorklogManager.AddEntry(closed.Id, "Sch", new Rect(0, 0, 1, 1), "Sorted", "", "Issue", "Closed", Array.Empty<string>());

        var workbooks = WorklogManager.GetWorkbooksForBoard("Commodore 64|250469");

        // The tab is a history of everything done to a board, so a finished repair must stay in
        // the list - that is the whole point of it. This is the same reasoning that made
        // GetLatestWorkbookForBoard status-blind; an Open-only list would make a workbook vanish
        // the moment it was completed, which reads as data loss.
        Assert.Equal(2, workbooks.Count);
        Assert.Contains(workbooks, w => w.Id == open.Id && w.Status == "Open");
        Assert.Contains(workbooks, w => w.Id == closed.Id && w.Status == "Closed");

        // The card's third line reads "{EntryCount} worklogs", so the count has to come back on
        // the listed records rather than only on the single-workbook lookups.
        Assert.All(workbooks, w => Assert.Equal(1, w.EntryCount));
    }

    [Fact]
    public void The_workbook_list_is_scoped_to_its_board_and_rejects_a_blank_key()
    {
        this.LoadWorklog();
        CreateWorkbook("Commodore 64|250469", "C64 job", "");
        CreateWorkbook("Amiga 500|A500", "Amiga job", "");

        Assert.Single(WorklogManager.GetWorkbooksForBoard("Commodore 64|250469"));
        Assert.Single(WorklogManager.GetWorkbooksForBoard("Amiga 500|A500"));

        // A blank key yields nothing rather than everything on disk. Returning everything would
        // list one board's repairs under another board's name, which is worse than showing none.
        Assert.Empty(WorklogManager.GetWorkbooksForBoard(""));
        Assert.Empty(WorklogManager.GetWorkbooksForBoard("   "));
    }

    [Fact]
    public void Adding_an_entry_to_a_workbook_with_no_folder_returns_null_instead_of_throwing()
    {
        this.LoadWorklog();

        Exception? thrown = Record.Exception(() =>
        {
            var result = WorklogManager.AddEntry(999, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Note", "Open", Array.Empty<string>());
            Assert.Null(result);
        });

        Assert.True(thrown is null);
    }

    [Fact]
    public void An_unknown_workbooks_entries_come_back_as_an_empty_list_instead_of_throwing()
    {
        this.LoadWorklog();

        var entries = WorklogManager.GetEntries(999);

        Assert.Empty(entries);
    }

    // Was "starts it with no comments". A new entry now opens its own audit trail with the
    // automatic "Worklog created" line, so the rule this guards - what an entry's comment list
    // holds the moment it is created - is unchanged; only the expected content moved.
    [Fact]
    public void Adding_an_entry_starts_it_with_only_the_created_comment_and_a_matching_title()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>());

        Assert.Equal("Bad cap", entry!.Title);
        Assert.Single(entry.Comments);
        Assert.Equal("Worklog created", entry.Comments[0].Text);
    }

    [Fact]
    public void Updating_an_entry_overwrites_it_in_place_and_recomputes_workbook_status()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>())!;

        entry.Title = "Bad cap - replaced";
        entry.State = "Closed";
        entry.Links.Add(new WorklogLinkRecord { Id = 1, Headline = "Datasheet", Url = "https://example.com" });

        bool updated = WorklogManager.UpdateEntry(workbook.Id, entry);

        Assert.True(updated);

        var storedEntries = WorklogManager.GetEntries(workbook.Id);
        Assert.Single(storedEntries);
        Assert.Equal("Bad cap - replaced", storedEntries[0].Title);
        Assert.Equal("Closed", storedEntries[0].State);
        Assert.Single(storedEntries[0].Links);

        // Editing State to Closed is how the full editor resolves an entry - the workbook must
        // auto-close exactly as it would from the quick-card flow.
        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));
    }

    [Fact]
    public void Editing_a_resolved_entry_back_to_open_reopens_an_already_closed_workbook()
    {
        // RecomputeWorkbookStatus's doc comment promises a still-Open entry "keeps (or
        // reopens) the workbook" - this pins down the reopen half specifically, via the full
        // editor's UpdateEntry path rather than AddEntry.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Closed", Array.Empty<string>())!;

        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));

        entry.State = "Open";
        WorklogManager.UpdateEntry(workbook.Id, entry);

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(active);
        Assert.Equal("Open", active!.Status);
    }

    // Regression: the full editor instant-saves after every sub-list change (add a comment, a link,
    // a work-done row). Because UpdateEntry replaces the whole record rather than merging, such a
    // save carries the editor's working copy wholesale - so if that copy still held the title and
    // state the entry was opened with, it silently reverted whatever the user had just retyped.
    // This pins the destructive half of that: a stale-scalar update overwrites good values on disk,
    // which is why WorklogEntryEditorWindow.PersistEntrySilently must sync its direct fields first.
    [Fact]
    public void Updating_an_entry_replaces_the_whole_record_so_stale_fields_overwrite_saved_ones()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "Leaking", "Issue", "Open", Array.Empty<string>())!;

        // The user retypes the headline and resolves the entry, and it reaches disk.
        entry.Title = "Bad cap - replaced";
        entry.State = "Closed";
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, entry));

        // A later write built from a copy that never saw those edits takes the record back.
        var staleCopy = WorklogManager.GetEntries(workbook.Id).Single();
        staleCopy.Title = "Bad cap";
        staleCopy.State = "Open";
        staleCopy.Comments.Add(new WorklogCommentRecord { Id = 2, Text = "Added a note", Date = DateTime.Now });
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, staleCopy));

        var stored = WorklogManager.GetEntries(workbook.Id).Single();
        Assert.Equal("Bad cap", stored.Title);
        Assert.Equal("Open", stored.State);

        // Two: the automatic "Worklog created" comment the entry was born with, plus the one the
        // stale copy added. What matters here is that the stale write took the record back
        // wholesale, not the count itself.
        Assert.Equal(2, stored.Comments.Count);
        Assert.Equal("Added a note", stored.Comments[1].Text);

        // And the reverted state feeds straight back into the workbook's Open/Closed status.
        Assert.Equal("Open", WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469")!.Status);
    }

    // Regression: SaveEntries swallowed every write exception and returned void, so UpdateEntry
    // reported true even when nothing reached disk - the editor then closed looking saved and the
    // user watched their edits revert on the next refresh. A directory sitting where the ".tmp"
    // file must be written fails the write deterministically, with no permissions games.
    [Fact]
    public void A_failed_write_is_reported_rather_than_being_swallowed()
    {
        string root = this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job");
        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>())!;

        Directory.CreateDirectory(Path.Combine(root, workbook.Id.ToString(), "entries.json.tmp"));

        entry.Title = "Bad cap - replaced";

        Assert.False(WorklogManager.UpdateEntry(workbook.Id, entry));
        Assert.False(WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Another", "", "Note", "Open", Array.Empty<string>()) is not null);

        // The entry on disk is untouched, which is the point: the caller must not report success.
        Assert.Equal("Bad cap", WorklogManager.GetEntries(workbook.Id).Single().Title);
    }

    [Fact]
    public void Updating_an_unknown_entry_returns_false_instead_of_throwing()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var phantom = new WorklogEntryRecord { Id = 999 };

        Exception? thrown = Record.Exception(() =>
        {
            bool updated = WorklogManager.UpdateEntry(workbook.Id, phantom);
            Assert.False(updated);
        });

        Assert.True(thrown is null);
    }

    [Fact]
    public void Updating_an_entry_in_an_unknown_workbook_returns_false_instead_of_throwing()
    {
        this.LoadWorklog();

        var phantom = new WorklogEntryRecord { Id = 1 };

        Exception? thrown = Record.Exception(() =>
        {
            bool updated = WorklogManager.UpdateEntry(999, phantom);
            Assert.False(updated);
        });

        Assert.True(thrown is null);
    }

    [Fact]
    public void The_entry_attachments_folder_is_created_inside_the_workbooks_own_folder()
    {
        string root = this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        string? attachmentsFolder = WorklogManager.GetEntryAttachmentsFolder(workbook.Id, 1);

        Assert.NotNull(attachmentsFolder);
        Assert.Equal(Path.Combine(root, workbook.Id.ToString(), "entry-1-files"), attachmentsFolder);
        Assert.True(Directory.Exists(attachmentsFolder));
    }

    [Fact]
    public void The_entry_attachments_folder_for_an_unknown_workbook_returns_null_instead_of_throwing()
    {
        this.LoadWorklog();

        string? attachmentsFolder = WorklogManager.GetEntryAttachmentsFolder(999, 1);

        Assert.Null(attachmentsFolder);
    }

    // ------------------------------------------------------------- automatic "created" comment

    // A new worklog starts its own history with the fact that it was created, so its Comments list
    // is an audit trail from the first entry rather than starting empty and only recording what
    // happened afterwards.
    [Fact]
    public void A_new_entry_starts_with_a_worklog_created_comment()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>());

        Assert.NotNull(entry);
        Assert.Single(entry!.Comments);
        Assert.Equal("Worklog created", entry.Comments[0].Text);
        Assert.Equal(1, entry.Comments[0].Id);
    }

    // The comment must survive the write, not just exist on the returned object - it is the stored
    // entry the editor will later read back and show.
    [Fact]
    public void The_created_comment_is_written_to_disk()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>());

        var stored = WorklogManager.GetEntries(workbook.Id);

        Assert.Single(stored);
        Assert.Single(stored[0].Comments);
        Assert.Equal("Worklog created", stored[0].Comments[0].Text);
    }

    // Each entry gets its OWN created comment - the list is per-entry, so a second entry must not
    // inherit or share the first one.
    [Fact]
    public void Every_new_entry_gets_its_own_created_comment()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "First", "", "Note", "Open", Array.Empty<string>());
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Second", "", "Note", "Open", Array.Empty<string>());

        var stored = WorklogManager.GetEntries(workbook.Id);

        Assert.Equal(2, stored.Count);
        Assert.All(stored, e =>
        {
            Assert.Single(e.Comments);
            Assert.Equal("Worklog created", e.Comments[0].Text);
        });
    }

    // ------------------------------------------------------------- "Show marked area"

    [Fact]
    public void A_new_entry_shows_its_marked_area_by_default()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>());

        Assert.True(entry!.ShowMarkedArea);
    }

    // The setting is a normal round-tripped field: unticking it must survive the write and the
    // read back, or the area would reappear the next time the board was opened.
    [Fact]
    public void Hiding_the_marked_area_survives_a_save_and_reload()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>())!;

        entry.ShowMarkedArea = false;
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, entry));

        Assert.False(WorklogManager.GetEntries(workbook.Id).Single().ShowMarkedArea);
    }

    // THE upgrade case. An entry written by a build that predates this field has no
    // "showMarkedArea" key at all, and must read back as true - a default of false would silently
    // blank every marked area on the board for anyone upgrading.
    [Fact]
    public void An_entry_written_before_the_field_existed_still_shows_its_area()
    {
        string root = this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Open", Array.Empty<string>());

        // Strip the key back out, reproducing exactly what an older build wrote.
        string entriesPath = Path.Combine(root, workbook.Id.ToString(), "entries.json");
        string json = File.ReadAllText(entriesPath);
        Assert.Contains("showMarkedArea", json);

        using (var document = JsonDocument.Parse(json))
        {
            var stripped = document.RootElement.EnumerateArray()
                .Select(e =>
                {
                    var map = new Dictionary<string, JsonElement>();
                    foreach (var property in e.EnumerateObject())
                    {
                        if (!string.Equals(property.Name, "showMarkedArea", StringComparison.Ordinal))
                        {
                            map[property.Name] = property.Value;
                        }
                    }
                    return map;
                })
                .ToList();

            File.WriteAllText(entriesPath, JsonSerializer.Serialize(stripped));
        }

        Assert.DoesNotContain("showMarkedArea", File.ReadAllText(entriesPath));

        Assert.True(WorklogManager.GetEntries(workbook.Id).Single().ShowMarkedArea);
    }

    // ------------------------------------------------------------- "Mark components completed"

    // A new entry has nothing done yet - a component that was just put in scope is work still to
    // do, so "not started" is the only honest starting point.
    [Fact]
    public void A_new_entry_has_no_completed_components()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Recap", "", "Issue", "Open", new[] { "C1", "C2" });

        Assert.Empty(entry!.CompletedComponentLabels);
    }

    [Fact]
    public void Completed_components_survive_a_save_and_reload()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Recap", "", "Issue", "Open", new[] { "C1", "C2" })!;

        entry.CompletedComponentLabels = new List<string> { "C1" };
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, entry));

        Assert.Equal(new[] { "C1" }, WorklogManager.GetEntries(workbook.Id).Single().CompletedComponentLabels);
    }

    // An entry written before this field existed reads back with an empty list rather than null -
    // every consumer enumerates it, so a null would be a crash rather than a missing feature.
    [Fact]
    public void An_entry_written_before_the_completed_field_existed_reads_back_empty()
    {
        string root = this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Recap", "", "Issue", "Open", new[] { "C1" });

        string entriesPath = Path.Combine(root, workbook.Id.ToString(), "entries.json");
        string json = File.ReadAllText(entriesPath);

        using (var document = JsonDocument.Parse(json))
        {
            var stripped = document.RootElement.EnumerateArray()
                .Select(e =>
                {
                    var map = new Dictionary<string, JsonElement>();
                    foreach (var property in e.EnumerateObject())
                    {
                        if (!string.Equals(property.Name, "completedComponentLabels", StringComparison.Ordinal))
                        {
                            map[property.Name] = property.Value;
                        }
                    }
                    return map;
                })
                .ToList();

            File.WriteAllText(entriesPath, JsonSerializer.Serialize(stripped));
        }

        var reloaded = WorklogManager.GetEntries(workbook.Id).Single();

        Assert.NotNull(reloaded.CompletedComponentLabels);
        Assert.Empty(reloaded.CompletedComponentLabels);
    }

    // ------------------------------------------------------------- collapsed list sections

    [Fact]
    public void A_new_entry_has_no_collapsed_sections()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Recap", "", "Issue", "Open", Array.Empty<string>());

        Assert.Empty(entry!.CollapsedSections);
    }

    [Fact]
    public void Collapsed_sections_survive_a_save_and_reload()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Recap", "", "Issue", "Open", Array.Empty<string>())!;

        entry.CollapsedSections = new List<string> { "EditorCommentsHeader", "EditorPhotosHeader" };
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, entry));

        Assert.Equal(
            new[] { "EditorCommentsHeader", "EditorPhotosHeader" },
            WorklogManager.GetEntries(workbook.Id).Single().CollapsedSections);
    }

    // An entry written before the field existed reads back with an empty list rather than null -
    // the editor enumerates it on open, so a null would be a crash rather than a missing feature.
    [Fact]
    public void An_entry_written_before_the_collapsed_field_existed_reads_back_empty()
    {
        string root = this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Recap", "", "Issue", "Open", Array.Empty<string>());

        string entriesPath = Path.Combine(root, workbook.Id.ToString(), "entries.json");

        using (var document = JsonDocument.Parse(File.ReadAllText(entriesPath)))
        {
            var stripped = document.RootElement.EnumerateArray()
                .Select(e =>
                {
                    var map = new Dictionary<string, JsonElement>();
                    foreach (var property in e.EnumerateObject())
                    {
                        if (!string.Equals(property.Name, "collapsedSections", StringComparison.Ordinal))
                        {
                            map[property.Name] = property.Value;
                        }
                    }
                    return map;
                })
                .ToList();

            File.WriteAllText(entriesPath, JsonSerializer.Serialize(stripped));
        }

        var reloaded = WorklogManager.GetEntries(workbook.Id).Single();

        Assert.NotNull(reloaded.CollapsedSections);
        Assert.Empty(reloaded.CollapsedSections);
    }

    // Persisting a fold must not carry unsaved direct-field edits with it. The editor writes the
    // fold onto the STORED record rather than its working copy, so a pending Description change or
    // an unticked "Show marked area" - fields the user can still abandon with Cancel - stay off
    // disk. This models that write and pins the rule the editor depends on.
    [Fact]
    public void Writing_a_fold_onto_the_stored_record_leaves_other_fields_untouched()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Recap", "Original description", "Issue", "Open", Array.Empty<string>())!;

        // A working copy carrying edits the user has NOT saved.
        var workingCopy = WorklogManager.GetEntries(workbook.Id).Single();
        workingCopy.Description = "Edited but not saved";
        workingCopy.ShowMarkedArea = false;

        // The fold is written onto a freshly read record, not onto that working copy.
        var stored = WorklogManager.GetEntries(workbook.Id).Single();
        stored.CollapsedSections = new List<string> { "EditorCommentsHeader" };
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, stored));

        var reloaded = WorklogManager.GetEntries(workbook.Id).Single();

        Assert.Equal(new[] { "EditorCommentsHeader" }, reloaded.CollapsedSections);
        Assert.Equal("Original description", reloaded.Description);
        Assert.True(reloaded.ShowMarkedArea);
        Assert.Equal(entry.Id, reloaded.Id);
    }

    // ------------------------------------------------- "which workbook is active" (pure)

    // The rule Main's worklog bar, "Show worklogs", "Add worklog" AND the Workbooks tab's
    // highlighted card all resolve through. It used to be written out twice, in two different
    // shapes, which is how the highlighted card and the bar came to be able to disagree.
    //
    // Pure, so these need no workbook folders on disk - which is the point of extracting it.
    [Fact]
    public void The_saved_active_workbook_wins_over_the_newest_one()
    {
        // Newest-first, matching GetWorkbooksForBoard's own order.
        var workbooks = new List<WorkbookRecord>
        {
            new() { Id = 3, Title = "Newest" },
            new() { Id = 2, Title = "Middle" },
            new() { Id = 1, Title = "Oldest" },
        };

        Assert.Equal(1, WorklogManager.ResolveActiveWorkbook(workbooks, 1)!.Id);
        Assert.Equal(2, WorklogManager.ResolveActiveWorkbook(workbooks, 2)!.Id);
    }

    // Nothing saved: the newest, which is what every worklog surface defaulted to before workbooks
    // could be activated at all.
    [Fact]
    public void With_no_saved_activation_the_newest_workbook_is_active()
    {
        var workbooks = new List<WorkbookRecord>
        {
            new() { Id = 3, Title = "Newest" },
            new() { Id = 1, Title = "Oldest" },
        };

        Assert.Equal(3, WorklogManager.ResolveActiveWorkbook(workbooks, null)!.Id);
    }

    // A saved id naming a workbook this board no longer has - the folder deleted by hand, or the id
    // left over from another board - must fall back rather than resolve to nothing. Returning null
    // here would make the worklog bar quietly show "no workbook" for a board that plainly has some.
    [Fact]
    public void A_saved_id_that_names_no_workbook_falls_back_to_the_newest()
    {
        var workbooks = new List<WorkbookRecord>
        {
            new() { Id = 3, Title = "Newest" },
            new() { Id = 1, Title = "Oldest" },
        };

        Assert.Equal(3, WorklogManager.ResolveActiveWorkbook(workbooks, 9999)!.Id);
    }

    [Fact]
    public void A_board_with_no_workbooks_has_no_active_workbook()
    {
        Assert.Null(WorklogManager.ResolveActiveWorkbook(new List<WorkbookRecord>(), null));
        Assert.Null(WorklogManager.ResolveActiveWorkbook(new List<WorkbookRecord>(), 1));
    }

    // ------------------------------------------------------- "which states mean finished"

    [Fact]
    public void Closed_is_the_one_resolved_entry_state()
    {
        Assert.True(WorklogManager.IsResolvedState("Closed"));
        Assert.False(WorklogManager.IsResolvedState("Open"));
    }

    // Case- and whitespace-insensitive, unlike the ResolvedEntryStates set it consults. States are
    // read back off disk and can carry a hand edit or an older build's casing; falling through to
    // "open" there draws a RED padlock on a pill whose own label reads "closed", which is
    // indistinguishable from the intended default and so would never be noticed.
    [Theory]
    [InlineData("closed")]
    [InlineData("CLOSED")]
    [InlineData(" Closed ")]
    public void A_differently_cased_closed_state_still_reads_as_resolved(string state)
    {
        Assert.True(WorklogManager.IsResolvedState(state));
    }

    // Anything unrecognised reads as unresolved - the safe direction, since it leaves a workbook
    // open rather than silently closing it.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Pending")]
    [InlineData("SomeFutureState")]
    public void An_unrecognised_state_is_not_resolved(string? state)
    {
        Assert.False(WorklogManager.IsResolvedState(state));
    }

    // The workbook axis, not the entry one - RecomputeWorkbookStatus writes this field. Same
    // case-insensitive read, same "anything unrecognised reads as open" fallback every status pill
    // in the app already applies.
    [Theory]
    [InlineData("Open", true)]
    [InlineData("open", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("Closed", false)]
    [InlineData("closed", false)]
    [InlineData(" Closed ", false)]
    public void A_workbook_status_reads_as_open_unless_it_is_recognisably_closed(string? status, bool expected)
    {
        Assert.Equal(expected, WorklogManager.IsWorkbookStatusOpen(status));
    }

    // The auto-close rule goes through the same IsResolvedState, so an entry stored as "closed"
    // closes its workbook rather than holding it open forever while its own pill reads Closed.
    [Fact]
    public void A_differently_cased_closed_entry_still_closes_its_workbook()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var entry = WorklogManager.AddEntry(
            workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Done", "", "Issue", "Open", Array.Empty<string>())!;

        Assert.Equal("Open", WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469")!.Status);

        // The casing a hand edit of entries.json, or an older build, could leave behind.
        entry.State = "closed";
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, entry));

        Assert.Equal("Closed", WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469")!.Status);
    }
}
