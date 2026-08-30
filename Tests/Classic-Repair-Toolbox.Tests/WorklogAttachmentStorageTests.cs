using Handlers.DataHandling;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace ClassicRepairToolbox.Tests;

// Tests for WorklogAttachmentStorage - the naming and vetting decisions made when a user picks or
// drops a photo/file onto a worklog entry, extracted from WorklogEntryEditorWindow so they can be
// tested without a window, a file dialog or a drop event.
//
// The collision rules matter more than they look: attachments are stored by name in one folder per
// entry, so a naming bug does not throw, it silently overwrites a photo the user already added.
public class WorklogAttachmentStorageTests
{
    // ------------------------------------------------------------------- ValidateSourceFile

    [Fact]
    public void A_real_image_file_is_accepted()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Path_("photo.png");
        File.WriteAllText(path, "not really a png, but the extension and existence are what is checked");

        var problem = WorklogAttachmentStorage.ValidateSourceFile(path, requireDisplayableImage: true);

        Assert.Equal(WorklogAttachmentStorage.AttachmentProblem.None, problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_blank_path_reports_that_nothing_was_selected(string? path)
    {
        var problem = WorklogAttachmentStorage.ValidateSourceFile(path, requireDisplayableImage: true);

        Assert.Equal(WorklogAttachmentStorage.AttachmentProblem.NoFileSelected, problem);
    }

    // A file the user dragged in and then moved or deleted before pressing Add.
    [Fact]
    public void A_missing_file_is_reported_as_not_found()
    {
        using var workspace = new TempWorkspace();

        var problem = WorklogAttachmentStorage.ValidateSourceFile(workspace.Path_("gone.png"), requireDisplayableImage: true);

        Assert.Equal(WorklogAttachmentStorage.AttachmentProblem.FileNotFound, problem);
    }

    // The photos section draws its attachments with Avalonia's Bitmap, so a format Bitmap cannot
    // decode has to be refused up front - it would otherwise be added and then show as a blank
    // frame forever. .svg is the interesting case: ExternalTargetLauncher allows it (the OS shell
    // opens it) but the app cannot draw it, so it must be refused here.
    [Theory]
    [InlineData("notes.txt")]
    [InlineData("datasheet.pdf")]
    [InlineData("diagram.svg")]
    [InlineData("archive.zip")]
    public void A_file_the_app_cannot_draw_is_refused_for_photos(string fileName)
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Path_(fileName);
        File.WriteAllText(path, "x");

        var problem = WorklogAttachmentStorage.ValidateSourceFile(path, requireDisplayableImage: true);

        Assert.Equal(WorklogAttachmentStorage.AttachmentProblem.NotDisplayableImage, problem);
    }

    // The same file is fine for the Files section, which never tries to draw it.
    [Fact]
    public void A_non_image_is_accepted_when_a_displayable_image_is_not_required()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Path_("datasheet.pdf");
        File.WriteAllText(path, "x");

        var problem = WorklogAttachmentStorage.ValidateSourceFile(path, requireDisplayableImage: false);

