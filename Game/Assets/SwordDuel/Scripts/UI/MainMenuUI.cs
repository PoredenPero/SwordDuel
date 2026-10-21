using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Glavni meni: PLAY (izbira levela), UPGRADES, SETTINGS, QUIT.
public class MainMenuUI : MonoBehaviour
{
    [Header("Zasloni")]
    public CanvasGroup mainPanel;
    public CanvasGroup levelPanel;
    public CanvasGroup upgradePanel;
    public CanvasGroup settingsPanel;

    [Header("Glavni zaslon")]
    public Button playButton;
    public Button upgradesButton;
    public Button settingsButton;
    public Button quitButton;
    public Text coinsText;
    public Text strengthText;
    public Text speedText;
    public Button[] backButtons;

    [Header("Izbira levela")]
    public LevelButton[] levelButtons;

    [Header("Nadgradnje")]
    public UpgradeRow[] upgradeRows;
    public Text upgradeCoinsText;

    [Header("Nastavitve")]
    public Slider volumeSlider;
    public Text volumeValue;
    public Slider sensitivitySlider;
    public Text sensitivityValue;
    public Button resetButton;
    public Text resetLabel;

    CanvasGroup current;
    bool resetArmed;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioListener.volume = SaveManager.Volume;

        playButton.onClick.AddListener(() => Show(levelPanel));
        upgradesButton.onClick.AddListener(() => Show(upgradePanel));
        settingsButton.onClick.AddListener(() => Show(settingsPanel));
        quitButton.onClick.AddListener(Quit);
        foreach (var b in backButtons) b.onClick.AddListener(() => Show(mainPanel));
        foreach (var row in upgradeRows) row.Purchased += Refresh;

        volumeSlider.value = SaveManager.Volume;
        sensitivitySlider.value = SaveManager.Sensitivity;
        volumeSlider.onValueChanged.AddListener(v => { SaveManager.Volume = v; RefreshSettings(); });
        sensitivitySlider.onValueChanged.AddListener(v => { SaveManager.Sensitivity = v; RefreshSettings(); });
        resetButton.onClick.AddListener(OnReset);

#if UNITY_WEBGL
        quitButton.gameObject.SetActive(false); // v brskalniku igre ni mogoče "zapreti"
#endif

        foreach (var panel in new[] { mainPanel, levelPanel, upgradePanel, settingsPanel }) SetVisible(panel, false);
        Show(GameSession.OpenLevelSelectOnMenu ? levelPanel : mainPanel);
        GameSession.OpenLevelSelectOnMenu = false;
    }

    void Show(CanvasGroup panel)
    {
        if (current != null) SetVisible(current, false);
        current = panel;
        resetArmed = false;
        Refresh();
        StopAllCoroutines();
        StartCoroutine(FadeIn(panel));
    }

    void Refresh()
    {
        coinsText.text = SaveManager.Coins.ToString();
        upgradeCoinsText.text = SaveManager.Coins.ToString();
        strengthText.text = "Strength  Lv " + SaveManager.StrengthLevel + "   (" + UpgradeManager.PlayerDamage + " dmg)";
        speedText.text = "Attack Speed  Lv " + SaveManager.SpeedLevel + "   (" + UpgradeManager.PlayerCooldown.ToString("0.00") + " s)";
        foreach (var b in levelButtons) b.Refresh();
        foreach (var row in upgradeRows) row.Refresh();
        RefreshSettings();
    }

    void RefreshSettings()
    {
        volumeValue.text = Mathf.RoundToInt(SaveManager.Volume * 100f) + "%";
        sensitivityValue.text = SaveManager.Sensitivity.ToString("0.0") + "x";
        resetLabel.text = resetArmed ? "CLICK AGAIN TO CONFIRM" : "RESET PROGRESS";
    }

    void OnReset()
    {
        // Prvi klik "oboroži" gumb, drugi klik res izbriše napredek
        if (!resetArmed)
        {
            resetArmed = true;
            RefreshSettings();
            return;
        }
        SaveManager.ResetProgress();
        resetArmed = false;
        Refresh();
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static void SetVisible(CanvasGroup panel, bool visible)
    {
        panel.gameObject.SetActive(visible);
        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }

    static IEnumerator FadeIn(CanvasGroup panel)
    {
        SetVisible(panel, true);
        var rt = (RectTransform)panel.transform;
        Vector2 basePos = rt.anchoredPosition;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 0.25f, 3f);
            panel.alpha = k;
            rt.anchoredPosition = basePos + new Vector2(0f, -20f * (1f - k));
            yield return null;
        }
        panel.alpha = 1f;
        rt.anchoredPosition = basePos;
    }
}
