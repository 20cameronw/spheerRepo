# Economy design snapshot

Everything here is **generated** from `Scripts/Economy` by the economy lab (`tools/labs/economy/run.ps1`, see `tools/labs/README.md`). Do not edit by hand; change the catalog or the tuning, then regenerate. `SnapshotTests` fails when the committed snapshot and the code disagree.

| File | What it is | Regenerate with |
|------|------------|-----------------|
| `ECONOMY.md` | The full human summary: resource table with how to obtain / used in, structure costs and times at levels 1/5/10, tool ladders, era targets with simulated times, the shard and score models, risks | `run.ps1 report docs\economy` |
| `ATLAS_TEXT.md` | Every resource and recipe entry as the in-game Atlas will show it (review copy; the screen builds the text at runtime from the catalog) | `run.ps1 codex docs\economy` |
| `ICON_BRIEF.md` | One-sentence silhouette brief per resource for icon art, grouped by category with colour | `run.ps1 codex docs\economy` |
| `TUNING_KEYS.md` | The 606 tuning keys with defaults and ranges (the Remote Config surface) | `run.ps1 codex docs\economy` |
| `chart_*.png` | Resources over time, tier over time, time per era, score over time for the three player archetypes | `run.ps1 charts docs\economy` |
| `data/*.json` | Versioned exports (resources, habitats, recipes, structures, tools, upgrades, research, obstacles, shards, score formula, travel, era targets, legacy ids, start, tuning, remote config) plus `manifest.json` with the tuning digest and counts | `run.ps1 export docs\economy\data` |

## Owner decisions recorded with this snapshot

* **Ascension** ships with the endgame package (not later): a hardcore player finishes the headline ladder about day 360, so the endless layer must exist at launch.
* **Batch crafting** of up to 150 per tap stays (crafting is instant; the UI offers 1 / 10 / Max).
* **Shard packs and cosmetic prices** are placeholders until the store tiers are configured in the stores.
* **Megaprojects** use no builder, so builders never block endgame work.
* **Score weights** stay as tuned until real leaderboard data exists; Remote Config can adjust them.
