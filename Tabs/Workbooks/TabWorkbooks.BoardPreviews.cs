using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Handlers.DataHandling;
using Handlers.Geometry;
using Handlers.Theming;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tabs.TabSchematics;

namespace CRT
{
    // ###########################################################################################
    // THE BOARD PANE (marker 3 in the mockup): every schematic image that has one or more worklog
    // entries belonging to the SELECTED workbook, each rendered with its own entries - a coloured,
    // dashed bounds rectangle for an entry with "show marked area" on, a plain "#N" pill (no
    // bounds) for one without. Real images, real entries, no sample data. Clicking a pill opens
    // that entry's editor, exactly as it does on the Schematics tab's own "Show worklogs" view -
    // see OnPreviewBadgePointerPressed. Clicking anywhere else on a preview SELECTS that schematic
    // (a highlighted border) and drives the entry list on the right - see SelectSchematic and
    // RefreshSelectedSchematicEntries.
    //
    // Reuses the exact same pieces TabSchematics.Worklog.cs uses for the "Show worklogs" list view
    // on the main Schematics tab: WorklogEntriesOverlay draws the bounds, and the badge is built to
    // the identical recipe (padlock glyph, category-coloured pill, white state disc) so an entry
    // looks the same wherever it appears in the app. What is NOT reused is the zoom/pan machinery -
    // this pane never zooms, so ViewMatrix is always the identity and the badge canvas needs no
    // inverse-scale transform or viewport-edge nudging.
    //
    // Rebuilt by RefreshBoardPreviews, called from SelectWorkbook, from the end of RefreshWorkbooks,
    // from RefreshBoardPreviewsForCurrentSelection (Main's public entry point - see that method's
    // own header for why board data needs a second, later refresh pass), and from
    // OnPreviewBadgePointerPressed after a save - so the board can never show a workbook other than
    // the one the top-line and the left panel agree on, nor a stale entry after it was edited.
    // ###########################################################################################
    public partial class TabWorkbooks
    {
        // fa-solid lock-open / lock, from WorklogGlyphs - see that class. These used to be a local
        // copy, justified on the grounds that this file and TabSchematics.Worklog.cs are
        // "independent partials of different classes"; the other copy in THIS class's own other
        // partial made that plainly untrue.
        private const int WorklogOpenCodepoint = WorklogGlyphs.OpenCodepoint;

        private const int WorklogClosedCodepoint = WorklogGlyphs.ClosedCodepoint;

        private static readonly string WorklogOpenGlyph = WorklogGlyphs.OpenGlyph;

        private static readonly string WorklogClosedGlyph = WorklogGlyphs.ClosedGlyph;

        // The schematic currently selected in the board pane - drives the entry list on the right.
        // Null means none selected (before the first RefreshBoardPreviews call, or the workbook has
        // no entries at all). RefreshBoardPreviews defaults this to the first schematic it builds a
        // preview for whenever the current value no longer names one that is actually shown, the
        // same "stay selected if still valid, else fall back" rule RefreshWorkbooks applies to the
        // selected workbook - see SelectSchematic.
        private string? thisSelectedSchematicName;

        // Which board thisSelectedSchematicName was chosen against.
        //
        // Without this, the selection was only reset when its name was absent from the NEW board's
        // grouped entries - so a schematic name shared between two boards (common across Commodore
        // revisions: "Motherboard", "Sheet 1") carried a selection made against a different board
        // straight over. thisSelectedWorkbookId does not have this problem because it is re-derived
        // per board from the saved active id; this field had no such anchor.
        private string? thisSelectedSchematicBoardKey;

        // ###########################################################################################
        // One Hand cursor for every preview and every badge this pane builds.
        //
        // Cursor is IDisposable and holds an HCURSOR on Win32, and these were constructed per preview
        // AND per badge on every rebuild, against controls thrown away by the next one, with nothing
        // disposing them. Elsewhere in the app a per-call new Cursor(...) is assigned to a single
        // long-lived control, which bounds it; here the count grew with rebuilds. One static instance
        // for the process removes the question rather than adding disposal to two more places.
        // ###########################################################################################
        private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

        // ###########################################################################################
        // Every schematic bitmap this pane has decoded, keyed by the absolute path it came from.
        //
        // WHO OWNS THESE: this tab, for as long as it is loaded. Nothing else may dispose one - and
        // that is the point of caching them rather than decoding per rebuild and disposing on clear.
        //
        // RefreshBoardPreviews clears BoardPreviewPanel and rebuilds every preview from scratch, and
        // it is reached from RefreshWorkbooks, SelectWorkbook, RefreshBoardPreviewsForCurrentSelection
        // and OnPreviewBadgePointerPressed - so Main.RefreshWorklogBar drives it on every board
        // change, entry save and workbook create/close. A fresh `new Bitmap(fullPath)` per preview
        // per pass stranded a full-resolution decode each time (a 4220x2941 schematic is ~47 MB of
        // BGRA), and the badge/SizeChanged closures below capture the bitmap, so the discarded
        // subtree kept itself alive through its own event tables rather than merely waiting for a
        // collection.
        //
        // Disposing on clear instead would be WORSE than the leak: ShowDialog does not block the
        // dispatcher, so RefreshWorklogBar can re-enter while a pill's editor is up, and that editor
        // documents that its schematic bitmap belongs to the caller. Disposing under it leaves
        // EditorLocationPreviewImage rendering a dead Skia surface - an ObjectDisposedException on
        // the render thread, which is fatal in Avalonia. Sharing one instance per path removes the
        // question: the same bitmap is handed to every preview and every editor, and nothing is
        // disposed until the tab itself goes away.
        //
        // Bounded by the number of distinct schematic images on the boards visited this session, not
        // by how often the pane is rebuilt, which is the difference that matters.
        // ###########################################################################################
        private readonly Dictionary<string, Bitmap> thisSchematicBitmapsByPath = new(StringComparer.OrdinalIgnoreCase);

