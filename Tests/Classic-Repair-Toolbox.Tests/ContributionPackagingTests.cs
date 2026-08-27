using Handlers.DataHandling;

namespace ClassicRepairToolbox.Tests;

// Tests for ContributionPackaging - the pure half of the component contribution upload.
//
// The zip entry names this class assigns are recorded per row inside ComponentContribution.json
// (the "ZipEntry" field), and the server-side review page (Assets/Webserver/app-contribution/api)
// uses them to locate each submitted file exactly. That makes the naming scheme a wire contract:
// "ReferencedFiles/<SectionFolder>/<NNN>_<filename>" with one global running number and one shared
// entry per distinct source file. Change the scheme and queued submissions stop resolving.
public class ContributionPackagingTests
{
    // -------------------------------------------------------------- AssignZipEntries

    [Fact]
    public void Entries_are_numbered_globally_across_sections_and_carry_the_section_folder()
    {
        var plan = ContributionPackaging.AssignZipEntries(new[]
        {
            new ContributionFileReference { SectionFolder = "ComponentImages", ResolvedSourcePath = @"C:\data\pin1.png" },
            new ContributionFileReference { SectionFolder = "ComponentImages", ResolvedSourcePath = @"C:\data\pin2.png" },
            new ContributionFileReference { SectionFolder = "BoardLocalFiles", ResolvedSourcePath = @"C:\data\schematic.pdf" },
        });

        Assert.Equal(
            new[]
            {
                "ReferencedFiles/ComponentImages/001_pin1.png",
                "ReferencedFiles/ComponentImages/002_pin2.png",
                "ReferencedFiles/BoardLocalFiles/003_schematic.pdf",
            },
            plan.EntryNames);

        Assert.Equal(3, plan.Attachments.Count);
        Assert.Equal(@"C:\data\pin1.png", plan.Attachments[0].SourcePath);
        Assert.Equal("ReferencedFiles/ComponentImages/001_pin1.png", plan.Attachments[0].ZipEntryName);
    }

    // Two rows pointing at the same file must share one zip entry instead of packing the file
    // twice - and the second row's ZipEntry still tells the server exactly where the file lives,
    // even when that entry was created under another section's folder.
    [Fact]
    public void Rows_sharing_a_source_file_share_one_zip_entry()
    {
        var plan = ContributionPackaging.AssignZipEntries(new[]
        {
            new ContributionFileReference { SectionFolder = "ComponentImages", ResolvedSourcePath = @"C:\data\shared.png" },
            new ContributionFileReference { SectionFolder = "ComponentLocalFiles", ResolvedSourcePath = @"C:\DATA\SHARED.PNG" },
        });

        Assert.Single(plan.Attachments);
        Assert.Equal("ReferencedFiles/ComponentImages/001_shared.png", plan.EntryNames[0]);
        Assert.Equal("ReferencedFiles/ComponentImages/001_shared.png", plan.EntryNames[1]);
    }

    // Different files that happen to have the same file name must NOT collapse into one entry.
    // The old upload matched submitted files by bare file name on the server, which made exactly
    // this case ambiguous - the per-row ZipEntry pointer exists to fix it.
    [Fact]
    public void Same_named_files_from_different_folders_get_distinct_entries()
    {
        var plan = ContributionPackaging.AssignZipEntries(new[]
        {
            new ContributionFileReference { SectionFolder = "ComponentImages", ResolvedSourcePath = @"C:\data\U1\pin1.png" },
            new ContributionFileReference { SectionFolder = "ComponentImages", ResolvedSourcePath = @"C:\data\U5\pin1.png" },
        });

        Assert.Equal(2, plan.Attachments.Count);
        Assert.Equal("ReferencedFiles/ComponentImages/001_pin1.png", plan.EntryNames[0]);
        Assert.Equal("ReferencedFiles/ComponentImages/002_pin1.png", plan.EntryNames[1]);
    }

    // A row whose file could not be resolved contributes nothing to the zip, gets an empty
    // ZipEntry, and must not consume a number - the server treats empty as "no file attached".
    [Fact]
    public void Unresolved_rows_get_an_empty_entry_and_do_not_consume_a_number()
    {
        var plan = ContributionPackaging.AssignZipEntries(new[]
        {
            new ContributionFileReference { SectionFolder = "ComponentImages", ResolvedSourcePath = null },
            new ContributionFileReference { SectionFolder = "ComponentImages", ResolvedSourcePath = "   " },
            new ContributionFileReference { SectionFolder = "ComponentImages", ResolvedSourcePath = @"C:\data\pin1.png" },
        });

        Assert.Equal(new[] { "", "", "ReferencedFiles/ComponentImages/001_pin1.png" }, plan.EntryNames);
        Assert.Single(plan.Attachments);
    }

    [Fact]
    public void No_references_produce_an_empty_plan()
    {
        var plan = ContributionPackaging.AssignZipEntries(Array.Empty<ContributionFileReference>());

        Assert.Empty(plan.EntryNames);
        Assert.Empty(plan.Attachments);
    }

