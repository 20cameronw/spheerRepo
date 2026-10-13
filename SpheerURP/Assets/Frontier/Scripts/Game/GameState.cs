using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// The game session: a facade over small id based services (<see cref="Inventory"/>, <see cref="Progression"/>, <see cref="WorldStore"/>,
    /// <see cref="PlayerStats"/>), the <see cref="UniverseService"/> and the <see cref="SaveService"/>. The rules live in those services
    /// (engine free, unit tested); this class translates between them and the ScriptableObject world the UI, visuals and combat use
    /// (<see cref="ResourceSO"/>, <see cref="StructureSO"/>, <see cref="UpgradeSO"/>, <see cref="PlanetData"/>) and signals changes.
    /// Persistence is debounced: every change marks the state dirty and the app flushes it (see <see cref="Tick"/> and <see cref="Flush"/>).
    /// </summary>
    public partial class GameState
    {
        public static GameState I;

        /// <summary>Redirects the save directory (screenshot harness, tests) so they never touch the player's real save.</summary>
        public static string SaveDirectoryOverride;

        public readonly GameDatabase db;
        public readonly UniverseService universe;
        public readonly IClock clock;
        /// <summary>The backpack (id based rules, see <see cref="Inventory"/>).</summary>
        public readonly Inventory backpack;
        public readonly Progression progression;
        public readonly WorldStore world;
        public readonly SaveService saves;
        public event Action Changed;

        /// <summary>The world the player is on.</summary>
        public PlanetData planet { get; private set; }
        /// <summary>Lifetime numbers (the inputs of the future Frontier Score).</summary>
        public PlayerStats stats { get; private set; }

        readonly Dictionary<string, ResourceSO> resourceById = new Dictionary<string, ResourceSO>(StringComparer.Ordinal);

        public GameState(GameDatabase db, bool loadExistingSave = true, IClock clock = null)
        {
            if (db == null) throw new ArgumentNullException("db");
            this.db = db;
            this.clock = clock ?? new SystemClock();
            I = this;
            foreach (var r in db.resources) if (r != null && !string.IsNullOrEmpty(r.id)) resourceById[r.id] = r;
            universe = UniverseService.For(db);
            backpack = new Inventory(() => Slots, StackSizeOf);
            progression = new Progression(() => FrontierSettings.I.xpPerLevelBase);
            world = new WorldStore();
            stats = new PlayerStats();
            saves = new SaveService(SaveDirectory(), this.clock, BuildSave);
            lastTickAt = this.clock.MonotonicSeconds;
            LoadReport = new SaveLoadReport { source = SaveSource.NewGame };
            if (loadExistingSave) LoadOrStart();
            else StartNewGame();
        }

        int StackSizeOf(string id)
        {
            ResourceSO r;
            return id != null && resourceById.TryGetValue(id, out r) ? Mathf.Max(1, r.stackSize) : 0;
        }

        // ---------- Inventory ----------
        public int Count(ResourceSO r) { return r == null ? 0 : backpack.Count(r.id); }

        public int Slots { get { return db.baseBackpackSlots + Mathf.RoundToInt(EffectSum(UpgradeEffect.BackpackSlots)); } }

        public int SlotsUsed { get { return backpack.SlotsUsed; } }

        public int FreeSpace(ResourceSO r) { return r == null ? 0 : backpack.FreeSpace(r.id); }

        /// <summary>Adds items, returns how many actually fit.</summary>
        public int Add(ResourceSO r, int amount, bool notify = true)
        {
            int accepted = r == null ? 0 : backpack.Add(r.id, amount);
            if (accepted > 0) saves.MarkDirty();
            if (notify) Notify();
            return accepted;
        }

        static IdAmount[] ToIds(List<ItemAmount> list)
        {
            var ids = new IdAmount[list.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = new IdAmount(list[i].resource != null ? list[i].resource.id : "", list[i].amount);
            return ids;
        }

        public bool Has(List<ItemAmount> cost) { return backpack.Has(ToIds(cost)); }

        public bool Spend(List<ItemAmount> cost)
        {
            if (!SpendQuietly(cost)) return false;
            Notify();
            return true;
        }

        bool SpendQuietly(List<ItemAmount> cost)
        {
            if (!backpack.Spend(ToIds(cost))) return false;
            saves.MarkDirty();
            return true;
        }

        // ---------- Upgrades ----------
        public int Level(UpgradeSO u) { return progression.UpgradeLevel(u.id); }

        public bool Buy(UpgradeSO u)
        {
            int lvl = Level(u);
            if (lvl >= u.maxLevel || !SpendQuietly(u.CostAt(lvl))) return false;
            progression.SetUpgradeLevel(u.id, lvl + 1, u.maxLevel);
            Notify();
            return true;
        }

        public float EffectSum(UpgradeEffect e)
        {
            float sum = 0f;
            foreach (var u in db.upgrades) if (u != null && u.effect == e) sum += u.valuePerLevel * Level(u);
            return sum;
        }

        public int ToolLevel(MinigameKind k)
        {
            int best = 0;
            foreach (var u in db.upgrades) if (u != null && u.effect == UpgradeEffect.ToolPower && u.tool == k) best += Level(u);
            return best;
        }

        public float ToolMultiplier(MinigameKind k)
        {
            float m = 1f;
            foreach (var u in db.upgrades)
                if (u != null && u.effect == UpgradeEffect.ToolPower && u.tool == k) m += u.valuePerLevel * Level(u);
            return m;
        }

        public float ToolTolerance(MinigameKind k) { return Mathf.Min(1.6f, 1f + 0.08f * ToolLevel(k)); }
        public float ToolDamage(MinigameKind k) { return 1f + FrontierSettings.I.toolDamagePerLevel * ToolLevel(k); }

        public string ToolName(MinigameKind k)
        {
            foreach (var u in db.upgrades)
                if (u != null && u.effect == UpgradeEffect.ToolPower && u.tool == k) return u.NameAtLevel(Level(u));
            return k.ToString();
        }

        public float TurretFireRate { get { return 1f + EffectSum(UpgradeEffect.TurretFireRate); } }
        public float TapDamage { get { return 8f * (1f + EffectSum(UpgradeEffect.TapDamage)); } }
        public float ReactorMax { get { return db.baseReactorHealth + EffectSum(UpgradeEffect.ReactorHealth); } }
        public float RewardBonus { get { return 1f + EffectSum(UpgradeEffect.RewardBonus) + 0.03f * (PlayerLevel - 1); } }
        public float FuelMultiplier { get { return Mathf.Max(0.25f, 1f - EffectSum(UpgradeEffect.FuelEfficiency)); } }

        // ---------- XP ----------
        public int xp { get { return progression.Xp; } }
        public int PlayerLevel { get { return progression.Level; } }
        public float LevelProgress { get { return progression.LevelProgress; } }
        public int totalWavesCleared { get { return stats.totalWavesCleared; } }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            progression.AddXp(amount);
            Notify();
        }

        // ---------- Structures ----------
        readonly List<PlacedStructure> structureList = new List<PlacedStructure>();
        readonly Dictionary<StructureRecord, PlacedStructure> wrappers = new Dictionary<StructureRecord, PlacedStructure>();
        int structureVersion = -1;

        /// <summary>Every structure the player built, on every planet, in placement order. The objects stay the same while the structure exists.</summary>
        public IReadOnlyList<PlacedStructure> structures
        {
            get
            {
                if (structureVersion != world.Version) RefreshStructures();
                return structureList;
            }
        }

        void RefreshStructures()
        {
            structureVersion = world.Version;
            structureList.Clear();
            var live = new HashSet<StructureRecord>();
            foreach (var delta in world.All)
            {
                foreach (var record in delta.structures)
                {
                    var def = db.FindStructure(record.defId);
                    if (def == null) continue;
                    PlacedStructure placed;
                    if (!wrappers.TryGetValue(record, out placed) || placed.def != def)
                    {
                        placed = new PlacedStructure(delta.planetId, def, record);
                        wrappers[record] = placed;
                    }
                    structureList.Add(placed);
                    live.Add(record);
                }
            }
            if (wrappers.Count == live.Count) return;
            var stale = new List<StructureRecord>();
            foreach (var record in wrappers.Keys) if (!live.Contains(record)) stale.Add(record);
            foreach (var record in stale) wrappers.Remove(record);
        }

        /// <summary>The structures on one planet, a fresh list.</summary>
        public List<PlacedStructure> StructuresOn(PlanetData p)
        {
            var list = new List<PlacedStructure>();
            foreach (var s in structures) if (s.planetId == p.Id) list.Add(s);
            return list;
        }

        public bool HasBase(PlanetData p)
        {
            foreach (var s in structures) if (s.planetId == p.Id) return true;
            return false;
        }

        public PlacedStructure StructureAt(PlanetData p, int slot)
        {
            foreach (var s in structures) if (s.planetId == p.Id && s.slot == slot) return s;
            return null;
        }

        public PlacedStructure StructureOfKind(PlanetData p, StructureKind k)
        {
            foreach (var s in structures) if (s.planetId == p.Id && s.def.kind == k) return s;
            return null;
        }

        public int CountOnPlanet(PlanetData p, StructureSO def)
        {
            int n = 0;
            foreach (var s in structures) if (s.planetId == p.Id && s.def == def) n++;
            return n;
        }

        public bool HasStructure(PlanetData p, StructureSO def) { return CountOnPlanet(p, def) > 0; }

        public bool AtLimit(PlanetData p, StructureSO def) { return def.maxPerPlanet > 0 && CountOnPlanet(p, def) >= def.maxPerPlanet; }

        public int FreePads(PlanetData p)
        {
            int used = 0;
            foreach (var s in structures) if (s.planetId == p.Id && s.slot >= 0 && s.slot < p.BuildingSlots) used++;
            return Mathf.Max(0, p.BuildingSlots - used);
        }

        public List<GatherSpot> SpotsFor(PlanetData p, MinigameKind game) { return p.SpotsFor(game); }

        public List<ResourceSO> ResourcesFor(PlanetData p) { return new List<ResourceSO>(p.Resources); }

        // Gather structures need a matching resource on the planet.
        public bool CanBuildHere(StructureSO def)
        {
            return !def.kind.IsGather() || SpotsFor(planet, def.kind.Game()).Count > 0;
        }

        public bool Place(StructureSO def, int slot)
        {
            if (slot < 0 || slot >= planet.BuildingSlots || AtLimit(planet, def) || !CanBuildHere(def) || StructureAt(planet, slot) != null || !SpendQuietly(def.buildCost)) return false;
            world.Place(planet.Id, def.id, slot);
            Notify();
            return true;
        }

        public float TurretPower(PlacedStructure s)
        {
            return s.def.damage * (1f + s.def.bonusPerLevel * (s.level - 1)) * (1f + EffectSum(UpgradeEffect.TurretDamage));
        }

        public bool UpgradeStructure(PlacedStructure s)
        {
            if (!IsPlaced(s) || s.level >= s.def.maxLevel || !SpendQuietly(s.def.UpgradeCost(s.level))) return false;
            world.SetLevel(s.record, s.level + 1);
            Notify();
            return true;
        }

        bool IsPlaced(PlacedStructure s)
        {
            return s != null && world.StructureAt(s.planetId, s.slot) == s.record;
        }

        public float StructureBonus(PlacedStructure s) { return s == null ? 0f : s.def.bonusPerLevel * (s.level - 1); }

        public float AlarmBonus(PlanetData p)
        {
            return 1f + StructureBonus(StructureOfKind(p, StructureKind.Alarm));
        }

        // ---------- Warehouses ----------
        /// <summary>The contents of a placed warehouse, grown to its current capacity.</summary>
        public StorageRecord StorageFor(PlacedStructure warehouse)
        {
            if (warehouse == null || warehouse.def.kind != StructureKind.Warehouse || !IsPlaced(warehouse))
                throw new ArgumentException("Storage requires a placed warehouse.");
            return world.StorageFor(warehouse.planetId, warehouse.slot, warehouse.def.StorageCapacity(warehouse.level));
        }

        public int Deposit(PlacedStructure warehouse, int slot, ResourceSO resource, int amount, bool notify = true)
        {
            if (warehouse.planetId != planet.Id || resource == null || amount <= 0) return 0;
            var storage = StorageFor(warehouse);
            if (slot < 0 || slot >= warehouse.def.StorageCapacity(warehouse.level)) return 0;
            int moved = WarehouseOps.Deposit(backpack, storage, slot, resource.id, amount);
            if (moved <= 0) return 0;
            saves.MarkDirty();
            if (notify) Notify();
            return moved;
        }

        public int Withdraw(PlacedStructure warehouse, int slot, int amount, bool notify = true)
        {
            if (warehouse.planetId != planet.Id || amount <= 0) return 0;
            int moved = WarehouseOps.Withdraw(backpack, StorageFor(warehouse), slot, amount);
            if (moved <= 0) return 0;
            saves.MarkDirty();
            if (notify) Notify();
            return moved;
        }

        public bool MoveStoredSlot(PlacedStructure warehouse, int from, int to, bool notify = true)
        {
            var storage = StorageFor(warehouse);
            if (warehouse.planetId != planet.Id || !WarehouseOps.Move(backpack, storage, from, to)) return false;
            saves.MarkDirty();
            if (notify) Notify();
            return true;
        }

        // ---------- Travel ----------
        /// <summary>Fuel for a trip from the current world to another (0 for the world the player is on). See <see cref="ITravelCostModel"/>.</summary>
        public int FuelCost(PlanetData to) { return universe.FuelCost(planet, to, FuelMultiplier); }

        public bool Travel(PlanetData to)
        {
            if (to == null || to.Id == planet.Id) return false;
            int cost = FuelCost(to);
            if (Count(db.fuel) < cost) return false;
            SpendQuietly(new List<ItemAmount> { new ItemAmount(db.fuel, cost) });
            planet = to;
            RecordArrival(to);
            Notify();
            Flush();
            return true;
        }

        /// <summary>Visit bookkeeping for a landing: the world's first/last visit and the stats that feed the Frontier Score.</summary>
        void RecordArrival(PlanetData p)
        {
            bool first = world.RecordVisit(p.Id, clock.UtcNow);
            stats.RecordLanding(universe.DistanceFromHomeLy(p), first);
        }

        // ---------- Waves ----------
        public int NextWave(PlanetData p) { return world.NextWave(p.Id); }

        public void WaveCleared(PlanetData p)
        {
            world.RecordWaveCleared(p.Id);
            stats.RecordWaveCleared();
            Notify();
        }

        // ---------- Refinery ----------
        public int OutputAmount(ItemAmount o, float bonus) { return Mathf.Max(1, Mathf.RoundToInt(o.amount * (1f + bonus))); }

        public bool Craft(RecipeSO r, float bonus = 0f)
        {
            var outputs = new IdAmount[r.outputs.Count];
            for (int i = 0; i < outputs.Length; i++)
                outputs[i] = new IdAmount(r.outputs[i].resource != null ? r.outputs[i].resource.id : "", OutputAmount(r.outputs[i], bonus));
            if (!backpack.Craft(ToIds(r.inputs), outputs)) return false;
            Notify();
            return true;
        }
    }
}
