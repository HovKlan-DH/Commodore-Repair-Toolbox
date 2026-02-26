using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CRT
{
    public static class DataManager
    {
        private const string DataRootArg = "--data-root=";
        private const string AppFolderName = "Commodore-Repair-Toolbox";
        private const string MainExcelFileName = "Commodore-Repair-Toolbox.xlsx";
        private const string SheetHardwareBoard = "Hardware & Board";

        // Column header names used for robust, order-independent column mapping
        private const string ColHardwareName = "Hardware name in drop-down";
        private const string ColBoardName = "Board name in drop-down";
        private const string ColExcelDataFile = "Excel data file";
        private const string ColHardwareNotes = "Hardware notes in \"Overview\" tab";

        private static string _dataRoot = string.Empty;

        public static string DataRoot => _dataRoot;
        public static List<HardwareBoardEntry> HardwareBoards { get; private set; } = [];

        // Raised with a general status message (e.g. "Checking files...", "Sync complete")
        public static event Action<string>? StatusChanged;

        // Raised with the relative file path of whichever file is currently being processed
        public static event Action<string>? FileDownloadChanged;

        // ###########################################################################################
        // Resolves the data root, ensures the folder exists, then synchronizes all files against
        // the online checksum manifest. Always runs at startup — never skips the online check.
        // ###########################################################################################
        public static async Task InitializeAsync(string[] args)
        {
            _dataRoot = ResolveDataRoot(args);
            Logger.Info($"Data root is [{_dataRoot}]");

            bool isNewRoot = !Directory.Exists(_dataRoot);
            Directory.CreateDirectory(_dataRoot);

            if (isNewRoot)
                Logger.Info("Data root folder created — all files will be downloaded from online source");
            else
                Logger.Info("Checking online source for new or updated files");

#if DEBUG
            Logger.Info("DEBUG build - skipping online sync");
#else
            RaiseStatus("Checking files against online source...");
            await OnlineServices.SyncDataAsync(_dataRoot, RaiseStatus, RaiseFileDownload);
#endif
            RaiseStatus("Loading hardware definitions...");
            await Task.Run(LoadMainExcel);
        }

        // ###########################################################################################
        // Parses --data-root from args, or falls back to a persistent AppData folder that survives
        // Velopack updates (which replace the install directory but leave AppData untouched).
        // ###########################################################################################
        private static string ResolveDataRoot(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith(DataRootArg, StringComparison.OrdinalIgnoreCase))
                    return arg[DataRootArg.Length..].Trim('"', '\'');
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, AppFolderName, "Data");
        }

        // ###########################################################################################
        // Reads hardware and board definitions from the main Excel file.
        // Column positions are resolved by header name so reordering columns is handled gracefully.
        // Hardware name is carried forward across rows where the cell is empty (merged cell pattern).
        // ###########################################################################################
        private static void LoadMainExcel()
        {
            var excelPath = Path.Combine(_dataRoot, MainExcelFileName);

            if (!File.Exists(excelPath))
            {
                Logger.Warning($"Main Excel file not found - [{excelPath}]");
                return;
            }

            ExcelPackage.License.SetNonCommercialPersonal("Dennis Helligsø");

            try
            {
                using var stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var package = new ExcelPackage(stream);
                var sheet = package.Workbook.Worksheets[SheetHardwareBoard];

                if (sheet == null)
                {
                    Logger.Warning($"Sheet [{SheetHardwareBoard}] not found in main Excel file");
                    return;
                }

                var colMap = FindHeaderRow(sheet, out int headerRow);

                if (colMap == null)
                {
                    Logger.Warning("Header row not found in main Excel file - verify column header names match expected values");
                    return;
                }

                Logger.Info($"Header row found at row [{headerRow}] in sheet [{SheetHardwareBoard}]");

                var entries = new List<HardwareBoardEntry>();
                int maxRow = sheet.Dimension?.End.Row ?? 0;
                string lastHwName = string.Empty;

                for (int row = headerRow + 1; row <= maxRow; row++)
                {
                    string hardwareName = GetCellText(sheet, row, colMap[ColHardwareName]);
                    string boardName = GetCellText(sheet, row, colMap[ColBoardName]);
                    string excelFile = GetCellText(sheet, row, colMap[ColExcelDataFile]);
                    string notes = GetCellText(sheet, row, colMap[ColHardwareNotes]);

                    // Carry forward hardware name for merged/empty cells in that column
                    if (!string.IsNullOrWhiteSpace(hardwareName))
                        lastHwName = hardwareName;
                    else
                        hardwareName = lastHwName;

                    // Skip rows that have neither a board name nor an Excel file reference
                    if (string.IsNullOrWhiteSpace(boardName) && string.IsNullOrWhiteSpace(excelFile))
                        continue;

                    entries.Add(new HardwareBoardEntry
                    {
                        HardwareName = hardwareName,
                        BoardName = boardName,
                        ExcelDataFile = excelFile,
                        HardwareNotes = notes
                    });
                }

                HardwareBoards = entries;
                Logger.Info($"Loaded [{entries.Count}] hardware/board entries from main Excel file");
            }
            catch (Exception ex)
            {
                Logger.Critical($"Failed to load main Excel file - [{ex.Message}]");
            }
        }

        // ###########################################################################################
        // Lazily loads and caches all sheets from the board-specific Excel file linked to the entry.
        // Delegates to BoardDataReader for parsing and caching. Returns null on failure.
        // ###########################################################################################
        public static async Task<BoardData?> LoadBoardDataAsync(HardwareBoardEntry entry)
        {
            var excelPath = Path.Combine(_dataRoot, entry.ExcelDataFile.Replace('/', Path.DirectorySeparatorChar));
            return await BoardDataReader.LoadAsync(excelPath, entry.ExcelDataFile);
        }

        // ###########################################################################################
        // Scans the worksheet for the first row containing all required column header names.
        // Matching is case-insensitive. Returns a header-name-to-column-index map on success,
        // or null when not all required headers are found.
        // ###########################################################################################
        private static Dictionary<string, int>? FindHeaderRow(ExcelWorksheet sheet, out int headerRow)
        {
            headerRow = -1;

            var required = new[] { ColHardwareName, ColBoardName, ColExcelDataFile, ColHardwareNotes };
            int maxRow = sheet.Dimension?.End.Row ?? 0;
            int maxCol = sheet.Dimension?.End.Column ?? 0;

            for (int row = 1; row <= maxRow; row++)
            {
                var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (int col = 1; col <= maxCol; col++)
                {
                    string text = NormalizeHeader(GetCellText(sheet, row, col));
                    if (!string.IsNullOrWhiteSpace(text))
                        colMap[text] = col;
                }

                if (required.All(h => colMap.ContainsKey(h)))
                {
                    headerRow = row;
                    return colMap;
                }
            }

            return null;
        }

        // ###########################################################################################
        // Collapses Alt+Enter line breaks in Excel cell headers into a single space, then trims.
        // ###########################################################################################
        private static string NormalizeHeader(string text)
        {
            var parts = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts).Trim();
        }

        // ###########################################################################################
        // Returns the trimmed text value of a worksheet cell, or an empty string if null or blank.
        // ###########################################################################################
        private static string GetCellText(ExcelWorksheet sheet, int row, int col)
            => sheet.Cells[row, col].Text?.Trim() ?? string.Empty;

        // ###########################################################################################
        // Fires the StatusChanged event with the given message.
        // ###########################################################################################
        private static void RaiseStatus(string message) => StatusChanged?.Invoke(message);

        // ###########################################################################################
        // Fires the FileDownloadChanged event with the given file path.
        // ###########################################################################################
        private static void RaiseFileDownload(string filePath) => FileDownloadChanged?.Invoke(filePath);
    }
}