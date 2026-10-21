using UnityEngine;

// Pravila za nadgradnje: cene, največji nivo in kako nadgradnja spremeni statistiko igralca.
public static class UpgradeManager
{
    public const float BaseHealth = 100f;
    public const float BaseDamage = 20f;
    public const float DamagePerLevel = 5f;
    public const float BaseCooldown = 1.2f;
    public const float CooldownPerLevel = 0.08f;

    public static readonly int[] StrengthCosts = { 100, 150, 225, 325, 450, 600, 800, 1050, 1350, 1700 };
    public static readonly int[] SpeedCosts = { 120, 180, 260, 360, 480, 620, 800, 1000 };

    public static int StrengthMax => StrengthCosts.Length;
    public static int SpeedMax => SpeedCosts.Length;

    public static float DamageAt(int level) => BaseDamage + DamagePerLevel * level;
    public static float CooldownAt(int level) => BaseCooldown - CooldownPerLevel * level;

    public static float PlayerDamage => DamageAt(SaveManager.StrengthLevel);
    public static float PlayerCooldown => CooldownAt(SaveManager.SpeedLevel);

    // Cena naslednjega nivoja ali -1, če je nadgradnja že na maksimumu
    public static int NextStrengthCost => SaveManager.StrengthLevel < StrengthMax ? StrengthCosts[SaveManager.StrengthLevel] : -1;
    public static int NextSpeedCost => SaveManager.SpeedLevel < SpeedMax ? SpeedCosts[SaveManager.SpeedLevel] : -1;

    public static bool TryBuyStrength() => TryBuy(NextStrengthCost, () => SaveManager.StrengthLevel++);
    public static bool TryBuySpeed() => TryBuy(NextSpeedCost, () => SaveManager.SpeedLevel++);

    static bool TryBuy(int cost, System.Action apply)
    {
        if (cost < 0 || SaveManager.Coins < cost) return false;
        SaveManager.Coins -= cost;
        apply();
        return true;
    }
}
