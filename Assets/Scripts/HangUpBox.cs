using UnityEngine;

public class HangUpBox : MonoBehaviour
{
    [Header("택배가 위치할 곳")]
    public Transform holdPoint;

    [Header("사정거리")]
    [SerializeField] private float grabRange = 2f;

    [SerializeField] private LayerMask boxLayer;

    // Setter를 오픈하여 PlayerInput에서 변경할 수 있거나 내부에서 다루도록 합니다.
    public GameObject currentBox { get; private set; } = null;
    
    public void TryGrabBox()
    {
        // 🔴 3D Physics.OverlapSphere 대신 2D Physics2D.OverlapCircle 사용
        // 검사 위치도 캐릭터 중심(transform.position)으로 변경하여 주변을 다 체크합니다.
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, grabRange, boxLayer);

        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Box"))
            {
                currentBox = hit.gameObject;
                
                // 🔴 3D Rigidbody 대신 2D Rigidbody2D 사용
                Rigidbody2D boxRb = currentBox.GetComponent<Rigidbody2D>();
                if (boxRb != null)
                {
                    boxRb.bodyType = RigidbodyType2D.Kinematic; 
                    // 🔴 2D에서는 useGravity 대신 simulated를 끄거나 velocity를 0으로 만듭니다.
                    boxRb.linearVelocity = Vector2.zero; 
                }
                currentBox.transform.SetParent(holdPoint);
                currentBox.transform.localPosition = Vector3.zero;
                currentBox.transform.localRotation = Quaternion.identity;

                Debug.Log("택배를 들었습니다!");
                break;
            }
        }
    }
    
    public void ReleaseBox()
    {
        if (currentBox == null) return;
        
        currentBox.transform.SetParent(null);
        
        // 🔴 3D Rigidbody 대신 2D Rigidbody2D 사용
        Rigidbody2D boxRb = currentBox.GetComponent<Rigidbody2D>();
        if (boxRb != null)
        {
            boxRb.bodyType = RigidbodyType2D.Dynamic;
            
            // 🔴 2D 던지기 구현 (오른쪽으로 던진다고 가정, 대전 상대 방향으로 변경 가능)
            // 3D의 ForceMode.Impulse 대신 2D는 바로 힘을 더하거나 속도를 줍니다.
            boxRb.AddForce(Vector2.right * 5f, ForceMode2D.Impulse);
        }

        currentBox = null;
        Debug.Log("택배를 놓았습니다!");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        // 🔴 기즈모도 2D 원(WireDiscard -> WireSphere를 쓰되 중심점 변경)으로 매칭
        Gizmos.DrawWireSphere(transform.position, grabRange);
    }
}
