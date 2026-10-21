using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Vodi en dvoboj: naloži areno in borca za izbrani level, odšteje 3-2-1,
// zazna zmago/poraz, podeli kovance (samo enkrat) in upravlja pavzo.
public class GameManager : MonoBehaviour
{
    public enum Phase { Countdown, Fighting, Ended }

    [Header("Prefabi")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;
    public GameObject hitSparkPrefab;

    [Header("Scena")]
    public CameraController cameraController;
    public GameUI ui;

    public LevelData Level { get; private set; }
    public Fighter Player { get; private set; }
    public Fighter Enemy { get; private set; }
    public Phase CurrentPhase { get; private set; }
    public bool IsPaused { get; private set; }

    PlayerController playerController;
    EnemyAI enemyAI;
    bool rewardGiven;

    void Start()
    {
        Time.timeScale = 1f;
        Level = LevelDatabase.Instance.Get(GameSession.SelectedLevel);

        // 1. Arena
        var arena = Instantiate(Level.arenaPrefab).GetComponent<Arena>();
        arena.ApplyLighting();

        // 2. Igralec s statistiko iz nadgradenj
        Player = Instantiate(playerPrefab, arena.playerSpawn.position, arena.playerSpawn.rotation).GetComponent<Fighter>();
        Player.damage = UpgradeManager.PlayerDamage;
        Player.attackCooldown = UpgradeManager.PlayerCooldown;
        Player.Health.SetMax(UpgradeManager.BaseHealth);
        playerController = Player.GetComponent<PlayerController>();
        playerController.cameraTransform = cameraController.transform;

        // 3. Nasprotnik s statistiko iz LevelData
        Enemy = Instantiate(enemyPrefab, arena.enemySpawn.position, arena.enemySpawn.rotation).GetComponent<Fighter>();
        Enemy.name = Level.enemyName;
        Enemy.damage = Level.enemyDamage;
        Enemy.attackCooldown = Level.enemyAttackCooldown;
        Enemy.attackAnimSpeed = Level.difficulty == Difficulty.Easy ? 0.8f : Level.difficulty == Difficulty.Medium ? 0.9f : 1f;
        Enemy.Health.SetMax(Level.enemyHealth);
        Enemy.transform.localScale = Vector3.one * Level.enemyScale;
        enemyAI = Enemy.GetComponent<EnemyAI>();
        enemyAI.moveSpeed = Level.enemyMoveSpeed;
        enemyAI.reactionTime = Level.enemyReactionTime;
        var visual = Enemy.GetComponentInChildren<CharacterVisual>();
        if (visual != null) visual.SetTint(Level.enemyTint);

        Player.target = Enemy;
        Enemy.target = Player;
        Player.Health.Died += () => EndFight(false);
        Enemy.Health.Died += () => EndFight(true);
        Fighter.AnyHit += OnAnyHit;
        Fighter.AnyBlock += OnAnyBlock;

        cameraController.SnapBehind(Player.transform);
        ui.Setup(this);
        StartCoroutine(CountdownRoutine());
    }

    void OnDestroy()
    {
        Fighter.AnyHit -= OnAnyHit;
        Fighter.AnyBlock -= OnAnyBlock;
        Time.timeScale = 1f;
    }

    IEnumerator CountdownRoutine()
    {
        CurrentPhase = Phase.Countdown;
        SetCursorLocked(true);
        cameraController.inputEnabled = true;
        if (Level.levelNumber == 1) ui.ShowTutorial(true);

        for (int i = 3; i > 0; i--)
        {
            ui.ShowCountdown(i.ToString());
            SoundFX.Play(SoundFX.Sound.Beep);
            yield return new WaitForSeconds(1f);
        }
        ui.ShowCountdown("FIGHT!");
        SoundFX.Play(SoundFX.Sound.Go);

        CurrentPhase = Phase.Fighting;
        playerController.inputEnabled = true;
        enemyAI.active = true;

        yield return new WaitForSeconds(0.8f);
        ui.ShowCountdown(null);
        if (Level.levelNumber == 1)
        {
            yield return new WaitForSeconds(6f);
            ui.ShowTutorial(false);
        }
    }

    void Update()
    {
        if (CurrentPhase != Phase.Ended && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetPaused(!IsPaused);
    }

    void OnAnyHit(Fighter attacker, Fighter victim, float damage)
    {
        Vector3 chest = victim.transform.position + Vector3.up * 1.2f;
        ui.ShowDamageNumber(chest, damage, victim == Player);
        cameraController.Shake(victim == Player ? 0.12f : 0.07f, 0.15f);

        var visual = victim.GetComponentInChildren<CharacterVisual>();
        if (visual != null) visual.Flash();

        if (hitSparkPrefab != null)
        {
            Vector3 toAttacker = (attacker.transform.position - victim.transform.position).normalized;
            var spark = Instantiate(hitSparkPrefab, chest + toAttacker * 0.3f, Quaternion.LookRotation(toAttacker));
            Destroy(spark, 2f);
        }
    }

    void OnAnyBlock(Fighter defender, Fighter attacker, bool shieldBroken)
    {
        Vector3 toAttacker = (attacker.transform.position - defender.transform.position).normalized;
        Vector3 shieldPoint = defender.transform.position + Vector3.up * 1.1f + toAttacker * 0.5f;

        if (shieldBroken) ui.ShowFloatingText(shieldPoint + Vector3.up * 0.4f, "SHIELD BROKEN!", new Color(1f, 0.45f, 0.3f), 1.2f);
        else ui.ShowFloatingText(shieldPoint + Vector3.up * 0.4f, "BLOCKED", new Color(0.7f, 0.85f, 1f), 0.9f);
        cameraController.Shake(shieldBroken ? 0.15f : 0.05f, shieldBroken ? 0.25f : 0.1f);

        if (hitSparkPrefab != null)
        {
            var spark = Instantiate(hitSparkPrefab, shieldPoint, Quaternion.LookRotation(toAttacker));
            Destroy(spark, 2f);
        }
    }

    // Zmaga ali poraz – kliče se samo enkrat, ker preverimo fazo
    void EndFight(bool victory)
    {
        if (CurrentPhase == Phase.Ended) return;
        CurrentPhase = Phase.Ended;
        playerController.inputEnabled = false;
        enemyAI.active = false;

        int coins = 0;
        if (victory && !rewardGiven)
        {
            rewardGiven = true;
            coins = Level.coinReward;
            SaveManager.Coins += coins;
            SaveManager.MarkCompleted(Level.levelNumber);
        }
        StartCoroutine(EndRoutine(victory, coins));
    }

    IEnumerator EndRoutine(bool victory, int coins)
    {
        // Kratek "slow motion" ob koncu boja
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(0.9f);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(1f);

        cameraController.inputEnabled = false;
        SetCursorLocked(false);
        SoundFX.Play(victory ? SoundFX.Sound.Victory : SoundFX.Sound.Defeat);
        bool hasNext = Level.levelNumber < LevelDatabase.Instance.Count;
        if (victory) ui.ShowVictory(coins, hasNext);
        else ui.ShowDefeat();
    }

    public void SetPaused(bool paused)
    {
        if (CurrentPhase == Phase.Ended) return;
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        cameraController.inputEnabled = !paused;
        SetCursorLocked(!paused);
        ui.ShowPause(paused);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // Gumbi na zaslonih zmage/poraza/pavze
    public void NextLevel() => GameSession.StartLevel(Level.levelNumber + 1);
    public void Replay() => GameSession.StartLevel(Level.levelNumber);
    public void MainMenu()
    {
        AudioListener.pause = false;
        GameSession.GoToMenu(true);
    }
}
