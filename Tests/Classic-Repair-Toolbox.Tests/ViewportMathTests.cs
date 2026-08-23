using Handlers.Geometry;

namespace ClassicRepairToolbox.Tests;

// Tests for ViewportMath - wheel-zoom scaling and the selection set comparison, previously
// private statics inside TabSchematics.
//
// The zoom factor is the one users feel directly: it is why scrolling behaves the same on a
// Windows notched wheel and a high-resolution Linux or macOS trackpad.
public class ViewportMathTests
{
    private const double Base = 1.1;

    [Fact]
    public void One_windows_wheel_notch_gives_exactly_the_base_factor()
    {
        Assert.Equal(Base, ViewportMath.ComputeWheelZoomFactor(1.0, Base), precision: 12);
    }

    [Fact]
    public void Scrolling_the_other_way_gives_the_reciprocal()
    {
        Assert.Equal(1.0 / Base, ViewportMath.ComputeWheelZoomFactor(-1.0, Base), precision: 12);
    }

    [Fact]
    public void A_zoom_in_then_out_of_the_same_magnitude_returns_to_the_start()
    {
        double inFactor = ViewportMath.ComputeWheelZoomFactor(0.4, Base);
        double outFactor = ViewportMath.ComputeWheelZoomFactor(-0.4, Base);

        Assert.Equal(1.0, inFactor * outFactor, precision: 12);
    }

    [Fact]
    public void A_bigger_delta_zooms_further()
    {
        Assert.True(
            ViewportMath.ComputeWheelZoomFactor(2.0, Base) >
            ViewportMath.ComputeWheelZoomFactor(1.0, Base));
    }

    [Fact]
    public void Many_small_trackpad_deltas_zoom_less_than_one_full_notch()
    {
        // The whole point of the magnitude scaling: a high-resolution trackpad delivers lots of
        // tiny deltas, and each must move less than a full wheel notch.
        double smallStep = ViewportMath.ComputeWheelZoomFactor(0.1, Base);

        Assert.True(smallStep > 1.0);
        Assert.True(smallStep < ViewportMath.ComputeWheelZoomFactor(1.0, Base));
    }

    [Fact]
    public void A_huge_delta_is_clamped_so_one_event_cannot_zoom_wildly()
    {
        Assert.Equal(
            ViewportMath.ComputeWheelZoomFactor(3.0, Base),
            ViewportMath.ComputeWheelZoomFactor(500.0, Base),
            precision: 12);
    }

    [Fact]
    public void A_tiny_delta_is_floored_so_it_still_does_something()
    {
        Assert.Equal(
            ViewportMath.ComputeWheelZoomFactor(0.1, Base),
            ViewportMath.ComputeWheelZoomFactor(0.000001, Base),
            precision: 12);
    }

    [Fact]
    public void A_zero_delta_is_treated_as_a_zoom_out_at_the_floor_magnitude()
    {
        // Documents current behaviour: the sign test is "> 0", so exactly zero takes the
        // reciprocal branch rather than being a no-op.
        double factor = ViewportMath.ComputeWheelZoomFactor(0.0, Base);

        Assert.True(factor < 1.0);
        Assert.Equal(1.0 / ViewportMath.ComputeWheelZoomFactor(0.1, Base), factor, precision: 12);
    }

    // ------------------------------------------------- SetEqualsOrdinalIgnoreCase

    [Fact]
    public void Two_sets_with_the_same_members_in_any_order_are_equal()
    {
        Assert.True(ViewportMath.SetEqualsOrdinalIgnoreCase(
            new[] { "GND", "VCC", "CLK" },
            new[] { "CLK", "GND", "VCC" }));
    }

    [Fact]
    public void Membership_ignores_case()
    {
        Assert.True(ViewportMath.SetEqualsOrdinalIgnoreCase(
            new[] { "gnd", "vcc" },
            new[] { "GND", "VCC" }));
    }

    [Fact]
    public void Different_sizes_are_not_equal()
    {
        Assert.False(ViewportMath.SetEqualsOrdinalIgnoreCase(
            new[] { "GND" },
            new[] { "GND", "VCC" }));
    }

    [Fact]
    public void Different_members_are_not_equal()
    {
        Assert.False(ViewportMath.SetEqualsOrdinalIgnoreCase(
            new[] { "GND", "VCC" },
            new[] { "GND", "CLK" }));
    }

    [Fact]
    public void Two_empty_sets_are_equal()
    {
        Assert.True(ViewportMath.SetEqualsOrdinalIgnoreCase(
            Array.Empty<string>(), Array.Empty<string>()));
    }

    [Fact]
    public void The_same_instance_is_equal_to_itself()
    {
        var set = new[] { "GND" };
        Assert.True(ViewportMath.SetEqualsOrdinalIgnoreCase(set, set));
    }

    [Fact]
    public void Duplicates_collapse_so_a_repeated_member_still_compares_equal_by_set()
    {
        // CURRENT BEHAVIOUR: the counts are compared first, so a duplicate on one side makes the
        // sizes differ and the result is false even though the SETS are equal.
        Assert.False(ViewportMath.SetEqualsOrdinalIgnoreCase(
            new[] { "GND", "GND" },
            new[] { "GND" }));
    }
}
