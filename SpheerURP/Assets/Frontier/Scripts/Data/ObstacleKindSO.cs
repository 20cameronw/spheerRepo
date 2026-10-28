using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(menuName = "Frontier/Economy/Obstacle Kind")]
    public sealed class ObstacleKindSO : ContentSO
    {
        public ObstacleKindDef data;
        public string modelKey;
    }
}
