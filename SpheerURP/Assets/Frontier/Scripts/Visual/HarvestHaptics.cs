using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Frontier
{
    public interface IHapticDevice : IDisposable
    {
        bool IsSupported { get; }
        void Pulse(float strength);
        void Stop();
    }

    public sealed class HapticPulseController : IDisposable
    {
        public const double MinimumIntervalSeconds = 0.12;
        readonly IHapticDevice device;
        double lastPulse = double.NegativeInfinity;
        bool enabled, active = true, disposed;

        public HapticPulseController(IHapticDevice device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public bool IsSupported { get { return !disposed && device.IsSupported; } }
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (disposed) throw new ObjectDisposedException(nameof(HapticPulseController));
                if (enabled == value) return;
                enabled = value;
                if (!value) device.Stop();
            }
        }
        public bool Active
        {
            get { return active; }
            set
            {
                if (disposed) throw new ObjectDisposedException(nameof(HapticPulseController));
                if (active == value) return;
                active = value;
                if (!value) device.Stop();
            }
        }

        public bool TryPulse(float strength, double unscaledSeconds)
        {
            if (float.IsNaN(strength) || float.IsInfinity(strength) || strength < 0f || strength > 1f)
                throw new ArgumentOutOfRangeException(nameof(strength));
            if (double.IsNaN(unscaledSeconds) || double.IsInfinity(unscaledSeconds) || unscaledSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            if (disposed || !enabled || !active || !device.IsSupported || strength == 0f) return false;
            if (unscaledSeconds - lastPulse < MinimumIntervalSeconds) return false;
            lastPulse = unscaledSeconds;
            device.Pulse(strength);
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            device.Stop();
            device.Dispose();
        }
    }

    public sealed class HarvestHaptics : MonoBehaviour
    {
        public const string PreferenceKey = "fr_haptics";
        static HarvestHaptics instance;
        HapticPulseController pulses;

        public static bool IsSupported { get { return instance != null && instance.pulses != null && instance.pulses.IsSupported; } }
        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(PreferenceKey, 0) == 1; }
            set
            {
                PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (instance != null && instance.pulses != null) instance.pulses.Enabled = value;
            }
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("Frontier: only one harvest haptics host may be active.", this);
                enabled = false;
                return;
            }
            instance = this;
            pulses = new HapticPulseController(CreateDevice()) { Enabled = Enabled };
        }

        void OnEnable()
        {
            if (pulses == null) return;
            pulses.Active = Application.isFocused;
            HarvestFeedback.HapticRequested += OnPulse;
        }

        void OnDisable()
        {
            HarvestFeedback.HapticRequested -= OnPulse;
            if (pulses != null) pulses.Active = false;
        }

        void OnApplicationFocus(bool focused) { if (pulses != null) pulses.Active = focused && isActiveAndEnabled; }
        void OnApplicationPause(bool paused) { if (pulses != null) pulses.Active = !paused && Application.isFocused && isActiveAndEnabled; }
        void OnPulse(HarvestCue cue, float strength) { pulses.TryPulse(strength, Time.realtimeSinceStartupAsDouble); }

        void OnDestroy()
        {
            HarvestFeedback.HapticRequested -= OnPulse;
            if (pulses != null) pulses.Dispose();
            if (instance == this) instance = null;
        }

        static IHapticDevice CreateDevice()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidHapticDevice();
#elif UNITY_IOS && !UNITY_EDITOR
            return new AppleHapticDevice();
#else
            return new UnsupportedHapticDevice();
#endif
        }

        sealed class UnsupportedHapticDevice : IHapticDevice
        {
            public bool IsSupported { get { return false; } }
            public void Pulse(float strength) { }
            public void Stop() { }
            public void Dispose() { }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        sealed class AndroidHapticDevice : IHapticDevice
        {
            AndroidJavaObject vibrator;
            AndroidJavaClass effect;
            bool supported, amplitude;
            public bool IsSupported { get { return supported; } }

            public AndroidHapticDevice()
            {
                try
                {
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                        supported = vibrator != null && vibrator.Call<bool>("hasVibrator");
                        if (supported && version.GetStatic<int>("SDK_INT") >= 26)
                        {
                            effect = new AndroidJavaClass("android.os.VibrationEffect");
                            amplitude = vibrator.Call<bool>("hasAmplitudeControl");
                        }
                    }
                }
                catch (AndroidJavaException exception) { Fail(exception); }
            }

            public void Pulse(float strength)
            {
                if (!supported) return;
                try
                {
                    long duration = (long)Mathf.Lerp(12f, 28f, strength);
                    if (effect != null)
                    {
                        int level = amplitude ? Mathf.Clamp(Mathf.RoundToInt(strength * 255f), 1, 255) : -1;
                        using (var pulse = effect.CallStatic<AndroidJavaObject>("createOneShot", duration, level))
                            vibrator.Call("vibrate", pulse);
                    }
                    else vibrator.Call("vibrate", duration);
                }
                catch (AndroidJavaException exception) { Fail(exception); }
            }

            public void Stop()
            {
                if (!supported) return;
                try { vibrator.Call("cancel"); }
                catch (AndroidJavaException exception) { Fail(exception); }
            }
            void Fail(AndroidJavaException exception)
            {
                supported = false;
                Debug.LogWarning("Frontier: Android haptics unavailable. " + exception.Message);
            }
            public void Dispose()
            {
                supported = false;
                if (vibrator != null) vibrator.Dispose();
                if (effect != null) effect.Dispose();
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        sealed class AppleHapticDevice : IHapticDevice
        {
            [DllImport("__Internal")]
            [return: MarshalAs(UnmanagedType.I1)]
            static extern bool FrontierHapticsSupported();
            [DllImport("__Internal")] static extern void FrontierHapticsPulse(float strength);
            public bool IsSupported { get { return FrontierHapticsSupported(); } }
            public void Pulse(float strength) { FrontierHapticsPulse(strength); }
            public void Stop() { }
            public void Dispose() { }
        }
#endif
    }
}
