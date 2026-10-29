using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public partial class UIRoot
    {
        const float WorldZoomSensitivity = 1.7f, MapOverzoomThreshold = 0.2f;
        float worldPinchDistance, worldOverzoom, lastWorldWheelAt;
        int lastWorldWheelFrame = -1;
        bool worldPinchAccepted, mapGestureHeld;
        Coroutine mapZoomTransition;

        bool CanZoomWorld { get { return !mapOpen && !busy && !introActive && !defense.Active &&
            modal == null && buildDef == null && shortageOverlay == null; } }

        void UpdateWorldZoomInput()
        {
            if (!CanZoomWorld)
            {
                worldPinchDistance = 0f; worldOverzoom = 0f; worldPinchAccepted = false;
                return;
            }
            if (Input.touchCount == 2)
            {
                Vector2 a = Input.GetTouch(0).position, b = Input.GetTouch(1).position;
                float distance = Vector2.Distance(a, b);
                if (worldPinchDistance <= 0f)
                    worldPinchAccepted = !IsOverUi(a) && !IsOverUi(b);
                else if (worldPinchAccepted && distance > 1f)
                    ApplyWorldZoom(Mathf.Log(distance / worldPinchDistance) * WorldZoomSensitivity);
                worldPinchDistance = Mathf.Max(1f, distance);
            }
            else
            {
                worldPinchDistance = 0f; worldPinchAccepted = false;
                PollWorldWheel(Input.mouseScrollDelta.y, Input.mousePosition);
                if (Time.unscaledTime - lastWorldWheelAt > 0.35f) worldOverzoom = 0f;
            }
        }

        void OnWorldWheel(WheelEvent evt)
        {
            if (!CanZoomWorld || evt.delta.y == 0f) return;
            HandleWorldWheel(-evt.delta.y / 3f);
            evt.StopPropagation();
        }

        void PollWorldWheel(float delta, Vector2 position)
        {
            if (delta != 0f && !IsOverUi(position)) HandleWorldWheel(delta);
        }

        void HandleWorldWheel(float detents)
        {
            if (!CanZoomWorld || lastWorldWheelFrame == Time.frameCount) return;
            lastWorldWheelFrame = Time.frameCount;
            if (Time.unscaledTime - lastWorldWheelAt > 0.35f) worldOverzoom = 0f;
            lastWorldWheelAt = Time.unscaledTime;
            ApplyWorldZoom(detents * 0.12f);
        }

        void ApplyWorldZoom(float delta)
        {
            if (!CanZoomWorld || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (popup != null) ClosePopup();
            float requested = planetView.Zoom01 + delta;
            planetView.Zoom01 = requested;
            worldOverzoom = delta < 0f ? worldOverzoom + Mathf.Max(0f, -requested) : 0f;
            if (worldOverzoom < MapOverzoomThreshold) return;
            worldOverzoom = 0f;
            OpenMap(true);
            if (!mapOpen) return;
            mapGestureHeld = Input.touchCount > 0;
            mapZoomTransition = StartCoroutine(AnimateMapZoom(true));
        }

        void ReturnFromMap()
        {
            if (!mapOpen || mapZoomTransition != null) return;
            mapGestureHeld = Input.touchCount > 0;
            mapZoomTransition = StartCoroutine(AnimateMapZoom(false));
        }

        IEnumerator AnimateMapZoom(bool entering)
        {
            var view = mapView;
            planetView.Zoom01 = 0f;
            float startBlend = entering ? 0f : 1f;
            float endBlend = 1f - startBlend;
            view.style.opacity = entering ? 0f : 1f;
            view.SetEnabled(false);
            yield return null;
            float elapsed = 0f;
            while (elapsed < 0.6f && view == mapView)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.6f);
                float eased = t * t * (3f - 2f * t);
                float blend = Mathf.Lerp(startBlend, endBlend, eased);
                planetView.MapZoomBlend = blend;
                view.style.opacity = blend;
                view.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.45f, 1f, blend));
                yield return null;
            }
            mapZoomTransition = null;
            if (view != mapView) yield break;
            if (entering)
            {
                view.style.opacity = 1f;
                view.style.scale = new Scale(Vector3.one);
                view.SetEnabled(true);
                planetView.MapZoomBlend = 1f;
            }
            else CloseMap();
        }
    }
}
