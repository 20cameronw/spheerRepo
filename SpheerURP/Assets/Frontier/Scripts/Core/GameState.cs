using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Frontier
{
    [Serializable]
    public class SaveData
    {
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();
        public List<string> upgradeIds = new List<string>();
        public List<int> upgradeLevels = new List<int>();
        public List<string> waveIds = new List<string>();
        public List<int> waveCounts = new List<int>();
        public List<string> structPlanets = new List<string>();
        public List<string> structIds = new List<string>();
        public List<int> structSlots = new List<int>();
        public List<int> structLevels = new List<int>();
        public int xp;
        public string planetId;
        public int totalWavesCleared;
    }

    [Serializable]
    public class PlacedStructure
    {
        public string planetId;
        public StructureSO def;
        public int slot;
        public int level = 1;
    }

    public class GameState
    {
        public static GameState I;

        public readonly GameDatabase db;
        public PlanetSO planet;
        public int totalWavesCleared;
        public int xp;
        public readonly List<PlacedStructure> structures = new List<PlacedStructure>();
        public event Action Changed;

        readonly Dictionary<ResourceSO, int> items = new Dictionary<ResourceSO, int>();
        readonly Dictionary<UpgradeSO, int> levels = new Dictionary<UpgradeSO, int>();
        readonly Dictionary<string, int> wavesCleared = new Dictionary<string, int>();

        static string SavePath { get { return Path.Combine(Application.persistentDataPath, "frontier_save.json"); } }

        public GameState(GameDatabase db)
        {
            this.db = db;
            I = this;
            if (!Load())
            {
                planet = db.startPlanet;
                foreach (var s in db.startingItems) if (s.resource != null) items[s.resource] = s.amount;
            }
        }

        // ---------- Inventory ----------
        public int Count(ResourceSO r) { int c; return items.TryGetValue(r, out c) ? c : 0; }

        static int SlotsFor(ResourceSO r, int count) { return count <= 0 ? 0 : Mathf.CeilToInt(count / (float)Mathf.Max(1, r.stackSize)); }

        public int Slots { get { return db.baseBackpackSlots + Mathf.RoundToInt(EffectSum(UpgradeEffect.BackpackSlots)); } }

        public int SlotsUsed
        {
            get { int n = 0; foreach (var kv in items) n += SlotsFor(kv.Key, kv.Value); return n; }
        }

        public int FreeSpace(ResourceSO r)
        {
            int usedByOthers = 0;
            foreach (var kv in items) if (kv.Key != r) usedByOthers += SlotsFor(kv.Key, kv.Value);
            int freeSlots = Mathf.Max(0, Slots - usedByOthers);
            return Mathf.Max(0, freeSlots * Mathf.Max(1, r.stackSize) - Count(r));
        }

        /// <summary>Adds items, returns how many actually fit.</summary>
        public int Add(ResourceSO r, int amount, bool notify = true)
        {
            int accepted = Mathf.Clamp(amount, 0, FreeSpace(r));
            if (accepted > 0) items[r] = Count(r) + accepted;
            if (notify) Notify();
            return accepted;
        }

        public bool Has(List<ItemAmount> cost)
        {
            foreach (var c in cost) if (Count(c.resource) < c.amount) return false;
            return true;
        }

        public bool Spend(List<ItemAmount> cost)
        {
            if (!Has(cost)) return false;
            foreach (var c in cost)
            {
                int left = Count(c.resource) - c.amount;
                if (left <= 0) items.Remove(c.resource); else items[c.resource] = left;
            }
            Notify();
            return true;
        }

        // ---------- Upgrades ----------
        public int Level(UpgradeSO u) { int l; return levels.TryGetValue(u, out l) ? l : 0; }

        public bool Buy(UpgradeSO u)
        {
            int lvl = Level(u);
            if (lvl >= u.maxLevel || !Spend(u.CostAt(lvl))) return false;
            levels[u] = lvl + 1;
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
        public float ToolDamage(MinigameKind k) { return 1f + 0.7f * ToolLevel(k); }

        public string ToolName(MinigameKind k)
        {
            foreach (var u in db.upgrades)
                if (u != null && u.effect == UpgradeEffect.ToolPower && u.tool == k) return u.NameAtLevel(Level(u));
            return k.ToString();
        }

        public int TurretCount { get { return 1 + Mathf.RoundToInt(EffectSum(UpgradeEffect.TurretCount)); } }
        public float TurretDamage { get { return 5f * (1f + EffectSum(UpgradeEffect.TurretDamage)); } }
        public float TapDamage { get { return 8f * (1f + EffectSum(UpgradeEffect.TapDamage)); } }
        public float ReactorMax { get { return db.baseReactorHealth + EffectSum(UpgradeEffect.ReactorHealth); } }
        public float RewardBonus { get { return 1f + EffectSum(UpgradeEffect.RewardBonus) + 0.03f * (PlayerLevel - 1); } }
        public float FuelMultiplier { get { return Mathf.Max(0.25f, 1f - EffectSum(UpgradeEffect.FuelEfficiency)); } }

        // ---------- XP ----------
        public int PlayerLevel { get { return 1 + Mathf.FloorToInt(Mathf.Sqrt(xp / 40f)); } }
        public float LevelProgress
        {
            get
            {
                int l = PlayerLevel - 1;
                float lo = 40f * l * l, hi = 40f * (l + 1) * (l + 1);
                return Mathf.Clamp01((xp - lo) / (hi - lo));
            }
        }

        public void AddXp(int amount) { if (amount <= 0) return; xp += amount; Notify(); }

        // ---------- Debug ----------
        public void DebugSetUpgrade(UpgradeSO u, int level) { levels[u] = Mathf.Clamp(level, 0, u.maxLevel); Notify(); }
        public void DebugSetLevel(int level) { xp = Mathf.RoundToInt(40f * Mathf.Max(0, level - 1) * Mathf.Max(0, level - 1)); Notify(); }
        public void DebugTeleport(PlanetSO p) { if (p == null) return; planet = p; Notify(); }

        // ---------- Structures ----------
        public PlacedStructure StructureAt(PlanetSO p, int slot)
        {
            foreach (var s in structures) if (s.planetId == p.id && s.slot == slot) return s;
            return null;
        }

        public PlacedStructure StructureOfKind(PlanetSO p, StructureKind k)
        {
            foreach (var s in structures) if (s.planetId == p.id && s.def.kind == k) return s;
            return null;
        }

        public int CountOnPlanet(PlanetSO p, StructureSO def)
        {
            int n = 0;
            foreach (var s in structures) if (s.planetId == p.id && s.def == def) n++;
            return n;
        }

        public bool HasStructure(PlanetSO p, StructureSO def) { return CountOnPlanet(p, def) > 0; }

        public bool AtLimit(PlanetSO p, StructureSO def) { return def.maxPerPlanet > 0 && CountOnPlanet(p, def) >= def.maxPerPlanet; }

        public int FreePads(PlanetSO p, int total)
        {
            int used = 0;
            foreach (var s in structures) if (s.planetId == p.id) used++;
            return total - used;
        }

        public List<GatherSpot> SpotsFor(PlanetSO p, MinigameKind game)
        {
            var list = new List<GatherSpot>();
            foreach (var sp in p.spots) if (sp.game == game && sp.resource != null) list.Add(sp);
            if (game == MinigameKind.Mine && db.scrap != null)
                list.Insert(0, new GatherSpot { label = "Surface Scrap", game = MinigameKind.Mine, resource = db.scrap, baseYield = 6, difficulty = 0.2f });
            return list;
        }

        // Gather structures need a matching resource on the planet.
        public bool CanBuildHere(StructureSO def)
        {
            return !def.kind.IsGather() || SpotsFor(planet, def.kind.Game()).Count > 0;
        }

        public bool Place(StructureSO def, int slot)
        {
            if (AtLimit(planet, def) || !CanBuildHere(def) || StructureAt(planet, slot) != null || !Spend(def.buildCost)) return false;
            structures.Add(new PlacedStructure { planetId = planet.id, def = def, slot = slot, level = 1 });
            Notify();
            return true;
        }

        public float TurretPower(PlacedStructure s)
        {
            return s.def.damage * (1f + s.def.bonusPerLevel * (s.level - 1)) * (1f + EffectSum(UpgradeEffect.TurretDamage));
        }

        public bool UpgradeStructure(PlacedStructure s)
        {
            if (s.level >= s.def.maxLevel || !Spend(s.def.UpgradeCost(s.level))) return false;
            s.level++;
            Notify();
            return true;
        }

        public float StructureBonus(PlacedStructure s) { return s == null ? 0f : s.def.bonusPerLevel * (s.level - 1); }

        public float AlarmBonus(PlanetSO p)
        {
            return 1f + StructureBonus(StructureOfKind(p, StructureKind.Alarm));
        }

        // ---------- Travel ----------
        public int FuelCost(PlanetSO to)
        {
            if (to == planet) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(PlanetSO.Distance(planet, to) * db.fuelPerMapUnit * FuelMultiplier));
        }

        public bool Travel(PlanetSO to)
        {
            int cost = FuelCost(to);
            if (to == planet || Count(db.fuel) < cost) return false;
            Spend(new List<ItemAmount> { new ItemAmount(db.fuel, cost) });
            planet = to;
            Notify();
            return true;
        }

        // ---------- Waves ----------
        public int NextWave(PlanetSO p) { int n; return (wavesCleared.TryGetValue(p.id, out n) ? n : 0) + 1; }

        public void WaveCleared(PlanetSO p)
        {
            wavesCleared[p.id] = NextWave(p);
            totalWavesCleared++;
            Notify();
        }

        // ---------- Refinery ----------
        public int OutputAmount(ItemAmount o, float bonus) { return Mathf.Max(1, Mathf.RoundToInt(o.amount * (1f + bonus))); }

        public bool Craft(RecipeSO r, float bonus = 0f)
        {
            if (!Has(r.inputs)) return false;
            var outs = new List<ItemAmount>();
            foreach (var o in r.outputs) outs.Add(new ItemAmount(o.resource, OutputAmount(o, bonus)));
            foreach (var o in outs) if (FreeSpace(o.resource) < o.amount && !CanFitAfter(r, outs, o.resource)) return false;
            Spend(r.inputs);
            foreach (var o in outs) Add(o.resource, o.amount, false);
            Notify();
            return true;
        }

        bool CanFitAfter(RecipeSO r, List<ItemAmount> outs, ResourceSO res)
        {
            int need = 0;
            foreach (var o in outs) if (o.resource == res) need += o.amount;
            int have = Count(res), inputUse = 0;
            foreach (var i in r.inputs) if (i.resource == res) inputUse += i.amount;
            int usedOthers = 0;
            foreach (var kv in items) if (kv.Key != res) usedOthers += SlotsFor(kv.Key, kv.Value);
            foreach (var i in r.inputs)
                if (i.resource != res && items.ContainsKey(i.resource))
                    usedOthers += SlotsFor(i.resource, Count(i.resource) - i.amount) - SlotsFor(i.resource, Count(i.resource));
            int cap = Mathf.Max(0, Slots - usedOthers) * Mathf.Max(1, res.stackSize);
            return have - inputUse + need <= cap;
        }

        // ---------- Persistence ----------
        public void Notify()
        {
            Save();
            if (Changed != null) Changed();
        }

        public void Save()
        {
            try
            {
                var s = new SaveData { planetId = planet != null ? planet.id : null, totalWavesCleared = totalWavesCleared };
                foreach (var kv in items) { s.itemIds.Add(kv.Key.id); s.itemCounts.Add(kv.Value); }
                foreach (var kv in levels) { s.upgradeIds.Add(kv.Key.id); s.upgradeLevels.Add(kv.Value); }
                foreach (var kv in wavesCleared) { s.waveIds.Add(kv.Key); s.waveCounts.Add(kv.Value); }
                s.xp = xp;
                foreach (var st in structures) { s.structPlanets.Add(st.planetId); s.structIds.Add(st.def.id); s.structSlots.Add(st.slot); s.structLevels.Add(st.level); }
                File.WriteAllText(SavePath, JsonUtility.ToJson(s));
            }
            catch (Exception e) { Debug.LogWarning("Frontier save failed: " + e.Message); }
        }

        bool Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return false;
                var s = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (s == null) return false;
                planet = db.FindPlanet(s.planetId) ?? db.startPlanet;
                for (int i = 0; i < s.itemIds.Count; i++)
                {
                    var r = db.FindResource(s.itemIds[i]);
                    if (r != null) items[r] = s.itemCounts[i];
                }
                for (int i = 0; i < s.upgradeIds.Count; i++)
                {
                    var u = db.FindUpgrade(s.upgradeIds[i]);
                    if (u != null) levels[u] = s.upgradeLevels[i];
                }
                for (int i = 0; i < s.waveIds.Count; i++) wavesCleared[s.waveIds[i]] = s.waveCounts[i];
                totalWavesCleared = s.totalWavesCleared;
                xp = s.xp;
                for (int i = 0; i < s.structIds.Count; i++)
                {
                    var def = db.FindStructure(s.structIds[i]);
                    if (def != null) structures.Add(new PlacedStructure { planetId = s.structPlanets[i], def = def, slot = s.structSlots[i], level = s.structLevels[i] });
                }
                return true;
            }
            catch { return false; }
        }

        public void ResetProgress()
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
            items.Clear(); levels.Clear(); wavesCleared.Clear(); structures.Clear(); xp = 0; totalWavesCleared = 0;
            planet = db.startPlanet;
            foreach (var s in db.startingItems) if (s.resource != null) items[s.resource] = s.amount;
            Notify();
        }
    }
}
