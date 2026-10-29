# Current systems reference

Behaviour notes for the systems that exist today. The long-term direction lives in [ROADMAP.md](ROADMAP.md).

## Player navigation and purchase resources

The dock is **Research / Map / Ship / Build**, in the existing positions. Ship always opens Cargo in one tap,
with the capacity gauge and used/total count on its dock button. Cargo contains carried items and its capacity
upgrade; surface salvage is explicitly unclaimed and not aboard. Tools contains harvest equipment. Systems contains
ship fuel reserves and tank upgrades, engine efficiency, jump/scanner range and hull protection with their upgrades.
Research contains Tech (available work first, completed nodes in a foldout) and Improvements (global defense and logistics).
Map exposes the planetary network from every selected world, plus ship-readiness access. Build retains local structures
and Construction Jobs. Tapping world structures still manages them; the lab links to Research and Ship tools.

Research, tool tiers and ship/defense/global upgrades use the same resource pool as construction: carried cargo plus
operational storage on the current planet connected by an operational Cargo Depot. Cost chips, eligibility and spending
share this rule. Cargo is spent first, then connected storage; insufficient totals spend nothing. Disconnected,
under-construction and remote storage, unclaimed salvage and Fuel Depot reserves are excluded. Remote resources still
require transfer or retrieval. XP, research prerequisites, local labs and other gates are unchanged.
No save schema, stable content IDs, inventory capacity, job timing or production-ad settings change with this navigation release.

## Shortage journeys, projects and returning sessions

Missing-resource chips on build, research, tools, upgrades, station recipes, obstacle clearance,
planet links and the Grand Archive open
stock-aware help with the requested amount, usable local total and shortfall. Usable stock comes first,
then disconnected local storage, affordable relay retrieval and production; remote stock remains explicitly
remote. Transfer energy and travel blockers are visible. Unknown deposits stay hidden until normal survey
or visit discovery. Manual Atlas/map alternatives remain available.
Obstacle clearance retains its existing **carried-only** spending rule; its help and availability use
that rule even with connected storage, offering withdrawal rather than pretending the stock is spendable.

The compact shortage overlay keeps the original panel, tab, selection, scroll and recipe batch underneath it.
Dismiss it with close, Back or the backdrop; there is no previous-step/return-to history stack.
Shortage help and explicitly tracked project actions revalidate current state; opening help never spends, crafts,
travels or accelerates jobs. Source searches reject cycles, and missing anchors produce recoverable feedback.
Research, lab, station-level, XP, capacity and hull blockers link to their existing destinations.

Players can explicitly track one project from an eligible card. Stable kind/content/world/slot/target/quantity
values are stored under `player.tracked-project` in existing save settings, not as UI objects or a schema bump.
The resume action and Menu project page show live requirements, completion and the original destination.
Replacing or clearing a project is explicit; removed content stays clearable.

Completed construction/upgrade jobs and actual stored offline Auto-Harvester/Dyson output form a dismissible
return summary. Links open the actual structure or its world. Output is still in its hopper/reserve, not cargo.
Load and same-process background/resume share the existing offline production and builder rules; background
saves establish an elapsed-time watermark so active play is not credited again. Repeated resume signals do
not repeat the elapsed interval. Real native lifecycle timing remains a device-test requirement.

## Gated gameplay rewards

`Resources/Frontier/GameplayRewardRules` ships with harvest/construction flags **off** and construction
seconds **zero**. Missing rules also fail closed. These flags are additional to provider/platform configuration,
consent and the existing per-placement policy; historical policy caps are not newly approved tuning.
No banners, interstitials, automatic requests or shortage-to-ad shortcuts are added.

The staged placements reuse stable service IDs `double-yield` and `timer-skip`. Harvest quotes the exact
additional output, requires space for both base and bonus, and does not appear on the first successful
harvest. Its normal Collect action remains available. Construction quotes the exact reduction on one job;
ordinary waiting and shard completion remain available. Eligibility checks share the service policy's
caps/cooldowns without consuming quota or showing an ad.

`player.reward-ticket` journals one request in existing settings. A detached snapshot secures base harvest
before an ad request; verified completion is recorded before atomic settlement. Settlement writes inventory,
discovery rewards or the exact job/crew timer before publishing live changes. Capacity failure retains an
earned ticket for retry. Overlap, replaced jobs, replaced saves and late callbacks cannot grant twice.
No-fill, cancellation, consent withdrawal, timeout and provider exceptions leave ordinary progression usable.
A 90-second request timeout does not interpret a late completion as a new reward.

