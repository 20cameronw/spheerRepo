using System.Collections;
using UnityEngine;

/// <summary>
/// Sends timed tutorial messages to first-time players via PopupManager.
/// Attach this to any persistent GameObject in the MainGame scene.
/// The tutorial fires once per install and is tracked in the save file.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    private struct TutorialStep
    {
        [Tooltip("Seconds after the previous message before this one appears.")]
        public float delay;
        [TextArea(2, 5)]
        public string message;
    }

    [Header("Tutorial Steps")]
    [SerializeField] private TutorialStep[] steps = new TutorialStep[]
    {
        new TutorialStep
        {
            delay   = 2f,
            message = "🌍 Welcome to Spheer! Spin your world to earn money. The faster you spin, the more you earn!"
        },
        new TutorialStep
        {
            delay   = 10f,
            message = "🏗️ Spend your money on structures to boost your passive income. Passive income keeps earning even when you're offline!"
        },
        new TutorialStep
        {
            delay   = 12f,
            message = "⚠️ Each world has a limited number of building slots — choose your structures wisely! You can unlock bigger worlds as you grow."
        },
        new TutorialStep
        {
            delay   = 12f,
            message = "🔬 Research upgrades unlock powerful bonuses. Upgrade your research to multiply your income, boost your defences, and more!"
        },
        new TutorialStep
        {
            delay   = 15f,
            message = "👾 Alien waves are coming! They invade in increasing difficulty. Each wave is harder than the last — there is no final wave!"
        },
        new TutorialStep
        {
            delay   = 12f,
            message = "🛡️ Build turrets and lazers to auto-defend your world. You can also tap aliens directly to deal bonus damage. Killing aliens earns XP!"
        },
        new TutorialStep
        {
            delay   = 15f,
            message = "✨ Once you've grown powerful, hit Prestige to earn Dark Matter — a permanent currency that boosts ALL future earnings. The grind is worth it!"
        },
    };

    private IEnumerator Start()
    {
        // Wait a frame for Player singleton to initialize
        yield return null;

        if (Player.Instance == null || Player.Instance.getHasSeenTutorial())
            yield break;

        Player.Instance.markTutorialSeen();
        StartCoroutine(SendTutorialMessages());
    }

    private IEnumerator SendTutorialMessages()
    {
        foreach (var step in steps)
        {
            yield return new WaitForSeconds(step.delay);
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowPopup(step.message);
        }
    }
}
