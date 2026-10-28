using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        bool showAllObjectives, showCompletedObjectives;

        void BuildGoals(VisualElement body)
        {
            var page = UiKit.Div();
            page.name = "frontier-goals";
            page.Add(UiKit.Text(UiText.Get("goals.title"), "fr-h2"));
            page.Add(UiKit.Text(UiText.Get("goals.help"), "fr-muted"));
            body.Add(page);
            if (gs.Objectives.Count == 0)
            {
                page.Add(UiKit.Text(UiText.Get("goals.unavailable"), "fr-muted", UiKit.Amber));
                return;
            }

            int completed = 0, ready = 0;
            foreach (var objective in gs.Objectives)
            {
                if (gs.ObjectiveClaimed(objective.id)) completed++;
                else if (gs.ObjectiveCompleted(objective.id)) ready++;
            }
            page.Add(Note(UiText.Get("goals.summary", completed, gs.Objectives.Count, ready), UiKit.Amber));
            if (gs.SaveIsReadOnly)
                page.Add(UiKit.Text(UiText.Get("goals.read-only"), "fr-muted", UiKit.Amber));
            if (ready > 0)
            {
                page.Add(UiKit.Text(UiText.Get("goals.ready"), "fr-h2"));
                foreach (var objective in gs.Objectives)
                    if (gs.ObjectiveCompleted(objective.id) && !gs.ObjectiveClaimed(objective.id))
                        page.Add(ObjectiveCard(objective));
            }
            if (completed + ready < gs.Objectives.Count)
            {
                page.Add(UiKit.Text(UiText.Get("goals.next"), "fr-h2"));
                int shown = 0, remaining = gs.Objectives.Count - completed - ready;
                foreach (var objective in gs.Objectives)
                {
                    if (gs.ObjectiveCompleted(objective.id)) continue;
                    if (!showAllObjectives && shown >= 3) break;
                    page.Add(ObjectiveCard(objective));
                    shown++;
                }
                if (remaining > 3)
                    page.Add(IconBtn(UiText.Get(showAllObjectives ? "goals.show-three" : "goals.show-all", remaining),
                        "star", UiKit.Amber, () => { showAllObjectives = !showAllObjectives; RefreshModal(); }));
            }
            if (completed > 0)
            {
                page.Add(IconBtn(UiText.Get(showCompletedObjectives ? "goals.hide-collected" : "goals.show-collected", completed),
                    "check", UiKit.Lime, () => { showCompletedObjectives = !showCompletedObjectives; RefreshModal(); }));
                if (showCompletedObjectives)
                    foreach (var objective in gs.Objectives)
                        if (gs.ObjectiveClaimed(objective.id)) page.Add(ObjectiveCard(objective));
            }
        }

        VisualElement ObjectiveCard(ObjectiveDefinition objective)
        {
            bool claimed = gs.ObjectiveClaimed(objective.id), complete = gs.ObjectiveCompleted(objective.id);
            var accent = claimed ? UiKit.Lime : UiKit.Amber;
            var card = Card(accent);
            card.name = "objective-" + objective.id;
            card.Add(UiKit.Text(objective.LocalizedTitle, "fr-h2"));
            card.Add(UiKit.Text(objective.LocalizedDescription, "fr-muted"));
            if (claimed)
            {
                card.Add(Note(UiText.Get("goals.recorded"), UiKit.Lime));
                return card;
            }
            int reward = gs.ObjectiveReward(objective);
            card.Add(Note(UiText.Get("goals.reward-summary", complete ? UiText.Get("goals.complete")
                : UiText.Get("goals.progress", gs.ObjectiveProgress(objective), objective.target), reward), accent));
            Button action;
            if (complete)
            {
                action = IconBtn(UiText.Get("goals.collect", reward), "star", UiKit.Amber, () =>
                {
                    var result = gs.ClaimObjective(objective.id, out var message);
                    Toast(message, result == ObjectiveClaimResult.Saved ? UiKit.Lime : UiKit.Amber);
                    RefreshModal();
                });
                action.name = "objective-claim-" + objective.id;
                UiKit.SetEnabledLook(action, !gs.SaveIsReadOnly);
            }
            else
            {
                string label = UiText.Get(objective.route == ObjectiveRoute.Gather ? "guide.action.gather-shop"
                    : objective.route == ObjectiveRoute.Build ? "guide.action.build-shop"
                    : objective.route == ObjectiveRoute.Map ? "guide.action.map"
                    : objective.route == ObjectiveRoute.Command ? "goals.action.command" : "goals.action.recipe");
                action = IconBtn(label, objective.route == ObjectiveRoute.Map ? "map" : "play", accent, () => OpenObjectiveRoute(objective.route));
                action.name = "objective-action-" + objective.id;
            }
            card.Add(action);
            return card;
        }

        void OpenObjectiveRoute(ObjectiveRoute route)
        {
            switch (route)
            {
                case ObjectiveRoute.Gather: OpenGatherShop(); break;
                case ObjectiveRoute.Build: shopTab = 0; OpenShop(); break;
                case ObjectiveRoute.Map: OpenMap(); break;
                case ObjectiveRoute.Command: researchTab = 2; progressionTab = 0; OpenResearch(); break;
                case ObjectiveRoute.Atlas: atlasTab = 1; OpenGuideAtlas(); break;
            }
        }
    }
}
