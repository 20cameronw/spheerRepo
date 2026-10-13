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
mouse/touch drags, including ordinary backpack rearrangement.

The starter backpack has 9 slots on purpose: it fills quickly, which teaches the Warehouse early. Research adds 9 slots per
tier (up to 63).

## Planet sizing and travel

Planet and structure source FBX import scales are calibrated to common units, and each `ModelLibrary` entry stores a
measured correction for its assembled prefab. **Frontier > Calibrate Model Scales** repeats this after replacing models.

All planets keep a displayed radius of 1.5 and atmosphere scale 5. Logical **World Size** grows with depth (1.00 to 2.12),
shrinking buildings, pads and pick colliders proportionally, while **Building Slots** grows from 12 to 40. Both are authored
on each `PlanetSO`, independent of display size. Saved buildings keep their slot positions. **Validate Planet Model Sizes**
checks calibrated mesh diameters and capacity.

Menu > Settings > **Structure Labels** switches between always-visible and selected-only labels and remembers the choice.

Travel plays the departure warp first, the flight mini-game second and the destination approach last. The star map separates
the six solar systems into three spiral galaxy regions; curved departure/arrival tangents and a moving ship show the route.
The map is illustrative, not a gravity simulation, and its spacing does not change fuel costs.

## Panel layout

Structure panels stay centered below the top HUD and above the dock. Only short, upgrade-only turret panels can follow their
structure, clamped to that area. Menus, the build/command/backpack panels and warehouse panels use the same area, so the top bar
and dock stay visible and tappable while the world behind them is dimmed by one full-screen scrim (tapping the scrim closes the
panel). Only mini-games and wave results cover the dock, and none of them cover the top HUD; all respect device safe-area insets.
Toasts start below the top bar and the build-mode banner sits just above the dock. Scrollable panels use a fixed vertical gutter
with a slim handle and no horizontal scrollbar. **Frontier > Validate Panel Layout** checks panel bounds and inventory rebuilds.
Sizes, colours, fonts and icons follow [DESIGN.md](DESIGN.md).

## Procedural worlds

`PlanetGenerator` builds deterministic worlds (biome palettes, capacity, gather spots) and **Frontier > Generate Deep Space
Planets** persists them as assets. The infinite, coordinate-driven universe that replaces this is described in the roadmap and its
generator already exists (next section).

## Universe core (generator V1, not wired into gameplay yet)

`Frontier.Core/Universe` (namespace `Frontier.Universe`, `Scripts/Core/Universe`) is the engine-free generator behind the infinite
map of [ROADMAP.md](ROADMAP.md) section 4. It is a pure function of `(UniverseConfig.Seed, GeneratorVersion, authored content,
coordinates)`; only deltas will ever be saved. Nothing in the game calls it yet: turning it into `PlanetData`, the map and travel
are the `m3-*` work packages.

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
* **Look at it.** **Frontier > Universe > Render Density PNG** writes the galaxy density map (30,000 ly across) to
  `SpheerURP/Screens/universe/`.
