using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        string researchBranch = "";
        int progressionTab;

        void BuildXpProgress(VisualElement body, bool showWaveAction)
        {
            var progress = UiKit.Div();
            progress.name = "player-xp-progression";
            progress.Add(UiKit.Text(UiText.Get("progression.xp-status", gs.PlayerLevel, gs.XpToNextLevel,
                gs.PlayerLevel + 1), "fr-muted"));
            progress.Add(UiKit.Text(UiText.Get("progression.xp-help"), "fr-tiny"));
            body.Add(progress);
            if (showWaveAction)
            {
                var waves = IconBtn(UiText.Get("progression.waves"), "alien", UiKit.Magenta, OpenProgressionWaves);
                waves.name = "progression-open-waves";
                body.Add(waves);
            }
        }

        void OpenProgressionWaves()
        {
            if (busy || defense.Active) return;
            CloseAll();
            var alarm = gs.StructureOfKind(gs.planet, StructureKind.Alarm);
            if (alarm != null)
            {
                if (world.SelectStructure(alarm)) return;
                Debug.LogError("Frontier: Alarm has no world anchor: " + alarm.def.id);
                return;
            }
            shopTab = 0;
            OpenShop();
            var def = gs.db.StructureOfKind(StructureKind.Alarm);
            var scroll = modalScroll;
            var card = def != null ? scroll.Q("shop-structure-" + def.id) : null;
            if (card != null) scroll.schedule.Execute(() =>
            {
                if (scroll.contentContainer.Contains(card)) scroll.ScrollTo(card);
            }).StartingIn(1);
        }

        void BuildTech(VisualElement body)
        {
            if (!gs.db.UsesImportedEconomy || gs.db.research == null)
            {
                body.Add(UiKit.Text(UiText.Get("research.unavailable"), "fr-muted"));
                return;
            }

            var branches = new List<string> { "" };
            foreach (var node in gs.db.research)
                if (node != null && node.data != null && !string.IsNullOrEmpty(node.data.branch) &&
                    !branches.Contains(node.data.branch)) branches.Add(node.data.branch);
            branches.Sort(1, branches.Count - 1, StringComparer.Ordinal);
            branches[0] = UiText.Get("research.all");

            var filter = new DropdownField();
            filter.AddToClassList("fr-tech-filter");
            filter.choices = branches.Select((branch, index) => index == 0 ? branch : PrettyName(branch)).ToList();
            int selectedBranch = string.IsNullOrEmpty(researchBranch) ? 0 : branches.IndexOf(researchBranch);
            filter.index = Mathf.Max(0, selectedBranch);
            filter.style.minHeight = UiKit.Size.Touch;
            filter.style.marginTop = UiKit.Space.S4;
            filter.style.marginBottom = UiKit.Space.S12;
            var filterSurface = new Color(15f / 255f, 23f / 255f, 50f / 255f);
            var filterText = new Color(234f / 255f, 245f / 255f, 1f);
            var filterOutline = new Color(38f / 255f, 54f / 255f, 92f / 255f);
            filter.style.backgroundColor = filterSurface;
            filter.style.color = filterText;
            filter.style.borderTopWidth = filter.style.borderBottomWidth = 2f;
            filter.style.borderLeftWidth = filter.style.borderRightWidth = 2f;
            filter.style.borderTopColor = filter.style.borderBottomColor = filterOutline;
            filter.style.borderLeftColor = filter.style.borderRightColor = filterOutline;
            filter.style.borderTopLeftRadius = filter.style.borderTopRightRadius = 18f;
            filter.style.borderBottomLeftRadius = filter.style.borderBottomRightRadius = 18f;
            var filterInput = filter.Q<VisualElement>(className: "unity-base-field__input");
            if (filterInput != null)
            {
                filterInput.style.backgroundColor = filterSurface;
                filterInput.style.color = filterText;
            }
            filter.RegisterValueChangedCallback(e =>
            {
                int index = filter.choices.IndexOf(e.newValue);
                if (index < 0) return;
                researchBranch = index == 0 ? "" : branches[index];
                RefreshModal();
            });
            body.Add(UiKit.Text(UiText.Get("research.branch"), "fr-tiny"));
            body.Add(filter);

            var nodes = gs.db.research.Where(node => node != null && node.data != null &&
                (string.IsNullOrEmpty(researchBranch) || node.data.branch == researchBranch))
                .OrderBy(node => node.data.tier)
                .ThenBy(node => node.data.branch, StringComparer.Ordinal)
                .ThenBy(node => node.LocalizedName, StringComparer.Ordinal);
            foreach (var node in nodes) BuildResearchCard(body, node);
        }

        void BuildResearchCard(VisualElement body, ResearchNodeSO node)
        {
            bool complete = gs.IsResearched(node.data.id);
            string lockReason = complete ? null : gs.ResearchLockReason(node);
            var card = Card(UiKit.Magenta, complete);
            var head = UiKit.Div("fr-row");
            head.Add(UiKit.Text(node.LocalizedName.ToUpper(), "fr-h2"));
            head.Add(UiKit.Div("fr-spacer"));
            head.Add(new FrontierBadge { text = UiText.Get(complete ? "research.complete" : "research.tier", Math.Max(1, node.data.tier)),
                Tint = complete ? UiKit.Lime : UiKit.Muted });
            card.Add(head);
            card.Add(UiKit.Text(UiText.Get("research.lab", PrettyName(node.data.branch), node.data.labLevel), "fr-tiny"));
            card.Add(UiKit.Text(node.LocalizedDescription, "fr-muted"));
            if (node.data.prereqs != null && node.data.prereqs.Length > 0)
                card.Add(UiKit.Text(UiText.Get("research.prerequisites", string.Join(", ", node.data.prereqs.Select(PrettyName))), "fr-small"));
            if (!complete) card.Add(UiKit.CostRow(CatalogCost(node.data.cost), gs, true, OpenResourceSource));
            if (complete)
                card.Add(Note(UiText.Get("research.complete-help", UnlockSummary(node.data.unlocks)), UiKit.Lime));
            else if (lockReason == null)
            {
                var buy = IconBtn(UiText.Get("research.buy"), "research", UiKit.Magenta, () =>
                {
                    if (gs.Research(node)) { Sfx.Good(); Toast(UiText.Get("research.purchased", node.LocalizedName), UiKit.Magenta); }
                    else Sfx.Bad();
                });
                UiKit.SetEnabledLook(buy, true);
                card.Add(buy);
                if (node.data.unlocks != null && node.data.unlocks.Length > 0)
                    card.Add(UiKit.Text(UiText.Get("research.unlocks", UnlockSummary(node.data.unlocks)), "fr-tiny"));
            }
            else card.Add(Note(lockReason, UiKit.Amber));
            body.Add(card);
        }

        void BuildCatalogTools(VisualElement body)
        {
            if (!gs.db.UsesImportedEconomy || gs.db.toolLadders == null) return;
            body.Add(SectionLabel(UiText.Get("tools.title")));
            body.Add(UiKit.Text(UiText.Get("tools.help"), "fr-muted"));
            foreach (var ladder in gs.db.toolLadders)
            {
                if (ladder == null || ladder.data == null || ladder.data.tiers == null || ladder.data.tiers.Length == 0) continue;
                if (gs.ToolUpgradeLadder(ladder) != ladder) continue;
                int current = gs.ToolTier(ladder);
                var currentRow = FindToolRow(ladder, current);
                var nextRow = FindToolRow(ladder, current + 1);
                string lockReason = gs.ToolTierLockReason(ladder);
                var color = ladder.color == default(Color) ? UiKit.Magenta : UiKit.Ui(ladder.color);
                var card = Card(color, nextRow == null);
                var head = UiKit.Div("fr-row");
                head.Add(UiKit.Text(ladder.data.kind == ToolKind.Pick ? UiText.Get("tools.mining") : ladder.LocalizedName.ToUpper(), "fr-h2"));
                head.Add(UiKit.Div("fr-spacer"));
                head.Add(UiKit.Text(nextRow == null ? UiText.Get("tools.max")
                    : UiText.Get("tools.tier", current, ladder.data.tiers.Where(row => row != null).Max(row => row.tier)), "fr-tiny"));
                card.Add(head);
                if (ladder.data.kind == ToolKind.Pick)
                    card.Add(UiKit.Text(UiText.Get("tools.mining.help"), "fr-muted"));
                if (currentRow != null)
                    card.Add(UiKit.Text(UiText.Get("tools.yield", ladder.TierName(currentRow), currentRow.yieldMult), "fr-small"));
                if (nextRow == null)
                {
                    card.Add(Note(UiText.Get("tools.max-help"), UiKit.Lime));
                }
                else
                {
                    card.Add(UiKit.Text(UiText.Get("tools.next", ladder.TierName(nextRow), nextRow.yieldMult), "fr-small"));
                    card.Add(UiKit.Text(UiText.Get("progression.required-level", Math.Max(1, nextRow.requiresReachTier)), "fr-tiny"));
                    card.Add(UiKit.CostRow(CatalogCost(nextRow.cost), gs, true, OpenResourceSource));
                    if (lockReason != null) card.Add(Note(lockReason, UiKit.Amber));
                    else
                    {
                        var buy = IconBtn(UiText.Get("tools.upgrade"), "up", color, () =>
                        {
                            if (gs.BuyToolTier(ladder)) { Sfx.Good(); Toast(UiText.Get("tools.purchased", ladder.TierName(nextRow)), color); }
                            else Sfx.Bad();
                        });
                        UiKit.SetEnabledLook(buy, true);
                        card.Add(buy);
                    }
                }
                body.Add(card);
            }
        }

        static ToolTierDef FindToolRow(ToolLadderSO ladder, int tier)
        {
            if (ladder == null || ladder.data == null || ladder.data.tiers == null) return null;
            foreach (var row in ladder.data.tiers)
                if (row != null && row.tier == tier) return row;
            return null;
        }

        List<ItemAmount> CatalogCost(Frontier.Economy.ItemAmount[] cost)
        {
            return ItemAmounts.FromIds(cost, gs.db.ResolveResource);
        }

        static string UnlockSummary(string[] unlocks)
        {
            if (unlocks == null || unlocks.Length == 0) return UiText.Get("research.progression");
            return string.Join(", ", unlocks.Select(PrettyName));
        }
    }
}
