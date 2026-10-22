using UnityEngine;

namespace Frontier
{
    public static class AscensionStyle
    {
        public static Color Accent(int rank)
        {
            return Color.HSVToRGB((float)((Mathf.Max(0, rank) * 0.618033988749895) % 1.0), 0.65f, 1f);
        }
    }

    public sealed class AscensionAura : MonoBehaviour
    {
        GameState state;
        PlanetView planet;
        Transform parent;
        GameObject ring;
        Material material;
        public int Rank { get; private set; }

        public void Init(PlanetView view, GameState game)
        {
            planet = view;
            state = game;
            planet.Rebuilt += OnPlanetRebuilt;
            state.Changed += Refresh;
            Refresh();
        }

        void OnPlanetRebuilt(PlanetData _) { Refresh(); }

        void Refresh()
        {
            if (planet.Current != state.planet || (Rank == state.AscensionRank && parent == planet.Root)) return;
            Clear();
            Rank = state.AscensionRank;
            parent = planet.Root;
            if (Rank <= 0 || parent == null) return;
            ring = new GameObject("Legacy halo");
            ring.transform.SetParent(parent, false);
            var line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.028f;
            line.positionCount = 64;
            material = Fx.AddMat(AscensionStyle.Accent(Rank) * 0.65f);
            line.sharedMaterial = material;
            float radius = planet.SurfaceRadius() + 0.3f;
            int crests = 3 + Rank % 5;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                float r = radius + Mathf.Sin(angle * crests) * 0.045f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0));
            }
        }

        void Clear()
        {
            if (ring != null) Destroy(ring);
            if (material != null) Destroy(material);
            ring = null;
            material = null;
        }

        void OnDestroy()
        {
            if (planet != null) planet.Rebuilt -= OnPlanetRebuilt;
            if (state != null) state.Changed -= Refresh;
            Clear();
        }
    }
}
