using System;
using System.Collections;
using UnityEngine;

// En napad: ime stanja v Animatorju, dolžina animacije in trenutek udarca (0 = začetek, 1 = konec animacije).
[Serializable]
public class AttackMove
{
    public string stateName = "Attack0";
    public float clipLength = 1f;
    [Range(0f, 1f)] public float hitTime = 0.5f;
}

// Skupna bojna logika za igralca IN nasprotnika: napad, zadetek, odziv na udarec in smrt.
// PlayerController in EnemyAI samo odločata, KDAJ napasti – vse ostalo naredi Fighter.
[RequireComponent(typeof(Health))]
public class Fighter : MonoBehaviour
{
    [Header("Statistika")]
    public float damage = 20f;
    [Tooltip("Najmanjši čas med dvema napadoma (s)")]
    public float attackCooldown = 1.2f;
    [Tooltip("Hitrost animacije napada (1 = normalno, manj = počasneje in lažje za izmikanje)")]
    public float attackAnimSpeed = 1f;

    [Header("Doseg udarca")]
    public float attackRange = 2.1f;
    [Tooltip("Kot pred borcem (v stopinjah), v katerem udarec zadene")]
    public float attackAngle = 70f;

    [Header("Odziv na udarec")]
    public float staggerDuration = 0.45f;
    [Tooltip("Po enem odzivu borec nekaj časa ne more biti znova prekinjen (prepreči 'stunlock')")]
    public float staggerImmunity = 1.3f;

    [Header("Ščit (desni klik)")]
    public bool canBlock;
    [Tooltip("Po toliko blokiranih udarcih gre ščit na cooldown")]
    public int blocksBeforeBreak = 2;
    public float shieldCooldown = 15f;
    [Tooltip("Ščit ustavi samo udarce od spredaj (kot v stopinjah)")]
    public float blockAngle = 80f;

    [Header("Animacije")]
    public Animator animator;
    public AttackMove[] attacks;

    [NonSerialized] public Fighter target;
    [NonSerialized] public bool invulnerable;

    public Health Health { get; private set; }
    public bool IsDead => Health.IsDead;
    public bool IsAttacking { get; private set; }
    public bool IsStaggered => Time.time < staggerEnd;
    public bool AttackReady => Time.time >= nextAttackTime;
    public bool CanAct => !IsDead && !IsAttacking && !IsStaggered && !IsBlocking;
    public Vector3 Knockback { get; private set; }

    public bool IsBlocking { get; private set; }
    public bool ShieldReady => Time.time >= shieldReadyTime;
    public float ShieldCooldownLeft => Mathf.Max(0f, shieldReadyTime - Time.time);
    public int BlocksLeft => blocksBeforeBreak - blockedHits;

    // Javi se ob vsakem zadetku v igri (napadalec, žrtev, škoda) – uporablja ga HUD in kamera
    public static event Action<Fighter, Fighter, float> AnyHit;
    // Javi se ob blokiranem udarcu (branilec, napadalec, ali se je ščit zlomil)
    public static event Action<Fighter, Fighter, bool> AnyBlock;

    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int AttackSpeedParam = Animator.StringToHash("AttackSpeed");

    float nextAttackTime;
    float staggerEnd;
    float staggerImmuneUntil;
    int nextAttackIndex;
    int blockedHits;
    float shieldReadyTime;
    Coroutine attackRoutine;
    Coroutine staggerRoutine;

    void Awake()
    {
        Health = GetComponent<Health>();
        Health.Died += OnDied;
    }

    void Update()
    {
        // Odriv po udarcu počasi izzveni
        Knockback = Vector3.MoveTowards(Knockback, Vector3.zero, 14f * Time.deltaTime);
    }

    // 0 = stoji, 1 = teče
    public void SetLocomotion(float amount)
    {
        animator.SetFloat(SpeedParam, amount, 0.1f, Time.deltaTime);
    }

    public bool TryAttack()
    {
        if (!CanAct || !AttackReady || attacks.Length == 0) return false;
        attackRoutine = StartCoroutine(AttackRoutine(attacks[nextAttackIndex]));
        nextAttackIndex = (nextAttackIndex + 1) % attacks.Length;
        return true;
    }

