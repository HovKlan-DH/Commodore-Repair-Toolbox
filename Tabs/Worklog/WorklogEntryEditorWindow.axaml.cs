using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Handlers.DataHandling;
using Handlers.Geometry;

namespace CRT
{
    // ###########################################################################################
    // The full worklog entry editor: opened by clicking a saved entry's pill on the schematic (see
    // TabSchematics.Worklog.cs's OnWorklogEntryPillPointerPressed). Edits everything the quick
    // "New fault" card can (title/description/category/state) plus the entry's own Links/Comments/
    // WorkDoneItems/Photos/Files sub-lists, and shows a read-only preview of where the entry's
    // marked area sits on its schematic.
    //
    // Works on a private working copy of the WorklogEntryRecord (thisEntry) built from the caller's
    // record; nothing is written back to disk until Save calls WorklogManager.UpdateEntry.
    // Cancel/closing the window discards the working copy entirely.
    // ###########################################################################################
    public partial class WorklogEntryEditorWindow : Window
    {
        private int thisWorkbookId;
        private WorklogEntryRecord thisEntry = new();
        private Bitmap? thisSchematicBitmap;

        private string thisSelectedCategory = "Note";
        private string thisSelectedState = "Open";

        private readonly ObservableCollection<WorklogLinkRow> thisLinkRows = new();
        private readonly ObservableCollection<WorklogCommentRow> thisCommentRows = new();
        private readonly ObservableCollection<WorklogWorkDoneRow> thisWorkDoneRows = new();
        private readonly ObservableCollection<WorklogAttachmentRow> thisPhotoRows = new();
        private readonly ObservableCollection<WorklogAttachmentRow> thisFileRows = new();

        // "Mark components in scope". Populated only when the caller supplies the components -
        // this window has no board data and no highlight rectangles of its own, so it cannot work
        // out which components an area touches; see InitializeComponentScope.
        private readonly ObservableCollection<WorklogEntryComponentRow> thisComponentRows = new();

        // Whether the caller supplied a scope at all. Distinct from "the list is empty": an area
        // that genuinely touches nothing shows "No components in this area", whereas an unknown
        // scope hides the section and, crucially, leaves the entry's saved ComponentLabels alone
        // rather than overwriting them with an empty list.
        private bool thisHasComponentScope;

        // Newest-first is the default sort for both lists - persisted globally via UserSettings so it
        // carries over between entries and app restarts, rather than resetting every time this window
        // is opened.
        private bool thisCommentsSortNewestFirst = UserSettings.WorklogCommentsSortNewestFirst;
        private bool thisWorkDoneSortNewestFirst = UserSettings.WorklogWorkDoneSortNewestFirst;

        // Guards against Initialize()'s own seeding of the direct fields (Title/Description text,
        // category/state selection) being mistaken for a user edit and enabling Save prematurely.
        private bool thisIsInitializing;

        // Set by PersistEntrySilently, so that even a Cancel/Escape close reports WasSaved = true
        // when a Links/Comments/Work-done/Photos/Files change already made it to disk.
        private bool thisHasPersistedChange;

        public bool WasSaved { get; private set; }

        public WorklogEntryEditorWindow()
        {
            this.InitializeComponent();

            this.EditorLinksList.ItemsSource = this.thisLinkRows;
            this.EditorCommentsList.ItemsSource = this.thisCommentRows;
            this.EditorWorkDoneList.ItemsSource = this.thisWorkDoneRows;
            this.EditorPhotosList.ItemsSource = this.thisPhotoRows;
            this.EditorFilesList.ItemsSource = this.thisFileRows;
            this.EditorComponentList.ItemsSource = this.thisComponentRows;

            // The marker's position is computed from EditorLocationPreviewGrid's OWN size, so the
            // redraw has to be driven by that grid rather than by the window. Dragging the
            // GridSplitter re-widths the preview column while the window's size never changes, so
            // a window-level SizeChanged does not fire and the marker kept coordinates computed
            // for the previous width - it drifted away from the area it is meant to mark, and only
            // snapped back when the window itself was resized.
            this.EditorLocationPreviewGrid.SizeChanged += (_, _) => this.RefreshLocationPreviewOverlay();

            this.AddHandler(KeyDownEvent, this.OnWindowPreviewKeyDown, RoutingStrategies.Tunnel);

            // The photo drag's move/release live on the LIST, not on the row that started it: the
            // dragged row is re-rendered as an empty placeholder the moment the drag begins, which
            // takes its own handlers out of the tree, and the row also moves out from under the
            // pointer as the list reorders. The list stays put for the whole gesture.
            // Tunnel so a release over a row's buttons still ends the drag rather than being eaten.
            this.EditorPhotosList.AddHandler(PointerMovedEvent, this.OnPhotoRowDragHandlePointerMoved, RoutingStrategies.Tunnel);
            this.EditorPhotosList.AddHandler(PointerReleasedEvent, this.OnPhotoRowDragHandlePointerReleased, RoutingStrategies.Tunnel);

            // The Files list drags through the same handlers - which list is being reordered comes
            // from the DragContext captured on press, not from which control raised the event.
            this.EditorFilesList.AddHandler(PointerMovedEvent, this.OnPhotoRowDragHandlePointerMoved, RoutingStrategies.Tunnel);
            this.EditorFilesList.AddHandler(PointerReleasedEvent, this.OnPhotoRowDragHandlePointerReleased, RoutingStrategies.Tunnel);

            // A release outside the list (dragged past the window edge, say) never reaches the
            // handlers above, which would strand the placeholder as a permanent empty slot. The
            // window-level handler commits the drop at wherever the placeholder currently sits.
            //
            // BUBBLE, not Tunnel. The window is the root, so on the tunnelling route it would fire
            // BEFORE the lists and commit every in-list drop itself - making the lists' own handlers
            // dead code and this "fallback" the actual primary path. Bubbling runs it last, so it
            // only ever sees a release the lists did not already handle, which is what the comment
            // above describes and what the release handler's early-return assumes.
            this.AddHandler(PointerReleasedEvent, this.OnPhotoRowDragHandlePointerReleased, RoutingStrategies.Bubble);

            // The thumbnails this window decoded hold unmanaged surfaces; without this the last set
            // survives the window itself. thisSchematicBitmap belongs to the caller and is not
            // touched here.
            this.Closed += (_, _) =>
            {
                foreach (var row in this.thisPhotoRows)
                {
                    row.Thumbnail?.Dispose();
                }

                // Teardown matches construction. A gesture still in flight when the window closes
                // (released outside it, so no release handler ever ran) leaves thisActiveDragContext
                // holding the entry's live lists and two bound delegates; clearing the collections
                // and the drag state drops those references with the window instead of after it.
                this.ResetPhotoDragState();

                this.thisPhotoRows.Clear();
                this.thisFileRows.Clear();
                this.thisLinkRows.Clear();
                this.thisCommentRows.Clear();
                this.thisWorkDoneRows.Clear();
                this.thisComponentRows.Clear();
            };
        }

        // ###########################################################################################
        // Escape acts like Cancel, same as the quick "New fault" card's Escape handling. Plain Enter
        // in the single-line Title field saves and closes (Title has no use for a literal newline);
        // in the multi-line Description field (AcceptsReturn) plain Enter is left alone so it keeps
        // inserting a newline, and only Ctrl+Enter saves - same convention as WorklogAddCommentWindow.
        // Handled on the Tunnel route so this runs before Description's own AcceptsReturn handling
        // inserts a newline - a bubbling KeyDown handler would run too late to stop that. Save only
        // actually commits when it is enabled (a direct field has been edited); otherwise Enter/
        // Ctrl+Enter is a no-op, same as clicking a disabled Save button would be.
        // ###########################################################################################
        private void OnWindowPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.OnCancelClick(sender, e);
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Enter)
                return;

            bool isDescriptionFocused = ReferenceEquals(e.Source, this.EditorDescriptionTextBox);
            if (isDescriptionFocused && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
                return;

            bool isTitleFocused = ReferenceEquals(e.Source, this.EditorTitleTextBox);
            if (!isDescriptionFocused && !isTitleFocused)
                return;

            e.Handled = true;

            if (this.EditorSaveButton.IsEnabled)
            {
                this.OnSaveClick(sender, e);
            }
        }

        // ###########################################################################################
        // Must be called before showing the dialog: seeds every field/list from the given entry and
        // loads the schematic bitmap for the location preview. workbookId is needed separately since
        // WorklogEntryRecord itself does not know which workbook it belongs to.
        // ###########################################################################################
        public void Initialize(int workbookId, WorklogEntryRecord entry, Bitmap? schematicBitmap)
        {
            this.thisIsInitializing = true;

            this.thisWorkbookId = workbookId;
            this.thisEntry = CloneEntry(entry);
            this.thisSchematicBitmap = schematicBitmap;

            this.EditorIdText.Text = $"#{this.thisEntry.Id}";
            this.EditorTitleTextBox.Text = this.thisEntry.Title;
            this.EditorDescriptionTextBox.Text = this.thisEntry.Description;
            this.EditorLocationSchematicNameText.Text = this.thisEntry.SchematicName;

            this.thisSelectedCategory = string.IsNullOrWhiteSpace(this.thisEntry.Category) ? "Note" : this.thisEntry.Category;
            this.thisSelectedState = string.IsNullOrWhiteSpace(this.thisEntry.State) ? "Open" : this.thisEntry.State;
            this.UpdateCategoryChipVisuals();
            this.UpdateStatePillVisuals();

            // Heals duplicate/gapped DisplayOrder values left by older builds before anything is
            // rendered, so the list cannot show two rows in an arbitrary order. Working-copy only -
            // it reaches disk with the next save rather than writing on open.
            WorklogAttachmentStorage.NormalizeDisplayOrder(this.thisEntry.Photos);
            WorklogAttachmentStorage.NormalizeDisplayOrder(this.thisEntry.Files);

            this.RefreshLinkRows();
            this.RefreshCommentRows();
            this.RefreshWorkDoneRows();
            this.RefreshPhotoRows();
            this.RefreshFileRows();

            this.EditorLocationPreviewImage.Source = this.thisSchematicBitmap;
            this.RefreshLocationPreviewOverlay();

            this.thisIsInitializing = false;
            this.EditorSaveButton.IsEnabled = false;
        }

