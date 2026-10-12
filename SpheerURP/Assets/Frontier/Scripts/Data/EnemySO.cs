using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
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
        public List<ItemAmount> bounty = new List<ItemAmount>();
    }
}
