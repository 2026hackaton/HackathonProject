using System.Collections;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class DeliveryDoorController : MonoBehaviour
    {
        [System.Serializable]
        public sealed class DeliveryDoorEntry
        {
            public Transform leftDoor;
            public Transform rightDoor;
            public Vector3 leftDoorClosedLocalPosition;
            public Vector3 rightDoorClosedLocalPosition;
            public Vector3 leftDoorOpenLocalPosition = new(0f, 0f, -24f);
            public Vector3 rightDoorOpenLocalPosition = new(0f, 0f, 24f);
            public Transform conveyor;
            public Vector3 conveyorHiddenLocalPosition;
            public Vector3 conveyorActiveLocalPosition = new(-36f, 2f, 0f);
            public Collider deliveryTrigger;
        }

        [SerializeField] private DeliveryDoorEntry[] doors = new DeliveryDoorEntry[3];
        [SerializeField, Min(0.01f)] private float doorOpenSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float doorCloseSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float conveyorMoveSeconds = 0.28f;

        private Coroutine _transitionRoutine;
        private int _activeIndex = -1;
        private int _readyIndex = -1;

        public int ActiveIndex => _activeIndex;

        private void Awake()
        {
            CloseAllDeliveryDoors();
        }

        private void OnDisable()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }
        }

        public void SetActiveDeliveryDoor(int targetIndex)
        {
            if (targetIndex < 0)
            {
                CloseAllDeliveryDoors();
                return;
            }

            if (targetIndex >= doors.Length)
            {
                Debug.LogError($"{nameof(DeliveryDoorController)} received invalid target index {targetIndex}.", this);
                return;
            }

            if (targetIndex == _activeIndex && targetIndex == _readyIndex)
                return;

            if (_transitionRoutine != null)
                StopCoroutine(_transitionRoutine);

            _transitionRoutine = StartCoroutine(TransitionRoutine(targetIndex));
        }

        public void CloseAllDeliveryDoors()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            for (int i = 0; i < doors.Length; i++)
                ApplyClosed(doors[i]);

            _activeIndex = -1;
            _readyIndex = -1;
        }

        public bool IsDeliveryReady(int targetIndex)
        {
            return targetIndex >= 0 && targetIndex == _readyIndex;
        }

        private IEnumerator TransitionRoutine(int targetIndex)
        {
            _readyIndex = -1;

            for (int i = 0; i < doors.Length; i++)
            {
                DeliveryDoorEntry entry = doors[i];
                if (entry?.deliveryTrigger != null)
                    entry.deliveryTrigger.enabled = false;
            }

            if (_activeIndex >= 0 && _activeIndex < doors.Length && _activeIndex != targetIndex)
            {
                DeliveryDoorEntry previous = doors[_activeIndex];
                yield return MoveConveyor(previous, previous.conveyorActiveLocalPosition, previous.conveyorHiddenLocalPosition);
                yield return MoveDoors(previous, previous.leftDoorOpenLocalPosition, previous.leftDoorClosedLocalPosition, previous.rightDoorOpenLocalPosition, previous.rightDoorClosedLocalPosition, doorCloseSeconds);
                ApplyClosed(previous);
            }

            for (int i = 0; i < doors.Length; i++)
            {
                if (i != targetIndex)
                    ApplyClosed(doors[i]);
            }

            DeliveryDoorEntry next = doors[targetIndex];
            if (!IsEntryUsable(next))
            {
                Debug.LogError($"{nameof(DeliveryDoorController)} target door {targetIndex} is missing required references.", this);
                _transitionRoutine = null;
                yield break;
            }

            yield return MoveDoors(next, next.leftDoorClosedLocalPosition, next.leftDoorOpenLocalPosition, next.rightDoorClosedLocalPosition, next.rightDoorOpenLocalPosition, doorOpenSeconds);
            yield return MoveConveyor(next, next.conveyorHiddenLocalPosition, next.conveyorActiveLocalPosition);

            if (next.deliveryTrigger != null)
                next.deliveryTrigger.enabled = true;

            _activeIndex = targetIndex;
            _readyIndex = targetIndex;
            _transitionRoutine = null;
        }

        private void ApplyClosed(DeliveryDoorEntry entry)
        {
            if (entry == null)
                return;

            if (entry.leftDoor != null)
                entry.leftDoor.localPosition = entry.leftDoorClosedLocalPosition;
            if (entry.rightDoor != null)
                entry.rightDoor.localPosition = entry.rightDoorClosedLocalPosition;
            if (entry.conveyor != null)
            {
                entry.conveyor.localPosition = entry.conveyorHiddenLocalPosition;
                entry.conveyor.gameObject.SetActive(true);
            }
            if (entry.deliveryTrigger != null)
                entry.deliveryTrigger.enabled = false;
        }

        private static bool IsEntryUsable(DeliveryDoorEntry entry)
        {
            return entry != null && entry.leftDoor != null && entry.rightDoor != null && entry.conveyor != null;
        }

        private IEnumerator MoveConveyor(DeliveryDoorEntry entry, Vector3 from, Vector3 to)
        {
            if (entry?.conveyor == null)
                yield break;

            yield return MoveLocal(entry.conveyor, from, to, conveyorMoveSeconds);
        }

        private IEnumerator MoveDoors(DeliveryDoorEntry entry, Vector3 leftFrom, Vector3 leftTo, Vector3 rightFrom, Vector3 rightTo, float seconds)
        {
            if (entry?.leftDoor == null || entry.rightDoor == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = EaseInOut(Mathf.Clamp01(elapsed / seconds));
                entry.leftDoor.localPosition = Vector3.LerpUnclamped(leftFrom, leftTo, t);
                entry.rightDoor.localPosition = Vector3.LerpUnclamped(rightFrom, rightTo, t);
                yield return null;
            }

            entry.leftDoor.localPosition = leftTo;
            entry.rightDoor.localPosition = rightTo;
        }

        private static IEnumerator MoveLocal(Transform target, Vector3 from, Vector3 to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = EaseInOut(Mathf.Clamp01(elapsed / seconds));
                target.localPosition = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            target.localPosition = to;
        }

        private static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
