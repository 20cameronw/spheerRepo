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

    /// <summary>Shared shell for all mini-games: overlay, title, arena, tick loop.</summary>
    public abstract class Minigame
    {
        protected VisualElement overlay, arena, controls;
        protected Label status, titleLabel;
        protected float tolerance = 1f, difficulty = 0.3f;
        /// <summary>Damage multiplier of the equipped tool (1 = bare hands).</summary>
        public float toolDamage = 1f;
        public int toolLevel;
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

        protected static void Place(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
        }
    }

    /// <summary>
    /// Rust-style resource node: strike the glowing weak spot to break the node down through stages.
    /// No timer - better tools deal more damage per hit and recover from a swing faster.
    /// </summary>
    public class NodeGame : Minigame
    {
        const int Stages = 3;
        readonly bool tree;
        VisualElement node, spot, spotCore, hpFill, swingFill;
        Vector2 spotPos;
        float spotSize, stageHp, hp, lockT, swingTime, pulse, shake;
        int stage, taps, spotHits, crits;
        bool ready;

        public NodeGame(bool tree) { this.tree = tree; }

        protected override string Title { get { return tree ? "FELL THE TREE" : "BREAK THE NODE"; } }
        protected override string Hint { get { return "Hit the glowing weak spot - it moves after every strike"; } }

        float NodeW { get { return tree ? 240f : 380f; } }
        float NodeH { get { return tree ? 440f : 380f; } }

        protected override void Build()
        {
            node = UiKit.Div();
            node.style.position = Position.Absolute;
            node.style.backgroundColor = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.95f);
            UiKit.Radius(node, tree ? 44f : 190f);
            UiKit.Border(node, color, 5f);
            UiKit.Halo(node, color, 70f, 0.45f);
            arena.Add(node);

            spot = UiKit.Div();
            spot.style.position = Position.Absolute;
            spot.pickingMode = PickingMode.Ignore;
            UiKit.Radius(spot, 200f);
            spot.style.backgroundColor = new Color(1f, 0.95f, 0.5f, 0.28f);
            UiKit.Border(spot, new Color(1f, 0.95f, 0.5f, 0.9f), 3f);
            UiKit.Halo(spot, new Color(1f, 0.9f, 0.3f), 50f, 0.8f);
            spotCore = UiKit.Div();
            spotCore.pickingMode = PickingMode.Ignore;
            spotCore.style.position = Position.Absolute;
            spotCore.style.left = Length.Percent(35); spotCore.style.right = Length.Percent(35);
            spotCore.style.top = Length.Percent(35); spotCore.style.bottom = Length.Percent(35);
            UiKit.Radius(spotCore, 200f);
            spotCore.style.backgroundColor = Color.white;
            spot.Add(spotCore);
            arena.Add(spot);

            var hpBar = UiKit.Div();
            hpBar.pickingMode = PickingMode.Ignore;
            hpBar.style.position = Position.Absolute;
            hpBar.style.left = Length.Percent(10); hpBar.style.right = Length.Percent(10);
            hpBar.style.top = 14; hpBar.style.height = 22;
            hpBar.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            UiKit.Radius(hpBar, 11f);
            UiKit.Border(hpBar, new Color(1, 1, 1, 0.3f), 2f);
            hpFill = UiKit.Div();
            hpFill.style.height = Length.Percent(100);
            UiKit.Radius(hpFill, 11f);
            hpFill.style.backgroundColor = color;
            hpBar.Add(hpFill);
            arena.Add(hpBar);

            var swBar = UiKit.Div();
            swBar.pickingMode = PickingMode.Ignore;
            swBar.style.position = Position.Absolute;
            swBar.style.left = Length.Percent(30); swBar.style.right = Length.Percent(30);
            swBar.style.bottom = 14; swBar.style.height = 10;
            swBar.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            UiKit.Radius(swBar, 5f);
            swingFill = UiKit.Div();
            swingFill.style.height = Length.Percent(100);
            UiKit.Radius(swingFill, 5f);
            swingFill.style.backgroundColor = Color.white;
            swBar.Add(swingFill);
            arena.Add(swBar);

            swingTime = 0.55f / Mathf.Sqrt(Mathf.Max(1f, toolDamage));
            stageHp = Mathf.Lerp(3f, 5.5f, difficulty);
            hp = stageHp;
            spotSize = Mathf.Clamp(120f * tolerance, 90f, 190f);

            arena.RegisterCallback<PointerDownEvent>(OnTap);
            MoveSpot();
            Status();
        }

        void Layout()
        {
            Place(node, (ArenaW - NodeW) * 0.5f, (ArenaH - NodeH) * 0.5f, NodeW, NodeH);
            Place(spot, spotPos.x - spotSize * 0.5f, spotPos.y - spotSize * 0.5f, spotSize, spotSize);
        }

        void MoveSpot()
        {
            float w = ArenaW > 1f ? ArenaW : 700f, h = ArenaH > 1f ? ArenaH : 700f;
            float rx = NodeW * 0.5f - spotSize * 0.55f, ry = NodeH * 0.5f - spotSize * 0.55f;
            var old = spotPos;
            for (int i = 0; i < 8; i++)
            {
                spotPos = new Vector2(w * 0.5f + UnityEngine.Random.Range(-rx, rx), h * 0.5f + UnityEngine.Random.Range(-ry, ry));
                if ((spotPos - old).magnitude > spotSize * 0.9f) break;
            }
            if (ArenaW > 1f) { ready = true; Layout(); }
        }

        void Status() { status.text = "Stage " + Mathf.Min(stage + 1, Stages) + " / " + Stages + "   Tool x" + toolDamage.ToString("0.0"); }

        void OnTap(PointerDownEvent e)
        {
            if (finished || lockT > 0f) return;
            Vector2 p = arena.WorldToLocal(e.position);
            taps++;
            float d = (p - spotPos).magnitude;
            float half = spotSize * 0.5f;
            float dmg;
            if (d <= half * 0.4f) { dmg = toolDamage * 1.7f; crits++; spotHits++; Sfx.Good(); }
            else if (d <= half) { dmg = toolDamage; spotHits++; Sfx.Play(520f, 0.1f, 0.3f); }
            else
            {
                bool onNode = Mathf.Abs(p.x - ArenaW * 0.5f) < NodeW * 0.5f && Mathf.Abs(p.y - ArenaH * 0.5f) < NodeH * 0.5f;
                dmg = onNode ? toolDamage * 0.2f : 0f;
                Sfx.Bad();
            }
            lockT = swingTime;
            shake = 1f;
            hp -= dmg;
            if (dmg > toolDamage * 0.5f) MoveSpot();
            if (hp <= 0f)
            {
                stage++;
                Sfx.Play(300f, 0.2f, 0.4f);
                if (stage >= Stages)
                {
                    float acc = taps > 0 ? (float)spotHits / taps : 0f;
                    Finish(new MinigameResult { quality = Mathf.Clamp01(0.3f + 0.7f * acc), perfects = crits, total = taps });
                    return;
                }
                hp = stageHp;
                float k = 1f - 0.18f * stage;
                node.style.scale = new StyleScale(new Scale(new Vector3(k, k, 1f)));
            }
            Status();
        }

        protected override void Tick(float dt)
        {
            if (!ready) { ready = true; MoveSpot(); }
            Layout();
            lockT = Mathf.Max(0f, lockT - dt);
            pulse += dt * 5f;
            shake = Mathf.MoveTowards(shake, 0f, dt * 4f);
            float s = 1f + 0.1f * Mathf.Sin(pulse);
            spot.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
            spot.style.opacity = lockT > 0f ? 0.35f : 1f;
            node.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-1f, 1f) * 10f * shake, UnityEngine.Random.Range(-1f, 1f) * 10f * shake));
            hpFill.style.width = Length.Percent(Mathf.Clamp01(hp / stageHp) * 100f);
            swingFill.style.width = Length.Percent((swingTime > 0f ? 1f - lockT / swingTime : 1f) * 100f);
        }
    }

    /// <summary>Hold to bore: keep the gauge in the moving band to fill the progress bar. No timer - better drills fill it faster.</summary>
    public class DrillGame : Minigame
    {
        VisualElement gauge, band, needle, progFill;
        float t, pressure = 0.1f, vel, inBand, progress;
        bool holding;

        protected override string Title { get { return "BORE"; } }
        protected override string Hint { get { return "Hold to raise pressure - stay inside the band to drill"; } }

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

            var prog = UiKit.Div();
            prog.style.position = Position.Absolute;
            prog.style.left = Length.Percent(8); prog.style.right = Length.Percent(68);
            prog.style.top = 30; prog.style.bottom = 30;
            prog.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            UiKit.Radius(prog, 16);
            UiKit.Border(prog, new Color(1, 1, 1, 0.3f), 3f);
            progFill = UiKit.Div();
            progFill.style.position = Position.Absolute;
            progFill.style.left = 0; progFill.style.right = 0; progFill.style.bottom = 0;
            UiKit.Radius(progFill, 16);
            progFill.style.backgroundColor = color;
            UiKit.Halo(progFill, color, 30f, 0.6f);
            prog.Add(progFill);
            arena.Add(prog);

            var btn = UiKit.Btn("HOLD", color, null, true);
            btn.style.minWidth = 520;
            btn.RegisterCallback<PointerDownEvent>(e => { holding = true; btn.CapturePointer(e.pointerId); });
            btn.RegisterCallback<PointerUpEvent>(e => { holding = false; if (btn.HasPointerCapture(e.pointerId)) btn.ReleasePointer(e.pointerId); });
            btn.RegisterCallback<PointerCancelEvent>(e => holding = false);
            controls.Add(btn);
        }

        protected override void Tick(float dt)
        {
            t += dt;
            float target = holding ? 0.7f : -0.8f;
            vel = Mathf.Lerp(vel, target, 5f * dt);
            pressure = Mathf.Clamp01(pressure + vel * dt);
            if (pressure <= 0f || pressure >= 1f) vel = 0f;

            float center = 0.5f + 0.3f * Mathf.Sin(t * (0.6f + difficulty)) + 0.08f * Mathf.Sin(t * 2.1f);
            float half = Mathf.Lerp(0.16f, 0.07f, difficulty) * tolerance;
            bool inside = Mathf.Abs(pressure - center) <= half;
            if (inside)
            {
                inBand += dt;
                progress += dt * 0.075f * toolDamage / (0.6f + difficulty);
            }

            band.style.bottom = Length.Percent((center - half) * 100f);
            band.style.height = Length.Percent(half * 2f * 100f);
            needle.style.bottom = Length.Percent(pressure * 100f);
            needle.style.backgroundColor = inside ? Color.white : new Color(1f, 0.4f, 0.4f);
            progFill.style.height = Length.Percent(Mathf.Clamp01(progress) * 100f);
            status.text = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "% drilled   " + Mathf.RoundToInt(inBand / Mathf.Max(0.01f, t) * 100f) + "% efficiency";

            if (progress >= 1f)
            {
                float eff = inBand / Mathf.Max(0.01f, t);
                Finish(new MinigameResult { quality = Mathf.Clamp01(0.3f + 0.7f * eff), perfects = Mathf.RoundToInt(inBand), total = Mathf.Max(1, Mathf.RoundToInt(t)) });
            }
        }
    }

    /// <summary>Asteroid-field dodge flown during warp. Quality (fewer hits) refunds part of the fuel.</summary>
    public class TravelGame : Minigame
    {
        const int Spawns = 18;
        class Obj { public VisualElement el; public int lane; public float y; public bool orb; }
        readonly List<Obj> objs = new List<Obj>();
        VisualElement ship;
        int lane = 1, spawned, resolved, hits, orbs;
        float spawnT = 0.6f, hitFlash, shipX = -1f;

        protected override string Title { get { return "ASTEROID FIELD"; } }
        protected override string Hint { get { return "Tap a lane to steer. Dodge rocks, grab orbs. Clean flying refunds fuel"; } }

        const float Size = 110f, ShipY = 120f;

        protected override void Build()
        {
            for (int i = 1; i < 3; i++)
            {
                var line = UiKit.Div();
                line.pickingMode = PickingMode.Ignore;
                line.style.position = Position.Absolute;
                line.style.left = Length.Percent(i * 100f / 3f); line.style.width = 2;
                line.style.top = 0; line.style.bottom = 0;
                line.style.backgroundColor = new Color(color.r, color.g, color.b, 0.3f);
                arena.Add(line);
            }
            ship = UiKit.Div();
            ship.pickingMode = PickingMode.Ignore;
            ship.style.position = Position.Absolute;
            ship.style.width = Size; ship.style.height = Size;
            UiKit.Radius(ship, 30f);
            ship.style.backgroundColor = new Color(color.r, color.g, color.b, 0.35f);
            UiKit.Border(ship, Color.white, 4f);
            UiKit.Halo(ship, color, 60f, 0.8f);
            arena.Add(ship);
            arena.RegisterCallback<PointerDownEvent>(e =>
            {
                float x = arena.WorldToLocal(e.position).x;
                lane = Mathf.Clamp((int)(x / Mathf.Max(1f, ArenaW) * 3f), 0, 2);
            });
            Status();
        }

        float LaneX(int l) { return l * ArenaW / 3f + (ArenaW / 3f - Size) * 0.5f; }
        void Status() { status.text = "Hull hits " + hits + "   Orbs " + orbs; }

        protected override void Tick(float dt)
        {
            float speed = (ArenaH * 0.5f) * (1f + difficulty * 0.8f);
            spawnT -= dt;
            if (spawned < Spawns && spawnT <= 0f)
            {
                bool orb = UnityEngine.Random.value < 0.22f;
                var o = new Obj { lane = UnityEngine.Random.Range(0, 3), y = ArenaH, orb = orb };
                o.el = UiKit.Div();
                o.el.pickingMode = PickingMode.Ignore;
                Color c = orb ? UiKit.Lime : UiKit.Red;
                o.el.style.position = Position.Absolute;
                o.el.style.width = Size * (orb ? 0.7f : 1f); o.el.style.height = Size * (orb ? 0.7f : 1f);
                UiKit.Radius(o.el, orb ? 40f : 26f);
                o.el.style.backgroundColor = new Color(c.r, c.g, c.b, orb ? 0.7f : 0.45f);
                UiKit.Border(o.el, c, 4f);
                UiKit.Halo(o.el, c, 40f, 0.7f);
                arena.Add(o.el);
                objs.Add(o);
                spawned++;
                spawnT = Mathf.Lerp(0.85f, 0.5f, difficulty);
            }

            if (shipX < 0f) shipX = LaneX(lane);
            shipX = Mathf.Lerp(shipX, LaneX(lane), Mathf.Clamp01(dt * 18f));
            ship.style.left = shipX;
            ship.style.bottom = ShipY - Size * 0.5f;
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 3f);
            ship.style.backgroundColor = Color.Lerp(new Color(color.r, color.g, color.b, 0.35f), new Color(1, 0.2f, 0.2f, 0.8f), hitFlash);

            for (int i = objs.Count - 1; i >= 0; i--)
            {
                var o = objs[i];
                o.y -= speed * dt;
                float sz = o.orb ? Size * 0.7f : Size;
                o.el.style.left = o.lane * ArenaW / 3f + (ArenaW / 3f - sz) * 0.5f;
                o.el.style.bottom = o.y;
                float dy = Mathf.Abs(o.y - ShipY);
                if (o.lane == lane && dy < Size * 0.8f)
                {
                    if (o.orb) { orbs++; Sfx.Good(); } else { hits++; hitFlash = 1f; Sfx.Bad(); }
                    Remove(i, o);
                }
                else if (o.y < -Size) Remove(i, o);
            }
            Status();
            if (spawned >= Spawns && resolved >= Spawns)
            {
                float q = Mathf.Clamp01(1f - hits * 0.17f + orbs * 0.03f);
                Finish(new MinigameResult { quality = q, perfects = orbs, total = Spawns });
            }
        }

        void Remove(int i, Obj o)
        {
            objs.RemoveAt(i);
            o.el.RemoveFromHierarchy();
            resolved++;
        }
    }
}
