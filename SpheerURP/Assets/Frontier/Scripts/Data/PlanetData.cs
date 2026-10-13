using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Frontier.Universe;

namespace Frontier
{
    /// <summary>
    /// The runtime planet: what every system of the game (state, defense, planet view, world surface, UI, weather, backdrop) uses where
    /// it used to take a <see cref="PlanetSO"/>. It wraps the immutable universe <see cref="PlanetDescriptor"/> and the owning star
    /// system, plus the authored <see cref="PlanetSO"/> for the 30 hand made Core Worlds (null for generated worlds). Authored worlds
    /// read their presentation straight from the asset (the authoring format stays the source of truth, and in-editor tuning keeps
    /// working); generated worlds get the documented defaults: standard 1.5 surface radius, atmosphere scale 5, model scale 1, mood Auto.
    /// Instances are cheap to hold and compare by <see cref="Id"/>.
    /// </summary>
    public sealed class PlanetData
    {
        /// <summary>Standard ground radius and atmosphere scale of every world on screen.</summary>
        public const float StandardSurfaceRadius = PlanetSO.StandardSurfaceRadius;
        public const float StandardAtmosphereScale = 5f;

        public readonly PlanetDescriptor Descriptor;
        /// <summary>The star system the planet orbits.</summary>
        public readonly StarSystem Star;
        /// <summary>The authoring asset of a Core World; null for generated worlds.</summary>
        public readonly PlanetSO Authored;
        /// <summary>Stable 64 bit seed for everything procedural about the look (surface pattern, rings, moons, mood): never a string hash.</summary>
        public readonly ulong Seed;

        readonly string description;
        readonly Color primary, secondary, rim;
        readonly List<GatherSpot> spots = new List<GatherSpot>();
        readonly List<ResourceSO> resources = new List<ResourceSO>();
        readonly GatherSpot surfaceScrap;
        readonly bool syntheticScrapAlways;

        public PlanetData(PlanetDescriptor descriptor, StarSystem star, PlanetSO authored, ulong universeSeed, Func<string, ResourceSO> resolve, ResourceSO scrap)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            if (star == null) throw new ArgumentNullException("star");
            Descriptor = descriptor;
            Star = star;
            Authored = authored;
            Seed = PlanetSeeds.VisualSeed(descriptor, universeSeed);
            description = authored != null ? authored.description : PlanetBlurb.Describe(descriptor, universeSeed);
            primary = ToColor(descriptor.Primary);
            secondary = ToColor(descriptor.Secondary);
            rim = ToColor(descriptor.Rim);

            if (authored != null)
            {
                foreach (var s in authored.spots) if (s != null && s.resource != null) spots.Add(s);
            }
            else
            {
                foreach (var d in descriptor.Deposits)
                {
                    var resource = resolve != null ? resolve(d.ResourceId) : null;
                    if (resource == null) continue;
                    spots.Add(new GatherSpot
                    {
                        label = SpotLabel(resource, d.Method),
                        game = HarvestMapping.ToMinigame(d.Method),
                        resource = resource,
                        baseYield = Mathf.Max(1, Mathf.RoundToInt(d.BaseYield * d.Richness)),
                        difficulty = Mathf.Clamp01(d.Difficulty)
                    });
                }
            }
            foreach (var s in spots) if (!resources.Contains(s.resource)) resources.Add(s.resource);
            if (scrap != null)
            {
                if (!resources.Contains(scrap)) resources.Add(scrap);
                surfaceScrap = new GatherSpot { label = "Surface Scrap", game = MinigameKind.Mine, resource = scrap, baseYield = 6, difficulty = 0.2f };
                // Authored worlds always offered the surface scrap on top of their own spots; a generated world already has scrap as a deposit.
                syntheticScrapAlways = authored != null;
            }
        }

        static Color ToColor(Rgb c) { return new Color(c.R, c.G, c.B, 1f); }

