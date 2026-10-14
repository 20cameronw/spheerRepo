namespace Frontier
{
    // Plain data enums shared by every layer. They live in Frontier.Core (no engine references), so ScriptableObjects,
    // game logic, rendering and the pure universe/economy code can all use them. Serialized values are the integers: never reorder.
    public enum MinigameKind { Chop, Mine, Drill, Siphon, Excavate, Salvage }

    public enum UpgradeCategory { Tools, Ship, Defense, Logistics }

    /// <summary>What an upgrade level changes. ToolPower remains for legacy saves/gameplay; the imported economy has separate tool ladders.</summary>
    public enum UpgradeEffect
    {
        ToolPower,
        BackpackSlots,
        FuelEfficiency,
        TurretFireRate,
        TurretDamage,
        TapDamage,
        ReactorHealth,
        RewardBonus,
        JumpRange,
        ScannerRange,
        FuelTankSlots,
        HullPlating,
        StackMultiplier
    }

    /// <summary>
    /// The behaviour of a structure. The first ten values are the original game (their numbers are in assets and saves); the economy catalog's
    /// other structures follow. Which kinds have a behaviour today is <see cref="StructureKinds.IsImplemented"/>.
    /// </summary>
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
        Fabricator,
        Warehouse,
        Foundry,
        ChemReactor,
        QuantumLab,
        FuelDepot,
        CargoDepot,
        Biodome,
        SiphonRig,
        Excavator,
        SalvageYard,
        ShieldPylon,
        MissileSilo,
        TeslaCoil,
        RepairBay,
        ResearchLab,
        RadarTower,
        ColonyHub,
        Hangar,
        AutoHarvester,
        Terraformer,
        WarpGate,
        DysonCollector
    }

    public enum BackdropMood { Auto, Calm, Rush, Gloom, Glow, Storm }

    public enum WeatherKind { Clear, Rain, Snow, Embers, Spores, Dust, Storm }

    public static class StructureKinds
    {
        /// <summary>Gather structures with an implemented harvest minigame.</summary>
        public static bool IsGather(this StructureKind k)
        {
            return k == StructureKind.Lumber || k == StructureKind.Mine || k == StructureKind.Drill ||
                k == StructureKind.Biodome || k == StructureKind.SiphonRig || k == StructureKind.Excavator ||
                k == StructureKind.SalvageYard;
        }
        public static bool IsTurret(this StructureKind k)
        {
            return k == StructureKind.Gunner || k == StructureKind.Laser || k == StructureKind.MissileSilo || k == StructureKind.TeslaCoil;
        }

        /// <summary>A structure that runs recipes (the crafting popup).</summary>
        public static bool IsStation(this StructureKind k)
        {
            return k == StructureKind.Refinery || k == StructureKind.Fabricator || k == StructureKind.Foundry || k == StructureKind.ChemReactor || k == StructureKind.QuantumLab;
        }

        /// <summary>A structure with a grid of storage slots (the warehouse popup).</summary>
        public static bool IsStorage(this StructureKind k)
        {
            return k == StructureKind.Warehouse || k == StructureKind.FuelDepot ||
                k == StructureKind.CargoDepot || k == StructureKind.AutoHarvester;
        }

        /// <summary>
        /// True when the kind has a behaviour in the game today. Other catalog kinds stay unpurchasable until
        /// their gameplay is implemented.
        /// </summary>
        public static bool IsImplemented(this StructureKind k)
        {
            switch (k)
            {
                case StructureKind.Refinery:
                case StructureKind.Alarm:
                case StructureKind.Lumber:
                case StructureKind.Mine:
                case StructureKind.Drill:
                case StructureKind.Gunner:
                case StructureKind.Laser:
                case StructureKind.Foundry:
                case StructureKind.ChemReactor:
                case StructureKind.QuantumLab:
                case StructureKind.Fabricator:
                case StructureKind.Warehouse:
                case StructureKind.FuelDepot:
                case StructureKind.CargoDepot:
                case StructureKind.Biodome:
                case StructureKind.AutoHarvester:
                case StructureKind.Hangar:
                case StructureKind.ShieldPylon:
                case StructureKind.RepairBay:
                case StructureKind.MissileSilo:
                case StructureKind.TeslaCoil:
                case StructureKind.ResearchLab:
                case StructureKind.RadarTower:
                case StructureKind.SiphonRig:
                case StructureKind.Excavator:
                case StructureKind.SalvageYard:
                case StructureKind.ColonyHub:
                    return true;
                default:
                    return false;
            }
        }

        public static MinigameKind Game(this StructureKind k)
        {
            return k == StructureKind.Lumber ? MinigameKind.Chop :
                k == StructureKind.Biodome ? MinigameKind.Chop :
                k == StructureKind.Mine ? MinigameKind.Mine :
                k == StructureKind.SiphonRig ? MinigameKind.Siphon :
                k == StructureKind.Excavator ? MinigameKind.Excavate :
                k == StructureKind.SalvageYard ? MinigameKind.Salvage : MinigameKind.Drill;
        }
    }
}
