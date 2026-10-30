using Frontier.Universe;
using UnityEngine;

namespace Frontier
{
    public static class PlanetSurfaceStyle
    {
        public static void ApplyClimate(Material material, PlanetData planet, Color primary)
        {
            material.SetFloat("_Biome", (int)planet.Biome);
            material.SetFloat("_LandMix", 0f);
            material.SetColor("_OceanColor", new Color(0.035f, 0.27f, 0.68f));
            material.SetColor("_LandColor", new Color(0.13f, 0.48f, 0.2f));
            material.SetColor("_CloudColor", CloudColor(planet, primary));
            material.SetFloat("_CloudCoverage", CloudCoverage(planet));
            material.SetFloat("_PolarCaps", PolarCapCoverage(planet));
        }

        public static Color CloudColor(PlanetData planet, Color primary)
        {
            return planet.ModelKey == "sylva" && planet.Biome == Biome.Lush
                ? Color.white : Color.Lerp(Color.white, primary, 0.22f);
        }

        public static Color AtmosphereColor(PlanetData planet, Color rim)
        {
            return planet.ModelKey == "sylva" && planet.Biome == Biome.Lush
                ? Color.white
                : rim;
        }

        public static float CloudCoverage(PlanetData planet)
        {
            if (planet.AuthoredId == "sylva") return 0f;
            float coverage;
            switch (planet.Weather)
            {
                case WeatherKind.Rain: coverage = 0.48f; break;
                case WeatherKind.Snow: coverage = 0.58f; break;
                case WeatherKind.Embers: coverage = 0.12f; break;
                case WeatherKind.Spores: coverage = 0.62f; break;
                case WeatherKind.Dust: coverage = 0.3f; break;
                case WeatherKind.Storm: coverage = 0.78f; break;
                default: coverage = 0.08f; break;
            }
            if (planet.Kind == PlanetKind.GasGiant) coverage = Mathf.Max(coverage, 0.72f);
            return coverage * Mathf.Clamp01(planet.AtmosphereIntensity);
        }

        public static float PolarCapCoverage(PlanetData planet)
        {
            if (planet.Biome == Biome.Glacial) return 1f;
            return (planet.Hazards & Hazards.Cold) != 0 ? 0.35f : 0f;
        }
    }
}
