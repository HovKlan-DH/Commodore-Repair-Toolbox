using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Handlers.DataHandling;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace CRT
{
    public partial class TabOverview : UserControl
    {
        private Main? _mainWindow;
        private List<OverviewRow> _allRows = new();
        private List<OverviewRow> _visibleRows = new();

        public TabOverview()
        {
            this.InitializeComponent();
        }

        // ###########################################################################################
        // Initializes the overview tab with a reference to the main window.
        // ###########################################################################################
        public void Initialize(Main mainWindow)
        {
            this._mainWindow = mainWindow;
        }

        // ###########################################################################################
        // Populates the overview list based on the selected board data.
        // ###########################################################################################
        public void LoadData(BoardData boardData)
        {
            var rows = new List<OverviewRow>();

            foreach (var comp in boardData.Components)
            {
                var note = boardData.ComponentImages
                    .FirstOrDefault(ci => string.Equals(ci.BoardLabel, comp.BoardLabel, StringComparison.OrdinalIgnoreCase))?.Note ?? string.Empty;

                var links = new List<OverviewLink>();

                links.AddRange(boardData.ComponentLocalFiles
                    .Where(lf => string.Equals(lf.BoardLabel, comp.BoardLabel, StringComparison.OrdinalIgnoreCase))
                    .Select(lf => new OverviewLink(lf.Name, lf.File, OverviewLinkType.LocalFile)));

                links.AddRange(boardData.ComponentLinks
                    .Where(l => string.Equals(l.BoardLabel, comp.BoardLabel, StringComparison.OrdinalIgnoreCase))
                    .Select(l => new OverviewLink(l.Name, l.Url, OverviewLinkType.WebLink)));

                rows.Add(new OverviewRow
                {
                    IsSelectedForPrint = true,
                    Component = comp.BoardLabel ?? string.Empty,
                    Category = comp.Category ?? string.Empty,
                    TechnicalName = comp.TechnicalNameOrValue ?? string.Empty,
                    FriendlyName = comp.FriendlyName ?? string.Empty,
                    PartNumber = comp.PartNumber ?? string.Empty,
                    ShortDescription = comp.Description ?? string.Empty,
                    Notes = note,
                    Links = links
                });
            }

            this._allRows = rows;
            this._visibleRows = rows;
            this.OverviewItemsControl.ItemsSource = this._visibleRows;
        }

        // ###########################################################################################
        // Filters the overview list based on the active component/category filters and search term.
        // ###########################################################################################
        public void ApplyFilter(string searchTerm)
        {
            this._visibleRows = this.GetFilteredRows(searchTerm);
            this.OverviewItemsControl.ItemsSource = this._visibleRows;
        }

        // ###########################################################################################
        // Returns the overview rows that survive the current component/category/search filters.
        // ###########################################################################################
        private List<OverviewRow> GetFilteredRows(string searchTerm)
        {
            IEnumerable<OverviewRow> rows = this._allRows;

            var visibleBoardLabels = this.GetVisibleOverviewBoardLabels();
            var selectedBoardLabels = this.GetSelectedOverviewBoardLabels();

            if (selectedBoardLabels.Count > 0)
            {
                rows = rows.Where(row => selectedBoardLabels.Contains(row.Component));
            }
            else if (visibleBoardLabels.Count > 0)
            {
                rows = rows.Where(row => visibleBoardLabels.Contains(row.Component));
            }
            else if (this._mainWindow != null)
            {
                rows = Enumerable.Empty<OverviewRow>();
            }

            var terms = string.IsNullOrWhiteSpace(searchTerm)
                ? Array.Empty<string>()
                : searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (terms.Length == 0)
            {
                return rows.ToList();
            }

            return rows
                .Where(row =>
                {
                    string displayString = this.BuildOverviewDisplayString(row);
                    if (string.IsNullOrWhiteSpace(displayString))
                        return false;

                    foreach (var term in terms)
                    {
                        if (displayString.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }

                    return true;
                })
                .ToList();
        }

        // ###########################################################################################
        // Builds the overview display string used by search and component popup text.
        // ###########################################################################################
        private string BuildOverviewDisplayString(OverviewRow row)
        {
            var parts = new List<string>(3);

            if (!string.IsNullOrWhiteSpace(row.Component))
                parts.Add(row.Component.Trim());
            if (!string.IsNullOrWhiteSpace(row.FriendlyName))
                parts.Add(row.FriendlyName.Trim());
            if (!string.IsNullOrWhiteSpace(row.TechnicalName))
                parts.Add(row.TechnicalName.Trim());

            return string.Join(" | ", parts);
        }

        // ###########################################################################################
        // Returns the currently visible component board labels from the main component list.
        // ###########################################################################################
        private HashSet<string> GetVisibleOverviewBoardLabels()
        {
            if (this._mainWindow == null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                this._mainWindow.ComponentFilterListBox.ItemsSource?
                    .Cast<Main.ComponentListItem>()
                    .Where(item => !string.IsNullOrWhiteSpace(item.BoardLabel))
                    .Select(item => item.BoardLabel.Trim())
                ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        // ###########################################################################################
        // Returns the currently selected component board labels from the main component list.
        // ###########################################################################################
        private HashSet<string> GetSelectedOverviewBoardLabels()
        {
            if (this._mainWindow == null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                this._mainWindow.ComponentFilterListBox.SelectedItems?
                    .Cast<Main.ComponentListItem>()
                    .Where(item => !string.IsNullOrWhiteSpace(item.BoardLabel))
                    .Select(item => item.BoardLabel.Trim())
                ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        // ###########################################################################################
        // Returns the rows currently visible in the overview and checked for printing.
        // ###########################################################################################
        private List<OverviewRow> GetPrintableRows()
        {
            IEnumerable<OverviewRow> rows = this._visibleRows.Count > 0
                ? this._visibleRows
                : (this.OverviewItemsControl.ItemsSource as IEnumerable<OverviewRow>) ?? Enumerable.Empty<OverviewRow>();

            return rows.Where(row => row.IsSelectedForPrint).ToList();
        }

        // ###########################################################################################
        // Selects or deselects every currently visible row for printing, driven by the header checkbox.
        // ###########################################################################################
        private void OnSelectAllPrintClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
            {
                return;
            }

            bool select = checkBox.IsChecked ?? false;

            foreach (var row in this._visibleRows)
            {
                row.IsSelectedForPrint = select;
            }
        }

        // ###########################################################################################
        // Builds a temporary printable HTML document containing the current component table.
        // ###########################################################################################
        private string BuildPrintableHtml(IEnumerable<OverviewRow> rows)
        {
            static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\" />");
            sb.AppendLine("<title>Component List</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("@page { size: A4 portrait; margin: 12mm; }");
            sb.AppendLine("html, body { margin: 0; padding: 0; }");
            sb.AppendLine("body { font-family: Segoe UI, Arial, sans-serif; font-size: 12px; color: #000; box-sizing: border-box; padding: 5px; }");
            sb.AppendLine("table { width: calc(100% - 1px); max-width: calc(100% - 4px); margin-right: 4px; border-collapse: collapse; table-layout: fixed; box-sizing: border-box; }");
            sb.AppendLine("th, td { border: 1px solid #666; padding: 6px 8px; text-align: left; vertical-align: top; word-wrap: break-word; box-sizing: border-box; }");
            sb.AppendLine("th { background: #eaeaea; font-weight: 700; }");
            sb.AppendLine("</style>");
            sb.AppendLine("<script>");
            sb.AppendLine("window.addEventListener('load', function () { window.print(); });");
            sb.AppendLine("</script>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th>Component</th>");
            sb.AppendLine("<th>Technical name</th>");
            sb.AppendLine("<th>Friendly name</th>");
            //            sb.AppendLine("<th>Part-number</th>");
            sb.AppendLine("<th colspan='2'>&nbsp;</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            foreach (var row in rows)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{Encode(row.Component)}</td>");
                sb.AppendLine($"<td>{Encode(row.TechnicalName)}</td>");
                sb.AppendLine($"<td>{Encode(row.FriendlyName)}</td>");
                //                sb.AppendLine($"<td>{Encode(row.PartNumber)}</td>");
                sb.AppendLine("<td colspan='2'>&nbsp;</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        // ###########################################################################################
        // Opens a printable HTML document for the currently visible overview rows.
        // ###########################################################################################
        private void OnPrintComponentListClick(object? sender, RoutedEventArgs e)
        {
            var printableRows = this.GetPrintableRows();
            if (printableRows.Count == 0)
            {
                Logger.Info("Overview print skipped because there are no rows checked for printing");
                return;
            }

            try
            {
                string tempFilePath = Path.Combine(
                    Path.GetTempPath(),
                    $"crt-overview-print-{Guid.NewGuid():N}.html");

                File.WriteAllText(tempFilePath, this.BuildPrintableHtml(printableRows), Encoding.UTF8);

                Process.Start(new ProcessStartInfo(tempFilePath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to print overview component list - [{ex.Message}]");
            }
        }

        // ###########################################################################################
        // Groups printable rows by category/technical name/friendly name for a bill-of-materials view.
        // ###########################################################################################
        private List<OverviewQuantityGroup> BuildQuantityGroups(IEnumerable<OverviewRow> rows)
        {
            return rows
                .GroupBy(row => new
                {
                    Category = row.Category ?? string.Empty,
                    TechnicalName = row.TechnicalName ?? string.Empty,
                    FriendlyName = row.FriendlyName ?? string.Empty
                })
                .Select(g => new OverviewQuantityGroup
                {
                    Type = g.Key.Category,
                    Components = string.Join(", ", g.Select(row => row.Component)),
                    TechnicalName = g.Key.TechnicalName,
                    FriendlyName = g.Key.FriendlyName,
                    Quantity = g.Count()
                })
                .OrderBy(g => g.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.TechnicalName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ###########################################################################################
        // Builds a temporary printable HTML document containing the bill of materials table.
        // ###########################################################################################
        private string BuildPrintableQuantitiesHtml(IEnumerable<OverviewQuantityGroup> groups)
        {
            static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\" />");
            sb.AppendLine("<title>Bill of Materials (BOM)</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("@page { size: A4 portrait; margin: 12mm; }");
            sb.AppendLine("html, body { margin: 0; padding: 0; }");
            sb.AppendLine("body { font-family: Segoe UI, Arial, sans-serif; font-size: 12px; color: #000; box-sizing: border-box; padding: 5px; }");
            sb.AppendLine("table { width: calc(100% - 1px); max-width: calc(100% - 4px); margin-right: 4px; border-collapse: collapse; table-layout: fixed; box-sizing: border-box; }");
            sb.AppendLine("th, td { border: 1px solid #666; padding: 6px 8px; text-align: left; vertical-align: top; word-wrap: break-word; box-sizing: border-box; }");
            sb.AppendLine("th { background: #eaeaea; font-weight: 700; }");
            sb.AppendLine("</style>");
            sb.AppendLine("<script>");
            sb.AppendLine("window.addEventListener('load', function () { window.print(); });");
            sb.AppendLine("</script>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th>Type</th>");
            sb.AppendLine("<th>Components</th>");
            sb.AppendLine("<th>Technical name</th>");
            sb.AppendLine("<th>Friendly name</th>");
            sb.AppendLine("<th>Quantity</th>");
            sb.AppendLine("<th>&nbsp;</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            foreach (var group in groups)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{Encode(group.Type)}</td>");
                sb.AppendLine($"<td>{Encode(group.Components)}</td>");
                sb.AppendLine($"<td>{Encode(group.TechnicalName)}</td>");
                sb.AppendLine($"<td>{Encode(group.FriendlyName)}</td>");
                sb.AppendLine($"<td>{group.Quantity}</td>");
                sb.AppendLine("<td>&nbsp;</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        // ###########################################################################################
        // Opens a printable HTML document with the bill of materials for the currently visible rows.
        // ###########################################################################################
        private void OnPrintComponentQuantitiesClick(object? sender, RoutedEventArgs e)
        {
            var printableRows = this.GetPrintableRows();
            if (printableRows.Count == 0)
            {
                Logger.Info("Overview print skipped because there are no rows checked for printing");
                return;
            }

            var groups = this.BuildQuantityGroups(printableRows);

            try
            {
                string tempFilePath = Path.Combine(
                    Path.GetTempPath(),
                    $"crt-overview-print-bom-{Guid.NewGuid():N}.html");

                File.WriteAllText(tempFilePath, this.BuildPrintableQuantitiesHtml(groups), Encoding.UTF8);

                Process.Start(new ProcessStartInfo(tempFilePath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to print overview bill of materials - [{ex.Message}]");
            }
        }

        // ###########################################################################################
        // Subscribes each row to pointer presses at the tunnel phase, so the click is seen before any
        // child control (button, checkbox, selectable text) has a chance to mark it as handled.
        // ###########################################################################################
        private void OnOverviewRowLoaded(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.AddHandler(InputElement.PointerPressedEvent, this.OnOverviewRowPointerPressed, RoutingStrategies.Tunnel);
            }
        }

        // ###########################################################################################
        // Toggles the row's print-selection checkbox when clicking anywhere in the row other than a
        // button or the checkbox itself (e.g. the component link or a file/web link).
        // ###########################################################################################
        private void OnOverviewRowPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border border || border.DataContext is not OverviewRow row)
                return;

            if (e.Source is Visual clickedVisual &&
                (clickedVisual.FindAncestorOfType<Button>(includeSelf: true) != null ||
                 clickedVisual.FindAncestorOfType<CheckBox>(includeSelf: true) != null))
            {
                return;
            }

            row.IsSelectedForPrint = !row.IsSelectedForPrint;
        }

        // ###########################################################################################
        // Opens the component info popup when clicking a component link.
        // ###########################################################################################
        private void OnComponentClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OverviewRow row && this._mainWindow != null)
            {
                string displayText = this.BuildOverviewDisplayString(row);
                this._mainWindow.OpenComponentInfoPopup(row.Component, displayText);
            }
        }

        // ###########################################################################################
        // Opens a link based on whether it is a local file or web URL.
        // ###########################################################################################
        private void OnLinkClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OverviewLink link)
            {
                if (link.IsLocalFile)
                {
                    var fullPath = Path.Combine(DataManager.DataRoot, link.Target.Replace('/', Path.DirectorySeparatorChar));
                    try
                    {
                        Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to open local file - [{fullPath}] - [{ex.Message}]");
                    }
                }
                else
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(link.Target) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to open web link - [{link.Target}] - [{ex.Message}]");
                    }
                }
            }
        }
    }

    public class OverviewRow : INotifyPropertyChanged
    {
        private bool _isSelectedForPrint = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsSelectedForPrint
        {
            get => this._isSelectedForPrint;
            set
            {
                if (this._isSelectedForPrint == value)
                    return;

                this._isSelectedForPrint = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsSelectedForPrint)));
            }
        }

        public string Component { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string TechnicalName { get; init; } = string.Empty;
        public string FriendlyName { get; init; } = string.Empty;
        public string PartNumber { get; init; } = string.Empty;
        public string ShortDescription { get; init; } = string.Empty;
        public string Notes { get; init; } = string.Empty;
        public List<OverviewLink> Links { get; init; } = new();
    }

    public class OverviewQuantityGroup
    {
        public string Type { get; init; } = string.Empty;
        public string Components { get; init; } = string.Empty;
        public string TechnicalName { get; init; } = string.Empty;
        public string FriendlyName { get; init; } = string.Empty;
        public int Quantity { get; init; }
    }

    public enum OverviewLinkType
    {
        LocalFile,
        WebLink
    }

    public class OverviewLink
    {
        public string Name { get; }
        public string Target { get; }
        public OverviewLinkType Type { get; }

        public bool IsLocalFile => this.Type == OverviewLinkType.LocalFile;
        public bool IsWebLink => this.Type == OverviewLinkType.WebLink;

        public OverviewLink(string name, string target, OverviewLinkType type)
        {
            this.Name = name;
            this.Target = target;
            this.Type = type;
        }
    }

}