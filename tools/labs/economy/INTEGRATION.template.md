# Integration guide: moving the economy from `economy-dev` into the Unity project

Generated from `INTEGRATION.template.md` by `run.ps1 integration` (the field tables and the Remote Config table are produced from the code, so they cannot drift). Contents of this release: {{COUNTS}}.

Nothing in this sandbox touches the repo or Unity. Everything below is what the Unity-side agent does later.

## 1. Decision: one runtime model, ScriptableObjects as authoring data

The economy is plain C# (`namespace Frontier.Economy`, C# 9, netstandard 2.1, no UnityEngine, no static mutable state). Two ways to consume it, in order of preference:

1. **Recommended: rebuild the catalog at runtime from a `Tuning`.** At startup: `var tuning = Tuning.CreateDefault(); apply Remote Config overrides with tuning.Set(key, value); var catalog = CatalogBuilder.Build(tuning);`. Every cost, time and effect is re-derived, so a Remote Config change to `cost.era.3` re-prices the whole Industry era. The ScriptableObjects then carry only what the code cannot know: sprites, models, colours, localisation keys. This is what the simulator, the tests and the exporter do, so what you ship is what was balanced.
2. **Fallback: bake once.** The importer fills ScriptableObjects from `data\*.json` and the game reads numbers from them; only the global multipliers (`cost.scale.*`, `cost.era.*`, `time.scale`) can then be changed remotely, applied as runtime multipliers on the baked values.

The SO types below mirror the data classes field for field (same names), so both paths use the same data.

## 2. Assemblies and files

| What | Where in the project | Notes |
|---|---|---|
| `economy-dev\src\Economy\*.cs` (31 files) | `Assets/Frontier/Scripts/Economy/` with `Frontier.Economy.asmdef` | `noEngineReferences: true`, depends on `Frontier.Core` |
| universe generator sources (unmodified) | `Frontier.Core.asmdef` (already planned by the universe work) | `Frontier.Economy` references it (`Frontier.Universe` types are used by the catalog, travel and obstacle code) |
| `economy-dev\src\Tests\*.cs` | `Assets/Frontier/Tests/EditMode/Economy/` with an Edit Mode test asmdef | NUnit attributes are already used. `SimulatorTests`, `ScoreTests` and `ShardTests` run the simulator (about 30 s together): mark them `[Category("Slow")]`. `SourceHygieneTests` reads the source files next to the build output: keep it in the sandbox or point it at the asmdef folder |
| `economy-dev\data\*.json` | `Assets/Frontier/Data/Economy/json/` | the importer's input; commit them |
| `economy-dev\reports\ATLAS_TEXT.md`, `ICON_BRIEF.md` | docs | Atlas screen text is generated at runtime from the catalog; the markdown is the review copy |

## 3. ScriptableObject types and fields

Existing assets are **extended, never replaced** (their current fields stay so old assets and the legacy default content keep working; the importer writes the new fields). New assets live next to them. The field tables are reflected from the data classes:

{{SCHEMA}}

`GameDatabase` gets the new arrays `ToolLadderSO[] toolLadders`, `ResearchNodeSO[] research`, `ObstacleKindSO[] obstacleKinds` and the single references `HabitatTableSO habitats`, `ObstacleRulesSO obstacleRules`, `ShardRulesSO shardRules`, `ScoreFormulaSO scoreFormula`, `LegacyIdMapSO legacyIds`, `EconomyTuningSO tuning`, plus `int builders` next to `baseBackpackSlots`. Display-only fields that the code does not read (`modelKey`, `color`, icon sprites, `description` for localisation) are authored by hand and are never overwritten by the importer.

Conventions: ids are `lower_snake`; enums are exported as strings (use `Newtonsoft.Json` with `StringEnumConverter`; `JsonUtility` cannot read enum names); every file is `{ schemaVersion, contentVersion, generator, family, items }`; fractions are `float`, but all arithmetic must be done in `double` (see Gotchas).

## 4. The editor importer (`Assets/Frontier/Editor/EconomyImporter.cs`)

