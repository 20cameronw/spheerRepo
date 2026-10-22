using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        bool networkFinaleActive;

        void OpenFuelTanks()
        {
            if (busy || defense.Active) return;
            OpenModal("fuel-tanks", "fuel", UiText.Get("fuel.title"), UiKit.Amber, body =>
            {
                BuildFuelReserves(body);
                body.Add(UiKit.Text(UiText.Get("fuel.help"), "fr-muted"));
                var upgrade = IconBtn(UiText.Get("fuel.upgrade"), "up", UiKit.Amber,
                    () => { researchTab = 2; progressionTab = 1; OpenResearch(); });
                upgrade.name = "fuel-open-upgrades";
                body.Add(upgrade);
            }, true);
        }

        void BuildFuelReserves(VisualElement body)
        {
            body.Add(UiKit.Text(UiText.Get("fuel.gallons", gs.FuelGallons.ToString("N0")), "fr-h2", UiKit.Amber));
            body.Add(UiKit.Text(UiText.Get("fuel.summary", gs.FuelTankUsed, gs.FuelTankSlots), "fr-small"));
            body.Add(UiKit.Bar(UiKit.Amber, out var fill));
            UiKit.SetFill(fill, gs.FuelTankUsed / (float)gs.FuelTankSlots);
            if (gs.FuelTankUsed > gs.FuelTankSlots)
                body.Add(Note(UiText.Get("fuel.overflow"), UiKit.Amber));
            foreach (var resource in gs.db.resources)
                if (gs.IsTankFuel(resource.id))
                    StatRow(body, Icons.ForResource(resource), UiKit.Ui(resource.color),
                        resource.LocalizedName, UiKit.Amount(resource, gs.Count(resource)));
        }

        void OpenNetwork()
        {
            if (busy || defense.Active) return;
            CloseMap();
            ClosePopup();
            OpenModal("planet-network", "warp", UiText.Get("network.title"), UiKit.Cyan, BuildNetwork, true);
        }

        void BuildNetwork(VisualElement body)
        {
            body.Add(UiKit.Text(UiText.Get("network.help"), "fr-muted"));
            if (gs.NetworkRules == null)
            {
                body.Add(Note(UiText.Get("network.lock.unavailable"), UiKit.Amber));
                return;
            }
            if (gs.NetworkComplete)
            {
                body.Add(UiKit.Text(UiText.Get("network.complete.title"), "fr-h2", UiKit.Lime));
                body.Add(UiKit.Text(UiText.Get("network.complete.reward"), "fr-muted"));
                var replay = IconBtn(UiText.Get("network.replay"), "play", UiKit.Cyan, PlayNetworkFinale);
                replay.name = "network-replay";
                body.Add(replay);
                body.Add(IconBtn(UiText.Get("network.prestige"), "star", UiKit.Magenta, () =>
                {
                    OpenMenu(); menuPage = "ascension"; RefreshModal();
                }));
            }
            else
            {
                body.Add(SectionLabel(UiText.Get("network.project.title")));
                var projectHelp = UiKit.Text(UiText.Get("network.project.help"), "fr-small");
                projectHelp.style.whiteSpace = WhiteSpace.Normal;
                body.Add(projectHelp);
                string[] assigned = gs.NetworkAssignedWorlds();
                for (int i = 0; i < gs.NetworkRules.resourceWorlds.Length; i++)
                {
                    var resource = gs.db.FindResource(gs.NetworkRules.resourceWorlds[i]);
                    var p = gs.universe.Get(assigned[i]);
                    var row = UiKit.Div("fr-row");
                    row.style.marginBottom = UiKit.Space.S8;
                    var chip = ResourceIconChip(resource, resource.LocalizedName);
                    if (p == null)
                        chip = UiKit.BindResourceFind(chip, resource, OpenResourceSource, "network-find-" + resource.id);
                    row.Add(chip);
                    var status = UiKit.Text(p != null ? p.Name : UiText.Get("network.world.missing"), "fr-small", p != null ? UiKit.Lime : UiKit.Amber);
                    status.style.whiteSpace = WhiteSpace.Normal;
                    status.style.flexShrink = 1;
                    status.style.flexGrow = 1;
                    status.style.minWidth = 0;
                    row.Add(status);
                    body.Add(row);
                }
                gs.NetworkWarehouseStatus(out int warehouses, out int slots);
                var archiveStatus = UiKit.Text(UiText.Get("network.archive.status", warehouses, gs.NetworkWarehouseRequirement,
                    slots, gs.NetworkRules.minimumStorageSlots), "fr-small");
                archiveStatus.style.whiteSpace = WhiteSpace.Normal;
                body.Add(archiveStatus);
                body.Add(UiKit.Text(UiText.Get("network.archive.help"), "fr-muted"));
                body.Add(NetworkCostRow(gs.NetworkCosts(true)));
                string reason = gs.NetworkCompletionLockReason();
                if (reason != null) body.Add(Note(reason, UiKit.Amber));
                var complete = IconBtn(UiText.Get("network.complete"), "warp", UiKit.Lime, () =>
                {
                    if (busy || defense.Active) return;
                    if (gs.TryCompleteNetwork(out string message)) PlayNetworkFinale();
                    else Toast(message, UiKit.Amber);
                });
                complete.name = "network-complete";
                UiKit.SetEnabledLook(complete, reason == null);
                body.Add(complete);
            }
            body.Add(SectionLabel(UiText.Get("network.destinations")));
            body.Add(NetworkCostRow(gs.NetworkCosts(false)));
            var destinations = new List<PlanetData>();
            foreach (var delta in gs.world.All)
            {
                if (delta.planetId == gs.planet.Id || delta.visits <= 0) continue;
                var p = gs.universe.Get(delta.planetId);
                if (p != null && gs.StructureOfKind(p, StructureKind.WarpGate) != null) destinations.Add(p);
            }
            destinations.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            if (destinations.Count == 0)
                body.Add(UiKit.Text(UiText.Get("network.destinations.empty"), "fr-muted"));
            foreach (var destination in destinations)
            {
                bool linked = gs.HasPlanetLink(gs.planet, destination);
                body.Add(UiKit.Text(destination.Name, "fr-h2"));
                if (linked)
                {
                    body.Add(UiKit.Text(UiText.Get("network.link.active"), "fr-small", UiKit.Lime));
                    var fly = IconBtn(UiText.Get("network.travel"), "warp", UiKit.Cyan, () => StartTravel(destination));
                    UiKit.SetEnabledLook(fly, gs.PlanRoute(destination).reachable);
                    body.Add(fly);
                    continue;
                }
                string reason = gs.PlanetLinkLockReason(destination);
                var link = IconBtn(UiText.Get("network.link", destination.Name), "warp", UiKit.Cyan, () =>
                {
                    if (busy || defense.Active) return;
                    bool ok = gs.TryLinkPlanet(destination, out string message);
                    Toast(message, ok ? UiKit.Lime : UiKit.Amber);
                });
                link.name = "network-link-" + destination.Id;
                UiKit.SetEnabledLook(link, reason == null);
                body.Add(link);
                if (reason != null) body.Add(Note(reason, UiKit.Amber));
            }
            var map = IconBtn(UiText.Get("network.open-map"), "map", UiKit.Cyan, () => { CloseModal(); OpenMap(); });
            map.name = "network-open-map";
            body.Add(map);
        }

        VisualElement NetworkCostRow(List<ItemAmount> costs)
        {
            var row = UiKit.Div("fr-row-wrap");
            foreach (var cost in costs)
            {
                var chip = UiKit.ItemChip(cost.resource, cost.amount, gs.PlanetCount(cost.resource));
                var resource = cost.resource;
                if (gs.PlanetCount(resource) < cost.amount)
                    chip = UiKit.BindResourceFind(chip, resource, OpenResourceSource);
                row.Add(chip);
            }
            return row;
        }

        void PlayNetworkFinale()
        {
            if (busy || defense.Active || !gs.NetworkComplete) return;
            CloseAll();
            busy = true;
            StartCoroutine(NetworkFinale());
        }

        IEnumerator NetworkFinale()
        {
            var film = Layer();
            film.name = "network-finale";
            film.pickingMode = PickingMode.Position;
            film.style.backgroundColor = new Color(0.015f, 0.025f, 0.075f, 1);
            film.style.justifyContent = Justify.FlexEnd;
            film.style.paddingLeft = film.style.paddingRight = UiKit.Space.S24;
            film.style.paddingBottom = inset.w + UiKit.Space.S24;
            root.Add(film);
            var art = Layer();
            art.pickingMode = PickingMode.Ignore;
            film.Add(art);
            float elapsed = 0;
            art.generateVisualContent += context => DrawNetworkFinale(context.painter2D, art.contentRect.size, elapsed);
            var title = UiKit.Text(UiText.Get("network.film.assembly"), "fr-title", UiKit.Cyan);
            title.style.whiteSpace = WhiteSpace.Normal;
            var text = UiKit.Text(UiText.Get("network.film.assembly-help"), "fr-muted");
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.marginBottom = UiKit.Space.S24;
            film.Add(title); film.Add(text);
            bool skip = false, finish = false, prestige = false;
            skipShipMoment = () => { skip = true; finish = true; };
            var skipButton = UiKit.Btn(UiText.Get("travel.skip-cinematic"), UiKit.Cyan, () => skip = true);
            skipButton.name = "network-finale-skip";
            film.Add(skipButton);
            bool oldFrozen = planetView.Frozen;
            planetView.Frozen = true;
            networkFinaleActive = true;
            hud.style.display = DisplayStyle.None;
            try
            {
                Sfx.ShipIgnition();
                while (elapsed < 14 && !skip)
                {
                    elapsed += Time.unscaledDeltaTime;
                    title.text = UiText.Get(elapsed < 5 ? "network.film.assembly" : elapsed < 9 ? "network.film.connected" : "network.film.lightspeed");
                    text.text = UiText.Get(elapsed < 5 ? "network.film.assembly-help" : elapsed < 9 ? "network.film.connected-help" : "network.complete.reward");
                    art.MarkDirtyRepaint();
                    yield return null;
                }
                elapsed = 14;
                art.MarkDirtyRepaint();
                skipButton.RemoveFromHierarchy();
                title.text = UiText.Get("network.film.thanks");
                text.text = UiText.Get("network.film.thanks-help");
                var explore = IconBtn(UiText.Get("network.keep-playing"), "map", UiKit.Lime, () => finish = true);
                explore.name = "network-finale-explore";
                film.Add(explore);
                var ascend = IconBtn(UiText.Get("network.prestige"), "star", UiKit.Magenta, () => { prestige = true; finish = true; });
                ascend.name = "network-finale-prestige";
                film.Add(ascend);
                explore.Focus();
                skipShipMoment = () => finish = true;
                while (!finish) yield return null;
            }
            finally
            {
                skipShipMoment = null;
                film.RemoveFromHierarchy();
                planetView.Frozen = oldFrozen;
                networkFinaleActive = false;
                hud.style.display = DisplayStyle.Flex;
                busy = false;
            }
            if (prestige) { OpenMenu(); menuPage = "ascension"; RefreshModal(); }
        }

        static void DrawNetworkFinale(Painter2D p, Vector2 size, float time)
        {
            Vector2 center = new Vector2(size.x * 0.5f, size.y * 0.34f);
            float radius = Mathf.Min(size.x * 0.35f, size.y * 0.23f);
            for (int i = 0; i < 64; i++)
            {
                float angle = i * 2.399963f;
                float distance = radius * (0.3f + (i % 13) * 0.12f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 at = center + direction * distance;
                if (time >= 9)
                {
                    float length = Mathf.SmoothStep(0, radius * 0.5f, (time - 9) / 5f);
                    p.strokeColor = new Color(0.3f, 0.85f, 1f, 0.55f); p.lineWidth = 2;
                    p.BeginPath(); p.MoveTo(at); p.LineTo(at + direction * length); p.Stroke();
                }
                else MapDisc(p, at, i % 3 == 0 ? 2 : 1, new Color(0.65f, 0.8f, 1f, 0.7f));
            }
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 0.4f - Mathf.PI * 0.5f;
                Vector2 at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float progress = Mathf.Clamp01((time - i * 0.6f) / 1.2f);
                p.strokeColor = time >= 5 ? UiKit.Lime : UiKit.Cyan; p.lineWidth = 3;
                p.BeginPath(); p.MoveTo(at); p.LineTo(Vector2.Lerp(at, center, progress)); p.Stroke();
                Color color = Color.Lerp(UiKit.Cyan, UiKit.Magenta, i / 4f);
                MapDisc(p, at, 18, color);
                MapDisc(p, at + new Vector2(-5, -5), 5, Color.white);
                if (progress > 0 && progress < 1) MapDisc(p, Vector2.Lerp(at, center, progress), 5, Color.white);
            }
            MapDisc(p, center, 35, UiKit.Cyan);
            MapDisc(p, center + new Vector2(8, 7), 17, UiKit.Magenta);
            p.strokeColor = UiKit.Lime; p.lineWidth = 4;
            float ring = 45 + Mathf.Min(time, 9) * 2;
            p.BeginPath(); p.Arc(center, ring, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
        }
    }
}
