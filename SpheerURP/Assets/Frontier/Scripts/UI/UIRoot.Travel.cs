using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
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
                    while (remaining > 0)
                    {
                        progress.text = "Leg " + (i + 1) + " / " + plan.LegCount + " · " + Mathf.CeilToInt(remaining) + "s";
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
                    seconds = leg.seconds, fromSystemId = leg.fromSystemId, toSystemId = leg.toSystemId
                };
                bool completed = false;
                TravelRunResult result = default;
                TravelMinigames.Create().Run(overlayLayer, request, r => { result = r; completed = true; });
                while (!completed) yield return null;
                results.Add(result);
            }
            if (autoOverlay != null) autoOverlay.RemoveFromHierarchy();
            if (!gs.Travel(plan, target))
            {
                yield return planetView.Approach(gs.planet);
                busy = false;
                Toast("Travel changed. Check your route and fuel, then try again.", UiKit.Red);
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
            yield return planetView.Approach(target);
            busy = false;
            Toast("Arrived at " + target.Name + (refund > 0 ? " · +" + refund + " fuel back" : ""), UiKit.Cyan);
        }

        VisualElement CreateAutopilotOverlay(out Label progress)
        {
            var overlay = UiKit.Div("fr-overlay");
            overlay.Add(UiKit.Text("AUTOPILOT", "fr-title", UiKit.Cyan));
            progress = UiKit.Text("", "fr-h2");
            overlay.Add(progress);
            overlay.Add(UiKit.Text("Fuel x1.6 · no piloting refund", "fr-small", UiKit.Muted));
            overlayLayer.Add(overlay);
            return overlay;
        }

        static string RouteStatus(RoutePlan p)
        {
            if (p == null) return "Calculating route…";
            if (p.Has(RouteReasons.AlreadyHere)) return "You are here";
            if (p.Has(RouteReasons.Unknown)) return "Destination unavailable";
            if (p.Has(RouteReasons.NoJumpDrive)) return "Jump drive required · upgrade your ship";
            if (p.Has(RouteReasons.SearchLimit)) return "Route search limit · choose a nearer stop";
            if (p.Has(RouteReasons.OutOfRange)) return "No route in jump range · upgrade your drive";
            if (p.Has(RouteReasons.Hazard)) return "Hull plating required · see hazards below";
            if (p.Has(RouteReasons.NotEnoughFuel)) return "Need " + p.fuelShort + " more energy · craft fuel";
            return p.LegCount + (p.intraSystem ? " orbital hop" : p.LegCount == 1 ? " jump" : " jumps") + " · " + p.totalLengthLy.ToString("0.#") + " ly";
        }
    }
}
