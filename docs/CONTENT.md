# Economy content pipeline

## Licensed audio cues

The editable `Resources/Frontier/AudioCueLibrary.asset` maps click/confirm/error, weapon shot, explosion,
warp, wood/stone/metal impacts and ship ignition/touchdown to selected CC0 clips. Eleven cue mappings share
ten short recordings/derivatives within a 1 MiB decoded sample ceiling. UI cues route through the UI mixer group; combat
and warp cues use SFX. The existing bounded voice pool, independent volume preferences and mute behavior
remain in force. Hyperspace borrows shot/impact/explosion assets without destroying them when a run closes;
its generated pickup/warning/chime cues remain separate. Chopping, mining and drill-contact sounds
use distinct wood, mining and metal assets with authored gain; grading/haptic timing remains unchanged.

Sources: Kenney [Interface Sounds 1.0](https://kenney.nl/assets/interface-sounds),
[Sci-Fi Sounds 1.0](https://kenney.nl/assets/sci-fi-sounds) and
[Impact Sounds 1.0](https://kenney.nl/assets/impact-sounds). All three archives explicitly permit commercial use
under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/); their original `License.txt` files are
retained beside each pack in `Resources/Frontier/Audio`. Only selected clips are included,
not whole archives. They import as preloaded mono PCM/decompress-on-load for predictable short-cue playback.
**Frontier > Audio > Import Licensed Cue Library** creates a missing library or appends newly supported
material/ship cues, preserving existing designer mappings/import settings. Ship ignition is an editor-baked,
normalized 550ms envelope of the licensed engine recording; touchdown reuses the metal contact asset at its
own authored gain. Neither ship cue uses runtime-generated tones.

## Adaptive soundscape

`Resources/Frontier/SoundscapeLibrary.asset` maps exploration/combat/travel arrangements and four weather
textures (calm wind, rain/storm, snow and embers). `FrontierSoundscape` follows actual defense and complete
manual/autopilot travel state; it does not change progression. Arrangement changes fade through silence on the
existing Music source, while weather uses the existing SFX bed. Hyperspace exclusively owns that bed during
travel. Ownership-aware cleanup prevents late engine disposal from stopping weather. Music resumes after
unmute; application pause stops the director's loops and resume fades music back in. Effects and Music sliders
remain independent, with twelve total sources and no runtime music/ambience synthesis.

The seven original loops are deterministic editor-baked PCM files under `Audio/Original`; their provenance is
recorded there. **Frontier > Audio > Create Original Soundscapes If Missing** bootstraps missing content but
never overwrites an existing library. Three aligned 16-second arrangements provide an initial soundtrack,
not an approved replacement for the master plan's commercially licensed music-pack/stem selection.
The travel engine borrows a shared, preloaded loop derived from Kenney Sci-Fi `spaceEngineSmall_000.ogg`;
its original source, 100ms seam-crossfaded derivative and CC0 license stay together in `Audio/KenneySciFi`.
The original source is retained for reproducibility. All eight loop assets together have an 8 MiB decoded
sample-data ceiling. Physical-device listening/mixing, full biome-specific art direction, menu/stem music-pack
selection and final audio sign-off remain open.

## This milestone

### Offline clock history

V2 saves now additionally retain `clock.highWaterUtc`, the greatest observed gameplay UTC. Older saves
derive it from `lastSavedUtc`. Auto-Harvester and Dyson offline production measure only intervals after
that watermark; a backward device-clock edit, saving and restarting cannot make the earlier interval
eligible again. Load reports warn while the clock is behind; new production resumes only past the recorded
time. Explicit restore/undo retain the greater local/incoming watermark without mutating the supplied
snapshot. An unreadable watermark loads read-only and rejects explicit restore rather than discarding history.
This does not establish trusted time: forward date changes, edited/deleted files, older automatic recovery
copies and other devices still require server authority before paid/live-service activation.

The reviewed economy is now importable as editable Unity ScriptableObjects. No economy values were redesigned.
`docs/economy/data` remains the single committed JSON source (regenerate it with the economy lab, never hand-edit it).
The engine-free JSON reader is also compiled and tested by the lab and the plain .NET CI project.

**The imported database is now the compatibility-guarded runtime default.** `Resources/Frontier/EconomyDatabase.asset` references
34 resources, 24 recipes, 31 structures, 6 tool ladders, 16 upgrades, 60 research nodes, 8 obstacle kinds, and the
habitat, obstacle, shard, score, legacy-id and tuning assets. It includes the exact level tables, costs, timers,
requirements, 606 tuning values, category colours and starting inventory from the export.

Every catalog resource has a distinct exact-id SVG, and every structure kind has its own SVG. The 47 new glyphs
extend the existing 48-unit white line-art family and inherit the resource/UI tint at use sites. Authored resource
icon overrides take precedence over canonical ids, which take precedence over keyword fallbacks. The icon check
verifies VectorImage import, 48×48 bounds and full distinct coverage; four paginated economy icon harness shots
show every glyph at 24/32/48/64 physical pixels.

`GameDatabase.Load()` validates the supported v1 catalog, tuning, required families, starting capacity, typed adapters,
legacy aliases, and the complete authored-world/combat links before selecting it. A missing, old, incomplete or
incompatible import falls back to `Frontier/GameDatabase`, then `DefaultContent` when that asset is absent.
The shipped scene leaves its database override empty; explicit legacy/custom overrides still work. An explicit
imported override goes through the same guard.

Selection creates a cached, transient runtime view (`ContentSource` points to the imported asset). It keeps the imported
resources, recipes, structures, upgrade rows and rules, but copies authored planets/enemies and the three legacy tool
upgrades to rebind their item references. No authored asset, GUID, legacy database or imported catalog is rewritten.
Legacy fuel deposits are omitted from the runtime view because fuel is now crafted-only; source deposits remain intact.
Procedural deposits use the imported raw-resource/habitat tables. Planet ids, authored placement, models and visual seeds
stay stable; generated deposit contents now follow economy content v1 rather than the former seven-resource table.

Both v1 migration and existing v2 saves resolve missing resource/structure/upgrade ids through `LegacyIdMapSO`.
Existing stable ids take precedence, so reloads never migrate them twice. `alloy` becomes `steel`, `satellite` becomes
`orbital_relay`; warehouse slots stay in place and colliding inventory aliases sum, while upgrade aliases take the maximum
owned level. Legacy `axe`/`pick`/`drill` purchases and levels remain legacy upgrades; catalog tiers for supported Axe/Pick/Drill ladders
are persisted alongside them in v2. The save schema stays v2, v1 originals are backed up, and newer read-only saves are never overwritten.

Imported recipes are filtered by exact station id, station level, reach and research, including the authoritative craft
action. Purchased research now passes authored research gates; node prerequisites, current-world lab level, reach tier,
catalog identity and resource costs are checked by the runtime. The Command panel exposes research and tool ladders.
Only Axe/Pick/Drill tiers can be purchased; their yield and active tool level are reflected in harvest rewards and tool feedback.
Other ladder game types remain visibly unavailable until their minigames exist.
Unsupported structures/effects cannot be purchased. Supported legacy actions remain synchronous: batch size is not treated
as free output, and upgrade rows use their cumulative values and correct units. This is a bounded progression slice,
**not** the complete economy progression loop.

## Import and validation

Editor menus:

* **Frontier > Generate Editable Data Assets** is a create-only legacy bootstrap: it stops when a database or data asset already exists.
  Use the reviewed economy importer for edits to the current economy assets.
* **Frontier > Economy > Import JSON** — reads the exports, validates their schemas, family/version envelopes,
  tuning digest and catalog references, then creates or updates assets by canonical id.
* **Frontier > Economy > Validate Imported Content** — reconstructs the catalog from the saved assets, runs
  `CatalogValidator`, checks drift against the export and validates tuning completeness/digest. Catalog validation rejects
  recipe dependency cycles, unreachable resources and non-bonus recipes outside the tuned value-conservation band.
  The editor icon check verifies unique resource glyph coverage; structures without a model key use the procedural kit fallback.

Batch import:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-unity.ps1 -Method Frontier.EditorTools.EconomyImporter.Import
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Platform EditMode -TestFilter Frontier.Tests.ContentImportTests
```

Assets live at `Assets/Frontier/Data/Economy/<Family>/<id>.asset`. Reimport updates existing objects in place,
preserving GUIDs and references. Removed ids are reported and retained, never deleted. Unchanged imports do not dirty
assets; the tests import twice and compare every generated asset and meta file byte-for-byte, including the legacy
database's bytes. Paths are identity: don't rename generated asset files.

### Ownership and overrides

* Imported: ids, costs, level rows, requirements, categories, catalog Atlas text, icon concepts, tuning and rule data.
* Authored: resource `color`, `description`, `icon`; structure `color`, `modelKey`; upgrade `color`, `icon`.
  New resources start with the exported colour. Non-empty top-level `displayName` and structure/upgrade `description`
  are also preserved for authoring/localisation and excluded from numeric drift checks.
* `ContentSO.lockedFields` pins exact **top-level** field names (e.g. `stackSize`, `levels`).
  Rules and new tool/research/obstacle assets wrap serializable economy data: pin `data` or `rows` as appropriate.
  Locks are skipped by drift checks, **not** by structural/catalog validity checks. Never lock `id` or rename ids.
  Nested-field locks are not supported. Tuning values must remain complete, in range, and match their recorded digest.
* Import logs each created/updated path, retired paths, content version, tuning digest and total changed count.
  A clean reimport reports `changed=0`.

Each original SO keeps its legacy fields and fallback methods. New `ToDef`/`ToCatalog` adapters normalize Unity's
empty-string representation of optional ids back to null, without mutating saved assets. Structure kinds and
upgrade effects are appended, never reordered, preserving old serialized enum integers. `StructureKindMap` is
the explicit canonical-id-to-behaviour map; imported kinds without gameplay are **not** claimed implemented.

## Remaining integration

1. Add builder jobs and timer execution; research completion, supported tool tiers and research gates are now persistent.
2. Wire timers/batch jobs and the remaining structure behaviours, tank/compression effects, full travel/hull progression,
   obstacles, megaprojects, shards and score. The current interim travel settings are unchanged.
3. Add full presentation coverage for Atlas/research/tools. Merely importing a structure is not gameplay.
4. Add safe live tuning/Remote Config overrides later; current travel reads the validated imported tuning and catalog.

The ordered design and runtime API guide remains `tools/labs/economy/INTEGRATION.template.md`; the balanced source and
review exports are documented in `docs/economy/README.md`. This pipeline deliberately does not rebalance them.

## Milestone verification (2026-10-13)

* Economy lab: full 111-test suite passed, including simulator bands, reachability, snapshots and source hygiene.
  The subsequent reader-only run passed all 21 cases, including four additional mixed/incomplete-export cases.
* Unity EditMode including Slow: 428/429 initially passed; the sole failure was the old assertion that every enum
  kind must exist in the live legacy database. Updated it to require implemented kinds live and imported-only
  kinds absent. The affected database/import fixtures then passed 12/12 (including two byte-identical imports).
  The five new import tests and all 21 JSON reader cases were exercised in the full run.
* Unity PlayMode: 8/8 passed.
* Editor checks: Planet, Storage, Combat, Icon and Layout all passed, with zero C# diagnostics.
* Harness: 24 captures across phone-1080x1920 and tablet-1536x2048 (HUD, build tabs, combat HUD and all four
  economy icon pages); zero harness errors, text-under-12dp, touch-under-48dp or overflow on either device.
  Contact sheets and full-resolution icon pages were visually reviewed. These are targeted captures, not a
  full four-device screen sweep.

Local, ignored artifacts: `SpheerURP/TestResults`, `SpheerURP/Logs`, and
`SpheerURP/Screens/economy-content` (including contact sheets). Batch Unity's default logs now also stay inside
the worktree's `SpheerURP/Logs`, rather than the system temporary directory.

The harness also added six pair-adjustment records to `Barlow-Medium-SDF.asset` while rendering the new labels.
Those additive font-feature changes are retained; no existing font data or interrupted work was discarded.

## Runtime cutover verification (2026-10-13)

Final full EditMode including Slow: **535 passed, 2 skipped** (Mono allocation API unavailable); PlayMode: **19 passed**;
all five editor check suites passed with **zero compile diagnostics**; economy lab: **127 passed**. Coverage includes
guarded selection/fallback, every selected catalog item reference, real v1 migration, mixed-id v2 roundtrips, legacy tools,
warehouse slots, cumulative effects, station/reach/research gates, crafted-only fuel and canonical Hyperspace loot.
No economy export/balance values were changed, and no new screenshot/device sweep was claimed. The existing generated
font SDF changes were excluded from the cutover commits.

## Persistent progression verification

Full EditMode including Slow: **539 passed, 2 skipped** (Mono allocation API unavailable); PlayMode: **19 passed**; all five
editor checks passed with zero compile diagnostics. Coverage includes research and supported-tool purchases, their effects and
save/load, reset behavior, invalid catalog entries, and round-tripping progression through a legacy-content fallback. Only
Axe/Pick/Drill ladders are enabled; builder timers, Atlas discovery/rewards and unsupported harvest minigames remain unimplemented.
