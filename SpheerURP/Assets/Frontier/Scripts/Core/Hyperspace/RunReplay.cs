using System;
using System.Collections.Generic;
using System.Globalization;

namespace Frontier.Hyperspace
{
    [Serializable]
    public struct ReplayAction
    {
        public int kind, count;
        public PlayerInput input;
    }

    /// <summary>Versioned input/action journal. Replaying reconstructs private pools, RNGs and boss state without saving engine objects.</summary>
    [Serializable]
    public sealed class RunReplay
    {
        // Bump when simulation behavior changes incompatibly, even if the schedule is unchanged.
        public const int CurrentVersion = 1;
        public const int MaximumTicks = 60 * 600;
        public int version = CurrentVersion;
        public RunParams parameters;
        public ShipStats ship;
        public string tuningSignature, scheduleDigest, stateDigest;
        public int ticks;
        public List<ReplayAction> actions = new List<ReplayAction>();
        [NonSerialized] public HyperspaceRun LiveRun;

        public static RunReplay Attach(HyperspaceRun run, string tuningSignature)
        {
            var replay = new RunReplay
            {
                parameters = run.Route.run, ship = run.Ship, tuningSignature = tuningSignature,
                scheduleDigest = Hex(run.Plan.Digest()), LiveRun = run
            };
            run.Replay = replay;
            replay.Capture();
            return replay;
        }

        public void Capture()
        {
            if (LiveRun != null) stateDigest = Hex(LiveRun.StateDigest());
        }

        internal void RecordStep(PlayerInput input)
        {
            if (++ticks > MaximumTicks) throw new InvalidOperationException("Hyperspace replay exceeds its bounded duration.");
            int last = actions.Count - 1;
            if (last >= 0 && actions[last].kind == 0 && Same(actions[last].input, input))
            {
                var action = actions[last];
                action.count++;
                actions[last] = action;
            }
            else actions.Add(new ReplayAction { count = 1, input = input });
        }

        internal void RecordAction(int kind) { actions.Add(new ReplayAction { kind = kind, count = 1 }); }

        public HyperspaceRun Restore(HyperspaceTuning tuning, EncounterLibrary library, string signature)
        {
            if (version != CurrentVersion || signature != tuningSignature)
                throw new InvalidOperationException("The saved Hyperspace simulation version or tuning is incompatible.");
            if (actions == null || ticks < 0 || ticks > MaximumTicks || actions.Count > MaximumTicks + 4 ||
                !Finite(parameters.distanceLy) || parameters.distanceLy < 1 || !Finite(parameters.danger) || parameters.danger < 0 ||
                !Finite(parameters.shipPower) || parameters.shipPower <= 0 || !Finite(parameters.plannedDurationSeconds) ||
                parameters.plannedDurationSeconds < 0 || parameters.tier < 0 || parameters.tier > 12 ||
                !Enum.IsDefined(typeof(RunMode), parameters.mode) || !ValidShip(ship))
                throw new InvalidOperationException("The saved Hyperspace inputs are invalid.");
            var run = HyperspaceRun.Create(parameters, ship, tuning, library);
            if (run.Plan.Digest().ToString("X16", CultureInfo.InvariantCulture) != scheduleDigest)
                throw new InvalidOperationException("The saved Hyperspace encounter schedule has changed.");
            int simulated = 0;
            foreach (var action in actions)
            {
                if (action.count < 1 || (action.count > MaximumTicks - simulated && action.kind == 0) ||
                    !Finite(action.input.targetX) || !Finite(action.input.targetY))
                    throw new InvalidOperationException("The saved Hyperspace input journal is invalid.");
                if (action.kind == 0)
                {
                    for (int i = 0; i < action.count; i++)
                    {
                        if (run.State != RunState.Running)
                            throw new InvalidOperationException("The saved Hyperspace journal steps a frozen run.");
                        run.Step(action.input);
                        run.Events.Clear();
                    }
                    simulated += action.count;
                }
                else if (action.count != 1 || action.kind != 1 && action.kind != 2)
                    throw new InvalidOperationException("The saved Hyperspace action is invalid.");
                else if (action.kind == 1)
                {
                    if (!run.Revive()) throw new InvalidOperationException("The saved Hyperspace revive is invalid.");
                }
                else
                {
                    if (run.State == RunState.Ended) throw new InvalidOperationException("The saved Hyperspace finish is duplicated.");
                    run.Abandon();
                }
                run.Events.Clear();
            }
            if (simulated != ticks || Hex(run.StateDigest()) != stateDigest)
                throw new InvalidOperationException("The saved Hyperspace state does not match its input journal.");
            LiveRun = run;
            run.Replay = this;
            return run;
        }

        static bool Same(PlayerInput a, PlayerInput b)
        {
            return BitConverter.SingleToInt32Bits(a.targetX) == BitConverter.SingleToInt32Bits(b.targetX) &&
                BitConverter.SingleToInt32Bits(a.targetY) == BitConverter.SingleToInt32Bits(b.targetY) && a.bomb == b.bomb;
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool Positive(float value) { return Finite(value) && value > 0 && value <= 1000000; }
        static bool ValidShip(ShipStats s)
        {
            return Positive(s.hull) && Positive(s.damage) && Positive(s.fireRate) && Positive(s.handling) &&
                Positive(s.shieldRecharge) && Finite(s.magnet) && s.magnet >= 0 && s.magnet <= 1000000 &&
                s.shield >= 0 && s.shield <= 1000 && s.pierce >= 0 && s.pierce <= 1000 && s.bombCapacity >= 0 && s.bombCapacity <= 1000;
        }
        static string Hex(ulong value) { return value.ToString("X16", CultureInfo.InvariantCulture); }
    }
}
