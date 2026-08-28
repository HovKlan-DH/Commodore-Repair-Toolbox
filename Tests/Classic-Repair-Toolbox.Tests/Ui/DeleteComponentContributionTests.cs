using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Threading;
using CRT;
using Handlers.DataHandling;

namespace ClassicRepairToolbox.Tests.Ui;

// The "delete this component" path through the contribution editor: the window opened on an
// existing component, then switched into a mode where nothing is edited and the whole component is
// proposed for removal instead.
//
// Three things make this worth testing through the real window rather than only through
// ContributionPackaging:
//
//   1. What the payload SAYS. A deletion is expressed as the five component-scoped sections sent
//      present-but-EMPTY, which the server reads as "remove every server row in this section" -
//      while the two board-wide sections must go out unchanged, or merging a deletion would also
//      propose removing every board local file and board link the board has.
//   2. What delete mode DISABLES. The button that leaves the mode again cannot live inside the
//      subtree the mode disables, because Avalonia gives a child no way to re-enable itself under
//      a disabled parent. That is a structural constraint on the markup, and nothing but a test
//      holds it in place.
//   3. Which validation is SKIPPED. A component whose image file is missing or undisplayable is a
//      prime candidate for deletion, so the image check that blocks an ordinary contribution must
//      not block this one.
//
// Private members are reached by reflection, the same approach and reasoning as
// NewComponentContributionTests: the logic is welded to a Window.
[Collection("HeadlessUi")]
public class DeleteComponentContributionTests
{
    // ---------------------------------------------------------------- the button itself

    // The offer only makes sense for something that exists. A new component is not in the board
    // data at all, so there is nothing a deletion could name.
    [Fact]
    public void The_delete_button_is_offered_for_an_existing_component_and_not_for_a_new_one()
    {
        UiTest.Run(() =>
        {
            var existing = new ComponentContributionWindow();
            LoadComponent(existing, BoardWithOneComponent());
            Assert.True(DeleteButton(existing).IsVisible);
            Assert.True(Hint(existing).IsVisible);

            var brandNew = new ComponentContributionWindow();
            brandNew.LoadNewComponent(BoardWithOneComponent(), string.Empty, "Commodore 64", "250407", "PAL", "board.xlsx");
            Assert.False(DeleteButton(brandNew).IsVisible);
            Assert.False(Hint(brandNew).IsVisible);
        });
    }

