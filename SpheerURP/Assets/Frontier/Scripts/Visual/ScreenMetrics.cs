using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// Screen size, safe area and density as the game sees them. Defaults to the real screen, and can be switched to a
    /// virtual device so the screenshot harness and layout tests can render any resolution, density or notch.
    /// </summary>
    public static class ScreenMetrics
    {
        static bool isVirtual;
        static int virtualWidth, virtualHeight;
        static Rect virtualSafe;
        static float virtualDensity = 1f;

        public static bool IsVirtual { get { return isVirtual; } }
        public static int Width { get { return isVirtual ? virtualWidth : Screen.width; } }
        public static int Height { get { return isVirtual ? virtualHeight : Screen.height; } }
        public static float Aspect { get { return Width / (float)Mathf.Max(1, Height); } }
        public static float ShortSide { get { return Mathf.Min(Width, Height); } }
        public static Rect SafeArea { get { return isVirtual ? virtualSafe : Screen.safeArea; } }

        /// <summary>Android-style density (dpi / 160), so 1 dp = Density physical pixels.</summary>
        public static float Density
        {
            get { return isVirtual ? virtualDensity : Mathf.Max(0.5f, (Screen.dpi > 1f ? Screen.dpi : 160f) / 160f); }
        }

        public static float PixelsToDp(float physicalPixels) { return physicalPixels / Density; }

        public static void SetVirtual(int width, int height, Rect? safeArea = null, float density = 2.625f)
        {
            isVirtual = true;
            virtualWidth = width; virtualHeight = height;
            virtualSafe = safeArea ?? new Rect(0f, 0f, width, height);
            virtualDensity = Mathf.Max(0.5f, density);
        }

        public static void ClearVirtual() { isVirtual = false; }
    }
}
