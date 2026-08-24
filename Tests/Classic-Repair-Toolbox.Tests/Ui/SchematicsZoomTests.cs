using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CRT;

namespace ClassicRepairToolbox.Tests.Ui;

// ###########################################################################################
// The schematic viewer's zoom limits, exercised on a real TabSchematics in a real (headless)
// window.
//
// ViewportMathTests already covers ComputeWheelZoomFactor - how far ONE wheel notch zooms.
// This covers what that number is then clamped to, which lives in TabSchematics.Viewport and
// so was previously unreachable by any test: zoom in far enough and it must stop dead at
// AppConfig.SchematicsMaxZoom; zoom out far enough and it must stop at the 1.0 baseline,
// because the image is already fitted by Stretch="Uniform" and zooming below that would show
// empty space.
//
// ApplySchematicsZoom is private, so it is reached by reflection - the same approach
// ExternalTargetLauncherTests uses, and for the same reason: the behaviour is worth pinning
// and the method is not worth widening just for a test.
// ###########################################################################################
[Collection("HeadlessUi")]
public class SchematicsZoomTests
{
    // Anchor for the zoom. Any point inside the container works; the limits do not depend on it.
    private static readonly Point ZoomCentre = new(400, 300);

    [Fact]
    public void Zooming_in_repeatedly_stops_exactly_at_the_maximum_zoom_level()
    {
        UiTest.Run(() =>
        {
            TabSchematics tab = CreateShownTabWithImage();

            // Far more notches than could ever be needed to reach the ceiling, so the test
            // proves it STOPS rather than merely that it climbs.
            for (int i = 0; i < 40; i++)
            {
                ApplyZoom(tab, AppConfig.SchematicsZoomFactor);

                Assert.True(
                    tab.schematicsMatrix.M11 <= AppConfig.SchematicsMaxZoom,
                    $"Zoom exceeded the maximum on notch {i + 1}: {tab.schematicsMatrix.M11}");
            }

            Assert.Equal(AppConfig.SchematicsMaxZoom, tab.schematicsMatrix.M11, precision: 6);
        });
    }

    [Fact]
    public void Zooming_out_repeatedly_stops_at_the_baseline_and_never_goes_below_it()
    {
        UiTest.Run(() =>
        {
            TabSchematics tab = CreateShownTabWithImage();

            // Zoom in first. Without this the test would pass even if zoom did nothing at
            // all, since the view already starts at the 1.0 baseline it is meant to land on.
            for (int i = 0; i < 5; i++)
            {
                ApplyZoom(tab, AppConfig.SchematicsZoomFactor);
            }

            Assert.True(tab.schematicsMatrix.M11 > 1.0, "Zooming in did not change the view.");

            for (int i = 0; i < 40; i++)
            {
                ApplyZoom(tab, 1.0 / AppConfig.SchematicsZoomFactor);

                Assert.True(
                    tab.schematicsMatrix.M11 >= 1.0,
                    $"Zoom fell below the baseline on notch {i + 1}: {tab.schematicsMatrix.M11}");
            }

            Assert.Equal(1.0, tab.schematicsMatrix.M11, precision: 6);
        });
    }

    [Fact]
    public void Zooming_all_the_way_in_then_all_the_way_out_returns_to_the_baseline()
    {
        UiTest.Run(() =>
        {
            TabSchematics tab = CreateShownTabWithImage();

            for (int i = 0; i < 40; i++)
            {
                ApplyZoom(tab, AppConfig.SchematicsZoomFactor);
            }

            Assert.Equal(AppConfig.SchematicsMaxZoom, tab.schematicsMatrix.M11, precision: 6);

            for (int i = 0; i < 40; i++)
            {
                ApplyZoom(tab, 1.0 / AppConfig.SchematicsZoomFactor);
            }

            // Below the baseline the viewer resets to Matrix.Identity outright rather than
            // clamping the scale alone, so the pan offset must be gone too - not just the zoom.
            Assert.Equal(Matrix.Identity, tab.schematicsMatrix);
        });
    }

    [Fact]
    public void A_nonsensical_zoom_factor_leaves_the_view_untouched()
    {
        UiTest.Run(() =>
        {
            TabSchematics tab = CreateShownTabWithImage();

            ApplyZoom(tab, AppConfig.SchematicsZoomFactor);
            Matrix afterOneNotch = tab.schematicsMatrix;

            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, 0.0, -2.0 })
            {
                ApplyZoom(tab, bad);

                Assert.Equal(afterOneNotch, tab.schematicsMatrix);
            }
        });
    }

    [Fact]
    public void Zoom_does_nothing_until_a_schematic_image_is_loaded()
    {
        UiTest.Run(() =>
        {
            // No bitmap assigned: the viewer has nothing to zoom, and must not move the matrix.
            var tab = new TabSchematics();

            ApplyZoom(tab, AppConfig.SchematicsZoomFactor);

            Assert.Equal(Matrix.Identity, tab.schematicsMatrix);
        });
    }

    // ###########################################################################################
    // A TabSchematics inside a shown window, laid out, with a schematic image loaded.
    //
    // The window and the layout pass matter: the clamping reads the container's bounds, so a
    // tab that was never measured would clamp against a zero-sized viewport.
    // ###########################################################################################
    private static TabSchematics CreateShownTabWithImage()
    {
        var tab = new TabSchematics();

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = tab,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        tab.currentFullResBitmap = new WriteableBitmap(
            new PixelSize(1200, 900),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        return tab;
    }

    private static void ApplyZoom(TabSchematics tab, double zoomFactor)
    {
        MethodInfo? method = typeof(TabSchematics).GetMethod(
            "ApplySchematicsZoom",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        method!.Invoke(tab, new object[] { zoomFactor, ZoomCentre });
    }
}
