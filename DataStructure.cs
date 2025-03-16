using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // 1) Load hardware from Data.xlsx
            using (var package = new ExcelPackage(new FileInfo(
                Path.Combine(Application.StartupPath, "Data", "Data.xlsx"))))
            {
                var worksheet = package.Workbook.Worksheets[0];
                string searchHeader = "Hardware name in drop-down";
                int row = 1;
                while (row <= worksheet.Dimension.End.Row)
                {
                    if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                    row++;
                }
                row++; // skip header

                while (worksheet.Cells[row, 1].Value != null)
                {
                    string name = worksheet.Cells[row, 1].Value.ToString();
                    string folder = worksheet.Cells[row, 2].Value.ToString();
                    string datafile = worksheet.Cells[row, 3].Value.ToString();

                    Hardware hw = new Hardware
                    {
                        Name = name,
                        Folder = folder,
                        Datafile = datafile
                    };
                    classHardware.Add(hw);
                    row++;
                }
            }

            // 2) Load boards for each hardware
            foreach (Hardware hardware in classHardware)
            {
                using (var package = new ExcelPackage(new FileInfo(
                    Path.Combine(Application.StartupPath, "Data", hardware.Folder, hardware.Datafile))))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    string searchHeader = "Board name in drop-down";
                    int row = 1;
                    while (row <= worksheet.Dimension.End.Row)
                    {
                        if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                        row++;
                    }
                    row++; // skip header

                    while (worksheet.Cells[row, 1].Value != null)
                    {
                        string name = worksheet.Cells[row, 1].Value.ToString();
                        string folder = worksheet.Cells[row, 2].Value.ToString();
                        string datafile = worksheet.Cells[row, 3].Value.ToString();

                        Board boarda = new Board
                        {
                            Name = name,
                            Folder = folder,
                            Datafile = datafile
                        };
                        if (hardware.Boards == null) hardware.Boards = new List<Board>();
                        hardware.Boards.Add(boarda);
                        row++;
                    }
                }
            }

            // 3) Load "BoardFile" entries (images)
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(
                        Path.Combine(Application.StartupPath, "Data", hardware.Folder, board.Folder, board.Datafile))))
                    {
                        var worksheet = package.Workbook.Worksheets["Board images"];
                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet [Board images] not found in ["+ hardware.Folder +"\\"+ board.Folder +"\\"+ board.Datafile +"]");
                        }
                        string searchHeader = "Images in list";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                            row++;
                        }
                        row++;
                        row++;

                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string name = worksheet.Cells[row, 1].Value.ToString();
                            string fileName = worksheet.Cells[row, 2].Value.ToString();
                            string colorZoom = worksheet.Cells[row, 3].Value.ToString();
                            string colorList = worksheet.Cells[row, 4].Value.ToString();

                            // Convert from fraction to 0-255
                            string cellValue = worksheet.Cells[row, 5].Value.ToString();
                            int opacityZoom = (int)(double.Parse(cellValue) * 100);
                            opacityZoom = (int)((opacityZoom / 100.0) * 255);

                            cellValue = worksheet.Cells[row, 6].Value.ToString();
                            int opacityList = (int)(double.Parse(cellValue) * 100);
                            opacityList = (int)((opacityList / 100.0) * 255);

                            BoardFileOverlays bf = new BoardFileOverlays
                            {
                                Name = name,
                                FileName = fileName,
                                HighlightColorTab = colorZoom,
                                HighlightColorList = colorList,
                                HighlightOpacityTab = opacityZoom,
                                HighlightOpacityList = opacityList
                            };
                            if (board.Files == null) board.Files = new List<BoardFileOverlays>();
                            board.Files.Add(bf);
                            row++;
                        }
                    }
                }
            }

            // 4) Load main "Components" for each board
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(
                        Path.Combine(Application.StartupPath, "Data", hardware.Folder, board.Folder, board.Datafile))))
                    {
                        var worksheet = package.Workbook.Worksheets["Components"];
                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet [Components] not found in [" + hardware.Folder + "\\" + board.Folder + "\\" + board.Datafile + "]");
                        }
                        string searchHeader = "Components";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                            row++;
                        }
                        row++;
                        row++;

                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string label = worksheet.Cells[row, 1].Value.ToString();
                            string nameTechnical = worksheet.Cells[row, 2].Value?.ToString() ?? "?";
                            string nameFriendly = worksheet.Cells[row, 3].Value?.ToString() ?? "?";
                            string type = worksheet.Cells[row, 4].Value?.ToString() ?? "Misc";
                            string filePinout = worksheet.Cells[row, 5].Value?.ToString() ?? "";
                            string oneliner = worksheet.Cells[row, 6].Value?.ToString() ?? "";
                            string description = worksheet.Cells[row, 7].Text;
