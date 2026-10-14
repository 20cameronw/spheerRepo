namespace Frontier.Hyperspace
{
    /// <summary>
    /// Headless self-test the host can run on any device or runtime (Mono, IL2CPP, CoreCLR): plays three fixed scenarios with the deterministic bots and returns digests of the plan,
    /// the event stream and the final state. The values must equal the golden constants in the unit tests; a mismatch means the runtime evaluates float math differently.
    /// </summary>
    public static class SelfCheck
    {
        public const int ScenarioCount = 3;

        /// <summary>Nine digests: (schedule, events, final state) for each of the three scenarios.</summary>
        public static ulong[] Run(HyperspaceTuning t, EncounterLibrary lib)
        {
            var d = new ulong[ScenarioCount * 3];
            Play(0, new RunParams(1UL, 5f, 0.6f, 0, 1f, RunMode.Normal), ShipStats.Starter(), BotSkill.Average(), t, lib, false, d);
            ShipStats mid = ShipStats.AtProgress(0.5f);
            Play(1, new RunParams(2UL, 800f, 7.2f, 6, t.ShipPower(mid), RunMode.Normal), mid, BotSkill.Expert(), t, lib, false, d);
            ShipStats end = ShipStats.Endgame();
            Play(2, new RunParams(3UL, 3000f, 13.8f, 12, t.ShipPower(end), RunMode.Challenge), end, BotSkill.Novice(), t, lib, true, d);
            return d;
        }

        static void Play(int index, RunParams p, ShipStats ship, BotSkill skill, HyperspaceTuning t, EncounterLibrary lib, bool revive, ulong[] digests)
        {
            HyperspaceRun run = HyperspaceRun.Create(p, ship, t, lib);
            var bot = new Bot(skill, t, p.seed);
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed)
                {
                    if (revive && run.CanRevive) run.Revive();
                    else
                    {
                        run.Abandon();
                        break;
                    }
                }
                run.Step(bot.Decide(run));
            }
            digests[index * 3] = run.Plan.Digest();
            digests[index * 3 + 1] = run.Events.Digest;
            digests[index * 3 + 2] = run.StateDigest();
        }
    }
}
