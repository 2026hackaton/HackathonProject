using UnityEngine;

/// <summary>
/// 배송구역 (2D 버전). 트리거 이벤트를 아예 안 씁니다 —
/// Collider2D는 인스펙터에서 크기 조절하고 눈으로 확인하는 용도로만 쓰고,
/// 실제 판정은 Contains()로 좌표를 직접 비교합니다. Rigidbody2D도 필요 없고
/// Is Trigger 체크도 필요 없습니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DeliveryZone : MonoBehaviour
{
    public static DeliveryZone Instance { get; private set; }

    Collider2D col;

    public Vector2 Center => col.bounds.center;

    void Awake()
    {
        Instance = this;
        col = GetComponent<Collider2D>();
    }

    public bool Contains(Vector2 worldPos) => col.bounds.Contains(worldPos);
}
