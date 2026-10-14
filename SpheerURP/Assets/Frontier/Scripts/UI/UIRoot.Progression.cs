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

        void BuildProgression(VisualElement body)
        {
            var tabs = UiKit.Div("fr-tabs");
            tabs.Add(TabButton("research", "TECH", progressionTab == 0, UiKit.Magenta, () => progressionTab = 0));
            tabs.Add(TabButton("ship", "SHIP", progressionTab == 1, UiKit.Magenta, () => progressionTab = 1));
            tabs.Add(TabButton("shield", "DEFENSE", progressionTab == 2, UiKit.Magenta, () => progressionTab = 2));
            body.Add(tabs);
            if (progressionTab == 0) BuildTech(body);
            else BuildUpgrades(body, progressionTab == 1 ? UpgradeCategory.Ship : UpgradeCategory.Defense);
        }

        void BuildTech(VisualElement body)
        {
            if (!gs.db.UsesImportedEconomy || gs.db.research == null)
            {
                body.Add(UiKit.Text("Research is not configured for this database.", "fr-muted"));
                return;
            }

            var branches = new List<string> { "" };
            foreach (var node in gs.db.research)
                if (node != null && node.data != null && !string.IsNullOrEmpty(node.data.branch) &&
                    !branches.Contains(node.data.branch)) branches.Add(node.data.branch);
            branches.Sort(1, branches.Count - 1, StringComparer.Ordinal);
            branches[0] = "All research";

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
            body.Add(UiKit.Text("RESEARCH BRANCH", "fr-tiny"));
            body.Add(filter);

            var nodes = gs.db.research.Where(node => node != null && node.data != null &&
                (string.IsNullOrEmpty(researchBranch) || node.data.branch == researchBranch))
                .OrderBy(node => node.data.tier)
                .ThenBy(node => node.data.branch, StringComparer.Ordinal)
                .ThenBy(node => node.data.displayName, StringComparer.Ordinal);
            foreach (var node in nodes) BuildResearchCard(body, node);
        }

        void BuildResearchCard(VisualElement body, ResearchNodeSO node)
        {
            bool complete = gs.IsResearched(node.data.id);
            string lockReason = complete ? null : gs.ResearchLockReason(node);
            var card = Card(UiKit.Magenta, complete);
            var head = UiKit.Div("fr-row");
            head.Add(UiKit.Text(node.data.displayName.ToUpper(), "fr-h2"));
            head.Add(UiKit.Div("fr-spacer"));
            head.Add(UiKit.Text(complete ? "COMPLETE" : "T" + node.data.tier, "fr-tiny", complete ? UiKit.Lime : (Color?)null));
            card.Add(head);
            card.Add(UiKit.Text(PrettyName(node.data.branch) + "  ·  LAB " + node.data.labLevel, "fr-tiny"));
            card.Add(UiKit.Text(node.data.description, "fr-muted"));
            if (node.data.prereqs != null && node.data.prereqs.Length > 0)
                card.Add(UiKit.Text("PREREQUISITES  ·  " + string.Join(", ", node.data.prereqs.Select(PrettyName)), "fr-small"));
            if (!complete) card.Add(UiKit.CostRow(CatalogCost(node.data.cost), gs, true));
            if (complete)
                card.Add(Note("Research complete. Unlocks: " + UnlockSummary(node.data.unlocks), UiKit.Lime));
            else if (lockReason == null)
            {
                var buy = IconBtn("RESEARCH", "research", UiKit.Magenta, () =>
                {
                    if (gs.Research(node)) { Sfx.Good(); Toast(node.data.displayName + " researched", UiKit.Magenta); }
                    else Sfx.Bad();
                });
                UiKit.SetEnabledLook(buy, true);
                card.Add(buy);
                if (node.data.unlocks != null && node.data.unlocks.Length > 0)
                    card.Add(UiKit.Text("UNLOCKS  ·  " + UnlockSummary(node.data.unlocks), "fr-tiny"));
            }
            else card.Add(Note(lockReason, UiKit.Amber));
            body.Add(card);
        }

        void BuildCatalogTools(VisualElement body)
        {
            if (!gs.db.UsesImportedEconomy || gs.db.toolLadders == null) return;
            body.Add(SectionLabel("CATALOG TOOL TIERS"));
            body.Add(UiKit.Text("Higher tiers improve harvest yield. Future harvest tools stay unavailable until their minigames ship.", "fr-muted"));
            foreach (var ladder in gs.db.toolLadders)
            {
                if (ladder == null || ladder.data == null || ladder.data.tiers == null || ladder.data.tiers.Length == 0) continue;
                int current = gs.ToolTier(ladder);
                var currentRow = FindToolRow(ladder, current);
                var nextRow = FindToolRow(ladder, current + 1);
                string lockReason = gs.ToolTierLockReason(ladder);
                bool supported = lockReason != "This tool's harvest game is not implemented";
                var color = ladder.color == default(Color) ? UiKit.Magenta : UiKit.Ui(ladder.color);
                var card = Card(color, nextRow == null);
                var head = UiKit.Div("fr-row");
                head.Add(UiKit.Text(ladder.data.displayName.ToUpper(), "fr-h2"));
                head.Add(UiKit.Div("fr-spacer"));
                head.Add(UiKit.Text(nextRow == null ? "MAX TIER" : "TIER " + current + "/" +
                    ladder.data.tiers.Where(row => row != null).Max(row => row.tier), "fr-tiny"));
                card.Add(head);
                if (currentRow != null)
                    card.Add(UiKit.Text(currentRow.displayName + "  ·  YIELD x" + currentRow.yieldMult.ToString("0.0"), "fr-small"));
                if (nextRow == null)
                {
                    card.Add(Note("Maximum tool tier reached.", UiKit.Lime));
                }
                else
                {
                    card.Add(UiKit.Text("NEXT  ·  " + nextRow.displayName + "  ·  YIELD x" + nextRow.yieldMult.ToString("0.0"), "fr-small"));
                    card.Add(UiKit.CostRow(CatalogCost(nextRow.cost), gs, true));
                    if (!supported) card.Add(Note(lockReason, UiKit.Amber));
                    else if (lockReason != null) card.Add(Note(lockReason, UiKit.Amber));
                    else
                    {
                        var buy = IconBtn("UPGRADE TOOL", "up", color, () =>
                        {
                            if (gs.BuyToolTier(ladder)) { Sfx.Good(); Toast(nextRow.displayName + " ready!", color); }
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
            if (unlocks == null || unlocks.Length == 0) return "Progression";
            return string.Join(", ", unlocks.Select(PrettyName));
        }
    }
}
