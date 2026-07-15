using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortMainMenuEffects : MonoBehaviour
    {
        [Header("Canvas UI Targets")]
        [SerializeField] private RectTransform logoTarget;
        [SerializeField] private RectTransform startButtonTarget;

        [Header("Logo Motion")]
        [SerializeField] private bool animateLogo = true;
        [SerializeField, Range(0f, 24f)] private float logoBobPixels = 7f;
        [SerializeField, Range(0f, 8f)] private float logoTiltDegrees = 1.5f;
        [SerializeField, Range(0.1f, 8f)] private float logoSpeed = 1.7f;

        [Header("Start Button Motion")]
        [SerializeField] private bool animateStartButton = true;
        [SerializeField, Range(0f, 0.12f)] private float buttonPulseScale = 0.025f;
        [SerializeField, Range(0f, 18f)] private float buttonHopPixels = 3f;
        [SerializeField, Range(0f, 8f)] private float buttonWobbleDegrees = 1.1f;
        [SerializeField, Range(0.1f, 10f)] private float buttonSpeed = 3.8f;
        [SerializeField, Min(0.5f)] private float buttonKickInterval = 2.6f;
        [SerializeField, Range(0.05f, 0.8f)] private float buttonKickDuration = 0.22f;

        private TransformSnapshot _logoBase;
        private TransformSnapshot _buttonBase;
        private bool _hasLogoBase;
        private bool _hasButtonBase;

        public void Configure(RectTransform logo, RectTransform startButton)
        {
            logoTarget = logo;
            startButtonTarget = startButton;
            CaptureBaseTransforms();
        }

        private void OnEnable()
        {
            CaptureBaseTransforms();
        }

        private void OnDisable()
        {
            RestoreBaseTransforms();
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            AnimateLogo(time);
            AnimateStartButton(time);
        }

        public void CaptureBaseTransforms()
        {
            if (logoTarget != null)
            {
                _logoBase = TransformSnapshot.From(logoTarget);
                _hasLogoBase = true;
            }

            if (startButtonTarget != null)
            {
                _buttonBase = TransformSnapshot.From(startButtonTarget);
                _hasButtonBase = true;
            }
        }

        public void RestoreBaseTransforms()
        {
            if (_hasLogoBase && logoTarget != null)
                _logoBase.ApplyTo(logoTarget);

            if (_hasButtonBase && startButtonTarget != null)
                _buttonBase.ApplyTo(startButtonTarget);
        }

        private void AnimateLogo(float time)
        {
            if (!animateLogo || logoTarget == null || !_hasLogoBase)
                return;

            float bob = Mathf.Sin(time * logoSpeed) * logoBobPixels;
            float tilt = Mathf.Sin(time * logoSpeed * 0.72f) * logoTiltDegrees;

            logoTarget.anchoredPosition = _logoBase.AnchoredPosition + new Vector2(0f, bob);
            logoTarget.localRotation = _logoBase.LocalRotation * Quaternion.Euler(0f, 0f, tilt);
        }

        private void AnimateStartButton(float time)
        {
            if (!animateStartButton || startButtonTarget == null || !_hasButtonBase)
                return;

            float pulse = 1f + Mathf.Sin(time * buttonSpeed) * buttonPulseScale;
            float wobble = Mathf.Sin(time * buttonSpeed * 0.87f) * buttonWobbleDegrees;
            float kick = GetKick01(time);
            float squashX = 1f + kick * 0.035f;
            float squashY = 1f - kick * 0.025f;
            float hop = Mathf.Abs(Mathf.Sin(time * buttonSpeed * 0.5f)) * buttonHopPixels + kick * 7f;

            startButtonTarget.anchoredPosition = _buttonBase.AnchoredPosition + new Vector2(0f, hop);
            startButtonTarget.localScale = new Vector3(
                _buttonBase.LocalScale.x * pulse * squashX,
                _buttonBase.LocalScale.y * pulse * squashY,
                _buttonBase.LocalScale.z);
            startButtonTarget.localRotation = _buttonBase.LocalRotation * Quaternion.Euler(0f, 0f, wobble + kick * 2.2f);
        }

        private float GetKick01(float time)
        {
            if (buttonKickInterval <= 0f || buttonKickDuration <= 0f)
                return 0f;

            float phase = Mathf.Repeat(time, buttonKickInterval);
            if (phase > buttonKickDuration)
                return 0f;

            return Mathf.Sin(phase / buttonKickDuration * Mathf.PI);
        }

        private readonly struct TransformSnapshot
        {
            public readonly Vector2 AnchoredPosition;
            public readonly Vector3 LocalScale;
            public readonly Quaternion LocalRotation;

            private TransformSnapshot(Vector2 anchoredPosition, Vector3 localScale, Quaternion localRotation)
            {
                AnchoredPosition = anchoredPosition;
                LocalScale = localScale;
                LocalRotation = localRotation;
            }

            public static TransformSnapshot From(RectTransform rect)
            {
                return new TransformSnapshot(rect.anchoredPosition, rect.localScale, rect.localRotation);
            }

            public void ApplyTo(RectTransform rect)
            {
                rect.anchoredPosition = AnchoredPosition;
                rect.localScale = LocalScale;
                rect.localRotation = LocalRotation;
            }
        }
    }
}
