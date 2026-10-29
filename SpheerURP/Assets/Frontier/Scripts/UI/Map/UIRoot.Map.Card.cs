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
            mapCard.style.maxHeight = Length.Percent(40);
            UiKit.SetAccent(mapCard, UiKit.Ui(planet.Rim));
            var header = UiKit.Div("fr-row");
            header.style.alignItems = Align.FlexStart;
            var identity = UiKit.Div("fr-card-text");
            var title = UiKit.Text(planet.Name, "fr-h2", UiKit.Ui(planet.Rim));
            title.style.whiteSpace = WhiteSpace.Normal;
            identity.Add(title);
            var subtitle = UiKit.Text(UiText.Get("map.world.subtitle", planet.Star.Name, planet.BiomeName, planet.SizeName), "fr-tiny", UiKit.Muted);
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            identity.Add(subtitle);
            header.Add(identity);
            var actions = UiKit.Div("fr-map-card-actions");
            actions.Add(UiKit.Text(here ? UiText.Get("map.here") : UiText.Get("map.tier", planet.Tier), "fr-tiny", UiKit.Cyan));
            var pin = new Toggle(UiText.Get("map.pin")) { value = gs.IsMapPinned(planet.Id) };
            pin.RegisterValueChangedCallback(e =>
            {
                if (e.newValue == gs.IsMapPinned(planet.Id)) return;
                gs.ToggleMapPin(planet);
                mapWorld.MarkDirtyRepaint();
            });
            pin.name = "map-pin-toggle";
            pin.SetEnabled(!gs.SaveIsReadOnly);
            actions.Add(pin);
            header.Add(actions);
            mapCard.Add(header);
            header.style.flexShrink = 0;
            var scroll = UiKit.VerticalScroll("fr-map-details");
            scroll.style.minHeight = 0; scroll.style.flexShrink = 1;
            mapCard.Add(scroll);
            Button travelAction = null;

            if (!here)
            {
                var row = UiKit.Div("fr-row");
                var status = UiKit.Text(RouteStatus(mapPreview), "fr-small", mapPreview != null && !mapPreview.reachable ? UiKit.Amber : UiKit.Cyan);
                status.style.whiteSpace = WhiteSpace.Normal;
                status.style.flexShrink = 1;
                row.Add(status);
                scroll.Add(row);
                if (!string.IsNullOrEmpty(mapNextStopPlanetId))
                {
                    var nextStop = gs.universe.Get(mapNextStopPlanetId);
                    if (nextStop != null)
                    {
                        scroll.Add(UiKit.Text(UiText.Get("map.next-stop"), "fr-tiny", UiKit.Amber));
                        var nextStopButton = UiKit.Btn(nextStop.Name, UiKit.Cyan, () =>
                        {
                            SelectMapWorld(nextStop);
                            CenterOn(nextStop);
                        });
                        nextStopButton.name = "map-next-stop";
                        nextStopButton.tooltip = UiText.Get("map.next-stop.tooltip", nextStop.Name, planet.Name);
                        scroll.Add(nextStopButton);
                    }
                }
                bool can = mapPreview != null && mapPreview.reachable;
                if (mapPreview == null || !mapPreview.instant)
                {
                    var mode = new Toggle(UiText.Get("map.autopilot.help")) { value = mapAutopilot };
                    mode.RegisterValueChangedCallback(e => { mapAutopilot = e.newValue; InvalidateMapPreview(); BuildMapCard(); });
                    scroll.Add(mode);
                    string price = mapPreview == null ? UiText.Get("map.calculating")
                        : UiText.Get(mapAutopilot ? "map.price.autopilot" : "map.price.manual",
                            (long)mapPreview.totalFuel * GameState.GallonsPerEnergy, gs.FuelGallons, mapPreview.totalSeconds);
                    var priceLabel = UiKit.Text(price, "fr-tiny", UiKit.Muted);
                    priceLabel.style.whiteSpace = WhiteSpace.Normal;
                    scroll.Add(priceLabel);
                }
                string action = mapPreview == null ? UiText.Get("map.action.calculating")
                    : can && mapPreview.instant ? UiText.Get(gs.HasPlanetLink(gs.planet, planet) ? "network.travel" : "network.lightspeed")
                    : can ? UiText.Get(mapAutopilot ? "map.action.autopilot" : "map.action.pilot", (long)mapPreview.totalFuel * GameState.GallonsPerEnergy)
                    : UiText.Get("map.action.unavailable");
                var fly = IconBtn(action, "warp", can ? UiKit.Magenta : UiKit.Muted, () => StartTravel(planet, mapAutopilot), false, can);
                UiKit.SetEnabledLook(fly, can);
                fly.tooltip = UiText.Get("map.travel.tooltip", planet.Name);
                fly.name = "map-travel-action";
                fly.style.flexShrink = 0;
                fly.style.marginTop = UiKit.Space.S12;
                travelAction = fly;
            }
            if (planet.Id != gs.planet.Id && gs.NetworkRules != null &&
                gs.StructureOfKind(gs.planet, StructureKind.WarpGate) != null)
            {
                if (gs.HasPlanetLink(gs.planet, planet))
                    scroll.Add(UiKit.Text(UiText.Get("network.link.active"), "fr-small", UiKit.Lime));
                else
                {
                    string reason = gs.PlanetLinkLockReason(planet);
                    var project = Project("link", planet.Id);
                    scroll.Add(ProjectCosts(gs.NetworkCosts(false), project));
                    AddProjectAction(scroll, project);
                    var link = IconBtn(UiText.Get("network.link", planet.Name), "warp", UiKit.Cyan, () =>
                    {
                        bool ok = gs.TryLinkPlanet(planet, out string message);
                        Toast(message, ok ? UiKit.Lime : UiKit.Amber);
                    });
                    link.name = "map-link-world";
                    UiKit.SetEnabledLook(link, reason == null);
                    scroll.Add(link);
                    if (reason != null) scroll.Add(Note(reason, UiKit.Amber));
                }
            }
            scroll.Add(UiKit.Text(UiText.Get(gs.HasBase(planet) ? "map.world.summary-base" : "map.world.summary",
                planet.BuildingSlots, planet.Danger), "fr-small"));
            if (planet.Hazards != Hazards.None)
            {
                scroll.Add(UiKit.Text(UiText.Get("map.hazards.required", PlanetData.HazardText(planet.Hazards), gs.Rules.RequiredPlatingLevel(planet.Tier)), "fr-tiny", UiKit.Amber));
                var gaps = gs.Rules.LandingGaps(gs.Ship, planet.Hazards, planet.Tier);
                if (gaps.Length > 0)
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
                    row.Add(ResourceIconChip(resource, resource.LocalizedName));
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
            if (mapPinPlanets.Count > 0)
            {
                scroll.Add(SectionLabel(UiText.Get("map.pins")));
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
            }
            var gate = gs.StructureOfKind(gs.planet, StructureKind.WarpGate);
            bool networkReady = gs.NetworkRules != null && gs.IsOperational(gate);
            var network = IconBtn(UiText.Get("network.title"), "warp", networkReady ? UiKit.Cyan : UiKit.Muted, OpenNetwork);
            network.name = "map-open-network";
            network.tooltip = UiText.Get(networkReady ? "network.title" : "network.map.lock");
            UiKit.SetEnabledLook(network, networkReady);
            scroll.Add(network);
            if (travelAction != null) mapCard.Add(travelAction);
            scroll.scrollOffset = new Vector2(0, oldScroll);
            mapCard.RegisterCallback<GeometryChangedEvent>(e => LayoutMapChrome());
            mapView.Add(mapCard);
            LayoutMapChrome();
        }
    }
}
