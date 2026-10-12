using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
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
        public float rewardPerWave = 0.1f;
        public float spawnInterval = 0.9f;
        public int maxAliens = 120;
        [Range(0f, 1f)] public float eliteChance = 0.35f;

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
            foreach (var e in pool) if (e != null && e.minWave <= wave) avail.Add(e);
            var result = new List<EnemySO>();
            if (avail.Count == 0) return result;

            while (result.Count < maxAliens)
            {
                var afford = new List<EnemySO>();
                foreach (var e in avail) if (e.threat <= budget) afford.Add(e);
                if (afford.Count == 0) break;

                EnemySO pick;
                if (rng.NextDouble() < eliteChance)
                {
                    pick = afford[0];
                    foreach (var e in afford) if (e.threat > pick.threat) pick = e;
                }
                else
                {
                    float total = 0f;
                    foreach (var e in afford) total += Mathf.Max(0.01f, e.weight);
                    float roll = (float)rng.NextDouble() * total;
                    pick = afford[afford.Count - 1];
                    foreach (var e in afford)
                    {
                        roll -= Mathf.Max(0.01f, e.weight);
                        if (roll <= 0f) { pick = e; break; }
                    }
                }
                result.Add(pick);
                budget -= pick.threat;
            }

            if (result.Count == 0)
            {
                var cheapest = avail[0];
                foreach (var e in avail) if (e.threat < cheapest.threat) cheapest = e;
                result.Add(cheapest);
            }
            return result;
        }
    }
}
