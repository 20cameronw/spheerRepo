# Testing

Everything that can run without Unity does. Five layers, fastest first:

| Layer | Covers | Needs | Run it | CI |
|-------|--------|-------|--------|----|
| **Core tests** (plain .NET) | `Frontier.Core`: universe generator, flavour text and golden hashes, plus inventory, progression, world store, save schema/migrator/store/debounce, map queries, route geometry and frozen shared enums (188) | .NET 8 SDK only | `cd tests` then `dotnet test Frontier.Core.Tests/Frontier.Core.Tests.csproj` | `core-tests.yml` (Ubuntu + Windows) |
| **EditMode** (Unity) | Core tests under Unity Mono, plus Data/Game/Economy/Hyperspace tests: authored worlds, universe service, model scales, saves, route planning, content import/runtime selection, simulation, and catalog/balance checks (539 passed in the progression slice; 2 Mono-only allocation probes skip) | Unity 6000.4.0f1 | `tools/run-tests.ps1 -Platform EditMode -IncludeSlow` | `unity-tests.yml` (needs a licence) |
| **PlayMode** (Unity) | Runtime smoke/travel and Hyperspace lifecycle tests: authored database/bootstrap, saves, generated-world rendering and surface sizing, minigame lifecycle, pause/revive/results, and render setup (19) | Unity (a GPU is not required) | `tools/run-tests.ps1 -Platform PlayMode` | `unity-tests.yml` (needs a licence) |
| **Check suites** (Unity editor) | the five `Frontier > Validate ...` checks: the pass/fail **gate** for every change | Unity | `tools/run-checks.ps1` | local only |
| **Screenshot harness** (Unity, GPU) | Current screens across four virtual devices + UI lint; includes generated-world maps and Hyperspace states | Unity + GPU | `tools/capture-screens.ps1` | local only |

Why the Core tests run twice: Unity's runner proves the code under the runtime the game actually uses (Mono in the editor), the
plain .NET project proves it in seconds on any machine and checks the determinism hashes on other operating systems. The same
source files feed both (see below), so there is nothing to keep in sync.

## Running everything locally (Windows PowerShell)

### Android device-test build

`tools\build-android.ps1` creates a **development APK**, not a signed store release. Install this Unity
version's Android Build Support, SDK/NDK and OpenJDK in Unity Hub first. It builds only the fresh Frontier
scene, validates bundled localization/audio and safe notification settings, and uses existing application
identifiers and debug signing. It never configures live providers, purchases or release credentials.
Custom keystores are deliberately rejected; APK output must be a new path and is never overwritten.
The Unity process shares the existing batch mutex/watchdog; temporary APK/export choices are restored.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-android.ps1 -Output .\Builds\Frontier-device-001.apk
```

Install the resulting artifact manually on a test device. A successful build alone does not certify
notification permission/delivery, audio/touch/haptics, offline timing, thermal/memory budgets, store receipt
validation, signing or release readiness. Do not submit this development artifact to a store.
The build log reports the actual APK file length separately from Unity's aggregate build size, which may
include unshipped native debugging/symbol output. The wrapper also checks that the APK exists and is
nonempty, then prints its byte length and SHA-256 for transfer/integrity checks.

**First native development build (2026-10-19):** Android IL2CPP/arm64 succeeded; APK length
51,992,897 bytes (49.6 MiB), minimum API 25 / target API 36, APK Signature Scheme v2 verified.
The current development package is `com.GameDevBois.SpheerURP`; final store identifiers are still unapproved.
Manifest inspection found Internet/network/vibration/post-notification permissions, no exact-alarm or
advertising-ID permission. No physical-device installation or store signing was performed.

Script execution is usually disabled by policy, so prefix scripts with `powershell -NoProfile -ExecutionPolicy Bypass -File`.

```powershell
# Core tests, no Unity (needs the .NET 8 SDK; ~5 s)
cd tests
dotnet test Frontier.Core.Tests/Frontier.Core.Tests.csproj

