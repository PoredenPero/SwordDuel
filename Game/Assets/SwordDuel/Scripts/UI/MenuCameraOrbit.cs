using UnityEngine;

// Kamera v ozadju glavnega menija počasi kroži okoli junaka.
public class MenuCameraOrbit : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float height = 1.6f;
    public float speed = 4f;
    public Vector3 lookOffset = new Vector3(0f, 1.1f, 0f);

    float angle = 200f;

    void LateUpdate()
    {
        if (target == null) return;
        angle += speed * Time.unscaledDeltaTime;
        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        transform.position = target.position + rot * new Vector3(0f, height, -distance);
        transform.LookAt(target.position + lookOffset);
    }
}
