# Frontier design system

How the Frontier UI looks and why. Code lives in `SpheerURP/Assets/Frontier`: tokens and styles in `Resources/Frontier/Tokens.uss`
and `Frontier.uss`, helpers in `Scripts/UI/UiKit.cs` and `Icons.cs`, fonts in `Art/Fonts`, icons in `Resources/Frontier/Icons`.
Findings that drove this work are in [AUDIT.md](AUDIT.md); the build-out plan is in [ROADMAP.md](ROADMAP.md).

## 1. Principles

1. **One thing is loud.** Neon colour plus glow is reserved for the **primary action**, the **selected item** and **live state**
   (a running wave, an armed build pad, a pressed button). Ordinary cards, rows, tabs and dock buttons are *tonal dark glass*: a
   dark surface with a thin, muted outline. A screen should read in one glance: what can I do, what is selected, what is live.
2. **Neon-space identity stays.** Deep navy surfaces, cyan/magenta/lime/amber accents, glowing planets behind the UI.
3. **Tokens, not numbers.** Sizes, spacing, colours and type come from `Tokens.uss` (or the `UiKit.Space/Size` mirrors in code).
   If a value is not in the scale, the scale is wrong, not the call site.
4. **Readable on a phone first.** Caption >= 12 dp, body >= 14 dp, every touch target >= 48 dp. The screenshot harness lints this
   on every screen of every device.
5. **Any portrait screen.** Layout is flex-based with a capped canvas width, safe-area aware, and verified on four virtual
   devices (plus an opt-in 360 dp phone).

## 2. Units and scale

The UI is rendered by one `PanelSettings.scale`. `UIRoot.ComputeScale(shortSidePx, density)` returns
`max(density / 2, shortSidePx / 900)`:

* On phones **1 UI unit = 0.5 dp** (`UIRoot.UnitsPerDp = 2`). A 411 dp phone is a 822-unit wide canvas, 390 dp is 780, 360 dp is 720.
* The canvas is never wider than **900 units** (`UIRoot.MaxCanvasWidth`). Tablets scale *up* instead of stretching the layout:
  the 1536x2048 tablet runs at 1.707 px per unit.
* The old `UiBoost` (a fixed 1.2 multiplier on a 1080 reference) is gone. It made dp targets impossible to reason about; now a
  token value is simply `dp * 2`.

Consequences used throughout: caption **26 units = 13 dp**, body **30 = 15 dp**, touch **104 = 52 dp**, spacing 8 = 4 dp.

Safe areas come from `ScreenMetrics` (never `Screen.*`). Content layers (popups, modals, banner) are inset by the safe area plus
`UIRoot.HudTopInset` (132, below the top bar) and `UIRoot.DockInset` (176, above the dock). Toasts start below the top bar.
Overlays that replace the world (mini-games, wave result) cover the dock.

## 3. Tokens (`Tokens.uss`)

Defined on `.fr-root`, so every descendant sees them. USS cannot use `var()` inside math or nested in functions
(`rgba(var(--c), .5)` is invalid), so every tint is its own token.

**Linear colour space.** The project renders in linear colour space, where translucent overlays blend far brighter than the same
alpha does in a design tool (an alpha of 0.14 reads like 0.38). Surfaces, outlines and tints are therefore **opaque colours picked
by eye**, identical on every device. Keep alpha for scrims, halos and Painter2D map layers only (and author those very low).

