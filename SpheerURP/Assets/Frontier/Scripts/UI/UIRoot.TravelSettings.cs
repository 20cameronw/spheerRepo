using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>Settings and debug controls of the travel mini-game (Hyperspace Run); see <see cref="TravelPrefs"/>.</summary>
    public partial class UIRoot
    {
        /// <summary>Settings page: assist mode and the drag sensitivity of the travel game.</summary>
        void BuildTravelSettings(VisualElement body)
        {
            bool assist = TravelPrefs.AssistMode;
            var assistBtn = IconBtn(assist ? "ASSIST MODE: ON" : "ASSIST MODE: OFF", "shield", assist ? UiKit.Lime : UiKit.Cyan, () =>
            {
                TravelPrefs.AssistMode = !TravelPrefs.AssistMode;
                RefreshModal();
            });
            assistBtn.name = "assist-toggle";
            body.Add(assistBtn);
            var hint = UiKit.Text("Assist mode: easier travel, lower rewards.", "fr-tiny");
            hint.style.marginTop = UiKit.Space.S4;
            body.Add(hint);

            body.Add(SectionLabel("TRAVEL DRAG SENSITIVITY"));
            var stepper = UiKit.Div("fr-stepper");
            float s = TravelPrefs.DragSensitivity;
            var minus = Icons.IconButton("minus", UiKit.Cyan, () => { TravelPrefs.DragSensitivity = TravelPrefs.DragSensitivity - TravelPrefs.SensitivityStep; RefreshModal(); });
            var plus = Icons.IconButton("plus", UiKit.Cyan, () => { TravelPrefs.DragSensitivity = TravelPrefs.DragSensitivity + TravelPrefs.SensitivityStep; RefreshModal(); });
            UiKit.SetEnabledLook(minus, s > TravelPrefs.SensitivityMin + 0.01f);
            UiKit.SetEnabledLook(plus, s < TravelPrefs.SensitivityMax - 0.01f);
            var val = UiKit.Text(s.ToString("0.0") + "x", "fr-h2");
            val.style.minWidth = 140;
            val.style.unityTextAlign = TextAnchor.MiddleCenter;
            stepper.Add(minus); stepper.Add(val); stepper.Add(plus);
            body.Add(stepper);
        }

        /// <summary>Debug page: choose the travel game, the performance overlay and the actions registered by the travel game's assembly.</summary>
        void BuildTravelDebug(VisualElement body)
        {
            var card = Card(UiKit.Amber);
            card.Add(UiKit.Text("TRAVEL GAME", "fr-h2"));
            bool legacy = TravelPrefs.UseLegacyGame;
            card.Add(IconBtn(legacy ? "PLAYING: LEGACY TAP GAME" : "PLAYING: HYPERSPACE RUN", "warp", legacy ? UiKit.Red : UiKit.Lime, () =>
            {
                TravelPrefs.UseLegacyGame = !TravelPrefs.UseLegacyGame;
                RefreshModal();
            }));
            bool overlay = TravelPrefs.DebugOverlay;
            card.Add(IconBtn(overlay ? "PERFORMANCE OVERLAY: ON" : "PERFORMANCE OVERLAY: OFF", "tools", overlay ? UiKit.Lime : UiKit.Cyan, () =>
            {
                TravelPrefs.DebugOverlay = !TravelPrefs.DebugOverlay;
                RefreshModal();
            }));
            foreach (var action in TravelDebug.Actions)
            {
                var a = action;
                card.Add(IconBtn(a.label.ToUpper(), "tools", UiKit.Amber, () =>
                {
                    string line;
                    try { line = a.run(); }
                    catch (Exception e) { line = "failed: " + e.Message; Debug.LogException(e); }
                    Toast(line, UiKit.Amber);
                }));
            }
            body.Add(card);
        }
    }
}
