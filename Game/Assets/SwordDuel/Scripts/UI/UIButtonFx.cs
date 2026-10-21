using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Učinek na gumbih: ob prehodu z miško se gumb rahlo poveča in obarva zlato, ob kliku zaigra zvok.
public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image background;
    public Text label;
    public Color normalColor = new Color(0.09f, 0.08f, 0.07f, 0.92f);
    public Color hoverColor = new Color(0.83f, 0.66f, 0.27f, 1f);
    public Color normalText = new Color(0.93f, 0.9f, 0.84f);
    public Color hoverText = new Color(0.08f, 0.07f, 0.05f);
    public float hoverScale = 1.04f;

    Button button;
    bool hovered;
    float blend;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnDisable()
    {
        hovered = false;
        blend = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;

    public void OnPointerClick(PointerEventData e)
    {
        if (button == null || button.interactable) SoundFX.Play(SoundFX.Sound.Click, 0.8f);
    }

    void Update()
    {
        bool active = hovered && (button == null || button.interactable);
        blend = Mathf.MoveTowards(blend, active ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        Apply();
    }

    void Apply()
    {
        bool interactable = button == null || button.interactable;
        float alpha = interactable ? 1f : 0.4f;
        if (background != null)
        {
            Color c = Color.Lerp(normalColor, hoverColor, blend);
            c.a *= alpha;
            background.color = c;
        }
        if (label != null)
        {
            Color c = Color.Lerp(normalText, hoverText, blend);
            c.a = alpha;
            label.color = c;
        }
        transform.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, blend);
    }
}