| Role | Token | Value |
|------|-------|-------|
| Background | `--fr-bg-0` | `rgb(3, 5, 16)` |
| Surfaces (glass tiers) | `--fr-surface-1/2/3` | `rgb(8,12,32)` card/panel, `rgb(15,23,50)` inset, `rgb(27,39,78)` hover/raised |
| Outline | `--fr-outline`, `--fr-outline-strong` | `rgb(38,54,92)`, `rgb(76,100,150)` |
| Text | `--fr-text-hi/lo/dim` | `rgb(234,245,255)`, `rgb(166,188,216)`, `rgb(112,132,164)` |
| Neon accents | `--fr-cyan/magenta/lime/amber`, `--fr-danger` | `#1AF2FF`, `#FF40F2`, `#80FF4D`, `#FFB340`, `#FF4D59` |
| Spacing | `--fr-sp-4/8/12/16/24/32` | 4, 8, 12, 16, 24, 32 units |
| Radius | `--fr-r-sm/md/lg` | 10, 18, 26 |
| Type | `--fr-fs-caption/body/label/title/display` | 26, 30, 32, 40, 56 |
| Controls | `--fr-touch`, `--fr-icon-sm/md/lg/xl` | 104, 32, 40, 56, 72 |
| Motion | `--fr-dur-fast/base/slow` | 100, 180, 320 ms |
| Scrim | `--fr-scrim` | `rgba(2,3,12,0.72)` |

**Accent classes.** A panel picks its accent with `.fr-acc-cyan|magenta|lime|amber|red|neutral`, which sets three variables for
all descendants: `--fr-acc` (neon, for text/icon/selected outline), `--fr-acc-line` (the muted version used for the *resting* thin
outline) and `--fr-acc-soft` (dark tint fill for pressed/selected/primary). In code: `UiKit.SetAccent(element, color)` snaps any
colour to the nearest accent class. The C# palette (`UiKit.Cyan`, `Magenta`, `Lime`, `Amber`, `Muted`, `Neutral`) mirrors the CSS
tokens; keep them in sync.

Transitions are limited to `scale` (press feedback). Transitioning background/border colours produced mid-transition grey frames
in captures, so state changes are instant.

## 4. Typography

Two SIL OFL faces, built as TextCore dynamic SDF FontAssets and referenced with `-unity-font-definition`:

* **Chakra Petch** (Medium, SemiBold, Bold): display, titles, labels, buttons, dock captions.
* **Barlow** (Regular, Medium, SemiBold): body, captions, numbers in lists.

`Frontier/Build Font Assets` (`Editor/FrontierFonts.cs`, method `FrontierFonts.Run`) regenerates `*-SDF.asset` next to the TTFs in
`Art/Fonts` (72 pt sampling, 6 px padding, SDFAA). The assets are **dynamic**: glyphs and atlas pages are never serialised
(`Clear Dynamic Data On Build`); the atlas is built from the TTF at runtime the first time each character is drawn (ASCII, Latin and
anything else the font has), only the kerning table is serialised.
Running the game does not change the committed files. Licences: `Art/Fonts/OFL-ChakraPetch.txt`, `OFL-Barlow.txt`.

| Level | Class | Size (units / dp) | Face | Use |
|-------|-------|-------------------|------|-----|
| Display | `.fr-t-display` | 56 / 28 | Chakra Petch Bold | big numbers and result headings (defined, not used yet) |
| Title | `.fr-t-title` (`.fr-title`) | 40 / 20 | Chakra Petch Bold | panel titles, planet name |
| Label | `.fr-t-label` (`.fr-h2`) | 32 / 16 | Chakra Petch SemiBold | card titles, buttons |
| Body | `.fr-t-body` (`.fr-muted`, `.fr-small`) | 30 / 15 | Barlow Medium/SemiBold | descriptions, rows, chips |
| Caption | `.fr-t-caption` (`.fr-tiny`) | 26 / 13 | Barlow Medium | dock captions, hints, section labels |

The legacy class names still work and map onto the scale. Nothing may go below caption; if text does not fit, shorten the copy or
let the row wrap, do not shrink the font.

## 5. Components and hierarchy

