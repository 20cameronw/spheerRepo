# Economy design snapshot

Everything here is **generated** from `Scripts/Economy` by the economy lab (`tools/labs/economy/run.ps1`, see `tools/labs/README.md`). Do not edit by hand; change the catalog or the tuning, then regenerate. `SnapshotTests` fails when the committed snapshot and the code disagree.

| File | What it is | Regenerate with |
|------|------------|-----------------|
| `ECONOMY.md` | The full human summary: resource table with how to obtain / used in, structure costs and times at levels 1/5/10, tool ladders, era targets with simulated times, the shard and score models, risks | `run.ps1 report docs\economy` |
| `ATLAS_TEXT.md` | Every resource and recipe entry as the in-game Atlas will show it (review copy; the screen builds the text at runtime from the catalog) | `run.ps1 codex docs\economy` |
| `ICON_BRIEF.md` | One-sentence silhouette brief per resource for icon art, grouped by category with colour | `run.ps1 codex docs\economy` |
| `TUNING_KEYS.md` | The 607 tuning keys with defaults and ranges (the Remote Config surface) | `run.ps1 codex docs\economy` |
| `chart_*.png` | Resources over time, tier over time, time per era, score over time for the three player archetypes | `run.ps1 charts docs\economy` |
| `data/*.json` | Versioned exports (resources, habitats, recipes, structures, tools, upgrades, research, obstacles, shards, score formula, travel, era targets, legacy ids, start, tuning, remote config) plus `manifest.json` with the tuning digest and counts | `run.ps1 export docs\economy\data` |

## Owner decisions recorded with this snapshot

* **Ascension** ships with the endgame package (not later): a hardcore player finishes the headline ladder about day 360, so the endless layer must exist at launch.
* **Batch crafting** of up to 150 per tap stays (crafting is instant; the UI offers 1 / 10 / Max).
* **Shard packs and cosmetic prices** are placeholders until the store tiers are configured in the stores.
* **Megaprojects** use no builder, so builders never block endgame work.
* **Score weights** stay as tuned until real leaderboard data exists; Remote Config can adjust them.

## Runtime storage bootstrap

Before an operational Cargo Depot exists, purchases must fit entirely in carried cargo; disconnected Warehouse stock is never spent. Backpack level 3 has a targeted bootstrap cost multiplier so it fits the level-2 backpack, and Cargo Logistics has a reduced authored value budget so its research fits the level-3 backpack. Subsequent costs and backpack capacities are unchanged. The operational Cargo Depot then enables the existing local storage pool; Fuel Depots and storage on other planets remain excluded.

| Bootstrap step | Cost slots | Available carried slots |
|---|---:|---:|
| Backpack 1 | 4 | 9 |
| Backpack 2 | 8 | 18 |
| Smelting research / Foundry 1 | 8 / 7 | 27 |
| Backpack 3 | 26 (previously 32) | 27 |
| Research Lab 2 / Steelmaking research | 7 / 28 | 36 |
| Cargo Logistics research | 36 (previously 38) | 36 |
| First Cargo Depot | 25 | 36 |

Backpack 3 now costs 190 wood, 190 stone, 90 planks and 30 iron plate (`cost.bootstrap.pack3_mult = 0.8`). Cargo Logistics now costs 85 iron plate, 350 planks, 50 glass, 100 copper wire and 260 scrap (`cost.v.cargo_logistics = 650`, previously 700). Capacity tests use the imported assets and include the actual research prerequisites, construction jobs and save/reload; they supply ingredients directly and do not estimate harvesting/travel time. At the exact-capacity research purchase, spare cargo can be stowed manually before paying.

The generated simulator timelines are catalog estimates, not validation of the current runtime's player-XP gates or shared mining-tool progression. Live session telemetry and playtesting are still needed before treating those day counts as release pacing targets.
