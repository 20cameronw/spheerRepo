# Economy content pipeline

## This milestone

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
owned level. Legacy `axe`/`pick`/`drill` purchases and levels remain legacy upgrades until tool-tier persistence exists.
The save schema stays v2, v1 originals are backed up, and newer read-only saves are never overwritten.

Imported recipes are filtered by exact station id, station level, reach and research, including the authoritative craft
action. Unsupported structures/effects cannot be purchased. Research requirements currently stay locked (Atlas entries
are not research grants); existing saved ownership survives without retroactive gating. Supported legacy actions remain
synchronous: batch size is not treated as free output, and upgrade rows use their cumulative values and correct units.
This is a safe content cutover, **not** the complete economy progression loop.

## Import and validation

Editor menus:

* **Frontier > Economy > Import JSON** — reads the exports, validates their schemas, family/version envelopes,
  tuning digest and catalog references, then creates or updates assets by canonical id.
* **Frontier > Economy > Validate Imported Content** — reconstructs the catalog from the saved assets, runs
  `CatalogValidator`, checks drift against the export and validates tuning completeness/digest.

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

1. Add persistent research completion, tool tiers and builder jobs; replace the temporary fail-closed research gates.
2. Wire timers/batch jobs and the remaining structure behaviours, tank/compression effects, full travel/hull progression,
   obstacles, megaprojects, shards and score. The current interim travel settings are unchanged.
3. Expose Atlas/research/tool screens and full presentation coverage. Merely importing a structure is not gameplay.
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
