using System;
using System.Collections.Generic;

namespace Frontier.Universe
{
    /// <summary>
    /// Static facts about one raw resource: its default harvest method, the first tier it can appear on, yield/difficulty tuning and
    /// where the coverage guarantee may inject it. Resource tables are DATA: nothing in the generator names a resource id.
    /// </summary>
    public sealed class ResourceDef
    {
        public string Id;
        public HarvestMethod DefaultMethod = HarvestMethod.Mine;
        /// <summary>Lowest planet tier on which this resource may appear anywhere.</summary>
        public int MinTier;
        /// <summary>True for ids that are not in the game yet (see <see cref="DefaultResourceTables.AddPlanned"/>).</summary>
        public bool Planned;
        public float BaseYield = 8f;
        public float YieldPerTier = 3f;
        /// <summary>Added to the tier-based deposit difficulty (0..1 scale).</summary>
        public float DifficultyOffset;
        /// <summary>Participates in the coverage guarantee (anchoring) when true.</summary>
        public bool Anchored = true;
        /// <summary>Biomes that may host an injected anchor deposit (the union with every biome whose profile lists the resource is added automatically).</summary>
        public uint AnchorBiomes;
        /// <summary>Asteroid belts may host an injected anchor deposit.</summary>
        public bool AnchorOnBelts = true;
        /// <summary>When the block has no strict host, any solid (non gas-giant) planet of sufficient tier may carry the anchor.</summary>
        public bool AnchorFallbackSolid = true;

        public ResourceDef() { }

        public ResourceDef(string id, HarvestMethod method, int minTier, float baseYield, float yieldPerTier, float difficultyOffset, bool planned = false)
        {
            Id = id;
            DefaultMethod = method;
            MinTier = minTier;
            BaseYield = baseYield;
            YieldPerTier = yieldPerTier;
            DifficultyOffset = difficultyOffset;
            Planned = planned;
        }

        public ResourceDef Clone() { return (ResourceDef)MemberwiseClone(); }
    }

    /// <summary>One line of a biome's resource profile: what can be found, how it is harvested, how likely it is, and from which tier on.</summary>
    public struct ResourceEntry
    {
        public string ResourceId;
        public HarvestMethod Method;
        public float Weight;
        public int MinTier;

        public ResourceEntry(string resourceId, HarvestMethod method, float weight, int minTier)
        {
            ResourceId = resourceId;
            Method = method;
            Weight = weight;
            MinTier = minTier;
        }
    }

    /// <summary>The weighted list of resources a biome (or asteroid belt) can offer.</summary>
    public sealed class ResourceProfile
    {
        public readonly List<ResourceEntry> Entries = new List<ResourceEntry>();

        public ResourceProfile Add(string id, HarvestMethod method, float weight, int minTier)
        {
            Entries.Add(new ResourceEntry(id, method, weight, minTier));
            return this;
        }

        public ResourceProfile Clone()
        {
            var p = new ResourceProfile();
            p.Entries.AddRange(Entries);
            return p;
        }

        public bool Contains(string id)
        {
            for (int i = 0; i < Entries.Count; i++) if (Entries[i].ResourceId == id) return true;
            return false;
        }
    }

    /// <summary>
    /// All resource data the generator uses: definitions, one profile per biome, an asteroid-belt profile, and the ids that are always
    /// present on every world (scrap). Build it with <see cref="DefaultResourceTables"/> or by hand, hand it to
    /// <see cref="UniverseConfig.Resources"/>; the Universe clones and freezes its own copy.
    /// </summary>
    public sealed class ResourceTable
    {
        public readonly List<ResourceDef> Defs = new List<ResourceDef>();
        public readonly ResourceProfile[] Profiles = new ResourceProfile[12];
        public readonly ResourceProfile BeltProfile = new ResourceProfile();
        public readonly List<string> AlwaysPresent = new List<string>();

        Dictionary<string, int> _index;
        bool _frozen;

        public ResourceTable()
        {
            for (int i = 0; i < Profiles.Length; i++) Profiles[i] = new ResourceProfile();
        }

        public bool IsFrozen { get { return _frozen; } }

