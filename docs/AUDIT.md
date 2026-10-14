# Baseline UI audit (before the Frontier production rework)

Captured with the screenshot harness (`tools/capture-screens.ps1`) on four virtual devices: 1080x1920 (2.625 density),
1170x2532 (3.0, iPhone-style notch and home indicator), 1440x3200 (3.5, camera cut-out) and a 1536x2048 tablet (2.0).
41 screens per device, 164 captures, 0 errors. The harness also lints every screen: text size and touch-target size are
converted to dp with the device density, and panels that spill off the screen are flagged.

Contact sheets of the phone captures: [sheet 1](audit/baseline-phone-sheet-01.jpg) ·
[sheet 2](audit/baseline-phone-sheet-02.jpg) · [sheet 3](audit/baseline-phone-sheet-03.jpg).

## Measured (phone 1080x1920)

| Metric | Result | Target |
|--------|--------|--------|
| Smallest text on most screens | **9.1 dp** (11–12 dp on the HUD) | caption >= 12 dp, body >= 14 dp |
| Labels under 12 dp | 20–28 per modal/popup, **45 of 52 on the map** | 0 |
| Smallest touch target | **27 dp** (popup buttons), 31 dp (modal buttons), 44 dp (map) | >= 48 dp |
| Panels spilling off screen | 0 | 0 |

## Findings from real pixels

| # | Screen | Finding | Planned fix |
|---|--------|---------|-------------|
| 1 | HUD | Four unlabeled dock buttons. The build icon reads as a robot head. The menu button (nine dots) collides with the top bar's right edge. | `ui-icon-system-v2`, `ui-hud-dock` |
| 2 | Map | Muddy brown-orange background (galaxy arm strokes at high zoom), HUD bleeds through the map, `+`/`-` buttons cover the galaxy title, selected world is not centred. | `m3-map-ui` |
| 3 | Worlds card | Names only: no harvest method, richness, hazard, tier. Fuel cost shows as a bare "28 / 40" with a drop icon. | `m3-map-ui`, `m4-atlas` |
| 4 | Build / Command | Every card has a bright neon border; ~10 text sizes; very dense small text; Satellite card is "coming soon". | `ui-design-tokens`, `ui-components` |
| 5 | Popups | Structure labels from the 3D world show through the translucent popup. Buttons are 27 dp tall. | `ui-components` |
| 6 | Menu | DEBUG tile and cheat tools are visible to every player. "Ranks" says leaderboards are not connected. | `ui-hud-dock`, `m7-score-leaderboard` |
| 7 | Backpack | One flat grid, no categories, no filter or sort. | `ui-components`, `m4-logistics` |
| 8 | Wave HUD | Tiny "WAVE 1 1/8" and reactor text (9 dp). | `ui-design-tokens` |
| 9 | Mini-games | Visually strong. No pause, abort or result card. Travel is a lane dodger. | `m5-minigame-framework`, `m5-hyperspace-run` |
| 10 | Overall | Same neon weight on every element, so nothing is primary. | `ui-design-tokens` |

These confirm the code-derived audit in the master plan; no finding was contradicted.

## Reproduce

```powershell
powershell -ExecutionPolicy Bypass -File tools/capture-screens.ps1                       # all devices
powershell -ExecutionPolicy Bypass -File tools/capture-screens.ps1 -Devices phone-1080x1920 -Only hud,map
powershell -ExecutionPolicy Bypass -File tools/contact-sheet.ps1 -Dir SpheerURP/Screens/<run>/phone-1080x1920
```

Output goes to `SpheerURP/Screens/<timestamp>/` (git-ignored): PNGs per device, `report.json` (lint) and `done.txt`.

## After tokens/icons (design tokens, fonts, icon system v2, HUD/dock)

Same harness, same four devices, now 45 shots per device (180 captures, 0 errors): the 41 baseline shots plus `menu-release`
(the menu without the DEBUG tile, as a release build shows it) and three icon contact sheets (`icons-ui`, `icons-items`,
`icons-color`). The design system is documented in [DESIGN.md](DESIGN.md).

