using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    public static partial class CatalogBuilder
    {
        // ======================================================================================== tool ladders
        sealed class ToolSpec
        {
            public string id, name, games;
            public ToolKind kind;
            public string[] names, bundles;
            public double v2, growth;
            public int reachBase;
        }

        static ToolSpec Tl(string id, string name, ToolKind kind, string games, double v2, double growth, int reachBase, string names, params string[] bundles)
        {
            return new ToolSpec { id = id, name = name, kind = kind, games = games, v2 = v2, growth = growth, reachBase = reachBase, names = names.Split('|'), bundles = bundles };
        }

        // Tiers 1..7: tier 1 is the free starting tool; bundles are the costs of tiers 2..7.
        static readonly ToolSpec[] ToolSpecs =
        {
            Tl("axe", "Axe", ToolKind.Axe, "CH", 40, 4.6, 0, "Bare Hands|Stone Axe|Iron Axe|Steel Axe|Crystal Axe|Plasma Chainsaw|Singularity Saw",
                "wood:1,stone:0.4", "wood:1,iron_plate:1.2", "planks:1.5,steel:2.5,wood:0.5", "steel:2,crystal:2.5,planks:1", "plasma:2,servo_motor:2,titanium_plate:2,steel:1", "warp_coil:2,quantum_chip:2,void_alloy:2,plasma:1"),
            Tl("pick", "Pickaxe", ToolKind.Pick, "M", 40, 4.6, 0, "Bare Hands|Stone Pick|Iron Pick|Steel Pick|Pneumatic Pick|Jackhammer|Plasma Jackhammer",
                "wood:1,stone:1", "wood:0.8,stone:0.8,iron_plate:1.2", "steel:2.5,stone:1,planks:1", "steel:2,copper_wire:1.5,circuit_board:1.5", "titanium_plate:2,servo_motor:2,plasma:1,steel:1", "quantum_chip:2,void_alloy:1.5,plasma:1.5,titanium_plate:1"),
            Tl("claw", "Salvage Claw", ToolKind.Claw, "V", 60, 4.6, 0, "Grabber|Magnet Claw|Servo Claw|Titan Claw|Plasma Claw|Gravity Claw|Singularity Claw",
                "iron_plate:2,copper_wire:1.5,planks:1", "steel:2,copper_wire:1.5,circuit_board:1", "steel:2,servo_motor:2,titanium_plate:1", "plasma:2,servo_motor:2,thermal_alloy:1.5", "quantum_chip:2,servo_motor:2,titanium_plate:1.5,plasma:1", "warp_coil:2,void_alloy:1.5,quantum_chip:2,plasma:1"),
            Tl("drill", "Drill", ToolKind.Drill, "D", 80, 4.7, 1, "Hand Auger|Pneumatic Drill|Crystal Bore|Plasma Bore|Quantum Bore|Singularity Bore|Void Bore",
                "iron_plate:2,stone:1,planks:0.8", "iron_plate:1.5,crystal:2,steel:1", "steel:2,crystal:2,plasma:1.5", "plasma:2,core:2,quantum_chip:1,servo_motor:1", "plasma:2,core:3,quantum_chip:2,thermal_alloy:1", "void_alloy:2,warp_coil:1.5,core:3,plasma:1"),
            Tl("siphon", "Siphon", ToolKind.Siphon, "S", 900, 4.6, 2, "Hand Pump|Vacuum Pump|Cryo Siphon|Turbine Siphon|Plasma Siphon|Vortex Siphon|Singularity Siphon",
                "steel:3,copper_wire:2,glass:1.5", "steel:2,glass:1,cryo_composite:1.5,methane:1", "steel:2,servo_motor:2,titanium_plate:1.5", "plasma:2,servo_motor:1.5,optic_lens:1,thermal_alloy:1", "quantum_chip:2,plasma:2,helium3:1.5,titanium_plate:1", "warp_coil:2,quantum_chip:2,antimatter:1,void_alloy:1"),
            Tl("scanner", "Excavator", ToolKind.Scanner, "X", 900, 4.6, 2, "Trowel|Sonar Scanner|Resonance Probe|Seismic Excavator|Plasma Excavator|Quantum Excavator|Void Excavator",
                "steel:3,glass:2,circuit_board:1.5", "optic_lens:2,circuit_board:2,steel:1.5", "steel:2,servo_motor:2,optic_lens:1.5", "plasma:2,servo_motor:1.5,quantum_chip:1", "quantum_chip:3,optic_lens:1.5,rad_shield_alloy:1,helium3:1", "void_alloy:2,warp_coil:1.5,quantum_chip:2,dark_matter:1"),
        };

        static readonly int[] ToolReachSteps = { 0, 1, 2, 3, 4, 6 };
        static readonly int[] ToolLabLevels = { 0, 0, 1, 2, 3, 4, 6 };
        static readonly string[] ToolResearch = { null, null, null, "toolsmithing_1", "toolsmithing_2", "toolsmithing_3", "toolsmithing_4" };
        static readonly double[] ToolMultDefault = { 1.0, 1.6, 2.4, 3.6, 5.4, 8.2, 12.5 };
        static readonly double[] ToolRunTimeDefault = { 1.00, 0.96, 0.92, 0.88, 0.84, 0.80, 0.76 };

        // ======================================================================================== upgrades
        sealed class UpgSpec
        {
            public string id, name, desc, bundle;
            public UpgradeGroup group;
            public EffectKind effect;
            public Hazards hazard;
            public double v1, growth;
            public double[] values;
            public string[] names, research;
            public int[] reach;
        }

        static UpgSpec Up(string id, string name, UpgradeGroup group, EffectKind effect, string desc, double v1, double growth, string bundle, double[] values, string names)
        {
            return new UpgSpec { id = id, name = name, group = group, effect = effect, desc = desc, v1 = v1, growth = growth, bundle = bundle, values = values, names = names == null ? null : names.Split('|') };
        }

        static UpgSpec Rq(this UpgSpec u, params string[] research) { u.research = research; return u; }

        static UpgSpec Rt(this UpgSpec u, params int[] reach) { u.reach = reach; return u; }

        static UpgSpec Hz(this UpgSpec u, Hazards h) { u.hazard = h; return u; }

        static readonly UpgSpec[] UpgradeSpecs =
        {
            // ---------------------------------------------------------------- Ship
            Up("pack", "Backpack", UpgradeGroup.Ship, EffectKind.BackpackSlots, "+9 slots (one more row). Carry more before you turn back.", 60, 3.0,
                "wood:1@1,stone:1@1,planks:1@2,iron_plate:1@3,steel:1.5@4,titanium_plate:1@6,biocomposite:1@7", new double[] { 18, 27, 36, 45, 54, 63, 72, 81 },
                "Satchel|Rucksack|Field Pack|Expedition Pack|Cargo Harness|Hauler Rig|Titan Frame|Exo-Hauler").Rt(0, 0, 1, 2, 3, 4, 5, 7),
            Up("engine", "Engine Efficiency", UpgradeGroup.Ship, EffectKind.FuelEfficiency, "Every trip burns less fuel.", 50, 3.0,
                "wood:1@1,scrap:1@1,iron_plate:1@3,steel:1.5@4,copper_wire:1@4,thermal_alloy:1@6,plasma:1@7,quantum_chip:1@9", new double[] { 0.92, 0.85, 0.78, 0.72, 0.66, 0.61, 0.56, 0.52, 0.48, 0.45 },
                "Tuned Injectors|Flow Manifold|Ion Nozzles|Cryo Intake|Plasma Lens|Phase Vents|Zero-Point Taps|Gradient Drive|Resonant Core|Quiet Singularity").Rt(0, 0, 1, 2, 3, 4, 5, 6, 8, 9),
            Up("jump", "Jump Range", UpgradeGroup.Ship, EffectKind.JumpRange, "A single jump reaches farther, so depth is earned.", 150, 3.1,
                "scrap:1@1,planks:1@1,iron_plate:1@1,steel:1.5@3,copper_wire:1@3,circuit_board:1@4,titanium_plate:1@5,quantum_chip:1@6,warp_coil:0.5@9", new double[] { 36, 55, 85, 130, 200, 310, 480, 750, 1200, 2000 },
                "Jump Coils|Capacitor Bank|Resonance Array|Folded Lattice|Deep Coils|Rift Anchors|Tunnel Drive|Gate Harmonics|Fold Engine|Boundless Drive")
                .Rq("jump_drive_1", "jump_drive_2", "jump_drive_2", "jump_drive_3", "jump_drive_3", "jump_drive_4", "jump_drive_4", "jump_drive_5", "jump_drive_5", "jump_drive_5"),
            Up("scanner_range", "Scanner Range", UpgradeGroup.Ship, EffectKind.ScannerRange, "Worlds within range of any world you have visited show their resources.", 100, 3.2,
                "scrap:1@1,glass:1@1,copper_wire:1@2,circuit_board:1@3,optic_lens:1@4,quantum_chip:1@6", new double[] { 60, 110, 180, 300, 500, 800, 1300, 2200 },
                "Long Lens|Sweep Array|Deep Survey|Parallax Net|Galactic Atlas|Dark Survey|Lattice Scan|Cosmic Ear")
                .Rq("survey_basics", "long_range_scanning_1", "long_range_scanning_1", "long_range_scanning_2", "long_range_scanning_2", "long_range_scanning_3", "long_range_scanning_3", "long_range_scanning_3"),
            Up("fuel_tank", "Fuel Tank", UpgradeGroup.Ship, EffectKind.FuelTankSlots, "Dedicated slots for fuel cells, plasma and antimatter, so fuel stops eating backpack space.", 60, 3.3,
                "scrap:1@1,planks:1@1,iron_plate:1@2,steel:1.5@3,glass:1@3,titanium_plate:1@5", new double[] { 2, 4, 7, 10, 14, 18 },
                "Auxiliary Cell|Reserve Tank|Baffled Tank|Cryo Tank|Plasma Tank|Containment Vault").Rt(0, 1, 2, 3, 4, 6),
            Up("hull_toxic", "Toxic Seal", UpgradeGroup.Ship, EffectKind.HullPlating, "Biocomposite plating that lets the hull set down on toxic worlds.", 900, 24.0,
                "biocomposite:3@1,steel:1@1,planks:0.5@1", new double[] { 1, 2, 3 }, "Biocomposite Seal|Triple Seal|Sealed Hull").Hz(Hazards.Toxic).Rq("hazmat_toxic", "hazmat_toxic", "hazmat_toxic").Rt(1, 4, 8),
            Up("hull_heat", "Thermal Plating", UpgradeGroup.Ship, EffectKind.HullPlating, "Thermal alloy plating for volcanic and desert worlds.", 6000, 14.0,
                "thermal_alloy:3@1,steel:1@1,titanium_plate:1@1", new double[] { 1, 2, 3 }, "Thermal Plating|Ceramic Shell|Magma Shell").Hz(Hazards.Heat).Rq("hazmat_heat", "hazmat_heat", "hazmat_heat").Rt(2, 5, 9),
            Up("hull_cold", "Cryo Plating", UpgradeGroup.Ship, EffectKind.HullPlating, "Cryo composite plating for glacial worlds.", 6000, 14.0,
                "cryo_composite:3@1,steel:1@1,biocomposite:1@1", new double[] { 1, 2, 3 }, "Cryo Plating|Thermal Break|Polar Shell").Hz(Hazards.Cold).Rq("hazmat_cold", "hazmat_cold", "hazmat_cold").Rt(2, 5, 9),
            Up("hull_rad", "Rad-Shield Plating", UpgradeGroup.Ship, EffectKind.HullPlating, "Rad-Shield alloy plating for irradiated worlds and hostile stars.", 40000, 12.0,
                "rad_shield_alloy:3@1,steel:1@1,glass:1@1", new double[] { 1, 2, 3 }, "Rad-Shield Plating|Lead Lattice|Pulsar Shell").Hz(Hazards.Radiation).Rq("hazmat_rad", "hazmat_rad", "hazmat_rad").Rt(3, 6, 9),
            Up("hull_void", "Void Shell", UpgradeGroup.Ship, EffectKind.HullPlating, "Void alloy shell for worlds where space itself is thin.", 500000, 8.0,
                "void_alloy:3@1,rad_shield_alloy:1@1,warp_coil:0.5@2", new double[] { 1, 2, 3 }, "Void Shell|Event Shell|Null Shell").Hz(Hazards.Void).Rq("hazmat_void", "hazmat_void", "hazmat_void").Rt(5, 7, 10),

            // ---------------------------------------------------------------- Defense (legacy ids and effects kept)
            Up("turret", "Turret Actuators", UpgradeGroup.Defense, EffectKind.TurretFireRate, "+15% firing speed for placed turrets. Does not spawn turrets.", 27, 2.2,
                "wood:1@1,scrap:1@1,iron_plate:1@2,steel:1.5@3,servo_motor:1@5", new double[] { 0.15, 0.30, 0.45, 0.60, 0.75, 0.90 }, null).Rt(0, 0, 1, 1, 2, 2),
            Up("turretdmg", "Turret Damage", UpgradeGroup.Defense, EffectKind.TurretDamage, "+35% turret damage.", 33, 2.2,
                "iron_plate:1@1,scrap:0.5@1,steel:1@3,crystal:1@4,thermal_alloy:1@6,plasma:1@8", new double[] { 0.35, 0.70, 1.05, 1.40, 1.75, 2.10, 2.45, 2.80, 3.15, 3.50 }, null).Rt(0, 0, 1, 1, 2, 2, 3, 3, 4, 4),
            Up("pulse", "Ion Pulse", UpgradeGroup.Defense, EffectKind.TapDamage, "+40% tap damage.", 28, 2.2,
                "stone:1@1,scrap:1@1,iron_plate:1@2,crystal:1@4,optic_lens:1@6", new double[] { 0.4, 0.8, 1.2, 1.6, 2.0, 2.4, 2.8, 3.2, 3.6, 4.0 }, null).Rt(0, 0, 1, 1, 2, 2, 3, 3, 4, 4),
            Up("shield", "Reactor Shield", UpgradeGroup.Defense, EffectKind.ReactorHealth, "+25 reactor health.", 27, 2.2,
                "wood:1@1,stone:1@1,planks:1@2,steel:1.5@3,biocomposite:1@5,titanium_plate:1@7", new double[] { 25, 50, 75, 100, 125, 150, 175, 200, 225, 250 }, null).Rt(0, 0, 1, 1, 2, 2, 3, 3, 4, 4),
            Up("salvage", "Salvage Rig", UpgradeGroup.Defense, EffectKind.RewardBonus, "+10% wave rewards.", 54, 2.2,
                "core:1@1,crystal:0.6@1,steel:1@3,plasma:1@6", new double[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 }, null).Rt(0, 0, 1, 1, 2, 2, 3, 3, 4, 4),

            // ---------------------------------------------------------------- Logistics
            Up("compression", "Cargo Compression", UpgradeGroup.Logistics, EffectKind.StackMultiplier, "Every stack in your backpack and warehouses holds more: the end-game answer to storage.", 18000, 5.5,
                "steel:2@1,titanium_plate:2@1,servo_motor:1.5@1,quantum_chip:1.5@2,void_alloy:1@3", new double[] { 1.15, 1.30, 1.50, 1.75 }, "Vacuum Packing|Dense Racking|Pressure Stacks|Quantum Compression")
                .Rq("compression_1", "compression_2", "compression_3", "compression_3").Rt(3, 5, 7, 9),
        };

        // ======================================================================================== research tree
        sealed class RsrSpec
        {
            public string id, name, desc, branch, extra, unlocks;
            public int tier;
            public string[] prereqs;
            public double v;
        }

        static RsrSpec Rs(string id, string name, string branch, int tier, string prereqs, double v, string extra, string unlocks, string desc)
        {
            return new RsrSpec
            {
                id = id, name = name, branch = branch, tier = tier, prereqs = prereqs.Length == 0 ? new string[0] : prereqs.Split(' '), v = v, extra = extra, unlocks = unlocks, desc = desc
            };
        }

        // Default bundle per node tier; a node adds its own topical fragment.
        static readonly string[] ResearchDefaultBundle =
        {
            "scrap:2,wood:1.5,stone:1,planks:1",
            "scrap:2,wood:1.5,stone:1,planks:1",
            "iron_plate:2,planks:1.5,glass:1,copper_wire:1,scrap:1",
            "steel:2.5,planks:0.8,glass:1.5,circuit_board:1.5,biocomposite:1",
            "steel:2,titanium_plate:2,circuit_board:2,optic_lens:2,thermal_alloy:1",
            "titanium_plate:2,quantum_chip:2,servo_motor:2,plasma:1.5,rad_shield_alloy:1",
            "quantum_chip:3,plasma:2,rad_shield_alloy:2,titanium_plate:2",
            "quantum_chip:3,warp_coil:2,void_alloy:2,antimatter:1.5,plasma:1",
        };

        static readonly RsrSpec[] ResearchSpecs =
        {
            // ---- Tier 1 (Research Lab level 1)
            Rs("survey_basics", "Survey Basics", "Navigation", 1, "", 60, "scrap:1", "structure:radar_tower;upgrade:scanner_range:1", "Calibrate the ship's sensors and raise a radar tower."),
            Rs("smelting", "Smelting", "Industry", 1, "", 90, "stone:1", "structure:foundry;recipe:smelt_iron_plate;recipe:smelt_glass", "Hot enough furnaces turn ore into plate and sand into glass."),
            Rs("fabrication", "Fabrication", "Industry", 1, "", 90, "planks:0.5", "structure:fabricator;recipe:draw_copper_wire", "Wire drawing and bench assembly."),
            Rs("deep_drilling", "Deep Drilling", "Harvest", 1, "", 70, "stone:1", "structure:driller;tool:drill:2", "Bores deep enough to reach ore under the crust."),
            Rs("bio_cultivation", "Bio-Cultivation", "Harvest", 1, "", 70, "planks:0.5", "structure:biodome", "Tended domes harvest the fast growth of green worlds."),
            Rs("jump_drive_1", "Jump Drive I", "Navigation", 1, "fabrication", 100, "copper_wire:1", "upgrade:jump:1", "The first jump coils: leave the home system."),

            // ---- Tier 2 (lab 2)
            Rs("steelmaking", "Steelmaking", "Industry", 2, "smelting", 500, "planks:2", "recipe:smelt_steel", "Burn timber into carbon and fold it into iron."),
            Rs("composites", "Composites", "Industry", 2, "fabrication bio_cultivation", 550, "biomass:1.5", "recipe:press_biocomposite", "Press plant fibre and resin into a poison-proof skin."),
            Rs("circuitry", "Circuitry", "Industry", 2, "fabrication", 550, "silica:1", "recipe:etch_circuit_board", "Etched copper traces on a glass-fibre sheet."),
            Rs("titanium_working", "Titanium Working", "Industry", 2, "smelting", 700, "titanium_ore:0.8", "recipe:smelt_titanium_plate", "A furnace hot enough for the stubborn grey ore."),
            Rs("gas_extraction", "Gas Extraction", "Harvest", 2, "deep_drilling", 650, "copper_wire:0.5", "structure:siphon_rig;tool:siphon:2", "Pumps and turbines for the storm bands."),
            Rs("geo_survey", "Geo-Survey", "Harvest", 2, "deep_drilling", 650, "silica:0.8", "structure:excavator;tool:scanner:2", "Sonar and trowel work for buried deposits."),
            Rs("salvage_ops", "Salvage Operations", "Harvest", 2, "fabrication", 550, "scrap:1", "structure:salvage_yard", "A yard to sort the wreckage of the galaxy."),
            Rs("fuel_logistics", "Fuel Logistics", "Logistics", 2, "smelting", 550, "fuel:1", "structure:fuel_depot", "Dense racks for fuel cells."),
            Rs("cargo_logistics", "Cargo Logistics", "Logistics", 2, "steelmaking", 650, "planks:1.5", "structure:cargo_depot", "One pool for every warehouse on a world."),
            Rs("ballistics", "Ballistics", "Defense", 2, "steelmaking", 600, "crystal:0.8", "structure:laser", "Focused beams for long range."),
            Rs("shield_tech", "Shield Tech", "Defense", 2, "circuitry", 600, "crystal:0.8", "structure:shield_pylon", "Fields that blunt incoming fire."),
            Rs("hazmat_toxic", "Hazmat: Toxic", "Navigation", 2, "composites", 800, "biocomposite:0.5", "upgrade:hull_toxic:1", "Seal the hull against toxic air."),
            Rs("jump_drive_2", "Jump Drive II", "Navigation", 2, "jump_drive_1", 500, "copper_wire:1", "upgrade:jump:2;upgrade:jump:3", "More stable coils: a longer first leap."),
            Rs("field_engineering", "Field Engineering", "Logistics", 2, "steelmaking", 600, "planks:1.5", "feature:builder_2", "A second crew works your sites."),
            Rs("long_range_scanning_1", "Long-Range Scanning I", "Navigation", 2, "survey_basics", 450, "glass:1", "upgrade:scanner_range:2;upgrade:scanner_range:3", "Wider survey sweeps."),
            Rs("toolsmithing_1", "Toolsmithing I", "Harvest", 2, "steelmaking", 650, "steel:0.5", "tool:*:4", "Steel heads on every handle."),

            // ---- Tier 3 (lab 3)
            Rs("chemistry", "Chemistry", "Industry", 3, "composites circuitry gas_extraction", 3500, "methane:1", "structure:chem_reactor;recipe:crystal_cell", "Reactors for resins, cryogels and charged gas."),
            Rs("thermal_metallurgy", "Thermal Metallurgy", "Industry", 3, "titanium_working steelmaking", 3500, "titanium_plate:1", "recipe:forge_thermal_alloy", "Alloys that glow without failing."),
            Rs("cryo_chemistry", "Cryo-Chemistry", "Industry", 3, "chemistry geo_survey", 3500, "ice:1", "recipe:mix_cryo_composite", "Foam-cored laminates for deep cold."),
            Rs("robotics", "Robotics", "Industry", 3, "circuitry steelmaking", 3200, "copper_wire:1", "recipe:assemble_servo_motor", "Sealed motors with precision gearheads."),
            Rs("optics", "Optics", "Industry", 3, "circuitry", 3200, "glass:1.5", "recipe:grind_optic_lens", "Lenses ground flawless enough for lasers."),
            Rs("hazmat_heat", "Hazmat: Heat", "Navigation", 3, "thermal_metallurgy", 4000, "thermal_alloy:0.5", "upgrade:hull_heat:1", "Plating for worlds where rock flows."),
            Rs("hazmat_cold", "Hazmat: Cold", "Navigation", 3, "cryo_chemistry", 4000, "cryo_composite:0.5", "upgrade:hull_cold:1", "Plating for worlds where air freezes."),
            Rs("missile_tech", "Missile Tech", "Defense", 3, "robotics ballistics", 3600, "thermal_alloy:0.5", "structure:missile_silo", "Splash missiles for the thick of a wave."),
            Rs("tesla_tech", "Tesla Tech", "Defense", 3, "robotics", 3600, "copper_wire:1", "structure:tesla_coil", "Chain lightning between aliens."),
            Rs("field_repair", "Field Repair", "Defense", 3, "robotics", 3400, "servo_motor:0.4", "structure:repair_bay", "Repair drones for the reactor."),
            Rs("colony_charter", "Colony Charter", "Logistics", 3, "field_engineering cargo_logistics", 4500, "biocomposite:0.8", "structure:colony_hub;feature:builder_3", "A hub that runs the settlement."),
            Rs("jump_drive_3", "Jump Drive III", "Navigation", 3, "jump_drive_2", 3000, "circuit_board:1", "upgrade:jump:4;upgrade:jump:5", "Folded lattices for deep hops."),
            Rs("nav_autopilot", "Autopilot Navigation", "Navigation", 3, "jump_drive_2 circuitry", 2800, "circuit_board:1", "feature:autopilot", "Skip the flight at a fuel premium."),
            Rs("toolsmithing_2", "Toolsmithing II", "Harvest", 3, "toolsmithing_1 thermal_metallurgy", 3500, "crystal:1", "tool:*:5", "Crystal-edged tools."),
            Rs("long_range_scanning_2", "Long-Range Scanning II", "Navigation", 3, "long_range_scanning_1 optics", 3000, "optic_lens:0.8", "upgrade:scanner_range:4;upgrade:scanner_range:5", "Parallax surveys of whole sectors."),

            // ---- Tier 4 (lab 4)
            Rs("plasma_physics", "Plasma Physics", "Industry", 4, "chemistry optics", 20000, "methane:1", "recipe:charge_plasma_cell", "Bottle a star in a flask."),
            Rs("radiation_shielding", "Radiation Shielding", "Industry", 4, "thermal_metallurgy chemistry", 22000, "radium:0.3", "recipe:forge_rad_shield", "Layered alloys that drink hard radiation."),
            Rs("quantum_computing", "Quantum Computing", "Industry", 4, "optics robotics hazmat_toxic", 25000, "helium3:1", "structure:quantum_lab;recipe:etch_quantum_chip", "Machines that think in more than one state."),
            Rs("hazmat_rad", "Hazmat: Radiation", "Navigation", 4, "radiation_shielding", 26000, "rad_shield_alloy:0.5", "upgrade:hull_rad:1", "Plating for worlds that glow in the dark."),
            Rs("orbital_logistics", "Orbital Logistics", "Logistics", 4, "quantum_computing cargo_logistics", 24000, "quantum_chip:0.3", "structure:orbital_relay", "Withdraw from any warehouse the beam can see."),
            Rs("automation", "Automation", "Logistics", 4, "robotics colony_charter", 22000, "servo_motor:0.5", "structure:auto_harvester", "Machines that gather bulk goods while you sleep."),
            Rs("hangar_ops", "Hangar Operations", "Logistics", 4, "colony_charter plasma_physics", 22000, "titanium_plate:0.8", "structure:hangar", "Ground crews that shave fuel off every launch."),
            Rs("toolsmithing_3", "Toolsmithing III", "Harvest", 4, "toolsmithing_2 plasma_physics", 20000, "plasma:0.5", "tool:*:6", "Plasma-edged tools."),
            Rs("jump_drive_4", "Jump Drive IV", "Navigation", 4, "jump_drive_3 quantum_computing", 18000, "quantum_chip:0.4", "upgrade:jump:6;upgrade:jump:7", "Anchors in the rift: long, safe jumps."),
            Rs("compression_1", "Cargo Compression I", "Logistics", 4, "automation", 18000, "titanium_plate:0.8", "upgrade:compression:1", "Vacuum-packed stacks."),

            // ---- Tier 5 (lab 5)
            Rs("terraforming", "Terraforming", "Megaprojects", 5, "plasma_physics quantum_computing colony_charter", 100000, "biocomposite:1", "structure:terraformer", "Reshape a world's climate in five stages."),
            Rs("long_range_scanning_3", "Long-Range Scanning III", "Navigation", 5, "long_range_scanning_2 quantum_computing", 80000, "quantum_chip:0.5", "upgrade:scanner_range:6;upgrade:scanner_range:7;upgrade:scanner_range:8", "A survey net that spans galaxies."),
            Rs("compression_2", "Cargo Compression II", "Logistics", 5, "compression_1 orbital_logistics", 90000, "servo_motor:0.5", "upgrade:compression:2", "Dense racking."),

            // ---- Tier 6 (lab 6)
            Rs("void_forging", "Void Forging", "Industry", 6, "radiation_shielding quantum_computing", 300000, "dark_matter:0.5", "recipe:grow_void_alloy", "Grow metal around a seed of nothing."),
            Rs("warp_theory", "Warp Theory", "Industry", 6, "quantum_computing hazmat_rad", 320000, "dark_matter:0.5", "recipe:wind_warp_coil", "Fold space inside a coil."),
            Rs("antimatter_synthesis", "Antimatter Synthesis", "Industry", 6, "plasma_physics quantum_computing", 240000, "helium3:0.8", "recipe:synthesize_antimatter", "Mirror-bottled energy: sixty-four cells in a pod."),
            Rs("hazmat_void", "Hazmat: Void", "Navigation", 6, "void_forging", 350000, "void_alloy:0.5", "upgrade:hull_void:1", "Plating for the places between places."),
            Rs("warp_gate_engineering", "Warp Gate Engineering", "Megaprojects", 6, "warp_theory terraforming", 420000, "warp_coil:0.4", "structure:warp_gate", "A ring in orbit that makes distance negotiable."),
            Rs("jump_drive_5", "Jump Drive V", "Navigation", 6, "jump_drive_4 warp_theory", 220000, "warp_coil:0.3", "upgrade:jump:8;upgrade:jump:9;upgrade:jump:10", "Tunnel drives and gate harmonics."),
            Rs("toolsmithing_4", "Toolsmithing IV", "Harvest", 6, "toolsmithing_3 void_forging", 250000, "void_alloy:0.5", "tool:*:7", "Singularity-edged tools."),

            // ---- Tier 7 (lab 7)
            Rs("stellar_engineering", "Stellar Engineering", "Megaprojects", 7, "warp_gate_engineering antimatter_synthesis", 1500000, "antimatter:0.5", "structure:dyson_collector", "Harness a star."),
            Rs("compression_3", "Cargo Compression III", "Logistics", 7, "compression_2 warp_theory", 700000, "warp_coil:0.3", "upgrade:compression:3;upgrade:compression:4", "Quantum compression: a hold inside a speck."),
            Rs("ascension_protocol", "Ascension Protocol", "Megaprojects", 7, "stellar_engineering", 3000000, "void_alloy:0.5", "feature:ascension", "Begin again in a new seeded universe, keeping what you learned."),
        };
    }
}
