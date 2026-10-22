using System;
using System.Collections.Generic;
using System.Globalization;

namespace Frontier.Services
{
    public readonly struct ServiceMessage
    {
        readonly string key, literal;
        readonly object[] arguments;

        ServiceMessage(string key, string literal, object[] arguments)
        {
            this.key = key;
            this.literal = literal;
            this.arguments = arguments;
        }

        public static ServiceMessage Literal(string text)
        {
            return new ServiceMessage(null, text ?? string.Empty, null);
        }

        public static ServiceMessage Get(string key, params object[] arguments)
        {
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            if (!sources.ContainsKey(key)) throw new KeyNotFoundException("Unknown service message: " + key);
            return new ServiceMessage(key, null, (object[])arguments.Clone());
        }

        public string Resolve(Func<string, object[], string> translate)
        {
            if (translate == null) throw new ArgumentNullException(nameof(translate));
            if (key == null) return literal ?? string.Empty;
            var resolved = new object[arguments.Length];
            for (int i = 0; i < resolved.Length; i++)
                resolved[i] = arguments[i] is ServiceMessage nested ? nested.Resolve(translate) : arguments[i];
            return translate(key, resolved);
        }

        public override string ToString()
        {
            return Resolve((id, args) => string.Format(CultureInfo.CurrentCulture, sources[id], args));
        }

        internal static ServiceMessage NotConfigured(string service)
        {
            return Get("service.not-configured", Label(service));
        }

        internal static ServiceMessage ConsentRequired(string category)
        {
            return Get("service.consent-required", Label(category));
        }

        static ServiceMessage Label(string english)
        {
            foreach (var entry in sources)
                if (entry.Key.StartsWith("service.label.", StringComparison.Ordinal) && entry.Value == english)
                    return Get(entry.Key);
            return Literal(english);
        }

        public static IEnumerable<KeyValuePair<string, string>> EnglishEntries
        {
            get { foreach (var entry in sources) yield return entry; }
        }

        static readonly Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "service.not-configured", "{0} is not configured." },
            { "service.consent-required", "Opt-in consent is required for {0}." },
            { "service.label.external", "External services" },
            { "service.label.ads", "Rewarded ads" },
            { "service.label.purchases", "Purchases" },
            { "service.label.authentication", "Authentication" },
            { "service.label.recovery", "Account recovery" },
            { "service.label.leaderboards", "Leaderboards" },
            { "service.label.cloud", "Cloud Save" },
            { "service.label.analytics", "Analytics" },
            { "service.label.remote", "Remote Config" },
            { "service.label.notifications", "Notifications" },
            { "service.label.local-notifications", "Local notifications" },
            { "service.label.deletion", "Account and cloud-save deletion" },
            { "service.label.advertising-consent", "advertising" },
            { "service.label.analytics-consent", "analytics" },
            { "service.label.analytics-collection", "analytics data collection" },
            { "service.label.remote-consent", "online remote configuration" },
            { "service.label.account-consent", "online account services" },
            { "service.label.leaderboard-consent", "online leaderboards" },
            { "service.label.cloud-consent", "cloud saves" },
            { "service.ads.placement", "A rewarded-ad placement is required." },
            { "service.ads.unknown", "Unknown rewarded-ad placement." },
            { "service.ads.busy", "A rewarded ad is already in progress." },
            { "service.ads.disabled", "This rewarded-ad placement is disabled." },
            { "service.ads.invalid-time", "Rewarded-ad usage or device time is invalid. Retry after the device clock is corrected." },
            { "service.ads.limit", "The daily limit for this rewarded-ad placement has been reached." },
            { "service.ads.cooldown", "This rewarded-ad placement is cooling down." },
            { "service.ads.mismatch", "The ad provider returned a different reward placement." },
            { "service.ads.rewound", "The device clock moved backwards during the rewarded ad." },
            { "service.ads.init-failed", "Ads could not connect. Check your connection and try again." },
            { "service.ads.load-failed", "No rewarded ad is available right now. You can keep playing without ads." },
            { "service.ads.display-failed", "The ad could not open. No reward was granted." },
            { "service.ads.timeout", "The ad service did not respond in time. No reward was granted." },
            { "service.ads.unconfirmed", "The ad closed without reward confirmation. No reward was granted." },
            { "service.ads.config-changed", "The ad configuration changed. Restart the game before watching an ad." },
            { "service.ads.test-editor", "The Editor cannot display real iOS ads. Install an iOS Development Build on your iPhone to use the LevelPlay test suite." },
            { "service.ads.test-device", "LevelPlay tests require an iOS Development Build with test diagnostics enabled. Release builds and Android cannot open this suite." },
            { "service.ads.test-ready", "Register this iPhone as a LevelPlay test device. In the suite choose Unity Ads > Test > Rewarded, not Live. Gameplay ads remain disabled." },
            { "service.purchase.receipt", "A purchase product, transaction ID and receipt payload are required." },
            { "service.purchase.busy", "A purchase is already being processed. Keep this purchase pending and retry." },
            { "service.purchase.destination", "A persistent purchase destination is required." },
            { "service.purchase.verification", "Purchase verification did not complete: {0}" },
            { "service.purchase.mismatch", "The verified purchase does not match the pending store transaction." },
            { "service.purchase.destination-changed", "The purchase destination changed. Keep this purchase pending and retry." },
            { "service.purchase.confirmation", "The purchase reward is saved, but store confirmation is pending. Retry without granting again. {0}" },
            { "service.purchase.read-only", "Purchases cannot be granted while this save is read-only." },
            { "service.purchase.retry", "The purchase destination changed. Retry this pending purchase." },
            { "service.purchase.identity", "The verified purchase identity is incomplete." },
            { "service.purchase.journal", "The purchase journal is invalid. Store reconciliation is required." },
            { "service.purchase.conflict", "The purchase journal conflicts with this transaction. Store reconciliation is required." },
            { "service.purchase.rewards", "The purchased shard pack has invalid catalog rewards." },
            { "service.purchase.unsupported", "This store product has no supported catalog reward." },
            { "service.purchase.wallet-full", "The shard wallet is full. Keep the purchase pending until there is room." },
            { "service.purchase.save-failed", "The purchase reward was not granted because its save failed. Keep the store transaction pending. {0}" },
            { "service.analytics.event", "An analytics event name is required." },
            { "service.analytics.parameters", "Analytics parameters require non-empty keys and values." },
            { "service.leaderboard.request", "A leaderboard ID and positive result limit are required." },
            { "service.leaderboard.board", "The requested leaderboard does not match the configured board." },
            { "service.leaderboard.limit", "Leaderboard page size must be between 1 and {0}." },
            { "service.leaderboard.sign-in", "Sign in before loading leaderboard scores." },
            { "service.leaderboard.score", "The configured leaderboard contains a score outside the supported integer range." },
            { "service.leaderboard.unknown-pilot", "Unknown pilot" },
            { "service.cloud.payload", "A cloud-save payload is required." },
            { "service.cloud.nonempty", "A non-empty cloud-save payload is required." },
            { "service.cloud.limit", "The cloud-save payload exceeds the 5 MiB limit." },
            { "service.cloud.sign-in-load", "Sign in before loading a cloud save." },
            { "service.cloud.sign-in-write", "Sign in before writing a cloud save." },
            { "service.cloud.empty", "The stored cloud save is empty and cannot be restored." },
            { "service.cloud.changed-account", "The signed-in account changed. No cloud save was deleted." },
            { "service.cloud.delete-lock", "The cloud save returned no write lock. Nothing was deleted; retry before deleting the account." },
            { "service.cloud.replace-lock", "A write lock is required to replace an existing cloud save. Reload it before retrying." },
            { "service.cloud.missing-lock", "Unity Cloud Save did not return a write lock. Reload the save before retrying." },
            { "service.cloud.conflict", "The cloud save changed on another device. Reload it before retrying." },
            { "service.recovery.sign-in", "Sign in to the current save before linking an account." },
            { "service.recovery.credential", "The recovery provider returned an empty credential." },
            { "service.recovery.provider", "The configured recovery provider is not supported." },
            { "service.remote.sign-in", "Sign in with an existing account before fetching remote configuration." },
            { "service.deletion.busy", "Account deletion is already in progress." },
            { "service.deletion.review", "The signed-in account changed. Review deletion again." },
            { "service.deletion.changed-account", "The signed-in account changed. No account was deleted." },
            { "service.deletion.cloud-only", "The cloud save was deleted, but the signed-in account changed. No account was deleted." },
            { "service.deletion.partial", "The cloud save was deleted, but account deletion failed. Retry account deletion. {0}" },
            { "service.deletion.unconfirmed", "Online deletion failed before cloud-save removal was confirmed. {0}" },
            { "service.notifications.busy", "Notification synchronization is already pending." },
            { "service.notifications.limit", "At most eight crew notifications are supported." },
            { "service.notifications.jobs", "Crew notifications must have unique IDs and nonempty text." },
            { "service.notifications.request", "Enable notifications again to request device permission when a timer is active." },
            { "service.notifications.permission-changed", "Notification settings or jobs changed while permission was pending." },
            { "service.notifications.sync-changed", "Notification settings or jobs changed during synchronization." },
            { "service.notifications.schedule-changed", "Notification settings or jobs changed during scheduling." },
            { "service.notifications.denied", "Notification permission was denied. Enable it in device settings to receive completion alerts." },
            { "service.notifications.future", "A future Frontier crew notification with title and body is required." },
            { "service.notifications.revoked", "Device notification permission is no longer granted." },
            { "service.notifications.id", "A valid Frontier crew notification ID is required." },
            { "service.notifications.channel", "Construction" },
            { "service.notifications.channel-description", "Completed construction, upgrades and clearance" }
        };
    }
}