Menu `Frontier/Economy/Import JSON`. It must:

1. Read `manifest.json`; refuse to run when `schemaVersion` is newer than the importer knows; show `contentVersion` and `tuningDigest`.
2. For each family file, create or update one asset per id at `Assets/Frontier/Data/Economy/<Family>/<id>.asset` (the path is the identity, so references survive re-imports); never delete assets, only report ids that disappeared.
3. Write only the data fields listed above; keep hand-authored presentation fields.
4. Rebuild `GameDatabase` arrays in catalog order, set `startingItems`, `baseBackpackSlots`, `builders`.
5. Run the validator in the Editor: every `ItemAmount.resourceId` resolves, every research prerequisite and unlock resolves, every recipe station exists, ids are unique and lower_snake (the same rules as `CatalogValidator.Validate`, which can simply be called on a catalog rebuilt from the same tuning, then compared to the SOs).
6. Be idempotent: importing unchanged JSON changes no asset (compare serialized values before writing) and prints a diff when something changes.
7. Optionally verify the round trip: rebuild `CatalogBuilder.Build(Tuning.CreateDefault())` and assert that the SO values equal the catalog.

## 5. Runtime wiring

| Concern | API to call |
|---|---|
| Build the economy | `CatalogBuilder.Build(tuning)` -> `EconomyCatalog` (prices: `catalog.Prices`) |
| Universe resource availability | `UniverseTables.BuildResourceTable(catalog)` into `UniverseConfig.Resources`; `UniverseTables.CreateConfig(catalog, seed)` builds the whole config; `StarterSystem.Create(catalog)` is the authored Sylva system |
| Travel | `new TravelModel(tuning, catalog)`: `PlanInterSystem`, `LegFuelCost`, `Refund`, `CanLand`; `new WorldRules(catalog, universeConfig)`: tier, danger and yield of a distance |
| Waves | `WaveEconomy.Reward(...)` |
| Obstacles | `ObstacleRules.Generate(catalog, universeSeed, planetId, pads, size, biome, hazards, tier, isHome)`; discount with `ObstacleRules.Discounted(amount, hubDiscountPercent)` |
| Timers and shards | `catalog.structures[i].levels[l].buildSeconds`; `ShardCurve.SkipCost(catalog.shards, secondsLeft)`; `BuilderPlan.Count/NextBuilderPrice`; `ShardLedger.Earn` on the server |
| Score | `ScoreCalculator.Compute(inputs, ScoreFormula.From(tuning))` on the client for display; the same call in Cloud Code for validation |
| Legacy saves | `new LegacyIdMap(catalog).MigrateInventory(...)`, `Resolve(LegacyKind, id)`: `alloy` -> `steel`, `satellite` -> `orbital_relay` |
| Balance regression | `SimEngine.Run(...)` with `SimFx`-style fixtures; `SimBands.Check` fails when a milestone leaves its band |

Persisted state stays small: the cleared-obstacle set per planet, builder jobs (start and end server time), megaproject installments, tool tiers, upgrade levels, research set, discovered resources and crafted recipes. Everything else is recomputed.

## 6. Remote Config

Keys are classified by `IntegrationDoc.Classify`; the full list with ranges is `data\remote_config.json` and `reports\TUNING_KEYS.md`.

{{REMOTE}}

Rules: apply overrides to the `Tuning` **before** calling `CatalogBuilder.Build`; clamp every override to the key's `min`/`max` (`Tuning.Set` does); keep the previous catalog when the build throws; never expose `Content` or `Dev` keys. Score weights are `Server` keys: the client shows the score from the shipped defaults, Cloud Code recomputes with the live values. Recommended first A/B levers: `cost.era.1..4` (pacing of the first two weeks), `shard.skip.multiplier`, `shard.earn.*`, `obstacle.density`, `travel.k`, `tool.mult.*`.

## 7. Ordered integration steps

