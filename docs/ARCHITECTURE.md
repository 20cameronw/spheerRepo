# Architecture and workflow

## Assemblies (`SpheerURP/Assets/Frontier`)

Code is split into assembly definitions that form one downward chain. An assembly may reference only the ones below it, so a
forbidden dependency (a data asset reaching into rendering, game logic reaching into the UI) does not compile.

```mermaid
flowchart BT
  Core["<b>Frontier.Core</b><br/>pure C#, no UnityEngine<br/>shared enums, universe generator,<br/>session services, save schema + protocol"]
  Economy["<b>Frontier.Economy</b><br/>pure C#, no UnityEngine<br/>catalog, costs, travel model,<br/>balance simulator, shards, score"]
  Data["<b>Frontier.Data</b><br/>ScriptableObjects, GameDatabase,<br/>ModelLibrary, PlanetData, DefaultContent"]
  Game["<b>Frontier.Game</b><br/>GameState facade, UniverseService,<br/>AuthoredWorlds, SaveService"]
  Visual["<b>Frontier.Visual</b><br/>planet, backdrop, weather, world surface,<br/>Fx/Sfx, ScreenMetrics, combat"]
  UI["<b>Frontier.UI</b><br/>UI Toolkit screens, UiKit, Icons,<br/>mini-games"]
  App["<b>Frontier.App</b><br/>FrontierBootstrap, FrontierRuntime,<br/>screenshot harness + UI lint"]
  Editor["<b>Frontier.Editor</b> (Editor only)<br/>menus, check suites, tools"]
  TE["<b>Frontier.Tests.EditMode</b><br/>(Editor only)"]
  TP["<b>Frontier.Tests.PlayMode</b>"]
  Economy --> Core
  Data --> Core
  Game --> Data
  Visual --> Game
  UI --> Visual
  App --> UI
  Editor --> App
  TE --> Game
  TP --> App
```

| Assembly | Folder | Contents | Engine |
|----------|--------|----------|--------|
| `Frontier.Core` | `Scripts/Core` | `GameEnums` (`StructureKind`, `MinigameKind`, `UpgradeEffect`, `WeatherKind`, ...); `Universe/`: the infinite deterministic universe generator (namespace `Frontier.Universe`, 15 files); `Session/`: `Inventory`, `Progression`, `WorldStore` (+ `PlanetDelta`, `PlayerStats`), `WarehouseOps`, `IClock`, `HarvestMapping`; `Save/`: save schema v2, the v1 type, `SaveMigrator`, `SaveStore`, `SaveDebouncer`; `Travel/`: `TravelModel`; `Flavor/`: planet descriptions and stable visual seeds. See [RUNTIME_MODEL.md](RUNTIME_MODEL.md) | **none** (`noEngineReferences`) |
| `Frontier.Economy` | `Scripts/Economy` | The whole game economy as pure, deterministic C# (`EconomyCatalog`: 34 resources, 24 recipes, 31 structures with per-level cost/time/effect rows, 6 tool ladders, 16 upgrades, 60 research nodes, obstacles, shard rules; `Tuning`: 606 keys; `TravelModel`, `WorldRules`, `Costs`, `JobTimes`, `ShardModel`, `ScoreFormula`; `Sim*`: the balance simulator and its era bands). Referenced by Data/Game when the content is wired in; see [economy/README.md](economy/README.md) | **none** (`noEngineReferences`) |
| `Frontier.Data` | `Scripts/Data` | `ResourceSO`, `PlanetSO` (authoring format of the 30 Core Worlds), `PlanetData` (the runtime planet), `StructureSO`, `RecipeSO`, `UpgradeSO`, `EnemySO`, `WaveFormulaSO`, `GameDatabase`, `FrontierSettings`, `ModelLibrary`, `ItemAmount`, `PlanetGenerator`, `DefaultContent` | UnityEngine |
| `Frontier.Game` | `Scripts/Game` | `GameState` (facade over the Core services: `GameState.I`, `PlacedStructure`), `UniverseService`, `AuthoredWorlds`, `Save/` (`SaveService`, the `JsonUtility` codec): the whole non-rendering game model | UnityEngine |
| `Frontier.Visual` | `Scripts/Visual` | `PlanetView`, `SpaceBackdrop`, `Weather`, `WorldSurface` (structures, pads, picking), `Fx` + `Sfx`, `ScreenMetrics`; `Combat/`: `DefenseController`, `AlienActor` | UnityEngine, URP |
| `Frontier.UI` | `Scripts/UI` | `UIRoot*.cs` partials, `UiKit` (tokens mirror, buttons, chips), `Icons`; `Minigames/`: `Minigame` base, `NodeGame`, `DrillGame`, `TravelGame`, `MinigameStage` | UI Toolkit, URP |
| `Frontier.App` | `Scripts/App` | `FrontierBootstrap` (scene entry), `FrontierRuntime`, `FrontierApp` (frame tick and save flushes on pause, focus loss and quit); `Harness/`: `HarnessRunner`, `UiLint` (compiled only in the editor and development builds) | everything |
| `Frontier.Editor` | `Editor` | menu tools and validation suites (`Frontier.EditorTools.*`) | UnityEditor, all of the above |
| `Frontier.Tests.EditMode` | `Tests/EditMode` | NUnit tests for Core, Data and Game (`Core/`, `Universe/`, `Game/`, `Fixtures/`); Editor only | UnityEditor, Test Runner |
| `Frontier.Tests.PlayMode` | `Tests/PlayMode` | smoke tests that boot the real session (every kind of generated world renders, the debounced save is written by the app component); the universe golden digest (so it can also run on a device) | everything |

