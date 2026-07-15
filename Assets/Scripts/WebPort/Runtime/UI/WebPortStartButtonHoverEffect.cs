using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class WebPortStartButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Targets")]
        [SerializeField] private RectTransform visualTarget;
        [SerializeField] private Graphic buttonGraphic;
        [SerializeField] private Graphic[] textGraphics;
        [SerializeField] private bool disableChildRaycastTargets = true;

        [Header("Hover Motion")]
        [SerializeField] private bool animateHover = true;
        [SerializeField, Min(1f)] private float hoverPixels = 16f;
        [SerializeField, Min(1f)] private float hoverScalePercent = 12f;
        [SerializeField, Min(0.01f)] private float hoverInDuration = 0.11f;
        [SerializeField, Min(0.01f)] private float hoverOutDuration = 0.10f;

        [Header("Press Motion")]
        [SerializeField, Min(0f)] private float pressedPixels = 5f;
        [SerializeField, Min(0f)] private float pressedScalePercent = 5f;

        [Header("Idle Motion")]
        [SerializeField] private bool animateIdleMotion = true;
        [SerializeField, Min(0f)] private float idleBobPixels = 4f;
        [SerializeField, Min(0f)] private float idleScalePercent = 2f;
        [SerializeField, Min(0f)] private float idleTiltDegrees = 1.2f;
        [SerializeField, Min(0.1f)] private float idleSpeed = 2.8f;

        [Header("Colors")]
        [SerializeField] private bool animateColors = true;
        [SerializeField] private Color hoverButtonColor = new(1f, 0.94f, 0.62f, 1f);
        [SerializeField] private Color hoverTextColor = new(1f, 0.88f, 0.18f, 1f);

        private Button _button;
        private Vector2 _basePosition;
        private Vector3 _baseScale = Vector3.one;
        private Quaternion _baseRotation = Quaternion.identity;
        private Color _baseButtonColor = Color.white;
        private Color[] _baseTextColors;
        private float _current;
        private float _target;
        private bool _pressed;
        private bool _captured;

        private void Reset()
        {
            _button = GetComponent<Button>();
            visualTarget = GetComponent<RectTransform>();
            buttonGraphic = _button != null ? _button.targetGraphic : GetComponent<Graphic>();
            textGraphics = GetComponentsInChildren<Graphic>(true);
        }

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            if (visualTarget == null)
                visualTarget = GetComponent<RectTransform>();

            if (buttonGraphic == null)
                buttonGraphic = _button != null ? _button.targetGraphic : GetComponent<Graphic>();

            if (textGraphics == null || textGraphics.Length == 0)
                textGraphics = GetChildTextGraphics();

            DisableChildRaycastTargetsIfNeeded();
            CaptureBaseState();
            ApplyState(0f);
        }

        private void OnEnable()
        {
            CaptureBaseState();
            _target = 0f;
            _current = 0f;
            _pressed = false;
            ApplyState(0f);
        }

        private void OnDisable()
        {
            RestoreBaseState();
            _target = 0f;
            _current = 0f;
            _pressed = false;
        }

        private void Update()
        {
            if (!animateHover || !_captured)
                return;

            float duration = _target > _current ? hoverInDuration : hoverOutDuration;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
            _current = Mathf.MoveTowards(_current, _target, step);
            ApplyState(_current);
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);

        public void OnPointerExit(PointerEventData eventData)
        {
            _pressed = false;
            SetHovered(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            ApplyState(_current);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            ApplyState(_current);
        }

        public void OnSelect(BaseEventData eventData) => SetHovered(true);

        public void OnDeselect(BaseEventData eventData)
        {
            _pressed = false;
            SetHovered(false);
        }

        public void Configure(RectTransform target, Graphic graphic)
        {
            visualTarget = target != null ? target : GetComponent<RectTransform>();
            buttonGraphic = graphic != null ? graphic : buttonGraphic;
            textGraphics = GetChildTextGraphics();
            DisableChildRaycastTargetsIfNeeded();
            CaptureBaseState();
            ApplyState(_current);
        }

        private void SetHovered(bool hovered)
        {
            if (_button != null && !_button.interactable)
                hovered = false;

            _target = hovered ? 1f : 0f;
        }

        private void CaptureBaseState()
        {
            if (visualTarget != null)
            {
                _basePosition = visualTarget.anchoredPosition;
                _baseScale = visualTarget.localScale;
                _baseRotation = visualTarget.localRotation;
            }

            if (buttonGraphic != null)
                _baseButtonColor = buttonGraphic.color;

            if (textGraphics == null)
                textGraphics = GetChildTextGraphics();

            _baseTextColors = new Color[textGraphics != null ? textGraphics.Length : 0];
            for (int i = 0; i < _baseTextColors.Length; i++)
                _baseTextColors[i] = textGraphics[i] != null ? textGraphics[i].color : Color.white;

            _captured = true;
        }

        private void RestoreBaseState()
        {
            if (!_captured)
                return;

            if (visualTarget != null)
            {
                visualTarget.anchoredPosition = _basePosition;
                visualTarget.localScale = _baseScale;
                visualTarget.localRotation = _baseRotation;
            }

            if (buttonGraphic != null)
                buttonGraphic.color = _baseButtonColor;

            if (textGraphics == null || _baseTextColors == null)
                return;

            int count = Mathf.Min(textGraphics.Length, _baseTextColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (textGraphics[i] != null)
                    textGraphics[i].color = _baseTextColors[i];
            }
        }

        private void ApplyState(float amount)
        {
            if (!_captured)
                return;

            float eased = EaseOutBack(amount);
            float pressOffset = _pressed ? pressedPixels : 0f;
            float pressScale = _pressed ? pressedScalePercent / 100f : 0f;

            if (visualTarget != null)
            {
                float idleTime = Time.unscaledTime * idleSpeed;
                float idleBob = animateIdleMotion ? Mathf.Sin(idleTime) * idleBobPixels : 0f;
                float idleScale = animateIdleMotion ? Mathf.Max(0f, Mathf.Sin(idleTime * 1.37f)) * idleScalePercent / 100f : 0f;
                float idleTilt = animateIdleMotion ? Mathf.Sin(idleTime * 0.83f) * idleTiltDegrees : 0f;

                visualTarget.anchoredPosition = _basePosition + new Vector2(0f, idleBob + hoverPixels * eased - pressOffset);
                float scale = 1f + idleScale + hoverScalePercent / 100f * eased - pressScale;
                visualTarget.localScale = new Vector3(_baseScale.x * scale, _baseScale.y * scale, _baseScale.z);
                visualTarget.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, idleTilt);
            }

            if (!animateColors)
                return;

            if (buttonGraphic != null)
                buttonGraphic.color = Color.Lerp(_baseButtonColor, hoverButtonColor, amount);

            if (textGraphics == null || _baseTextColors == null)
                return;

            int count = Mathf.Min(textGraphics.Length, _baseTextColors.Length);
            for (int i = 0; i < count; i++)
            {
                Graphic textGraphic = textGraphics[i];
                if (textGraphic != null)
                    textGraphic.color = Color.Lerp(_baseTextColors[i], hoverTextColor, amount);
            }
        }

        private Graphic[] GetChildTextGraphics()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            if (graphics == null || graphics.Length == 0)
                return new Graphic[0];

            int count = 0;
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null && graphics[i] != buttonGraphic)
                    count++;
            }

            Graphic[] result = new Graphic[count];
            int index = 0;
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null && graphics[i] != buttonGraphic)
                    result[index++] = graphics[i];
            }

            return result;
        }

        private void DisableChildRaycastTargetsIfNeeded()
        {
            if (!disableChildRaycastTargets || textGraphics == null)
                return;

            for (int i = 0; i < textGraphics.Length; i++)
            {
                if (textGraphics[i] != null)
                    textGraphics[i].raycastTarget = false;
            }
        }

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float overshoot = 1.18f;
            float p = t - 1f;
            return 1f + p * p * ((overshoot + 1f) * p + overshoot);
        }
    }
}
