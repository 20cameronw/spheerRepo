using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(menuName = "Frontier/Economy/Legacy Id Map")]
    public sealed class LegacyIdMapSO : ContentSO
    {
        public LegacyIdEntry[] rows;
    }
}
