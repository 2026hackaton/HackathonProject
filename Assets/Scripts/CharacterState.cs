using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어/CPU 공용 "몸 상태" 컴포넌트 (2D 버전).
/// 레이어/마스크를 전혀 안 씁니다 — 살아있는 캐릭터 전부를 static 리스트로 들고 있다가
/// 밀치기 판정 때 거리만 비교합니다. 3D 버전에서 겪은 Character Mask 설정 누락 문제를
/// 구조적으로 없앤 버전입니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CharacterState : MonoBehaviour
{
    public enum CombatState { Normal, Staggered, Ragdoll }

    public static readonly List<CharacterState> All = new List<CharacterState>();

    [Header("식별")]
    public bool isPlayer = false;
    public string displayName = "CPU";

    [Header("스턴 / 기절 밸런스")]
    [SerializeField] float staggerDuration = 2.0f;
    [SerializeField] float staggerSpeedMult = 0.45f;
    [SerializeField] float ragdollDuration = 1.4f;
    [SerializeField] float postRagdollInvuln = 0.6f;
    [SerializeField] float ragdollKnockForce = 6f;
    [SerializeField] float ragdollSpin = 720f; // 초당 회전각(도), 기절 중 빙글빙글 도는 연출

    [Header("밀치기 (제자리 즉시 판정, 대시 없음)")]
    [SerializeField] float pushCooldown = 2.0f;
    [SerializeField] float pushRange = 1.2f;

    [Header("차징 중 이동 배율")]
    [SerializeField] float chargeSpeedMult = 0.55f;

    [Header("소켓")]
    [Tooltip("상자를 들었을 때 붙는 위치. 비워두면 캐릭터 트랜스폼을 그대로 씁니다.")]
    public Transform handSocket;

    public CombatState State { get; private set; } = CombatState.Normal;
    public int HitStreak { get; private set; }
    public float InvulnTimer { get; private set; }
    public float PushCooldownTimer { get; private set; }
    public bool IsCharging { get; set; }
    public float ChargeTime { get; set; }
    public BoxItem CarriedBox { get; private set; }
    public bool IsCarrying => CarriedBox != null;
    public bool CanAct => State != CombatState.Ragdoll;

    public event Action OnPlayerHit;
    public event Action OnStaggerEnter;
    public event Action OnRagdollEnter;
    public event Action OnRecovered;

    Rigidbody2D rb;
    float stateTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        float dt = Time.deltaTime;
        if (InvulnTimer > 0f) InvulnTimer = Mathf.Max(0f, InvulnTimer - dt);
        if (PushCooldownTimer > 0f) PushCooldownTimer = Mathf.Max(0f, PushCooldownTimer - dt);

        if (State == CombatState.Ragdoll)
        {
            stateTimer -= dt;
            if (stateTimer <= 0f) RecoverFromRagdoll();
            return;
        }

        if (State == CombatState.Staggered)
        {
            stateTimer -= dt;
            if (stateTimer <= 0f)
            {
                State = CombatState.Normal;
                HitStreak = 0;
                OnRecovered?.Invoke();
            }
        }
    }

    public float GetSpeedMultiplier()
    {
        float m = 1f;
        if (State == CombatState.Staggered) m *= staggerSpeedMult;
        if (IsCharging) m *= chargeSpeedMult;
        return m;
    }

    /// <summary>제자리 밀치기. 사거리 안의 모든 캐릭터에게 동시에 판정됩니다.</summary>
    public bool TryPush()
    {
        if (!CanAct || IsCarrying || PushCooldownTimer > 0f) return false;
        PushCooldownTimer = pushCooldown;

        foreach (var other in All)
        {
            if (other == this) continue;
            float d = Vector2.Distance(other.transform.position, transform.position);
            if (d <= pushRange) other.ApplyHit(transform.position);
        }
        return true;
    }

    public void ApplyHit(Vector2 attackerPos)
    {
        if (InvulnTimer > 0f || State == CombatState.Ragdoll) return;

        HitStreak++;
        if (isPlayer) OnPlayerHit?.Invoke();

        Vector2 knockDir = (Vector2)transform.position - attackerPos;
        knockDir = knockDir.sqrMagnitude > 0.01f ? knockDir.normalized : Vector2.up;

        if (HitStreak >= 2) EnterRagdoll(knockDir);
        else EnterStagger();
    }

    void EnterStagger()
    {
        State = CombatState.Staggered;
        stateTimer = staggerDuration;
        OnStaggerEnter?.Invoke();
    }

    void EnterRagdoll(Vector2 knockDir)
    {
        State = CombatState.Ragdoll;
        stateTimer = ragdollDuration;

        if (IsCarrying) CarriedBox.ForceDrop(true); // 충격으로 놓침 (폭탄이면 여기서 터짐)

        rb.freezeRotation = false;
        rb.linearVelocity = knockDir * ragdollKnockForce;
        rb.angularVelocity = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * ragdollSpin;

        OnRagdollEnter?.Invoke();
    }

    void RecoverFromRagdoll()
    {
        State = CombatState.Normal;
        HitStreak = 0;
        InvulnTimer = postRagdollInvuln;

        rb.angularVelocity = 0f;
        transform.rotation = Quaternion.identity;
        rb.freezeRotation = true;

        OnRecovered?.Invoke();
    }

    public bool TryPickUp(BoxItem box)
    {
        if (IsCarrying || !CanAct || box == null || box.CurrentHolder != null) return false;
        CarriedBox = box;
        box.OnPickedUp(this);
        return true;
    }

    /// <summary>BoxItem 쪽에서 상자가 손을 떠날 때(던짐/강제드롭/납품) 호출합니다.</summary>
    public void ClearCarried() => CarriedBox = null;
}
