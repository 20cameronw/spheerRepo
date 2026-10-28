using System;
using System.Collections.Generic;
using System.Globalization;
using Frontier.Services;

namespace Frontier
{
    public sealed class FirstSessionFunnel
    {
        readonly FrontierServices services;
        readonly Action<string> reportFailure;
        string localSaveIdentity;
        int furthestStep;
        bool observing, dismissed, active;

        public FirstSessionFunnel(FrontierServices services, Action<string> reportFailure)
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.reportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
        }

        public void Observe(string saveIdentity, bool available, int step)
        {
            ValidateStep(step);
            if (string.IsNullOrEmpty(saveIdentity)) throw new ArgumentException("A local save identity is required.", nameof(saveIdentity));
            if (localSaveIdentity != saveIdentity)
            {
                localSaveIdentity = saveIdentity;
                observing = dismissed = false;
            }
            active = available && !dismissed;
            if (!active)
            {
                if (observing) furthestStep = Math.Max(furthestStep, step);
                return;
            }
            if (!observing)
            {
                observing = true;
                furthestStep = step;
                Record("onboarding_progress_observed", step);
                return;
            }
            if (step <= furthestStep) return;
            int previous = furthestStep;
            // Advance even when telemetry is unavailable: never replay pre-consent activity later.
            furthestStep = step;
            for (int completed = previous; completed < step; completed++)
                Record("onboarding_step_completed", completed);
            if (step == FirstSessionObjectives.Count) Record("onboarding_completed", step);
        }

        public void Action(int step)
        {
            ValidateStep(step);
            if (active && observing) Record("onboarding_action", step);
        }

        public void Dismiss(int step)
        {
            ValidateStep(step);
            if (!active || !observing) return;
            dismissed = true;
            active = false;
            Record(step == FirstSessionObjectives.Count ? "onboarding_dismissed" : "onboarding_skipped", step);
        }

        void Record(string name, int step)
        {
            var consent = services.Consent.Current;
            if (!consent.hasDecision || !consent.analyticsAllowed || !services.Analytics.IsConfigured) return;
            var result = services.Analytics.RecordEvent(name, new Dictionary<string, string>
            {
                { "step", step.ToString(CultureInfo.InvariantCulture) },
                { "objective", ObjectiveId(step) }
            });
            if (!result.succeeded) reportFailure("First-session analytics " + name + ": " + result.status + ". " + result.message);
        }

        static void ValidateStep(int step)
        {
            if (step < 0 || step > FirstSessionObjectives.Count) throw new ArgumentOutOfRangeException(nameof(step));
        }

        static string ObjectiveId(int step)
        {
            switch (step)
            {
                case 0: return "lumber_camp";
                case 1: return "harvest";
                case 2: return "warehouse";
                case 3: return "craft";
                case 4: return "defense";
                case 5: return "travel";
                default: return "complete";
            }
        }
    }
}
