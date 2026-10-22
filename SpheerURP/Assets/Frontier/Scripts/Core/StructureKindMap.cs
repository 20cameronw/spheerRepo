using System.Collections.Generic;

namespace Frontier
{
    /// <summary>
    /// Which <see cref="StructureKind"/> (behaviour) each structure id of the economy catalog has. The catalog names the structures; the
    /// game decides what they do. Ids that are not in the table are the economy's, not the game's, and the importer reports them.
    /// </summary>
    public static class StructureKindMap
    {
        static readonly Dictionary<string, StructureKind> ById = new Dictionary<string, StructureKind>(System.StringComparer.Ordinal)
        {
            { "refinery", StructureKind.Refinery }, { "alarm", StructureKind.Alarm }, { "lumber", StructureKind.Lumber }, { "mine", StructureKind.Mine },
            { "driller", StructureKind.Drill }, { "gunner", StructureKind.Gunner }, { "laser", StructureKind.Laser }, { "orbital_relay", StructureKind.Satellite },
            { "fabricator", StructureKind.Fabricator }, { "warehouse", StructureKind.Warehouse }, { "foundry", StructureKind.Foundry },
            { "chem_reactor", StructureKind.ChemReactor }, { "quantum_lab", StructureKind.QuantumLab }, { "fuel_depot", StructureKind.FuelDepot },
            { "cargo_depot", StructureKind.CargoDepot }, { "biodome", StructureKind.Biodome }, { "siphon_rig", StructureKind.SiphonRig },
            { "excavator", StructureKind.Excavator }, { "salvage_yard", StructureKind.SalvageYard }, { "shield_pylon", StructureKind.ShieldPylon },
            { "missile_silo", StructureKind.MissileSilo }, { "tesla_coil", StructureKind.TeslaCoil }, { "repair_bay", StructureKind.RepairBay },
            { "research_lab", StructureKind.ResearchLab }, { "radar_tower", StructureKind.RadarTower }, { "colony_hub", StructureKind.ColonyHub },
            { "hangar", StructureKind.Hangar }, { "auto_harvester", StructureKind.AutoHarvester }, { "terraformer", StructureKind.Terraformer },
            { "warp_gate", StructureKind.WarpGate }, { "dyson_collector", StructureKind.DysonCollector }
        };

        public static bool TryGet(string structureId, out StructureKind kind)
        {
            if (structureId != null && ById.TryGetValue(structureId, out kind)) return true;
            kind = StructureKind.Refinery;
            return false;
        }

        public static IEnumerable<string> Ids { get { return ById.Keys; } }
    }
}
