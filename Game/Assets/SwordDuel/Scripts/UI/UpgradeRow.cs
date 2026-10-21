using System;
using UnityEngine;
using UnityEngine.UI;

// Ena vrstica na zaslonu nadgradenj (Strength ali Attack Speed).
public class UpgradeRow : MonoBehaviour
{
    public enum Kind { Strength, AttackSpeed }

    public Kind kind;
    public Text levelText;
    public Text statText;
    public Text costText;
    public Button buyButton;
    public Text buyLabel;
    public Image[] pips;
    public Color pipOn = new Color(0.83f, 0.66f, 0.27f);
    public Color pipOff = new Color(1f, 1f, 1f, 0.12f);

    public event Action Purchased;

    void Awake()
    {
        buyButton.onClick.AddListener(Buy);
    }

    void Buy()
    {
        bool ok = kind == Kind.Strength ? UpgradeManager.TryBuyStrength() : UpgradeManager.TryBuySpeed();
        if (!ok) return;
        SoundFX.Play(SoundFX.Sound.Coin);
        Purchased?.Invoke();
    }

    public void Refresh()
    {
        bool strength = kind == Kind.Strength;
        int level = strength ? SaveManager.StrengthLevel : SaveManager.SpeedLevel;
        int max = strength ? UpgradeManager.StrengthMax : UpgradeManager.SpeedMax;
        int cost = strength ? UpgradeManager.NextStrengthCost : UpgradeManager.NextSpeedCost;
        bool maxed = cost < 0;

        levelText.text = "Level " + level + " / " + max;
        string current = strength ? UpgradeManager.DamageAt(level) + " damage" : UpgradeManager.CooldownAt(level).ToString("0.00") + " s cooldown";
        string next = maxed ? "" : strength ? UpgradeManager.DamageAt(level + 1).ToString() : UpgradeManager.CooldownAt(level + 1).ToString("0.00") + " s";
        statText.text = maxed ? current : current + "  <color=#D4A845>→ " + next + "</color>";
        costText.text = maxed ? "MAX LEVEL" : cost + " coins";
        costText.color = maxed || SaveManager.Coins >= cost ? new Color(0.93f, 0.9f, 0.84f) : new Color(0.95f, 0.4f, 0.35f);
        buyButton.interactable = !maxed && SaveManager.Coins >= cost;
        buyLabel.text = maxed ? "MAXED" : "UPGRADE";

        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].gameObject.SetActive(i < max);
            pips[i].color = i < level ? pipOn : pipOff;
        }
    }
}