# Unity tests (batch mode; the log is SpheerURP/Logs/tests-<platform>.log, results SpheerURP/TestResults/<platform>.xml)
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform PlayMode
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode -TestFilter "Frontier.Tests.StorageTests" -ShowAll
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode -OutputMatch "digest"   # print the golden hashes

# The gate: the five editor check suites (planet sizes, storage, combat, icons, layout)
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-checks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-checks.ps1 -Only Storage,Layout

# Screens and visual regression (see ARCHITECTURE.md; acceptance rules below)
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\capture-screens.ps1 -Devices phone-1080x1920
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\capture-screens.ps1 -Devices phone-1080x1920 -Only "gen-,map-generated,menu-debug-teleport"
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\image-diff.ps1 -A <runA>\phone-1080x1920 -B <runB>\phone-1080x1920
```

`run-tests.ps1` goes through `run-unity.ps1`, so it shares the named mutex (only one editor opens the project at a time), the
watchdog and the filtered log. It prints every failing test with its message and the first stack lines, plus a
`passed/failed/skipped` line, and exits `0` (all passed or skipped), `1` (a test failed), `2` (no results: compile error, crash or
timeout) or `3` (no test ran, usually a wrong filter). It never passes `-quit` (the test runner exits by itself) and does not pass
`-nographics`, so tests that render keep working. In the Unity editor the same tests are in **Window > General > Test Runner**.

Never let tests or tools touch the real save: set `GameState.SaveDirectoryOverride` first. The EditMode tests do it with
`SaveSandbox` (`Tests/EditMode/Game/SaveSandbox.cs`), the PlayMode tests in their `SetUp`, the editor checks with
`CheckSupport.IsolateSaves()`. Changes are saved *debounced*, so a test that looks at the file calls `GameState.Flush()` or `Save()`
first; tests of the debounce itself drive a `FakeClock` instead of waiting (`GameState(db, loadSave, clock)`).

### Economy runtime cutover regressions

`EconomyRuntimeTests` covers active/cached selection, guarded Inspector imports, missing/unsupported/incomplete imports,
missing world/combat/rule links and aliases, `DefaultContent` fallback, rebound resource references, cumulative effects,
crafted-only fuel, exact station/level/reach/research gates, legacy tools, the real v1 fixture, mixed-id v2 inventories and
warehouse slots, upgrade-alias merging, idempotent reloads, legacy fallback roundtrips and newer-save read-only protection.
`ContentImportTests` still imports twice byte-for-byte while preserving the **legacy** database asset. The PlayMode
bootstrap smoke test also asserts that the imported content is the live default.
Hyperspace component loot resolves the legacy `alloy` id to selected `steel`, while a legacy fallback still awards `alloy`.

Focused command (use the full suites before landing):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Platform EditMode -TestFilter Frontier.Tests.EconomyRuntimeTests
```

The runtime coverage check now follows raw resource definitions instead of requiring harvestable fuel. `ProgressionContentTests`
verify research spending/save-load, supported tool-tier yield/level/save-load, invalid-entry handling, and preservation through a
legacy-content fallback. Builder/timer jobs and unsupported tool minigames still need their separate wiring. Focused Atlas encounter coverage verifies that the first actual alien spawn is recorded once, grants no unspecified reward and survives save/reload; resource-location samples and missing-cost navigation are present. `StorageTests;EconomyRuntimeTests` cover local storage capacity, stack/conservation edges, category-filtered quick transfers, Cargo Depot pooling/exclusions, same-system relay energy payment and upgraded remote range; the focused run passed 45 tests. Atlas/cost-navigation regression across economy, storage, progression and save round-trips passed 60 tests; all five Atlas phone captures linted cleanly.

