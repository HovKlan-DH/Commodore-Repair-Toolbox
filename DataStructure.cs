using Commodore_Repair_Toolbox;
using OfficeOpenXml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace Commodore_Retro_Toolbox
{
    public class DataStructure
    {

        public static void GetAllData(List<Hardware> classHardware)
        {
            // I am using this as "Polyform Noncommercial license"
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // --------------------------------------------------------------------
            // Get all "hardware" types/names from the Excel data file

            using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\Data.xlsx")))
            {
                // Assuming data is in the first worksheet
                var worksheet = package.Workbook.Worksheets[0];

                // Find the row that starts with the "searchHeader"
                string searchHeader = "\"Hardware\" name in drop-down";
                int row = 1;
                while (row <= worksheet.Dimension.End.Row)
                {
                    if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                    {
                        break; // found the starting row
                    }
                    row++;
                }

                // Skip the header row
                row++;

                // Now, start reading data from the identified row
                while (worksheet.Cells[row, 1].Value != null)
                {
                    string name = worksheet.Cells[row, 1].Value.ToString();
                    string folder = worksheet.Cells[row, 2].Value.ToString();
                    Hardware hardware = new Hardware
                    {
                        Name = name,
                        Folder = folder,
                    };
                    classHardware.Add(hardware);
                    row++;
                }
            }

            // --------------------------------------------------------------------
            // Get all "board" types/names from the Excel data file, within the specific hardware

            List<Board> classBoard = new List<Board>();
            foreach (Hardware hardware in classHardware)
            {
                using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\" + hardware.Folder + "\\Data.xlsx")))
                {
                    var worksheet = package.Workbook.Worksheets[0];

                    // Find the row that starts with the "searchHeader"
                    string searchHeader = "\"Board\" name in drop-down";
                    int row = 1;
                    while (row <= worksheet.Dimension.End.Row)
                    {
                        if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                        {
                            break; // found the starting row
                        }
                        row++;
                    }

                    // Skip the header row
                    row++;

                    // Now, start reading data from the identified row
                    while (worksheet.Cells[row, 1].Value != null)
                    {
                        string name = worksheet.Cells[row, 1].Value.ToString();
                        string folder = worksheet.Cells[row, 2].Value.ToString();
                        Board boarda = new Board
                        {
                            Name = name,
                            Folder = folder,
                        };
                        classBoard.Add(boarda);

                        // Associate the board with the hardware
                        // Create the "Boards" property if it is NULL and then add the board
                        if (hardware.Boards == null)
                        {
                            hardware.Boards = new List<Board>();
                        }
                        hardware.Boards.Add(boarda);
                        row++;
                    }
                }
            }

            // --------------------------------------------------------------------
            // Get all "image files" from the Excel data file, within the specific board

            List<Commodore_Repair_Toolbox.File> classFile = new List<Commodore_Repair_Toolbox.File>();
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\" + hardware.Folder + "\\" + board.Folder + "\\Data.xlsx")))
                    {
                        var worksheet = package.Workbook.Worksheets[0];

                        // Find the row that starts with the "searchHeader"
                        string searchHeader = "LIST IMAGES";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                            {
                                break; // found the starting row
                            }
                            row++;
                        }

                        // Skip the header row
                        row++;
                        row++;

                        // Now, start reading data from the identified row
                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string name = worksheet.Cells[row, 1].Value.ToString();
                            string file = worksheet.Cells[row, 2].Value.ToString();
                            Commodore_Repair_Toolbox.File filea = new Commodore_Repair_Toolbox.File
                            {
                                Name = name,
                                FileName = file,
                            };
                            classFile.Add(filea);

                            // Associate the board with the hardware
                            // Create the "Boards" property if it is NULL and then add the board
                            if (board.Files == null)
                            {
                                board.Files = new List<Commodore_Repair_Toolbox.File>();
                            }
                            board.Files.Add(filea);
                            row++;
                        }
                    }
                }
            }

            // --------------------------------------------------------------------
            // Get all "components" from the Excel data file, within the specific board.
            // This is the main component data - not the bounds with coordinates.
            // The components are processed one time per board.

            List<ComponentBoard> classComponentBoard = new List<ComponentBoard>();
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {

                    using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\" + hardware.Folder + "\\" + board.Folder + "\\Data.xlsx")))
                    {
                        var worksheet = package.Workbook.Worksheets[0];

                        // Find the row that starts with the "searchHeader"
                        string searchHeader = "COMPONENTS";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                            {
                                break; // found the starting row
                            }
                            row++;
                        }

                        // Skip the header row
                        row++;
                        row++;

                        // Now, start reading data from the identified row
                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string name = worksheet.Cells[row, 1].Value.ToString();
                            ComponentBoard component = new ComponentBoard
                            {
                                NameLabel = name,
                            };
                            classComponentBoard.Add(component);

                            // Associate the board with the hardware
                            // Create the "Boards" property if it is NULL and then add the board
                            if (board.Components == null)
                            {
                                board.Components = new List<ComponentBoard>();
                            }
                            board.Components.Add(component);
                            row++;
                        }
                    }
                }
            }

            // --------------------------------------------------------------------
            // Get all "components" from the Excel data file, within the specific board.
            // This is the component bounds (coordinates and size).
            // The components are processed per file for the specific board.
            // This is merely to create the data structure - not to populate with data.

            List<ComponentBounds> classComponentBounds = new List<ComponentBounds>();
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    foreach (Commodore_Repair_Toolbox.File file in board.Files)
                    {

                        using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\" + hardware.Folder + "\\" + board.Folder + "\\Data.xlsx")))
                        {
                            var worksheet = package.Workbook.Worksheets[0];

                            // Find the row that starts with the "searchHeader"
                            string searchHeader = "COMPONENTS";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                                {
                                    break; // found the starting row
                                }
                                row++;
                            }

                            // Skip the header row
                            row++;
                            row++;

                            // Now, start reading data from the identified row
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string name = worksheet.Cells[row, 1].Value.ToString();
                                ComponentBounds component = new ComponentBounds
                                {
                                    NameLabel = name,
                                };
                                classComponentBounds.Add(component);

                                // Associate the board with the hardware
                                // Create the "Boards" property if it is NULL and then add the board
                                if (file.Components == null)
                                {
                                    file.Components = new List<ComponentBounds>();
                                }
                                file.Components.Add(component);
                                row++;
                            }
                        }
                    }
                }
            }

            // --------------------------------------------------------------------
            // Get all bounds (coordinates and sizes) and populate it in 
            // data data structure.

            // Assuming that classHardware is already populated and well-formed
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    // Construct your file path here
                    string jsonFilePath = Application.StartupPath + "\\Data\\" + hardware.Folder + "\\" + board.Folder + "\\Data_Highlightning.json";
                    string jsonContent = System.IO.File.ReadAllText(jsonFilePath);
                    JObject jsonData = JObject.Parse(jsonContent);

                    foreach (var image in jsonData)
                    {
                        string imageName = image.Key;

                        // Assume you find the file by its name
                        var file = board.Files.FirstOrDefault(f => f.Name == imageName);

                        if (file != null)
                        {
                            foreach (var boardData in image.Value)
                            {
                                // Populate other file properties like highlight-tab-color
                                file.HighlightColorTab = boardData["highlight-tab-color"].ToString();

                                foreach (var componentData in (JArray)boardData["component"])
                                {
                                    foreach (JProperty componentProp in componentData)
                                    {
                                        string componentName = componentProp.Name;
                                        var componentBounds = file.Components.FirstOrDefault(c => c.NameLabel == componentName);

                                        if (componentBounds != null)
                                        {
                                            if (componentBounds.Overlays == null)
                                            {
                                                componentBounds.Overlays = new List<Overlay>();
                                            }

                                            foreach (var bounds in (JArray)componentProp.Value)
                                            {
                                                int x = (int)bounds["x"];
                                                int y = (int)bounds["y"];
                                                int width = (int)bounds["width"];
                                                int height = (int)bounds["height"];
                                                Rectangle rect = new Rectangle(x, y, width, height);

                                                Overlay overlay = new Overlay
                                                {
                                                    Bounds = rect
                                                };

                                                componentBounds.Overlays.Add(overlay);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            /*
            foreach (Hardware hardware in classHardware)
            {
                string hardwareName = hardware.Name;
                string hardwareFolder = hardware.Folder;

                foreach (Board board in hardware.Boards)
                {
                    string boardName = board.Name;
                    string boardFolder = board.Folder;

                    // Read the JSON data file
                    string jsonFilePath = Application.StartupPath + "\\Data\\" + hardwareFolder + "\\" + boardFolder + "\\Data_Highlightning.json";
                    string jsonContent = System.IO.File.ReadAllText(jsonFilePath);

                    JObject jsonData = JObject.Parse(jsonContent);

                    // The first data point in the file is the image name (not the file name)
                    foreach (var image in jsonData)
                    {
                        string imageName = image.Key;

                        // Walk through data inside the image data
                        foreach (var boardData in image.Value)
                        {
                            // HEST - set this data in the data structure!!!
                            string htc = boardData["highlight-tab-color"].ToString();

                            // Walk through data inside the component area
                            foreach (var componentData in (JArray)boardData["component"])
                            {
                                // Walk through data inside a specific component
                                foreach (JProperty hest in componentData)
                                {
                                    string componentName = hest.Name;

                                    // Get all bounds for the specific component (coordinate and size)
                                    foreach (var bounds in (JArray)hest.Value)
                                    {
                                        int x = (int)bounds["x"];
                                        int y = (int)bounds["y"];
                                        int width = (int)bounds["width"];
                                        int height = (int)bounds["height"];
                                        Rectangle rect = new Rectangle(x, y, width, height);

                                        // Define a new class element
                                        Overlay overlay = new Overlay
                                        {
                                            Bounds = rect
                                        };

                                        // Now we need to find 
                                        
                                        // Search for the correct Hardware, Board, File, and ComponentBounds
                                        Hardware foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareName);  // Replace with actual criteria
                                        if (foundHardware != null)
                                        {
                                            Board foundBoard = foundHardware.Boards.FirstOrDefault(b => b.Name == boardName);  // Replace with actual criteria
                                            if (foundBoard != null)
                                            {
                                                Commodore_Repair_Toolbox.File foundFile = foundBoard.Files.FirstOrDefault(f => f.Name == imageName);  // Replace with actual criteria
                                                if (foundFile != null)
                                                {
                                                    ComponentBounds foundComponentBounds = foundFile.Components.FirstOrDefault(c => c.NameLabel == componentName);
                                                    if (foundComponentBounds != null)
                                                    {
                                                        if (foundComponentBounds.Overlays == null)
                                                        {
                                                            foundComponentBounds.Overlays = new List<Overlay>();
                                                        }

                                                        // Add the Overlay object to the Overlays list of the found ComponentBounds
                                                        foundComponentBounds.Overlays.Add(overlay);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            */
        }

        // ------------------------------------------------

    }
}
