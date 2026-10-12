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
        [Tooltip("Rings are reserved for rare planets.")]
        public bool hasRings;
        [Tooltip("Surface pattern: 0 bands, 1 swirls, 2 craters, 3 lava veins, 4 crystalline facets.")]
        public int style;

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