`FxBurstPoolTests` checks reuse of the particle, bolt and ripple pools, and verifies that multiple `GlowQuad` renderers retain independent tints while sharing the cached material. Run the focused PlayMode regression with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform PlayMode -TestFilter FxBurstPoolTests
```

Saved map pins and surveyed-system history are covered by `SaveRoundTripTests`; compact map-discovery bitsets are tested for legacy-ID migration and multi-index sectors. `MapQueryTests` verifies that previously discovered systems remain surveyable for resource filters. The bootstrap smoke test opens the live map, checks that the home system is recorded, pins the selected world and verifies its destination entry appears.

`HyperspaceView.SetupTests` verifies the route duration reaches the run setup, and `Hyperspace.DirectorTests` checks requested run lengths and the minimum boss encounter window. The legacy distance-based duration remains covered by `RouteDurationGrowsFromTwentySecondsToNinety`.

**Cutover validation (2026-10-13):** final full EditMode including Slow: 535 passed / 2 Mono-only allocation probes skipped;
PlayMode: 19 passed; all five editor suites (Planet, Storage, Combat, Icon, Layout) passed with zero compile diagnostics;
economy console lab: 127 passed. The transition exposed old harvestable-fuel coverage and over-capacity travel-test
assumptions; the tests now follow the selected raw table and provision enough backpack capacity for an exact refund.
No new screenshot sweep or on-device/IL2CPP run was performed for this bounded milestone.

## Screenshot harness acceptance

### Harvest rule and bot regressions

`NodeHarvestRun`, `DrillHarvestRun`, `SiphonHarvestRun`, `ExcavationHarvestRun` and `SalvageHarvestRun` live in `Frontier.Core`
and drive all six actual harvest games, not separate test-only simulations. Their tests are linked into the .NET test project
and also run under Unity Mono. They cover the
existing damage/yield rules, rejected cooldown spam, stage overkill, tool scaling, bounded bot completion, deterministic
replays, pressure/contact and invalid inputs. `MinigameFlowTests` also drives the rendered games through completion,
verifies paused input is ignored, and waits for explicit collection before the exactly-once result callback.
The common harvest input gate also rejects input before scene readiness, after completion and after stage cleanup.

```powershell
dotnet test .\tests\Frontier.Core.Tests\Frontier.Core.Tests.csproj --filter "FullyQualifiedName~HarvestRun"
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode -TestFilter "NodeHarvestRunTests;DrillHarvestRunTests;HarvestRunsTests"
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform PlayMode -TestFilter MinigameFlowTests
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\capture-screens.ps1 -Devices phone-1080x1920,phone-1080x2340 -Only "game-chop,game-mine,game-drill,minigame-result,minigame-drill-result"
```

The drill screenshot cases locate a real generated drilling deposit. A missing deposit or failed game start is an error,
not a passing screenshot of the world HUD. Siphon bots respect timing/cooldown, excavation bots solve using only Manhattan
echo feedback across 64 seeds, and salvage bots cover captures, misses and partial/zero yield. Rendered bots cover all six
games, shared tool/cooldown feedback and the excavation grid's visual reset between seams. These are numerical/action-path
bots, not automatic screen-space aiming or a substitute for touch usability and on-device performance checks.

`HarvestFeedbackTests` verifies that all harvest cue/tool combinations reuse warmed clips, real playback stays within
the ten-source voice pool, and destroyed audio hosts are treated as uninitialised. It also checks that the haptics request
hook is independent of audio mute and does not request vibration on misses, glances, sonar clues or cooldown spam.
`HarvestHaptics` subscribes on enable and unsubscribes on disable/destruction; it shares the audio camera's lifecycle.
The persisted `fr_haptics` preference defaults off and is independent of sound. `HapticPulseTests` covers support/opt-in,
120ms minimum cadence, zero-strength rejection, focus/disable cancellation, disposal and preference restoration.
Android uses a cached vibrator service with short `VibrationEffect` pulses (legacy short-pulse fallback before API 26).
The build hook adds `android.permission.VIBRATE` idempotently without replacing the generated manifest. iOS uses
`UIImpactFeedbackGenerator` and hardware capability detection; the build hook links UIKit/CoreHaptics.
Editor/desktop support is explicitly false, and Settings explains why its control is disabled.
Both managed runtime branches and platform build hooks compile against the installed Unity modules, but that is not a
native iOS build, Android device run or physical haptics verification. Check focus loss, app suspension, opt-out,
hardware capability, pulse strength/cadence and OS-level settings on real devices before release.
The live games use procedural cached audio cues; device audio-quality checks remain required.

`FxBurstPoolTests` waits for burst, bolt and ripple effects to expire, then verifies the next effect reuses the warmed
particle system or transient object rather than growing its pool. `GlowQuad` uses shared cached materials with
per-renderer tint but still creates persistent scene objects; prefab-based authoring and native-device performance remain separate work.

Empty-state navigation is covered by rendered smoke cases for backpacks, undeveloped bases, empty storage and relays
without remote sources. Routes must open a real gatherer/shop/map and never grant resources. The `pack-empty` capture
snapshots and restores every carried resource in `finally`; capture it together with a later `menu-settings` shot on
both phone profiles to detect fixture leakage. After this slice the full PlayMode suite passed 32 cases and Layout
passed; four captures had no errors, overflow, undersized text or undersized touch targets.
Result tests poll for the actual scheduled panel with a five-second deadline, not a fixed post-animation timing margin.

### Presentation budgets and live eras

`PresentationBudgetTests` covers mobile hardware boundaries (including unknown memory), explicit preset limits,
portrait/landscape/hardware texture caps, aspect rounding, no upscaling and invalid input. `PresentationQualityTests`
checks persisted choices, immediate frame targets, authored frame caps and explicit recovery from corrupt preferences.
Rendered smoke coverage submits the actual Settings controls and checks the live camera plus real harvest/travel RTs.
The `quality-low-*` captures preserve and restore preference presence/values, so later shots/devices do not inherit Low.
These checks do not certify physical-device fps, thermals, memory or battery usage.

`EraRulesTests` verifies all six simulator/live era gates, compound requirements and simulator milestone identities.
`EraProgressionTests` covers actual landing/lab/upgrade facts, a permanent reached-era high-water mark even after losing
a project, save/reload/reset, old v2 saves without a marker and crafting proof versus merely owning antimatter.
Inference and reload grant no retroactive premium currency. The rendered bootstrap case opens the Ranks goal card and
submits its actual next-era map action.

After live-era integration the full engine-free suite passed 433 cases; full Unity EditMode passed 684 with two
runtime-specific allocation probes skipped; full PlayMode passed 33. All five editor checks passed. Final phone captures
include Ranks and restored/Low Settings, with all overflow/text/touch thresholds checked explicitly.

### One-time objectives and achievements

`ObjectiveLedgerTests` covers duplicate normalization, malformed records, unknown-id preservation, historical baselines,
exactly-once claims and snapshot isolation. `AchievementRewardsTests` uses the existing small/medium/large tuning and
rejects unbudgeted or recurring ids. `ObjectiveProgressionTests` checks the authored 26-entry/275-shard lifetime budget,
real facts, unfinished/offline construction, atomic reward/ledger persistence, old saves, reload/reset, lost evidence,
read-only saves, a full wallet and explicit failed-write/retry handling without duplicate payment.
`SmokeTests.Goals_CollectsRealReward_AndRoutesToTheBuildShop` submits actual UI controls.

Post-objectives full regression: 444 engine-free cases; 705 Unity EditMode passed with two runtime-specific allocation
skips; 34 PlayMode passed; all five editor checks passed. Four initial Goals/Ranks phone captures plus two final Goals
captures have zero errors, overflowing elements, sub-12dp text or sub-48dp controls. Both phone widths were inspected.
These goals introduce no new recurring/time-trusted reward path; the full economy simulator/band tests remain green.

### First-session guidance and funnel regressions

`FirstSessionFunnelTests` checks baseline vs. completed objectives, per-observation high-water deduplication, skip/reset,
provider failures, and consent withdrawal/regrant. No events are queued while consent or configuration is unavailable,
and an initial observation of an existing save does not invent historical completions. Only stable step/objective fields
are sent: no save identity, world coordinates, names or resources. The external analytics provider remains unconfigured.
`SmokeTests.FirstSessionCues_PointToLiveActions_AndRespectSkipAndAffordability` exercises the real guide actions and
build/harvest/craft/wave UI, including one pointer per panel, disabled-action suppression and guide dismissal.
Pointers are static and non-interactive; guide actions select existing local structures through the normal world-tap path.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform EditMode -TestFilter "FirstSessionFunnelTests;ServicesTests"
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1 -Platform PlayMode -TestFilter FirstSessionCues
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\capture-screens.ps1 -Devices phone-1080x1920,phone-1080x2340 -Only guide-cue
```

