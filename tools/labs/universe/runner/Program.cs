using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Frontier.Universe.Runner
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string cmd = args.Length > 0 ? args[0] : "test";
            try
            {
                switch (cmd)
                {
                    case "density": return Visuals.QuickDensity(args);
                    case "render": return Visuals.RenderAll(args);
                    case "gallery": return Visuals.RenderGallery(args);
                    case "inspect": return Inspect.Run(args);
                    case "test": return TestRunner.Run(args);
                    default:
                        Console.WriteLine("unknown command " + cmd);
                        return 2;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 3;
            }
        }
    }
}