1. Create `Frontier.Economy.asmdef` and `Frontier.Core.asmdef`; copy the sources; open Unity and confirm both compile with zero warnings (the sandbox builds with warnings as errors).
2. Copy the tests into an Edit Mode assembly; run them: expect **all green** (counts in `PROGRESS.md`). Compare `Tuning.CreateDefault().Digest()` with `manifest.json -> tuningDigest`, and run `MonoCheck` (`crosscheck.ps1` shows how) once inside the editor's Mono: the catalog, obstacle, shard, score and simulator digests must equal the CoreCLR ones.
3. Create the new SO types and extend the existing ones (section 3); add the new `GameDatabase` fields.
4. Write `EconomyImporter` (section 4); import `data\*.json`; commit the generated assets; run the importer twice and confirm the second run changes nothing.
5. Add `EconomyService` (builds `Tuning` -> `CatalogBuilder.Build`, applies Remote Config, exposes the catalog); replace `DefaultContent` data lookups with catalog lookups, keeping the legacy ids working through `LegacyIdMap`.
6. Save migration `v1 -> v2`: map legacy ids, give every migrated player the builders they would have earned, convert upgrade levels of `axe`/`pick`/`drill` into tool tiers (`tier = level + 1`).
7. Universe: build `UniverseConfig` from the catalog (`UniverseTables.CreateConfig`), keep the authored Sylva starter system; run the coverage tests in the project.
8. Gameplay systems in this order: backpack and Warehouse capacity and stacks; recipes and batch crafting (instant); structures with levels, costs, builder timers; tools and upgrades; research and the Lab gate; travel fuel, jump range and hull plating gates; waves; obstacles and clearing; megaproject installments; Atlas screen; shards, shop and ads; score and Rank screen; Ascension.
9. Hook analytics to the milestone names in `data\era_targets.json` (`first_warehouse`, `first_fuel`, `first_hop`, `era_*`) so live pacing can be compared with the bands.
10. Before each live balance change: change keys in a copy of `Tuning`, run `SimBands.Check` for the three archetypes (and `ScoreTests`), then push the override.

## 8. Gotchas found while building this

- **Do arithmetic in `double`.** `int * float` is evaluated in single precision on CoreCLR but may be promoted on Mono; the cross-runtime check caught exactly this in the obstacle code (a 0.5 rounding case). Store `float` in SOs, cast to `double` before any multiplication.
- **No static mutable state, no clocks, no `string.GetHashCode`, no `Math.Pow/Exp/Log` from the platform**: use `DetMath` and the universe `Hash` (`SourceHygieneTests` enforces it).
- **The generator version matters.** Resource availability and the 576 ly coverage proof belong to universe generator version 1; changing the generator means rerunning `UniverseCoverageTests`.
- **Fuel is crafted only.** No deposit yields `fuel`; an importer or UI that still expects a drilled fuel resource will show an empty source.
- **Megaprojects use installments and no builder**; their stage timers are the only multi-day timers (cap 10 days).
- **Warehouse capacity is a table** (18, 27, 36, 45, 54, 66, 84, 108, 138, 174), not `slots + perLevel x level`: read `StructureLevelDef.capacity`.
- **`ResourceDef` exists twice**: `Frontier.Economy.ResourceDef` (the catalog) and `Frontier.Universe.ResourceDef` (the generator's own). Files that import both namespaces need `using ResourceDef = Frontier.Economy.ResourceDef;` (or fully qualified names).
- **Era names are gates, not tiers** (Interstellar spans tiers 5 to 7): analytics must use the milestone names.

## 9. Files and commands in the sandbox

```
build:   powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
tests:   powershell -NoProfile -ExecutionPolicy Bypass -File run.ps1 test [--filter Name]
sim:     run.ps1 sim [casual|regular|hardcore|all] [days=730] [seed=1] [stats=1] [goals=1] [key=value ...]
export:  run.ps1 export        (data\*.json)
reports: run.ps1 report | codex | charts reports | integration
x-check: powershell -NoProfile -ExecutionPolicy Bypass -File crosscheck.ps1   (CoreCLR vs Unity Mono)
```
