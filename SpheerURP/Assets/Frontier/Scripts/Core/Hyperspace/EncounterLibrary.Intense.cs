namespace Frontier.Hyperspace
{
    public sealed partial class EncounterLibrary
    {
        // ---------------------------------------------------------------- dense late-game formations
        void BuildIntense()
        {
            var drain = new TemplateBuilder("drone_rain", 3, 12, 14f, EncounterRole.Build, EncounterFamily.Drones, 0.5f).Notes("Twelve drones fall through the play area from above in a scattered stream.");
            for (int i = 0; i < 12; i++) drain.DroneDrift((float)(2.3f * HMath.Sin(1.9 * (double)i)), 0.8f, (float)(9.0 * (double)i), 0f, -1.5f);
            Register(drain);

            var wave = new TemplateBuilder("zigzag_wave_dense", 5, 12, 18f, EncounterRole.Peak, EncounterFamily.Drones, 0.65f).Notes("Two interleaved snakes of zigzaggers.");
            for (int i = 0; i < 12; i++) wave.Zig(0f, 1.0f, (float)(8.0 * (double)i), 1.9f, 2.2f, (float)(0.55 * (double)i + ((i & 1) == 0 ? 0f : HMath.Pi)));
            Register(wave);

            var swarm = new TemplateBuilder("drone_swarm", 4, 12, 20f, EncounterRole.Peak, EncounterFamily.Drones, 0.6f).Notes("Three swaying ranks of seven drones: nowhere to hide but over or under.");
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 7; c++) swarm.Zig((float)(-2.4 + 0.8 * (double)c), 0.5f, (float)(24.0 * (double)r), 0.9f, 1.4f, (r & 1) == 0 ? 0f : HMath.Pi);
            Register(swarm);

            var wall = new TemplateBuilder("asteroid_wall", 5, 12, 18f, EncounterRole.Peak, EncounterFamily.Asteroids, 0.65f).Notes("Sixteen rocks in staggered layers; every sixth one is huge.");
            for (int i = 0; i < 16; i++)
            {
                AsteroidSize sz = (i % 6 == 0) ? AsteroidSize.Large : ((i % 2 == 0) ? AsteroidSize.Medium : AsteroidSize.Small);
                wall.Asteroid(sz, (float)(-2.4 + (double)((i * 7) % 11) * 0.48), (float)(-2.8 + (double)((i * 5) % 13) * 0.47), (float)(5.0 * (double)i));
            }
            Register(wall);

            var storm = new TemplateBuilder("seeker_storm", 7, 12, 22f, EncounterRole.Peak, EncounterFamily.Seekers, 0.7f).Notes("Five missiles from alternating corners, each locking on in turn.");
            float[] sx = { -2.4f, 2.4f, 2.4f, -2.4f, 0f };
            float[] sy = { 2.8f, -2.8f, 2.8f, -2.8f, 3.2f };
            for (int i = 0; i < 5; i++) storm.Seeker(sx[i], sy[i], (float)(42.0 * (double)i));
            Register(storm);

            var nest = new TemplateBuilder("turret_nest", 6, 12, 22f, EncounterRole.Peak, EncounterFamily.Turrets, 0.65f).Notes("Three turrets lock in turn: left aimed shot, a centre spread, right aimed shot.");
            nest.Turret(TurretMode.Aimed, -2.1f, 1.0f, 0f, 1.7f).Turret(TurretMode.Fan, 0f, 2.8f, 84f, 1.7f).Turret(TurretMode.Aimed, 2.1f, -1.0f, 168f, 1.7f);
            Register(nest);

            var belt = new TemplateBuilder("mine_belt", 6, 12, 14f, EncounterRole.Peak, EncounterFamily.Mines, 0.6f).Notes("Four rows of two mines with big blast rings; the channel between them snakes.");
            float[] bl = { -2.0f, -1.6f, -2.2f, -1.8f };
            float[] br = { 1.8f, 2.2f, 1.6f, 2.0f };
            float[] bly = { 0.5f, -1.0f, 1.0f, -0.5f };
            for (int i = 0; i < 4; i++) belt.Mine(bl[i], bly[i], (float)(27.0 * (double)i)).Mine(br[i], -bly[i], (float)(27.0 * (double)i));
            Register(belt);

            var phalanx = new TemplateBuilder("elite_phalanx", 7, 12, 26f, EncounterRole.Peak, EncounterFamily.Elites, 0.65f).Notes("Two shielded elites staggered, with a screen of drones between them.");
            phalanx.Elite(-1.4f, 1.0f, 0f).Elite(1.4f, 1.0f, 36f);
            phalanx.Drone(0f, 1.0f, 12f).Drone(-0.8f, 0.4f, 18f).Drone(0.8f, 0.4f, 18f).Drone(0f, 1.6f, 24f);
            Register(phalanx);

            var sw = new TemplateBuilder("swooper_swarm", 6, 12, 20f, EncounterRole.Peak, EncounterFamily.Swoopers, 0.7f).Notes("Ten swoopers peel in from both sides in a continuous stream.");
            for (int i = 0; i < 10; i++)
            {
                double s = (i % 2 == 0) ? -1f : 1f;
                int v = i % 4;
                sw.Swoop((float)(s * 4.2), 3.2f, (float)(6.0 * (double)i), (float)(s * 0.5), 3.6f, (float)(-s * (0.4 + 0.15 * (double)v)), (float)(-1.0 + 0.5 * (double)v), 1.5f);
            }
            Register(sw);

            var gauntlet = new TemplateBuilder("gate_gauntlet", 6, 12, 22f, EncounterRole.Peak, EncounterFamily.Gates, 0.8f).Notes("Six slits in a row, 0.8 s apart; the gap jumps side to side.");
            float[] gg = { -1.4f, 1.0f, -0.8f, 1.2f, -1.0f, 0.7f };
            for (int i = 0; i < 6; i++) gauntlet.Gate(GateKind.Slit, gg[i], 0f, (float)(30.0 * (double)i), 1.25f);
            Register(gauntlet);

            var chain = new TemplateBuilder("gate_ring_chain", 8, 12, 22f, EncounterRole.Peak, EncounterFamily.Gates, 0.8f).Notes("Four drifting round holes in a chain, left-up, right-down, left-down, right-up.");
            float[] rx = { -1.0f, 1.0f, -0.8f, 0.8f };
            float[] ry = { 1.0f, -0.5f, -1.5f, 0.5f };
            for (int i = 0; i < 4; i++) chain.Gate(GateKind.Ring, rx[i], ry[i], (float)(30.0 * (double)i), 1.15f, 0.5f, 1.2f, (float)(1.3 * (double)i), 0.4f);
            Register(chain);

            var maze = new TemplateBuilder("laser_maze", 9, 12, 28f, EncounterRole.Peak, EncounterFamily.Gates, 0.85f).Notes("Slit, slot, ring, slit, slot: every wall wants a different axis.");
            maze.Gate(GateKind.Slit, -1.4f, 0f, 0f, 1.2f).Gate(GateKind.Slot, 0f, 1.5f, 27f, 1.2f).Gate(GateKind.Ring, 0.8f, -1.2f, 54f, 1.15f, 0.4f, 1.2f, 0f, 0.3f)
                .Gate(GateKind.Slit, 1.4f, 0f, 81f, 1.2f).Gate(GateKind.Slot, 0f, -1.5f, 108f, 1.2f);
            Register(maze);

            var storm2 = new TemplateBuilder("crossfire_storm", 8, 12, 26f, EncounterRole.Peak, EncounterFamily.Turrets, 0.7f).Notes("Four turrets take turns locking you, one of them with a spread.");
            storm2.Turret(TurretMode.Aimed, -2.1f, 1.0f, 0f, 1.7f).Turret(TurretMode.Aimed, 2.1f, -1.0f, 84f, 1.7f).Turret(TurretMode.Fan, 0f, 2.8f, 168f, 1.7f).Turret(TurretMode.Aimed, -2.1f, -1.0f, 252f, 1.7f);
            Register(storm2);
        }
    }
}
