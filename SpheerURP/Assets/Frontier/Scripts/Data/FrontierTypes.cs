using System;

namespace Frontier
{
    public enum MinigameKind { Chop, Mine, Drill }

    public enum UpgradeCategory { Tools, Ship, Defense }

    public enum UpgradeEffect
    {
        ToolPower,
        BackpackSlots,
        FuelEfficiency,
        TurretCount,
        TurretDamage,
        TapDamage,
        ReactorHealth,
        RewardBonus
    }

    [Serializable]
    public struct ItemAmount
    {
        public ResourceSO resource;
        public int amount;

        public ItemAmount(ResourceSO resource, int amount)
        {
            this.resource = resource;
            this.amount = amount;
        }
    }
}
