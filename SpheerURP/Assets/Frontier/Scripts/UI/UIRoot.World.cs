using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        VisualElement popup;
        Transform popupAnchor;
        string popupKey;
        Action<VisualElement> popupBuild;
        Color popupColor;
        ScrollView popupScroll;
        bool popupCompact;
        PlacedStructure jobPopupStructure;
        Label jobStatusLabel;
        float nextJobStatusRefresh;
        int obstaclePopupPad = -1;
        Label obstacleStatusLabel, obstacleSkipButtonText;
        Button obstacleSkipButton;
        float nextObstacleStatusRefresh;

        StructureSO buildDef;
        VisualElement banner;
        int alarmFuel = 3;

        void OnGatherTapped(GatherSpot spot, Transform anchor)
        {
            if (busy || defense.Active) return;
            CloseModal();
            if (popup != null && popupAnchor == anchor) { ClosePopup(); return; }
            PlacedStructure gatherer = null;
            foreach (var structure in gs.structures)
            {
                if (structure.planetId != gs.planet.Id || !structure.def.kind.IsGather() || !gs.IsOperational(structure)) continue;
                foreach (var candidate in gs.SpotsFor(gs.planet, structure.def.kind))
                    if (candidate.resource == spot.resource && candidate.game == spot.game &&
                        (gatherer == null || gs.StructureBonus(structure) > gs.StructureBonus(gatherer))) gatherer = structure;
            }
            var owner = gatherer;
            OpenPopup("resource-" + spot.resource.id, anchor, UiKit.Ui(spot.resource.color), body =>
            {
                body.Add(UiKit.Text(spot.resource.LocalizedName, "fr-h2"));
                body.Add(UiKit.Text(spot.resource.LocalizedDescription, "fr-small"));
                if (owner != null)
                {
                    body.Add(UiKit.Text(UiText.Get("world.site.tool", gs.ToolName(spot.game)), "fr-small"));
                    body.Add(IconBtn(UiText.Get("world.site.gather"), Icons.ForTool(spot.game), UiKit.Lime, () =>
                    {
                        ClosePopup();
                        StartGather(spot, anchor, gs.StructureBonus(owner));
                    }));
                }
                else
                {
                    body.Add(UiKit.Text(UiText.Get("world.site.build"), "fr-small"));
                    body.Add(IconBtn(UiText.Get("nav.build"), "build", UiKit.Lime, OpenShop));
                }
            }, true);
        }

        // ------------------------------------------------------------ popup core
        void OpenPopup(string key, Transform anchor, Color accent, Action<VisualElement> build, bool compact = false)
        {
            ClosePopup();
            popupKey = key; popupAnchor = anchor; popupBuild = build; popupColor = accent;
            popupCompact = compact;
            popup = new FrontierPopup { Compact = compact };
            popup.pickingMode = PickingMode.Position;
            UiKit.SetAccent(popup, accent);
            popup.style.opacity = 0f;
            popupLayer.Add(popup);
            BuildPopupBody();
            // Selecting a structure holds the world still until it's tapped again (or closed).
            planetView.Frozen = true;
            world.Selected = anchor;
            Sfx.Click();
        }

        void BuildPopupBody()
        {
            popupScroll = UiKit.VerticalScroll("fr-popup-scroll");
            popup.Add(popupScroll);
            popupBuild(popupScroll.contentContainer);
            popup.Add(PopupClose());
        }

        VisualElement PopupClose()
        {
            var x = Icons.IconButton("close", UiKit.Muted, ClosePopup, 0f, true);
            x.AddToClassList("fr-popup-close");
            return x;
        }

        void ClosePopup()
        {
            if (popup != null) popup.RemoveFromHierarchy();
            popup = null; popupAnchor = null; popupBuild = null; popupKey = null;
            popupScroll = null;
            jobPopupStructure = null; jobStatusLabel = null;
            obstaclePopupPad = -1; obstacleStatusLabel = null; obstacleSkipButtonText = null; obstacleSkipButton = null;
            if (planetView != null) planetView.Frozen = false;
            if (world != null) world.Selected = null;
        }

        void RefreshBuilderStatus()
        {
            if (jobStatusLabel == null || jobPopupStructure == null || Time.unscaledTime < nextJobStatusRefresh) return;
            nextJobStatusRefresh = Time.unscaledTime + 1f;
            jobStatusLabel.text = gs.BuilderJobStatus(jobPopupStructure);
            if (string.IsNullOrEmpty(jobStatusLabel.text)) jobStatusLabel.text = UiText.Get("structure.ready");
        }

        void RefreshObstacleStatus()
        {
            if (obstaclePopupPad < 0 || obstacleStatusLabel == null || Time.unscaledTime < nextObstacleStatusRefresh) return;
            nextObstacleStatusRefresh = Time.unscaledTime + 1f;
            int remaining = gs.ObstacleClearSecondsRemaining(obstaclePopupPad);
            obstacleStatusLabel.text = UiText.Get(remaining > 0 ? "obstacle.clearing" : "obstacle.completing", FormatJobDuration(remaining));
            if (obstacleSkipButton == null) return;
            int cost = gs.ObstacleSkipCost(obstaclePopupPad);
            if (obstacleSkipButtonText != null) obstacleSkipButtonText.text = UiText.Get("obstacle.skip", cost);
            UiKit.SetEnabledLook(obstacleSkipButton, gs.ShardBalance >= cost);
        }

        static string FormatJobDuration(int seconds)
        {
            if (seconds >= 3600) return UiText.Get("duration.hours-minutes", seconds / 3600, seconds % 3600 / 60);
            if (seconds >= 60) return UiText.Get("duration.minutes-seconds", seconds / 60, seconds % 60);
            return UiText.Get("duration.seconds", seconds);
        }

        void RefreshPopup()
        {
            if (popup == null || popupBuild == null) return;
            var offset = popupScroll.scrollOffset;
            popupScroll.contentContainer.Clear();
            popupBuild(popupScroll.contentContainer);
            var scroll = popupScroll;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset).StartingIn(1);
        }

        void UpdatePopupPosition()
        {
            if (popup == null) return;
            if (popupAnchor == null) { ClosePopup(); return; }

            float w = popup.resolvedStyle.width, h = popup.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w < 1f) return;
            float rw = popupLayer.resolvedStyle.width, rh = popupLayer.resolvedStyle.height;
            float left = (rw - w) * 0.5f, top = (rh - h) * 0.5f;
            if (popupCompact && h <= 420f && h < rh * 0.65f)
            {
                Vector3 sp = cam.WorldToScreenPoint(popupAnchor.TransformPoint(Vector3.up * 0.35f));
                if (sp.z > 0f)
                {
                    Vector2 pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, ScreenMetrics.Height - sp.y));
                    pp = popupLayer.WorldToLocal(pp);
                    left = Mathf.Clamp(pp.x - w * 0.5f, 0f, Mathf.Max(0f, rw - w));
                    top = Mathf.Clamp(pp.y - h - 24f, 0f, Mathf.Max(0f, rh - h));
                }
            }
            popup.style.left = left;
            popup.style.top = top;
            popup.style.opacity = 1f;
        }

        // ------------------------------------------------------------ structures
        void OnStructureTapped(PlacedStructure s, Transform t)
        {
            if (busy || defense.Active) return;
            // Tapping the selected structure again releases it.
            if (popup != null && popupAnchor == t) { ClosePopup(); return; }
            CloseModal();
            if (gs.IsUnderConstruction(s) || gs.IsUpgradeInProgress(s))
            {
                Color jobColor = UiKit.Ui(s.def.color);
                OpenPopup("builder" + s.slot, t, jobColor, body =>
                {
                    StructureHeader(body, s, jobColor, UiText.Get("structure.builder"));
                    jobStatusLabel = UiKit.Text(gs.BuilderJobStatus(s), "fr-small", UiKit.Cyan);
                    body.Add(jobStatusLabel);
                    if (s.def.kind == StructureKind.Terraformer)
                    {
                        int stage = s.record.pendingUpgradeLevel > s.level ? s.record.pendingUpgradeLevel : s.level;
                        body.Add(SectionLabel(UiText.Get("structure.stage", stage, s.def.maxLevel, StructureEffect(s.def, stage))));
                        body.Add(UiKit.Text(UiText.Get("structure.terraformer.pending"), "fr-tiny"));
                    }
                    else if (s.def.kind == StructureKind.WarpGate || s.def.kind == StructureKind.DysonCollector)
                    {
                        int stage = s.record.pendingUpgradeLevel > s.level ? s.record.pendingUpgradeLevel : s.level;
                        body.Add(SectionLabel(UiText.Get("structure.stage", stage, s.def.maxLevel, StructureEffect(s.def, stage))));
                        body.Add(UiKit.Text(UiText.Get(s.def.kind == StructureKind.WarpGate
                            ? "structure.warp.pending" : "structure.dyson.pending"), "fr-tiny"));
                    }
                    body.Add(UiKit.Text(UiText.Get("structure.offline-time"), "fr-tiny"));
                });
                jobPopupStructure = s;
                return;
            }
            var k = s.def.kind;
            if (k.IsStorage()) OpenWarehouse(s, t);
            else if (k == StructureKind.Satellite) OpenRelay(s, t);
            else if (k == StructureKind.DysonCollector) OpenDysonCollector(s, t);
            else if (k == StructureKind.WarpGate) OpenWarpGate(s, t);
            else if (k == StructureKind.Terraformer) OpenTerraformer(s, t);
            else if (k.IsStation()) OpenRefinery(s, t);
            else if (k == StructureKind.ResearchLab) OpenResearchLab(s, t);
            else if (k == StructureKind.RadarTower) OpenRadarTower(s, t);
            else if (k == StructureKind.ColonyHub) OpenColonyHub(s, t);
            else if (k == StructureKind.Hangar) OpenHangar(s, t);
            else if (k == StructureKind.ShieldPylon) OpenShieldPylon(s, t);
            else if (k == StructureKind.RepairBay) OpenRepairBay(s, t);
            else if (k == StructureKind.Alarm) OpenAlarm(s, t);
            else if (k.IsGather()) OpenGatherer(s, t);
            else if (k.IsTurret()) OpenTurret(s, t);
            else OpenSimple(s, t, UiText.Get("structure.offline"), UiText.Get("structure.planned", s.def.LocalizedName));
        }

        void OnObstacleTapped(int pad, Transform t)
        {
            if (busy || defense.Active) return;
            if (buildDef != null) ExitBuild();
            var obstacle = gs.ObstacleAt(gs.planet, pad);
            if (!obstacle.HasValue) return;
            CloseModal();
            var kind = gs.ObstacleKind(obstacle.Value);
            Color accent = UiKit.Amber;
            OpenPopup("obstacle-" + gs.planet.Id + "-" + pad, t, accent, body =>
            {
                body.Add(UiKit.Text(kind != null ? ContentText.Get("obstacle", kind.id, "name", kind.displayName).ToUpperInvariant() : UiText.Get("obstacle.title"), "fr-h2"));
                if (kind != null && !string.IsNullOrEmpty(kind.description))
                    body.Add(UiKit.Text(ContentText.Get("obstacle", kind.id, "description", kind.description), "fr-tiny"));
                body.Add(SectionLabel(UiText.Get("obstacle.cost")));
                var cost = gs.ObstacleClearCost(gs.planet, pad);
                body.Add(cost.Count == 0 ? UiKit.Text(UiText.Get("obstacle.free"), "fr-small") : UiKit.CostRow(cost, gs, true, OpenAtlasResource));

                body.Add(SectionLabel(UiText.Get("obstacle.salvage")));
                ResourceSO salvage = gs.db.FindResource(obstacle.Value.reward.resourceId);
                var rewards = UiKit.Div("fr-row-wrap");
                if (salvage != null) rewards.Add(ResourceIconChip(salvage, obstacle.Value.reward.amount + " " + salvage.LocalizedName));
                rewards.Add(UiKit.Chip("star", UiKit.Magenta, UiText.Get("common.xp", obstacle.Value.xp)));
                if (obstacle.Value.podShards > 0)
                    rewards.Add(UiKit.Chip("star", UiKit.Magenta, UiText.Get("obstacle.shards", obstacle.Value.podShards)));
                body.Add(rewards);

                int remaining = gs.ObstacleClearSecondsRemaining(pad);
                if (remaining > 0)
                {
                    obstacleStatusLabel = UiKit.Text(UiText.Get("obstacle.clearing", FormatJobDuration(remaining)), "fr-small", UiKit.Cyan);
                    body.Add(obstacleStatusLabel);
                    body.Add(UiKit.Text(UiText.Get("obstacle.timer-help"), "fr-tiny"));
                    int skipCost = gs.ObstacleSkipCost(pad);
                    obstacleSkipButton = IconBtn(UiText.Get("obstacle.skip", skipCost), "bolt", UiKit.Magenta, () =>
                    {
                        if (gs.SkipObstacleClear(pad)) { Sfx.Good(); Toast(UiText.Get("obstacle.cleared"), UiKit.Lime); }
                        else { Sfx.Bad(); Toast(UiText.Get("obstacle.skip-blocked"), UiKit.Red); }
                    });
                    obstacleSkipButtonText = obstacleSkipButton.Q<Label>(className: "fr-btn-label");
                    UiKit.SetEnabledLook(obstacleSkipButton, gs.ShardBalance >= skipCost);
                    body.Add(obstacleSkipButton);
                }
                else
                {
                    string locked = gs.ObstacleClearLockReason(pad);
                    if (!string.IsNullOrEmpty(locked))
                        body.Add(Note(locked, UiKit.Amber));
                    var clear = IconBtn(UiText.Get(obstacle.Value.seconds <= 0 ? "obstacle.clear-now" : "obstacle.assign"),
                        "build", UiKit.Amber, () =>
                        {
                            if (gs.StartObstacleClear(pad))
                            {
                                if (gs.ObstacleClearSecondsRemaining(pad) == 0)
                                    Toast(UiText.Get("obstacle.cleared"), UiKit.Lime);
                                else Toast(UiText.Get("obstacle.assigned"), UiKit.Cyan);
                                Sfx.Good();
                            }
                            else
                            {
                                Sfx.Bad();
                                Toast(gs.ObstacleClearLockReason(pad) ?? UiText.Get("obstacle.blocked"), UiKit.Red);
                            }
                        }, true, gs.ObstacleClearLockReason(pad) == null);
                    UiKit.SetEnabledLook(clear, gs.ObstacleClearLockReason(pad) == null);
                    body.Add(clear);
                }
            });
            obstaclePopupPad = pad;
        }

        void StructureHeader(VisualElement body, PlacedStructure s, Color c, string subtitle = null)
        {
            var head = UiKit.Div("fr-card-head");
            head.AddToClassList("fr-popup-head");
            head.Add(Icons.Make(StructureIcon(s.def.kind), c, UiKit.Size.IconXl));
            var col = UiKit.Div("fr-card-text");
            col.Add(UiKit.Text(UiText.Get("structure.header", s.def.LocalizedName.ToUpper(), s.level), "fr-h2"));
            if (!string.IsNullOrEmpty(subtitle)) col.Add(UiKit.Text(subtitle, "fr-tiny"));
            head.Add(col);
            body.Add(head);
        }

        static VisualElement SectionLabel(string text)
        {
            var l = UiKit.Text(text, "fr-popup-section");
            return l;
        }

        void UpgradeButton(VisualElement body, PlacedStructure s, Color c)
        {
            if (gs.IsUpgradeInProgress(s))
            {
                body.Add(Note(gs.BuilderJobStatus(s), UiKit.Cyan));
                return;
            }
            if (gs.IsUnderConstruction(s))
            {
                body.Add(Note(gs.BuilderJobStatus(s), UiKit.Amber));
                return;
            }
            if (s.level >= s.def.maxLevel) { body.Add(Note(UiText.Get("structure.max"), UiKit.Lime)); return; }
            var cost = s.def.UpgradeCost(s.level);
            body.Add(SectionLabel(UiText.Get("structure.upgrade-preview", s.level + 1, StructureEffect(s.def, s.level + 1))));
            body.Add(UiKit.CostRow(cost, gs, true, OpenAtlasResource));
            var up = IconBtn(UiText.Get("command.upgrade"), "up", c, () =>
            {
                if (gs.UpgradeStructure(s))
                {
                    Sfx.Good();
                    Toast(gs.IsUpgradeInProgress(s) ? UiText.Get("command.upgrade-started", gs.BuilderJobStatus(s)) :
                        UiText.Get("command.upgraded", s.def.LocalizedName, s.level), c);
                }
                else Sfx.Bad();
            });
            if (gs.StructureUpgradeLockReason(s) != null) body.Add(Note(gs.StructureUpgradeLockReason(s), UiKit.Amber));
            UiKit.SetEnabledLook(up, gs.CanUpgradeStructure(s));
            body.Add(up);
        }

        void OpenSimple(PlacedStructure s, Transform t, string title, string text)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("simple" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, title);
                body.Add(UiKit.Text(text, "fr-tiny"));
            });
        }

        void OpenTerraformer(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("terraformer" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.terraformer.subtitle"));
                body.Add(UiKit.Text(s.def.LocalizedDescription, "fr-tiny"));
                body.Add(SectionLabel(UiText.Get("structure.terraformer.bonus")));
                body.Add(UiKit.Text(UiText.Get("structure.terraformer.yield", Mathf.RoundToInt(gs.TerraformerHarvestBonus * 100f)),
                    "fr-small", UiKit.Lime));
                body.Add(UiKit.Text(UiText.Get("structure.megaproject-stages", gs.stats.megaprojectStages), "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenWarpGate(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("warpgate" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.warp.subtitle"));
                int pct = Mathf.RoundToInt(gs.WarpGateEffect(gs.planet) * 100f);
                body.Add(UiKit.Text(UiText.Get("structure.warp.bonus", pct), "fr-small", UiKit.Cyan));
                var ship = gs.Ship;
                double baseRange = gs.Rules.Model.JumpRangeLy(ship.jumpLevel);
                body.Add(UiKit.Text(UiText.Get("structure.warp.range", baseRange, gs.Rules.JumpRangeLy(ship)), "fr-small"));
                body.Add(UiKit.Text(UiText.Get("structure.warp.discount", gs.TravelFuelDiscount(gs.planet) * 100f), "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenDysonCollector(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("dyson" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.dyson.subtitle"));
                double rate = Math.Max(0.0, s.def.EffectAt(s.level));
                double capHours = Math.Max(0.0, s.def.CapacityAt(s.level));
                double capacity = gs.DysonFuelBufferCapacity(s);
                double stored = gs.DysonFuelReserveEnergy(s);
                int ready = gs.DysonFuelReady(s);
                body.Add(UiKit.Text(UiText.Get("structure.dyson.production", rate, capHours), "fr-small", UiKit.Cyan));
                body.Add(UiKit.Text(UiText.Get("structure.dyson.reserve", stored, capacity), "fr-small"));
                body.Add(UiKit.Text(UiText.Get("structure.dyson.help"), "fr-tiny"));
                double fuelPerCell = gs.db.fuel == null ? 1.0 : Math.Max(0.0, gs.db.fuel.fuelEnergy);
                string action = UiText.Get(ready > 0 ? "structure.dyson.collect" :
                    stored >= fuelPerCell ? "structure.dyson.full" : "structure.dyson.empty", ready);
                var collect = IconBtn(action, "pack", UiKit.Cyan, () =>
                {
                    int amount = gs.ClaimDysonFuel(s);
                    if (amount > 0)
                    {
                        Sfx.Good();
                        Toast(UiText.Get("structure.dyson.collected", amount), UiKit.Cyan);
                        RefreshModal();
                    }
                    else Sfx.Bad();
                });
                UiKit.SetEnabledLook(collect, ready > 0);
                body.Add(collect);
                UpgradeButton(body, s, c);
            });
        }

        void OpenResearchLab(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("researchlab" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.lab.subtitle"));
                body.Add(UiKit.Text(UiText.Get("structure.lab.help"), "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenColonyHub(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("colonyhub" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.hub.subtitle"));
                int bonus = Mathf.Max(0, Mathf.RoundToInt(s.def.EffectAt(s.level)));
                body.Add(UiKit.Text(UiText.Get("structure.hub.capacity", bonus, gs.BuilderCount, gs.db.shardRules.data.maxBuilders), "fr-small"));
                body.Add(UiKit.Text(UiText.Get("structure.hub.help", Mathf.Clamp(s.def.CapacityAt(s.level), 0, 90)), "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenRadarTower(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("radartower" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.radar.subtitle"));
                float bonus = gs.RadarRangeBonus(gs.planet);
                body.Add(UiKit.Text(UiText.Get("structure.radar.coverage", gs.ScannerRangeLy + bonus, bonus), "fr-small"));
                body.Add(UiKit.Text(UiText.Get("structure.radar.help"), "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenHangar(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("hangar" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.hangar.subtitle"));
                body.Add(UiKit.Text(UiText.Get("structure.hangar.discount", gs.TravelFuelDiscount(gs.planet) * 100f), "fr-small"));
                body.Add(UiKit.Text(UiText.Get("structure.hangar.help"), "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenShieldPylon(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("shieldpylon" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.shield.subtitle"));
                body.Add(UiKit.Text(UiText.Get("structure.shield.maximum", gs.ReactorMax, Mathf.Max(0f, s.def.EffectAt(s.level)) * 100f), "fr-small"));
                body.Add(UiKit.Text(UiText.Get("structure.shield.help"), "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenRepairBay(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("repairbay" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("structure.repair.subtitle"));
                body.Add(UiKit.Text(UiText.Get("structure.repair.rate", gs.ReactorRepairPerSecond(gs.planet)), "fr-small"));
                body.Add(UiKit.Text(UiText.Get("structure.repair.help"), "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenTurret(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("turret" + s.slot, t, c, body =>
            {
                string behavior = UiText.Get(s.def.kind == StructureKind.MissileSilo
                    ? "structure.turret.missile"
                    : s.def.kind == StructureKind.TeslaCoil
                        ? "structure.turret.tesla" : "structure.turret.automatic");
                StructureHeader(body, s, c, behavior);
                body.Add(UiKit.Text(UiText.Get("structure.turret.stats", gs.TurretPower(s), s.def.range), "fr-small"));
                UpgradeButton(body, s, c);
            }, true);
        }

        void OpenGatherer(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            var game = s.def.kind.Game();
            OpenPopup("gather" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, UiText.Get("gather.tool", gs.ToolName(game), gs.ToolLevel(game), gs.ToolDamage(game)));
                float bonus = gs.StructureBonus(s);
                var spots = gs.SpotsFor(gs.planet, s.def.kind);
                body.Add(SectionLabel(UiText.Get(spots.Count > 0 ? "gather.title" : "gather.empty")));
                string verb = UiText.Get(s.def.kind == StructureKind.Biodome ? "gather.harvest" :
                    game == MinigameKind.Drill ? "gather.drill" : game == MinigameKind.Siphon ? "gather.siphon" :
                    game == MinigameKind.Excavate ? "gather.excavate" : game == MinigameKind.Salvage ? "gather.salvage" :
                    game == MinigameKind.Chop ? "gather.chop" : "gather.mine");
                bool guideMarked = false;
                foreach (var spot in spots)
                {
                    var sp = spot;
                    Color rc = UiKit.Ui(sp.resource.color);
                    var row = UiKit.Div("fr-row");
                    row.AddToClassList("fr-popup-line");
                    int est = Mathf.Max(1, Mathf.RoundToInt(sp.baseYield * gs.ToolMultiplier(game) *
                        (1f + bonus + gs.TerraformerHarvestBonus)));
                    row.Add(Icons.Make(Icons.ForResource(sp.resource), rc, UiKit.Size.IconLg));
                    var txt = UiKit.Div("fr-card-text");
                    txt.Add(UiKit.Text(sp.resource.LocalizedName, "fr-small"));
                    txt.Add(UiKit.Text(UiText.Get("gather.estimate", est), "fr-tiny"));
                    row.Add(txt);
                    var go = IconBtn(verb, "play", c, () => { ClosePopup(); StartGather(sp, t, bonus); });
                    go.style.marginTop = 0;
                    row.Add(go);
                    if (!guideMarked && s.def.kind == StructureKind.Lumber)
                    {
                        AddFirstSessionCue(body, 1);
                        guideMarked = true;
                    }
                    body.Add(row);
                }
                UpgradeButton(body, s, c);
            });
        }

        int MaxCrafts(RecipeSO r)
        {
            int n = 999;
            foreach (var i in r.inputs)
                if (i.resource != null && i.amount > 0) n = Mathf.Min(n, gs.PlanetCount(i.resource) / i.amount);
            return n == 999 ? 0 : n;
        }

        VisualElement RecipeResource(ResourceSO resource, int amount, bool lack = false)
        {
            var row = UiKit.Div("fr-recipe-resource");
            row.Add(Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color), UiKit.Size.IconSm));
            row.Add(UiKit.Text(amount + " " + resource.LocalizedName, "fr-small", lack ? UiKit.Red : (Color?)null));
            return row;
        }

        void OpenRefinery(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("station" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, s.def.LocalizedDescription);
                float bonus = gs.StructureBonus(s);
                int batchSize = gs.StationBatchSize(s);
                body.Add(UiKit.Text(UiText.Get(batchSize == 1 ? "craft.capacity.one" : "craft.capacity.many", batchSize), "fr-small"));
                body.Add(UiKit.Text(UiText.Get(gs.CargoDepotActive ? "craft.pooled-help" : "craft.pack-help"), "fr-tiny"));
                body.Add(SectionLabel(UiText.Get("craft.recipes")));
                bool guideMarked = false;
                foreach (var rec in gs.db.recipes)
                {
                    if (!gs.RecipeAt(rec, s)) continue;
                    var r = rec;
                    string locked = gs.RecipeLockReason(r, s);
                    int can = locked == null ? MaxCrafts(r) : 0;
                    var card = Card(c);
                    if (locked != null) card.Add(Note(locked, UiKit.Amber));
                    card.AddToClassList("fr-recipe");
                    if (can <= 0) card.AddToClassList("fr-card-dim");

                    var line = UiKit.Div("fr-recipe-flow");
                    var inputs = UiKit.Div("fr-recipe-side");
                    inputs.Add(UiKit.Text(UiText.Get("craft.inputs", batchSize), "fr-tiny"));
                    foreach (var i in r.inputs)
                        if (i.resource != null)
                        {
                            int required = (int)Math.Min(int.MaxValue, (long)i.amount * batchSize);
                            inputs.Add(RecipeResource(i.resource, required, gs.PlanetCount(i.resource) < required));
                        }
                    line.Add(inputs);
                    var arrow = UiKit.Text(">", "fr-h2");
                    arrow.AddToClassList("fr-recipe-arrow");
                    line.Add(arrow);
                    var outputs = UiKit.Div("fr-recipe-side");
                    outputs.Add(UiKit.Text(UiText.Get("craft.outputs", batchSize), "fr-tiny"));
                    foreach (var o in r.outputs)
                        if (o.resource != null)
                            outputs.Add(RecipeResource(o.resource, (int)Math.Min(int.MaxValue, (long)gs.OutputAmount(o, bonus) * batchSize)));
                    line.Add(outputs);
                    card.Add(line);

                    var have = UiKit.Div("fr-row-wrap");
                    have.style.marginTop = UiKit.Space.S8;
                    var haveText = new System.Text.StringBuilder();
                    bool first = true;
                    foreach (var i in r.inputs)
                    {
                        if (i.resource == null) continue;
                        if (!first) haveText.Append(",  ");
                        haveText.Append(gs.PlanetCount(i.resource)).Append(' ').Append(i.resource.LocalizedName);
                        first = false;
                    }
                    have.Add(UiKit.Text(UiText.Get("craft.have", haveText.ToString()), "fr-tiny"));
                    card.Add(have);

                    var btns = UiKit.Div("fr-row");
                    btns.style.justifyContent = Justify.FlexEnd;
                    btns.style.marginTop = UiKit.Space.S4;
                    int batchCount = Math.Min(batchSize, can);
                    var batch = UiKit.Btn(UiText.Get("craft.make-batch", batchCount), c, () => Craft(r, s, bonus, batchCount));
                    UiKit.SetEnabledLook(batch, can > 0);
                    btns.Add(batch);
                    if (can > batchCount)
                    {
                        var max = UiKit.Btn(UiText.Get("craft.make-all", can), c, () => Craft(r, s, bonus, can, true));
                        max.style.marginLeft = UiKit.Space.S12;
                        UiKit.SetEnabledLook(max, true);
                        btns.Add(max);
                    }
                    if (!guideMarked && can > 0)
                    {
                        AddFirstSessionCue(card, 3);
                        guideMarked = true;
                    }
                    card.Add(btns);
                    body.Add(card);
                }
            });
        }

        void Craft(RecipeSO r, PlacedStructure station, float bonus, int times, bool all = false)
        {
            int made = 0;
            batching = true;
            try
            {
                if (all)
                {
                    int remaining = times;
                    int batchSize = gs.StationBatchSize(station);
                    while (remaining > 0)
                    {
                        int batch = Math.Min(batchSize, remaining);
                        while (batch > 0 && !gs.CraftBatch(r, station, batch, bonus)) batch--;
                        if (batch == 0) break;
                        made += batch;
                        remaining -= batch;
                    }
                }
                else if (gs.CraftBatch(r, station, times, bonus))
                    made = times;
            }
            finally
            {
                batching = false;
            }
            if (made > 0) { Sfx.Good(); planetView.Punch(); }
            else { Sfx.Bad(); Toast(UiText.Get("craft.blocked"), UiKit.Red); }
            OnStateChanged();
        }

        void OpenAlarm(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("alarm", t, c, body =>
            {
                int have = gs.Count(gs.db.fuel);
                alarmFuel = Mathf.Clamp(alarmFuel, 1, Mathf.Max(1, Mathf.Min(have, 40)));
                StructureHeader(body, s, c, UiText.Get("wave.call-help"));

                var stepper = UiKit.Div("fr-stepper");
                var minus = Icons.IconButton("minus", UiKit.Amber, () => { alarmFuel = Mathf.Max(1, alarmFuel - 1); RefreshPopup(); });
                var plus = Icons.IconButton("plus", UiKit.Amber, () => { alarmFuel = Mathf.Min(Mathf.Max(1, Mathf.Min(have, 40)), alarmFuel + 1); RefreshPopup(); });
                UiKit.SetEnabledLook(minus, alarmFuel > 1);
                UiKit.SetEnabledLook(plus, alarmFuel < Mathf.Min(have, 40));
                var val = UiKit.Div("fr-row");
                val.style.minWidth = 150; val.style.justifyContent = Justify.Center;
                var fuelIcon = Icons.Make("fuel", UiKit.Amber, UiKit.Size.IconMd);
                fuelIcon.style.marginRight = UiKit.Space.S8;
                val.Add(fuelIcon);
                val.Add(UiKit.Text(alarmFuel.ToString(), "fr-stepper-val", have >= alarmFuel ? (Color?)null : UiKit.Red));
                stepper.Add(minus); stepper.Add(val); stepper.Add(plus);
                body.Add(stepper);

                var info = defense.Preview(alarmFuel);
                body.Add(SectionLabel(UiText.Get("wave.preview", info.waveNumber, info.aliens.Count)));
                body.Add(UiKit.Text(UiText.Get("wave.scaling", info.healthMult, info.damageMult, info.speedMult), "fr-tiny"));
                if (info.modifier != null)
                {
                    var modifier = UiKit.Text(UiText.Get("wave.modifier", gs.db.formula.ModifierName(info.modifier).ToUpperInvariant(),
                        gs.db.formula.ModifierDescription(info.modifier)), "fr-small", UiKit.Amber);
                    modifier.style.whiteSpace = WhiteSpace.Normal;
                    body.Add(modifier);
                }
                var counts = new Dictionary<EnemySO, int>();
                foreach (var a in info.aliens) { int n; counts.TryGetValue(a, out n); counts[a] = n + 1; }
                var cr = UiKit.Div("fr-row-wrap"); cr.style.marginTop = UiKit.Space.S4;
                foreach (var kv in counts)
                {
                    var chip = UiKit.Div("fr-itemchip");
                    var dot = UiKit.Div("fr-dot"); dot.style.backgroundColor = UiKit.Ui(kv.Key.color);
                    dot.style.marginRight = UiKit.Space.S8; dot.style.marginLeft = UiKit.Space.S4;
                    chip.Add(dot);
                    string rank = kv.Key.rank == AlienRank.Standard ? "" : UiText.Get("wave.rank." + kv.Key.rank.ToString().ToLowerInvariant());
                    var identity = UiKit.Div();
                    identity.Add(UiKit.Text(UiText.Get(kv.Key.rank == AlienRank.Standard ? "wave.alien" : "wave.ranked-alien",
                        kv.Key.LocalizedName, kv.Value, rank), "fr-small"));
                    if (kv.Key.armor > 0f)
                        identity.Add(UiKit.Text(UiText.Get("wave.armor", Mathf.RoundToInt(kv.Key.armor * 100f)), "fr-tiny", UiKit.Amber));
                    chip.Add(identity);
                    cr.Add(chip);
                }
                body.Add(cr);

                body.Add(SectionLabel(UiText.Get("wave.rewards")));
                var rr = UiKit.Div("fr-row-wrap"); rr.style.marginTop = UiKit.Space.S4;
                foreach (var i in info.reward)
                    if (i.resource != null) rr.Add(ResourceIconChip(i.resource, i.amount + " " + i.resource.LocalizedName));
                rr.Add(UiKit.Chip("star", UiKit.Magenta, UiText.Get("common.xp", info.xp)));
                body.Add(rr);

                bool can = have >= alarmFuel;
                var call = IconBtn(UiText.Get(can ? "wave.call" : "wave.need-fuel"), "alarm", can ? UiKit.Magenta : UiKit.Red, () =>
                {
                    ClosePopup();
                    if (!defense.StartWave(alarmFuel)) Toast(UiText.Get("wave.call-blocked"), UiKit.Red);
                }, true, can);
                call.style.marginTop = UiKit.Space.S16;
                UiKit.SetEnabledLook(call, can);
                if (can) AddFirstSessionCue(body, 4);
                body.Add(call);
            });
        }

        // ------------------------------------------------------------ build mode
        void EnterBuild(StructureSO def)
        {
            ExitBuild();
            buildDef = def;
            world.BuildMode = true;
            Color c = UiKit.Ui(def.color);
            banner = UiKit.Div("fr-banner"); banner.pickingMode = PickingMode.Ignore;
            var box = UiKit.Div("fr-banner-box");
            UiKit.SetAccent(box, c);
            UiKit.Halo(box, c, 26f, 0.14f);
            box.Add(Icons.Make(StructureIcon(def.kind), c, UiKit.Size.IconLg));
            box.Add(UiKit.Text(UiText.Get("build.pad-help"), "fr-h2", c));
            var cancel = Icons.IconButton("close", UiKit.Muted, ExitBuild, 0f, true);
            cancel.style.marginLeft = UiKit.Space.S16;
            box.Add(cancel);
            banner.Add(box);
            bannerLayer.Add(banner);
        }

        void ExitBuild()
        {
            buildDef = null;
            if (world != null) world.BuildMode = false;
            if (banner != null) banner.RemoveFromHierarchy();
            banner = null;
        }

        void OnSlotTapped(int slot, Transform t)
        {
            if (buildDef == null) return;
            var def = buildDef;
            if (!gs.Place(def, slot)) { }
            else
            {
                var placed = gs.StructureAt(gs.planet, slot);
                Sfx.Good();
                Fx.Burst(t.position, def.color, 50, 5f, 0.25f);
                planetView.Punch();
                Toast(UiText.Get(gs.IsUnderConstruction(placed) ? "build.started" : "build.completed",
                    def.LocalizedName), UiKit.Ui(def.color));
                ExitBuild();
                return;
            }
            Sfx.Bad();
            Toast(gs.AvailableBuilderCount <= 0 && gs.StructureJobSeconds(def, 1) > 0
                ? gs.BuilderAvailabilityReason() : UiText.Get("build.resources-missing"), UiKit.Red);
        }
    }
}
