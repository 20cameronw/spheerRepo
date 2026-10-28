#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.EditorTools
{
    public class FrontierLayoutChecks : EditorWindow
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject host;
        UIRoot ui;
        GameState state, previous;
        VisualElement canvas, popupLayer, modalLayer, overlayLayer;
        int step;
        double next;
        const float InsetTop = 30f;
        static readonly Vector2[] Sizes = { new Vector2(900, 1600), new Vector2(1600, 900) };

        public static void Run()
        {
            var window = CreateInstance<FrontierLayoutChecks>();
            window.titleContent = new GUIContent("Frontier layout checks");
            window.Show();
            window.StartChecks();
        }

        [MenuItem("Frontier/Validate Panel Layout")]
        public static void RunFromMenu() { Run(); }

        void Set(string name, object value) { typeof(UIRoot).GetField(name, Private).SetValue(ui, value); }
        object Call(string name, params object[] args) { return typeof(UIRoot).GetMethod(name, Private).Invoke(ui, args); }

        void StartChecks()
        {
            CheckSupport.IsolateSaves();
            previous = GameState.I;
            state = new GameState(GameDatabase.Load(), false);
            var pack = state.db.FindUpgrade("pack");
            if (pack != null) state.DebugSetUpgrade(pack, pack.maxLevel);
            foreach (var resource in state.db.resources) state.Add(resource, resource.stackSize, false);
            host = new GameObject("LayoutCheckHost");
            host.SetActive(false);
            ui = host.AddComponent<UIRoot>();
            Set("gs", state);
            Set("planetView", host.AddComponent<PlanetView>());
            Set("world", host.AddComponent<WorldSurface>());
            canvas = UiKit.Div("fr-root");
            canvas.style.flexGrow = 0;
            canvas.style.flexShrink = 0;
            canvas.style.position = Position.Relative;
            canvas.styleSheets.Clear();
            UiKit.ApplyStyles(canvas);
            rootVisualElement.Add(canvas);
            popupLayer = UiKit.Div("fr-fill");
            modalLayer = UiKit.Div("fr-fill");
            overlayLayer = UiKit.Div("fr-fill");
            canvas.Add(popupLayer); canvas.Add(modalLayer); canvas.Add(overlayLayer);
            Set("root", canvas); Set("popupLayer", popupLayer); Set("modalLayer", modalLayer);
            Set("overlayLayer", overlayLayer);
            EditorApplication.update += Tick;
            PrepareSize(0);
        }

        void PrepareSize(int index)
        {
            canvas.style.width = Sizes[index].x; canvas.style.height = Sizes[index].y;
            var insets = new Vector4(20, InsetTop, 20, 24);
            UIRoot.LayoutContentLayer(popupLayer, insets, false);
            UIRoot.LayoutContentLayer(modalLayer, insets, false);
            UIRoot.LayoutContentLayer(overlayLayer, insets, true);
            Call("OpenModal", "pack", "ship", UiText.Get("nav.backpack"), UiKit.Amber,
                (Action<VisualElement>)(body => Call("BuildShip", body)), true);
            next = EditorApplication.timeSinceStartup + 0.6;
        }

        void CheckScroll(ScrollView scroll)
        {
            Check(scroll.horizontalScrollerVisibility == ScrollerVisibility.Hidden, "Horizontal bar policy changed.");
            Check(scroll.verticalScrollerVisibility == ScrollerVisibility.AlwaysVisible, "Vertical gutter is unstable.");
            Check(scroll.horizontalScroller.resolvedStyle.display == DisplayStyle.None, "Horizontal bar rendered.");
            foreach (var grid in scroll.Query<VisualElement>(className: "fr-grid").ToList())
                foreach (var slot in grid.Children())
                    Check(slot.worldBound.xMax <= grid.worldBound.xMax + 1f, "Inventory slot overflows grid.");
        }

        void CheckBounds(VisualElement panel, VisualElement area)
        {
            Check(!float.IsNaN(panel.layout.height) && panel.layout.height > 1, "Panel did not lay out.");
            Check(panel.worldBound.yMin >= canvas.worldBound.yMin + InsetTop + UIRoot.HudTopInset - 1f, "Panel covers top bar.");
            Check(panel.worldBound.yMax <= area.worldBound.yMax + 1f, "Panel escapes content area.");
            Check(panel.worldBound.xMin >= area.worldBound.xMin - 1f && panel.worldBound.xMax <= area.worldBound.xMax + 1f,
                "Panel escapes safe width.");
        }

        void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                var panel = canvas.panel;
                if (panel == null) throw new InvalidOperationException("Editor panel is not attached.");
                ValidateLayout();
                // The first validated layout schedules the entrance; measure after its transform settles.
                if (canvas.Q(className: "fr-surface-enter-start") != null)
                {
                    next = EditorApplication.timeSinceStartup + 0.4;
                    return;
                }
                int phase = step % 5;
                if (phase <= 1)
                {
                    CheckBounds(canvas.Q(className: "fr-modal"), modalLayer);
                    CheckScroll(canvas.Q<ScrollView>(className: "fr-modal-scroll"));
                    if (phase == 0)
                    {
                        Call("MovePackSlot", 0, 1);
                        Set("packSel", 1);
                        Call("RefreshModal");
                    }
                    else
                    {
                        Call("CloseModal");
                        var placed = state.StructureAt(state.planet, 0) ?? state.DebugPlace(state.db.StructureOfKind(StructureKind.Refinery), 0);
                        Call("OpenRefinery", placed, host.transform);
                    }
                }
                else if (phase <= 3)
                {
                    Call("UpdatePopupPosition");
                    ValidateLayout();
                    var popup = canvas.Q(className: "fr-popup");
                    CheckBounds(popup, popupLayer);
                    Check(Vector2.Distance(popup.worldBound.center, popupLayer.worldBound.center) < 1f,
                        "Large structure popup is not centered.");
                    CheckScroll(canvas.Q<ScrollView>(className: "fr-popup-scroll"));
                    if (phase == 2)
                    {
                        RecipeSO recipe = null;
                        foreach (var candidate in state.db.recipes) if (!candidate.fabricator) { recipe = candidate; break; }
                        Check(recipe != null && state.Craft(recipe), "Craft fixture failed.");
                        Call("RefreshPopup");
                    }
                    else
                    {
                        Call("ClosePopup");
                        var overlay = UiKit.Div("fr-overlay");
                        overlay.Add(UiKit.Text("DRILL THE ORE", "fr-title"));
                        overlay.Add(UiKit.Text("Hold to drill. Release to cool the tool.", "fr-muted"));
                        overlay.Add(UiKit.Div("fr-arena"));
                        var controls = UiKit.Div("fr-row");
                        controls.Add(UiKit.Btn("HOLD", UiKit.Cyan, null, true));
                        overlay.Add(controls);
                        overlayLayer.Add(overlay);
                    }
                }
                else
                {
                    CheckBounds(canvas.Q(className: "fr-overlay"), overlayLayer);
                    var arena = canvas.Q(className: "fr-arena");
                    Check(arena.layout.height >= 120f, "Minigame arena collapsed.");
                    Check(arena.worldBound.yMax < canvas.Q(className: "fr-overlay").worldBound.yMax - 80f,
                        "Minigame arena pushed controls out of bounds.");
                    overlayLayer.Clear();
                    Debug.Log("Frontier: inventory rebuild, crafting popup and minigame bounds passed at " + Sizes[step / 5] + ".");
                    if (step == 9) { Finish(0); return; }
                    PrepareSize(1);
                }

                void ValidateLayout()
                {
                    canvas.panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Invoke(canvas.panel, null);
                }
                step++;
                next = EditorApplication.timeSinceStartup + 0.4;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        void Finish(int code)
        {
            EditorApplication.update -= Tick;
            DestroyImmediate(host);
            GameState.I = previous;
            Close();
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
#endif
