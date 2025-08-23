using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{

    [Header("Panels")]
    [SerializeField] private MenuPanel MainMenu;
    [SerializeField] private MenuPanel WorldsShop;
    [SerializeField] private MenuPanel ResearchShop;
    [SerializeField] private MenuPanel StructuresShop;
    [SerializeField] private MenuPanel DebugMenu;
    [SerializeField] private MenuPanel InfoMenu;
    [SerializeField] private MenuPanel PrestigeMenu;

    [Range(0, 3)]
    [SerializeField] private float openPanelDelay;

    private MenuPanel currentPanel;

    [Space(10)]
    [Header("Animated text manager")]
    [SerializeField] private GameObject textPrefab; 
    [SerializeField] private Transform canvasTransform;

    void Awake() {
        EnemySpawner.OnWaveStarted += handleWaveStarted;
        EnemySpawner.OnWaveCompleted += handleWaveCompleted;
    }

    public IEnumerator waitAndOpenPanel(string panelName) {
            yield return new WaitForSeconds(openPanelDelay);
            MenuPanel panel = getPanelFromName(panelName);
            currentPanel = panel;
            panel.OpenPanel();
    }

    public void OpenPanel(string panelName) {
        if (currentPanel != null && currentPanel.isOpen && currentPanel == getPanelFromName(panelName))
        {
            //its this panel, so close it.
            ClosePanel();
        } else if (currentPanel == null) {
            //if there is no panel open, open this one
            MenuPanel panel = getPanelFromName(panelName);
            currentPanel = panel;
            panel.OpenPanel();
        } else {
            //if there is a panel open and its not this one, close it, wait, and open this one
            ClosePanel();
            StartCoroutine(waitAndOpenPanel(panelName));
        }
    }

    public void ClosePanel() {
        if (currentPanel != null) {
            currentPanel.ClosePanel();
            currentPanel = null;
        }
    }

    private MenuPanel getPanelFromName(string name) {
        MenuPanel panel = null;
        switch (name) {
            case "main":
                panel = MainMenu;
                break;
            case "worlds":
                panel = WorldsShop;
                break;
            case "research":
                panel = ResearchShop;
                break;
            case "structures":
                panel = StructuresShop;
                break;
            case "debug menu":
                panel = DebugMenu;
                break;
            case "info":
                panel = InfoMenu;
                break;
            case "prestige":
                panel = PrestigeMenu;
                break;
            default:
                panel = MainMenu;
                break;
        }
        return panel;
    }

    public void CreateAnimatedText(string message, Color color, float size = 1f, bool isWaveMessage = false)
    {
        GameObject textObject = Instantiate(textPrefab, canvasTransform);
        TMP_Text textComponent = textObject.GetComponent<TMP_Text>();
        textComponent.text = message;
        textComponent.color = color;
        textComponent.fontSize = Mathf.RoundToInt(30 * size);

        if (isWaveMessage)
        {
            textObject.transform.localPosition = new Vector3(0, 250, 0);
            textComponent.fontSize = 50;

            textObject.transform.localScale = Vector3.zero;
            LeanTween.scale(textObject, Vector3.one, 0.5f).setEaseOutBack().setOnComplete(() =>
            {
                LeanTween.alphaText(textObject.GetComponent<RectTransform>(), 0, 1f).setDelay(1.5f).setOnComplete(() =>
                {
                    Destroy(textObject);
                });
            });
        }
        else
        {
            Vector3 randomOffset = new Vector3(Random.Range(-40f, 40f), Random.Range(-40f, 40f), 0);
            textObject.transform.localPosition = randomOffset;
            textObject.transform.localScale = Vector3.one * 0.5f;
            
            Vector3 moveOffset = new Vector3(Random.Range(-10f, 10f), Random.Range(20f, 40f), 0);
            LeanTween.moveLocal(textObject, textObject.transform.localPosition + moveOffset, 1f).setEaseOutCubic();
            LeanTween.alphaText(textObject.GetComponent<RectTransform>(), 0, 1f).setOnComplete(() =>
            {
                Destroy(textObject);
            });
        }
    }

    public void handleWaveCompleted(int wave) {
        string message = "Wave " + wave + " ended";
        Debug.Log(message);
        CreateAnimatedText(message, Color.white, 1f, true);
    }

    public void handleWaveStarted(int wave) {
        string message = "Wave " + wave + " started";
        Debug.Log(message);
        CreateAnimatedText(message, Color.white, 1f, true);
    }

    public void MineResource() {
        float reward = Player.Instance.getPower();
        string message = "+" + reward;
        CreateAnimatedText(message, Color.yellow, 0.6f);
    }

    void OnDisable() {
        EnemySpawner.OnWaveStarted -= handleWaveStarted;
        EnemySpawner.OnWaveCompleted -= handleWaveCompleted;
    }
}
