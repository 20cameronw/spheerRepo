using System;

namespace Frontier.Universe
{
    /// <summary>
    /// Deterministic math. Math.Sin/Cos/Exp/Log/Pow/Atan2 delegate to the platform C runtime, which is allowed to differ in the
    /// last bit between CPUs, operating systems and IL2CPP builds. The generator must give the same universe on every device, so
    /// it only uses the functions below. They are built from + - * / and Math.Sqrt/Floor (all correctly rounded by IEEE-754),
    /// polynomial kernels and exact bit manipulation, with relative error around 1e-15.
    /// </summary>
    public static class DetMath
    {
        public const double Pi = 3.14159265358979323846;
        public const double TwoPi = 6.28318530717958647692;
        public const double HalfPi = 1.57079632679489661923;
        public const double Ln2 = 0.693147180559945309417;

        const double Ln2Hi = 6.93147180369123816490e-01;
        const double Ln2Lo = 1.90821492927058770002e-10;
        const double Log2E = 1.44269504088896338700e+00;
        const double TwoOverPi = 6.36619772367581382433e-01;
        const double PiO2A = 1.57079632673412561417e+00;
        const double PiO2B = 6.07710050630396597660e-11;
        const double PiO2C = 2.02226624871116645580e-21;

        /// <summary>e^x for -700 &lt;= x &lt;= 709 (clamped outside).</summary>
        public static double Exp(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            if (x > 709.0) x = 709.0;
            if (x < -700.0) return 0.0;
            double kf = Math.Floor(x * Log2E + 0.5);
            double r = (x - kf * Ln2Hi) - kf * Ln2Lo;
            double p = 1.6059043836821613e-10;
            p = p * r + 2.08767569878681e-09;
            p = p * r + 2.505210838544172e-08;
            p = p * r + 2.755731922398589e-07;
            p = p * r + 2.7557319223985893e-06;
            p = p * r + 2.48015873015873e-05;
            p = p * r + 1.984126984126984e-04;
            p = p * r + 0.001388888888888889;
            p = p * r + 0.008333333333333333;
            p = p * r + 0.041666666666666664;
            p = p * r + 0.16666666666666666;
            p = p * r + 0.5;
            p = p * r + 1.0;
            p = p * r + 1.0;
            int k = (int)kf;
            return p * BitConverter.Int64BitsToDouble((long)(k + 1023) << 52);
        }

        /// <summary>Natural logarithm for x &gt; 0 (returns -Infinity for 0 and NaN for negatives).</summary>
        public static double Log(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            if (x < 0.0) return double.NaN;
            if (x == 0.0) return double.NegativeInfinity;
            if (double.IsPositiveInfinity(x)) return x;
            long bits = BitConverter.DoubleToInt64Bits(x);
            int e = (int)((bits >> 52) & 0x7FF);
            if (e == 0)
            {
                bits = BitConverter.DoubleToInt64Bits(x * 18014398509481984.0);
                e = (int)((bits >> 52) & 0x7FF) - 54;
            }
            e -= 1023;
            double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
            if (m > 1.4142135623730951)
            {
                m *= 0.5;
                e += 1;
            }
            double f = m - 1.0;
            double s = f / (2.0 + f);
            double s2 = s * s;
            double p = 0.04;
            p = p * s2 + 0.043478260869565216;
            p = p * s2 + 0.047619047619047616;
            p = p * s2 + 0.05263157894736842;
            p = p * s2 + 0.058823529411764705;
            p = p * s2 + 0.06666666666666667;
            p = p * s2 + 0.07692307692307693;
            p = p * s2 + 0.09090909090909091;
            p = p * s2 + 0.1111111111111111;
            p = p * s2 + 0.14285714285714285;
            p = p * s2 + 0.2;
            p = p * s2 + 0.3333333333333333;
            p = p * s2 + 1.0;
            double dk = e;
            return dk * Ln2Hi + (2.0 * s * p + dk * Ln2Lo);
        }

        public static double Log2(double x) { return Log(x) * Log2E; }

        /// <summary>x^y for x &gt; 0.</summary>
        public static double Pow(double x, double y)
        {
            if (y == 0.0) return 1.0;
            if (x <= 0.0) return x == 0.0 ? (y > 0.0 ? 0.0 : double.PositiveInfinity) : double.NaN;
            return Exp(y * Log(x));
        }

