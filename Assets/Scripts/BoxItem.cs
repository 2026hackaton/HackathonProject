using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BoxType { Normal, Bomb, Return, Creature }
public enum BoxPhase { Ground, Held, Flying, Returning }

/// <summary>
/// 상자 하나의 상태 (2D 버전). 물리 엔진에 의존하지 않고 좌표를 직접 스크립트로
/// 움직입니다 (HTML 프로토타입과 동일한 방식) — Rigidbody2D 충돌/트리거 설정이
/// 전혀 필요 없습니다.
///
/// - Normal: 기본 상자
/// - Bomb: 충격(던져서 착지 / 밀쳐서 놓침)을 받으면 폭발
/// - Return: 배송구역에 넣어도 returnCyclesRequired 만큼은 다시 걸어나감
/// - Creature: 바닥(Ground)에 있을 때 스스로 배회
///
/// 배송구역은 트리거가 아니라 "지금 이 좌표가 구역 안인가"를 매 프레임 직접 물어보는
/// 방식(DeliveryZone.Contains)이라, 공중에서 존에 들어가는 순간 바로 납품됩니다.
/// 즉 폭탄도 잘 조준해서 던지면 바닥에 닿아 터지기 전에 안전하게 점수가 됩니다.
/// </summary>
public class BoxItem : MonoBehaviour
{
    public static readonly List<BoxItem> All = new List<BoxItem>();

    [Header("종류")]
    public BoxType boxType = BoxType.Normal;

    [Header("시각 (선택) - 공중에 떠 보이게 할 자식 트랜스폼")]
    [SerializeField] Transform visualPivot;
    [SerializeField] float heightVisualScale = 1f;

    [Header("반품 택배 설정")]
    [SerializeField] int returnCyclesRequired = 1;
    [SerializeField] float returnWalkOutDistance = 2.2f;
    [SerializeField] float returnWalkOutDuration = 1.4f;

    [Header("생물 택배 설정")]
    [SerializeField] float creatureWalkSpeed = 1.3f;
    [SerializeField] float creatureWanderRadius = 2f;

    [Header("폭탄 택배 설정")]
    [SerializeField] float explosionRadius = 2.2f;
    [SerializeField] GameObject explosionVfxPrefab;

    [Header("던지기 물리 (스크립트 포물선, 진짜 중력 아님)")]
    [SerializeField] float gravity = 9f;

    public BoxPhase Phase { get; private set; } = BoxPhase.Ground;
    public CharacterState CurrentHolder { get; private set; }
    public CharacterState ThrownBy { get; private set; }

    Vector2 flyVel;
    float height;
    float heightVel;
    int returnsLeft;
    Vector2 wanderTarget;
    float wanderTimer;
    TruckSpawnPoint spawnerRef;

    void Awake()
    {
        returnsLeft = returnCyclesRequired;
        wanderTarget = transform.position;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);
    void OnDestroy() => spawnerRef?.NotifyBoxGone();

    void Update()
    {
        float dt = Time.deltaTime;

        if (Phase == BoxPhase.Flying)
        {
            transform.position += (Vector3)(flyVel * dt);
            heightVel -= gravity * dt;
            height += heightVel * dt;

            if (CheckZoneDeliveryMidAir()) return;

            if (height <= 0f)
            {
                height = 0f;
                if (boxType == BoxType.Bomb) { Explode(); return; }
                SetGroundPhase();
            }
        }
        else if (Phase == BoxPhase.Ground && boxType == BoxType.Creature)
        {
            UpdateCreatureWander(dt);
        }

        if (visualPivot != null)
            visualPivot.localPosition = new Vector3(0f, height * heightVisualScale, 0f);
    }

    bool CheckZoneDeliveryMidAir()
    {
        var zone = DeliveryZone.Instance;
        if (zone == null || !zone.Contains(transform.position)) return false;
        Deliver(ThrownBy);
        return true;
    }

    void UpdateCreatureWander(float dt)
    {
        wanderTimer -= dt;
        if (wanderTimer <= 0f)
        {
            Vector2 rnd = Random.insideUnitCircle * creatureWanderRadius;
            wanderTarget = (Vector2)transform.position + rnd;
            wanderTimer = Random.Range(1.5f, 3.5f);
        }
        Vector2 dir = wanderTarget - (Vector2)transform.position;
        if (dir.magnitude > 0.1f)
            transform.position += (Vector3)(dir.normalized * creatureWalkSpeed * dt);
    }

    public void OnSpawnedFromTruck(TruckSpawnPoint spawner) => spawnerRef = spawner;

    public void OnPickedUp(CharacterState holder)
    {
        CurrentHolder = holder;
        Phase = BoxPhase.Held;
        Transform socket = holder.handSocket != null ? holder.handSocket : holder.transform;
        transform.SetParent(socket);
        transform.localPosition = Vector3.zero;
    }

    public void Throw(Vector2 velocity, float upVel, CharacterState thrower)
    {
        thrower.ClearCarried();
        CurrentHolder = null;
        ThrownBy = thrower;
        Phase = BoxPhase.Flying;
        transform.SetParent(null);
        flyVel = velocity;
        height = 0.2f;
        heightVel = upVel;
    }

    /// <summary>밀쳐서(피격) 손에서 놓쳤을 때. 폭탄은 여기서 즉시 터집니다.</summary>
    public void ForceDrop(bool dueToImpact)
    {
        if (CurrentHolder != null) CurrentHolder.ClearCarried();
        CurrentHolder = null;
        transform.SetParent(null);

        if (boxType == BoxType.Bomb && dueToImpact)
        {
            Explode();
            return;
        }

        Phase = BoxPhase.Flying;
        flyVel = Random.insideUnitCircle * 1.2f;
        height = 0.2f;
        heightVel = 1.5f;
    }

    void SetGroundPhase()
    {
        Phase = BoxPhase.Ground;
        flyVel = Vector2.zero;
        height = 0f;
        heightVel = 0f;
    }

    void Explode()
    {
        foreach (var cs in CharacterState.All)
        {
            float d = Vector2.Distance(cs.transform.position, transform.position);
            if (d <= explosionRadius) cs.ApplyHit(transform.position);
        }
        if (explosionVfxPrefab != null)
            Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    /// <summary>걸어서 납품(PlayerController/CPUAgent) 또는 공중 납품(위 CheckZoneDeliveryMidAir)에서 호출.</summary>
    public void Deliver(CharacterState scorer)
    {
        if (boxType == BoxType.Return && returnsLeft > 0)
        {
            returnsLeft--;
            if (CurrentHolder != null) { CurrentHolder.ClearCarried(); CurrentHolder = null; }
            transform.SetParent(null);
            Phase = BoxPhase.Returning;
            StartCoroutine(WalkOutRoutine());
            return;
        }

        if (CurrentHolder != null) { CurrentHolder.ClearCarried(); CurrentHolder = null; }
        if (scorer != null) GameManager.Instance.AddScore(scorer);
        Destroy(gameObject);
    }

    IEnumerator WalkOutRoutine()
    {
        var zone = DeliveryZone.Instance;
        Vector2 dir = zone != null
            ? ((Vector2)transform.position - zone.Center)
            : Vector2.up;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
        dir.Normalize();

        Vector2 start = transform.position;
        Vector2 target = start + dir * returnWalkOutDistance;
        float t = 0f;
        while (t < returnWalkOutDuration)
        {
            t += Time.deltaTime;
            transform.position = Vector2.Lerp(start, target, t / returnWalkOutDuration);
            yield return null;
        }
        SetGroundPhase();
    }
}