        // ###########################################################################################
        // The Save button starts disabled and is only ever enabled by an edit to one of the direct
        // fields (Title, Description, category, state) - see OnDirectFieldTextChanged and the
        // category/state pointer handlers below. Everything else (links/comments/work done, and
        // delete/reorder on any sub-list) saves itself instantly via PersistEntrySilently, so losing
        // those was never a matter of forgetting to click Save.
        // ###########################################################################################
        private void MarkDirty()
        {
            if (this.thisIsInitializing)
                return;

            this.EditorSaveButton.IsEnabled = true;
        }

        private void OnDirectFieldTextChanged(object? sender, TextChangedEventArgs e)
        {
            this.MarkDirty();
        }

        // ###########################################################################################
        // Copies the direct fields (Title/Description/category/state) out of their controls and into
        // the working copy. Every write to disk must go through this first, because the working copy
        // is only ever updated here - the controls are the live value until it runs.
        // ###########################################################################################
        private void SyncDirectFieldsToEntry()
        {
            this.thisEntry.Title = this.EditorTitleTextBox.Text?.Trim() ?? string.Empty;
            this.thisEntry.Description = this.EditorDescriptionTextBox.Text?.Trim() ?? string.Empty;
            this.thisEntry.Category = this.thisSelectedCategory;
            this.thisEntry.State = this.thisSelectedState;

            // Only when a scope was actually supplied. If the caller could not determine it, the
            // checklist was never shown and the rows are empty - writing that back would silently
            // clear a component list the user never saw, let alone chose to empty.
            //
            // Labels the checklist never offered are CARRIED OVER rather than dropped. The rows
            // come from the highlight rectangles as they are right now, so a label saved earlier
            // whose component has since been renamed or removed from the board data has no row to
            // tick - and this method runs on every instant-save (adding a photo, deleting a file,
            // any drag reorder), not just on Save. Without this the user would lose that label the
            // moment they touched anything unrelated, with no Save click and nothing to notice.
            if (this.thisHasComponentScope)
            {
                var offered = new HashSet<string>(
                    this.thisComponentRows.Select(r => r.BoardLabel),
                    StringComparer.OrdinalIgnoreCase);

                var keptFromBeforeOpening = (this.thisEntry.ComponentLabels ?? new List<string>())
                    .Where(label => !offered.Contains(label))
                    .ToList();

                this.thisEntry.ComponentLabels = this.thisComponentRows
                    .Where(r => r.IsChecked)
                    .Select(r => r.BoardLabel)
                    .Concat(keptFromBeforeOpening)
                    .ToList();
            }
        }

        // ###########################################################################################
        // Supplies the "Mark components in scope" checklist. Called by the opener straight after
        // Initialize, because working out which components an entry's area touches needs the board
        // data and the per-schematic highlight rectangles - both of which live in TabSchematics,
        // not here. This window just renders what it is given and reports back the ticked rows.
        //
        // Each row starts ticked if its label is already in the entry's saved ComponentLabels, so
        // reopening an entry shows the choice the user made last time rather than re-ticking
        // everything. That is the one behavioural difference from the quick "New fault" card,
        // where every row starts ticked because the entry has no saved selection yet.
        // ###########################################################################################
        public void InitializeComponentScope(IReadOnlyList<(string BoardLabel, string DisplayName)> componentsInScope)
        {
            // Populating the checklist is not an edit. The flag is re-raised because Initialize has
            // already cleared it by the time this runs, and building the rows drives the CheckBox
            // bindings - without this the window opened with Save already enabled, making every
            // entry look modified before the user had touched anything.
            this.thisIsInitializing = true;

            try
            {
                this.PopulateComponentScope(componentsInScope);
            }
            finally
            {
                this.thisIsInitializing = false;
            }

            // Set last and outside the guard: Save must be disabled on open regardless of what the
            // binding traffic above did.
            this.EditorSaveButton.IsEnabled = false;
        }

        private void PopulateComponentScope(IReadOnlyList<(string BoardLabel, string DisplayName)> componentsInScope)
        {
            this.thisHasComponentScope = true;
            this.thisComponentRows.Clear();

            var alreadySelected = new HashSet<string>(
                this.thisEntry.ComponentLabels ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var component in componentsInScope)
            {
                this.thisComponentRows.Add(new WorklogEntryComponentRow
                {
                    BoardLabel = component.BoardLabel,
                    DisplayName = component.DisplayName,
                    IsChecked = alreadySelected.Contains(component.BoardLabel)
                });
            }

            this.EditorComponentScopePanel.IsVisible = true;
            this.EditorComponentCountText.Text = $"{this.thisComponentRows.Count} found";
            this.EditorNoComponentsText.IsVisible = this.thisComponentRows.Count == 0;
        }

        // ###########################################################################################
        // "All" / "None" bulk links, and whole-row click-to-toggle - the same interactions the quick
        // card's checklist offers. Each marks the window dirty so the Save button enables, since
        // changing the scope is a real edit to the entry.
        // ###########################################################################################
        private void OnEditorSelectAllComponentsClick(object? sender, RoutedEventArgs e)
        {
            foreach (var row in this.thisComponentRows)
            {
                row.IsChecked = true;
            }

            this.MarkDirty();
        }

        private void OnEditorSelectNoneComponentsClick(object? sender, RoutedEventArgs e)
        {
            foreach (var row in this.thisComponentRows)
            {
                row.IsChecked = false;
            }

            this.MarkDirty();
        }

