# Economy content pipeline

## This milestone

The reviewed economy is now importable as editable Unity ScriptableObjects. No economy values were redesigned.
`docs/economy/data` remains the single committed JSON source (regenerate it with the economy lab, never hand-edit it).
The engine-free JSON reader is also compiled and tested by the lab and the plain .NET CI project.

**The imported database is staged, not the live game's database.** `Resources/Frontier/EconomyDatabase.asset` references
34 resources, 24 recipes, 31 structures, 6 tool ladders, 16 upgrades, 60 research nodes, 8 obstacle kinds, and the
habitat, obstacle, shard, score, legacy-id and tuning assets. It includes the exact level tables, costs, timers,
requirements, 606 tuning values, category colours and starting inventory from the export.

Every catalog resource has a distinct exact-id SVG, and every structure kind has its own SVG. The 47 new glyphs
extend the existing 48-unit white line-art family and inherit the resource/UI tint at use sites. Authored resource
icon overrides take precedence over canonical ids, which take precedence over keyword fallbacks. The icon check
verifies VectorImage import, 48×48 bounds and full distinct coverage; four paginated economy icon harness shots
show every glyph at 24/32/48/64 physical pixels.

`GameDatabase.Load()` and the scene still use `Frontier/GameDatabase`. Its assets, GUIDs, save semantics and current
gameplay stay unchanged. Staging avoids exposing research-gated recipes or new costs before research, tool tiers,
builders and economy save migration exist. Core Worlds and combat assets in the staged database are references to
the original authored assets; **their legacy deposit resource references are not migrated yet**. Do not point the
scene at the staged database as a substitute for the remaining gameplay integration.

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
assets; the tests import twice and compare every generated asset and meta file byte-for-byte, including the live
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

1. Build an economy runtime service from `Tuning`/`CatalogBuilder`, including safe Remote Config overrides.
2. Migrate saved legacy resource/structure ids and tool upgrades; add persistent research, tool tiers and builders.
3. Migrate authored deposit references and wire the economy resource table into universe generation.
4. Wire inventory/warehouse tables, station/research gates and batch crafting, structure builders/timers, tools,
   upgrade effects, research, travel/hull gates, waves, obstacle clearing, megaprojects, shards and score.
5. Expose Atlas/research/tool screens and full presentation coverage. Merely importing a structure is not gameplay.

The ordered design and runtime API guide remains `tools/labs/economy/INTEGRATION.template.md`; the balanced source and
review exports are documented in `docs/economy/README.md`. This pipeline deliberately does not rebalance them.
