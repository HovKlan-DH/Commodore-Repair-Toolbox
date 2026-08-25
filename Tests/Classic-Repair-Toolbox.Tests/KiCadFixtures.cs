namespace ClassicRepairToolbox.Tests;

// Minimal but structurally real KiCad file content used by the loader tests. These are hand
// written rather than copied from Assets/Data so each test can isolate one feature, and so the
// suite does not depend on any particular board's contributed data staying unchanged.
internal static class KiCadFixtures
{
    // A board with two nets, one 14-pin-style footprint, a track, a via, an arc and a zone.
    public const string Pcb = """
    (kicad_pcb (version 20221018) (generator pcbnew)
      (net 0 "")
      (net 1 "GND")
      (net 2 "/Sheet1/CLK")

      (footprint "Package_DIP:DIP-14" (layer "F.Cu")
        (at 100 50)
        (fp_text reference "U1" (at 0 -5) (layer "F.SilkS"))
        (pad "1" thru_hole rect (at -3.81 -7.62) (size 1.6 1.6) (layers "*.Cu" "*.Mask") (net 1 "GND"))
        (pad "2" thru_hole oval (at -2.54 -7.62) (size 1.6 1.6) (layers "*.Cu" "*.Mask") (net 2 "/Sheet1/CLK"))
      )

      (segment (start 100 50) (end 110 50) (width 0.25) (layer "F.Cu") (net 1))
      (segment (start 110 50) (end 110 60) (width 0.25) (layer "B.Cu") (net 2))
      (via (at 105 55) (size 0.8) (drill 0.4) (layers "F.Cu" "B.Cu") (net 1))
      (arc (start 120 50) (mid 122 52) (end 124 50) (width 0.25) (layer "F.Cu") (net 2))

      (zone (net 1) (net_name "GND") (layer "B.Cu")
        (polygon (pts (xy 0 0) (xy 10 0) (xy 10 10) (xy 0 10)))
        (filled_polygon (pts (xy 1 1) (xy 9 1) (xy 9 9) (xy 1 9)))
      )
    )
    """;

    // KiCad 7+ writes the reference as a (property "Reference" ...) instead of (fp_text reference ...).
    public const string PcbWithPropertyReference = """
    (kicad_pcb (version 20240108) (generator pcbnew)
      (net 1 "VCC")
      (footprint "R_0805" (layer "F.Cu")
        (at 10 10)
        (property "Reference" "R1")
        (pad "1" smd rect (at -1 0) (size 1 1) (layers "F.Cu") (net 1 "VCC"))
      )
    )
    """;

    // Pre-footprint KiCad used (module ...) even inside a .kicad_pcb file.
    public const string PcbWithLegacyModule = """
    (kicad_pcb (version 20171130)
      (net 1 "GND")
      (module "DIP-8" (layer F.Cu)
        (at 20 20)
        (fp_text reference "U9" (at 0 0) (layer F.SilkS))
        (pad "1" thru_hole circle (at 1 1) (size 1 1) (layers *.Cu) (net 1 "GND"))
      )
    )
    """;

    // A footprint placed on the bottom copper layer. KiCad flips a footprint by rewriting the
    // pads' stored local coordinates, so what the file holds is already the flipped geometry and
    // the loader must not mirror it a second time.
    public const string PcbBottomLayerFootprint = """
    (kicad_pcb (version 20221018)
      (net 1 "GND")
      (footprint "DIP-8" (layer "B.Cu")
        (at 100 100)
        (fp_text reference "U2" (at 0 0) (layer "B.SilkS"))
        (pad "1" thru_hole circle (at 5 3) (size 1 1) (layers "*.Cu") (net 1 "GND"))
      )
    )
    """;

    // Rotated rectangular and oval pads. The footprint sits at 90 degrees, so KiCad writes 90 into
    // each pad's own (at ...) too; pad 3 additionally carries a further 90 of its own, giving 180.
    public const string PcbRotatedPads = """
    (kicad_pcb (version 20221018)
      (net 1 "GND")
      (footprint "Connector" (layer "F.Cu")
        (at 50 50 90)
        (fp_text reference "CN1" (at 0 0) (layer "F.SilkS"))
        (pad "1" smd rect (at 0 0 90) (size 2 0.8) (layers "F.Cu") (net 1 "GND"))
        (pad "2" thru_hole oval (at 2 0 90) (size 2 0.8) (layers "*.Cu") (net 1 "GND"))
        (pad "3" smd roundrect (at 4 0 180) (size 2 0.8) (layers "F.Cu") (net 1 "GND"))
        (pad "4" smd rect (at 6 0) (size 2 0.8) (layers "F.Cu") (net 1 "GND"))
      )
    )
    """;

    // A footprint rotated 90 degrees: pad offsets rotate with it.
    public const string PcbRotatedFootprint = """
    (kicad_pcb (version 20221018)
      (net 1 "GND")
      (footprint "DIP-8" (layer "F.Cu")
        (at 100 100 90)
        (fp_text reference "U3" (at 0 0) (layer "F.SilkS"))
        (pad "1" thru_hole circle (at 10 0) (size 1 1) (layers "*.Cu") (net 1 "GND"))
      )
    )
    """;

    public const string Schematic = """
    (kicad_sch (version 20230121) (generator eeschema)
      (uuid "11111111-2222-3333-4444-555555555555")
      (wire (pts (xy 10 10) (xy 20 10)))
      (wire (pts (xy 20 10) (xy 20 20)))
      (label "CLK" (at 15 10 0))
      (global_label "GND" (at 20 20 0))
      (symbol (lib_id "Device:R") (at 30 30 0)
        (property "Reference" "R1" (at 30 28 0))
        (property "Value" "10k" (at 30 32 0))
      )
    )
    """;
}
