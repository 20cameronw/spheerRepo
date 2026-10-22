using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>
    /// The value model: raw resources have anchor prices (scrap-equivalents), crafted resources are priced from their PRIMARY recipe
    /// (the first recipe in the catalog that makes them) plus a station premium. Every other recipe must stay inside the conservation
    /// band checked by the tests, so no recipe mints free value.
    /// </summary>
    public static class ValueModel
    {
        public static readonly string[] Stations = { "refinery", "foundry", "fabricator", "chem_reactor", "quantum_lab" };

        public static void RegisterKeys(Tuning t)
        {
            t.Register("value", "value.premium.refinery", 0.10, 0, 1, "Value added by a Refinery recipe (output price = input price x (1 + premium)).");
            t.Register("value", "value.premium.foundry", 0.20, 0, 1, "Value added by a Foundry recipe.");
            t.Register("value", "value.premium.fabricator", 0.25, 0, 1, "Value added by a Fabricator recipe.");
            t.Register("value", "value.premium.chem_reactor", 0.30, 0, 1, "Value added by a Chem Reactor recipe.");
            t.Register("value", "value.premium.quantum_lab", 0.35, 0, 1, "Value added by a Quantum Lab recipe.");
            t.Register("value", "value.max_ratio.base", 1.25, 1.0, 3.0, "Conservation limit for tier 0 recipes: value out / value in (bonus recipes are exempt).");
            t.Register("value", "value.max_ratio.per_tier", 0.03, 0, 0.2, "Conservation limit grows by this per recipe tier.");
            t.Register("value", "value.min_ratio", 0.20, 0, 1.0, "Lowest allowed value out / value in (lossy recipes such as the scrap burner stay above this).");
        }

        public static double Premium(string stationId, Tuning t)
        {
            return t.D("value.premium." + stationId);
        }

        /// <summary>Upper bound of value out / value in for a recipe of this tier.</summary>
        public static double MaxRatio(int tier, Tuning t)
        {
            return t.D("value.max_ratio.base") + t.D("value.max_ratio.per_tier") * tier;
        }

        /// <summary>Fills <see cref="ResourceDef.baseValue"/> of every crafted resource from its primary recipe. Raw anchors must be set.</summary>
        public static void Derive(ResourceDef[] resources, RecipeDef[] recipes, Tuning t)
        {
            var index = new Dictionary<string, int>();
            for (int i = 0; i < resources.Length; i++) index[resources[i].id] = i;
            var state = new int[resources.Length];
            for (int i = 0; i < resources.Length; i++) Compute(i, resources, recipes, index, state, t);
        }

        static double Compute(int i, ResourceDef[] resources, RecipeDef[] recipes, Dictionary<string, int> index, int[] state, Tuning t)
        {
            ResourceDef r = resources[i];
            if (state[i] == 2) return r.baseValue;
            if (state[i] == 1) throw new InvalidOperationException("recipe cycle through " + r.id);
            if (r.category == ResourceCategory.Raw)
            {
                if (!(r.baseValue > 0f)) throw new InvalidOperationException("raw resource " + r.id + " has no anchor value");
                state[i] = 2;
                return r.baseValue;
            }
            state[i] = 1;
            RecipeDef primary = null;
            for (int k = 0; k < recipes.Length && primary == null; k++)
                if (recipes[k].outputs.Length > 0 && recipes[k].outputs[0].resourceId == r.id) primary = recipes[k];
            if (primary == null) throw new InvalidOperationException("crafted resource " + r.id + " has no recipe");
            double input = 0.0;
            for (int k = 0; k < primary.inputs.Length; k++)
            {
                int j;
                if (!index.TryGetValue(primary.inputs[k].resourceId, out j)) throw new InvalidOperationException("recipe " + primary.id + " uses unknown " + primary.inputs[k].resourceId);
                input += Compute(j, resources, recipes, index, state, t) * primary.inputs[k].amount;
            }
            double v = input * (1.0 + Premium(primary.stationId, t)) / primary.outputs[0].amount;
            r.baseValue = CostMath.F(v, 2);
            state[i] = 2;
            return r.baseValue;
        }
    }

    /// <summary>Prices and tier-0 volumes of every resource, read from a finished catalog.</summary>
    public sealed class PriceBook
    {
        readonly Dictionary<string, double> _value = new Dictionary<string, double>();
        readonly Dictionary<string, double> _t0 = new Dictionary<string, double>();
        readonly Dictionary<string, RecipeDef> _primary = new Dictionary<string, RecipeDef>();

        public PriceBook(ResourceDef[] resources, RecipeDef[] recipes)
        {
            for (int i = 0; i < resources.Length; i++) _value[resources[i].id] = resources[i].baseValue;
            for (int k = 0; k < recipes.Length; k++)
            {
                string o = recipes[k].outputs[0].resourceId;
                if (!_primary.ContainsKey(o)) _primary[o] = recipes[k];
            }
            for (int i = 0; i < resources.Length; i++) ComputeT0(resources[i].id, 0);
        }

        double ComputeT0(string id, int depth)
        {
            double v;
            if (_t0.TryGetValue(id, out v)) return v;
            if (depth > 32) throw new InvalidOperationException("recipe depth too large at " + id);
            if (id == "wood" || id == "stone" || id == "scrap") { _t0[id] = 1.0; return 1.0; }
            RecipeDef r;
            if (!_primary.TryGetValue(id, out r)) { _t0[id] = 0.0; return 0.0; }
            double sum = 0.0;
            for (int k = 0; k < r.inputs.Length; k++) sum += ComputeT0(r.inputs[k].resourceId, depth + 1) * r.inputs[k].amount;
            v = sum / r.outputs[0].amount;
            _t0[id] = v;
            return v;
        }

        public IDictionary<string, double> Prices { get { return _value; } }

        public double Value(string id)
        {
            double v;
            if (!_value.TryGetValue(id, out v)) throw new KeyNotFoundException("no price for " + id);
            return v;
        }

        public double Value(ItemAmount[] items)
        {
            double s = 0.0;
            for (int i = 0; i < items.Length; i++) s += Value(items[i].resourceId) * items[i].amount;
            return s;
        }

        /// <summary>Wood-equivalents of tier-0 material (wood, stone, scrap and what planks, fuel, steel ... are made of) in one unit.</summary>
        public double T0Volume(string id)
        {
            double v;
            return _t0.TryGetValue(id, out v) ? v : 0.0;
        }

        public double T0Volume(ItemAmount[] items)
        {
            double s = 0.0;
            for (int i = 0; i < items.Length; i++) s += T0Volume(items[i].resourceId) * items[i].amount;
            return s;
        }

        /// <summary>The recipe that defines the item's price and tier-0 volume (null for raw resources).</summary>
        public RecipeDef PrimaryRecipe(string id)
        {
            RecipeDef r;
            return _primary.TryGetValue(id, out r) ? r : null;
        }
    }
}
