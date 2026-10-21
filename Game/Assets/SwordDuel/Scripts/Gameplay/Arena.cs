using UnityEngine;

// Postavitev ene arene: kje se pojavita borca, kako velik je prostor za boj in kakšna je osvetlitev.
public class Arena : MonoBehaviour
{
    public static Arena Current { get; private set; }

    public string arenaName = "Arena";
    public Transform playerSpawn;
    public Transform enemySpawn;

    [Header("Meja (borca ne moreta iz kroga)")]
    public Transform boundsCenter;
    public float boundsRadius = 8f;

    [Header("Osvetlitev")]
    public Light sun;
    public Material skybox;
    public Color skyColor = new Color(0.55f, 0.6f, 0.7f);
    public Color equatorColor = new Color(0.4f, 0.38f, 0.35f);
    public Color groundColor = new Color(0.2f, 0.18f, 0.15f);
    public bool fog;
    public Color fogColor = Color.gray;
    public float fogDensity = 0.01f;

    void OnEnable()
    {
        Current = this;
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
    }

    public void ApplyLighting()
    {
        if (skybox != null) RenderSettings.skybox = skybox;
        if (sun != null) RenderSettings.sun = sun;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyColor;
        RenderSettings.ambientEquatorColor = equatorColor;
        RenderSettings.ambientGroundColor = groundColor;
        RenderSettings.fog = fog;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        DynamicGI.UpdateEnvironment();
    }

    Vector3 Center => boundsCenter != null ? boundsCenter.position : transform.position;

    // Popravi premik tako, da borec ostane znotraj kroga
    public Vector3 ClampMove(Vector3 position, Vector3 delta)
    {
        Vector3 next = position + delta;
        Vector3 offset = next - Center;
        offset.y = 0f;
        if (offset.magnitude <= boundsRadius) return delta;
        offset = offset.normalized * boundsRadius;
        Vector3 clamped = new Vector3(Center.x + offset.x, next.y, Center.z + offset.z);
        return clamped - position;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Vector3 c = Center;
        Vector3 prev = c + new Vector3(boundsRadius, 0.05f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * boundsRadius, 0.05f, Mathf.Sin(a) * boundsRadius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
