using System;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>
    /// What the ship can do, as far as travel and the map are concerned: jump range, engine efficiency, scanner radius and the hull plating
    /// per hazard, all as upgrade levels (the numbers live in <see cref="TravelModel"/>), plus how full the hold is. Plain data, so the
    /// route planner, the fog of war and the tests need no game objects.
    /// </summary>
    public sealed class ShipCapabilities
    {
        /// <summary>Catalog ids of the upgrades that drive travel (the economy catalog; the content package imports them as upgrade definitions).</summary>
        public const string JumpId = "jump", EngineId = "engine", ScannerId = "scanner_range";

        /// <summary>Jump drive level, 0 = no drive (home system only), up to 10 (2,000 ly per jump).</summary>
        public int jumpLevel;
        /// <summary>Engine efficiency level, 0 = stock engine, up to 10 (0.45 fuel factor).</summary>
        public int engineLevel;
        /// <summary>Scanner level, 0 = the base survey radius, up to 8.</summary>
        public int scannerLevel;
        /// <summary>Plating level per hazard in <see cref="TravelModel.HazardOrder"/> order: heat, cold, toxic, radiation, void (0 = none, up to 3).</summary>
        public readonly int[] platingLevels = new int[TravelModel.HazardCount];
        /// <summary>How full the hold is, 0 (empty) to 1 (full): a full hold burns more fuel.</summary>
        public double cargoFill;
        /// <summary>Fuel discount from structures (0 until the Hangar and Warp Gate exist).</summary>
        public double discount;
        /// <summary>When false, <see cref="TravelRules.CanLand"/> never blocks (the interim rule of the branch before the plating upgrades exist).</summary>
        public bool enforceHazardPlating = true;

        public ShipCapabilities Clone()
        {
            var c = new ShipCapabilities { jumpLevel = jumpLevel, engineLevel = engineLevel, scannerLevel = scannerLevel, cargoFill = cargoFill, discount = discount, enforceHazardPlating = enforceHazardPlating };
            Array.Copy(platingLevels, c.platingLevels, platingLevels.Length);
            return c;
        }

        public int Plating(Hazards single)
        {
            int i = TravelModel.HazardIndex(single);
            return i < 0 ? 0 : platingLevels[i];
        }

        /// <summary>
        /// Maps upgrade levels (read by catalog id, 0 for an upgrade that does not exist yet) to capabilities. The interim levels raise the
        /// jump and scanner levels to a floor so the game stays playable until the content package ships the real upgrades (see
        /// FrontierSettings.interimJumpLevel); they never lower a level the player really has.
        /// </summary>
        public static ShipCapabilities FromUpgradeLevels(Func<string, int> levelOf, double cargoFill, int interimJumpLevel, int interimScannerLevel, bool enforceHazardPlating)
        {
            var caps = new ShipCapabilities { enforceHazardPlating = enforceHazardPlating, cargoFill = Math.Max(0.0, Math.Min(1.0, cargoFill)) };
            caps.jumpLevel = Math.Max(Math.Max(0, levelOf(JumpId)), Math.Max(0, interimJumpLevel));
            caps.engineLevel = Math.Max(0, levelOf(EngineId));
            caps.scannerLevel = Math.Max(Math.Max(0, levelOf(ScannerId)), Math.Max(0, interimScannerLevel));
            for (int i = 0; i < TravelModel.HazardCount; i++) caps.platingLevels[i] = Math.Max(0, Math.Min(3, levelOf(TravelModel.HullIds[i])));
            return caps;
        }
    }
}
