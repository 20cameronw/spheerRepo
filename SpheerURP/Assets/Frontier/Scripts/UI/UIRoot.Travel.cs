using System.Collections;
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
            skipShipMoment = null;
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
                    while (elapsed < 0.65f)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                planetRoot.SetActive(planetVisible);
                world.gameObject.SetActive(surfaceVisible);
                intro.RemoveFromHierarchy();
                skipShipMoment = null;
                yield return ShipMoment(true, firstLaunch ? 5.2f : 4.6f, true);
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
            if (plan.instant)
            {
                if (!gs.TryInstantTravel(target, out string instantError)) { Toast(instantError, UiKit.Amber); return; }
                busy = true;
                CloseAll();
                StartCoroutine(InstantArrival());
                return;
            }
            if (!gs.BeginTravelCheckpoint(plan, out string error)) { Sfx.Bad(); Toast(error, UiKit.Red); return; }
            busy = true;
            CloseAll();
            StartCoroutine(TravelSequence(target, plan));
        }

        IEnumerator InstantArrival()
        {
            try
            {
                IsTravelling = true;
                yield return backdrop.DoWarp(() => planetView.Show(gs.planet, true, gs.TerraformerStageOn(gs.planet)));
                yield return ShipMoment(true, 0.45f);
                Toast(UiText.Get("travel.arrived", gs.planet.Name), UiKit.Cyan);
            }
            finally
            {
                IsTravelling = false;
                busy = false;
            }
        }

        IEnumerator TravelSequence(PlanetData target, RoutePlan plan)
        {
            IsTravelling = true;
            yield return ShipMoment(false);
            StartCoroutine(backdrop.DoWarp(null));
            yield return planetView.Depart();
            while (backdrop.Warping) yield return null;
            var trip = gs.PendingTravel;
            var from = gs.planet;
            VisualElement autoOverlay = null;
            Label progress = null;
            if (plan.autopilot)
            {
                autoOverlay = CreateAutopilotOverlay(out progress);
            }

            for (int i = trip.nextLeg; i < plan.LegCount; i++)
            {
                RouteLeg leg = plan.legs[i];
                var checkpoint = trip.legs[i];
                if (plan.autopilot)
                {
                    float remaining = checkpoint.autopilotRemaining;
                    int shownSeconds = -1;
                    while (remaining > 0)
                    {
                        int seconds = Mathf.CeilToInt(remaining);
                        if (seconds != shownSeconds)
                        {
                            shownSeconds = seconds;
                            progress.text = UiText.Get("travel.leg-progress", i + 1, plan.LegCount, seconds);
                            gs.MarkTravelDirty();
                        }
                        remaining = Mathf.Max(0, remaining - Time.unscaledDeltaTime);
                        checkpoint.autopilotRemaining = remaining;
                        yield return null;
                    }
                    if (!gs.CompleteTravelLeg(false, 0, 0, null))
                        Toast(UiText.Get("travel.recovery.pending-save"), UiKit.Amber);
                    continue;
                }
                if (!SeedText.TryParse(checkpoint.seed, out ulong seed))
                    throw new System.InvalidOperationException("The active travel leg has an invalid seed.");
                var request = new TravelRunRequest
                {
                    seed = seed, checkpoint = checkpoint,
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
                if (!gs.CompleteTravelLeg(result.completed, result.quality, result.xp, result.loot))
                    Toast(UiText.Get("travel.recovery.pending-save"), UiKit.Amber);
            }
            if (autoOverlay != null) autoOverlay.RemoveFromHierarchy();
            if (!gs.SettlePendingTravel(out int refund, out string message))
            {
                yield return planetView.Approach(gs.planet, gs.TerraformerStageOn(gs.planet));
                IsTravelling = false;
                ShowTravelRecoveryFailure(message);
                yield break;
            }
            backdrop.Apply(target);
            selected = target;
            yield return planetView.Approach(target, gs.TerraformerStageOn(target));
            yield return ShipMoment(true);
            busy = false;
            IsTravelling = false;
            Toast(string.IsNullOrEmpty(message)
                ? UiText.Get(refund > 0 ? "travel.arrived.refund" : "travel.arrived",
                    target.Name, (long)refund * GameState.GallonsPerEnergy) : message,
                string.IsNullOrEmpty(message) ? UiKit.Cyan : UiKit.Amber);
        }

        public void ResumeInterruptedTravel()
        {
            if (gs.PendingTravel == null || busy) return;
            busy = true;
            CloseAll();
            if (!gs.ValidatePendingTravel(out var target, out string error))
            {
                ShowTravelRecoveryFailure(error);
                return;
            }
            if (TravelMinigames.PrepareRecovery == null)
            {
                ShowTravelRecoveryFailure(UiText.Get("travel.recovery.unavailable"));
                return;
            }
            error = TravelMinigames.PrepareRecovery(gs.PendingTravel, gs.planet, target);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning("Frontier travel recovery refused: " + error);
                ShowTravelRecoveryFailure(UiText.Get("travel.recovery.incompatible"));
                return;
            }
            StartCoroutine(TravelSequence(target, gs.PendingTravel.plan));
        }

        void ShowTravelRecoveryFailure(string message)
        {
            var overlay = Layer();
            overlay.name = "travel-recovery-error";
            overlay.pickingMode = PickingMode.Position;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            var card = Card(UiKit.Amber);
            card.style.width = Length.Percent(90);
            card.style.maxWidth = 680;
            card.style.maxHeight = Length.Percent(90);
            card.Add(UiKit.Text(UiText.Get("travel.recovery.title"), "fr-h2"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            scroll.style.minHeight = 0;
            var detail = UiKit.Text(message, "fr-muted");
            scroll.Add(detail);
            scroll.Add(UiKit.Text(UiText.Get("travel.recovery.discard-help"), "fr-small"));
            card.Add(scroll);
            var discard = UiKit.Btn(UiText.Get("travel.recovery.discard"), UiKit.Amber, () =>
            {
                if (!gs.DiscardPendingTravel(out string error)) { detail.text = error; return; }
                overlay.RemoveFromHierarchy();
                busy = false;
                IsTravelling = false;
                OnStateChanged();
                if (!string.IsNullOrEmpty(error)) Toast(error, UiKit.Amber);
            });
            discard.name = "travel-recovery-discard";
            card.Add(discard);
            overlay.Add(card);
            root.Add(overlay);
        }

        IEnumerator ShipMoment(bool arriving, float duration = 0.7f, bool orbitalApproach = false)
        {
            var cameraTransform = cam != null ? cam.transform : null;
            Vector3 cameraOrigin = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            var ship = ShipRig.Create(transform);
            ship.transform.localScale = Vector3.one * 0.48f;
            Vector3 center = planetView.Spin.position;
            Vector3 radial = (cam.transform.position - center).normalized;
            Vector3 up = Vector3.ProjectOnPlane(cam.transform.up, radial).normalized;
            float radius = planetView.SurfaceRadius();
            float tangentDepth = cam.orthographic ? 0f : Mathf.Clamp01(radius / Vector3.Distance(cam.transform.position, center));
            Vector3 contact = center + (up * Mathf.Sqrt(1f - tangentDepth * tangentDepth) + radial * tangentDepth) * radius;
            Vector3 ground = contact + up * 0.2f;
            Vector3 sky = ground + up * (orbitalApproach ? 0.85f : 2f);
            ship.transform.rotation = Quaternion.LookRotation(cam.transform.right, up);
            bool skip = false;
            Button skipButton = null;
            skipShipMoment = null;
            if (!orbitalApproach)
            {
                skipShipMoment = () => skip = true;
                skipButton = UiKit.Btn(UiText.Get("travel.skip-cinematic"), UiKit.Cyan, () => skip = true);
                skipButton.name = "travel-skip-cinematic";
                skipButton.style.position = Position.Absolute;
                skipButton.style.right = UiKit.Space.S16;
                skipButton.style.top = UiKit.Space.S16;
                overlayLayer.Add(skipButton);
            }
            try
            {
                Sfx.ShipIgnition();
                float elapsed = 0f;
                while (elapsed < duration && !skip)
                {
                    float t = Mathf.Clamp01(elapsed / duration);
                    if (orbitalApproach && t < 0.68f)
                    {
                        float orbit = t / 0.68f;
                        float angle = -Mathf.PI * 2f * Mathf.Pow(1f - orbit, 1.2f);
                        Vector3 orbitUp = (sky - center).normalized;
                        Vector3 outward = orbitUp * Mathf.Cos(angle) + cam.transform.right * Mathf.Sin(angle);
                        Vector3 tangent = cam.transform.right * Mathf.Cos(angle) - orbitUp * Mathf.Sin(angle);
                        ship.transform.position = center + outward * Vector3.Distance(sky, center);
                        ship.transform.rotation = Quaternion.LookRotation(tangent, outward);
                        float braking = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.76f, 1f, orbit));
                        ship.SetFlight(0.65f * (1f - braking), braking, 0f);
                        ship.SetLandingThrust(braking * 0.7f, elapsed);
                    }
                    else
                    {
                        float descent = orbitalApproach ? Mathf.InverseLerp(0.68f, 1f, t) : t;
                        float height = arriving ? Mathf.Pow(1f - descent, 3f) : descent * descent;
                        ship.transform.position = Vector3.Lerp(ground, sky, height);
                        ship.transform.rotation = Quaternion.LookRotation(cam.transform.right, up);
                        ship.SetFlight(0f,
                            arriving ? Mathf.Clamp01(descent * 2f + (orbitalApproach ? 1f : 0f)) :
                            Mathf.Clamp01(1f - descent * 3f), 0f);
                        float thrust = arriving ? (0.7f + Mathf.Sin(descent * Mathf.PI) * 0.3f) *
                            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, descent))) :
                            0.35f + descent * 0.65f;
                        ship.SetLandingThrust(thrust, elapsed);
                    }
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (arriving)
                {
                    ship.transform.position = ground;
                    ship.SetFlight(0f, 1f, 0f);
                    ship.SetLandingThrust(0f, 0f);
                    Sfx.ShipTouchdown();
                    Fx.Ripple(contact, UiKit.Cyan, 1.2f, 0.4f);
                    Fx.Burst(contact, UiKit.Tint(gs.planet.Primary, 0.6f), 12, 0.7f, 0.08f);
                    float settleElapsed = 0f;
                    while (cameraTransform != null && settleElapsed < TouchdownSettleDuration && !skip)
                    {
                        float t = Mathf.Clamp01(settleElapsed / TouchdownSettleDuration);
                        float offset = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * TouchdownCameraImpulse;
                        ship.transform.position = ground - up * (Mathf.Sin(t * Mathf.PI) * 0.025f);
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
                skipButton?.RemoveFromHierarchy();
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
            if (p.Has(RouteReasons.NotEnoughFuel)) return UiText.Get("route.fuel-short", (long)p.fuelShort * GameState.GallonsPerEnergy);
            if (p.instant) return UiText.Get("network.route.free");
            return UiText.Get(p.intraSystem ? "route.orbital" : p.LegCount == 1 ? "route.single-jump" : "route.multiple-jumps",
                p.LegCount, p.totalLengthLy.ToString("0.#"));
        }
    }
}