| Component | Class(es) | Resting look | Neon only when |
|-----------|-----------|--------------|----------------|
| Dock button | `.fr-dock-btn` | surface-1, thin accent-line outline, accent icon, grey caption | pressed (accent fill + caption) |
| Top bar | `.fr-topbar-bar` | surface-1 pill with a planet-tinted hairline | never (the menu button is a tonal round button) |
| Button | `.fr-btn` | tonal, thin accent-line outline, accent label | hover/press; `-primary` = accent fill + 3 px outline (**one per screen**) |
| Ghost button | `.fr-btn-ghost` | transparent, strong outline, white label | n/a (secondary and back actions) |
| Icon button | `.fr-iconbtn` | tonal square/round, >= 104 units | pressed |
| Modal / popup | `.fr-modal`, `.fr-popup` | surface-1, 2 px muted outline, 3 px accent top edge | n/a (the accent top edge identifies the panel) |
| Tab | `.fr-tab` (column: icon over label) | surface-2, outline | `.fr-tab-on` selected (accent outline + soft fill) |
| Card / row | `.fr-card` | surface-2, thin outline, no halo | `.fr-card-on` selected, `.fr-card-dim` locked/unavailable |
| Item chip | `.fr-itemchip` | tonal pill; icon carries the colour | never |
| Slot (backpack) | `.fr-slot` | surface-2 | selected slot gets the accent halo |
| Toast / banner | `.fr-toast`, `.fr-banner-box` | surface-1 with a thin coloured outline | banner glow marks the armed build mode |

Spacing rules: panel padding 24, gap between cards 12, gap inside a card 8-12, row heights come from `--fr-touch`, never below it
for tappable rows. Corner radius: `r-lg` panels and primary buttons, `r-md` cards and tabs, `r-sm` chips and slots.

## 6. Icon system v2

### Pipeline

Icons are **SVG files** in `Resources/Frontier/Icons/<name>.svg`. Unity's built-in SVG importer
(`Unity.VectorGraphics.Editor.SVGImporter`, default settings) turns each into a `VectorImage`; `Icons.Make(name, color, size)`
loads it with `Resources.Load<VectorImage>` and tints it via `-unity-background-image-tint-color`, so every file is plain white
and gets its colour from a token. The result is resolution independent and antialiased from 24 to 128 px. The old polyline table
(`Painter2D`) and the PNG icons are removed. The Painter2D/SVG-path fallback was not needed: the importer renders arcs, ellipses,
dashes, opacity and round joins correctly, and tinting works on `VectorImage` backgrounds.

Two importer details worth knowing:

* The default tessellation mode ("Antialiased Arc Encodings") **crops the image to the drawn shapes** and ignores the viewBox. Every
  SVG therefore starts with an invisible full-grid rect, `<rect width="48" height="48" fill="#FFFFFF" fill-opacity="0"/>`, which keeps
  the 48x48 grid so all icons share one optical box. Do not remove it.
* Triangulation mode keeps the viewport but is not antialiased; do not switch to it.

### Drawing rules

* 48-unit grid, **2.5-unit stroke**, round caps and joins, ~3 units of safe padding, optical balance over mathematical centring.
* **One metaphor per icon.** No fills, except a faint (12-16 %) fill on object silhouettes so they read as solid at small sizes.
* At most **one small accent** (dot, spark, bubble), as in the flask's bubbles.
* White only (`#FFFFFF`). Colour comes from the tint.
* Every glyph must stay distinct at 32 px. Check it on the icon sheets (below), not in the editor preview.

All 95 icons are original drawings (no third-party shapes, so no attribution file is needed). The economy content
milestone adds 47 glyphs without replacing the existing visual family.

### Set

| Group | Icons |
|-------|-------|
| Dock and controls | `research` (flask, **COMMAND**), `map` (planet with orbit), `pack` (backpack), `build` (**hammer crossed with a toothed hand saw**), `menu` (hamburger), `close`, `plus`, `minus`, `up`, `back`, `check`, `lock`, `play` |
| HUD and glyphs | `fuel`, `star`, `bolt`, `skull`, `sound`, `mute`, `gear`, `ranks`, `tutorial`, `about`, `alien`, `shield`, `tools`, `base`, `warp`, `ship` |
| Resources | `wood` (log end), `stone` (faceted rock), `iron` (ore chunks with a shine), `crystal` (gem), `fuel` (drop), `core` (orb with slit pupil), `scrap` (broken gear), `alloy` (ingot), `plasma` (energy cell), `cube` (generic fallback) |
| Structures | `refinery`, `fabricator` (anvil), `alarm` (beacon), `lumber` (axe), `mine` (pick), `drill`, `gunner`, `laser`, `satellite`, `warehouse` (crates) |
| Economy resources | All 34 canonical resource ids now resolve to distinct glyphs; `alloy` remains for legacy content. Concepts: [economy/ICON_BRIEF.md](economy/ICON_BRIEF.md). |
| Economy structures | 21 additional glyphs cover every appended `StructureKind`, from `foundry` through `dyson_collector`; imported data does not imply implemented gameplay. |

