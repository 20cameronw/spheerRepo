#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        void BuildStateTestControls(VisualElement body)
        {
            var card = Card(UiKit.Cyan);
            card.name = "debug-state-controls";
            card.AddToClassList("fr-debug-state");
            card.Add(UiKit.Text(UiText.Get("debug.state-help"), "fr-muted"));
            void Number(VisualElement parent, string label, int value, Action<int> set)
            {
                var input = new IntegerField(label) { value = value };
                input.style.minHeight = UiKit.Size.Touch;
                input.labelElement.style.whiteSpace = WhiteSpace.Normal;
                input.labelElement.style.minWidth = 0;
                input.labelElement.style.flexShrink = 1;
                input.labelElement.style.flexBasis = Length.Percent(45);
                parent.Add(input);
                parent.Add(IconBtn(UiText.Get("debug.apply"), "check", UiKit.Cyan, () =>
                {
                    if (input.value < 0) { Toast(UiText.Get("debug.invalid"), UiKit.Amber); return; }
                    set(input.value);
                    RefreshModal();
                }));
            }
            Number(card, UiText.Get("shards.title"), gs.ShardBalance, gs.DebugSetShards);
            Number(card, UiText.Get("debug.level"), gs.PlayerLevel, level => gs.DebugSetLevel(Mathf.Max(1, level)));
            Number(card, UiText.Get("debug.exact-xp"), gs.xp, gs.DebugSetXp);
            card.Add(IconBtn(UiText.Get("debug.finish-jobs"), "builders", UiKit.Cyan, gs.DebugFinishAllJobs));
            var research = new Foldout { text = UiText.Get("debug.research") };
            card.Add(research);
            foreach (var node in gs.db.research)
            {
                var item = node;
                var toggle = new Toggle(node.LocalizedName) { value = gs.IsResearched(node.data.id) };
                toggle.style.minHeight = UiKit.Size.Touch;
                toggle.RegisterValueChangedCallback(e => gs.DebugSetResearch(item, e.newValue));
                research.Add(toggle);
            }
            var tools = new Foldout { text = UiText.Get("debug.tools") };
            card.Add(tools);
            foreach (var ladder in gs.db.toolLadders.Where(ladder => gs.ToolUpgradeLadder(ladder) == ladder))
            {
                var item = ladder;
                int max = ladder.data.tiers.Max(row => row.tier);
                Number(tools, ladder.LocalizedName, gs.ToolTier(ladder),
                    tier => gs.DebugSetToolTier(item, Mathf.Clamp(tier, 1, max)));
            }
            var upgrades = new Foldout { text = UiText.Get("debug.individual-upgrades") };
            card.Add(upgrades);
            foreach (var upgrade in gs.db.upgrades)
            {
                var item = upgrade;
                Number(upgrades, upgrade.LocalizedName, gs.Level(upgrade),
                    level => gs.DebugSetUpgrade(item, Mathf.Clamp(level, 0, item.MaxLevel)));
            }
            var inventory = new Foldout { text = UiText.Get("debug.inventory-exact") };
            card.Add(inventory);
            foreach (var resource in gs.db.resources)
            {
                var item = resource;
                Number(inventory, resource.LocalizedName, gs.Count(resource), amount => gs.DebugSetResource(item, amount));
            }
            var structures = new Foldout { text = UiText.Get("debug.structures") };
            card.Add(structures);
            foreach (var structure in gs.structures)
            {
                var item = structure;
                var section = UiKit.Div();
                Number(section, structure.def.LocalizedName + " / " + gs.universe.Get(structure.planetId).Name,
                    structure.level, level => gs.DebugSetStructureLevel(item, Mathf.Clamp(level, 1, item.def.maxLevel)));
                var upgradeJob = new Toggle(UiText.Get("debug.upgrade-job")) { value = gs.IsUpgradeInProgress(item) };
                upgradeJob.style.minHeight = UiKit.Size.Touch;
                upgradeJob.SetEnabled(item.level < item.def.maxLevel);
                section.Add(upgradeJob);
                Number(section, UiText.Get("debug.timer-seconds"), gs.BuilderJobSecondsRemaining(item),
                    seconds => gs.DebugSetBuilderJob(item, seconds,
                        upgradeJob.value && item.level < item.def.maxLevel ? item.level + 1 : 0));
                structures.Add(section);
            }
            var place = new Foldout { text = UiText.Get("debug.place") };
            var clearance = new Foldout { text = UiText.Get("debug.clearance-jobs") };
            card.Add(clearance);
            foreach (var delta in gs.world.All)
            {
                var planet = gs.universe.Get(delta.planetId);
                foreach (var job in delta.obstacleJobs)
                {
                    int pad = job.pad;
                    Number(clearance, UiText.Get("debug.clearance-pad", planet.Name, pad + 1),
                        gs.ObstacleClearSecondsRemaining(planet, pad),
                        seconds => gs.DebugSetObstacleTimer(planet, pad, seconds));
                }
            }
            card.Add(place);
            foreach (var definition in gs.db.structures.Where(definition => definition.Implemented))
            {
                var item = definition;
                place.Add(IconBtn(definition.LocalizedName, "build", UiKit.Cyan, () =>
                {
                    int slot = Enumerable.Range(0, gs.planet.BuildingSlots).Where(
                        pad => gs.StructureAt(gs.planet, pad) == null && !gs.ObstacleAt(gs.planet, pad).HasValue)
                        .DefaultIfEmpty(-1).First();
                    if (slot < 0 || gs.DebugPlace(item, slot) == null)
                        Toast(UiText.Get("debug.no-pad"), UiKit.Amber);
                    RefreshModal();
                }));
            }
            body.Add(card);
            body.Add(SectionLabel(UiText.Get("debug.minigames")));
            foreach (MinigameKind kind in Enum.GetValues(typeof(MinigameKind)))
            {
                var game = kind;
                body.Add(IconBtn(kind.ToString(), "play", UiKit.Cyan, () => DebugGather(game)));
            }
            body.Add(UiKit.Text(UiText.Get("debug.minigames-help"), "fr-tiny"));
        }
    }
}
#endif