        Assert.Equal(WorklogAttachmentStorage.AttachmentProblem.None, problem);
    }

    // Format is judged before existence: telling the user the type is wrong is more useful than
    // "not found" when they picked a .txt that is sitting right there.
    [Fact]
    public void A_wrong_type_is_reported_even_when_the_file_does_not_exist()
    {
        using var workspace = new TempWorkspace();

        var problem = WorklogAttachmentStorage.ValidateSourceFile(workspace.Path_("missing.txt"), requireDisplayableImage: true);

        Assert.Equal(WorklogAttachmentStorage.AttachmentProblem.NotDisplayableImage, problem);
    }

    [Fact]
    public void Every_refusal_carries_a_message()
    {
        foreach (var problem in new[]
        {
            WorklogAttachmentStorage.AttachmentProblem.NoFileSelected,
            WorklogAttachmentStorage.AttachmentProblem.FileNotFound,
            WorklogAttachmentStorage.AttachmentProblem.NotDisplayableImage
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(WorklogAttachmentStorage.DescribeProblem(problem)));
        }
    }

    // "None" is not a refusal, so it deliberately has nothing to say.
    [Fact]
    public void An_accepted_file_has_no_message()
    {
        Assert.Equal(string.Empty, WorklogAttachmentStorage.DescribeProblem(WorklogAttachmentStorage.AttachmentProblem.None));
    }

    // ------------------------------------------------------------- CopyAttachmentIntoFolder

    [Fact]
    public void Copying_puts_the_bytes_in_the_folder_under_the_stored_name()
    {
        using var workspace = new TempWorkspace();
        string source = workspace.Path_("source", "photo.png");
        File.WriteAllText(source, "image-bytes");
        string destinationFolder = workspace.Path_("entry-1-files", "placeholder");
        destinationFolder = Path.GetDirectoryName(destinationFolder)!;

        bool copied = WorklogAttachmentStorage.CopyAttachmentIntoFolder(source, destinationFolder, "stored.png");

        Assert.True(copied);
        Assert.Equal("image-bytes", File.ReadAllText(Path.Combine(destinationFolder, "stored.png")));
    }

    // The attachments folder does not exist until the first attachment is added.
    [Fact]
    public void Copying_creates_the_attachments_folder_when_it_is_missing()
    {
        using var workspace = new TempWorkspace();
        string source = workspace.Path_("photo.png");
        File.WriteAllText(source, "x");
        string destinationFolder = Path.Combine(workspace.Root, "entry-7-files");

        bool copied = WorklogAttachmentStorage.CopyAttachmentIntoFolder(source, destinationFolder, "stored.png");

        Assert.True(copied);
        Assert.True(File.Exists(Path.Combine(destinationFolder, "stored.png")));
    }

    // Reported rather than thrown, so the caller can refuse to add a row that would point at a
    // file which never landed. A row whose image can never load is worse than a refused add.
    [Fact]
    public void A_failed_copy_is_reported_rather_than_thrown()
    {
        using var workspace = new TempWorkspace();
        string destinationFolder = Path.Combine(workspace.Root, "entry-1-files");

        bool copied = WorklogAttachmentStorage.CopyAttachmentIntoFolder(
            Path.Combine(workspace.Root, "does-not-exist.png"),
            destinationFolder,
            "stored.png");

        Assert.False(copied);
        Assert.False(File.Exists(Path.Combine(destinationFolder, "stored.png")));
    }

    // Overwriting is intended now that stored names are built from the owning record's id:
    // replacing a photo's image reuses that photo's name on purpose, and the old bytes are meant
    // to go. There is no accidental-collision case left for a refusal to protect against.
    [Fact]
    public void Copying_over_an_existing_stored_name_replaces_it()
    {
        using var workspace = new TempWorkspace();
        string source = workspace.Path_("new.png");
        File.WriteAllText(source, "new-bytes");

        string destinationFolder = Path.Combine(workspace.Root, "entry-1-files");
        Directory.CreateDirectory(destinationFolder);
        File.WriteAllText(Path.Combine(destinationFolder, "stored.png"), "original-bytes");

        bool copied = WorklogAttachmentStorage.CopyAttachmentIntoFolder(source, destinationFolder, "stored.png");

        Assert.True(copied);
        Assert.Equal("new-bytes", File.ReadAllText(Path.Combine(destinationFolder, "stored.png")));
    }

    // --------------------------------------------------------------- TryReplaceAttachmentFile

    // Builds an attachments folder holding one photo, and returns the folder plus the stored name.
    private static (string Folder, string StoredName) GivenAnExistingPhoto(TempWorkspace workspace, string sourceName, int photoId)
    {
        string folder = Path.Combine(workspace.Root, "entry-1-files");
        Directory.CreateDirectory(folder);

        string storedName = WorklogAttachmentStorage.BuildStoredFileName(sourceName, WorklogAttachmentStorage.PhotoFilePrefix, photoId);
        File.WriteAllText(Path.Combine(folder, storedName), "original-bytes");

        return (folder, storedName);
    }

    // Changing the extension changes the stored name, so the previous file would be orphaned -
    // invisible to the app but taking up space forever - if it were not removed.
    [Fact]
    public void Replacing_with_a_different_extension_deletes_the_original()
    {
        using var workspace = new TempWorkspace();
        var (folder, originalName) = GivenAnExistingPhoto(workspace, "board.jpg", photoId: 3);

        string source = workspace.Path_("replacement.png");
        File.WriteAllText(source, "new-bytes");

        bool replaced = WorklogAttachmentStorage.TryReplaceAttachmentFile(
            source,
            folder,
            originalName,
            WorklogAttachmentStorage.BuildStoredFileName(source, WorklogAttachmentStorage.PhotoFilePrefix, 3),
            out string storedName);

        Assert.True(replaced);
        Assert.False(File.Exists(Path.Combine(folder, originalName)));
        Assert.Equal("new-bytes", File.ReadAllText(Path.Combine(folder, storedName)));
        Assert.Single(Directory.GetFiles(folder));
    }

    // A differently-named source with the same extension also changes the stored name.
    [Fact]
    public void Replacing_with_a_different_file_name_deletes_the_original()
    {
        using var workspace = new TempWorkspace();
        var (folder, originalName) = GivenAnExistingPhoto(workspace, "board.png", photoId: 3);

        string source = workspace.Path_("something-else.png");
        File.WriteAllText(source, "new-bytes");

        WorklogAttachmentStorage.TryReplaceAttachmentFile(
            source,
            folder,
            originalName,
            WorklogAttachmentStorage.BuildStoredFileName(source, WorklogAttachmentStorage.PhotoFilePrefix, 3),
            out string storedName);

        Assert.NotEqual(originalName, storedName);
        Assert.False(File.Exists(Path.Combine(folder, originalName)));
        Assert.Single(Directory.GetFiles(folder));
    }

    // The dangerous case: re-picking a file with the same name gives the SAME stored name, so the
    // copy overwrote it in place. Deleting "the previous file" here would delete the replacement
    // that was just written, leaving the row pointing at nothing.
    [Fact]
    public void Replacing_with_the_same_stored_name_keeps_the_new_bytes()
    {
        using var workspace = new TempWorkspace();
        var (folder, originalName) = GivenAnExistingPhoto(workspace, "board.png", photoId: 3);

        string source = workspace.Path_("elsewhere", "board.png");
        File.WriteAllText(source, "new-bytes");

        bool replaced = WorklogAttachmentStorage.TryReplaceAttachmentFile(
            source,
            folder,
            originalName,
            WorklogAttachmentStorage.BuildStoredFileName(source, WorklogAttachmentStorage.PhotoFilePrefix, 3),
            out string storedName);

        Assert.True(replaced);
        Assert.Equal(originalName, storedName);
        Assert.True(File.Exists(Path.Combine(folder, storedName)));
        Assert.Equal("new-bytes", File.ReadAllText(Path.Combine(folder, storedName)));
        Assert.Single(Directory.GetFiles(folder));
    }

    // A failed copy must leave the original alone and keep the record pointing at it, rather than
    // deleting the only file the row still has.
    [Fact]
    public void A_failed_replacement_keeps_the_original_file_and_name()
    {
        using var workspace = new TempWorkspace();
        var (folder, originalName) = GivenAnExistingPhoto(workspace, "board.png", photoId: 3);

        bool replaced = WorklogAttachmentStorage.TryReplaceAttachmentFile(
            Path.Combine(workspace.Root, "does-not-exist.png"),
            folder,
            originalName,
            "3_does-not-exist.png",
            out string storedName);

        Assert.False(replaced);
        Assert.Equal(originalName, storedName);
        Assert.Equal("original-bytes", File.ReadAllText(Path.Combine(folder, originalName)));
        Assert.Single(Directory.GetFiles(folder));
    }

    // ------------------------------------------------------------------ BuildStoredFileName

    // The id prefix is what makes the name unique by construction, and what later lets a deleted
    // photo's file be identified with confidence.
    [Fact]
    public void A_stored_name_carries_its_owner_prefix_and_id()
    {
        string name = WorklogAttachmentStorage.BuildStoredFileName(
            Path.Combine("C:", "camera", "IMG_1234.jpg"),
            WorklogAttachmentStorage.PhotoFilePrefix,
            3);

        Assert.Equal("3_IMG_1234.jpg", name);
    }

    // The bug this prevents: two photos picked from different folders that are both "IMG_1234.jpg"
    // used to land on the same stored name, and the second overwrote the first one's bytes while
    // both rows sat in the list pointing at it.
    [Fact]
    public void The_same_source_name_under_two_ids_gives_two_different_stored_names()
    {
        string first = WorklogAttachmentStorage.BuildStoredFileName("IMG_1234.jpg", WorklogAttachmentStorage.PhotoFilePrefix, 1);
        string second = WorklogAttachmentStorage.BuildStoredFileName("IMG_1234.jpg", WorklogAttachmentStorage.PhotoFilePrefix, 2);

        Assert.NotEqual(first, second);
    }

    // Photos and Files number their ids independently, so photo #3 and file #3 both exist and would
    // collide in the folder they share without the differing prefix.
    [Fact]
    public void A_photo_and_a_file_with_the_same_id_do_not_collide()
    {
        string photo = WorklogAttachmentStorage.BuildStoredFileName("notes.png", WorklogAttachmentStorage.PhotoFilePrefix, 3);
        string file = WorklogAttachmentStorage.BuildStoredFileName("notes.png", WorklogAttachmentStorage.FileFilePrefix, 3);

        Assert.NotEqual(photo, file);
    }

    // Replacing a photo's image reuses its id, so the name is stable - that is what lets the copy
    // overwrite the old bytes in place instead of orphaning them.
    [Fact]
    public void The_same_id_and_source_name_always_give_the_same_stored_name()
    {
        string first = WorklogAttachmentStorage.BuildStoredFileName("board.png", WorklogAttachmentStorage.PhotoFilePrefix, 7);
        string second = WorklogAttachmentStorage.BuildStoredFileName("board.png", WorklogAttachmentStorage.PhotoFilePrefix, 7);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void A_path_with_no_file_name_falls_back_to_a_default(string? path)
    {
        string name = WorklogAttachmentStorage.BuildStoredFileName(path, WorklogAttachmentStorage.PhotoFilePrefix, 1);

        Assert.Equal("1_attachment", name);
    }

    // A dropped file's name comes from wherever it was dragged from, so it is not assumed to be a
    // legal name here. A separator is the dangerous case: the stored name is combined into a path,
    // so leaving one in would write outside the attachments folder.
    [Fact]
    public void A_name_carrying_a_directory_separator_is_sanitized()
    {
        string name = WorklogAttachmentStorage.BuildStoredFileName("../../evil.png", WorklogAttachmentStorage.PhotoFilePrefix, 1);

        Assert.DoesNotContain("..", name);
        Assert.DoesNotContain("/", name);
        Assert.DoesNotContain("\\", name);
    }

    // ------------------------------------------------------------------ DeleteAttachmentFile

    [Fact]
    public void Deleting_removes_the_file_from_the_folder()
    {
        using var workspace = new TempWorkspace();
        string folder = Path.Combine(workspace.Root, "entry-1-files");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "1_board.png");
        File.WriteAllText(path, "bytes");

        bool deleted = WorklogAttachmentStorage.DeleteAttachmentFile(folder, "1_board.png");

        Assert.True(deleted);
        Assert.False(File.Exists(path));
    }

    // The caller's goal is that the file is gone; one already removed outside the app satisfies
    // that, and the row must still be deletable from the list.
    [Fact]
    public void Deleting_an_already_missing_file_counts_as_success()
    {
        using var workspace = new TempWorkspace();
        string folder = Path.Combine(workspace.Root, "entry-1-files");
        Directory.CreateDirectory(folder);

        Assert.True(WorklogAttachmentStorage.DeleteAttachmentFile(folder, "never-existed.png"));
    }

    // A stored name that escapes its folder (hand-edited entries.json, or a record written before
    // names were sanitized) must not delete anything outside the attachments folder.
    [Fact]
    public void Deleting_refuses_a_name_that_escapes_the_attachments_folder()
    {
        using var workspace = new TempWorkspace();
        string folder = Path.Combine(workspace.Root, "entry-1-files");
        Directory.CreateDirectory(folder);

        string outsidePath = Path.Combine(workspace.Root, "important.txt");
        File.WriteAllText(outsidePath, "must survive");

        bool deleted = WorklogAttachmentStorage.DeleteAttachmentFile(folder, Path.Combine("..", "important.txt"));

        Assert.False(deleted);
        Assert.True(File.Exists(outsidePath));
    }

    [Theory]
    [InlineData(null, "photo.png")]
    [InlineData("folder", null)]
    [InlineData("folder", "")]
    public void Deleting_with_a_missing_folder_or_name_is_refused(string? folder, string? fileName)
    {
        Assert.False(WorklogAttachmentStorage.DeleteAttachmentFile(folder, fileName));
    }

    // ------------------------------------------------------------------- ReorderAttachment

    private static List<WorklogAttachmentRecord> BuildAttachments(int count)
    {
        var attachments = new List<WorklogAttachmentRecord>();
        for (int i = 0; i < count; i++)
        {
            attachments.Add(new WorklogAttachmentRecord { Id = i + 1, FileName = $"photo{i + 1}.png", DisplayOrder = i });
        }

        return attachments;
    }

    private static string OrderOf(List<WorklogAttachmentRecord> attachments) =>
        string.Join(",", attachments.OrderBy(a => a.DisplayOrder).Select(a => a.Id));

    [Fact]
    public void Dragging_a_row_down_puts_it_at_the_dropped_position()
    {
        var attachments = BuildAttachments(4);

        WorklogAttachmentStorage.ReorderAttachment(attachments, id: 1, targetIndex: 2);

        Assert.Equal("2,3,1,4", OrderOf(attachments));
    }

    [Fact]
    public void Dragging_a_row_up_puts_it_at_the_dropped_position()
    {
        var attachments = BuildAttachments(4);

        WorklogAttachmentStorage.ReorderAttachment(attachments, id: 4, targetIndex: 1);

        Assert.Equal("1,4,2,3", OrderOf(attachments));
    }

    // A drag flung past the end means "put it last", which is what the user is asking for rather
    // than an error to discard.
    [Fact]
    public void A_target_past_the_end_lands_at_the_end()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.ReorderAttachment(attachments, id: 1, targetIndex: 99);

        Assert.Equal("2,3,1", OrderOf(attachments));
    }

    [Fact]
    public void A_negative_target_lands_at_the_start()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.ReorderAttachment(attachments, id: 3, targetIndex: -5);

        Assert.Equal("3,1,2", OrderOf(attachments));
    }

    // Dropping a row back where it started must not disturb the order.
    [Fact]
    public void Dropping_a_row_on_itself_changes_nothing()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.ReorderAttachment(attachments, id: 2, targetIndex: 1);

        Assert.Equal("1,2,3", OrderOf(attachments));
    }

    // DisplayOrder is renumbered densely from 0, so an entry whose stored orders were sparse (or
    // duplicated by an older version) comes out clean rather than preserving the gaps.
    [Fact]
    public void Reordering_renumbers_display_order_densely_from_zero()
    {
        var attachments = new List<WorklogAttachmentRecord>
        {
            new() { Id = 1, DisplayOrder = 5 },
            new() { Id = 2, DisplayOrder = 40 },
            new() { Id = 3, DisplayOrder = 40 }
        };

        WorklogAttachmentStorage.ReorderAttachment(attachments, id: 1, targetIndex: 2);

        Assert.Equal(new[] { 0, 1, 2 }, attachments.OrderBy(a => a.DisplayOrder).Select(a => a.DisplayOrder).ToArray());
    }

    [Fact]
    public void Reordering_an_unknown_id_changes_nothing()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.ReorderAttachment(attachments, id: 99, targetIndex: 0);

        Assert.Equal("1,2,3", OrderOf(attachments));
    }

    [Fact]
    public void Reordering_a_single_item_list_is_a_no_op()
    {
        var attachments = BuildAttachments(1);

        WorklogAttachmentStorage.ReorderAttachment(attachments, id: 1, targetIndex: 3);

        Assert.Equal("1", OrderOf(attachments));
    }

    // ------------------------------------------------------------------- StepAttachment

    [Fact]
    public void Stepping_up_swaps_with_the_row_above()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.StepAttachment(attachments, id: 3, direction: -1);

        Assert.Equal("1,3,2", OrderOf(attachments));
    }

    [Fact]
    public void Stepping_down_swaps_with_the_row_below()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.StepAttachment(attachments, id: 1, direction: 1);

        Assert.Equal("2,1,3", OrderOf(attachments));
    }

    // At the ends the button is inert rather than wrapping around or clamping onto itself.
    [Fact]
    public void Stepping_up_from_the_top_does_nothing()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.StepAttachment(attachments, id: 1, direction: -1);

        Assert.Equal("1,2,3", OrderOf(attachments));
    }

    [Fact]
    public void Stepping_down_from_the_bottom_does_nothing()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.StepAttachment(attachments, id: 3, direction: 1);

        Assert.Equal("1,2,3", OrderOf(attachments));
    }

    [Fact]
    public void Stepping_an_unknown_id_changes_nothing()
    {
        var attachments = BuildAttachments(3);

        WorklogAttachmentStorage.StepAttachment(attachments, id: 99, direction: 1);

        Assert.Equal("1,2,3", OrderOf(attachments));
    }

    // ---------------------------------------------------------------- NormalizeDisplayOrder

    // The state an older build could leave behind: its add path started DisplayOrder at 1 while its
    // reorder renumbered from 0, so a photo added after a reorder took an order another row already
    // held. Two rows sharing an order sort arbitrarily, so the list order could change per session.
    [Fact]
    public void Duplicate_display_orders_are_renumbered_densely()
    {
        var attachments = new List<WorklogAttachmentRecord>
        {
            new() { Id = 1, DisplayOrder = 0 },
            new() { Id = 2, DisplayOrder = 1 },
            new() { Id = 3, DisplayOrder = 1 }
        };

        bool changed = WorklogAttachmentStorage.NormalizeDisplayOrder(attachments);

        Assert.True(changed);
        Assert.Equal(new[] { 0, 1, 2 }, attachments.OrderBy(a => a.DisplayOrder).Select(a => a.DisplayOrder).ToArray());
    }

    [Fact]
    public void Gapped_display_orders_are_closed_up()
    {
        var attachments = new List<WorklogAttachmentRecord>
        {
            new() { Id = 1, DisplayOrder = 5 },
            new() { Id = 2, DisplayOrder = 40 },
            new() { Id = 3, DisplayOrder = 900 }
        };

        WorklogAttachmentStorage.NormalizeDisplayOrder(attachments);

        Assert.Equal("1,2,3", OrderOf(attachments));
        Assert.Equal(new[] { 0, 1, 2 }, attachments.OrderBy(a => a.DisplayOrder).Select(a => a.DisplayOrder).ToArray());
    }

    // Renumbering must not reshuffle rows that were already in a sensible order.
    [Fact]
    public void Normalizing_preserves_the_existing_relative_order()
    {
        var attachments = new List<WorklogAttachmentRecord>
        {
            new() { Id = 7, DisplayOrder = 2 },
            new() { Id = 4, DisplayOrder = 9 },
            new() { Id = 1, DisplayOrder = 0 }
        };

        WorklogAttachmentStorage.NormalizeDisplayOrder(attachments);

        Assert.Equal("1,7,4", OrderOf(attachments));
    }

    // Reported so the caller can skip a save it does not need.
    [Fact]
    public void An_already_dense_list_reports_no_change()
    {
        var attachments = BuildAttachments(4);

        Assert.False(WorklogAttachmentStorage.NormalizeDisplayOrder(attachments));
    }

    [Fact]
    public void Normalizing_an_empty_list_reports_no_change()
    {
        Assert.False(WorklogAttachmentStorage.NormalizeDisplayOrder(new List<WorklogAttachmentRecord>()));
    }
}