        public ResourceDef AddDef(ResourceDef def)
        {
            if (_frozen) throw new InvalidOperationException("ResourceTable is frozen");
            Defs.Add(def);
            return def;
        }

        public ResourceProfile ProfileFor(Biome biome) { return Profiles[(int)biome]; }

        public ResourceProfile ProfileFor(Biome biome, PlanetKind kind)
        {
            return kind == PlanetKind.AsteroidBelt ? BeltProfile : Profiles[(int)biome];
        }

        public ResourceDef Find(string id)
        {
            int i = IndexOf(id);
            return i < 0 ? null : Defs[i];
        }

        public bool Contains(string id) { return IndexOf(id) >= 0; }

        /// <summary>Index into <see cref="Defs"/>, or -1.</summary>
        public int IndexOf(string id)
        {
            if (id == null) return -1;
            if (_index != null)
            {
                int v;
                return _index.TryGetValue(id, out v) ? v : -1;
            }
            for (int i = 0; i < Defs.Count; i++) if (Defs[i].Id == id) return i;
            return -1;
        }

        public int MinTierOf(string id)
        {
            var d = Find(id);
            return d == null ? int.MaxValue : d.MinTier;
        }

        public ResourceTable Clone()
        {
            var t = new ResourceTable();
            for (int i = 0; i < Defs.Count; i++) t.Defs.Add(Defs[i].Clone());
            for (int i = 0; i < Profiles.Length; i++) t.Profiles[i] = Profiles[i].Clone();
            t.BeltProfile.Entries.AddRange(BeltProfile.Entries);
            t.AlwaysPresent.AddRange(AlwaysPresent);
            return t;
        }

        /// <summary>Validates the table, completes anchor-host masks from the profiles, builds lookups and makes it immutable.</summary>
        public void Freeze()
        {
            if (_frozen) return;
            var index = new Dictionary<string, int>();
            for (int i = 0; i < Defs.Count; i++)
            {
                var d = Defs[i];
                if (string.IsNullOrEmpty(d.Id)) throw new ArgumentException("Resource definition without an id");
                if (index.ContainsKey(d.Id)) throw new ArgumentException("Duplicate resource id: " + d.Id);
                index[d.Id] = i;
            }
            for (int b = 0; b < Profiles.Length; b++) CheckProfile(Profiles[b], index, "profile " + (Biome)b);
            CheckProfile(BeltProfile, index, "belt profile");
            for (int i = 0; i < AlwaysPresent.Count; i++)
                if (!index.ContainsKey(AlwaysPresent[i])) throw new ArgumentException("AlwaysPresent references unknown resource " + AlwaysPresent[i]);
            for (int b = 0; b < Profiles.Length; b++)
                for (int e = 0; e < Profiles[b].Entries.Count; e++)
                    Defs[index[Profiles[b].Entries[e].ResourceId]].AnchorBiomes |= 1u << b;
            for (int e = 0; e < BeltProfile.Entries.Count; e++)
                Defs[index[BeltProfile.Entries[e].ResourceId]].AnchorOnBelts = true;
            _index = index;
            _frozen = true;
        }

        void CheckProfile(ResourceProfile p, Dictionary<string, int> index, string what)
        {
            for (int e = 0; e < p.Entries.Count; e++)
            {
                var en = p.Entries[e];
                int di;
                if (!index.TryGetValue(en.ResourceId ?? "", out di)) throw new ArgumentException(what + " references unknown resource '" + en.ResourceId + "'");
                if (en.Weight < 0f) throw new ArgumentException(what + ": negative weight for " + en.ResourceId);
                if (en.MinTier < Defs[di].MinTier) throw new ArgumentException(what + ": entry " + en.ResourceId + " MinTier " + en.MinTier + " is below the definition's MinTier " + Defs[di].MinTier);
            }
        }

        /// <summary>The default tables. <paramref name="includePlanned"/> adds the planned resource ids (biomass ... dark_matter).</summary>
        public static ResourceTable CreateDefault(bool includePlanned)
        {
            var t = new ResourceTable();
            DefaultResourceTables.AddCurrent(t);
            if (includePlanned) DefaultResourceTables.AddPlanned(t);
            return t;
        }
    }

