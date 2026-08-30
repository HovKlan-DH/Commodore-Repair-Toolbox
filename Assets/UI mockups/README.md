# Worklog mockups

Interactive UI mockups for the proposed repair-worklog feature. Open
[`worklog-mockup.html`](worklog-mockup.html) in a browser — it is self-contained (no network, no
build) and has a concept switcher along the top.

The mockups are drawn at **1.5×** the app's logical units, matching a 150 % DPI screenshot.
Divide by 1.5 to get Avalonia values: `16.5px` in the mockup is `FontSize="11"`, `12px` padding is
`Padding="8"`, `6px` gaps are `Spacing="4"`.

Colours in the mockup are the resolved values of existing keys in
[`Main/App.axaml`](../Main/App.axaml) — use the keys, not the hex codes:

| Mockup hex | Resource key |
| --- | --- |
| `#f2f2f2` | `Schematics_Panels_Bg` |
| `#808080` | `Schematics_Panels_Border`, `Form_Border` |
| `#f0e68c` | `Schematics_Name_Bg` / `Thumbnail_Bg` (Khaki) |
| `#cd5c5c` | `Main_TabUnderline_Selected`, `Schematics_Region_PAL_Bg` (IndianRed) |
| `#d3d3d3` / `#a9a9a9` | `Button_Bg` / `Button_Border` |
| `#4f8a5b` / `#3e6e48` | `Button_Ok_Bg` / `Button_Ok_Border` |

New colours the feature needs (category of a worklog entry) have no key yet and should get one per
theme: `Worklog_Category_Note` `#8A8A8A`, `Worklog_Category_Cosmetic` `#2F6FB5`,
`Worklog_Category_Suspected` `#C8880E`, `Worklog_Category_Confirmed` `#CD5C5C` (IndianRed, as
`Text_Fail_Fg`), and `Worklog_State_Fixed` `#4C8C31` for the check badge.

## The concepts

**Current UI** — the Schematics tab as it is today, for diffing against the others.

**A; Create entry**
- A full-width job bar above the `TabControl` in [`Main/Main.axaml`](../Main/Main.axaml), in the same
  row group as the existing banners. Shows the active job, its status and entry count, and the
  actions *Add entry* / *Close job*. Visible from every tab, so any capture knows its target.
- On the schematic, hold and drag a rectangle of any size. On release the quick card opens under it,
  anchored to that rectangle in image coordinates.
- Right-clicking a component opens the same card anchored to the component instead of an area.
- Worklog marks for the active job draw on the board, toggled by a new row in the Global settings
  panel ("Show worklog marks for active job"), plus a small legend panel.

**B; Full entry details**
- Opened from *More details…* on the quick card, and from any saved entry. A separate window in the
  same style as `ComponentInfoWindow`.
- Left column: title, date/time, category, state, closed-on date, description, part used, time
  spent, cost, and a measurements table (point / value / expected).
- Right column: the anchor with a board preview and *Re-draw area* / *Change side*, related
  components, captioned photos with a cover image, oscilloscope captures, and other files.
- The split is intentional: the quick card holds only what can be typed with a soldering iron in the
  other hand, and saving it already writes a valid entry. Everything else is in the editor, and any
  entry can be reopened there, so the card never has to grow.

**C; Worklog tab**
- A new top-level tab. Left: jobs for the currently selected board, with a *New job* button and a
  search field. Right: the selected job, with the board as the centre of the view — each entry is a
  numbered pin on the board and a numbered card in the list beside it, and selecting either
  highlights the other. Filter chips by status; a side toggle for top/bottom.

## Data model as drawn

- A **board** is identified by a free-text **Board ID** — a serial number where one exists, the
  owner's own label otherwise, or nothing at all. It is not required and not unique-enforced. A board
  can have several jobs over time — in the mockup `#0042` and `#0031` are both ID 249381.
- A **job** is locked to one hardware + board (Commodore 64 / 250469), and carries an id, board ID,
  title, status (open/closed), start date, and its entries.
- An **entry** carries two independent fields plus its content:
  - **Category** — what it is: `Note` (documentation, no fault), `Cosmetic` (flux, scratches, old
    repair marks; no electrical meaning), `Suspected` (may be a problem, needs investigation),
    `Confirmed` (established by measurement or observation).
  - **State** — where it stands: `Pending` → `Fixed` or `Ruled out`. The third value is needed for
    the common case of a suspicion that was tested and cleared. Notes typically stay `Pending` or are
    closed straight away.
  - Then: date/time, title, description, anchor, related components, part used, measurements, photos,
    oscilloscope captures, files, time spent, cost, closed-on date.
- On the board, the pin colour is the **category** and a green check badge is the **fixed** state; a
  ruled-out entry draws faded with a grey cross. Two axes, one glance, no extra colours.
- Replacing a part is *work done*, not a status — it lives in the description and the part-used
  field, which is why the earlier "Replaced / Verified" values are gone.
- An **anchor** is either a component reference (`C36`), a rectangle in image coordinates on a named
  schematic and side, or nothing.
- Everything is local. One folder per job under the data root; attached photos are copied into it,
  never referenced in place. Nothing is uploaded and nothing goes near the contribution flow.

## Open points

- Should the marks overlay reuse `SchematicHighlightsOverlay` or be its own layer?
- Should a drag-created area pre-fill *Related components* from the highlight bounds it encloses?
- Is a PDF/HTML export of a job worth having in the first version?
