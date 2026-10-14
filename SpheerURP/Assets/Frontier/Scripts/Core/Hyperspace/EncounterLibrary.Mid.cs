namespace Frontier.Hyperspace
{
    public sealed partial class EncounterLibrary
    {
        // ---------------------------------------------------------------- tier 3-5+
        void BuildMid()
        {
            var cross = new TemplateBuilder("turret_crossfire", 3, 12, 11f, EncounterRole.Peak, EncounterFamily.Turrets, 0.55f).Notes("Two turrets on opposite edges take turns locking you.");
            cross.Turret(TurretMode.Aimed, -2.1f, 1.0f, 0f, 1.7f).Turret(TurretMode.Aimed, 2.1f, -1.0f, 84f, 1.7f);
            Register(cross);

            var ring = new TemplateBuilder("gate_ring", 3, 12, 8f, EncounterRole.Peak, EncounterFamily.Gates, 0.6f).Notes("Wall with a round hole drifting in an ellipse.");
            ring.Gate(GateKind.Ring, 0f, 0.6f, 0f, 1.1f, 1.0f, 1.3f, 0f, 0.8f);
            Register(ring);

            var seq = new TemplateBuilder("gate_sequence", 3, 12, 14f, EncounterRole.Peak, EncounterFamily.Gates, 0.75f).Notes("Four static slits whose gaps alternate sides: a slalom of walls.");
            float[] gx = { -1.6f, 1.6f, -0.8f, 1.4f };
            for (int i = 0; i < 4; i++) seq.Gate(GateKind.Slit, gx[i], 0f, (float)(36.0 * (double)i), 1.3f);
            Register(seq);

            var seek = new TemplateBuilder("seeker_single", 3, 12, 5f, EncounterRole.Build, EncounterFamily.Seekers, 0.65f).Notes("One homing missile launched from a corner. Shoot it or sidestep late.");
            seek.Seeker(2.4f, 2.6f, 0f);
            Register(seek);

            var elite = new TemplateBuilder("elite_single", 3, 12, 10f, EncounterRole.Peak, EncounterFamily.Elites, 0.5f).Notes("Shielded elite with two flank drones.");
            elite.Elite(0f, 1.0f, 0f).Drone(-1.8f, 0f, 0f).Drone(1.8f, 0f, 0f);
            Register(elite);

            var corridor = new TemplateBuilder("minefield_corridor", 3, 12, 10f, EncounterRole.Build, EncounterFamily.Mines, 0.5f).Notes("Mines alternate left and right of the centre line: weave.");
            for (int i = 0; i < 5; i++) corridor.Mine((i % 2 == 0) ? -1.1f : 1.1f, 0.4f, (float)(24.0 * (double)i));
            Register(corridor);

            var rows = new TemplateBuilder("drone_slot_rows", 3, 12, 11f, EncounterRole.Build, EncounterFamily.Drones, 0.55f).Notes("Two pairs of drone rows with a slot between them; the slot moves up and down.");
            float[] gy = { -1.4f, 1.4f };
            for (int p = 0; p < 2; p++)
            {
                for (int c = 0; c < 4; c++)
                {
                    double x = -1.8 + 1.2 * (double)c;
                    rows.Drone((float)x, (float)((double)gy[p] - 1.5), (float)(28.0 * (double)p));
                    rows.Drone((float)x, (float)((double)gy[p] + 1.5), (float)(28.0 * (double)p));
                }
            }
            Register(rows);

            var storm = new TemplateBuilder("asteroid_storm", 4, 12, 14f, EncounterRole.Peak, EncounterFamily.Asteroids, 0.6f).Notes("Dense field of rocks of all sizes.");
            for (int i = 0; i < 12; i++)
            {
                AsteroidSize sz = (i % 5 == 0) ? AsteroidSize.Large : ((i % 2 == 0) ? AsteroidSize.Medium : AsteroidSize.Small);
                storm.Asteroid(sz, (float)(2.3f * HMath.Sin(2.1 * (double)i + 0.5)), (float)(2.8f * HMath.Sin(1.7 * (double)i + 1.0)), (float)(7.0 * (double)i));
            }
            Register(storm);

            var bar = new TemplateBuilder("gate_bar_spin", 4, 12, 9f, EncounterRole.Peak, EncounterFamily.Gates, 0.6f).Notes("A long rotating beam through the centre; keep to the safe side as it sweeps.");
            bar.Gate(GateKind.Bar, 0f, 0f, 0f, 3.6f, 0.35f, 1.8f, 0f, 0f, 0.5f);
            Register(bar);

            var barrage = new TemplateBuilder("swooper_barrage", 4, 12, 12f, EncounterRole.Peak, EncounterFamily.Swoopers, 0.65f).Notes("Six swoopers in a staggered volley.");
            for (int i = 0; i < 6; i++)
            {
                double s = (i % 2 == 0) ? -1f : 1f;
                barrage.Swoop((float)(s * 4.2), 3.2f, (float)(8.0 * (double)i), (float)(s * 0.4), 3.6f, (float)(-s * (0.3 + 0.25 * (double)(i % 3))), (float)(-1.2 + 0.5 * (double)(i % 3)), 1.5f);
            }
            Register(barrage);

            var swarm = new TemplateBuilder("seeker_swarm", 5, 12, 14f, EncounterRole.Peak, EncounterFamily.Seekers, 0.75f).Notes("Three missiles from different corners, spaced so each can be dodged on its own.");
            swarm.Seeker(-2.4f, 2.8f, 0f).Seeker(2.4f, -2.8f, 40f).Seeker(2.4f, 2.8f, 80f);
            Register(swarm);

            var escort = new TemplateBuilder("elite_escort", 5, 12, 18f, EncounterRole.Peak, EncounterFamily.Elites, 0.6f).Notes("Elite flanked by drones and zigzaggers.");
            escort.Elite(0f, 1.0f, 0f).Drone(-1.5f, 1.0f, 6f).Drone(1.5f, 1.0f, 6f).Drone(0f, -0.4f, 6f);
            escort.Zig(-1.8f, 1.4f, 24f, 1.2f, 2.0f, 0f).Zig(1.8f, 1.4f, 24f, 1.2f, 2.0f, HMath.Pi);
            Register(escort);

            var gaunt = new TemplateBuilder("mine_gauntlet", 5, 12, 14f, EncounterRole.Peak, EncounterFamily.Mines, 0.6f).Notes("Six mines in a loose zigzag with a fuel canister inside each turn.");
            float[] mx = { -1.4f, 1.4f, -1.4f, 1.4f, -1.4f, 1.4f };
            for (int i = 0; i < 6; i++)
            {
                gaunt.Mine(mx[i], (i % 2 == 0) ? 1.2f : -1.2f, (float)(22.0 * (double)i));
                if (i % 2 == 1) gaunt.Reward(PickupType.Fuel, (float)((double)(-mx[i]) * 0.3), 0f, (float)(22.0 * (double)i - 11.0));
            }
            Register(gaunt);

            var rg = new TemplateBuilder("reward_gauntlet", 4, 12, 6f, EncounterRole.Calm, EncounterFamily.Reward, 0.5f).Notes("Risk and reward: fuel canisters weave between four mines.");
            rg.Mine(-1.4f, 1.6f, 0f).Mine(1.4f, -1.6f, 30f).Mine(-1.4f, 1.6f, 60f).Mine(1.4f, -1.6f, 90f);
            rg.Reward(PickupType.Fuel, 0.8f, 1.6f, 0f).Reward(PickupType.Fuel, 0f, 0f, 15f).Reward(PickupType.Fuel, -0.8f, -1.6f, 30f).Reward(PickupType.Fuel, 0f, 0f, 45f)
                .Reward(PickupType.Fuel, 0.8f, 1.6f, 60f).Reward(PickupType.Fuel, 0f, 0f, 75f).Reward(PickupType.Bomb, -0.8f, -1.6f, 90f);
            Register(rg);
        }

        // ---------------------------------------------------------------- tier 6+
        void BuildHigh()
        {
            var wall = new TemplateBuilder("turret_wall", 6, 12, 15f, EncounterRole.Peak, EncounterFamily.Turrets, 0.65f).Notes("Three turrets fire spread shots at the same instant, twice.");
            wall.Turret(TurretMode.Fan, -2.0f, 2.8f, 0f, 1.7f, 1).Turret(TurretMode.Fan, 0f, 2.8f, 0f, 1.7f, 1).Turret(TurretMode.Fan, 2.0f, 2.8f, 0f, 1.7f, 1);
            Register(wall);

            var slalom = new TemplateBuilder("gate_slalom_turrets", 6, 12, 18f, EncounterRole.Peak, EncounterFamily.Gates, 0.7f).Notes("Two static slits with an aimed turret between them.");
            slalom.Gate(GateKind.Slit, -1.4f, 0f, 0f, 1.2f).Gate(GateKind.Slit, 1.4f, 0f, 60f, 1.2f).Turret(TurretMode.Aimed, 0f, 2.8f, 0f, 1.7f);
            Register(slalom);

            var dring = new TemplateBuilder("gate_double_ring", 7, 12, 16f, EncounterRole.Peak, EncounterFamily.Gates, 0.7f).Notes("Two drifting round holes, opposite corners.");
            dring.Gate(GateKind.Ring, -1.0f, 1.5f, 0f, 1.2f, 0.7f, 1.4f, 0f, 0.5f).Gate(GateKind.Ring, 1.0f, -1.0f, 54f, 1.2f, 0.7f, 1.4f, 2.0f, 0.5f);
            Register(dring);

            var combo = new TemplateBuilder("seeker_elite_combo", 8, 12, 22f, EncounterRole.Peak, EncounterFamily.Elites, 0.7f).Notes("Elite up front, two homing missiles behind it.");
            combo.Elite(0f, 1.0f, 0f).Seeker(-2.4f, 2.8f, 36f).Seeker(2.4f, 2.8f, 84f);
            Register(combo);

            var agm = new TemplateBuilder("asteroid_gate_mix", 6, 12, 14f, EncounterRole.Peak, EncounterFamily.Gates, 0.65f).Notes("Rocks, then a wall with a slit.");
            agm.Asteroid(AsteroidSize.Large, -1.6f, 0.4f, 0f).Asteroid(AsteroidSize.Medium, 1.6f, -1.5f, 16f).Asteroid(AsteroidSize.Medium, 0.4f, 2.2f, 30f).Asteroid(AsteroidSize.Small, -1.0f, -1.8f, 44f);
            agm.Gate(GateKind.Slit, 0.9f, 0f, 84f, 1.3f);
            Register(agm);

            var tide = new TemplateBuilder("drone_tide", 6, 12, 16f, EncounterRole.Peak, EncounterFamily.Drones, 0.65f).Notes("Three synchronised ranks of zigzaggers sweeping sideways.");
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 4; c++) tide.Zig((float)(-1.8 + 1.2 * (double)c), 1.0f, (float)(26.0 * (double)r), 1.1f, 1.6f, (float)(1.57 * (double)r));
            }
            Register(tide);
        }

        // ---------------------------------------------------------------- boss
        void BuildBoss()
        {
            var boss = new TemplateBuilder("boss_guardian", 5, 12, 40f, EncounterRole.Boss, EncounterFamily.Boss, 0.8f, 1f, false).Notes("Guardian mini-boss: three phases, weak point, bounded duration.");
            boss.Guardian(0f, 0.6f, 0f);
            Register(boss);
        }
    }
}
