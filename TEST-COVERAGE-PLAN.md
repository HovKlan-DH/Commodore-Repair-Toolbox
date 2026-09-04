# Test coverage implementation plan

**Status:** proposal, nothing implemented yet.
**Audience:** an agent picking up one step at a time.
**Baseline measured:** 2026-09-04, Release, `main` @ `28d9743f`.

This is a temporary working file. Delete it when the work is done.

---

## How to use this document

Each step below is **independently shippable** and leaves the suite green. Do them in order —
step 1 unblocks steps that follow. Do **not** attempt several steps in one change; each one is
sized to be a single reviewable commit.

**Before starting any step**, read `.claude/CLAUDE.md` in full. It is the authority on this
repo's conventions and it overrides anything here that contradicts it. In particular:

- Tests are part of the change, never a follow-up.
- `dotnet test Classic-Repair-Toolbox.slnx` must pass before reporting a step done. This is
  machine-enforced by the `Stop` hook.
- Never call `UserSettings.Load()`, `DataManager.InitializeAsync()`, `WorklogManager.Load()` or
  `Logger.Initialize()` from a test. Use the `LoadFrom(...)` seams.
- **Do not touch `CHANGELOG.md`.** Not as a finishing touch, not to tidy it. If you think an
  entry is warranted, say so in your summary and leave it to the maintainer.
- Any new test class touching `UserSettings`, `DataManager`, `WorklogManager` or `BoardDataReader`
  statics must join the matching xUnit collection, or it will pass alone and fail in the full run.
  A class can only join ONE collection; headless UI tests must be in `"HeadlessUi"`.

### The measured baseline (reproduce, don't trust)

```
dotnet test Classic-Repair-Toolbox.slnx -c Release --collect:"XPlat Code Coverage"
```

Then read `lines-covered` / `lines-valid` from `coverage.cobertura.xml` under
`Tests/Classic-Repair-Toolbox.Tests/TestResults/`.

| Scope | Covered | Total | % |
| --- | ---: | ---: | ---: |
| Overall | 11,276 | 28,046 | 40.2% |
| `Handlers/` | 6,174 | 7,822 | 78.9% |
| `Tabs/` + `Main/` | 5,102 | 20,224 | **25.2%** |

2,055 tests, 0 failing, ~43s. Branch coverage 31.3% (3,796 / 12,130).

**Always quote the denominator and the build configuration.** Debug and Release instrument
different line counts and are not comparable.

---

## Step 1 — Separate `Main`'s construction from its startup — ✅ DONE

**Outcome (measured after the change):** overall **40.2% → 43.7%**, UI **25.2% → 30.1%**,
`Main.axaml.cs` **0.0% → 15.3%**. Suite 2,055 → 2,067 tests, 0 failing, ~41s.

Delivered: `Main.StartAsync()` holding the three outward-facing calls; `App` calls it after
`Show()`; `StartBackgroundSyncAsync` changed `async void` → `async Task` so it can be composed;
new `Tests/.../Ui/MainWindowTests.cs` with 12 tests.

The yield estimate below (~1,200–1,500 lines) was optimistic: the actual gain in `Main.axaml.cs`
was ~247 lines. The constructor and the methods reachable without a board are now covered, but
most of the file's bulk is board-loading and schematics coordination that needs real `BoardData`
— reachable now that the window constructs, but it needs the seams in steps 5–7 to be driven.
**Treat the remaining per-step estimates in this document with the same scepticism.**

*Original step description follows, for reference.*


**Why:** `Main/Main.axaml.cs` is 1,615 lines at **0.0%** coverage — the single largest uncovered
file in the repo. It is uncovered because no test can construct it, and that is not a limitation
of headless Avalonia. It is what the constructor does. This one change unblocks more coverage
than any other available edit, and it is a prerequisite for steps 5 and 7.

**Yield:** ~1,200–1,500 lines.

### 1a. The problem, precisely

`Main()` at `Main/Main.axaml.cs:126` ends with three calls that reach the outside world:

| Call | Line (approx) | Why it blocks tests |
| --- | --- | --- |
| `PopulateHardwareDropDown()` | 205 | Reads `DataManager.HardwareBoards` static state |
| `CheckForAppUpdateNowAsync()` | 300 | Real HTTP to GitHub, gated on `UserSettings.CheckVersionOnLaunch` |
| `StartBackgroundSyncAsync()` | 320 | Network sync against the checksum manifest |

Everything above those in the constructor is ordinary control wiring and is safe headlessly.

### 1b. What to do

1. Add `internal async Task StartAsync()` to `Main`. Move the three calls above into it, in the
   same order they currently run. Keep the `if (UserSettings.CheckVersionOnLaunch)` and
   `if (DataManager.DataUpdateRequiresAppUpdate)` guards with the calls they guard.
