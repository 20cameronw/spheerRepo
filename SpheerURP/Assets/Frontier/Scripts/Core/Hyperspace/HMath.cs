using System;

namespace Frontier.Hyperspace
{
    /// <summary>
    /// Deterministic maths for the Hyperspace simulation.
    ///
    /// DETERMINISM RULE (checked by tools\FloatLint): simulation arithmetic is done in <c>double</c>; public state stays <c>float</c> and is rounded
    /// exactly once, by an explicit <c>(float)</c> cast at the store. Reason: Unity's Mono evaluates float expressions and float locals at double
    /// precision, while CoreCLR (and IL2CPP) round every single-precision operation, so any multi-step <c>float</c> expression can differ in the
    /// last bit between runtimes. + - * / and sqrt on doubles are correctly rounded IEEE operations and identical everywhere.
    /// The transcendental functions below use fixed series/polynomials only (never Math.Sin/Cos/Pow/Exp/Log, which are libm dependent).
    /// The overloads that only select a value (Abs/Min/Max/Clamp/Finite) exist for float, double and int because they never round.
    /// </summary>
    public static class HMath
    {
        /// <summary>pi as a float constant for template authoring data.</summary>
        public const float Pi = (float)Math.PI;
        public const double PiD = Math.PI;
        public const double TwoPiD = 2.0 * Math.PI;
        public const double HalfPiD = 0.5 * Math.PI;

        const double TwoOverPi = 0.63661977236758134308;
        const double PiO2Hi = 1.57079632673412561417;     // fdlibm pio2_1: 33 significant bits, so k * PiO2Hi is exact for |k| < 2^20
        const double PiO2Lo = 6.07710050650619224932e-11; // pi/2 - PiO2Hi
        const double Ln2 = 0.69314718055994530942;
        const double InvLn2 = 1.44269504088896340736;
        const double Log10Of2 = 0.30102999566398119521;

        // ---- selection helpers (exact) -------------------------------------------------------------------------------------------------

        public static float Abs(float v) { return Math.Abs(v); }
        public static double Abs(double v) { return Math.Abs(v); }
        public static int Abs(int v) { return v < 0 ? -v : v; }

        public static float Min(float a, float b) { return a < b ? a : b; }
        public static double Min(double a, double b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }

        public static float Max(float a, float b) { return a > b ? a : b; }
        public static double Max(double a, double b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }

        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static double Clamp01(double v) { return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v); }

        /// <summary>Returns the value, or the fallback when it is NaN or infinite.</summary>
        public static float Finite(float v, float fallback) { return float.IsNaN(v) || float.IsInfinity(v) ? fallback : v; }
        public static double Finite(double v, double fallback) { return double.IsNaN(v) || double.IsInfinity(v) ? fallback : v; }

        // ---- arithmetic helpers (double) -----------------------------------------------------------------------------------------------

        public static double Lerp(double a, double b, double t) { return a + (b - a) * t; }

        public static double Sqrt(double v) { return v > 0.0 ? Math.Sqrt(v) : 0.0; }

        public static double Length(double x, double y) { return Math.Sqrt(x * x + y * y); }

        public static double Dist2(double x0, double y0, double x1, double y1)
        {
            double dx = x1 - x0, dy = y1 - y0;
            return dx * dx + dy * dy;
        }

