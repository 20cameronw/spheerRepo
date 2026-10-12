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

    public enum StructureKind
    {
        Refinery,
        Alarm,
        Lumber,
        Mine,
        Drill,
        Gunner,
        Laser,
        Satellite,
        Fabricator
    }

    public enum BackdropMood { Auto, Calm, Rush, Gloom, Glow, Storm }

    public enum WeatherKind { Clear, Rain, Snow, Embers, Spores, Dust, Storm }

    public static class StructureKinds
    {
        public static bool IsGather(this StructureKind k) { return k == StructureKind.Lumber || k == StructureKind.Mine || k == StructureKind.Drill; }
        public static bool IsTurret(this StructureKind k) { return k == StructureKind.Gunner || k == StructureKind.Laser; }

        public static MinigameKind Game(this StructureKind k)
        {
            return k == StructureKind.Lumber ? MinigameKind.Chop : k == StructureKind.Mine ? MinigameKind.Mine : MinigameKind.Drill;
        }
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