### Naming and lookups

* `Icons.Make("fuel", color, size)`, `Icons.IconButton`, `Icons.Apply`: unknown names fall back to `cube`.
* `Icons.ForResource(resource)`: an authored `icon` override first, then the exact resource id.
* `ForResourceId(id)`: all 34 economy resource ids have dedicated glyphs, then a **keyword category fallback**
  (`"unknown_copper_ore"` gets `iron`, `"helium"` gets `fuel`, `"relic"` gets `core`...), then `cube`.
* `Icons.ForStructure(kind)` and `ForTool(kind)` for structures and harvest tools (`UIRoot.StructureIcon(kind)` delegates).
* `Icons.Groups` lists every shipped name; the contact sheets are built from it.

### Adding an icon

1. Draw `<name>.svg` on the 48 grid following the rules above, including the invisible rect.
2. Drop it into `Resources/Frontier/Icons`; Unity imports it (commit the `.meta`).
3. Add the name to `Icons.Groups` and, if it is a resource/structure, to the mapping above.
4. Run the icon check (**Frontier > Validate Icons**, or
   `tools/run-unity.ps1 -Method Frontier.EditorTools.FrontierIconChecks.Run`): every listed icon imports as a 48x48 `VectorImage`,
   every SVG on disk is listed, every resource and structure has its own glyph, and the keyword fallback works.
5. Run the harness (`-Only icons`) and inspect the original sheets plus the four `icons-economy-*` pages.

### Contact sheets

Harness shots draw icons at **24, 32, 48 and 64 physical px** on a dark surface with labels: `icons-ui` (controls and
glyphs), `icons-items` (legacy resources and structures), `icons-color` (live items in their game colours), and
`icons-economy-resources-1/2` plus `icons-economy-structures-1/2` (complete catalog coverage, paginated to avoid clipping).
They are the acceptance test for legibility and consistency.

## 7. Debug gate

The menu's **DEBUG** tile and page exist only when `Debug.isDebugBuild || Application.isEditor` (`UIRoot.DebugToolsAvailable`). The
harness runs in the editor and still captures them; the `menu-release` shot forces the release behaviour
(`DebugMenuAsRelease()`) to prove the gate hides the tile.

## 8. Verifying a change

```powershell
# render and lint (4 devices; add -Devices phone-1080x2340 for the opt-in 360 dp phone)
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\capture-screens.ps1 -Out <abs dir under SpheerURP\Screens\>
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\contact-sheet.ps1 -Dir <run>\phone-1080x1920 -Columns 7 -Rows 2 -ThumbWidth 270
```

`report.json` has per-shot lint: `minFontDp`, `textUnder12dp`, `minTouchDp`, `touchUnder48dp`, `overflowing`. Acceptance for UI
changes: zero `overflowing` everywhere and zero `textUnder12dp` / `touchUnder48dp` on the phone devices (mini-game overlays may keep
small 3D-adjacent labels but must be reported). Then look at the contact sheets of every device: lint cannot see overlaps,
clipping or inconsistent padding.

## 9. Known gaps

* The mini-game arena render target is sized from the old reference height (`Minigames.cs`), so on phones it is ~75 % of native
  resolution; it was out of scope for this pass.
* World-space structure labels (3D text) can peek out from under a popup.
* The tutorial copy still describes the old button positions (text-size fixes only were in scope).
* Map labels are world-space and scale with the zoom, so the lint ignores them; they remain small at the default zoom by design.
* A UXML/UI Builder migration of the code-built HUD is planned; class names and tokens are already shaped for it.
