using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    public static partial class CatalogBuilder
    {
        sealed class ResSpec
        {
            public string id, name, color, accent, icon, desc;
            public ResourceCategory cat;
            public int tier, stack, fuel;
            public HarvestMethod[] methods;
            public double value, baseYield, perTier, diff;
            public Hazards haz;

            public ResSpec Y(double baseYield, double perTier, double diff)
            {
                this.baseYield = baseYield;
                this.perTier = perTier;
                this.diff = diff;
                return this;
            }

            public ResSpec Fuel(int energy)
            {
                fuel = energy;
                return this;
            }
        }

        // Method letters: C chop, M mine, D drill, S siphon, X excavate, V salvage, H harvest. Empty = crafted (no harvest method).
        static HarvestMethod[] Methods(string letters)
        {
            if (letters.Length == 0) return null;
            var m = new HarvestMethod[letters.Length];
            for (int i = 0; i < m.Length; i++)
            {
                switch (letters[i])
                {
                    case 'C': m[i] = HarvestMethod.Chop; break;
                    case 'M': m[i] = HarvestMethod.Mine; break;
                    case 'D': m[i] = HarvestMethod.Drill; break;
                    case 'S': m[i] = HarvestMethod.Siphon; break;
                    case 'X': m[i] = HarvestMethod.Excavate; break;
                    case 'V': m[i] = HarvestMethod.Salvage; break;
                    case 'H': m[i] = HarvestMethod.Harvest; break;
                    default: throw new ArgumentException("method letter " + letters[i]);
                }
            }
            return m;
        }

        static ResSpec R(string id, string name, ResourceCategory cat, int tier, string methods, double value, int stack, Hazards haz,
            string color, string accent, string icon, string desc)
        {
            return new ResSpec { id = id, name = name, cat = cat, tier = tier, methods = Methods(methods), value = value, stack = stack, haz = haz, color = color, accent = accent, icon = icon, desc = desc };
        }

        const ResourceCategory Raw = ResourceCategory.Raw, Pro = ResourceCategory.Processed, Alo = ResourceCategory.Alloy, Com = ResourceCategory.Component, Ene = ResourceCategory.Energy;
        const Hazards HNone = Hazards.None;

        /// <summary>The locked resource list (34, five categories). Raw values are anchors; crafted values are derived from recipes.</summary>
        static readonly ResSpec[] ResourceSpecs =
        {
            // ---------------------------------------------------------------- Raw (15)
            R("wood", "Wood", Raw, 0, "C", 1.0, 20, HNone, "#E0A23A", "#7A4A18",
                "A stout log lying on a diagonal with one round cut face showing three growth rings.",
                "Timber from the lush worlds, cut green and stacked by the cartload. Every settlement begins with it, and every great project comes back to it.").Y(10, 0.0, -0.02),
            R("stone", "Stone", Raw, 0, "M", 1.0, 20, HNone, "#9DB4CC", "#5B6E85",
                "A rounded boulder with three visible faceted planes and a flat base.",
                "Dense grey rock that rings when struck. It holds the foundations of everything and never runs out.").Y(8, 0.0, -0.05),
            R("scrap", "Scrap", Raw, 0, "VM", 1.0, 30, HNone, "#AEB4BE", "#E58A3A",
                "A jagged torn metal plate with a single round bolt hole punched through it.",
                "Wreckage and worn-out machinery lie on every world, because someone always got there first. Pressed, burned or melted, none of it is ever wasted.").Y(10, 0.0, -0.08),
            R("biomass", "Biomass", Raw, 1, "H", 2.0, 20, Hazards.Toxic, "#7FCB4F", "#2F6B2A",
                "A young sprout: two broad leaves flaring from a short curved stem.",
                "Fast-growing fibrous growth, thick with sap and spores. It ferments into clean fuel and weaves into seals that shrug off poison.").Y(9, 0.5, 0.0),
            R("iron", "Iron Ore", Raw, 1, "MD", 2.5, 30, Hazards.Heat, "#D9553A", "#3A2A26",
                "An angular trapezoid nugget crossed by one bright diagonal vein.",
                "Rust-red ore heavy with metal, found in veins under volcanic crust and dry plains. Smelted, it becomes the skeleton of everything you build.").Y(7, 1.0, 0.0),
            R("copper_ore", "Copper Ore", Raw, 1, "M", 3.0, 30, Hazards.Heat, "#B8733F", "#4FB0A0",
                "A cluster of three round bubbly lobes, like a bunch of grapes, flecked with teal.",
                "Green-streaked ore that conducts better than anything else you can dig up. Drawn into wire, it carries every signal and every current.").Y(6, 0.8, 0.0),
            R("silica", "Silica", Raw, 1, "MX", 2.0, 30, Hazards.Heat, "#EBDDB0", "#B09050",
                "A conical heap of sand with a rippled crest and a few loose grains around the base.",
                "Fine quartz sand from the dry basins and crystal flats. Melted it turns to glass, and ground it polishes lenses.").Y(8, 0.6, -0.02),
            R("ice", "Ice", Raw, 2, "X", 4.0, 24, Hazards.Cold, "#9FE3F5", "#2E84A8",
                "A tilted rounded cube with a chipped corner and a curved glint across one face.",
                "Hard blue ice cut from glaciers, comet shells and shadowed craters. It holds the cold that cryogenic hulls and quantum lattices depend on.").Y(8, 0.8, 0.0),
            R("titanium_ore", "Titanium Ore", Raw, 2, "DM", 8.0, 40, Hazards.Heat, "#8795A8", "#E0E6EE",
                "A squat six-sided nugget with a ridged band across its middle and two bright specks.",
                "Grey ore so dense it blunts ordinary drills. Refined, it turns light, strong and unbothered by fire or vacuum.").Y(5, 0.7, 0.08),
            R("crystal", "Crystal", Raw, 2, "MD", 8.0, 48, HNone, "#4DE3E8", "#1A6E86",
                "A tall faceted spear of crystal flanked by two shorter shards.",
                "Resonant crystal that hums when struck and glows when worked. It sharpens tools, focuses beams and stores more energy than it should.").Y(5, 0.8, 0.05),
            R("methane", "Methane", Raw, 2, "S", 8.0, 48, Hazards.Toxic, "#7CE0C3", "#1F7F6A",
                "A ball-and-stick molecule: one large dot joined by four spokes to four small dots.",
                "Pale gas siphoned from swamps, oceans and giant worlds. A little of it burns hotter than a hold full of wood.").Y(8, 0.8, 0.05),
            R("helium3", "Helium-3", Raw, 3, "S", 22.0, 64, Hazards.Toxic, "#C8B8FF", "#6C55D6",
                "A round balloon with a small knot at the bottom and a curling string.",
                "A rare light gas drifting in the storm bands of the giants. Ships crave it, and so does every quantum machine.").Y(4, 0.6, 0.10),
            R("core", "Xeno Core", Raw, 3, "D", 45.0, 32, Hazards.Radiation, "#E05CC8", "#6B1F5E",
                "An almond-shaped eye with a vertical slit pupil, ringed by short thorns.",
                "A pulsing organic knot torn from alien hives and fallen raiders. Nobody agrees what it is, but every advanced machine runs better with one inside.").Y(2, 0.3, 0.10),
            R("radium", "Radium", Raw, 4, "X", 55.0, 64, Hazards.Radiation, "#F2F23A", "#5E6A12",
                "A round hazard disc with three fan blades around a centre dot (the trefoil).",
                "A faintly glowing salt that ticks in the dark. Dangerous to carry, essential for shielding, and priceless to anyone who knows how to contain it.").Y(3, 0.4, 0.15),
            R("dark_matter", "Dark Matter", Raw, 6, "X", 130.0, 99, Hazards.Void, "#6B3FD6", "#0B0618",
                "A solid dark disc crossed by one thin offset orbit ellipse, like an eclipsed planet.",
                "A pocket of nothing that pulls on everything. Dug from the deepest shattered worlds, it is the last ingredient of the last machines.").Y(2, 0.25, 0.20),

            // ---------------------------------------------------------------- Processed (6)
            R("fuel", "Fuel Cell", Pro, 0, "", 0, 24, HNone, "#7BE05A", "#2A7A22",
                "A squat cell with a small nub on top and a lightning bolt cut out of its body.",
                "Compact cells of refined energy that power every jump. Cooked from whatever burns: wood in the first days, gas and ice when the ship is bigger.").Fuel(1),
            R("planks", "Planks", Pro, 0, "", 0, 20, HNone, "#E8C48A", "#8A5A2A",
                "A stack of three flat boards, each offset a little, with a nail dot at the end.",
                "Sawn timber cut to length. It frames the buildings of every era, and the late projects eat it by the thousand."),
            R("iron_plate", "Iron Plate", Pro, 1, "", 0, 30, HNone, "#B7BDC6", "#D9553A",
                "A thick flat slab drawn as a parallelogram with a round rivet in each corner.",
                "Rolled sheets of smelted iron, still warm from the foundry. The first real building material of any colony."),
            R("copper_wire", "Copper Wire", Pro, 1, "", 0, 40, HNone, "#E0925A", "#7A3E1A",
                "A round spool wound with turns of wire, one loose end trailing off to the right.",
                "Copper drawn thin and wound on spools. Everything with a pulse, from a lamp to a reactor, needs a length of it."),
            R("glass", "Glass", Pro, 1, "", 0, 24, HNone, "#BFEFF2", "#FFFFFF",
                "A tall upright window pane with a plus-shaped cross frame and one diagonal glint.",
                "Clear, hard panes melted from silica. They keep out the weather, hold the vacuum and become lenses when polished."),
            R("titanium_plate", "Titanium Plate", Pro, 2, "", 0, 40, HNone, "#A9B7C9", "#3A4A63",
                "A rectangle with chamfered corners and one raised X-shaped brace stamped across it.",
                "Pressed titanium cut into light, stubborn armor panels. It survives heat that melts steel and cold that shatters it."),

            // ---------------------------------------------------------------- Alloys (6)
            R("steel", "Steel", Alo, 2, "", 0, 48, HNone, "#9FC4E8", "#3E5F85",
                "An I-beam seen end-on: two wide flanges joined by a thin web.",
                "Iron hardened with the carbon of burnt timber. The backbone of tools, turrets and hulls."),
            R("biocomposite", "Biocomposite", Alo, 2, "", 0, 32, Hazards.Toxic, "#6BC98A", "#1F5A3A",
                "A pointed leaf-shaped plate with a centre rib and a faint cross-hatch weave.",
                "Plant fibre pressed with resin into a flexible, self-sealing skin. Toxic air eats steel but slides off this."),
            R("thermal_alloy", "Thermal Alloy", Alo, 3, "", 0, 48, Hazards.Heat, "#FF8A4A", "#7A2A10",
                "A trapezoid ingot with three wavy heat-shimmer lines rising above it.",
                "Titanium and glass fused into a ceramic-bright metal that glows without failing. Hulls plated with it can land where rock still flows."),
            R("cryo_composite", "Cryo Composite", Alo, 3, "", 0, 48, Hazards.Cold, "#7FD4FF", "#1F5F8A",
                "A hexagonal tile with six small triangular frost notches and a dot at its centre.",
                "A foam-cored laminate that keeps its strength far below freezing. It lets a ship set down where the air itself is ice."),
            R("rad_shield_alloy", "Rad-Shield Alloy", Alo, 4, "", 0, 64, Hazards.Radiation, "#F0D040", "#3A3A10",
                "A heater-shield outline cut into three horizontal bands, like layers of lead.",
                "Layered steel and radium-laced glass that drinks hard radiation. Shielding like this opens the worlds that glow in the dark."),
            R("void_alloy", "Void Alloy", Alo, 6, "", 0, 64, Hazards.Void, "#9A6BFF", "#1A1030",
                "A dark diamond with a bright dot at its centre and a thin offset outline around it.",
                "A black metal grown around a seed of dark matter. It bends light and survives where space itself is thin."),

            // ---------------------------------------------------------------- Components (5)
            R("circuit_board", "Circuit Board", Com, 2, "", 0, 40, HNone, "#4FC98A", "#C8F0DA",
                "A rectangle with three L-shaped traces ending in round pads and one square chip.",
                "Etched copper on a glass-fibre sheet. Every control, sensor and drone starts with one."),
            R("servo_motor", "Servo Motor", Com, 3, "", 0, 24, HNone, "#B28CF5", "#4A2E8A",
                "A squat cylinder carrying a short shaft topped with a six-tooth cog.",
                "A sealed motor with a precision gearhead. It moves every arm, drill carriage and turret in the colony."),
            R("optic_lens", "Optic Lens", Com, 3, "", 0, 32, HNone, "#8FE3FF", "#FFFFFF",
                "A bi-convex lens shape with a bright crescent glint inside a thin mounting ring.",
                "A ground and polished lens, flawless enough to focus a laser or read a star. Scanners and turrets are blind without it."),
            R("quantum_chip", "Quantum Chip", Com, 5, "", 0, 64, HNone, "#C79CFF", "#33E0FF",
                "A small square chip with pin rows on all four sides and a glowing diamond die in the middle.",
                "A lattice of cooled helium and etched crystal that computes in more than one state at once. Navigation and research run on it."),
            R("warp_coil", "Warp Coil", Com, 6, "", 0, 16, HNone, "#E0A8FF", "#FFFFFF",
                "A donut-shaped ring wound from five loops of coil with a bright spark at its centre.",
                "A superconducting coil wound around a speck of dark matter. Charge it and space folds; it is the last part of every great ship."),

            // ---------------------------------------------------------------- Energy (2)
            R("plasma", "Plasma Cell", Ene, 4, "", 0, 48, HNone, "#D45BFF", "#FFD0FF",
                "A round-bottomed flask with a flat neck and an S-shaped swirl of flame inside.",
                "A magnetic bottle of superheated gas that carries eight times the energy of a fuel cell. It lets a deep run fit in a few slots.").Fuel(8),
            R("antimatter", "Antimatter Pod", Ene, 6, "", 0, 99, Hazards.Void, "#FF5A7A", "#FFE0E8",
                "A pill-shaped capsule with a bright centre band, a zigzag seam and a pin at each end.",
                "A pod of trapped antiparticles in a mirrored field. Sixty-four cells of energy in a package you can hold, and a carelessness you cannot survive.").Fuel(64),
        };

        static readonly string[] CategoryFamilyHex = { "#D9A441", "#5CC8B0", "#8FB4E8", "#B28CF5", "#FF7A55" };

        static ResourceDef[] BuildResources(Tuning t)
        {
            var list = new ResourceDef[ResourceSpecs.Length];
            for (int i = 0; i < list.Length; i++)
            {
                ResSpec s = ResourceSpecs[i];
                var d = new ResourceDef
                {
                    id = s.id,
                    displayName = s.name,
                    category = s.cat,
                    tier = s.tier,
                    harvestMethods = s.methods,
                    baseValue = s.cat == ResourceCategory.Raw ? (float)t.D("value.raw." + s.id) : 0f,
                    stackSize = s.stack,
                    hazardAffinity = s.haz,
                    fuelEnergy = s.fuel,
                    atlasDescription = s.desc,
                    iconConcept = s.icon,
                    colorHex = s.color,
                    accentHex = s.accent,
                    baseYield = s.cat == ResourceCategory.Raw ? CostMath.F(t.D("yield.base." + s.id), 3) : 0f,
                    yieldPerTier = s.cat == ResourceCategory.Raw ? CostMath.F(t.D("yield.per_tier." + s.id), 3) : 0f,
                    difficultyOffset = s.cat == ResourceCategory.Raw ? CostMath.F(s.diff, 3) : 0f
                };
                list[i] = d;
            }
            return list;
        }

        static void RegisterResourceKeys(Tuning t)
        {
            for (int i = 0; i < ResourceSpecs.Length; i++)
            {
                ResSpec s = ResourceSpecs[i];
                if (s.cat != ResourceCategory.Raw) continue;
                t.Register("value", "value.raw." + s.id, s.value, 0.1, 100000, "Anchor price of " + s.name + " in scrap-equivalents (crafted prices derive from recipes).");
                t.Register("yield", "yield.base." + s.id, s.baseYield, 0.5, 1000, "Deposit base yield of " + s.name + " at tier 0 (before richness, tool and structure bonuses).");
                t.Register("yield", "yield.per_tier." + s.id, s.perTier, 0, 100, "Extra base yield of " + s.name + " per planet tier.");
            }
        }
    }
}
