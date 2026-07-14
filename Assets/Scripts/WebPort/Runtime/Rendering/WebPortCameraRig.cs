using UnityEngine;

namespace Hackathon.WebPort
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class WebPortCameraRig : MonoBehaviour
    {
        private static readonly Vector3 Offset = new(0f, 140f, 420f);

        [SerializeField] private Transform target;
        [SerializeField] private float focusHeight = 25f;

        private UnityEngine.Camera _camera;

        public UnityEngine.Camera Camera => _camera;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _camera.fieldOfView = 38f;
            ApplyBackground();
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 2000f;
        }

        private void ApplyBackground()
        {
            WebPortVisualConfig config = WebPortVisuals.Config;
            if (config.skyboxMaterial != null)
            {
                RenderSettings.skybox = config.skyboxMaterial;
                _camera.clearFlags = CameraClearFlags.Skybox;
                return;
            }

            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = WebPortVisuals.PageBackground;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            Snap();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 focus = target.position;
            transform.position = focus + Offset;
            transform.LookAt(focus + Vector3.up * focusHeight);
        }

        public void Snap()
        {
            if (target == null)
                return;

            Vector3 focus = target.position;
            transform.position = focus + Offset;
            transform.LookAt(focus + Vector3.up * focusHeight);
        }
    }
}
