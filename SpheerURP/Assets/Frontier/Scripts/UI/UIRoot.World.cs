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

        StructureSO buildDef;
        VisualElement banner;
        int alarmFuel = 3;

        // ------------------------------------------------------------ popup core
        void OpenPopup(string key, Transform anchor, Color accent, Action<VisualElement> build)
        {
            ClosePopup();
            popupKey = key; popupAnchor = anchor; popupBuild = build; popupColor = accent;
            popup = UiKit.Div("fr-popup");
            popup.pickingMode = PickingMode.Position;
            UiKit.Border(popup, UiKit.Ui(accent, 0.9f), 3f);
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
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("fr-popup-scroll");
            popup.Add(scroll);
            popupBuild(scroll.contentContainer);
            popup.Add(PopupClose());
        }

        VisualElement PopupClose()
        {
            var x = Icons.IconButton("close", UiKit.Red, ClosePopup, 60f, true);
            x.AddToClassList("fr-popup-close");
            return x;
        }

        void ClosePopup()
        {
            if (popup != null) popup.RemoveFromHierarchy();
            popup = null; popupAnchor = null; popupBuild = null; popupKey = null;
            if (planetView != null) planetView.Frozen = false;
            if (world != null) world.Selected = null;
        }

        void RefreshPopup()
        {
            if (popup == null || popupBuild == null) return;
            popup.Clear();
            BuildPopupBody();
        }

        void UpdatePopupPosition()
        {
            if (popup == null) return;
            if (popupAnchor == null) { ClosePopup(); return; }

            float w = popup.resolvedStyle.width, h = popup.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w < 1f) return;
            Vector3 sp = cam.WorldToScreenPoint(popupAnchor.position + popupAnchor.up * 0.35f);
            if (sp.z <= 0f) { ClosePopup(); return; }
            Vector2 pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
            float rw = root.resolvedStyle.width, rh = root.resolvedStyle.height;
            float left = Mathf.Clamp(pp.x - w * 0.5f, 8f + inset.x, Mathf.Max(8f, rw - w - 8f - inset.z));
            float top = Mathf.Clamp(pp.y - h - 30f, 8f + inset.y, Mathf.Max(8f, rh - h - 8f - inset.w));
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
            if (k == StructureKind.Refinery || k == StructureKind.Fabricator) OpenRefinery(s, t);
            else if (k == StructureKind.Alarm) OpenAlarm(s, t);
            else if (k.IsGather()) OpenGatherer(s, t);
            else if (k.IsTurret()) OpenTurret(s, t);
            else OpenSimple(s, t, "UPLINK COMING SOON", "Satellites will unlock orbital abilities in a future update.");
        }

        void StructureHeader(VisualElement body, PlacedStructure s, Color c, string subtitle = null)
        {
            var head = UiKit.Div("fr-row");
            head.AddToClassList("fr-popup-head");
            head.Add(Icons.Make(StructureIcon(s.def.kind), c, 60f));
            var col = UiKit.Div();
            col.style.flexShrink = 1;
            col.style.marginLeft = 10;
            col.Add(UiKit.Text(s.def.displayName.ToUpper() + "  LV " + s.level, "fr-h2", c));
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
            if (s.level >= s.def.maxLevel) { body.Add(UiKit.Text("MAX LEVEL", "fr-small", UiKit.Lime)); return; }
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
            });
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
                    row.Add(Icons.Make(Icons.ForResource(sp.resource), rc, 48f));
                    var txt = UiKit.Div(); txt.style.marginLeft = 10; txt.style.flexShrink = 1;
                    txt.Add(UiKit.Text(sp.resource.displayName, "fr-small", rc));
                    txt.Add(UiKit.Text("about " + est + " per run", "fr-tiny"));
                    row.Add(txt);
                    row.Add(UiKit.Div("fr-spacer"));
                    var go = IconBtn(verb, "play", rc, () => { ClosePopup(); StartGather(sp, t, bonus); });
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
            row.Add(Icons.Make(Icons.ForResource(resource), UiKit.Ui(resource.color), 32f));
            row.Add(UiKit.Text(amount + " " + resource.displayName, "fr-small", lack ? UiKit.Red : Color.white));
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
                    if (can <= 0) card.style.opacity = 0.7f;

                    var line = UiKit.Div("fr-recipe-flow");
                    var inputs = UiKit.Div("fr-recipe-side");
                    inputs.Add(UiKit.Text("INPUT", "fr-tiny"));
                    foreach (var i in r.inputs)
                        if (i.resource != null) inputs.Add(RecipeResource(i.resource, i.amount, gs.Count(i.resource) < i.amount));
                    line.Add(inputs);
                    var arrow = UiKit.Text(">", "fr-h2", c);
                    arrow.AddToClassList("fr-recipe-arrow");
                    line.Add(arrow);
                    var outputs = UiKit.Div("fr-recipe-side");
                    outputs.Add(UiKit.Text("OUTPUT", "fr-tiny"));
                    foreach (var o in r.outputs)
                        if (o.resource != null) outputs.Add(RecipeResource(o.resource, gs.OutputAmount(o, bonus)));
                    line.Add(outputs);
                    card.Add(line);

                    var have = UiKit.Div("fr-row-wrap");
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
                    btns.style.marginTop = 6;
                    var one = UiKit.Btn("MAKE 1", c, () => Craft(r, bonus, 1));
                    UiKit.SetEnabledLook(one, can > 0);
                    var max = UiKit.Btn(can > 1 ? "MAKE ALL (" + can + ")" : "MAKE ALL", c, () => Craft(r, bonus, 100));
                    max.style.marginLeft = 12;
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
                var head = UiKit.Div("fr-row");
                head.Add(Icons.Make("alarm", c, 48f));
                head.Add(UiKit.Text("ALARM  LV " + s.level, "fr-h2", c));
                body.Add(head);

                var stepper = UiKit.Div("fr-stepper");
                var minus = Icons.IconButton("minus", UiKit.Amber, () => { alarmFuel = Mathf.Max(1, alarmFuel - 1); RefreshPopup(); }, 72f);
                var plus = Icons.IconButton("plus", UiKit.Amber, () => { alarmFuel = Mathf.Min(Mathf.Max(1, Mathf.Min(have, 40)), alarmFuel + 1); RefreshPopup(); }, 72f);
                var val = UiKit.Div("fr-row");
                val.style.minWidth = 130; val.style.justifyContent = Justify.Center;
                val.Add(Icons.Make("fuel", UiKit.Amber, 40f));
                val.Add(UiKit.Text(alarmFuel.ToString(), "fr-stepper-val", have >= alarmFuel ? Color.white : UiKit.Red));
                stepper.Add(minus); stepper.Add(val); stepper.Add(plus);
                body.Add(stepper);

                var info = defense.Preview(alarmFuel);
                body.Add(UiKit.Text("WAVE " + info.waveNumber + "  -  " + info.aliens.Count + " aliens  -  HP x" + info.healthMult.ToString("0.0"), "fr-tiny"));
                var counts = new Dictionary<EnemySO, int>();
                foreach (var a in info.aliens) { int n; counts.TryGetValue(a, out n); counts[a] = n + 1; }
                var cr = UiKit.Div("fr-row-wrap"); cr.style.marginTop = 4;
                foreach (var kv in counts)
                {
                    var chip = UiKit.Div("fr-itemchip");
                    UiKit.Border(chip, UiKit.Ui(kv.Key.color, 0.8f), 2f);
                    var dot = UiKit.Div("fr-dot"); dot.style.backgroundColor = UiKit.Ui(kv.Key.color);
                    dot.style.marginRight = 6;
                    chip.Add(dot);
                    chip.Add(UiKit.Text("x" + kv.Value, "fr-small"));
                    cr.Add(chip);
                }
                body.Add(cr);

                var rr = UiKit.Div("fr-row-wrap"); rr.style.marginTop = 4;
                foreach (var i in info.reward)
                    if (i.resource != null) rr.Add(ResourceIconChip(i.resource, i.amount.ToString()));
                var xp = UiKit.Div("fr-itemchip");
                UiKit.Border(xp, UiKit.Ui(UiKit.Magenta, 0.8f), 2f);
                xp.Add(Icons.Make("star", UiKit.Magenta));
                xp.Add(UiKit.Text(info.xp.ToString(), "fr-small"));
                rr.Add(xp);
                body.Add(rr);

                bool can = have >= alarmFuel;
                var call = IconBtn(can ? "CALL" : "NEED FUEL", "alarm", can ? UiKit.Magenta : UiKit.Red, () =>
                {
                    ClosePopup();
                    defense.StartWave(alarmFuel);
                }, true);
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
            UiKit.Border(box, c, 3f);
            UiKit.Halo(box, c, 26f, 0.3f);
            box.Add(Icons.Make(StructureIcon(def.kind), c, 44f));
            box.Add(UiKit.Text("TAP A GLOWING PAD", "fr-h2", c));
            var cancel = Icons.IconButton("close", UiKit.Red, ExitBuild, 72f);
            cancel.style.marginLeft = 16;
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
