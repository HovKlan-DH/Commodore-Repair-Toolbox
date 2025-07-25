﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    internal class PolylinesManagement
    {
        public static (int LineIndex, int PointIndex) selectedMarker = (-1, -1); // tracks the selected marker
        public static int selectedPolylineIndex = -1;
        private static List<Point> currentPolyline = null; // current polyline being drawn
        private static int MarkerRadius = 5; // radius of the marker circle
        public static Dictionary<string, List<List<Point>>> imagePolylines = new Dictionary<string, List<List<Point>>>();
        public static List<List<Point>> polylines = new List<List<Point>>();
        public static Dictionary<(string ImageName, int PolylineIndex), Color> polylineColors = new Dictionary<(string, int), Color>();
        public static HashSet<(string ImageName, int PolylineIndex)> visiblePolylines = new HashSet<(string, int)>();
        public static Dictionary<Color, bool> CheckboxStates = new Dictionary<Color, bool>();
        private Main main; // instance of the "Main" form

        public static Color LastSelectedPolylineColor { get; set; } = Color.Red;


        // ###########################################################################################
        // Constructor for the PolylinesManagement class.
        // ###########################################################################################

        public PolylinesManagement(Main mainForm)
        {
            main = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
        }


        // ###########################################################################################
        // Handle mouse-down events for drawing polylines and selecting markers (for movement or new ones).
        // ###########################################################################################

        public void panelImageMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                HandleRightClick(e);
                return;
            }

            bool isHoveringOverComponent = Cursor.Current == Cursors.Hand;

            if (e.Button == MouseButtons.Left && !isHoveringOverComponent)
            {
                bool clickedOnMarker = false;
                bool clickedOnLine = false;

                // First pass: Check if we're clicking on a marker of the ALREADY selected polyline
                if (selectedPolylineIndex != -1 && selectedPolylineIndex < polylines.Count)
                {
                    for (int j = 0; j < polylines[selectedPolylineIndex].Count; j++)
                    {
                        Point scaledMarker = ScalePoint(polylines[selectedPolylineIndex][j]);
                        if (IsPointInMarker(e.Location, scaledMarker))
                        {
                            selectedMarker = (selectedPolylineIndex, j);
                            Main.overlayPanel.Invalidate();
                            clickedOnMarker = true;
                            return;
                        }
                    }
                }

                // Second pass: If not clicking on a marker from the selected polyline, check all other polylines
                if (!clickedOnMarker)
                {
                    for (int i = 0; i < polylines.Count; i++)
                    {
                        // Skip the already checked polyline
                        if (i == selectedPolylineIndex) continue;

                        for (int j = 0; j < polylines[i].Count; j++)
                        {
                            Point scaledMarker = ScalePoint(polylines[i][j]);
                            if (IsPointInMarker(e.Location, scaledMarker))
                            {
                                selectedMarker = (i, j);
                                selectedPolylineIndex = i;
                                Main.overlayPanel.Invalidate();
                                clickedOnMarker = true;
                                return;
                            }
                        }
                    }
                }

                // If not clicking on a marker, check if clicking on a line segment
                if (!clickedOnMarker)
                {
                    // First check if clicking on the selected polyline
                    if (selectedPolylineIndex != -1 && selectedPolylineIndex < polylines.Count)
                    {
                        for (int j = 0; j < polylines[selectedPolylineIndex].Count - 1; j++)
                        {
                            Point scaledStart = ScalePoint(polylines[selectedPolylineIndex][j]);
                            Point scaledEnd = ScalePoint(polylines[selectedPolylineIndex][j + 1]);
                            Point closestPoint = GetClosestPointOnLine(scaledStart, scaledEnd, e.Location);

                            if (IsPointNearLine(e.Location, closestPoint))
                            {
                                // Insert new marker immediately
                                Point newPointUnscaled = new Point((int)(closestPoint.X / Main.zoomFactor), (int)(closestPoint.Y / Main.zoomFactor));
                                polylines[selectedPolylineIndex].Insert(j + 1, newPointUnscaled);
                                selectedMarker = (selectedPolylineIndex, j + 1);

                                Main.overlayPanel.Invalidate();
                                clickedOnLine = true;
                                return;
                            }
                        }
                    }

                    // If not clicking on selected polyline, check all polylines
                    for (int i = 0; i < polylines.Count; i++)
                    {
                        // Skip the already checked polyline
                        if (i == selectedPolylineIndex) continue;

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
                                clickedOnLine = true;
                                return;
                            }
                        }
                    }
                }

                // If we didn't click on a marker or line, deselect the current selection
                if (!clickedOnMarker && !clickedOnLine && selectedPolylineIndex != -1)
                {
                    selectedPolylineIndex = -1;
                    selectedMarker = (-1, -1);
                    Main.overlayPanel.Invalidate();
                }

                // Start a new polyline if one isn't already being drawn
                if (currentPolyline == null)
                {
                    currentPolyline = new List<Point>();
                    selectedPolylineIndex = polylines.Count;
                }
                Point pointUnscaled = new Point((int)(e.Location.X / Main.zoomFactor), (int)(e.Location.Y / Main.zoomFactor));
                currentPolyline.Add(pointUnscaled);
                return;
            }
        }


        // ###########################################################################################
        // Handle mouse-up events for completing the polyline.
        // ###########################################################################################

        public void panelImageMain_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
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
//                    Debug.WriteLine($"Adding polyline: {Main.schematicSelectedName}, Index: {newPolylineIndex}, Color: {LastSelectedPolylineColor}");

                    // Set the newly created polyline as selected
                    selectedPolylineIndex = newPolylineIndex;

                    // Save the updated polylines to the configuration
                    SavePolylinesToConfig();

                    // Update the visibility panel and counters
                    main.PopulatePolylineVisibilityPanel();
                }
                currentPolyline = null; // Reset the current polyline
                SavePolylinesToConfig();
            }
        }


        // ###########################################################################################
        // Handle right-click events for deleting markers or entire polylines.
        // ###########################################################################################

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
                        // If the polyline has only two markers left, remove the whole polyline
                        if (polylines[i].Count <= 2)
                        {
                            RemovePolyline(i);
                            // Clear selection since the polyline no longer exists
                            selectedMarker = (-1, -1);
                            selectedPolylineIndex = -1;
                        }
                        // Otherwise, remove only the clicked marker but keep the polyline selected
                        else
                        {
                            int currentPolyline = i; // Store the polyline index before removing the marker
                            polylines[i].RemoveAt(j);

                            // Keep the polyline selected but clear the marker selection
                            selectedMarker = (-1, -1);
                            selectedPolylineIndex = currentPolyline;
                        }

                        Main.overlayPanel.Invalidate();
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
                        SavePolylinesToConfig();

                        // Update the visibility panel and counters
                        main.PopulatePolylineVisibilityPanel();
                        return;
                    }
                }
            }
        }


        // ###########################################################################################
        // Remove a polyline and update related data structures.
        // ###########################################################################################

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


        // ###########################################################################################
        // Handle mouse-movement over the overlay panel.
        // This is the entire "Schematics" image - not the individual overlay for components.
        // ###########################################################################################

        public void panelImageMain_MouseMove(object sender, MouseEventArgs e)
        {
            
            if (e.Button == MouseButtons.Left && currentPolyline != null)
            {
                Point pointUnscaled = new Point((int)(e.Location.X / Main.zoomFactor), (int)(e.Location.Y / Main.zoomFactor));
                if (currentPolyline.Count == 1) // Only update the second point dynamically
                {
                    int x_polyline = currentPolyline[0].X;
                    int y_polyline = currentPolyline[0].Y;
                    int x_pointUnscaled = pointUnscaled.X;
                    int y_pointUnscaled = pointUnscaled.Y;

                    // Do not allow that we insert an empty polyline
                    if (x_polyline != x_pointUnscaled || y_polyline != y_pointUnscaled)
                    {
                        currentPolyline.Add(pointUnscaled);
                    }
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


        // ###########################################################################################
        // Toggle visibility of polylines with a specific color.
        // ###########################################################################################

        public void TogglePolylineVisibility(Color color, bool isVisible)
        {
            foreach (var imageName in imagePolylines.Keys)
            {
                var polylines = imagePolylines[imageName];

                for (int polylineIndex = 0; polylineIndex < polylines.Count; polylineIndex++)
                {
                    if (polylineColors.TryGetValue((imageName, polylineIndex), out var polylineColor) && polylineColor.ToArgb() == color.ToArgb())
                    {
                        if (isVisible)
                        {
                            visiblePolylines.Add((imageName, polylineIndex));
                        }
                        else
                        {
                            visiblePolylines.Remove((imageName, polylineIndex));
                        }
                    }
                }
            }

            // Save the checkbox state
            CheckboxStates[color] = isVisible;

            // Redraw the overlay panel to reflect changes
            Main.overlayPanel.Invalidate();

            SaveCheckboxStates();
        }


        // ###########################################################################################
        // Paint event for painting polylines on the main "Schematics" image.
        // ###########################################################################################

        public void panelImageMain_Paint(object sender, PaintEventArgs e)
        {
            // Get the polylines for the selected schematic image
            polylines = imagePolylines.ContainsKey(Main.schematicSelectedName)
                ? imagePolylines[Main.schematicSelectedName]
                : new List<List<Point>>();

            // Draw all polylines
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


        // ###########################################################################################
        // Draw a polyline with the specified color and thickness.
        // ###########################################################################################       

        private static void DrawPolyline(Graphics graphics, List<Point> polyline, Pen defaultPen, int polylineIndex)
        {
            var key = (Main.schematicSelectedName, polylineIndex);
            Color lineColor = polylineColors.ContainsKey(key) ? polylineColors[key] : defaultPen.Color;
            bool isSelected = (polylineIndex == selectedPolylineIndex);

            using (Pen whitePen = new Pen(Color.White, 9))
            using (Pen customPen = new Pen(lineColor, 5))
            {
                whitePen.LineJoin = LineJoin.Round;
                customPen.LineJoin = LineJoin.Round;

                // Draw lines for polylines with more than one point
                for (int i = 0; i < polyline.Count - 1; i++)
                {
                    Point scaledStart = ScalePoint(polyline[i]);
                    Point scaledEnd = ScalePoint(polyline[i + 1]);

                    if (isSelected)
                    {
                        graphics.DrawLine(whitePen, scaledStart, scaledEnd);
                    }
                    graphics.DrawLine(customPen, scaledStart, scaledEnd);
                }
            }

            // Always draw markers, even for "empty" polylines (2 identical points)
            if (isSelected || polyline.Count == 2)
            {
                foreach (var point in polyline)
                {
                    Point scaledPoint = ScalePoint(point);
                    DrawMarker(graphics, scaledPoint, lineColor);
                }
            }
        }


        // ###########################################################################################
        // Draw a marker at the specified point with the given color.
        // ###########################################################################################

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

            // Draw the "outer" outline
            using (Pen largerBlackPen = new Pen(color, 4)) // Thickness slightly larger than the white outline
            {
                graphics.DrawEllipse(largerBlackPen, markerBounds);
            }

            // Draw the white outline
            using (Pen whitePen = new Pen(Color.White, 2)) // Original white outline
            {
                graphics.DrawEllipse(whitePen, markerBounds);
            }

            // Draw the "inner" outline
            using (Pen smallerBlackPen = new Pen(color, 1)) // Thickness slightly smaller than the white outline
            {
                Rectangle smallerBounds = new Rectangle(
                    markerBounds.X + 1,
                    markerBounds.Y + 1,
                    markerBounds.Width - 2,
                    markerBounds.Height - 2
                );
                graphics.DrawEllipse(smallerBlackPen, smallerBounds);
            }
        }


        // ###########################################################################################
        // Movement of polyline via keyboard arrows.
        // ###########################################################################################

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


        // ###########################################################################################
        // Scale a point based on zoom factor.
        // ###########################################################################################

        private static Point ScalePoint(Point point)
        {
            return new Point((int)(point.X * Main.zoomFactor), (int)(point.Y * Main.zoomFactor));
        }


        // ###########################################################################################
        // Get the closest concrete point on a polyline from the position of the mouse cursor.
        // ###########################################################################################

        private static Point GetClosestPointOnLine(Point start, Point end, Point clickPoint)
        {
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;

            if (dx == 0 && dy == 0) return start; // Line is a point

            float t = ((clickPoint.X - start.X) * dx + (clickPoint.Y - start.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t)); // Clamp t to the range [0, 1]

            return new Point((int)(start.X + t * dx), (int)(start.Y + t * dy));
        }


        // ###########################################################################################
        // Check if a point (mouse cursor) is near a polyline.
        // ###########################################################################################

        private static bool IsPointNearLine(Point clickPoint, Point closestPoint)
        {
            const int proximityThreshold = 5; // within 5px then it is a "hit"
            return Math.Abs(clickPoint.X - closestPoint.X) <= proximityThreshold &&
                   Math.Abs(clickPoint.Y - closestPoint.Y) <= proximityThreshold;
        }


        // ###########################################################################################
        // Check if a point (mouse cursor) is inside a marker.
        // ###########################################################################################

        private static bool IsPointInMarker(Point point, Point markerCenter)
        {
            const int proximityThreshold = 10;
            int effectiveRadius = MarkerRadius + proximityThreshold;
            return Math.Pow(point.X - markerCenter.X, 2) + Math.Pow(point.Y - markerCenter.Y, 2) <= Math.Pow(effectiveRadius, 2);
            //return Math.Pow(point.X - markerCenter.X, 2) + Math.Pow(point.Y - markerCenter.Y, 2) <= Math.Pow(MarkerRadius, 2);
        }


        // ###########################################################################################
        // Save polylines to the configuration file.
        // ###########################################################################################

        public static void SavePolylinesToConfig()
        {
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[SavePolylinesToConfig] called from [" + callerName + "]");
#endif

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

                    // Create a key in the format: Traces|hardware|board|schematic
                    string configKey = $"Traces|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{schematicName}";

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


        // ###########################################################################################
        // Load polylines from the configuration file.
        // ###########################################################################################

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

                // Load checkbox states after loading polylines
                LoadCheckboxStates();

                foreach (var file in bd.Files)
                {
                    string schematicName = file.Name;
                    string configKey = $"Traces|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{schematicName}";
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

                            // Check if the checkbox for this color is checked
                            if (loadedColors.ContainsKey(i) && CheckboxStates.TryGetValue(loadedColors[i], out bool isChecked) && isChecked)
                            {
                                visiblePolylines.Add(key);
                            }
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


        // ###########################################################################################
        // Serialize polylines to a string format for saving in the configuration file.
        // ###########################################################################################

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


        // ###########################################################################################
        // Deserialize polylines from a string format as received from the configuration file.
        // ###########################################################################################

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

        public static void SaveCheckboxStates()
        {
            // Build a unique config key from hardware and board
            string configKey = $"TracesCheckboxStates|{Main.hardwareSelectedName}|{Main.boardSelectedName}";

            // Serialize the checkbox states as "R,G,B=True/False"
            var serializedStates = CheckboxStates
                .ToDictionary(
                    kvp => $"{kvp.Key.R},{kvp.Key.G},{kvp.Key.B}", // Serialize color as "R,G,B"
                    kvp => kvp.Value.ToString()
                );

            string serializedData = string.Join(";", serializedStates.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            // Save to configuration
            Configuration.SaveSetting(configKey, serializedData);
        }


        // ###########################################################################################
        // Load "Traces visible" checkbox states from the configuration file.
        // ###########################################################################################

        public static void LoadCheckboxStates()
        {
            try
            {
                // Build a unique config key from hardware and board
                string configKey = $"TracesCheckboxStates|{Main.hardwareSelectedName}|{Main.boardSelectedName}";

                // Retrieve the serialized data from the configuration
                string serializedData = Configuration.GetSetting(configKey, "");

                // Clear the current CheckboxStates dictionary
                CheckboxStates.Clear();

                if (!string.IsNullOrEmpty(serializedData))
                {
                    // Deserialize the data in the format "R,G,B=True/False;..."
                    var entries = serializedData.Split(';');
                    foreach (var entry in entries)
                    {
                        var parts = entry.Split('=');
                        if (parts.Length == 2)
                        {
                            // Parse the color (R,G,B)
                            var colorParts = parts[0].Split(',');
                            if (colorParts.Length == 3 &&
                                int.TryParse(colorParts[0], out int r) &&
                                int.TryParse(colorParts[1], out int g) &&
                                int.TryParse(colorParts[2], out int b))
                            {
                                Color color = Color.FromArgb(r, g, b);

                                // Parse the visibility state (True/False)
                                if (bool.TryParse(parts[1], out bool isVisible))
                                {
                                    CheckboxStates[color] = isVisible;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading checkbox states: {ex.Message}");
            }
        }


        // ###########################################################################################
        // Clear all traces for the selected board.
        // ###########################################################################################

        public static void ClearTracesForBoard(Board selectedBoard)
        {
            if (selectedBoard?.Files != null)
            {
                // Walkthrough all schematic images
                foreach (var file in selectedBoard.Files)
                {
                    // Remove the specific schematic image from the "imagePolylines" list
                    if (imagePolylines.ContainsKey(file.Name))
                    {
                        imagePolylines[file.Name].Clear();
                    }

                    // Remove all visible polylines for the specific schematic image
                    visiblePolylines.RemoveWhere(key => key.ImageName == file.Name);

                    // Remove all related polyline colors for the specific schematic image
                    var keysToRemove = polylineColors.Keys.Where(key => key.ImageName == file.Name).ToList();
                    foreach (var key in keysToRemove)
                    {
                        polylineColors.Remove(key);
                    }
                }

                // Clear "Traces visible" checkboxes state
                var usedColors = polylineColors.Values.ToHashSet();
                var unusedColors = CheckboxStates.Keys.Where(color => !usedColors.Contains(color)).ToList();
                foreach (var color in unusedColors)
                {
                    CheckboxStates.Remove(color);
                }

                SavePolylinesToConfig();
                SaveCheckboxStates();
            }

            // ###########################################################################################
        }

    }
}
