using UnityEngine;

/// <summary>
/// CPU 행동 (2D 버전, 로직은 3D판과 동일):
///  - 상자가 없고 밀치기 쿨타임이 돌았는데 누군가 사거리 안에 있으면 무조건 밀친다.
///  - 아니면 가장 가까운 바닥 상자를 주우러 가고, 들고 있으면 배송구역으로 걸어간다.
/// CPU는 던지지 않고 항상 걸어서 납품합니다 (단순화).
/// </summary>
[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(Rigidbody2D))]
public class CPUAgent : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3.6f;
    [SerializeField] float pushRange = 1.2f;
    [SerializeField] float pickupRadius = 1.1f;

    Rigidbody2D rb;
    CharacterState state;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        state = GetComponent<CharacterState>();
        state.isPlayer = false;
    }

    void Update()
    {
        if (state.IsCarrying && DeliveryZone.Instance != null &&
            DeliveryZone.Instance.Contains(transform.position))
        {
            state.CarriedBox.Deliver(state);
        }
    }

    void FixedUpdate()
    {
        if (!state.CanAct) return; // 기절 중엔 물리 넉백에 맡김

        if (!state.IsCarrying && state.PushCooldownTimer <= 0f)
        {
            CharacterState nearest = FindNearestCharacter();
            if (nearest != null &&
                Vector2.Distance(nearest.transform.position, transform.position) <= pushRange)
            {
                FaceTarget(nearest.transform.position);
                state.TryPush();
                rb.linearVelocity = Vector2.zero;
                return;
            }
        }

        Vector2 moveDir = Vector2.zero;

        if (state.IsCarrying)
        {
            if (DeliveryZone.Instance != null)
                moveDir = SeekDirection(DeliveryZone.Instance.Center);
        }
        else
        {
            BoxItem target = FindNearestBox();
            if (target != null)
            {
                moveDir = SeekDirection(target.transform.position);
                if (Vector2.Distance(target.transform.position, transform.position) < pickupRadius)
                    state.TryPickUp(target);
            }
        }

        rb.linearVelocity = moveDir * moveSpeed * state.GetSpeedMultiplier();
        if (moveDir.sqrMagnitude > 0.01f) FaceTarget((Vector2)transform.position + moveDir);
    }

    Vector2 SeekDirection(Vector2 worldPos)
    {
        Vector2 dir = worldPos - (Vector2)transform.position;
        return dir.sqrMagnitude > 0.04f ? dir.normalized : Vector2.zero;
    }

    void FaceTarget(Vector2 worldPos)
    {
        Vector2 dir = worldPos - (Vector2)transform.position;
        if (dir.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    CharacterState FindNearestCharacter()
    {
        CharacterState best = null;
        float bestD = float.MaxValue;
        foreach (var cs in CharacterState.All)
        {
            if (cs == state) continue;
            float d = Vector2.Distance(cs.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = cs; }
        }
        return best;
    }

    BoxItem FindNearestBox()
    {
        BoxItem best = null;
        float bestD = float.MaxValue;
        foreach (var box in BoxItem.All)
        {
            if (box.CurrentHolder != null) continue;
            float d = Vector2.Distance(box.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = box; }
        }
        return best;
    }
}
