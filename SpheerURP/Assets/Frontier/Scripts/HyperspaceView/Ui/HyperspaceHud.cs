using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.Hyperspace.View
{
    public sealed class HyperspaceHud : IDisposable
    {
        public VisualElement Surface { get; private set; }
        public Button Bomb { get; private set; }
        readonly VisualElement root, dialog, panel, hullFill, progressFill, bombFill;
        readonly Label status, score, boss, notice, tip, power, perf;
        readonly Action resume, finish, revive, close;
        Button reviveButton;
        bool detaching;
        float noticeUntil;

        public HyperspaceHud(VisualElement host, Vector4 insets, RenderTexture texture,
            Action pause, Action resume, Action finish, Action revive, Action close)
        {
            this.resume = resume; this.finish = finish; this.revive = revive; this.close = close;
            root = new VisualElement { name = "hyperspace-run" };
            Fill(root); root.style.backgroundColor = new Color(0.015f, 0.025f, 0.065f);
            host.Add(root);
            root.RegisterCallback<DetachFromPanelEvent>(_ => { detaching = true; close(); });
            var image = new Image { image = texture, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            Fill(image); root.Add(image);
            Surface = new VisualElement { name = "hyperspace-input" }; Fill(Surface); root.Add(Surface);
            var safe = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(safe); safe.style.left = insets.x + 14; safe.style.top = insets.y + 12;
            safe.style.right = insets.z + 14; safe.style.bottom = insets.w + 12; root.Add(safe);
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.backgroundColor = new Color(0.015f, 0.035f, 0.08f, 0.92f);
            top.style.paddingLeft = top.style.paddingRight = 12;
            top.style.paddingTop = top.style.paddingBottom = 8;
            safe.Add(top);
            var row = new VisualElement { pickingMode = PickingMode.Ignore }; row.style.flexDirection = FlexDirection.Row;
            var title = Text("HYPERSPACE RUN", 20, UiKit.Cyan); title.style.flexGrow = 1; row.Add(title);
            var pauseButton = UiKit.Btn("PAUSE", UiKit.Cyan, pause);
            pauseButton.name = "hyperspace-pause"; pauseButton.style.minWidth = 170; row.Add(pauseButton); top.Add(row);
            hullFill = Bar(top, UiKit.Cyan);
            status = Text("", 16, Color.white); top.Add(status);
            progressFill = Bar(top, UiKit.Lime);
            score = Text("", 16, Color.white); top.Add(score);
            power = Text("", 14, UiKit.Cyan); top.Add(power);
            perf = Text("", 12, UiKit.Lime); top.Add(perf);
            boss = Text("", 20, UiKit.Magenta); boss.style.marginTop = 8; safe.Add(boss);
            notice = Text("", 20, UiKit.Amber); notice.style.unityTextAlign = TextAnchor.MiddleCenter;
            notice.style.marginTop = 16; safe.Add(notice);
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.style.position = Position.Absolute; bottom.style.bottom = 0; bottom.style.left = bottom.style.right = 0;
            tip = Text("", 16, Color.white); tip.style.whiteSpace = WhiteSpace.Normal;
            tip.style.unityTextAlign = TextAnchor.MiddleCenter; bottom.Add(tip);
            Bomb = UiKit.Btn("BOMB", UiKit.Amber, null, true);
            Bomb.name = "hyperspace-bomb"; Bomb.style.alignSelf = Align.FlexEnd; Bomb.style.minWidth = 140;
            bottom.Add(Bomb); safe.Add(bottom);
            bombFill = Bar(bottom, UiKit.Amber);
            dialog = new VisualElement { name = "hyperspace-dialog" }; Fill(dialog);
            dialog.style.backgroundColor = new Color(0.005f, 0.012f, 0.035f, 0.88f);
            dialog.style.justifyContent = Justify.Center; dialog.style.alignItems = Align.Center;
            dialog.style.paddingLeft = dialog.style.paddingRight = 24; root.Add(dialog);
            panel = new VisualElement(); panel.style.width = Length.Percent(100); panel.style.maxWidth = 840;
            panel.style.maxHeight = Length.Percent(85);
            dialog.Add(panel); HideDialog();
        }

        static void Fill(VisualElement v)
        {
            v.style.position = Position.Absolute; v.style.left = v.style.right = v.style.top = v.style.bottom = 0;
        }
        static Label Text(string text, int size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size * 2; l.style.color = color; return l;
        }
        static VisualElement Bar(VisualElement parent, Color color)
        {
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.height = 5; track.style.marginTop = track.style.marginBottom = 6;
            track.style.backgroundColor = new Color(0.15f, 0.22f, 0.3f);
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.height = Length.Percent(100); fill.style.backgroundColor = color;
            track.Add(fill); parent.Add(track); return fill;
        }
        public void Refresh(HyperspaceRun run)
        {
            var p = run.Player;
            hullFill.style.width = Length.Percent(100 * Mathf.Clamp01(p.hull / p.maxHull));
            progressFill.style.width = Length.Percent(100 * run.Progress);
            status.text = $"HULL {Mathf.CeilToInt(p.hull)}   SHIELD {p.shield}/{p.maxShield}";
            score.text = $"{run.Score:N0} PTS   {run.Kills} KILLS   x{p.combo}   {run.Progress:P0}";
            boss.text = run.BossActive ? $"GUARDIAN  /  PHASE {run.BossPhase}  /  {run.BossHpFraction:P0}" : "";
            Bomb.text = $"BOMB  {p.bombs}/{p.maxBombs}";
            bombFill.style.width = Length.Percent(100 * Mathf.Clamp01((float)p.bombKillProgress / run.Tuning.bombKillsPerCharge));
            power.text = p.overdrive > 0 ? $"OVERDRIVE  {p.overdrive:0.0}s"
                : p.shield < p.maxShield ? $"SHIELD RECHARGING  {p.shieldRegenTimer:0.0}s" : "";
            Bomb.SetEnabled(run.State == RunState.Running && p.bombs > 0);
            if (Time.unscaledTime > noticeUntil) notice.text = "";
        }
        public void BuildRoute(HyperspaceRun run)
        {
            var track = progressFill.parent;
            for (int i = 0; i < run.Plan.encounters.Length; i++)
            {
                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.style.position = Position.Absolute;
                tick.style.left = Length.Percent(100f * run.Plan.encounters[i].anchorStep / Mathf.Max(1, run.EndStep));
                tick.style.width = 2; tick.style.height = 9; tick.style.top = -2;
                tick.style.backgroundColor = new Color(0.5f, 0.7f, 0.7f, 0.6f); track.Add(tick);
            }
        }
        public void Performance(HyperspaceRun run, int triangles)
        {
            perf.text = TravelPrefs.DebugOverlay
                ? $"{1f / Mathf.Max(0.001f, Time.unscaledDeltaTime):0} FPS  ·  {triangles:N0} TRI  ·  {run.EnemyCount} ENEMIES\n" +
                    $"BULLETS {run.ActiveBullets}  BOLTS {run.ActiveBolts}  DROPPED EVENTS {run.Events.Dropped}  VOICES {Sfx.BusyVoices}"
                : "";
        }
        public void ShowTip() { tip.text = "Drag anywhere to steer. Auto-fire is on.\nDodge the warning shapes. Tap BOMB to clear danger."; }
        public void React(HEvent e)
        {
            string text = null;
            switch (e.type)
            {
                case EventType.BossWarning: text = "GUARDIAN APPROACHING"; break;
                case EventType.BombCharged: text = "BOMB READY"; break;
                case EventType.OverdriveStart: text = "OVERDRIVE"; break;
                case EventType.ShieldBreak: text = "SHIELD BROKEN"; break;
                case EventType.WaveCleared: text = "WAVE CLEAR"; break;
            }
            if (text != null) { notice.text = text; noticeUntil = Time.unscaledTime + 2; }
            if (e.step > 600) tip.text = "";
        }
        void Dialog(string title, string body)
        {
            panel.Clear(); dialog.style.display = DisplayStyle.Flex;
            var heading = Text(title, 30, UiKit.Cyan); heading.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(heading);
            var copy = Text(body, 18, Color.white); copy.style.whiteSpace = WhiteSpace.Normal;
            copy.style.marginTop = 12; copy.style.marginBottom = 20; panel.Add(copy);
        }
        Button Button(string text, Color color, Action action)
        {
            var b = UiKit.Btn(text, color, action, true); b.style.marginTop = 10; panel.Add(b); return b;
        }
        public void ShowPause()
        {
            Dialog("RUN PAUSED", "Your route is frozen. Finishing early completes the trip without loot or a fuel refund.");
            Button("RESUME", UiKit.Cyan, resume);
            Button("FINISH TRIP", UiKit.Amber, finish);
        }
        public void ShowDowned(bool available, bool free)
        {
            Dialog("HULL LOST", available
                ? "Revive with half hull and 3 seconds of protection. Each revive reduces your grade."
                : "No revives remain. You can still finish the trip, without loot or a fuel refund.");
            reviveButton = available ? Button(free ? "REVIVE · FREE" : "REVIVE · VIEW OFFER", UiKit.Cyan, revive) : null;
            if (reviveButton != null) reviveButton.name = "hyperspace-revive";
            Button("FINISH TRIP", UiKit.Amber, finish).name = "hyperspace-finish";
        }
        public void SetOfferPending(bool pending)
        {
            reviveButton?.SetEnabled(!pending);
            if (reviveButton != null && pending) reviveButton.text = "WAITING FOR OFFER…";
        }
        public void ShowResult(RunResult result, TravelRunResult travel, int fuel)
        {
            string title = travel.completed ? "GRADE " + result.grade : "TRIP COMPLETE";
            Dialog(title, travel.completed
                ? $"{result.killCount} kills  ·  best combo x{result.maxCombo}\n{result.hitsTaken} hits  ·  {result.revivesUsed} revives\n{(result.cleanRun ? "Clean run!\n" : "")}+{fuel} fuel refunded  ·  +{travel.xp} XP"
                : "You reached your destination.\nNo loot or fuel refund for an unfinished run.");
            if (travel.loot != null) foreach (var drop in travel.loot) panel.Add(UiKit.ItemChip(drop.resource, drop.amount));
            Button("CONTINUE TO DESTINATION", UiKit.Cyan, close).name = "hyperspace-continue";
        }
        public void HideDialog() { dialog.style.display = DisplayStyle.None; }
        public void Dispose() { if (!detaching) root.RemoveFromHierarchy(); }
    }
}
