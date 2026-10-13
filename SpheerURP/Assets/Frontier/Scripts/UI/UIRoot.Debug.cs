#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>Entry points for the screenshot harness and play-mode tests. Not compiled into release builds.</summary>
    public partial class UIRoot
    {
        public VisualElement DebugRoot { get { return root; } }
        public float DebugPanelScale { get { return panelSettings != null ? panelSettings.scale : 1f; } }
        public bool DebugBusy { get { return busy; } }

        public void DebugRelayout() { ApplyLayout(true); }

        /// <summary>Closes everything and restores default tab/page state so each capture starts from a known screen.</summary>
        public void DebugReset()
        {
            CloseAll();
            if (overlayLayer != null) overlayLayer.Clear();
            if (toastLayer != null) toastLayer.Clear();
            busy = false;
            menuPage = ""; shopTab = 0; researchTab = 0; toolTab = 0; packSel = -1; confirmReset = false;
            selected = gs.planet;
        }

        public void DebugShop(int tab) { DebugReset(); shopTab = tab; OpenShop(); }
        public void DebugResearch(int tab, int tool = 0) { DebugReset(); researchTab = tab; toolTab = tool; OpenResearch(); }
        public void DebugPack(int select) { DebugReset(); packSel = select; OpenPack(); }

        public void DebugMenu(string page)
        {
            DebugReset();
            OpenMenu();
            menuPage = page ?? "";
            RefreshModal();
        }

        public void DebugMap(PlanetSO pick, float zoom)
        {
            DebugReset();
            OpenMap();
            if (pick != null) selected = pick;
            mapZoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            if (mapView == null) return;
            BuildMapNodes();
            BuildMapCard();
            CenterOn(pick != null ? pick : gs.planet);
            mapWorld.MarkDirtyRepaint();
        }

        public void DebugBuild(StructureSO def) { DebugReset(); EnterBuild(def); }

        public void DebugToast(string text, Color color) { Toast(text, color); }

        public bool DebugGather(MinigameKind kind)
        {
            DebugReset();
            var spots = gs.SpotsFor(gs.planet, kind);
            if (spots.Count == 0) return false;
            StartGather(spots[0], null, 0f);
            return true;
        }

        public void DebugTravelGame()
        {
            DebugReset();
            busy = true;
            new TravelGame().Run(overlayLayer, UiKit.Magenta, 1f, 0.5f, r => { busy = false; });
        }

        public void DebugTapStructure(PlacedStructure s)
        {
            DebugReset();
            world.SimulateTap(s);
        }
    }
}
#endif
