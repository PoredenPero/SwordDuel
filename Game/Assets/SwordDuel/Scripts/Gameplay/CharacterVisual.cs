using System.Collections;
using UnityEngine;

// Barva lika (vsak nasprotnik ima svojo) in rdeč "blisk", ko lik dobi udarec.
public class CharacterVisual : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public Color tint = Color.white;
    public Color flashColor = new Color(1f, 0.15f, 0.1f);

    Renderer[] renderers;
    MaterialPropertyBlock block;
    Coroutine flashRoutine;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        Apply(Color.black);
    }

    public void SetTint(Color color)
    {
        tint = color;
        Apply(Color.black);
    }

    public void Flash()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        const float time = 0.18f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Apply(Color.Lerp(flashColor * 1.5f, Color.black, t / time));
            yield return null;
        }
        Apply(Color.black);
        flashRoutine = null;
    }

    void Apply(Color emission)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, tint);
            block.SetColor(EmissionId, emission);
            r.SetPropertyBlock(block);
        }
    }
}
