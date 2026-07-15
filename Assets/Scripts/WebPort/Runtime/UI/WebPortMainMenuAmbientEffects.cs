using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    [DisallowMultipleComponent]
    public sealed class WebPortMainMenuAmbientEffects : MonoBehaviour
    {
        [Header("Doorway Flicker")]
        [SerializeField] private bool enableDoorwayFlicker = true;
        [SerializeField] private Vector2 doorwayPosition = new(-20f, -86f);
        [SerializeField] private Vector2 doorwaySize = new(130f, 210f);
        [SerializeField] private Color doorwayColor = new(0.015f, 0.035f, 0.045f, 0.34f);
        [SerializeField, Range(0f, 1f)] private float doorwayPulseAlpha = 0.13f;
        [SerializeField, Min(0.1f)] private float doorwayPulseSpeed = 1.4f;

        [Header("Dust / Paper")]
        [SerializeField] private bool enableDust = true;
        [SerializeField, Range(0, 64)] private int particleCount = 22;
        [SerializeField] private Vector2 particleYRange = new(-244f, -136f);
        [SerializeField] private Vector2 particleSpeedRange = new(8f, 26f);
        [SerializeField] private Vector2 particleSizeRange = new(2f, 6f);
        [SerializeField] private Color dustColor = new(0.92f, 0.86f, 0.72f, 0.34f);
        [SerializeField] private Color paperColor = new(1f, 0.98f, 0.86f, 0.50f);

        private const int RandomSeed = 9173;

        private RectTransform _rect;
        private Image _doorwayImage;
        private readonly Particle[] _particles = new Particle[64];
        private int _activeParticleCount;

        public void Configure(
            bool doorwayFlicker,
            Vector2 doorPosition,
            Vector2 doorSize,
            bool dust,
            int dustCount)
        {
            enableDoorwayFlicker = doorwayFlicker;
            doorwayPosition = doorPosition;
            doorwaySize = doorSize;
            enableDust = dust;
            particleCount = Mathf.Clamp(dustCount, 0, _particles.Length);
            Rebuild();
        }

        private void Awake()
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            Rebuild();
        }

        private void OnDestroy()
        {
            _doorwayImage = null;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] != null)
                    _particles[i].Clear();
            }
        }

        private void Update()
        {
            if (_rect == null)
                return;

            UpdateDoorway();
            UpdateParticles();
        }

        private void Rebuild()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            if (_rect == null)
                return;

            Stretch(_rect);
            RebuildDoorway();
            RebuildParticles();
        }

        private void RebuildDoorway()
        {
            if (!enableDoorwayFlicker)
            {
                if (_doorwayImage != null)
                    _doorwayImage.gameObject.SetActive(false);
                return;
            }

            if (_doorwayImage == null)
            {
                GameObject obj = new("Doorway Flicker");
                obj.transform.SetParent(transform, false);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
                RectTransform rect = obj.AddComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                _doorwayImage = obj.AddComponent<Image>();
                _doorwayImage.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor;
                _doorwayImage.raycastTarget = false;
            }

            _doorwayImage.gameObject.SetActive(true);
            RectTransform doorwayRect = _doorwayImage.rectTransform;
            doorwayRect.anchoredPosition = doorwayPosition;
            doorwayRect.sizeDelta = doorwaySize;
        }

        private void RebuildParticles()
        {
            _activeParticleCount = enableDust ? Mathf.Clamp(particleCount, 0, _particles.Length) : 0;
            System.Random random = new(RandomSeed);

            Rect canvasRect = _rect.rect;
            float halfWidth = Mathf.Max(1f, canvasRect.width * 0.5f);
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] == null)
                    _particles[i] = new Particle();

                if (i >= _activeParticleCount)
                {
                    _particles[i].SetActive(false);
                    continue;
                }

                if (_particles[i].Image == null)
                    _particles[i].Create(transform, i);

                _particles[i].Reset(
                    random,
                    -halfWidth,
                    halfWidth,
                    particleYRange,
                    particleSpeedRange,
                    particleSizeRange,
                    i % 5 == 0 ? paperColor : dustColor);
            }
        }

        private void UpdateDoorway()
        {
            if (!enableDoorwayFlicker || _doorwayImage == null)
                return;

            RectTransform doorwayRect = _doorwayImage.rectTransform;
            doorwayRect.anchoredPosition = doorwayPosition;
            doorwayRect.sizeDelta = doorwaySize;

            float time = Time.unscaledTime * doorwayPulseSpeed;
            float pulse = Mathf.Sin(time) * 0.5f + 0.5f;
            float irregular = Mathf.Sin(time * 2.73f + 1.1f) * 0.5f + 0.5f;
            Color color = doorwayColor;
            color.a = Mathf.Clamp01(doorwayColor.a + doorwayPulseAlpha * pulse * irregular);
            _doorwayImage.color = color;
        }

        private void UpdateParticles()
        {
            if (!enableDust || _activeParticleCount <= 0)
                return;

            Rect canvasRect = _rect.rect;
            float halfWidth = Mathf.Max(1f, canvasRect.width * 0.5f);
            float left = -halfWidth - 24f;
            float right = halfWidth + 24f;

            for (int i = 0; i < _activeParticleCount; i++)
            {
                if (_particles[i] == null)
                    continue;

                _particles[i].Update(Time.unscaledDeltaTime, left, right, particleYRange);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private sealed class Particle
        {
            public Image Image { get; private set; }

            private RectTransform _rect;
            private float _speed;
            private float _bobPhase;
            private float _bobSpeed;
            private float _bobPixels;
            private float _rotationSpeed;

            public void Create(Transform parent, int index)
            {
                GameObject obj = new($"Dust {index + 1:00}");
                obj.transform.SetParent(parent, false);
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
                _rect = obj.AddComponent<RectTransform>();
                _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
                Image = obj.AddComponent<Image>();
                Image.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor;
                Image.raycastTarget = false;
            }

            public void Reset(
                System.Random random,
                float left,
                float right,
                Vector2 yRange,
                Vector2 speedRange,
                Vector2 sizeRange,
                Color color)
            {
                SetActive(true);
                float size = Lerp(sizeRange.x, sizeRange.y, Next01(random));
                _rect.sizeDelta = new Vector2(size, Mathf.Max(1f, size * Lerp(0.55f, 1.25f, Next01(random))));
                _rect.anchoredPosition = new Vector2(
                    Lerp(left, right, Next01(random)),
                    Lerp(yRange.x, yRange.y, Next01(random)));
                _rect.localRotation = Quaternion.Euler(0f, 0f, Lerp(-18f, 18f, Next01(random)));
                Image.color = color;

                _speed = Lerp(speedRange.x, speedRange.y, Next01(random));
                _bobPhase = Lerp(0f, Mathf.PI * 2f, Next01(random));
                _bobSpeed = Lerp(1.2f, 2.4f, Next01(random));
                _bobPixels = Lerp(2f, 8f, Next01(random));
                _rotationSpeed = Lerp(-18f, 18f, Next01(random));
            }

            public void Update(float deltaTime, float left, float right, Vector2 yRange)
            {
                if (Image == null)
                    return;

                Vector2 position = _rect.anchoredPosition;
                position.x += _speed * deltaTime;
                position.y += Mathf.Sin(Time.unscaledTime * _bobSpeed + _bobPhase) * _bobPixels * deltaTime;

                if (position.x > right)
                {
                    position.x = left;
                    position.y = Lerp(yRange.x, yRange.y, Mathf.Repeat(_bobPhase * 0.137f + Time.unscaledTime * 0.03f, 1f));
                }

                _rect.anchoredPosition = position;
                _rect.Rotate(0f, 0f, _rotationSpeed * deltaTime);
            }

            public void SetActive(bool active)
            {
                if (Image != null)
                    Image.gameObject.SetActive(active);
            }

            public void Clear()
            {
                Image = null;
                _rect = null;
            }

            private static float Next01(System.Random random) => (float)random.NextDouble();
            private static float Lerp(float a, float b, float t) => a + (b - a) * Mathf.Clamp01(t);
        }
    }
}