        // ###########################################################################################
        // Returns the shared decoded bitmap for a schematic image path, decoding it on first use.
        // Null when the file is missing or cannot be decoded - one bad path drops that schematic
        // rather than the whole pane, the same failure mode DataManager already tolerates elsewhere.
        // ###########################################################################################
        private Bitmap? GetOrDecodeSchematicBitmap(string fullPath)
        {
            if (this.thisSchematicBitmapsByPath.TryGetValue(fullPath, out var cached))
                return cached;

            try
            {
                if (!File.Exists(fullPath))
                    return null;

                var decoded = new Bitmap(fullPath);
                this.thisSchematicBitmapsByPath[fullPath] = decoded;
                return decoded;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Workbooks tab: could not load schematic image [{fullPath}] - [{ex.Message}]");
                return null;
            }
        }

        // ###########################################################################################
        // Releases every decoded schematic bitmap. Called from DetachedFromVisualTree - the point at
        // which no preview, and no editor opened from one, can still be showing any of them.
        //
        // Not on a board change or a workbook switch: an editor opened from a pill outlives the
        // refresh that a save triggers, and it renders the bitmap this tab handed it. See the cache's
        // own header.
        // ###########################################################################################
        private void DisposeSchematicBitmaps()
        {
            foreach (var bitmap in this.thisSchematicBitmapsByPath.Values)
            {
                bitmap.Dispose();
            }

            this.thisSchematicBitmapsByPath.Clear();
        }

        // ###########################################################################################
        // Public entry point for Main: rebuilds JUST the board pane for whatever workbook and
        // schematic are currently selected, without touching the workbook list or the top-line.
        //
        // Called from Main.SetComponentHighlightRects, i.e. whenever the component highlight-rect
        // cache is replaced - a board load finishing, or a region switch. The pane's pills go on
        // screen as soon as the board data is assigned, but that cache is populated later, by the
        // board load's fire-and-forget task; a pill clicked in between found no rects for its
        // schematic, so BuildWorklogEntryComponentScope returned null and the editor opened WITHOUT
        // "Mark components in scope" - the same "the two modals are not identical" bug this feature
        // was written to fix, back as an intermittent one. A region switch had the mirror problem:
        // HighlightRectBuilder skips highlights failing IsVisibleByRegion, so the cache legitimately
        // changes shape and the pane has to be rebuilt against it.
        //
        // The list and the top-line are deliberately NOT rebuilt here: neither depends on the
        // highlight cache, and rebuilding them would reset nothing usefully.
        // ###########################################################################################
        public void RefreshBoardPreviewsForCurrentSelection()
        {
            // Its own refresh pass, like SelectSchematic - see the clear there.
            this.thisEntriesReadThisPass.Clear();
            this.RefreshBoardPreviews();
        }

