#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Frontier.EditorTools
{
    public static class FrontierCombatChecks
    {
        [MenuItem("Frontier/Validate Combat Balance")]
        public static void Run()
        {
            CheckSupport.IsolateSaves();
            var db = GameDatabase.Load();
            var previousState = GameState.I;
            var go = new GameObject("CombatValidation");
            try
            {
                var state = new GameState(db, false);
                var defense = go.AddComponent<DefenseController>();
                defense.Init(null, null, state);
                int previews = 0;
                foreach (var planet in db.planets)
                {
                    state.DebugTeleport(state.universe.Get(planet.id));
                    WaveInfo previous = null;
                    for (int fuel = 1; fuel <= 40; fuel++)
                    {
                        var info = defense.Preview(fuel);
                        var repeat = defense.Preview(fuel);
                        Check(info.aliens.Count == repeat.aliens.Count, "Repeated preview changed alien count.");
                        for (int i = 0; i < info.aliens.Count; i++)
                            Check(info.aliens[i] == repeat.aliens[i], "Repeated preview changed alien type.");
                        Check(info.reward.Count == repeat.reward.Count, "Repeated preview changed reward types.");
                        for (int i = 0; i < info.reward.Count; i++)
                            Check(info.reward[i].resource == repeat.reward[i].resource && info.reward[i].amount == repeat.reward[i].amount,
                                "Repeated preview changed rewards.");
                        if (previous != null)
                        {
                            Check(info.aliens.Count >= previous.aliens.Count, "More fuel reduced alien count.");
                            for (int i = 0; i < previous.aliens.Count; i++)
                                Check(info.aliens[i] == previous.aliens[i], "More fuel replaced existing aliens.");
                            Check(info.healthMult > previous.healthMult && info.damageMult > previous.damageMult && info.speedMult > previous.speedMult,
                                "More fuel did not increase difficulty.");
                            Check(info.xp >= previous.xp, "More fuel reduced XP.");
                            foreach (var reward in previous.reward)
                            {
                                int amount = 0;
                                foreach (var next in info.reward) if (next.resource == reward.resource) amount = next.amount;
                                Check(amount >= reward.amount, "More fuel reduced " + reward.resource.displayName + ".");
                            }
                        }
                        previous = info;
                        previews++;
                    }
                }
                foreach (var planet in db.planets)
                    for (int wave = 1; wave <= 20; wave++)
                    {
                        System.Collections.Generic.List<EnemySO> previous = null;
                        for (int fuel = 1; fuel <= 40; fuel++)
                        {
                            var aliens = db.formula.Build(db.enemies, wave,
                                db.formula.Budget(wave, planet.danger, DefenseController.FuelMult(fuel)), new System.Random(7919 * wave));
                            if (previous != null)
                            {
                                Check(aliens.Count >= previous.Count, "Wave builder count decreased.");
                                for (int i = 0; i < previous.Count; i++)
                                    Check(aliens[i] == previous[i], "Wave builder lost its prefix.");
                            }
                            previous = aliens;
                        }
                    }
                var shader = Resources.Load<Shader>("Frontier/StructureOutline");
                Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Selection outline shader failed to import.");
                Debug.Log("Frontier: combat checks passed (" + previews + " previews; fuel 1-40; wave prefixes 1-20).");
            }
            finally
            {
                GameState.I = previousState;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Frontier combat check: " + message);
        }
    }
}
#endif
