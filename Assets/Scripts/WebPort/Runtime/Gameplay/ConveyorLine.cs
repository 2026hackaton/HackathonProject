using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    [DisallowMultipleComponent]
    public sealed class ConveyorLine : MonoBehaviour
    {
        [Header("Box")]
        [SerializeField] private GameObject boxPrefab;
        [SerializeField, Min(1)] private int poolSize = 6;
        [SerializeField, Min(0.01f)] private float boxSpeed = 30f;
        [SerializeField, Min(0f)] private float spawnIntervalSeconds = 1.2f;
        [SerializeField] private float surfaceOffset = 4f;
        [SerializeField, Min(0f)] private float lateralJitter = 0f;
        [SerializeField] private bool alignRotationToDirection = true;
        [SerializeField] private bool reverseDirection;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private readonly Queue<GameObject> _inactiveBoxes = new();
        private readonly List<Transform> _activeBoxes = new();

        private Transform _boxPoolRoot;
        private Vector3 _start;
        private Vector3 _end;
        private bool _hasPath;
        private float _spawnTimer;

        private void Awake()
        {
            RecalculatePath();
            BuildPool();
        }

        private void OnDisable()
        {
            for (int i = 0; i < _activeBoxes.Count; i++)
            {
                if (_activeBoxes[i] == null)
                    continue;

                _activeBoxes[i].gameObject.SetActive(false);
                _inactiveBoxes.Enqueue(_activeBoxes[i].gameObject);
            }

            _activeBoxes.Clear();
            _spawnTimer = 0f;
        }

        private void Update()
        {
            if (!_hasPath || boxPrefab == null)
                return;

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnIntervalSeconds)
            {
                _spawnTimer -= spawnIntervalSeconds;
                TrySpawnBox();
            }

            for (int i = _activeBoxes.Count - 1; i >= 0; i--)
            {
                Transform box = _activeBoxes[i];
                Vector3 next = Vector3.MoveTowards(box.position, _end, boxSpeed * Time.deltaTime);
                box.position = next;

                if (next == _end)
                {
                    box.gameObject.SetActive(false);
                    _inactiveBoxes.Enqueue(box.gameObject);
                    _activeBoxes.RemoveAt(i);
                }
            }
        }

        private void BuildPool()
        {
            if (boxPrefab == null)
                return;

            GameObject poolRoot = new("Boxes (Pooled)");
            _boxPoolRoot = poolRoot.transform;
            _boxPoolRoot.SetParent(transform, false);

            for (int i = 0; i < poolSize; i++)
            {
                GameObject box = Instantiate(boxPrefab, _boxPoolRoot);
                box.SetActive(false);
                _inactiveBoxes.Enqueue(box);
            }
        }

        private void TrySpawnBox()
        {
            if (_inactiveBoxes.Count == 0)
                return;

            Vector3 direction = (_end - _start).normalized;
            Vector3 position = _start;
            if (lateralJitter > 0f)
            {
                Vector3 lateral = Vector3.Cross(direction, Vector3.up);
                position += lateral * Random.Range(-lateralJitter, lateralJitter);
            }

            GameObject box = _inactiveBoxes.Dequeue();
            box.transform.SetPositionAndRotation(
                position,
                alignRotationToDirection ? Quaternion.LookRotation(direction, Vector3.up) : boxPrefab.transform.rotation);
            box.SetActive(true);

            _activeBoxes.Add(box.transform);
        }

        [ContextMenu("Recalculate Path")]
        private void RecalculatePath()
        {
            _hasPath = TryComputeBeltBounds(out Bounds bounds);
            if (_hasPath)
                ComputeEndpoints(bounds, out _start, out _end);
        }

        private bool TryComputeBeltBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
            foreach (Renderer renderer in renderers)
            {
                if (_boxPoolRoot != null && renderer.transform.IsChildOf(_boxPoolRoot))
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private void ComputeEndpoints(Bounds bounds, out Vector3 start, out Vector3 end)
        {
            bool longAxisIsX = bounds.size.x >= bounds.size.z;
            float top = bounds.max.y + surfaceOffset;

            Vector3 a, b;
            if (longAxisIsX)
            {
                a = new Vector3(bounds.min.x, top, bounds.center.z);
                b = new Vector3(bounds.max.x, top, bounds.center.z);
            }
            else
            {
                a = new Vector3(bounds.center.x, top, bounds.min.z);
                b = new Vector3(bounds.center.x, top, bounds.max.z);
            }

            start = reverseDirection ? b : a;
            end = reverseDirection ? a : b;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !TryComputeBeltBounds(out Bounds bounds))
                return;

            ComputeEndpoints(bounds, out Vector3 start, out Vector3 end);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(start, 6f);
            Gizmos.DrawLine(start, end);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(end, 6f);
        }
    }
}
