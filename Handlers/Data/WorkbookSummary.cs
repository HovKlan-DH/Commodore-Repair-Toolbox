using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Handlers.DataHandling
{
    // ###########################################################################################
    // What one workbook adds up to, across every entry in it: hours, cost, how many entries are in
    // each category and each state, and how many comments/links/photos/files/components the whole
    // workbook carries.
    //
    // The Workbooks tab shows these in the collapsible strip under its top-line, and the PDF/ZIP
    // export prints them as its opening section - two surfaces, one set of numbers, so an exported
    // document can never disagree with what the app showed when it was produced.
    //
    // Pure: takes the records it is given and returns a value. No file reads, no controls - the
    // caller has already read the entries (the tab from its within-pass cache, the exporter from
    // WorklogManager.GetEntries), and doing it again here would both slow the tab down and let the
    // two surfaces read different states of the same folder.
    // ###########################################################################################
    public static class WorkbookSummary
    {
        // The three categories and two states are fixed vocabulary (WorklogManager's model, as
        // opposed to the four-category one the old mockup drew), and the summary reports a count
        // for each of them INCLUDING the zeroes: "Issue 0" is information - it says this workbook
        // records no faults - whereas a row that silently omits the empty categories makes the
        // reader work out which are missing.
        public static readonly IReadOnlyList<string> Categories = new[] { "Note", "Cosmetic", "Issue" };

        public static readonly IReadOnlyList<string> States = new[] { "Open", "Closed" };

        public sealed class Totals
        {
            public int EntryCount { get; init; }

            public double TotalHours { get; init; }

            public double TotalCost { get; init; }

            public int CommentCount { get; init; }

            public int LinkCount { get; init; }

            public int PhotoCount { get; init; }

            public int FileCount { get; init; }

            public int WorkDoneCount { get; init; }

            // How many entries carry at least one component in scope, and how many DISTINCT
            // components the workbook touches across all of its entries. Distinct rather than a
            // running sum: the same chip legitimately appears in several entries (checked in one,
            // replaced in another), and adding those up would report more components than the board
            // has - which reads as a data error to anyone who knows the board.
            public int ComponentCount { get; init; }

            public int CompletedComponentCount { get; init; }

            // Keyed by the vocabulary above, every key always present - see Categories/States.
            public IReadOnlyDictionary<string, int> EntriesByCategory { get; init; } =
                new Dictionary<string, int>();

            public IReadOnlyDictionary<string, int> EntriesByState { get; init; } =
                new Dictionary<string, int>();

            public int OpenEntryCount =>
                this.EntriesByState.TryGetValue("Open", out int open) ? open : 0;

            public int ClosedEntryCount =>
                this.EntriesByState.TryGetValue("Closed", out int closed) ? closed : 0;
        }

        // ###########################################################################################
        // Sums a workbook's entries. A null or empty list gives an all-zero Totals rather than null,
        // because every caller renders the result and a workbook with no entries yet is an ordinary
        // state, not a failure - "0 entries · 0 h" is the correct thing to show for it.
        // ###########################################################################################
        public static Totals Summarize(IEnumerable<WorklogEntryRecord>? entries)
        {
            var list = entries?.ToList() ?? new List<WorklogEntryRecord>();

            var byCategory = Categories.ToDictionary(c => c, _ => 0, StringComparer.OrdinalIgnoreCase);
            var byState = States.ToDictionary(s => s, _ => 0, StringComparer.OrdinalIgnoreCase);

            // Component labels are the user's own text from the board data, and the same component
            // reached from two entries must count once - hence a set, case-insensitively, matching
            // how every other component-label comparison in this app is done.
            var components = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var completedComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            double totalHours = 0.0;
            double totalCost = 0.0;
            int comments = 0, links = 0, photos = 0, files = 0, workDone = 0;

            foreach (var entry in list)
            {
                if (entry == null)
                    continue;

                var (hours, cost) = WorklogEntryScope.GetWorkDoneTotals(entry);
                totalHours += hours;
                totalCost += cost;

                comments += entry.Comments.Count;
                links += entry.Links.Count;
                photos += entry.Photos.Count;
                files += entry.Files.Count;
                workDone += entry.WorkDoneItems.Count;

                foreach (var label in entry.ComponentLabels.Where(l => !string.IsNullOrWhiteSpace(l)))
                    components.Add(label.Trim());

                foreach (var label in entry.CompletedComponentLabels.Where(l => !string.IsNullOrWhiteSpace(l)))
                    completedComponents.Add(label.Trim());

                // An unrecognised category or state - a value from a future build, or an entry
                // edited by hand - is counted nowhere rather than being forced into one of the
                // known buckets. Miscounting it as "Note" would quietly misreport the workbook;
                // EntryCount below still counts the entry itself, so nothing goes missing.
                if (byCategory.ContainsKey(entry.Category ?? string.Empty))
                    byCategory[entry.Category!]++;

                if (byState.ContainsKey(entry.State ?? string.Empty))
                    byState[entry.State!]++;
            }

            return new Totals
            {
                EntryCount = list.Count,
                TotalHours = totalHours,
                TotalCost = totalCost,
                CommentCount = comments,
                LinkCount = links,
                PhotoCount = photos,
                FileCount = files,
                WorkDoneCount = workDone,
                ComponentCount = components.Count,
                CompletedComponentCount = completedComponents.Count,
                EntriesByCategory = byCategory,
                EntriesByState = byState
            };
        }

        // ###########################################################################################
        // ONE PIECE of a summary line: a number and the words around it.
        //
        // Exists because the numbers are rendered BOLD and the words are not, which a single string
        // cannot express - the UI needs to know where each number starts and ends. Splitting it
        // here rather than re-finding the digits in the UI keeps the "which part is the number"
        // decision beside the code that produced it; a regex over a finished string would have to
        // guess about "0.5 h" and about a workbook id inside a title.
        //
        // Prefix is what comes before the number (usually empty), Suffix the unit or noun after it.
        // ###########################################################################################
        public readonly record struct Stat(string Prefix, string Number, string Suffix);

        // ###########################################################################################
        // The always-visible headline, as its parts: worklogs, hours, cost, and how many are open.
        //
        // "WORKLOGS", not "entries" - the app calls these worklogs everywhere the user can see
        // (the worklog bar, "Add worklog", the workbook cards), and "entry" was internal vocabulary
        // leaking out through this one line.
        //
        // Hours and cost are formatted exactly as WorklogEntryEditorWindow's own SummaryText and the
        // entry detail card's stats row do - "{0:0.##}" in InvariantCulture, and a bare cost number
        // with no currency symbol. The app never asks which currency the user works in, so printing
        // one would be a guess; the number is the user's own figure back again.
        // ###########################################################################################
        public static IReadOnlyList<Stat> BuildHeadlineStats(Totals totals) => new[]
        {
            new Stat(string.Empty, totals.EntryCount.ToString(CultureInfo.InvariantCulture),
                totals.EntryCount == 1 ? " worklog" : " worklogs"),
            new Stat(string.Empty, totals.TotalHours.ToString("0.##", CultureInfo.InvariantCulture), " h"),
            new Stat(string.Empty, totals.TotalCost.ToString("0.##", CultureInfo.InvariantCulture), string.Empty),
            new Stat(string.Empty, totals.OpenEntryCount.ToString(CultureInfo.InvariantCulture), " open")
        };

        // The attachment counts as parts, so each number can be bolded. Same content as
        // FormatAttachmentBreakdown below, which the PDF still uses as plain text.
        public static IReadOnlyList<Stat> BuildAttachmentStats(Totals totals) => new[]
        {
            CountStat(totals.CommentCount, "comment", "comments"),
            CountStat(totals.LinkCount, "link", "links"),
            CountStat(totals.PhotoCount, "photo", "photos"),
            CountStat(totals.FileCount, "file", "files"),
            new Stat(string.Empty, totals.WorkDoneCount.ToString(CultureInfo.InvariantCulture), " work done")
        };

        public static IReadOnlyList<Stat> BuildComponentStats(Totals totals) => new[]
        {
            CountStat(totals.ComponentCount, "component in scope", "components in scope"),
            new Stat(string.Empty, totals.CompletedComponentCount.ToString(CultureInfo.InvariantCulture), " completed")
        };

        private static Stat CountStat(int count, string singular, string plural) =>
            new(string.Empty, count.ToString(CultureInfo.InvariantCulture), " " + (count == 1 ? singular : plural));

        // ###########################################################################################
        // The category and state counts, as (label, count) pairs for the UI to render as pills.
        //
        // Every category and every state is returned INCLUDING the zeroes - see Categories. A pill
        // reading "0 Issue" is information: it says this workbook records no faults. Dropping the
        // empty ones would leave the reader working out which are missing, and would make the row
        // change width as a workbook is worked on.
        // ###########################################################################################
        public static IReadOnlyList<(string Label, int Count)> BuildCategoryCounts(Totals totals) =>
            Categories.Select(c => (c, totals.EntriesByCategory.TryGetValue(c, out int n) ? n : 0)).ToList();

        public static IReadOnlyList<(string Label, int Count)> BuildStateCounts(Totals totals) =>
            States.Select(s => (s, totals.EntriesByState.TryGetValue(s, out int n) ? n : 0)).ToList();

        // ###########################################################################################
        // The same lines as flat strings, for the PDF export - which has no pills and no bold runs
        // to put a number in, and wants one plain sentence per line.
        //
        // These share the Stat/count builders above rather than formatting the numbers a second
        // time, so the document and the screen cannot disagree about what a workbook adds up to.
        // ###########################################################################################
        public static string FormatHeadline(Totals totals) =>
            string.Join(" · ", BuildHeadlineStats(totals).Select(FormatStat));

        public static string FormatCategoryBreakdown(Totals totals) =>
            string.Join(" · ", BuildCategoryCounts(totals).Select(c => $"{c.Count} {c.Label}"));

        public static string FormatStateBreakdown(Totals totals) =>
            string.Join(" · ", BuildStateCounts(totals).Select(s => $"{s.Count} {s.Label}"));

        public static string FormatAttachmentBreakdown(Totals totals) =>
            string.Join(" · ", BuildAttachmentStats(totals).Select(FormatStat));

        public static string FormatComponentBreakdown(Totals totals) =>
            string.Join(" · ", BuildComponentStats(totals).Select(FormatStat));

        private static string FormatStat(Stat stat) => stat.Prefix + stat.Number + stat.Suffix;
    }
}
