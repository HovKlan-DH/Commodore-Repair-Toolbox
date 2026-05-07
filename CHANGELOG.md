- Fixed component labels was shown on top of panels
- Fixed selected component did not blink in thumbnails
- Fixed it should not zoom schematic image when inside a panel
- Fixed "Netlist names" panel should not show in schematic images without KiCad data
- Fixed thumbnail opacity can be 100% (pure solid)
- Added `Important signals` panel in "Schematics" tab
- Added visualization of KiCad traces on opposite side
- Added visualization of KiCad zones
- Added visualization of KiCad traces also in schematic images (not only PCB images)
- Changed update banner notification when main Excel data file has newer version
- Changed "Clear All" button is now always visible in "Netlist name" panels, if it has selected traces
- Changed it is possible to pan, so panels in "Schematics" tab does not hide the schematic below it
- Changed KiCad calibration mapping to use interactive trace-map instead of manual calibration points
- Breaking changes:
  - Changed data format for KiCad files
    - Now using original raw KiCad data files instead of a JSON file with converted data
  - Changed data format for highlighted component bounds
    - Moved from board Excel sheet to JSON file
  - Changed data format for KiCad calibration points
    - Moved from board Excel sheet to JSON file

> [!CAUTION]
> Due to breaking data format changes, the major version has been increased to reflect this is important.
> No more data updates will be given for versions **below 2.0.0**, and as future versions may break current
> data, then you are recommended to update.
