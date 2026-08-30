using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
        private void PersistEntrySilently()
        {
            this.SyncDirectFieldsToEntry();

            if (WorklogManager.UpdateEntry(this.thisWorkbookId, this.thisEntry))
            {
                this.thisHasPersistedChange = true;
                this.EditorSaveFailedText.IsVisible = false;
                return;
            }

            // "Silently" covers not closing the window and not touching WasSaved - not hiding a
            // failure. The sub-list change the user just made is only in the working copy.
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
        // Photos/images - Add has no functionality yet (no file picker wired up); delete and
        // drag-reorder (via the up/down buttons) work against the working copy so they are ready
        // for whenever Add is implemented.
        // ###########################################################################################
        private void RefreshPhotoRows()
        {
            this.thisPhotoRows.Clear();
            foreach (var photo in this.thisEntry.Photos.OrderBy(p => p.DisplayOrder))
            {
                this.thisPhotoRows.Add(new WorklogAttachmentRow { Id = photo.Id, FileName = photo.FileName, Comment = photo.Comment });
            }
            this.EditorNoPhotosText.IsVisible = this.thisPhotoRows.Count == 0;
        }

        private void OnAddPhotoClick(object? sender, RoutedEventArgs e)
        {
            // Not implemented yet - uploading real photo files is a follow-up piece of work.
        }

        private void OnDeletePhotoClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                this.thisEntry.Photos.RemoveAll(p => p.Id == id);
                this.RefreshPhotoRows();
                this.PersistEntrySilently();
            }
        }

        private void OnMovePhotoUpClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                MoveAttachment(this.thisEntry.Photos, id, -1);
                this.RefreshPhotoRows();
                this.PersistEntrySilently();
            }
        }

        private void OnMovePhotoDownClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                MoveAttachment(this.thisEntry.Photos, id, 1);
                this.RefreshPhotoRows();
                this.PersistEntrySilently();
            }
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
                MoveAttachment(this.thisEntry.Files, id, -1);
                this.RefreshFileRows();
                this.PersistEntrySilently();
            }
        }

        private void OnMoveFileDownClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int id })
            {
                MoveAttachment(this.thisEntry.Files, id, 1);
                this.RefreshFileRows();
                this.PersistEntrySilently();
            }
        }

        // ###########################################################################################
        // Swaps the DisplayOrder of the id'd attachment with its neighbour in the given direction
        // (-1 = up/earlier, +1 = down/later), then renumbers every row 0..N-1 so DisplayOrder always
        // stays a dense, gap-free ordering regardless of how many swaps have happened.
        // ###########################################################################################
        private static void MoveAttachment(System.Collections.Generic.List<WorklogAttachmentRecord> attachments, int id, int direction)
        {
            var ordered = attachments.OrderBy(a => a.DisplayOrder).ToList();
            int index = ordered.FindIndex(a => a.Id == id);
            int targetIndex = index + direction;

            if (index < 0 || targetIndex < 0 || targetIndex >= ordered.Count)
                return;

            (ordered[index], ordered[targetIndex]) = (ordered[targetIndex], ordered[index]);

            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].DisplayOrder = i;
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
                this.EditorSaveFailedText.IsVisible = true;
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

    public sealed class WorklogAttachmentRow
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
}
