using UnityEngine;
using UnityEngine.UI;

// Vrstica zdravja, ki se gladko premika. Svetla "sled" za njo pokaže, koliko je bilo pravkar izgubljeno.
public class HealthBar : MonoBehaviour
{
    public Image fill;
    public Image trail;
    public Text valueText;

    Health health;
    float shown = 1f;
    float trailShown = 1f;
    float trailDelay;

    public void Bind(Health h)
    {
        health = h;
        shown = trailShown = 1f;
        health.Damaged += _ => trailDelay = 0.4f;
    }

    void Update()
    {
        if (health == null) return;
        float target = health.Normalized;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2.5f);
        trailDelay -= Time.unscaledDeltaTime;
        if (trailDelay <= 0f) trailShown = Mathf.MoveTowards(trailShown, shown, Time.unscaledDeltaTime * 0.8f);
        if (trailShown < shown) trailShown = shown;

        fill.fillAmount = shown;
        if (trail != null) trail.fillAmount = trailShown;
        if (valueText != null) valueText.text = Mathf.CeilToInt(health.Current) + " / " + Mathf.CeilToInt(health.maxHealth);
    }
}
