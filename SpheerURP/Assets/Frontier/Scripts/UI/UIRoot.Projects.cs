using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        VisualElement projectResume;
        bool returnSummaryDismissed;
        string projectContext;
        int observedReturnVersion;
        VisualElement shortageOverlay, shortageReturnFocus;
        ScrollView shortageScroll;
        PlayerProject shortageProject;
        ResourceSO shortageResource;
        int shortageRequired;

        void ResetProjectContextIfNeeded()
        {
            if (projectContext == gs.RewardRequestContext) return;
            projectContext = gs.RewardRequestContext;
            CloseShortage();
            returnSummaryDismissed = false;
        }

        PlayerProject Project(string kind, string id, int target = 1, int slot = -1, int quantity = 1)
        {
            return new PlayerProject { kind = kind, id = id, target = target, slot = slot,
                quantity = quantity, planetId = gs.planet.Id };
        }

        VisualElement ProjectCosts(List<ItemAmount> costs, PlayerProject project)
        {
            return UiKit.CostRow(costs, gs, true, resource =>
                OpenShortage(project, resource, costs.Where(c => c.resource == resource).Sum(c => c.amount)),
                resource => gs.ProjectResourceCount(project, resource));
        }

        void AddProjectAction(VisualElement body, PlayerProject project)
        {
            BuildProjectGates(body, project);
            var heading = body.childCount > 0 ? body.ElementAt(0) : null;
            bool inline = heading != null && (heading.ClassListContains("fr-row") || heading.ClassListContains("fr-card-head"));
            var track = ProjectTrackButton(project);
            if (inline)
            {
                track.style.marginLeft = UiKit.Space.S8;
                heading.Add(track);
            }
            else
            {
                track.style.alignSelf = Align.FlexEnd;
                body.Add(track);
            }
        }

        Button ProjectTrackButton(PlayerProject project)
        {
            var track = Icons.IconButton("star", UiKit.Muted, () =>
            {
                if (!gs.TrackProject(project, out var error)) Toast(error, UiKit.Amber);
                else Toast(UiText.Get("project.tracked", gs.ProjectState(project).name), UiKit.Cyan);
            });
            track.name = "track-" + project.kind + "-" + project.id;
            track.tooltip = UiText.Get("project.track");
            return track;
        }

        void BuildProjectGates(VisualElement body, PlayerProject project)
        {
            int xp = 1, lab = 0;
            string prerequisite = null;
            if (project.kind == "research")
            {
                var node = gs.db.research.First(n => n.data.id == project.id);
                xp = node.data.tier; lab = node.data.labLevel;
                prerequisite = node.data.prereqs?.FirstOrDefault(id => !gs.IsResearched(id));
            }
            else if (project.kind == "tool")
            {
                var ladder = gs.db.toolLadders.First(t => t.data.id == project.id);
                var tier = FindToolRow(ladder, gs.ToolTier(ladder) + 1);
                if (tier != null) { xp = tier.requiresReachTier; lab = tier.requiresLabLevel; prerequisite = tier.researchPrereq; }
            }
            else if (project.kind == "recipe")
            {
                var recipe = gs.db.recipes.First(r => r.id == project.id);
                xp = recipe.tier; prerequisite = recipe.unlockResearch;
                var station = gs.structures.FirstOrDefault(s => s.planetId == project.planetId && s.slot == project.slot);
                if (station != null && station.level < recipe.stationLevel)
                    body.Add(IconBtn(UiText.Get("project.upgrade-station"), "up", UiKit.Cyan,
                        () => OpenProject(Project("structure", station.def.id, station.level + 1, station.slot))));
            }
            else if (project.kind == "upgrade")
            {
                var upgrade = gs.db.FindUpgrade(project.id);
                var row = upgrade.NextRow(gs.Level(upgrade));
                if (row != null) { xp = row.requiresReachTier; prerequisite = row.researchPrereq; }
            }
            else if (project.kind == "build" || project.kind == "structure")
            {
                var definition = gs.db.FindStructure(project.id);
                var row = definition.Row(project.kind == "build" ? 1 : project.target);
                prerequisite = definition.unlockResearch;
                if (row != null) { xp = row.requiresReachTier; lab = row.requiresLabLevel; }
                xp = Math.Max(xp, definition.unlockTier);
            }
            if (!string.IsNullOrEmpty(prerequisite) && !gs.IsResearched(prerequisite))
                body.Add(IconBtn(UiText.Get("project.prerequisite"), "research", UiKit.Cyan,
                    () => OpenProject(Project("research", prerequisite))));
            var localLab = gs.StructureOfKind(gs.planet, StructureKind.ResearchLab);
            if (lab > 0 && (localLab == null || localLab.level < lab))
                body.Add(IconBtn(UiText.Get("project.lab"), "research_lab", UiKit.Cyan, () =>
                    {
                        if (localLab != null) OpenSourceStructure(localLab);
                        else
                        {
                            var definition = gs.db.StructureOfKind(StructureKind.ResearchLab);
                            OpenProject(Project("build", definition.id));
                        }
                    }));
            if (xp > gs.PlayerLevel)
                body.Add(IconBtn(UiText.Get("project.xp"), "alarm", UiKit.Cyan,
                    () => { CloseAll(); if (!OpenFirstSessionStructure(StructureKind.Alarm)) { shopTab = 0; OpenShop(); } }));
        }

        void OpenShortage(PlayerProject project, ResourceSO resource, int required)
        {
            if (busy || defense.Active) return;
            CloseShortage();
            shortageProject = project; shortageResource = resource; shortageRequired = required;
            shortageReturnFocus = root.panel.focusController.focusedElement as VisualElement;
            shortageOverlay = UiKit.Div("fr-backdrop fr-scrim");
            shortageOverlay.name = "shortage-overlay";
            shortageOverlay.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == shortageOverlay) { CloseShortage(); e.StopPropagation(); }
            });
            var panel = new FrontierModal();
            panel.AddToClassList("fr-shortage");
            UiKit.SetAccent(panel, UiKit.Cyan);
            var heading = UiKit.Div("fr-modal-head");
            heading.Add(Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color)));
            heading.Add(UiKit.Text(resource.LocalizedName, "fr-modal-title"));
            var track = ProjectTrackButton(project);
            track.name = "shortage-track";
            heading.Add(track);
            var close = Icons.IconButton("close", UiKit.Muted, CloseShortage);
            close.name = "shortage-close";
            close.tooltip = UiText.Get("common.close");
            heading.Add(close);
            panel.Add(heading);
            shortageScroll = UiKit.VerticalScroll("fr-modal-scroll");
            shortageScroll.contentContainer.AddToClassList("fr-project-body");
            panel.Add(shortageScroll);
            shortageOverlay.Add(panel);
            overlayLayer.Add(shortageOverlay);
            RefreshShortage();
            close.Focus();
        }

        void CloseShortage()
        {
            shortageOverlay?.RemoveFromHierarchy();
            shortageOverlay = null; shortageScroll = null;
            shortageProject = null; shortageResource = null;
            if (shortageReturnFocus != null && shortageReturnFocus.panel != null) shortageReturnFocus.Focus();
            shortageReturnFocus = null;
        }

        void ShortageAction(VisualElement row, string labelKey, string icon, Color accent, Action action)
        {
            var button = Icons.IconButton(icon, accent, () => { CloseShortage(); action(); });
            button.name = "shortage-" + labelKey.Substring("project.".Length);
            button.tooltip = UiText.Get(labelKey);
            row.Add(button);
        }

        void RefreshShortage()
        {
            if (shortageScroll == null) return;
            var body = shortageScroll.contentContainer;
            var offset = shortageScroll.scrollOffset;
            body.Clear();
            var project = shortageProject;
            var resource = shortageResource;
            int have = gs.ProjectResourceCount(project, resource);
            body.Add(UiKit.Text(UiText.Get("project.shortfall", shortageRequired, resource.LocalizedName,
                have, Math.Max(0, shortageRequired - have)), "fr-small"));
            foreach (var source in gs.ResourceOptions(resource, shortageRequired, project.kind == "obstacle"))
            {
                var section = UiKit.Div("fr-shortage-source");
                body.Add(section);
                if (source.kind == ResourceSourceKind.Usable)
                {
                    section.Add(Note(UiText.Get("project.stock-ready"), UiKit.Lime));
                    continue;
                }
                var actions = UiKit.Div("fr-shortage-actions");
                if (source.kind == ResourceSourceKind.LocalStock || source.kind == ResourceSourceKind.RemoteStock)
                {
                    section.Add(UiKit.Text(UiText.Get("project.stored", source.amount, resource.LocalizedName,
                        source.planet.Name), "fr-small"));
                    if (source.kind == ResourceSourceKind.RemoteStock)
                    {
                        var route = gs.PlanRoute(source.planet);
                        section.Add(Note(route.Has(RouteReasons.Hazard) ? UiText.Get("project.landing-blocked") :
                            RouteStatus(route), route.reachable ? UiKit.Cyan : UiKit.Amber));
                        if (route.reachable) section.Add(UiKit.Text(UiText.Get("project.travel-cost",
                            (long)route.totalFuel * GameState.GallonsPerEnergy), "fr-tiny"));
                    }
                    if (source.kind == ResourceSourceKind.LocalStock)
                    {
                        ShortageAction(actions, "project.withdraw", "warehouse", UiKit.Cyan,
                            () => OpenSourceStructure(source.structure));
                        var depot = gs.db.StructureOfKind(StructureKind.CargoDepot);
                        if (depot != null && project.kind != "obstacle")
                            ShortageAction(actions, "project.connect", "cargo_depot", UiKit.Cyan,
                                () => OpenProject(Project("build", depot.id)));
                    }
                    else if (source.relay != null)
                    {
                        section.Add(UiKit.Text(UiText.Get("project.transfer-cost", source.energy), "fr-tiny"));
                        ShortageAction(actions, "project.open-relay", "satellite", UiKit.Cyan,
                            () => OpenSourceStructure(source.relay));
                    }
                    ShortageAction(actions, "project.view-world", "map", UiKit.Cyan, () =>
                    {
                        CloseAll(); OpenMap(); SelectMapWorld(source.planet); CenterOn(source.planet);
                    });
                }
                else
                {
                    if (source.kind == ResourceSourceKind.Planet)
                    {
                        var route = gs.PlanRoute(source.planet);
                        section.Add(Note(route.Has(RouteReasons.Hazard) ? UiText.Get("project.landing-blocked") :
                            RouteStatus(route), route.reachable ? UiKit.Cyan : UiKit.Amber));
                        if (route.Has(RouteReasons.Hazard))
                            ShortageAction(actions, "project.hull", "shield", UiKit.Amber,
                                () => { CloseAll(); OpenShip(ShipPage.Systems); });
                    }
                    var definition = source.structure != null ? source.structure.def : source.build;
                    if (definition != null) section.Add(UiKit.Text(definition.LocalizedName, "fr-small"));
                    string label = source.kind == ResourceSourceKind.Structure ? "project.produce" :
                        source.kind == ResourceSourceKind.Build ? "project.build-producer" : "project.find-world";
                    string icon = source.kind == ResourceSourceKind.Build ? "build" :
                        definition != null ? StructureIcon(definition.kind) : "map";
                    if (source.kind != ResourceSourceKind.Unavailable)
                        ShortageAction(actions, label, icon, UiKit.Lime, () => OpenResourceSource(resource));
                    if (source.reason != null) section.Add(Note(source.reason, UiKit.Amber));
                }
                section.Add(actions);
            }
            var common = UiKit.Div("fr-shortage-actions");
            if (gs.FreeSpace(resource) <= 0)
            {
                body.Add(Note(UiText.Get("project.capacity"), UiKit.Amber));
                ShortageAction(common, "project.capacity", "pack", UiKit.Amber,
                    () => { CloseAll(); OpenShip(ShipPage.Cargo); });
            }
            ShortageAction(common, "project.manual", "tutorial", UiKit.Muted, () => OpenAtlasResource(resource));
            body.Add(common);
            var scroll = shortageScroll;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).StartingIn(1);
        }

        void OpenSourceStructure(PlacedStructure structure)
        {
            CloseAll();
            if (!world.SelectStructure(structure))
            {
                Debug.LogWarning("Frontier: project source anchor unavailable: " + structure.def.id);
                Toast(UiText.Get("project.anchor-missing"), UiKit.Amber);
                OpenGatherShop();
            }
        }

        void OpenProject(PlayerProject project)
        {
            if (busy || defense.Active) return;
            if (project == null) { Toast(UiText.Get("project.missing"), UiKit.Amber); return; }
            var targetWorld = gs.universe.Get(project.planetId);
            if (targetWorld == null || gs.ProjectState(project).name == null)
            {
                Toast(UiText.Get("project.missing"), UiKit.Amber);
                OpenProjectPanel();
                return;
            }
            CloseAll();
            if (targetWorld.Id != gs.planet.Id)
            {
                OpenMap(); SelectMapWorld(targetWorld); CenterOn(targetWorld);
                Toast(UiText.Get("project.return-world"), UiKit.Amber);
                return;
            }
            switch (project.kind)
            {
                case "research":
                    researchBranch = ""; OpenResearch();
                    ScrollProject(modalScroll, "research-" + project.id);
                    break;
                case "upgrade":
                    var upgrade = gs.db.FindUpgrade(project.id);
                    if (upgrade.category == UpgradeCategory.Defense || upgrade.category == UpgradeCategory.Logistics)
                        OpenResearch(ResearchPage.Improvements);
                    else OpenShip(upgrade.effect == UpgradeEffect.BackpackSlots ? ShipPage.Cargo :
                        upgrade.category == UpgradeCategory.Tools ? ShipPage.Tools : ShipPage.Systems);
                    ScrollProject(modalScroll, "upgrade-" + project.id);
                    break;
                case "tool": OpenShip(ShipPage.Tools); ScrollProject(modalScroll, "tool-" + project.id); break;
                case "build":
                    shopTab = gs.db.FindStructure(project.id).kind.IsGather() ? 1 : 0; OpenShop();
                    ScrollProject(modalScroll, "shop-structure-" + project.id);
                    break;
                case "network": OpenNetwork(); break;
                case "link":
                    OpenMap(); SelectMapWorld(gs.universe.Get(project.id)); CenterOn(gs.universe.Get(project.id));
                    break;
                case "obstacle":
                    if (!world.SimulateObstacleTap(project.slot))
                    {
                        Toast(UiText.Get("project.anchor-missing"), UiKit.Amber); OpenProjectPanel();
                    }
                    break;
                default:
                    var structure = gs.structures.FirstOrDefault(s => s.planetId == project.planetId && s.slot == project.slot);
                    if (structure == null || !world.SelectStructure(structure))
                    {
                        Toast(UiText.Get("project.anchor-missing"), UiKit.Amber); OpenProjectPanel();
                    }
                    else if (project.kind == "recipe") ScrollProject(popupScroll, "recipe-" + project.id);
                    break;
            }
        }

        static void ScrollProject(ScrollView scroll, string name)
        {
            if (scroll == null) return;
            scroll.schedule.Execute(() =>
            {
                var card = scroll.Q(name);
                if (card != null) scroll.ScrollTo(card);
            }).StartingIn(1);
        }

        void OpenProjectPanel()
        {
            if (busy || defense.Active) return;
            CloseAll();
            OpenModal("project", "star", UiText.Get("project.title"), UiKit.Cyan, body =>
            {
                body.AddToClassList("fr-project-body");
                BuildRewardRecovery(body);
                var project = gs.TrackedProject;
                var status = gs.ProjectState(project);
                if (project == null) body.Add(Note(UiText.Get("project.empty"), UiKit.Muted));
                else
                {
                    body.Add(UiKit.Text(status.name ?? UiText.Get("project.missing"), "fr-h2"));
                    body.Add(Note(UiText.Get(status.complete ? "project.complete" : "project.remaining"), UiKit.Cyan));
                    if (status.blocker != null) body.Add(Note(status.blocker, UiKit.Amber));
                    if (gs.planet.Id == project.planetId) body.Add(ProjectCosts(status.costs, project));
                    var open = IconBtn(UiText.Get("project.open"), "play", UiKit.Lime, () => OpenProject(project));
                    open.name = "project-open";
                    UiKit.SetEnabledLook(open, status.name != null);
                    body.Add(open);
                    body.Add(IconBtn(UiText.Get("project.clear"), "close", UiKit.Muted, () =>
                    {
                        if (!gs.TrackProject(null, out var error)) Toast(error, UiKit.Amber);
                    }));
                }
                if (!returnSummaryDismissed && gs.ReturnItems.Count > 0)
                {
                    body.Add(SectionLabel(UiText.Get("project.while-away")));
                    foreach (var item in gs.ReturnItems)
                    {
                        var entry = item;
                        var planet = gs.universe.Get(entry.planetId);
                        var resource = gs.db.FindResource(entry.resourceId);
                        body.Add(UiKit.Text(entry.resourceId == null ? UiText.Get("project.job-ready", planet.Name) :
                            UiText.Get("project.output-ready", entry.amount, resource.LocalizedName, planet.Name), "fr-small"));
                        body.Add(IconBtn(UiText.Get("project.open"), "map", UiKit.Cyan, () =>
                        {
                            CloseAll();
                            if (planet.Id == gs.planet.Id)
                            {
                                var structure = gs.structures.FirstOrDefault(s => s.planetId == entry.planetId && s.slot == entry.slot);
                                if (structure != null) { OpenSourceStructure(structure); return; }
                            }
                            OpenMap(); SelectMapWorld(planet);
                        }));
                    }
                    var dismiss = IconBtn(UiText.Get("project.dismiss-summary"), "close", UiKit.Muted, () =>
                    {
                        returnSummaryDismissed = true; RefreshProjectResume(); RefreshModal();
                    });
                    dismiss.name = "project-dismiss-summary";
                    body.Add(dismiss);
                }
            });
        }

        void BuildProjectResume()
        {
            projectResume = IconBtn(UiText.Get("project.resume"), "star", UiKit.Cyan, OpenProjectPanel);
            projectResume.name = "project-resume";
            projectResume.style.position = Position.Absolute;
            projectResume.style.bottom = DockInset;
            projectResume.style.left = UiKit.Space.S16;
            hud.Add(projectResume);
        }

        void RefreshProjectResume()
        {
            if (projectResume == null) return;
            if (observedReturnVersion != gs.ReturnSummaryVersion)
            {
                observedReturnVersion = gs.ReturnSummaryVersion;
                returnSummaryDismissed = false;
            }
            bool useful = gs.HasTrackedProject || !returnSummaryDismissed && gs.ReturnItems.Count > 0;
            var reward = gs.GameplayReward;
            useful |= reward != null && reward.state != "settled" && reward.state != "cancelled";
            projectResume.style.display = useful && !busy && !defense.Active && modal == null && popup == null &&
                !mapOpen && buildDef == null && shortageOverlay == null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