All asmdefs are `autoReferenced: false` and reference each other by name. There is no `Assembly-CSharp` any more: a script placed
outside the folders above would compile into a new one that cannot see any Frontier type, so every new script goes under one of them.

Why these boundaries (and not one assembly per folder):

* **Visual + Combat, UI + Mini-games are merged on purpose.** Combat sits on top of `WorldSurface`/`PlanetView`/`Fx`, nothing in Visual
  calls into it, so a separate assembly would only add a reference. The mini-games are UI Toolkit overlays built with `UiKit`, and
  `UIRoot` starts them directly, so splitting them from `UIRoot` would force a cycle (`UI -> Minigames -> UiKit`) or a third "kit"
  assembly. Both can be split later without code changes (move the folder, add an asmdef) if they grow.
* **The low-level helpers sit at the bottom of the presentation layers.** `Fx`/`Sfx` and `ScreenMetrics` are used by every presentation
  class, so they live in Visual; the colour clamp that `WorldSurface` and `AlienActor` borrowed from `UiKit` is now `Fx.Displayable`
  (`UiKit.Ui` forwards to it). `PlanetSO.StandardSurfaceRadius` replaced `PlanetView.Radius` as the source of truth, which removed the only
  `Data -> Visual` edge (`PlanetView.Radius` still exists and aliases it).
* **The harness lives in `Frontier.App`.** It drives the live `FrontierRuntime`, which only the bootstrap can publish, so a
  separate Harness assembly would be a cycle. Its code is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
* Assembly count stays small on purpose: seven runtime/editor assemblies plus two test assemblies. Every assembly adds reload and
  reference overhead; the single downward chain is what protects the layering.

Rules of the road for the assemblies:

* **Core is pure.** No `UnityEngine`/`UnityEditor`, no `System.Random`, `string.GetHashCode`, `DateTime.Now` in anything that must be
  deterministic. Anything you want to unit test without Unity or run off the main thread belongs here.
* **Data holds only authored data and its lookups.** No GameObjects, no rendering, no UI.
* **Game has no rendering and no UI.** Presentation observes it through events (`GameState.Changed`, `PlanetView.Rebuilt`), never the
  other way round.
* **Session logic is id based.** The Core services (`Inventory`, `Progression`, `WorldStore`, ...) know strings, never
  ScriptableObjects; `GameState` adds the `ResourceSO` / `StructureSO` / `UpgradeSO` convenience overloads. New per-world data goes
  into `PlanetDelta` and the save schema (see [RUNTIME_MODEL.md](RUNTIME_MODEL.md) section 8).
* **One planet type.** Systems take a `PlanetData` (authored or generated), never a `PlanetSO`; `PlanetSO` is the authoring format
  of the Core Worlds and is only read by `AuthoredWorlds`, `PlanetData` and the map.
* **Visual knows nothing about UI Toolkit screens; UI knows Visual.** Pass data or delegates upward instead of reaching down
  (`FrontierBootstrap` wires `IsOverUi`, `Rebuilt`, ... for exactly this reason).
* **Nothing references `Frontier.Editor`.** Editor-only code never leaks into player builds.
* Keep namespace `Frontier` for runtime code (the assembly is not a namespace); editor tools use `Frontier.EditorTools`.

### Where new code goes

