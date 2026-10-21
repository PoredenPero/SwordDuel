using UnityEngine;

// Umetna inteligenca nasprotnika kot preprost avtomat stanj (state machine):
// Idle -> Chase -> Attack -> (Hurt) -> Chase ... -> Dead
[RequireComponent(typeof(CharacterController), typeof(Fighter))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Idle, Chase, Attack, Hurt, Dead }

    [Header("Nastavitve (GameManager jih nastavi iz LevelData)")]
    public float moveSpeed = 3f;
    [Tooltip("Zakasnitev odziva v sekundah – lažji nasprotniki reagirajo počasneje")]
    public float reactionTime = 0.6f;
    public float detectRange = 40f;
    public float turnSpeed = 360f;
    public float gravity = -20f;

    [System.NonSerialized] public bool active;

    public State CurrentState { get; private set; } = State.Idle;

    CharacterController controller;
    Fighter fighter;
    float stateTimer;
    float verticalVelocity;

    // Na kateri razdalji se ustavi in napade (malo manj od dosega meča)
    float StopDistance => fighter.attackRange * 0.8f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        fighter = GetComponent<Fighter>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        stateTimer += dt;

        Fighter player = fighter.target;
        Vector3 move = Vector3.zero;

        // Prehodi, ki veljajo v vsakem stanju
        if (fighter.IsDead) ChangeState(State.Dead);
        else if (fighter.IsStaggered) ChangeState(State.Hurt);
        else if (!active || player == null || player.IsDead) ChangeState(State.Idle);

        float distance = player != null ? FlatDistance(player.transform.position) : Mathf.Infinity;

        switch (CurrentState)
        {
            case State.Idle:
                // 1. Zaznaj igralca (po reakcijskem času)
                if (active && player != null && !player.IsDead && distance < detectRange && stateTimer >= reactionTime)
                    ChangeState(State.Chase);
                break;

            case State.Chase:
                // 2. Teci proti igralcu  3. ustavi se na razdalji napada
                FaceTarget(player, dt);
                if (distance > StopDistance) move = DirectionTo(player) * moveSpeed;
                else ChangeState(State.Attack);
                break;

            case State.Attack:
                // 4. Glej proti igralcu  5. napadi  6. počakaj glede na težavnost  7. lovi, če se igralec umakne
                if (!fighter.IsAttacking)
                {
                    FaceTarget(player, dt);
                    if (distance > fighter.attackRange) ChangeState(State.Chase);
                    else if (stateTimer >= reactionTime * 0.5f && fighter.AttackReady && IsFacing(player)) fighter.TryAttack();
                }
                break;

            case State.Hurt:
                // 8. Kratek odziv na udarec, potem spet lovi
                if (!fighter.IsStaggered) ChangeState(State.Chase);
                break;

            case State.Dead:
                // 9. Mrtev – nič več ne počne
                break;
        }

        Vector3 delta = (move + fighter.Knockback) * dt;
        if (Arena.Current != null) delta = Arena.Current.ClampMove(transform.position, delta);
        verticalVelocity = controller.isGrounded ? -2f : verticalVelocity + gravity * dt;
        delta.y = verticalVelocity * dt;
        if (controller.enabled) controller.Move(delta);

        // Hitrost animacije teka glede na hitrost gibanja (5 m/s = polni tek)
        fighter.SetLocomotion(fighter.IsDead ? 0f : Mathf.Clamp01(move.magnitude / 5f));
    }

    void ChangeState(State next)
    {
        if (CurrentState == next) return;
        CurrentState = next;
        stateTimer = 0f;
    }

    float FlatDistance(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    Vector3 DirectionTo(Fighter other)
    {
        Vector3 d = other.transform.position - transform.position;
        d.y = 0f;
        return d.normalized;
    }

    void FaceTarget(Fighter other, float dt)
    {
        if (other == null) return;
        Vector3 d = DirectionTo(other);
        if (d.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * dt);
    }

    bool IsFacing(Fighter other) => Vector3.Angle(transform.forward, DirectionTo(other)) < fighter.attackAngle * 0.5f;
}