        /// <summary>Sine and cosine together (accurate for |x| up to about 1e5).</summary>
        public static void SinCos(double x, out double sin, out double cos)
        {
            double kf = Math.Floor(x * TwoOverPi + 0.5);
            double r = x - kf * PiO2A;
            r -= kf * PiO2B;
            r -= kf * PiO2C;
            double r2 = r * r;

            double s = -7.647163731819816e-13;
            s = s * r2 + 1.6059043836821613e-10;
            s = s * r2 - 2.505210838544172e-08;
            s = s * r2 + 2.7557319223985893e-06;
            s = s * r2 - 1.984126984126984e-04;
            s = s * r2 + 0.008333333333333333;
            s = s * r2 - 0.16666666666666666;
            s = s * r2 + 1.0;
            s *= r;

            double c = 4.779477332387385e-14;
            c = c * r2 - 1.1470745597729725e-11;
            c = c * r2 + 2.08767569878681e-09;
            c = c * r2 - 2.755731922398589e-07;
            c = c * r2 + 2.48015873015873e-05;
            c = c * r2 - 0.001388888888888889;
            c = c * r2 + 0.041666666666666664;
            c = c * r2 - 0.5;
            c = c * r2 + 1.0;

            switch (((int)kf) & 3)
            {
                case 0: sin = s; cos = c; break;
                case 1: sin = c; cos = -s; break;
                case 2: sin = -s; cos = -c; break;
                default: sin = -c; cos = s; break;
            }
        }

        public static double Sin(double x) { double s, c; SinCos(x, out s, out c); return s; }

        public static double Cos(double x) { double s, c; SinCos(x, out s, out c); return c; }

        static double AtanUnit(double t)
        {
            double a = t / (1.0 + Math.Sqrt(1.0 + t * t));
            double b = a / (1.0 + Math.Sqrt(1.0 + a * a));
            double c = b / (1.0 + Math.Sqrt(1.0 + b * b));
            double c2 = c * c;
            double p = -0.06666666666666667;
            p = p * c2 + 0.07692307692307693;
            p = p * c2 - 0.09090909090909091;
            p = p * c2 + 0.1111111111111111;
            p = p * c2 - 0.14285714285714285;
            p = p * c2 + 0.2;
            p = p * c2 - 0.3333333333333333;
            p = p * c2 + 1.0;
            return 8.0 * c * p;
        }

        public static double Atan(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            double ax = Math.Abs(x);
            double r = ax > 1.0 ? HalfPi - AtanUnit(1.0 / ax) : AtanUnit(ax);
            return x < 0.0 ? -r : r;
        }

        /// <summary>Angle of the vector (x, y) in (-pi, pi]. Atan2(0, 0) is 0.</summary>
        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            if (x == 0.0 && y == 0.0) return 0.0;
            if (Math.Abs(y) > Math.Abs(x))
            {
                double a = Atan(x / y);
                return y > 0.0 ? HalfPi - a : -HalfPi - a;
            }
            double b = Atan(y / x);
            if (x > 0.0) return b;
            return y >= 0.0 ? b + Pi : b - Pi;
        }

        public static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public static double Clamp01(double v) { return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v); }

        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public static double Lerp(double a, double b, double t) { return a + (b - a) * t; }

        /// <summary>Hermite smooth step from 0 at edge0 to 1 at edge1.</summary>
        public static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>Floor division for ints (rounds toward negative infinity).</summary>
        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }
    }

    /// <summary>Deterministic 2D value noise built on <see cref="Hash"/> (pure arithmetic, no platform math).</summary>
    public static class Noise
    {
        static double Lattice(ulong seed, long ix, long iy)
        {
            unchecked
            {
                return Hash.Unit(Hash.Mix(seed + (ulong)ix * 0x9E3779B185EBCA87UL + (ulong)iy * 0xC2B2AE3D27D4EB4FUL));
            }
        }

        /// <summary>Smooth value noise in [0,1) (quintic fade).</summary>
        public static double Value(ulong seed, double x, double y)
        {
            double fx = Math.Floor(x), fy = Math.Floor(y);
            long ix = (long)fx, iy = (long)fy;
            double tx = x - fx, ty = y - fy;
            double sx = tx * tx * tx * (tx * (tx * 6.0 - 15.0) + 10.0);
            double sy = ty * ty * ty * (ty * (ty * 6.0 - 15.0) + 10.0);
            double v00 = Lattice(seed, ix, iy), v10 = Lattice(seed, ix + 1, iy);
            double v01 = Lattice(seed, ix, iy + 1), v11 = Lattice(seed, ix + 1, iy + 1);
            double a = v00 + (v10 - v00) * sx;
            double b = v01 + (v11 - v01) * sx;
            return a + (b - a) * sy;
        }

        /// <summary>Fractal Brownian motion of value noise, normalised to [0,1).</summary>
        public static double Fbm(ulong seed, double x, double y, int octaves)
        {
            double sum = 0.0, amp = 1.0, norm = 0.0, freq = 1.0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Value(seed + (ulong)i * 0x632BE59BD9B4E019UL, x * freq + i * 17.31, y * freq - i * 9.77);
                norm += amp;
                amp *= 0.5;
                freq *= 2.03;
            }
            return sum / norm;
        }
    }
}
