using UnityEngine;
using UnityEngine.InputSystem;

// Upravljanje igralca: WASD gibanje glede na kamero, levi klik napad, desni klik (drži) ščit, Space izmik (dash).
[RequireComponent(typeof(CharacterController), typeof(Fighter))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float turnSpeed = 720f;
    public float gravity = -20f;

    [Header("Izmik (Space)")]
    public float dashSpeed = 11f;
    public float dashDuration = 0.22f;
    public float dashCooldown = 0.9f;

    [Header("Samodejno obračanje proti nasprotniku")]
    public float lockOnRange = 4.5f;

    [System.NonSerialized] public Transform cameraTransform;
    [System.NonSerialized] public bool inputEnabled;

    CharacterController controller;
    Fighter fighter;
    float verticalVelocity;
    float dashEnd;
    float nextDashTime;
    float attackBufferedUntil;
    Vector3 dashDirection;

    public bool IsDashing => Time.time < dashEnd;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        fighter = GetComponent<Fighter>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 move = inputEnabled ? ReadMoveInput() : Vector3.zero;

        if (inputEnabled && !fighter.IsDead)
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) TryDash(move);
            // Kratek "spomin" klika: če klikneš malo prezgodaj, napad sproži takoj, ko je mogoče
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) attackBufferedUntil = Time.time + 0.25f;
        }

        // Ščit je dvignjen, dokler držiš desni klik
        bool wantBlock = inputEnabled && !IsDashing && Mouse.current != null && Mouse.current.rightButton.isPressed;
        fighter.SetBlocking(wantBlock);

        if (Time.time < attackBufferedUntil && !IsDashing && fighter.CanAct && fighter.AttackReady)
        {
            FaceTargetIfClose(1f);
            if (fighter.TryAttack()) attackBufferedUntil = 0f;
        }

        fighter.invulnerable = IsDashing;

        Vector3 velocity;
        if (fighter.IsDead || fighter.IsStaggered)
        {
            velocity = Vector3.zero;
        }
        else if (IsDashing)
        {
            velocity = dashDirection * dashSpeed;
        }
        else if (fighter.IsAttacking || fighter.IsBlocking)
        {
            // Med napadom in blokiranjem igralec stoji in se obrača proti nasprotniku
            velocity = Vector3.zero;
            FaceTargetIfClose(8f * dt);
        }
        else
        {
            velocity = move * moveSpeed;
            if (move.sqrMagnitude > 0.01f) RotateTowards(move, turnSpeed * dt);
        }

        velocity += fighter.Knockback;
        verticalVelocity = controller.isGrounded ? -2f : verticalVelocity + gravity * dt;

        Vector3 delta = velocity * dt;
        if (Arena.Current != null) delta = Arena.Current.ClampMove(transform.position, delta);
        delta.y = verticalVelocity * dt;
        if (controller.enabled) controller.Move(delta);

        float planarSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        fighter.SetLocomotion(fighter.IsDead ? 0f : Mathf.Clamp01(planarSpeed / moveSpeed));
    }

    Vector3 ReadMoveInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || cameraTransform == null) return Vector3.zero;

        float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        Vector3 move = forward.normalized * z + right.normalized * x;
        return Vector3.ClampMagnitude(move, 1f);
    }

    void TryDash(Vector3 move)
    {
        if (Time.time < nextDashTime || !fighter.CanAct) return;
        dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : -transform.forward;
        dashEnd = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;
        SoundFX.Play(SoundFX.Sound.Dash, 0.6f, 0.1f);
    }

    // Med napadom se igralec obrne proti bližnjemu nasprotniku, da udarci ne gredo mimo
    void FaceTargetIfClose(float amount)
    {
        var target = fighter.target;
        if (target == null || target.IsDead) return;
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > lockOnRange) return;
        RotateTowards(toTarget, amount >= 1f ? 360f : 720f * amount);
    }

    void RotateTowards(Vector3 direction, float maxDegrees)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion wanted = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, maxDegrees);
    }
}
