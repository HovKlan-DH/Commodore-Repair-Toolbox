using System.Reflection;

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Threading;
using CRT;

namespace ClassicRepairToolbox.Tests.Ui;

// The "Create worklog" quick card that opens after an area is drag-drawn on the board.
//
// A worklog with no title is unidentifiable: the worklog list and the on-board pill would show
// nothing but its "#N". So the card's "Add worklog" button is gated on the title box having real
// text in it, and these pin that gate down - including the whitespace case, which is the one that
// silently slips through, because the title is Trim()ed before it is persisted and a title of
// spaces would therefore be saved as an empty one.
[Collection("HeadlessUi")]
public class WorklogCreateCardTests
{
    private static void SetTitle(TextBox titleBox, string? text)
    {
        titleBox.Text = text;

        // Avalonia raises TextChanged from a posted dispatcher job rather than from the setter,
        // so the gate has not run yet at the point the setter returns.
        Dispatcher.UIThread.RunJobs();
    }

    private static void WithCard(Action<TextBox, Button> body)
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();

            var titleBox = tab.FindControl<TextBox>("WorklogEntryTitleTextBox")!;
            var saveButton = tab.FindControl<Button>("WorklogEntryCardSaveButton")!;

            body(titleBox, saveButton);
        });
    }

    // The card opens with an empty title, so the button must start disabled rather than being
    // enabled until the first keystroke re-evaluates it.
    [Fact]
    public void The_add_worklog_button_starts_disabled()
    {
        WithCard((titleBox, saveButton) =>
        {
            Assert.True(string.IsNullOrEmpty(titleBox.Text));
            Assert.False(saveButton.IsEnabled);
        });
    }

    [Fact]
    public void A_blank_title_disables_the_add_worklog_button()
    {
        WithCard((titleBox, saveButton) =>
        {
            SetTitle(titleBox, "Bad capacitor");
            Assert.True(saveButton.IsEnabled);

            SetTitle(titleBox, string.Empty);

            Assert.False(saveButton.IsEnabled);
        });
    }

    // Whitespace is not a title. The gate has to agree with what the save actually writes, and
    // the save trims - so " " and "" must be treated the same.
    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void A_whitespace_only_title_disables_the_add_worklog_button(string title)
    {
        WithCard((titleBox, saveButton) =>
        {
            SetTitle(titleBox, "Bad capacitor");
            Assert.True(saveButton.IsEnabled);

            SetTitle(titleBox, title);

            Assert.False(saveButton.IsEnabled);
        });
    }

    [Fact]
    public void Typing_a_title_re_enables_the_add_worklog_button()
    {
        WithCard((titleBox, saveButton) =>
        {
            SetTitle(titleBox, string.Empty);
            Assert.False(saveButton.IsEnabled);

            SetTitle(titleBox, "Bad capacitor");

            Assert.True(saveButton.IsEnabled);
        });
    }

    // The card's labels, asserted so a rename cannot silently drift from what the user was told
    // the buttons and fields say.
    [Fact]
    public void The_card_uses_the_worklog_wording_for_its_fields_and_button()
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();

            Assert.Equal("Worklog title", tab.FindControl<TextBox>("WorklogEntryTitleTextBox")!.PlaceholderText);
            Assert.Equal("Description of worklog", tab.FindControl<TextBox>("WorklogEntryDescriptionTextBox")!.PlaceholderText);
            Assert.Equal("Add worklog", tab.FindControl<Button>("WorklogEntryCardSaveButton")!.Content);
        });
    }

    // ------------------------------------------------------------- parked-pill canvas

    // The pills of entries whose "Show marked area" is unticked are parked in the schematic
    // panel's top-right corner, on their OWN canvas - not the badge canvas beside it, which
    // carries the view matrix and would pan and zoom them with the board.
    [Fact]
    public void The_parked_pill_canvas_is_separate_from_the_anchored_badge_canvas()
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();

            var parked = tab.FindControl<Canvas>("SchematicsWorklogParkedBadgeCanvas")!;
            var anchored = tab.FindControl<Canvas>("SchematicsWorklogEntriesBadgeCanvas")!;

            Assert.NotSame(anchored, parked);

            // The anchored canvas carries a transform it is given the view matrix through; the
            // parked one must not, or its pills would move with the board.
            Assert.NotNull(anchored.RenderTransform);
            Assert.Null(parked.RenderTransform);
        });
    }

    // In Avalonia a null Background is not hit-testable while Transparent IS - verified with a
    // probe, not assumed. A background here would make the canvas swallow every press across the
    // whole schematic panel and kill panning, so its absence is load-bearing rather than an
    // oversight waiting to be "fixed".
    [Fact]
    public void The_parked_pill_canvas_has_no_background_so_it_cannot_swallow_board_clicks()
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();

            Assert.Null(tab.FindControl<Canvas>("SchematicsWorklogParkedBadgeCanvas")!.Background);
        });
    }

    // The pills park BELOW the "Netlist names" panel in z-order, since they step aside for it
    // rather than covering it.
    [Fact]
    public void The_parked_pills_sit_below_the_netlist_panel_in_z_order()
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();

            var parked = tab.FindControl<Canvas>("SchematicsWorklogParkedBadgeCanvas")!;
            var panel = tab.FindControl<Border>("KiCadNetConnectionsPanel")!;

            Assert.True(parked.ZIndex < panel.ZIndex, $"parked pills at z{parked.ZIndex} are not below the panel at z{panel.ZIndex}");
        });
    }

    // ------------------------------------------------------------- "Show marked area"

    // The same setting the full editor offers, so a worklog can be created with its area already
    // hidden rather than having to be created and then edited.
    [Fact]
    public void The_card_offers_the_show_marked_area_checkbox_ticked_by_default()
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();
            var checkBox = tab.FindControl<CheckBox>("WorklogEntryShowMarkedAreaCheckBox")!;

            Assert.Equal("Show marked area on schematics image", checkBox.Content);
            Assert.True(checkBox.IsChecked);
        });
    }

    // It sits ABOVE the components list, where the user reads it before deciding on scope.
    [Fact]
    public void The_checkbox_sits_above_the_components_in_scope_list()
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();
            var window = new Window { Width = 800, Height = 900, Content = tab };

            try
            {
                window.Show();
                tab.FindControl<Border>("SchematicsNewWorklogEntryCardBorder")!.IsVisible = true;
                window.Measure(new Size(800, 900));
                window.Arrange(new Rect(0, 0, 800, 900));
                Dispatcher.UIThread.RunJobs();

                var checkBox = tab.FindControl<CheckBox>("WorklogEntryShowMarkedAreaCheckBox")!;
                var list = tab.FindControl<ItemsControl>("WorklogEntryComponentList")!;

                double checkBoxY = checkBox.TranslatePoint(new Point(0, 0), window)!.Value.Y;
                double listY = list.TranslatePoint(new Point(0, 0), window)!.Value.Y;

                Assert.True(checkBoxY < listY, $"the checkbox at {checkBoxY} is not above the list at {listY}");
            }
            finally
            {
                window.Close();
            }
        });
    }

    // Renamed from "Mark components in scope" to match the full editor.
    [Fact]
    public void The_components_list_uses_the_shorter_heading()
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();

            Assert.Equal(
                "Components in scope",
                tab.FindControl<TextBlock>("WorklogEntryComponentScopeHeadingText")!.Text);
        });
    }

    // ------------------------------------------------------------- the mode hint

    // Clicking "Add worklog" leaves the user with a crosshair and no instruction, so the mode
    // shows a khaki label in the tab-header row saying what to do with it. The wording is asserted
    // for its key phrases rather than verbatim, so rewording it is not a test failure.
    [Fact]
    public void The_worklog_mode_hint_says_to_mark_an_area_and_why()
    {
        Assert.Contains("mark an area", CRT.Main.WorklogAreaModeHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("components in scope", CRT.Main.WorklogAreaModeHint, StringComparison.OrdinalIgnoreCase);
    }

    // The hint is a label over the tab-header row, not something the user has to interact with,
    // so it must never take a click - least of all the very click that dismisses it.
    //
    // Main itself is not constructible in a test (its constructor initialises every tab and reads
    // the real data root), so the markup is read from the compiled AXAML rather than from a live
    // window. That is enough to hold the two properties that matter here.
    [Fact]
    public void The_mode_hint_is_not_hit_testable_and_starts_hidden()
    {
        string markup = ReadMainWindowMarkup();

        int hintIndex = markup.IndexOf("x:Name=\"ModeHintBorder\"", StringComparison.Ordinal);
        Assert.True(hintIndex >= 0, "ModeHintBorder is not in Main.axaml");

        // The Border's own attribute block, up to the end of its opening tag.
        string element = markup[hintIndex..markup.IndexOf('>', hintIndex)];

        Assert.Contains("IsVisible=\"False\"", element, StringComparison.Ordinal);
        Assert.Contains("IsHitTestVisible=\"False\"", element, StringComparison.Ordinal);
    }

    // It must sit ABOVE the data-sync icon so it covers it, rather than being crowded beside it.
    [Fact]
    public void The_mode_hint_sits_above_the_data_sync_icon()
    {
        string markup = ReadMainWindowMarkup();

        int hintZ = ReadZIndex(markup, "ModeHintBorder");
        int syncZ = ReadZIndex(markup, "DataSyncStatusIconBorder");

        Assert.True(hintZ > syncZ, $"the hint at ZIndex {hintZ} does not cover the sync icon at {syncZ}");
    }

    // The hint's text must WRAP inside its box rather than running past the window edge.
    //
    // This is not a style preference - it is a structural trap. A horizontal StackPanel measures
    // its children with INFINITE width, so a TextWrapping="Wrap" TextBlock inside one never wraps
    // however small the surrounding Border's MaxWidth is. Measured: inside a StackPanel the real
    // hint text laid out 1092px wide on a single line, overflowing a 560px box; as the Border's
    // direct child it lays out 540px across two lines.
    //
    // The layout is rebuilt here rather than read from Main.axaml, because the defect is in how
    // the controls MEASURE each other and only a real layout pass can show it.
    [Fact]
    public void The_mode_hint_text_wraps_inside_its_box_instead_of_overflowing()
    {
        UiTest.Run(() =>
        {
            const double maxWidth = 560;

            var text = new TextBlock
            {
                Text = CRT.Main.WorklogAreaModeHint,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };

            var border = new Border
            {
                MaxWidth = maxWidth,
                Padding = new Thickness(10, 4),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Child = text,
            };

            var window = new Window { Width = 1200, Height = 300, Content = new Panel { Children = { border } } };

            try
            {
                window.Show();
                window.Measure(new Size(1200, 300));
                window.Arrange(new Rect(0, 0, 1200, 300));
                Dispatcher.UIThread.RunJobs();

                Assert.True(
                    text.Bounds.Width <= maxWidth,
                    $"the hint text laid out {text.Bounds.Width}px wide, past its {maxWidth}px box - it is not wrapping");

                // Two lines at this width, so the assertion above is not passing merely because the
                // text happened to be short.
                Assert.True(
                    text.Bounds.Height > 20,
                    $"the hint text is only {text.Bounds.Height}px tall - it did not wrap onto a second line, so this test is not exercising the case it claims");
            }
            finally
            {
                window.Close();
            }
        });
    }

    // No icon, and not bold - the hint is a sentence to read, not an alert to react to.
    [Fact]
    public void The_mode_hint_is_plain_text_with_no_icon()
    {
        string markup = ReadMainWindowMarkup();

        int start = markup.IndexOf("x:Name=\"ModeHintBorder\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "ModeHintBorder is not in Main.axaml");

        int end = markup.IndexOf("</Border>", start, StringComparison.Ordinal);
        string block = markup[start..end];

        Assert.DoesNotContain("ModeHintIcon", block, StringComparison.Ordinal);
        Assert.DoesNotContain("FontAwesome", block, StringComparison.Ordinal);
        Assert.DoesNotContain("FontWeight", block, StringComparison.Ordinal);
    }

    private static int ReadZIndex(string markup, string controlName)
    {
        int index = markup.IndexOf($"x:Name=\"{controlName}\"", StringComparison.Ordinal);
        Assert.True(index >= 0, $"{controlName} is not in Main.axaml");

        string element = markup[index..markup.IndexOf('>', index)];

        var match = System.Text.RegularExpressions.Regex.Match(element, @"ZIndex=""(\d+)""");
        Assert.True(match.Success, $"{controlName} declares no ZIndex");

        return int.Parse(match.Groups[1].Value);
    }

    private static string ReadMainWindowMarkup()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "Main", "Main.axaml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find Main/Main.axaml above {AppContext.BaseDirectory}");
    }

    // ------------------------------------------------------------- the components count

    // The count reports the SELECTION against the total, matching the full editor, and it is LIVE -
    // it used to read "{N} found", which said nothing about the choice the user had made and never
    // changed when they made one.
    //
    // Driven through the tab's own checklist rows and its All/None handlers by reflection: the card
    // is only populated by a real drag on a loaded board, which a headless test cannot stage, but
    // the counting rule itself is what matters here.
    [Fact]
    public void The_count_reports_the_live_selection_against_the_total()
    {
        UiTest.Run(() =>
        {
            var tab = new TabSchematics();

            var rowsField = typeof(TabSchematics).GetField("thisWorklogEntryComponentRows", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var rows = (System.Collections.ObjectModel.ObservableCollection<WorklogEntryComponentRow>)rowsField.GetValue(tab)!;

            rows.Clear();
            for (int i = 1; i <= 8; i++)
            {
                rows.Add(new WorklogEntryComponentRow { BoardLabel = $"C{i}", DisplayName = "Ceramic", IsChecked = true });
            }

            var update = typeof(TabSchematics).GetMethod("UpdateWorklogEntryComponentCount", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var selectNone = typeof(TabSchematics).GetMethod("OnWorklogEntrySelectNoneComponentsClick", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var countText = tab.FindControl<TextBlock>("WorklogEntryComponentCountText")!;

            update.Invoke(tab, null);
            Assert.Equal("8 of 8 selected", countText.Text);

            // The failure the review found: unticking everything left the count frozen.
            selectNone.Invoke(tab, new object?[] { null, null });
            Assert.Equal("0 of 8 selected", countText.Text);
        });
    }
}
