using UnityEngine;

public enum Difficulty { Easy, Medium, Hard }

// Podatki o enem levelu: kdo je nasprotnik, kako močan je, koliko kovancev prinese in v kateri areni se borimo.
// Nov level dodaš tako, da ustvariš nov LevelData (Create > Sword Duel > Level) in ga dodaš v LevelDatabase.
[CreateAssetMenu(menuName = "Sword Duel/Level", fileName = "Level")]
public class LevelData : ScriptableObject
{
    [Header("Level")]
    public int levelNumber = 1;
    public Difficulty difficulty = Difficulty.Easy;
    public GameObject arenaPrefab;
    public int coinReward = 50;

    [Header("Nasprotnik")]
    public string enemyName = "Training Dummy";
    public int enemyHealth = 100;
    public int enemyDamage = 8;
    [Tooltip("Sekunde med napadi nasprotnika")]
    public float enemyAttackCooldown = 2.4f;
    [Tooltip("Hitrost teka nasprotnika (m/s)")]
    public float enemyMoveSpeed = 2.6f;
    [Tooltip("Koliko sekund nasprotnik 'razmišlja', preden reagira")]
    public float enemyReactionTime = 0.8f;
    public Color enemyTint = Color.white;
    public float enemyScale = 1f;
}
