using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// HUD med bojem (zdravje, ime nasprotnika, level) in zasloni za pavzo, zmago in poraz.
public class GameUI : MonoBehaviour
{
    [Header("HUD")]
    public HealthBar playerBar;
    public HealthBar enemyBar;
    public Text enemyNameText;
    public Text levelText;
    public Text countdownText;
    public GameObject tutorialPanel;
    public Text damageNumberTemplate;
    public Button pauseButton;

    [Header("Ščit")]
    public Image shieldIcon;
    public Image shieldCooldownFill;
    public Text shieldCooldownText;
    public Image[] shieldPips;

    [Header("Pavza")]
    public CanvasGroup pausePanel;
    public Button resumeButton;
    public Button restartButton;
    public Button pauseMenuButton;

    [Header("Zmaga")]
    public CanvasGroup victoryPanel;
    public Text victoryCoinsText;
    public Text victoryTotalText;
    public Button nextLevelButton;
    public Button replayButton;
    public Button victoryMenuButton;

    [Header("Poraz")]
    public CanvasGroup defeatPanel;
    public Button retryButton;
    public Button defeatMenuButton;

    GameManager game;

    public static Color DifficultyColor(Difficulty d) =>
        d == Difficulty.Easy ? new Color(0.45f, 0.85f, 0.45f) :
        d == Difficulty.Medium ? new Color(0.95f, 0.72f, 0.25f) :
        new Color(0.95f, 0.32f, 0.28f);

    public void Setup(GameManager gm)
    {
        game = gm;
        playerBar.Bind(gm.Player.Health);
        enemyBar.Bind(gm.Enemy.Health);
        enemyNameText.text = gm.Level.enemyName.ToUpper();
        string color = ColorUtility.ToHtmlStringRGB(DifficultyColor(gm.Level.difficulty));
        levelText.text = "LEVEL " + gm.Level.levelNumber + "   <color=#" + color + ">" + gm.Level.difficulty.ToString().ToUpper() + "</color>";

        pauseButton.onClick.AddListener(() => gm.SetPaused(true));
        resumeButton.onClick.AddListener(() => gm.SetPaused(false));
        restartButton.onClick.AddListener(gm.Replay);
        pauseMenuButton.onClick.AddListener(gm.MainMenu);
        nextLevelButton.onClick.AddListener(gm.NextLevel);
        replayButton.onClick.AddListener(gm.Replay);
        victoryMenuButton.onClick.AddListener(gm.MainMenu);
        retryButton.onClick.AddListener(gm.Replay);
        defeatMenuButton.onClick.AddListener(gm.MainMenu);

        HidePanel(pausePanel);
        HidePanel(victoryPanel);
        HidePanel(defeatPanel);
        tutorialPanel.SetActive(false);
        countdownText.gameObject.SetActive(false);
        damageNumberTemplate.gameObject.SetActive(false);
    }

    // Ikona ščita: zlata = pripravljen, sivi krog odšteva cooldown, pikice = koliko blokov še zdrži
    void Update()
    {
        if (game == null || game.Player == null) return;
        var player = game.Player;
        bool ready = player.ShieldReady;
        float left = player.ShieldCooldownLeft;

        shieldCooldownFill.fillAmount = ready ? 0f : left / player.shieldCooldown;
        shieldCooldownText.gameObject.SetActive(!ready);
        shieldCooldownText.text = Mathf.CeilToInt(left).ToString();

        Color iconColor = ready ? new Color(0.83f, 0.66f, 0.27f) : new Color(0.45f, 0.42f, 0.38f);
        if (player.IsBlocking) iconColor = new Color(0.75f, 0.9f, 1f);
        shieldIcon.color = iconColor;

        for (int i = 0; i < shieldPips.Length; i++)
            shieldPips[i].color = ready && i < player.BlocksLeft ? new Color(0.83f, 0.66f, 0.27f) : new Color(1f, 1f, 1f, 0.15f);
    }

    public void ShowCountdown(string text)
    {
        countdownText.gameObject.SetActive(text != null);
        if (text == null) return;
        countdownText.text = text;
        StartCoroutine(Pop(countdownText.transform, 1.6f));
    }

    public void ShowTutorial(bool show) => tutorialPanel.SetActive(show);

    public void ShowPause(bool show)
    {
        if (show) StartCoroutine(ShowPanel(pausePanel));
        else HidePanel(pausePanel);
        pauseButton.gameObject.SetActive(!show);
    }

    public void ShowVictory(int coins, bool hasNextLevel)
    {
        victoryCoinsText.text = "+" + coins;
        victoryTotalText.text = "Total: " + SaveManager.Coins + " coins";
        nextLevelButton.gameObject.SetActive(hasNextLevel);
        pauseButton.gameObject.SetActive(false);
        StartCoroutine(ShowPanel(victoryPanel));
    }

    public void ShowDefeat()
    {
        pauseButton.gameObject.SetActive(false);
        StartCoroutine(ShowPanel(defeatPanel));
    }

    public void ShowDamageNumber(Vector3 worldPosition, float amount, bool onPlayer)
    {
        Color color = onPlayer ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.85f, 0.4f);
        ShowFloatingText(worldPosition, Mathf.RoundToInt(amount).ToString(), color, 1f);
    }

    // Besedilo, ki se pojavi nad likom in odplava navzgor (škoda, "BLOCKED" ...)
    public void ShowFloatingText(Vector3 worldPosition, string text, Color color, float scale)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z < 0f) return;

        var number = Instantiate(damageNumberTemplate, damageNumberTemplate.transform.parent);
        number.gameObject.SetActive(true);
        number.text = text;
        number.color = color;
        number.transform.position = screen + new Vector3(Random.Range(-30f, 30f), 0f, 0f);
        StartCoroutine(FloatAway(number, scale * (text.Length > 4 ? 0.7f : 1f)));
    }

    IEnumerator FloatAway(Text number, float scale)
    {
        Vector3 start = number.transform.position;
        Color color = number.color;
        const float time = 0.9f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            number.transform.position = start + Vector3.up * (80f * k);
            number.transform.localScale = scale * Vector3.one * (k < 0.15f ? Mathf.Lerp(0.6f, 1.3f, k / 0.15f) : Mathf.Lerp(1.3f, 1f, (k - 0.15f) / 0.85f));
            color.a = 1f - k * k;
            number.color = color;
            yield return null;
        }
        Destroy(number.gameObject);
    }

    static void HidePanel(CanvasGroup panel)
    {
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        panel.gameObject.SetActive(false);
    }

    static IEnumerator ShowPanel(CanvasGroup panel)
    {
        panel.gameObject.SetActive(true);
        panel.interactable = true;
        panel.blocksRaycasts = true;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.25f;
            panel.alpha = k;
            panel.transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
            yield return null;
        }
        panel.alpha = 1f;
        panel.transform.localScale = Vector3.one;
    }

    static IEnumerator Pop(Transform t, float from)
    {
        for (float time = 0f; time < 0.25f; time += Time.unscaledDeltaTime)
        {
            t.localScale = Vector3.one * Mathf.Lerp(from, 1f, time / 0.25f);
            yield return null;
        }
        t.localScale = Vector3.one;
    }
}
