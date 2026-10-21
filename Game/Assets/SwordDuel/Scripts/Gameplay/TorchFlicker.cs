using UnityEngine;

// Luč bakle rahlo utripa, da deluje kot pravi ogenj.
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    public float baseIntensity = 2f;
    public float flicker = 0.35f;
    public float speed = 6f;

    Light torchLight;
    float seed;

    void Awake()
    {
        torchLight = GetComponent<Light>();
        seed = Random.value * 100f;
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;
        torchLight.intensity = baseIntensity * (1f + noise * flicker);
    }
}
