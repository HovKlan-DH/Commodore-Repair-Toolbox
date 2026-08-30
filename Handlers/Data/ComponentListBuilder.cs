using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Handlers.DataHandling
{
    // ###########################################################################################
    // Lightweight view model for a component list item.
    // ###########################################################################################
    public sealed class ComponentListItem
    {
        public string DisplayText { get; init; } = string.Empty;
        public string BoardLabel { get; init; } = string.Empty;
        public string SelectionKey { get; init; } = string.Empty;
        public override string ToString() => this.DisplayText;
    }

    // ###########################################################################################
    // One row of the worklog entry card's "Mark components in scope" checklist: a component's
    // board label kept apart from its friendly/technical name, since the UI bolds only the label.
    // ###########################################################################################
    public sealed class ComponentInScope
    {
        public string BoardLabel { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    // ###########################################################################################
    // Builds the main window's component list: the region filter, the category filter, the
    // multi-term search, and the composed display text and selection key for each row.
    //
    // Extracted from Main so the filtering rules can be tested. Two rules matter and are easy to
    // break by accident: a component with a blank region is shared and shows in every region,
    // and search terms are ANDed across the whole composed display string rather than matched
    // against any single field.
    // ###########################################################################################
    public static class ComponentListBuilder
    {
        // ###########################################################################################
        // Returns the distinct component categories in first-seen order, ignoring blank ones.
        // ###########################################################################################
        public static List<string> BuildDistinctCategories(BoardData boardData)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categories = new List<string>();

            foreach (var component in boardData.Components)
            {
                if (!string.IsNullOrWhiteSpace(component.Category) && seen.Add(component.Category))
                    categories.Add(component.Category);
            }

            return categories;
        }

        // ###########################################################################################
        // Builds component list items filtered by the given region and search string.
        // ###########################################################################################
        public static List<ComponentListItem> BuildComponentItems(BoardData boardData, string region, HashSet<string>? categoryFilter = null, string searchTerm = "")
        {
            var items = new List<ComponentListItem>();

            var searchTerms = string.IsNullOrWhiteSpace(searchTerm)
                ? Array.Empty<string>()
                : searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var component in boardData.Components)
            {
                var componentRegion = component.Region?.Trim() ?? string.Empty;

                if (!string.IsNullOrEmpty(componentRegion) &&
                    !string.Equals(componentRegion, region, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (categoryFilter != null && !categoryFilter.Contains(component.Category ?? string.Empty))
                    continue;

                var parts = new List<string>(3);
                if (!string.IsNullOrWhiteSpace(component.BoardLabel))
                    parts.Add(component.BoardLabel.Trim());
                if (!string.IsNullOrWhiteSpace(component.FriendlyName))
                    parts.Add(component.FriendlyName.Trim());
                if (!string.IsNullOrWhiteSpace(component.TechnicalNameOrValue))
                    parts.Add(component.TechnicalNameOrValue.Trim());

                if (parts.Count == 0)
                    continue;

                string displayString = string.Join(" | ", parts);

                if (searchTerms.Length > 0)
                {
                    bool matches = true;
                    foreach (var term in searchTerms)
                    {
                        if (displayString.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (!matches)
                        continue;
                }

                items.Add(new ComponentListItem
                {
                    BoardLabel = component.BoardLabel?.Trim() ?? string.Empty,
                    DisplayText = displayString,
                    SelectionKey = string.Join("\u001F",
                        component.BoardLabel?.Trim() ?? string.Empty,
                        component.FriendlyName?.Trim() ?? string.Empty,
                        component.TechnicalNameOrValue?.Trim() ?? string.Empty,
                        component.Region?.Trim() ?? string.Empty)
                });
            }

            return items;
        }

        // ###########################################################################################
        // Builds the "Mark components in scope" checklist rows: every component whose board label is
        // in boardLabelsInScope, kept in boardData.Components order (the same order Overview and the
        // main Component list use - neither re-sorts) rather than the order boardLabelsInScope was
        // built in.
        // ###########################################################################################
        public static List<ComponentInScope> BuildComponentsInScope(BoardData boardData, IReadOnlyCollection<string> boardLabelsInScope)
        {
            // Always rebuilt with OrdinalIgnoreCase rather than trusting the caller's set, so
            // matching stays case-insensitive - board label comparisons are case-insensitive
            // everywhere else in the app (e.g. the schematic hover lookups) - regardless of which
            // comparer the caller's HashSet happened to be constructed with.
            var scope = new HashSet<string>(boardLabelsInScope, StringComparer.OrdinalIgnoreCase);
            var results = new List<ComponentInScope>();

            foreach (var component in boardData.Components)
            {
                string boardLabel = component.BoardLabel?.Trim() ?? string.Empty;

                if (!scope.Contains(boardLabel))
                    continue;

                var parts = new List<string>(2);
                if (!string.IsNullOrWhiteSpace(component.FriendlyName))
                    parts.Add(component.FriendlyName.Trim());
                if (!string.IsNullOrWhiteSpace(component.TechnicalNameOrValue))
                    parts.Add(component.TechnicalNameOrValue.Trim());

                results.Add(new ComponentInScope
                {
                    BoardLabel = boardLabel,
                    DisplayName = string.Join(" | ", parts)
                });
            }

            return results;
        }

        // ###########################################################################################
        // Returns true when the provided board has at least one component explicitly tagged as PAL or NTSC.
        // This is what decides whether the region switch is offered at all for a board.
        // ###########################################################################################
        public static bool HasExplicitRegionComponents(BoardData? boardData)
        {
            if (boardData == null)
                return false;

            return boardData.Components.Any(component =>
                string.Equals(component.Region?.Trim(), "PAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(component.Region?.Trim(), "NTSC", StringComparison.OrdinalIgnoreCase));
        }

        // ###########################################################################################
        // Returns true when the supplied path points at a supported modern KiCad raw file.
        // ###########################################################################################
        public static bool IsSupportedKiCadRawFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string extension = Path.GetExtension(path.Trim());

            return string.Equals(extension, ".kicad_pcb", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".kicad_pro", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".kicad_sch", StringComparison.OrdinalIgnoreCase);
        }

        // ###########################################################################################
        // Appends protected-file count information to a sync banner message when applicable.
        // ###########################################################################################
        public static string BuildSyncBannerText(string message, int protectedFilesCount)
        {
            return protectedFilesCount > 0
                ? $"{message}; protected contribution related files are [{protectedFilesCount}]"
                : message;
        }
    }
}
