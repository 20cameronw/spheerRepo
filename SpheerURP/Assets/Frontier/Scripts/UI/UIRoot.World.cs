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
        float popupOpenedAt;

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
            popupOpenedAt = Time.unscaledTime;
            build(popup);
            Sfx.Click();
        }

        void ClosePopup()
        {
            if (popup != null) popup.RemoveFromHierarchy();
            popup = null; popupAnchor = null; popupBuild = null; popupKey = null;
        }

        void RefreshPopup()
        {
            if (popup == null || popupBuild == null) return;
            popup.Clear();
            popupBuild(popup);
        }

        void UpdatePopupPosition()
        {
            if (popup == null) return;
            if (popupAnchor == null || !world.Facing(popupAnchor.position)) { ClosePopup(); return; }

            // A press outside the popup dismisses it; a new tap on a node then reopens one on release.
            if (Input.GetMouseButtonDown(0) && Time.unscaledTime - popupOpenedAt > 0.15f && !IsOverUi(Input.mousePosition))
            {
                ClosePopup();
                return;
            }

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
            CloseModal();
            var k = s.def.kind;
            if (k == StructureKind.Refinery) OpenRefinery(s, t);
            else if (k == StructureKind.Alarm) OpenAlarm(s, t);
            else if (k.IsGather()) OpenGatherer(s, t);
            else if (k.IsTurret()) OpenTurret(s, t);
            else OpenSimple(s, t, "UPLINK COMING SOON", "Satellites will unlock orbital abilities in a future update.");
        }

        void StructureHeader(VisualElement body, PlacedStructure s, Color c)
        {
            var head = UiKit.Div("fr-row");
            head.Add(Icons.Make(StructureIcon(s.def.kind), c, 48f));
            head.Add(UiKit.Text(s.def.displayName.ToUpper() + "  LV " + s.level, "fr-h2", c));
            body.Add(head);
        }

        void UpgradeButton(VisualElement body, PlacedStructure s, Color c)
        {
            if (s.level >= s.def.maxLevel) { body.Add(UiKit.Text("MAXED", "fr-small", UiKit.Lime)); return; }
            var cost = s.def.UpgradeCost(s.level);
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
                StructureHeader(body, s, c);
                body.Add(UiKit.Text(title, "fr-small", c));
                body.Add(UiKit.Text(text, "fr-tiny"));
            });
        }

        void OpenTurret(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("turret" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c);
                body.Add(UiKit.Text("Fires automatically during waves", "fr-tiny"));
                body.Add(UiKit.Text("DMG " + gs.TurretPower(s).ToString("0.#") + "   RANGE " + s.def.range.ToString("0"), "fr-small"));
                UpgradeButton(body, s, c);
            });
        }

        void OpenGatherer(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            var game = s.def.kind.Game();
            OpenPopup("gather" + s.slot, t, c, body =>
            {
                StructureHeader(body, s, c);
                body.Add(UiKit.Text(gs.ToolName(game) + "  LV " + gs.ToolLevel(game), "fr-tiny"));
                float bonus = gs.StructureBonus(s);
                foreach (var spot in gs.SpotsFor(gs.planet, game))
                {
                    var sp = spot;
                    Color rc = UiKit.Ui(sp.resource.color);
                    var row = UiKit.Div("fr-row");
                    row.style.marginTop = 6;
                    int est = Mathf.Max(1, Mathf.RoundToInt(sp.baseYield * gs.ToolMultiplier(game) * (1f + bonus)));
                    row.Add(ResourceIconChip(sp.resource, "~" + est));
                    row.Add(UiKit.Div("fr-spacer"));
                    row.Add(IconBtn("START", "play", rc, () => { ClosePopup(); StartGather(sp, t, bonus); }));
                    body.Add(row);
                }
                UpgradeButton(body, s, c);
            });
        }

        void OpenRefinery(PlacedStructure s, Transform t)
        {
            Color c = UiKit.Ui(s.def.color);
            OpenPopup("refinery", t, c, body =>
            {
                var head = UiKit.Div("fr-row");
                head.Add(Icons.Make("refinery", c, 48f));
                head.Add(UiKit.Text("REFINERY  LV " + s.level, "fr-h2", c));
                body.Add(head);
                float bonus = gs.StructureBonus(s);
                foreach (var rec in gs.db.recipes)
                {
                    var r = rec;
                    var card = Card(c);
                    card.style.marginTop = 8; card.style.marginBottom = 0;
                    var line = UiKit.Div("fr-row-wrap");
                    foreach (var i in r.inputs)
                        if (i.resource != null) line.Add(ResourceIconChip(i.resource, gs.Count(i.resource) + "/" + i.amount, gs.Count(i.resource) < i.amount));
                    line.Add(Icons.Make("play", c, 30f));
                    foreach (var o in r.outputs)
                        if (o.resource != null) line.Add(ResourceIconChip(o.resource, gs.OutputAmount(o, bonus).ToString()));
                    card.Add(line);
                    var btns = UiKit.Div("fr-row");
                    btns.style.justifyContent = Justify.FlexEnd;
                    bool can = gs.Has(r.inputs);
                    var one = UiKit.Btn("x1", c, () => Craft(r, bonus, 1));
                    one.AddToClassList("fr-btn-mini");
                    UiKit.SetEnabledLook(one, can);
                    var max = UiKit.Btn("MAX", c, () => Craft(r, bonus, 100));
                    max.AddToClassList("fr-btn-mini");
                    UiKit.SetEnabledLook(max, can);
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
