using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(fileName = "New Resource", menuName = "Frontier/Resource")]
    public class ResourceSO : ScriptableObject
    {
        public string id;
        public string displayName;
        [ColorUsage(false, true)] public Color color = Color.white;
        [Tooltip("How many of this resource fit in one backpack slot.")]
        public int stackSize = 20;
        [TextArea] public string description;
    }
}
