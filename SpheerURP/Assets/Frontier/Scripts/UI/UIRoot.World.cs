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
            if (planetView != null) planetView.Frozen = false;
            if (world != null) world.Selected = null;
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
            var k = s.def.kind;
            if (k == StructureKind.Warehouse) OpenWarehouse(s, t);
            else if (k == StructureKind.Refinery || k == StructureKind.Fabricator) OpenRefinery(s, t);
            else if (k == StructureKind.Alarm) OpenAlarm(s, t);
            else if (k.IsGather()) OpenGatherer(s, t);
            else if (k.IsTurret()) OpenTurret(s, t);
            else OpenSimple(s, t, "UPLINK COMING SOON", "Satellites will unlock orbital abilities in a future update.");
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
            if (s.level >= s.def.maxLevel) { body.Add(Note("MAX LEVEL", UiKit.Lime)); return; }
            var cost = s.def.UpgradeCost(s.level);
            body.Add(SectionLabel("UPGRADE TO LV " + (s.level + 1) + "  (" + StructureEffect(s.def, s.level + 1) + ")"));
            body.Add(UiKit.CostRow(cost, gs, true));
            var up = IconBtn("UPGRADE", "up", c, () =>
            {
                if (gs.UpgradeStructure(s)) { Sfx.Good(); Toast(s.def.displayName + " LV " + s.level, c); }
                else Sfx.Bad();
            });
            UiKit.SetEnabledLook(up, gs.Has(cost));
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

        void OpenTurret(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("turret" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c, "Fires automatically during waves");
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
                var spots = gs.SpotsFor(gs.planet, game);
                body.Add(SectionLabel(spots.Count > 0 ? "GATHER ON THIS PLANET" : "NOTHING TO GATHER HERE"));
                string verb = game == MinigameKind.Drill ? "DRILL" : game == MinigameKind.Chop ? "CHOP" : "MINE";
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
                if (i.resource != null && i.amount > 0) n = Mathf.Min(n, gs.Count(i.resource) / i.amount);
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
                bool fab = s.def.kind == StructureKind.Fabricator;
                StructureHeader(body, s, c, fab ? "Combine materials into advanced parts" : "Convert resources into fuel and other materials");
                float bonus = gs.StructureBonus(s);
                body.Add(SectionLabel("RECIPES  -  you give  >  you get"));
                foreach (var rec in gs.db.recipes)
                {
                    if (rec.fabricator != fab) continue;
                    var r = rec;
                    int can = MaxCrafts(r);
                    var card = Card(c);
                    card.AddToClassList("fr-recipe");
                    if (can <= 0) card.AddToClassList("fr-card-dim");

                    var line = UiKit.Div("fr-recipe-flow");
                    var inputs = UiKit.Div("fr-recipe-side");
                    inputs.Add(UiKit.Text("INPUT", "fr-tiny"));
                    foreach (var i in r.inputs)
                        if (i.resource != null) inputs.Add(RecipeResource(i.resource, i.amount, gs.Count(i.resource) < i.amount));
                    line.Add(inputs);
                    var arrow = UiKit.Text(">", "fr-h2");
                    arrow.AddToClassList("fr-recipe-arrow");
                    line.Add(arrow);
                    var outputs = UiKit.Div("fr-recipe-side");
                    outputs.Add(UiKit.Text("OUTPUT", "fr-tiny"));
                    foreach (var o in r.outputs)
                        if (o.resource != null) outputs.Add(RecipeResource(o.resource, gs.OutputAmount(o, bonus)));
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
                        haveText.Append(gs.Count(i.resource)).Append(' ').Append(i.resource.displayName);
                        first = false;
                    }
                    have.Add(UiKit.Text(haveText.ToString(), "fr-tiny"));
                    card.Add(have);

                    var btns = UiKit.Div("fr-row");
                    btns.style.justifyContent = Justify.FlexEnd;
                    btns.style.marginTop = UiKit.Space.S4;
                    var one = UiKit.Btn("MAKE 1", c, () => Craft(r, bonus, 1));
                    UiKit.SetEnabledLook(one, can > 0);
                    var max = UiKit.Btn(can > 1 ? "MAKE ALL (" + can + ")" : "MAKE ALL", c, () => Craft(r, bonus, 100));
                    max.style.marginLeft = UiKit.Space.S12;
                    UiKit.SetEnabledLook(max, can > 1);
                    btns.Add(one); btns.Add(max);
                    card.Add(btns);
                    body.Add(card);
                }
            });
        }

        void Craft(RecipeSO r, float bonus, int times)
        {
            int made = 0;
            batching = true;
            for (int i = 0; i < times; i++)
            {
                if (!gs.Craft(r, bonus)) break;
                made++;
            }
            batching = false;
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
                Sfx.Good();
                Fx.Burst(t.position, def.color, 50, 5f, 0.25f);
                planetView.Punch();
                Toast(def.displayName + " built!", UiKit.Ui(def.color));
                ExitBuild();
                return;
            }
            Sfx.Bad();
            Toast("Not enough resources", UiKit.Red);
        }
    }
}