Guide capture fixtures activate the real guide action and require the expected pointer to exist. A failed setup cannot
silently pass with a screenshot of the underlying HUD.

### Visual acceptance

`tools/capture-screens.ps1` is the visual gate for anything that draws. A run is accepted when it reports **0 errors** (an exception
inside a shot's setup is recorded, and counted, per device and shot in `report.json`) and the UI lint is **0 / 0 / 0** on all four
devices (text under 12 dp, touch targets under 48 dp, panels spilling off screen). Compare the static screens with an earlier run
using `tools/image-diff.ps1` (mean RGB difference per screen, under about 0.5 for a screen nothing should have touched). Some shots
are never stable: the three mini-game shots start from randomised layouts and differ by about 10 points between *any* two runs, the
travel game and the wave HUD by a few points, and every world with weather or moons by one or two. The generated-world shots wait
2.5 s so the backdrop has eased into the world's mood; `-Only "gen-,map-generated,menu-debug-teleport"` captures just those 20
(names are stable, numbers 46-65).
Each of them writes the world's `PlanetData.Dump()` to the Unity log (`%TEMP%\frontier-unity-*.log`).

## Where tests live

```
SpheerURP/Assets/Frontier/Tests/
  EditMode/   Frontier.Tests.EditMode (Editor only; references Core, Data, Game; NUnit)
    Core/       engine-free fixtures that only need Frontier.Core: session services, save, travel  -> also run by the plain .NET project
    Universe/   the universe generator and planet flavour tests (namespace Frontier.Universe.Tests)  -> also run by the plain .NET project
    Game/       fixtures that need ScriptableObjects, GameState, AssetDatabase, JsonUtility        -> Unity only
    Fixtures/   real legacy v1 save files (JSON) written by the original game; read by Game/SaveFormatTests and GameStateTests
  PlayMode/   Frontier.Tests.PlayMode (references everything up to Frontier.App)
tests/                              plain .NET projects, outside the Unity project (Unity never imports this folder)
  Frontier.Core/Frontier.Core.csproj          netstandard2.1, C# 9, warnings are errors; LINKS Scripts/Core/**/*.cs
  Frontier.Core.Tests/...csproj               net8.0, NUnit 3; LINKS Tests/EditMode/Core and Tests/EditMode/Universe
  Directory.Build.props, global.json          shared settings, SDK pinned to 8.0.x
```

Where does a new test go?

* Pure logic that only needs `Frontier.Core` (hashing, noise, universe, inventory, world store, save protocol and migrations,
  travel, later score and economy formulas): `Tests/EditMode/Core` (or `Universe`). It must not use `UnityEngine` (so no
  `JsonUtility`: the Core tests use a tiny fake `ISaveCodec`). It is picked up automatically by Unity **and** by `core-tests.yml`:
  the csproj globs the folders, so new files need no registration.
* Needs ScriptableObjects, `GameState`, `AssetDatabase`, models or `JsonUtility`: `Tests/EditMode/Game`.
* Needs a running scene, the UI, rendering or coroutines: `Tests/PlayMode` (`[UnityTest]`). Destroy what you create and restore
  statics (`FrontierRuntime.Clear()`, `GameState.I`).
* Keep tests deterministic: fixed seeds, no wall-clock thresholds tighter than a few hundred milliseconds.
* **A migration test starts from a real file.** Run the old build through a representative scenario, store its save under
  `Tests/EditMode/Fixtures`, and migrate that (see [RUNTIME_MODEL.md](RUNTIME_MODEL.md) section 9).

## The universe generator and its golden hashes

`Frontier.Core/Universe` is a pure function of `(seed, generator version, authored content, coordinates)`. Determinism is
enforced by:

* **Golden digests** (`DeterminismTests`): 2,000 sectors of the plain generator `0x8133861D42016686`, the landmark galaxies plus six
  authored systems `0x4962F4E5D4987768`, and the 31 attribute stream names `0x4787A4CF60E8072C`. They are also checked bit for
  bit for `DetMath` (`0x2F64190E99D0C782`) and the density field (`0x0D889345B9C637EE`).
* **Source hygiene** (`SourceHygieneTests`): no engine references, no `System.Random`, `string.GetHashCode`, `DateTime`, platform
  `Math.Sin/Pow/...` (use `DetMath`), no float arithmetic, no static mutable state, no C# 10+ syntax. It reads the sources from
  `Scripts/Core/Universe` (found from the project folder, the bin folder of the CI project or the working directory) and fails
  if it cannot find them.
* The `Frontier.Core` assembly must reference only `netstandard` (`CoreAssembly_ReferencesOnlyNetStandard`).
* **Device determinism** (`Tests/PlayMode/UniverseDeterminismTests`): the plain-generator golden digest as a PlayMode test, so it
  can run on a player build (Test Runner > PlayMode > Run on a connected device). **Not yet run on an IL2CPP/ARM64 phone**: the C++
  compiler may contract `a * b + c` into a fused multiply-add there, which would change the last bit of double results and, in
  the worst case, the generated universe. Run it on a real device before any generator output is persisted for players.

A failing golden test means generator V1 output changed. That is only acceptable as a **new generator version** (see the header of
`Universe.cs`); V1 stays frozen and its goldens keep passing. Never "fix" a golden constant to make a test green.
Print the current values with `-OutputMatch "digest"`.

## Session, save and planet tests

What proves the foundation (the id based session services, save v2 and the runtime planet; see [RUNTIME_MODEL.md](RUNTIME_MODEL.md)):

* **Inventory parity** (`Core/InventoryParityTests`): a copy of the *original* `GameState` inventory algorithm lives inside the test as
  an oracle and is compared with `Inventory` over more than 10,000 seeded random operations (add, spend, free space, craft fit) with
  varied stack sizes and capacities. `ProgressionTests` does the same for the level curve.
* **Save** (`Core/SaveMigratorTests`, `SaveStoreTests`, `SaveDebouncerTests`; `Game/SaveFormatTests`, `SaveRoundTripTests`,
  `GameStateTests`): v1 -> v2 migration from a real v1 fixture (structures, warehouses with contents, waves, upgrades, xp, a non
  start planet), the v2 round trip through the real `JsonUtility` codec, a corrupt primary recovered from the backup, a leftover temp
  file recovered, a future schema version loaded read-only and never overwritten, unknown ids dropped with warnings, and the
  debounce / flush behaviour driven by a `FakeClock`.
* **Authored worlds** (`Game/AuthoredWorldsTests`): all 30 `PlanetSO` converted, ids unique, the canonical id to key table frozen
  (a golden table), the tier at each system's position against its authored tiers, valid deposits, no generated system overlapping
  an authored one, two builds identical.
* **Generated planets** (`Game/UniverseServiceTests`, `PlanetRuntimeTests`, `PlanetBiomeTests`): 500 random generated planets resolve
  with finite colours, a model key that exists in the `ModelLibrary`, at least 12 slots, a world size in range, gather spots that
  include scrap and a stable visual seed; every biome and size class can be found quickly enough for a debug menu; the authoring
  `PlanetBiome` enum equals the generator's. `Universe/PlanetFlavorTests` covers the generated descriptions (never empty, well
  formed, varied, deterministic, independent hash streams).