    // The button sits inside the "Component" section, which starts collapsed - so the notice panel
    // at the top, which is always visible, has to say the option exists at all.
    [Fact]
    public void An_existing_component_is_told_the_delete_option_is_there()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());

            Assert.Contains("Delete this component", Hint(window).Text ?? string.Empty);
        });
    }

    // ---------------------------------------------------------------- entering and leaving

    // The heart of the mode: every data section goes inactive and dimmed, while the two things
    // still needed to submit - the comment and the email - stay live.
    [Fact]
    public void Delete_mode_dims_and_disables_every_data_section_but_not_the_comment()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());

            ToggleDelete(window);

            foreach (var section in DataSections(window))
            {
                Assert.False(section.IsEnabled);
                Assert.True(section.Opacity < 1.0);
            }

            Assert.True(window.FindControl<TextBox>("MandatoryCommentTextBox")!.IsEnabled);
            Assert.True(window.FindControl<TextBox>("EmailTextBox")!.IsEnabled);
        });
    }

    // The structural constraint spelled out in the header. A control under a disabled parent is
    // simply not interactive in Avalonia and cannot opt out, so the button must not be a descendant
    // of anything delete mode disables - or the mode could never be left again.
    [Fact]
    public void The_delete_button_is_not_inside_any_of_the_sections_it_disables()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());
            var button = DeleteButton(window);

            foreach (var section in DataSections(window))
            {
                Assert.False(IsDescendantOf(button, section));
            }

            ToggleDelete(window);

            // The property that actually decides it, checked rather than inferred from the tree.
            Assert.True(button.IsEffectivelyEnabled);
        });
    }

    // Entering delete mode opens the section holding the button, so the way out is never folded
    // away behind the contributor.
    [Fact]
    public void Entering_delete_mode_opens_the_component_section_and_renames_both_buttons()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());

            Assert.False(window.FindControl<Expander>("ComponentExpander")!.IsExpanded);

            ToggleDelete(window);

            Assert.True(window.FindControl<Expander>("ComponentExpander")!.IsExpanded);
            Assert.Equal("Cancel deletion", DeleteButton(window).Content);
            Assert.Equal("Send deletion request", window.FindControl<Button>("SubmitButton")!.Content);
        });
    }

    // Backing out has to leave the window exactly as usable as it was, or a mis-click would end
    // the session's editing.
    [Fact]
    public void Leaving_delete_mode_restores_every_section()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());

            ToggleDelete(window);
            ToggleDelete(window);

            foreach (var section in DataSections(window))
            {
                Assert.True(section.IsEnabled);
                Assert.Equal(1.0, section.Opacity);
            }

            Assert.Equal("Delete this component", DeleteButton(window).Content);
            Assert.Equal("Send contribution update", window.FindControl<Button>("SubmitButton")!.Content);
        });
    }

    // ---------------------------------------------------------------- what the notice says

    // The counts are the whole point: the sections are collapsed, so without them the contributor
    // is agreeing to lose data they cannot see.
    [Fact]
    public void Delete_mode_names_the_component_and_counts_what_goes_with_it()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());

            ToggleDelete(window);

            Assert.Equal("You are proposing to DELETE this component", HeadingText(window));

            // U1 in BoardWithOneComponent carries one image, one highlight and one link.
            string notice = window.FindControl<TextBlock>("DeleteComponentNoticeTextBlock")!.Text ?? string.Empty;
            Assert.Contains("[U1]", notice);
            Assert.Contains("1 component image", notice);
            Assert.Contains("1 schematic highlight", notice);
            Assert.Contains("1 link", notice);

            Assert.True(window.FindControl<TextBlock>("DeleteComponentNoticeTextBlock")!.IsVisible);
            Assert.False(window.FindControl<TextBlock>("ExistingComponentNoticeTextBlock")!.IsVisible);
            Assert.False(window.FindControl<TextBlock>("NewComponentNoticeTextBlock")!.IsVisible);
            Assert.False(Hint(window).IsVisible);
        });
    }

    // ---------------------------------------------------------------- the payload

    // The shape the server reads as a deletion: five empty component-scoped lists, both board-wide
    // lists intact. Sending the board-wide ones empty would propose deleting them too.
    [Fact]
    public void A_deletion_empties_the_component_sections_and_leaves_the_board_wide_ones_alone()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());
            ToggleDelete(window);

            var payload = BuildPayload(window);

            Assert.True(payload.DeleteComponent);

            Assert.Empty(payload.Components);
            Assert.Empty(payload.ComponentImages);
            Assert.Empty(payload.ComponentHighlights);
            Assert.Empty(payload.ComponentLocalFiles);
            Assert.Empty(payload.ComponentLinks);

            Assert.Equal(2, payload.BoardLocalFiles.Count);
            Assert.Single(payload.BoardLinks);
        });
    }

    // The component is identified by the label and UUID the window was OPENED with. Reading them
    // back off the now-empty component rows would leave the server unable to tell what to remove.
    [Fact]
    public void A_deletion_still_names_the_component_it_removes()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());
            ToggleDelete(window);

            var payload = BuildPayload(window);

            Assert.Equal("U1", payload.ComponentBoardLabel);
            Assert.Equal("11111111-1111-4111-8111-111111111111", payload.ComponentUuidV4);
            Assert.Contains("U1", payload.ComponentDisplayText);
        });
    }

    // An ordinary contribution is unaffected - the flag is false and every section is populated.
    [Fact]
    public void An_ordinary_contribution_does_not_claim_to_be_a_deletion()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());

            var payload = BuildPayload(window);

            Assert.False(payload.DeleteComponent);
            Assert.Single(payload.Components);
            Assert.Single(payload.ComponentImages);
        });
    }

    // AssignZipEntriesToPayload walks the source row collections and the payload lists in lockstep
    // by index. A deletion has emptied the payload side while the source rows are all still there,
    // so it must not run at all - and a deletion attaches no files in any case. An unstamped
    // ZipEntry on the board-wide rows is what proves it did not run.
    [Fact]
    public void A_deletion_attaches_no_files()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());
            ToggleDelete(window);

            var payload = BuildPayload(window);

            Assert.Empty(payload.ComponentImages);
            Assert.All(payload.BoardLocalFiles, row => Assert.Equal(string.Empty, row.ZipEntry));
        });
    }

    // ---------------------------------------------------------------- submitting

    // The one field a deletion still has to carry. Without it the reviewer has a component
    // proposed for removal and no reason why.
    [Fact]
    public void Sending_a_deletion_with_no_comment_marks_the_comment_and_stops()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());
            window.Show();
            ToggleDelete(window);

            window.FindControl<TextBox>("EmailTextBox")!.Text = "contributor@example.com";
            window.FindControl<TextBox>("MandatoryCommentTextBox")!.Text = string.Empty;

            Submit(window);

            // ShowStatus posts its update, so the status line is empty until the queue is drained.
            PumpLayout(window);

            Assert.Contains("mandatory change comment", StatusText(window));
            Assert.Contains("HasError", window.FindControl<TextBox>("MandatoryCommentTextBox")!.Classes);
        });
    }

    // A component image row that would refuse an ordinary contribution must not refuse a deletion:
    // broken image data is a reason to delete the component, not a reason the deletion is invalid.
    //
    // The guard is asserted rather than the submission being run, because a submission that gets
    // past validation goes on to post over the network - which no test here may do.
    [Fact]
    public void An_unusable_image_row_does_not_block_a_deletion()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());

            // Exactly what ValidateComponentImageRows rejects: a row with no file at all.
            var imageRow = GetRows<ContributionComponentImageRow>(window, "thisComponentImageRows").Single();
            imageRow.OriginalFilePath = string.Empty;
            imageRow.FileLocation = string.Empty;
            imageRow.File = string.Empty;

            // It really is unusable - the ordinary path stops on it, and does so because it is asked.
            Assert.True((bool)Invoke(window, "ShouldValidateContributedRows")!);
            Assert.NotNull(Invoke(window, "ValidateComponentImageRows"));

            ToggleDelete(window);

            // ...and the deletion path never asks.
            Assert.False((bool)Invoke(window, "ShouldValidateContributedRows")!);
        });
    }

    // The success line has to say the component is still in the local data, or a contributor who
    // does not see it vanish concludes the deletion did not work.
    [Fact]
    public void The_success_line_says_the_component_stays_until_the_deletion_is_accepted()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());
            ToggleDelete(window);

            string text = SuccessText(window);

            Assert.Contains("[U1]", text);
            Assert.Contains("reviewed and accepted", text);
        });
    }

    // The notification mail must not read like an ordinary edit - see BuildFeedbackText.
    [Fact]
    public void The_feedback_summary_announces_a_deletion()
    {
        UiTest.Run(() =>
        {
            var window = new ComponentContributionWindow();
            LoadComponent(window, BoardWithOneComponent());

            string beforeToggle = (string)Invoke(window, "BuildContributionFeedbackText", "Wrong component")!;
            Assert.Contains("Request type: Component update", beforeToggle);

            ToggleDelete(window);

            string afterToggle = (string)Invoke(window, "BuildContributionFeedbackText", "Wrong component")!;
            Assert.Contains("Request type: DELETE COMPONENT", afterToggle);
        });
    }

    // ---------------------------------------------------------------- board data

    // U1 carries one row in each component-scoped section, so the notice counts are unambiguous.
    // The board-wide rows are what a deletion has to carry back untouched.
    private static BoardData BoardWithOneComponent()
    {
        return new BoardData
        {
            RevisionDate = "2026-01-15",
            Components =
            {
                new ComponentEntry
                {
                    UuidV4 = "11111111-1111-4111-8111-111111111111",
                    BoardLabel = "U1",
                    FriendlyName = "VIC-II",
                    Category = "IC",
                    Region = "PAL"
                },
                new ComponentEntry { BoardLabel = "C1", Category = "Capacitor" }
            },
            ComponentImages =
            {
                new ComponentImageEntry { BoardLabel = "U1", Region = "PAL", Pin = "1", Name = "Pin 1", File = "Scope baseline/u1-pin1.png" }
            },
            ComponentHighlights =
            {
                new ComponentHighlightEntry { SchematicName = "Sheet 1", BoardLabel = "U1", X = "10", Y = "20", Width = "30", Height = "40" }
            },
            ComponentLinks =
            {
                new ComponentLinkEntry { BoardLabel = "U1", Name = "Datasheet", Url = "https://example.com/vic" }
            },
            BoardLocalFiles =
            {
                new BoardLocalFileEntry { Category = "Schematics", Name = "Sheet 1" },
                new BoardLocalFileEntry { Category = "Schematics", Name = "Sheet 2" }
            },
            BoardLinks =
            {
                new BoardLinkEntry { Category = "Repairs", Name = "Repair log", Url = "https://example.com/log" }
            }
        };
    }

    // ---------------------------------------------------------------- helpers

    // An empty data root on purpose: the folder scan then finds nothing, and no test here needs a
    // file to exist - so nothing touches the user's real data.
    private static void LoadComponent(ComponentContributionWindow window, BoardData boardData)
    {
        window.LoadComponent(boardData, string.Empty, "Commodore 64", "250407", "PAL", "U1", "board.xlsx");
    }

    // Presses the button through the real handler, so the test exercises what a click does.
    private static void ToggleDelete(ComponentContributionWindow window)
    {
        Invoke(window, "OnToggleDeleteComponentClick", null, new Avalonia.Interactivity.RoutedEventArgs());
        Dispatcher.UIThread.RunJobs();
    }

    private static Button DeleteButton(ComponentContributionWindow window)
    {
        return window.FindControl<Button>("DeleteComponentButton")!;
    }

    private static TextBlock Hint(ComponentContributionWindow window)
    {
        return window.FindControl<TextBlock>("DeleteComponentHintTextBlock")!;
    }

    // The six containers delete mode switches off, taken from the window's own list rather than
    // re-listed here - so a section added later is covered without touching this file.
    private static IEnumerable<Control> DataSections(ComponentContributionWindow window)
    {
        return (IEnumerable<Control>)Invoke(window, "GetDataSectionControls")!;
    }

    private static bool IsDescendantOf(Control candidate, Control ancestor)
    {
        for (var parent = candidate.Parent; parent != null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static ObservableCollection<T> GetRows<T>(ComponentContributionWindow window, string fieldName)
    {
        var field = typeof(ComponentContributionWindow).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (ObservableCollection<T>)field!.GetValue(window)!;
    }

    private static string SuccessText(ComponentContributionWindow window)
    {
        return (string)Invoke(window, "BuildSubmissionSuccessText")!;
    }

    private static string HeadingText(ComponentContributionWindow window)
    {
        return window.FindControl<TextBlock>("ContributionNoticeHeadingTextBlock")!.Text ?? string.Empty;
    }

    private static string StatusText(ComponentContributionWindow window)
    {
        return window.FindControl<TextBlock>("StatusTextBlock")!.Text ?? string.Empty;
    }

    private static ComponentContributionPayload BuildPayload(ComponentContributionWindow window)
    {
        return (ComponentContributionPayload)Invoke(
            window,
            "BuildPayload",
            "contributor@example.com",
            "This component does not exist on the board")!;
    }

    // Stops before the first await: the comment check refuses the form, so nothing is posted.
    private static void Submit(ComponentContributionWindow window)
    {
        Invoke(window, "OnSubmitClick", null, new Avalonia.Interactivity.RoutedEventArgs());
    }

    // Headless windows do not lay out on their own, and ShowStatus posts its update rather than
    // writing it - so the dispatcher is drained and a layout pass forced by hand.
    private static void PumpLayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Measure(window.ClientSize);
        window.Arrange(new Avalonia.Rect(window.ClientSize));
        Dispatcher.UIThread.RunJobs();
    }

    private static object? Invoke(ComponentContributionWindow window, string methodName, params object?[] arguments)
    {
        var method = typeof(ComponentContributionWindow).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        return method!.Invoke(window, arguments.Length == 0 ? null : arguments);
    }
}
