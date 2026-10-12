using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [Serializable]
    public class GatherSpot
    {
        public string label = "Gather";
        public MinigameKind game;
        public ResourceSO resource;
        public int baseYield = 5;
        [Range(0f, 1f)] public float difficulty = 0.3f;
    }

    [CreateAssetMenu(fileName = "New Planet", menuName = "Frontier/Planet")]
    public class PlanetSO : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public int tier;
        [Tooltip("Position on the star map (0..1). Travel fuel is based on distance.")]
        public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
        [Tooltip("Scales wave difficulty on this planet.")]
        public float danger = 1f;

        public Color primary = new Color(0.2f, 0.8f, 0.4f);
        public Color secondary = new Color(0.03f, 0.2f, 0.1f);
        [ColorUsage(false, true)] public Color rim = new Color(0.4f, 1.5f, 1f);

        public WeatherKind weather = WeatherKind.Clear;
        [Tooltip("Space backdrop vibe. Auto derives it from weather and the planet id.")]
        public BackdropMood mood = BackdropMood.Auto;
        [Tooltip("Rings are reserved for rare planets.")]
        public bool hasRings;
        [Tooltip("Surface pattern: 0 bands, 1 swirls, 2 craters, 3 lava veins, 4 crystalline facets.")]
        public int style;

        [Header("Visual intensity")]
        [Range(0f, 2f), Tooltip("Surface rim and halo brightness. Lower values give natural, quieter worlds.")]
        public float glowIntensity = 1f;
        [Range(0f, 2f), Tooltip("Atmosphere brightness, independent of the surface glow.")]
        public float atmosphereIntensity = 1f;
        [Range(0f, 20f), Tooltip("Surface pattern frequency. Zero uses the planet's seeded variation.")]
        public float surfaceBands;

        [Header("Size")]
        [Tooltip("Key into ModelLibrary for the 3D model. If empty, falls back to planet id.")]
        public string modelKey;
        public string ModelKey { get { return !string.IsNullOrEmpty(modelKey) ? modelKey : id; } }
        [Tooltip("Radius of the planet's ground SphereCollider. Structures sit on this sphere; satellites orbit above it. 1.5 = standard. " +
                 "In Play mode you can also edit the collider on PlanetRoot/Spin/Surface and it saves back here.")]
        public float surfaceRadius = PlanetView.Radius;
        [Tooltip("Multiplier on the standard planet model size (every model is first fitted to the same diameter).")]
        public float modelScale = 1f;
        [Tooltip("Atmosphere sphere diameter (Transform scale). Default is 5 on every world.")]
        public float atmosphereScale = 5f;
        [Header("World capacity")]
        [Tooltip("Logical world size. Larger worlds keep the same screen size but buildings appear smaller.")]
        [Min(1f)] public float worldSize = 1f;
        [Tooltip("Number of building pads. Existing slot indices are preserved when capacity increases.")]
        [Min(12)] public int buildingSlots = 12;
        public int BuildingSlots { get { return Mathf.Max(12, buildingSlots); } }
        public float StructureScale { get { return 1f / Mathf.Max(1f, worldSize); } }

        [InspectorName("Planet Resources"), Tooltip("Available resources, gathering methods, yield and difficulty. These are not physical spots or spawn positions.")]
        public List<GatherSpot> spots = new List<GatherSpot>();
        [Tooltip("Solar system this planet orbits (see PlanetSO.Systems).")]
        public string systemId;

        public class StarSystem
        {
            public string id, name; public Vector2 center; public Color color;
            public StarSystem(string id, string name, float x, float y, Color c) { this.id = id; this.name = name; center = new Vector2(x, y); color = c; }
        }

        public static readonly StarSystem[] Systems =
        {
            new StarSystem("sylva", "Sylva System", 0.22f, 0.80f, new Color(1f, 0.95f, 0.6f)),
            new StarSystem("ember", "Emberfall", 0.56f, 0.84f, new Color(1f, 0.45f, 0.2f)),
            new StarSystem("verdance", "Verdance", 0.25f, 0.40f, new Color(0.5f, 1f, 0.6f)),
            new StarSystem("umbra", "Umbra", 0.82f, 0.68f, new Color(0.7f, 0.4f, 1f)),
            new StarSystem("frost", "Frostline", 0.58f, 0.30f, new Color(0.6f, 0.9f, 1f)),
            new StarSystem("edge", "The Fracture", 0.88f, 0.12f, new Color(1f, 1f, 1f)),
        };

        public static StarSystem SystemOf(string id)
        {
            foreach (var s in Systems) if (s.id == id) return s;
            return null;
        }

        public static float MapScale { get { return FrontierSettings.I.mapScale; } }

        public static float Distance(PlanetSO a, PlanetSO b)
        {
            return Vector2.Distance(a.mapPosition, b.mapPosition) * MapScale;
        }
    }
}