    /// <summary>
    /// Default resource data. <see cref="AddCurrent"/> uses ONLY ids that exist in the game today (wood, stone, scrap, iron, crystal, fuel,
    /// core). <see cref="AddPlanned"/> is the clearly separated table of planned ids, enabled by UniverseConfig.EnablePlannedResources.
    /// </summary>
    public static class DefaultResourceTables
    {
        public static void AddCurrent(ResourceTable t)
        {
            var wood = t.AddDef(new ResourceDef("wood", HarvestMethod.Chop, 0, 12f, 5f, -0.02f));
            var stone = t.AddDef(new ResourceDef("stone", HarvestMethod.Mine, 0, 8f, 3f, -0.05f));
            var scrap = t.AddDef(new ResourceDef("scrap", HarvestMethod.Salvage, 0, 10f, 3f, -0.08f));
            var iron = t.AddDef(new ResourceDef("iron", HarvestMethod.Mine, 1, 8f, 4f, 0f));
            var crystal = t.AddDef(new ResourceDef("crystal", HarvestMethod.Mine, 2, 6f, 2f, 0.05f));
            var fuel = t.AddDef(new ResourceDef("fuel", HarvestMethod.Drill, 1, 6f, 2f, 0.05f));
            var core = t.AddDef(new ResourceDef("core", HarvestMethod.Drill, 3, 2f, 1f, 0.10f));
            scrap.AnchorBiomes = BiomeMask.All;

            t.AlwaysPresent.Add("scrap");

            var M = HarvestMethod.Mine; var D = HarvestMethod.Drill; var C = HarvestMethod.Chop;
            var S = HarvestMethod.Salvage; var G = HarvestMethod.Siphon;

            t.ProfileFor(Biome.Lush)
                .Add("wood", C, 1.00f, 0).Add("stone", M, 0.55f, 0).Add("fuel", D, 0.30f, 1).Add("crystal", M, 0.12f, 2).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Mineral)
                .Add("stone", M, 1.00f, 0).Add("iron", M, 0.90f, 1).Add("wood", C, 0.20f, 0).Add("fuel", D, 0.35f, 1).Add("crystal", M, 0.15f, 2).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Glacial)
                .Add("fuel", D, 1.00f, 1).Add("crystal", M, 0.75f, 2).Add("wood", C, 0.30f, 0).Add("stone", M, 0.45f, 0).Add("core", D, 0.12f, 5).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Volcanic)
                .Add("iron", M, 1.00f, 1).Add("fuel", D, 0.80f, 1).Add("stone", M, 0.40f, 0).Add("crystal", D, 0.25f, 2).Add("core", D, 0.30f, 3).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Fungal)
                .Add("wood", C, 1.00f, 0).Add("fuel", D, 0.60f, 1).Add("core", D, 0.40f, 3).Add("stone", M, 0.20f, 0).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Crystal)
                .Add("crystal", M, 1.00f, 2).Add("fuel", D, 0.60f, 1).Add("iron", M, 0.50f, 1).Add("stone", M, 0.40f, 0).Add("core", D, 0.15f, 4).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Obsidian)
                .Add("iron", M, 1.00f, 1).Add("crystal", M, 0.60f, 2).Add("core", D, 0.55f, 3).Add("stone", M, 0.30f, 0).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Shattered)
                .Add("crystal", M, 1.00f, 2).Add("core", D, 0.80f, 3).Add("fuel", D, 0.70f, 1).Add("iron", M, 0.25f, 1).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Desert)
                .Add("stone", M, 1.00f, 0).Add("iron", M, 0.60f, 1).Add("fuel", D, 0.30f, 1).Add("crystal", M, 0.10f, 2).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Oceanic)
                .Add("wood", C, 0.80f, 0).Add("fuel", D, 0.70f, 1).Add("crystal", M, 0.30f, 2).Add("stone", M, 0.20f, 0).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.GasGiant)
                .Add("fuel", G, 1.00f, 1).Add("crystal", M, 0.30f, 2).Add("scrap", S, 0.50f, 0);
            t.ProfileFor(Biome.Barren)
                .Add("stone", M, 1.00f, 0).Add("iron", M, 0.60f, 1).Add("fuel", D, 0.20f, 1).Add("crystal", M, 0.15f, 2).Add("scrap", S, 0.50f, 0);
            t.BeltProfile
                .Add("scrap", S, 1.00f, 0).Add("stone", M, 0.80f, 0).Add("iron", M, 0.80f, 1).Add("crystal", M, 0.20f, 2);
        }

        /// <summary>The planned resource ids with their minimum tiers: biomass 1, copper_ore 1, silica 1, ice 2, titanium_ore 2, methane 2, helium3 3, radium 4, dark_matter 6.</summary>
        public static void AddPlanned(ResourceTable t)
        {
            t.AddDef(new ResourceDef("biomass", HarvestMethod.Harvest, 1, 10f, 4f, 0f, true));
            t.AddDef(new ResourceDef("copper_ore", HarvestMethod.Mine, 1, 8f, 3f, 0f, true));
            t.AddDef(new ResourceDef("silica", HarvestMethod.Excavate, 1, 10f, 3f, -0.02f, true));
            t.AddDef(new ResourceDef("ice", HarvestMethod.Excavate, 2, 10f, 3f, 0f, true));
            t.AddDef(new ResourceDef("titanium_ore", HarvestMethod.Mine, 2, 5f, 2f, 0.08f, true));
            t.AddDef(new ResourceDef("methane", HarvestMethod.Siphon, 2, 8f, 2f, 0.05f, true));
            t.AddDef(new ResourceDef("helium3", HarvestMethod.Siphon, 3, 5f, 2f, 0.10f, true));
            t.AddDef(new ResourceDef("radium", HarvestMethod.Excavate, 4, 2f, 1f, 0.15f, true));
            t.AddDef(new ResourceDef("dark_matter", HarvestMethod.Excavate, 6, 1f, 1f, 0.20f, true));

            var M = HarvestMethod.Mine; var X = HarvestMethod.Excavate; var H = HarvestMethod.Harvest; var G = HarvestMethod.Siphon;

            t.ProfileFor(Biome.Lush).Add("biomass", H, 0.70f, 1);
            t.ProfileFor(Biome.Mineral).Add("copper_ore", M, 0.80f, 1).Add("silica", X, 0.40f, 1).Add("titanium_ore", M, 0.35f, 2);
            t.ProfileFor(Biome.Glacial).Add("ice", X, 1.00f, 2);
            t.ProfileFor(Biome.Volcanic).Add("copper_ore", M, 0.50f, 1).Add("titanium_ore", M, 0.60f, 2).Add("radium", X, 0.12f, 4);
            t.ProfileFor(Biome.Fungal).Add("biomass", H, 1.00f, 1).Add("methane", G, 0.40f, 2);
            t.ProfileFor(Biome.Crystal).Add("silica", X, 0.60f, 1).Add("ice", X, 0.20f, 2).Add("radium", X, 0.30f, 4);
            t.ProfileFor(Biome.Obsidian).Add("titanium_ore", M, 0.50f, 2).Add("radium", X, 0.45f, 4).Add("dark_matter", X, 0.15f, 6);
            t.ProfileFor(Biome.Shattered).Add("titanium_ore", M, 0.40f, 2).Add("radium", X, 0.50f, 4).Add("dark_matter", X, 0.35f, 6);
            t.ProfileFor(Biome.Desert).Add("silica", X, 1.00f, 1).Add("copper_ore", M, 0.40f, 1);
            t.ProfileFor(Biome.Oceanic).Add("biomass", H, 0.80f, 1).Add("methane", G, 0.35f, 2);
            t.ProfileFor(Biome.GasGiant).Add("methane", G, 0.90f, 2).Add("helium3", G, 0.80f, 3);
            t.ProfileFor(Biome.Barren).Add("ice", X, 0.30f, 2).Add("silica", X, 0.50f, 1).Add("copper_ore", M, 0.30f, 1).Add("titanium_ore", M, 0.20f, 2);
            t.BeltProfile.Add("ice", X, 0.50f, 2).Add("titanium_ore", M, 0.50f, 2).Add("copper_ore", M, 0.50f, 1);
        }
    }
}
