using System;
using System.Threading.Tasks;
using Frontier.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        GameplayRewardRules gameplayRewardRules;
        bool gameplayAdShowing;
        string rewardDiscardArmed;
        VisualElement adConsentDialog;
        TaskCompletionSource<bool> adConsentDecision;

        bool GameplayOfferAllowed(bool placementEnabled, string placement, bool requireConsent = true)
        {
            var ticket = gs.GameplayReward;
            var available = services?.Ads is IRewardedAdAvailability availability
                ? availability.RewardedAvailability(placement) : ServiceResult.Success();
            return placementEnabled && services != null && services.Ads != null && services.Consent != null &&
                services.Ads.IsConfigured &&
                (!requireConsent || (services.Consent.Current.hasDecision && services.Consent.Current.adsAllowed)) &&
                (available.succeeded || (!requireConsent && available.status == ServiceStatus.ConsentRequired)) &&
                !gs.SaveIsReadOnly && !gameplayAdShowing &&
                (ticket == null || ticket.state == "settled" || ticket.state == "cancelled");
        }

        Task<bool> RequestRewardedConsent()
        {
            if (services.Consent.Current.hasDecision && services.Consent.Current.adsAllowed)
                return Task.FromResult(true);
            if (adConsentDecision != null) return adConsentDecision.Task;
            adConsentDecision = new TaskCompletionSource<bool>();
            var decision = adConsentDecision.Task;
            adConsentDialog = UiKit.Div("fr-backdrop");
            adConsentDialog.name = "rewarded-consent";
            adConsentDialog.pickingMode = PickingMode.Position;
            adConsentDialog.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == adConsentDialog) CompleteRewardedConsent(false);
            });
            var panel = new FrontierModal();
            panel.Add(UiKit.Text(UiText.Get("reward.consent.title"), "fr-h2"));
            panel.Add(UiKit.Text(UiText.Get("reward.consent.help"), "fr-muted"));
            if (!string.IsNullOrEmpty(services.PrivacyPolicyUrl))
                panel.Add(IconBtn(UiText.Get("privacy.policy"), "about", UiKit.Cyan,
                    () => Application.OpenURL(services.PrivacyPolicyUrl)));
            var allow = IconBtn(UiText.Get("reward.consent.allow"), "play", UiKit.Cyan,
                () => CompleteRewardedConsent(true));
            allow.name = "rewarded-consent-allow";
            panel.Add(allow);
            var cancel = IconBtn(UiText.Get("reward.consent.cancel"), "close", UiKit.Muted,
                () => CompleteRewardedConsent(false));
            cancel.name = "rewarded-consent-cancel";
            panel.Add(cancel);
            adConsentDialog.Add(panel);
            overlayLayer.Add(adConsentDialog);
            return decision;
        }

        void CompleteRewardedConsent(bool allow)
        {
            if (adConsentDecision == null) return;
            var decision = adConsentDecision;
            adConsentDecision = null;
            adConsentDialog.RemoveFromHierarchy();
            adConsentDialog = null;
            if (allow)
            {
                var current = services.Consent.Current;
                services.Consent.SavePreferences(current.analyticsAllowed, true, current.onlineServicesAllowed);
            }
            decision.TrySetResult(allow);
        }

        void BuildShardAdOffer(VisualElement body)
        {
            if (gameplayRewardRules == null || !gameplayRewardRules.shardsEnabled) return;
            var rules = gs.db.shardRules != null ? gs.db.shardRules.data : null;
            int amount = rules != null ? rules.adReward : 0;
            var card = Card(UiKit.Cyan);
            card.Add(UiKit.Text(UiText.Get("shards.ad.title"), "fr-h2"));
            card.Add(UiKit.Text(UiText.Get("shards.ad.help", amount), "fr-muted"));
            if (amount <= 0)
            {
                card.Add(Note(UiText.Get("shards.ad.unavailable"), UiKit.Muted));
                body.Add(card);
                return;
            }
            if (services == null || services.Consent == null || services.Ads == null || !services.Ads.IsConfigured)
            {
                card.Add(Note(UiText.Get("shards.ad.unavailable"), UiKit.Amber));
                body.Add(card);
                return;
            }
            if (!services.Consent.Current.hasDecision || !services.Consent.Current.adsAllowed)
            {
                card.Add(Note(UiText.Get("shards.ad.consent"), UiKit.Muted));
            }
            if (gs.SaveIsReadOnly || (long)gs.ShardBalance + amount > int.MaxValue)
            {
                card.Add(Note(UiText.Get(gs.SaveIsReadOnly ? "shards.ad.read-only" : "shards.ad.wallet-full"), UiKit.Amber));
                body.Add(card);
                return;
            }
            ServiceResult available = services.Ads is IRewardedAdAvailability availability
                ? availability.RewardedAvailability("free-shards") : ServiceResult.Success();
            if (!available.succeeded && available.status != ServiceStatus.ConsentRequired)
            {
                card.Add(Note(available.text.Resolve(UiText.Get), UiKit.Amber));
                body.Add(card);
                return;
            }

            Button watch = null;
            watch = IconBtn(UiText.Get("shards.ad.watch", amount), "play", UiKit.Cyan, async () =>
            {
                if (!await RequestRewardedConsent() || this == null) return;
                if (!GameplayOfferAllowed(gameplayRewardRules.shardsEnabled, "free-shards") ||
                    (long)gs.ShardBalance + amount > int.MaxValue)
                {
                    Toast(UiText.Get("reward.changed"), UiKit.Amber);
                    RefreshModal();
                    return;
                }
                if (!gs.PrepareGameplayReward("free-shards", null, amount, null, out var ticket, out var error))
                {
                    Toast(error, UiKit.Amber);
                    return;
                }
                UiKit.SetEnabledLook(watch, false);
                Toast(UiText.Get("shards.ad.loading"), UiKit.Cyan);
                var result = await ShowGameplayReward(ticket);
                if (this == null) return;
                Toast(result.succeeded && result.value.rewardEarned ? UiText.Get("shards.ad.claimed", amount) :
                    result.succeeded ? UiText.Get("reward.not-earned") : result.text.Resolve(UiText.Get), UiKit.Cyan);
                RefreshModal();
            });
            watch.name = "shards-watch-ad";
            card.Add(watch);
            body.Add(card);
        }

        async Task<ServiceResult<RewardedAdResult>> ShowGameplayReward(GameplayRewardTicket ticket)
        {
            var serviceSet = services;
            var state = gs;
            string context = state.RewardRequestContext;
            gameplayAdShowing = true;
            try
            {
                var request = serviceSet.Ads.ShowRewardedAsync(ticket.placement);
                var winner = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(90)));
                if (this == null || gs != state || context != state.RewardRequestContext)
                    return ServiceResult<RewardedAdResult>.Failed(ServiceMessage.Get("reward.changed"));
                bool earned = false;
                ServiceResult<RewardedAdResult> result;
                if (winner != request)
                {
                    ObserveLateAdCompletion(request);
                    result = ServiceResult<RewardedAdResult>.Failed(ServiceMessage.Get("reward.timeout"));
                }
                else
                {
                    result = await request;
                    earned = result.succeeded && result.value.rewardEarned && result.value.placementId == ticket.placement &&
                        serviceSet.Consent.Current.adsAllowed;
                    if (result.succeeded && result.value.rewardEarned && !earned)
                        result = ServiceResult<RewardedAdResult>.Failed(ServiceMessage.Get("reward.changed"));
                }

                static async void ObserveLateAdCompletion(Task<ServiceResult<RewardedAdResult>> request)
                {
                    try { await request; }
                    catch (Exception error) { Debug.LogWarning("Frontier: expired ad request failed: " + error.Message); }
                }
                if (!state.ResolveGameplayReward(context, ticket.id, earned, out var error))
                    return ServiceResult<RewardedAdResult>.Failed(error);
                return result;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (!state.ResolveGameplayReward(context, ticket.id, false, out var saveError))
                    Debug.LogWarning("Frontier: ad request recovery required: " + saveError);
                return ServiceResult<RewardedAdResult>.Failed(ServiceMessage.Get("reward.failed"));
            }
            finally { gameplayAdShowing = false; }
        }

        void BuildConstructionOffer(VisualElement body, PlacedStructure job)
        {
            if (gameplayRewardRules == null || !GameplayOfferAllowed(gameplayRewardRules.constructionEnabled, "timer-skip", false)) return;
            int seconds = Math.Min(gameplayRewardRules.constructionSeconds, gs.BuilderJobSecondsRemaining(job));
            if (seconds <= 0) return;
            Button watch = null;
            watch = IconBtn(UiText.Get("reward.construction", FormatJobDuration(seconds)), "play", UiKit.Cyan, async () =>
            {
                if (!await RequestRewardedConsent() || this == null) return;
                int current = Math.Min(gameplayRewardRules.constructionSeconds, gs.BuilderJobSecondsRemaining(job));
                if (current <= 0 || !GameplayOfferAllowed(gameplayRewardRules.constructionEnabled, "timer-skip"))
                { Toast(UiText.Get("reward.changed"), UiKit.Amber); RefreshModal(); return; }
                if (!gs.PrepareGameplayReward("timer-skip", null, current, job, out var ticket, out var error))
                { Toast(error, UiKit.Amber); return; }
                UiKit.SetEnabledLook(watch, false);
                var result = await ShowGameplayReward(ticket);
                if (this == null) return;
                Toast(result.succeeded && result.value.rewardEarned ? UiText.Get("reward.saved") :
                    result.succeeded ? UiText.Get("reward.not-earned") : result.text.Resolve(UiText.Get), UiKit.Cyan);
                RefreshModal();
            });
            watch.name = "construction-reward";
            body.Add(watch);
        }

        void BuildRewardRecovery(VisualElement body)
        {
            var ticket = gs.GameplayReward;
            if (ticket == null || ticket.state == "settled" || ticket.state == "cancelled" || gameplayAdShowing) return;
            body.Add(Note(UiText.Get(ticket.state == "earned" ? "reward.recovery-earned" : "reward.recovery-pending"), UiKit.Amber));
            if (ticket.state == "earned")
                body.Add(IconBtn(UiText.Get("reward.retry"), "check", UiKit.Lime, () =>
                {
                    if (!gs.SettleGameplayReward(out var error)) Toast(error, UiKit.Amber);
                    RefreshModal();
                }));
            body.Add(IconBtn(UiText.Get(ticket.state == "earned" ? "reward.discard" : "reward.dismiss"), "close", UiKit.Muted, () =>
            {
                if (ticket.state == "earned" && rewardDiscardArmed != ticket.id)
                {
                    rewardDiscardArmed = ticket.id;
                    Toast(UiText.Get("reward.confirm-discard"), UiKit.Amber);
                    return;
                }
                if (!gs.CancelUnverifiedGameplayReward(out var error)) Toast(error, UiKit.Amber);
                RefreshModal();
            }));
        }
    }
}
