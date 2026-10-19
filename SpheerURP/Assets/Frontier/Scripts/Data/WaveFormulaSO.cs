using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [System.Serializable]
    public sealed class WaveModifier
    {
        public string name;
        public string description;
        [Min(1f)] public float health = 1f;
        [Min(1f)] public float damage = 1f;
        [Min(1f)] public float speed = 1f;
        [Min(1f)] public float reward = 1f;
    }

    /// <summary>
    /// Turns (wave number, planet danger, risk) into a threat budget, then spends the
    /// budget on aliens. Tune the numbers here to retune the whole game's difficulty.
    /// </summary>
    [CreateAssetMenu(fileName = "Wave Formula", menuName = "Frontier/Wave Formula")]
    public class WaveFormulaSO : ScriptableObject
    {
        public float baseBudget = 5f;
        public float linearPerWave = 2f;
        public float growthPerWave = 1.12f;
        public float dangerWeight = 0.5f;
        public float healthPerWave = 0.07f;
        [Tooltip("Additional alien health per fuel cell above one.")]
        public float healthPerFuel = 0.08f;
        [Tooltip("Additional reactor damage per fuel cell above one.")]
        public float damagePerFuel = 0.04f;
        [Tooltip("Additional approach speed per fuel cell above one.")]
        public float speedPerFuel = 0.01f;
        [Tooltip("Seconds aliens must be visible before placed turrets can target them.")]
        public float visibleApproachTime = 1.2f;
        public float rewardPerWave = 0.1f;
        public float spawnInterval = 0.9f;
        public int maxAliens = 120;
        [Range(0f, 1f)] public float eliteChance = 0.35f;
        [Min(1)] public int modifiersFromWave = 4;
        public List<WaveModifier> modifiers = new List<WaveModifier>();

        public WaveModifier Modifier(int wave)
        {
            if (wave < modifiersFromWave || modifiers.Count == 0) return null;
            return modifiers[(wave - modifiersFromWave) % modifiers.Count];
        }

        public float Budget(int wave, float danger, float risk)
        {
            float n = Mathf.Max(0, wave - 1);
            return (baseBudget + linearPerWave * n) * Mathf.Pow(growthPerWave, n) * (1f + danger * dangerWeight) * risk;
        }

        public float HealthMult(int wave) { return 1f + healthPerWave * Mathf.Max(0, wave - 1); }

        public float RewardMult(int wave, float risk) { return (1f + rewardPerWave * Mathf.Max(0, wave - 1)) * risk; }

        public float Interval(int wave) { return Mathf.Max(0.25f, spawnInterval / (1f + 0.03f * wave)); }

        public List<EnemySO> Build(IList<EnemySO> pool, int wave, float budget, System.Random rng)
        {
            var avail = new List<EnemySO>();
            EnemySO boss = null;
            foreach (var e in pool)
            {
                if (e == null || !e.Available(wave)) continue;
                if (e.rank == AlienRank.Boss)
                {
                    if (boss == null || e.threat > boss.threat) boss = e;
                }
                else avail.Add(e);
            }
            var result = new List<EnemySO>();
            if (avail.Count == 0) return result;

            var cheapest = avail[0];
            foreach (var e in avail) if (e.threat < cheapest.threat) cheapest = e;
            result.Add(cheapest);
            budget -= Mathf.Max(0.01f, cheapest.threat);

            // Reserve the boss before random escorts: more fuel still extends the same sequence.
            if (boss != null && result.Count < maxAliens)
            {
                if (boss.threat > budget) return result;
                result.Add(boss);
                budget -= Mathf.Max(0.01f, boss.threat);
            }

            while (result.Count < maxAliens)
            {
                EnemySO pick;
                if (rng.NextDouble() < eliteChance)
                {
                    pick = avail[0];
                    foreach (var e in avail) if (e.threat > pick.threat) pick = e;
                }
                else
                {
                    float total = 0f;
                    foreach (var e in avail) total += Mathf.Max(0.01f, e.weight);
                    float roll = (float)rng.NextDouble() * total;
                    pick = avail[avail.Count - 1];
                    foreach (var e in avail)
                    {
                        roll -= Mathf.Max(0.01f, e.weight);
                        if (roll <= 0f) { pick = e; break; }
                    }
                }
                // The same random sequence is a prefix at every budget: extra fuel
                // adds enemies instead of replacing the preview with a new roll.
                if (pick.threat > budget) break;
                result.Add(pick);
                budget -= Mathf.Max(0.01f, pick.threat);
            }

            return result;
        }
    }
}
