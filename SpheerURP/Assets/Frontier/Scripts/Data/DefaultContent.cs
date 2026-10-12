using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    /// <summary>In-memory default content, used when no GameDatabase asset exists in Resources.</summary>
    public static class DefaultContent
    {
        static T New<T>(string name) where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            o.name = name;
            return o;
        }

        static ResourceSO Res(string id, string name, Color c, string desc)
        {
            var r = New<ResourceSO>(id);
            r.id = id; r.displayName = name; r.color = c; r.description = desc; r.stackSize = 64;
            return r;
        }

        static List<ItemAmount> L(params ItemAmount[] a) { return new List<ItemAmount>(a); }
        static ItemAmount A(ResourceSO r, int n) { return new ItemAmount(r, n); }

        static GatherSpot Spot(string label, MinigameKind g, ResourceSO r, int y, float d)
        {
            return new GatherSpot { label = label, game = g, resource = r, baseYield = y, difficulty = d };
        }

        static PlanetSO Planet(string id, string name, string desc, int tier, Vector2 pos, float danger,
            Color primary, Color secondary, Color rim, params GatherSpot[] spots)
        {
            var p = New<PlanetSO>(id);
            p.id = id; p.displayName = name; p.description = desc; p.tier = tier; p.mapPosition = pos;
            p.danger = danger; p.primary = primary; p.secondary = secondary; p.rim = rim;
            p.spots = new List<GatherSpot>(spots);
            return p;
        }

        static PlanetSO Extra(PlanetSO p, int style, WeatherKind w, bool rings = false)
        {
            p.style = style; p.weather = w; p.hasRings = rings;
            return p;
        }

        static UpgradeSO Up(string id, string name, string desc, UpgradeCategory cat, UpgradeEffect fx, float perLevel,
            int max, float growth, Color color, List<ItemAmount> cost, MinigameKind tool = MinigameKind.Chop, string[] names = null)
        {
            var u = New<UpgradeSO>(id);
            u.id = id; u.displayName = name; u.description = desc; u.category = cat; u.effect = fx;
            u.valuePerLevel = perLevel; u.maxLevel = max; u.costGrowth = growth; u.color = color;
            u.baseCost = cost; u.tool = tool; u.levelNames = names;
            return u;
        }

        static UpgradeSO Tiers(UpgradeSO u, params List<ItemAmount>[] costs)
        {
            u.levelCosts = new List<CostTier>();
            foreach (var c in costs) u.levelCosts.Add(new CostTier { cost = c });
            return u;
        }

        static EnemySO Alien(string id, string name, Color c, PrimitiveType shape, float scale, float hp, float speed,
            float dmg, float threat, int minWave, float weight, List<ItemAmount> bounty)
        {
            var e = New<EnemySO>(id);
            e.id = id; e.displayName = name; e.color = c; e.shape = shape; e.scale = scale; e.health = hp;
            e.speed = speed; e.damage = dmg; e.threat = threat; e.minWave = minWave; e.weight = weight; e.bounty = bounty;
            return e;
        }

        static StructureSO Struct(string id, string name, string desc, StructureKind kind, Color c, string model, List<ItemAmount> cost, float bonus,
            int max = 1, float dmg = 0f, float interval = 0.7f, float range = 8f, float beam = 0.07f)
        {
            var s = New<StructureSO>(id);
            s.id = id; s.displayName = name; s.description = desc; s.kind = kind; s.color = c; s.modelKey = model;
            s.buildCost = cost; s.bonusPerLevel = bonus; s.maxPerPlanet = max;
            s.damage = dmg; s.fireInterval = interval; s.range = range; s.beamWidth = beam;
            return s;
        }

        static RecipeSO Recipe(string name, List<ItemAmount> inputs, List<ItemAmount> outputs)
        {
            var r = New<RecipeSO>(name);
            r.displayName = name; r.inputs = inputs; r.outputs = outputs;
            return r;
        }

        public static GameDatabase Create()
        {
            var wood = Res("wood", "Wood", new Color(1.8f, 1.1f, 0.35f), "Timber from the lush worlds. Everything starts here.");
            var stone = Res("stone", "Stone", new Color(0.8f, 1.2f, 1.8f), "Dense mineral rock.");
            var iron = Res("iron", "Iron Ore", new Color(2.0f, 0.8f, 0.4f), "Raw metal for tools and turrets.");
            var crystal = Res("crystal", "Crystal", new Color(0.3f, 2.0f, 2.2f), "Resonant crystal, glows when struck.");
            var fuel = Res("fuel", "Fuel Cell", new Color(0.7f, 2.2f, 0.4f), "Powers the ship between planets.");
            var core = Res("core", "Xeno Core", new Color(2.2f, 0.4f, 1.8f), "Salvaged from fallen aliens.");

            var wave = New<WaveFormulaSO>("Wave Formula");

            var planets = new[]
            {
                Planet("sylva", "Sylva", "A gentle forest world. Perfect for a first axe swing.", 0,
                    new Vector2(0.15f, 0.85f), 0.6f, new Color(0.15f, 0.85f, 0.4f), new Color(0.02f, 0.22f, 0.12f), new Color(0.4f, 2f, 1f),
                    Spot("Ironbark Grove", MinigameKind.Chop, wood, 8, 0.25f),
                    Spot("Mossy Outcrop", MinigameKind.Mine, stone, 4, 0.3f)),
                Planet("ferrum", "Ferrum", "Rust-red plains laced with ore veins. Few trees.", 1,
                    new Vector2(0.27f, 0.78f), 1.2f, new Color(0.9f, 0.4f, 0.15f), new Color(0.25f, 0.06f, 0.03f), new Color(2f, 0.9f, 0.3f),
                    Spot("Scrub Trees", MinigameKind.Chop, wood, 3, 0.35f),
                    Spot("Quarry", MinigameKind.Mine, stone, 7, 0.35f),
                    Spot("Ore Seam", MinigameKind.Mine, iron, 4, 0.45f)),
                Planet("glacia", "Glacia", "Frozen world with cryo-vents full of fuel.", 2,
                    new Vector2(0.2f, 0.65f), 1.6f, new Color(0.4f, 0.8f, 1f), new Color(0.05f, 0.12f, 0.3f), new Color(0.5f, 1.8f, 2.4f),
                    Spot("Cryo Siphon", MinigameKind.Drill, fuel, 4, 0.45f),
                    Spot("Ice Crystals", MinigameKind.Mine, crystal, 3, 0.5f)),
                Planet("cinder", "Cinder", "Volcanic and hostile. Metal-rich, crystal-poor.", 2,
                    new Vector2(0.36f, 0.88f), 2f, new Color(1f, 0.25f, 0.1f), new Color(0.12f, 0.02f, 0.02f), new Color(2.4f, 0.5f, 0.2f),
                    Spot("Magma Vent", MinigameKind.Drill, iron, 7, 0.55f),
                    Spot("Slag Field", MinigameKind.Mine, iron, 8, 0.5f),
                    Spot("Deep Bore", MinigameKind.Drill, crystal, 2, 0.65f)),
                Planet("arbor", "Arbor Prime", "A planet-sized jungle. Titanic trees, dense crystal roots.", 3,
                    new Vector2(0.4f, 0.66f), 3f, new Color(0.25f, 1f, 0.25f), new Color(0.02f, 0.18f, 0.04f), new Color(1f, 2.4f, 0.5f),
                    Spot("Titan Grove", MinigameKind.Chop, wood, 30, 0.5f),
                    Spot("Root Crystals", MinigameKind.Mine, crystal, 5, 0.55f)),
                Planet("nyx", "Nyx", "A dark hive world. Dangerous, valuable.", 4,
                    new Vector2(0.3f, 0.5f), 4.5f, new Color(0.7f, 0.25f, 1f), new Color(0.08f, 0.02f, 0.2f), new Color(2f, 0.5f, 2.4f),
                    Spot("Crystal Heart", MinigameKind.Drill, crystal, 10, 0.7f),
                    Spot("Hive Breach", MinigameKind.Drill, core, 3, 0.75f),
                    Spot("Void Ore", MinigameKind.Mine, iron, 12, 0.6f)),

                Extra(Planet("aurum", "Aurum", "Endless golden dunes hiding dense mineral beds.", 2,
                    new Vector2(0.52f, 0.9f), 2.4f, new Color(1f, 0.78f, 0.25f), new Color(0.35f, 0.16f, 0.03f), new Color(2.4f, 1.6f, 0.4f),
                    Spot("Dune Quarry", MinigameKind.Mine, stone, 12, 0.4f),
                    Spot("Gilded Seam", MinigameKind.Mine, iron, 7, 0.5f)), 1, WeatherKind.Dust),
                Extra(Planet("tide", "Tidewater", "An ocean world of floating kelp forests and fuel-rich brine.", 3,
                    new Vector2(0.58f, 0.7f), 3.2f, new Color(0.1f, 0.5f, 1f), new Color(0.02f, 0.08f, 0.3f), new Color(0.3f, 1.6f, 2.6f),
                    Spot("Kelp Forest", MinigameKind.Chop, wood, 22, 0.45f),
                    Spot("Brine Wells", MinigameKind.Drill, fuel, 7, 0.5f)), 1, WeatherKind.Rain),
                Extra(Planet("zephyr", "Zephyr", "A pastel gas giant with floating platforms in the storm bands.", 3,
                    new Vector2(0.74f, 0.55f), 3.6f, new Color(0.95f, 0.6f, 0.85f), new Color(0.3f, 0.2f, 0.6f), new Color(2.2f, 1.2f, 2.2f),
                    Spot("Gas Siphon", MinigameKind.Drill, fuel, 10, 0.55f),
                    Spot("Cloud Crystals", MinigameKind.Mine, crystal, 5, 0.55f)), 1, WeatherKind.Storm),
                Extra(Planet("pyre", "Pyre", "A molten shell cracked open by rivers of lava.", 4,
                    new Vector2(0.5f, 0.42f), 4.8f, new Color(1f, 0.55f, 0.05f), new Color(0.08f, 0.01f, 0.01f), new Color(3f, 0.9f, 0.1f),
                    Spot("Lava Tap", MinigameKind.Drill, iron, 16, 0.6f),
                    Spot("Cinder Vein", MinigameKind.Drill, crystal, 5, 0.7f)), 3, WeatherKind.Embers),
                Extra(Planet("halcyon", "Halcyon", "A serene teal ice world, still and deceptively rich.", 4,
                    new Vector2(0.12f, 0.4f), 4.2f, new Color(0.5f, 1f, 0.9f), new Color(0.02f, 0.25f, 0.3f), new Color(0.4f, 2.4f, 2.2f),
                    Spot("Glacier Core", MinigameKind.Drill, fuel, 12, 0.6f),
                    Spot("Frost Geodes", MinigameKind.Mine, crystal, 7, 0.6f)), 4, WeatherKind.Snow),
                Extra(Planet("mire", "Mire", "A rotting swamp world. Spore storms and enormous fungal trees.", 4,
                    new Vector2(0.42f, 0.22f), 5f, new Color(0.55f, 0.75f, 0.15f), new Color(0.1f, 0.12f, 0.02f), new Color(1.6f, 2.4f, 0.3f),
                    Spot("Fungal Giants", MinigameKind.Chop, wood, 45, 0.55f),
                    Spot("Bog Wells", MinigameKind.Drill, fuel, 8, 0.6f),
                    Spot("Husk Nest", MinigameKind.Drill, core, 2, 0.7f)), 2, WeatherKind.Spores),
                Extra(Planet("obsidian", "Obsidian", "Black glass plains threaded with violet crystal.", 5,
                    new Vector2(0.82f, 0.78f), 6f, new Color(0.15f, 0.1f, 0.25f), new Color(0.02f, 0.0f, 0.06f), new Color(1.6f, 0.4f, 2.8f),
                    Spot("Glass Shelf", MinigameKind.Mine, iron, 20, 0.65f),
                    Spot("Violet Spires", MinigameKind.Mine, crystal, 9, 0.7f)), 4, WeatherKind.Storm),
                Extra(Planet("vesper", "Vesper", "Twilight world of endless dusk and warring hives.", 5,
                    new Vector2(0.64f, 0.16f), 6.5f, new Color(0.5f, 0.25f, 0.9f), new Color(0.12f, 0.02f, 0.3f), new Color(1.8f, 0.8f, 2.8f),
                    Spot("Dusk Ore", MinigameKind.Mine, iron, 22, 0.7f),
                    Spot("Hive Heart", MinigameKind.Drill, core, 5, 0.75f)), 2, WeatherKind.Dust),
                Extra(Planet("prism", "Prism", "A crystalline world that fractures light into rainbows. Rare.", 6,
                    new Vector2(0.88f, 0.4f), 7.5f, new Color(0.4f, 1f, 1f), new Color(0.7f, 0.2f, 1f), new Color(2f, 2f, 2.8f),
                    Spot("Prism Fields", MinigameKind.Mine, crystal, 18, 0.75f),
                    Spot("Refraction Bore", MinigameKind.Drill, fuel, 14, 0.75f)), 4, WeatherKind.Snow, true),
                Extra(Planet("kaos", "Kaos", "A shattered world orbiting nothing. The end of the map.", 7,
                    new Vector2(0.9f, 0.08f), 9f, new Color(0.9f, 0.9f, 1f), new Color(0.03f, 0.03f, 0.06f), new Color(2.8f, 2.8f, 3.2f),
                    Spot("Null Seam", MinigameKind.Drill, core, 9, 0.85f),
                    Spot("Singular Ore", MinigameKind.Mine, crystal, 24, 0.8f)), 2, WeatherKind.Storm, true),
                Extra(Planet("tundra", "Tundra Rho", "A windswept frozen steppe on the outer ring.", 3,
                    new Vector2(0.14f, 0.12f), 3.4f, new Color(0.85f, 0.95f, 1f), new Color(0.25f, 0.3f, 0.45f), new Color(0.8f, 1.6f, 2.4f),
                    Spot("Frozen Pines", MinigameKind.Chop, wood, 26, 0.5f),
                    Spot("Permafrost Pit", MinigameKind.Mine, stone, 16, 0.5f)), 0, WeatherKind.Snow),
                Extra(Planet("coral", "Coralis", "A living reef world with bioluminescent forests.", 4,
                    new Vector2(0.32f, 0.3f), 4.4f, new Color(1f, 0.4f, 0.6f), new Color(0.3f, 0.05f, 0.2f), new Color(2.6f, 0.8f, 1.6f),
                    Spot("Glow Reef", MinigameKind.Chop, wood, 30, 0.55f),
                    Spot("Pearl Beds", MinigameKind.Mine, crystal, 8, 0.6f)), 2, WeatherKind.Spores),
            };

            planets[1].style = 2; planets[2].style = 4; planets[3].style = 3; planets[4].style = 1; planets[5].style = 4;

            planets[0].weather = WeatherKind.Rain;
            planets[1].weather = WeatherKind.Dust;
            planets[2].weather = WeatherKind.Snow;
            planets[3].weather = WeatherKind.Embers;
            planets[4].weather = WeatherKind.Storm;
            planets[5].weather = WeatherKind.Spores;
            planets[5].hasRings = true;

            var upgrades = new[]
            {
                Tiers(Up("axe", "Axe", "Hit the glowing weak spot to fell trees. Better axes deal more damage and swing faster.", UpgradeCategory.Tools, UpgradeEffect.ToolPower, 0.6f, 6, 1.9f,
                    wood.color, L(A(wood, 10), A(stone, 4)), MinigameKind.Chop,
                    new[] { "Bare Hands", "Stone Axe", "Iron Axe", "Steel Axe", "Crystal Axe", "Plasma Chainsaw", "Singularity Saw" }),
                    L(A(wood, 6), A(stone, 2)), L(A(wood, 14), A(stone, 8)), L(A(wood, 20), A(iron, 8)),
                    L(A(iron, 18), A(wood, 30)), L(A(crystal, 10), A(iron, 25)), L(A(crystal, 30), A(core, 8))),
                Tiers(Up("pick", "Pickaxe", "Strike the shiny spot to break nodes through stages. Better picks crack them faster.", UpgradeCategory.Tools, UpgradeEffect.ToolPower, 0.6f, 6, 1.9f,
                    stone.color, L(A(wood, 8), A(stone, 8)), MinigameKind.Mine,
                    new[] { "Bare Hands", "Stone Pick", "Iron Pick", "Steel Pick", "Pneumatic Pick", "Jackhammer", "Plasma Jackhammer" }),
                    L(A(wood, 8), A(stone, 6)), L(A(wood, 14), A(stone, 14)), L(A(stone, 25), A(iron, 8)),
                    L(A(iron, 20), A(stone, 30)), L(A(iron, 35), A(crystal, 8)), L(A(crystal, 30), A(core, 8))),
                Tiers(Up("drill", "Drill", "Hold to bore through rock. Better drills fill the progress gauge faster.", UpgradeCategory.Tools, UpgradeEffect.ToolPower, 0.6f, 6, 1.9f,
                    iron.color, L(A(wood, 20), A(iron, 10)), MinigameKind.Drill,
                    new[] { "Hand Auger", "Pneumatic Drill", "Crystal Bore", "Plasma Bore", "Quantum Bore", "Singularity Bore", "Void Bore" }),
                    L(A(wood, 20), A(iron, 10)), L(A(iron, 25), A(stone, 20)), L(A(iron, 30), A(crystal, 8)),
                    L(A(crystal, 18), A(iron, 40)), L(A(crystal, 30), A(core, 6)), L(A(crystal, 50), A(core, 15))),
                Up("pack", "Backpack", "+9 slots (one more row). Carry more before you turn back.", UpgradeCategory.Ship, UpgradeEffect.BackpackSlots, 9f, 6, 1.7f,
                    new Color(1.6f, 1.6f, 0.4f), L(A(wood, 25), A(stone, 10))),
                Up("engine", "Engine Efficiency", "-8% fuel for every trip.", UpgradeCategory.Ship, UpgradeEffect.FuelEfficiency, 0.08f, 8, 1.6f,
                    fuel.color, L(A(wood, 10), A(iron, 10), A(crystal, 3))),
                Up("turret", "Orbital Turret", "+1 automatic turret during waves.", UpgradeCategory.Defense, UpgradeEffect.TurretCount, 1f, 6, 1.7f,
                    new Color(2f, 0.5f, 1.6f), L(A(wood, 15), A(iron, 12))),
                Up("turretdmg", "Turret Damage", "+35% turret damage.", UpgradeCategory.Defense, UpgradeEffect.TurretDamage, 0.35f, 10, 1.6f,
                    crystal.color, L(A(iron, 10), A(crystal, 5))),
                Up("pulse", "Ion Pulse", "+40% tap damage.", UpgradeCategory.Defense, UpgradeEffect.TapDamage, 0.4f, 10, 1.6f,
                    new Color(0.6f, 1.4f, 2.4f), L(A(stone, 12), A(iron, 6))),
                Up("shield", "Reactor Shield", "+25 reactor health.", UpgradeCategory.Defense, UpgradeEffect.ReactorHealth, 25f, 10, 1.5f,
                    new Color(0.4f, 2f, 1.2f), L(A(wood, 20), A(stone, 15))),
                Up("salvage", "Salvage Rig", "+10% wave rewards.", UpgradeCategory.Defense, UpgradeEffect.RewardBonus, 0.1f, 10, 1.8f,
                    core.color, L(A(core, 5), A(crystal, 3))),
            };

            var enemies = new[]
            {
                Alien("scout", "Scout", new Color(0.3f, 1.6f, 2.2f), PrimitiveType.Sphere, 0.45f, 10f, 1.3f, 4f, 1f, 1, 2f, L(A(core, 1))),
                Alien("swarmer", "Swarmer", new Color(2.2f, 1.6f, 0.3f), PrimitiveType.Capsule, 0.35f, 6f, 2.2f, 3f, 1f, 3, 2f, L(A(core, 1))),
                Alien("raider", "Raider", new Color(2.2f, 0.4f, 1.8f), PrimitiveType.Cube, 0.6f, 28f, 1.0f, 8f, 3f, 2, 1.5f, L(A(core, 1), A(fuel, 1))),
                Alien("brute", "Brute", new Color(2.4f, 0.3f, 0.3f), PrimitiveType.Cylinder, 0.9f, 120f, 0.6f, 25f, 10f, 5, 1f, L(A(core, 3), A(fuel, 2))),
                Alien("titan", "Titan", new Color(2.4f, 1.2f, 0.2f), PrimitiveType.Sphere, 1.5f, 500f, 0.45f, 60f, 30f, 10, 1f, L(A(core, 8), A(fuel, 5))),
            };

            var recipes = new[]
            {
                Recipe("Wood Distillate", L(A(wood, 8)), L(A(fuel, 3))),
                Recipe("Crystal Cell", L(A(crystal, 3)), L(A(fuel, 12))),
            };

            var structures = new[]
            {
                Struct("refinery", "Refinery", "Converts raw materials into fuel. Tap it to run an exchange. Higher levels refine more per batch.",
                    StructureKind.Refinery, new Color(0.7f, 2.2f, 0.4f), "struct_refinery", L(A(wood, 15), A(stone, 6)), 0.25f),
                Struct("alarm", "Alarm Beacon", "Tap it to burn fuel and call a wave. More fuel means a longer fight and bigger rewards.",
                    StructureKind.Alarm, new Color(2.2f, 0.4f, 1.8f), "struct_alarm", L(A(wood, 12), A(stone, 8)), 0.15f),
                Struct("lumber", "Lumber Camp", "Tap it to chop the trees on this planet. Higher levels yield more.",
                    StructureKind.Lumber, new Color(1.8f, 1.1f, 0.35f), "struct_lumber", L(A(wood, 6)), 0.2f),
                Struct("mine", "Mining Rig", "Tap it to mine ore and stone on this planet. Higher levels yield more.",
                    StructureKind.Mine, new Color(0.8f, 1.2f, 1.8f), "struct_mine", L(A(wood, 8)), 0.2f),
                Struct("driller", "Deep Driller", "Tap it to drill fuel, crystal and cores. Higher levels yield more.",
                    StructureKind.Drill, new Color(2f, 0.8f, 0.4f), "struct_drill", L(A(wood, 14), A(stone, 10), A(iron, 6)), 0.2f),
                Struct("gunner", "Gunner Turret", "Rapid fire kinetic turret. Fires during waves.",
                    StructureKind.Gunner, new Color(2.2f, 1.4f, 0.4f), "struct_gunner", L(A(wood, 12), A(iron, 8)), 0.3f,
                    0, 4f, 0.45f, 8f, 0.05f),
                Struct("laser", "Laser Turret", "Slow, heavy beam with long range. Fires during waves.",
                    StructureKind.Laser, new Color(0.4f, 2.2f, 2.2f), "struct_laser", L(A(stone, 12), A(iron, 12), A(crystal, 4)), 0.3f,
                    0, 14f, 1.3f, 11f, 0.12f),
                Struct("satellite", "Satellite", "Uplink relay. Does nothing yet - future upgrades will plug in here.",
                    StructureKind.Satellite, new Color(0.6f, 1.4f, 2.4f), "struct_satellite", L(A(iron, 10), A(crystal, 3)), 0.1f),
            };

            var db = New<GameDatabase>("GameDatabase");
            db.structures = structures;
            db.resources = new[] { wood, stone, iron, crystal, fuel, core };
            db.planets = planets;
            db.upgrades = upgrades;
            db.enemies = enemies;
            db.recipes = recipes;
            db.formula = wave;
            db.fuel = fuel;
            db.startPlanet = planets[0];
            db.startingItems = L(A(fuel, 10), A(wood, 24));
            return db;
        }
    }
}
