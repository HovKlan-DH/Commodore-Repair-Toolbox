- Added icon for showing status and to refresh online data




- **Application**
  - Fixed zoom-in keeps its mouse/image position at first zoom-in
  - Fixed "Clear" button did not clear component filter input field
  - Fixed board labels (if visible) are shown on top of traces and pads
  - Fixed (again) `ESCAPE` did not close active component info popup in Linux, if set to "Open multiple windows for popup" #84
  - Fixed horizontal splitter in "Schematics" tab is shown in correct position at application launch
  - Fixed (experimental) that **macOS** trackpad was not working properly
  - Added "Global settings" panel in "Schematics" tab and moved some functionalities to it
  - Added option how KiCad traces should be highlighted in new "Global settings" panel
  - Added option to highlight first pin on component, if schematic has KiCad data
  - Added user configurable theme colors (editable through configuration file)
  - Changed all components can be selected or highlighted in "Schematics" image, not being dependent on selected categories any more
  - Changed KiCad calibration points only includes pads and reference texts (only relevant for contributors using KiCad)
  - Changes in component label editor:
    - Added component multi-select for move and resize
    - Added component snap-align on move and resize
    - Added component duplication with the letter `D`
    - Added an "undo" and "redo" stack with `CTRL`+`Z` and `CTRL`+`Y`
    - Added banner to show when in this mode
    - Changed component search filter will now also search for components when inside component label editor
    - Changed it will disable online data updates if any changes have been applied, to prevent data loss of local file
    - Changed it will reuse category when creating new component
    - Changed it is no longer possible to change to another schematic, board or hardware
    - Changed YouTube video for showcasing the component label editor:
      - [How to use component label editor in CRT](https://youtu.be/u-UkD-m4Z6o)
  - Removed "Data contribution" checkboxes in "Configuration" tab
    - Same functionality now applies when checking the "Enable contributor mode" in the "Global settings" panel
  - Refactored parts of code for better performance and minor text changes
- **Data**
  - **Commodore 64**
    - **KU-14194HB**
      - Added new board (has KiCad data)
    - **250407**
      - Reexported images from KiCad in a better quality
      - Relabeled all images
    - **250469**
      - Reexported images from KiCad in a better quality
      - Relabeled all images

> [!CAUTION]
> Due to major update of Avalonia UI then Windows 32-bit is no longer supported 😥
> The newest available .NET LTS (Long-Term Support) will still be included for all 64-bit packages.