//                            description = description.Replace(((char)10).ToString(), Environment.NewLine);
//                            description = description.Replace(((char)13).ToString(), Environment.NewLine);
                            description = description.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

                            ComponentBoard comp = new ComponentBoard
                            {
                                Label = label,
                                NameTechnical = nameTechnical,
                                NameFriendly = nameFriendly,
                                Type = type,
                                ImagePinout = filePinout,
                                OneLiner = oneliner,
                                Description = description
                            };
                            if (board.Components == null) board.Components = new List<ComponentBoard>();
                            board.Components.Add(comp);
                            row++;
                        }
                    }
                }
            }

            // 5) Create empty "ComponentBounds" for each BoardFile
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    foreach (BoardFileOverlays bf in board.Files)
                    {
                        using (var package = new ExcelPackage(new FileInfo(
                            Path.Combine(Application.StartupPath, "Data", hardware.Folder, board.Folder, board.Datafile))))
                        {
                            var worksheet = package.Workbook.Worksheets["Components"];
                            if (worksheet == null)
                            {
                                throw new Exception("Worksheet [Components] not found in [" + hardware.Folder + "\\" + board.Folder + "\\" + board.Datafile + "]");
                            }
                            string searchHeader = "Components";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                                row++;
                            }
                            row++;
                            row++;

                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string name = worksheet.Cells[row, 1].Value.ToString();
                                ComponentBounds cb = new ComponentBounds
                                {
                                    Label = name
                                };
                                if (bf.Components == null) bf.Components = new List<ComponentBounds>();
                                bf.Components.Add(cb);
                                row++;
                            }
                        }
                    }
                }
            }

            // 6) Load "component local files" (datasheets)
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(
                        Path.Combine(Application.StartupPath, "Data", hardware.Folder, board.Folder, board.Datafile))))
                    {
                        var worksheet = package.Workbook.Worksheets["Component local files"];
                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet [Components local files] not found in [" + hardware.Folder + "\\" + board.Folder + "\\" + board.Datafile + "]");
                        }
                        string searchHeader = "Component local files";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                            row++;
                        }
                        row++;
                        row++;

                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string componentName = worksheet.Cells[row, 1].Value.ToString();
                            string name = worksheet.Cells[row, 2].Value.ToString();
                            string fileName = worksheet.Cells[row, 3].Value.ToString();

                            var classComponent = board.Components.FirstOrDefault(c => c.Label == componentName);
                            if (classComponent != null)
                            {
                                if (classComponent.LocalFiles == null) classComponent.LocalFiles = new List<ComponentLocalFiles>();
                                classComponent.LocalFiles.Add(new ComponentLocalFiles
                                {
                                    Name = name,
                                    FileName = fileName
                                });
                            }
                            row++;
                        }
                    }
                }
            }

            // 7) Load "component links"
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(
                        Path.Combine(Application.StartupPath, "Data", hardware.Folder, board.Folder, board.Datafile))))
                    {
                        var worksheet = package.Workbook.Worksheets["Component links"];
                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet [Component links] not found in [" + hardware.Folder + "\\" + board.Folder + "\\" + board.Datafile + "]");
                        }
                        string searchHeader = "Component links";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                            row++;
                        }
                        row++;
                        row++;

                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string componentName = worksheet.Cells[row, 1].Value.ToString();
                            string linkName = worksheet.Cells[row, 2].Value.ToString();
                            string linkUrl = worksheet.Cells[row, 3].Value.ToString();

                            var comp = board.Components.FirstOrDefault(c => c.Label == componentName);
                            if (comp != null)
                            {
                                if (comp.ComponentLinks == null) comp.ComponentLinks = new List<ComponentLinks>();
                                comp.ComponentLinks.Add(new ComponentLinks
                                {
                                    Name = linkName,
                                    Url = linkUrl
                                });
                            }
                            row++;
                        }
                    }
                }
            }

            // 8) Load "component highlights" (overlay rectangles)
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(
                        Path.Combine(Application.StartupPath, "Data", hardware.Folder, board.Folder, board.Datafile))))
                    {
                        var worksheet = package.Workbook.Worksheets["Component highlights"];
                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet [Component highlights] not found in [" + hardware.Folder + "\\" + board.Folder + "\\" + board.Datafile + "]");
                        }
                        string searchHeader = "Image and component highlight bounds";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                            row++;
                        }
                        row++;
                        row++;

                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string imageName = worksheet.Cells[row, 1].Value.ToString();
                            string componentName = worksheet.Cells[row, 2].Value.ToString();
                            int x = (int)(double)worksheet.Cells[row, 3].Value;
                            int y = (int)(double)worksheet.Cells[row, 4].Value;
                            int w = (int)(double)worksheet.Cells[row, 5].Value;
                            int h = (int)(double)worksheet.Cells[row, 6].Value;

                            var bf = board.Files?.FirstOrDefault(f => f.Name == imageName);
                            var compBounds = bf?.Components.FirstOrDefault(c => c.Label == componentName);
                            if (compBounds != null)
                            {
                                if (compBounds.Overlays == null) compBounds.Overlays = new List<Overlay>();
                                compBounds.Overlays.Add(new Overlay
                                {
                                    Bounds = new Rectangle(x, y, w, h)
                                });
                            }
                            row++;
                        }
                    }
                }
            }

            // 9) Load "board links"
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(
                        Path.Combine(Application.StartupPath, "Data", hardware.Folder, board.Folder, board.Datafile))))
                    {
                        var worksheet = package.Workbook.Worksheets["Board links"];
                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet [Board links] not found in [" + hardware.Folder + "\\" + board.Folder + "\\" + board.Datafile + "]");
                        }
                        string searchHeader = "Board links";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                            row++;
                        }
                        row++;
                        row++;

                        // Initialize the BoardLinks list if it's null
                        if (board.BoardLinks == null)
                        {
                            board.BoardLinks = new List<BoardLink>();
                        }

                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string category = worksheet.Cells[row, 1].Value.ToString();
                            string linkName = worksheet.Cells[row, 2].Value.ToString();
                            string linkUrl = worksheet.Cells[row, 3].Value.ToString();

                            // Create a new BoardLink instance and add it to the BoardLinks list
                            BoardLink boardLink = new BoardLink
                            {
                                Category = category,
                                Name = linkName,
                                Url = linkUrl
                            };
                            board.BoardLinks.Add(boardLink);

                            row++;
                        }
                    }
                }
            }

            // 10) Load "Board local files"
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(
                        Path.Combine(Application.StartupPath, "Data", hardware.Folder, board.Folder, board.Datafile))))
                    {
                        var worksheet = package.Workbook.Worksheets["Board local files"];
                        if (worksheet == null)
                        {
                            throw new Exception("Worksheet [Board local files] not found in [" + hardware.Folder + "\\" + board.Folder + "\\" + board.Datafile + "]");
                        }
                        string searchHeader = "Board local files";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value?.ToString() == searchHeader) break;
                            row++;
                        }
                        row++;
                        row++;

                        // Initialize the BoardLinks list if it's null
                        if (board.BoardLocalFiles == null)
                        {
                            board.BoardLocalFiles = new List<BoardLocalFiles>();
                        }

                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string category = worksheet.Cells[row, 1].Value.ToString();
                            string fileName = worksheet.Cells[row, 2].Value.ToString();
                            string fileLocation = worksheet.Cells[row, 3].Value.ToString();

                            // Create a new BoardLocalFiles instance and add it to the BoardLocalFiles list
                            BoardLocalFiles boardLocalFile = new BoardLocalFiles
                            {
                                Category = category,
                                Name = fileName,
                                Datafile = fileLocation
                            };
                            board.BoardLocalFiles.Add(boardLocalFile);

                            row++;
                        }
                    }
                }
            }

        }
    }
}