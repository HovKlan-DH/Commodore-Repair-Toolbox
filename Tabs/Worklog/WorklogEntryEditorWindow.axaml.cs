using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
        private string thisSelectedState = "Pending";

        private readonly ObservableCollection<WorklogLinkRow> thisLinkRows = new();
        private readonly ObservableCollection<WorklogCommentRow> thisCommentRows = new();
        private readonly ObservableCollection<WorklogWorkDoneRow> thisWorkDoneRows = new();
        private readonly ObservableCollection<WorklogAttachmentRow> thisPhotoRows = new();
        private readonly ObservableCollection<WorklogAttachmentRow> thisFileRows = new();

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

            this.SizeChanged += (_, _) => this.RefreshLocationPreviewOverlay();

            this.AddHandler(KeyDownEvent, this.OnWindowPreviewKeyDown, RoutingStrategies.Tunnel);

            // The photo drag's move/release live on the LIST, not on the row that started it: the
            // dragged row is re-rendered as an empty placeholder the moment the drag begins, which
            // takes its own handlers out of the tree, and the row also moves out from under the
            // pointer as the list reorders. The list stays put for the whole gesture.
            // Tunnel so a release over a row's buttons still ends the drag rather than being eaten.
            this.EditorPhotosList.AddHandler(PointerMovedEvent, this.OnPhotoRowDragHandlePointerMoved, RoutingStrategies.Tunnel);
            this.EditorPhotosList.AddHandler(PointerReleasedEvent, this.OnPhotoRowDragHandlePointerReleased, RoutingStrategies.Tunnel);

            // A release outside the list (dragged past the window edge, say) never reaches the
            // handler above, which would strand the placeholder as a permanent empty slot. The
            // window-level handler commits the drop at wherever the placeholder currently sits.
            this.AddHandler(PointerReleasedEvent, this.OnPhotoRowDragHandlePointerReleased, RoutingStrategies.Tunnel);

            // The thumbnails this window decoded hold unmanaged surfaces; without this the last set
            // survives the window itself. thisSchematicBitmap belongs to the caller and is not
            // touched here.
            this.Closed += (_, _) =>
            {
                foreach (var row in this.thisPhotoRows)
                {
                    row.Thumbnail?.Dispose();
                }
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
            this.thisSelectedState = string.IsNullOrWhiteSpace(this.thisEntry.State) ? "Pending" : this.thisEntry.State;
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
        // window's ThemeVariant-keyed resources (Worklog_Category_*, Worklog_State_Fixed, etc.) live
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
            this.ApplyCategoryChipVisualState(this.EditorCategoryNoteChip, this.EditorCategoryNoteDot, this.EditorCategoryNoteText, "Note");
            this.ApplyCategoryChipVisualState(this.EditorCategoryCosmeticChip, this.EditorCategoryCosmeticDot, this.EditorCategoryCosmeticText, "Cosmetic");
            this.ApplyCategoryChipVisualState(this.EditorCategoryIssueChip, this.EditorCategoryIssueDot, this.EditorCategoryIssueText, "Issue");

            this.EditorIdBadge.Background = new SolidColorBrush(this.ResolveCategoryColor(this.thisSelectedCategory));
        }

        private void ApplyCategoryChipVisualState(Border chip, Ellipse dot, TextBlock label, string category)
        {
            var categoryBrush = this.ResolveThemeBrush($"Worklog_Category_{category}", new SolidColorBrush(Colors.IndianRed));

            if (string.Equals(this.thisSelectedCategory, category, StringComparison.Ordinal))
            {
                chip.Background = categoryBrush;
                chip.BorderBrush = categoryBrush;
                chip.BorderThickness = new Thickness(2);
                chip.Opacity = 0.9;
                dot.Fill = Brushes.White;
                label.Foreground = Brushes.White;
                label.FontWeight = FontWeight.SemiBold;
            }
            else
            {
                chip.Background = this.ResolveThemeBrush("Form_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
                chip.BorderBrush = this.ResolveThemeBrush("Form_Border", new SolidColorBrush(Color.Parse("#CCCCCC")));
                chip.BorderThickness = new Thickness(1);
                chip.Opacity = 1.0;
                dot.Fill = categoryBrush;
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
            this.ApplyStatePillVisualState(this.EditorStatePendingPill, this.EditorStatePendingIcon, this.EditorStatePendingText, "Pending", "Worklog_Category_Issue");
            this.ApplyStatePillVisualState(this.EditorStateRuledOutPill, this.EditorStateRuledOutIcon, this.EditorStateRuledOutText, "RuledOut", "Worklog_Category_Note");
            this.ApplyStatePillVisualState(this.EditorStateFixedPill, this.EditorStateFixedIcon, this.EditorStateFixedText, "Fixed", "Worklog_State_Fixed");
        }

        private void ApplyStatePillVisualState(Border pill, TextBlock icon, TextBlock label, string state, string colorResourceKey)
        {
            var stateBrush = this.ResolveThemeBrush(colorResourceKey, new SolidColorBrush(Colors.IndianRed));

            if (string.Equals(this.thisSelectedState, state, StringComparison.Ordinal))
            {
                pill.Background = this.ResolveThemeBrush("Schematics_Panels_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
                pill.BorderBrush = stateBrush;
                pill.BorderThickness = new Thickness(2);
                icon.Foreground = stateBrush;
                label.Foreground = stateBrush;
                label.FontWeight = FontWeight.SemiBold;
            }
            else
            {
                pill.Background = this.ResolveThemeBrush("Form_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
                pill.BorderBrush = this.ResolveThemeBrush("Form_Border", new SolidColorBrush(Color.Parse("#CCCCCC")));
                pill.BorderThickness = new Thickness(1);
                icon.Foreground = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);
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

            // The id is settled before the name, because the stored name is built from it.
            int nextId = this.thisEntry.Photos.Count == 0 ? 1 : this.thisEntry.Photos.Max(p => p.Id) + 1;

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

            var photo = this.thisEntry.Photos.FirstOrDefault(p => p.Id == id);
            if (photo == null)
            {
                return;
            }

            // Stops the click also reaching the row, which would open the editor behind the viewer.
            e.Handled = true;

            var viewer = new WorklogPhotoViewerWindow();
            viewer.Initialize(photo.FileName, photo.Comment, this.ResolveAttachmentPath(photo.FileName));
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
            dialog.InitializeForEdit(photo.FileName, photo.Comment, this.ResolveAttachmentPath(photo.FileName));

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
                if (!WorklogAttachmentStorage.TryReplaceAttachmentFile(
                        result.SourcePath!,
                        attachmentsFolder,
                        previousFileName,
                        newStoredFileName,
                        out _))
                {
                    // The record already names the new file, so put it back and re-save rather than
                    // leaving entries.json pointing at bytes that were never written.
                    photo.FileName = previousFileName;
                    this.ShowSaveFailed("The photo could not be copied into the worklog.");
                    this.PersistEntrySilently();
                }
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
        // Drag-to-reorder for photo rows, replacing the old up/down buttons.
        //
        // Only the row's empty space starts a drag: the thumbnail and the two icon buttons handle
        // their own pointer events and mark them handled, so pressing those never begins a drag.
        // That is also why the row shows the north/south cursor only over that empty space - the
        // cursor is set on the panel that carries the drag, not on the whole row.
        //
        // A press alone does not start the drag; it only arms it. The drag begins once the pointer
        // has actually moved a few pixels, so a plain click on a row cannot reorder anything by
        // accident.
        // ###########################################################################################
        private int thisDraggedPhotoId = -1;

        private Point thisPhotoDragStartPoint;

        private bool thisIsDraggingPhoto;

        // Far enough that a click with a shaky hand is not a drag, small enough to feel immediate.
        private const double PhotoDragThreshold = 4.0;

        private void OnPhotoRowDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control { Tag: int id })
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            this.thisDraggedPhotoId = id;
            this.thisPhotoDragStartPoint = e.GetPosition(this.EditorPhotosList);
            this.thisIsDraggingPhoto = false;
        }

        private void OnPhotoRowDragHandlePointerMoved(object? sender, PointerEventArgs e)
        {
            if (this.thisDraggedPhotoId < 0)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                // The button was released somewhere that did not reach the release handler (outside
                // the window, say); without this the next move would resume a drag the user ended.
                this.ResetPhotoDragState();
                return;
            }

            var current = e.GetPosition(this.EditorPhotosList);

            if (!this.thisIsDraggingPhoto &&
                Math.Abs(current.Y - this.thisPhotoDragStartPoint.Y) < PhotoDragThreshold &&
                Math.Abs(current.X - this.thisPhotoDragStartPoint.X) < PhotoDragThreshold)
            {
                return;
            }

            if (!this.thisIsDraggingPhoto)
            {
                this.BeginPhotoDragPlaceholder();
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
                this.MovePhotoPlaceholderTo(this.ResolvePhotoDropIndex(current));
            }
            finally
            {
                this.thisIsApplyingPhotoPlaceholderMove = false;
            }
        }

        private bool thisIsApplyingPhotoPlaceholderMove;

        private void OnPhotoRowDragHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (this.thisDraggedPhotoId < 0 || !this.thisIsDraggingPhoto)
            {
                this.ResetPhotoDragState();
                return;
            }

            int draggedId = this.thisDraggedPhotoId;

            // The placeholder is already sitting at the drop position, so its index in the row list
            // IS the target - no need to re-measure against the pointer, which would disagree with
            // what the user was just shown if the pointer sat between two rows.
            int targetIndex = this.IndexOfPhotoRow(draggedId);

            this.ResetPhotoDragState();

            if (targetIndex < 0)
            {
                this.RefreshPhotoRows();
                return;
            }

            WorklogAttachmentStorage.ReorderAttachment(this.thisEntry.Photos, draggedId, targetIndex);
            this.RefreshPhotoRows();
            this.PersistEntrySilently();
        }

        // ###########################################################################################
        // Turns the dragged row into the placeholder, sized to the height it currently occupies so
        // the gap does not jump when its content is swapped for the empty outline.
        // ###########################################################################################
        private void BeginPhotoDragPlaceholder()
        {
            int index = this.IndexOfPhotoRow(this.thisDraggedPhotoId);
            if (index < 0)
            {
                return;
            }

            var row = this.thisPhotoRows[index];

            var container = this.EditorPhotosList.ContainerFromIndex(index);
            if (container != null && container.Bounds.Height > 0)
            {
                row.PlaceholderHeight = container.Bounds.Height;
            }

            this.CapturePhotoRowBoundaries();

            row.IsDropPlaceholder = true;
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

        private void CapturePhotoRowBoundaries()
        {
            this.thisPhotoRowDragBoundaries.Clear();

            // One entry per row, always - index i in this list means row i. Skipping a row whose
            // container is not realized would shorten the list and shift every later boundary's
            // meaning by one, so an unmeasurable row gets an interpolated midpoint instead and the
            // two lists stay aligned. ResolvePhotoDropIndex relies on that 1:1 correspondence to
            // return an index into thisPhotoRows.
            double runningY = 0;

            for (int i = 0; i < this.thisPhotoRows.Count; i++)
            {
                var container = this.EditorPhotosList.ContainerFromIndex(i);
                Point? topLeft = container?.TranslatePoint(new Point(0, 0), this.EditorPhotosList);

                double height = container != null && container.Bounds.Height > 0
                    ? container.Bounds.Height
                    : this.thisPhotoRows[i].PlaceholderHeight;

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
        private void MovePhotoPlaceholderTo(int targetIndex)
        {
            if (targetIndex < 0)
            {
                return;
            }

            int currentIndex = this.IndexOfPhotoRow(this.thisDraggedPhotoId);
            if (currentIndex < 0)
            {
                return;
            }

            targetIndex = Math.Clamp(targetIndex, 0, this.thisPhotoRows.Count - 1);
            if (targetIndex == currentIndex)
            {
                return;
            }

            this.thisPhotoRows.Move(currentIndex, targetIndex);
        }

        private int IndexOfPhotoRow(int id)
        {
            for (int i = 0; i < this.thisPhotoRows.Count; i++)
            {
                if (this.thisPhotoRows[i].Id == id)
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
            foreach (var row in this.thisPhotoRows)
            {
                row.IsDropPlaceholder = false;
            }

            // Cleared so the next drag cannot resolve against the previous drag's layout.
            this.thisPhotoRowDragBoundaries.Clear();

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
        private int ResolvePhotoDropIndex(Point pointerInList)
        {
            if (this.thisPhotoRows.Count == 0 || this.thisPhotoRowDragBoundaries.Count == 0)
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
        // Files - same "Add is a no-op, delete/reorder work" shape as Photos above.
        // ###########################################################################################
        private void RefreshFileRows()
        {
            this.thisFileRows.Clear();
            foreach (var file in this.thisEntry.Files.OrderBy(f => f.DisplayOrder))
            {
                this.thisFileRows.Add(new WorklogAttachmentRow { Id = file.Id, FileName = file.FileName, Comment = file.Comment });
            }
            this.EditorNoFilesText.IsVisible = this.thisFileRows.Count == 0;
        }

        private void OnAddFileClick(object? sender, RoutedEventArgs e)
        {
            // Not implemented yet - uploading real files is a follow-up piece of work.
        }

        private void OnDeleteFileClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                this.thisEntry.Files.RemoveAll(f => f.Id == id);
                this.RefreshFileRows();
                this.PersistEntrySilently();
            }
        }

        private void OnMoveFileUpClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                WorklogAttachmentStorage.StepAttachment(this.thisEntry.Files, id, -1);
                this.RefreshFileRows();
                this.PersistEntrySilently();
            }
        }

        private void OnMoveFileDownClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                WorklogAttachmentStorage.StepAttachment(this.thisEntry.Files, id, 1);
                this.RefreshFileRows();
                this.PersistEntrySilently();
            }
        }

        // ###########################################################################################
        // Cancel/Escape discards pending edits to the direct fields (Title/Description/category/
        // state), but still reports WasSaved when a Links/Comments/Work-done/Photos/Files change
        // already made it to disk via PersistEntrySilently, so the caller knows to refresh.
        //
        // "Pending" is the limit of what Cancel can undo: an instant-save commits the direct fields
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
        // workbook's Open/Closed status - editing State to Fixed/RuledOut here is exactly how the
        // user resolves an entry from the full editor, same rule as the quick "New fault" card.
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
        // The row's own height while it is the placeholder, so the gap matches the row being
        // dragged rather than collapsing to the empty box's natural size.
        // ###########################################################################################
        // Only used if the row's container has not been measured yet; BeginPhotoDragPlaceholder
        // replaces it with the row's real height. Roughly a 144px thumbnail plus the row padding.
        public double PlaceholderHeight { get; set; } = 162.0;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
