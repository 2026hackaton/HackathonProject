using UnityEngine;

namespace Camera
{
    public sealed class IsoFollowCamera : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private Transform target;

        [Header("View")]
        [SerializeField, Range(35f, 75f)] private float pitch = 45f;
        [SerializeField] private float yaw = 45f;
        [SerializeField] private float distance = 12f;
        [SerializeField] private float heightOffset = 1.2f;

        [Header("Follow")]
        [SerializeField] private float positionSmoothTime = 0.12f;
        [SerializeField] private float rotationLerpSpeed = 12f;

        private Vector3 _velocity;

        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            _velocity = Vector3.zero;
            if (snap)
                Snap();
        }

        void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<UnityEngine.Camera>();
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            var focusPoint = target.position + Vector3.up * heightOffset;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = focusPoint - rotation * Vector3.forward * distance;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _velocity,
                positionSmoothTime
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                rotation,
                rotationLerpSpeed * Time.deltaTime
            );
        }

        void Snap()
        {
            if (target == null)
                return;

            var focusPoint = target.position + Vector3.up * heightOffset;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);

            transform.position = focusPoint - rotation * Vector3.forward * distance;
            transform.rotation = rotation;
        }
    }
}