After termination, a persisted earned ticket is retryable. An interrupted *pending* ticket is unverified:
the UI never invents SDK completion and lets the player dismiss it; secured base harvest is retained.
An obsolete earned ticket requires explicit two-step discard. Save failures are reported, not treated as
successful grants. A reward that could not be durably verified before process death cannot be reconstructed
from a client-only SDK callback; server verification/native termination tests remain activation gates.
Cloud restores reject unresolved local or incoming reward journals, preventing snapshot reward replay.
Restoring/restarting replaces the callback context.

### Reward frequency decision and remaining tuning proposal

User-directed update: no daily cap on the first-stage harvest/construction placements, rather than a
small daily allowance. `unlimitedDaily` is explicit, so old saves/assets with omitted flags retain their
existing finite-cap behavior and zero still disables a placement unless unlimited is explicitly set.
Persisted counts saturate safely; cooldown, consent, request overlap and reward settlement checks remain.
Only `double-yield` and `timer-skip` change now; later placements retain their historical inactive-stage
configuration pending their separate evaluation. Gameplay flags and production platform gates remain off.

Not annoying means passive contextual buttons, never automatic ad screens, notifications or repeat
decline dialogs. Normal Collect/wait/finish actions remain; harvest offers once per completed result,
does not retry after no-fill on that result, and does not appear on the first successful harvest.
High availability is not permission to nag or remove the no-ad path. Native experience and child-inclusive
review are still mandatory. Benefit sizes and cooldown tuning below remain proposals, not activation.

| Placement | Proposed benefit | Daily cap decision | Proposed cooldown |
|---|---|---|---|
| Harvest | +100% of that completed run, only when all output fits | None | 180 seconds |
| Construction | Up to 120 seconds on the selected job | None | 600 seconds |
| Emergency fuel / revive / targeted supplies / bonus shards | No new placement or grant in this stage | 0 for staged activation | N/A |

This is not measured retention or a balance certification. With equal-yield harvests, N normal runs give
N run-units; K accepted bonuses give N+K, with K no greater than eligible completed runs and subject to
cooldown. Twenty runs with five accepted bonuses give 25 units (25% extra); accepting a bonus for every
eligible run approaches twice the normal output. There is no longer a three-yield or six-crew-minute
daily ceiling. Under proposed cooldowns, the sustained rate is at most 20 harvest acceptances/hour and
6 construction acceptances/hour (up to 12 crew-minutes/hour), also bounded by real runs/jobs/ad availability.
An initial immediately available offer can add one at an interval boundary. This is a material
economy acceleration risk to evaluate, not a reason to make the ordinary path punitive. Actual harvest
rewards scale with tool/quality output. No direct fuel, shard, research, XP or hazard bypass is introduced.
Model low/mid/late-game bottlenecks and interactions
using observed no-ad/ad-assisted journeys before approving any activation or adding fuel/revive/supplies.
Existing nonzero policy entries remain historical development configuration, not this proposal's approval.

### Measurement proposal and activation gates

Proposed schema version 1 (definitions only; no new emitters are enabled): `player_flow_step` with stable
milestone ID and elapsed-time bucket; `shortage_journey` with target kind/content ID, resource ID and
entry/destination/return/completed stage; `progression_blocked` with resource/level/lab/capacity/travel enum;
`project_state` with kind/content ID and tracked/completed/cleared stage; `return_action` with item kind
and opened/dismissed stage; `reward_flow` with placement, eligible/offered/accepted/filled/completed/settled/
failed stage and bounded outcome enum. Do not send free text, planet names, save payloads or age/birthday.
Retention/progression reporting and any necessary SDK identifiers require a separately approved
child-inclusive privacy/retention design; do not infer consent from gameplay or previous editor preferences.

Before production: approve amounts/cooldowns and quantitative targets; complete child-inclusive
legal/platform/SDK/creative/consent review and HTTPS privacy policy; configure and verify Android/iOS;
approve dashboard schemas before enabling collection; test native touch, pause/resume, offline/no-fill,
consent withdrawal, app termination, durable settlement and save failure; observe new players finding Cargo,
choosing a goal and returning from a multistep shortage. Start harvest/construction only, evaluate, then
separately consider fuel/revive. Targeted supplies require separate economy approval. Editor tests and
virtual-phone captures do not satisfy these external approval and device gates.

## Combat

Player-built surface turrets and fuel-funded alarm waves; alarms never spawn orbital turrets. The former Orbital Turret
research is now **Turret Actuators**, increasing placed-turret firing speed by 15% per level (saved upgrade ID and effect
index preserved).

