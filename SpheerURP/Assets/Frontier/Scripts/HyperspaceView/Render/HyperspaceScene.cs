using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Frontier.Hyperspace.View
{
    /// <summary>One pooled entity mesh plus a bounded shared player ship rig; no scene objects per enemy.</summary>
    public sealed class HyperspaceScene : IDisposable
    {
        public RenderTexture Texture { get; private set; }
        public int TriangleCount { get { return indices.Count / 3 + playerRig.TriangleCount; } }
        readonly Camera camera;
        readonly Mesh mesh;
        readonly Material material;
        readonly ShipRig playerRig;
        readonly UniversalRenderPipeline.SingleCameraRequest renderRequest;
        readonly List<Vector3> vertices = new List<Vector3>(60000);
        readonly List<Color> colors = new List<Color>(60000);
        readonly List<int> indices = new List<int>(60000);
        readonly Burst[] bursts = new Burst[64];
        int nextBurst;
        float kick;
        struct Burst { public Vector3 p; public Color color; public float start; public float size; }
        static readonly Color Cyan = new Color(0.3f, 0.95f, 1f);
        static readonly Color Orange = new Color(1f, 0.55f, 0.16f);
        readonly int ascensionRank;
        public Color TrailColor { get; private set; }

        public HyperspaceScene(Transform parent, int ascensionRank = 0)
        {
            this.ascensionRank = Math.Max(0, ascensionRank);
            TrailColor = this.ascensionRank > 0 ? AscensionStyle.Accent(this.ascensionRank) : Cyan;
            var go = new GameObject("Warp Camera"); go.transform.SetParent(parent, false);
            camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 1.8f, -10);
            camera.transform.LookAt(new Vector3(0, 0.2f, 30));
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 180;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.02f, 0.055f);
            camera.cullingMask = 1 << 31;
            camera.allowHDR = false; camera.allowMSAA = false;
            camera.depth = -10;
            PresentationQuality.Budget.FitSurface(ScreenMetrics.Width, ScreenMetrics.Height, SystemInfo.maxTextureSize,
                out int width, out int height);
            Texture = new RenderTexture(width, height, 24) { name = "Hyperspace View", antiAliasing = 1 };
            Texture.Create(); camera.targetTexture = Texture;
            renderRequest = new UniversalRenderPipeline.SingleCameraRequest { destination = Texture };
            mesh = new Mesh { name = "Hyperspace pooled geometry", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            material = new Material(Resources.Load<Shader>("Frontier/Hyperspace/WarpVertex")) { name = "Warp vertex colour" };
            playerRig = ShipRig.Create(parent, 31, cameraOnly: true);
            playerRig.transform.localScale = Vector3.one * 0.42f;
        }

        public void Present(HyperspaceRun run, float alpha)
        {
            vertices.Clear(); colors.Clear(); indices.Clear();
            float dt = alpha * HyperspaceTuning.Dt;
            float scroll = (float)run.Scroll;
            camera.fieldOfView = Mathf.Clamp(60 + 6 * (run.Speed - 25) / 15, 58, 70);
            kick = Mathf.MoveTowards(kick, 0, Time.unscaledDeltaTime * 2);
            float distance = Mathf.Max(10, 3.4f / (Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * ScreenMetrics.Aspect));
            camera.transform.position = new Vector3(Mathf.Sin(Time.unscaledTime * 73) * kick * 0.1f, 1.8f, -distance);
            camera.transform.LookAt(new Vector3(0, 0.2f, 30));
            // Stable star positions belong to presentation, never consume the simulation RNG.
            for (int i = 0; i < PresentationQuality.Budget.TravelStars; i++)
            {
                float angle = i * 2.399963f, radius = 5 + (i % 13) * 0.8f;
                float z = Mathf.Repeat(i * 9.73f - scroll, 160);
                var p = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
                Line(p, p + Vector3.forward * (0.7f + run.Speed * 0.06f), 0.018f, i % 5 == 0 ? Orange * 0.6f : Cyan * 0.35f);
            }
            for (int i = 0; i < 9; i++)
            {
                float z = Mathf.Repeat(i * 20 - scroll, 180);
                Ring(new Vector3(0, 0, z), 6.3f, 0.025f, new Color(0.055f, 0.14f, 0.23f), 24);
            }
            // Faint frame at the actual player-plane bounds.
            Rect(-2.7f, -3.4f, 2.7f, 3.4f, 0.2f, 0.02f, Cyan * 0.25f);
            for (int i = 0; i < run.EnemyHigh; i++)
            {
                var e = run.Enemies[i];
                if (!e.active || (e.flags & EnemyFlags.Hidden) != 0) continue;
                var p = new Vector3(e.x + e.vx * dt, e.y + e.vy * dt, e.z - (run.Speed + e.vzOwn) * dt);
                Color c = EnemyColor(e.type);
                if (e.flash < 0.065f) c = Color.white;
                if (e.type == EntityType.LaserGate) { Gate(e, p.z); continue; }
                if (e.type == EntityType.Guardian)
                {
                    Gem(p, new Vector3(5.5f, 2.5f, 1.8f), c);
                    var weak = new Vector3(e.aimX, e.aimY, e.z - 2);
                    Ring(weak, 0.7f, 0.1f, Color.white);
                    if (run.BossVolleyEta > 0)
                    {
                        for (int shot = 0; shot < run.BossVolleyCount; shot++)
                        {
                            float offset = (shot - (run.BossVolleyCount - 1) * 0.5f) * run.Tuning.turretFanSpread;
                            var aim = new Vector3(run.BossVolleyAimX + offset, run.BossVolleyAimY, 0);
                            Ring(aim, 0.5f, 0.04f, Orange);
                            Line(p, aim, 0.025f, Orange * 0.7f);
                        }
                    }
                }
                else if (e.type == EntityType.Drone || e.type == EntityType.Zigzag || e.type == EntityType.Seeker)
                    Ship(p, e.radius, e.vx * -0.08f, c, false);
                else if (e.type == EntityType.Swooper)
                {
                    Ring(p, e.radius, e.radius * 0.2f, c, 12);
                    Gem(p, Vector3.one * e.radius * 0.5f, Color.white);
                }
                else
                {
                    Gem(p, Vector3.one * e.radius, c);
                    if (e.type == EntityType.TurretPod) Line(p, p + Vector3.back * 1.1f, 0.14f, Orange);
                }
                if (e.shield > 0) Ring(p + Vector3.back * 0.2f, e.radius * 1.3f, 0.04f, Cyan);
                if (e.type == EntityType.Mine)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        float a = j * Mathf.PI / 3;
                        var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                        Line(p + d * e.radius, p + d * e.radius * 1.7f, 0.06f, c);
                    }
                    if ((e.flags & EnemyFlags.Armed) != 0) Ring(p, 1.25f, 0.035f, c);
                }
                if ((e.flags & EnemyFlags.Telegraphing) != 0)
                {
                    int count = e.type == EntityType.TurretPod && (TurretMode)e.variant == TurretMode.Fan
                        ? run.Tuning.FanCountFor(run.Route.tier) : 1;
                    for (int shot = 0; shot < count; shot++)
                    {
                        float offset = (shot - (count - 1) * 0.5f) * run.Tuning.turretFanSpread;
                        var aim = new Vector3(e.aimX + offset, e.aimY, 0);
                        Ring(aim, 0.5f + 0.15f * Mathf.Sin(e.age * 16), 0.045f, Orange);
                        Line(p, aim, 0.02f, Orange * 0.6f);
                        Line(aim + Vector3.left * 0.7f, aim + Vector3.right * 0.7f, 0.03f, Orange);
                    }
                }
                if (e.hp < e.maxHp && e.maxHp > 0)
                    Line(p + new Vector3(-e.radius, e.radius + 0.2f, -0.3f),
                        p + new Vector3(-e.radius + 2 * e.radius * e.hp / e.maxHp, e.radius + 0.2f, -0.3f), 0.06f, c);
            }
            for (int i = 0; i < run.BulletHigh; i++)
            {
                var b = run.Bullets[i]; if (!b.active) continue;
                var p = new Vector3(b.x + b.vx * dt, b.y + b.vy * dt, b.z + b.vz * dt);
                Line(p, p + Vector3.back * 1.1f, 0.07f, Cyan);
            }
            for (int i = 0; i < run.BoltHigh; i++)
            {
                var b = run.Bolts[i]; if (!b.active) continue;
                var p = new Vector3(b.x + b.vx * dt, b.y + b.vy * dt, b.z + b.vz * dt);
                Gem(p, new Vector3(b.radius, b.radius, 0.4f), Orange);
                Line(p, p + Vector3.forward * 1.4f, 0.06f, Orange * 0.7f);
            }
            for (int i = 0; i < run.PickupHigh; i++)
            {
                var p = run.Pickups[i]; if (!p.active) continue;
                var pos = new Vector3(p.x, p.y, p.z - run.Speed * dt);
                Color c = PickupColor(p.type);
                Gem(pos, new Vector3(0.25f, 0.4f, 0.25f), c);
                Ring(pos, 0.48f, 0.045f, c, p.type == PickupType.LootCrate ? 4 : 8);
                if (p.magnetized) Line(pos, new Vector3(run.Player.x, run.Player.y, 0), 0.015f, c * 0.5f);
            }
            var player = run.Player;
            var ship = new Vector3(player.x + player.vx * dt, player.y + player.vy * dt, 0);
            playerRig.transform.position = ship;
            playerRig.gameObject.SetActive(player.invulnerable <= 0 || (run.StepIndex / 5) % 2 == 0);
            playerRig.SetFlight(Mathf.Clamp01(run.Speed / 50f), 0f, -player.vx * 4f);
            Line(ship + Vector3.back * 0.3f, ship + Vector3.back * (1.2f + run.Speed * 0.025f), 0.16f,
                player.overdrive > 0 ? Color.magenta : TrailColor);
            if (ascensionRank > 0)
            {
                float spread = 0.12f + (ascensionRank % 3) * 0.025f;
                for (int side = -1; side <= 1; side += 2)
                    Line(ship + Vector3.right * side * spread + Vector3.back * 0.4f,
                        ship + Vector3.right * side * spread + Vector3.back * (1.6f + run.Speed * 0.025f),
                        0.025f, TrailColor * 0.6f);
            }
            if (player.shield > 0) Ring(ship, 0.52f, 0.025f, Cyan);
            if (player.overdrive > 0) Ring(ship, 0.65f, 0.045f, Color.magenta);
            for (int i = 0; i < bursts.Length; i++)
            {
                var b = bursts[i]; float age = Time.unscaledTime - b.start;
                if (b.size <= 0 || age > 0.5f) continue;
                Color c = b.color * (1 - age * 2);
                Ring(b.p, 0.1f + age * b.size, 0.04f, c);
                for (int j = 0; j < 6; j++)
                {
                    var d = new Vector3(Mathf.Cos(j * 1.047f), Mathf.Sin(j * 1.047f), 0);
                    Line(b.p + d * age * b.size, b.p + d * (age * b.size + 0.25f), 0.04f, c);
                }
            }
            mesh.Clear(false);
            mesh.SetVertices(vertices); mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = new Bounds(new Vector3(0, 0, 70), new Vector3(100, 100, 240));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 31, camera, 0, null, ShadowCastingMode.Off, false);
            playerRig.DrawForCamera(camera);
            RenderPipeline.SubmitRenderRequest(camera, renderRequest);
        }

        public void React(HEvent e)
        {
            if (e.type == EventType.PlayerHit) kick = 1;
            if (e.type != EventType.EnemyKilled && e.type != EventType.EnemyHit && e.type != EventType.BombUsed &&
                e.type != EventType.ShieldBreak && e.type != EventType.PickupCollected && e.type != EventType.BossDefeated) return;
            bursts[nextBurst] = new Burst
            {
                p = new Vector3(e.x, e.y, e.z), start = Time.unscaledTime,
                size = e.type == EventType.BombUsed ? 20 : 4,
                color = e.type == EventType.PlayerHit ? Color.red : EnemyColor((EntityType)e.a)
            };
            nextBurst = (nextBurst + 1) % bursts.Length;
        }

        void Gate(Enemy e, float z)
        {
            Hazard h = Hazards.Gate(e);
            Color c = Orange;
            if (h.shape == HazardShape.Slit)
            {
                Quad(-3.4f, -4.4f, h.a - h.b, 4.4f, z, c * 0.25f);
                Quad(h.a + h.b, -4.4f, 3.4f, 4.4f, z, c * 0.25f);
                Line(new Vector3(h.a - h.b, -4.4f, z), new Vector3(h.a - h.b, 4.4f, z), 0.09f, c);
                Line(new Vector3(h.a + h.b, -4.4f, z), new Vector3(h.a + h.b, 4.4f, z), 0.09f, c);
                Rect(h.a - h.b, -3.4f, h.a + h.b, 3.4f, 0.05f, 0.025f, Cyan * 0.5f);
            }
            else if (h.shape == HazardShape.Slot)
            {
                Quad(-3.4f, -4.4f, 3.4f, h.a - h.b, z, c * 0.25f);
                Quad(-3.4f, h.a + h.b, 3.4f, 4.4f, z, c * 0.25f);
                Line(new Vector3(-3.4f, h.a - h.b, z), new Vector3(3.4f, h.a - h.b, z), 0.09f, c);
                Line(new Vector3(-3.4f, h.a + h.b, z), new Vector3(3.4f, h.a + h.b, z), 0.09f, c);
                Rect(-2.7f, h.a - h.b, 2.7f, h.a + h.b, 0.05f, 0.025f, Cyan * 0.5f);
            }
            else if (h.shape == HazardShape.Ring)
            {
                var p = new Vector3(h.a, h.b, z);
                Ring(p, h.c, 0.09f, c);
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16;
                    var d0 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    var d1 = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0);
                    Tri(p + d0 * h.c, p + d0 * 12, p + d1 * 12, c * 0.2f);
                    Tri(p + d0 * h.c, p + d1 * 12, p + d1 * h.c, c * 0.2f);
                }
                Ring(new Vector3(h.a, h.b, 0), h.c, 0.025f, Cyan * 0.5f);
            }
            else
            {
                var p = new Vector3(h.a, h.b, z);
                var d = new Vector3(Mathf.Cos(h.c), Mathf.Sin(h.c), 0) * h.d;
                Line(p - d, p + d, h.e * 2, c);
            }
        }

        public static Color EnemyColor(EntityType type)
        {
            switch (type)
            {
                case EntityType.Zigzag: return new Color(0.4f, 1, 0.4f);
                case EntityType.Swooper: case EntityType.Guardian: return new Color(1, 0.2f, 0.7f);
                case EntityType.TurretPod: case EntityType.Seeker: return Orange;
                case EntityType.Mine: return new Color(1, 0.22f, 0.25f);
                case EntityType.Asteroid: return new Color(0.5f, 0.58f, 0.7f);
                case EntityType.Elite: return new Color(0.65f, 0.35f, 1);
                default: return Cyan;
            }
        }
        static Color PickupColor(PickupType type)
        {
            switch (type)
            {
                case PickupType.Fuel: return new Color(0.4f, 1, 0.4f);
                case PickupType.Shield: return Cyan;
                case PickupType.Overdrive: return Color.magenta;
                default: return Orange;
            }
        }
        void Ship(Vector3 p, float r, float bank, Color c, bool player)
        {
            float direction = player ? 1 : -1;
            var nose = p + new Vector3(0, 0.1f, r * 2 * direction);
            var left = p + new Vector3(-r * 1.5f, bank, -r * direction);
            var right = p + new Vector3(r * 1.5f, -bank, -r * direction);
            var peak = p + Vector3.up * r * 0.65f;
            Tri(nose, left, peak, c); Tri(nose, peak, right, c * 0.6f);
            Tri(left, right, peak, Cyan * 0.5f);
            Line(left, nose, 0.045f, Cyan); Line(right, nose, 0.045f, Cyan);
        }
        void Gem(Vector3 p, Vector3 size, Color c)
        {
            var front = p + Vector3.back * size.z; var back = p + Vector3.forward * size.z;
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2, b = (i + 1) * Mathf.PI / 2;
                var v0 = p + new Vector3(Mathf.Cos(a) * size.x, Mathf.Sin(a) * size.y, 0);
                var v1 = p + new Vector3(Mathf.Cos(b) * size.x, Mathf.Sin(b) * size.y, 0);
                Tri(front, v0, v1, c * (0.45f + i * 0.15f));
                Tri(back, v1, v0, c * 0.4f); Line(v0, v1, 0.035f, c);
            }
        }
        void Ring(Vector3 p, float radius, float width, Color color, int segments = 24)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Line(p + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius,
                    p + new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0) * radius, width, color);
            }
        }
        void Rect(float x0, float y0, float x1, float y1, float z, float width, Color c)
        {
            Line(new Vector3(x0, y0, z), new Vector3(x1, y0, z), width, c);
            Line(new Vector3(x1, y0, z), new Vector3(x1, y1, z), width, c);
            Line(new Vector3(x1, y1, z), new Vector3(x0, y1, z), width, c);
            Line(new Vector3(x0, y1, z), new Vector3(x0, y0, z), width, c);
        }
        void Quad(float x0, float y0, float x1, float y1, float z, Color c)
        {
            Tri(new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), c);
            Tri(new Vector3(x0, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z), c);
        }
        void Line(Vector3 a, Vector3 b, float width, Color c)
        {
            var n = Vector3.Cross(b - a, (a + b) * 0.5f - camera.transform.position).normalized * width * 0.5f;
            Tri(a - n, a + n, b + n, c); Tri(a - n, b + n, b - n, c);
        }
        void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }
        public void Dispose()
        {
            if (playerRig != null) UnityEngine.Object.Destroy(playerRig.gameObject);
            camera.targetTexture = null; camera.enabled = false;
            Texture.Release(); UnityEngine.Object.Destroy(Texture);
            UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(material);
        }
    }
}