| Device | Labels < 12 dp (before > after) | Touch targets < 48 dp (before > after) | Overflowing panels | Smallest text | Smallest touch target |
|--------|------|------|------|------|------|
| phone 1080x1920 | 570 > **0** | 196 > **0** | 0 > 0 | 9.1 > **13.0 dp** | 27.4 > **51.8 dp** |
| phone 1170x2532 | 581 > **0** | 199 > **0** | 0 > 0 | 8.7 > **13.0 dp** | 26.0 > **52.0 dp** |
| phone 1440x3200 | 593 > **0** | 204 > **0** | 0 > 0 | 9.1 > **13.0 dp** | 27.4 > **52.0 dp** |
| tablet 1536x2048 | 0 > 0 | 0 > 0 | 0 > 0 | 17.1 > 22.2 dp | 51.0 > 88.5 dp |
| phone 1080x2340, 360 dp (new, opt-in) | n/a > 0 | n/a > 0 | n/a > 0 | 13.0 dp | 52.0 dp |

Totals over the four standard devices: **1744 > 0** labels under 12 dp and **599 > 0** touch targets under 48 dp. Every screen
listed in the brief (hud, build-mode, shop-tab0-3, command-*, pack*, menu*, map* with the world/planet card, popup-*, toast,
wave-hud, wave-result) is at zero on both reference phones, and so are the mini-game overlays (`game-*`, smallest text 13.0 dp).
The four check suites (planets, storage, combat, layout) and the new icon check pass; the project compiles without warnings.

Before and after, phone 1080x1920 (top row before, bottom row after):
[screens 1](audit/after-tokens-icons-screens-1.jpg) · [screens 2](audit/after-tokens-icons-screens-2.jpg) ·
[all icons at 24/32/48/64 px](audit/after-tokens-icons-icon-sheets.jpg).

| # | Baseline finding | Status |
|---|------------------|--------|
| 1 | Unlabeled dock, robot-head build icon, menu button colliding with the top bar | **Fixed.** Four captioned dock buttons (COMMAND, MAP, PACK, BUILD), build = hammer crossed with a saw, hamburger menu button with its own slot beside the top bar. |
| 2 | Map: muddy brown background, `+`/`-` over the galaxy title | **Mostly fixed.** The brown was oversized system halos at too much alpha; they are now faint and capped, the map is opaque deep navy, and the tools sit in a row at the top right. The selected world is still not re-centred (map track). |
| 3 | Worlds card: names only, bare fuel cost | **Restyled only.** Tonal card, WARP is the single primary action; content changes belong to the map/atlas track. |
| 4 | Build / Command: neon on every card, ~10 text sizes | **Fixed.** Tonal cards with thin outlines, accent only on the selected tab/card and the primary button, five type sizes. Satellite is still "coming soon" (content). |
| 5 | Popups: world labels through the popup, 27 dp buttons | **Fixed** for buttons (52 dp) and surfaces (opaque). A world-space label can still peek out just below a popup edge. |
| 6 | DEBUG tile visible to everyone | **Fixed.** Hidden unless `Debug.isDebugBuild || Application.isEditor`. Ranks still says leaderboards are not connected (content). |
| 7 | Backpack: flat grid, no categories | Unchanged (components track). Slot layout tidied: icon on top, count bottom-right. |
| 8 | Wave HUD 9 dp text | **Fixed** (13 dp or larger). |
| 9 | Mini-games | Unchanged apart from text sizes. |
| 10 | Same neon weight everywhere | **Fixed** by the hierarchy rule in DESIGN.md. |

Known remaining issues: the mini-game arena render target is still sized from the old reference height (~75 % of native
resolution on phones); the tutorial copy still describes the old button positions; map world labels stay small at the default
zoom; dynamic FontAssets are stored empty (glyphs are added lazily from the TTF at runtime, so the first use of a character costs a
little once). Details in [DESIGN.md](DESIGN.md#9-known-gaps).
