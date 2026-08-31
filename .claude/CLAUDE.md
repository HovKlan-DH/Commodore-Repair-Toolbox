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
| Settings / startup | `UserSettings`, `DataManager` (data-root + master workbook), `DataValidator` (smoke only), `SimulationOptions` |
| Headless UI (`Tests/.../Ui/`) | All eight tabs built headlessly, plus component highlight selection and schematics zoom - see [Headless UI tests](#headless-ui-tests) |
| Geometry (`Handlers/Geometry/`) | `PolygonGeometry`, `RectGeometry`, `KiCadLayerGeometry`, `KiCadPadGeometry`, `OverlayCullGeometry`, `KiCadOverlayCacheKeys`, `KiCadOverlayNetCache`, `ViewportMath`, `KiCadNetGraphBuilder`, `KiCadHoverIndex`, `HighlightRectBuilder`, `LabelEditorGeometry` |

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

Use `TempWorkspace` for anything that touches the filesystem; it creates and deletes a temp folder.
Tests that mutate `UserSettings` or `DataManager` static state live in the `"UserSettings"` and
`"DataManager"` xUnit collections so they run sequentially. `BoardDataReader` has the same
problem — its loaded boards sit in a shared static cache (a `ConcurrentDictionary`, so thread-safe,
but still one cache that tests clear and repopulate) — so `BoardDataReaderTests`
and `BoardDataWriterTests` share the `"BoardData"` collection.
**Any new test class that touches one of these statics must join its collection**, or it will
pass alone and fail intermittently in the full run.

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
`Resources`, `Schematics` — each an Avalonia `UserControl` (`.axaml`) with its logic in the paired
`.axaml.cs`.

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
