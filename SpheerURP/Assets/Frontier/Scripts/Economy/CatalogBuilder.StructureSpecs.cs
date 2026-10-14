using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    public static partial class CatalogBuilder
    {
        sealed class StructSpec
        {
            public string id, name, desc, effectKind = "", capacityKind = "", research, bundle, legacy;
            public StructureRole role;
            public int tier, footprint = 1, maxPer = 1, maxLevel = 10, unlockTier;
            public double v1, v2, growth = 1.7, timeStart, timeStep = 0.85, effBase, effPer;
            public double capBase, capPer;
            public double[] effTable, capTable;
            public HarvestMethod[] gathers = new HarvestMethod[0];
            public double damage, interval, range;
            public bool mega;
            public double megaTime = 1.0;

            public StructSpec C(double v1, double growth, string bundle, double v2 = 0.0) { this.v1 = v1; this.growth = growth; this.bundle = bundle; this.v2 = v2; return this; }
            public StructSpec T(double start, double step) { timeStart = start; timeStep = step; return this; }
            public StructSpec E(string kind, double baseValue, double perLevel) { effectKind = kind; effBase = baseValue; effPer = perLevel; return this; }
            public StructSpec ET(string kind, params double[] table) { effectKind = kind; effTable = table; return this; }
            public StructSpec Cap(string kind, double baseValue, double perLevel) { capacityKind = kind; capBase = baseValue; capPer = perLevel; return this; }
            public StructSpec CapT(string kind, params double[] table) { capacityKind = kind; capTable = table; return this; }
            public StructSpec U(int tier, string research) { unlockTier = tier; this.research = research; return this; }
            public StructSpec G(string letters) { gathers = Methods(letters); return this; }
            public StructSpec Gun(double damage, double interval, double range) { this.damage = damage; this.interval = interval; this.range = range; return this; }
            public StructSpec Footprint(int f, int maxPer = 1) { footprint = f; this.maxPer = maxPer; return this; }
            public StructSpec Levels(int n) { maxLevel = n; return this; }
            public StructSpec Legacy(string kind) { legacy = kind; return this; }
            public StructSpec Mega(double timeMult) { mega = true; maxLevel = 5; megaTime = timeMult; return this; }
        }

        static StructSpec S(string id, string name, StructureRole role, int tier, string desc)
        {
            return new StructSpec { id = id, name = name, role = role, tier = tier, desc = desc };
        }

        const StructureRole Gat = StructureRole.Gather, Prc = StructureRole.Process, Sto = StructureRole.Storage, Def = StructureRole.Defense, Uti = StructureRole.Utility, Meg = StructureRole.Megaproject;

        // Bundle shorthand reused by the growth of cost over levels: new parts enter at later levels, each below a third of the active weight.
        const string Early = "scrap:1@1,wood:0.4@2,planks:0.5@3,iron_plate:0.5@5,steel:0.6@8";

        /// <summary>The 31 structures: 9 existing (legacy kinds) and 22 new. The satellite is folded into the Orbital Relay.</summary>
        static readonly StructSpec[] StructureSpecs =
        {
            // ------------------------------------------------------------ Gather (7)
            S("lumber", "Lumber Camp", Gat, 0, "Tap it to chop the trees on this planet. Higher levels yield more.")
                .Legacy("Lumber").G("CH").C(6, 1.70, "scrap:1@1,wood:0.4@2,planks:0.5@4,iron_plate:0.5@6,steel:0.6@8").T(0, 0.80).E("yieldBonus", 0, 0.25),
            S("mine", "Mining Rig", Gat, 0, "Tap it to mine stone and ore on this planet. Free to build. Higher levels yield more.")
                .Legacy("Mine").G("MV").C(0, 1.70, "scrap:1@2,stone:0.6@3,planks:0.5@4,iron_plate:0.5@6,steel:0.6@8", 8).T(0, 0.80).E("yieldBonus", 0, 0.25),
            S("driller", "Deep Driller", Gat, 1, "Tap it to drill ore, crystal and xeno cores. Higher levels yield more.")
                .Legacy("Drill").G("D").C(33, 1.70, "scrap:25@1,stone:8@1,planks:5@2,iron_plate:8@3,steel:8@6").T(0.6, 0.85).E("yieldBonus", 0, 0.20).U(1, "deep_drilling"),
            S("biodome", "Biodome Harvester", Gat, 1, "Tap it to harvest the plant growth on this planet. Higher levels yield more.")
                .G("H").C(60, 1.70, "scrap:2@1,planks:1.5@1,glass:0.8@3,iron_plate:0.8@4,steel:0.8@7").T(0.8, 0.85).E("yieldBonus", 0, 0.20).U(1, "bio_cultivation"),
            S("siphon_rig", "Gas Siphon Rig", Gat, 2, "Tap it to siphon gas from the atmosphere. Higher levels yield more.")
                .G("S").C(700, 1.70, "steel:3@1,planks:1@1,glass:1.5@1,copper_wire:1@3,biocomposite:1@5").T(2.0, 0.90).E("yieldBonus", 0, 0.20).U(2, "gas_extraction"),
            S("excavator", "Excavation Rig", Gat, 2, "Tap it to excavate buried ice, sand and rare salts. Higher levels yield more.")
                .G("X").C(700, 1.70, "steel:3@1,planks:1@1,glass:1.5@1,circuit_board:1@3,optic_lens:1@6").T(2.0, 0.90).E("yieldBonus", 0, 0.20).U(2, "geo_survey"),
            S("salvage_yard", "Salvage Yard", Gat, 2, "Sorts wreckage into scrap and the occasional cache. Higher levels yield more.")
                .G("V").C(650, 1.70, "scrap:1.5@1,steel:2.5@1,planks:1@1,circuit_board:1@4,servo_motor:1@7").T(2.0, 0.90).E("yieldBonus", 0, 0.20).U(2, "salvage_ops"),

            // ------------------------------------------------------------ Process (5)
            S("refinery", "Refinery", Prc, 0, "Converts raw materials into fuel and planks. Higher levels craft bigger batches per tap and unlock better recipes.")
                .Legacy("Refinery").C(15, 1.75, "scrap:1@1,wood:0.4@2,planks:0.5@3,iron_plate:0.5@5,steel:0.6@8").T(0, 0.85).ET("batchSize", 1, 2, 4, 8, 14, 24, 40, 64, 100, 150),
            S("fabricator", "Fabricator", Prc, 1, "Draws wire and assembles components. Higher levels craft bigger batches per tap and unlock better recipes.")
                .Legacy("Fabricator").C(70, 1.75, "scrap:2@1,planks:1.5@1,iron_plate:1@2,copper_wire:0.8@4,steel:1@6").T(0.8, 0.90).ET("batchSize", 1, 2, 4, 8, 14, 24, 40, 64, 100, 150).U(1, "fabrication"),
            S("foundry", "Foundry", Prc, 1, "Smelts ore into plates and alloys. Higher levels craft bigger batches per tap and unlock tougher alloys.")
                .C(90, 1.75, "scrap:2@1,planks:1.5@1,stone:1@1,iron_plate:1@3,steel:1.2@6").T(0.8, 0.90).ET("batchSize", 1, 2, 4, 8, 14, 24, 40, 64, 100, 150).U(1, "smelting"),
            S("chem_reactor", "Chem Reactor", Prc, 3, "Mixes composites and charges plasma cells.")
                .Footprint(2).C(4500, 1.75, "steel:3@1,planks:0.8@1,biocomposite:2@1,glass:1.5@1,circuit_board:2@3,titanium_plate:2@5").T(3.5, 0.90).ET("batchSize", 1, 2, 4, 8, 14, 24, 40, 64, 100, 150).U(3, "chemistry"),
            S("quantum_lab", "Quantum Lab", Prc, 4, "Etches quantum chips and grows void alloy, warp coils and antimatter.")
                .Footprint(2).C(30000, 1.68, "steel:2@1,planks:0.4@1,titanium_plate:2.5@1,optic_lens:2@1,circuit_board:2@1,quantum_chip:2@5").T(4.5, 0.90).ET("batchSize", 1, 2, 4, 8, 14, 24, 40, 64, 100, 150).U(4, "quantum_computing"),

            // ------------------------------------------------------------ Storage and logistics (4)
            S("warehouse", "Warehouse", Sto, 0, "Stores resources on this planet. Drag stacks between your backpack and storage. The first upgrades add 9 slots each; the late ones add far more.")
                .Legacy("Warehouse").Footprint(1, 0).C(30, 1.60, "scrap:2@1,wood:1@1,planks:0.8@3,iron_plate:0.8@5,steel:1@7").T(0, 0.80).E("none", 0, 0).CapT("storageSlots", 18, 27, 36, 45, 54, 66, 84, 108, 138, 174),
            S("fuel_depot", "Fuel Depot", Sto, 2, "Holds fuel cells, plasma and antimatter in dense racks that do not use backpack slots.")
                .C(600, 1.65, "steel:3@1,planks:1@1,fuel:1@1,glass:1@2,titanium_plate:1@5").T(2.0, 0.85).E("energyStackBonus", 0, 0.15).Cap("energySlots", 6, 3).U(2, "fuel_logistics").Levels(8),
            S("cargo_depot", "Cargo Depot", Sto, 2, "Pools every warehouse on this planet, so crafting and building draw from all of them at once.")
                .Footprint(2).C(900, 1.65, "steel:3@1,planks:1.5@1,iron_plate:1.5@1,circuit_board:1@3,biocomposite:1@5").T(2.5, 0.90).E("none", 0, 0).Cap("storageSlots", 6, 6).U(2, "cargo_logistics").Levels(8),
            S("orbital_relay", "Orbital Relay", Sto, 4, "Links your warehouses by a tight beam: withdraw remotely across the system, then across the galaxy, for a little energy. Replaces the old Satellite.")
                .Legacy("Satellite").C(25000, 1.70, "steel:2@1,planks:0.4@1,titanium_plate:2.5@1,circuit_board:2@1,quantum_chip:2@4,servo_motor:1.5@5").T(4.5, 0.90)
                .E("transferEnergyFactor", 1.0, -0.07).CapT("linkRadiusLy", 0, 0, 60, 150, 400, 1000, 3000, 10000, 30000, 100000).U(4, "orbital_logistics"),

            // ------------------------------------------------------------ Defense (6)
            S("gunner", "Gunner Turret", Def, 0, "Rapid fire kinetic turret. Fires during waves.")
                .Legacy("Gunner").Footprint(1, 0).C(29, 1.75, "scrap:1.5@1,wood:1@1,stone:0.6@1,iron_plate:0.8@3,steel:1@6").T(0, 0.80).E("damageMult", 1.0, 0.30).Gun(4, 0.45, 8),
            S("laser", "Laser Turret", Def, 2, "Slow, heavy beam with long range. Fires during waves.")
                .Legacy("Laser").Footprint(1, 0).C(800, 1.75, "steel:3@1,glass:1.5@1,crystal:2@1,planks:1@1,optic_lens:1@5,servo_motor:1@7").T(2.0, 0.90).E("damageMult", 1.0, 0.30).Gun(14, 1.3, 11).U(2, "ballistics"),
            S("shield_pylon", "Shield Pylon", Def, 2, "Projects a field that raises reactor health.")
                .C(700, 1.75, "steel:3@1,glass:1.5@1,crystal:1.5@1,planks:1@1,circuit_board:1@4,biocomposite:1@6").T(2.0, 0.90).E("reactorHealthBonus", 0.20, 0.20).U(2, "shield_tech"),
            S("missile_silo", "Missile Silo", Def, 3, "Launches a splash missile that clears clustered raiders. Fires during waves.")
                .Footprint(2, 0).C(4200, 1.75, "steel:3@1,planks:0.8@1,thermal_alloy:2.5@1,servo_motor:2@2,titanium_plate:1.5@4").T(3.5, 0.90).E("damageMult", 1.0, 0.30).Gun(40, 3.0, 14).U(3, "missile_tech"),
            S("tesla_coil", "Tesla Coil", Def, 3, "Arcs lightning between nearby aliens. Fires during waves.")
                .Footprint(1, 0).C(4000, 1.75, "steel:3@1,copper_wire:1@1,crystal:2@1,circuit_board:2@2,optic_lens:1.5@5").T(3.5, 0.90).E("damageMult", 1.0, 0.30).Gun(9, 0.9, 6).U(3, "tesla_tech"),
            S("repair_bay", "Repair Bay", Def, 3, "Slowly repairs the reactor during a wave.")
                .C(3800, 1.75, "steel:3@1,planks:0.8@1,biocomposite:2@1,circuit_board:2@2,servo_motor:1.5@5").T(3.5, 0.90).E("reactorRegenPerSecond", 0.40, 0.40).U(3, "field_repair"),

            // ------------------------------------------------------------ Utility (6)
            S("alarm", "Alarm Beacon", Uti, 0, "Tap it to burn fuel and call a wave. More fuel means a longer fight and bigger rewards.")
                .Legacy("Alarm").C(12, 1.70, "scrap:1@1,wood:0.4@2,planks:0.5@3,iron_plate:0.5@5,steel:0.6@8").T(0, 0.80).E("waveRewardBonus", 0, 0.15),
            S("research_lab", "Research Lab", Uti, 1, "Unlocks research. Higher levels open deeper research nodes and tool tiers.")
                .Footprint(2).C(80, 1.80, "planks:2@1,scrap:1.5@1,iron_plate:1@2,glass:1@3,copper_wire:0.8@4,circuit_board:1@6").T(1.0, 0.95).E("labLevel", 1, 1.0).U(1, null),
            S("radar_tower", "Radar Tower", Uti, 1, "Extends the survey radius: see more worlds around you before you jump.")
                .C(60, 1.70, "scrap:2@1,planks:1.5@1,iron_plate:1@3,copper_wire:0.8@4,circuit_board:0.8@6").T(1.0, 0.85).E("surveyRadiusBonusLy", 15, 15).U(1, "survey_basics").Levels(8),
            S("colony_hub", "Colony Hub", Uti, 3, "Adds builder crews at levels 3 and 7. Obstacle clearing is planned for a later update.")
                .Footprint(2).C(6000, 1.80, "planks:1@1,steel:3@1,biocomposite:2@1,glass:1.5@1,circuit_board:2@3,servo_motor:1.5@6").T(3.5, 0.95)
                .ET("extraBuilders", 0, 0, 1, 1, 1, 1, 2, 2, 2, 2).Cap("obstacleDiscountPercent", 0, 5).U(3, "colony_charter"),
            S("hangar", "Hangar", Uti, 4, "A spaceport hangar: cuts the fuel cost of every trip.")
                .Footprint(3).C(35000, 1.75, "steel:2@1,planks:0.5@1,titanium_plate:2.5@1,servo_motor:2@2,quantum_chip:1.5@5").T(4.5, 0.90).E("travelFuelDiscount", 0.035, 0.035).U(4, "hangar_ops"),
            S("auto_harvester", "Auto-Harvester", Uti, 4, "Slowly gathers raw bulk materials while you are away. Active harvesting stays far better.")
                .Footprint(1, 2).C(28000, 1.75, "steel:2@1,planks:0.5@1,servo_motor:2@1,circuit_board:2@1,titanium_plate:1.5@4").T(4.5, 0.90).E("runEquivalentsPerHour", 6, 2.0).Cap("offlineCapHours", 8, 0).U(4, "automation"),

            // ------------------------------------------------------------ Megaprojects (3): five stages each, staged contributions, long timers
            // Weights are VALUE shares: tier-0 bulk stays around one to three percent of a stage, which is still thousands of wood and stone.
            S("terraformer", "Terraformer", Meg, 5, "Megaproject. Reshapes the planet in five stages; each stage permanently raises harvest yield everywhere.")
                .Footprint(4).C(600000, 1.90, "wood:0.8@1,stone:0.5@1,planks:1.1@1,steel:4@1,biocomposite:5@1,titanium_plate:8@1,thermal_alloy:11@1,servo_motor:12@1,plasma:15@1,quantum_chip:30@1,void_alloy:12@3").E("globalYieldBonus", 0, 0.06).U(5, "terraforming").Mega(1.0),
            S("warp_gate", "Warp Gate", Meg, 6, "Megaproject. A ring gate in orbit: each stage cuts fuel costs and extends jump range.")
                .Footprint(3).C(1500000, 1.90, "wood:0.6@1,stone:0.4@1,planks:0.9@1,steel:3@1,titanium_plate:8@1,rad_shield_alloy:12@1,plasma:14@1,quantum_chip:30@1,warp_coil:20@2,void_alloy:12@3").E("gateFuelDiscount", 0, 0.08).U(6, "warp_gate_engineering").Mega(1.15),
            S("dyson_collector", "Dyson Collector", Meg, 7, "Megaproject. A swarm of collectors around the star feeds your ships fuel while you sleep.")
                .Footprint(4).C(4000000, 1.90, "wood:0.4@1,stone:0.4@1,planks:0.7@1,steel:2.5@1,titanium_plate:6@1,rad_shield_alloy:10@1,plasma:12@1,quantum_chip:25@1,warp_coil:20@1,antimatter:18@2,void_alloy:15@1").E("passiveEnergyPerHour", 0, 400).U(7, "stellar_engineering").Mega(1.3),
        };
    }
}
