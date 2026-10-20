using System;
using System.Collections.Generic;
using Frontier.Economy;
using Frontier.Universe;
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
        public UniverseService universe { get; private set; }
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
            db = GameDatabase.ForRuntime(db);
            this.db = db;
            this.clock = clock ?? new SessionClock(new SystemClock());
            I = this;
            foreach (var r in db.resources) if (r != null && !string.IsNullOrEmpty(r.id)) resourceById[r.id] = r;
            universe = UniverseService.For(db);
            backpack = new Inventory(() => Slots, StackSizeOf);
            progression = new Progression(() => FrontierSettings.I.xpPerLevelBase);
            world = new WorldStore();
            stats = new PlayerStats();
            saves = new SaveService(SaveDirectory(), this.clock, BuildSave);
            InitializeObjectiveCatalog();
            InitializeAscensionRules();
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
            if (accepted > 0)
            {
                saves.MarkDirty();
                RecordAtlasResourceAcquired(r.id);
            }
            if (notify) Notify();
            return accepted;
        }

        /// <summary>Commits a rewarded harvest bonus to disk before publishing the extra inventory.</summary>
        public bool TryCommitHarvestBonus(ResourceSO resource, int amount, out int granted, out string message)
        {
            granted = 0;
            message = "";
            if (resource == null || amount <= 0 || backpack.Count(resource.id) <= 0)
            {
                message = UiText.Get("harvest.bonus.collect-first");
                return false;
            }
            if (SaveIsReadOnly)
            {
                message = saves.LastError;
                return false;
            }

            int accepted = Math.Min(amount, backpack.FreeSpace(resource.id));
            if (accepted <= 0)
            {
                message = UiText.Get("harvest.bonus.no-room");
                return false;
            }

            var candidate = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
            StackRecord stack = null;
            foreach (var entry in candidate.player.inventory)
            {
                if (entry.resourceId != resource.id) continue;
                stack = entry;
                break;
            }
            int current = stack != null ? stack.count : 0;
            if ((long)current + accepted > int.MaxValue)
            {
                message = UiText.Get("harvest.bonus.stack-full");
                return false;
            }
            if (stack == null)
                candidate.player.inventory.Add(new StackRecord { resourceId = resource.id, count = accepted });
            else stack.count += accepted;
            candidate.player.inventory.Sort((a, b) => string.CompareOrdinal(a.resourceId, b.resourceId));

            if (!saves.CommitSnapshot(candidate))
            {
                message = string.IsNullOrEmpty(saves.LastError) ? UiText.Get("harvest.bonus.save-failed") : saves.LastError;
                return false;
            }

            granted = backpack.Add(resource.id, accepted);
            if (granted != accepted)
                throw new InvalidOperationException("The committed harvest bonus no longer fits the backpack.");
            if (Changed != null) Changed();
            return true;
        }

        public void RecordHarvestRunCompleted()
        {
            stats.RecordHarvestRunCompleted();
            ObserveObjectives();
            saves.MarkDirty();
        }

        static IdAmount[] ToIds(List<ItemAmount> list)
        {
            var ids = new IdAmount[list.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = new IdAmount(list[i].resource != null ? list[i].resource.id : "", list[i].amount);
            return ids;
        }

        public bool Has(List<ItemAmount> cost) { return backpack.Has(ToIds(cost)); }

        public bool HasPlanetResources(List<ItemAmount> cost) { return HasPlanetResources(ToIds(cost)); }

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

        public bool HasPlanetResources(IList<IdAmount> cost)
        {
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var item in cost)
            {
                if (string.IsNullOrEmpty(item.Id) || item.Amount < 0) return false;
                long total;
                totals.TryGetValue(item.Id, out total);
                totals[item.Id] = total + item.Amount;
            }
            var pooled = PooledWarehouseStorages(planet);
            foreach (var item in totals)
            {
                long available = backpack.Count(item.Key);
                foreach (var storage in pooled)
                    available += WarehouseOps.Held(storage, item.Key);
                if (available < item.Value) return false;
            }
            return true;
        }

        public int PlanetCount(ResourceSO resource)
        {
            if (resource == null) return 0;
            long count = backpack.Count(resource.id);
            foreach (var storage in PooledWarehouseStorages(planet))
                count += WarehouseOps.Held(storage, resource.id);
            return (int)Math.Min(int.MaxValue, count);
        }

        public bool CargoDepotActive
        {
            get { return planet != null && StructureOfKind(planet, StructureKind.CargoDepot) != null; }
        }

        bool SpendPlanetResourcesQuietly(IList<IdAmount> cost)
        {
            if (!HasPlanetResources(cost)) return false;
            var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in cost)
            {
                int amount;
                remaining.TryGetValue(item.Id, out amount);
                remaining[item.Id] = checked(amount + item.Amount);
            }

            bool storedChanged = false;
            foreach (var item in new List<string>(remaining.Keys))
            {
                int fromPack = Math.Min(backpack.Count(item), remaining[item]);
                if (fromPack > 0)
                {
                    backpack.Remove(item, fromPack);
                    remaining[item] -= fromPack;
                }
            }
            foreach (var storage in PooledWarehouseStorages(planet))
            {
                foreach (var stack in storage.slots)
                {
                    if (stack == null || stack.count <= 0 || string.IsNullOrEmpty(stack.resourceId)) continue;
                    int needed;
                    if (!remaining.TryGetValue(stack.resourceId, out needed) || needed <= 0) continue;
                    int removed = Math.Min(needed, stack.count);
                    stack.count -= removed;
                    remaining[stack.resourceId] = needed - removed;
                    if (stack.count == 0) stack.resourceId = null;
                    storedChanged = true;
                }
            }
            saves.MarkDirty();
            if (storedChanged) world.Touch();
            return true;
        }

        List<StorageRecord> PooledWarehouseStorages(PlanetData p)
        {
            var result = new List<StorageRecord>();
            if (p == null || StructureOfKind(p, StructureKind.CargoDepot) == null) return result;
            foreach (var structure in StructuresOn(p))
            {
                if (!IsOperational(structure) || !structure.def.kind.IsStorage() ||
                    structure.def.kind == StructureKind.FuelDepot) continue;
                result.Add(StorageFor(structure));
            }
            return result;
        }

        bool TryCraftWithPlanetResources(IList<IdAmount> inputs, IList<IdAmount> outputs)
        {
            var packSpend = new List<IdAmount>();
            var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                int amount;
                remaining.TryGetValue(input.Id, out amount);
                remaining[input.Id] = checked(amount + input.Amount);
            }
            foreach (var input in remaining)
            {
                int fromPack = Math.Min(backpack.Count(input.Key), input.Value);
                if (fromPack > 0) packSpend.Add(new IdAmount(input.Key, fromPack));
            }
            if (!HasPlanetResources(inputs) || !backpack.CanFitAfterSpending(packSpend, outputs)) return false;
            if (!SpendPlanetResourcesQuietly(inputs)) return false;
            foreach (var output in outputs)
            {
                int added = backpack.Add(output.Id, output.Amount);
                if (added != output.Amount)
                    throw new InvalidOperationException("Craft output did not fit after its inputs were spent: " + output);
            }
            return true;
        }

        // ---------- Upgrades ----------
        public int Level(UpgradeSO u) { return progression.UpgradeLevel(u.id); }

        public bool Buy(UpgradeSO u)
        {
            if (!CanBuy(u)) return false;
            int lvl = Level(u);
            if (!SpendQuietly(u.CostAt(lvl))) return false;
            progression.SetUpgradeLevel(u.id, lvl + 1, u.MaxLevel);
            Notify();
            return true;
        }

        public float EffectSum(UpgradeEffect e)
        {
            float sum = 0f;
            foreach (var u in db.upgrades) if (u != null && u.effect == e) sum += u.ContributionAt(Level(u), db.baseBackpackSlots);
            return sum;
        }

        public int ToolLevel(MinigameKind k)
        {
            int best = 0;
            foreach (var u in db.upgrades) if (u != null && u.effect == UpgradeEffect.ToolPower && u.tool == k) best += Level(u);
            if (db.UsesImportedEconomy && db.toolLadders != null)
            {
                HarvestMethod method = HarvestMapping.FromMinigame(k);
                foreach (var ladder in db.toolLadders)
                    if (IsSupportedTool(ladder) && ladder.data.games != null &&
                        Array.IndexOf(ladder.data.games, method) >= 0)
                        best = Math.Max(best, ToolTier(ladder) - 1);
            }
            return best;
        }

        public float ToolMultiplier(MinigameKind k)
        {
            float m = 1f;
            foreach (var u in db.upgrades)
                if (u != null && u.effect == UpgradeEffect.ToolPower && u.tool == k) m += u.valuePerLevel * Level(u);
            return m * CatalogToolYieldMultiplier(k) * LegacyHarvestMultiplier;
        }

        public float ToolTolerance(MinigameKind k) { return Mathf.Min(1.6f, 1f + 0.08f * ToolLevel(k)); }
        public float ToolDamage(MinigameKind k) { return 1f + FrontierSettings.I.toolDamagePerLevel * ToolLevel(k); }

        public string ToolName(MinigameKind k)
        {
            if (db.toolLadders != null)
            {
                HarvestMethod method = HarvestMapping.FromMinigame(k);
                foreach (var ladder in db.toolLadders)
                {
                    if (ladder == null || ladder.data == null || ladder.data.games == null ||
                        Array.IndexOf(ladder.data.games, method) < 0) continue;
                    int tier = ToolTier(ladder);
                    if (ladder.data.tiers == null) break;
                    foreach (var row in ladder.data.tiers)
                        if (row != null && row.tier == tier) return row.displayName;
                }
            }
            foreach (var u in db.upgrades)
                if (u != null && u.effect == UpgradeEffect.ToolPower && u.tool == k) return u.NameAtLevel(Level(u));
            return k.ToString();
        }

        public float TurretFireRate { get { return 1f + EffectSum(UpgradeEffect.TurretFireRate); } }
        public float TapDamage { get { return 8f * (1f + EffectSum(UpgradeEffect.TapDamage)); } }
        public float ReactorMax
        {
            get
            {
                float baseHealth = db.baseReactorHealth + EffectSum(UpgradeEffect.ReactorHealth);
                var pylon = StructureOfKind(planet, StructureKind.ShieldPylon);
                float bonus = pylon != null && pylon.def.effectKind == "reactorHealthBonus"
                    ? Mathf.Max(0f, pylon.def.EffectAt(pylon.level)) : 0f;
                return baseHealth * (1f + bonus);
            }
        }

        public float ReactorRepairPerSecond(PlanetData p)
        {
            var bay = StructureOfKind(p, StructureKind.RepairBay);
            return bay != null && bay.def.effectKind == "reactorRegenPerSecond"
                ? Mathf.Max(0f, bay.def.EffectAt(bay.level))
                : 0f;
        }
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
            foreach (var s in structures) if (s.planetId == p.Id && s.def.kind == k && IsOperational(s)) return s;
            return null;
        }

        public float TerraformerHarvestBonus
        {
            get
            {
                float bonus = 0f;
                foreach (var structure in structures)
                {
                    if (structure.def.kind != StructureKind.Terraformer || !IsOperational(structure) ||
                        structure.def.effectKind != "globalYieldBonus") continue;
                    bonus += Mathf.Max(0f, structure.def.EffectAt(structure.level));
                }
                return bonus;
            }
        }

        public int TerraformerStageOn(PlanetData p)
        {
            var terraformer = p == null ? null : StructureOfKind(p, StructureKind.Terraformer);
            return terraformer == null ? 0 : terraformer.level;
        }

        void RecordMegaprojectStage(StructureSO definition)
        {
            if (definition != null && definition.role == StructureRole.Megaproject)
                stats.RecordMegaprojectStage();
        }

        public int CountOnPlanet(PlanetData p, StructureSO def)
        {
            int n = 0;
            foreach (var s in structures) if (s.planetId == p.Id && s.def == def) n++;
            return n;
        }

        public bool HasStructure(PlanetData p, StructureSO def)
        {
            foreach (var structure in structures)
                if (structure.planetId == p.Id && structure.def == def && IsOperational(structure)) return true;
            return false;
        }

        public bool AtLimit(PlanetData p, StructureSO def) { return def.maxPerPlanet > 0 && CountOnPlanet(p, def) >= def.maxPerPlanet; }

        public int FreePads(PlanetData p)
        {
            int used = 0;
            foreach (var s in structures) if (s.planetId == p.Id && s.slot >= 0 && s.slot < p.BuildingSlots) used++;
            int blocked = ObstaclesOn(p).Count;
            return Mathf.Max(0, p.BuildingSlots - used - blocked);
        }

        public List<GatherSpot> SpotsFor(PlanetData p, MinigameKind game) { return p.SpotsFor(game); }

        public List<GatherSpot> SpotsFor(PlanetData p, StructureKind kind)
        {
            if (p == null) return new List<GatherSpot>();
            HarvestMethod method = kind == StructureKind.Biodome
                ? HarvestMethod.Harvest
                : HarvestMapping.FromMinigame(kind.Game());
            var candidates = p.SpotsFor(HarvestMapping.ToMinigame(method));
            if (!db.UsesImportedEconomy) return candidates;

            var matching = new List<GatherSpot>();
            foreach (var spot in candidates)
            {
                if (spot.resource != null && spot.resource.harvestMethods != null &&
                    Array.IndexOf(spot.resource.harvestMethods, method) >= 0) matching.Add(spot);
            }
            return matching;
        }

        public List<ResourceSO> ResourcesFor(PlanetData p) { return new List<ResourceSO>(p.Resources); }

        // Gather structures need a matching resource on the planet.
        public bool CanBuildHere(StructureSO def)
        {
            return BuildLockReason(def) == null &&
                (!def.kind.IsGather() || SpotsFor(planet, def.kind).Count > 0) &&
                (def.kind != StructureKind.AutoHarvester || AutoHarvestSpots(planet).Count > 0);
        }

        public bool Place(StructureSO def, int slot)
        {
            if (CompleteBuilderJobs()) Notify();
            int seconds = StructureJobSeconds(def, 1);
            if (slot < 0 || slot >= planet.BuildingSlots || AtLimit(planet, def) || !CanBuildHere(def) ||
                StructureAt(planet, slot) != null || ObstacleAt(planet, slot).HasValue ||
                (seconds > 0 && AvailableBuilderCount <= 0) ||
                !SpendPlanetResourcesQuietly(ToIds(def.buildCost))) return false;
            var record = world.Place(planet.Id, def.id, slot);
            if (record == null) return false;
            if (seconds > 0) StartBuilderJob(StructureAt(planet, slot), seconds, 0);
            else
            {
                RecordAtlasStructureBuilt(def);
                RecordMegaprojectStage(def);
            }
            Notify();
            return true;
        }

        public float TurretPower(PlacedStructure s)
        {
            float mult = s.def.HasLevelRows && s.def.effectKind == "damageMult" ? s.def.EffectAt(s.level) :
                1f + s.def.bonusPerLevel * (s.level - 1);
            return s.def.damage * mult * (1f + EffectSum(UpgradeEffect.TurretDamage));
        }

        public bool UpgradeStructure(PlacedStructure s)
        {
            if (CompleteBuilderJobs()) Notify();
            if (!CanUpgradeStructure(s) || !SpendPlanetResourcesQuietly(ToIds(s.def.UpgradeCost(s.level)))) return false;
            int targetLevel = s.level + 1;
            int seconds = StructureJobSeconds(s.def, targetLevel);
            if (seconds > 0) StartBuilderJob(s, seconds, targetLevel);
            else
            {
                world.SetLevel(s.record, targetLevel);
                RecordMegaprojectStage(s.def);
            }
            Notify();
            return true;
        }

        bool IsPlaced(PlacedStructure s)
        {
            return s != null && world.StructureAt(s.planetId, s.slot) == s.record;
        }

        public float StructureBonus(PlacedStructure s)
        {
            if (s == null || !IsOperational(s)) return 0f;
            if (!s.def.HasLevelRows) return s.def.bonusPerLevel * (s.level - 1);
            // Catalog stations grow batch size, not free output. Never interpret batchSize=1 as +100% yield.
            return s.def.effectKind == "yieldBonus" || s.def.effectKind == "waveRewardBonus" ? s.def.EffectAt(s.level) : 0f;
        }

        public int StationBatchSize(PlacedStructure station)
        {
            if (station == null || !IsOperational(station) || !IsPlaced(station) ||
                station.def.effectKind != "batchSize") return 1;
            return Mathf.Max(1, Mathf.RoundToInt(station.def.EffectAt(station.level)));
        }

        public float RadarRangeBonus(PlanetData p)
        {
            var radar = p == null ? null : StructureOfKind(p, StructureKind.RadarTower);
            return radar != null && radar.def.effectKind == "surveyRadiusBonusLy" ?
                Mathf.Max(0f, radar.def.EffectAt(radar.level)) : 0f;
        }

        public float AlarmBonus(PlanetData p)
        {
            return 1f + StructureBonus(StructureOfKind(p, StructureKind.Alarm));
        }

        // ---------- Warehouses ----------
        /// <summary>The contents of a placed storage structure, grown to its current capacity.</summary>
        public StorageRecord StorageFor(PlacedStructure warehouse)
        {
            if (warehouse == null || !warehouse.def.kind.IsStorage() || !IsPlaced(warehouse))
                throw new ArgumentException("Storage requires a placed storage structure.");
            return world.StorageFor(warehouse.planetId, warehouse.slot, warehouse.def.StorageCapacity(warehouse.level));
        }

        public bool CanStore(PlacedStructure storage, ResourceSO resource)
        {
            return storage != null && storage.def.kind.IsStorage() && resource != null &&
                (storage.def.kind != StructureKind.FuelDepot || resource.fuelEnergy > 0);
        }

        int StorageStackSize(PlacedStructure storage, ResourceSO resource)
        {
            int baseSize = backpack.StackSizeOf(resource.id);
            if (storage.def.kind != StructureKind.FuelDepot || resource.fuelEnergy <= 0) return baseSize;
            return Mathf.Max(baseSize, Mathf.FloorToInt(baseSize * (1f + storage.def.EffectAt(storage.level))));
        }

        public int Deposit(PlacedStructure warehouse, int slot, ResourceSO resource, int amount, bool notify = true)
        {
            if (warehouse == null || warehouse.planetId != planet.Id || !CanStore(warehouse, resource) || amount <= 0) return 0;
            var storage = StorageFor(warehouse);
            if (slot < 0 || slot >= warehouse.def.StorageCapacity(warehouse.level)) return 0;
            int moved = WarehouseOps.Deposit(backpack, storage, slot, resource.id, amount, StorageStackSize(warehouse, resource));
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

        public int QuickStore(PlacedStructure warehouse, ResourceCategory? category = null, bool notify = true)
        {
            if (warehouse == null || warehouse.planetId != planet.Id || !IsPlaced(warehouse)) return 0;
            int moved = 0;
            int capacity = warehouse.def.StorageCapacity(warehouse.level);
            foreach (var resource in db.resources)
            {
                if (resource == null || (category.HasValue && resource.category != category.Value) ||
                    !CanStore(warehouse, resource)) continue;
                int remaining = Count(resource);
                for (int slot = 0; slot < capacity && remaining > 0; slot++)
                {
                    int deposited = Deposit(warehouse, slot, resource, remaining, false);
                    remaining -= deposited;
                    moved += deposited;
                }
            }
            if (moved > 0 && notify) Notify();
            return moved;
        }

        public int QuickWithdraw(PlacedStructure warehouse, ResourceCategory? category = null, bool notify = true)
        {
            if (warehouse == null || warehouse.planetId != planet.Id || !IsPlaced(warehouse)) return 0;
            int moved = 0;
            var storage = StorageFor(warehouse);
            for (int slot = 0; slot < storage.slots.Count; slot++)
            {
                var entry = storage.slots[slot];
                var resource = entry == null ? null : db.FindResource(entry.resourceId);
                if (resource == null || entry.count <= 0 || (category.HasValue && resource.category != category.Value)) continue;
                moved += Withdraw(warehouse, slot, entry.count, false);
            }
            if (moved > 0 && notify) Notify();
            return moved;
        }

        public bool MoveStoredSlot(PlacedStructure warehouse, int from, int to, bool notify = true)
        {
            var storage = StorageFor(warehouse);
            if (warehouse.planetId != planet.Id) return false;
            var source = from >= 0 && from < storage.slots.Count ? db.FindResource(storage.slots[from].resourceId) : null;
            var target = to >= 0 && to < storage.slots.Count ? db.FindResource(storage.slots[to].resourceId) : null;
            if (warehouse.def.kind == StructureKind.FuelDepot &&
                ((source != null && !CanStore(warehouse, source)) || (target != null && !CanStore(warehouse, target)))) return false;
            int stackLimit = source != null ? StorageStackSize(warehouse, source) : 0;
            if (!WarehouseOps.Move(backpack, storage, from, to, stackLimit)) return false;
            saves.MarkDirty();
            if (notify) Notify();
            return true;
        }

        // ---------- Travel ----------
        // Route planning, fuel payment and the ship's capabilities live in GameState.Ship.cs.

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
            if (!CanCraft(r)) return false;
            var outputs = new IdAmount[r.outputs.Count];
            for (int i = 0; i < outputs.Length; i++)
                outputs[i] = new IdAmount(r.outputs[i].resource != null ? r.outputs[i].resource.id : "", OutputAmount(r.outputs[i], bonus));
            if (!TryCraftWithPlanetResources(ToIds(r.inputs), outputs)) return false;
            RecordAtlasRecipeCrafted(r);
            foreach (var output in r.outputs)
                if (output.resource != null) RecordAtlasResourceAcquired(output.resource.id);
            Notify();
            return true;
        }

        public bool CraftBatch(RecipeSO recipe, PlacedStructure station, int batches, float bonus = 0f)
        {
            if (batches < 1 || batches > StationBatchSize(station) || RecipeLockReason(recipe, station) != null)
                return false;
            IdAmount[] inputs, outputs;
            if (!TryScaleAmounts(recipe.inputs, batches, 0f, false, out inputs) ||
                !TryScaleAmounts(recipe.outputs, batches, bonus, true, out outputs) ||
                !TryCraftWithPlanetResources(inputs, outputs)) return false;
            RecordAtlasRecipeCrafted(recipe);
            foreach (var output in recipe.outputs)
                if (output.resource != null) RecordAtlasResourceAcquired(output.resource.id);
            Notify();
            return true;
        }

        bool TryScaleAmounts(IList<ItemAmount> items, int batches, float bonus, bool scaleOutput, out IdAmount[] amounts)
        {
            amounts = null;
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item.resource == null || string.IsNullOrEmpty(item.resource.id) || item.amount <= 0) return false;
                int perCraft = scaleOutput ? OutputAmount(item, bonus) : item.amount;
                long scaled = (long)perCraft * batches;
                long total;
                totals.TryGetValue(item.resource.id, out total);
                total += scaled;
                if (total > int.MaxValue) return false;
                totals[item.resource.id] = total;
            }
            amounts = new IdAmount[totals.Count];
            int index = 0;
            foreach (var total in totals)
                amounts[index++] = new IdAmount(total.Key, (int)total.Value);
            return amounts.Length > 0;
        }
    }
}