        // ###########################################################################################
        // Rebuilds the board pane for the currently selected workbook.
        //
        // Groups the workbook's entries by schematic name, keeps only the ones that match a real
        // schematic image on the current board (an entry can reference a schematic that was since
        // renamed or removed from the board data - it is skipped rather than shown with no image),
        // and builds one preview per remaining schematic.
        // ###########################################################################################
        private void RefreshBoardPreviews()
        {
            if (this.BoardPreviewPanel == null)
                return;

            this.BoardPreviewPanel.Children.Clear();

            var boardData = this.CurrentBoardDataForPreviews;

            // GroupBy before ToDictionary, NOT a bare ToDictionary: board Excel files arrive from
            // classic-repair-toolbox.dk independently of app releases, and BoardDataReader.MapSchematics
            // does no dedup, no uniqueness validation and no case normalisation - whatever the
            // Schematics sheet holds arrives verbatim. A bare ToDictionary throws ArgumentException on
            // the first duplicate, and OrdinalIgnoreCase makes that MORE likely than the default
            // comparer would ("Sheet 1" and "sheet 1" collide here while being two distinct schematics
            // everywhere else). Nothing in this tab catches, so it propagated out through
            // RefreshWorkbooks and Main.RefreshWorklogBar and took down board selection entirely.
            // First wins, matching the GroupBy on entries just below, which already tolerated this.
            var schematicsByName = (boardData?.Schematics ?? new List<BoardSchematicEntry>())
                .Where(s => !string.IsNullOrWhiteSpace(s.SchematicName))
                .GroupBy(s => s.SchematicName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // > 0, not >= 0: workbook ids start at 1 and the rest of the app uses 0 as "no workbook"
            // (Main.RefreshWorklogBar's own "showByDefault ? activeWorkbook.Id : 0"), so admitting 0
            // here would have this half of the feature treat a sentinel as a real workbook.
            // Through the pass cache, so a rebuild that already read this workbook's entries for the
            // search filter does not read them again - see GetEntriesForThisPass.
            var allWorkbookEntries = this.thisSelectedWorkbookId > 0
                ? this.GetEntriesForThisPass(this.thisSelectedWorkbookId)
                : new List<WorklogEntryRecord>();

            // Narrowed to the search's matches (null = no search active), using the set
            // RefreshWorkbooks already computed - so a filtered board pane shows exactly the pills
            // whose entries matched, and the entry list below it agrees, both from one decision.
            var matchedEntryIds = this.MatchedEntryIdsForWorkbook(this.thisSelectedWorkbookId);
            var entries = matchedEntryIds == null
                ? allWorkbookEntries
                : allWorkbookEntries.Where(e => matchedEntryIds.Contains(e.Id)).ToList();

            var entriesBySchematic = entries
                .Where(e => schematicsByName.ContainsKey(e.SchematicName))
                .GroupBy(e => e.SchematicName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // A BOARD switch drops the selection outright, before the "still valid?" test below could
            // accidentally keep it: schematic names are not unique across boards, so a name that
            // happens to exist on both would otherwise carry a selection made against the previous
            // board - see thisSelectedSchematicBoardKey.
            string currentBoardKey = this.BoardKeyOverrideForTests ?? this.MainWindow?.GetCurrentBoardKey() ?? string.Empty;
            if (!string.Equals(currentBoardKey, this.thisSelectedSchematicBoardKey, StringComparison.OrdinalIgnoreCase))
            {
                this.thisSelectedSchematicName = null;
                this.thisSelectedSchematicBoardKey = currentBoardKey;
            }

            // Otherwise it stays selected across a rebuild if it is still one of the schematics
            // actually shown (a save via the pill's editor re-enters this method, and must not
            // silently reset which schematic's entries the right panel is showing); failing that it
            // falls back to the first one - the same "keep if still valid, else fall back" rule
            // RefreshWorkbooks applies to the selected workbook.
            if (entriesBySchematic.Count == 0)
            {
                this.thisSelectedSchematicName = null;
            }
            else if (!entriesBySchematic.Any(g => string.Equals(g.Key, this.thisSelectedSchematicName, StringComparison.OrdinalIgnoreCase)))
            {
                this.thisSelectedSchematicName = entriesBySchematic[0].Key;
            }

            foreach (var group in entriesBySchematic)
            {
                var schematic = schematicsByName[group.Key];
                bool isSelected = string.Equals(group.Key, this.thisSelectedSchematicName, StringComparison.OrdinalIgnoreCase);
                var preview = this.BuildSchematicPreview(schematic, group.ToList(), this.thisSelectedWorkbookId, isSelected);
                if (preview == null)
                    continue;

                this.BoardPreviewPanel.Children.Add(preview);
            }

            this.NoBoardPreviewsText.IsVisible = entriesBySchematic.Count == 0;

            // As on the two lists: "none matched" is not the same as "none recorded", and saying
            // "yet" for a search result reads as the entries having gone missing.
            this.NoBoardPreviewsText.Text = matchedEntryIds != null && allWorkbookEntries.Count > 0
                ? "No worklog entries in this workbook match your search."
                : "No worklog entries recorded against a schematic image for this workbook yet.";

            // The entries this pass already read are handed on rather than re-read: GetEntries has
            // no cache (File.ReadAllText + Deserialize + a per-entry normalise/migrate loop, every
            // call), and this method and the entry list were reading the same workbook's file twice
            // per rebuild.
            this.RefreshSelectedSchematicEntries(entries);
        }

        // ###########################################################################################
        // Selects a schematic by name, called when the user clicks its preview (anywhere except a
        // pill - see BuildSchematicPreview's own click handler). Restyles the previews' border
        // (exactly one gets the selected colour) and rebuilds the entry list on the right for it.
        //
        // Re-clicking the already-selected schematic is a no-op, matching SelectWorkbook's own rule
        // for the same reason: there is nothing to select that is not already selected.
        // ###########################################################################################
        // Exposed to the test project so WorkbooksBoardPreviewTests can select a schematic without
        // fighting pointer-event routing against a preview Border - the same idea as
        // SelectWorkbookForTests. The running app always reaches this through a preview's own
        // PointerPressed handler in BuildSchematicPreview, never directly.
        internal void SelectSchematicForTests(string schematicName) => this.SelectSchematic(schematicName);

        private void SelectSchematic(string schematicName)
        {
            if (string.Equals(schematicName, this.thisSelectedSchematicName, StringComparison.OrdinalIgnoreCase))
                return;

            this.thisSelectedSchematicName = schematicName;

            // This is its own refresh pass - it does not come through RefreshWorkbooks, which is
            // what normally clears the cache - so clear it here too. Without this a click could be
            // served entries read before an intervening save.
            this.thisEntriesReadThisPass.Clear();

            foreach (var child in this.BoardPreviewPanel.Children)
            {
                if (child is not Border preview || preview.Tag is not string previewSchematicName)
                    continue;

                ApplySchematicPreviewSelectedBorder(preview, string.Equals(previewSchematicName, schematicName, StringComparison.OrdinalIgnoreCase));
            }

            this.RefreshSelectedSchematicEntries();
        }

        // ###########################################################################################
        // Rebuilds the entry list on the right (marker 4) for whichever schematic is currently
        // selected in the board pane. Called from RefreshBoardPreviews (a workbook switch, or the
        // selection defaulting to the first schematic) and from SelectSchematic (a click).
        //
        // Takes the workbook's entries when the caller has just read them (RefreshBoardPreviews does,
        // for its own grouping) and reads them itself otherwise - SelectSchematic runs this on its
        // own, with no board-pane rebuild alongside it and so nothing already in hand. GetEntries is
        // uncached and re-parses the whole file per call, so the shared read is worth threading
        // through.
        // ###########################################################################################
        private void RefreshSelectedSchematicEntries(List<WorklogEntryRecord>? workbookEntries = null)
        {
            if (this.SelectedSchematicEntriesPanel == null)
                return;

            this.SelectedSchematicEntriesPanel.Children.Clear();

            if (this.thisSelectedSchematicName == null)
            {
                this.SelectedSchematicEntriesHeaderText.Text = "Select a schematic";
                this.NoSelectedSchematicEntriesText.Text = "Click a schematic image on the left to see its entries here.";
                this.NoSelectedSchematicEntriesText.IsVisible = true;
                return;
            }

            var allEntries = workbookEntries
                ?? (this.thisSelectedWorkbookId > 0
                    ? this.GetEntriesForThisPass(this.thisSelectedWorkbookId)
                    : new List<WorklogEntryRecord>());

            // Narrowed to what the search matched, using the SAME set RefreshWorkbooks computed for
            // this workbook rather than re-running the query here - so the list cannot disagree with
            // the workbook card and the board pane about what matched. Null means no search is
            // active and every entry is shown.
            var matchedEntryIds = this.MatchedEntryIdsForWorkbook(this.thisSelectedWorkbookId);

            var entries = allEntries
                .Where(e => string.Equals(e.SchematicName, this.thisSelectedSchematicName, StringComparison.OrdinalIgnoreCase))
                .Where(e => matchedEntryIds == null || matchedEntryIds.Contains(e.Id))
                .OrderBy(e => e.Id)
                .ToList();

            this.SelectedSchematicEntriesHeaderText.Text =
                $"{this.thisSelectedSchematicName} · {WorklogEntryScope.FormatCount(entries.Count, "entry", "entries")}";

            if (entries.Count == 0)
            {
                // A search that hid them all is a different thing from a schematic that never had
                // any, and saying "yet" for it reads as the entries having been lost.
                this.NoSelectedSchematicEntriesText.Text = matchedEntryIds != null
                    ? "No worklog entries on this schematic match your search."
                    : "No worklog entries for this schematic yet.";
                this.NoSelectedSchematicEntriesText.IsVisible = true;
                return;
            }

            this.NoSelectedSchematicEntriesText.IsVisible = false;

            foreach (var entry in entries)
            {
                this.SelectedSchematicEntriesPanel.Children.Add(this.BuildEntryDetailCard(entry));
            }
        }

        // fa-regular note-sticky / fa-solid paint-roller / fa-solid triangle-exclamation - the SAME
        // codepoints WorklogEntryEditorWindow.axaml uses for its three category chips
        // (EditorCategoryNoteIcon/CosmeticIcon/IssueIcon), spelled as hex codepoints rather than
        // literal glyph characters so this source file stays plain ASCII. Note is the one category
        // whose glyph is Regular rather than Solid.
        private const int NoteCategoryCodepoint = 0xF15C;

        private const int CosmeticCategoryCodepoint = 0xF5D0;

        private const int IssueCategoryCodepoint = 0xF188;

        private static readonly Dictionary<string, (int Codepoint, bool IsRegular)> CategoryIconsByName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Note"] = (NoteCategoryCodepoint, true),
            ["Cosmetic"] = (CosmeticCategoryCodepoint, false),
            ["Issue"] = (IssueCategoryCodepoint, false)
        };

        // ###########################################################################################
        // Builds one entry's detail card for the selected-schematic list: ONE 1px-bordered panel
        // holding four stacked rows - "#{N} {Title}", the description, a category chip + status
        // pill, and the stats row - per the layout this list was specifically asked for. Deliberately NOT the
        // anchor tag / timestamp / photo-thumbnail layout the entry list used to show (that markup
        // is gone) - this is a different, narrower view: what an entry IS, not where it sits or
        // what evidence backs it. The full picture is still one click away, via the entry's pill on
        // the board pane above (OnPreviewBadgePointerPressed).
        //
        // The "#N" badge is drawn in the same filled/white-text visual WorklogEntryEditorWindow uses
        // for its own EditorIdBadge - filled is right there, since it names WHICH workbook entry this
        // is rather than a selection state. The category chip and status pill beside it are
        // deliberately the OUTLINED variant instead - see BuildOutlinedCategoryChip.
        // ###########################################################################################
        // An instance method rather than static: the title and description are drawn through
        // BuildHighlightedTextBlock, which needs THIS tab's current search query to know what to
        // mark. Everything else it builds is still static.
        private Border BuildEntryDetailCard(WorklogEntryRecord entry)
        {
            string title = string.IsNullOrWhiteSpace(entry.Title) ? "(untitled)" : entry.Title;
            Color categoryColor = ResolveWorklogCategoryColor(entry.Category);

            var idBadge = new Border
            {
                Background = new SolidColorBrush(categoryColor),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"#{entry.Id}",
                    FontSize = 13,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White
                }
            };

            var titleText = this.BuildHighlightedTextBlock(title, 13, TextWrapping.Wrap, block =>
            {
                block.FontWeight = FontWeight.Bold;
                block.VerticalAlignment = VerticalAlignment.Center;
            });

            var titleRow = new WrapPanel { ItemSpacing = 8, LineSpacing = 4 };
            titleRow.Children.Add(titleText);
            titleRow.Children.Add(idBadge);

            bool hasDescription = !string.IsNullOrWhiteSpace(entry.Description);
            var descriptionText = this.BuildHighlightedTextBlock(
                hasDescription ? entry.Description : "(no description)",
                11,
                TextWrapping.Wrap,
                block =>
                {
                    if (!hasDescription)
                        block.Foreground = ResolveThemeBrushStatic("Workbooks_Faint_Fg");
                });

            var categoryChip = BuildOutlinedCategoryChip(entry.Category, categoryColor);
            var statusPill = BuildOutlinedStatePill(entry.State);

            var categoryStatusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            categoryStatusRow.Children.Add(categoryChip);
            categoryStatusRow.Children.Add(statusPill);

            var statsRow = BuildEntryStatsRow(entry);

            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(titleRow);
            stack.Children.Add(descriptionText);
            stack.Children.Add(categoryStatusRow);
            stack.Children.Add(statsRow);

            return new Border
            {
                Background = ResolveThemeBrushStatic("Workbooks_Panel_Bg"),
                BorderBrush = ResolveThemeBrushStatic("Workbooks_RowSeparator"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Child = stack
            };
        }

        // ###########################################################################################
        // The fourth row on an entry's detail card: total hours spent and total cost (both summed
        // across the entry's Work done rows, the same sums WorklogEntryEditorWindow's own
        // RefreshWorkDoneRows shows beside that section's heading - "{hours} h" / the bare cost
        // number, no currency symbol, matching SummaryText's own "{HoursSpent:0.##} h · {Cost:0.##}"
        // formatting exactly), then how many comments, links, photos and files the entry carries -
        // one number each, since an entry can have any number of each and this card is meant to say
        // how much is behind the pill without opening it. A WrapPanel, matching every other
        // multi-item row on this card and in the full editor's own list headers: six items can
        // outrun the entry list's narrower widths, and wrapping is preferable to clipping or a
        // horizontal scrollbar inside a vertically-scrolling list.
        // ###########################################################################################
        private static WrapPanel BuildEntryStatsRow(WorklogEntryRecord entry)
        {
            var (totalHours, totalCost) = WorklogEntryScope.GetWorkDoneTotals(entry);

            var row = new WrapPanel { ItemSpacing = 10, LineSpacing = 4 };
            row.Children.Add(BuildEntryStatText($"{totalHours.ToString("0.##", CultureInfo.InvariantCulture)} h"));
            row.Children.Add(BuildEntryStatText(totalCost.ToString("0.##", CultureInfo.InvariantCulture)));
            row.Children.Add(BuildEntryStatText(WorklogEntryScope.FormatCount(entry.Comments.Count, "comment", "comments")));
            row.Children.Add(BuildEntryStatText(WorklogEntryScope.FormatCount(entry.Links.Count, "link", "links")));
            row.Children.Add(BuildEntryStatText(WorklogEntryScope.FormatCount(entry.Photos.Count, "photo", "photos")));
            row.Children.Add(BuildEntryStatText(WorklogEntryScope.FormatCount(entry.Files.Count, "file", "files")));
            return row;
        }

        private static TextBlock BuildEntryStatText(string text) => new()
        {
            Text = text,
            FontSize = 10,
            Foreground = ResolveThemeBrushStatic("Workbooks_Muted_Fg"),
            VerticalAlignment = VerticalAlignment.Center
        };


        // The category chip in the UNSELECTED visual from WorklogEntryEditorWindow's own
        // ApplyCategoryChipVisualState else-branch: outlined rather than filled - Form_Bg
        // background, a 1px Form_Border outline, icon and label both in the ordinary foreground
        // colour. This list has no selection concept (nothing here is "the chosen category" the
        // way a click in the full editor would make one), so the filled "selected" look the id
        // badge still uses does not apply here - reported explicitly: the filled look should only
        // ever mean "this is the selected one".
        //
        // Named BuildOutlined*, not BuildFilled*: it was called the latter while building the
        // former, which read as an instruction to restore precisely the bug that was reported.
        private static Border BuildOutlinedCategoryChip(string category, Color categoryColor)
        {
            var (codepoint, isRegular) = CategoryIconsByName.TryGetValue(category, out var icon) ? icon : (NoteCategoryCodepoint, true);
            string glyph = char.ConvertFromUtf32(codepoint);

            var labelBrush = ResolveThemeBrushStatic("Schematics_Panels_Fg");

            var iconText = new TextBlock
            {
                Text = glyph,
                FontFamily = isRegular ? ResolveFontAwesomeRegular() : ResolveFontAwesomeSolid(),
                FontSize = 11,
                Padding = FontAwesomeGlyphMetrics.GetTopOverflowThickness(codepoint, 11.0),
                Foreground = labelBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            var labelText = new TextBlock
            {
                Text = category,
                FontSize = 11,
                Foreground = labelBrush,
                VerticalAlignment = VerticalAlignment.Center
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            content.Children.Add(iconText);
            content.Children.Add(labelText);

            return new Border
            {
                Background = ResolveThemeBrushStatic("Form_Bg"),
                BorderBrush = ResolveThemeBrushStatic("Form_Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4),
                Child = content
            };
        }

        // The status pill in the UNSELECTED visual from WorklogEntryEditorWindow's own
        // ApplyStatePillVisualState else-branch: outlined rather than filled - Form_Bg background,
        // a 1px Form_Border outline, the padlock in the state's own colour (that is its identity,
        // not a selection cue) and the label in the ordinary foreground. Same reasoning as
        // BuildOutlinedCategoryChip: no selection concept in this list, so no filled pill.
        private static Border BuildOutlinedStatePill(string state)
        {
            bool isResolved = WorklogManager.IsResolvedState(state);
            Color stateColor = ResolveWorklogStateColor(state);
            int glyphCodepoint = isResolved ? WorklogClosedCodepoint : WorklogOpenCodepoint;
            const double glyphFontSize = 11.0;

            var iconText = new TextBlock
            {
                Text = isResolved ? WorklogClosedGlyph : WorklogOpenGlyph,
                FontFamily = ResolveFontAwesomeSolid(),
                FontSize = glyphFontSize,
                Foreground = new SolidColorBrush(stateColor),
                Padding = FontAwesomeGlyphMetrics.GetTopOverflowThickness(glyphCodepoint, glyphFontSize),
                VerticalAlignment = VerticalAlignment.Center
            };
            var labelText = new TextBlock
            {
                Text = state,
                FontSize = 11,
                Foreground = ResolveThemeBrushStatic("Schematics_Panels_Fg"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            content.Children.Add(iconText);
            content.Children.Add(labelText);

            return new Border
            {
                Background = ResolveThemeBrushStatic("Form_Bg"),
                BorderBrush = ResolveThemeBrushStatic("Form_Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 2),
                Child = content
            };
        }

        // ###########################################################################################
        // Builds one schematic's preview: its image (natural aspect ratio, capped in height so a
        // workbook with several schematics does not force endless scrolling for one board-sized
        // image) with the entries overlay and badges on top, and a caption naming the schematic.
        //
        // Returns null when the image file cannot be loaded - happens if the data root is out of
        // sync with a board's Excel file, the same failure mode DataManager already tolerates
        // elsewhere - so one bad path drops that schematic rather than the whole pane.
        //
        // Clicking anywhere on the returned Border except a pill selects this schematic (see the
        // PointerPressed handler near the end and OnPreviewBadgePointerPressed's own e.Handled,
        // which stops a pill click from also reaching this one). The Border is tagged with the
        // schematic's name so SelectSchematic can find it again to restyle its border without a
        // second dictionary mapping controls back to names.
        //
        // An instance method, not static: each badge's click handler needs to reach
        // OnPreviewBadgePointerPressed on THIS tab.
        // ###########################################################################################
        private Border? BuildSchematicPreview(BoardSchematicEntry schematic, List<WorklogEntryRecord> entries, int workbookId, bool isSelected)
        {
            if (string.IsNullOrWhiteSpace(schematic.SchematicImageFile))
                return null;

            string fullPath = Path.Combine(
                DataManager.DataRoot,
                schematic.SchematicImageFile.Replace('/', Path.DirectorySeparatorChar));

            var bitmap = this.GetOrDecodeSchematicBitmap(fullPath);
            if (bitmap == null)
                return null;

            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                MaxHeight = 420
            };

            var overlayEntries = new List<WorklogEntriesOverlay.Entry>();

            // Anchored badges (Tag = the entry's pixel rect) sit on the marked area, positioned in
            // PositionPreviewOverlayAndBadges once the image has its real size. Parked badges (Tag =
            // null) have no area to anchor to - they stack in the image's own top-right corner
            // instead, exactly what TabSchematics.Worklog.cs's LayOutWorklogParkedBadges does for
            // the main Schematics view. Same Left/Top alignment as the overlay below, for the same
            // reason: both sets of positions are in the image's own content coordinates, which only
            // line up with the canvas's (0,0) if the canvas starts where the image does.
            // Hit-test visible, unlike the overlay and border outline below - the pills are the
            // only clickable thing on this preview (see OnPreviewBadgePointerPressed), matching the
            // real Schematics tab's own split between SchematicsWorklogEntriesOverlay
            // (IsHitTestVisible="False", the bounds) and SchematicsWorklogEntriesBadgeCanvas
            // (clickable, the pills).
            var badgeCanvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            foreach (var entry in entries)
            {
                Color color = ResolveWorklogCategoryColor(entry.Category);
                var badge = BuildPreviewBadge(entry, color);
                badgeCanvas.Children.Add(badge);

                // Captured entry id, not read back off Tag: Tag is already spoken for below (it
                // carries the anchor Rect for PositionPreviewOverlayAndBadges, or stays null for a
                // parked badge), and overloading it a second way for the click target would make
                // one of the two meanings silently clobber the other. workbookId is the same for
                // every badge in this preview, so it is captured once rather than per-entry.
                //
                // Not unsubscribed on rebuild, and does not need to be: this closure and the
                // SizeChanged one below reference only controls INSIDE this preview's own subtree
                // (plus `this`, which points outward and so retains nothing), so once
                // BoardPreviewPanel.Children.Clear() drops the preview the whole cycle is
                // unreachable together and collects. What did NOT collect was the decoded bitmap's
                // unmanaged Skia surface - see thisSchematicBitmapsByPath, which is what actually
                // fixed the leak.
                int entryId = entry.Id;
                badge.PointerPressed += (_, e) => this.OnPreviewBadgePointerPressed(workbookId, entryId, bitmap, e);

                // "Show marked area" ticked: a coloured bounds rectangle on the overlay, badge
                // anchored to it. Unticked: no rectangle at all, badge parked in the corner instead
                // (Tag stays null) - matching TabSchematics.Worklog.cs's own ShowMarkedArea branch
                // exactly. Getting this backwards was the original bug report: every badge was
                // anchored to its marker regardless of ShowMarkedArea, so an entry meant to show
                // only a parked pill still appeared pinned to the spot it was drawn at.
                if (entry.ShowMarkedArea)
                {
                    var pixelRect = new Rect(entry.AreaX, entry.AreaY, entry.AreaWidth, entry.AreaHeight);
                    overlayEntries.Add(new WorklogEntriesOverlay.Entry(pixelRect, color, entry.Id));
                    badge.Tag = pixelRect;
                }
            }

            // Left/Top-aligned to match the Image exactly - WorklogEntriesOverlay.Render assumes
            // its OWN Bounds starts at the image's content origin (see GetImageContentRect's own
            // comment: SchematicsImage is Left/Top-aligned, so bitmap content starts at (0,0)).
            // Stretch (the Grid default) would give it the whole cell instead, which is a
            // different rect than the image occupies whenever the image's aspect ratio does not
            // match the cell's - i.e. almost always.
            var overlay = new WorklogEntriesOverlay
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                BitmapPixelSize = bitmap.PixelSize,
                ViewMatrix = Matrix.Identity,
                Entries = overlayEntries
            };

            // The 1px border marking the image's boundary, so it reads clearly against the pane's
            // own background - Image has no border property of its own, and a schematic with a lot
            // of white/transparent area was otherwise hard to tell apart from the pane behind it.
            // A separate sibling Grid child, NOT a wrapper around the image: wrapping it would
            // inset the image by the border's thickness, throwing off every pixel-rect calculation
            // below that assumes image.Bounds starts at the image control's own (0,0) - exactly the
            // assumption GetImageContentRect documents for the real Schematics tab's own image.
            // Sized and positioned to match the image exactly in PositionPreviewOverlayAndBadges,
            // same as the overlay and the badge canvas.
            var imageBorderOutline = new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            // The overlay, the border outline and the badge canvas all need the image's ARRANGED
            // size to place themselves correctly (GetImageContentRect depends on it), which is not
            // known until this preview has been through a layout pass. SizeChanged catches every
            // pass, not just the first, so a window resize that changes how large this
            // Uniform-stretched image renders keeps all three in step with it.
            image.SizeChanged += (_, _) => PositionPreviewOverlayAndBadges(image, overlay, imageBorderOutline, badgeCanvas, bitmap.PixelSize);

            var imageLayer = new Grid();
            imageLayer.Children.Add(image);
            imageLayer.Children.Add(imageBorderOutline);
            imageLayer.Children.Add(overlay);
            imageLayer.Children.Add(badgeCanvas);

            string displayName = string.IsNullOrWhiteSpace(schematic.CadName) ? schematic.SchematicName : schematic.CadName;
            var caption = new TextBlock
            {
                Text = displayName,
                FontSize = 11,
                FontWeight = FontWeight.Bold
            };

            var body = new StackPanel { Spacing = 6 };
            body.Children.Add(caption);
            body.Children.Add(imageLayer);

            var preview = new Border
            {
                Background = ThemeResources.ResolveBrush("Form_Bg", Brushes.Transparent),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Cursor = HandCursor,
                Tag = schematic.SchematicName,
                Child = body
            };

            ApplySchematicPreviewSelectedBorder(preview, isSelected);

            string schematicName = schematic.SchematicName;
            preview.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(preview).Properties.IsLeftButtonPressed)
                    this.SelectSchematic(schematicName);
            };

            return preview;
        }

        // ###########################################################################################
        // Colours a schematic preview's own border to show whether it is the selected one - the
        // same IndianRed accent BuildWorkbookCard's selected-card left edge uses, so "selected"
        // reads as one consistent colour language across this tab. A plain 1px neutral outline
        // (Workbooks_RowSeparator, matching every other panel border here) when not selected.
        //
        // 2px thick either way, not 1px growing to 2px on selection: changing thickness on select
        // would shift the whole preview by a pixel and visibly nudge the image and its pills,
        // which a colour change alone does not.
        // ###########################################################################################
        private static void ApplySchematicPreviewSelectedBorder(Border preview, bool isSelected)
        {
            preview.BorderBrush = isSelected
                ? ResolveThemeBrushStatic("Main_TabUnderline_Selected")
                : ResolveThemeBrushStatic("Workbooks_RowSeparator");
        }

        // ###########################################################################################
        // Positions the overlay and every badge for one schematic preview against the image's
        // current arranged size. Re-run on every SizeChanged, not only the first layout pass, so a
        // window resize keeps the bounds and pill positions in step with the Uniform-stretched
        // image rather than freezing them at whatever size the image happened to have on first
        // measure.
        // ###########################################################################################
        // A parked badge's stack sits inset from the image's own edges, matching
        // TabSchematics.Worklog.cs's WorklogParkedBadgeMargin/WorklogParkedBadgeSpacing constants -
        // the same visual gap the real Schematics tab uses for its own parked pills.
        private const double ParkedBadgeMargin = 10.0;

        private const double ParkedBadgeSpacing = 6.0;

        private static void PositionPreviewOverlayAndBadges(Image image, WorklogEntriesOverlay overlay, Border imageBorderOutline, Canvas badgeCanvas, PixelSize bitmapPixelSize)
        {
            var imageBounds = image.Bounds.Size;
            overlay.Width = imageBounds.Width;
            overlay.Height = imageBounds.Height;
            overlay.InvalidateVisual();

            imageBorderOutline.Width = imageBounds.Width;
            imageBorderOutline.Height = imageBounds.Height;

            if (imageBounds.Width <= 0 || imageBounds.Height <= 0)
                return;

            var contentRect = RectGeometry.GetImageContentRect(imageBounds, bitmapPixelSize);
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
                return;

            // Measure first, then hand the whole set to WorklogBadgeLayout, which decides where each
            // one goes. The anchored-vs-parked rule lives there rather than here because getting it
            // backwards was a reported bug and it is worth pinning with a fast unit test rather than
            // only through a full headless layout pass - see that class.
            var badges = new List<Border>();
            var requests = new List<WorklogBadgeLayout.BadgePlacementRequest>();

            foreach (var child in badgeCanvas.Children)
            {
                if (child is not Border badge)
                    continue;

                badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                // Tag carries the anchor rect for a "show marked area" entry, and stays null for one
                // without - see BuildSchematicPreview, which sets it.
                badges.Add(badge);
                requests.Add(new WorklogBadgeLayout.BadgePlacementRequest(
                    badge.DesiredSize,
                    badge.Tag is Rect pixelRect ? pixelRect : null));
            }

            var positions = WorklogBadgeLayout.ArrangeBadges(
                requests, contentRect, bitmapPixelSize, ParkedBadgeMargin, ParkedBadgeSpacing);

            for (int i = 0; i < badges.Count && i < positions.Count; i++)
            {
                Canvas.SetLeft(badges[i], positions[i].X);
                Canvas.SetTop(badges[i], positions[i].Y);
            }
        }

        // ###########################################################################################
        // Resolves the "Mark components in scope"/"Mark components completed" checklist for an entry.
        //
        // The computation is WorklogEntryScope.BuildComponentsInScope, the SAME call
        // TabSchematics.Worklog.cs makes - not a near-identical copy of it, which is what the two
        // were. This tab and that one are required to open the SAME modal, and a fix applied to one
        // copy and missed on the other is exactly the divergence already reported once.
        //
        // What differs is only where the two inputs come from. TabSchematics reads its own
        // highlightRectsBySchematicAndLabel field; this tab borrows that same cache off
        // MainWindow.TabSchematicsControl (via HighlightRectsBySchematicAndLabelForPreviews, the same
        // override-then-real-MainWindow seam CurrentBoardDataForPreviews uses), which is really
        // Main's - it writes it from four places and TabSchematics never assigns it.
        //
        // The cache being CURRENT here is not an accident of timing: Main routes every write to it
        // through SetComponentHighlightRects, which refreshes this pane as it writes, so a pill only
        // exists on screen against the cache it will be looked up in. Before that, the pane's pills
        // appeared while the board load's fire-and-forget task had yet to populate it, and a click in
        // that window silently dropped the checklist.
        // ###########################################################################################
        private List<(string BoardLabel, string DisplayName)>? BuildWorklogEntryComponentScope(WorklogEntryRecord entry) =>
            WorklogEntryScope.BuildComponentsInScope(
                this.CurrentBoardDataForPreviews,
                this.HighlightRectsBySchematicAndLabelForPreviews,
                entry);

        // Exposed to the test project so a component-scope test can call this directly without
        // driving a real pointer press through to WorklogEntryEditorWindow.ShowDialog, which blocks
        // headlessly with nothing to dismiss it (see A_pill_has_a_hand_cursor_and_the_canvas_it_sits_on_is_clickable's
        // own comment) - the same idea as SelectWorkbookForTests and SelectSchematicForTests.
        internal List<(string BoardLabel, string DisplayName)>? BuildWorklogEntryComponentScopeForTests(WorklogEntryRecord entry) =>
            this.BuildWorklogEntryComponentScope(entry);

        // ###########################################################################################
        // Opens a pill's worklog entry in the same editor window the Schematics tab's own "Show
        // worklogs" badges open - see TabSchematics.Worklog.cs.OnWorklogEntryPillPointerPressed,
        // which this mirrors, INCLUDING the component-scope checklist (BuildWorklogEntryComponentScope
        // above) - the modal opened from here is now the exact same modal the Schematics tab opens,
        // not a lookalike missing that section, which was a reported gap.
        //
        // The WHOLE body is inside the try, not just the post-await refresh. This is an async void
        // handler, so an exception anywhere in it - including before the first await - is rethrown on
        // the sync context with no caller to catch it and reaches App's global handler as a
        // PROCESS-FATAL crash, losing unsaved work in every other tab from one click on a pill. The
        // prologue is not exception-free: GetEntries reads and deserializes entries.json, Initialize
        // loads XAML and decodes photo thumbnails off disk, and ShowDialog itself can throw
        // synchronously. entries.json locked by AV, cloud sync or a second CRT instance, or one
        // corrupt photo thumbnail, is enough.
        //
        // thisIsOpeningEntryEditor guards re-entrancy: e.Handled stops this press reaching the
        // preview's own handler, but does nothing about a SECOND press while the first dialog is
        // still being constructed - ShowDialog is awaited, not blocking, so the dispatcher keeps
        // pumping input. A double-click, or pill #1 then pill #2 in quick succession, opened two
        // editors over the same entries.json, and UpdateEntry rewrites that file wholesale, so the
        // second save silently discarded the first. TabSchematics is partly shielded from this by
        // its own thisIsWorklogEntryMode flag; this pane had no equivalent.
        // ###########################################################################################
        // True while a pill's editor is being opened or is on screen. See
        // OnPreviewBadgePointerPressed for what it guards against.
        private bool thisIsOpeningEntryEditor;

        private async void OnPreviewBadgePointerPressed(int workbookId, int entryId, Bitmap schematicBitmap, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            // Without this, the press bubbles up to the preview's own click handler and selects
            // the schematic underneath the pill in addition to opening the entry's editor - both
            // firing off one click is not what either affordance is meant to do.
            e.Handled = true;

            if (this.thisIsOpeningEntryEditor)
                return;

            this.thisIsOpeningEntryEditor = true;

            try
            {
                var entry = WorklogManager.GetEntries(workbookId).FirstOrDefault(x => x.Id == entryId);
                if (entry == null)
                    return;

                if (TopLevel.GetTopLevel(this) is not Window ownerWindow)
                    return;

                var editor = new WorklogEntryEditorWindow();
                editor.Initialize(workbookId, entry, schematicBitmap);

                var componentsInScope = this.BuildWorklogEntryComponentScope(entry);
                if (componentsInScope != null)
                {
                    editor.InitializeComponentScope(componentsInScope);
                }

                await editor.ShowDialog(ownerWindow);

                if (!editor.WasSaved)
                    return;

                // ONE refresh, through Main: RefreshWorklogBar calls RefreshWorkbooks, which ends in
                // RefreshBoardPreviews - so a direct RefreshBoardPreviews() here as well simply
                // rebuilt the pane twice per save. With no MainWindow (headless tests) there is
                // nothing to funnel through, so the pane is rebuilt directly instead.
                if (this.MainWindow != null)
                {
                    this.MainWindow.RefreshWorklogBar();
                }
                else
                {
                    this.RefreshBoardPreviews();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Workbooks tab: failed to open or refresh after editing entry [#{entryId}]: [{ex.Message}]");
            }
            finally
            {
                this.thisIsOpeningEntryEditor = false;
            }
        }

        // ###########################################################################################
        // One "#N" badge + state pill, from the SAME WorklogBadgeBuilder the Schematics tab's own
        // "Show worklogs" view uses - so an entry looks identical wherever it is drawn, rather than
        // by two copies of the same forty-five lines each asserting that it must.
        //
        // No ScaleTransform here: unlike the Schematics tab's badges, this pane never zooms, so the
        // badge is drawn at its natural size with no inverse-scale compensation. The click handler
        // that opens the entry's editor is wired up by the caller (BuildSchematicPreview).
        // ###########################################################################################
        private static Border BuildPreviewBadge(WorklogEntryRecord entry, Color categoryColor) =>
            WorklogBadgeBuilder.Build(entry, categoryColor, ResolveWorklogStateColor(entry.State));

        // ###########################################################################################
        // Resolves a saved entry's category colour - the same Worklog_Category_{category} theme
        // resources TabSchematics.Worklog.cs's ResolveWorklogCategoryColor reads, so a "Cosmetic"
        // entry is the same blue everywhere it is drawn.
        // ###########################################################################################
        private static Color ResolveWorklogCategoryColor(string category) =>
            ThemeResources.ResolveColor($"Worklog_Category_{category}", Colors.IndianRed);

        // Mirrors TabSchematics.Worklog.cs's ResolveWorklogStateColor: Closed is green, anything
        // else (including an unrecognised future value) reads as open/red.
        private static Color ResolveWorklogStateColor(string state) =>
            ThemeResources.ResolveColor(
                WorklogManager.IsResolvedState(state) ? "Worklog_Status_Closed" : "Worklog_Status_Open",
                Colors.IndianRed);

        // See ThemeResources for why a ThemeDictionaries-scoped key needs the two-step
        // Application.Current + ActualThemeVariant lookup. Falls back to IndianRed, matching every
        // other worklog colour resolver in this app when a key is somehow missing.
        private static IBrush ResolveThemeBrushStatic(string resourceKey) =>
            ThemeResources.ResolveBrush(resourceKey);
    }
}
