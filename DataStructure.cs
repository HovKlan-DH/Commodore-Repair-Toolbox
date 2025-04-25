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

            string filePathMain = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.xlsx");

            // Exit check
            if (!File.Exists(filePathMain))
            {
                string error = $"File [{filePathMain}] does not exists";
                MessageBox.Show(error, "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            // 1) Load hardware from initial data file (Commodore-Repair-Toolbox.xlsx)
            using (var package = new ExcelPackage(new FileInfo(filePathMain)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                string searchHeader = "Hardware name in drop-down";
                int row = 1;
                while (row <= worksheet.Dimension.End.Row)
                {
                    // Check "row" and column 2
                    if (worksheet.Cells[row, 2].Value?.ToString() == searchHeader) break;
                    row++;
                }
                row++; // skip headers

                while (worksheet.Cells[row, 1].Value != null)
                {
                    string active = worksheet.Cells[row, 1].Value?.ToString() ?? "0";

                    // Only add hardware, if it is marked as active
                    if (active == "1")
                    {
                        string nameHardware = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                        string nameBoard = worksheet.Cells[row, 3].Value?.ToString() ?? "";
                        string datafile = worksheet.Cells[row, 4].Value?.ToString() ?? "";

                        // Check if the board datafile exists
                        string filePath = Path.Combine(Application.StartupPath, datafile);
                        if (File.Exists(filePath))
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
                            string error = $"ERROR: Excel file [Commodore-Repair-Toolbox.xlsx] row [{row}] the file [" + filePath + "] does not exists";
                            Main.DebugOutput(error);
                        }
                    } else
                    {
                        string nameHardware = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                        string nameBoard = worksheet.Cells[row, 3].Value?.ToString() ?? "";
                        string error = $"INFO: Excel file [Commodore-Repair-Toolbox.xlsx] row [{row}] the hardware [{nameHardware}] and board [{nameBoard}] set to [not active]";
                        Main.DebugOutput(error);
                    }
                    row++;
                }
            }

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

            // 2) Load "Board" entries (schematics/images)
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(
                        filePathBoardData)))
                    {
                        string sheet = "Board schematics";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        if (worksheet != null)
                        {
                            string revisionDate = "(unknown)";
                            string searchRevisionDate = "# Revision date:";
                            board.RevisionDate = revisionDate; // make sure we set some value to the board

                            string searchHeader = "Schematic name";

                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                // Get the "revision date", if it is present
                                if (worksheet.Cells[row, 1].Value?.ToString().StartsWith(searchRevisionDate) == true)
                                {
                                    string fullValue = worksheet.Cells[row, 1].Value.ToString();
                                    revisionDate = fullValue.Substring(searchRevisionDate.Length).Trim(); // Extract and trim the date
                                    board.RevisionDate = revisionDate;
                                }

                                // Check "row" and column 2 for the "searchHeader" (will be below the above IF-sentense)
                                if (worksheet.Cells[row, 2].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string active = worksheet.Cells[row, 1].Value?.ToString() ?? "0";
                                string name = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                string fileName = worksheet.Cells[row, 3].Value?.ToString() ?? "";
                                string colorZoom = worksheet.Cells[row, 4].Value?.ToString() ?? "";
                                string colorList = worksheet.Cells[row, 5].Value?.ToString() ?? "";

                                // Only add schematic, if it is marked as active
                                if (active == "1")
                                {
                                    string filePath = Path.Combine(Application.StartupPath, fileName);
                                    if (File.Exists(filePath))
                                    {
                                        // Convert from fraction to 0-255
                                        string cellValue = worksheet.Cells[row, 6].Value?.ToString() ?? "";
                                        int opacityZoom = (int)(double.Parse(cellValue) * 100);
                                        opacityZoom = (int)((opacityZoom / 100.0) * 255);

                                        cellValue = worksheet.Cells[row, 7].Value?.ToString() ?? "";
                                        int opacityList = (int)(double.Parse(cellValue) * 100);
                                        opacityList = (int)((opacityList / 100.0) * 255);

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

            // 4) Load main "Components" for each board
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePath = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        string sheet = "Components";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {

                            string searchHeader = "Components";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers
                            row++; // (need to investigate this - why is this extra row needed!?)

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string label = worksheet.Cells[row, 1].Value?.ToString() ?? "";
                                string nameTechnical = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                string nameFriendly = worksheet.Cells[row, 3].Value?.ToString() ?? "";
                                string type = worksheet.Cells[row, 4].Value?.ToString() ?? "Misc";
                                string oneliner = worksheet.Cells[row, 5].Value?.ToString() ?? "";
                                string description = worksheet.Cells[row, 6].Text ?? "";
                                description = description.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

                                string nameDisplay = label;
                                nameDisplay += nameTechnical != "" ? " | " + nameTechnical : "";
                                nameDisplay += nameFriendly != "" ? " | " + nameFriendly : "";

                                BoardComponents comp = new BoardComponents
                                {
                                    Label = label,
                                    NameTechnical = nameTechnical,
                                    NameFriendly = nameFriendly,
                                    NameDisplay = nameDisplay,
                                    Type = type,
                                    OneLiner = oneliner,
                                    Description = description
                                };
                                if (board.Components == null) board.Components = new List<BoardComponents>();
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

            // 5) Create empty "ComponentBounds" for each BoardFile
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string sheet = "Components";

                    if (board.Files != null)
                    {
                        foreach (BoardOverlays bo in board.Files)
                        {
        
                            string filePath = Path.Combine(Application.StartupPath, board.DataFile);
                            using (var package = new ExcelPackage(new FileInfo(filePath)))
                            {
                                var worksheet = package.Workbook.Worksheets[sheet];

                                // Break check
                                if (worksheet != null)
                                {
                                    string searchHeader = "Components";
                                    int row = 1;
                                    while (row <= worksheet.Dimension.End.Row)
                                    {
                                        if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                        row++;
                                    }
                                    row++; // skip headers
                                    row++; // (need to investigate this - why is this extra row needed!?)

                                    while (worksheet.Cells[row, 1].Value != null)
                                    {
                                        string name = worksheet.Cells[row, 1].Value.ToString();
                                        ComponentBounds cb = new ComponentBounds
                                        {
                                            Label = name
                                        };
                                        if (bo.Components == null) bo.Components = new List<ComponentBounds>();
                                        bo.Components.Add(cb);
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
                        string fileName = Path.GetFileName(Path.Combine(Application.StartupPath, board.DataFile));
                        string error = $"ERROR: Excel file [{fileName}] does not have any schematic images, so cannot create highlight bounds";
                        Main.DebugOutput(error);
                    }
                }
            }

            // 6) Load "component oscilliscope"
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(
                        filePathBoardData)))
                    {
                        string sheet = "Component oscilloscope";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {

                            string searchHeader = "Component behaviour";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers
                            row++;

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string componentName = worksheet.Cells[row, 1].Value?.ToString() ?? "";
                                string region = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                string pin = worksheet.Cells[row, 3].Value?.ToString() ?? "";
                                string name = worksheet.Cells[row, 4].Value?.ToString() ?? "";
                                string reading = worksheet.Cells[row, 5].Value?.ToString() ?? "";

                                // Add it, if we have some data
                                if (pin != "" && reading != "")
                                {
                                    var classComponent = board?.Components?.FirstOrDefault(c => c.Label == componentName);
                                    if (classComponent != null)
                                    {
                                        if (classComponent.Oscilloscope == null) classComponent.Oscilloscope = new List<ComponentOscilloscope>();
                                        classComponent.Oscilloscope.Add(new ComponentOscilloscope
                                        {
                                            Name = name,
                                            Region = region,
                                            Pin = pin,
                                            Reading = reading
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
            
            // 6) Load "component local files" (datasheets)
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(
                        filePathBoardData)))
                    {
                        string sheet = "Component local files";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {

                            string searchHeader = "Component local files";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers
                            row++;

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string componentName = worksheet.Cells[row, 1].Value?.ToString() ?? "";
                                string name = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                string fileName = worksheet.Cells[row, 3].Value?.ToString() ?? "";

                                // Log if file does not exists
                                string filePath = Path.Combine(Application.StartupPath, fileName);
                                if (!File.Exists(filePath))
                                {
                                    string fileName1 = Path.GetFileName(filePathBoardData);
                                    string error = $"ERROR: Excel file [{fileName1}] worksheet [{sheet}] file [{fileName}] does not exists";
                                    Main.DebugOutput(error);
                                }
                                else
                                {
                                    // Add file to class
                                    var classComponent = board?.Components?.FirstOrDefault(c => c.Label == componentName);
                                    if (classComponent != null)
                                    {
                                        if (classComponent.LocalFiles == null) classComponent.LocalFiles = new List<ComponentLocalFiles>();
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

            // 7) Load "component links"
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePath = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(
                        filePath)))
                    {
                        string sheet = "Component links";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {

                            string searchHeader = "Component links";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers
                            row++;

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string componentName = worksheet.Cells[row, 1].Value?.ToString() ?? "";
                                string linkName = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                string linkUrl = worksheet.Cells[row, 3].Value?.ToString() ?? "";

                                var comp = board?.Components?.FirstOrDefault(c => c.Label == componentName);
                                if (comp != null)
                                {
                                    if (comp.ComponentLinks == null) comp.ComponentLinks = new List<ComponentLinks>();
                                    comp.ComponentLinks.Add(new ComponentLinks
                                    {
                                        Name = linkName,
                                        Url = linkUrl
                                    });
                                } else
                                {
                                    string fileName = Path.GetFileName(filePath);
                                    string error = $"ERROR: Excel file [{fileName}] in worksheet [{sheet}] the component [{componentName}] does not exists";
                                    Main.DebugOutput(error);
                                }
                                    row++;
                            }
                        } else
                        {
                            string fileName = Path.GetFileName(filePath);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // 8) Load "component highlights" (overlay rectangles)
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(
                        filePathBoardData)))
                    {
                        string sheet = "Component highlights";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {

                            string searchHeader = "Component highlights";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers
                            row++; // (need to investigate this - why is this extra row needed!?)

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string imageName = worksheet.Cells[row, 1].Value?.ToString() ?? "";
                                string componentName = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                int x = worksheet.Cells[row, 3].Value != null ? (int)(double)worksheet.Cells[row, 3].Value : 0;
                                int y = worksheet.Cells[row, 4].Value != null ? (int)(double)worksheet.Cells[row, 4].Value : 0;
                                int w = worksheet.Cells[row, 5].Value != null ? (int)(double)worksheet.Cells[row, 5].Value : 0;
                                int h = worksheet.Cells[row, 6].Value != null ? (int)(double)worksheet.Cells[row, 6].Value : 0;

                                var bf = board.Files?.FirstOrDefault(f => f.Name == imageName);

                                // Break check
                                if (bf == null)
                                {
                                    string fileName = Path.GetFileName(filePathBoardData);
                                    Main.DebugOutput($"ERROR: Excel file [{fileName}] worksheet [{sheet}] schematic name [{imageName}] does not exists for component [{componentName}]");
                                }
                                else
                                {
                                    // Break check
                                    if (bf.Components == null)
                                    {
                                        string fileName = Path.GetFileName(filePathBoardData);
                                        Main.DebugOutput($"ERROR: Excel file [{fileName}] no components can be found in worksheet [Components]");
                                        break;
                                    }

                                    var compBounds = bf?.Components.FirstOrDefault(c => c.Label == componentName);
                                    if (compBounds != null)
                                    {
                                        if (compBounds.Overlays == null) compBounds.Overlays = new List<Overlay>();
                                        compBounds.Overlays.Add(new Overlay
                                        {
                                            Bounds = new Rectangle(x, y, w, h)
                                        });
                                    }
                                }
                                row++;
                            }
                        } else
                        {
                            string fileName = Path.GetFileName(filePathBoardData);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // 9) Load "board links"
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePath = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(
                        filePath)))
                    {
                        string sheet = "Board links";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {

                            string searchHeader = "Board links";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers
                            row++;

                            // Initialize the BoardLinks list if it's null
                            if (board.BoardLinks == null)
                            {
                                board.BoardLinks = new List<BoardLinks>();
                            }

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string category = worksheet.Cells[row, 1].Value?.ToString() ?? "";
                                string linkName = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                string linkUrl = worksheet.Cells[row, 3].Value?.ToString() ?? "";

                                // Create a new BoardLink instance and add it to the BoardLinks list
                                BoardLinks boardLink = new BoardLinks
                                {
                                    Category = category,
                                    Name = linkName,
                                    Url = linkUrl
                                };
                                board.BoardLinks.Add(boardLink);

                                row++;
                            }
                        } else
                        {
                            string fileName = Path.GetFileName(filePath);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // 10) Load "Board local files"
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(
                        filePathBoardData)))
                    {
                        string sheet = "Board local files";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {

                            string searchHeader = "Board local files";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers
                            row++;

                            // Initialize the BoardLinks list if it's null
                            if (board.BoardLocalFiles == null)
                            {
                                board.BoardLocalFiles = new List<BoardLocalFiles>();
                            }

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string category = worksheet.Cells[row, 1].Value?.ToString() ?? "";
                                string name = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                string fileName = worksheet.Cells[row, 3].Value?.ToString() ?? "";

                                // Log if file does not exists
                                //                            filePath = Path.Combine(Application.StartupPath, hardware.Folder, board.Folder, fileName);
                                string filePath = Path.Combine(Application.StartupPath, fileName);
                                if (!File.Exists(filePath))
                                {
                                    string fileName1 = Path.GetFileName(filePathBoardData);
                                    string error = $"ERROR: Excel file [{fileName1}] worksheet [{sheet}] file [{fileName}] does not exists";
                                    Main.DebugOutput(error);
                                }
                                else
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
                        } else
                        {
                            string fileName = Path.GetFileName(filePathBoardData);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            }

            // 11) Load "component images" (datasheets)
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    string filePathBoardData = Path.Combine(Application.StartupPath, board.DataFile);
                    using (var package = new ExcelPackage(new FileInfo(
                        filePathBoardData)))
                    {
                        string sheet = "Component images";
                        var worksheet = package.Workbook.Worksheets[sheet];

                        // Break check
                        if (worksheet != null)
                        {

                            string searchHeader = "Component images";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++; // skip headers
                            row++;

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string componentName = worksheet.Cells[row, 1].Value?.ToString() ?? "";
                                string region = worksheet.Cells[row, 2].Value?.ToString() ?? "";
                                string pin = worksheet.Cells[row, 3].Value?.ToString() ?? "";
                                string name = worksheet.Cells[row, 4].Value?.ToString() ?? "";
                                string fileName = worksheet.Cells[row, 5].Value?.ToString() ?? "";

                                // Log if file does not exists
                                string filePath = Path.Combine(Application.StartupPath, fileName);
                                if (!File.Exists(filePath))
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
                                        if (classComponent.ComponentImages == null) classComponent.ComponentImages = new List<ComponentImages>();
                                        classComponent.ComponentImages.Add(new ComponentImages
                                        {
                                            Region = region,
                                            Pin = pin,
                                            Name = name,
                                            FileName = fileName
                                        });
                                    }
                                }
                                row++;
                            }
                        } else
                        {
                            string fileName = Path.GetFileName(filePathBoardData);
                            string error = $"ERROR: Excel file [{fileName}] the worksheet [{sheet}] is not found";
                            Main.DebugOutput(error);
                        }
                    }
                }
            } // 11) Load "component images" (datasheets)

            // Check if classHardware has schematic files for all boards - if not, remove the board
            var hardwareToRemove = new List<Hardware>();

            foreach (var hardware in classHardware)
            {
                var boardsToRemove = new List<Board>();

                foreach (var board in hardware.Boards)
                {
                    if (board.Files == null || board.Files.Count == 0)
                    {
                        string error = $"ERROR: Hardware [{hardware.Name}] board [{board.Name}] does not have any schematic image files - removing board";
                        Main.DebugOutput(error);
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
    }
}