| You are adding | Put it in |
|----------------|-----------|
| An enum or POCO shared by data, logic and rendering; hashing, noise, universe generation, score or economy formulas | `Frontier.Core` (stay engine-free) |
| A ScriptableObject type, a content table, a data lookup | `Frontier.Data` |
| Inventory, wallet, timers, progression, save/load, anything the UI reads but that draws nothing | `Frontier.Game` |
| A MonoBehaviour that draws or animates the world (planet, structures, enemies), shared FX/audio, device metrics | `Frontier.Visual` |
| A screen, panel, widget, icon, mini-game | `Frontier.UI` |
| Scene entry, wiring of the pieces above, debug/harness hooks | `Frontier.App` |
| A menu item, importer, validation suite | `Frontier/Editor` (`Frontier.EditorTools`) |

If code seems to need a type from a layer above, the type is in the wrong place or an event/delegate is missing; do not add a
reference upward.

## Code map (`SpheerURP/Assets/Frontier`)

| Folder | Contents |
|--------|----------|
| `Scripts/Core` | `Frontier.Core`: `GameEnums.cs`; `Universe/` (15 files): the pure, deterministic universe generator; `Session/`, `Save/`, `Travel/`, `Flavor/`: the engine-free session services, save schema and protocol, travel formula and planet descriptions |
| `Scripts/Economy` | `Frontier.Economy`: the economy catalog, tuning, costs, travel/world rules, shards, score formula and the balance simulator (pure C#; nothing wired into gameplay yet) |
| `Scripts/Data` | `Frontier.Data`: ScriptableObject definitions (`ResourceSO`, `PlanetSO`, `StructureSO`, `RecipeSO`, `UpgradeSO`, `EnemySO`, `WaveFormulaSO`, `GameDatabase`, `FrontierSettings`, `ModelLibrary`), `PlanetData` (runtime planet), `ItemAmount`, `PlanetGenerator`, `DefaultContent` |
| `Scripts/Game` | `Frontier.Game`: `GameState` (facade: inventory, upgrades, structures, storage, waves, travel; `GameState.Persistence.cs` holds save/load, new game and debug helpers), `UniverseService`, `AuthoredWorlds`, `Save/` |
| `Scripts/Visual` | `Frontier.Visual`: planet view, space backdrop, weather, world surface (structures, pads, picking), FX and procedural audio, `ScreenMetrics`; `Combat/` holds `DefenseController` and `AlienActor` |
| `Scripts/UI` | `Frontier.UI`: UI Toolkit interface built in code: `UIRoot*.cs` partials, `UiKit` (tokens mirror, buttons, chips), `Icons` (SVG icon loader and name lookups); `Minigames/` holds the `Minigame` base, Chop/Mine (`NodeGame`), `DrillGame`, `TravelGame`, `MinigameStage` (off-screen 3D stage shown in a UI element) |
| `Scripts/App` | `Frontier.App`: `FrontierBootstrap`, `FrontierRuntime`, `FrontierApp` (lifecycle: tick, save flushes); `Harness/` is the screenshot harness runtime and UI lint (compiled only in the editor and development builds) |
| `Art` | models, model prefabs, materials, `Fonts` (Chakra Petch and Barlow TTFs, OFL licences, generated SDF FontAssets) |
| `Resources/Frontier` | data assets, shaders, `Tokens.uss` + `Frontier.uss`, `Icons/*.svg`, panel settings, `ModelLibrary` |
| `Editor` | `Frontier.Editor`: menu tools and validation scripts |
| `Tests` | `EditMode/` (`Core/`, `Universe/`, `Game/`, `Fixtures/`) and `PlayMode/` NUnit tests, see [TESTING.md](TESTING.md) |

`tests/` at the repository root holds the plain .NET projects that compile `Frontier.Core` and `Frontier.Economy` (netstandard2.1) and
run the engine-free tests without Unity; it sits outside `SpheerURP/` so Unity never imports it.

`tools/labs/` holds two console labs (universe, economy) that compile the same sources in place with the compiler and .NET runtime
inside the Unity editor: tests, density maps, the balance simulator, charts and the JSON/Markdown exports published to
[docs/economy](economy/README.md). See [tools/labs/README.md](../tools/labs/README.md).

`FrontierBootstrap` (the only object in `Scenes/Frontier.unity` besides a camera and light) builds the camera, post-processing,
planet, backdrop, defense, UI and world surface from the `GameDatabase` asset, then publishes a `FrontierRuntime`.

## Rules of the road

- **Never read `Screen.*` directly in game or UI code.** Use `ScreenMetrics` (size, safe area, density) so the screenshot harness
  and layout tests can render any virtual device.
- **Tools and tests must not touch the real save.** Set `GameState.SaveDirectoryOverride` first (`CheckSupport.IsolateSaves()` in
  editor checks, `SaveSandbox` in EditMode tests, handled automatically by the harness). Changes are saved debounced (about a second
  after the last one, immediately on pause, focus loss, quit and travel): a test that needs the file calls `Flush()` or `Save()`.
- **Never modify a loaded asset at runtime.** `FrontierBootstrap` instantiates its `PanelSettings`; do the same for any other
  asset you need to change in play mode, otherwise Unity serializes the change back into the asset.
- Data lives in ScriptableObjects under `Resources/Frontier`. `DefaultContent` only bootstraps missing data.
- Text files are stored with LF (`.gitattributes`). Commit messages end with the Copilot co-author trailer.

## Editor tools (Frontier menu)

| Menu | Purpose |
|------|---------|
| Generate Editable Data Assets / Generate Deep Space Planets | create-only bootstrap for fresh data folders; refuses to overwrite existing database/data assets |
| Build Model Library / Calibrate Model Scales / Bake Model Ground Radii | rebuild the model library, bake scale corrections, bake the per-model ground radius that generated worlds use |
| Validate Planet Model Sizes / Storage / Combat Balance / Panel Layout / Icons | validation suites (the batch gate, see [TESTING.md](TESTING.md)) |
| Universe > Universe Viewer | read-only seed/coordinate preview with density heat map, sector/system/planet inspector and descriptor copy |
| Universe > Render Density PNG | draws the galaxy density map of the universe generator (30,000 ly across) to `SpheerURP/Screens/universe/` |
| Build Font Assets | regenerate the SDF FontAssets next to the TTFs in `Art/Fonts` |
| Capture Screens | screenshot harness from inside the editor |
| Apply Mobile Project Settings | portrait lock, IL2CPP, ARM64, stripping |
| Reset Save | delete the local save |

## Command line (PowerShell)

All scripts live in `tools/`. If script execution is disabled, prefix with `powershell -NoProfile -ExecutionPolicy Bypass -File`.

```powershell
tools/run-unity.ps1 -Method Frontier.EditorTools.FrontierPlanetChecks.Run          # any editor method, with watchdog + lock
tools/run-unity.ps1 -Method Frontier.EditorTools.FrontierLayoutChecks.Run -NoQuit   # checks that exit by themselves
tools/run-checks.ps1 [-Only Storage,Layout]                                        # the five check suites (the gate)
tools/run-tests.ps1 -Platform EditMode|PlayMode [-TestFilter ...] [-ShowAll]       # Unity Test Framework, pass/fail summary
tools/capture-screens.ps1 [-Devices phone-1080x1920] [-Only hud,map]               # screenshots + report.json (UI lint)
tools/contact-sheet.ps1 -Dir SpheerURP/Screens/<run>/phone-1080x1920               # labelled overview sheets
tools/image-diff.ps1 -A <runA>/<device> -B <runB>/<device>                         # per-screen visual regression numbers
cd tests; dotnet test Frontier.Core.Tests/Frontier.Core.Tests.csproj               # engine-free tests, no Unity needed
```

Virtual devices: `phone-1080x1920`, `phone-1170x2532`, `phone-1440x3200`, `tablet-1536x2048`, plus the opt-in 360 dp phone
`phone-1080x2340` (only rendered when named in `-Devices`). Per device the harness captures 65 screens: the original 45 (numbers and
names are stable, so runs stay comparable with `image-diff.ps1`) followed by 20 generated-world screens (`gen-*`, a large world with
sixteen buildings, the map and the debug teleport menu). Besides the screens it captures the icon contact sheets
(`icons-ui`, `icons-items`, `icons-color`) and `menu-release` (the menu as a release build sees it, without the DEBUG tile). Each
`gen-*` shot logs the world's descriptor (`PlanetData.Dump()`) to the Unity log.

Unity runs are serialised through a named mutex because only one editor can open the project. Do not pass `-nographics` to anything
that renders. The harness writes to `SpheerURP/Screens/<timestamp>/` (git-ignored).

## Where this is going

See [ROADMAP.md](ROADMAP.md): the map and travel rework on top of the generated universe, ~34 resources and an Atlas, a Hyperspace Run
shooter for travel, new harvest games, timers/obstacles, premium currency and rewarded ads, leaderboards, cinematics and release
readiness. How worlds, the session and the save work today is in [RUNTIME_MODEL.md](RUNTIME_MODEL.md). Baseline UI findings are in
[AUDIT.md](AUDIT.md); the design system (tokens, fonts, icons) is in [DESIGN.md](DESIGN.md); today's behaviour is in
[SYSTEMS.md](SYSTEMS.md).
