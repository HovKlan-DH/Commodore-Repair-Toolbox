using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Handlers.DataHandling;

namespace CRT
{
    // ###########################################################################################
    // Modal dialog collecting the two fields a new workbook needs (a description and an optional
    // note); the id shown is only a preview of what WorklogManager.CreateWorkbook will allocate.
    // Returns the created WorkbookRecord via ShowDialog, or null when cancelled.
    // ###########################################################################################
    public partial class CreateWorkbookWindow : Window
    {
        private string thisBoardKey = string.Empty;

        public CreateWorkbookWindow()
        {
            this.InitializeComponent();

            this.WorkbookIdPreviewText.Text = $"#{WorklogManager.PeekNextId()}";

            this.Opened += (_, _) =>
                Dispatcher.UIThread.Post(() => this.TitleTextBox.Focus(), DispatcherPriority.Background);
        }

        // ###########################################################################################
        // Must be called before showing the dialog: sets the board the new workbook belongs to.
        // ###########################################################################################
        public void Initialize(string boardKey)
        {
            this.thisBoardKey = boardKey;
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            this.Close(null);
        }

        private void OnCreateClick(object? sender, RoutedEventArgs e)
        {
            string title = this.TitleTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title))
            {
                this.TitleValidationText.Text = "A description is required.";
                this.TitleValidationText.IsVisible = true;
                this.TitleTextBox.Focus();
                return;
            }

            this.TitleValidationText.IsVisible = false;

            string note = this.NoteTextBox.Text?.Trim() ?? string.Empty;
            var record = WorklogManager.CreateWorkbook(this.thisBoardKey, title, note);

            if (record == null)
            {
                // The workbook root was unusable or the write failed - closing with null here would
                // just make the dialog vanish as if the user had cancelled, so say so instead and
                // keep what they typed. The log carries the underlying reason.
                this.TitleValidationText.Text = "Could not create the workbook - see the log for details.";
                this.TitleValidationText.IsVisible = true;
                return;
            }

            this.Close(record);
        }
    }
}
