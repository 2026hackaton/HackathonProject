using UnityEngine;

/// <summary>
/// 오소그래픽 카메라가 플레이어를 부드럽게 따라가게 합니다.
/// Main Camera의 Projection을 Orthographic으로 설정해두세요.
/// </summary>
public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smooth = 8f;
    [SerializeField] float zOffset = -10f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = new Vector3(target.position.x, target.position.y, zOffset);
        transform.position = Vector3.Lerp(
            transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }
}