2. In `Main/App.axaml.cs`, at **line 419**, the sequence is currently:

   ```csharp
   var main = new Main();
   desktop.MainWindow = main;
   main.Show();
   splash.Close();
   ```

   Insert the start call so behaviour is byte-for-byte identical to today. `StartBackgroundSyncAsync`
   is currently `async void` fired from the constructor, so it must stay fire-and-forget — do not
   `await StartAsync()` in a way that delays `main.Show()` or the splash close, or you have changed
   startup timing. Prefer:

   ```csharp
   var main = new Main();
   desktop.MainWindow = main;
   main.Show();
   splash.Close();
   _ = main.StartAsync();
   ```

   Confirm against the real ordering when you get there; the requirement is that the user-visible
   startup sequence and the `StartupTimeline` milestones do not move.
3. Leave the rest of the constructor alone. It is not the problem.

### 1c. Verify you have not changed startup

This is the risk in this step. `CLAUDE.md` is explicit that DEBUG and RELEASE must behave
identically and that startup timing is logged. Before and after your change:

- Run the app by hand (`dotnet run`, or F5) and confirm: splash appears, data initialises, main
  window opens, hardware/board dropdowns populate, the sync banner behaves as before.
- Compare the `StartupTimeline` milestone lines in the log. They should not change in ordering.
- Run with `--simulate-update=99.0.0` and confirm the update banner still appears.

### 1d. Tests to add

New file `Tests/Classic-Repair-Toolbox.Tests/Ui/MainWindowTests.cs`, `[Collection("HeadlessUi")]`.
Construct `Main` inside `UiTest.Run(...)` and **never call `StartAsync`**. Cover:

- The window constructs without throwing (the foundation; everything else depends on it).
- `ApplyOscilloscopeTabVisibility()` — tab hidden/shown per the setting, and `MoveSelectionOffHiddenTab`
  moving selection to the first still-visible tab when the selected tab is hidden.
- `ApplyWorklogBarVisibility()` — same, for the Workbooks tab and worklog bar together.
- Region toggling — `OnPalRegionClick` / `OnNtscRegionClick` change `LocalRegion`, and
  `UpdateRegionButtonsState` reflects it. Assert on observable state, not on the click handler.
- The banner show/hide pairs: `ShowApplicationUpdateAvailableBanner` / `HideApplicationUpdateBanner`,
  and `ShowMainExcelRequiresAppUpdateBanner` / `HideMainExcelRequiresAppUpdateBanner`.
- `GetCurrentBoardKey()` and `GetCurrentBoardEntry()` with no selection — the empty/null paths.
- `RefreshWorklogBar()` — the funnel every worklog change passes through. Point `WorklogManager`
  at a temp root via `LoadFrom` first.

**Note on `PopulateHardwareDropDown`:** once it is out of the constructor it is only reachable by
calling it. Either expose it as `internal` and test it against a `DataManager.LoadFrom(...)` temp
data root, or leave it for step 5. Do not seed `DataManager` static state carelessly — join the
`"DataManager"` collection if you touch it.

---

## Step 2 — Extract `LabelEditor.Snap.cs` into `Handlers/Geometry/` — ✅ DONE

**Outcome:** overall **43.7% → 44.8%**, UI **30.1% → 30.7%**, and the extracted maths went from
**0% → 71.6%** (282/394 lines in `LabelEditorSnapGeometry.cs`). Suite 2,067 → 2,093, 0 failing.

Delivered: `Handlers/Geometry/LabelEditorSnapGeometry.cs` (the ~950 lines of maths, plus the
`LabelEditorSnapContext` struct); `EditableComponentHighlight` moved to `Handlers/Geometry/`;
`TabSchematics.LabelEditor.Snap.cs` reduced to a 74-line rim whose one piece of real work is
`BuildVisiblePixelRect` (which also de-duplicated a block that was copy-pasted in two methods);
26 new tests in `LabelEditorSnapGeometryTests.cs`; `CLAUDE.md` and the `TabSchematics` file map
updated.

Two notes for whoever does the remaining steps:

- **The context is passed by value, not `in`.** C# forbids capturing an `in` parameter inside a
  local function, and this code is built out of local functions closing over the context. Do not
  "optimise" it back to `in`.
- **Mutation-test the assertions.** My first blocking-path test passed even with the entire
  blocking rule disabled — it asserted "did not snap" in a geometry where nothing would have
  snapped anyway. It is now a pair: one test proving the snap happens, one adding only the blocker
  and proving it is refused. Verify a new test fails when you break the thing it names.

*Original step description follows, for reference.*


**Why:** 479 uncovered lines at **0.0%**, and it is the largest block of pure maths left in the
codebase. `CLAUDE.md` currently describes this as "real surgery, not a lift-and-shift" — that
assessment predates measurement and is more pessimistic than the code warrants.

**Yield:** ~400–480 lines.

### 2a. What the coupling actually is

