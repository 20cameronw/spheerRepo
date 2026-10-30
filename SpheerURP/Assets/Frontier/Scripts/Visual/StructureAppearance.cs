using UnityEngine;

namespace Frontier
{
    public static class StructureAppearance
    {
        static readonly Color32[] LevelAccents =
        {
            new Color32(224, 91, 20, 255),
            new Color32(218, 153, 32, 255),
            new Color32(150, 182, 45, 255),
            new Color32(47, 175, 101, 255),
            new Color32(29, 171, 168, 255),
            new Color32(39, 153, 218, 255),
            new Color32(91, 115, 224, 255),
            new Color32(153, 91, 214, 255),
            new Color32(209, 79, 162, 255),
            new Color32(238, 189, 74, 255)
        };

        public static Color AccentForLevel(int level)
        {
            return LevelAccents[Mathf.Clamp(level - 1, 0, LevelAccents.Length - 1)];
        }
    }
}
