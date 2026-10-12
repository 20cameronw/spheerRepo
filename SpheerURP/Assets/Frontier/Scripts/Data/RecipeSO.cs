using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(fileName = "New Recipe", menuName = "Frontier/Refinery Recipe")]
    public class RecipeSO : ScriptableObject
    {
        public string displayName;
        public List<ItemAmount> inputs = new List<ItemAmount>();
        public List<ItemAmount> outputs = new List<ItemAmount>();
    }
}
