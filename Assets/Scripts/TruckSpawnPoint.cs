using UnityEngine;

/// <summary>
/// 트럭 위치에서 상자를 주기적으로 스폰합니다 (2D 버전, 로직은 3D판과 동일).
/// </summary>
public class TruckSpawnPoint : MonoBehaviour
{
    [Header("타입별 상자 프리팹")]
    [SerializeField] GameObject normalBoxPrefab;
    [SerializeField] GameObject bombBoxPrefab;
    [SerializeField] GameObject returnBoxPrefab;
    [SerializeField] GameObject creatureBoxPrefab;

    [Header("스폰 확률 가중치 (합이 100이 아니어도 자동 정규화됩니다)")]
    [SerializeField] float normalWeight = 70f;
    [SerializeField] float bombWeight = 10f;
    [SerializeField] float returnWeight = 10f;
    [SerializeField] float creatureWeight = 10f;

    [Header("스폰 규칙")]
    [SerializeField] int maxBoxesAlive = 3;
    [SerializeField] float respawnDelay = 1.1f;
    [SerializeField] Vector2 spawnAreaSize = new Vector2(2f, 2f);

    float timer;
    int aliveCount;

    void Update()
    {
        if (aliveCount >= maxBoxesAlive) return;
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SpawnOne();
            timer = respawnDelay;
        }
    }

    void SpawnOne()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null) return;

        Vector2 pos = (Vector2)transform.position + new Vector2(
            Random.Range(-spawnAreaSize.x, spawnAreaSize.x),
            Random.Range(-spawnAreaSize.y, spawnAreaSize.y));

        var go = Instantiate(prefab, pos, Quaternion.identity);
        aliveCount++;

        var box = go.GetComponent<BoxItem>();
        if (box != null) box.OnSpawnedFromTruck(this);
    }

    public void NotifyBoxGone() => aliveCount = Mathf.Max(0, aliveCount - 1);

    GameObject PickPrefab()
    {
        float total = normalWeight + bombWeight + returnWeight + creatureWeight;
        if (total <= 0f) return normalBoxPrefab;

        float r = Random.Range(0f, total);
        if ((r -= normalWeight) < 0f) return normalBoxPrefab;
        if ((r -= bombWeight) < 0f) return bombBoxPrefab;
        if ((r -= returnWeight) < 0f) return returnBoxPrefab;
        return creatureBoxPrefab;
    }
}
