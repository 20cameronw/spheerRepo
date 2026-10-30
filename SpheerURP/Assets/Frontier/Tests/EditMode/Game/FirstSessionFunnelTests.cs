using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class FirstSessionFunnelTests
    {
        sealed class ConsentStore : IConsentStore
        {
            public ConsentPreferences Load() { return default; }
            public void Save(ConsentPreferences preferences) { }
        }

        sealed class Analytics : IAnalyticsService
        {
            public bool IsConfigured { get; set; } = true;
            public ServiceResult result = ServiceResult.Success();
            public readonly List<string> names = new List<string>();
            public readonly List<IReadOnlyDictionary<string, string>> payloads = new List<IReadOnlyDictionary<string, string>>();
            public void SetDataCollectionAllowed(bool allowed) { }
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public ServiceResult RecordEvent(string eventName, IReadOnlyDictionary<string, string> parameters)
            {
                names.Add(eventName);
                payloads.Add(parameters);
                return result;
            }
        }

        ConsentService consent;
        Analytics analytics;
        FirstSessionFunnel funnel;
        List<string> failures;

        [SetUp]
        public void SetUp()
        {
            consent = new ConsentService(new ConsentStore());
            consent.SavePreferences(true, false, false);
            analytics = new Analytics();
            failures = new List<string>();
            var offline = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, offline, offline, offline, offline, offline,
                analytics, offline, new LocalTimeService(), offline);
            funnel = new FirstSessionFunnel(services, failures.Add);
        }

        [Test]
        public void Progress_RecordsCrossedObjectivesOnce_WithoutReplayingRegressions()
        {
            funnel.Observe("private-save", true, 0);
            funnel.Observe("private-save", true, 0);
            funnel.Observe("private-save", true, 3);
            funnel.Observe("private-save", true, 1);
            funnel.Observe("private-save", true, 3);
            funnel.Observe("private-save", true, FirstSessionObjectives.Count);
            funnel.Observe("private-save", true, FirstSessionObjectives.Count);
            Assert.AreEqual(FirstSessionObjectives.Count + 2, analytics.names.Count);
            Assert.AreEqual("onboarding_progress_observed", analytics.names[0]);
            for (int step = 0; step < FirstSessionObjectives.Count; step++)
            {
                Assert.AreEqual("onboarding_step_completed", analytics.names[step + 1]);
                Assert.AreEqual(step.ToString(), analytics.payloads[step + 1]["step"]);
            }
            Assert.AreEqual("onboarding_completed", analytics.names[FirstSessionObjectives.Count + 1]);
            foreach (var payload in analytics.payloads)
            {
                Assert.AreEqual(2, payload.Count);
                Assert.IsTrue(payload.ContainsKey("step"));
                Assert.IsTrue(payload.ContainsKey("objective"));
                CollectionAssert.DoesNotContain(new List<string>(payload.Values), "private-save");
            }
            Assert.IsEmpty(failures);
        }

        [Test]
        public void ExistingProgress_IsBaselineNotRetroactiveCompletion()
        {
            funnel.Observe("old-save", true, 4);
            Assert.AreEqual(1, analytics.names.Count);
            Assert.AreEqual("onboarding_progress_observed", analytics.names[0]);
            Assert.AreEqual("4", analytics.payloads[0]["step"]);
            funnel.Observe("old-save", true, 5);
            Assert.AreEqual(2, analytics.names.Count);
            Assert.AreEqual("defense", analytics.payloads[1]["objective"]);
        }

        [Test]
        public void ConsentWithdrawalAndRegrant_DoNotReplayActivity()
        {
            consent.Reset();
            funnel.Observe("save", true, 0);
            funnel.Observe("save", true, 2);
            funnel.Action(2);
            Assert.IsEmpty(analytics.names);
            consent.SavePreferences(true, false, false);
            funnel.Observe("save", true, 2);
            Assert.IsEmpty(analytics.names);
            funnel.Observe("save", true, 3);
            Assert.AreEqual(1, analytics.names.Count);
            Assert.AreEqual("warehouse", analytics.payloads[0]["objective"]);
            consent.SavePreferences(false, false, false);
            funnel.Observe("save", true, 5);
            consent.SavePreferences(true, false, false);
            funnel.Observe("save", true, 5);
            Assert.AreEqual(1, analytics.names.Count);
            funnel.Action(5);
            Assert.AreEqual("onboarding_action", analytics.names[1]);
        }

        [Test]
        public void UnconfiguredProvider_DoesNotQueueOrReportExpectedOfflineStateAsFailure()
        {
            analytics.IsConfigured = false;
            funnel.Observe("save", true, 0);
            funnel.Observe("save", true, 3);
            Assert.IsEmpty(analytics.names);
            Assert.IsEmpty(failures);
            analytics.IsConfigured = true;
            funnel.Observe("save", true, 3);
            Assert.IsEmpty(analytics.names);
            funnel.Observe("save", true, 4);
            Assert.AreEqual(1, analytics.names.Count);
            Assert.AreEqual("craft", analytics.payloads[0]["objective"]);
        }

        [Test]
        public void Dismiss_IsIdempotent_AndResetStartsAnIndependentObservation()
        {
            funnel.Observe("save", true, 1);
            funnel.Action(1);
            funnel.Dismiss(1);
            funnel.Dismiss(1);
            funnel.Action(1);
            funnel.Observe("save", true, 2);
            CollectionAssert.AreEqual(new[] { "onboarding_progress_observed", "onboarding_action", "onboarding_skipped" },
                analytics.names);
            funnel.Observe("reset-save", true, FirstSessionObjectives.Count);
            funnel.Dismiss(FirstSessionObjectives.Count);
            Assert.AreEqual("onboarding_progress_observed", analytics.names[3]);
            Assert.AreEqual("onboarding_dismissed", analytics.names[4]);
        }

        [Test]
        public void UnavailableGuide_DoesNotSendActionsOrBackfillHiddenProgress()
        {
            funnel.Observe("save", false, 0);
            funnel.Action(0);
            funnel.Dismiss(0);
            Assert.IsEmpty(analytics.names);
            funnel.Observe("save", true, 0);
            funnel.Observe("save", false, 2);
            funnel.Action(2);
            funnel.Observe("save", true, 2);
            Assert.AreEqual(1, analytics.names.Count);
        }

        [Test]
        public void ProviderFailure_IsReportedOncePerEvent_WithoutRetryingOnRefresh()
        {
            analytics.result = ServiceResult.Failed("Test provider failure");
            funnel.Observe("save", true, 0);
            funnel.Observe("save", true, 0);
            Assert.AreEqual(1, failures.Count);
            StringAssert.Contains("onboarding_progress_observed", failures[0]);
            StringAssert.Contains("Test provider failure", failures[0]);
        }

        [Test]
        public void InvalidInput_IsExplicitAndCannotSendEvents()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => funnel.Observe("save", true, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => funnel.Observe("save", true, FirstSessionObjectives.Count + 1));
            Assert.Throws<ArgumentException>(() => funnel.Observe("", true, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => funnel.Action(FirstSessionObjectives.Count + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => funnel.Dismiss(-1));
            Assert.IsEmpty(analytics.names);
        }
    }
}
