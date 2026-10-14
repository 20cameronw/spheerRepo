using System;
using Frontier.Universe;

// Runs the strict-determinism digests on any CLR that can load a netstandard2.1 library (used to compare .NET 8 with Unity's Mono).
public static class MonoCheck
{
    static ulong Bits(double v) { return unchecked((ulong)BitConverter.DoubleToInt64Bits(v)); }

    public static int Main(string[] args)
    {
        Console.WriteLine("runtime: " + Environment.Version + " / " + (Type.GetType("Mono.Runtime") != null ? "Mono" : "CoreCLR"));

        ulong h = 0x1234UL;
        var r = new Rng(31337UL);
        for (int i = 0; i < 4000; i++)
        {
            double x = r.Range(-40.0, 40.0), pos = r.Range(1e-5, 1e5), y = r.Range(-9.0, 9.0);
            double s, c;
            DetMath.SinCos(x * 3.0, out s, out c);
            h = Hash.Combine(h, Bits(DetMath.Exp(x)));
            h = Hash.Combine(h, Bits(DetMath.Log(pos)));
            h = Hash.Combine(h, Bits(s));
            h = Hash.Combine(h, Bits(c));
            h = Hash.Combine(h, Bits(DetMath.Atan2(y, x)));
            h = Hash.Combine(h, Bits(DetMath.Pow(pos, 0.37)));
            h = Hash.Combine(h, Bits(DetMath.Log2(pos)));
        }
        Console.WriteLine("DetMath raw-bit digest      0x" + h.ToString("X16"));

        var uni = new Frontier.Universe.Universe(new UniverseConfig(), Landmarks.CreateDefault());
        ulong d = 0x5678UL;
        var r2 = new Rng(424242UL);
        for (int i = 0; i < 20000; i++)
        {
            double x = r2.Range(-14000.0, 14000.0), y = r2.Range(-14000.0, 14000.0);
            d = Hash.Combine(d, Bits(uni.Density(x, y)));
        }
        for (int i = 0; i < 4000; i++)
        {
            double x = r2.Range(-2500.0, 2500.0), y = r2.Range(-2500.0, 2500.0);
            d = Hash.Combine(d, Bits(uni.Density(x, y)));
        }
        Console.WriteLine("density raw-bit digest      0x" + d.ToString("X16"));

        var plain = new Frontier.Universe.Universe(new UniverseConfig());
        Console.WriteLine("golden pure generator       0x" + UniverseDigest.OfRegion(plain, 55, -190, 105, -150).ToString("X16"));
        var lm = new Frontier.Universe.Universe(new UniverseConfig(), Landmarks.CreateDefault());
        Console.WriteLine("landmarks region digest     0x" + UniverseDigest.OfRegion(lm, -25, -20, 25, 20).ToString("X16"));
        return 0;
    }
}