Tune `WaveFormulaSO` for threat budget, health/damage/speed per fuel cell, spawn interval and visible approach time
(default 1.2 s). More fuel extends a repeatable enemy sequence and raises individual difficulty; victory rewards never
decrease when fuel increases. Enemies enter just outside the side edges of the camera and cannot be auto-targeted until
they have spent the approach time on screen. Placed turrets still respect their configured range.

Structures use non-emissive materials, with a glowing mesh outline only while selected. Alien bodies use species-colored
surfaces, bright veins, arrival pulses, trails and hit flashes. **Frontier > Validate Combat Balance** checks repeatable
previews, monotonic fuel rewards/difficulty, enemy-sequence prefixes and the selection shader import.

## Warehouses

Build a **Warehouse** from Build > Econ (20 Scrap + 10 Wood). Each starts with 18 slots and gains 9 per upgrade, to 54 at
level 5 (`StructureSO.storageSlots` / `storageSlotsPerLevel`). Warehouses take normal building slots and store their own
resources, saved by planet and slot.

Tap a warehouse to open its storage and your backpack together. Drag stacks either way or rearrange either grid; matching
resources merge up to the stack limit, transfers move only what fits and never discard the remainder. Stored resources stay
on that planet; an operational Cargo Depot is required to spend them without withdrawing first. A translucent icon and stack count follow
mouse/touch drags, including ordinary backpack rearrangement. A resource-family filter applies to both grids; **Quick Store**
and **Quick Withdraw** transfer as much as fits in the selected family (or all families), preserving any remainder.
The panel shows used versus total storage slots. Multiple Warehouses are allowed; an operational Cargo Depot pools local
Warehouse, Cargo Depot and Auto-Harvester contents for building, crafting, research and equipment/global upgrades without
exposing Fuel Depot reserves.

An **Orbital Relay** pulls from operational storage on other visited worlds. Same-system transfers are available at every
relay tier; higher tiers extend the beam into other systems. Each pull spends energy from ship fuel tanks,
scaled by stack size and distance. Remote Fuel Depot reserves remain inaccessible.

The starter backpack has 9 slots; Ship > Cargo exposes its capacity upgrades. The imported catalog supplies the current
capacity progression; local Warehouse slots are separate from carried capacity.

## Planet sizing and travel

Planet and structure source FBX import scales are calibrated to common units, and each `ModelLibrary` entry stores a
measured correction for its assembled prefab. **Frontier > Calibrate Model Scales** repeats this after replacing models.

All planets keep a displayed radius of 1.5 and atmosphere scale 5. Logical **World Size** grows with depth (1.00 to 2.12 for the
Core Worlds), shrinking buildings, pads and pick colliders proportionally, while **Building Slots** grows from 12 to 40. The Core
Worlds author both on their `PlanetSO`; a generated world takes them from its size class (Dwarf, Small, Medium, Large, Giant,
Colossal: World Size 1.00, 1.15, 1.40, 1.75, 2.10, 2.50 and 12, 16, 24, 36, 48, 64 slots). Saved buildings keep their slot
positions. **Validate Planet Model Sizes** checks calibrated mesh diameters and capacity.

Some meshes have a visible body smaller than their bounds (spiky or lens shaped models), so each `ModelLibrary` entry also carries a
baked **ground scale** (`Frontier > Bake Model Ground Radii`): a generated world's ground radius is 1.5 times it, which keeps pads
and buildings touching the surface while the planet keeps its on-screen size. A generated world is never larger or smaller on
screen than a Core World; only its pads and buildings scale.

Menu > Settings > **Structure Labels** switches between always-visible and selected-only labels and remembers the choice.

Travel plays the departure warp first, the flight mini-game second and the destination approach last. The star map separates
the six solar systems into three spiral galaxy regions; curved departure/arrival tangents and a moving ship show the route.
The map is illustrative and does not set the price: fuel comes from `TravelModel` ([RUNTIME_MODEL.md](RUNTIME_MODEL.md) section 6),
`min(3 + AU, 8)` inside a system and `11 + 0.017 x light-years` between systems, scaled by the engine efficiency upgrades, at least
1 (Sylva to Glacia is 5). Every arrival is recorded: the world's visits and first visit, the number of worlds landed on and the
farthest distance from home. If the current world is generated, the map shows a marker for it and a short info line; the full map
for generated space is a later work package.

## Saves

