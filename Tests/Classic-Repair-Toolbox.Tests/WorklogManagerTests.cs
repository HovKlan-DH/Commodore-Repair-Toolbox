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
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Pending", Array.Empty<string>());

        string entriesPath = Path.Combine(root, workbook.Id.ToString(), "entries.json");
        File.WriteAllText(entriesPath, """
        [
          {
            "id": 1,
            "schematicName": "Sch",
            "title": "Bad cap",
            "category": "Issue",
            "state": "Pending",
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
            "Pending",
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

        WorklogManager.AddEntry(first.Id, "Sch", new Rect(0, 0, 1, 1), "A", "", "Note", "Pending", Array.Empty<string>());
        var secondEntryInFirst = WorklogManager.AddEntry(first.Id, "Sch", new Rect(0, 0, 1, 1), "B", "", "Note", "Pending", Array.Empty<string>());
        var firstEntryInSecond = WorklogManager.AddEntry(second.Id, "Sch", new Rect(0, 0, 1, 1), "C", "", "Note", "Pending", Array.Empty<string>());

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
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "First", "", "Note", "Pending", Array.Empty<string>());

        int previewed = WorklogManager.PeekNextEntryId(workbook.Id);
        var secondEntry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Second", "", "Note", "Pending", Array.Empty<string>());

        Assert.Equal(2, previewed);
        Assert.Equal(previewed, secondEntry!.Id);
    }

    [Fact]
    public void Peek_next_entry_id_is_scoped_to_its_own_workbook_not_the_workbooks_own_id()
    {
        this.LoadWorklog();
        var first = CreateWorkbook("Commodore 64|250469", "First job", "");
        var second = CreateWorkbook("Amiga 500|A500", "Second job", "");
        WorklogManager.AddEntry(first.Id, "Sch", new Rect(0, 0, 1, 1), "A", "", "Note", "Pending", Array.Empty<string>());

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
    public void Adding_a_pending_entry_keeps_the_workbook_open()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Issue", "Pending", Array.Empty<string>());

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(active);
        Assert.Equal("Open", active!.Status);
        Assert.Equal(1, active.EntryCount);
    }

    [Fact]
    public void A_workbook_auto_closes_once_its_only_entry_is_fixed()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Issue", "Fixed", Array.Empty<string>());

        // A Closed workbook is no longer "active" - GetActiveWorkbookForBoard only returns Open ones.
        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));

        var entries = WorklogManager.GetEntries(workbook.Id);
        Assert.Single(entries);
    }

    [Fact]
    public void A_workbook_auto_closes_once_its_only_entry_is_ruled_out()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Note", "RuledOut", Array.Empty<string>());

        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));
    }

    [Fact]
    public void A_workbook_stays_open_while_any_entry_is_still_pending()
    {
        // Two entries: one resolved, one still pending - the workbook must not close just because
        // *an* entry was resolved, only once *every* entry is.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Fixed one", "", "Issue", "Fixed", Array.Empty<string>());
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Still open", "", "Issue", "Pending", Array.Empty<string>());

        var active = WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469");
        Assert.NotNull(active);
        Assert.Equal("Open", active!.Status);
        Assert.Equal(2, active.EntryCount);
    }

    [Fact]
    public void A_workbook_closes_once_its_last_outstanding_entry_is_resolved()
    {
        // Resolving the first entry alone must not close the workbook (a second is still pending);
        // resolving the second one too must close it.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "First", "", "Issue", "Fixed", Array.Empty<string>());
        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Second", "", "Issue", "RuledOut", Array.Empty<string>());

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

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Note", "RuledOut", Array.Empty<string>());

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

        WorklogManager.AddEntry(older.Id, "Sch", new Rect(0, 0, 1, 1), "Still open", "", "Issue", "Pending", Array.Empty<string>());
        WorklogManager.AddEntry(newer.Id, "Sch", new Rect(0, 0, 1, 1), "Done", "", "Issue", "Fixed", Array.Empty<string>());

        Assert.Equal(older.Id, WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469")!.Id);
        Assert.Equal(newer.Id, WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469")!.Id);
    }

    [Fact]
    public void Adding_a_pending_entry_to_a_closed_workbook_reopens_it()
    {
        // Why the worklog bar needs no separate "Reopen" affordance: "Add entry" stays available on
        // a closed workbook, and the entry it adds is Pending, which RecomputeWorkbookStatus turns
        // straight back into Open.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Done", "", "Issue", "Fixed", Array.Empty<string>());
        Assert.Equal("Closed", WorklogManager.GetLatestWorkbookForBoard("Commodore 64|250469")!.Status);

        WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "New fault", "", "Issue", "Pending", Array.Empty<string>());

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

    [Fact]
    public void Adding_an_entry_to_a_workbook_with_no_folder_returns_null_instead_of_throwing()
    {
        this.LoadWorklog();

        Exception? thrown = Record.Exception(() =>
        {
            var result = WorklogManager.AddEntry(999, "Sch", new Rect(0, 0, 1, 1), "Desc", "", "Note", "Pending", Array.Empty<string>());
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

    [Fact]
    public void Adding_an_entry_starts_it_with_no_comments_and_a_matching_title()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");

        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Pending", Array.Empty<string>());

        Assert.Equal("Bad cap", entry!.Title);
        Assert.Empty(entry.Comments);
    }

    [Fact]
    public void Updating_an_entry_overwrites_it_in_place_and_recomputes_workbook_status()
    {
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Pending", Array.Empty<string>())!;

        entry.Title = "Bad cap - replaced";
        entry.State = "Fixed";
        entry.Links.Add(new WorklogLinkRecord { Id = 1, Headline = "Datasheet", Url = "https://example.com" });

        bool updated = WorklogManager.UpdateEntry(workbook.Id, entry);

        Assert.True(updated);

        var storedEntries = WorklogManager.GetEntries(workbook.Id);
        Assert.Single(storedEntries);
        Assert.Equal("Bad cap - replaced", storedEntries[0].Title);
        Assert.Equal("Fixed", storedEntries[0].State);
        Assert.Single(storedEntries[0].Links);

        // Editing State to Fixed is how the full editor resolves an entry - the workbook must
        // auto-close exactly as it would from the quick-card flow.
        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));
    }

    [Fact]
    public void Editing_a_resolved_entry_back_to_pending_reopens_an_already_closed_workbook()
    {
        // RecomputeWorkbookStatus's doc comment promises a still-Pending entry "keeps (or
        // reopens) the workbook" - this pins down the reopen half specifically, via the full
        // editor's UpdateEntry path rather than AddEntry.
        this.LoadWorklog();
        var workbook = CreateWorkbook("Commodore 64|250469", "C64 job", "");
        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Fixed", Array.Empty<string>())!;

        Assert.Null(WorklogManager.GetActiveWorkbookForBoard("Commodore 64|250469"));

        entry.State = "Pending";
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
        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "Leaking", "Issue", "Pending", Array.Empty<string>())!;

        // The user retypes the headline and resolves the entry, and it reaches disk.
        entry.Title = "Bad cap - replaced";
        entry.State = "Fixed";
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, entry));

        // A later write built from a copy that never saw those edits takes the record back.
        var staleCopy = WorklogManager.GetEntries(workbook.Id).Single();
        staleCopy.Title = "Bad cap";
        staleCopy.State = "Pending";
        staleCopy.Comments.Add(new WorklogCommentRecord { Id = 1, Text = "Added a note", Date = DateTime.Now });
        Assert.True(WorklogManager.UpdateEntry(workbook.Id, staleCopy));

        var stored = WorklogManager.GetEntries(workbook.Id).Single();
        Assert.Equal("Bad cap", stored.Title);
        Assert.Equal("Pending", stored.State);
        Assert.Single(stored.Comments);

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
        var entry = WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Bad cap", "", "Issue", "Pending", Array.Empty<string>())!;

        Directory.CreateDirectory(Path.Combine(root, workbook.Id.ToString(), "entries.json.tmp"));

        entry.Title = "Bad cap - replaced";

        Assert.False(WorklogManager.UpdateEntry(workbook.Id, entry));
        Assert.False(WorklogManager.AddEntry(workbook.Id, "Sch", new Rect(0, 0, 1, 1), "Another", "", "Note", "Pending", Array.Empty<string>()) is not null);

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
}
