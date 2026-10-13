# Architecture and workflow

## Code map (`SpheerURP/Assets/Frontier`)

| Folder | Contents |
|--------|----------|
| `Scripts/Data` | ScriptableObject definitions (`ResourceSO`, `PlanetSO`, `StructureSO`, `RecipeSO`, `UpgradeSO`, `EnemySO`, `WaveFormulaSO`, `GameDatabase`, `FrontierSettings`, `ModelLibrary`), `PlanetGenerator`, `DefaultContent` |
| `Scripts/Core` | `GameState`: inventory, upgrades, structures, storage, waves, travel, save/load (to be split, see roadmap) |
| `Scripts/UI` | UI Toolkit interface built in code: `UIRoot*.cs` partials, `UiKit` (tokens mirror, buttons, chips), `Icons` (SVG icon loader and name lookups), `ScreenMetrics` |
| `Scripts/Minigames` | `Minigame` base, Chop/Mine (`NodeGame`), `DrillGame`, `TravelGame`, `MinigameStage` (off-screen 3D stage shown in a UI element) |
| `Scripts/Visual` | planet view, space backdrop, weather, world surface (structures, pads, picking), FX and procedural audio |
| `Scripts/Combat` | `DefenseController`, `AlienActor` |
| `Scripts/Harness` | screenshot harness runtime and UI lint (compiled only in the editor and development builds) |
| `Art` | models, model prefabs, materials, `Fonts` (Chakra Petch and Barlow TTFs, OFL licences, generated SDF FontAssets) |
| `Resources/Frontier` | data assets, shaders, `Tokens.uss` + `Frontier.uss`, `Icons/*.svg`, panel settings, `ModelLibrary` |
| `Editor` | menu tools and validation scripts |

`FrontierBootstrap` (the only object in `Scenes/Frontier.unity` besides a camera and light) builds the camera, post-processing,
planet, backdrop, defense, UI and world surface from the `GameDatabase` asset, then publishes a `FrontierRuntime`.

## Rules of the road

- **Never read `Screen.*` directly in game or UI code.** Use `ScreenMetrics` (size, safe area, density) so the screenshot harness
  and layout tests can render any virtual device.
- **Tools and tests must not touch the real save.** Set `GameState.SaveDirectoryOverride` first (`CheckSupport.IsolateSaves()` in
  editor checks, handled automatically by the harness).
- **Never modify a loaded asset at runtime.** `FrontierBootstrap` instantiates its `PanelSettings`; do the same for any other
  asset you need to change in play mode, otherwise Unity serializes the change back into the asset.
- Data lives in ScriptableObjects under `Resources/Frontier`. `DefaultContent` only bootstraps missing data.
- Text files are stored with LF (`.gitattributes`). Commit messages end with the Copilot co-author trailer.

## Editor tools (Frontier menu)

| Menu | Purpose |
|------|---------|
| Generate Editable Data Assets / Generate Deep Space Planets | bootstrap or extend data assets |
| Build Model Library / Calibrate Model Scales | rebuild the model library and bake scale corrections |
| Validate Planet Model Sizes / Storage / Combat Balance / Panel Layout / Icons | validation suites |
| Build Font Assets | regenerate the SDF FontAssets next to the TTFs in `Art/Fonts` |
| Capture Screens | screenshot harness from inside the editor |
| Apply Mobile Project Settings | portrait lock, IL2CPP, ARM64, stripping |
| Reset Save | delete the local save |

## Command line (PowerShell)

All scripts live in `tools/`. If script execution is disabled, prefix with `powershell -NoProfile -ExecutionPolicy Bypass -File`.

```powershell
tools/run-unity.ps1 -Method Frontier.EditorTools.FrontierPlanetChecks.Run          # any editor method, with watchdog + lock
tools/run-unity.ps1 -Method Frontier.EditorTools.FrontierLayoutChecks.Run -NoQuit   # checks that exit by themselves
tools/capture-screens.ps1 [-Devices phone-1080x1920] [-Only hud,map]               # screenshots + report.json (UI lint)
tools/contact-sheet.ps1 -Dir SpheerURP/Screens/<run>/phone-1080x1920               # labelled overview sheets
tools/image-diff.ps1 -A <runA>/<device> -B <runB>/<device>                         # per-screen visual regression numbers
```

Virtual devices: `phone-1080x1920`, `phone-1170x2532`, `phone-1440x3200`, `tablet-1536x2048`, plus the opt-in 360 dp phone
`phone-1080x2340` (only rendered when named in `-Devices`). Besides the screens, the harness captures the icon contact sheets
(`icons-ui`, `icons-items`, `icons-color`) and `menu-release` (the menu as a release build sees it, without the DEBUG tile).

Unity runs are serialised through a named mutex because only one editor can open the project. Do not pass `-nographics` to anything
that renders. The harness writes to `SpheerURP/Screens/<timestamp>/` (git-ignored).

## Where this is going

See [ROADMAP.md](ROADMAP.md): infinite deterministic universe with delta saves, ~34 resources and an Atlas, a Hyperspace Run
shooter for travel, new harvest games, timers/obstacles, premium currency and rewarded ads, leaderboards, cinematics and release
readiness. Baseline UI findings are in [AUDIT.md](AUDIT.md); the design system (tokens, fonts, icons) is in [DESIGN.md](DESIGN.md);
today's behaviour is in [SYSTEMS.md](SYSTEMS.md).
