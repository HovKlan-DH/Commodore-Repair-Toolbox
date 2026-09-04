- **Application**
  - Added "Workbook" feature for tracking repairs
  - Added option to add a new component in "Contribute" tab
  - Added option to delete an existing component in "Contribute" tab (available on the specific component)
  - Changed a contribution via "Contribute" tab can only be done with version `2.5.0` or newer
  - Changed info banner shows specifically if it is using BETA as source when downloading data updates
  - Fixed manifest file is now compressed on backend, resulting in faster execution of the online data check
  - Fixed timeout was too low when fetching manifest file
  - Fixed configuration for test cases, which should not be included when compiling using `dotnet publish`
  - Refactored "Contribute" tab and its backend server review process
  - Refactored parts of code base for more test coverage


Hvilke typer tests er der i projektet? Unit Test, Smoke Test, UI headless test (hvad betyder det)?
End-report show different test covertage

Check for duplicate code and make part of end-report

make sure ALL crashes/stacktraces is caught in logfile!

new memory - even if I ask for something, then do reason with me if this is not feasible either due to complexity or it seems unsupported.
