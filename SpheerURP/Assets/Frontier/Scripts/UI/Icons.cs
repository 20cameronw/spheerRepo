using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>
    /// Icon system v2. Minimal line icons authored as SVG on a 48-unit grid (2.5 unit stroke, round caps and joins, one
    /// metaphor each, white) in Resources/Frontier/Icons. Unity's built-in SVG importer turns them into VectorImage assets
    /// (antialiased arc encoding, resolution independent) and every use tints them with -unity-background-image-tint-color.
    /// Each SVG carries an invisible full-viewBox rect so the importer keeps the 48x48 grid instead of cropping to the shapes.
    /// </summary>
    public static class Icons
    {
        /// <summary>Generic glyph for anything without its own icon.</summary>
        public const string Fallback = "cube";
        const string Folder = "Frontier/Icons/";

        static readonly Dictionary<string, VectorImage> cache = new Dictionary<string, VectorImage>();

        /// <summary>Every icon the game ships, grouped for the harness contact sheet and for tooling.</summary>
        public static readonly string[][] Groups =
        {
            new[] { "research", "map", "pack", "build", "menu", "close", "plus", "minus", "up", "back", "check", "lock", "play" },
            new[] { "fuel", "star", "star-filled", "camera", "bolt", "skull", "sound", "mute", "gear", "ranks", "tutorial", "about", "alien", "shield", "tools", "base", "warp", "ship" },
            new[] { "wood", "stone", "iron", "crystal", "fuel", "core", "scrap", "alloy", "plasma", "cube",
                "biomass", "copper_ore", "silica", "ice", "titanium_ore", "methane", "helium3", "radium", "dark_matter",
                "planks", "iron_plate", "copper_wire", "glass", "titanium_plate", "steel", "biocomposite", "thermal_alloy",
                "cryo_composite", "rad_shield_alloy", "void_alloy", "circuit_board", "servo_motor", "optic_lens", "quantum_chip", "warp_coil", "antimatter" },
            new[] { "refinery", "fabricator", "alarm", "lumber", "mine", "drill", "gunner", "laser", "satellite", "warehouse",
                "foundry", "chem_reactor", "quantum_lab", "fuel_depot", "cargo_depot", "biodome", "siphon_rig", "excavator",
                "salvage_yard", "shield_pylon", "missile_silo", "tesla_coil", "repair_bay", "research_lab", "radar_tower",
                "colony_hub", "hangar", "auto_harvester", "terraformer", "warp_gate", "dyson_collector" }
        };

        static VectorImage Load(string name)
        {
            if (string.IsNullOrEmpty(name)) name = Fallback;
            VectorImage image;
            if (cache.TryGetValue(name, out image)) return image;
            image = Resources.Load<VectorImage>(Folder + name);
            if (image == null && name != Fallback) image = Load(Fallback);
            cache[name] = image;
            return image;
        }

        public static bool Has(string name)
        {
            return !string.IsNullOrEmpty(name) && Resources.Load<VectorImage>(Folder + name) != null;
        }

        // ------------------------------------------------------------ names
        static readonly string[] ResourceKinds = { "wood", "stone", "iron", "crystal", "fuel", "alloy", "plasma", "scrap", "core" };

        static readonly string[][] ResourceWords =
        {
            new[] { "wood", "log", "timber", "plank", "lumber", "bark", "resin", "sap" },
            new[] { "stone", "rock", "granite", "basalt", "clay", "sand", "gravel", "mineral", "obsidian" },
            new[] { "iron", "ore", "metal", "copper", "nickel", "titan", "tin", "lead" },
            new[] { "crystal", "gem", "prism", "shard", "quartz", "diamond", "jewel" },
            new[] { "fuel", "gas", "oil", "fluid", "liquid", "water", "ice", "helium", "methane" },
            new[] { "alloy", "ingot", "plate", "steel", "bronze", "composite" },
            new[] { "plasma", "energy", "charge", "volt", "battery", "antimatter", "ion" },
            new[] { "scrap", "junk", "debris", "salvage", "bolt", "cog", "gear", "wreck" },
            new[] { "core", "xeno", "alien", "relic", "artifact", "organ", "spore", "egg" }
        };

        /// <summary>Exact id first, then a keyword-based category (so future resource ids get a sensible glyph), then the generic cube.</summary>
        public static string ForResource(ResourceSO r)
        {
            if (r == null) return Fallback;
            if (!string.IsNullOrEmpty(r.icon) && Has(r.icon)) return r.icon;
            return ForResourceId(r.id);
        }

        public static string ForResourceId(string id)
        {
            id = (id ?? "").Trim().ToLowerInvariant();
            if (id.Length == 0) return Fallback;
            foreach (var name in Groups[2])
                if (id == name) return name;
            for (int i = 0; i < ResourceKinds.Length; i++)
                if (id == ResourceKinds[i]) return ResourceKinds[i];
            for (int i = 0; i < ResourceWords.Length; i++)
                foreach (var word in ResourceWords[i])
                    if (id.Contains(word)) return ResourceKinds[i];
            return Fallback;
        }

        public static string ForStructure(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Refinery: return "refinery";
                case StructureKind.Fabricator: return "fabricator";
                case StructureKind.Alarm: return "alarm";
                case StructureKind.Lumber: return "lumber";
                case StructureKind.Mine: return "mine";
                case StructureKind.Drill: return "drill";
                case StructureKind.Gunner: return "gunner";
                case StructureKind.Laser: return "laser";
                case StructureKind.Satellite: return "satellite";
                case StructureKind.Warehouse: return "warehouse";
                case StructureKind.Foundry: return "foundry";
                case StructureKind.ChemReactor: return "chem_reactor";
                case StructureKind.QuantumLab: return "quantum_lab";
                case StructureKind.FuelDepot: return "fuel_depot";
                case StructureKind.CargoDepot: return "cargo_depot";
                case StructureKind.Biodome: return "biodome";
                case StructureKind.SiphonRig: return "siphon_rig";
                case StructureKind.Excavator: return "excavator";
                case StructureKind.SalvageYard: return "salvage_yard";
                case StructureKind.ShieldPylon: return "shield_pylon";
                case StructureKind.MissileSilo: return "missile_silo";
                case StructureKind.TeslaCoil: return "tesla_coil";
                case StructureKind.RepairBay: return "repair_bay";
                case StructureKind.ResearchLab: return "research_lab";
                case StructureKind.RadarTower: return "radar_tower";
                case StructureKind.ColonyHub: return "colony_hub";
                case StructureKind.Hangar: return "hangar";
                case StructureKind.AutoHarvester: return "auto_harvester";
                case StructureKind.Terraformer: return "terraformer";
                case StructureKind.WarpGate: return "warp_gate";
                case StructureKind.DysonCollector: return "dyson_collector";
                default: return Fallback;
            }
        }

        public static string ForTool(MinigameKind kind)
        {
            return kind == MinigameKind.Chop ? "lumber" : kind == MinigameKind.Mine ? "mine" : "drill";
        }

        // ------------------------------------------------------------ elements
        /// <summary>A tinted icon element. Size 0 leaves sizing to the stylesheet (.fr-icon).</summary>
        public static VisualElement Make(string name, Color color, float size = 0f)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList("fr-icon");
            if (size > 0f) { e.style.width = size; e.style.height = size; e.style.flexShrink = 0; }
            Apply(e, name, color);
            return e;
        }

        public static void Apply(VisualElement e, string name, Color color)
        {
            var image = Load(name);
            e.style.backgroundImage = image != null ? new StyleBackground(image) : new StyleBackground(StyleKeyword.None);
            e.style.unityBackgroundImageTintColor = UiKit.Ui(color);
        }

        /// <summary>A square tonal icon button with a thin accent outline (glow and fill only while pressed).</summary>
        public static Button IconButton(string icon, Color color, System.Action onClick, float size = 0f, bool round = false)
        {
            var b = new Button();
            b.AddToClassList("fr-iconbtn");
            if (round) b.AddToClassList("fr-round");
            UiKit.SetAccent(b, color);
            if (size > 0f) { b.style.width = size; b.style.height = size; b.style.minHeight = size; }
            var ic = Make(icon, color);
            ic.AddToClassList("fr-iconbtn-icon");
            b.Add(ic);
            b.clicked += () => { Sfx.Click(); if (onClick != null) onClick(); };
            return b;
        }
    }
}
