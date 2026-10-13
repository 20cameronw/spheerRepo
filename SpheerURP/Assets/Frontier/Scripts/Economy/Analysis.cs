using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>What can be obtained starting from the tier-0 state, found by a fixpoint over harvests, recipes, structures, research and upgrades.</summary>
    public sealed class ReachabilityReport
    {
        public readonly HashSet<string> resources = new HashSet<string>();
        public readonly HashSet<string> structures = new HashSet<string>();
        public readonly HashSet<string> research = new HashSet<string>();
        public readonly Dictionary<string, int> structureLevel = new Dictionary<string, int>();
        public readonly Dictionary<string, int> toolTier = new Dictionary<string, int>();
        public readonly Dictionary<string, int> upgradeLevel = new Dictionary<string, int>();
        /// <summary>Iteration of the fixpoint in which each resource first became obtainable (0 = at the start).</summary>
        public readonly Dictionary<string, int> resourceRound = new Dictionary<string, int>();
        public int reachTier;
        public int rounds;
        public readonly List<string> problems = new List<string>();
    }

    /// <summary>Proves that everything the economy asks for can be produced from the starting state (no orphan or circular requirements).</summary>
    public static class ReachabilityAnalyzer
    {
        public const int UnlimitedTier = 40;

        public static ReachabilityReport Analyze(EconomyCatalog cat)
        {
            var rep = new ReachabilityReport();
            var model = new Model(cat, rep);
            model.Run();
            return rep;
        }

        sealed class Model
        {
            readonly EconomyCatalog _c;
            readonly ReachabilityReport _r;
            readonly int[] _plating = new int[TravelModel.HazardCount];
            int _round;

            public Model(EconomyCatalog c, ReachabilityReport r) { _c = c; _r = r; }

            bool Has(string id) { return _r.resources.Contains(id); }

            bool HasAll(ItemAmount[] items)
            {
                for (int i = 0; i < items.Length; i++) if (!Has(items[i].resourceId)) return false;
                return true;
            }

            int Level(string structureId) { int l; return _r.structureLevel.TryGetValue(structureId, out l) ? l : 0; }

            bool Add(string id)
            {
                if (!_r.resources.Add(id)) return false;
                _r.resourceRound[id] = _round;
                return true;
            }

            bool GatherStructureFor(HarvestMethod m)
            {
                if (m == HarvestMethod.Salvage) return true;
                for (int i = 0; i < _c.structures.Length; i++)
                {
                    StructureDef s = _c.structures[i];
                    if (s.role != StructureRole.Gather) continue;
                    for (int k = 0; k < s.gathers.Length; k++) if (s.gathers[k] == m && Level(s.id) >= 1) return true;
                }
                return false;
            }

            bool HazardsCovered(Biome biome, int tier)
            {
                Hazards h = UniverseTables.BiomeHazards(biome, tier);
                int need = tier >= 9 ? 3 : (tier >= 5 ? 2 : 1);
                for (int i = 0; i < TravelModel.HazardOrder.Length; i++)
                    if ((h & TravelModel.HazardOrder[i]) != 0 && _plating[i] < need) return false;
                return true;
            }

            public void Run()
            {
                foreach (ItemAmount s in _c.start.startingItems) Add(s.resourceId);
                for (int i = 0; i < _c.tools.Length; i++) _r.toolTier[_c.tools[i].id] = 1;
                _r.reachTier = 2;
                bool changed = true;
                while (changed && _round < 200)
                {
                    changed = false;
                    _round++;

                    // Raw resources: a habitat, a gather structure for the method, a tier we can reach, hazards we can plate against.
                    for (int i = 0; i < _c.habitats.Length; i++)
                    {
                        HabitatDef h = _c.habitats[i];
                        if (Has(h.resourceId)) continue;
                        if (h.minTier > _r.reachTier) continue;
                        if (!GatherStructureFor(h.method)) continue;
                        if (!HazardsCovered(h.biome, h.minTier)) continue;
                        changed |= Add(h.resourceId);
                    }
                    // Recipes.
                    for (int i = 0; i < _c.recipes.Length; i++)
                    {
                        RecipeDef rc = _c.recipes[i];
                        if (!HasAll(rc.inputs)) continue;
                        if (Level(rc.stationId) < rc.stationLevel) continue;
                        if (rc.unlockResearch != null && !_r.research.Contains(rc.unlockResearch)) continue;
                        for (int k = 0; k < rc.outputs.Length; k++) changed |= Add(rc.outputs[k].resourceId);
                    }
                    // Structures and their levels.
                    for (int i = 0; i < _c.structures.Length; i++)
                    {
                        StructureDef s = _c.structures[i];
                        int have = Level(s.id);
                        while (have < s.maxLevel)
                        {
                            StructureLevelDef d = s.levels[have];
                            if (!HasAll(d.cost)) break;
                            if (d.requiresReachTier > _r.reachTier) break;
                            if (have == 0 && s.unlockResearch != null && !_r.research.Contains(s.unlockResearch)) break;
                            if (d.requiresLabLevel > Level("research_lab")) break;
                            have++;
                            _r.structureLevel[s.id] = have;
                            _r.structures.Add(s.id);
                            changed = true;
                        }
                    }
                    // Research nodes.
                    for (int i = 0; i < _c.research.Length; i++)
                    {
                        ResearchNodeDef n = _c.research[i];
                        if (_r.research.Contains(n.id)) continue;
                        if (!HasAll(n.cost)) continue;
                        if (Level("research_lab") < n.labLevel) continue;
                        bool pre = true;
                        for (int k = 0; k < n.prereqs.Length; k++) if (!_r.research.Contains(n.prereqs[k])) pre = false;
                        if (!pre) continue;
                        _r.research.Add(n.id);
                        changed = true;
                    }
                    // Tool tiers.
                    for (int i = 0; i < _c.tools.Length; i++)
                    {
                        ToolLadderDef l = _c.tools[i];
                        int have = _r.toolTier[l.id];
                        while (have < 7)
                        {
                            ToolTierDef d = l.tiers[have];
                            if (!HasAll(d.cost)) break;
                            if (d.researchPrereq != null && !_r.research.Contains(d.researchPrereq)) break;
                            if (d.requiresReachTier > _r.reachTier) break;
                            if (d.requiresLabLevel > Level("research_lab")) break;
                            have++;
                            _r.toolTier[l.id] = have;
                            changed = true;
                        }
                    }
                    // Ship, defense and logistics upgrades.
                    for (int i = 0; i < _c.upgrades.Length; i++)
                    {
                        UpgradeDef u = _c.upgrades[i];
                        int have;
                        _r.upgradeLevel.TryGetValue(u.id, out have);
                        while (have < u.maxLevel)
                        {
                            UpgradeLevelDef d = u.levels[have];
                            if (!HasAll(d.cost)) break;
                            if (d.researchPrereq != null && !_r.research.Contains(d.researchPrereq)) break;
                            if (d.requiresReachTier > _r.reachTier) break;
                            have++;
                            _r.upgradeLevel[u.id] = have;
                            changed = true;
                            if (u.effect == EffectKind.HullPlating)
                            {
                                int hi = TravelModel.HazardIndex(u.hazard);
                                if (hi >= 0) _plating[hi] = have;
                            }
                        }
                    }
                    // Reach: once a jump drive exists every tier can be walked to.
                    int jump;
                    _r.upgradeLevel.TryGetValue("jump", out jump);
                    int newReach = jump >= 1 ? UnlimitedTier : 2;
                    if (newReach != _r.reachTier) { _r.reachTier = newReach; changed = true; }
                }
                _r.rounds = _round;
                Report();
            }

            void Report()
            {
                for (int i = 0; i < _c.resources.Length; i++) if (!_r.resources.Contains(_c.resources[i].id)) _r.problems.Add("resource " + _c.resources[i].id + " is not obtainable");
                for (int i = 0; i < _c.structures.Length; i++)
                    if (Level(_c.structures[i].id) < _c.structures[i].maxLevel) _r.problems.Add("structure " + _c.structures[i].id + " stops at level " + Level(_c.structures[i].id) + " of " + _c.structures[i].maxLevel);
                for (int i = 0; i < _c.research.Length; i++) if (!_r.research.Contains(_c.research[i].id)) _r.problems.Add("research " + _c.research[i].id + " is not obtainable");
                for (int i = 0; i < _c.tools.Length; i++) if (_r.toolTier[_c.tools[i].id] < 7) _r.problems.Add("tool " + _c.tools[i].id + " stops at tier " + _r.toolTier[_c.tools[i].id]);
                for (int i = 0; i < _c.upgrades.Length; i++)
                {
                    int l;
                    _r.upgradeLevel.TryGetValue(_c.upgrades[i].id, out l);
                    if (l < _c.upgrades[i].maxLevel) _r.problems.Add("upgrade " + _c.upgrades[i].id + " stops at level " + l + " of " + _c.upgrades[i].maxLevel);
                }
                for (int i = 0; i < _c.recipes.Length; i++)
                {
                    RecipeDef rc = _c.recipes[i];
                    bool ok = HasAll(rc.inputs) && Level(rc.stationId) >= rc.stationLevel && (rc.unlockResearch == null || _r.research.Contains(rc.unlockResearch));
                    if (!ok) _r.problems.Add("recipe " + rc.id + " cannot be run");
                }
            }
        }
    }

    /// <summary>Structural checks of a catalog (ids, references, tiers, text). Returns human readable problems; empty means clean.</summary>
    public static class CatalogValidator
    {
        static void Used(HashSet<string> set, ItemAmount[] items) { for (int i = 0; i < items.Length; i++) set.Add(items[i].resourceId); }

        public static HashSet<string> UsedResources(EconomyCatalog c)
        {
            var used = new HashSet<string>();
            for (int i = 0; i < c.recipes.Length; i++) Used(used, c.recipes[i].inputs);
            for (int i = 0; i < c.structures.Length; i++) for (int l = 0; l < c.structures[i].levels.Length; l++) Used(used, c.structures[i].levels[l].cost);
            for (int i = 0; i < c.tools.Length; i++) for (int l = 0; l < c.tools[i].tiers.Length; l++) Used(used, c.tools[i].tiers[l].cost);
            for (int i = 0; i < c.upgrades.Length; i++) for (int l = 0; l < c.upgrades[i].levels.Length; l++) Used(used, c.upgrades[i].levels[l].cost);
            for (int i = 0; i < c.research.Length; i++) Used(used, c.research[i].cost);
            for (int i = 0; i < c.resources.Length; i++) if (c.resources[i].fuelEnergy > 0) used.Add(c.resources[i].id);
            return used;
        }

        static void CheckItems(EconomyCatalog c, ItemAmount[] items, string where, List<string> problems)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (c.Resource(items[i].resourceId) == null) problems.Add(where + " references unknown resource '" + items[i].resourceId + "'");
                if (items[i].amount <= 0) problems.Add(where + " has a non-positive amount of " + items[i].resourceId);
                for (int k = i + 1; k < items.Length; k++) if (items[k].resourceId == items[i].resourceId) problems.Add(where + " lists " + items[i].resourceId + " twice");
            }
        }

        static void Unique(string kind, IEnumerable<string> ids, HashSet<string> global, List<string> problems)
        {
            var local = new HashSet<string>();
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id)) { problems.Add(kind + " with empty id"); continue; }
                if (!local.Add(id)) problems.Add("duplicate " + kind + " id " + id);
                if (!global.Add(id)) problems.Add(kind + " id " + id + " collides with an id of another kind");
                foreach (char ch in id) if (!((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_')) { problems.Add(kind + " id " + id + " is not lower_snake"); break; }
            }
        }

        static bool IsHex(string s)
        {
            if (s == null || s.Length != 7 || s[0] != '#') return false;
            for (int i = 1; i < 7; i++) { char ch = s[i]; if (!((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f'))) return false; }
            return true;
        }

        static bool PlainText(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            for (int i = 0; i < s.Length; i++) if (s[i] > 126 || (s[i] < 32)) return false;
            return true;
        }

        public static List<string> Validate(EconomyCatalog c)
        {
            var problems = new List<string>();
            var global = new HashSet<string>();
            var ids = new List<string>();
            foreach (var r in c.resources) ids.Add(r.id);
            Unique("resource", ids, global, problems);
            var other = new HashSet<string>();
            ids.Clear(); foreach (var r in c.recipes) ids.Add(r.id); Unique("recipe", ids, other, problems);
            var g2 = new HashSet<string>();
            ids.Clear(); foreach (var s in c.structures) ids.Add(s.id); Unique("structure", ids, g2, problems);
            ids.Clear(); foreach (var t in c.tools) ids.Add(t.id); Unique("tool", ids, g2, problems);
            ids.Clear(); foreach (var u in c.upgrades) ids.Add(u.id); Unique("upgrade", ids, g2, problems);
            ids.Clear(); foreach (var n in c.research) ids.Add(n.id); Unique("research", ids, g2, problems);

            var icons = new HashSet<string>();
            foreach (ResourceDef r in c.resources)
            {
                if (r.stackSize < 1) problems.Add(r.id + ": stack size");
                if (!(r.baseValue > 0f)) problems.Add(r.id + ": base value");
                if (r.tier < 0) problems.Add(r.id + ": tier");
                if (!IsHex(r.colorHex) || !IsHex(r.accentHex)) problems.Add(r.id + ": colour hex");
                if (!PlainText(r.atlasDescription) || !PlainText(r.iconConcept) || !PlainText(r.displayName)) problems.Add(r.id + ": text must be plain ASCII");
                if (!icons.Add(r.iconConcept)) problems.Add(r.id + ": icon concept is not unique");
                if (r.category == ResourceCategory.Raw && (r.harvestMethods == null || r.harvestMethods.Length == 0)) problems.Add(r.id + ": raw resource without harvest method");
                if (r.category != ResourceCategory.Raw && r.harvestMethods != null) problems.Add(r.id + ": crafted resource with a harvest method");
                if (r.category == ResourceCategory.Energy && r.fuelEnergy < 2) problems.Add(r.id + ": energy resource without fuel energy");
            }
            foreach (RecipeDef r in c.recipes)
            {
                CheckItems(c, r.inputs, "recipe " + r.id + " inputs", problems);
                CheckItems(c, r.outputs, "recipe " + r.id + " outputs", problems);
                if (c.Structure(r.stationId) == null) problems.Add("recipe " + r.id + " station " + r.stationId + " is not a structure");
                else if (r.stationLevel < 1 || r.stationLevel > c.Structure(r.stationId).maxLevel) problems.Add("recipe " + r.id + " station level");
                if (r.unlockResearch != null && c.Research(r.unlockResearch) == null) problems.Add("recipe " + r.id + " research " + r.unlockResearch + " missing");
                int maxIn = 0;
                foreach (ItemAmount a in r.inputs) { ResourceDef rd = c.Resource(a.resourceId); if (rd != null && rd.tier > maxIn) maxIn = rd.tier; }
                foreach (ItemAmount a in r.outputs)
                {
                    ResourceDef rd = c.Resource(a.resourceId);
                    if (rd != null && rd.category == ResourceCategory.Raw && rd.id != "stone") problems.Add("recipe " + r.id + " makes raw resource " + rd.id);
                    // Only the primary recipe (the one that prices the item) must not output something below its own inputs.
                    List<RecipeDef> makers = rd == null ? null : c.RecipesFor(rd.id);
                    if (rd != null && rd.category != ResourceCategory.Raw && makers.Count > 0 && makers[0] == r && rd.tier < maxIn)
                        problems.Add("recipe " + r.id + " output " + rd.id + " (tier " + rd.tier + ") is below its inputs (tier " + maxIn + ")");
                }
            }
            foreach (StructureDef s in c.structures)
            {
                if (s.levels.Length != s.maxLevel) problems.Add(s.id + ": level rows");
                if (s.footprint < 1 || s.footprint > 6) problems.Add(s.id + ": footprint");
                if (s.unlockResearch != null && c.Research(s.unlockResearch) == null) problems.Add("structure " + s.id + " research " + s.unlockResearch + " missing");
                if (!PlainText(s.description)) problems.Add(s.id + ": description");
                foreach (StructureLevelDef d in s.levels) CheckItems(c, d.cost, "structure " + s.id + " L" + d.level, problems);
            }
            foreach (ToolLadderDef l in c.tools)
            {
                if (l.tiers.Length != 7) problems.Add(l.id + ": needs 7 tiers");
                foreach (ToolTierDef d in l.tiers)
                {
                    CheckItems(c, d.cost, "tool " + l.id + " T" + d.tier, problems);
                    if (d.researchPrereq != null && c.Research(d.researchPrereq) == null) problems.Add("tool " + l.id + " T" + d.tier + " research " + d.researchPrereq + " missing");
                }
            }
            foreach (UpgradeDef u in c.upgrades)
                foreach (UpgradeLevelDef d in u.levels)
                {
                    CheckItems(c, d.cost, "upgrade " + u.id + " L" + d.level, problems);
                    if (d.researchPrereq != null && c.Research(d.researchPrereq) == null) problems.Add("upgrade " + u.id + " L" + d.level + " research " + d.researchPrereq + " missing");
                }
            foreach (ResearchNodeDef n in c.research)
            {
                CheckItems(c, n.cost, "research " + n.id, problems);
                foreach (string p in n.prereqs) if (c.Research(p) == null) problems.Add("research " + n.id + " prereq " + p + " missing");
                foreach (string u in n.unlocks)
                {
                    string[] f = u.Split(':');
                    bool ok = false;
                    if (f[0] == "structure") ok = c.Structure(f[1]) != null;
                    else if (f[0] == "recipe") ok = c.Recipe(f[1]) != null;
                    else if (f[0] == "tool") ok = f[1] == "*" || c.Tool(f[1]) != null;
                    else if (f[0] == "upgrade") ok = c.Upgrade(f[1]) != null;
                    else if (f[0] == "feature") ok = f[1].Length > 0;
                    if (!ok) problems.Add("research " + n.id + " unlocks unknown '" + u + "'");
                }
            }
            foreach (HabitatDef h in c.habitats)
            {
                ResourceDef r = c.Resource(h.resourceId);
                if (r == null) { problems.Add("habitat references unknown resource " + h.resourceId); continue; }
                if (r.category != ResourceCategory.Raw) problems.Add("habitat lists non-raw resource " + r.id);
                if (h.minTier < r.tier) problems.Add("habitat " + h.biome + "/" + r.id + " min tier below the resource tier");
                bool allowed = false;
                foreach (HarvestMethod m in r.harvestMethods) if (m == h.method) allowed = true;
                if (!allowed) problems.Add("habitat " + h.biome + "/" + r.id + " uses method " + h.method + " which the resource does not allow");
                if (!(h.weight > 0f)) problems.Add("habitat weight");
            }
            foreach (ResourceDef r in c.resources)
            {
                if (r.category != ResourceCategory.Raw) continue;
                int profiles = 0;
                foreach (HabitatDef h in c.habitats) if (h.resourceId == r.id) profiles++;
                if (profiles < 1) problems.Add("raw resource " + r.id + " has no habitat");
            }
            foreach (LegacyIdEntry e in c.legacyIds)
            {
                bool ok = e.kind == LegacyKind.Resource ? c.Resource(e.id) != null : (e.kind == LegacyKind.Structure ? c.Structure(e.id) != null : (c.Upgrade(e.id) != null || c.Tool(e.id) != null));
                if (!ok) problems.Add("legacy id " + e.legacyId + " maps to unknown " + e.id);
            }
            ids.Clear(); foreach (ObstacleKindDef k in c.obstacles) ids.Add(k.id); Unique("obstacle", ids, new HashSet<string>(), problems);
            foreach (ObstacleKindDef k in c.obstacles)
            {
                if (c.Resource(k.rewardResource) == null) problems.Add("obstacle " + k.id + " reward resource " + k.rewardResource + " missing");
                if (k.byTier == null || k.byTier.Length != c.obstacleRules.maxTier + 1) { problems.Add("obstacle " + k.id + " tier rows"); continue; }
                foreach (ObstacleTierCost tc in k.byTier)
                {
                    CheckItems(c, tc.cost, "obstacle " + k.id + " T" + tc.tier, problems);
                    CheckItems(c, new[] { tc.reward }, "obstacle " + k.id + " T" + tc.tier + " reward", problems);
                }
                if (!PlainText(k.description) || !PlainText(k.iconConcept) || !PlainText(k.displayName)) problems.Add("obstacle " + k.id + ": text must be plain ASCII");
            }
            HashSet<string> used = UsedResources(c);
            foreach (ResourceDef r in c.resources) if (!used.Contains(r.id)) problems.Add("resource " + r.id + " is never used (orphan)");
            return problems;
        }
    }
}
