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
            header.Add(UiKit.Text(here ? UiText.Get("map.here") : UiText.Get("map.tier", planet.Tier), "fr-tiny", UiKit.Cyan));
            mapCard.Add(header);
            var subtitle = UiKit.Text(UiText.Get("map.world.subtitle", planet.Star.Name, planet.BiomeName, planet.SizeName), "fr-tiny", UiKit.Muted);
            subtitle.style.marginBottom = 8;
            mapCard.Add(subtitle);
            var pin = UiKit.Btn(UiText.Get(gs.IsMapPinned(planet.Id) ? "map.unpin" : "map.pin"),
                gs.IsMapPinned(planet.Id) ? UiKit.Cyan : UiKit.Muted,
                () =>
                {
                    gs.ToggleMapPin(planet);
                    mapWorld.MarkDirtyRepaint();
                });
            pin.name = "map-pin-toggle";
            pin.SetEnabled(!gs.SaveIsReadOnly);
            mapCard.Add(pin);

            if (!here)
            {
                var row = UiKit.Div("fr-row");
                row.Add(UiKit.Text(RouteStatus(mapPreview), "fr-small", mapPreview != null && !mapPreview.reachable ? UiKit.Amber : UiKit.Cyan));
                mapCard.Add(row);
                if (!string.IsNullOrEmpty(mapNextStopPlanetId))
                {
                    var nextStop = gs.universe.Get(mapNextStopPlanetId);
                    if (nextStop != null)
                    {
                        mapCard.Add(UiKit.Text(UiText.Get("map.next-stop"), "fr-tiny", UiKit.Amber));
                        var nextStopButton = UiKit.Btn(nextStop.Name, UiKit.Cyan, () =>
                        {
                            SelectMapWorld(nextStop);
                            CenterOn(nextStop);
                        });
                        nextStopButton.name = "map-next-stop";
                        nextStopButton.tooltip = UiText.Get("map.next-stop.tooltip", nextStop.Name, planet.Name);
                        mapCard.Add(nextStopButton);
                    }
                }
                var mode = new Toggle(UiText.Get("map.autopilot.help")) { value = mapAutopilot };
                mode.RegisterValueChangedCallback(e => { mapAutopilot = e.newValue; InvalidateMapPreview(); BuildMapCard(); });
                mapCard.Add(mode);
                bool can = mapPreview != null && mapPreview.reachable;
                string price = mapPreview == null ? UiText.Get("map.calculating")
                    : UiText.Get(mapAutopilot ? "map.price.autopilot" : "map.price.manual",
                        mapPreview.totalFuel, gs.FuelOnBoard.Energy, mapPreview.totalSeconds);
                mapCard.Add(UiKit.Text(price, "fr-tiny", UiKit.Muted));
                string action = mapPreview == null ? UiText.Get("map.action.calculating")
                    : can ? UiText.Get(mapAutopilot ? "map.action.autopilot" : "map.action.pilot", mapPreview.totalFuel)
                    : UiText.Get("map.action.unavailable");
                var fly = IconBtn(action, "warp", can ? UiKit.Magenta : UiKit.Muted, () => StartTravel(planet, mapAutopilot), false, can);
                UiKit.SetEnabledLook(fly, can);
                fly.tooltip = UiText.Get("map.travel.tooltip", planet.Name);
                mapCard.Add(fly);
            }
            foreach (var child in mapCard.Children()) child.style.flexShrink = 0;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.minHeight = 0; scroll.style.flexShrink = 1;
            mapCard.Add(scroll);
            scroll.Add(UiKit.Text(UiText.Get(gs.HasBase(planet) ? "map.world.summary-base" : "map.world.summary",
                planet.BuildingSlots, planet.Danger), "fr-small"));
            if (planet.Hazards == Hazards.None) scroll.Add(UiKit.Text(UiText.Get("map.hazards.none"), "fr-tiny", UiKit.Lime));
            else
            {
                scroll.Add(UiKit.Text(UiText.Get("map.hazards.required", PlanetData.HazardText(planet.Hazards), gs.Rules.RequiredPlatingLevel(planet.Tier)), "fr-tiny", UiKit.Amber));
                var gaps = gs.Rules.LandingGaps(gs.Ship, planet.Hazards, planet.Tier);
                if (gaps.Length == 0)
                    scroll.Add(UiKit.Text(UiText.Get("map.hazards.ready"), "fr-tiny", UiKit.Lime));
                else
                {
                    foreach (var gap in gaps)
                        scroll.Add(UiKit.Text(UiText.Get("map.hazards.gap", gap.hazard, gap.have, gap.need), "fr-tiny", UiKit.Amber));
                    if (!gs.Ship.enforceHazardPlating)
                        scroll.Add(UiKit.Text(UiText.Get("map.hazards.disabled"), "fr-tiny", UiKit.Muted));
                }
            }
            scroll.Add(SectionLabel(UiText.Get(surveyed ? "map.resources" : "map.unsurveyed")));
            if (!surveyed) scroll.Add(UiKit.Text(UiText.Get("map.survey.help", gs.ScannerRangeLy), "fr-small", UiKit.Muted));
            else
                foreach (var deposit in planet.Descriptor.Deposits)
                {
                    var resource = gs.db.FindResource(deposit.ResourceId);
                    if (resource == null) continue;
                    var row = UiKit.Div("fr-row-wrap");
                    row.Add(ResourceIconChip(resource, resource.displayName));
                    row.Add(UiKit.Text(UiText.Get("map.deposit", deposit.Method, deposit.Richness, deposit.BaseYield), "fr-tiny", UiKit.Muted));
                    scroll.Add(row);
                }
            scroll.Add(SectionLabel(UiText.Get("map.worlds-in", planet.Star.Name.ToUpper())));
            var worlds = UiKit.Div("fr-row-wrap");
            foreach (var world in gs.universe.PlanetsOf(planet.Star))
            {
                var captured = world;
                var button = UiKit.Btn(world.Name, world.Id == planet.Id ? UiKit.Cyan : UiKit.Muted, () => SelectMapWorld(captured));
                button.style.marginRight = 8; button.style.marginBottom = 8;
                worlds.Add(button);
            }
            scroll.Add(worlds);
            scroll.Add(SectionLabel(UiText.Get("map.pins")));
            if (mapPinPlanets.Count == 0)
                scroll.Add(UiKit.Text(UiText.Get("map.pins.empty"), "fr-tiny", UiKit.Muted));
            else
                foreach (var pinned in mapPinPlanets)
                {
                    var captured = pinned;
                    var button = UiKit.Btn(UiText.Get("map.pinned-world", captured.Name, captured.Star.Name),
                        captured.Id == planet.Id ? UiKit.Cyan : UiKit.Muted,
                        () =>
                        {
                            SelectMapWorld(captured);
                            CenterOn(captured);
                        });
                    button.name = "map-pin-" + captured.Id;
                    button.style.marginBottom = 8;
                    scroll.Add(button);
                }
            scroll.scrollOffset = new Vector2(0, oldScroll);
            mapCard.RegisterCallback<GeometryChangedEvent>(e => LayoutMapChrome());
            mapView.Add(mapCard);
            LayoutMapChrome();
        }
    }
}
