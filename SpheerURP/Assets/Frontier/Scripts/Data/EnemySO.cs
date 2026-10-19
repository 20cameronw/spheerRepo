using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum AlienRank { Standard, Elite, Boss }

    [CreateAssetMenu(fileName = "New Alien", menuName = "Frontier/Alien")]
    public class EnemySO : ScriptableObject
    {
        public string id;
        public string displayName;
        [ColorUsage(false, true)] public Color color = Color.magenta;
        public PrimitiveType shape = PrimitiveType.Sphere;
        public float scale = 0.5f;
        public float health = 10f;
        public float speed = 1.2f;
        [Tooltip("Damage dealt to the reactor on impact.")]
        public float damage = 5f;
        [Tooltip("Cost from the wave's threat budget.")]
        public float threat = 1f;
        [Tooltip("First wave number this alien can appear in.")]
        public int minWave = 1;
        public float weight = 1f;
        public AlienRank rank;
        [Range(0f, 0.9f)] public float armor;
        [Tooltip("Boss recurrence, counted from minWave. Bosses spawn at most once, within the wave budget.")]
        [Min(1)] public int bossInterval = 5;
        public List<ItemAmount> bounty = new List<ItemAmount>();

        public bool Available(int wave)
        {
            return wave >= minWave && (rank != AlienRank.Boss || (wave - minWave) % Mathf.Max(1, bossInterval) == 0);
        }

        public float DamageAfterArmor(float amount)
        {
            return Mathf.Max(0f, amount) * (1f - Mathf.Clamp(armor, 0f, 0.9f));
        }
    }
}