    // -------------------------------------------------------------- ResolveExistingFilePath

    [Fact]
    public void An_existing_absolute_path_resolves_to_itself()
    {
        using var workspace = new TempWorkspace();
        string file = Path.GetFullPath(workspace.WriteFile("external/photo.png", "x"));

        Assert.Equal(file, ContributionPackaging.ResolveExistingFilePath(workspace.Path_("data"), file));
    }

    [Fact]
    public void A_missing_absolute_path_resolves_to_null()
    {
        using var workspace = new TempWorkspace();

        Assert.Null(ContributionPackaging.ResolveExistingFilePath(
            workspace.Path_("data"),
            workspace.Path_("external", "does-not-exist.png")));
    }

    // Relative paths are the form stored in the board Excel files ("Commodore/C64/250407/x.png"),
    // always with forward slashes, and resolve against the data root.
    [Fact]
    public void A_relative_path_with_forward_slashes_resolves_inside_the_data_root()
    {
        using var workspace = new TempWorkspace();
        string expected = Path.GetFullPath(workspace.WriteFile("data/Commodore/C64/pin1.png", "x"));

        string? resolved = ContributionPackaging.ResolveExistingFilePath(
            workspace.Path_("data"),
            "Commodore/C64/pin1.png");

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void A_relative_path_missing_from_the_data_root_resolves_to_null()
    {
        using var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.Path_("data"));

        Assert.Null(ContributionPackaging.ResolveExistingFilePath(workspace.Path_("data"), "Commodore/missing.png"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_input_resolves_to_null(string? pathValue)
    {
        using var workspace = new TempWorkspace();

        Assert.Null(ContributionPackaging.ResolveExistingFilePath(workspace.Path_("data"), pathValue));
    }

    [Fact]
    public void A_relative_path_with_no_data_root_resolves_to_null()
    {
        Assert.Null(ContributionPackaging.ResolveExistingFilePath("", "Commodore/C64/pin1.png"));
    }

    // -------------------------------------------------------------- TryParseOutdatedVersionResponse

    // The server rejects submissions from outdated application versions with a response body
    // containing "OUTDATED_VERSION <newest>" (see Assets/Webserver/app-contribution/api/index.php).
    // Token and version-right-after-it are the contract; the rest of the body is free text.
    [Fact]
    public void The_outdated_version_rejection_is_recognized_and_names_the_newest_version()
    {
        bool recognized = ContributionPackaging.TryParseOutdatedVersionResponse(
            "ERROR: OUTDATED_VERSION 2.5.0 - this Classic Repair Toolbox version (2.3.0) is too old to contribute data; please update first.",
            out string newestVersion);

        Assert.True(recognized);
        Assert.Equal("2.5.0", newestVersion);
    }

    // A server that could not name the newest version still gets the "please update" handling,
    // just without a concrete number.
    [Fact]
    public void An_outdated_version_rejection_without_a_version_number_is_still_recognized()
    {
        bool recognized = ContributionPackaging.TryParseOutdatedVersionResponse(
            "ERROR: OUTDATED_VERSION - please update.",
            out string newestVersion);

        Assert.True(recognized);
        Assert.Equal(string.Empty, newestVersion);
    }

    [Fact]
    public void The_token_is_matched_case_insensitively()
    {
        Assert.True(ContributionPackaging.TryParseOutdatedVersionResponse(
            "error: outdated_version 2.5.0-beta.1", out string newestVersion));
        Assert.Equal("2.5.0-beta.1", newestVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Success")]
    [InlineData("ERROR: The contribution zip does not contain ComponentContribution.json.")]
    public void Other_responses_are_not_mistaken_for_the_outdated_version_rejection(string? responseBody)
    {
        Assert.False(ContributionPackaging.TryParseOutdatedVersionResponse(responseBody, out _));
    }

    // -------------------------------------------------------------- BuildFeedbackText

    // The server (app-contribution/index.php and api/index.php) reformats the summary around the
    // literal marker line "Mandatory change comment:" - the marker is part of the contract.
    [Fact]
    public void The_feedback_text_lists_the_context_and_ends_with_the_mandatory_comment_marker()
    {
        string text = ContributionPackaging.BuildFeedbackText(
            "Commodore 64",
            "250407",
            "U1 | VIC-II | 6569R5",
            "6b29fc40-ca47-1067-b31d-00dd010662da",
            "PAL",
            "Corrected the pin 5 baseline image");

        Assert.Contains("Hardware: Commodore 64", text);
        Assert.Contains("Board: 250407", text);
        Assert.Contains("Component: U1 | VIC-II | 6569R5", text);
        Assert.Contains("Component UUID v4: 6b29fc40-ca47-1067-b31d-00dd010662da", text);
        Assert.Contains("Region context: PAL", text);
        Assert.Contains("Mandatory change comment:" + Environment.NewLine + "Corrected the pin 5 baseline image", text);
    }
}
