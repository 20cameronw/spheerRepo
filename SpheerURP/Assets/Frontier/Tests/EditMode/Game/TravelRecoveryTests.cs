using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Frontier.Hyperspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public class TravelRecoveryTests
    {
        SaveSandbox sandbox;
        GameDatabase db;

        [SetUp]
        public void SetUp() { sandbox = new SaveSandbox(); db = GameDatabase.Load(); }
        [TearDown]
        public void TearDown() { sandbox.Dispose(); }

        static RunReplay RoundTrip(RunReplay replay)
        {
            replay.Capture();
            return JsonUtility.FromJson<RunReplay>(JsonUtility.ToJson(replay));
        }

        [Test]
        public void ReplayRestoresExactRunningDownedRevivedAndAbandonedStates()
        {
            var tuning = new HyperspaceTuning();
            var ship = ShipStats.Starter();
            var library = EncounterLibrary.CreateDefault();
            string signature = JsonUtility.ToJson(tuning);
            Assert.Greater(signature.Length, 1000, "The complete tuning must be fingerprinted.");
            var run = HyperspaceRun.Create(new RunParams(ulong.MaxValue - 7, 3000, 13.8f, 12, ship.Power(tuning), RunMode.Normal), ship, tuning, library);
            var replay = RunReplay.Attach(run, signature);
            for (int i = 0; i < 120; i++) run.Step(new PlayerInput(0, 0, false));
            Assert.AreEqual(1, replay.actions.Count, "Held input should be run-length encoded.");
            var restored = RoundTrip(replay).Restore(tuning, library, signature);
            Assert.AreEqual(run.StateDigest(), restored.StateDigest());
            Assert.AreEqual(run.Route.run.seed, restored.Route.run.seed, "All 64 seed bits must survive JSON.");
            while (run.State == RunState.Running) run.Step(new PlayerInput(0, 0, false));
            Assert.AreEqual(RunState.Downed, run.State);
            restored = RoundTrip(replay).Restore(tuning, library, signature);
            Assert.AreEqual(run.StateDigest(), restored.StateDigest());
            Assert.IsTrue(run.Revive());
            for (int i = 0; i < 60; i++) run.Step(new PlayerInput(1.25f, -0.75f, i == 0));
            restored = RoundTrip(replay).Restore(tuning, library, signature);
            Assert.AreEqual(1, restored.RevivesUsed);
            Assert.AreEqual(run.StateDigest(), restored.StateDigest());
            run.Abandon();
            restored = RoundTrip(replay).Restore(tuning, library, signature);
            Assert.AreEqual(run.StateDigest(), restored.StateDigest());
            Assert.IsTrue(restored.Abandoned);
            Assert.IsFalse(restored.BuildResult().reachedEnd);
        }

        [Test]
        public void ReplayRejectsChangedRulesDamagedInputsAndMismatchedState()
        {
            var tuning = new HyperspaceTuning();
            var ship = ShipStats.Starter();
            var library = EncounterLibrary.CreateDefault();
            string signature = JsonUtility.ToJson(tuning);
            var run = HyperspaceRun.Create(new RunParams(123, 5, 0.6f, 0, 1, RunMode.Normal), ship, tuning, library);
            var replay = RunReplay.Attach(run, signature);
            run.Step(new PlayerInput(0, 0, false));
            var changed = RoundTrip(replay);
            changed.version++;
            Assert.Throws<InvalidOperationException>(() => changed.Restore(tuning, library, signature));
            Assert.Throws<InvalidOperationException>(() => replay.Restore(tuning, library, signature + "changed"));
            changed = RoundTrip(replay);
            changed.stateDigest = "invalid";
            Assert.Throws<InvalidOperationException>(() => changed.Restore(tuning, library, signature));
            changed = RoundTrip(replay);
            var action = changed.actions[0]; action.count = RunReplay.MaximumTicks + 1; changed.actions[0] = action;
            Assert.Throws<InvalidOperationException>(() => changed.Restore(tuning, library, signature));
            changed = RoundTrip(replay);
            changed.actions.Add(new ReplayAction { kind = 1, count = 1 });
            Assert.Throws<InvalidOperationException>(() => changed.Restore(tuning, library, signature));
        }

        GameState PreparedState()
        {
            var state = new GameState(db, false);
            foreach (var upgrade in db.upgrades) state.DebugSetUpgrade(upgrade, upgrade.MaxLevel);
            state.backpack.Set(db.fuel.id, 100);
            return state;
        }

        [Test]
        public void PendingLegRewardsAndFuelSettleOnceInTheSameDurableSnapshot()
        {
            var state = PreparedState();
            var target = state.universe.Get("glacia");
            var plan = state.PlanRoute(target);
            Assert.IsTrue(plan.reachable);
            string origin = state.planet.Id;
            int xp = state.xp, fuel = state.Count(db.fuel), scrap = state.Count(db.scrap);
            Assert.IsTrue(state.BeginTravelCheckpoint(plan, out string error), error);
            Assert.IsFalse(state.Travel(target), "Another journey must not bypass the pending transaction.");
            int expectedRefund = 0;
            foreach (var leg in plan.legs)
            {
                expectedRefund += state.Rules.Refund(leg.fuel, 0.75, false);
                Assert.IsTrue(state.CompleteTravelLeg(true, 0.75f, 40, new List<ItemAmount> { new ItemAmount(db.scrap, 3) }));
            }
            var restarted = new GameState(db, true);
            Assert.AreEqual(origin, restarted.planet.Id);
            Assert.AreEqual(fuel, restarted.Count(db.fuel));
            Assert.AreEqual(xp, restarted.xp);
            Assert.AreEqual(scrap, restarted.Count(db.scrap));
            Assert.IsNotNull(restarted.PendingTravel);
            Assert.AreEqual(plan.LegCount, restarted.PendingTravel.nextLeg);
            Assert.IsTrue(restarted.SettlePendingTravel(out int refund, out error), error);
            Assert.IsEmpty(error);
            Assert.AreEqual(expectedRefund, refund);
            Assert.AreEqual(fuel - plan.totalFuel + refund, restarted.Count(db.fuel));
            Assert.AreEqual(xp + 40 * plan.LegCount, restarted.xp);
            Assert.AreEqual(scrap + 3 * plan.LegCount, restarted.Count(db.scrap));
            Assert.AreEqual(target.Id, restarted.planet.Id);
            var settled = new GameState(db, true);
            Assert.IsNull(settled.PendingTravel);
            Assert.AreEqual(restarted.xp, settled.xp);
            Assert.AreEqual(restarted.Count(db.fuel), settled.Count(db.fuel));
            Assert.IsFalse(settled.SettlePendingTravel(out _, out _));
            var saved = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            Assert.IsNull(saved.travel);
            Assert.AreEqual(settled.xp, saved.player.xp);
        }

        [Test]
        public void InvalidTravelIsPreservedUntilAnExplicitDurableDiscard()
        {
            var state = PreparedState();
            Assert.IsTrue(state.BeginTravelCheckpoint(state.PlanRoute(state.universe.Get("glacia")), out _));
            state.PendingTravel.plan.legs[0].lengthLy += 1;
            Assert.IsFalse(state.ValidatePendingTravel(out _, out _), "Materially changed route distances must be rejected.");
            state.PendingTravel.version++;
            Assert.IsTrue(state.Save());
            var restarted = new GameState(db, true);
            Assert.IsFalse(restarted.ValidatePendingTravel(out _, out _));
            Assert.IsNotNull(restarted.PendingTravel);
            Assert.IsFalse(restarted.SettlePendingTravel(out _, out _));
            Assert.IsTrue(restarted.DiscardPendingTravel(out string error), error);
            Assert.IsNull(new GameState(db, true).PendingTravel);
        }

        [Test]
        public void FailedSettlementRetainsAReplayableTransactionAndDoesNotDuplicateRewards()
        {
            var state = PreparedState();
            var plan = state.PlanRoute(state.universe.Get("glacia"));
            Assert.IsTrue(state.BeginTravelCheckpoint(plan, out _));
            int xp = state.xp, fuel = state.Count(db.fuel);
            foreach (var leg in plan.legs) Assert.IsTrue(state.CompleteTravelLeg(true, 0, 17, null));
            string blockedTemp = sandbox.SaveFile + SaveSchema.TempSuffix;
            Directory.CreateDirectory(blockedTemp);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            try
            {
                Assert.IsTrue(state.SettlePendingTravel(out _, out string message));
                Assert.AreEqual(UiText.Get("travel.recovery.pending-save"), message);
                Assert.IsTrue(state.saves.IsDirty);
                Assert.IsNull(state.PendingTravel);
            }
            finally { Directory.Delete(blockedTemp); }
            var restarted = new GameState(db, true);
            Assert.IsNotNull(restarted.PendingTravel);
            Assert.AreEqual(xp, restarted.xp);
            Assert.AreEqual(fuel, restarted.Count(db.fuel));
            Assert.IsTrue(restarted.SettlePendingTravel(out _, out string error), error);
            var settled = new GameState(db, true);
            Assert.AreEqual(xp + 17 * plan.LegCount, settled.xp);
            Assert.AreEqual(fuel - plan.totalFuel, settled.Count(db.fuel));
            Assert.IsNull(settled.PendingTravel);
        }

        [Test]
        public void AutopilotRestoresRemainingTimeWithoutCreatingAPhantomRun()
        {
            var state = PreparedState();
            var plan = state.PlanRoute(state.universe.Get("glacia"), true);
            Assert.IsTrue(plan.reachable);
            Assert.IsTrue(state.BeginTravelCheckpoint(plan, out _));
            float remaining = plan.legs[0].seconds * 0.5f;
            state.PendingTravel.legs[0].autopilotRemaining = remaining;
            Assert.IsTrue(state.Save());
            var restarted = new GameState(db, true);
            Assert.IsTrue(restarted.ValidatePendingTravel(out _, out string error), error);
            Assert.AreEqual(remaining, restarted.PendingTravel.legs[0].autopilotRemaining);
            Assert.IsNull(restarted.PendingTravel.legs[0].replay);
            Assert.AreEqual(0, restarted.PendingTravel.nextLeg);
        }

        [Test]
        public void RestartRetainsCompletedLegRewardsWhileContinuingTheNextLeg()
        {
            var state = new GameState(db, false);
            foreach (var upgrade in db.upgrades)
                if (upgrade.id == "pack" || upgrade.id.StartsWith("hull_", StringComparison.Ordinal))
                    state.DebugSetUpgrade(upgrade, upgrade.MaxLevel);
            state.backpack.Set(db.fuel.id, 200);
            var plan = state.PlanRoute(state.universe.Get("cinder"));
            Assert.IsTrue(plan.reachable);
            Assert.Greater(plan.LegCount, 1);
            Assert.IsTrue(state.BeginTravelCheckpoint(plan, out _));
            int xp = state.xp, scrap = state.Count(db.scrap), fuel = state.Count(db.fuel);
            Assert.IsTrue(state.CompleteTravelLeg(true, 0.75f, 23, new List<ItemAmount> { new ItemAmount(db.scrap, 2) }));
            var restarted = new GameState(db, true);
            Assert.IsTrue(restarted.ValidatePendingTravel(out _, out string error), error +
                "\nSaved: " + JsonUtility.ToJson(restarted.PendingTravel.plan) +
                "\nCurrent: " + JsonUtility.ToJson(restarted.PlanRoute(restarted.universe.Get("cinder"))));
            Assert.AreEqual(1, restarted.PendingTravel.nextLeg);
            Assert.IsTrue(restarted.PendingTravel.legs[0].finished);
            Assert.IsFalse(restarted.PendingTravel.legs[1].finished);
            Assert.AreEqual(xp, restarted.xp);
            Assert.AreEqual(scrap, restarted.Count(db.scrap));
            for (int i = 1; i < plan.LegCount; i++) Assert.IsTrue(restarted.CompleteTravelLeg(false, 0, 0, null));
            Assert.IsTrue(restarted.SettlePendingTravel(out int refund, out error), error);
            var settled = new GameState(db, true);
            Assert.AreEqual(xp + 23, settled.xp);
            Assert.AreEqual(scrap + 2, settled.Count(db.scrap));
            Assert.AreEqual(fuel - plan.totalFuel + refund, settled.Count(db.fuel));
            Assert.IsNull(settled.PendingTravel);
        }
    }
}
