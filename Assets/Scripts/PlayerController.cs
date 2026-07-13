using UnityEngine;

/// <summary>
/// 조작 (2D 버전):
///  - WASD: 월드 기준 이동
///  - 마우스: 화면 좌표를 월드 좌표로 변환해서 그 방향을 바라봄 (레이캐스트 필요 없음 —
///    2D라서 카메라가 이미 정면에서 내려다보고 있으니 ScreenToWorldPoint 한 줄이면 끝)
///  - 우클릭: 근처에 상자 있으면 줍기, 없으면 제자리 밀치기
///  - 좌클릭(누르고 있기): 상자를 들고 있을 때만 유효. 누르면 차징, 떼면 던짐
///
/// 대시 없음. 레이어/마스크 설정도 필요 없음 (BoxItem.All, CharacterState.All 사용).
/// </summary>
[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed = 4.2f;

    [Header("줍기 판정")]
    [SerializeField] float pickupRadius = 1.1f;

    [Header("차징 던지기")]
    [SerializeField] float chargeMax = 1.1f;
    [SerializeField] float throwSpeedMin = 5f;
    [SerializeField] float throwSpeedMax = 10f;
    [SerializeField] float throwUpMin = 3f;
    [SerializeField] float throwUpMax = 5.5f;

    Rigidbody2D rb;
    CharacterState state;
    Camera cam;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        state = GetComponent<CharacterState>();
        state.isPlayer = true;
        cam = Camera.main;
    }

    void Update()
    {
        UpdateAim();

        if (Input.GetMouseButtonDown(1)) HandleRightClickDown();
        if (Input.GetMouseButtonDown(0)) HandleLeftClickDown();
        if (Input.GetMouseButtonUp(0)) HandleLeftClickUp();

        if (state.IsCharging) state.ChargeTime += Time.deltaTime;

        // 상자를 들고 배송구역 안으로 걸어 들어왔는지 매 프레임 체크 (트리거 이벤트 없이)
        if (state.IsCarrying && DeliveryZone.Instance != null &&
            DeliveryZone.Instance.Contains(transform.position))
        {
            state.CarriedBox.Deliver(state);
        }
    }

    void FixedUpdate()
    {
        if (!state.CanAct) return;

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        Vector2 dir = new Vector2(x, y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        rb.linearVelocity = dir * moveSpeed * state.GetSpeedMultiplier();
    }

    void UpdateAim()
    {
        if (cam == null || !state.CanAct) return;
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = mouseWorld - (Vector2)transform.position;
        if (dir.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void HandleRightClickDown()
    {
        if (!state.CanAct || state.IsCarrying) return;

        BoxItem nearest = FindNearestBox();
        if (nearest != null) state.TryPickUp(nearest);
        else state.TryPush();
    }

    void HandleLeftClickDown()
    {
        if (!state.CanAct || !state.IsCarrying) return;
        state.IsCharging = true;
        state.ChargeTime = 0f;
    }

    void HandleLeftClickUp()
    {
        if (!state.IsCharging) return;
        state.IsCharging = false;
        ThrowCarriedBox();
    }

    void ThrowCarriedBox()
    {
        BoxItem box = state.CarriedBox;
        if (box == null) return;

        float t = Mathf.Clamp01(state.ChargeTime / chargeMax);
        float spd = Mathf.Lerp(throwSpeedMin, throwSpeedMax, t);
        float up = Mathf.Lerp(throwUpMin, throwUpMax, t);

        Vector2 dir = transform.right; // Z회전 0도 = +X를 바라보는 것으로 정의
        box.Throw(dir * spd, up, state);
    }

    BoxItem FindNearestBox()
    {
        BoxItem best = null;
        float bestD = pickupRadius;
        foreach (var box in BoxItem.All)
        {
            if (box.CurrentHolder != null) continue;
            float d = Vector2.Distance(box.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = box; }
        }
        return best;
    }
}
