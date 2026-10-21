using UnityEngine;
using UnityEngine.InputSystem;

// Kamera iz tretje osebe: sledi igralcu, z miško jo vrtiš, ne gre skozi zidove in se strese ob zadetku.
public class CameraController : MonoBehaviour
{
    public Transform target;
    public float distance = 4.8f;
    public float height = 1.8f;
    [Tooltip("Zamik kamere v desno (pogled čez ramo), da junak ne zakriva nasprotnika")]
    public float shoulderOffset = 0.75f;
    public float mouseSensitivity = 0.12f;
    public float minPitch = -5f;
    public float maxPitch = 55f;
    public float followSmoothing = 12f;
    public float collisionRadius = 0.25f;

    [System.NonSerialized] public bool inputEnabled;

    float yaw;
    float pitch = 18f;
    Vector3 focus;
    float shakeStrength;
    float shakeTime;

    public void SnapBehind(Transform t)
    {
        target = t;
        yaw = t.eulerAngles.y;
        focus = t.position + Vector3.up * height + t.right * shoulderOffset;
        LateUpdate();
    }

    public void Shake(float strength, float duration)
    {
        shakeStrength = Mathf.Max(shakeStrength, strength);
        shakeTime = Mathf.Max(shakeTime, duration);
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (inputEnabled && Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity * SaveManager.Sensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 wantedFocus = target.position + Vector3.up * height + Quaternion.Euler(0f, yaw, 0f) * Vector3.right * shoulderOffset;
        focus = Vector3.Lerp(focus, wantedFocus, 1f - Mathf.Exp(-followSmoothing * Time.unscaledDeltaTime));

        Vector3 back = rotation * Vector3.back;
        float actualDistance = distance;

        // Če je med igralcem in kamero zid, kamero približamo (liki ne štejejo)
        foreach (var hit in Physics.SphereCastAll(focus, collisionRadius, back, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider is CharacterController || hit.distance <= 0f) continue;
            actualDistance = Mathf.Min(actualDistance, hit.distance);
        }

        Vector3 shake = Vector3.zero;
        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            shake = Random.insideUnitSphere * shakeStrength;
            if (shakeTime <= 0f) shakeStrength = 0f;
        }

        transform.position = focus + back * actualDistance + shake;
        transform.rotation = rotation;
    }
}
