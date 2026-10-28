using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    public static partial class CatalogBuilder
    {
        static ItemAmount[] Parse(string items)
        {
            string[] p = items.Split(' ');
            var list = new List<ItemAmount>(p.Length);
            for (int i = 0; i < p.Length; i++)
            {
                if (p[i].Length == 0) continue;
                int c = p[i].IndexOf(':');
                list.Add(new ItemAmount(p[i].Substring(0, c), int.Parse(p[i].Substring(c + 1), System.Globalization.CultureInfo.InvariantCulture)));
            }
            return list.ToArray();
        }

        static RecipeDef Rc(string id, string name, RecipeKind kind, string station, int level, int tier, string inputs, string outputs, string research = null)
        {
            return new RecipeDef { id = id, displayName = name, kind = kind, stationId = station, stationLevel = level, tier = tier, inputs = Parse(inputs), outputs = Parse(outputs), unlockResearch = research, bonus = false };
        }

        /// <summary>
        /// All recipes, instant (crafting never has a timer). Order matters: the FIRST recipe that makes an item is its primary recipe and
        /// defines its price. Ratios make mid and late goods consume large tier-0 volume (planks, steel's carbon, biocomposite).
        /// </summary>
        static RecipeDef[] BuildRecipes()
        {
            var P = RecipeKind.Processing; var A = RecipeKind.Alloy; var C = RecipeKind.Component; var E = RecipeKind.Energy;
            return new[]
            {
                // ------------------------------------------------ Refinery (tier 0 station, levels 1..3 for recipes)
                Rc("distill_wood", "Wood Distillate", P, "refinery", 1, 0, "wood:8", "fuel:3"),
                Rc("burn_scrap", "Scrap Burner", P, "refinery", 1, 0, "scrap:10", "fuel:2"),
                Rc("saw_planks", "Saw Planks", P, "refinery", 1, 0, "wood:2", "planks:1"),
                Rc("press_scrap", "Scrap Press", P, "refinery", 1, 0, "scrap:8", "stone:2"),
                Rc("ferment_biomass", "Biomass Ferment", P, "refinery", 2, 1, "biomass:6", "fuel:5"),
                Rc("crystal_cell", "Crystal Cell", E, "refinery", 2, 2, "crystal:3", "fuel:8"),
                Rc("condense_methane", "Methane Condenser", E, "refinery", 3, 2, "methane:3", "fuel:9"),

                // ------------------------------------------------ Foundry (tier 1)
                Rc("smelt_iron_plate", "Smelt Iron Plate", P, "foundry", 1, 1, "iron:3 fuel:1", "iron_plate:2", "smelting"),
                Rc("smelt_glass", "Smelt Glass", P, "foundry", 1, 1, "silica:3 fuel:1", "glass:2", "smelting"),
                Rc("smelt_steel", "Smelt Steel", A, "foundry", 2, 2, "iron_plate:2 planks:5", "steel:3", "steelmaking"),
                Rc("smelt_titanium_plate", "Press Titanium Plate", P, "foundry", 3, 2, "titanium_ore:3 fuel:2", "titanium_plate:2", "titanium_working"),
                Rc("forge_thermal_alloy", "Forge Thermal Alloy", A, "foundry", 4, 3, "titanium_plate:2 glass:2 steel:1", "thermal_alloy:2", "thermal_metallurgy"),
                Rc("forge_rad_shield", "Forge Rad-Shield", A, "foundry", 5, 4, "steel:2 titanium_plate:2 glass:2 radium:1", "rad_shield_alloy:2", "radiation_shielding"),

                // ------------------------------------------------ Fabricator (tier 1)
                Rc("draw_copper_wire", "Draw Copper Wire", P, "fabricator", 1, 1, "copper_ore:2", "copper_wire:3", "fabrication"),
                Rc("press_biocomposite", "Press Biocomposite", A, "fabricator", 2, 2, "biomass:4 planks:4 fuel:1", "biocomposite:2", "composites"),
                Rc("etch_circuit_board", "Etch Circuit Board", C, "fabricator", 2, 2, "copper_wire:3 silica:2", "circuit_board:1", "circuitry"),
                Rc("assemble_servo_motor", "Assemble Servo Motor", C, "fabricator", 3, 3, "steel:2 copper_wire:3 circuit_board:1", "servo_motor:1", "robotics"),
                Rc("grind_optic_lens", "Grind Optic Lens", C, "fabricator", 3, 3, "glass:3 crystal:1", "optic_lens:2", "optics"),

                // ------------------------------------------------ Chem Reactor (tier 3)
                Rc("mix_cryo_composite", "Mix Cryo Composite", A, "chem_reactor", 1, 3, "ice:4 biocomposite:2 methane:1", "cryo_composite:2", "cryo_chemistry"),
                Rc("charge_plasma_cell", "Charge Plasma Cell", E, "chem_reactor", 2, 4, "fuel:4 crystal:2 methane:1", "plasma:1", "plasma_physics"),

                // ------------------------------------------------ Quantum Lab (tier 4)
                Rc("etch_quantum_chip", "Etch Quantum Chip", C, "quantum_lab", 1, 5, "circuit_board:2 optic_lens:2 helium3:3", "quantum_chip:1", "quantum_computing"),
                Rc("grow_void_alloy", "Grow Void Alloy", A, "quantum_lab", 2, 6, "rad_shield_alloy:2 dark_matter:1 titanium_plate:2", "void_alloy:2", "void_forging"),
                Rc("wind_warp_coil", "Wind Warp Coil", C, "quantum_lab", 3, 6, "copper_wire:6 quantum_chip:2 titanium_plate:2 dark_matter:1", "warp_coil:1", "warp_theory"),
                Rc("synthesize_antimatter", "Synthesize Antimatter", E, "quantum_lab", 4, 6, "plasma:4 dark_matter:2 helium3:2", "antimatter:1", "antimatter_synthesis"),
            };
        }
    }
}
