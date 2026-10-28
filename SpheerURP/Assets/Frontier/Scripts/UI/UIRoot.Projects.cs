using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        readonly Stack<Action> shortageReturns = new Stack<Action>();
        Action shortageOrigin;
        PlayerProject shortageProject;
        string modalIcon, modalTitle;
        Color modalAccent;
        VisualElement projectResume;
        bool returnSummaryDismissed;
        string projectContext;
        int observedReturnVersion;

        void ResetProjectContextIfNeeded()
        {
            if (projectContext == gs.RewardRequestContext) return;
            projectContext = gs.RewardRequestContext;
            shortageOrigin = null; shortageProject = null; shortageReturns.Clear();
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
                OpenShortage(project, resource, costs.Where(c => c.resource == resource).Sum(c => c.amount)));
        }

        void AddProjectAction(VisualElement body, PlayerProject project)
        {
            BuildProjectGates(body, project);
            var track = IconBtn(UiText.Get("project.track"), "star", UiKit.Cyan, () =>
            {
                if (!gs.TrackProject(project, out var error)) Toast(error, UiKit.Amber);
                else Toast(UiText.Get("project.tracked", gs.ProjectState(project).name), UiKit.Cyan);
            });
            track.name = "track-" + project.kind + "-" + project.id;
            body.Add(track);
        }

        void FollowProject(PlayerProject origin, Action action)
        {
            if (shortageReturns.Count >= 12) { Toast(UiText.Get("project.depth"), UiKit.Amber); return; }
            var restore = CaptureProjectLocation(origin);
            if (shortageOrigin == null) { shortageOrigin = restore; shortageProject = origin; }
            shortageReturns.Push(restore);
            action();
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
                        () => FollowProject(project, () => OpenProject(Project("structure", station.def.id, station.level + 1, station.slot)))));
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
                    () => FollowProject(project, () => OpenProject(Project("research", prerequisite)))));
            var localLab = gs.StructureOfKind(gs.planet, StructureKind.ResearchLab);
            if (lab > 0 && (localLab == null || localLab.level < lab))
                body.Add(IconBtn(UiText.Get("project.lab"), "research_lab", UiKit.Cyan, () =>
                    FollowProject(project, () =>
                    {
                        if (localLab != null) OpenSourceStructure(localLab);
                        else
                        {
                            var definition = gs.db.StructureOfKind(StructureKind.ResearchLab);
                            OpenProject(Project("build", definition.id));
                        }
                    })));
            if (xp > gs.PlayerLevel)
                body.Add(IconBtn(UiText.Get("project.xp"), "alarm", UiKit.Cyan,
                    () => FollowProject(project, () => { CloseAll(); if (!OpenFirstSessionStructure(StructureKind.Alarm)) { shopTab = 0; OpenShop(); } })));
        }

        Action CaptureProjectLocation(PlayerProject project)
        {
            string planetId = gs.planet.Id;
            if (modal != null)
            {
                var build = modalBuild;
                string key = modalKey, icon = modalIcon, title = modalTitle;
                Color accent = modalAccent;
                var offset = modalScroll.scrollOffset;
                var savedResearch = researchPage; var savedShip = shipPage; int savedShop = shopTab;
                string savedBranch = researchBranch, savedMenu = menuPage;
                int savedAtlas = atlasTab, savedPack = packSel;
                return () =>
                {
                    if (gs.planet.Id != planetId) { OpenProject(project); return; }
                    researchPage = savedResearch; shipPage = savedShip; shopTab = savedShop;
                    researchBranch = savedBranch; menuPage = savedMenu; atlasTab = savedAtlas; packSel = savedPack;
                    CloseAll();
                    OpenModal(key, icon, title, accent, build);
                    var scroll = modalScroll;
                    scroll.schedule.Execute(() => scroll.scrollOffset = offset).StartingIn(1);
                };
            }
            if (popup != null)
            {
                var build = popupBuild; var anchor = popupAnchor; string key = popupKey;
                Color color = popupColor; bool compact = popupCompact;
                var offset = popupScroll.scrollOffset;
                return () =>
                {
                    if (gs.planet.Id != planetId || anchor == null || gs.ProjectState(project).name == null)
                    {
                        Toast(UiText.Get("project.anchor-missing"), UiKit.Amber);
                        OpenProject(project);
                        return;
                    }
                    CloseAll();
                    OpenPopup(key, anchor, color, build, compact);
                    var scroll = popupScroll;
                    scroll.schedule.Execute(() => scroll.scrollOffset = offset).StartingIn(1);
                };
            }
            return () => OpenProject(project);
        }

        void BuildJourneyLinks(VisualElement body)
        {
            if (shortageOrigin == null) return;
            var row = UiKit.Div("fr-row-wrap");
            if (shortageReturns.Count > 0)
            {
                var back = IconBtn(UiText.Get("project.previous"), "back", UiKit.Cyan, () =>
                {
                    var restore = shortageReturns.Pop();
                    restore();
                });
                back.name = "shortage-previous";
                row.Add(back);
            }
            var original = IconBtn(UiText.Get("project.return", gs.ProjectState(shortageProject).name ??
                UiText.Get("project.title")), "back", UiKit.Amber, ReturnToProject);
            original.name = "shortage-return";
            original.style.maxWidth = Length.Percent(100);
            original.Q<Label>(className: "fr-btn-label").style.whiteSpace = WhiteSpace.Normal;
            row.Add(original);
            body.Add(row);
        }

        void ReturnToProject()
        {
            var restore = shortageOrigin;
            shortageOrigin = null;
            shortageReturns.Clear();
            restore?.Invoke();
        }

        void OpenShortage(PlayerProject project, ResourceSO resource, int required)
        {
            if (busy || defense.Active) return;
            if (shortageReturns.Count >= 12)
            {
                Toast(UiText.Get("project.depth"), UiKit.Amber);
                return;
            }
            var restore = CaptureProjectLocation(project);
            if (shortageOrigin == null) { shortageOrigin = restore; shortageProject = project; }
            shortageReturns.Push(restore);
            CloseAll();
            OpenModal("shortage-" + resource.id, Icons.ForResource(resource), resource.LocalizedName, UiKit.Cyan, body =>
            {
                int have = gs.PlanetCount(resource);
                body.Add(UiKit.Text(UiText.Get("project.shortfall", required, resource.LocalizedName,
                    have, Math.Max(0, required - have)), "fr-small"));
                if (gs.FreeSpace(resource) <= 0)
                    body.Add(IconBtn(UiText.Get("project.capacity"), "pack", UiKit.Amber,
                        () => FollowProject(project, () => { CloseAll(); OpenShip(ShipPage.Cargo); })));
                foreach (var option in gs.ResourceOptions(resource, required))
                {
                    var source = option;
                    if (source.kind == ResourceSourceKind.Usable)
                    {
                        body.Add(Note(UiText.Get("project.stock-ready"), UiKit.Lime));
                        continue;
                    }
                    if (source.kind == ResourceSourceKind.LocalStock || source.kind == ResourceSourceKind.RemoteStock)
                    {
                        body.Add(UiKit.Text(UiText.Get("project.stored", source.amount, resource.LocalizedName,
                            source.planet.Name), "fr-small"));
                        if (source.kind == ResourceSourceKind.RemoteStock)
                        {
                            var route = gs.PlanRoute(source.planet);
                            body.Add(Note(RouteStatus(route), route.reachable ? UiKit.Cyan : UiKit.Amber));
                            if (route.reachable) body.Add(UiKit.Text(UiText.Get("project.travel-cost",
                                (long)route.totalFuel * GameState.GallonsPerEnergy), "fr-tiny"));
                        }
                        if (source.kind == ResourceSourceKind.LocalStock)
                        {
                            body.Add(IconBtn(UiText.Get("project.withdraw"), "warehouse", UiKit.Cyan,
                                () => OpenSourceStructure(source.structure)));
                            var depot = gs.db.StructureOfKind(StructureKind.CargoDepot);
                            if (depot != null)
                                body.Add(IconBtn(UiText.Get("project.connect"), "cargo_depot", UiKit.Cyan,
                                    () => FollowProject(project, () => OpenProject(Project("build", depot.id)))));
                        }
                        else if (source.relay != null)
                        {
                            body.Add(UiKit.Text(UiText.Get("project.transfer-cost", source.energy), "fr-tiny"));
                            body.Add(IconBtn(UiText.Get("project.open-relay"), "satellite", UiKit.Cyan,
                                () => OpenSourceStructure(source.relay)));
                        }
                        body.Add(IconBtn(UiText.Get("project.view-world"), "map", UiKit.Cyan, () =>
                        {
                            CloseAll(); OpenMap(); SelectMapWorld(source.planet); CenterOn(source.planet);
                        }));
                    }
                    else
                    {
                        if (source.kind == ResourceSourceKind.Planet)
                        {
                            body.Add(Note(RouteStatus(gs.PlanRoute(source.planet)), UiKit.Cyan));
                            if (gs.PlanRoute(source.planet).Has(RouteReasons.Hazard))
                                body.Add(IconBtn(UiText.Get("project.hull"), "shield", UiKit.Amber,
                                    () => FollowProject(project, () => { CloseAll(); OpenShip(ShipPage.Systems); })));
                        }
                        string label = source.kind == ResourceSourceKind.Structure ? "project.produce" :
                            source.kind == ResourceSourceKind.Build ? "project.build-producer" :
                            source.kind == ResourceSourceKind.Planet ? "project.find-world" : "project.manual";
                        body.Add(IconBtn(UiText.Get(label), source.kind == ResourceSourceKind.Planet ? "map" : "play",
                            UiKit.Lime, () => OpenResourceSource(resource)));
                        if (source.reason != null) body.Add(Note(source.reason, UiKit.Amber));
                    }
                }
                body.Add(IconBtn(UiText.Get("project.manual"), "map", UiKit.Muted, () => OpenAtlasResource(resource)));
                AddProjectAction(body, shortageProject);
            });
        }

        void OpenSourceStructure(PlacedStructure structure)
        {
            CloseAll();
            if (!world.SelectStructure(structure))
            {
                Debug.LogWarning("Frontier: project source anchor unavailable: " + structure.def.id);
                Toast(UiText.Get("project.anchor-missing"), UiKit.Amber);
                OpenProject(shortageProject);
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
                    body.Add(IconBtn(UiText.Get("common.close"), "close", UiKit.Muted, () =>
                    {
                        returnSummaryDismissed = true; RefreshProjectResume(); RefreshModal();
                    }));
                }
            });
        }

        void BuildProjectResume()
        {
            projectResume = IconBtn(UiText.Get("project.resume"), "star", UiKit.Cyan, () =>
            {
                if (shortageOrigin != null) ReturnToProject(); else OpenProjectPanel();
            });
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
            bool useful = shortageOrigin != null || gs.HasTrackedProject || !returnSummaryDismissed && gs.ReturnItems.Count > 0;
            var reward = gs.GameplayReward;
            useful |= reward != null && reward.state != "settled" && reward.state != "cancelled";
            projectResume.style.display = useful && !busy && !defense.Active && modal == null && popup == null &&
                !mapOpen && buildDef == null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
