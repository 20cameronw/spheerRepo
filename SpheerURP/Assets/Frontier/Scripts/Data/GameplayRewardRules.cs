using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(menuName = "Frontier/Gameplay Reward Rules")]
    public sealed class GameplayRewardRules : ScriptableObject
    {
        [Tooltip("Keep disabled until reward tuning, child-inclusive policy and native-device review are approved.")]
        public bool harvestEnabled;
        public bool constructionEnabled;
        [Min(0)] public int constructionSeconds;
    }
}