* **Travel** (`Core/TravelModelTests`): a same-system hop is cheaper than any inter-system hop, cost never decreases with distance,
  at least 1, Sylva to Glacia is 5.

## The check suites (the gate)

Every change must keep all five passing, and the Unity log must show no `warning CS` from Frontier code:

| Suite | Menu item / method | Asserts |
|-------|--------------------|---------|
| Planet | `Frontier > Validate Planet Model Sizes` / `FrontierPlanetChecks` | source models import at unit size, planet models fit diameter 3, structure diameters and tier capacities, map route endpoints and orbital joins, baked model ground radii match a fresh measurement (fix: `Frontier > Bake Model Ground Radii`) |
| Storage | `Frontier > Validate Warehouse Storage` / `FrontierStorageChecks` | warehouse capacity, transfers, conservation, isolation, upgrades, persistence, full backpack |
| Combat | `Frontier > Validate Combat Balance` / `FrontierCombatChecks` | repeatable previews for fuel 1-40 on every planet, monotonic rewards and difficulty, wave prefixes 1-20, selection shader import |
| Icons | `Frontier > Validate Icons` / `FrontierIconChecks` | Registered SVG icons import as `VectorImage`, every SVG is listed, every resource and structure has its own glyph |
| Layout | `Frontier > Validate Panel Layout` / `FrontierLayoutChecks` | panel bounds, inventory rebuilds, crafting popup, mini-game arena at 900x1600 and 1600x900 |