        /// <summary>Hermite smooth step of x between the two edges (0 below edge0, 1 above edge1).</summary>
        public static double SmoothStep(double edge0, double edge1, double x)
        {
            if (!(edge1 > edge0)) return x >= edge1 ? 1.0 : 0.0;
            double t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0 - 2.0 * t);
        }

        public static double MoveTowards(double current, double target, double maxDelta)
        {
            double d = target - current;
            if (d <= maxDelta && d >= -maxDelta) return target;
            return d > 0.0 ? current + maxDelta : current - maxDelta;
        }

        // ---- trigonometry --------------------------------------------------------------------------------------------------------------

        public static double Sin(double x)
        {
            int q;
            double r = Reduce(x, out q);
            switch (q)
            {
                case 0: return KSin(r);
                case 1: return KCos(r);
                case 2: return -KSin(r);
                default: return -KCos(r);
            }
        }

        public static double Cos(double x)
        {
            int q;
            double r = Reduce(x, out q);
            switch (q)
            {
                case 0: return KCos(r);
                case 1: return -KSin(r);
                case 2: return -KCos(r);
                default: return KSin(r);
            }
        }

        public static void SinCos(double x, out double sin, out double cos)
        {
            int q;
            double r = Reduce(x, out q);
            double s = KSin(r), c = KCos(r);
            switch (q)
            {
                case 0: sin = s; cos = c; break;
                case 1: sin = c; cos = -s; break;
                case 2: sin = -s; cos = -c; break;
                default: sin = -c; cos = s; break;
            }
        }

        /// <summary>Reduces x to r in [-pi/4, pi/4] and the quadrant q (0..3) so that sin(x) = sin(r + q*pi/2). Non-finite or huge input maps to 0.</summary>
        static double Reduce(double x, out int q)
        {
            if (!(x > -1.0e6 && x < 1.0e6)) { q = 0; return 0.0; }
            double k = Math.Floor(x * TwoOverPi + 0.5);
            q = (int)((long)k & 3L);
            return (x - k * PiO2Hi) - k * PiO2Lo;
        }

        // Taylor kernels in nested form; |r| <= pi/4 gives an error below 1e-16.
        static double KSin(double r)
        {
            double z = r * r;
            double p = 1.0 - z / 210.0;
            p = 1.0 - z / 156.0 * p;
            p = 1.0 - z / 110.0 * p;
            p = 1.0 - z / 72.0 * p;
            p = 1.0 - z / 42.0 * p;
            p = 1.0 - z / 20.0 * p;
            p = 1.0 - z / 6.0 * p;
            return r * p;
        }

        static double KCos(double r)
        {
            double z = r * r;
            double p = 1.0 - z / 240.0;
            p = 1.0 - z / 182.0 * p;
            p = 1.0 - z / 132.0 * p;
            p = 1.0 - z / 90.0 * p;
            p = 1.0 - z / 56.0 * p;
            p = 1.0 - z / 30.0 * p;
            p = 1.0 - z / 12.0 * p;
            return 1.0 - z / 2.0 * p;
        }

        /// <summary>Angle of the vector (x, y) in (-pi, pi]; Atan2(0, 0) = 0.</summary>
        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return 0.0;
            double ax = x < 0.0 ? -x : x;
            double ay = y < 0.0 ? -y : y;
            if (ax == 0.0 && ay == 0.0) return 0.0;
            bool swap = ay > ax;
            double a = Atan01(swap ? ax / ay : ay / ax);
            if (swap) a = HalfPiD - a;
            if (x < 0.0) a = PiD - a;
            return y < 0.0 ? -a : a;
        }

        static double Atan01(double t)
        {
            double bias = 0.0;
            if (t > 0.41421356237309503)
            {
                t = (t - 1.0) / (t + 1.0);
                bias = 0.78539816339744831;
            }
            double z = t * t;
            double p = 1.0 / 41.0;
            for (int k = 19; k >= 0; k--) p = 1.0 / (2 * k + 1) - z * p;
            return bias + t * p;
        }

        // ---- logarithms / exponentials -------------------------------------------------------------------------------------------------

        /// <summary>log2(x); x &lt;= 0 returns -1074 and NaN returns 0 so callers never see NaN or -Infinity.</summary>
        public static double Log2(double x)
        {
            if (double.IsNaN(x)) return 0.0;
            if (x <= 0.0) return -1074.0;
            if (x >= double.MaxValue) return 1024.0;
            long bits = BitConverter.DoubleToInt64Bits(x);
            int e = (int)((bits >> 52) & 0x7FFL);
            if (e == 0)
            {
                bits = BitConverter.DoubleToInt64Bits(x * 18014398509481984.0);
                e = (int)((bits >> 52) & 0x7FFL) - 54;
            }
            int ex = e - 1023;
            double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
            if (m > 1.4142135623730951) { m *= 0.5; ex += 1; }
            double s = (m - 1.0) / (m + 1.0);
            double z = s * s;
            double p = 1.0 / 23.0;
            p = 1.0 / 21.0 + z * p;
            p = 1.0 / 19.0 + z * p;
            p = 1.0 / 17.0 + z * p;
            p = 1.0 / 15.0 + z * p;
            p = 1.0 / 13.0 + z * p;
            p = 1.0 / 11.0 + z * p;
            p = 1.0 / 9.0 + z * p;
            p = 1.0 / 7.0 + z * p;
            p = 1.0 / 5.0 + z * p;
            p = 1.0 / 3.0 + z * p;
            p = 1.0 + z * p;
            return ex + 2.0 * s * p * InvLn2;
        }

        public static double Log10(double x) { return Log2(x) * Log10Of2; }

        /// <summary>2^x; saturates instead of overflowing and flushes values below 2^-1021 to 0.</summary>
        public static double Exp2(double x)
        {
            if (double.IsNaN(x)) return 1.0;
            if (x >= 1023.0) return 8.98846567431158e307;
            if (x <= -1021.0) return 0.0;
            double n = Math.Floor(x + 0.5);
            double g = (x - n) * Ln2;
            double p = 1.0 + g / 14.0;
            p = 1.0 + g / 13.0 * p;
            p = 1.0 + g / 12.0 * p;
            p = 1.0 + g / 11.0 * p;
            p = 1.0 + g / 10.0 * p;
            p = 1.0 + g / 9.0 * p;
            p = 1.0 + g / 8.0 * p;
            p = 1.0 + g / 7.0 * p;
            p = 1.0 + g / 6.0 * p;
            p = 1.0 + g / 5.0 * p;
            p = 1.0 + g / 4.0 * p;
            p = 1.0 + g / 3.0 * p;
            p = 1.0 + g / 2.0 * p;
            p = 1.0 + g * p;
            return BitConverter.Int64BitsToDouble((long)((int)n + 1023) << 52) * p;
        }

        /// <summary>x^y for x &gt; 0 (Pow(0, y) and negative bases return 0; Pow(x, 0) = 1).</summary>
        public static double Pow(double x, double y)
        {
            if (!(x > 0.0)) return 0.0;
            if (y == 0.0) return 1.0;
            return Exp2(y * Log2(x));
        }
    }
}
