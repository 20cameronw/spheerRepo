using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(menuName = "Frontier/Gameplay Reward Rules")]
    public sealed class GameplayRewardRules : ScriptableObject
    {
        [Tooltip("Player-initiated offers still require a configured native provider and explicit advertising opt-in.")]
        public bool harvestEnabled;
        public bool constructionEnabled;
        public bool shardsEnabled;
        [Min(0)] public int constructionSeconds;
    }
}
