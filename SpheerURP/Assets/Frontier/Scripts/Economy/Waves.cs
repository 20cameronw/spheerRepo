using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>The outcome of one called wave, before random rounding.</summary>
    public struct WaveReward
    {
        public double threatBudget;
        public double valueBurned;
        public double valueReturned;
        public double coresExpected;
        public double fuelRefundExpected;
        public int xp;
        public int seconds;
    }

    /// <summary>
    /// Called waves: the fuel burned in the Alarm Beacon sets the threat budget (the game's formula) and the reward is the burned value
    /// times an efficiency of about 1.3 to 2, mostly in Xeno Cores. Burning more never lowers the reward (tested); the efficiency is
    /// capped so waves stay a convenient core faucet and never beat harvesting by a wide margin.
    /// </summary>
    public sealed class WaveEconomy
    {
        readonly double _baseBudget, _linear, _growth, _dangerWeight, _fuelBase, _fuelPer;
        readonly double _baseEff, _effPerWave, _effCapWaves, _effPerTier, _bonusCap, _coreShare, _refundShare;
        readonly double _secBase, _secPerEnergy, _secMax, _xpPerThreat, _maxEnergy, _defenseK;

        public WaveEconomy(Tuning t)
        {
            _baseBudget = t.D("wave.base_budget");
            _linear = t.D("wave.linear_per_wave");
            _growth = t.D("wave.growth_per_wave");
            _dangerWeight = t.D("wave.danger_weight");
            _fuelBase = t.D("wave.fuel_budget_base");
            _fuelPer = t.D("wave.fuel_budget_per");
            _baseEff = t.D("wave.base_eff");
            _effPerWave = t.D("wave.eff_per_wave");
            _effCapWaves = t.D("wave.eff_cap_waves");
            _effPerTier = t.D("wave.eff_per_tier");
            _bonusCap = t.D("wave.bonus_cap");
            _coreShare = t.D("wave.core_share");
            _refundShare = t.D("wave.refund_share");
            _secBase = t.D("wave.seconds_base");
            _secPerEnergy = t.D("wave.seconds_per_energy");
            _secMax = t.D("wave.seconds_max");
            _xpPerThreat = t.D("wave.xp_per_threat");
            _maxEnergy = t.D("wave.max_energy");
            _defenseK = t.D("wave.defense_k");
        }

        public static void RegisterKeys(Tuning t)
        {
            t.Register("wave", "wave.base_budget", 5, 0.5, 100, "Threat budget of wave 1 at danger 0 (legacy 5).");
            t.Register("wave", "wave.linear_per_wave", 2, 0, 20, "Budget added per wave number (legacy 2).");
            t.Register("wave", "wave.growth_per_wave", 1.12, 1.0, 1.5, "Budget growth per wave number (legacy 1.12).");
            t.Register("wave", "wave.danger_weight", 0.5, 0, 3, "Budget x (1 + danger x weight) (legacy 0.5).");
            t.Register("wave", "wave.fuel_budget_base", 0.5, 0, 5, "Budget x (base + per x energy burned) (legacy 0.5).");
            t.Register("wave", "wave.fuel_budget_per", 0.25, 0, 5, "Budget growth per energy unit burned (legacy 0.25).");
            t.Register("wave", "wave.base_eff", 1.30, 0.5, 5, "Value returned per value of fuel burned at wave 1.");
            t.Register("wave", "wave.eff_per_wave", 0.03, 0, 0.5, "Efficiency gained per wave cleared on the world.");
            t.Register("wave", "wave.eff_cap_waves", 20, 1, 1000, "Waves after which the per-wave gain stops.");
            t.Register("wave", "wave.eff_per_tier", 0.04, 0, 0.5, "Efficiency gained per planet tier.");
            t.Register("wave", "wave.bonus_cap", 1.0, 0, 5, "Largest total bonus from Alarm Beacon, Salvage Rig and player level.");
            t.Register("wave", "wave.core_share", 0.70, 0, 1, "Share of the returned value paid in Xeno Cores.");
            t.Register("wave", "wave.refund_share", 0.20, 0, 1, "Share of the returned value paid back as fuel.");
            t.Register("wave", "wave.seconds_base", 40, 5, 600, "Seconds of the shortest wave.");
            t.Register("wave", "wave.seconds_per_energy", 0.8, 0, 10, "Extra seconds per energy unit burned.");
            t.Register("wave", "wave.seconds_max", 150, 20, 900, "Longest wave.");
            t.Register("wave", "wave.xp_per_threat", 6, 0, 100, "XP per threat unit (legacy 6).");
            t.Register("wave", "wave.max_energy", 400, 1, 100000, "Most energy units one wave may burn.");
            t.Register("wave", "wave.defense_k", 2.0, 0.1, 100, "A wave is winnable when its threat budget is at most defense rating x k.");
        }

        public double MaxEnergy { get { return _maxEnergy; } }

        public double DefenseK { get { return _defenseK; } }

        /// <summary>Threat budget of a wave: (base + linear x (w-1)) x growth^(w-1) x (1 + danger x weight) x (fuelBase + fuelPer x energy).</summary>
        public double Budget(int wave, double danger, double energyBurned)
        {
            int n = Math.Max(0, wave - 1);
            return (_baseBudget + _linear * n) * DetMath.Pow(_growth, n) * (1.0 + danger * _dangerWeight) * (_fuelBase + _fuelPer * Math.Max(1.0, Math.Min(energyBurned, _maxEnergy)));
        }

        /// <summary>Efficiency (value returned per value burned) with every bonus applied.</summary>
        public double Efficiency(int wave, int tier, double alarmBonus, double salvageBonus, int playerLevel)
        {
            double eff = _baseEff + _effPerWave * Math.Min(Math.Max(0, wave - 1), _effCapWaves) + _effPerTier * tier;
            double bonus = alarmBonus + salvageBonus + 0.03 * Math.Max(0, playerLevel - 1);
            if (bonus > _bonusCap) bonus = _bonusCap;
            return eff * (1.0 + bonus);
        }

        public WaveReward Reward(int wave, double energyBurned, double valueBurned, double danger, int tier, double alarmBonus, double salvageBonus, int playerLevel, double coreValue, double fuelUnitValue)
        {
            var r = new WaveReward();
            double e = Math.Max(1.0, Math.Min(energyBurned, _maxEnergy));
            r.threatBudget = Budget(wave, danger, e);
            r.valueBurned = valueBurned;
            r.valueReturned = valueBurned * Efficiency(wave, tier, alarmBonus, salvageBonus, playerLevel);
            r.coresExpected = r.valueReturned * _coreShare / coreValue;
            r.fuelRefundExpected = r.valueReturned * _refundShare / fuelUnitValue;
            r.xp = Math.Max(1, CostMath.RoundInt(r.threatBudget * _xpPerThreat * (1.0 + alarmBonus)));
            r.seconds = CostMath.RoundInt(Math.Min(_secMax, _secBase + _secPerEnergy * e));
            return r;
        }
    }
}