`tools/run-checks.ps1` runs them through `run-unity.ps1` (the layout suite needs `-NoQuit`, handled for you). The ported
EditMode tests cover the plain assertions of Storage, Combat (formula part) and Planet (model sizes); the editor checks stay
because they also exercise live objects (`DefenseController`, `WorldSurface`, `UIRoot`) and are the batch gate.

To confirm a clean compile, search the log for `error CS|warning CS`. Unity only prints diagnostics for assemblies it recompiles,
so to prove "zero warnings" for everything, delete `SpheerURP/Library/ScriptAssemblies` (and `Library/Bee`) and run any batch
command once.

## The economy tests, the snapshot and the labs

`Tests/EditMode/Economy` (91 tests, namespace `Frontier.Economy.Tests`) cover the economy catalog and everything built on it: catalog integrity
(ids, references, categories, tiers), recipe conservation and reachability from the starting state, cost and tuning monotonicity, the travel
model, resource coverage of the universe with the economy's habitat table (Monte-Carlo), the balance simulator (determinism, era bands for the
three player archetypes, the return-to-starter-planets loop), obstacles, the shard model, the Frontier Score (monotonic, anti-farm, not
purchasable) and source hygiene (no UnityEngine, clocks, platform math or static mutable state in `Scripts/Economy`). They run in Unity's
EditMode runner, in the plain .NET project (so also in `core-tests.yml` on Linux) and in the console lab, all from the same files.

