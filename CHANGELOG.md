- **Application**
  - Added "Workbook" feature for tracking repairs
  - Added option to add a new component in "Contribute" tab
  - Added option to delete an existing component in "Contribute" tab (available on the specific component)
  - Changed a contribution via "Contribute" tab can only be done with version `2.5.0` or newer
  - Changed info banner shows specifically if it is using BETA as source when downloading data updates
  - Fixed manifest file is now server compressed before being fetched, resulting in faster execution of this check
  - Fixed timeout was too low when fetching manifest file
  - Fixed minor configuration for test cases, which should not be included when compiling using `dotnet publish`
  - Refactored "Contribute" tab and its backend server review process
  - Refactored parts of code base for more test coverage


  Config tab
    - rename worklog til Workbook
    - rename Misc til Wirkbook

  I workbooks tab:
    - hver worklog (i højre side) skal være clickable
    - hvir worklog (samme sted) skal være outlined i samme farve som deres kategori - ligeledes skal status være ens alle steder
    - mangler en summary per board
    - eksport til .. ?

Hvilke typer tests er der i projektet? Unit Test, Smoke Test, UI headless test (hvad betyder det)?
