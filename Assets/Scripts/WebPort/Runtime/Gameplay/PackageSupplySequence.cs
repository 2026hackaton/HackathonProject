using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class PackageSupplySequence : MonoBehaviour
    {
        [Header("Supply Door")]
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField] private Vector3 leftDoorClosedLocalPosition;
        [SerializeField] private Vector3 rightDoorClosedLocalPosition;
        [SerializeField] private Vector3 leftDoorOpenLocalPosition = new(0f, 0f, -28f);
        [SerializeField] private Vector3 rightDoorOpenLocalPosition = new(0f, 0f, 28f);
        [SerializeField, Min(0.01f)] private float doorOpenSeconds = 0.16f;
        [SerializeField, Min(0.01f)] private float doorCloseSeconds = 0.18f;

        [Header("Truck")]
        [SerializeField] private Transform truck;
        [SerializeField] private bool flipTruckX;
        [SerializeField] private Vector3 truckHiddenLocalPosition = new(-180f, 28f, 0f);
        [SerializeField] private Vector3 truckActiveLocalPosition = new(-56f, 28f, 0f);
        [SerializeField, Min(0.01f)] private float truckMoveSeconds = 0.36f;
        [SerializeField, Min(0f)] private float truckShakeSeconds = 0.18f;
        [SerializeField, Min(0f)] private float truckShakeDistance = 5f;

        [Header("Package Drop")]
        [SerializeField] private Transform dropOrigin;
        [SerializeField, Min(0f)] private float dropHeight = 82f;
        [SerializeField, Min(0f)] private float planarJitter = 8f;
        [SerializeField, Min(0f)] private float packageIntervalSeconds = 0.28f;
        [SerializeField, Min(0f)] private float afterDropPauseSeconds = 0.24f;

        [Header("Notification")]
        [SerializeField] private PackageRefillNotification refillNotification;
        [SerializeField] private string refillMessage = "새로운 택배가 도착합니다!";

        private Coroutine _routine;
        private bool _isPlaying;

        public bool IsPlaying => _isPlaying;
        public float DropHeight => dropHeight;

        private void Awake()
        {
            ApplyInitialState();
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _isPlaying = false;
        }

        public void PlayInitialSupply()
        {
            PlaySupply(Array.Empty<Vector3>(), null, null, false);
        }

        public void PlayRefillSupply()
        {
            PlaySupply(Array.Empty<Vector3>(), null, null, true);
        }

        public bool PlayInitialSupply(IReadOnlyList<Vector3> packagePositions, Action<int, Vector3> spawnPackage, Action completed = null)
        {
            return PlaySupply(packagePositions, spawnPackage, completed, false);
        }

        public bool PlayRefillSupply(IReadOnlyList<Vector3> packagePositions, Action<int, Vector3> spawnPackage, Action completed = null)
        {
            return PlaySupply(packagePositions, spawnPackage, completed, true);
        }

        private bool PlaySupply(IReadOnlyList<Vector3> packagePositions, Action<int, Vector3> spawnPackage, Action completed, bool showNotification)
        {
            if (_isPlaying)
                return false;

            if (!HasRequiredReferences())
            {
                SpawnImmediately(packagePositions, spawnPackage);
                completed?.Invoke();
                return true;
            }

            if (showNotification)
                refillNotification?.ShowNotification(refillMessage);

            _routine = StartCoroutine(SupplyRoutine(packagePositions, spawnPackage, completed));
            return true;
        }

        private IEnumerator SupplyRoutine(IReadOnlyList<Vector3> packagePositions, Action<int, Vector3> spawnPackage, Action completed)
        {
            _isPlaying = true;
            ApplyInitialState();

            yield return MoveDoors(leftDoorClosedLocalPosition, leftDoorOpenLocalPosition, rightDoorClosedLocalPosition, rightDoorOpenLocalPosition, doorOpenSeconds);
            yield return MoveLocal(truck, truckHiddenLocalPosition, truckActiveLocalPosition, truckMoveSeconds);
            if (truckShakeSeconds > 0f && truckShakeDistance > 0f)
                yield return ShakeTruck();

            int count = packagePositions?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 basePosition = packagePositions[i];
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * planarJitter;
                Vector3 dropPosition = new(basePosition.x + jitter.x, dropHeight, basePosition.z + jitter.y);
                spawnPackage?.Invoke(i, dropPosition);
                if (packageIntervalSeconds > 0f)
                    yield return new WaitForSeconds(packageIntervalSeconds);
            }

            if (afterDropPauseSeconds > 0f)
                yield return new WaitForSeconds(afterDropPauseSeconds);

            yield return MoveLocal(truck, truckActiveLocalPosition, truckHiddenLocalPosition, truckMoveSeconds);
            truck.gameObject.SetActive(false);
            yield return MoveDoors(leftDoorOpenLocalPosition, leftDoorClosedLocalPosition, rightDoorOpenLocalPosition, rightDoorClosedLocalPosition, doorCloseSeconds);

            _isPlaying = false;
            _routine = null;
            completed?.Invoke();
        }

        private void ApplyInitialState()
        {
            if (leftDoor != null)
                leftDoor.localPosition = leftDoorClosedLocalPosition;
            if (rightDoor != null)
                rightDoor.localPosition = rightDoorClosedLocalPosition;
            if (truck != null)
            {
                truck.localPosition = truckHiddenLocalPosition;
                truck.gameObject.SetActive(false);
                Vector3 scale = truck.localScale;
                scale.x = Mathf.Abs(scale.x) * (flipTruckX ? -1f : 1f);
                truck.localScale = scale;
            }
        }

        private bool HasRequiredReferences()
        {
            if (leftDoor == null || rightDoor == null || truck == null)
            {
                Debug.LogError($"{nameof(PackageSupplySequence)} requires left/right supply doors and a truck transform.", this);
                return false;
            }

            return true;
        }

        private static void SpawnImmediately(IReadOnlyList<Vector3> packagePositions, Action<int, Vector3> spawnPackage)
        {
            int count = packagePositions?.Count ?? 0;
            for (int i = 0; i < count; i++)
                spawnPackage?.Invoke(i, packagePositions[i]);
        }

        private IEnumerator MoveDoors(Vector3 leftFrom, Vector3 leftTo, Vector3 rightFrom, Vector3 rightTo, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(elapsed / seconds));
                leftDoor.localPosition = Vector3.LerpUnclamped(leftFrom, leftTo, t);
                rightDoor.localPosition = Vector3.LerpUnclamped(rightFrom, rightTo, t);
                yield return null;
            }

            leftDoor.localPosition = leftTo;
            rightDoor.localPosition = rightTo;
        }

        private static IEnumerator MoveLocal(Transform target, Vector3 from, Vector3 to, float seconds)
        {
            target.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(elapsed / seconds));
                target.localPosition = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            target.localPosition = to;
        }

        private IEnumerator ShakeTruck()
        {
            Vector3 origin = truck.localPosition;
            float elapsed = 0f;
            while (elapsed < truckShakeSeconds)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / truckShakeSeconds);
                float wave = Mathf.Sin(p * Mathf.PI * 4f) * (1f - p);
                truck.localPosition = origin + Vector3.forward * wave * truckShakeDistance;
                yield return null;
            }

            truck.localPosition = origin;
        }

        private static float EaseOut(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }
    }
}