        /// <summary>Label of a generated gather spot, from its resource and how it is harvested.</summary>
        public static string SpotLabel(ResourceSO resource, HarvestMethod method)
        {
            string noun;
            switch (method)
            {
                case HarvestMethod.Chop: noun = "Grove"; break;
                case HarvestMethod.Drill: noun = "Bore"; break;
                case HarvestMethod.Siphon: noun = "Siphon"; break;
                case HarvestMethod.Excavate: noun = "Dig"; break;
                case HarvestMethod.Salvage: noun = "Heap"; break;
                case HarvestMethod.Harvest: noun = "Beds"; break;
                default: noun = "Seam"; break;
            }
            return resource.displayName + " " + noun;
        }

        // ---- identity --------------------------------------------------------------------------------------------------

        /// <summary>Canonical universe id ("p:X:Y:I:P"): the key of every save record about this world.</summary>
        public string Id { get { return Descriptor.Id; } }
        public PlanetKey Key { get { return Descriptor.Key; } }
        /// <summary>The legacy authored id ("sylva") of a Core World; null for generated worlds.</summary>
        public string AuthoredId { get { return Descriptor.AuthoredId; } }
        public bool IsAuthored { get { return Authored != null; } }
        public string Name { get { return Authored != null ? Authored.displayName : Descriptor.Name; } }
        public string Description { get { return description; } }
        public string SystemName { get { return Star.Name; } }

        // ---- gameplay numbers ------------------------------------------------------------------------------------------

        public int Tier { get { return Authored != null ? Authored.tier : Descriptor.Tier; } }
        public float Danger { get { return Authored != null ? Authored.danger : Descriptor.Danger; } }
        /// <summary>Logical size: bigger worlds have more pads and smaller structures but look the same size on screen.</summary>
        public float WorldSize { get { return Authored != null ? Authored.worldSize : Descriptor.WorldSize; } }
        public int BuildingSlots { get { return Authored != null ? Authored.BuildingSlots : Mathf.Max(12, Descriptor.BuildingSlots); } }
        public float StructureScale { get { return 1f / Mathf.Max(1f, WorldSize); } }

        // ---- presentation ----------------------------------------------------------------------------------------------

        public Color Primary { get { return Authored != null ? Authored.primary : primary; } }
        public Color Secondary { get { return Authored != null ? Authored.secondary : secondary; } }
        public Color Rim { get { return Authored != null ? Authored.rim : rim; } }
        public WeatherKind Weather { get { return Authored != null ? Authored.weather : (WeatherKind)(int)Descriptor.Weather; } }
        public BackdropMood Mood { get { return Authored != null ? Authored.mood : BackdropMood.Auto; } }
        public bool HasRings { get { return Authored != null ? Authored.hasRings : Descriptor.HasRings; } }
        public int MoonCount { get { return Descriptor.MoonCount; } }
        /// <summary>Surface pattern: 0 bands, 1 swirls, 2 craters, 3 lava veins, 4 crystalline facets.</summary>
        public int Style { get { return Authored != null ? Authored.style : Descriptor.Style; } }
        public float GlowIntensity { get { return Authored != null ? Authored.glowIntensity : Descriptor.GlowIntensity; } }
        public float AtmosphereIntensity { get { return Authored != null ? Authored.atmosphereIntensity : Descriptor.AtmosphereIntensity; } }
        public float SurfaceBands { get { return Authored != null ? Authored.surfaceBands : Descriptor.SurfaceBands; } }
        /// <summary>Key into the ModelLibrary of the planet mesh.</summary>
        public string ModelKey { get { return Authored != null ? Authored.ModelKey : Descriptor.ModelKey; } }
        public float ModelScale { get { return Authored != null ? Authored.modelScale : 1f; } }
        public float SurfaceRadius { get { return Authored != null ? Authored.surfaceRadius : StandardSurfaceRadius; } }
        public float AtmosphereScale { get { return Authored != null ? Authored.atmosphereScale : StandardAtmosphereScale; } }

        // ---- what the universe says about it ---------------------------------------------------------------------------

