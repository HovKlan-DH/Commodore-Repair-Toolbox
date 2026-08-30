using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace CRT
{
    // ###########################################################################################
    // Tiny modal collecting one "Work done" row: a description plus hours spent and cost. Returns
    // the triple via ShowDialog, or null when cancelled. The date/time is stamped by the caller
    // (DateTime.Now at the moment of Add), not entered here.
    // ###########################################################################################
    public partial class WorklogAddWorkDoneWindow : Window
    {
        public WorklogAddWorkDoneWindow()
        {
            this.InitializeComponent();

            this.Opened += (_, _) =>
                Dispatcher.UIThread.Post(() => this.DescriptionTextBox.Focus(), DispatcherPriority.Background);

            this.AddHandler(KeyDownEvent, this.OnWindowPreviewKeyDown, RoutingStrategies.Tunnel);
        }

        // ###########################################################################################
        // Switches the dialog into "edit" mode: pre-fills the existing description/hours/cost and
        // relabels the title/submit button, so the same modal serves both "Add work" and the Work
        // done row's click-to-edit behavior.
        // ###########################################################################################
        public void InitializeForEdit(string text, double hoursSpent, double cost)
        {
            this.Title = "Edit work done";
            this.HeaderText.Text = "Edit work done";
            this.AddButton.Content = "Update work";
            this.DescriptionTextBox.Text = text;
            this.HoursNumericUpDown.Value = (decimal)hoursSpent;
            this.CostNumericUpDown.Value = (decimal)cost;
        }

        // ###########################################################################################
        // Escape cancels, same as the comment dialog. Plain Enter is deliberately left alone since the
        // description field is multi-line - Ctrl+Enter submits instead (see the hint text under the
        // textarea). Handled on the Tunnel route so it fires before DescriptionTextBox's own
        // AcceptsReturn handling inserts a newline - a bubbling KeyDown handler would run too late.
        // ###########################################################################################
        private void OnWindowPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.OnCancelClick(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                this.OnAddClick(sender, e);
                e.Handled = true;
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            this.Close(null);
        }

        private void OnAddClick(object? sender, RoutedEventArgs e)
        {
            string text = this.DescriptionTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                this.DescriptionValidationText.IsVisible = true;
                return;
            }

            double hoursSpent = (double?)this.HoursNumericUpDown.Value ?? 0.0;
            double cost = (double?)this.CostNumericUpDown.Value ?? 0.0;

            this.Close(((string Text, double HoursSpent, double Cost)?)(text, hoursSpent, cost));
        }
    }
}