`SnapshotTests` compares the committed `docs/economy/data/manifest.json` (tuning digest and entity counts) with what the code generates, so a
catalog or tuning change cannot land without regenerating the reviewable snapshot (`tools/labs/economy/run.ps1 export docs\economy\data`, then
`report`, `codex`, `charts`).

The labs under `tools/labs` need no Unity window: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 test`
(91 tests, ~25 s) and `...\universe\run.ps1 test` (193 tests, ~4 s). Their `crosscheck.ps1` scripts compare result digests on CoreCLR and on
Unity's Mono, which is how the float-precision difference between the two runtimes was found for the simulator code; keep arithmetic in
`double` and round once at stores when a result must be bit-identical across runtimes.

## Continuous integration

### `core-tests.yml` (no setup)

Runs on every push and pull request on `ubuntu-latest` and `windows-latest`: `actions/setup-dotnet` (8.0.x), then
`dotnet test` from `tests/` (where `global.json` pins the 8.0 SDK even though runners carry newer ones). It compiles
`Frontier.Core` as netstandard2.1 / C# 9 with warnings as errors (what Unity compiles) and runs the 193 Core tests, so the golden
hashes are verified on both operating systems. Results are uploaded as `.trx` artifacts.

### `unity-tests.yml` (needs a Unity licence from the repository owner)

Runs EditMode and PlayMode through [GameCI](https://game.ci) (`game-ci/unity-test-runner@v4`, project `SpheerURP`, Unity
`6000.4.0f1`, Library folder cached). A first `license` job checks whether the licence secret exists; **without it the Unity job
is skipped, not failed**. What the owner has to do once:

1. Create the licence file: for a **Personal** licence follow the GameCI activation guide
   (<https://game.ci/docs/github/activation>) and keep the resulting `.ulf` file. A Pro/Plus licence uses its serial number instead.
2. Add repository secrets (Settings > Secrets and variables > Actions):
   `UNITY_LICENSE` (the whole `.ulf` file content), `UNITY_EMAIL`, `UNITY_PASSWORD`; or, for Pro/Plus, `UNITY_SERIAL` with
   `UNITY_EMAIL` and `UNITY_PASSWORD`.
3. Optional: Settings > Actions > General > Workflow permissions > "Read and write" so GameCI can publish the results as a check
   run. Without it the results are still uploaded as artifacts (`unity-EditMode-results`, `unity-PlayMode-results`).
4. When the Unity version changes, update `ProjectSettings/ProjectVersion.txt` **and** `unityVersion` in the workflow; the version
   needs a matching `unityci/editor` image (6000.4.0f1 images existed on Docker Hub in March 2026).

Not covered in CI: the check suites, the screenshot harness and player builds (they need a licence plus a GPU or platform
modules). Run `tools/run-checks.ps1` before every commit. The PlayMode smoke tests do not need a GPU: they also pass with
`-nographics` (verified locally), which is how GameCI-style containers usually run Unity.

## Troubleshooting

* `run-tests.ps1` exit code 2 and "NO RESULTS": Unity did not produce a result file. Open `SpheerURP/Logs/tests-<platform>.log` and
  search for `error CS`, `Scripts have compiler errors` or a crash.
* "Another Unity batch run held the lock": a previous run is still going. The mutex only serialises runs of this tooling; a
  Unity editor that already has the project open (same path) must be closed first. An editor on a different project path is fine.
* Licensing handshake errors and one `DirectoryNotFoundException` for `PackageCache/com.unity.collections` in the log are harmless.
* `dotnet` not found: install the .NET 8 SDK (<https://dotnet.microsoft.com/download/dotnet/8.0>). Unity's bundled runtime cannot
  build projects.
* A test passes alone but fails after others: a test left a static behind (`GameState.I`, `FrontierRuntime`, a save override).
  Use `SaveSandbox` and clean up in `TearDown`.

**Rewarded harvest transaction check:** `SaveRoundTripTests` verifies that a double-yield bonus is committed before the
extra inventory is published, survives reload, and leaves the base harvest unchanged after a failed write. `MinigameFlowTests`
verifies result-screen offer success and suppression when the caller disallows it. Focused run: 17 EditMode and 14 PlayMode
tests passed; the default-off ad policy and unavailable provider remain unchanged.
