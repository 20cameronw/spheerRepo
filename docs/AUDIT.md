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