        public Biome Biome { get { return Descriptor.Biome; } }
        public SizeClass Size { get { return Descriptor.Size; } }
        public PlanetKind Kind { get { return Descriptor.Kind; } }
        public Hazards Hazards { get { return Descriptor.Hazards; } }
        /// <summary>Readable biome ("Gas giant", "Lush").</summary>
        public string BiomeName { get { return Biome == Biome.GasGiant ? "Gas giant" : Biome.ToString(); } }
        public string SizeName { get { return Size.ToString(); } }

        // ---- resources and gather spots --------------------------------------------------------------------------------

        /// <summary>Every resource the planet offers, in the order of its deposits, scrap last when it was not a deposit already.</summary>
        public IReadOnlyList<ResourceSO> Resources { get { return resources; } }

        /// <summary>The planet's own gather spots (without the surface scrap every world offers to the Mine game).</summary>
        public IReadOnlyList<GatherSpot> Spots { get { return spots; } }

        /// <summary>The gather spots for one mini-game, a fresh list. The Mine game also offers the surface scrap first.</summary>
        public List<GatherSpot> SpotsFor(MinigameKind game)
        {
            var list = new List<GatherSpot>();
            foreach (var s in spots) if (s.game == game && s.resource != null) list.Add(s);
            if (game == MinigameKind.Mine && surfaceScrap != null)
            {
                bool covered = false;
                if (!syntheticScrapAlways) foreach (var s in list) if (s.resource == surfaceScrap.resource) { covered = true; break; }
                if (!covered) list.Insert(0, surfaceScrap);
            }
            return list;
        }

        public bool Offers(ResourceSO resource) { return resources.Contains(resource); }

        // ---- debugging -------------------------------------------------------------------------------------------------

        /// <summary>A readable multi-line description of the world and its descriptor, for logs and bug reports.</summary>
        public string Dump()
        {
            var d = Descriptor;
            var sb = new StringBuilder();
            sb.Append(Name).Append("  [").Append(Id).Append(']').AppendLine();
            sb.Append("  ").Append(IsAuthored ? "authored Core World '" + AuthoredId + "'" : "generated (generator v" + d.GeneratorVersion + ")").AppendLine();
            sb.Append("  system ").Append(Star.Name).Append(' ').Append(Star.Id).Append("  ").Append(Star.StarClass).Append(" star  at (")
                .Append(Star.X.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(", ")
                .Append(Star.Y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(") ly  system tier ").Append(Star.Tier).AppendLine();
            sb.Append("  ").Append(d.Biome).Append(' ').Append(d.Kind).Append(' ').Append(d.Size).Append("  tier ").Append(Tier).Append("  danger ")
                .Append(Danger.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append("  hazards ").Append(d.Hazards).AppendLine();
            sb.Append("  world size ").Append(WorldSize.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("  slots ").Append(BuildingSlots)
                .Append("  structure scale ").Append(StructureScale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
            sb.Append("  weather ").Append(Weather).Append("  rings ").Append(HasRings).Append("  moons ").Append(MoonCount).Append("  style ").Append(Style)
                .Append("  model ").Append(ModelKey).Append("  seed 0x").Append(Seed.ToString("X16")).AppendLine();
            sb.Append("  orbit ").Append(d.OrbitRadius.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(" AU  phase ")
                .Append(d.OrbitPhase.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("  temperature ")
                .Append(d.Temperature.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" K").AppendLine();
            sb.Append("  colours primary ").Append(d.Primary).Append("  secondary ").Append(d.Secondary).Append("  rim ").Append(d.Rim).AppendLine();
            sb.Append("  deposits");
            foreach (var dep in d.Deposits) sb.Append("  ").Append(dep);
            sb.AppendLine();
            sb.Append("  ").Append(Description);
            return sb.ToString();
        }

        public override string ToString() { return Name + " [" + Id + "]"; }

        // Worlds are identified by their canonical id: the generated-world cache may hand out a new object for the same world, so
        // compare with Equals (or .Id), never with ==.
        public override bool Equals(object obj)
        {
            var other = obj as PlanetData;
            return other != null && string.Equals(other.Id, Id, StringComparison.Ordinal);
        }

        public override int GetHashCode() { return Id.GetHashCode(); }
    }
}