        // The checkbox and both labels are IsHitTestVisible="False", so this Border handler is the
        // only thing that sees the click - which is what makes the whole row a hit target.
        private void OnEditorComponentRowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is WorklogEntryComponentRow row)
            {
                row.IsChecked = !row.IsChecked;
                this.MarkDirty();
            }
        }

        // ###########################################################################################
        // Persists the working copy immediately, the same way Save does, but without touching
        // WasSaved or closing the window - used after every add/edit/delete on the Links/Comments/
        // Work done/Photos/Files sub-lists so none of that is lost if the window is later closed via
        // Cancel or Escape without the direct fields ever having been touched.
        //
        // It syncs the direct fields first, and MUST keep doing so. Without that, adding a comment
        // after retyping the headline wrote the record with the OLD headline and state, silently
        // reverting what the user had just typed - the sub-list write cannot save "only its half"
        // of the record, because UpdateEntry replaces the whole thing. A consequence worth knowing:
        // an instant-save therefore commits in-progress direct-field edits too, so Cancel can no
        // longer discard them. That is deliberate - it matches what is on screen, and Cancel already
        // could not undo an instant-saved sub-list change.
        // ###########################################################################################
        // Returns whether THIS save reached disk. thisHasPersistedChange cannot answer that - it is
        // sticky for the window's lifetime so Cancel can still report WasSaved - so a caller that
        // must not act on a failed save (deleting an attachment's bytes, say) reads this instead.
        private bool PersistEntrySilently()
        {
            this.SyncDirectFieldsToEntry();

            if (WorklogManager.UpdateEntry(this.thisWorkbookId, this.thisEntry))
            {
                this.thisHasPersistedChange = true;
                this.EditorSaveFailedText.IsVisible = false;
                return true;
            }

            // "Silently" covers not closing the window and not touching WasSaved - not hiding a
            // failure. The sub-list change the user just made is only in the working copy.
            this.ShowSaveFailed(DefaultSaveFailedMessage);
            return false;
        }

        private const string DefaultSaveFailedMessage = "Could not save - see the log for details.";

        // ###########################################################################################
        // Shows a failure in the footer's status line. Always sets the text rather than only the
        // visibility, because the line is shared: an attachment failure writes its own wording, and
        // without rewriting it a later ordinary save failure would report the attachment's problem.
        // ###########################################################################################
        private void ShowSaveFailed(string message)
        {
            this.EditorSaveFailedText.Text = message;
            this.EditorSaveFailedText.IsVisible = true;
        }

        // ###########################################################################################
        // Deep-enough copy so editing in this window (including list add/delete) cannot mutate the
        // caller's record until Save explicitly commits it back via WorklogManager.UpdateEntry.
        //
        // Every sub-list is null-coalesced. WorklogManager.ReadEntries already normalizes what it
        // loads (see NormalizeEntryCollections there, and why System.Text.Json can produce nulls
        // despite the "= new()" initializers), but this takes a record from a caller rather than
        // straight from disk, and it runs in Initialize before the window is shown - so an
        // unguarded dereference here would throw before the user ever saw the editor.
        // ###########################################################################################
        private static WorklogEntryRecord CloneEntry(WorklogEntryRecord source)
        {
            return new WorklogEntryRecord
            {
                Id = source.Id,
                SchematicName = source.SchematicName,
                AreaX = source.AreaX,
                AreaY = source.AreaY,
                AreaWidth = source.AreaWidth,
                AreaHeight = source.AreaHeight,
                Title = source.Title,
                Description = source.Description,
                Category = source.Category,
                State = source.State,
                ComponentLabels = source.ComponentLabels?.ToList() ?? new(),
                CreatedDate = source.CreatedDate,
                Links = source.Links?.Select(l => new WorklogLinkRecord { Id = l.Id, Headline = l.Headline, Url = l.Url }).ToList() ?? new(),
                Comments = source.Comments?.Select(c => new WorklogCommentRecord { Id = c.Id, Text = c.Text, Date = c.Date }).ToList() ?? new(),
                WorkDoneItems = source.WorkDoneItems?.Select(w => new WorklogWorkDoneRecord { Id = w.Id, Text = w.Text, Date = w.Date, HoursSpent = w.HoursSpent, Cost = w.Cost }).ToList() ?? new(),
                Photos = source.Photos?.Select(p => new WorklogAttachmentRecord { Id = p.Id, FileName = p.FileName, Comment = p.Comment, DisplayOrder = p.DisplayOrder }).ToList() ?? new(),
                Files = source.Files?.Select(f => new WorklogAttachmentRecord { Id = f.Id, FileName = f.FileName, Comment = f.Comment, DisplayOrder = f.DisplayOrder }).ToList() ?? new(),
            };
        }

        // ###########################################################################################
        // Resolves a theme brush by key, falling back when the resource cannot be found - same idiom
        // TabSchematics.ResolveThemeBrush uses, including its Application.Current fallback: this
        // window's ThemeVariant-keyed resources (Worklog_Category_*, Worklog_Status_Closed, etc.) live
        // in App.axaml's ResourceDictionary.ThemeDictionaries, and plain TryFindResource does not
        // always resolve a themed key by itself - without this second lookup every category chip and
        // state pill silently fell back to the caller's fallback color instead of its real one.
        // ###########################################################################################
        private IBrush ResolveThemeBrush(string key, IBrush fallback)
        {
            if (this.TryFindResource(key, out var localResource) && localResource is IBrush localBrush)
                return localBrush;

            if (Application.Current != null)
            {
                var theme = Application.Current.ActualThemeVariant;
                if (Application.Current.TryGetResource(key, theme, out var appResource) && appResource is IBrush appBrush)
                    return appBrush;
            }

            return fallback;
        }

        private Color ResolveCategoryColor(string category)
        {
            var brush = this.ResolveThemeBrush($"Worklog_Category_{category}", new SolidColorBrush(Colors.IndianRed));
            return brush is ISolidColorBrush solidBrush ? solidBrush.Color : Colors.IndianRed;
        }

        // ###########################################################################################
        // Draws the entry's marked-area rectangle over the (fully visible, unzoomed) schematic
        // preview image on the right - a static reference showing where on the board this entry
        // applies, not an interactive viewer.
        // ###########################################################################################
        private void RefreshLocationPreviewOverlay()
        {
            this.EditorLocationPreviewOverlayCanvas.Children.Clear();

            if (this.thisSchematicBitmap == null)
                return;

            var controlSize = this.EditorLocationPreviewGrid.Bounds.Size;
            if (controlSize.Width <= 0 || controlSize.Height <= 0)
                return;

            // Centered, not origin-anchored: EditorLocationPreviewImage is Stretch="Uniform" with no
            // alignment set, so Avalonia centres the content in the fixed-height preview box. Using
            // the origin-anchored GetImageContentRect drew the marker off by half the letterbox -
            // pointing at the wrong part of the board, and clipped away entirely on tall schematics.
            var contentRect = RectGeometry.GetCenteredImageContentRect(controlSize, this.thisSchematicBitmap.PixelSize);
            var pixelRect = new Rect(this.thisEntry.AreaX, this.thisEntry.AreaY, this.thisEntry.AreaWidth, this.thisEntry.AreaHeight);
            var localRect = RectGeometry.PixelToLocalRect(pixelRect, contentRect, this.thisSchematicBitmap.PixelSize);

            var color = this.ResolveCategoryColor(this.thisSelectedCategory);

            var marker = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = Math.Max(1, localRect.Width),
                Height = Math.Max(1, localRect.Height),
                Fill = new SolidColorBrush(color, 0.18),
                Stroke = new SolidColorBrush(color, 1.0),
                StrokeThickness = 2,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 }
            };

            Canvas.SetLeft(marker, localRect.X);
            Canvas.SetTop(marker, localRect.Y);
            this.EditorLocationPreviewOverlayCanvas.Children.Add(marker);
        }

        private void OnEditorCategoryChipPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border { Tag: string category })
            {
                this.thisSelectedCategory = category;
                this.UpdateCategoryChipVisuals();
                this.RefreshLocationPreviewOverlay();
                this.MarkDirty();
            }
        }

        private void UpdateCategoryChipVisuals()
        {
            this.ApplyCategoryChipVisualState(this.EditorCategoryNoteChip, this.EditorCategoryNoteText, "Note");
            this.ApplyCategoryChipVisualState(this.EditorCategoryCosmeticChip, this.EditorCategoryCosmeticText, "Cosmetic");
            this.ApplyCategoryChipVisualState(this.EditorCategoryIssueChip, this.EditorCategoryIssueText, "Issue");

            this.EditorIdBadge.Background = new SolidColorBrush(this.ResolveCategoryColor(this.thisSelectedCategory));
        }

        // Text only - see UpdateWorklogEntryCategoryChipVisuals in TabSchematics.Worklog.cs for why
        // the chips carry no colour dot.
        private void ApplyCategoryChipVisualState(Border chip, TextBlock label, string category)
        {
            var categoryBrush = this.ResolveThemeBrush($"Worklog_Category_{category}", new SolidColorBrush(Colors.IndianRed));

            if (string.Equals(this.thisSelectedCategory, category, StringComparison.Ordinal))
            {
                chip.Background = categoryBrush;
                chip.BorderBrush = categoryBrush;
                chip.BorderThickness = new Thickness(2);
                chip.Opacity = 0.9;
                label.Foreground = Brushes.White;
                label.FontWeight = FontWeight.SemiBold;
            }
            else
            {
                chip.Background = this.ResolveThemeBrush("Form_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
                chip.BorderBrush = this.ResolveThemeBrush("Form_Border", new SolidColorBrush(Color.Parse("#CCCCCC")));
                chip.BorderThickness = new Thickness(1);
                chip.Opacity = 1.0;
                label.Foreground = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);
                label.FontWeight = FontWeight.Normal;
            }
        }

        private void OnEditorStatePillPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border { Tag: string state })
            {
                this.thisSelectedState = state;
                this.UpdateStatePillVisuals();
                this.MarkDirty();
            }
        }

        private void UpdateStatePillVisuals()
        {
            this.ApplyStatePillVisualState(this.EditorStateOpenPill, this.EditorStateOpenText, "Open", "Worklog_Status_Open");
            this.ApplyStatePillVisualState(this.EditorStateClosedPill, this.EditorStateClosedText, "Closed", "Worklog_Status_Closed");
        }

        // The dot is left at its state colour in both pills (it is the state's identity, not a
        // selection cue) - only the outline and label weight mark which one is selected, matching
        // the category chips directly above.
        private void ApplyStatePillVisualState(Border pill, TextBlock label, string state, string colorResourceKey)
        {
            var stateBrush = this.ResolveThemeBrush(colorResourceKey, new SolidColorBrush(Colors.IndianRed));

            if (string.Equals(this.thisSelectedState, state, StringComparison.Ordinal))
            {
                pill.Background = this.ResolveThemeBrush("Schematics_Panels_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
                pill.BorderBrush = stateBrush;
                pill.BorderThickness = new Thickness(2);
                label.Foreground = stateBrush;
                label.FontWeight = FontWeight.SemiBold;
            }
            else
            {
                pill.Background = this.ResolveThemeBrush("Form_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
                pill.BorderBrush = this.ResolveThemeBrush("Form_Border", new SolidColorBrush(Color.Parse("#CCCCCC")));
                pill.BorderThickness = new Thickness(1);
                label.Foreground = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);
                label.FontWeight = FontWeight.Normal;
            }
        }

        // ###########################################################################################
        // Links of interest
        // ###########################################################################################
        private void RefreshLinkRows()
        {
            this.thisLinkRows.Clear();
            foreach (var link in this.thisEntry.Links)
            {
                this.thisLinkRows.Add(new WorklogLinkRow { Id = link.Id, Headline = link.Headline, Url = link.Url });
            }
            this.EditorNoLinksText.IsVisible = this.thisLinkRows.Count == 0;
        }

        private async void OnAddLinkClick(object? sender, RoutedEventArgs e)
        {
            var dialog = new WorklogAddLinkWindow();
            var result = await dialog.ShowDialog<(string Headline, string Url)?>(this);
            if (result == null)
                return;

            int nextId = this.thisEntry.Links.Count == 0 ? 1 : this.thisEntry.Links.Max(l => l.Id) + 1;
            this.thisEntry.Links.Add(new WorklogLinkRecord { Id = nextId, Headline = result.Value.Headline, Url = result.Value.Url });
            this.RefreshLinkRows();
            this.PersistEntrySilently();
        }

        private void OnDeleteLinkClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                this.thisEntry.Links.RemoveAll(l => l.Id == id);
                this.RefreshLinkRows();
                this.PersistEntrySilently();
            }
        }

        // ###########################################################################################
        // Clicking anywhere on a link row (other than its Edit/Delete icons, which handle their own
        // Click and so never reach here) opens the link in the system browser, via the same sanctioned
        // launcher the rest of the app uses for external URLs.
        // ###########################################################################################
        private void OnLinkRowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border { Tag: string url } && !string.IsNullOrWhiteSpace(url))
            {
                ExternalTargetLauncher.TryOpen(url);
            }
        }

        private async void OnEditLinkClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: int id })
                return;

            var link = this.thisEntry.Links.FirstOrDefault(l => l.Id == id);
            if (link == null)
                return;

            var dialog = new WorklogAddLinkWindow();
            dialog.InitializeForEdit(link.Headline, link.Url);
            var result = await dialog.ShowDialog<(string Headline, string Url)?>(this);
            if (result == null)
                return;

            link.Headline = result.Value.Headline;
            link.Url = result.Value.Url;
            this.RefreshLinkRows();
            this.PersistEntrySilently();
        }

        // ###########################################################################################
        // Comments
        // ###########################################################################################
        private void RefreshCommentRows()
        {
            this.thisCommentRows.Clear();
            var orderedComments = this.thisCommentsSortNewestFirst
                ? this.thisEntry.Comments.OrderByDescending(c => c.Date)
                : this.thisEntry.Comments.OrderBy(c => c.Date);
            foreach (var comment in orderedComments)
            {
                this.thisCommentRows.Add(new WorklogCommentRow
                {
                    Id = comment.Id,
                    Text = comment.Text,
                    DateText = comment.Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                });
            }

            this.UpdateCommentsSortIconVisuals();
        }

        private void OnCommentsSortNewestFirstClick(object? sender, RoutedEventArgs e)
        {
            this.thisCommentsSortNewestFirst = true;
            UserSettings.WorklogCommentsSortNewestFirst = true;
            this.RefreshCommentRows();
        }

        private void OnCommentsSortOldestFirstClick(object? sender, RoutedEventArgs e)
        {
            this.thisCommentsSortNewestFirst = false;
            UserSettings.WorklogCommentsSortNewestFirst = false;
            this.RefreshCommentRows();
        }

        private void UpdateCommentsSortIconVisuals()
        {
            var activeBrush = this.ResolveThemeBrush("Main_TabUnderline_Selected", new SolidColorBrush(Colors.IndianRed));
            var inactiveBrush = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);

            this.CommentsSortNewestFirstIcon.Foreground = this.thisCommentsSortNewestFirst ? activeBrush : inactiveBrush;
            this.CommentsSortOldestFirstIcon.Foreground = !this.thisCommentsSortNewestFirst ? activeBrush : inactiveBrush;
        }

        private async void OnAddCommentClick(object? sender, RoutedEventArgs e)
        {
            var dialog = new WorklogAddCommentWindow();
            var result = await dialog.ShowDialog<string?>(this);
            if (string.IsNullOrWhiteSpace(result))
                return;

            int nextId = this.thisEntry.Comments.Count == 0 ? 1 : this.thisEntry.Comments.Max(c => c.Id) + 1;
            this.thisEntry.Comments.Add(new WorklogCommentRecord { Id = nextId, Text = result.Trim(), Date = DateTime.Now });
            this.RefreshCommentRows();
            this.PersistEntrySilently();
        }

        private void OnDeleteCommentClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                this.thisEntry.Comments.RemoveAll(c => c.Id == id);
                this.RefreshCommentRows();
                this.PersistEntrySilently();
            }
        }

        // ###########################################################################################
        // Clicking anywhere on a comment row (other than its Delete icon, which handles its own Click
        // and so never reaches here) reopens the same modal Add-comment uses, pre-filled for editing.
        // ###########################################################################################
        private async void OnCommentRowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border { Tag: int id })
                return;

            var comment = this.thisEntry.Comments.FirstOrDefault(c => c.Id == id);
            if (comment == null)
                return;

            var dialog = new WorklogAddCommentWindow();
            dialog.InitializeForEdit(comment.Text);
            var result = await dialog.ShowDialog<string?>(this);
            if (string.IsNullOrWhiteSpace(result))
                return;

            comment.Text = result.Trim();
            this.RefreshCommentRows();
            this.PersistEntrySilently();
        }

        // ###########################################################################################
        // Work done
        // ###########################################################################################
        private void RefreshWorkDoneRows()
        {
            this.thisWorkDoneRows.Clear();
            var orderedWork = this.thisWorkDoneSortNewestFirst
                ? this.thisEntry.WorkDoneItems.OrderByDescending(w => w.Date)
                : this.thisEntry.WorkDoneItems.OrderBy(w => w.Date);
            foreach (var work in orderedWork)
            {
                this.thisWorkDoneRows.Add(new WorklogWorkDoneRow
                {
                    Id = work.Id,
                    Text = work.Text,
                    DateText = work.Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    SummaryText = $"{work.HoursSpent:0.##} h · {work.Cost:0.##}"
                });
            }

            double totalHours = this.thisEntry.WorkDoneItems.Sum(w => w.HoursSpent);
            double totalCost = this.thisEntry.WorkDoneItems.Sum(w => w.Cost);
            this.EditorWorkDoneHeaderText.Text = $"Work done (total {totalHours:0.##} h · {totalCost:0.##})";

            this.UpdateWorkDoneSortIconVisuals();
        }

        private void OnWorkDoneSortNewestFirstClick(object? sender, RoutedEventArgs e)
        {
            this.thisWorkDoneSortNewestFirst = true;
            UserSettings.WorklogWorkDoneSortNewestFirst = true;
            this.RefreshWorkDoneRows();
        }

        private void OnWorkDoneSortOldestFirstClick(object? sender, RoutedEventArgs e)
        {
            this.thisWorkDoneSortNewestFirst = false;
            UserSettings.WorklogWorkDoneSortNewestFirst = false;
            this.RefreshWorkDoneRows();
        }

        private void UpdateWorkDoneSortIconVisuals()
        {
            var activeBrush = this.ResolveThemeBrush("Main_TabUnderline_Selected", new SolidColorBrush(Colors.IndianRed));
            var inactiveBrush = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);

            this.WorkDoneSortNewestFirstIcon.Foreground = this.thisWorkDoneSortNewestFirst ? activeBrush : inactiveBrush;
            this.WorkDoneSortOldestFirstIcon.Foreground = !this.thisWorkDoneSortNewestFirst ? activeBrush : inactiveBrush;
        }

        private async void OnAddWorkDoneClick(object? sender, RoutedEventArgs e)
        {
            var dialog = new WorklogAddWorkDoneWindow();
            var result = await dialog.ShowDialog<(string Text, double HoursSpent, double Cost)?>(this);
            if (result == null)
                return;

            int nextId = this.thisEntry.WorkDoneItems.Count == 0 ? 1 : this.thisEntry.WorkDoneItems.Max(w => w.Id) + 1;
            this.thisEntry.WorkDoneItems.Add(new WorklogWorkDoneRecord
            {
                Id = nextId,
                Text = result.Value.Text,
                Date = DateTime.Now,
                HoursSpent = result.Value.HoursSpent,
                Cost = result.Value.Cost
            });
            this.RefreshWorkDoneRows();
            this.PersistEntrySilently();
        }

        private void OnDeleteWorkDoneClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                this.thisEntry.WorkDoneItems.RemoveAll(w => w.Id == id);
                this.RefreshWorkDoneRows();
                this.PersistEntrySilently();
            }
        }

        // ###########################################################################################
        // Clicking anywhere on a work-done row (other than its Delete icon, which handles its own
        // Click and so never reaches here) reopens the same modal "Add work" uses, pre-filled for
        // editing - same click-to-edit behavior as the Links and Comments rows.
        // ###########################################################################################
        private async void OnWorkDoneRowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border { Tag: int id })
                return;

            var work = this.thisEntry.WorkDoneItems.FirstOrDefault(w => w.Id == id);
            if (work == null)
                return;

            var dialog = new WorklogAddWorkDoneWindow();
            dialog.InitializeForEdit(work.Text, work.HoursSpent, work.Cost);
            var result = await dialog.ShowDialog<(string Text, double HoursSpent, double Cost)?>(this);
            if (result == null)
                return;

            work.Text = result.Value.Text;
            work.HoursSpent = result.Value.HoursSpent;
            work.Cost = result.Value.Cost;
            this.RefreshWorkDoneRows();
            this.PersistEntrySilently();
        }

        // ###########################################################################################
        // Photos/images. The metadata (file name, comment, order) lives in entries.json with the
        // entry; the bytes live in the entry's own "entry-<id>-files" folder, resolved through
        // WorklogManager.GetEntryAttachmentsFolder. Adding copies the chosen file in there under a
        // name that cannot collide with an existing one - see WorklogAttachmentStorage.
        // ###########################################################################################
        private void RefreshPhotoRows()
        {
            // Each thumbnail is a decoded Bitmap holding an unmanaged surface. This method runs on
            // every add/edit/delete/reorder and re-decodes the lot, so without disposing the old
            // ones each refresh orphaned a full set until a finalizer eventually ran.
            //
            // Collected before Clear() but disposed after it: an Image is still bound to the bitmap
            // until the row leaves the collection, and disposing one out from under a live binding
            // risks a render against a freed surface.
            var discardedThumbnails = this.thisPhotoRows.Select(row => row.Thumbnail).Where(bitmap => bitmap != null).ToList();

            this.thisPhotoRows.Clear();

            foreach (var bitmap in discardedThumbnails)
            {
                bitmap!.Dispose();
            }
            foreach (var photo in this.thisEntry.Photos.OrderBy(p => p.DisplayOrder))
            {
                this.thisPhotoRows.Add(new WorklogAttachmentRow
                {
                    Id = photo.Id,
                    FileName = photo.FileName,
                    DisplayFileName = WorklogAttachmentStorage.GetDisplayFileName(photo.FileName, WorklogAttachmentStorage.PhotoFilePrefix, photo.Id),
                    Comment = photo.Comment,
                    Thumbnail = this.TryLoadPhotoThumbnail(photo.FileName)
                });
            }
            this.EditorNoPhotosText.IsVisible = this.thisPhotoRows.Count == 0;
        }

        // ###########################################################################################
        // Resolves the on-disk path of one of this entry's attachments, or null when the workbook
        // folder cannot be resolved or the file is not there.
        // ###########################################################################################
        private string? ResolveAttachmentPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string? attachmentsFolder = WorklogManager.GetEntryAttachmentsFolder(this.thisWorkbookId, this.thisEntry.Id);
            if (attachmentsFolder == null)
            {
                return null;
            }

            // Fully qualified: this file also uses Avalonia.Controls.Shapes, which has its own Path.
            string path = System.IO.Path.Combine(attachmentsFolder, fileName);
            return File.Exists(path) ? path : null;
        }

        // ###########################################################################################
        // Decodes a row thumbnail, scaled down on load rather than at full resolution - a phone
        // photo is several thousand pixels wide and the row shows it at 64, so decoding the full
        // image would spend memory the list never uses. Failure is not fatal: the row renders with
        // a "missing" marker instead, since a photo file can be deleted or corrupted outside the app.
        // ###########################################################################################
        private Bitmap? TryLoadPhotoThumbnail(string fileName)
        {
            string? path = this.ResolveAttachmentPath(fileName);
            if (path == null)
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(path);
                return Bitmap.DecodeToWidth(stream, 256);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load worklog photo thumbnail [{fileName}]: {ex.Message}");
                return null;
            }
        }

        // ###########################################################################################
        // Adds one photo: collect the file and comment, copy the bytes into the entry's attachments
        // folder, then record the metadata. The record is only added once the copy has succeeded -
        // a row pointing at a file that never landed would show as permanently broken.
        // ###########################################################################################
        private async void OnAddPhotoClick(object? sender, RoutedEventArgs e)
        {
            // async void cannot be awaited, so anything thrown after the first await reaches the
            // global handler instead of this window. GetEntryAttachmentsFolder calls
            // Directory.CreateDirectory, which throws on a read-only or disconnected folder - a
            // reportable condition, not a crash.
            try
            {
                await this.AddPhotoAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to add worklog photo: {ex.Message}");
                this.ShowSaveFailed("The photo could not be added - see the log for details.");
            }
        }

        private async Task AddPhotoAsync()
        {
            var dialog = new WorklogAddPhotoWindow();
            var result = await dialog.ShowDialog<WorklogAddPhotoWindow.PhotoResult?>(this);
            if (result == null || string.IsNullOrWhiteSpace(result.SourcePath))
            {
                return;
            }

            string? attachmentsFolder = WorklogManager.GetEntryAttachmentsFolder(this.thisWorkbookId, this.thisEntry.Id);
            if (attachmentsFolder == null)
            {
                this.ShowSaveFailed("Could not resolve where to store the photo.");
                return;
            }

            // The id is settled before the name, because the stored name is built from it. It also
            // skips any id whose file is already in the folder - see AllocateAttachmentId for why
            // plain Max(Id) + 1 can silently overwrite an orphaned attachment.
            int nextId = WorklogAttachmentStorage.AllocateAttachmentId(
                this.thisEntry.Photos,
                WorklogAttachmentStorage.PhotoFilePrefix,
                WorklogAttachmentStorage.ListAttachmentFileNames(attachmentsFolder));

            // Ordering is 0-based to match ReorderAttachment, which renumbers densely from 0. When
            // this started at 1, the first photo added after any drag-reorder took the same
            // DisplayOrder as an existing row, and two rows sharing an order sort arbitrarily.
            int nextOrder = this.thisEntry.Photos.Count == 0 ? 0 : this.thisEntry.Photos.Max(p => p.DisplayOrder) + 1;

            string storedFileName = WorklogAttachmentStorage.BuildStoredFileName(
                result.SourcePath, WorklogAttachmentStorage.PhotoFilePrefix, nextId);

            if (!WorklogAttachmentStorage.CopyAttachmentIntoFolder(result.SourcePath, attachmentsFolder, storedFileName))
            {
                this.ShowSaveFailed("The photo could not be copied into the worklog.");
                return;
            }

            this.thisEntry.Photos.Add(new WorklogAttachmentRecord
            {
                Id = nextId,
                FileName = storedFileName,
                Comment = result.Comment,
                DisplayOrder = nextOrder
            });

            this.RefreshPhotoRows();

            // A failed save means entries.json will never mention this photo, so the bytes just
            // copied in would sit in the attachments folder forever with nothing referencing them.
            // Undoing the copy keeps the folder consistent with what was actually recorded.
            if (!this.PersistEntrySilently())
            {
                this.thisEntry.Photos.RemoveAll(p => p.Id == nextId);
                WorklogAttachmentStorage.DeleteAttachmentFile(attachmentsFolder, storedFileName);
                this.RefreshPhotoRows();
            }
        }

        // ###########################################################################################
        // Clicking a photo row's thumbnail opens the full-size viewer. Separate from the row's Edit
        // button on purpose: viewing is the common action and editing is the deliberate one, so the
        // large target views and the small explicit one edits.
        // ###########################################################################################
        private void OnPhotoThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control { Tag: int id })
            {
                return;
            }

            // Left button only, so a right-click does not open the viewer.
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var photo = this.thisEntry.Photos.FirstOrDefault(p => p.Id == id);
            if (photo == null)
            {
                return;
            }

            // Stops the click also reaching the row, which would open the editor behind the viewer.
            e.Handled = true;

            // The display name, not the stored one - the user never chose the "photo3_" prefix.
            var viewer = new WorklogPhotoViewerWindow();
            viewer.Initialize(
                WorklogAttachmentStorage.GetDisplayFileName(photo.FileName, WorklogAttachmentStorage.PhotoFilePrefix, photo.Id),
                photo.Comment,
                this.ResolveAttachmentPath(photo.FileName));
            viewer.ShowDialog(this);
        }

        // ###########################################################################################
        // Editing a photo reopens the same modal pre-filled, matching the comment and work-done
        // rows. A replacement image is copied in alongside the old one and the record repointed;
        // the previous file is deliberately left on disk rather than deleted, because an entry that
        // has not been saved yet can still be cancelled, and deleting here would take the original
        // with it. See the note on Delete below - the same reasoning applies.
        // ###########################################################################################
        private async void OnEditPhotoClick(object? sender, RoutedEventArgs e)
        {
            // See OnAddPhotoClick for why the body is wrapped.
            try
            {
                await this.EditPhotoAsync(sender);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to edit worklog photo: {ex.Message}");
                this.ShowSaveFailed("The photo could not be updated - see the log for details.");
            }
        }

        private async Task EditPhotoAsync(object? sender)
        {
            if (sender is not Button { Tag: int id })
            {
                return;
            }

            var photo = this.thisEntry.Photos.FirstOrDefault(p => p.Id == id);
            if (photo == null)
            {
                return;
            }

            var dialog = new WorklogAddPhotoWindow();
            dialog.InitializeForEdit(
                WorklogAttachmentStorage.GetDisplayFileName(photo.FileName, WorklogAttachmentStorage.PhotoFilePrefix, photo.Id),
                photo.Comment,
                this.ResolveAttachmentPath(photo.FileName));

            var result = await dialog.ShowDialog<WorklogAddPhotoWindow.PhotoResult?>(this);
            if (result == null)
            {
                return;
            }

            string previousFileName = photo.FileName;
            string previousComment = photo.Comment;

            string? attachmentsFolder = null;
            string newStoredFileName = string.Empty;

            if (!string.IsNullOrWhiteSpace(result.SourcePath))
            {
                attachmentsFolder = WorklogManager.GetEntryAttachmentsFolder(this.thisWorkbookId, this.thisEntry.Id);
                if (attachmentsFolder == null)
                {
                    this.ShowSaveFailed("Could not resolve where to store the photo.");
                    return;
                }

                newStoredFileName = WorklogAttachmentStorage.BuildStoredFileName(
                    result.SourcePath, WorklogAttachmentStorage.PhotoFilePrefix, photo.Id);

                photo.FileName = newStoredFileName;
            }

            photo.Comment = result.Comment;

            // The record is saved BEFORE the file is swapped, because the swap deletes the image it
            // replaces. Doing it the other way round meant a failed save left entries.json naming a
            // file that had already been deleted - and Cancel then discarded the working copy, so
            // the row was permanently broken with no way back to the original.
            if (!this.PersistEntrySilently())
            {
                photo.FileName = previousFileName;
                photo.Comment = previousComment;
                this.RefreshPhotoRows();
                return;
            }

            if (attachmentsFolder != null)
            {
                // Copies the new file in and removes the one it replaces, leaving exactly one file
                // behind whether or not the stored name changed - see TryReplaceAttachmentFile.
                this.RollBackAttachmentFileNameIfSwapFailed(
                    photo,
                    previousFileName,
                    newStoredFileName,
                    result.SourcePath!,
                    attachmentsFolder,
                    "The photo could not be copied into the worklog.");
            }

            this.RefreshPhotoRows();
        }

        // ###########################################################################################
        // Removes the photo, metadata and bytes both. Deleting the file is safe because the stored
        // name carries the photo's own id (see BuildStoredFileName), so it can only ever belong to
        // the record being removed - the app copied it in and nothing else points at it.
        //
        // The file goes only after the metadata change has been persisted: if the save fails the
        // row is still listed, and deleting first would leave it pointing at nothing.
        // ###########################################################################################
        private void OnDeletePhotoClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: int id })
            {
                return;
            }

            var photo = this.thisEntry.Photos.FirstOrDefault(p => p.Id == id);
            if (photo == null)
            {
                return;
            }

            string fileName = photo.FileName;

            this.thisEntry.Photos.RemoveAll(p => p.Id == id);
            this.RefreshPhotoRows();

            if (this.PersistEntrySilently())
            {
                WorklogAttachmentStorage.DeleteAttachmentFile(
                    WorklogManager.GetEntryAttachmentsFolder(this.thisWorkbookId, this.thisEntry.Id),
                    fileName);
            }
        }

        // ###########################################################################################
        // Drag-to-reorder for the Photos and Files rows, replacing up/down buttons in both.
        //
        // Only the row's empty space starts a drag: the thumbnail, the file link and the icon
        // buttons handle their own pointer events and mark them handled, so pressing those never
        // begins a drag. That is also why the row shows the north/south cursor only over that empty
        // space - the cursor is set on the panel that carries the drag, not on the whole row.
        //
        // A press alone does not start the drag; it only arms it. The drag begins once the pointer
        // has actually moved a few pixels, so a plain click on a row cannot reorder anything by
        // accident.
        //
        // One implementation serves both lists, parameterised by the DragContext below. The logic
        // here is subtle in three places (the frozen boundary snapshot, the re-entrancy guard, the
        // placeholder-index-as-target rule), and a second copy would be a second place for those to
        // be got wrong.
        // ###########################################################################################
        // Records is a lookup, not a captured list: see CreatePhotoDragContext for why holding the
        // list itself across a gesture can go stale.
        private sealed record DragContext(
            ItemsControl List,
            ObservableCollection<WorklogAttachmentRow> Rows,
            Func<List<WorklogAttachmentRecord>> Records,
            Action Refresh);

        // Built once per gesture rather than on every press.
        //
        // These were properties allocating a fresh record plus a bound delegate on every pointer
        // press, including presses that immediately bailed. More importantly, Records held a direct
        // reference to thisEntry's list captured at press time, and the context survives the whole
        // gesture - so anything replacing the thisEntry record mid-drag (rather than mutating its
        // lists in place) left the release handler reordering a detached list and persisting
        // nothing the user could see. Reading the list through thisEntry at use time cannot go
        // stale that way.
        private DragContext CreatePhotoDragContext() => new(
            this.EditorPhotosList, this.thisPhotoRows, () => this.thisEntry.Photos, this.RefreshPhotoRows);

        private DragContext CreateFileDragContext() => new(
            this.EditorFilesList, this.thisFileRows, () => this.thisEntry.Files, this.RefreshFileRows);

        private DragContext? thisActiveDragContext;

        private int thisDraggedPhotoId = -1;

        private Point thisPhotoDragStartPoint;

        private bool thisIsDraggingPhoto;

        // Far enough that a click with a shaky hand is not a drag, small enough to feel immediate.
        private const double PhotoDragThreshold = 4.0;

        private void OnPhotoRowDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            this.BeginRowDrag(sender, e, this.CreatePhotoDragContext());
        }

        private void OnFileRowDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            this.BeginRowDrag(sender, e, this.CreateFileDragContext());
        }

        private void BeginRowDrag(object? sender, PointerPressedEventArgs e, DragContext context)
        {
            if (sender is not Control { Tag: int id })
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            this.thisActiveDragContext = context;
            this.thisDraggedPhotoId = id;
            this.thisPhotoDragStartPoint = e.GetPosition(context.List);
            this.thisIsDraggingPhoto = false;
        }

        private void OnPhotoRowDragHandlePointerMoved(object? sender, PointerEventArgs e)
        {
            if (this.thisDraggedPhotoId < 0 || this.thisActiveDragContext == null)
            {
                return;
            }

            var context = this.thisActiveDragContext;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                // The button was released somewhere that did not reach the release handler (outside
                // the window, say); without this the next move would resume a drag the user ended.
                this.ResetPhotoDragState();
                return;
            }

            var current = e.GetPosition(context.List);

            if (!this.thisIsDraggingPhoto &&
                Math.Abs(current.Y - this.thisPhotoDragStartPoint.Y) < PhotoDragThreshold &&
                Math.Abs(current.X - this.thisPhotoDragStartPoint.X) < PhotoDragThreshold)
            {
                return;
            }

            if (!this.thisIsDraggingPhoto)
            {
                // Only a placeholder that was actually established starts the drag. If the row has
                // gone (a refresh landed between press and first move), the boundaries are empty
                // and every later step is a no-op that still LOOKS like a drag: no visual feedback
                // while dragging, and on release a full Refresh that re-decodes every thumbnail
                // from disk for a gesture that changed nothing. Better to end it here.
                if (!this.BeginPhotoDragPlaceholder())
                {
                    this.ResetPhotoDragState();
                    return;
                }
            }

            this.thisIsDraggingPhoto = true;

            // Move the dragged row to wherever the pointer now is, so the gap follows the pointer
            // and the surrounding rows shift into the order the drop will produce. The row is drawn
            // as an outlined slot while it is the placeholder, so what the user sees is the space
            // it will occupy rather than the row itself trailing the cursor.
            //
            // Guarded against re-entry: Move() reorders the collection, which makes Avalonia recycle
            // the row containers, which changes the element under the cursor and raises further
            // pointer events synchronously. Those re-enter this handler and move again, and the list
            // flickers between orders for as long as the pointer is held there. The flag makes the
            // nested calls no-ops so one physical mouse move produces exactly one reorder.
            if (this.thisIsApplyingPhotoPlaceholderMove)
            {
                return;
            }

            this.thisIsApplyingPhotoPlaceholderMove = true;
            try
            {
                this.MovePhotoPlaceholderTo(context, this.ResolvePhotoDropIndex(context, current));
            }
            finally
            {
                this.thisIsApplyingPhotoPlaceholderMove = false;
            }
        }

        private bool thisIsApplyingPhotoPlaceholderMove;

        private void OnPhotoRowDragHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (this.thisDraggedPhotoId < 0 || !this.thisIsDraggingPhoto || this.thisActiveDragContext == null)
            {
                this.ResetPhotoDragState();
                return;
            }

            var context = this.thisActiveDragContext;
            int draggedId = this.thisDraggedPhotoId;

            // The placeholder is already sitting at the drop position, so its index in the row list
            // IS the target - no need to re-measure against the pointer, which would disagree with
            // what the user was just shown if the pointer sat between two rows.
            int targetIndex = this.IndexOfPhotoRow(context, draggedId);

            this.ResetPhotoDragState();

            if (targetIndex < 0)
            {
                context.Refresh();
                return;
            }

            WorklogAttachmentStorage.ReorderAttachment(context.Records(), draggedId, targetIndex);
            context.Refresh();
            this.PersistEntrySilently();
        }

        // ###########################################################################################
        // Turns the dragged row into the placeholder, sized to the height it currently occupies so
        // the gap does not jump when its content is swapped for the empty outline.
        // ###########################################################################################
        // Returns false when no placeholder could be established, so the caller can abandon the
        // gesture instead of running a drag with no boundaries and no visible row.
        private bool BeginPhotoDragPlaceholder()
        {
            var context = this.thisActiveDragContext;
            if (context == null)
            {
                return false;
            }

            // Cleared up front so an early return below cannot leave the PREVIOUS drag's boundaries
            // in place - ResolvePhotoDropIndex would then measure this drag against the other list's
            // geometry, and with photo rows several times taller than file rows the drop lands at a
            // wildly wrong index and is persisted immediately.
            this.thisPhotoRowDragBoundaries.Clear();

            int index = this.IndexOfPhotoRow(context, this.thisDraggedPhotoId);
            if (index < 0)
            {
                return false;
            }

            var row = context.Rows[index];

            var container = context.List.ContainerFromIndex(index);
            if (container != null && container.Bounds.Height > 0)
            {
                row.PlaceholderHeight = container.Bounds.Height;
            }

            this.CapturePhotoRowBoundaries(context);

            row.IsDropPlaceholder = true;
            return true;
        }

        // ###########################################################################################
        // The Y positions of the row boundaries as they are at the moment the drag starts, used to
        // decide which slot the pointer is over for the rest of the gesture.
        //
        // A snapshot rather than live measurement, because measuring live feeds the swap back into
        // its own input: moving the placeholder re-lays out the list, which moves the very rows the
        // next measurement reads, which can select a different slot, which moves them back - the
        // rows oscillate every frame. That feedback is unavoidable once rows differ in height (they
        // do now that each image is sized by its own aspect ratio), because a swap shifts the
        // layout by the difference between two row heights rather than leaving it unchanged.
        //
        // Against a frozen frame the pointer position alone decides the slot, so the same pointer
        // position always gives the same answer and there is nothing to oscillate.
        // ###########################################################################################
        private readonly List<double> thisPhotoRowDragBoundaries = new();

        private void CapturePhotoRowBoundaries(DragContext context)
        {
            this.thisPhotoRowDragBoundaries.Clear();

            // One entry per row, always - index i in this list means row i. Skipping a row whose
            // container is not realized would shorten the list and shift every later boundary's
            // meaning by one, so an unmeasurable row gets an interpolated midpoint instead and the
            // two lists stay aligned. ResolvePhotoDropIndex relies on that 1:1 correspondence to
            // return an index into the row collection.
            double runningY = 0;

            for (int i = 0; i < context.Rows.Count; i++)
            {
                var container = context.List.ContainerFromIndex(i);
                Point? topLeft = container?.TranslatePoint(new Point(0, 0), context.List);

                double height = container != null && container.Bounds.Height > 0
                    ? container.Bounds.Height
                    : context.Rows[i].PlaceholderHeight;

                double top = topLeft?.Y ?? runningY;

                // The midpoint of each row as laid out before anything moved. The pointer being
                // past a midpoint means the drop belongs after that row.
                this.thisPhotoRowDragBoundaries.Add(top + (height / 2.0));

                runningY = top + height;
            }
        }

        // ###########################################################################################
        // Moves the placeholder row to the given index, leaving the collection untouched when it is
        // already there - a Move on every pointer frame would rebuild containers continuously and
        // make the list flicker.
        // ###########################################################################################
        private void MovePhotoPlaceholderTo(DragContext context, int targetIndex)
        {
            if (targetIndex < 0)
            {
                return;
            }

            int currentIndex = this.IndexOfPhotoRow(context, this.thisDraggedPhotoId);
            if (currentIndex < 0)
            {
                return;
            }

            targetIndex = Math.Clamp(targetIndex, 0, context.Rows.Count - 1);
            if (targetIndex == currentIndex)
            {
                return;
            }

            context.Rows.Move(currentIndex, targetIndex);
        }

        private int IndexOfPhotoRow(DragContext context, int id)
        {
            for (int i = 0; i < context.Rows.Count; i++)
            {
                if (context.Rows[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        // ###########################################################################################
        // Ends the drag and returns every row to its normal appearance. Clearing the flag on all
        // rows rather than just the dragged one means an interrupted drag (the window closing, a
        // refresh landing mid-drag) cannot stitch a row permanently as a placeholder.
        // ###########################################################################################
        private void ResetPhotoDragState()
        {
            // Both lists are cleared, not just the active one: the flag must never survive a drag,
            // and an interrupted gesture can leave the context null while a row still carries it.
            foreach (var row in this.thisPhotoRows)
            {
                row.IsDropPlaceholder = false;
            }

            foreach (var row in this.thisFileRows)
            {
                row.IsDropPlaceholder = false;
            }

            // Cleared so the next drag cannot resolve against the previous drag's layout.
            this.thisPhotoRowDragBoundaries.Clear();

            this.thisActiveDragContext = null;
            this.thisDraggedPhotoId = -1;
            this.thisIsDraggingPhoto = false;
        }

        // ###########################################################################################
        // Which slot the pointer is over, measured against the boundaries captured when the drag
        // started (see CapturePhotoRowBoundaries for why a live measurement oscillates).
        //
        // Above the first boundary gives 0 and past the last gives the final index, so a drag flung
        // past either end lands at that end instead of being discarded.
        // ###########################################################################################
        private int ResolvePhotoDropIndex(DragContext context, Point pointerInList)
        {
            if (context.Rows.Count == 0 || this.thisPhotoRowDragBoundaries.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < this.thisPhotoRowDragBoundaries.Count; i++)
            {
                if (pointerInList.Y < this.thisPhotoRowDragBoundaries[i])
                {
                    // Also covers a pointer dragged above the list entirely.
                    return i;
                }
            }

            // Past the last midpoint - the drop belongs at the end.
            return this.thisPhotoRowDragBoundaries.Count - 1;
        }

        // ###########################################################################################
        // Files - the same storage shape as Photos (bytes in the entry's "entry-<id>-files" folder,
        // metadata in entries.json), differing in what is accepted and how a row is presented:
        //
        //  - accepted types come from ExternalTargetLauncher's openable set rather than the narrower
        //    drawable-image one, so a PDF or CSV is fine and an executable/script/shortcut is not;
        //  - nothing is rendered for the file, just its name as a link, since there is no useful
        //    preview for a document;
        //  - clicking the link opens it through ExternalTargetLauncher, scoped to the attachments
        //    folder, so the same containment and extension rules apply as anywhere else in the app.
        // ###########################################################################################
        private void RefreshFileRows()
        {
            this.thisFileRows.Clear();
            foreach (var file in this.thisEntry.Files.OrderBy(f => f.DisplayOrder))
            {
                this.thisFileRows.Add(new WorklogAttachmentRow
                {
                    Id = file.Id,
                    FileName = file.FileName,
                    DisplayFileName = WorklogAttachmentStorage.GetDisplayFileName(file.FileName, WorklogAttachmentStorage.FileFilePrefix, file.Id),
                    Comment = file.Comment
                });
            }
            this.EditorNoFilesText.IsVisible = this.thisFileRows.Count == 0;
        }

        private async void OnAddFileClick(object? sender, RoutedEventArgs e)
        {
            // See OnAddPhotoClick for why the body is wrapped.
            try
            {
                await this.AddFileAsync();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to add worklog file: {ex.Message}");
                this.ShowSaveFailed("The file could not be added - see the log for details.");
            }
        }

        private async Task AddFileAsync()
        {
            var dialog = new WorklogAddPhotoWindow();
            dialog.InitializeForFileKind();

            var result = await dialog.ShowDialog<WorklogAddPhotoWindow.PhotoResult?>(this);
            if (result == null || string.IsNullOrWhiteSpace(result.SourcePath))
            {
                return;
            }

            string? attachmentsFolder = WorklogManager.GetEntryAttachmentsFolder(this.thisWorkbookId, this.thisEntry.Id);
            if (attachmentsFolder == null)
            {
                this.ShowSaveFailed("Could not resolve where to store the file.");
                return;
            }

            // See AddPhotoAsync - the same id/orphan reasoning applies to files.
            int nextId = WorklogAttachmentStorage.AllocateAttachmentId(
                this.thisEntry.Files,
                WorklogAttachmentStorage.FileFilePrefix,
                WorklogAttachmentStorage.ListAttachmentFileNames(attachmentsFolder));
            int nextOrder = this.thisEntry.Files.Count == 0 ? 0 : this.thisEntry.Files.Max(f => f.DisplayOrder) + 1;

            string storedFileName = WorklogAttachmentStorage.BuildStoredFileName(
                result.SourcePath, WorklogAttachmentStorage.FileFilePrefix, nextId);

            if (!WorklogAttachmentStorage.CopyAttachmentIntoFolder(result.SourcePath, attachmentsFolder, storedFileName))
            {
                this.ShowSaveFailed("The file could not be copied into the worklog.");
                return;
            }

            this.thisEntry.Files.Add(new WorklogAttachmentRecord
            {
                Id = nextId,
                FileName = storedFileName,
                Comment = result.Comment,
                DisplayOrder = nextOrder
            });

            this.RefreshFileRows();

            // Undo the copy when the save fails, so the folder never holds bytes that entries.json
            // does not mention - same reasoning as the photo add path.
            if (!this.PersistEntrySilently())
            {
                this.thisEntry.Files.RemoveAll(f => f.Id == nextId);
                WorklogAttachmentStorage.DeleteAttachmentFile(attachmentsFolder, storedFileName);
                this.RefreshFileRows();
            }
        }

        private async void OnEditFileClick(object? sender, RoutedEventArgs e)
        {
            // See OnAddPhotoClick for why the body is wrapped.
            try
            {
                await this.EditFileAsync(sender);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to edit worklog file: {ex.Message}");
                this.ShowSaveFailed("The file could not be updated - see the log for details.");
            }
        }

        private async Task EditFileAsync(object? sender)
        {
            if (sender is not Button { Tag: int id })
            {
                return;
            }

            var file = this.thisEntry.Files.FirstOrDefault(f => f.Id == id);
            if (file == null)
            {
                return;
            }

            var dialog = new WorklogAddPhotoWindow();
            dialog.InitializeForEdit(
                WorklogAttachmentStorage.GetDisplayFileName(file.FileName, WorklogAttachmentStorage.FileFilePrefix, file.Id),
                file.Comment,
                existingImagePath: null,
                WorklogAttachmentStorage.AttachmentKind.File);

            var result = await dialog.ShowDialog<WorklogAddPhotoWindow.PhotoResult?>(this);
            if (result == null)
            {
                return;
            }

            string previousFileName = file.FileName;
            string previousComment = file.Comment;

            string? attachmentsFolder = null;
            string newStoredFileName = string.Empty;

            if (!string.IsNullOrWhiteSpace(result.SourcePath))
            {
                attachmentsFolder = WorklogManager.GetEntryAttachmentsFolder(this.thisWorkbookId, this.thisEntry.Id);
                if (attachmentsFolder == null)
                {
                    this.ShowSaveFailed("Could not resolve where to store the file.");
                    return;
                }

                newStoredFileName = WorklogAttachmentStorage.BuildStoredFileName(
                    result.SourcePath, WorklogAttachmentStorage.FileFilePrefix, file.Id);

                file.FileName = newStoredFileName;
            }

            file.Comment = result.Comment;

            // Saved before the swap, because the swap deletes what it replaces - see the photo edit
            // path for why the other order strands a record pointing at a deleted file.
            if (!this.PersistEntrySilently())
            {
                file.FileName = previousFileName;
                file.Comment = previousComment;
                this.RefreshFileRows();
                return;
            }

            if (attachmentsFolder != null)
            {
                // Same swap-and-roll-back as the photo path - see RollBackAttachmentFileNameIfSwapFailed.
                this.RollBackAttachmentFileNameIfSwapFailed(
                    file,
                    previousFileName,
                    newStoredFileName,
                    result.SourcePath!,
                    attachmentsFolder,
                    "The file could not be copied into the worklog.");
            }

            this.RefreshFileRows();
        }

        // ###########################################################################################
        // Swaps an attachment's bytes for a newly picked file and, if that fails, puts the record's
        // stored name back so entries.json never names bytes that were never written.
        //
        // Shared by the photo and file edit paths, which had the same flaw independently: the
        // roll-back save's result was ignored, so if THAT save failed too the record was left
        // naming the new file while only the old bytes existed. The row then resolved to null
        // forever - "That file could no longer be found" - with no way back to the original.
        //
        // When the roll-back cannot be persisted the record is still restored in memory, so what
        // the user sees matches the bytes on disk, and the message says the entry needs saving.
        // That is the best available outcome: the alternative is leaving a name on screen that
        // nothing on disk backs.
        // ###########################################################################################
        private void RollBackAttachmentFileNameIfSwapFailed(
            WorklogAttachmentRecord record,
            string previousFileName,
            string newStoredFileName,
            string sourcePath,
            string attachmentsFolder,
            string failureMessage)
        {
            if (WorklogAttachmentStorage.TryReplaceAttachmentFile(
                    sourcePath,
                    attachmentsFolder,
                    previousFileName,
                    newStoredFileName,
                    out _))
            {
                return;
            }

            // The record already names the new file, so put it back before re-saving.
            record.FileName = previousFileName;

            if (this.PersistEntrySilently())
            {
                this.ShowSaveFailed(failureMessage);
                return;
            }

            this.ShowSaveFailed(failureMessage + " The entry could not be saved either - use Save to retry.");
        }

        // ###########################################################################################
        // Opens the clicked file through ExternalTargetLauncher, scoped to the entry's attachments
        // folder rather than the data root: worklog workbooks live in AppData, outside the data root
        // the launcher defaults to, so without the override every attachment would be refused as
        // "outside allowed scope". The containment check still applies - just against this folder.
        // ###########################################################################################
        private void OnFileRowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control { Tag: int id })
            {
                return;
            }

            // Marked handled IMMEDIATELY, before any other guard can return.
            //
            // This link sits inside the row's drag Panel, whose PointerPressed arms a reorder. The
            // Panel is skipped only when this handler marks the event handled - so every early
            // return below that happens BEFORE this line lets the press fall through and arm a drag
            // the user never started. The next few pixels of movement then cross the drag threshold,
            // turn the row into a placeholder, and on release commit a reorder and save it.
            //
            // That was the intermittent "clicking a file link does nothing" fault: a right-click, or
            // a click on a row whose record had just been replaced, silently armed a phantom drag
            // instead of opening anything. It is why the guards below now run after this line, and
            // why this must stay first.
            e.Handled = true;

            // Left button only. This launches an external application through the OS shell, so a
            // right-click reaching for a context menu must not open the document - unlike the photo
            // thumbnail, whose press opens a modal the user can simply close.
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var file = this.thisEntry.Files.FirstOrDefault(f => f.Id == id);
            if (file == null)
            {
                return;
            }

            string? path = this.ResolveAttachmentPath(file.FileName);
            if (path == null)
            {
                this.ShowSaveFailed("That file could no longer be found.");
                return;
            }

            string? attachmentsFolder = WorklogManager.GetEntryAttachmentsFolder(this.thisWorkbookId, this.thisEntry.Id);
            bool opened = attachmentsFolder != null && ExternalTargetLauncher.TryOpen(path, attachmentsFolder);
            if (!opened)
            {
                this.ShowSaveFailed("That file could not be opened.");
            }
        }

        // ###########################################################################################
        // Removes the file, metadata and bytes both - the stored name carries the record's own id,
        // so it can only belong to the row being removed. The bytes go only after the metadata
        // change reached disk, matching OnDeletePhotoClick.
        // ###########################################################################################
        private void OnDeleteFileClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: int id })
            {
                return;
            }

            var file = this.thisEntry.Files.FirstOrDefault(f => f.Id == id);
            if (file == null)
            {
                return;
            }

            string fileName = file.FileName;

            this.thisEntry.Files.RemoveAll(f => f.Id == id);
            this.RefreshFileRows();

            if (this.PersistEntrySilently())
            {
                WorklogAttachmentStorage.DeleteAttachmentFile(
                    WorklogManager.GetEntryAttachmentsFolder(this.thisWorkbookId, this.thisEntry.Id),
                    fileName);
            }
        }
        // ###########################################################################################
        // Cancel/Escape discards pending edits to the direct fields (Title/Description/category/
        // state), but still reports WasSaved when a Links/Comments/Work-done/Photos/Files change
        // already made it to disk via PersistEntrySilently, so the caller knows to refresh.
        //
        // "Open" is the limit of what Cancel can undo: an instant-save commits the direct fields
        // along with the sub-list change (see PersistEntrySilently), so once one has run, the
        // direct-field values at that moment are already on disk and Cancel cannot take them back.
        // ###########################################################################################
        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            this.WasSaved = this.thisHasPersistedChange;
            this.Close(this.WasSaved);
        }

        // ###########################################################################################
        // Commits the working copy back via WorklogManager.UpdateEntry, which also recomputes the
        // workbook's Open/Closed status - editing State to Closed here is exactly how the user
        // resolves an entry from the full editor, same rule as the quick "New fault" card.
        // ###########################################################################################
        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            this.SyncDirectFieldsToEntry();

            if (!WorklogManager.UpdateEntry(this.thisWorkbookId, this.thisEntry))
            {
                // Nothing reached disk. Closing here would report success and the user would watch
                // their edits revert on the next refresh, so keep the window open with what they
                // typed still in it and say so. The log carries the underlying reason.
                this.ShowSaveFailed(DefaultSaveFailedMessage);
                return;
            }

            this.WasSaved = true;
            this.Close(this.WasSaved);
        }
    }

    // ###########################################################################################
    // Row types for the editor's ItemsControls. Public and top-level so the compiled DataTemplates
    // in WorklogEntryEditorWindow.axaml can bind to them - same reasoning as WorklogEntryComponentRow
    // in TabSchematics.Worklog.cs.
    // ###########################################################################################
    public sealed class WorklogLinkRow
    {
        public int Id { get; set; }
        public string Headline { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public sealed class WorklogCommentRow
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
    }

    public sealed class WorklogWorkDoneRow
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
        public string SummaryText { get; set; } = string.Empty;
    }

    public sealed class WorklogAttachmentRow : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        // ###########################################################################################
        // The file name without the "{id}_" storage prefix - what the Files list shows as its link
        // text. The prefix keeps names unique on disk and means nothing to the user.
        // ###########################################################################################
        public string DisplayFileName { get; set; } = string.Empty;

        // ###########################################################################################
        // Thumbnail for a photo row, decoded once when the row is built rather than by a binding
        // converter, so a file that has gone missing or will not decode simply leaves this null and
        // the row still lists its name and comment. Always null for file rows, which show no image.
        // ###########################################################################################
        public Avalonia.Media.Imaging.Bitmap? Thumbnail { get; set; }

        public bool HasThumbnail => this.Thumbnail != null;

        // ###########################################################################################
        // Shown in place of the thumbnail when the image is unavailable, so a broken photo row reads
        // as broken instead of as a blank square.
        // ###########################################################################################
        public bool HasNoThumbnail => this.Thumbnail == null;

        // ###########################################################################################
        // Hides the comment line entirely when there is none, keeping rows compact - a photo is
        // allowed to carry no comment.
        // ###########################################################################################
        public bool HasComment => !string.IsNullOrWhiteSpace(this.Comment);

        // ###########################################################################################
        // True while this row is the one being dragged, which draws it as an empty outlined slot
        // showing where a drop would land. Following SchematicThumbnail's IsDropPlaceholder: the
        // template swaps between the placeholder box and the real content on this flag.
        //
        // Unlike the thumbnail list, no separate placeholder object is inserted - the dragged row
        // moves within the collection and renders as the placeholder itself, so the gap is exactly
        // the height of the row being moved and the list shows the order it will end up in.
        // ###########################################################################################
        public bool IsDropPlaceholder
        {
            get => this.thisIsDropPlaceholder;
            set
            {
                if (this.thisIsDropPlaceholder == value)
                {
                    return;
                }

                this.thisIsDropPlaceholder = value;
                this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(this.IsDropPlaceholder)));
                this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(this.IsNotDropPlaceholder)));
            }
        }

        public bool IsNotDropPlaceholder => !this.thisIsDropPlaceholder;

        private bool thisIsDropPlaceholder;

        // ###########################################################################################
        // The row's own height while it is the placeholder, so the gap matches the row being dragged
        // rather than collapsing to the empty box's natural size.
        //
        // MUST raise PropertyChanged: BeginPhotoDragPlaceholder measures the row and assigns this
        // immediately before setting IsDropPlaceholder, and without notification the Height binding
        // kept whatever value it first read. The placeholder then drew at a fixed size regardless of
        // the row - unnoticeable for photo rows, which happen to be about that tall, and obvious in
        // the Files list, where a ~50px row left a gap three times its height.
        //
        // The starting value is only used if a drag somehow begins before the row has been measured.
        // It is deliberately small: too short is a brief visual glitch, too tall is the bug above.
        // ###########################################################################################
        public double PlaceholderHeight
        {
            get => this.thisPlaceholderHeight;
            set
            {
                // The value is ALWAYS stored; only the notification is gated. Returning early
                // without assigning kept the previous row's height in the field, so the property
                // and the row it describes disagreed - and a row that genuinely measured within
                // half a pixel of the seed value was indistinguishable from one never measured at
                // all. Storing first keeps the state honest; the threshold still suppresses the
                // sub-pixel churn that made the list flicker mid-drag.
                bool isMeaningfulChange = Math.Abs(this.thisPlaceholderHeight - value) >= 0.5;

                this.thisPlaceholderHeight = value;

                if (isMeaningfulChange)
                {
                    this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(this.PlaceholderHeight)));
                }
            }
        }

        private double thisPlaceholderHeight = 48.0;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
