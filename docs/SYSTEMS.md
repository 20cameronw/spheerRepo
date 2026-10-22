# Current systems reference

Behaviour notes for the systems that exist today. The long-term direction lives in [ROADMAP.md](ROADMAP.md).

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
on that planet and are not used automatically for crafting, fuel or construction. A translucent icon and stack count follow
mouse/touch drags, including ordinary backpack rearrangement. A resource-family filter applies to both grids; **Quick Store**
and **Quick Withdraw** transfer as much as fits in the selected family (or all families), preserving any remainder.
The panel shows used versus total storage slots. Multiple Warehouses are allowed; an operational Cargo Depot pools local
Warehouse and Auto-Harvester contents for building and crafting without exposing Fuel Depot reserves.

An **Orbital Relay** pulls from operational storage on other visited worlds. Same-system transfers are available at every
relay tier; higher tiers extend the beam into other systems. Each pull spends energy from fuel carried in the backpack,
scaled by stack size and distance. Remote Fuel Depot reserves remain inaccessible.

The starter backpack has 9 slots on purpose: it fills quickly, which teaches the Warehouse early. Research adds 9 slots per
tier (up to 63).

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
