namespace Frontier.Hyperspace
{
    public sealed partial class EncounterLibrary
    {
        /// <summary>
        /// Frozen output of the solver-driven composite search (runner command "compose"): id, parts, offset of the second part in z units at 30 u/s, mirror of the second part,
        /// first tier, declared speed fraction (the measured minimum over all tiers, rounded up). Every recipe is re-proved by the unit tests, so a changed part cannot silently break one;
        /// a recipe that stops passing is deleted here or re-searched.
        /// </summary>
        static Recipe[] CompositeRecipes()
        {
            return new[]
            {
                R("gate_sequence+turret_crossfire", "gate_sequence", "turret_crossfire", 30f, true, 3, 0.9f),
                R("asteroid_storm+gate_sequence", "asteroid_storm", "gate_sequence", 0f, false, 4, 0.8f),
                R("gate_sequence+seeker_swarm", "gate_sequence", "seeker_swarm", -60f, false, 5, 0.9f),
                R("asteroid_wall+gate_sequence", "asteroid_wall", "gate_sequence", -30f, true, 5, 0.7f),
                R("gate_sequence+turret_nest", "gate_sequence", "turret_nest", 30f, false, 6, 0.9f),
                R("gate_ring_chain+drone_swarm", "gate_ring_chain", "drone_swarm", -30f, false, 8, 0.9f),
                R("gate_ring_chain+mine_belt", "gate_ring_chain", "mine_belt", -30f, true, 8, 1.0f),
                R("elite_phalanx+gate_ring_chain", "elite_phalanx", "gate_ring_chain", 0f, true, 8, 1.0f),
                R("laser_maze+seeker_swarm", "laser_maze", "seeker_swarm", 0f, false, 9, 1.0f),
                R("laser_maze+elite_escort", "laser_maze", "elite_escort", -30f, false, 9, 0.9f),
                R("laser_maze+minefield_corridor", "laser_maze", "minefield_corridor", -30f, false, 9, 0.9f),
            };
        }
    }
}
