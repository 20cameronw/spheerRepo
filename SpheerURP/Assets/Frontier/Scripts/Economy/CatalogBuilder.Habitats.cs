using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    public static partial class CatalogBuilder
    {
        static HarvestMethod MethodOf(char c)
        {
            switch (c)
            {
                case 'C': return HarvestMethod.Chop;
                case 'M': return HarvestMethod.Mine;
                case 'D': return HarvestMethod.Drill;
                case 'S': return HarvestMethod.Siphon;
                case 'X': return HarvestMethod.Excavate;
                case 'V': return HarvestMethod.Salvage;
                case 'H': return HarvestMethod.Harvest;
                default: throw new ArgumentException("method letter " + c);
            }
        }

        // One line per biome: "resource method weight minTier;" ... The belt is the asteroid-belt profile. Scrap is in every profile (always present).
        static readonly string[] HabitatLines =
        {
            "Lush|wood C 1.00 0;stone M 0.55 0;biomass H 0.70 1;copper_ore M 0.10 1;crystal M 0.12 2;scrap V 0.50 0",
            "Mineral|stone M 1.00 0;iron M 0.90 1;copper_ore M 0.80 1;silica M 0.40 1;titanium_ore M 0.35 2;crystal M 0.15 2;wood C 0.20 0;scrap V 0.50 0",
            "Glacial|ice X 1.00 2;crystal M 0.75 2;wood C 0.30 0;stone M 0.45 0;core D 0.12 5;scrap V 0.50 0",
            "Volcanic|iron D 1.00 1;copper_ore M 0.50 1;stone M 0.40 0;titanium_ore D 0.60 2;crystal D 0.25 2;core D 0.30 3;radium X 0.12 4;scrap V 0.50 0",
            "Fungal|wood C 1.00 0;biomass H 1.00 1;methane S 0.40 2;core D 0.40 3;stone M 0.20 0;scrap V 0.50 0",
            "Crystal|crystal M 1.00 2;silica M 0.60 1;iron M 0.50 1;stone M 0.40 0;ice X 0.20 2;radium X 0.30 4;core D 0.15 4;scrap V 0.50 0",
            "Obsidian|iron M 1.00 1;titanium_ore D 0.50 2;crystal M 0.60 2;core D 0.55 3;radium X 0.45 4;dark_matter X 0.15 6;stone M 0.30 0;scrap V 0.50 0",
            "Shattered|crystal M 1.00 2;core D 0.80 3;titanium_ore D 0.40 2;iron M 0.25 1;radium X 0.50 4;dark_matter X 0.35 6;scrap V 0.50 0",
            "Desert|stone M 1.00 0;silica M 1.00 1;iron M 0.60 1;copper_ore M 0.40 1;crystal M 0.10 2;scrap V 0.50 0",
            "Oceanic|wood C 0.80 0;biomass H 0.80 1;methane S 0.35 2;crystal M 0.30 2;stone M 0.20 0;ice X 0.10 2;scrap V 0.50 0",
            "GasGiant|methane S 0.90 2;helium3 S 0.80 3;crystal M 0.30 2;scrap V 0.50 0",
            "Barren|stone M 1.00 0;iron M 0.60 1;copper_ore M 0.30 1;silica M 0.50 1;ice X 0.30 2;titanium_ore M 0.20 2;crystal M 0.15 2;scrap V 0.50 0",
            "Belt|scrap V 1.00 0;stone M 0.80 0;iron M 0.80 1;copper_ore M 0.50 1;ice X 0.50 2;titanium_ore M 0.50 2;crystal M 0.20 2",
        };

        static HabitatDef[] BuildHabitats()
        {
            var list = new List<HabitatDef>();
            for (int i = 0; i < HabitatLines.Length; i++)
            {
                string[] head = HabitatLines[i].Split('|');
                bool belt = head[0] == "Belt";
                Biome biome = belt ? Biome.Barren : (Biome)Enum.Parse(typeof(Biome), head[0]);
                string[] rows = head[1].Split(';');
                for (int k = 0; k < rows.Length; k++)
                {
                    string[] f = rows[k].Trim().Split(' ');
                    list.Add(new HabitatDef
                    {
                        resourceId = f[0],
                        biome = biome,
                        belt = belt,
                        method = MethodOf(f[1][0]),
                        weight = float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture),
                        minTier = int.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture)
                    });
                }
            }
            return list.ToArray();
        }
    }
}
