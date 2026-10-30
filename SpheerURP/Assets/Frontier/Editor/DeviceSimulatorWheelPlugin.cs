#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.EditorTools
{
    public sealed class DeviceSimulatorWheelPlugin : DeviceSimulatorPlugin
    {
        VisualElement editorRoot;
        public override string title { get { return "Frontier wheel input"; } }

        public override VisualElement OnCreateUI()
        {
            var hint = new Label("Scroll over the simulated screen to zoom. Menus keep their own scrolling.");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                Disconnect();
                editorRoot = evt.destinationPanel.visualTree;
                editorRoot.RegisterCallback<WheelEvent>(ForwardWheel, TrickleDown.TrickleDown);
            });
            hint.RegisterCallback<DetachFromPanelEvent>(evt => Disconnect());
            return hint;
        }

        public override void OnDestroy() { Disconnect(); }

        void Disconnect()
        {
            if (editorRoot != null)
                editorRoot.UnregisterCallback<WheelEvent>(ForwardWheel, TrickleDown.TrickleDown);
            editorRoot = null;
        }

        void ForwardWheel(WheelEvent evt)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || evt.delta == Vector3.zero) return;
            var runtime = FrontierRuntime.Current;
            if (runtime == null || runtime.Ui == null) return;
            var view = evt.target as VisualElement;
            while (view != null && view.GetType().FullName != "UnityEditor.DeviceSimulation.DeviceView")
                view = view.parent;
            if (view == null) return;

            // Simulator has no public wheel/screen conversion API; isolate its preview contract here.
            var type = view.GetType();
            var transform = ReadProperty<Matrix4x4>(type, view, "ViewToScreen");
            var insets = ReadProperty<Vector4>(type, view, "ScreenInsets");
            var orientation = ReadProperty<ScreenOrientation>(type, view, "ScreenOrientation");
            var width = ReadField<int>(type, view, "m_ScreenWidth");
            var height = ReadField<int>(type, view, "m_ScreenHeight");
            Vector2 portrait = transform.MultiplyPoint(view.WorldToLocal(evt.mousePosition));
            var rendered = new Rect(insets.x, insets.y, width - insets.x - insets.z, height - insets.y - insets.w);
            if (!rendered.Contains(portrait)) return;
            Vector2 normalized = Rect.PointToNormalized(rendered, portrait);
            Vector2 screen = ToRuntimeScreen(normalized, orientation, new Vector2(Screen.width, Screen.height));
            if (runtime.Ui.EditorSendWheel(screen, evt.delta)) evt.StopPropagation();
        }

        public static Vector2 ToRuntimeScreen(Vector2 portrait, ScreenOrientation orientation, Vector2 size)
        {
            Vector2 normalized;
            switch (orientation)
            {
                case ScreenOrientation.PortraitUpsideDown: normalized = new Vector2(1f - portrait.x, portrait.y); break;
                case ScreenOrientation.LandscapeLeft: normalized = new Vector2(portrait.y, portrait.x); break;
                case ScreenOrientation.LandscapeRight: normalized = new Vector2(1f - portrait.y, 1f - portrait.x); break;
                default: normalized = new Vector2(portrait.x, 1f - portrait.y); break;
            }
            return Vector2.Scale(normalized, size);
        }

        static T ReadProperty<T>(Type type, object target, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) throw new InvalidOperationException("Frontier simulator wheel: missing preview property " + name);
            return (T)property.GetValue(target);
        }

        static T ReadField<T>(Type type, object target, string name)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Frontier simulator wheel: missing preview field " + name);
            return (T)field.GetValue(target);
        }
    }
}
#endif
