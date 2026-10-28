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

        bool GameplayOfferAllowed(bool placementEnabled, string placement)
        {
            var ticket = gs.GameplayReward;
            return placementEnabled && services != null && services.Ads.IsConfigured &&
                services.Consent.Current.hasDecision && services.Consent.Current.adsAllowed &&
                (!(services.Ads is IRewardedAdAvailability availability) || availability.RewardedAvailability(placement).succeeded) &&
                !gs.SaveIsReadOnly && !gameplayAdShowing &&
                (ticket == null || ticket.state == "settled" || ticket.state == "cancelled");
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
            if (gameplayRewardRules == null || !GameplayOfferAllowed(gameplayRewardRules.constructionEnabled, "timer-skip")) return;
            int seconds = Math.Min(gameplayRewardRules.constructionSeconds, gs.BuilderJobSecondsRemaining(job));
            if (seconds <= 0) return;
            Button watch = null;
            watch = IconBtn(UiText.Get("reward.construction", FormatJobDuration(seconds)), "play", UiKit.Cyan, async () =>
            {
                int current = Math.Min(gameplayRewardRules.constructionSeconds, gs.BuilderJobSecondsRemaining(job));
                if (current != seconds || !GameplayOfferAllowed(gameplayRewardRules.constructionEnabled, "timer-skip"))
                { Toast(UiText.Get("reward.changed"), UiKit.Amber); RefreshModal(); return; }
                if (!gs.PrepareGameplayReward("timer-skip", null, seconds, job, out var ticket, out var error))
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
