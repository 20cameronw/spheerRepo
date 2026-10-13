# Testing

Everything that can run without Unity does. Five layers, fastest first:

| Layer | Covers | Needs | Run it | CI |
|-------|--------|-------|--------|----|
| **Core tests** (plain .NET) | `Frontier.Core`: the universe generator (99 tests incl. the golden hashes) and the frozen shared enums (4) | .NET 8 SDK only | `cd tests` then `dotnet test Frontier.Core.Tests/Frontier.Core.Tests.csproj` | `core-tests.yml` (Ubuntu + Windows) |
| **EditMode** (Unity) | the Core tests again, in the editor's Mono, plus Data/Game tests: storage, waves, planets and models, content integrity, saves (33) | Unity 6000.4.0f1 | `tools/run-tests.ps1 -Platform EditMode` | `unity-tests.yml` (needs a licence) |
| **PlayMode** (Unity) | smoke: the authored database loads, `FrontierBootstrap` builds camera, planet, backdrop, defense, UI and world surface without errors; the universe golden digest on the PlayMode runtime (also runnable on a device) | Unity (a GPU is not required) | `tools/run-tests.ps1 -Platform PlayMode` | `unity-tests.yml` (needs a licence) |
| **Check suites** (Unity editor) | the five `Frontier > Validate ...` checks: the pass/fail **gate** for every change | Unity | `tools/run-checks.ps1` | local only |
| **Screenshot harness** (Unity, GPU) | every screen on four virtual devices + UI lint | Unity + GPU | `tools/capture-screens.ps1` | local only |

Why the Core tests run twice: Unity's runner proves the code under the runtime the game actually uses (Mono in the editor), the
plain .NET project proves it in seconds on any machine and checks the determinism hashes on other operating systems. The same
source files feed both (see below), so there is nothing to keep in sync.

## Running everything locally (Windows PowerShell)

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

# Screens and visual regression (see ARCHITECTURE.md)
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\capture-screens.ps1 -Devices phone-1080x1920
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\image-diff.ps1 -A <runA>\phone-1080x1920 -B <runB>\phone-1080x1920
```

`run-tests.ps1` goes through `run-unity.ps1`, so it shares the named mutex (only one editor opens the project at a time), the
watchdog and the filtered log. It prints every failing test with its message and the first stack lines, plus a
`passed/failed/skipped` line, and exits `0` (all passed or skipped), `1` (a test failed), `2` (no results: compile error, crash or
timeout) or `3` (no test ran, usually a wrong filter). It never passes `-quit` (the test runner exits by itself) and does not pass
`-nographics`, so tests that render keep working. In the Unity editor the same tests are in **Window > General > Test Runner**.

Never let tests or tools touch the real save: set `GameState.SaveDirectoryOverride` first. The EditMode tests do it with
`SaveSandbox` (`Tests/EditMode/Game/SaveSandbox.cs`), the PlayMode tests in their `SetUp`, the editor checks with
`CheckSupport.IsolateSaves()`.

## Where tests live

```
SpheerURP/Assets/Frontier/Tests/
  EditMode/   Frontier.Tests.EditMode (Editor only; references Core, Data, Game; NUnit)
    Core/       engine-free fixtures that only need Frontier.Core   -> also run by the plain .NET project
    Universe/   the universe generator tests (namespace Frontier.Universe.Tests)  -> also run by the plain .NET project
    Game/       fixtures that need ScriptableObjects, GameState, AssetDatabase    -> Unity only
  PlayMode/   Frontier.Tests.PlayMode (references everything up to Frontier.App)
tests/                              plain .NET projects, outside the Unity project (Unity never imports this folder)
  Frontier.Core/Frontier.Core.csproj          netstandard2.1, C# 9, warnings are errors; LINKS Scripts/Core/**/*.cs
  Frontier.Core.Tests/...csproj               net8.0, NUnit 3; LINKS Tests/EditMode/Core and Tests/EditMode/Universe
  Directory.Build.props, global.json          shared settings, SDK pinned to 8.0.x
```

Where does a new test go?

* Pure logic that only needs `Frontier.Core` (hashing, noise, universe, later score and economy formulas): `Tests/EditMode/Core`
  (or `Universe`). It must not use `UnityEngine`. It is picked up automatically by Unity **and** by `core-tests.yml`: the csproj
  globs the folders, so new files need no registration.
* Needs ScriptableObjects, `GameState`, `AssetDatabase` or models: `Tests/EditMode/Game`.
* Needs a running scene, the UI, rendering or coroutines: `Tests/PlayMode` (`[UnityTest]`). Destroy what you create and restore
  statics (`FrontierRuntime.Clear()`, `GameState.I`).
* Keep tests deterministic: fixed seeds, no wall-clock thresholds tighter than a few hundred milliseconds.

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

## The check suites (the gate)

Every change must keep all five passing, and the Unity log must show no `warning CS` from Frontier code:

| Suite | Menu item / method | Asserts |
|-------|--------------------|---------|
| Planet | `Frontier > Validate Planet Model Sizes` / `FrontierPlanetChecks` | source models import at unit size, planet models fit diameter 3, structure diameters and tier capacities, map route endpoints and orbital joins |
| Storage | `Frontier > Validate Warehouse Storage` / `FrontierStorageChecks` | warehouse capacity, transfers, conservation, isolation, upgrades, persistence, full backpack |
| Combat | `Frontier > Validate Combat Balance` / `FrontierCombatChecks` | repeatable previews for fuel 1-40 on every planet, monotonic rewards and difficulty, wave prefixes 1-20, selection shader import |
| Icons | `Frontier > Validate Icons` / `FrontierIconChecks` | 48 icons import as 48x48 `VectorImage`, every SVG listed, every resource and structure has its own glyph |
| Layout | `Frontier > Validate Panel Layout` / `FrontierLayoutChecks` | panel bounds, inventory rebuilds, crafting popup, mini-game arena at 900x1600 and 1600x900 |

`tools/run-checks.ps1` runs them through `run-unity.ps1` (the layout suite needs `-NoQuit`, handled for you). The ported
EditMode tests cover the plain assertions of Storage, Combat (formula part) and Planet (model sizes); the editor checks stay
because they also exercise live objects (`DefenseController`, `WorldSurface`, `UIRoot`) and are the batch gate.

To confirm a clean compile, search the log for `error CS|warning CS`. Unity only prints diagnostics for assemblies it recompiles,
so to prove "zero warnings" for everything, delete `SpheerURP/Library/ScriptAssemblies` (and `Library/Bee`) and run any batch
command once.

## Continuous integration

### `core-tests.yml` (no setup)

Runs on every push and pull request on `ubuntu-latest` and `windows-latest`: `actions/setup-dotnet` (8.0.x), then
`dotnet test` from `tests/` (where `global.json` pins the 8.0 SDK even though runners carry newer ones). It compiles
`Frontier.Core` as netstandard2.1 / C# 9 with warnings as errors (what Unity compiles) and runs the 103 Core tests, so the golden
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
