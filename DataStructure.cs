using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    public class DataStructure
    {
        // Loads all data from Excel into classHardware
        public static void GetAllData(List<Hardware> classHardware)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Dennis Helligsø");

            string filePathMain = Path.Combine(DataPaths.DataRoot, "Commodore-Repair-Toolbox.xlsx");

            // Exit check
            if (!File.Exists(filePathMain))
            {
                string error = $"File [{filePathMain}] does not exists";
                MessageBox.Show(error, "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            // ---

            // 1a) Load hardware from initial data file (Commodore-Repair-Toolbox.xlsx)
            using (var package = new ExcelPackage(new FileInfo(filePathMain)))
            {
                var worksheet = package.Workbook.Worksheets[0]; // sheet = "Hardware & Board"
                string searchHeader = "Hardware and boards";
                int row = 1;

                // Find the row that contains the section header ("Hardware name in drop-down") in column 1
                while (row <= worksheet.Dimension.End.Row)
                {
                    if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                    {
                        break;
                    }

                    row++;
                }

                // If we never found the header, treat as fatal misconfiguration
                if (row > worksheet.Dimension.End.Row)
                {
                    string error = $"ERROR: Excel file [Commodore-Repair-Toolbox.xlsx] the header row with label [{searchHeader}] was not found";
                    Main.DebugOutput(error);

                    string logPath = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.log");
                    string message = $"No valid hardware definition section found in [{filePathMain}].\r\n\r\nView troubleshoot log for details [{logPath}].";
                    MessageBox.Show(message, "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(-1);
                }

                // Next row is the "column headers" row
                row++;
                int headerRow = row;

                // Build a header name -> column index map
                var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                    {
                        headerToColumn[headerValue] = col;
                    }
                }

                // Data starts after the header row
                row = headerRow + 1;

                // Read rows until column 1 becomes empty (same sentinel pattern as elsewhere)
                while (worksheet.Cells[row, 1].Value != null)
                {
                    // These header names must match the text in the Excel header row
                    string nameHardware = GetCellString(worksheet, headerToColumn, row, "Hardware name in drop-down", string.Empty);
                    string nameBoard = GetCellString(worksheet, headerToColumn, row, "Board name in drop-down", string.Empty);
                    string datafile = GetCellString(worksheet, headerToColumn, row, "Excel data file", string.Empty);

                    // Report a warning if path contains a backslash
                    if (!string.IsNullOrEmpty(datafile) && datafile.Contains("\\"))
                    {
                        string error = $"WARNING: Excel file [Commodore-Repair-Toolbox.xlsx] row [{row}] the path [{datafile}] contains a backslash";
                        Main.DebugOutput(error);
                    }

                    // Check if the board datafile exists
                    string filePath = Path.Combine(DataPaths.DataRoot, datafile);
                    if (!string.IsNullOrEmpty(datafile) && File.Exists(filePath))
                    {
                        // Create the hardware in class, if it does not already exists
                        Hardware hw = classHardware.FirstOrDefault(h => h.Name == nameHardware);
                        if (hw == null)
                        {
                            hw = new Hardware
                            {
                                Name = nameHardware,
                                Boards = new List<Board>()
                            };
                            classHardware.Add(hw);
                        }

                        // Add board to hardware
                        Board board = new Board
                        {
                            Name = nameBoard,
                            DataFile = datafile
                        };
                        hw.Boards.Add(board);
                    }
                    else
                    {
                        // Only log if a datafile value is present; empty rows will be ignored via the while-condition
                        if (!string.IsNullOrEmpty(datafile))
                        {
                            string error = $"ERROR: Excel file [Commodore-Repair-Toolbox.xlsx] row [{row}] the file [{filePath}] does not exists";
                            Main.DebugOutput(error);
                        }
                    }

                    row++;
                }
            } // 1) Load hardware from initial data file (Commodore-Repair-Toolbox.xlsx)

            // ---

            // 1b) Load "version match"
            using (var package = new ExcelPackage(new FileInfo(filePathMain)))
            {
                var worksheet = package.Workbook.Worksheets[1]; // sheet = "Version match"
                string searchHeader = "CRT version(s) where this Excel will work";
                int row = 1;

                // Find the row that contains the section header in column 1
                while (row <= worksheet.Dimension.End.Row)
                {
                    if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                    {
                        break;
                    }

                    row++;
                }

                // If we never found the header, treat as fatal misconfiguration
                if (row > worksheet.Dimension.End.Row)
                {
                    string error = $"ERROR: Excel file [Commodore-Repair-Toolbox.xlsx] the header row with label [{searchHeader}] was not found";
                    Main.DebugOutput(error);

                    string logPath = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.log");
                    string message = $"No valid hardware definition section found in [{filePathMain}].\r\n\r\nView troubleshoot log for details [{logPath}].";
                    MessageBox.Show(message, "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(-1);
                }

                // This is the "column headers" row
                row++;
                int headerRow = row;

                // Build a header name -> column index map
                var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                    {
                        headerToColumn[headerValue] = col;
                    }
                }

                // Data starts after the header row
                row = headerRow + 1;

                // Read rows until column 1 becomes empty (same sentinel pattern as elsewhere)
                string[] versionsFromExcel = {};
                while (worksheet.Cells[row, 1].Value != null)
                {
                    // These header names must match the text in the Excel header row
                    string version = GetCellString(worksheet, headerToColumn, row, "Version", string.Empty);

                    // Add "version" to "versionsFromExcel" array
                    Array.Resize(ref versionsFromExcel, versionsFromExcel.Length + 1);
                    versionsFromExcel[versionsFromExcel.Length - 1] = version;

                    row++;
                }

                // Check if at least one of the versions in "versionsFromExcel" matches "Main.versionThis", based on length of string for "versionsFromExcel"
                bool versionMatchFound = false;
                foreach (string version in versionsFromExcel)
                {
                    if (Main.versionThis.StartsWith(version, StringComparison.Ordinal))
                    {
                        versionMatchFound = true;
                        break;
                    }
                }
                if (!versionMatchFound)
                {
                    Array.Resize(ref Main.versionMismatch, Main.versionMismatch.Length + 1);
                    Main.versionMismatch[Main.versionMismatch.Length - 1] = filePathMain;
                }
            } // 1b)

            // ---

            // Exit check for "Hardware"
            if (classHardware.Count == 0)
            {
                string filePath = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.log");
                string error = $"No active hardware defined in [{filePathMain}].\r\n\r\nView troubleshoot log for details [{filePath}].";
                MessageBox.Show(error, "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            // Exit check for "Boards"
            foreach (Hardware hardware in classHardware)
            {
                if (hardware.Boards == null || hardware.Boards.Count == 0)
                {
                    string filePath = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.log");
                    string error = $"No active boards defined in [{filePathMain}].\r\n\r\nView troubleshoot log for details [{filePath}].";
                    MessageBox.Show(error, "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(-1);
                }
            }

            // Shadow initialization
            Main.shadow_structure = new Dictionary<string, Dictionary<string, List<string>>>();

            // ---

            // 2) Load "Board" entries (schematics/images) sheet for all boards
            foreach (Hardware hardware in classHardware)
            {

                // Shadow "hardware"
                if (!Main.shadow_structure.ContainsKey(hardware.Name))
                {
                    Main.shadow_structure[hardware.Name] = new Dictionary<string, List<string>>();
                }

                foreach (Board board in hardware.Boards)
                {

                    // Shadow "board"
                    if (!Main.shadow_structure[hardware.Name].ContainsKey(board.Name))
                    {
                        Main.shadow_structure[hardware.Name][board.Name] = new List<string>();
                    }

                    string filePathBoardData = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePathBoardData)))
                    {
                        string sheet = "Board schematics";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        if (worksheet != null)
                        {
                            string revisionDate = "(unknown)";
                            string searchRevisionDate = "# Revision date:";
                            board.RevisionDate = revisionDate; // default value

                            string sectionHeader = "Board schematic images";
                            string columnHeaderMarker = "Schematic name";

                            int row = 1;

                            // 1) Scan to find revision date and the section header row
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                string col1 = worksheet.Cells[row, 1].Value?.ToString();

                                // Get the "revision date", if it is present
                                if (!string.IsNullOrEmpty(col1) && col1.StartsWith(searchRevisionDate, StringComparison.Ordinal))
                                {
                                    string fullValue = col1;
                                    revisionDate = fullValue.Substring(searchRevisionDate.Length).Trim();
                                    board.RevisionDate = revisionDate;
                                }

                                // Find the section header row (e.g. "Board schematics")
                                if (string.Equals(col1, sectionHeader, StringComparison.Ordinal))
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the section header, log and continue
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePathBoardData);
                                string error = $"ERROR: Excel file [{fileName}] the section header row for worksheet [{sheet}] with label [{sectionHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // 2) Next row should contain the column headers (including "Schematic name")
                            row++;
                            int headerRow = row;

                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Ensure we at least have the main required headers
                            if (!headerToColumn.ContainsKey(columnHeaderMarker) ||
                                !headerToColumn.ContainsKey("Schematic image file") ||
                                !headerToColumn.ContainsKey("Main image highlight color") ||
                                !headerToColumn.ContainsKey("Main highlight opacity") ||
                                !headerToColumn.ContainsKey("Thumbnail image highlight color") ||
                                !headerToColumn.ContainsKey("Thumbnail highlight opacity"))
                            {
                                string fileName = Path.GetFileName(filePathBoardData);
                                string error = $"ERROR: Excel file [{fileName}] worksheet [{sheet}] missing one or more required headers for board schematics";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // 3) Data starts after the header row
                            row = headerRow + 1;

                            while (worksheet.Cells[row, headerToColumn[columnHeaderMarker]].Value != null)
                            {
                                string name = GetCellString(worksheet, headerToColumn, row, "Schematic name", string.Empty);
                                string fileName = GetCellString(worksheet, headerToColumn, row, "Schematic image file", string.Empty);
                                string colorZoom = GetCellString(worksheet, headerToColumn, row, "Main image highlight color", string.Empty);
                                string colorList = GetCellString(worksheet, headerToColumn, row, "Thumbnail image highlight color", string.Empty);

                                string opacityZoomText = GetCellString(worksheet, headerToColumn, row, "Main highlight opacity", "1");
                                string opacityListText = GetCellString(worksheet, headerToColumn, row, "Thumbnail highlight opacity", "1");

                                // Report a warning if path contains a backslash
                                if (!string.IsNullOrEmpty(fileName) && fileName.Contains("\\"))
                                {
                                    string error = $"WARNING: Excel file [{board.DataFile}], worksheet [{sectionHeader}] row [{row}] the path [{fileName}] contains a backslash";
                                    Main.DebugOutput(error);
                                }

                                // Shadow "board"
                                if (!string.IsNullOrEmpty(name) &&
                                    !Main.shadow_structure[hardware.Name][board.Name].Contains(name))
                                {
                                    Main.shadow_structure[hardware.Name][board.Name].Add(name);
                                }

                                // Get configuration setting
                                string boardConfigKey = $"ConfigurationCheckBoxState|{hardware.Name}|{board.Name}|{name}";
                                bool boardCheckedInConfig = Configuration.GetSetting(boardConfigKey, "True") == "True";

                                // Only add schematic, if it is marked as active
                                if (boardCheckedInConfig && !string.IsNullOrEmpty(fileName))
                                {
                                    string filePath = Path.Combine(DataPaths.DataRoot, fileName);
                                    if (File.Exists(filePath))
                                    {
                                        // Convert from fraction (0–1) to 0–255
                                        double zoomFraction;
                                        if (!double.TryParse(opacityZoomText, out zoomFraction))
                                        {
                                            zoomFraction = 1.0;
                                        }

                                        double listFraction;
                                        if (!double.TryParse(opacityListText, out listFraction))
                                        {
                                            listFraction = 1.0;
                                        }

                                        int opacityZoom = (int)(zoomFraction * 255.0);
                                        int opacityList = (int)(listFraction * 255.0);

                                        BoardOverlays bo = new BoardOverlays
                                        {
                                            Name = name,
                                            SchematicFileName = fileName,
                                            HighlightColorTab = colorZoom,
                                            HighlightColorList = colorList,
                                            HighlightOpacityTab = opacityZoom,
                                            HighlightOpacityList = opacityList
                                        };
                                        if (board.Files == null)
                                        {
                                            board.Files = new List<BoardOverlays>();
                                        }

                                        board.Files.Add(bo);
                                    }
                                    else
                                    {
                                        string fileName1 = Path.GetFileName(filePathBoardData);
                                        string fileName2 = Path.GetFileName(filePath);
                                        string error = $"ERROR: Excel file [{fileName1}] and worksheet [{sheet}] the file [{fileName2}] does not exists";
                                        Main.DebugOutput(error);
                                    }
                                }
                                else if (!boardCheckedInConfig && !string.IsNullOrEmpty(name))
                                {
                                    string info = $"INFO: Excel file [{board.DataFile}] and schematic [{name}] is disabled";
                                    Main.DebugOutput(info);
                                }

                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePathBoardData);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // Check if any "Board.Files" are defined across all hardware and boards
            bool hasFiles = false;
            foreach (var hardware in classHardware)
            {
                foreach (var board in hardware.Boards)
                {
                    if (board.Files != null && board.Files.Count > 0)
                    {
                        hasFiles = true;
                        break;
                    }
                }
                if (hasFiles) break;
            }
            if (!hasFiles)
            {
                string error = "No schematic images defined or invalid data format in any board.\r\n\r\nView troubleshoot log for details [Commodore-Repair-Toolbox.log].";
                MessageBox.Show(error, "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            // ---

            // 4) Load main "Components" sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePath = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        string sheet = "Components";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "Components";
                            int row = 1;

                            // Find the row that contains the section header ("Components") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }
                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePath);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Data starts after the header row.
                            row = headerRow + 1;

                            // Use first column as the "end of data" sentinel, as before
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // These header names must match the text in the Excel header row
                                string label = GetCellString(worksheet, headerToColumn, row, "Board label", "");
                                string nameTechnical = GetCellString(worksheet, headerToColumn, row, "Technical name or value", "");
                                string nameFriendly = GetCellString(worksheet, headerToColumn, row, "Friendly name", "");
                                string partnumber = GetCellString(worksheet, headerToColumn, row, "Part-number", "");
                                string type = GetCellString(worksheet, headerToColumn, row, "Category", "Misc");
                                string region = GetCellString(worksheet, headerToColumn, row, "Region", "");
                                string oneliner = GetCellString(worksheet, headerToColumn, row, "Short one-liner description\n(one short line only!)", "");

                                string nameDisplay = label;
                                nameDisplay += nameTechnical != "" ? " | " + nameTechnical : "";
                                nameDisplay += nameFriendly != "" ? " | " + nameFriendly : "";

                                BoardComponents comp = new BoardComponents
                                {
                                    Label = label,
                                    NameTechnical = nameTechnical,
                                    NameFriendly = nameFriendly,
                                    Partnumber = partnumber,
                                    NameDisplay = nameDisplay,
                                    Type = type,
                                    Region = region,
                                    OneLiner = oneliner
                                };

                                if (board.Components == null)
                                {
                                    board.Components = new List<BoardComponents>();
                                }

                                board.Components.Add(comp);
                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePath);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // ---

            // 5) Create empty "ComponentBounds" for each BoardFile
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string sheet = "Components";

                    // Get configuration setting
                    string boardConfigKey = $"ConfigurationCheckBoxState|{hardware.Name}|{board.Name}";
                    bool boardCheckedInConfig = Configuration.GetSetting(boardConfigKey, "True") == "True";

                    // Continue, if the hardware and board is checked/active
                    if (boardCheckedInConfig)
                    {
                        if (board.Files != null)
                        {
                            foreach (BoardOverlays bo in board.Files)
                            {
                                string filePath = Path.Combine(DataPaths.DataRoot, board.DataFile);
                                using (var package = new ExcelPackage(new FileInfo(filePath)))
                                {
                                    var worksheet = package.Workbook.Worksheets[sheet];

                                    // Break check
                                    if (worksheet != null)
                                    {
                                        string searchHeader = "Components";
                                        int row = 1;

                                        // Find the row that contains the section header ("Components") in column 1
                                        while (row <= worksheet.Dimension.End.Row)
                                        {
                                            if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                            {
                                                break;
                                            }

                                            row++;
                                        }

                                        // If we never found the header, log and continue to next overlay/board
                                        if (row > worksheet.Dimension.End.Row)
                                        {
                                            string fileName = Path.GetFileName(filePath);
                                            string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                            Main.DebugOutput(error);
                                            continue;
                                        }

                                        // Next row is the "column headers" row
                                        row++;
                                        int headerRow = row;

                                        // Build a header name -> column index map
                                        var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                                        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                                        {
                                            string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                            if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                            {
                                                headerToColumn[headerValue] = col;
                                            }
                                        }

                                        // Data starts after the header row
                                        row = headerRow + 1;

                                        // Create ComponentBounds entries for each component label (order-agnostic)
                                        while (worksheet.Cells[row, 1].Value != null)
                                        {
                                            string label = GetCellString(worksheet, headerToColumn, row, "Board label", string.Empty);

                                            if (!string.IsNullOrEmpty(label))
                                            {
                                                ComponentBounds cb = new ComponentBounds
                                                {
                                                    Label = label
                                                };

                                                if (bo.Components == null)
                                                {
                                                    bo.Components = new List<ComponentBounds>();
                                                }

                                                bo.Components.Add(cb);
                                            }

                                            row++;
                                        }
                                    }
                                    else
                                    {
                                        string fileName = Path.GetFileName(filePath);
                                        string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                                        Main.DebugOutput(error);
                                    }
                                }
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(Path.Combine(DataPaths.DataRoot, board.DataFile));
                            string error = $"ERROR: Excel file [{fileName}] does not have any schematic images, so cannot create highlight bounds";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // ---

            // 6) Load "component local files" (datasheets) sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePathBoardData)))
                    {
                        string sheet = "Component local files";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "Component local files";
                            int row = 1;

                            // Find the row that contains the section header ("Component local files") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePathBoardData);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Data starts after the header row
                            row = headerRow + 1;

                            // Read rows until column 1 becomes empty (same sentinel pattern as elsewhere)
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // These header names must match the text in the Excel header row
                                string componentName = GetCellString(worksheet, headerToColumn, row, "Board label", string.Empty);
                                string name = GetCellString(worksheet, headerToColumn, row, "Name", string.Empty);
                                string fileName = GetCellString(worksheet, headerToColumn, row, "File", string.Empty);

                                // Report a warning if path contains a backslash
                                if (fileName.Contains("\\"))
                                {
                                    string error = $"WARNING: Excel file [{board.DataFile}], worksheet [{searchHeader}] row [{row}] the path [{fileName}] contains a backslash";
                                    Main.DebugOutput(error);
                                }

                                // Log if file does not exists
                                string filePath = Path.Combine(DataPaths.DataRoot, fileName);
                                if (!string.IsNullOrEmpty(fileName) && !File.Exists(filePath))
                                {
                                    string fileName1 = Path.GetFileName(filePathBoardData);
                                    string error = $"ERROR: Excel file [{fileName1}] worksheet [{sheet}] file [{fileName}] does not exists";
                                    Main.DebugOutput(error);
                                }
                                else if (!string.IsNullOrEmpty(componentName))
                                {
                                    // Add file to class
                                    var classComponent = board?.Components?.FirstOrDefault(c => c.Label == componentName);
                                    if (classComponent != null)
                                    {
                                        if (classComponent.LocalFiles == null)
                                        {
                                            classComponent.LocalFiles = new List<ComponentLocalFiles>();
                                        }

                                        classComponent.LocalFiles.Add(new ComponentLocalFiles
                                        {
                                            Name = name,
                                            FileName = fileName
                                        });
                                    }
                                }

                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePathBoardData);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // ---

            // 7) Load "component links" sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePath = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        string sheet = "Component links";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "Component links";
                            int row = 1;

                            // Find the row that contains the section header ("Component links") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePath);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Data starts after the header row
                            row = headerRow + 1;

                            // Read rows until column 1 becomes empty (same sentinel pattern as elsewhere)
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // Header names must match the column headers in the Excel sheet
                                string componentName = GetCellString(worksheet, headerToColumn, row, "Board label", string.Empty);
                                string linkName = GetCellString(worksheet, headerToColumn, row, "Name", string.Empty);
                                string linkUrl = GetCellString(worksheet, headerToColumn, row, "URL", string.Empty);

                                var comp = board?.Components?.FirstOrDefault(c => c.Label == componentName);
                                if (comp != null && (!string.IsNullOrEmpty(linkName) || !string.IsNullOrEmpty(linkUrl)))
                                {
                                    if (comp.ComponentLinks == null)
                                    {
                                        comp.ComponentLinks = new List<ComponentLinks>();
                                    }

                                    comp.ComponentLinks.Add(new ComponentLinks
                                    {
                                        Name = linkName,
                                        Url = linkUrl
                                    });
                                }
                                else if (comp == null && !string.IsNullOrEmpty(componentName))
                                {
                                    string fileName = Path.GetFileName(filePath);
                                    string error = $"ERROR: Excel file [{fileName}] in worksheet [{sheet}] the component [{componentName}] does not exists";
                                    Main.DebugOutput(error);
                                }

                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePath);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // ---

            // 8) Load "component highlights" (overlay rectangles) sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePathBoardData)))
                    {
                        string sheet = "Component highlights";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "Component highlights";
                            int row = 1;

                            // Find the row that contains the section header ("Component highlights") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePathBoardData);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Data starts after the header row
                            row = headerRow + 1;

                            // Read rows until column 1 becomes empty (same sentinel pattern as elsewhere)
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // Header names must match the column headers in the Excel sheet
                                string imageName = GetCellString(worksheet, headerToColumn, row, "Schematic name", string.Empty);
                                string componentName = GetCellString(worksheet, headerToColumn, row, "Board label", string.Empty);

                                int x = 0;
                                int y = 0;
                                int w = 0;
                                int h = 0;

                                // X coordinate
                                string xText = GetCellString(worksheet, headerToColumn, row, "X", string.Empty);
                                if (!string.IsNullOrEmpty(xText))
                                {
                                    double xVal;
                                    if (double.TryParse(xText, out xVal))
                                    {
                                        x = (int)xVal;
                                    }
                                }

                                // Y coordinate
                                string yText = GetCellString(worksheet, headerToColumn, row, "Y", string.Empty);
                                if (!string.IsNullOrEmpty(yText))
                                {
                                    double yVal;
                                    if (double.TryParse(yText, out yVal))
                                    {
                                        y = (int)yVal;
                                    }
                                }

                                // Width
                                string wText = GetCellString(worksheet, headerToColumn, row, "Width", string.Empty);
                                if (!string.IsNullOrEmpty(wText))
                                {
                                    double wVal;
                                    if (double.TryParse(wText, out wVal))
                                    {
                                        w = (int)wVal;
                                    }
                                }

                                // Height
                                string hText = GetCellString(worksheet, headerToColumn, row, "Height", string.Empty);
                                if (!string.IsNullOrEmpty(hText))
                                {
                                    double hVal;
                                    if (double.TryParse(hText, out hVal))
                                    {
                                        h = (int)hVal;
                                    }
                                }

                                var bf = board.Files?.FirstOrDefault(f => f.Name == imageName);

                                // Get configuration setting for this schematic
                                string boardConfigKey = $"ConfigurationCheckBoxState|{hardware.Name}|{board.Name}|{imageName}";
                                bool boardCheckedInConfig = Configuration.GetSetting(boardConfigKey, "True") == "True";

                                // Continue, if the hardware and board is checked/active
                                if (boardCheckedInConfig)
                                {
                                    if (bf == null)
                                    {
                                        string fileName = Path.GetFileName(filePathBoardData);
                                        Main.DebugOutput($"ERROR: Excel file [{fileName}] worksheet [{sheet}] schematic name [{imageName}] does not exists for component [{componentName}]");
                                    }
                                    else
                                    {
                                        if (bf.Components == null)
                                        {
                                            string fileName = Path.GetFileName(filePathBoardData);
                                            Main.DebugOutput($"ERROR: Excel file [{fileName}] no components can be found in worksheet [Components]");
                                            break;
                                        }

                                        var compBounds = bf.Components.FirstOrDefault(c => c.Label == componentName);
                                        if (compBounds != null)
                                        {
                                            if (compBounds.Overlays == null)
                                            {
                                                compBounds.Overlays = new List<Overlay>();
                                            }

                                            compBounds.Overlays.Add(new Overlay
                                            {
                                                Bounds = new Rectangle(x, y, w, h)
                                            });
                                        }
                                    }
                                }

                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePathBoardData);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // ---

            // 9) Load "board links" sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePath = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        string sheet = "Board links";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "Board links";
                            int row = 1;

                            // Find the row that contains the section header ("Board links") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePath);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Initialize the BoardLinks list if it's null
                            if (board.BoardLinks == null)
                            {
                                board.BoardLinks = new List<BoardLinks>();
                            }

                            // Data starts after the header row
                            row = headerRow + 1;

                            // Read rows until column 1 becomes empty (same sentinel pattern as elsewhere)
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // Header names must match the column headers in the Excel sheet
                                string category = GetCellString(worksheet, headerToColumn, row, "Category", string.Empty);
                                string linkName = GetCellString(worksheet, headerToColumn, row, "Name", string.Empty);
                                string linkUrl = GetCellString(worksheet, headerToColumn, row, "URL", string.Empty);

                                if (!string.IsNullOrEmpty(category) ||
                                    !string.IsNullOrEmpty(linkName) ||
                                    !string.IsNullOrEmpty(linkUrl))
                                {
                                    BoardLinks boardLink = new BoardLinks
                                    {
                                        Category = category,
                                        Name = linkName,
                                        Url = linkUrl
                                    };

                                    board.BoardLinks.Add(boardLink);
                                }

                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePath);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // ---

            // 10) Load "Board local files" sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePathBoardData)))
                    {
                        string sheet = "Board local files";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "Board local files";
                            int row = 1;

                            // Find the row that contains the section header ("Board local files") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePathBoardData);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Initialize the BoardLocalFiles list if it's null
                            if (board.BoardLocalFiles == null)
                            {
                                board.BoardLocalFiles = new List<BoardLocalFiles>();
                            }

                            // Data starts after the header row
                            row = headerRow + 1;

                            // Read rows until column 1 becomes empty (same sentinel pattern as elsewhere)
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // Header names must match the column headers in the Excel sheet
                                string category = GetCellString(worksheet, headerToColumn, row, "Category", string.Empty);
                                string name = GetCellString(worksheet, headerToColumn, row, "Name", string.Empty);
                                string fileName = GetCellString(worksheet, headerToColumn, row, "File", string.Empty);

                                // Report a warning if path contains a backslash
                                if (fileName.Contains("\\"))
                                {
                                    string error = $"WARNING: Excel file [{board.DataFile}], worksheet [{searchHeader}] row [{row}] the path [{fileName}] contains a backslash";
                                    Main.DebugOutput(error);
                                }

                                // Log if file does not exists
                                string filePath = Path.Combine(DataPaths.DataRoot, fileName);
                                if (!string.IsNullOrEmpty(fileName) && !File.Exists(filePath))
                                {
                                    string fileName1 = Path.GetFileName(filePathBoardData);
                                    string error = $"ERROR: Excel file [{fileName1}] worksheet [{sheet}] file [{fileName}] does not exists";
                                    Main.DebugOutput(error);
                                }
                                else if (!string.IsNullOrEmpty(fileName))
                                {
                                    // Add file to class
                                    BoardLocalFiles boardLocalFile = new BoardLocalFiles
                                    {
                                        Category = category,
                                        Name = name,
                                        Datafile = fileName
                                    };
                                    board.BoardLocalFiles.Add(boardLocalFile);
                                }

                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePathBoardData);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // ---

            // 11) Load "component images" (datasheets) sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePathBoardData)))
                    {
                        string sheet = "Component images";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "Component images";
                            int row = 1;

                            // Find the row that contains the section header ("Component images") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePathBoardData);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Data starts after the header row
                            row = headerRow + 1;

                            // Read rows until column 1 becomes empty (same sentinel pattern as elsewhere)
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // Header names must match the column headers in the Excel sheet
                                string componentName = GetCellString(worksheet, headerToColumn, row, "Board label", string.Empty);
                                string region = GetCellString(worksheet, headerToColumn, row, "Region", string.Empty);
                                string pin = GetCellString(worksheet, headerToColumn, row, "Pin", string.Empty);
                                string name = GetCellString(worksheet, headerToColumn, row, "Name", string.Empty);
                                string reading = GetCellString(worksheet, headerToColumn, row, "Expected oscilloscope reading", string.Empty);
                                string fileName = GetCellString(worksheet, headerToColumn, row, "File", string.Empty);
                                string note = GetCellString(worksheet, headerToColumn, row, "Note", string.Empty);

                                // Report a warning if path contains a backslash
                                if (fileName.Contains("\\"))
                                {
                                    string error = $"WARNING: Excel file [{board.DataFile}], worksheet [{searchHeader}] row [{row}] the path [{fileName}] contains a backslash";
                                    Main.DebugOutput(error);
                                }

                                // Log if file does not exists (but only if a name is given)
                                string filePath = Path.Combine(DataPaths.DataRoot, fileName);
                                if (!string.IsNullOrEmpty(fileName) && !File.Exists(filePath))
                                {
                                    string fileName1 = Path.GetFileName(filePathBoardData);
                                    string error = $"ERROR: Excel file [{fileName1}] worksheet [{sheet}] the file [{fileName}] does not exists";
                                    Main.DebugOutput(error);
                                }
                                else
                                {
                                    // Add file to class
                                    var classComponent = board?.Components?.FirstOrDefault(c => c.Label == componentName);
                                    if (classComponent != null)
                                    {
                                        if (classComponent.ComponentImages == null)
                                        {
                                            classComponent.ComponentImages = new List<ComponentImages>();
                                        }

                                        classComponent.ComponentImages.Add(new ComponentImages
                                        {
                                            Region = region,
                                            Pin = pin,
                                            Name = name,
                                            Reading = reading,
                                            FileName = fileName,
                                            Note = note
                                        });
                                    }
                                }

                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePathBoardData);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            } // 11) Load "component images" (datasheets)

            // ---

            // 12) Load "Credits" sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePath = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        string sheet = "Credits";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "Board credits";
                            int row = 1;

                            // Find the row that contains the section header ("Board credits") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePath);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Initialize the BoardCredits list if it's null
                            if (board.BoardCredits == null)
                            {
                                board.BoardCredits = new List<BoardCredits>();
                            }

                            // Data starts after the header row
                            row = headerRow + 1;

                            // Read rows until column 1 becomes empty
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // Header names must match the column headers in the Excel sheet
                                string category = GetCellString(worksheet, headerToColumn, row, "Category", string.Empty);
                                string subCategory = GetCellString(worksheet, headerToColumn, row, "Sub-category", string.Empty);
                                string name = GetCellString(worksheet, headerToColumn, row, "Name or handle", string.Empty);
                                string contact = GetCellString(worksheet, headerToColumn, row, "Contact (email or web page)", string.Empty);

                                // Only add non-empty rows
                                if (!string.IsNullOrEmpty(category) ||
                                    !string.IsNullOrEmpty(subCategory) ||
                                    !string.IsNullOrEmpty(name) ||
                                    !string.IsNullOrEmpty(contact))
                                {
                                    BoardCredits boardCredits = new BoardCredits
                                    {
                                        Category = category,
                                        SubCategory = subCategory,
                                        Name = name,
                                        Contact = contact
                                    };
                                    board.BoardCredits.Add(boardCredits);
                                }

                                row++;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePath);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            } // 12) Load "Credits"

            // ---

            // 13) Load "Version match" sheet for all boards
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePath = Path.Combine(DataPaths.DataRoot, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        string sheet = "Version match";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {
                            string searchHeader = "CRT version(s) where this Excel will work";
                            int row = 1;

                            // Find the row that contains the section header ("Board credits") in column 1
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader)
                                {
                                    break;
                                }

                                row++;
                            }

                            // If we never found the header, log and continue to next board
                            if (row > worksheet.Dimension.End.Row)
                            {
                                string fileName = Path.GetFileName(filePath);
                                string error = $"ERROR: Excel file [{fileName}] the header row for worksheet [{sheet}] with label [{searchHeader}] was not found";
                                Main.DebugOutput(error);
                                continue;
                            }

                            // Next row is the "column headers" row
                            row++;
                            int headerRow = row;

                            // Build a header name -> column index map
                            var headerToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                            {
                                string headerValue = worksheet.Cells[headerRow, col].Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(headerValue) && !headerToColumn.ContainsKey(headerValue))
                                {
                                    headerToColumn[headerValue] = col;
                                }
                            }

                            // Initialize the BoardCredits list if it's null
                            if (board.BoardCredits == null)
                            {
                                board.BoardCredits = new List<BoardCredits>();
                            }

                            // Data starts after the header row
                            row = headerRow + 1;

                            // Read rows until column 1 becomes empty
                            string[] versionsFromExcel = { };
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                // Header names must match the column headers in the Excel sheet
                                string version = GetCellString(worksheet, headerToColumn, row, "Version", string.Empty);

                                // Add "version" to "versionsFromExcel" array
                                Array.Resize(ref versionsFromExcel, versionsFromExcel.Length + 1);
                                versionsFromExcel[versionsFromExcel.Length - 1] = version;

                                row++;
                            }

                            // Check if at least one of the versions in "versionsFromExcel" matches "Main.versionThis", based on length of string for "versionsFromExcel"
                            bool versionMatchFound = false;
                            foreach (string version in versionsFromExcel)
                            {
                                if (Main.versionThis.StartsWith(version, StringComparison.Ordinal))
                                {
                                    versionMatchFound = true;
                                    break;
                                }
                            }
                            if (!versionMatchFound)
                            {
                                Array.Resize(ref Main.versionMismatch, Main.versionMismatch.Length + 1);
                                Main.versionMismatch[Main.versionMismatch.Length - 1] = filePath;
                            }
                        }
                        else
                        {
                            string fileName = Path.GetFileName(filePath);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            } // 13) Load "Version match"

            // ---

            // Check if classHardware has schematic files for all boards - if not, remove the board
            var hardwareToRemove = new List<Hardware>();

            foreach (var hardware in classHardware)
            {
                var boardsToRemove = new List<Board>();

                foreach (var board in hardware.Boards)
                {
                    // Get configuration setting
                    string boardConfigKey = $"ConfigurationCheckBoxState|{hardware.Name}|{board.Name}";
                    bool boardCheckedInConfig = Configuration.GetSetting(boardConfigKey, "True") == "True";

                    if (board.Files == null || board.Files.Count == 0)
                    {
                        // Only log an error, if the board is checked/active in the configuration
                        if (boardCheckedInConfig)
                        {
                            string error = $"ERROR: Hardware [{hardware.Name}] board [{board.Name}] does not have any schematic image files - removing board";
                            Main.DebugOutput(error);
                        }
                        boardsToRemove.Add(board);
                    }
                }

                // Remove boards that lack schematic files
                foreach (var board in boardsToRemove)
                {
                    hardware.Boards.Remove(board);
                }

                // If the hardware has no boards left, mark it for removal
                if (hardware.Boards.Count == 0)
                {
                    string error = $"ERROR: Hardware [{hardware.Name}] does not have any boards associated - removing hardware";
                    Main.DebugOutput(error);
                    hardwareToRemove.Add(hardware);
                }
            }

            // Remove hardware that has no boards left
            foreach (var hardware in hardwareToRemove)
            {
                classHardware.Remove(hardware);
            }

        }

        // Get a string value by header name
        private static string GetCellString(ExcelWorksheet ws, Dictionary<string, int> headerToColumn, int r, string headerName, string defaultValue)
            {
                int col;
                if (!headerToColumn.TryGetValue(headerName, out col))
                {
                    return defaultValue;
                }
                return ws.Cells[r, col].Value?.ToString() ?? defaultValue;
            }
        }

    }