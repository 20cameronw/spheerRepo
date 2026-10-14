using UnityEngine;

namespace Frontier.Hyperspace.View
{
    /// <summary>
    /// Normalised strength of each upgrade family of the player's ship: 0 = nothing bought (the starter ship), 1 = the family is maxed (the endgame ship).
    /// This is the stable input of <see cref="ShipStatsMapper"/>: when the new upgrade families ship, only the code that fills this struct changes
    /// (<see cref="ShipFamiliesFromGame.Read"/>), never the mapper's outputs.
    /// </summary>
    public struct ShipFamilies
    {
        /// <summary>Reactor health upgrades (today "Reactor Shield"): hull, shield charges and shield recharge.</summary>
        public float reactor;
        /// <summary>Turret damage upgrades: damage per bullet.</summary>
        public float turretDamage;
        /// <summary>Turret actuator upgrades: bullets per second.</summary>
        public float turretRate;
        /// <summary>Ion pulse (tap damage) upgrades: how many enemies a bullet pierces.</summary>
        public float pulse;
        /// <summary>Reward upgrades (today "Salvage Rig"): pickup magnet radius.</summary>
        public float reward;
        /// <summary>Engine upgrades: handling (how quickly the ship follows the finger).</summary>
        public float engine;
        /// <summary>Player level: bomb bay size.</summary>
        public int playerLevel;
    }

    /// <summary>
    /// Pure mapping from upgrade strengths to the simulation's <see cref="ShipStats"/>. Every stat interpolates linearly between
    /// <see cref="ShipStats.Starter"/> (family 0) and <see cref="ShipStats.Endgame"/> (family 1); backpack size never matters.
    /// <list type="table">
    /// <item><term>hull</term><description>reactor: 100 to 450</description></item>
    /// <item><term>shield, shieldRecharge</term><description>reactor: 1 to 5 charges, 10 s to 3.5 s per charge</description></item>
    /// <item><term>damage</term><description>turretDamage: 10 to 44</description></item>
    /// <item><term>fireRate</term><description>turretRate: 7 to 16 bullets/s</description></item>
    /// <item><term>pierce</term><description>pulse: 0 to 3</description></item>
    /// <item><term>magnet</term><description>reward: 1.5 to 4.5 u</description></item>
    /// <item><term>handling</term><description>engine: 1.0 to 1.55</description></item>
    /// <item><term>bombCapacity</term><description>player level: 1 bomb, +1 at levels 8, 16 and 24</description></item>
    /// </list>
    /// </summary>
    public static class ShipStatsMapper
    {
        public static readonly int[] BombLevels = { 8, 16, 24 };

        public static ShipStats Map(ShipFamilies f)
        {
            ShipStats a = ShipStats.Starter(), b = ShipStats.Endgame();
            float reactor = Unit(f.reactor);
            int bombs = 1;
            for (int i = 0; i < BombLevels.Length; i++) if (f.playerLevel >= BombLevels[i]) bombs++;
            return new ShipStats
            {
                hull = Mathf.Lerp(a.hull, b.hull, reactor),
                shield = Mathf.RoundToInt(Mathf.Lerp(a.shield, b.shield, reactor)),
                shieldRecharge = Mathf.Lerp(a.shieldRecharge, b.shieldRecharge, reactor),
                damage = Mathf.Lerp(a.damage, b.damage, Unit(f.turretDamage)),
                fireRate = Mathf.Lerp(a.fireRate, b.fireRate, Unit(f.turretRate)),
                pierce = Mathf.RoundToInt(Mathf.Lerp(a.pierce, b.pierce, Unit(f.pulse))),
                magnet = Mathf.Lerp(a.magnet, b.magnet, Unit(f.reward)),
                handling = Mathf.Lerp(a.handling, b.handling, Unit(f.engine)),
                bombCapacity = Mathf.Min(b.bombCapacity, bombs)
            };
        }

        static float Unit(float v) { return float.IsNaN(v) ? 0f : Mathf.Clamp01(v); }
    }

    /// <summary>Reads the upgrade families from the live game (the only code that knows today's upgrade ids and effects).</summary>
    public static class ShipFamiliesFromGame
    {
        public static ShipFamilies Read(GameState gs)
        {
            return new ShipFamilies
            {
                reactor = Strength(gs, UpgradeEffect.ReactorHealth),
                turretDamage = Strength(gs, UpgradeEffect.TurretDamage),
                turretRate = Strength(gs, UpgradeEffect.TurretFireRate),
                pulse = Strength(gs, UpgradeEffect.TapDamage),
                reward = Strength(gs, UpgradeEffect.RewardBonus),
                engine = Strength(gs, UpgradeEffect.FuelEfficiency),
                playerLevel = gs.PlayerLevel
            };
        }

        /// <summary>Bought effect divided by the effect of the fully upgraded family (0..1).</summary>
        public static float Strength(GameState gs, UpgradeEffect effect)
        {
            float max = 0f;
            foreach (var u in gs.db.upgrades)
                if (u != null && u.effect == effect) max += u.ContributionAt(u.MaxLevel, gs.db.baseBackpackSlots);
            return max <= 0f ? 0f : Mathf.Clamp01(gs.EffectSum(effect) / max);
        }
    }
}