I enumerated every `this.` reference in the file. There are **9 distinct dependencies across
~970 lines**, and all of them are data or simple predicates — none is a control being mutated:

| Dependency | Refs | Kind |
| --- | ---: | --- |
| `thisLabelEditorWorkingHighlights` | 8 | `List<EditableComponentHighlight>` (data) |
| `thisLabelEditorDragMode` | 7 | enum (data) |
| `currentFullResBitmap` | 6 | read for `.PixelSize` only |
| `SchematicsContainer` | 6 | read for `.Bounds` only |
| `GetCurrentSchematicName()` | 4 | returns `string` |
| `ApplyLabelEditorResizeSnap` | 4 | internal recursion, moves with the code |
| `schematicsMatrix` | 2 | `Matrix` (value type) |
| `IsSelectedLabelEditorHighlight` | 2 | predicate `(highlight) => bool` |
| `GetLabelEditorImageContentRect()` | 2 | delegates to `GetSchematicsContentRect()`, returns `Rect` |

The `currentFullResBitmap` / `SchematicsContainer` / `schematicsMatrix` references are clustered
in exactly two near-identical blocks — **lines 103–123 and 515–535** — which both compute one
`Rect? visiblePixelRect` and then use nothing else from the UI.

### 2b. What to do

1. Create `Handlers/Geometry/LabelEditorSnapGeometry.cs` as a plain static class.
2. Define a context struct carrying the resolved values, not the controls:

   ```csharp
   public readonly record struct LabelEditorSnapContext(
       IReadOnlyList<EditableComponentHighlight> WorkingHighlights,
       LabelEditorDragMode DragMode,
       string SchematicName,
       Rect? VisiblePixelRect,
       Func<EditableComponentHighlight, bool> IsSelected);
   ```

   Note `VisiblePixelRect` is **already computed** — the caller does the two UI blocks and passes
   the result in. That is what collapses the coupling from 14 touch-points to one value.
3. Move the three methods across, changing `private void ApplyX(...)` to
   `public static void ApplyX(in LabelEditorSnapContext context, ...)`:
   - `ApplyLabelEditorResizeSnap` (line 41)
   - `ApplyLabelEditorMoveSnap` (line 478)
   - `ApplyNewLabelEditorRectangleSnap` (line 891)

   Keep the `ref` parameters and the `snapGuides` list exactly as they are — the callers depend on
   both.
4. `EditableComponentHighlight` currently lives in `TabSchematics.Types.cs`. It is a plain data
   type; move it to `Handlers/Geometry/` (or `KiCadRenderNodes.cs`, which already holds shared
   DTOs) so `Handlers/` does not depend on `Tabs/`. **Check the dependency direction compiles** —
   `Handlers/` must not reference `Tabs/`.
5. Leave thin wrappers in `TabSchematics.LabelEditor.Snap.cs` that build the context (including the
   two `visiblePixelRect` blocks) and delegate. The file shrinks to roughly the UI rim.

### 2c. Tests to add

New file `Tests/Classic-Repair-Toolbox.Tests/LabelEditorSnapGeometryTests.cs`. Pure logic, no
collection needed, no `UiTest`. Cover per `CLAUDE.md`'s rules — failure and edge cases, not just
the happy path:

- Snapping to a neighbouring edge within the 2.0 threshold; **not** snapping just outside it.
- `suppressSnap: true` is a no-op in all three methods.
- `DragMode.None` and `DragMode.Move` return early from the resize snap (its own guard).
- Snap guides are emitted for a match and not for a near-miss (`guideMatchThreshold` 0.5).
- Empty `WorkingHighlights`, and a `SchematicName` that is null/blank.
- `VisiblePixelRect` null — the un-clipped path.
- Move-snap with a multi-selection, and with `selectedHighlights.Count == 0`.

**Then update `CLAUDE.md`:** its "do not go looking for more to extract" list names
`TabSchematics.LabelEditor.Snap.cs` as the largest remaining candidate. Once this step lands that
entry is wrong and must be corrected in the same change.

---

## Step 3 — Split `PolylineManagement.cs` — ✅ DONE (reduced scope — read this before step 4)

**Outcome:** overall **44.8% → 44.9%**, UI unchanged at **30.7%**. `TraceGeometry.cs` is at
**100%** (39/39); `PolylineManagement.cs` went 0% → 6.1%. Suite 2,093 → 2,116, 0 failing.

**The estimate below (~250–350 lines) was wrong, and so was the premise.** This step assumed a
"model half" that would lift out cleanly. There isn't one. `ManagedPolyline` is *not* a model: it
wraps a live Avalonia `Polyline` shape plus its `Ellipse` markers and holds a `TabSchematics`
reference, and it **is** the storage — `_polylines` is a list of them. So `GetHitMarker`,
`GetHitSegment`, `UndoLastDeletion`, `ExportTraces`, `AddTraceFromModel` and the palette methods
all walk live UI objects. Moving them means rewriting the class around a separate model — a
redesign, not an extraction, and beyond what this step authorised.

