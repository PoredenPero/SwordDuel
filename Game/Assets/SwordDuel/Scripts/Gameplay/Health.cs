using System;
using UnityEngine;

// Življenjske točke borca. Javi, ko dobi udarec (Damaged) in ko umre (Died).
public class Health : MonoBehaviour
{
    public float maxHealth = 100f;

    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;
    public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;

    public event Action<float> Damaged;
    public event Action Died;

    void Awake()
    {
        Current = maxHealth;
    }

    public void SetMax(float max)
    {
        maxHealth = max;
        Current = max;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Max(0f, Current - amount);
        Damaged?.Invoke(amount);
        if (IsDead) Died?.Invoke();
    }
}
