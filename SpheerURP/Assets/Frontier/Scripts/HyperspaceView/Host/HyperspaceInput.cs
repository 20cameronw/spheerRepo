using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.Hyperspace.View
{
    public sealed class HyperspaceInput : IDisposable
    {
        readonly VisualElement surface;
        readonly HyperspaceTuning tuning;
        Vector2 anchor, shipAnchor, target;
        PlayerState player;
        int pointer = -1;
        bool bomb;

        public HyperspaceInput(VisualElement surface, HyperspaceTuning tuning)
        {
            this.surface = surface; this.tuning = tuning;
            surface.RegisterCallback<PointerDownEvent>(Down);
            surface.RegisterCallback<PointerMoveEvent>(Move);
            surface.RegisterCallback<PointerUpEvent>(Up);
            surface.RegisterCallback<PointerCancelEvent>(CancelPointer);
            surface.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
        }

        void Down(PointerDownEvent e)
        {
            if (pointer >= 0 || e.button != 0) return;
            pointer = e.pointerId; anchor = e.position;
            shipAnchor = new Vector2(player.x, player.y);
            target = shipAnchor;
            surface.CapturePointer(pointer);
        }
        void Move(PointerMoveEvent e)
        {
            if (e.pointerId != pointer) return;
            // UI Toolkit positions are panel units, with y down (not physical pixels).
            float scale = 2f * tuning.areaHalfWidth / Mathf.Max(1f, surface.resolvedStyle.width * 0.8f);
            Vector2 delta = (Vector2)e.position - anchor;
            target = shipAnchor + new Vector2(delta.x, -delta.y) * (scale * TravelPrefs.DragSensitivity);
        }
        void Up(PointerUpEvent e) { if (e.pointerId == pointer) Release(); }
        void CancelPointer(PointerCancelEvent e) { if (e.pointerId == pointer) Cancel(); }
        void CaptureOut(PointerCaptureOutEvent e) { if (e.pointerId == pointer) pointer = -1; }
        public void PressBomb() { bomb = true; }
        public void Cancel()
        {
            Release(); bomb = false;
            target = new Vector2(player.x, player.y);
        }
        void Release()
        {
            int old = pointer; pointer = -1;
            if (old >= 0 && surface.HasPointerCapture(old)) surface.ReleasePointer(old);
        }
        public void ConsumeBomb() { bomb = false; }
        public PlayerInput Read(PlayerState state)
        {
            player = state;
            var result = new PlayerInput(target.x, target.y, bomb);
            return result;
        }
        public void Dispose()
        {
            Cancel();
            surface.UnregisterCallback<PointerDownEvent>(Down);
            surface.UnregisterCallback<PointerMoveEvent>(Move);
            surface.UnregisterCallback<PointerUpEvent>(Up);
            surface.UnregisterCallback<PointerCancelEvent>(CancelPointer);
            surface.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);
        }
    }
}
