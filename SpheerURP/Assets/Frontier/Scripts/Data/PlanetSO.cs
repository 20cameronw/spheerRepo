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

        public const float MapScale = 40f;

        public static float Distance(PlanetSO a, PlanetSO b)
        {
            return Vector2.Distance(a.mapPosition, b.mapPosition) * MapScale;
        }
    }
}
