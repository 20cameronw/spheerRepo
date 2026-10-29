using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        static readonly string[] FirstSessionTitleKeys =
        {
            "guide.build.title", "guide.gather.title", "guide.store.title",
            "guide.craft.title", "guide.defend.title", "guide.travel.title"
        };

        static readonly string[] FirstSessionDescriptionKeys =
        {
            "guide.build.help", "guide.gather.help", "guide.store.help",
            "guide.craft.help", "guide.defend.help", "guide.travel.help"
        };

        VisualElement firstSessionGuideCard;
        Label firstSessionGuideProgress, firstSessionGuideTitle, firstSessionGuideDescription;
        Button firstSessionGuideAction;
        bool firstSessionGuideActive, firstSessionGuideVisibilityKnown, firstSessionGuideVisible;
        bool debugFirstSessionGuideOverride;
        int debugFirstSessionGuideStep;
        FirstSessionFunnel firstSessionFunnel;

        void BuildFirstSessionGuide()
        {
            if (services != null && firstSessionFunnel == null)
                firstSessionFunnel = new FirstSessionFunnel(services, message => Debug.LogWarning(message));
            firstSessionGuideCard = UiKit.Div("fr-guide-card");
            firstSessionGuideCard.pickingMode = PickingMode.Position;
            PositionFirstSessionGuide();

            var heading = UiKit.Div("fr-guide-heading");
            firstSessionGuideTitle = UiKit.Text("", "fr-guide-title fr-h2");
            heading.Add(firstSessionGuideTitle);
            firstSessionGuideProgress = UiKit.Text("", "fr-guide-progress fr-tiny", UiKit.Cyan);
            heading.Add(firstSessionGuideProgress);

            var skip = Icons.IconButton("close", UiKit.Muted, DismissFirstSessionGuide, 0f, true);
            skip.name = "first-session-guide-skip";
            skip.tooltip = UiText.Get("guide.skip");
            skip.AddToClassList("fr-guide-close");
            heading.Add(skip);
            firstSessionGuideCard.Add(heading);

            firstSessionGuideDescription = UiKit.Text("", "fr-guide-description fr-muted");
            firstSessionGuideCard.Add(firstSessionGuideDescription);

            firstSessionGuideAction = UiKit.Btn("", UiKit.Lime, RunFirstSessionGuideAction, false, true);
            firstSessionGuideAction.name = "first-session-guide-action";
            firstSessionGuideAction.AddToClassList("fr-guide-action");
            firstSessionGuideCard.Add(firstSessionGuideAction);
            hud.Add(firstSessionGuideCard);
            RefreshFirstSessionGuide();
        }

        void PositionFirstSessionGuide()
        {
            firstSessionGuideCard.style.position = Position.Absolute;
            firstSessionGuideCard.style.left = UiKit.Space.S16;
            firstSessionGuideCard.style.right = UiKit.Space.S16;
            firstSessionGuideCard.style.top = HudTopInset;
        }

        int CurrentFirstSessionGuideStep
        {
            get
            {
                if (debugFirstSessionGuideOverride) return Mathf.Clamp(debugFirstSessionGuideStep, 0, FirstSessionObjectives.Count);
                return !gs.SaveIsReadOnly && gs.FirstSessionGuideAvailable ? FirstSessionObjectives.CurrentStep(gs) : -1;
            }
        }

        bool IsFirstSessionBuildTarget(StructureKind kind)
        {
            switch (CurrentFirstSessionGuideStep)
            {
                case 0: return kind == StructureKind.Lumber;
                case 2: return kind == StructureKind.Warehouse;
                case 3: return kind == StructureKind.Refinery;
                case 4: return kind == StructureKind.Alarm;
                default: return false;
            }
        }

        void AddFirstSessionCue(VisualElement parent, int step)
        {
            if (CurrentFirstSessionGuideStep != step) return;
            var cue = UiKit.Div("fr-guide-cue");
            cue.name = "first-session-cue-" + step;
            cue.pickingMode = PickingMode.Ignore;
            var text = UiKit.Text(UiText.Get(FirstSessionTitleKeys[step]), "fr-guide-cue-text fr-tiny", UiKit.Lime);
            text.pickingMode = PickingMode.Ignore;
            cue.Add(text);
            var arrow = Icons.Make("up", UiKit.Lime, UiKit.Size.IconSm);
            arrow.pickingMode = PickingMode.Ignore;
            arrow.AddToClassList("fr-guide-cue-arrow");
            cue.Add(arrow);
            parent.Add(cue);
        }

        void RefreshFirstSessionGuide()
        {
            if (firstSessionGuideCard == null) return;
            firstSessionGuideActive = debugFirstSessionGuideOverride || (!gs.SaveIsReadOnly && gs.FirstSessionGuideAvailable);
            if (!debugFirstSessionGuideOverride)
                firstSessionFunnel?.Observe(gs.FirstSessionGuideIdentity, firstSessionGuideActive, FirstSessionObjectives.CurrentStep(gs));
            if (!firstSessionGuideActive)
            {
                UpdateFirstSessionGuideVisibility();
                return;
            }

            int step = debugFirstSessionGuideOverride
                ? Mathf.Clamp(debugFirstSessionGuideStep, 0, FirstSessionObjectives.Count)
                : FirstSessionObjectives.CurrentStep(gs);
            bool complete = step >= FirstSessionObjectives.Count;
            firstSessionGuideProgress.text = UiText.Get("guide.progress",
                complete ? FirstSessionObjectives.Count : step + 1, FirstSessionObjectives.Count);
            firstSessionGuideTitle.text = UiText.Get(complete ? "guide.complete.title" : FirstSessionTitleKeys[step]);
            firstSessionGuideDescription.text = complete
                ? UiText.Get("guide.complete.help")
                : UiText.Get(FirstSessionDescriptionKeys[step]);
            firstSessionGuideAction.text = complete ? UiText.Get("guide.action.atlas") : GuideActionLabel(step);
            UpdateFirstSessionGuideVisibility();
        }

        string GuideActionLabel(int step)
        {
            switch (step)
            {
                case 0: return UiText.Get("guide.action.gather-shop");
                case 1: return UiText.Get(gs.StructureOfKind(gs.planet, StructureKind.Lumber) != null
                    ? "guide.action.lumber" : "guide.action.gather-shop");
                case 2: return UiText.Get("guide.action.build-shop");
                case 3: return UiText.Get(gs.StructureOfKind(gs.planet, StructureKind.Refinery) != null
                    ? "guide.action.refinery" : "guide.action.build-shop");
                case 4: return UiText.Get(gs.StructureOfKind(gs.planet, StructureKind.Alarm) != null
                    ? "guide.action.alarm" : "guide.action.alarm-shop");
                default: return UiText.Get("guide.action.map");
            }
        }

        void UpdateFirstSessionGuideVisibility()
        {
            if (firstSessionGuideCard == null) return;
            bool visible = firstSessionGuideActive && modal == null && popup == null && !mapOpen &&
                buildDef == null && !busy && (defense == null || !defense.Active);
            if (!firstSessionGuideVisibilityKnown || visible != firstSessionGuideVisible)
            {
                firstSessionGuideVisibilityKnown = true;
                firstSessionGuideVisible = visible;
                firstSessionGuideCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        void DismissFirstSessionGuide()
        {
            if (debugFirstSessionGuideOverride)
                debugFirstSessionGuideOverride = false;
            else
            {
                firstSessionFunnel?.Dismiss(FirstSessionObjectives.CurrentStep(gs));
                gs.DismissFirstSessionGuide();
            }
            RefreshFirstSessionGuide();
        }

        void RunFirstSessionGuideAction()
        {
            int step = debugFirstSessionGuideOverride
                ? Mathf.Clamp(debugFirstSessionGuideStep, 0, FirstSessionObjectives.Count)
                : FirstSessionObjectives.CurrentStep(gs);
            if (!debugFirstSessionGuideOverride) firstSessionFunnel?.Action(step);

            if (step == 1 && OpenFirstSessionStructure(StructureKind.Lumber)) return;
            if (step == 3 && OpenFirstSessionStructure(StructureKind.Refinery)) return;
            if (step == 4 && OpenFirstSessionStructure(StructureKind.Alarm)) return;
            if (step == 0 || step == 1)
            {
                OpenGatherShop();
            }
            else if (step == 2 || step == 3 || step == 4)
            {
                shopTab = 0;
                OpenShop();
            }
            else if (step == 5)
                OpenMap();
            else if (step >= FirstSessionObjectives.Count)
                OpenGuideAtlas();
        }

        void OpenGuideAtlas()
        {
            if (defense.Active || busy) return;
            if (modalKey != "menu") OpenMenu();
            menuPage = "atlas";
            RefreshModal();
        }

        bool OpenFirstSessionStructure(StructureKind kind)
        {
            var structure = gs.StructureOfKind(gs.planet, kind);
            if (structure == null) return false;
            if (!world.SelectStructure(structure))
                Toast(UiText.Get("guide.structure-appearing"), UiKit.Amber);
            return true;
        }

        void OpenGatherShop()
        {
            shopTab = 1;
            OpenShop();
        }

        PlacedStructure AvailableLocalGatherer()
        {
            foreach (var structure in gs.StructuresOn(gs.planet))
                if (structure.def.kind.IsGather() && ReadyToHarvest(structure) &&
                    gs.SpotsFor(gs.planet, structure.def.kind).Count > 0) return structure;
            return null;
        }

        void RunEmptyPackAction()
        {
            var gatherer = AvailableLocalGatherer();
            if (gatherer == null) OpenHarvestList();
            else if (!world.SelectStructure(gatherer))
                Toast(UiText.Get("guide.gatherer-appearing"), UiKit.Amber);
        }
    }
}