    IEnumerator AttackRoutine(AttackMove move)
    {
        IsAttacking = true;
        nextAttackTime = Time.time + attackCooldown;

        // Animacija se pospeši, če je čas med napadi krajši od animacije – tako ostaneta usklajena
        float speed = Mathf.Max(1f, move.clipLength / (attackCooldown * 0.9f)) * attackAnimSpeed;
        float duration = move.clipLength / speed;
        animator.SetFloat(AttackSpeedParam, speed);
        animator.CrossFadeInFixedTime(move.stateName, 0.08f, 0, 0f);

        // Škoda se lahko zgodi samo v kratkem oknu okoli trenutka udarca v animaciji
        float hitStart = duration * Mathf.Max(0f, move.hitTime - 0.12f);
        float hitEnd = duration * Mathf.Min(1f, move.hitTime + 0.1f);
        bool swung = false, hit = false;

        for (float t = 0f; t < duration * 0.92f; t += Time.deltaTime)
        {
            if (!swung && t >= hitStart)
            {
                swung = true;
                SoundFX.Play(SoundFX.Sound.Swing, 0.7f, 0.12f);
            }
            if (!hit && t >= hitStart && t <= hitEnd && TargetInReach())
            {
                hit = true;
                target.ReceiveHit(damage, this);
            }
            yield return null;
        }

        IsAttacking = false;
        attackRoutine = null;
        animator.CrossFadeInFixedTime("Locomotion", 0.15f);
    }

    // Preprosto in zanesljivo zaznavanje zadetka: nasprotnik mora biti dovolj blizu in pred nami
    public bool TargetInReach()
    {
        if (target == null || target.IsDead) return false;
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > attackRange) return false;
        return Vector3.Angle(transform.forward, toTarget) <= attackAngle;
    }

    // Dvigne ali spusti ščit. Blokirati ni mogoče med napadom, odzivom na udarec ali ko je ščit na cooldownu.
    public void SetBlocking(bool wantBlock)
    {
        bool block = wantBlock && canBlock && ShieldReady && !IsDead && !IsAttacking && !IsStaggered;
        if (block == IsBlocking) return;
        IsBlocking = block;
        animator.CrossFadeInFixedTime(block ? "Block" : "Locomotion", 0.12f);
    }

    public void ReceiveHit(float amount, Fighter attacker)
    {
        if (IsDead || invulnerable) return;

        Vector3 push = transform.position - attacker.transform.position;
        push.y = 0f;

        // Udarec od spredaj med blokiranjem ne naredi škode
        if (IsBlocking && Vector3.Angle(transform.forward, -push) <= blockAngle)
        {
            BlockHit(attacker, push);
            return;
        }

        Health.TakeDamage(amount);
        AnyHit?.Invoke(attacker, this, amount);
        SoundFX.Play(SoundFX.Sound.Hit, 0.9f, 0.1f);
        Knockback = push.normalized * 4f;

        if (!IsDead && Time.time >= staggerImmuneUntil) Stagger();
    }

    void BlockHit(Fighter attacker, Vector3 push)
    {
        blockedHits++;
        Knockback = push.normalized * 2.5f;
        bool broken = blockedHits >= blocksBeforeBreak;

        if (broken)
        {
            // Ščit "poči": gre na cooldown, borec se za trenutek opoteče
            blockedHits = 0;
            shieldReadyTime = Time.time + shieldCooldown;
            SoundFX.Play(SoundFX.Sound.ShieldBreak);
            Stagger();
        }
        else
        {
            SoundFX.Play(SoundFX.Sound.Block, 1f, 0.08f);
        }
        AnyBlock?.Invoke(this, attacker, broken);
    }

    void Stagger()
    {
        CancelAttack();
        IsBlocking = false;
        staggerEnd = Time.time + staggerDuration;
        staggerImmuneUntil = Time.time + staggerImmunity;
        animator.CrossFadeInFixedTime("Hit", 0.05f, 0, 0f);
        if (staggerRoutine != null) StopCoroutine(staggerRoutine);
        staggerRoutine = StartCoroutine(EndStagger());
    }

    IEnumerator EndStagger()
    {
        yield return new WaitForSeconds(staggerDuration);
        staggerRoutine = null;
        if (!IsDead && !IsAttacking) animator.CrossFadeInFixedTime("Locomotion", 0.2f);
    }

    void CancelAttack()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = null;
        IsAttacking = false;
    }

    void OnDied()
    {
        CancelAttack();
        IsBlocking = false;
        if (staggerRoutine != null) StopCoroutine(staggerRoutine);
        animator.CrossFadeInFixedTime("Die", 0.1f, 0, 0f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
