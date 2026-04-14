- **Application**
  - Fixed (again) `ESCAPE` did not close active component info popup in Linux, if set to "Open multiple windows for popup" #84
  - Fixed horizontal splitter in "Schematics" tab is shown in correct position at application launch
  - Added option for how KiCad traces should be highlighted on hover in "Configuration" tab
    - You can choose to only highlight traces on hover when holding down `SHIFT`
  - Added option for highlighting first pin on component, if schematic has KiCad data
  - Added user configurable theme colors (editable through configuration file)
  - Changed all components can now always be selected or highlighted in "Schematics" image, not being dependent on category any more
  - Changes in "Enable component label editor":
    - Added component multi-select
    - Added component snap-align
    - Added an "undo" and "redo" stack with `CTRL`+`Z` and `CTRL`+`Y`
  - Changed KiCad calibration points only includes pads and reference texts (only relevant for KiCad contributors)
  - Refactored zooming/panning with many components selected has better performance
- **Data**
  - **Commodore 64**
    - **250407**
      - Reexported KiCad images in a better quality
      - Relabeled all KiCad images
    - **250469**
      - Reexported KiCad images in a better quality
      - Relabeled all KiCad images

> [!CAUTION]
> Due to major update of Avalonia UI then Windows 32-bit is no longer supported 😥
> The newest available .NET LTS (Long-Term Support) will still be included for all 64-bit packages.


- Added interactive KiCad traces and netlists for these boards:
  - **Commodore 64**
    - **250407 (long board)**
    - **250469 (short board)**
- Added YouTube video for showcasing interactive traces functionalities:
  - [How to use interactive traces in CRT](https://youtu.be/Y55nC_gJbH4)
- Added "Board settings" panel in "Schematics" tab
- Added "Print component list" in "Overview" tab
- Changed it will only show selected categories/components in "Overview" tab
- Changed it now highlights the component when hovering mouse over it
- Refactored parts of code for better performance
