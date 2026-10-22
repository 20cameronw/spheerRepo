namespace Frontier
{
    /// <summary>
    /// A structure the player built, as the rest of the game sees it: the saved <see cref="StructureRecord"/> (live, so level and slot
    /// always reflect the world store) bound to its <see cref="StructureSO"/> definition and the canonical id of its planet.
    /// Instances are cached by <see cref="GameState"/> per record, so a reference held by the UI stays valid while the structure exists.
    /// </summary>
    public sealed class PlacedStructure
    {
        /// <summary>Canonical universe id of the planet the structure stands on.</summary>
        public readonly string planetId;
        public readonly StructureSO def;
        public readonly StructureRecord record;

        public PlacedStructure(string planetId, StructureSO def, StructureRecord record)
        {
            this.planetId = planetId;
            this.def = def;
            this.record = record;
        }

        public int slot { get { return record.slot; } }
        public int level { get { return record.level; } }

        public override string ToString() { return (def != null ? def.id : "?") + " LV" + level + " @" + planetId + "#" + slot; }
    }
}