Progress is saved to `frontier_save.json` (schema v2: the player's state plus *deltas*, only what the player changed on each
world; see [RUNTIME_MODEL.md](RUNTIME_MODEL.md) section 7). Changes are written about a second after the last one (at most 10 s
after the first), and immediately when the app is paused, loses focus or quits, and when the player travels. Each write goes
through a temp file that is verified before it replaces the old save, and the previous good save is kept as
`frontier_save.json.bak`; if the main file is damaged the game loads the backup. An old v1 save is migrated on first start (the
original is kept as `frontier_save.v1.bak.json`). A save written by a newer version of the game is played read-only and never
overwritten. **Menu > Settings > Reset Progress** (and **Frontier > Reset Save**) delete all of these files.

## Panel layout

Structure panels stay centered below the top HUD and above the dock. Only short, upgrade-only turret panels can follow their
structure, clamped to that area. Menus, the build/command/backpack panels and warehouse panels use the same area, so the top bar
and dock stay visible and tappable while the world behind them is dimmed by one full-screen scrim (tapping the scrim closes the
panel). Only mini-games and wave results cover the dock, and none of them cover the top HUD; all respect device safe-area insets.
Toasts start below the top bar and the build-mode banner sits just above the dock. Scrollable panels use a fixed vertical gutter
with a slim handle and no horizontal scrollbar. **Frontier > Validate Panel Layout** checks panel bounds and inventory rebuilds.
Sizes, colours, fonts and icons follow [DESIGN.md](DESIGN.md).

## Procedural worlds

Every planet the player can visit comes from the universe generator (next section); the 30 hand made **Core Worlds** are authored
content inside that same universe (`PlanetSO` assets, merged by `AuthoredWorlds`). `PlanetGenerator` and **Frontier > Generate Deep
Space Planets** remain as an authoring tool that persists extra `PlanetSO` assets; they no longer define the playable universe.

All generator biomes render: Lush, Mineral, Glacial, Volcanic, Fungal, Crystal, Obsidian, Shattered, Desert, Oceanic, Gas giant and
Barren, with every size class, rings, up to seven moons and every weather kind. A generated world gets a short deterministic
description (biome, weather, hazards, size, rings and moons) and a stable 64 bit visual seed, so it looks the same on every device
and run. **Menu > Debug > Generated world** (editor and development builds only) teleports to a world with a chosen biome, size and
minimum tier, and **Dump descriptor** copies the current world's descriptor to the log and the clipboard.

## Universe core (generator V1)

`Frontier.Core/Universe` (namespace `Frontier.Universe`, `Scripts/Core/Universe`) is the engine-free generator behind the infinite
map of [ROADMAP.md](ROADMAP.md) section 4. It is a pure function of `(UniverseConfig.Seed, GeneratorVersion, authored content,
coordinates)`; only deltas are ever saved. The game reaches it through `UniverseService` (planets as `PlanetData`, lookups by id,
travel pricing); the map and travel UI that exploit the whole universe are the `m3-*` work packages. How the pieces fit together
is in [RUNTIME_MODEL.md](RUNTIME_MODEL.md).

* **Coordinates and ids.** Positions are doubles in light-years, home at (0, 0), a sector is 64 ly. Ids are exact bit packings
  (`SectorCoord`, `SystemKey`, `PlanetKey`; string forms like `s:X:Y:I`), never hashes.
* **API.** `new Universe(new UniverseConfig(), Landmarks.CreateDefault())`, then `SystemsInSector`, `SystemsInRect`,
  `PlanetsOf`, `GetPlanet(key[, storedGeneratorVersion])`, `Density`, `SampleDensity`, `TierAt`, `GalaxyAt`. Landmark galaxies and
  authored Core Worlds come in through `AuthoredContent`. `Universe` is both a namespace and a class: in code inside
  `namespace Frontier` write `using Uni = Frontier.Universe.Universe;`.
* **Determinism.** One named hash stream per attribute (`StreamSalts`), `DetMath` instead of the platform `Math` functions, double
  arithmetic with a single explicit rounding to float. Golden digests and source-hygiene tests enforce it ([TESTING.md](TESTING.md));
  V1 is frozen, every change to its output is a new generator version.
* **Cost.** On a desktop editor 1,000 sectors with every planet generate in about 4 ms (1,326 planets) and repeated lookups
  allocate nothing; sectors and planet blocks are cached in bounded LRU caches and the type is safe to use from a background worker.
* **Look at it.** **Frontier > Universe > Universe Viewer** previews a read-only density heat map at a chosen hexadecimal
  seed and coordinate, then inspects the containing sector, systems and planets; selecting a planet shows its runtime
  descriptor, which can be copied. Preview changes do not affect the active game or save. **Frontier > Universe > Render
  Density PNG** still writes the default 30,000 ly galaxy map to `SpheerURP/Screens/universe/`.
