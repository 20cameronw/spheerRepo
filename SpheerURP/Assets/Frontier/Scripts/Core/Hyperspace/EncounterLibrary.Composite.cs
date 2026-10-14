using System.Collections.Generic;

namespace Frontier.Hyperspace
{
    public sealed partial class EncounterLibrary
    {
        /// <summary>
        /// Overlays template b on template a: b's entries are shifted by offsetZ (z units at 30 u/s; negative starts b first) and optionally mirrored left-right.
        /// Composites are how dense late-game encounters are built from verified parts; the fairness solver then proves the combination itself.
        /// </summary>
        public static EncounterTemplate ComposeTemplate(string id, EncounterTemplate a, EncounterTemplate b, float offsetZ, bool mirrorB, int minTier, int maxTier, EncounterRole role, float declaredSpeed)
        {
            var tb = new TemplateBuilder(id, minTier, maxTier, (float)((double)a.cost + b.cost), role, EncounterFamily.Mixed, declaredSpeed, 1f, a.mirrorable && b.mirrorable);
            tb.Notes(a.id + " + " + b.id + " (offset " + offsetZ + (mirrorB ? ", mirrored" : "") + ")");
            for (int i = 0; i < a.entries.Length; i++) tb.Add(a.entries[i]);
            for (int i = 0; i < b.entries.Length; i++)
            {
                SpawnEntry e = b.entries[i];
                if (mirrorB) e = EncounterInstantiator.Mirror(e);
                e.zOffset = (float)((double)e.zOffset + offsetZ);
                if (e.group != 0) e.group += 100;
                tb.Add(e);
            }
            return tb.Done();
        }

        struct Recipe
        {
            public string id, a, b;
            public float offset;
            public bool mirrorB;
            public int minTier;
            public float declared;
        }

        static Recipe R(string id, string a, string b, float offset, bool mirrorB, int minTier, float declared)
        {
            return new Recipe { id = id, a = a, b = b, offset = offset, mirrorB = mirrorB, minTier = minTier, declared = declared };
        }

        /// <summary>Composite templates found by the solver-driven search (runner command "compose") and frozen here as recipes.</summary>
        void BuildComposites()
        {
            Recipe[] recipes = CompositeRecipes();
            for (int i = 0; i < recipes.Length; i++)
            {
                Recipe r = recipes[i];
                EncounterTemplate a = ById(r.a), b = ById(r.b);
                EncounterTemplate c = ComposeTemplate(r.id, a, b, r.offset, r.mirrorB, r.minTier, 12, EncounterRole.Peak, r.declared);
                c.index = templates.Count;
                templates.Add(c);
            }
        }
    }
}
