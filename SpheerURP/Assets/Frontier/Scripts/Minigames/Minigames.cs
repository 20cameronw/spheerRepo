using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public struct MinigameResult
    {
        public float quality;
        public int perfects;
        public int total;
    }

    /// <summary>Shared shell for all gathering mini-games: overlay, title, arena, tick loop.</summary>
    public abstract class Minigame
    {
        protected VisualElement overlay, arena, controls;
        protected Label status, titleLabel;
        protected float tolerance = 1f, difficulty = 0.3f;
        protected Color color = UiKit.Cyan;
        protected bool finished;
        IVisualElementScheduledItem loop;
        Action<MinigameResult> onDone;
        long lastMs;

        protected abstract string Title { get; }
        protected abstract string Hint { get; }
        protected abstract void Build();
        protected abstract void Tick(float dt);

        protected float ArenaW { get { float w = arena.layout.width; return float.IsNaN(w) ? 0f : w; } }
        protected float ArenaH { get { float h = arena.layout.height; return float.IsNaN(h) ? 0f : h; } }

        public void Run(VisualElement host, Color accent, float tol, float diff, Action<MinigameResult> done)
        {
            color = UiKit.Ui(accent); tolerance = tol; difficulty = diff; onDone = done;

            overlay = UiKit.Div("fr-overlay");
            titleLabel = UiKit.Text(Title, "fr-title", color);
            overlay.Add(titleLabel);
            overlay.Add(UiKit.Text(Hint, "fr-muted"));
            arena = UiKit.Div("fr-arena");
            UiKit.Border(arena, new Color(color.r, color.g, color.b, 0.7f), 3f);
            overlay.Add(arena);
            controls = UiKit.Div("fr-row");
            overlay.Add(controls);
            status = UiKit.Text("", "fr-h2", Color.white);
            overlay.Add(status);
            host.Add(overlay);

            Build();
            lastMs = (long)(Time.realtimeSinceStartup * 1000f);
            loop = overlay.schedule.Execute(() =>
            {
                long now = (long)(Time.realtimeSinceStartup * 1000f);
                float dt = Mathf.Min(0.05f, (now - lastMs) / 1000f);
                lastMs = now;
                if (!finished && ArenaW > 1f) Tick(dt);
            }).Every(16);
        }

        protected void Finish(MinigameResult r)
        {
            if (finished) return;
            finished = true;
            status.text = "Quality " + Mathf.RoundToInt(r.quality * 100f) + "%";
            overlay.schedule.Execute(() =>
            {
                if (loop != null) loop.Pause();
                overlay.RemoveFromHierarchy();
                if (onDone != null) onDone(r);
            }).StartingIn(900);
        }
    }

    /// <summary>Tap when the sweeping marker is inside the sweet zone.</summary>
    public class ChopGame : Minigame
    {
        const int Swings = 8;
        VisualElement zone, marker, orb;
        float phase, zoneCenter, zoneWidth, scoreSum, flash;
        int swing, perfects;
        bool locked;

        protected override string Title { get { return "CHOP"; } }
        protected override string Hint { get { return "Tap when the marker is in the glowing zone"; } }

        protected override void Build()
        {
            orb = UiKit.Div();
            orb.style.width = 240; orb.style.height = 240;
            UiKit.Radius(orb, 120);
            orb.style.backgroundColor = new Color(color.r, color.g, color.b, 0.25f);
            UiKit.Border(orb, color, 4f);
            UiKit.Halo(orb, color, 80f, 0.5f);
            orb.style.position = Position.Absolute;
            orb.style.left = Length.Percent(50); orb.style.top = Length.Percent(18);
            orb.style.marginLeft = -120;
            arena.Add(orb);

            var bar = UiKit.Div();
            bar.style.position = Position.Absolute;
            bar.style.left = Length.Percent(8); bar.style.right = Length.Percent(8);
            bar.style.top = Length.Percent(62); bar.style.height = 80;
            bar.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            UiKit.Radius(bar, 20);
            UiKit.Border(bar, new Color(1, 1, 1, 0.25f), 2f);
            arena.Add(bar);

            zone = UiKit.Div();
            zone.style.position = Position.Absolute; zone.style.top = 0; zone.style.bottom = 0;
            zone.style.backgroundColor = new Color(UiKit.Lime.r, UiKit.Lime.g, UiKit.Lime.b, 0.55f);
            UiKit.Radius(zone, 16);
            UiKit.Halo(zone, UiKit.Lime, 30f, 0.6f);
            bar.Add(zone);

            marker = UiKit.Div();
            marker.style.position = Position.Absolute; marker.style.top = -10; marker.style.bottom = -10;
            marker.style.width = 14;
            marker.style.backgroundColor = Color.white;
            UiKit.Radius(marker, 7);
            UiKit.Halo(marker, Color.white, 20f, 0.7f);
            bar.Add(marker);

            overlay.RegisterCallback<PointerDownEvent>(OnTap);
            NewZone();
            UpdateStatus();
        }

        void NewZone()
        {
            zoneWidth = Mathf.Lerp(0.26f, 0.1f, difficulty) * tolerance;
            zoneCenter = UnityEngine.Random.Range(zoneWidth * 0.5f + 0.05f, 1f - zoneWidth * 0.5f - 0.05f);
            zone.style.left = Length.Percent((zoneCenter - zoneWidth * 0.5f) * 100f);
            zone.style.width = Length.Percent(zoneWidth * 100f);
        }

        float Marker { get { return 0.5f + 0.5f * Mathf.Sin(phase); } }

        void UpdateStatus() { status.text = "Swing " + Mathf.Min(swing + 1, Swings) + " / " + Swings; }

        void OnTap(PointerDownEvent e)
        {
            if (finished || locked) return;
            float d = Mathf.Abs(Marker - zoneCenter);
            float s;
            if (d <= zoneWidth * 0.18f) { s = 1f; perfects++; Sfx.Good(); }
            else if (d <= zoneWidth * 0.5f) { s = 0.6f; Sfx.Play(520f, 0.1f, 0.25f); }
            else { s = 0f; Sfx.Bad(); }
            scoreSum += s;
            flash = 1f;
            orb.style.backgroundColor = s > 0f ? new Color(UiKit.Lime.r, UiKit.Lime.g, UiKit.Lime.b, 0.6f) : new Color(UiKit.Red.r, UiKit.Red.g, UiKit.Red.b, 0.6f);
            swing++;
            if (swing >= Swings)
            {
                locked = true;
                Finish(new MinigameResult { quality = scoreSum / Swings, perfects = perfects, total = Swings });
                return;
            }
            NewZone();
            UpdateStatus();
        }

        protected override void Tick(float dt)
        {
            phase += dt * Mathf.PI * 2f * (0.45f + 0.6f * difficulty);
            marker.style.left = Length.Percent(Marker * 100f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
            float s = 1f + flash * 0.15f;
            orb.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
            if (flash <= 0f) orb.style.backgroundColor = new Color(color.r, color.g, color.b, 0.25f);
        }
    }

    /// <summary>Tap glowing nodes before their ring collapses.</summary>
    public class MineGame : Minigame
    {
        const int TotalNodes = 12;
        class Node { public VisualElement root, ring; public float age, life; }
        readonly List<Node> nodes = new List<Node>();
        int spawned, resolved, perfects;
        float scoreSum, spawnTimer = 0.4f;

        protected override string Title { get { return "MINE"; } }
        protected override string Hint { get { return "Tap the nodes - later in the pulse is better"; } }
        protected override void Build() { status.text = "0 / " + TotalNodes; }

        void Spawn()
        {
            float size = 150f;
            var n = new Node { life = Mathf.Lerp(2.0f, 1.1f, difficulty) * Mathf.Min(1.4f, tolerance) };
            n.root = UiKit.Div();
            n.root.style.position = Position.Absolute;
            n.root.style.width = size; n.root.style.height = size;
            n.root.style.left = UnityEngine.Random.Range(10f, Mathf.Max(11f, ArenaW - size - 10f));
            n.root.style.top = UnityEngine.Random.Range(10f, Mathf.Max(11f, ArenaH - size - 10f));

            n.ring = UiKit.Div();
            n.ring.style.position = Position.Absolute;
            n.ring.style.left = 0; n.ring.style.right = 0; n.ring.style.top = 0; n.ring.style.bottom = 0;
            UiKit.Radius(n.ring, size / 2f);
            UiKit.Border(n.ring, color, 5f);
            n.root.Add(n.ring);

            var core = UiKit.Div();
            core.style.position = Position.Absolute;
            core.style.left = 35; core.style.right = 35; core.style.top = 35; core.style.bottom = 35;
            UiKit.Radius(core, 40);
            core.style.backgroundColor = new Color(color.r, color.g, color.b, 0.85f);
            UiKit.Halo(core, color, 50f, 0.7f);
            n.root.Add(core);

            n.root.RegisterCallback<PointerDownEvent>(e => { Hit(n); e.StopPropagation(); });
            arena.Add(n.root);
            nodes.Add(n);
            spawned++;
        }

        void Hit(Node n)
        {
            if (!nodes.Contains(n) || finished) return;
            float progress = Mathf.Clamp01(n.age / n.life);
            float s = 0.5f + 0.5f * progress;
            if (progress > 0.7f) { perfects++; s = 1f; Sfx.Good(); } else Sfx.Play(480f + progress * 300f, 0.08f, 0.25f);
            scoreSum += s;
            Resolve(n);
        }

        void Resolve(Node n)
        {
            nodes.Remove(n);
            n.root.RemoveFromHierarchy();
            resolved++;
            status.text = resolved + " / " + TotalNodes;
            if (resolved >= TotalNodes)
                Finish(new MinigameResult { quality = scoreSum / TotalNodes, perfects = perfects, total = TotalNodes });
        }

        protected override void Tick(float dt)
        {
            spawnTimer -= dt;
            if (spawned < TotalNodes && spawnTimer <= 0f)
            {
                Spawn();
                spawnTimer = Mathf.Lerp(1.0f, 0.65f, difficulty);
            }
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var n = nodes[i];
                n.age += dt;
                float k = Mathf.Clamp01(n.age / n.life);
                float s = Mathf.Lerp(2.2f, 1f, k);
                n.ring.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
                if (n.age >= n.life) { Sfx.Bad(); Resolve(n); }
            }
        }
    }

    /// <summary>Hold to build pressure; keep the gauge inside the moving band.</summary>
    public class DrillGame : Minigame
    {
        const float Duration = 12f;
        VisualElement gauge, band, needle;
        Label timeLabel;
        float t, pressure = 0.1f, vel, inBand;
        bool holding;

        protected override string Title { get { return "DRILL"; } }
        protected override string Hint { get { return "Hold to raise pressure - stay inside the band"; } }

        protected override void Build()
        {
            gauge = UiKit.Div();
            gauge.style.position = Position.Absolute;
            gauge.style.left = Length.Percent(50); gauge.style.marginLeft = -80;
            gauge.style.width = 160; gauge.style.top = 30; gauge.style.bottom = 30;
            gauge.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            UiKit.Radius(gauge, 30);
            UiKit.Border(gauge, new Color(1, 1, 1, 0.3f), 3f);
            arena.Add(gauge);

            band = UiKit.Div();
            band.style.position = Position.Absolute; band.style.left = 0; band.style.right = 0;
            band.style.backgroundColor = new Color(UiKit.Lime.r, UiKit.Lime.g, UiKit.Lime.b, 0.5f);
            UiKit.Radius(band, 20);
            UiKit.Halo(band, UiKit.Lime, 30f, 0.6f);
            gauge.Add(band);

            needle = UiKit.Div();
            needle.style.position = Position.Absolute; needle.style.left = -30; needle.style.right = -30;
            needle.style.height = 12;
            needle.style.backgroundColor = Color.white;
            UiKit.Radius(needle, 6);
            UiKit.Halo(needle, color, 25f, 0.8f);
            gauge.Add(needle);

            var btn = UiKit.Btn("HOLD", color, null, true);
            btn.style.minWidth = 520;
            btn.RegisterCallback<PointerDownEvent>(e => { holding = true; btn.CapturePointer(e.pointerId); });
            btn.RegisterCallback<PointerUpEvent>(e => { holding = false; if (btn.HasPointerCapture(e.pointerId)) btn.ReleasePointer(e.pointerId); });
            btn.RegisterCallback<PointerCancelEvent>(e => holding = false);
            controls.Add(btn);

            timeLabel = status;
        }

        protected override void Tick(float dt)
        {
            t += dt;
            float target = holding ? 0.7f : -0.8f;
            vel = Mathf.Lerp(vel, target, 5f * dt);
            pressure = Mathf.Clamp01(pressure + vel * dt);
            if (pressure <= 0f || pressure >= 1f) vel = 0f;

            float center = 0.5f + 0.3f * Mathf.Sin(t * (0.6f + difficulty)) + 0.08f * Mathf.Sin(t * 2.1f);
            float half = Mathf.Lerp(0.16f, 0.07f, difficulty) * tolerance * 0.5f * 2f;
            bool inside = Mathf.Abs(pressure - center) <= half;
            if (inside) inBand += dt;

            band.style.bottom = Length.Percent((center - half) * 100f);
            band.style.height = Length.Percent(half * 2f * 100f);
            needle.style.bottom = Length.Percent(pressure * 100f);
            needle.style.backgroundColor = inside ? Color.white : new Color(1f, 0.4f, 0.4f);
            timeLabel.text = Mathf.CeilToInt(Duration - t) + "s   " + Mathf.RoundToInt(inBand / Mathf.Max(0.01f, t) * 100f) + "% on target";

            if (t >= Duration)
                Finish(new MinigameResult { quality = Mathf.Clamp01(inBand / Duration), perfects = Mathf.RoundToInt(inBand), total = (int)Duration });
        }
    }
}
