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

        // ------------------------------------------------------------ popup core
        void OpenPopup(string key, Transform anchor, Color accent, Action<VisualElement> build, bool compact = false)
        {
            ClosePopup();
            popupKey = key; popupAnchor = anchor; popupBuild = build; popupColor = accent;
            popupCompact = compact;
            popup = UiKit.Div("fr-popup");
            if (compact) popup.AddToClassList("fr-popup-compact");
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
            if (string.IsNullOrEmpty(jobStatusLabel.text)) jobStatusLabel.text = "READY";
        }

        void RefreshObstacleStatus()
        {
            if (obstaclePopupPad < 0 || obstacleStatusLabel == null || Time.unscaledTime < nextObstacleStatusRefresh) return;
            nextObstacleStatusRefresh = Time.unscaledTime + 1f;
            int remaining = gs.ObstacleClearSecondsRemaining(obstaclePopupPad);
            obstacleStatusLabel.text = remaining > 0 ? "CLEARING  ·  " + FormatJobDuration(remaining) : "CLEARANCE COMPLETING";
            if (obstacleSkipButton == null) return;
            int cost = gs.ObstacleSkipCost(obstaclePopupPad);
            if (obstacleSkipButtonText != null) obstacleSkipButtonText.text = "SKIP  ·  " + cost + " SHARDS";
            UiKit.SetEnabledLook(obstacleSkipButton, gs.ShardBalance >= cost);
        }

        static string FormatJobDuration(int seconds)
        {
            if (seconds >= 3600) return (seconds / 3600) + "h " + (seconds % 3600 / 60).ToString("00") + "m";
            if (seconds >= 60) return (seconds / 60) + "m " + (seconds % 60).ToString("00") + "s";
            return seconds + "s";
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
                    StructureHeader(body, s, jobColor, "Builder crew assigned");
                    jobStatusLabel = UiKit.Text(gs.BuilderJobStatus(s), "fr-small", UiKit.Cyan);
                    body.Add(jobStatusLabel);
                    body.Add(UiKit.Text("Jobs use local device time in this offline build.", "fr-tiny"));
                });
                jobPopupStructure = s;
                return;
            }
            var k = s.def.kind;
            if (k.IsStorage()) OpenWarehouse(s, t);
            else if (k == StructureKind.Satellite) OpenRelay(s, t);
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
            else OpenSimple(s, t, "STRUCTURE OFFLINE", s.def.displayName + " gameplay is planned for a future update.");
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
                body.Add(UiKit.Text(kind != null ? kind.displayName.ToUpperInvariant() : "SURFACE OBSTRUCTION", "fr-h2"));
                if (kind != null && !string.IsNullOrEmpty(kind.description))
                    body.Add(UiKit.Text(kind.description, "fr-tiny"));
                body.Add(SectionLabel("CLEARANCE COST"));
                var cost = gs.ObstacleClearCost(gs.planet, pad);
                body.Add(cost.Count == 0 ? UiKit.Text("No materials required", "fr-small") : UiKit.CostRow(cost, gs, true));

                body.Add(SectionLabel("RECOVERABLE SALVAGE"));
                ResourceSO salvage = gs.db.FindResource(obstacle.Value.reward.resourceId);
                var rewards = UiKit.Div("fr-row-wrap");
                if (salvage != null) rewards.Add(ResourceIconChip(salvage, obstacle.Value.reward.amount + " " + salvage.displayName));
                rewards.Add(UiKit.Chip("star", UiKit.Magenta, obstacle.Value.xp + " XP"));
                if (obstacle.Value.podShards > 0)
                    rewards.Add(UiKit.Chip("star", UiKit.Magenta, "+" + obstacle.Value.podShards + " shards"));
                body.Add(rewards);

                int remaining = gs.ObstacleClearSecondsRemaining(pad);
                if (remaining > 0)
                {
                    obstacleStatusLabel = UiKit.Text("CLEARING  ·  " + FormatJobDuration(remaining), "fr-small", UiKit.Cyan);
                    body.Add(obstacleStatusLabel);
                    body.Add(UiKit.Text("Clearance continues while you explore. Timers use local device time.", "fr-tiny"));
                    int skipCost = gs.ObstacleSkipCost(pad);
                    obstacleSkipButton = IconBtn("SKIP  ·  " + skipCost + " SHARDS", "bolt", UiKit.Magenta, () =>
                    {
                        if (gs.SkipObstacleClear(pad)) { Sfx.Good(); Toast("Obstruction cleared", UiKit.Lime); }
                        else { Sfx.Bad(); Toast("Not enough shards to finish clearance", UiKit.Red); }
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
                    var clear = IconBtn(obstacle.Value.seconds <= 0 ? "CLEAR NOW" : "ASSIGN CLEARANCE",
                        "build", UiKit.Amber, () =>
                        {
                            if (gs.StartObstacleClear(pad))
                            {
                                if (gs.ObstacleClearSecondsRemaining(pad) == 0)
                                    Toast("Obstruction cleared", UiKit.Lime);
                                else Toast("Clearance crew assigned", UiKit.Cyan);
                                Sfx.Good();
                            }
                            else
                            {
                                Sfx.Bad();
                                Toast(gs.ObstacleClearLockReason(pad) ?? "Clearance could not start", UiKit.Red);
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
            col.Add(UiKit.Text(s.def.displayName.ToUpper() + "  LV\u00A0" + s.level, "fr-h2"));
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
            if (s.level >= s.def.maxLevel) { body.Add(Note("MAX LEVEL", UiKit.Lime)); return; }
            var cost = s.def.UpgradeCost(s.level);
            body.Add(SectionLabel("UPGRADE TO LV " + (s.level + 1) + "  (" + StructureEffect(s.def, s.level + 1) + ")"));
            body.Add(UiKit.CostRow(cost, gs, true));
            var up = IconBtn("UPGRADE", "up", c, () =>
            {
                if (gs.UpgradeStructure(s))
                {
                    Sfx.Good();
                    Toast(gs.IsUpgradeInProgress(s) ? "Upgrade started · " + gs.BuilderJobStatus(s) :
                        s.def.displayName + " LV " + s.level, c);
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

        void OpenResearchLab(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("researchlab" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, "Research and tool upgrades");
                body.Add(UiKit.Text("Unlocks research and tool tiers in COMMAND. Higher lab levels open advanced research.", "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenColonyHub(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("colonyhub" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, "Builder crew capacity");
                int bonus = Mathf.Max(0, Mathf.RoundToInt(s.def.EffectAt(s.level)));
                body.Add(UiKit.Text("Hub bonus: +" + bonus + " crew  ·  Colony total: " + gs.BuilderCount + "/" +
                    gs.db.shardRules.data.maxBuilders, "fr-small"));
                body.Add(UiKit.Text("Hub upgrades add crews at levels 3 and 7. Obstacle-clearing discounts are not active yet.", "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenRadarTower(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("radartower" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, "Expands local survey coverage");
                float bonus = gs.RadarRangeBonus(gs.planet);
                body.Add(UiKit.Text("Coverage around this system: " + (gs.ScannerRangeLy + bonus).ToString("0") + " ly  (+" +
                    bonus.ToString("0") + " ly from tower)", "fr-small"));
                body.Add(UiKit.Text("Reveals additional systems and their resource deposits on the star map.", "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenHangar(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("hangar" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, "Reduces fuel for trips leaving this planet");
                body.Add(UiKit.Text("Departure discount: " + (gs.TravelFuelDiscount(gs.planet) * 100f).ToString("0.#") + "%", "fr-small"));
                body.Add(UiKit.Text("This local bonus applies to manual and autopilot routes. It does not reduce travel costs from other planets.", "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenShieldPylon(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("shieldpylon" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, "Reinforces this planet's reactor during called waves");
                body.Add(UiKit.Text("Reactor maximum: " + gs.ReactorMax.ToString("0.#") + "  ·  +" +
                    (Mathf.Max(0f, s.def.EffectAt(s.level)) * 100f).ToString("0.#") + "% shield field", "fr-small"));
                body.Add(UiKit.Text("Only the planet with this operational Pylon receives the extra reactor health.", "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenRepairBay(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("repairbay" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, "Repairs reactor damage during called waves");
                body.Add(UiKit.Text("Repair rate: " + gs.ReactorRepairPerSecond(gs.planet).ToString("0.##") + " health / second", "fr-small"));
                body.Add(UiKit.Text("Repairs stop at the reactor's maximum health and do not affect other planets.", "fr-tiny"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenTurret(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("turret" + s.slot, t, c, body =>
            {
                string behavior = s.def.kind == StructureKind.MissileSilo
                    ? "Splash missiles hit clustered raiders during waves"
                    : s.def.kind == StructureKind.TeslaCoil
                        ? "Lightning chains between nearby raiders during waves"
                        : "Fires automatically during waves";
                StructureHeader(body, s, c, behavior);
                body.Add(UiKit.Text("DAMAGE " + gs.TurretPower(s).ToString("0.#") + "     RANGE " + s.def.range.ToString("0"), "fr-small"));
                UpgradeButton(body, s, c);
            }, true);
        }

        void OpenGatherer(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            var game = s.def.kind.Game();
            OpenPopup("gather" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, "Tool: " + gs.ToolName(game) + "  (LV " + gs.ToolLevel(game) + ", damage x" + gs.ToolDamage(game).ToString("0.0") + ")");
                float bonus = gs.StructureBonus(s);
                var spots = gs.SpotsFor(gs.planet, s.def.kind);
                body.Add(SectionLabel(spots.Count > 0 ? "GATHER ON THIS PLANET" : "NOTHING TO GATHER HERE"));
                string verb = s.def.kind == StructureKind.Biodome ? "HARVEST" :
                    game == MinigameKind.Drill ? "DRILL" : game == MinigameKind.Siphon ? "SIPHON" :
                    game == MinigameKind.Excavate ? "EXCAVATE" : game == MinigameKind.Salvage ? "SALVAGE" :
                    game == MinigameKind.Chop ? "CHOP" : "MINE";
                foreach (var spot in spots)
                {
                    var sp = spot;
                    Color rc = UiKit.Ui(sp.resource.color);
                    var row = UiKit.Div("fr-row");
                    row.AddToClassList("fr-popup-line");
                    int est = Mathf.Max(1, Mathf.RoundToInt(sp.baseYield * gs.ToolMultiplier(game) * (1f + bonus)));
                    row.Add(Icons.Make(Icons.ForResource(sp.resource), rc, UiKit.Size.IconLg));
                    var txt = UiKit.Div("fr-card-text");
                    txt.Add(UiKit.Text(sp.resource.displayName, "fr-small"));
                    txt.Add(UiKit.Text("about " + est + " per run", "fr-tiny"));
                    row.Add(txt);
                    var go = IconBtn(verb, "play", c, () => { ClosePopup(); StartGather(sp, t, bonus); });
                    go.style.marginTop = 0;
                    row.Add(go);
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
            row.Add(UiKit.Text(amount + " " + resource.displayName, "fr-small", lack ? UiKit.Red : (Color?)null));
            return row;
        }

        void OpenRefinery(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("station" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, s.def.description);
                float bonus = gs.StructureBonus(s);
                int batchSize = gs.StationBatchSize(s);
                body.Add(UiKit.Text("BATCH CAPACITY  ·  " + batchSize + " CRAFT" + (batchSize == 1 ? "" : "S") + " PER TAP", "fr-small"));
                body.Add(UiKit.Text(gs.CargoDepotActive
                    ? "Recipe inputs draw from this planet's backpack and pooled Warehouses. Outputs go to the backpack."
                    : "Recipe inputs and outputs use the backpack.", "fr-tiny"));
                body.Add(SectionLabel("RECIPES  -  you give  >  you get"));
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
                    inputs.Add(UiKit.Text("INPUT · BATCH x" + batchSize, "fr-tiny"));
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
                    outputs.Add(UiKit.Text("OUTPUT · BATCH x" + batchSize, "fr-tiny"));
                    foreach (var o in r.outputs)
                        if (o.resource != null)
                            outputs.Add(RecipeResource(o.resource, (int)Math.Min(int.MaxValue, (long)gs.OutputAmount(o, bonus) * batchSize)));
                    line.Add(outputs);
                    card.Add(line);

                    var have = UiKit.Div("fr-row-wrap");
                    have.style.marginTop = UiKit.Space.S8;
                    var haveText = new System.Text.StringBuilder("You have: ");
                    bool first = true;
                    foreach (var i in r.inputs)
                    {
                        if (i.resource == null) continue;
                        if (!first) haveText.Append(",  ");
                        haveText.Append(gs.PlanetCount(i.resource)).Append(' ').Append(i.resource.displayName);
                        first = false;
                    }
                    have.Add(UiKit.Text(haveText.ToString(), "fr-tiny"));
                    card.Add(have);

                    var btns = UiKit.Div("fr-row");
                    btns.style.justifyContent = Justify.FlexEnd;
                    btns.style.marginTop = UiKit.Space.S4;
                    int batchCount = Math.Min(batchSize, can);
                    var batch = UiKit.Btn("MAKE BATCH x" + batchCount, c, () => Craft(r, s, bonus, batchCount));
                    UiKit.SetEnabledLook(batch, can > 0);
                    btns.Add(batch);
                    if (can > batchCount)
                    {
                        var max = UiKit.Btn("MAKE ALL (" + can + ")", c, () => Craft(r, s, bonus, can, true));
                        max.style.marginLeft = UiKit.Space.S12;
                        UiKit.SetEnabledLook(max, true);
                        btns.Add(max);
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
            else { Sfx.Bad(); Toast("Not enough room or materials", UiKit.Red); }
            OnStateChanged();
        }

        void OpenAlarm(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("alarm", t, c, body =>
            {
                int have = gs.Count(gs.db.fuel);
                alarmFuel = Mathf.Clamp(alarmFuel, 1, Mathf.Max(1, Mathf.Min(have, 40)));
                StructureHeader(body, s, c, "More fuel attracts more aliens, with higher health, damage and speed.");

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
                body.Add(SectionLabel("WAVE " + info.waveNumber + "  -  " + info.aliens.Count + " ALIENS"));
                body.Add(UiKit.Text("Health x" + info.healthMult.ToString("0.00") + "   Damage x" + info.damageMult.ToString("0.00") + "   Speed x" + info.speedMult.ToString("0.00"), "fr-tiny"));
                var counts = new Dictionary<EnemySO, int>();
                foreach (var a in info.aliens) { int n; counts.TryGetValue(a, out n); counts[a] = n + 1; }
                var cr = UiKit.Div("fr-row-wrap"); cr.style.marginTop = UiKit.Space.S4;
                foreach (var kv in counts)
                {
                    var chip = UiKit.Div("fr-itemchip");
                    var dot = UiKit.Div("fr-dot"); dot.style.backgroundColor = UiKit.Ui(kv.Key.color);
                    dot.style.marginRight = UiKit.Space.S8; dot.style.marginLeft = UiKit.Space.S4;
                    chip.Add(dot);
                    chip.Add(UiKit.Text(kv.Key.displayName + " x" + kv.Value, "fr-small"));
                    cr.Add(chip);
                }
                body.Add(cr);

                body.Add(SectionLabel("VICTORY REWARDS"));
                var rr = UiKit.Div("fr-row-wrap"); rr.style.marginTop = UiKit.Space.S4;
                foreach (var i in info.reward)
                    if (i.resource != null) rr.Add(ResourceIconChip(i.resource, i.amount + " " + i.resource.displayName));
                rr.Add(UiKit.Chip("star", UiKit.Magenta, info.xp + " XP"));
                body.Add(rr);

                bool can = have >= alarmFuel;
                var call = IconBtn(can ? "CALL" : "NEED FUEL", "alarm", can ? UiKit.Magenta : UiKit.Red, () =>
                {
                    ClosePopup();
                    if (!defense.StartWave(alarmFuel)) Toast("Cannot call this wave. Check your fuel or wait for the current wave.", UiKit.Red);
                }, true, can);
                call.style.marginTop = UiKit.Space.S16;
                UiKit.SetEnabledLook(call, can);
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
            box.Add(UiKit.Text("TAP A GLOWING PAD", "fr-h2", c));
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
                Toast(gs.IsUnderConstruction(placed) ? def.displayName + " construction started" :
                    def.displayName + " built!", UiKit.Ui(def.color));
                ExitBuild();
                return;
            }
            Sfx.Bad();
            Toast(gs.AvailableBuilderCount <= 0 && gs.StructureJobSeconds(def, 1) > 0
                ? gs.BuilderAvailabilityReason() : "Not enough resources", UiKit.Red);
        }
    }
}
