using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        const float TouchdownSettleDuration = 0.24f;
        const float TouchdownCameraImpulse = 0.035f;
        ITravelMinigame activeTravelMinigame;
        System.Action skipShipMoment;

        public IEnumerator PlayArrivalIntro(bool firstLaunch)
        {
            if (busy || defense.Active) yield break;
            busy = true;
            introActive = true;
            CloseAll();
            var planetRoot = planetView.Root.gameObject;
            bool planetVisible = planetRoot.activeSelf;
            bool surfaceVisible = world.gameObject.activeSelf;
            var top = hud.Q<VisualElement>(className: "fr-topbar");
            var dock = hud.Q<VisualElement>(className: "fr-dock");
            var intro = Layer();
            intro.name = "arrival-intro";
            intro.pickingMode = PickingMode.Position;
            intro.style.justifyContent = Justify.Center;
            intro.style.alignItems = Align.Center;
            root.Add(intro);
            bool skip = false;
            skipShipMoment = () => skip = true;
            var skipButton = UiKit.Btn(UiText.Get("travel.skip-cinematic"), UiKit.Cyan, () => skip = true);
            skipButton.name = "intro-skip";
            skipButton.style.position = Position.Absolute;
            skipButton.style.right = inset.z + UiKit.Space.S16;
            skipButton.style.top = inset.y + UiKit.Space.S16;
            intro.Add(skipButton);
            try
            {
                if (firstLaunch)
                {
                    planetRoot.SetActive(false);
                    world.gameObject.SetActive(false);
                    var logo = UiKit.Text(UiText.Get("about.title"), "fr-title", UiKit.Cyan);
                    logo.name = "intro-logo";
                    intro.Add(logo);
                    float elapsed = 0f;
                    while (elapsed < 0.65f && !skip)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                planetRoot.SetActive(planetVisible);
                world.gameObject.SetActive(surfaceVisible);
                intro.RemoveFromHierarchy();
                skipShipMoment = null;
                if (skip) yield break;
                yield return ShipMoment(true, firstLaunch ? 1.4f : 0.7f);
                introActive = false;
                float reveal = 0f;
                while (reveal < 0.22f)
                {
                    float offset = Mathf.Pow(1f - Mathf.Clamp01(reveal / 0.22f), 3f);
                    top.style.translate = new Translate(0f, -HudTopInset * offset, 0f);
                    dock.style.translate = new Translate(0f, DockInset * offset, 0f);
                    reveal += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            finally
            {
                if (planetRoot != null) planetRoot.SetActive(planetVisible);
                if (world != null) world.gameObject.SetActive(surfaceVisible);
                intro.RemoveFromHierarchy();
                skipShipMoment = null;
                top.style.translate = StyleKeyword.Null;
                dock.style.translate = StyleKeyword.Null;
                busy = false;
                introActive = false;
                hud.style.display = DisplayStyle.Flex;
                UpdateFirstSessionGuideVisibility();
            }
        }

        void StartTravel(PlanetData target, bool autopilot = false)
        {
            if (busy || defense.Active || backdrop.Warping) return;
            RoutePlan plan = gs.PlanRoute(target, autopilot);
            if (!plan.reachable) { Sfx.Bad(); Toast(RouteStatus(plan), UiKit.Red); return; }
            busy = true;
            CloseAll();
            StartCoroutine(TravelSequence(target, plan));
        }

        IEnumerator TravelSequence(PlanetData target, RoutePlan plan)
        {
            IsTravelling = true;
            yield return ShipMoment(false);
            StartCoroutine(backdrop.DoWarp(null));
            yield return planetView.Depart();
            while (backdrop.Warping) yield return null;
            var results = new List<TravelRunResult>();
            var from = gs.planet;
            VisualElement autoOverlay = null;
            Label progress = null;
            if (plan.autopilot)
            {
                autoOverlay = CreateAutopilotOverlay(out progress);
            }

            for (int i = 0; i < plan.LegCount; i++)
            {
                RouteLeg leg = plan.legs[i];
                if (plan.autopilot)
                {
                    float remaining = leg.seconds;
                    int shownSeconds = -1;
                    while (remaining > 0)
                    {
                        int seconds = Mathf.CeilToInt(remaining);
                        if (seconds != shownSeconds)
                        {
                            shownSeconds = seconds;
                            progress.text = UiText.Get("travel.leg-progress", i + 1, plan.LegCount, seconds);
                        }
                        remaining -= Time.unscaledDeltaTime;
                        yield return null;
                    }
                    results.Add(default);
                    continue;
                }
                var request = new TravelRunRequest
                {
                    seed = Frontier.Universe.Hash.Fnv1a64(leg.fromSystemId + ">" + leg.toSystemId) ^ (ulong)(uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                    distanceLy = leg.lengthLy, danger = target.Danger, tier = target.Tier,
                    legIndex = i, legCount = plan.LegCount, from = from, to = target,
                    seconds = leg.seconds, fuelCost = leg.fuel,
                    fromSystemId = leg.fromSystemId, toSystemId = leg.toSystemId
                };
                bool completed = false;
                TravelRunResult result = default;
                activeTravelMinigame = TravelMinigames.Create();
                activeTravelMinigame.Run(overlayLayer, request, r => { result = r; completed = true; });
                while (!completed) yield return null;
                activeTravelMinigame = null;
                results.Add(result);
            }
            if (autoOverlay != null) autoOverlay.RemoveFromHierarchy();
            if (!gs.Travel(plan, target))
            {
                yield return planetView.Approach(gs.planet, gs.TerraformerStageOn(gs.planet));
                yield return ShipMoment(true);
                busy = false;
                IsTravelling = false;
                Toast(UiText.Get("travel.route-changed"), UiKit.Red);
                yield break;
            }
            int refund = 0;
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (!result.completed) continue;
                refund += gs.RefundLeg(plan.legs[i], result.quality, plan.autopilot);
                if (result.loot != null)
                    foreach (var item in result.loot) if (item.resource != null && item.amount > 0) gs.Add(item.resource, item.amount);
                if (result.xp > 0) gs.AddXp(result.xp);
            }
            gs.Flush();
            backdrop.Apply(target);
            selected = target;
            yield return planetView.Approach(target, gs.TerraformerStageOn(target));
            yield return ShipMoment(true);
            busy = false;
            IsTravelling = false;
            Toast(UiText.Get(refund > 0 ? "travel.arrived.refund" : "travel.arrived", target.Name, refund), UiKit.Cyan);
        }

        IEnumerator ShipMoment(bool arriving, float duration = 0.7f)
        {
            var cameraTransform = cam != null ? cam.transform : null;
            Vector3 cameraOrigin = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            var ship = ShipRig.Create(transform);
            ship.transform.localScale = Vector3.one * 0.48f;
            Vector3 center = planetView.Spin.position;
            Vector3 radial = (cam.transform.position - center).normalized;
            Vector3 ground = center + radial * (planetView.SurfaceRadius() + 0.3f) + cam.transform.up * 0.2f;
            Vector3 sky = ground + cam.transform.up * 2f + radial * 1.2f;
            ship.transform.rotation = Quaternion.LookRotation(cam.transform.up, radial);
            var heat = arriving ? Fx.GlowQuad(ship.transform, UiKit.Tint(UiKit.Amber, 0.45f), 1.3f,
                Vector3.forward * 0.45f) : null;
            bool skip = false;
            skipShipMoment = () => skip = true;
            var skipButton = UiKit.Btn(UiText.Get("travel.skip-cinematic"), UiKit.Cyan, () => skip = true);
            skipButton.name = "travel-skip-cinematic";
            skipButton.style.position = Position.Absolute;
            skipButton.style.right = UiKit.Space.S16;
            skipButton.style.top = UiKit.Space.S16;
            overlayLayer.Add(skipButton);
            try
            {
                Sfx.ShipIgnition();
                float elapsed = 0f;
                while (elapsed < duration && !skip)
                {
                    float t = Mathf.Clamp01(elapsed / duration);
                    float height = arriving ? Mathf.Pow(1f - t, 3f) : t * t;
                    ship.transform.position = Vector3.Lerp(ground, sky, height);
                    ship.SetFlight(arriving ? 0.3f + height * 0.7f : 0.3f + t * 0.7f,
                        arriving ? Mathf.Clamp01(t * 2f) : Mathf.Clamp01(1f - t * 3f), 0f);
                    if (heat != null)
                    {
                        heat.SetActive(t < 0.75f);
                        FxTransientPool.SetGlowQuadSize(heat.transform, 0.3f + height);
                    }
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (arriving)
                {
                    ship.transform.position = ground;
                    ship.SetFlight(0f, 1f, 0f);
                    Sfx.ShipTouchdown();
                    Fx.Ripple(ground - radial * 0.2f, UiKit.Cyan, 1.2f, 0.4f);
                    Fx.Burst(ground - radial * 0.2f, UiKit.Tint(gs.planet.Primary, 0.6f), 12, 0.7f, 0.08f);
                    float settleElapsed = 0f;
                    while (cameraTransform != null && settleElapsed < TouchdownSettleDuration && !skip)
                    {
                        float t = Mathf.Clamp01(settleElapsed / TouchdownSettleDuration);
                        float offset = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * TouchdownCameraImpulse;
                        cameraTransform.position = cameraOrigin + cameraTransform.up * offset;
                        settleElapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }
            finally
            {
                if (cameraTransform != null) cameraTransform.position = cameraOrigin;
                skipShipMoment = null;
                skipButton.RemoveFromHierarchy();
                if (heat != null) FxTransientPool.ReleaseGlowQuad(heat, true);
                if (ship != null) Destroy(ship.gameObject);
            }
        }

        VisualElement CreateAutopilotOverlay(out Label progress)
        {
            var overlay = UiKit.Div("fr-overlay");
            overlay.Add(UiKit.Text(UiText.Get("travel.autopilot"), "fr-title", UiKit.Cyan));
            progress = UiKit.Text("", "fr-h2");
            overlay.Add(progress);
            overlay.Add(UiKit.Text(UiText.Get("travel.autopilot.help"), "fr-small", UiKit.Muted));
            overlayLayer.Add(overlay);
            return overlay;
        }

        static string RouteStatus(RoutePlan p)
        {
            if (p == null) return UiText.Get("route.calculating");
            if (p.Has(RouteReasons.AlreadyHere)) return UiText.Get("route.already-here");
            if (p.Has(RouteReasons.Unknown)) return UiText.Get("route.unknown");
            if (p.Has(RouteReasons.NoJumpDrive)) return UiText.Get("route.no-drive");
            if (p.Has(RouteReasons.SearchLimit)) return UiText.Get("route.search-limit");
            if (p.Has(RouteReasons.OutOfRange)) return UiText.Get("route.out-of-range");
            if (p.Has(RouteReasons.Hazard)) return UiText.Get("route.hazard");
            if (p.Has(RouteReasons.NotEnoughFuel)) return UiText.Get("route.fuel-short", p.fuelShort);
            return UiText.Get(p.intraSystem ? "route.orbital" : p.LegCount == 1 ? "route.single-jump" : "route.multiple-jumps",
                p.LegCount, p.totalLengthLy.ToString("0.#"));
        }
    }
}
