using UnityEngine;

namespace Frontier
{
    /// <summary>Global tuning knobs. Edit the asset in Resources/Frontier/FrontierSettings.</summary>
    [CreateAssetMenu(fileName = "FrontierSettings", menuName = "Frontier/Settings")]
    public class FrontierSettings : ScriptableObject
    {
        [Header("Planet")]
        [Tooltip("Degrees per second the planet spins when you aren't touching it.")]
        public float defaultSpin = 16f;
        [Tooltip("Degrees of rotation per pixel dragged.")]
        public float dragDegPerPx = 0.32f;

        [Header("Progression")]
        [Tooltip("Player level = 1 + floor(sqrt(xp / xpPerLevelBase)).")]
        public float xpPerLevelBase = 40f;
        [Tooltip("Tool damage per level = 1 + this * level.")]
        public float toolDamagePerLevel = 0.7f;
        [Tooltip("Share of travel fuel refunded by a perfect travel mini-game (0-1).")]
        [Range(0f, 1f)] public float travelRefundMax = 0.4f;

        [Header("Scene")]
        public Color lightColor = new Color(0.85f, 0.9f, 1f);
        public float lightIntensity = 1.6f;
        public Color ambientColor = new Color(0.32f, 0.34f, 0.5f);
        public Color backgroundColor = new Color(0.01f, 0.012f, 0.04f);
        public float cameraFov = 50f;
        public int targetFrameRate = 60;

        [Header("UI")]
        [Tooltip("UI is scaled so the shorter screen side equals this many pixels.")]
        public float uiReferenceHeight = 1080f;

        static FrontierSettings instance;

        public static FrontierSettings I
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<FrontierSettings>("Frontier/FrontierSettings");
                    if (instance == null) instance = CreateInstance<FrontierSettings>();
                }
                return instance;
            }
        }

        public static void Use(FrontierSettings s) { if (s != null) instance = s; }
    }
}