What actually moved (`Handlers/Geometry/TraceGeometry.cs`, 23 tests):
`CanvasToNormalized` / `NormalizedToCanvas` (the normalized-storage conversion), `ApplyNodeSnapping`,
`Distance` / `DistanceSquared` / `DistancePointToSegment`, and `IsLegacyCanvasCoordinate`. The
hit-testing methods now call these per node/segment while the walking stays in the tab. This also
de-duplicated the legacy-coordinate conversion, which was a second inline copy of
`CanvasToNormalized` inside `AddTraceFromModel`.

**What this means for the projections.** `PolylineManagement`'s remaining ~420 uncovered lines are
reachable only through headless interaction tests (step 5's technique), not through extraction.
The same is very likely true of step 4 — **verify the coupling before assuming the estimate**, the
way this step did, rather than trusting the number.

*Original step description follows, for reference. Its "model half" list is the part that did not
survive contact with the code.*


**Why:** 468 uncovered lines at **0.0%**. It is already a standalone non-partial class with a
public API, so nothing needs inventing — the model half simply has nothing calling it from a test.

**Yield:** ~250–350 lines.

### 3a. What to do

The class mixes canvas rendering with a testable model. Separate them:

- **Model half — move to `Handlers/Geometry/` or a new `Handlers/Traces/`:** the trace model and
  its transforms. `ExportTraces` / `ImportTraces` / `ExportSingleTrace` / `AddTraceFromModel`
  (round-tripping), `CanvasToNormalized` / `NormalizedToCanvas` (coordinate maths),
  `ApplySnappingCanvas`, `GetHitMarker` / `GetHitSegment` (hit-testing geometry),
  `Distance` / `DistanceSquared`, the undo stack (`PushUndo`, `UndoLastDeletion`), and the palette
  logic (`GetDefaultPaletteColors`, `AddOrReplacePaletteColor`, colour visibility).
- **Keep in the tab:** the constructor taking `Canvas` and `TabSchematics`, the pointer handlers
  (`OnPointerPressed` / `OnPointerMoved` / `OnPointerReleased`) and anything mutating canvas
  children. These stay UI.

The pointer handlers are the awkward middle: they contain real decision logic. Have them compute
via the extracted helpers and keep only the canvas mutation inline.

### 3b. Tests to add

`Tests/Classic-Repair-Toolbox.Tests/PolylineModelTests.cs`. Cover:

- Export → import round-trip preserves points, colour and count.
- Normalised ↔ canvas conversion round-trips, including at a non-1.0 scale factor
  (`UpdateScaleFactor`).
- Hit-testing: a point on a marker, on a segment, and outside tolerance; the split point returned
  for a segment hit.
- Snapping within and outside tolerance.
- Undo restores the last deleted polyline; undo on an empty stack is a no-op.
- Palette: adding a colour that already exists replaces rather than duplicates; visibility toggles
  by colour.
- Empty trace list, single-point polyline, malformed imported model.

---

## Step 4 — Extract the KiCad calibration maths

**Why:** `TabSchematics.KiCad.Calibration.cs` is 472 uncovered lines at **0.0%**.

**Yield:** ~200–300 lines.

**What to do:** the interactive mode (pointer capture, prompts, mode state) stays in the tab. The
transform derivation — computing the calibration offset and scale from the picked point pairs, and
validating a calibration — is pure and moves to `Handlers/Geometry/`.

**Caution:** `CLAUDE.md` records that `TabSchematics.KiCad.Geometry.cs`'s world↔local mapping reads
`currentFullResBitmap` for the calibration offset scale. Resolve that value at the call site and
pass it in, exactly as step 2 does with `VisiblePixelRect`. Do not move the bitmap read itself.

**Tests:** derive a known transform from synthetic point pairs; degenerate input (identical points,
a single pair, zero extent); a calibration that should be rejected.

---

## Step 5 — Headless interaction tests for the label editor — ✅ DONE (label editor part)

**Outcome:** overall **44.9% → 46.6%**, UI **30.7% → 33.2%** — the largest single jump so far,
and it confirms the plan's core claim that interaction tests, not extraction, are where the UI
coverage is. Suite 2,116 → 2,143, 0 failing.

| File | Before | After |
| --- | ---: | ---: |
| `TabSchematics.LabelEditor.cs` | 1.3% | **28.6%** |
| `TabSchematics.LabelEditor.Interaction.cs` | 0.4% | **37.6%** |
| `TabSchematics.LabelEditor.Snap.cs` (rim) | 0% | **43.2%** |
| `LabelEditorGeometry.cs` | — | **92.5%** |
| `ComponentLabelEditorOverlay.cs` | — | **28.7%** |

Delivered: `Tabs/Schematics/TabSchematics.LabelEditor.TestSeams.cs` (a new partial holding the
`...ForTests` seams, so the 1,412-line editor file did not grow) and 27 tests in
`Ui/LabelEditorInteractionTests.cs` covering enter/cancel, selection (single, replace, toggle,
clear), move (including that successive updates do not compound, and multi-selection), resize
(each edge and a corner), delete, keyboard nudge/expand and its two refusal cases, and
undo/redo (including reverse order and that a no-op drag records no step).

**How they drive it, and the honest limit.** The editor's pointer handlers do two jobs: convert a
pointer position to bitmap pixels (needs a laid-out control and a decoded bitmap — not available
headlessly), then act on the result. These tests enter at the second job, in the same bitmap-pixel
space the real handlers hand over. **The editing behaviour is the shipped behaviour; the
pointer-to-pixel conversion ahead of it is still only verified by running the app.** Say this
plainly rather than claiming the whole flow is covered.

One production change was needed: `LoadLabelEditorWorkingCopyForCurrentSchematic` now reads board
data through a `CurrentBoardDataForLabelEditor` property backed by
`CurrentBoardDataOverrideForTests`, mirroring `TabWorkbooks` exactly. In the running app the
override is null and the behaviour is unchanged.

Mutation-verified: a 1px drag shift (7 tests caught it), removing the redo-stack push (1), and
removing the Shift+Alt refusal (1).

**Still outstanding from this step:** the worklog area-marking flow (step 6 covers it separately)
and `ComponentInfoWindow` (step 7). The remaining uncovered bulk of `LabelEditor.cs` is the
apply/save path — validation dialogs and Excel writes — which needs `ShowDialog` and a real
`Main`.

*Original step description follows, for reference.*


**Why:** this is the regression safety net the maintainer specifically asked for.
`TabSchematics.LabelEditor.cs` (788 uncovered, 1.3%) and `.Interaction.cs` (533 uncovered, 0.4%)
are 1,321 uncovered lines between them — the largest interaction-shaped gap in the codebase.

**Yield:** ~600–900 lines.

### 5a. The harness already exists — use it

No new infrastructure is needed. `HeadlessWindowExtensions` is already driving real input in:

- `Ui/PointerHitTestingTests.cs` — `window.MouseDown(point, MouseButton.Left)`
- `Ui/WorklogCompletedComponentsTests.cs` — `MouseDown` + `MouseUp` pairs
- `Ui/WorklogListSectionTests.cs` — the same, for list rows
- `Ui/DeleteWorkbookWindowTests.cs` — `window.KeyPress(key, modifiers, physicalKey, keySymbol)`

Copy those patterns. Everything runs inside `UiTest.Run(...)`, and **every assertion must be inside
the body** — reading a control property from the test thread throws.

### 5b. Flows to cover

New file `Tests/Classic-Repair-Toolbox.Tests/Ui/LabelEditorInteractionTests.cs`,
`[Collection("HeadlessUi")]`. In rough order of value:

1. **Activation** — the editor enters and leaves label-editor mode; `IsLabelEditorActive` reflects it.
2. **Draw** — a drag on the overlay creates a new highlight rect of the expected bounds.
3. **Select** — a click selects; a click on empty space deselects; multi-select behaviour.
4. **Resize** — dragging each handle changes the correct edge, and the opposite edge stays put.
5. **Move** — dragging the body translates without resizing.
6. **Undo/redo** — after a draw, a move and a resize; and that redo after a new edit is discarded.
7. **Cancel** — leaves the working set unchanged on disk.

Assert on **observable state** — the resulting rect, the selection, the working-highlight count.
Per `CLAUDE.md`, a construction test adds nothing here: the XAML compiler already catches renamed
`x:Name`s and broken `avares://` paths.

**Sequencing note:** step 2 moves the snapping maths out from under these methods. Doing step 2
first means these tests are written against the final shape. If you do step 5 first, expect to
update them during step 2 — which rule 2 requires anyway.

---

## Step 6 — Headless tests for the worklog area-marking flow — ✅ DONE

**Outcome:** overall **46.6% → 47.2%**, UI **33.2% → 34.1%**.
`TabSchematics.Worklog.cs` **7.4% → 29.1%**. Suite 2,143 → 2,159, 0 failing.

Delivered: `Tabs/Schematics/TabSchematics.Worklog.TestSeams.cs` and 16 tests in
`Ui/WorklogAreaMarkingTests.cs` — entering the mode; both refusal cases (no schematic image
loaded, label editor already active) and the reciprocal case that opening the label editor
cancels an active marking mode; cancel (including that cancelling when not in the mode is
harmless); the draft rubber-band following the pointer; the accepted area; a backwards drag
normalising to the same rectangle; and the accept/reject threshold — click, too-small, wide-but-flat
rejected, just-over-minimum accepted.

**One production refactor, done to avoid a lying test.** `CompleteDrawingWorklogEntryRectangle`
ended by calling `OpenNewWorklogEntryEditor` → `ShowDialog`, which needs a real owner window and
cannot run headlessly. The accept/reject decision is now split into `TryFinishWorklogEntryDrawing`,
which **both** the shipped path and the test seam call, so the rule under test is the shipped rule
with only the modal skipped. The first draft of the seam duplicated that decision instead — which
would have let the two drift silently — and was rewritten.

Mutation-verified: accepting every drag size (3 tests caught it) and removing the label-editor
exclusion (1).

**Still not covered:** the editor handoff itself (`ShowDialog`), and the badge/pill placement
this flow feeds — `ThumbnailWorklogPillsOverlay` is already at 96.8% via its own tests, but the
`TabSchematics.Worklog.cs` code that positions the live badge needs a laid-out control.

*Original step description follows, for reference.*


**Why:** `TabSchematics.Worklog.cs` is 527 uncovered lines at 7.4%, and this exact flow has
already produced **two reported regressions** (the parked-vs-anchored badge bug, reported once
against the Workbooks board pane and again against the Schematics thumbnails). It is demonstrably
the flow most likely to break silently.

**Yield:** ~300–400 lines.

**What to cover:** "Add worklog" starts area-marking mode → the mode hint appears → a drag on the
schematic defines the area → `OpenNewWorklogEntryEditor` is reached with the drawn area and the
right schematic. Then: cancelling the mode (`OnWorklogCancelEntryClick`) clears it and restores the
buttons via `ResetWorklogEntryModeButtons`; and switching board or activating another workbook
cancels an in-progress drawing (which captured the old workbook id when it started).

`WorklogEntryEditorWindow` calls `ShowDialog`, which does not block the dispatcher. Either assert
up to the point the editor is constructed, or follow whatever pattern the existing worklog editor
tests use — check `Ui/WorklogEditorNewEntryTests.cs` first and match it rather than inventing a
second approach.

---

## Step 7 — `ComponentInfoWindow` — ✅ DONE

**Outcome:** overall **47.2% → 49.7%**, UI **34.1% → 37.4%** — the biggest single step so far.
`ComponentInfoWindow.axaml.cs` **0.0% → 31.6%**, and its AXAML **0% → 91.5%**.
Suite 2,159 → 2,178, 0 failing.

**No seams were needed.** `SetComponent` is already public and takes plain data (entries, images,
local files, links, region, data root), so the tests drive the real entry point directly. Nothing
was blocking this file except that no test had ever opened the window — worth remembering before
adding seams elsewhere: check the public surface first.

Delivered: 19 tests in `Ui/ComponentInfoWindowTests.cs` — title composition (joining, skipping
blank parts, board-label-only, and the display-text last resort), region selection (the matching
entry wins, a region-less entry is the fallback, the toggle buttons hide on a board with no
region components), the category/part-number and description lines and their hidden states,
local-file and link filtering by board label with data-root path resolution, and that a second
`SetComponent` fully replaces the first one's content.

**Two of my initial expectations were wrong, and the code was right both times:**

- `InfoDescription` is a read-only `TextBox`, not a `TextBlock` — so the description can be
  selected and copied. The test now asserts through `TextBox`.
- The display-text fallback does **not** fire merely because there is no component entry: the
  board label alone still forms a title. It is the last resort for when there is no entry *and*
  no label. That is now two tests instead of one wrong one.

Mutation-verified: removing the local-file board-label filter (2 tests caught it) and ignoring
region in `PickComponentEntry` (1).

**Still uncovered here:** async image loading (needs real files), the oscilloscope session state,
the IC test panel, and external-file opening (`Process.Start`, out of scope per rule 6).

*Original step description follows, for reference.*


**Why:** 804 uncovered lines at **0.0%** — the third-worst file, and it is a `Window`, which is a
shape already proven testable by `Ui/DeleteWorkbookWindowTests.cs`.

**Yield:** ~350–500 lines.

**What to do:** construct the window headlessly with synthetic `BoardData` (use
`BoardWorkbookBuilder` — it already exists in the test project) and assert on what it renders:
the component fields shown, the image/entry queries it drives (`ComponentImageQueries` is already
covered at the `Handlers/` level, so assert the window asks the right questions), links and their
formatting, and the oscilloscope-baseline branch when an entry carries one.

It needs `Main` for some state. Depending on step 1's outcome either construct a real `Main`, or
add an override seam following the exact `CurrentBoardDataOverrideForTests` /
`BoardKeyOverrideForTests` pattern in `Tabs/Workbooks/TabWorkbooks.axaml.cs` — that is the
established convention for this and should not be reinvented.

---

## Step 8 — Oscilloscope tab: introduce a client abstraction — ✅ DONE

**Outcome:** overall **49.7% → 50.1%** (past the halfway mark), UI **37.4% → 38.0%**.
`TabOscilloscope.axaml.cs` **7.5% → 15.7%**; its AXAML **0% → 100%**. Suite 2,178 → 2,190.

Delivered: `Handlers/Oscilloscope/IScopeClient.cs` (the three methods the tab actually calls -
`SendAsync`, `QueryLineAsync`, `QueryBinaryBlockAsync`), `ScopeScpiClient` implementing it, the
tab's ~10 method signatures switched to the interface, `Tabs/Oscilloscope/TabOscilloscope.TestSeams.cs`,
and 12 tests in `Ui/OscilloscopeSequencingTests.cs` driving a `FakeScopeClient`.

Covered: which commands a palette sends and in what order; write-only commands NOT being sent as
queries (waiting on a response that never comes is how a session hangs); response parsing into the
tab's cached values including scientific notation and InvariantCulture; unparseable responses
leaving the cache alone; cached values formatted back into Set commands; both failure modes
(no cached value, missing command definition) throwing rather than sending nonsense; and the
session log carrying both directions with the *IDN? serial masked.

**The estimate (~700–1,000 lines) was too high; the real gain was ~136 lines in the tab.** The
sequencing was the testable part and it is now tested, but the bulk of that 3,237-line file is
connection lifecycle, background workers, popup windows and control wiring — none of it reachable
without a real scope or a laid-out window. **The abstraction was still worth doing**: it is what
makes the SCPI conversation assertable at all, and `Handlers/Oscilloscope/` is now at 96-100%
across every class except the deliberately-excluded client.

One expectation of mine was wrong again: I assumed `FormatScpiNumber` emitted scientific notation
(`1.5E+000`). It uses `G15`, so it emits `1.5`. Test corrected, code untouched.

Mutation-verified: making every command expect a response (1 test caught it) and disabling the
*IDN? serial masking (3, including this step's own).

**`ScopeScpiClient` remains at 0% deliberately** — real TCP, listed in `CLAUDE.md`'s
"deliberately not covered". Do not chase it.

*Original step description follows, for reference.*


**Why:** `TabOscilloscope.axaml.cs` is 3,237 lines at 7.5% — the second-worst file. The
`Handlers/Oscilloscope/` layer beneath it is already at 79.5%, so the gap is **command sequencing
and UI state**, not the SCPI maths.

**Yield:** ~700–1,000 lines.

**What to do:** follow the pattern that already works for the programmer. `Handlers/MiniPro`
reaches 60.9% precisely because `IMiniproRunner` exists with `MiniproProcessRunner` and
`MockMiniproRunner` behind it. Do the same here:

1. Define an interface over `ScopeScpiClient`'s surface (connect, send command, read response).
2. Have `TabOscilloscope` depend on the interface, defaulting to the real client.
3. Extract the baseline-capture sequencing that currently sits between the controls and the client
   into `Handlers/Oscilloscope/`, following `ScopeFormatting` / `ScopePayloadParser` — both already
   extracted from this tab.
4. Add a fake client to the test project and test the sequencing against it: command ordering,
   retry/timeout handling, and how a malformed or truncated response is handled.

**Leave `ScopeScpiClient` itself uncovered.** It is a real-TCP I/O boundary and `CLAUDE.md` lists
it as deliberately out of scope. The abstraction below it is the thing to test.

---

## Step 9 — The remaining small tabs — ✅ DONE

**Outcome:** overall **50.1% → 50.7%**, UI **38.0% → 38.7%**. Suite 2,190 → 2,214, 0 failing.

| File | Before | After |
| --- | ---: | ---: |
| `TabAbout.axaml.cs` | 17.5% | **77.8%** |
| `TabOverview.axaml.cs` | 4.4% | **42.1%** |
| `TabConfiguration.axaml.cs` | 20.1% | **28.6%** |

Delivered: 24 tests in `Ui/SmallTabsTests.cs`. **No seams were needed** - like step 7, the
testable surface was already public.

- **Configuration** (both directions): saved settings reaching the controls, a toggle reaching
  `UserSettings`, the theme drop-down including its unknown-value fallback, and the two
  sync-dependent checkboxes being disabled when launch sync is off.
- **Overview**: one row per component with links joined from the separate file/link tables,
  rows starting print-selected, search narrowing (case-insensitive, terms ANDed, blank = no
  filter), and a second `LoadData` replacing rather than appending.
- **About**: version display and its `(unknown)` fallback, the revision-date row shown/collapsed,
  credits listed, which contacts are clickable, and a second board's credits replacing the first.

**`TabFeedback` was deliberately left at 5.1%.** Its substance is an HTTP upload and a
file-picker dialog - real network and a modal, both out of scope per rule 6. Covering it would
mean an abstraction like step 8's `IScopeClient`; worth doing only if the upload logic itself
starts changing.

One more of my expectations was wrong: I assumed `@someone_on_a_forum` would not be treated as a
clickable contact. `ContactLinkFormatter.IsContactEmail` is "contains @ and no space", so it is -
deliberately, per that class's own comment (the no-space rule exists to avoid linkifying prose,
not to validate addresses). The test now pins the real rule with a comment explaining it.

Mutation-verified: always-enabling a sync-dependent checkbox (1 test) and ORing the Overview
search terms instead of ANDing (1).

*Original step description follows, for reference.*


**Why:** the long tail. Do it last, precisely *because* it is easy — it must not crowd out steps
1–8.

**Yield:** ~600–800 lines.

| Tab | Uncovered | Current |
| --- | ---: | ---: |
| `TabConfiguration` | 215 | 42.4% |
| `TabOverview` | 215 | 37.3% |
| `TabFeedback` | 191 | 26.5% |
| `TabAbout` | 84 | 53.3% |

Mostly settings round-trips, list population and visibility toggles. Skip the launcher paths —
`TabConfiguration`'s launchers call `Process.Start`, which rule 6 puts out of scope.

---

## Step 10 — Publish coverage from CI — ✅ DONE

Delivered: `.github/workflows/coverage-summary.sh` plus three changes to
`build-and-unittest.yml` — the existing `dotnet test` step now also collects coverage into
`TestResults/`, a "Coverage summary" step renders the totals into the GitHub job summary, and the
raw Cobertura XML is uploaded as a 7-day artifact for per-file inspection.

Verified locally by running the exact CI command end-to-end: 2,214 tests pass, the report lands
where the workflow's `find` looks for it, and the summary renders **50.7% lines (14,249/28,102)**
and **38.1% branches** — matching the figures measured throughout this document.

Deliberate choices, each of which is a "do not change this later without meaning to":

- **No threshold, no gate.** Coverage is reported, never enforced. A floor set while the suite is
  still growing either blocks unrelated work or is set so low it means nothing. Making it a gate
  is a separate decision for the maintainer.
- **`if: always()`** on both steps, so the figures still appear when the suite is red — a failing
  run is exactly when knowing what was exercised is useful.
- **The release workflow was left alone.** Its test step is a release gate: it must stay fast and
  simply fail on red. Coverage belongs on the health check that runs on every push.
- **Still no percentage committed to any document**, `CLAUDE.md` included. That rule is what this
  step exists to honour, not to work around.

The script degrades gracefully: a missing or malformed report prints a note and exits 0 rather
than failing the build, and it works when run by hand (it guards on `GITHUB_STEP_SUMMARY`), so
`bash .github/workflows/coverage-summary.sh <report>` is a usable local command too.

*Original step description follows, for reference.*


**Why:** `CLAUDE.md` is right that a percentage written into a document goes stale silently and is
then quoted as fact. That is exactly what happened: the working assumption was ~25% overall, when
25.2% is the UI figure and the overall number is 40.2%.

**What to do:** extend `.github/workflows/build-and-unittest.yml` to run the existing suite with
`--collect:"XPlat Code Coverage"` and publish the result — as a build artifact, or parsed into the
job summary as `lines-covered / lines-valid (%)` plus the configuration.

**Do not add a coverage gate that fails the build.** Not yet: a threshold set while these steps are
in flight will either be trivially passable or will block unrelated work. Establish the trend
first; the maintainer can decide on a floor later.

Also **do not write the percentage into `CLAUDE.md`** — its no-figure-in-a-document rule is
deliberate and this step exists to honour it, not to work around it.

---

## Where this lands

| After | Overall | UI only | Character |
| --- | ---: | ---: | --- |
| today | 40.2% | 25.2% | Handlers strong, UI thin |
| step 1 | ~45% | ~32% | Structural unlock |
| steps 2–4 | ~49% | ~37% | Fast, permanent unit tests |
| steps 5–7 | ~56% | ~48% | **Real regression safety net** |
| step 8 | ~59% | ~53% | Last big file addressed |
| step 9 | ~62% | ~57% | Long tail |

Estimates assume ~60–70% of each targeted file becomes reachable, which is what the
already-converted areas actually achieved. Direction, not contract.

### Aim for ~60%, not 80%

A meaningful share of what remains **should** stay uncovered on purpose, and `CLAUDE.md`'s
"deliberately not covered" list is sound engineering judgment rather than a backlog:
`ScopeScpiClient` (real TCP), `MiniproProcessRunner` (spawns a process), `UpdateService` (real
HTTP), `OnlineServices`' network half, and the thumbnail bitmap builders that need a real display.
Chasing a number past that line means writing tests that assert nothing.

### Watch the wall clock

2,055 tests currently run in ~43s with `parallelizeTestCollections: false` serialising every
collection. That is the right call given the shared statics, but steps 5–7 add headless tests
linearly. If the suite approaches a couple of minutes, the lever is **reducing shared static
state** — more injected seams like `ActivateWorkbookOverrideForTests` — so collections can safely
run in parallel again. Do not re-enable parallelism underneath the statics as they stand; the
file's own comment explains why that races.
