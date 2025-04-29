using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    internal class PolylinesManagement
    {

        public static bool isDrawing = false;
        public static (int LineIndex, int PointIndex) selectedMarker = (-1, -1); // Tracks the selected marker
        public static int selectedPolylineIndex = -1;
        private static List<Point> currentPolyline = null; // Current polyline being drawn
        private const int MarkerRadius = 6; // Radius of the marker circle
        
        public static Dictionary<string, List<List<Point>>> imagePolylines = new Dictionary<string, List<List<Point>>>();
        public static List<List<Point>> polylines = new List<List<Point>>(); // List of polylines
        public static Dictionary<(string ImageName, int PolylineIndex), Color> polylineColors = new Dictionary<(string, int), Color>();

        public static HashSet<(string ImageName, int PolylineIndex)> visiblePolylines = new HashSet<(string, int)>();
        public static Color LastSelectedPolylineColor { get; set; } = Color.Red; // Default to red

        private Main main;

        public PolylinesManagement(Main mainForm)
        {
            main = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
        }

        public void panelImageMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                HandleRightClick(e);
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                bool clickedOnMarker = false;

                // Check if clicking on an existing marker
                for (int i = 0; i < polylines.Count; i++)
                {
                    for (int j = 0; j < polylines[i].Count; j++)
                    {
                        Point scaledMarker = ScalePoint(polylines[i][j]);
                        if (IsPointInMarker(e.Location, scaledMarker))
                        {
                            selectedMarker = (i, j);
                            selectedPolylineIndex = i;
                            Main.overlayPanel.Invalidate();
                            main.UpdateButtonColorPolylineState();
                            clickedOnMarker = true;
                            return;
                        }
                    }
                }

                // If not clicking on a marker, check if clicking on a line segment
                if (!clickedOnMarker)
                {
                    for (int i = 0; i < polylines.Count; i++)
                    {
                        for (int j = 0; j < polylines[i].Count - 1; j++)
                        {
                            Point scaledStart = ScalePoint(polylines[i][j]);
                            Point scaledEnd = ScalePoint(polylines[i][j + 1]);
                            Point closestPoint = GetClosestPointOnLine(scaledStart, scaledEnd, e.Location);

                            if (IsPointNearLine(e.Location, closestPoint))
                            {
                                selectedPolylineIndex = i;
                                selectedMarker = (-1, -1);

                                // Insert new marker immediately
                                Point newPointUnscaled = new Point((int)(closestPoint.X / Main.zoomFactor), (int)(closestPoint.Y / Main.zoomFactor));
                                polylines[i].Insert(j + 1, newPointUnscaled);
                                selectedMarker = (i, j + 1);

                                Main.overlayPanel.Invalidate();
                                main.UpdateButtonColorPolylineState();
                                return;
                            }
                        }
                    }
                }

                // If in drawing mode, add points to a new polyline
                if (isDrawing)
                {
                    if (currentPolyline == null)
                    {
                        currentPolyline = new List<Point>();
                        selectedPolylineIndex = polylines.Count;
                    }
                    Point pointUnscaled = new Point((int)(e.Location.X / Main.zoomFactor), (int)(e.Location.Y / Main.zoomFactor));
                    currentPolyline.Add(pointUnscaled);
                    return;
                }

                // Deselect if clicking empty space
                selectedPolylineIndex = -1;
                selectedMarker = (-1, -1);
                Main.overlayPanel.Invalidate();
                main.UpdateButtonColorPolylineState();
            }
        }


        public void panelImageMain_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawing && e.Button == MouseButtons.Left)
            {
                if (currentPolyline != null && currentPolyline.Count > 1)
                {
                    // Save the completed polyline to the dictionary
                    imagePolylines[Main.schematicSelectedName].Add(currentPolyline);

                    // Assign the last selected color to the new polyline
                    var newPolylineIndex = imagePolylines[Main.schematicSelectedName].Count - 1;
                    polylineColors[(Main.schematicSelectedName, newPolylineIndex)] = LastSelectedPolylineColor;

                    // Add the new polyline to the visiblePolylines set
                    visiblePolylines.Add((Main.schematicSelectedName, newPolylineIndex));
                    Debug.WriteLine($"Adding polyline: {Main.schematicSelectedName}, Index: {newPolylineIndex}, Color: {LastSelectedPolylineColor}");

                    // Save the updated polylines to the configuration
                    SavePolylinesToConfig();

                    // Update the visibility panel and counters
                    main.PopulatePolylineVisibilityPanel();
                }
                currentPolyline = null; // Reset the current polyline
            }
            else if (e.Button == MouseButtons.Left)
            {
                selectedMarker = (-1, -1); // Deselect marker
                SavePolylinesToConfig();
            }
        }


        private void HandleRightClick(MouseEventArgs e)
        {
            // First, check if the right-click is on a marker.
            for (int i = 0; i < polylines.Count; i++)
            {
                for (int j = 0; j < polylines[i].Count; j++)
                {
                    Point scaledMarker = ScalePoint(polylines[i][j]);
                    if (IsPointInMarker(e.Location, scaledMarker))
                    {
                        // If the polyline has only two markers left, remove the whole polyline.
                        if (polylines[i].Count <= 2)
                        {
                            RemovePolyline(i);
                        }
                        else // Otherwise, remove only the clicked marker.
                        {
                            polylines[i].RemoveAt(j);
                        }
                        selectedMarker = (-1, -1);
                        selectedPolylineIndex = -1;
                        Main.overlayPanel.Invalidate();
                        main.UpdateButtonColorPolylineState();
                        SavePolylinesToConfig();

                        // Update the visibility panel and counters
                        main.PopulatePolylineVisibilityPanel();
                        return;
                    }
                }
            }

            // If no marker was hit, check if the right-click is near a line segment.
            for (int i = 0; i < polylines.Count; i++)
            {
                for (int j = 0; j < polylines[i].Count - 1; j++)
                {
                    Point scaledStart = ScalePoint(polylines[i][j]);
                    Point scaledEnd = ScalePoint(polylines[i][j + 1]);
                    Point closestPoint = GetClosestPointOnLine(scaledStart, scaledEnd, e.Location);
                    if (IsPointNearLine(e.Location, closestPoint))
                    {
                        // Right-click on a polyline (not on a marker) deletes the entire polyline.
                        RemovePolyline(i);
                        selectedMarker = (-1, -1);
                        selectedPolylineIndex = -1;
                        Main.overlayPanel.Invalidate();
                        main.UpdateButtonColorPolylineState();
                        SavePolylinesToConfig();

                        // Update the visibility panel and counters
                        main.PopulatePolylineVisibilityPanel();
                        return;
                    }
                }
            }
        }

        // Helper method to remove a polyline and update related data structures
        private void RemovePolyline(int index)
        {
            // Remove the polyline from the list
            polylines.RemoveAt(index);

            // Remove the polyline from the visiblePolylines set
            visiblePolylines.Remove((Main.schematicSelectedName, index));

            // Remove the polyline's color from the polylineColors dictionary
            polylineColors.Remove((Main.schematicSelectedName, index));

            // Adjust indices for remaining polylines in polylineColors and visiblePolylines
            var keysToUpdate = polylineColors.Keys
                .Where(k => k.ImageName == Main.schematicSelectedName && k.PolylineIndex > index)
                .ToList();

            foreach (var oldKey in keysToUpdate)
            {
                var newKey = (oldKey.ImageName, oldKey.PolylineIndex - 1);
                polylineColors[newKey] = polylineColors[oldKey];
                polylineColors.Remove(oldKey);

                if (visiblePolylines.Contains(oldKey))
                {
                    visiblePolylines.Remove(oldKey);
                    visiblePolylines.Add(newKey);
                }
            }
        }


        // MouseMove event for overlayPanel
        public void panelImageMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && e.Button == MouseButtons.Left && currentPolyline != null)
            {
                Point pointUnscaled = new Point((int)(e.Location.X / Main.zoomFactor), (int)(e.Location.Y / Main.zoomFactor));
                if (currentPolyline.Count == 1) // Only update the second point dynamically
                {
                    currentPolyline.Add(pointUnscaled);
                }
                else
                {
                    currentPolyline[currentPolyline.Count - 1] = pointUnscaled;
                }
                Main.overlayPanel.Invalidate();
            }
            else if (selectedMarker.LineIndex != -1 && e.Button == MouseButtons.Left)
            {
                Point newPointUnscaled = new Point((int)(e.Location.X / Main.zoomFactor), (int)(e.Location.Y / Main.zoomFactor));

                if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    var polyline = polylines[selectedMarker.LineIndex];
                    if (selectedMarker.PointIndex >= 0 && selectedMarker.PointIndex < polyline.Count)
                    {
                        Point? previousPoint = selectedMarker.PointIndex > 0 ? polyline[selectedMarker.PointIndex - 1] : (Point?)null;
                        Point? nextPoint = selectedMarker.PointIndex < polyline.Count - 1 ? polyline[selectedMarker.PointIndex + 1] : (Point?)null;

                        // Case 1: Only two points in polyline (start + end) or this is an outer marker
                        if (polyline.Count == 2 ||
                            (selectedMarker.PointIndex == 0 && nextPoint.HasValue) ||
                            (selectedMarker.PointIndex == polyline.Count - 1 && previousPoint.HasValue))
                        {
                            // For start marker (align with the end marker)
                            if (selectedMarker.PointIndex == 0 && nextPoint.HasValue)
                            {
                                int deltaX = Math.Abs(nextPoint.Value.X - newPointUnscaled.X);
                                int deltaY = Math.Abs(nextPoint.Value.Y - newPointUnscaled.Y);

                                if (deltaX < deltaY)
                                {
                                    // Snap X to align vertically
                                    newPointUnscaled.X = nextPoint.Value.X;
                                }
                                else
                                {
                                    // Snap Y to align horizontally
                                    newPointUnscaled.Y = nextPoint.Value.Y;
                                }
                            }
                            // For end marker (align with the start marker)
                            else if (selectedMarker.PointIndex == polyline.Count - 1 && previousPoint.HasValue)
                            {
                                int deltaX = Math.Abs(previousPoint.Value.X - newPointUnscaled.X);
                                int deltaY = Math.Abs(previousPoint.Value.Y - newPointUnscaled.Y);

                                if (deltaX < deltaY)
                                {
                                    // Snap X to align vertically
                                    newPointUnscaled.X = previousPoint.Value.X;
                                }
                                else
                                {
                                    // Snap Y to align horizontally
                                    newPointUnscaled.Y = previousPoint.Value.Y;
                                }
                            }
                        }
                        // Case 2: Inner marker (between start and end) - align both X and Y
                        else if (previousPoint.HasValue && nextPoint.HasValue)
                        {
                            // For inner markers, we align both horizontally AND vertically
                            // Based on the closest neighbors

                            // Find closest X value from either previous or next point
                            if (Math.Abs(previousPoint.Value.X - newPointUnscaled.X) <
                                Math.Abs(nextPoint.Value.X - newPointUnscaled.X))
                            {
                                newPointUnscaled.X = previousPoint.Value.X;
                            }
                            else
                            {
                                newPointUnscaled.X = nextPoint.Value.X;
                            }

                            // Find closest Y value from either previous or next point  
                            if (Math.Abs(previousPoint.Value.Y - newPointUnscaled.Y) <
                                Math.Abs(nextPoint.Value.Y - newPointUnscaled.Y))
                            {
                                newPointUnscaled.Y = previousPoint.Value.Y;
                            }
                            else
                            {
                                newPointUnscaled.Y = nextPoint.Value.Y;
                            }
                        }
                    }
                }

                polylines[selectedMarker.LineIndex][selectedMarker.PointIndex] = newPointUnscaled;
                Main.overlayPanel.Invalidate();
            }
        }





        public void panelImageMain_Paint(object sender, PaintEventArgs e)
        {
            // Make sure we're using the correct polylines list for the current schematic
            polylines = imagePolylines.ContainsKey(Main.schematicSelectedName)
                ? imagePolylines[Main.schematicSelectedName]
                : new List<List<Point>>();

            // Draw all polylines for the current image
            for (int polylineIndex = 0; polylineIndex < polylines.Count; polylineIndex++)
            {
                // Skip drawing this polyline, if it is hidden
                if (!visiblePolylines.Contains((Main.schematicSelectedName, polylineIndex)))
                {
                    continue;
                }

                // Get the color for this polyline (default to Red if no color is specified)
                Color lineColor = polylineColors.ContainsKey((Main.schematicSelectedName, polylineIndex))
                    ? polylineColors[(Main.schematicSelectedName, polylineIndex)]
                    : Color.Red;

                DrawPolyline(e.Graphics, polylines[polylineIndex], new Pen(lineColor), polylineIndex);
            }

            // Draw the current polyline being drawn (if any)
            if (currentPolyline != null && currentPolyline.Count > 1)
            {
                // Use the last selected color for the current polyline
                Color currentPolylineColor = LastSelectedPolylineColor;
                using (Pen currentPen = new Pen(currentPolylineColor, 2)) // Adjust thickness as needed
                {
                    DrawPolyline(e.Graphics, currentPolyline, currentPen, -1);
                }
            }
        }

        private static void DrawPolyline(Graphics graphics, List<Point> polyline, Pen defaultPen, int polylineIndex)
        {
            // Use the composite key to get the color
            var key = (Main.schematicSelectedName, polylineIndex);
            Color lineColor = polylineColors.ContainsKey(key) ? polylineColors[key] : defaultPen.Color;
            bool isSelected = (polylineIndex == selectedPolylineIndex);

            using (Pen customPen = new Pen(lineColor, 5))
            using (Pen outlinePen = new Pen(Color.Black, 9)) // 9 is thicker than 5
            {
                outlinePen.LineJoin = LineJoin.Round;
                customPen.LineJoin = LineJoin.Round;

                for (int i = 0; i < polyline.Count - 1; i++)
                {
                    Point scaledStart = ScalePoint(polyline[i]);
                    Point scaledEnd = ScalePoint(polyline[i + 1]);

                    if (isSelected)
                    {
                        graphics.DrawLine(outlinePen, scaledStart, scaledEnd); // Draw outline if selected
                    }

                    graphics.DrawLine(customPen, scaledStart, scaledEnd); // Draw the line
                }
            }

            // Draw markers only if the polyline is selected
            if (isSelected)
            {
                foreach (var point in polyline)
                {
                    Point scaledPoint = ScalePoint(point);
                    DrawMarker(graphics, scaledPoint, lineColor);
                }
            }
        }


        private static void DrawMarker(Graphics graphics, Point point, Color color)
        {
            Rectangle markerBounds = new Rectangle(
                point.X - MarkerRadius,
                point.Y - MarkerRadius,
                MarkerRadius * 2,
                MarkerRadius * 2
            );

            // Fill the marker with the selected color
            using (Brush brush = new SolidBrush(color))
            {
                graphics.FillEllipse(brush, markerBounds);
            }

            // Draw the white outline after the fill
            using (Pen outlinePen = new Pen(Color.White, 2))
            {
                graphics.DrawEllipse(outlinePen, markerBounds);
            }
        }


        public static void MovePolyline(int polylineIndex, int dx, int dy)
        {
            if (polylineIndex >= 0 && polylineIndex < polylines.Count)
            {
                for (int i = 0; i < polylines[polylineIndex].Count; i++)
                {
                    Point currentPoint = polylines[polylineIndex][i];
                    polylines[polylineIndex][i] = new Point(currentPoint.X + dx, currentPoint.Y + dy);
                }
            }
        }

        private static Point GetClosestPointOnLine(Point start, Point end, Point clickPoint)
        {
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;

            if (dx == 0 && dy == 0) return start; // Line is a point

            float t = ((clickPoint.X - start.X) * dx + (clickPoint.Y - start.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t)); // Clamp t to the range [0, 1]

            return new Point((int)(start.X + t * dx), (int)(start.Y + t * dy));
        }

        // Helper method to check if a point is near a line
        private static bool IsPointNearLine(Point clickPoint, Point closestPoint)
        {
            const int proximityThreshold = 5; // Adjust as needed
            return Math.Abs(clickPoint.X - closestPoint.X) <= proximityThreshold &&
                   Math.Abs(clickPoint.Y - closestPoint.Y) <= proximityThreshold;
        }

        // Helper method to scale a point based on zoom factor
        private static Point ScalePoint(Point point)
        {
            return new Point((int)(point.X * Main.zoomFactor), (int)(point.Y * Main.zoomFactor));
        }

        // Helper method to check if a point is inside a marker
        private static bool IsPointInMarker(Point point, Point markerCenter)
        {
            return Math.Pow(point.X - markerCenter.X, 2) + Math.Pow(point.Y - markerCenter.Y, 2) <= Math.Pow(MarkerRadius, 2);
        }

        private static string SerializePolylines(List<List<Point>> polylines, Dictionary<int, Color> colors)
        {
            StringBuilder sb = new StringBuilder();

            // Format: [polylineCount];[color,points,color,points,...]
            sb.Append(polylines.Count);

            for (int i = 0; i < polylines.Count; i++)
            {
                sb.Append(";");

                // Add color information (R,G,B)
                Color color = colors.ContainsKey(i) ? colors[i] : Color.Red;
                sb.Append(color.R).Append(",").Append(color.G).Append(",").Append(color.B);

                // Add points for this polyline
                sb.Append(":");
                List<Point> polyline = polylines[i];
                sb.Append(polyline.Count);

                foreach (var point in polyline)
                {
                    sb.Append(",").Append(point.X).Append(",").Append(point.Y);
                }
            }

            return sb.ToString();
        }

        




        public static void SavePolylinesToConfig()
        {
            try
            {
                foreach (var entry in imagePolylines)
                {
                    string schematicName = entry.Key;
                    List<List<Point>> schematicPolylines = entry.Value;

                    // Extract color data for this schematic
                    Dictionary<int, Color> schematicColors = new Dictionary<int, Color>();
                    for (int i = 0; i < schematicPolylines.Count; i++)
                    {
                        // Use the composite key to get the color
                        var key = (schematicName, i);
                        schematicColors[i] = polylineColors.ContainsKey(key) ? polylineColors[key] : Color.Red;
                    }

                    // Create a key in the format: Polylines|hardware|board|schematic
                    string configKey = $"Polylines|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{schematicName}";

                    // Save polylines if they exist, otherwise remove the configuration entry
                    if (schematicPolylines.Count > 0)
                    {
                        string serialized = SerializePolylines(schematicPolylines, schematicColors);
                        Configuration.SaveSetting(configKey, serialized);
                        Debug.WriteLine($"Saved {schematicPolylines.Count} polylines for {configKey}");
                    }
                    else
                    {
                        // Remove the configuration entry if no polylines exist
                        Configuration.SaveSetting(configKey, "");
                        Debug.WriteLine($"Removed polylines for {configKey}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving polylines: {ex.Message}");
            }
        }

        public static void LoadPolylines()
        {
            try
            {
                // Clear existing polylines
                imagePolylines.Clear();
                polylineColors.Clear();

                var hw = Main.classHardware.FirstOrDefault(h => h.Name == Main.hardwareSelectedName);
                if (hw == null) return;

                var bd = hw.Boards.FirstOrDefault(b => b.Name == Main.boardSelectedName);
                if (bd == null || bd.Files == null) return;

                foreach (var file in bd.Files)
                {
                    string schematicName = file.Name;
                    string configKey = $"Polylines|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{schematicName}";
                    string serialized = Configuration.GetSetting(configKey, "");

                    if (!string.IsNullOrEmpty(serialized))
                    {
                        var (loadedPolylines, loadedColors) = DeserializePolylines(serialized);
                        imagePolylines[schematicName] = loadedPolylines;

                        // Add colors to the polylineColors dictionary using the composite key
                        for (int i = 0; i < loadedPolylines.Count; i++)
                        {
                            var key = (schematicName, i);
                            if (loadedColors.ContainsKey(i))
                            {
                                polylineColors[key] = loadedColors[i];
                            }

                            // Add all polylines to visiblePolylines by default
                            visiblePolylines.Add(key);
                        }

                        Debug.WriteLine($"Loaded {loadedPolylines.Count} polylines for {configKey}");
                    }
                    else
                    {
                        // Initialize with empty list
                        imagePolylines[schematicName] = new List<List<Point>>();
                    }
                }

                // Update the current polyline reference to the selected schematic
                if (!string.IsNullOrEmpty(Main.schematicSelectedName) && imagePolylines.ContainsKey(Main.schematicSelectedName))
                {
                    polylines = imagePolylines[Main.schematicSelectedName];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading polylines: {ex.Message}");
            }
        }

        private static (List<List<Point>>, Dictionary<int, Color>) DeserializePolylines(string data)
        {
            List<List<Point>> polylines = new List<List<Point>>();
            Dictionary<int, Color> colors = new Dictionary<int, Color>();

            if (string.IsNullOrEmpty(data))
                return (polylines, colors);

            string[] parts = data.Split(';');
            if (parts.Length < 1)
                return (polylines, colors);

            // Parse polyline count
            if (!int.TryParse(parts[0], out int polylineCount))
                return (polylines, colors);

            for (int i = 0; i < polylineCount && i + 1 < parts.Length; i++)
            {
                string polylinePart = parts[i + 1];
                string[] colorAndPoints = polylinePart.Split(':');

                if (colorAndPoints.Length != 2)
                    continue;

                // Parse color
                string[] colorParts = colorAndPoints[0].Split(',');
                if (colorParts.Length >= 3 &&
                    int.TryParse(colorParts[0], out int r) &&
                    int.TryParse(colorParts[1], out int g) &&
                    int.TryParse(colorParts[2], out int b))
                {
                    colors[polylines.Count] = Color.FromArgb(r, g, b); // Use the current polyline index
                }

                // Parse points
                string[] pointsParts = colorAndPoints[1].Split(',');
                if (pointsParts.Length < 1 || !int.TryParse(pointsParts[0], out int pointCount))
                    continue;

                List<Point> polyline = new List<Point>();

                for (int j = 0; j < pointCount && (j * 2 + 1) < pointsParts.Length - 1; j++)
                {
                    int offset = j * 2 + 1;
                    if (int.TryParse(pointsParts[offset], out int x) &&
                        int.TryParse(pointsParts[offset + 1], out int y))
                    {
                        polyline.Add(new Point(x, y));
                    }
                }

                if (polyline.Count > 0)
                    polylines.Add(polyline);
            }

            return (polylines, colors);
        }



    }
}
