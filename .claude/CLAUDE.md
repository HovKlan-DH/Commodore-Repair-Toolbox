# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Classic Repair Toolbox (CRT) is a cross-platform (Windows/Linux/macOS) Avalonia desktop app that helps
hardware enthusiasts diagnose and repair vintage computers. It presents schematics, component data,
oscilloscope baselines, and interactive KiCad traces for a curated set of hardware (mostly Commodore,
plus Amstrad and ZX Spectrum boards). It also drives real test equipment: SCPI oscilloscopes over TCP
and a MiniPro USB IC programmer/tester.

## Hands off CHANGELOG.md

**Never create, edit, rewrite, reformat or delete [CHANGELOG.md](../CHANGELOG.md) unless the
maintainer explicitly asks for it in that message.** It is written by hand, in the maintainer's own
words, and it is the body of every GitHub Release — an "improvement" there is not a small edit, it
is words the maintainer never wrote going out under their name.

This holds even when a change would normally warrant a changelog entry, and even when the file
already has uncommitted edits in it (those are the maintainer's, in progress). Do not touch it as a
"finishing touch" on a feature, do not tidy its formatting, and do not stage, commit, revert or
`git checkout` it. If you think an entry is needed, say so in your summary and let the maintainer
write it. "Update the changelog" from the maintainer is the only permission — and it covers that
one request, not the rest of the session.

## Build, test, run, publish

There is a unit test suite covering the UI-free logic in `Handlers/`. **Always run it, always add
tests for new logic, and always update the existing tests when you change covered behaviour** — see
[Tests](#tests) below for the full rules. UI behaviour has no automated coverage, so changes to the
tabs and overlays still need the app built and run by hand (see [BUILDING.md](../BUILDING.md) for full
per-OS instructions).

- **Run the tests: `dotnet test Classic-Repair-Toolbox.slnx`** (~2s; needs no hardware and no display)
- Build (Release, matches CI): `dotnet build Classic-Repair-Toolbox.slnx -c Release`
- Build (Debug, default): VS Code task `build`, or `dotnet build Classic-Repair-Toolbox.slnx`
- Run/iterate: VS Code task `watch` (`dotnet watch run --project Classic-Repair-Toolbox.slnx`), or F5 in
  VS Code (`.vscode/launch.json`), or open `Classic-Repair-Toolbox.slnx` in Visual Studio
- Self-contained publish for a specific OS: `dotnet publish -c Release -f net10.0 -r <rid> --self-contained`
  where `<rid>` is one of `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`
- **The build configuration does not change what the app does.** A DEBUG build and a RELEASE build
  given the same arguments behave identically — same update check, same data sync, same diagnostics.
  Nothing may be gated on `#if DEBUG`; the one remaining use is `AppConfig.IsDebugBuild`, which is
  reported in the log and read by nothing else. RELEASE still matters for *timings* (DEBUG is
  JIT-only) and for warnings-as-errors, not for behaviour.
- **Command-line switches**, all parsed once at startup:
  - `--data-root=<path>` — use a different `Data` folder (default: next to the executable).
  - `--simulate-update[=<version>]` — offer a fake application update (default `99.0.0`), fake the
    download and skip the restart, so the update banner can be exercised without a release. See
    [Handlers/Data/SimulationOptions.cs](../Handlers/Data/SimulationOptions.cs). Active in RELEASE
    builds too, on purpose; the startup log shouts about it and the banner says `(simulated)`.
  - Both are set for F5 and the `watch` task in [.vscode/launch.json](../.vscode/launch.json) and
    [.vscode/tasks.json](../.vscode/tasks.json).
- **To skip the online data sync while iterating, untick "Check for new or updated data at
  application launch" in the Configuration tab.** It is a normal user setting, not a build or
  command-line concern — it short-circuits the manifest fetch, and with no manifest the board-Excel
  sync and the background image sync skip themselves too.

## Tests

`Tests/Classic-Repair-Toolbox.Tests/` (xUnit) covers the UI-free logic in `Handlers/`. It needs no
oscilloscope, no MiniPro programmer and no display, and runs in about two seconds.

**Tests are part of the change, not a follow-up. These rules are not optional:**

1. **ALWAYS write tests for logic you add.** Any new function that parses, maps, validates,
   compares, or does geometry/maths arrives *with* its tests in the same change — never "tests can
   come later". If it is pure, it goes in `Handlers/` (see `Handlers/Geometry/` for logic pulled out
   of a tab) and it gets a test file. Do not ask whether to add tests; add them.
2. **ALWAYS update the existing tests when you change covered behaviour.** If you touch a class that
   has tests, updating them is part of that same change. Never leave a red suite, never defer the
   update, and never delete a test to make a change pass.
3. **ALWAYS run `dotnet test Classic-Repair-Toolbox.slnx` before reporting any code change as done.**
   "It compiles" is not a completed change. Report the real result, including failures and counts.
   **This rule is machine-enforced, not a courtesy.** The `Stop` hook in [settings.json](settings.json)
   runs [hooks/require-green-tests.sh](hooks/require-green-tests.sh) when you try to end a turn, and a
   red suite blocks the handover with the failing tests fed back to you. It builds Release (matching
   CI) and only fires when `.cs`, `.csproj`, `.axaml` or `.slnx` files actually changed, so it is free
   on turns that touch no code. GitHub then runs the suite again on every push
   ([.github/workflows/build-and-unittest.yml](../.github/workflows/build-and-unittest.yml)), and a
   red suite blocks releases too.
4. **A failing test is a question, not an obstacle.** Decide whether the behaviour change was intended.
   If it was, update the expectation and say so explicitly in your summary. If it wasn't, fix the code.
   Never edit an assertion just to get to green, and never weaken one (e.g. loosening a tolerance or
   dropping a case) to avoid understanding a failure.
5. **When you fix a bug, first add the test that fails because of it.** Then fix the code and show
   the test going green. That test is the thing that stops the bug coming back.
6. **Never write a test that needs hardware, a network call, a display, or that starts a process.**
   Headless UI tests do not breach this - Avalonia's headless platform needs no display - but they
   must go through `UiTest.Run(...)`; see [Headless UI tests](#headless-ui-tests).
   `ExternalTargetLauncher`'s accept path calls `Process.Start`, so its tests exercise the private
   containment predicates by reflection instead; the header comment in `ExternalTargetLauncherTests.cs`
   explains the reasoning. Follow the same principle for anything else with real-world side effects.
   If logic is untestable because it is welded to a control, extract the logic (rule 1) rather than
   giving up on testing it.
7. **These are characterisation tests** — they pin down what the code does today so future changes
   cannot alter it silently. Several encode deliberate quirks (relative-tolerance value matching, "no
   vector grid + a successful summary IS a pass", micro sign vs Greek mu, the dead `region` argument
   in `BoardDataWriter`). Read the comment before assuming a test is wrong.

**Writing them:** one test file per class under test, named `<ClassName>Tests.cs`. Give each test a
sentence-shaped name saying what must hold (`A_faulty_chip_fails_and_names_the_failing_pin`), and put
a comment above anything non-obvious explaining *why* it matters — a reader should learn the rule from
the test. Use `TempWorkspace` for filesystem work. Cover the failure and edge cases, not just the
happy path: blank input, malformed contributed data, wrong region, wrong board side, locale-specific
number formats.

### What is covered

| Area | Classes |
| --- | --- |
| Oscilloscope | `ScopeValueMapper`, `ScopeCommandResolver`, `ScopeCommandPaletteDefinitions`, `ScopeFormatting`, `ScopePayloadParser` |
| IC testing | `MiniproOutputParser`, `IcTestService` (via `MockMiniproRunner` and local test doubles) |
| Security | `ExternalTargetLauncher`, `OnlineServices`' manifest-validation predicates |
| KiCad | `KiCadRawProjectLoader`, `KiCadProjectLoader`, the `KiCadProjectData` model |
| Board data | `BoardDataReader`, `BoardDataWriter`, `BoardComponentHighlightStorage`, `ComponentListBuilder`, `ComponentImageQueries`, `OverviewHtmlBuilder`, `ContactLinkFormatter` |
| Worklog | `WorklogManager` (including `ResolveActiveWorkbook`, `IsResolvedState`, `IsWorkbookStatusOpen`), `WorklogEntryScope` |
| Settings / startup | `UserSettings`, `DataManager` (data-root + master workbook), `DataValidator` (smoke only), `SimulationOptions` |
| Headless UI (`Tests/.../Ui/`) | All nine tabs built headlessly, the worklog and Workbooks palettes, plus component highlight selection and schematics zoom - see [Headless UI tests](#headless-ui-tests) |
| Geometry (`Handlers/Geometry/`) | `PolygonGeometry`, `RectGeometry`, `KiCadLayerGeometry`, `KiCadPadGeometry`, `OverlayCullGeometry`, `KiCadOverlayCacheKeys`, `KiCadOverlayNetCache`, `ViewportMath`, `KiCadNetGraphBuilder`, `KiCadHoverIndex`, `HighlightRectBuilder`, `LabelEditorGeometry`, `WorklogBadgeLayout` |

`Handlers/` is where the real coverage is; most of the uncovered remainder is `Tabs/` and `Main/`,
Avalonia code-behind that is verified by running the app.

**No coverage percentage is recorded here on purpose** - a figure in a document goes stale silently
and then gets quoted as fact. Measure it when you actually need it:

```
dotnet test Classic-Repair-Toolbox.slnx --collect:"XPlat Code Coverage"
```

and read `lines-covered` / `lines-valid` from the `coverage.cobertura.xml` it writes under
`Tests/.../TestResults/`. **Always quote the denominator alongside the percentage, and say which
build configuration you used** - Debug and Release instrument different numbers of lines (~26.7k vs
~21.4k), so two bare percentages from different configurations are not comparable.

### `Handlers/Geometry/` — pure logic pulled out of the UI

This folder exists because ~2,000 lines of genuinely pure logic were trapped as `private`
members of `TabSchematics`, where no test could reach them: polygon and zone maths, KiCad layer
filtering, the 550-line net-graph builder that decides which copper belongs to which net, the
spatial hover index, highlight rect building and label-editor handle geometry.

**When you add pure logic to a tab, put it here instead.** These classes use Avalonia's
`Point`/`Rect`/`Matrix` value types but never touch a control, so they test with no display.
`KiCadRenderNodes.cs` holds the DTOs they share (formerly nested private types).

The same extraction has since been done for the other tabs, into the area folder that fits rather
than into `Geometry/`: `Handlers/Oscilloscope/ScopeFormatting.cs` and `ScopePayloadParser.cs` (from
`TabOscilloscope`), and `Handlers/Data/ComponentListBuilder.cs`, `ComponentImageQueries.cs`,
`OverviewHtmlBuilder.cs` + `OverviewModels.cs`, `ContactLinkFormatter.cs` (from `Main`,
`ComponentInfoWindow`, `TabOverview` and `TabAbout`). **Match that pattern: geometry goes in
`Geometry/`, everything else goes in the `Handlers/` folder for its area.**

That sweep also removed four copies of logic that already existed in `Handlers/`
(`ComputeWheelZoomFactor`, `GetImageContentRect`, `PixelToLocalRect`, and a second
oscilloscope-title-suffix stripper). Before writing a helper in a tab, grep `Handlers/` for it —
these duplicates all arose from someone re-implementing a helper they could not see.

### Test seams — use these, do not work around them

Three classes are static singletons that would otherwise read and write the user's real files. Each
has an `internal` seam; the test project sees them via `InternalsVisibleTo` in the csproj.

- `UserSettings.LoadFrom(path)` — `Load()` resolves the AppData path and delegates here. Tests point
  it at a temp file. **Never call `UserSettings.Load()` from a test.**
- `DataManager.LoadFrom(dataRoot, workbookName)` — the local half of `InitializeAsync`, no network and
  no seeding. **Never call `DataManager.InitializeAsync()` from a test.**
- `Logger` writes nothing until `Logger.Initialize()` is called, and no test calls it — which is why
  the `Logger.*` calls inside classes under test are inert. **Never call `Logger.Initialize()` from a
  test**, or the suite starts writing to the user's real log file.
- `WorklogManager.LoadFrom(root)` — the same idea for the local "Workbook" folder. **Never call
  `WorklogManager.Load()` from a test.** `TabWorkbooks.BoardKeyOverrideForTests` is the matching
  seam on the UI side: the Workbooks tab normally reads its board key off `Main`, which no test
  constructs, so the override lets the list be tested without standing up the main window.

Use `TempWorkspace` for anything that touches the filesystem; it creates and deletes a temp folder.
Tests that mutate `UserSettings` or `DataManager` static state live in the `"UserSettings"` and
`"DataManager"` xUnit collections so they run sequentially. `BoardDataReader` has the same
problem — its loaded boards sit in a shared static cache (a `ConcurrentDictionary`, so thread-safe,
but still one cache that tests clear and repopulate) — so `BoardDataReaderTests`
and `BoardDataWriterTests` share the `"BoardData"` collection.
**Any new test class that touches one of these statics must join its collection**, or it will
pass alone and fail intermittently in the full run.

**Collections do not run in parallel with each other.** `Tests/.../xunit.runner.json` sets
`"parallelizeTestCollections": false`. A class can only join ONE collection, and the headless UI
tests must be in `"HeadlessUi"` to share the dispatcher thread - which left `WorkbooksListTests`
racing the `"UserSettings"` collection over `UserSettings`' shared `_data` object and save path, with
unique dictionary keys giving no protection at all. Serialising every collection costs about five
seconds on a ~40s suite and removes the whole class of race. Keep that file.

### Headless UI tests

`Tests/Classic-Repair-Toolbox.Tests/Ui/` builds every tab through Avalonia's headless platform -
no display, no GPU, so it runs on CI like any other test. Two of the files are the harness:
`TestAppBuilder.cs` (a `CRT.App` subclass whose `OnFrameworkInitializationCompleted` is deliberately
empty, since the real one calls `Logger.Initialize()`, shows a splash and syncs over the network)
and `UiTest.cs` (runs a body on the UI thread). The rest are the tests themselves:

| File | Covers |
| --- | --- |
| `TabConstructionTests.cs` | Every tab constructs without throwing |
| `ComponentHighlightSelectionTests.cs` | Selecting/deselecting in the component filter box, and the highlights that appear and vanish across the main image and every thumbnail |
| `SchematicsZoomTests.cs` | The schematic viewer's zoom limits, and zoom anchoring - that the point under the mouse pointer stays under the mouse pointer |
| `WorkbooksPaletteTests.cs` | The Workbooks tab's `Workbooks_*` theme keys: that each resolves, that both themes define all of them, and that the pin colours stay identical across themes |
| `WorkbooksListTests.cs` | The Workbooks tab's workbook list and selection: counts and their singular/plural forms, card contents, newest-first order, board scoping, default/click selection, that the status pill (list and top-line) uses the shared Open/Closed brushes, the activation fallback chain (`UserSettings.ActiveWorkbookIdByBoard` wins over newest; a stale saved id falls back to newest), and that both splitters' widths are restored from `UserSettings.WorkbooksLeftPanelWidth`/`WorkbooksEntryListWidth` via `ApplySplitterWidthsForTests` |
| `WorkbooksBoardPreviewTests.cs` | The Workbooks tab's board pane: one preview per schematic with an entry in the selected workbook, shared previews for co-located entries, unknown-schematic and missing-image entries skipped without dropping other previews, the pane rebuilding when the selected workbook changes, (via `BuildShownTab`'s real layout pass) that a "show marked area" ON entry's badge anchors to its marker while an OFF entry's badge parks in the image's top-right corner instead, that a pill carries a Hand cursor on a hit-test-visible canvas, `RefreshBoardPreviewsForCurrentSelection` populating the pane from board data supplied after an earlier empty build without touching the workbook list, schematic selection (default-first, highlight moving on click, the entry list switching to the clicked schematic), an entry detail card's four rows read back by content (including the stats row's hours/cost/comment/link/photo/file counts, populated through `WorklogManager.UpdateEntry` rather than a bare in-memory record), that the card has exactly one outer border with no border wrapping any individual row, that the Legend panel is gone, and `BuildWorklogEntryComponentScopeForTests` (matched components whose highlight rect the entry's area touches, `null` with no cached highlight rects at all and `null` when the entry's own schematic has no cache entry) - the computation `OnPreviewBadgePointerPressed` hands to `WorklogEntryEditorWindow.InitializeComponentScope` so the modal opened from a pill is provably the same one the Schematics tab opens |

**Do NOT add the `Avalonia.Headless.XUnit` package to get `[AvaloniaFact]`.** At 12.1.1 it depends
on xunit **v3** while this suite is on xunit 2.9.3; adding it makes every `Fact` and `InlineData`
in the project ambiguous and produces ~850 build errors. `UiTest.Run(...)` drives the same public
session API directly and keeps xunit 2. Anything touching a control must go through it, or
Avalonia throws for want of a dispatcher.

**Know what these do and do not catch.** The XAML compiler already fails the build on a renamed
`x:Name` (CS1061) and on a broken `avares://` path, and a missing `StaticResource` key is silently
tolerated by Avalonia at runtime - all three were tested. So construction tests do not guard the
markup; what they add is that constructor *logic* cannot throw, plus a foundation for interaction
tests (`HeadlessWindowExtensions` gives `MouseDown`, `KeyPress`, `MouseWheel`). Prefer adding
interaction tests that assert observable state over more construction tests.

### Deliberately not covered

- **Rendering, layout and pointer interaction in `Tabs/` and `Main/`.** The tabs are now built
  headlessly (below), which proves they construct; whether the result *looks* right is still
  verified by running the app. `Main` itself is not constructed by any test.
- **I/O boundary classes**: `OnlineServices`' network half (`FetchManifestAsync`, `SyncFilesAsync`,
  `DownloadFileAsync`), `UpdateService` (real HTTP), `ScopeScpiClient` (real TCP),
  `MiniproProcessRunner` (spawns a process), and `DataManager`'s sync/seed/orphan-cleanup half.
  The abstraction below each of these (`IMiniproRunner`) is the thing to test, not the boundary.
  **`OnlineServices` is only half excluded.** The four predicates that validate a manifest entry
  before anything is written — `TryValidateManifestEntry`, `TryResolveValidatedLocalPath`,
  `TryNormalizeManifestChecksum`, `TryCreateTrustedDownloadUri` — are pure string/`Uri`/`Path` logic
  and *are* covered, by `OnlineServicesTests` via reflection (the same approach and the same
  reasoning as `ExternalTargetLauncherTests`). They decide where a downloaded file lands and which
  server it may come from, on input that arrives over the network, so they are a trust boundary
  rather than an I/O one.
- **`DataValidator`'s findings.** `ValidateAllDataAsync` returns a bare `Task` and reports everything
  through `Logger`, so the tests only prove it walks real data without throwing. Testing what it
  actually detects means changing it to return its findings — a public API change, and a decision for
  the maintainer rather than something to do in passing.

That extraction has been done — see `Handlers/Geometry/` above. What is left inside `TabSchematics`
is genuinely UI: event handlers, control updates and rendering. Two methods that look pure are not
(`GetOrCreateKiCadSchematicHoverHitTestCache` and `HitTestKiCadSchematicOverlayForHover` both read
instance caches and the view matrix), so they stayed put.

The same sweep has now been done across the other tabs. What remains in `Tabs/` and `Main/` was
checked and is genuinely UI-bound, so **do not go looking for more to extract there** — the
candidates that look pure are not:

- **`TabSchematics.LabelEditor.Snap.cs`** (~970 lines of snapping maths, the single largest block of
  maths left). It reads `SchematicsContainer.Bounds`, `currentFullResBitmap` and `schematicsMatrix`
  directly, so extracting it means changing its signature, not moving it. Worth doing one day;
  it is real surgery, not a lift-and-shift, and it needs its own change.
- **`TabSchematics.KiCad.Geometry.cs`** — the world↔local mapping reads `currentFullResBitmap` for
  the calibration offset scale.
- **Thumbnail bitmap builders** (`CreateScaledThumbnail`, `CreateHighlightedThumbnail`) — these need
  `RenderTargetBitmap` and therefore a display.
- **`TabConfiguration`'s launchers and `ComponentContribution.BuildPayload`** — `Process.Start` and
  zip/file I/O respectively, which rule 6 puts out of scope.

The test project lives inside the app's project folder, so [Classic-Repair-Toolbox.csproj](../Classic-Repair-Toolbox.csproj)
excludes `Tests/**` from its compile glob. Leave that exclusion in place.

## Code layout conventions

- **No MVVM.** UI logic lives directly in `.axaml.cs` code-behind files throughout the project, not in
  separate ViewModels.
- **Large controls are split into partial-class files** named `<Class>.<Area>.cs`, sitting beside the
  `.axaml.cs` (see the `TabSchematics.*` set). Aim to keep any one file under ~1,500 lines; when a file
  outgrows that, add another partial rather than letting it grow.
- **Every partial file opens with a header comment** stating what that part owns, and the `.axaml.cs`
  part carries the file map for the whole class. When you add, rename or move a part, update the map in
  the `.axaml.cs` header too.
- **Fields are declared in the part that owns them**, not collected at the top of the class. The parts
  are one class, so state is still shared across them.
- **Pure logic does not belong in a tab.** If a method touches no Avalonia control, put it in
  `Handlers/` (`Handlers/Geometry/` for maths and geometry) as a plain static class, not as a private
  member of a `UserControl` — that is the difference between logic that can be tested and logic that
  cannot. It then gets tests, per the [Tests](#tests) rules.
- Biggest files right now, for context budgeting: [Tabs/Oscilloscope/TabOscilloscope.axaml.cs](../Tabs/Oscilloscope/TabOscilloscope.axaml.cs)
  (~3,400 lines), [Main/Main.axaml.cs](../Main/Main.axaml.cs) (~2,700),
  [Tabs/Schematics/ComponentInfoWindow.axaml.cs](../Tabs/Schematics/ComponentInfoWindow.axaml.cs) (~1,800),
  [Handlers/Data/DataManager.cs](../Handlers/Data/DataManager.cs) (~1,600). Read the part you need rather
  than the whole file.

## Architecture

### Bootstrap (`Main/`)

- [Main/Program.cs](../Main/Program.cs) — entry point; initializes Velopack then starts Avalonia.
- [Main/App.axaml.cs](../Main/App.axaml.cs) — contains `AppConfig`, a static class holding nearly every
  tunable value in the app (file/folder names, sync URLs, timeouts, zoom limits, debug flags, version
  helpers). **Check here first before hardcoding a new constant elsewhere.** `App` itself wires up theme
  application (including JSON-defined user-preference theme colors), global exception logging, and the
  startup sequence: show `Splash` → `DataManager.InitializeAsync` (loads/syncs hardware data) → open
  `Main` window → fire-and-forget version check-in.
- [Main/Main.ModeHint.cs](../Main/Main.ModeHint.cs) — the khaki "what to do next" label in the tab-header
  row, shown while a mode (e.g. worklog area-marking) is waiting for the user to act. `ShowModeHint`/
  `HideModeHint`; it clears itself on the first pointer press.
- [Main/Main.axaml.cs](../Main/Main.axaml.cs) — the main window's code-behind (~2,700 lines). It acts as
  the central controller coordinating board selection, schematics zoom/pan/thumbnails, and cross-tab
  state. It reaches directly into `TabSchematicsControl` members (`currentThumbnails`,
  `highlightIndexBySchematic`), so changes to those ripple here.

### Tabs (`Tabs/`)

One folder per UI tab — `About`, `Configuration`, `Contribute`, `Feedback`, `Oscilloscope`, `Overview`,
`Resources`, `Schematics`, `Workbooks` — each an Avalonia `UserControl` (`.axaml`) with its logic in
the paired `.axaml.cs`. `Worklog/` is the odd one out: it holds the worklog's dialog windows, not a
tab.

Two tabs are conditional, both hidden by a Configuration checkbox and both shown from `Main.axaml.cs`:
`Oscilloscope` (`ApplyOscilloscopeTabVisibility`) and `Workbooks` (`ApplyWorklogBarVisibility`, which
drives the tab and the worklog bar together since they are one feature). Each moves selection to the
first still-visible tab when it is hidden while selected.

**`Tabs/Workbooks/` is partway from mockup to functional** — concept "C; Worklog tab" from
[Assets/UI mockups/worklog-mockup.html](../Assets/UI%20mockups/worklog-mockup.html), built as markup
so the layout can be tweaked in the running app, with real data and behaviour landing incrementally
on top of it. Split across `TabWorkbooks.axaml(.cs)` and `TabWorkbooks.BoardPreviews.cs` (the
`.Board Previews` partial owns the board pane specifically — see its own header).

- **Real:** the left-hand workbook list (`RefreshWorkbooks`, one card per
  `WorklogManager.GetWorkbooksForBoard(boardKey)` result); clicking a card **activates** that
  workbook app-wide (`SelectWorkbook`, see "Activation" below) WITHOUT leaving this tab - the
  top-line and the board pane update in place for the newly active workbook; the board pane
  (`RefreshBoardPreviews`, in `TabWorkbooks.BoardPreviews.cs`) — every schematic image with one or
  more entries in the ACTIVE workbook, drawn with a 1px black outline so its boundary reads clearly
  against the pane. Each entry is drawn one of two ways, mirroring `TabSchematics.Worklog.cs`'s own
  `ShowMarkedArea` branch exactly: ticked gets a dashed bounds rectangle on the schematic tab's own
  `WorklogEntriesOverlay` plus its "#N" badge anchored to that area; unticked gets NO rectangle, and
  its badge is parked in the image's own top-right corner instead (`ParkedBadgeGeometry.ArrangeInTopRightBlock`,
  the same geometry the real Schematics tab's parked pills use), stacking with any other parked
  badges rather than overlapping them. Getting this backwards - anchoring every badge to its marker
  regardless of `ShowMarkedArea` - was a reported bug; `WorkbooksBoardPreviewTests` pins both
  branches down by actual on-screen position, not just presence, specifically to catch a
  regression like it. **Every pill is clickable** (`OnPreviewBadgePointerPressed`), opening the
  EXACT SAME `WorklogEntryEditorWindow` the Schematics tab's own "Show worklogs" badges open,
  including the "Mark components in scope"/"Mark components completed" checklist
  (both tabs now call the one shared `WorklogEntryScope.BuildComponentsInScope` in `Handlers/Data/`,
  rather than the near-identical copy each used to carry) - an earlier version omitted that section because it needs a highlight-rect cache
  only `TabSchematics` built; reported as the two modals not actually being identical, and fixed by
  reading that SAME cache off `MainWindow.TabSchematicsControl.highlightRectsBySchematicAndLabel`
  (via the `HighlightRectsBySchematicAndLabelForPreviews`/`...OverrideForTests` seam, the same
  override-then-real-`MainWindow` pattern `CurrentBoardDataForPreviews` already used) rather than
  building a second copy - `Main.ApplyRegionFilterAsync` already keeps that cache current on every
  board load and region switch regardless of which tab is selected, so nothing new has to be kept
  in sync. **Clicking anywhere else on a preview** (not a pill - `OnPreviewBadgePointerPressed` sets
  `e.Handled` so the two clicks cannot both fire) **selects that schematic** (`SelectSchematic`): a
  highlighted border (the same IndianRed `Main_TabUnderline_Selected` accent the selected workbook
  card uses) and the entry list on the right (marker 4) switches to that schematic's entries, each
  rendered by `BuildEntryDetailCard` as ONE 1px-bordered card - not one border per field, a reported
  regression from an earlier three-separately-bordered-panel layout - holding four stacked rows:
  `"#{N} {Title}"` (the "#N" a small filled badge in the category colour, exactly like
  `WorklogEntryEditorWindow`'s `EditorIdBadge` - filled is still right here, since it names which
  workbook entry this is, not a selection state), the description, a category chip and status pill,
  then a stats row. The category chip and status pill (`BuildFilledCategoryChip`/`BuildFilledStatePill`,
  renamed from `BuildFilled*`, which described the opposite of what they build) render in the
  UNSELECTED/outlined visual `WorklogEntryEditorWindow` itself uses
  for a NOT-currently-chosen category chip / state pill (`Form_Bg` background, a 1px `Form_Border`
  outline) rather than that window's filled "selected" look - reported explicitly: this list has no
  selection concept the way a click in the full editor does, so a filled pill here would falsely
  read as "this is the chosen one." The stats row (`BuildEntryStatsRow`) sums total hours and cost
  across the entry's `WorkDoneItems` (`"{hours} h"` / the bare cost number, matching
  `WorklogEntryEditorWindow`'s own `SummaryText` formatting exactly - no currency symbol) plus how
  many comments, links, photos and files the entry carries - one number each, added because a
  workbook's worth of pills gave no sense of how much was behind each one without opening it.
  Deliberately not the smaller dot-plus-label the board pane's own pills use for category, and not
  the anchor-tag/timestamp/photo layout the mockup drew there, both gone. Defaults to the
  alphabetically-first schematic with entries when nothing is yet selected, and stays on the current
  selection across a rebuild if it is still shown (a save via a pill's editor re-enters
  `RefreshBoardPreviews` and must not reset it) - same "keep if valid, else fall back" rule
  `RefreshWorkbooks` applies to the selected workbook. All real pieces are refreshed from
  `Main.RefreshWorklogBar`, the one funnel every worklog change already passes through, so none of
  them can go stale in a case the bar handles. **The Legend panel the mockup drew under the board
  pane is gone** - removed as unneeded once every pill already named its own category and state.
- **Not wired up:** the "Find a previous repair" field. It is `IsEnabled="False"` with a
  "(not yet available)" placeholder rather than left looking functional - a fully styled search box
  that silently filters nothing is indistinguishable from a broken one. Re-enable it in the same
  change that gives it a handler.

**Board-data timing.** `Main.OnBoardSelectionChanged` used to call `RefreshWorklogBar` (which
rebuilds this whole tab) BEFORE it awaited `DataManager.LoadBoardDataAsync`, so that pass ran against
the PREVIOUS board's data, or none at all on the session's first board. Reported as "even if my
workbook has data, it does not get reflected in the tab". **That call now sits AFTER
`_currentBoardData` is assigned**, so it runs once, against the right board - the ordering fix rather
than the patch-up second call it originally got. The three early-return paths in that method
(no selection, no data file, unreadable board data) each refresh explicitly, so the tab shows the
board that IS selected rather than the previous one's contents; a SUPERSEDED load deliberately
refreshes nothing, since the newer load owns every surface.

`TabWorkbooks.RefreshBoardPreviewsForCurrentSelection` survives for a different job: it is called
from `Main.SetComponentHighlightRects` whenever the component highlight-rect cache is replaced (a
board load finishing, or a region switch), because the pane's badges are clickable before that cache
is populated and a click in that window silently dropped the editor's component checklist.

**Activation.** Before this, every worklog-facing control (the bar, "Show worklogs", "Add worklog")
always acted on `WorklogManager.GetLatestWorkbookForBoard` - there was no way to point any of them at
an older or closed workbook. Clicking a card in the Workbooks tab now overrides that: `Main`'s
`ActivateWorkbook(boardKey, workbookId)` saves the choice to `UserSettings.ActiveWorkbookIdByBoard`
(per board, persisted) and calls `RefreshWorklogBar`. Creating a workbook activates it too
(`OnWorklogCreateWorkbookClick`); a bare refresh would leave the bar on a previously-activated older
one and write the next drawn entry into it. `ActivateWorkbook` also cancels any in-progress
entry-drawing mode, which captured the OLD workbook's id when it started.

**`WorklogManager.ResolveActiveWorkbook(workbooks, savedActiveId)` is the ONE place "which workbook"
is decided** - saved id if it still names a workbook on this board, else the newest. Pure, unit
tested, and called from both `Main.ResolveActiveWorkbookForBoard` and `TabWorkbooks.RefreshWorkbooks`,
which used to implement the same rule separately in two different shapes.

`TabWorkbooks.SelectWorkbook` has NO "already selected, nothing to do" guard, deliberately:
`RefreshWorkbooks` highlights a default card without saving anything, so the card on screen can look
active while `ActiveWorkbookIdByBoard` is empty, and an id-equality early return made clicking that
exact card a no-op. It DELIBERATELY does not switch tabs - activating a workbook is meant to be seen
on this tab. `Main.SwitchToSchematicsTab` still exists and is still used, just not from here:
`OnWorklogAddEntryClick` calls it, since drawing a new entry needs the real schematic view.

Activation goes through a settable `Action<string, int>` (`thisActivateWorkbook`, set by `Initialize`
to `Main.ActivateWorkbook`) rather than an `if (MainWindow != null)` branch. That matters for tests:
with the branch, EVERY headless test ran the no-`MainWindow` side and the shipped path - persist,
then re-derive the selection from the saved id - was pinned by nothing at all. Tests now inject
`ActivateWorkbookOverrideForTests` and drive the real path.

**Splitters.** Both of this tab's `GridSplitter`s persist their width, the same pattern
`UserSettings.LeftPanelWidth` (`Main`'s own left sidebar) and `TabSchematics.ApplySchematicsSplitterRatio`
follow: `UserSettings.WorkbooksLeftPanelWidth` for the outer one (left workbook list vs. everything
else) and `UserSettings.WorkbooksEntryListWidth` for the inner one (board pane vs. entry list).
Unlike the Schematics tab's splitter these are plain app-wide pixel widths, not a per-board ratio -
this tab's layout does not depend on which board is selected. `Initialize` applies both (via the
private `ApplySplitterWidths`, exposed to tests as `ApplySplitterWidthsForTests`) right after
`InitializeComponent`, clamped to a usable range so a width saved on a large monitor cannot restore
off-screen on a small one. Each splitter's `PointerReleased` handler
(`OnOuterSplitterPointerReleased`/`OnBoardEntrySplitterPointerReleased`) saves the new width back,
deferred via `Dispatcher.UIThread.Post` so it reads the column's `Bounds` after the drag has actually
been applied - the same reason `Main`'s own splitter handler defers.

**Both are wired with `AddHandler(..., handledEventsToo: true)`, not a `PointerReleased="..."` markup
attribute** - `GridSplitter` marks the event handled as it finishes its drag, so a plain subscription
never runs and neither width was ever saved. Same as `Main.OnMainSplitterPointerReleased` and
`TabSchematics`, which both carry the same comment.

Cards, the top-line pill and preview badges are all built in code rather than by a `DataTemplate`,
because their brushes need the two-step `Application.Current` + `ThemeVariant` lookup a template
binding cannot express — the same reason `Main` builds the worklog bar's own pill in code.

**Schematic bitmaps are shared, not decoded per rebuild.** `thisSchematicBitmapsByPath` holds one
decoded `Bitmap` per image path for the life of the tab, disposed in `OnDetachedFromVisualTree`. A
fresh `new Bitmap(path)` per preview per pass stranded a full-resolution decode every time (a
4220x2941 schematic is ~47 MB of BGRA) on every board change, entry save and workbook create/close.
Disposing on clear instead would be WORSE: `ShowDialog` does not block the dispatcher, so
`RefreshWorklogBar` can re-enter while a badge's editor is up, and that editor documents that its
schematic bitmap belongs to the caller - disposing under it is an `ObjectDisposedException` on the
render thread, fatal in Avalonia.

Its `Workbooks_*` theme keys in `App.axaml` are pinned by `WorkbooksPaletteTests` - which now covers
only the six keys the tab actually paints; sixteen further mockup-era keys were referenced by nothing
but that test and have been deleted along with it. The list, selection and activation chain are
pinned by `WorkbooksListTests`; the board pane by `WorkbooksBoardPreviewTests` (uses
`TabWorkbooks.CurrentBoardDataOverrideForTests`, alongside `BoardKeyOverrideForTests`, so a test
never has to construct `Main`); `ActiveWorkbookIdByBoard`'s persistence itself by
`UserSettingsTests`. Note the mockup's still-hardcoded entry list draws the
README's four-category vocabulary (Note/Cosmetic/Suspected/Confirmed, Pending/Fixed/Ruled out),
which is NOT the shipped `WorklogManager` model (Note/Cosmetic/Issue, Open/Closed) that every REAL
piece of this tab already uses — reconciling the two is part of making the entry list functional.

#### Schematics (`Tabs/Schematics/`)

The most complex tab: it renders schematic/PCB images with three overlay layers (component highlights,
an interactive KiCad trace/copper overlay, user-drawn polyline traces), hosts the component label
editor, and hosts the MiniPro IC-test panel.

`TabSchematics` is one partial class split by area across the files below. **Find the right file here
before grepping** — the same header map is repeated in
[Tabs/Schematics/TabSchematics.axaml.cs](../Tabs/Schematics/TabSchematics.axaml.cs):

| File | Owns |
| --- | --- |
| `TabSchematics.axaml.cs` | Construction, one-time wiring in `Initialize`, fullscreen/splitter layout, the user-drawn trace colour palette, shared parse/theme helpers |
| `TabSchematics.Types.cs` | Private data types shared by the other parts (cache records, hover candidates, undo state, `EditableComponentHighlight`) |
| `TabSchematics.Viewport.cs` | Zoom, pan, the transform matrix, matrix clamping, content/viewport rects |
| `TabSchematics.Input.cs` | Pointer, wheel, gesture and keyboard handlers — these only dispatch |
| `TabSchematics.Thumbnails.cs` | Thumbnail list, selection, thumbnail bitmaps, drag-to-reorder |
| `TabSchematics.Highlights.cs` | Component highlight overlays, blink visuals, hover UI, on-schematic labels |
| `TabSchematics.LabelEditor.cs` | Label editor lifecycle, menu, apply/cancel, validation and save dialogs, search, undo/redo |
| `TabSchematics.LabelEditor.Interaction.cs` | Label editor selection, resize handles, drawing, dragging, coordinate conversions |
| `TabSchematics.LabelEditor.Snap.cs` | Label editor snapping maths and guide lines |
| `TabSchematics.KiCad.cs` | KiCad project load, board-label→net/reference mapping, selection sets, runtime cache scopes |
| `TabSchematics.KiCad.Panels.cs` | The "Important signals" and "Net connections" side panels |
| `TabSchematics.KiCad.Render.cs` | Draws the KiCad overlay, refresh scheduling, pin-1 marking |
| `TabSchematics.KiCad.RenderCache.cs` | Builds/caches per-net PCB render nodes and connected-segment chains |
| `TabSchematics.KiCad.Geometry.cs` | KiCad world ↔ screen mapping, world bounds, curve sampling, zone polygon geometry |
| `TabSchematics.KiCad.HitTest.cs` | Hover hit-testing, hit-test caches, hover throttling, trace hover mode UI |
| `TabSchematics.KiCad.Calibration.cs` | Interactive KiCad trace calibration mode |
| `TabSchematics.Settings.cs` | Board-level and global setting rows, and restoring them per board |

Supporting classes in the same folder are ordinary (non-partial) types: `KiCadOverlayRenderControl`,
`SchematicHighlightsOverlay`, `PolylineManagement` (user-drawn traces; reaches into
`TabSchematics.schematicsMatrix`), `HighlightSpatialIndex`, `SchematicThumbnail`, `ComponentInfoWindow`,
`ComponentLabelEditorOverlay`, and `IcTestPanel`.

### Data layer (`Handlers/Data/`)

- `DataManager` (static) — resolves the data root (default location or `--data-root=`), loads the master
  Excel workbook, and drives sync against the online checksum manifest.
- `BoardData` / `BoardDataReader` / `BoardDataWriter` — the schema (in [Handlers/Data/BoardData.cs](../Handlers/Data/BoardData.cs))
  and read/write logic (via EPPlus) for per-board Excel files: schematics, components, component
  images/highlights, local files, links, credits, and KiCad signal mappings.
- `DataValidator` — validates board/contribution data.
- `UserSettings` — JSON-persisted user preferences (theme, window placement, MiniPro path override, etc.).
- `KiCadProjectData` / `KiCadProjectLoader` / `KiCadRawProjectLoader` — parse raw KiCad PCB/schematic
  files into a normalized bundle so the Schematics tab can highlight matching copper and wire geometry.
- `Logger` — writes to the app's log file in the AppData folder alongside settings.
- `ComponentListBuilder` — the main window's component list: region filter, category filter, search,
  and the `ComponentListItem` rows it produces. Also owns `IsSupportedKiCadRawFile`.
- `ComponentImageQueries` — which component images and entries the popup shows for a region, and
  which of them carry an oscilloscope baseline.
- `OverviewHtmlBuilder` / `OverviewModels` — bill-of-materials grouping and the printable HTML for
  the Overview tab, plus the `OverviewRow`/`OverviewLink` models it renders. **The Overview AXAML
  binds these through an `xmlns:data="clr-namespace:Handlers.DataHandling"` mapping** — if you move
  or rename them, update `TabOverview.axaml` too.
- `ContactLinkFormatter` — classifies a contributor's contact string and builds its href.

### Content (`Assets/Data/`)

All hardware reference content is data, not code: organized as `<Manufacturer>/<Hardware>/<Board>/...`
(e.g. `Commodore/C64/250407/`), with per-manufacturer `Shared files` folders and a top-level
`Generic shared files` folder for cross-manufacturer component images. A master workbook
(`Classic-Repair-Toolbox.xlsx`, sheets `Hardware & Board` and `Oscilloscope`) lists all available
hardware/boards and oscilloscope SCPI command sets; each board also has its own Excel file matching the
`BoardData` schema. This content is what `DataManager` syncs from `classic-repair-toolbox.dk` at runtime
using SHA-256 checksums, independent of app releases — adding new hardware is primarily a data
contribution, not a code change.

### External integrations (`Handlers/`)

- `Online/OnlineServices` — fetches the checksum manifest and syncs changed data files.
- `Online/UpdateService` — checks/applies application updates via Velopack against GitHub Releases
  (`GitHubOwner`/`GitHubRepo` in `AppConfig`).
- `Oscilloscope/ScopeScpiClient` — raw SCPI-over-TCP client; `ScopeCommandPalette`/`ScopeCommandResolver`/
  `ScopeValueMapper` translate the data-driven `OscilloscopeEntry` command strings (per brand/model, from
  the master workbook) into actual scope interactions for baseline capture.
- `MiniPro/` — integration with the MiniPro USB IC programmer for in-app IC testing. `IMiniproRunner` is
  the abstraction; `MiniproProcessRunner` spawns the bundled `minipro.exe` (streamed, cancellable output),
  `MockMiniproRunner` simulates it without hardware attached. `IcTestCatalogue`/`IcTestModel`/`IcTestService`
  manage the IC test definitions. Only Windows (`win-x64`) currently bundles the `minipro` binary — see the
  conditional `ItemGroup` in [Classic-Repair-Toolbox.csproj](../Classic-Repair-Toolbox.csproj).
- `Security/ExternalTargetLauncher` — the only sanctioned way to open an external link or local file from
  the UI; it restricts targets to HTTP/HTTPS/mailto URIs or local paths that resolve inside the current
  data root *and* carry a document/image/data file extension from its allowlist (it hands files to the
  OS shell, which would run a `.exe`/`.bat`/`.lnk` instead of displaying it), rejecting anything else.
  Use this rather than shelling out directly when opening user/data-supplied links or files.

### Contribution webserver (`Assets/Webserver/`)

A working copy of the PHP deployed at `classic-repair-toolbox.dk/app-contribution/` — the server
side of the Contribute tab, split into two entities: `api/index.php` receives the uploads the app
posts to `AppConfig.ContributionUploadUrl`, and `review/index.php` + `review/functions.php` are the
review queue (admin-only, IP-restricted via `review/.htaccess`) that diffs each submission against
the live server data and merges or rejects it. `api/index.php` requires `review/functions.php` for
its shared helpers. It is deployed by hand and never ships with the app (Assets are whitelisted
per-file in the csproj).

**The payload is a two-sided contract.** `ComponentContributionPayload` in
[Tabs/Contribute/ComponentContribution.axaml.cs](../Tabs/Contribute/ComponentContribution.axaml.cs)
is what `review/functions.php` parses, and the PHP deliberately mirrors the app's Excel-reading
rules (case-insensitive headers, exact board-file resolution, `.json`-beside-the-Excel highlights,
the `# Revision date:` marker). When you change either side, change the other in the same
sitting, and run the PHP suite too: `php Assets/Webserver/tests/run-tests.php`
(~1s, no webserver needed — PHP is installed on this machine; the tests sit outside
`app-contribution/` so that folder stays an exact mirror of what is deployed). **When the payload contract
changes, also bump `$minimumContributionVersion` in `api/index.php`** to the first released
version carrying the new contract — older apps are rejected at upload with an update-required
message. Full details, file inventory (including which files are legacy) and known caveats:
[Assets/Webserver/app-contribution/README.md](../Assets/Webserver/app-contribution/README.md).

## Release process

Versioning lives in [Classic-Repair-Toolbox.csproj](../Classic-Repair-Toolbox.csproj)
(`AssemblyVersion`/`InformationalVersion`) — bump `InformationalVersion` there before releasing, since
that is the only place a release version is entered. Releases are made by hand from the GitHub Actions
tab — run [.github/workflows/build-and-release.yml](../.github/workflows/build-and-release.yml) with no
inputs — **never by pushing a tag**; that trigger was deliberately removed. Its first job reads
`InformationalVersion` straight from the csproj (via `dotnet msbuild -getProperty:InformationalVersion`)
and derives pre-release status from it (a version containing `-`, e.g. `2.5.0-beta.1`, is a pre-release;
a bare `X.Y.Z` is not) — every other job consumes that one job's output rather than re-deriving it. It
then runs the test suite and stops there if it is red, then a CodeQL scan, then builds/signs/packages
(Velopack) self-contained builds for win-x64, linux-x64, osx-x64 and osx-arm64, and finally publishes a
GitHub Release using [CHANGELOG.md](../CHANGELOG.md) as the release body. The tag is created by that
last step, so a failed run leaves nothing behind to clean up and the same version number can simply be
re-run once the fix is pushed. That release body is written by hand and is off-limits to Claude —
see [Hands off CHANGELOG.md](#hands-off-changelogmd).
