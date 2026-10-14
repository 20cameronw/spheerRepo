using Frontier.Universe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        string mapCardFor;

        void BuildMapCard()
        {
            if (mapView == null) return;
            float oldScroll = 0;
            if (mapCard != null)
            {
                var old = mapCard.Q<ScrollView>();
                if (old != null && selected != null && mapCardFor == selected.Id) oldScroll = old.scrollOffset.y;
                mapCard.RemoveFromHierarchy();
            }
            var planet = selected ?? gs.planet;
            mapCardFor = planet.Id;
            bool surveyed = Surveyed(planet.Star), here = planet.Id == gs.planet.Id;
            mapCard = UiKit.Div("fr-map-card");
            mapCard.style.maxHeight = Length.Percent(46);
            UiKit.SetAccent(mapCard, UiKit.Ui(planet.Rim));
            var header = UiKit.Div("fr-row");
            var title = UiKit.Text(planet.Name, "fr-h2", UiKit.Ui(planet.Rim));
            title.style.flexShrink = 1; title.style.flexGrow = 1;
            header.Add(title);
            header.Add(UiKit.Text(here ? "YOU ARE HERE" : "TIER " + planet.Tier, "fr-tiny", UiKit.Cyan));
            mapCard.Add(header);
            var subtitle = UiKit.Text(planet.Star.Name + " · " + planet.BiomeName + " · " + planet.SizeName, "fr-tiny", UiKit.Muted);
            subtitle.style.marginBottom = 8;
            mapCard.Add(subtitle);

            if (!here)
            {
                var row = UiKit.Div("fr-row");
                row.Add(UiKit.Text(RouteStatus(mapPreview), "fr-small", mapPreview != null && !mapPreview.reachable ? UiKit.Amber : UiKit.Cyan));
                mapCard.Add(row);
                var mode = new Toggle("Autopilot · fuel x1.6, no refund") { value = mapAutopilot };
                mode.RegisterValueChangedCallback(e => { mapAutopilot = e.newValue; InvalidateMapPreview(); BuildMapCard(); });
                mapCard.Add(mode);
                bool can = mapPreview != null && mapPreview.reachable;
                string price = mapPreview == null ? "Calculating…" : mapPreview.totalFuel + " / " + gs.FuelOnBoard.Energy + " energy aboard · " + mapPreview.totalSeconds + "s" + (mapAutopilot ? "" : " estimate");
                mapCard.Add(UiKit.Text(price, "fr-tiny", UiKit.Muted));
                string action = mapPreview == null ? "CALCULATING…" : can ? (mapAutopilot ? "AUTOPILOT" : "PILOT") + " · " + mapPreview.totalFuel + " ENERGY" : "ROUTE UNAVAILABLE";
                var fly = IconBtn(action, "warp", can ? UiKit.Magenta : UiKit.Muted, () => StartTravel(planet, mapAutopilot), false, can);
                UiKit.SetEnabledLook(fly, can);
                fly.tooltip = "Travel to " + planet.Name;
                mapCard.Add(fly);
            }
            foreach (var child in mapCard.Children()) child.style.flexShrink = 0;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.minHeight = 0; scroll.style.flexShrink = 1;
            mapCard.Add(scroll);
            scroll.Add(UiKit.Text(planet.BuildingSlots + " building slots · danger " + planet.Danger.ToString("0.0")
                + (gs.HasBase(planet) ? " · BASE" : ""), "fr-small"));
            if (planet.Hazards == Hazards.None) scroll.Add(UiKit.Text("No environmental hazards", "fr-tiny", UiKit.Lime));
            else
            {
                scroll.Add(UiKit.Text("Hazards: " + planet.Hazards + " · hull level " + gs.Rules.RequiredPlatingLevel(planet.Tier) + " per hazard", "fr-tiny", UiKit.Amber));
                var gaps = gs.Rules.LandingGaps(gs.Ship, planet.Hazards, planet.Tier);
                if (gaps.Length == 0)
                    scroll.Add(UiKit.Text("HULL READY · SAFE TO LAND", "fr-tiny", UiKit.Lime));
                else
                {
                    foreach (var gap in gaps)
                        scroll.Add(UiKit.Text(gap.hazard + " plating " + gap.have + " / " + gap.need, "fr-tiny", UiKit.Amber));
                    if (!gs.Ship.enforceHazardPlating)
                        scroll.Add(UiKit.Text("Landing protection is disabled in development settings", "fr-tiny", UiKit.Muted));
                }
            }
            scroll.Add(SectionLabel(surveyed ? "RESOURCES · HOW TO HARVEST" : "UNSURVEYED"));
            if (!surveyed) scroll.Add(UiKit.Text("Fly within " + gs.ScannerRangeLy.ToString("0") + " ly of this system to reveal its deposits.", "fr-small", UiKit.Muted));
            else
                foreach (var deposit in planet.Descriptor.Deposits)
                {
                    var resource = gs.db.FindResource(deposit.ResourceId);
                    if (resource == null) continue;
                    var row = UiKit.Div("fr-row-wrap");
                    row.Add(ResourceIconChip(resource, resource.displayName));
                    row.Add(UiKit.Text(deposit.Method + " · richness x" + deposit.Richness.ToString("0.00") + " · yield " + deposit.BaseYield, "fr-tiny", UiKit.Muted));
                    scroll.Add(row);
                }
            scroll.Add(SectionLabel("WORLDS IN " + planet.Star.Name.ToUpper()));
            var worlds = UiKit.Div("fr-row-wrap");
            foreach (var world in gs.universe.PlanetsOf(planet.Star))
            {
                var captured = world;
                var button = UiKit.Btn(world.Name, world.Id == planet.Id ? UiKit.Cyan : UiKit.Muted, () => SelectMapWorld(captured));
                button.style.marginRight = 8; button.style.marginBottom = 8;
                worlds.Add(button);
            }
            scroll.Add(worlds);
            scroll.scrollOffset = new Vector2(0, oldScroll);
            mapCard.RegisterCallback<GeometryChangedEvent>(e => LayoutMapChrome());
            mapView.Add(mapCard);
            LayoutMapChrome();
        }
    }
}
