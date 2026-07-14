using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class WebPortBoxStartButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private enum ButtonRectImageSizing
        {
            StretchToButtonRect,
            FitInsideButtonRect,
            FillButtonRect,
        }

        [Header("Button")]
        [SerializeField] private Button button;
        [SerializeField] private bool closeWhenPointerExits = true;
        [SerializeField] private bool keepOpenWhenSelected = true;
        [SerializeField] private bool bringToFrontOnEnable = true;

        [Header("Animator")]
        [SerializeField] private Animator boxAnimator;
        [SerializeField] private bool preferAnimator = true;
        [SerializeField] private bool controlAnimatorPlayback = true;
        [SerializeField] private string openStateName = "BoxOpen_Animation";
        [SerializeField] private string closeStateName = "BoxClose_Animation";
        [SerializeField, Min(0.01f)] private float fallbackAnimatorPlaybackDuration = 0.58f;
        [SerializeField] private string openBoolParameter = "IsOpen";
        [SerializeField] private string hoverTriggerParameter = "Hover";
        [SerializeField] private string pressTriggerParameter = "Press";

        [Header("Procedural Box Parts")]
        [SerializeField] private RectTransform boxRoot;
        [SerializeField] private RectTransform lidTransform;
        [SerializeField] private RectTransform labelRoot;
        [SerializeField] private CanvasGroup labelCanvasGroup;
        [SerializeField] private bool animateLabelWithCode = true;
        [SerializeField] private bool animateLidWithCode;

        [Header("Sprite Frames")]
        [SerializeField] private Image boxImage;
        [SerializeField] private SpriteRenderer sourceSpriteRenderer;
        [SerializeField] private bool copySpriteRendererFrameToImage = true;
        [SerializeField] private bool hideSourceSpriteRenderer = true;
        [SerializeField] private bool playSpriteFrames = true;
        [SerializeField, Min(1f)] private float spriteFramesPerSecond = 18f;
        [SerializeField] private bool resizeImageToSprite = true;
        [SerializeField] private bool sizeImageFromButtonRect = true;
        [SerializeField] private ButtonRectImageSizing buttonRectSizing = ButtonRectImageSizing.StretchToButtonRect;
        [SerializeField, Min(0.01f)] private float spriteSizeMultiplier = 1f;
        [SerializeField] private Vector2 spriteSizePadding;
        [SerializeField] private bool preserveImageAspect = true;
        [SerializeField] private bool lockImageSizeDuringSpriteAnimation = true;
        [SerializeField] private bool resetImageScaleOnSpriteFrame = true;
        [SerializeField] private bool centerChildImageOnLayout = true;
        [SerializeField] private bool stretchChildImageToButtonRect = true;
        [SerializeField] private bool normalizeTrimmedSpriteFrames = true;
        [SerializeField] private bool compositeFramesOverClosedSprite = true;
        [SerializeField] private Vector2Int normalizedSpriteCanvasSize = new(64, 64);
        [SerializeField] private Vector2 normalizedSpriteReferenceSize = new(64f, 16f);
        [SerializeField] private Vector2 normalizedSpritePivot = new(0.5f, 0.25f);
        [SerializeField] private Sprite sizeReferenceSprite;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openSprite;
        [SerializeField] private Sprite[] openFrames = new Sprite[0];
        [SerializeField] private Sprite[] closeFrames = new Sprite[0];

        [Header("Idle Motion")]
        [SerializeField] private bool animateIdleMotion = false;
        [SerializeField, Range(0f, 20f)] private float idleBobPixels = 4f;
        [SerializeField, Range(0f, 8f)] private float idleTiltDegrees = 1.2f;
        [SerializeField, Range(0f, 0.12f)] private float idlePulseScale = 0.018f;
        [SerializeField, Range(0.1f, 10f)] private float idleMotionSpeed = 2.7f;
        [SerializeField] private bool preventScaleBelowBase = true;

        [Header("Closed Pose")]
        [SerializeField] private Vector2 lidClosedAnchoredPosition;
        [SerializeField] private Vector3 lidClosedRotation;
        [SerializeField] private Vector3 lidClosedScale = Vector3.one;
        [SerializeField] private Vector2 labelHiddenAnchoredPosition = new(0f, -8f);
        [SerializeField] private Vector3 labelHiddenScale = new(0.92f, 0.92f, 1f);

        [Header("Open Pose")]
        [SerializeField] private Vector2 lidOpenAnchoredPosition = new(0f, 34f);
        [SerializeField] private Vector3 lidOpenRotation = new(0f, 0f, -10f);
        [SerializeField] private Vector3 lidOpenScale = new(1.02f, 1.02f, 1f);
        [SerializeField] private Vector2 labelVisibleAnchoredPosition = new(0f, 28f);
        [SerializeField] private Vector3 labelVisibleScale = Vector3.one;
        [SerializeField] private bool autoSetLabelVisiblePosition = true;
        [SerializeField, Min(0f)] private float labelRisePixels = 64f;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float openDuration = 0.16f;
        [SerializeField, Min(0.01f)] private float closeDuration = 0.11f;
        [SerializeField, Range(0f, 0.5f)] private float labelDelay = 0.12f;
        [SerializeField, Min(0.01f)] private float labelMoveDuration = 0.18f;
        [SerializeField, Range(0f, 0.2f)] private float pressSquash = 0.045f;

        [Header("Optional Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip pressSound;

        private Coroutine _transitionRoutine;
        private Coroutine _spriteRoutine;
        private Coroutine _animatorPlaybackRoutine;
        private Vector2 _buttonBaseSize;
        private Vector2 _lockedSpriteAnimationSize;
        private Vector2 _lockedButtonVisualSize;
        private Vector2 _lastAppliedImageSize;
        private Sprite _lastLayoutSprite;
        private Sprite _lastSyncedRendererSprite;
        private readonly Dictionary<Sprite, Sprite> _normalizedSpriteCache = new();
        private readonly Dictionary<Sprite, Sprite> _compositedFrameSpriteCache = new();
        private bool _hasLockedSpriteAnimationSize;
        private bool _hasLockedButtonVisualSize;
        private Vector2 _boxBaseAnchoredPosition;
        private Quaternion _boxBaseRotation = Quaternion.identity;
        private Vector3 _boxBaseScale = Vector3.one;
        private bool _isPointerOver;
        private bool _isSelected;
        private bool _isPressed;
        private bool _isOpen;
        private bool _isAnimatorPlaybackActive;
        private bool _hasButtonBaseSize;
        private bool _hasCapturedPose;
        private bool _warnedMissingOpenParameter;
        private bool _warnedMissingHoverParameter;
        private bool _warnedMissingPressParameter;
        private bool _warnedMissingRaycastGraphic;
        private bool _warnedSharedRootAndImage;

        private void Reset()
        {
            button = GetComponent<Button>();
            boxImage = GetComponent<Image>();
            boxRoot = GetComponent<RectTransform>();
        }

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (boxRoot == null)
                boxRoot = GetComponent<RectTransform>();

            if (boxImage == null)
                boxImage = GetComponent<Image>();

            CaptureButtonBaseSize();

            if (sourceSpriteRenderer == null)
                sourceSpriteRenderer = GetComponent<SpriteRenderer>();

            if (hideSourceSpriteRenderer && sourceSpriteRenderer != null)
                sourceSpriteRenderer.enabled = false;

            if (controlAnimatorPlayback && boxAnimator != null)
                boxAnimator.enabled = false;

            WarnIfRootAndImageAreSame();
            EnsurePointerRaycastTarget();
            CaptureSpriteDefaults();
            DisableAnimatorWhenUsingDirectFrames();
            ApplySprite(boxImage != null ? boxImage.sprite : null);

            if (labelRoot != null && labelCanvasGroup == null)
                labelCanvasGroup = labelRoot.GetComponent<CanvasGroup>();

            if (labelRoot != null && labelCanvasGroup == null)
                labelCanvasGroup = labelRoot.gameObject.AddComponent<CanvasGroup>();

            if (bringToFrontOnEnable)
                transform.SetAsLastSibling();

            CaptureInitialPoseIfNeeded();
            ApplyClosedPoseImmediate();
        }

        private void OnEnable()
        {
            if (bringToFrontOnEnable)
                transform.SetAsLastSibling();
        }

        private void OnDisable()
        {
            StopTransition();
            StopSpriteRoutine();
            StopAnimatorPlayback();
            _isPointerOver = false;
            _isSelected = false;
            _isPressed = false;
            _isOpen = false;
            ApplyAnimatorOpen(false);
            ApplyClosedSpriteImmediate();
            ApplyClosedPoseImmediate();
            RestoreBoxRootPose();
        }

        private void Update()
        {
            AnimateBoxRootIdle();
        }

        private void LateUpdate()
        {
            DisableAnimatorWhenUsingDirectFrames();
            HideSourceRendererIfNeeded();
            SyncSpriteRendererFrameToImage();
            EnforceCurrentImageLayout();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver = true;
            Open();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            if (closeWhenPointerExits && (!keepOpenWhenSelected || !_isSelected))
                Close();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            PlayOneShot(pressSound);
            SetAnimatorTrigger(pressTriggerParameter);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected = true;
            Open();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            if (!_isPointerOver)
                Close();
        }

        public void Open()
        {
            if (!CanInteract())
                return;

            if (_isOpen)
                return;

            _isOpen = true;
            BringLabelToFront();
            PlayOneShot(openSound);
            if (HasDirectSpriteFrames(true))
            {
                StopAnimatorPlayback();
                PlaySpriteTransition(true);
            }
            else
            {
                SetAnimatorTrigger(hoverTriggerParameter);
                ApplyAnimatorOpen(true);
                PlayControlledAnimatorState(true);
            }

            if (ShouldPlayProceduralTransition())
                PlayProceduralTransition(true);
        }

        public void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            PlayOneShot(closeSound);
            if (HasDirectSpriteFrames(false))
            {
                StopAnimatorPlayback();
                PlaySpriteTransition(false);
            }
            else
            {
                ApplyAnimatorOpen(false);
                PlayControlledAnimatorState(false);
            }

            if (ShouldPlayProceduralTransition())
                PlayProceduralTransition(false);
        }

        public void ApplyClosedPoseImmediate()
        {
            if (animateLidWithCode && lidTransform != null)
            {
                lidTransform.anchoredPosition = lidClosedAnchoredPosition;
                lidTransform.localRotation = Quaternion.Euler(lidClosedRotation);
                lidTransform.localScale = lidClosedScale;
            }

            if (animateLabelWithCode && labelRoot != null)
            {
                labelRoot.anchoredPosition = labelHiddenAnchoredPosition;
                labelRoot.localScale = labelHiddenScale;
            }

            if (animateLabelWithCode && labelCanvasGroup != null)
            {
                labelCanvasGroup.alpha = 0f;
                labelCanvasGroup.interactable = false;
                labelCanvasGroup.blocksRaycasts = false;
            }
        }

        private bool CanInteract()
        {
            return button == null || button.interactable;
        }

        private bool ShouldUseAnimator()
        {
            return preferAnimator && boxAnimator != null;
        }

        private bool ShouldPlayProceduralTransition()
        {
            return animateLidWithCode && lidTransform != null || animateLabelWithCode && labelRoot != null;
        }

        private void CaptureInitialPoseIfNeeded()
        {
            if (_hasCapturedPose)
                return;

            if (boxRoot != null)
            {
                _boxBaseAnchoredPosition = boxRoot.anchoredPosition;
                _boxBaseRotation = boxRoot.localRotation;
                _boxBaseScale = boxRoot.localScale;
            }

            if (lidTransform != null)
            {
                lidClosedAnchoredPosition = lidTransform.anchoredPosition;
                lidClosedRotation = lidTransform.localEulerAngles;
                lidClosedScale = lidTransform.localScale;
            }

            if (labelRoot != null)
            {
                labelHiddenAnchoredPosition = labelRoot.anchoredPosition;
                labelHiddenScale = labelRoot.localScale;
                if (autoSetLabelVisiblePosition)
                    labelVisibleAnchoredPosition = labelHiddenAnchoredPosition + new Vector2(0f, labelRisePixels);
            }

            _hasCapturedPose = true;
        }

        private void PlayProceduralTransition(bool opening)
        {
            StopTransition();
            _transitionRoutine = StartCoroutine(TransitionRoutine(opening));
        }

        private IEnumerator TransitionRoutine(bool opening)
        {
            CaptureInitialPoseIfNeeded();

            float boxDuration = opening ? openDuration : closeDuration;
            float duration = opening
                ? Mathf.Max(boxDuration, labelDelay + labelMoveDuration)
                : Mathf.Max(boxDuration, labelMoveDuration);
            float elapsed = 0f;

            Vector2 lidFromPosition = lidTransform != null ? lidTransform.anchoredPosition : Vector2.zero;
            Quaternion lidFromRotation = lidTransform != null ? lidTransform.localRotation : Quaternion.identity;
            Vector3 lidFromScale = lidTransform != null ? lidTransform.localScale : Vector3.one;

            Vector2 labelFromPosition = labelRoot != null ? labelRoot.anchoredPosition : Vector2.zero;
            Vector3 labelFromScale = labelRoot != null ? labelRoot.localScale : Vector3.one;
            float labelFromAlpha = labelCanvasGroup != null ? labelCanvasGroup.alpha : 0f;

            Vector2 lidToPosition = opening ? lidOpenAnchoredPosition : lidClosedAnchoredPosition;
            Quaternion lidToRotation = Quaternion.Euler(opening ? lidOpenRotation : lidClosedRotation);
            Vector3 lidToScale = opening ? lidOpenScale : lidClosedScale;

            Vector2 labelToPosition = opening ? labelVisibleAnchoredPosition : labelHiddenAnchoredPosition;
            Vector3 labelToScale = opening ? labelVisibleScale : labelHiddenScale;
            float labelToAlpha = opening ? 1f : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / boxDuration);
                float eased = EaseOutBackLight(t);
                float labelT = opening
                    ? Mathf.Clamp01((elapsed - labelDelay) / labelMoveDuration)
                    : Mathf.Clamp01(elapsed / labelMoveDuration);
                float labelEased = Smooth01(labelT);

                if (animateLidWithCode && lidTransform != null)
                {
                    lidTransform.anchoredPosition = Vector2.LerpUnclamped(lidFromPosition, lidToPosition, eased);
                    lidTransform.localRotation = Quaternion.SlerpUnclamped(lidFromRotation, lidToRotation, eased);
                    lidTransform.localScale = Vector3.LerpUnclamped(lidFromScale, lidToScale, eased);
                }

                if (animateLabelWithCode && labelRoot != null)
                {
                    labelRoot.anchoredPosition = Vector2.LerpUnclamped(labelFromPosition, labelToPosition, labelEased);
                    labelRoot.localScale = Vector3.LerpUnclamped(labelFromScale, labelToScale, labelEased);
                }

                if (animateLabelWithCode && labelCanvasGroup != null)
                    labelCanvasGroup.alpha = Mathf.Lerp(labelFromAlpha, labelToAlpha, labelEased);

                yield return null;
            }

            if (animateLabelWithCode && labelCanvasGroup != null)
            {
                labelCanvasGroup.alpha = labelToAlpha;
                labelCanvasGroup.interactable = false;
                labelCanvasGroup.blocksRaycasts = false;
            }

            _transitionRoutine = null;
        }

        private void StopTransition()
        {
            if (_transitionRoutine == null)
                return;

            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        private void PlaySpriteTransition(bool opening)
        {
            if (!playSpriteFrames || boxImage == null)
                return;

            Sprite[] frames = opening ? openFrames : closeFrames;
            Sprite fallback = opening ? openSprite : closedSprite;
            Sprite finalSprite = GetFinalFrameSprite(frames, fallback);

            StopSpriteRoutine();
            CaptureLockedSpriteAnimationSize(finalSprite != null ? finalSprite : fallback);

            if (frames != null && frames.Length > 0)
            {
                _spriteRoutine = StartCoroutine(SpriteFrameRoutine(frames, finalSprite));
                return;
            }

            if (finalSprite != null)
                ApplySpriteFrame(finalSprite);

            ClearLockedAnimationSizes();
        }

        private bool HasDirectSpriteFrames(bool opening)
        {
            if (!playSpriteFrames || boxImage == null)
                return false;

            Sprite[] frames = opening ? openFrames : closeFrames;
            Sprite fallback = opening ? openSprite : closedSprite;
            return frames != null && frames.Length > 0 || fallback != null;
        }

        private bool HasAnyDirectSpriteFrames()
        {
            return playSpriteFrames
                && boxImage != null
                && ((openFrames != null && openFrames.Length > 0)
                    || (closeFrames != null && closeFrames.Length > 0)
                    || openSprite != null
                    || closedSprite != null);
        }

        private void DisableAnimatorWhenUsingDirectFrames()
        {
            if (!HasAnyDirectSpriteFrames() || boxAnimator == null)
                return;

            if (boxAnimator.enabled)
                boxAnimator.enabled = false;

            _isAnimatorPlaybackActive = false;
        }

        private void HideSourceRendererIfNeeded()
        {
            if (!hideSourceSpriteRenderer || sourceSpriteRenderer == null)
                return;

            if (sourceSpriteRenderer.enabled)
                sourceSpriteRenderer.enabled = false;
        }

        private IEnumerator SpriteFrameRoutine(Sprite[] frames, Sprite finalSprite)
        {
            float delay = 1f / Mathf.Max(1f, spriteFramesPerSecond);

            for (int i = 0; i < frames.Length; i++)
            {
                Sprite frame = frames[i];
                if (frame != null)
                    ApplySpriteFrame(frame);

                yield return new WaitForSecondsRealtime(delay);
            }

            if (finalSprite != null)
                ApplySpriteFrame(finalSprite);

            ClearLockedAnimationSizes();
            EnforceCurrentImageLayout();
            _spriteRoutine = null;
        }

        private void StopSpriteRoutine()
        {
            if (_spriteRoutine == null)
                return;

            StopCoroutine(_spriteRoutine);
            _spriteRoutine = null;
            ClearLockedAnimationSizes();
        }

        private void CaptureSpriteDefaults()
        {
            if (boxImage == null)
                return;

            if (closedSprite == null)
                closedSprite = boxImage.sprite;

            if (openSprite == null && openFrames != null && openFrames.Length > 0)
                openSprite = openFrames[openFrames.Length - 1];
        }

        private void ApplyClosedSpriteImmediate()
        {
            if (boxImage == null || closedSprite == null)
                return;

            ApplySprite(closedSprite);
        }

        private void ApplySprite(Sprite sprite)
        {
            if (boxImage == null || sprite == null)
                return;

            Sprite displaySprite = GetDisplaySprite(sprite, false);
            boxImage.sprite = displaySprite;
            ApplyImageLayout(displaySprite, true);
        }

        private void ApplySpriteFrame(Sprite sprite)
        {
            if (boxImage == null || sprite == null)
                return;

            Sprite displaySprite = GetDisplaySprite(sprite, true);
            boxImage.sprite = displaySprite;
            ApplyImageLayout(displaySprite, true);
        }

        private void EnforceCurrentImageLayout()
        {
            if (boxImage == null || boxImage.sprite == null || !resizeImageToSprite)
                return;

            ApplyImageLayout(boxImage.sprite, false);
        }

        private void SyncSpriteRendererFrameToImage()
        {
            if (!copySpriteRendererFrameToImage || sourceSpriteRenderer == null || boxImage == null)
                return;
            if (controlAnimatorPlayback && !_isAnimatorPlaybackActive)
                return;

            Sprite sourceSprite = sourceSpriteRenderer.sprite;
            if (sourceSprite == null || _lastSyncedRendererSprite == sourceSprite)
                return;

            Sprite displaySprite = GetDisplaySprite(sourceSprite, false);
            boxImage.sprite = displaySprite;
            ApplyImageLayout(displaySprite, true);
            _lastSyncedRendererSprite = sourceSprite;
        }

        private void ApplyImageLayout(Sprite sprite, bool force)
        {
            if (boxImage == null || sprite == null)
                return;

            boxImage.preserveAspect = preserveImageAspect && (!sizeImageFromButtonRect || buttonRectSizing != ButtonRectImageSizing.StretchToButtonRect);
            if (resetImageScaleOnSpriteFrame)
                boxImage.rectTransform.localScale = Vector3.one;

            if (!resizeImageToSprite)
                return;

            RectTransform imageRect = boxImage.rectTransform;
            if (StretchChildImageIfNeeded(imageRect))
                return;

            CenterChildImageIfNeeded(imageRect);
            Vector2 spriteSize = _hasLockedSpriteAnimationSize
                ? _lockedSpriteAnimationSize
                : sizeImageFromButtonRect
                    ? GetButtonBasedSpriteSize(sprite)
                    : sprite.rect.size;
            Vector2 finalSize = spriteSize * spriteSizeMultiplier + spriteSizePadding;
            if (!force && _lastLayoutSprite == sprite && Approximately(_lastAppliedImageSize, finalSize))
                return;

            imageRect.sizeDelta = finalSize;
            _lastLayoutSprite = sprite;
            _lastAppliedImageSize = finalSize;
        }

        private bool StretchChildImageIfNeeded(RectTransform imageRect)
        {
            if (imageRect == null || boxRoot == null || imageRect == boxRoot || !sizeImageFromButtonRect)
                return false;

            bool shouldStretchChild = stretchChildImageToButtonRect || buttonRectSizing == ButtonRectImageSizing.StretchToButtonRect;
            if (!shouldStretchChild)
                return false;

            boxImage.type = Image.Type.Simple;
            boxImage.useSpriteMesh = false;
            boxImage.preserveAspect = false;
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = ShouldNormalizeSpriteFrames() ? normalizedSpritePivot : new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = GetNormalizedVisualOffset();
            imageRect.localScale = Vector3.one;
            imageRect.sizeDelta = _hasLockedButtonVisualSize ? _lockedButtonVisualSize : GetButtonVisualSize();
            _lastLayoutSprite = boxImage.sprite;
            _lastAppliedImageSize = imageRect.sizeDelta;
            return true;
        }

        private void CenterChildImageIfNeeded(RectTransform imageRect)
        {
            if (!centerChildImageOnLayout || imageRect == null || boxRoot == null || imageRect == boxRoot)
                return;

            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
        }

        private void CaptureLockedSpriteAnimationSize(Sprite fallback)
        {
            ClearLockedAnimationSizes();
            if (!lockImageSizeDuringSpriteAnimation || boxImage == null)
                return;

            if (buttonRectSizing == ButtonRectImageSizing.StretchToButtonRect && sizeImageFromButtonRect)
            {
                _lockedButtonVisualSize = GetButtonVisualSize();
                _hasLockedButtonVisualSize = true;
            }

            Sprite sizeSprite = GetSizeReferenceSprite(fallback != null ? fallback : boxImage.sprite);
            if (sizeSprite == null)
                return;

            _lockedSpriteAnimationSize = sizeImageFromButtonRect
                ? GetButtonBasedSpriteSize(sizeSprite)
                : sizeSprite.rect.size;
            _hasLockedSpriteAnimationSize = true;
        }

        private void ClearLockedAnimationSizes()
        {
            _hasLockedSpriteAnimationSize = false;
            _hasLockedButtonVisualSize = false;
        }

        private void PlayControlledAnimatorState(bool opening)
        {
            if (!controlAnimatorPlayback || boxAnimator == null)
                return;

            string stateName = opening ? openStateName : closeStateName;
            if (string.IsNullOrWhiteSpace(stateName))
                return;

            StopAnimatorPlayback();
            _animatorPlaybackRoutine = StartCoroutine(ControlledAnimatorRoutine(stateName));
        }

        private IEnumerator ControlledAnimatorRoutine(string stateName)
        {
            _isAnimatorPlaybackActive = true;
            if (boxAnimator != null)
            {
                boxAnimator.enabled = true;
                boxAnimator.Play(stateName, 0, 0f);
                boxAnimator.Update(0f);
            }

            float duration = GetAnimatorClipLength(stateName);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (boxAnimator != null)
            {
                boxAnimator.Update(0f);
                boxAnimator.enabled = false;
            }

            SyncSpriteRendererFrameToImage();
            EnforceCurrentImageLayout();
            _isAnimatorPlaybackActive = false;
            _animatorPlaybackRoutine = null;
        }

        private void StopAnimatorPlayback()
        {
            if (_animatorPlaybackRoutine != null)
            {
                StopCoroutine(_animatorPlaybackRoutine);
                _animatorPlaybackRoutine = null;
            }

            _isAnimatorPlaybackActive = false;
            if (controlAnimatorPlayback && boxAnimator != null)
                boxAnimator.enabled = false;
        }

        private float GetAnimatorClipLength(string stateName)
        {
            if (boxAnimator == null || boxAnimator.runtimeAnimatorController == null)
                return fallbackAnimatorPlaybackDuration;

            AnimationClip[] clips = boxAnimator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && clip.name == stateName)
                    return Mathf.Max(0.01f, clip.length);
            }

            return fallbackAnimatorPlaybackDuration;
        }

        private Vector2 GetButtonBasedSpriteSize(Sprite sprite)
        {
            Vector2 targetSize = _buttonBaseSize;
            targetSize.x = Mathf.Abs(targetSize.x);
            targetSize.y = Mathf.Abs(targetSize.y);
            if (targetSize.x <= 0.01f || targetSize.y <= 0.01f)
                return sprite.rect.size;

            if (buttonRectSizing == ButtonRectImageSizing.StretchToButtonRect || !preserveImageAspect)
                return targetSize;

            Sprite referenceSprite = GetSizeReferenceSprite(sprite);
            Vector2 spriteSize = referenceSprite.rect.size;
            float spriteAspect = spriteSize.x / Mathf.Max(1f, spriteSize.y);
            float targetAspect = targetSize.x / Mathf.Max(1f, targetSize.y);

            if (buttonRectSizing == ButtonRectImageSizing.FillButtonRect)
            {
                if (spriteAspect > targetAspect)
                    return new Vector2(targetSize.y * spriteAspect, targetSize.y);

                return new Vector2(targetSize.x, targetSize.x / spriteAspect);
            }

            if (spriteAspect > targetAspect)
                return new Vector2(targetSize.x, targetSize.x / spriteAspect);

            return new Vector2(targetSize.y * spriteAspect, targetSize.y);
        }

        private Sprite GetSizeReferenceSprite(Sprite fallback)
        {
            if (sizeReferenceSprite != null)
                return sizeReferenceSprite;
            if (closedSprite != null)
                return closedSprite;
            if (openSprite != null)
                return openSprite;
            if (openFrames != null && openFrames.Length > 0 && openFrames[0] != null)
                return openFrames[0];

            return fallback;
        }

        private Sprite GetFinalFrameSprite(Sprite[] frames, Sprite fallback)
        {
            if (frames != null)
            {
                for (int i = frames.Length - 1; i >= 0; i--)
                {
                    if (frames[i] != null)
                        return frames[i];
                }
            }

            return fallback;
        }

        private Vector2 GetButtonVisualSize()
        {
            Vector2 size = GetCurrentButtonRectSize();
            if (ShouldNormalizeSpriteFrames())
            {
                float referenceWidth = Mathf.Max(1f, normalizedSpriteReferenceSize.x);
                float referenceHeight = Mathf.Max(1f, normalizedSpriteReferenceSize.y);
                size = new Vector2(
                    size.x * Mathf.Max(1, normalizedSpriteCanvasSize.x) / referenceWidth,
                    size.y * Mathf.Max(1, normalizedSpriteCanvasSize.y) / referenceHeight);
            }

            return size * spriteSizeMultiplier + spriteSizePadding;
        }

        private Vector2 GetNormalizedVisualOffset()
        {
            if (!ShouldNormalizeSpriteFrames())
                return Vector2.zero;

            Vector2 buttonSize = GetCurrentButtonRectSize();
            float canvasHeight = Mathf.Max(1, normalizedSpriteCanvasSize.y);
            float referenceHeight = Mathf.Max(1f, normalizedSpriteReferenceSize.y);
            float normalizedReferenceCenter = normalizedSpritePivot.y - referenceHeight / (2f * canvasHeight);
            return new Vector2(0f, normalizedReferenceCenter * buttonSize.y * canvasHeight / referenceHeight);
        }

        private Vector2 GetCurrentButtonRectSize()
        {
            RectTransform sourceRect = boxRoot != null ? boxRoot : GetComponent<RectTransform>();
            if (sourceRect == null)
                return _hasButtonBaseSize ? _buttonBaseSize : Vector2.zero;

            Vector2 size = sourceRect.rect.size;
            if (size.x <= 0.01f || size.y <= 0.01f)
                size = sourceRect.sizeDelta;

            size.x = Mathf.Abs(size.x);
            size.y = Mathf.Abs(size.y);
            if (size.x <= 0.01f || size.y <= 0.01f)
                return _hasButtonBaseSize ? _buttonBaseSize : Vector2.zero;

            return size;
        }

        private Sprite GetDisplaySprite(Sprite source, bool animationFrame)
        {
            if (!ShouldNormalizeSpriteFrames() || source == null)
                return source;

            bool compositeFrame = animationFrame && compositeFramesOverClosedSprite && closedSprite != null && source != closedSprite;
            Dictionary<Sprite, Sprite> cache = compositeFrame ? _compositedFrameSpriteCache : _normalizedSpriteCache;
            if (cache.TryGetValue(source, out Sprite cached) && cached != null)
                return cached;

            Sprite normalized = CreateNormalizedSprite(source, compositeFrame ? closedSprite : null);
            cache[source] = normalized != null ? normalized : source;
            return cache[source];
        }

        private bool ShouldNormalizeSpriteFrames()
        {
            return normalizeTrimmedSpriteFrames;
        }

        private Sprite CreateNormalizedSprite(Sprite source, Sprite background)
        {
            int canvasWidth = Mathf.Max(1, normalizedSpriteCanvasSize.x);
            int canvasHeight = Mathf.Max(1, normalizedSpriteCanvasSize.y);
            Texture2D texture = new(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
            {
                name = $"{source.name}_UIFrame",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Color[] clear = new Color[canvasWidth * canvasHeight];
            texture.SetPixels(clear);
            texture.Apply(false, false);

            if (background != null)
                CopySpriteIntoCanvas(background, texture, canvasWidth, canvasHeight);

            if (!CopySpriteIntoCanvas(source, texture, canvasWidth, canvasHeight))
            {
                Destroy(texture);
                return null;
            }

            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, canvasWidth, canvasHeight),
                normalizedSpritePivot,
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = background != null ? $"{source.name}_CompositeUIFrame" : $"{source.name}_UIFrame";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private bool CopySpriteIntoCanvas(Sprite source, Texture2D texture, int canvasWidth, int canvasHeight)
        {
            Rect sourceRect = source.rect;
            Vector2 pivotPixels = new(normalizedSpritePivot.x * canvasWidth, normalizedSpritePivot.y * canvasHeight);
            int sourceX = Mathf.RoundToInt(sourceRect.x);
            int sourceY = Mathf.RoundToInt(sourceRect.y);
            int sourceWidth = Mathf.RoundToInt(sourceRect.width);
            int sourceHeight = Mathf.RoundToInt(sourceRect.height);
            int destinationX = Mathf.RoundToInt(pivotPixels.x - source.pivot.x);
            int destinationY = Mathf.RoundToInt(pivotPixels.y - source.pivot.y);

            int clippedSourceX = sourceX;
            int clippedSourceY = sourceY;
            int clippedDestinationX = destinationX;
            int clippedDestinationY = destinationY;
            int clippedWidth = sourceWidth;
            int clippedHeight = sourceHeight;

            if (clippedDestinationX < 0)
            {
                int offset = -clippedDestinationX;
                clippedSourceX += offset;
                clippedWidth -= offset;
                clippedDestinationX = 0;
            }

            if (clippedDestinationY < 0)
            {
                int offset = -clippedDestinationY;
                clippedSourceY += offset;
                clippedHeight -= offset;
                clippedDestinationY = 0;
            }

            if (clippedDestinationX + clippedWidth > canvasWidth)
                clippedWidth = canvasWidth - clippedDestinationX;
            if (clippedDestinationY + clippedHeight > canvasHeight)
                clippedHeight = canvasHeight - clippedDestinationY;

            if (clippedWidth <= 0 || clippedHeight <= 0)
                return false;

            try
            {
                Graphics.CopyTexture(
                    source.texture,
                    0,
                    0,
                    clippedSourceX,
                    clippedSourceY,
                    clippedWidth,
                    clippedHeight,
                    texture,
                    0,
                    0,
                    clippedDestinationX,
                    clippedDestinationY);
            }
            catch
            {
                return false;
            }

            return true;
        }

        private void CaptureButtonBaseSize()
        {
            if (_hasButtonBaseSize)
                return;

            RectTransform sourceRect = boxRoot != null ? boxRoot : GetComponent<RectTransform>();
            if (sourceRect == null)
                return;

            Vector2 size = sourceRect.rect.size;
            if (size.x <= 0.01f || size.y <= 0.01f)
                size = sourceRect.sizeDelta;

            size.x = Mathf.Abs(size.x);
            size.y = Mathf.Abs(size.y);
            if (size.x <= 0.01f || size.y <= 0.01f)
                return;

            _buttonBaseSize = size;
            _hasButtonBaseSize = true;
        }

        private void BringLabelToFront()
        {
            if (labelRoot == null)
                return;

            labelRoot.SetAsLastSibling();
        }

        private void ApplyAnimatorOpen(bool open)
        {
            if (boxAnimator == null || string.IsNullOrWhiteSpace(openBoolParameter))
                return;

            if (!HasAnimatorParameter(openBoolParameter, AnimatorControllerParameterType.Bool))
            {
                if (!_warnedMissingOpenParameter)
                {
                    Debug.LogWarning($"[WebPortBoxStartButtonView] Animator '{boxAnimator.name}' does not have a Bool parameter named '{openBoolParameter}'. Box hover open animation cannot run.", this);
                    _warnedMissingOpenParameter = true;
                }
                return;
            }

            boxAnimator.SetBool(openBoolParameter, open);
        }

        private void SetAnimatorTrigger(string parameter)
        {
            if (boxAnimator == null || string.IsNullOrWhiteSpace(parameter))
                return;

            if (!HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
            {
                if (parameter == hoverTriggerParameter && !_warnedMissingHoverParameter)
                {
                    Debug.LogWarning($"[WebPortBoxStartButtonView] Animator '{boxAnimator.name}' does not have a Trigger parameter named '{parameter}'. Hover trigger will be skipped.", this);
                    _warnedMissingHoverParameter = true;
                }
                else if (parameter == pressTriggerParameter && !_warnedMissingPressParameter)
                {
                    Debug.LogWarning($"[WebPortBoxStartButtonView] Animator '{boxAnimator.name}' does not have a Trigger parameter named '{parameter}'. Press trigger will be skipped.", this);
                    _warnedMissingPressParameter = true;
                }
                return;
            }

            boxAnimator.ResetTrigger(parameter);
            boxAnimator.SetTrigger(parameter);
        }

        private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType type)
        {
            if (boxAnimator == null)
                return false;

            AnimatorControllerParameter[] parameters = boxAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter animatorParameter = parameters[i];
                if (animatorParameter.type == type && animatorParameter.name == parameter)
                    return true;
            }

            return false;
        }

        private void AnimateBoxRootIdle()
        {
            if (boxRoot == null || !_hasCapturedPose)
                return;

            float time = Time.unscaledTime;
            float hoverLift = _isOpen ? 5f : 0f;
            float bob = animateIdleMotion ? Mathf.Sin(time * idleMotionSpeed) * idleBobPixels : 0f;
            float tilt = animateIdleMotion ? Mathf.Sin(time * idleMotionSpeed * 0.77f) * idleTiltDegrees : 0f;
            float pulse = animateIdleMotion ? 1f + Mathf.Sin(time * idleMotionSpeed * 1.35f) * idlePulseScale : 1f;
            if (preventScaleBelowBase)
                pulse = Mathf.Max(1f, pulse);
            float squashX = _isPressed ? 1f + pressSquash : 1f;
            float squashY = _isPressed ? 1f - pressSquash : 1f;

            boxRoot.anchoredPosition = _boxBaseAnchoredPosition + new Vector2(0f, hoverLift + bob);
            boxRoot.localRotation = _boxBaseRotation * Quaternion.Euler(0f, 0f, tilt);
            boxRoot.localScale = new Vector3(
                _boxBaseScale.x * pulse * squashX,
                _boxBaseScale.y * pulse * squashY,
                _boxBaseScale.z);
        }

        private void RestoreBoxRootPose()
        {
            if (boxRoot == null || !_hasCapturedPose)
                return;

            boxRoot.anchoredPosition = _boxBaseAnchoredPosition;
            boxRoot.localRotation = _boxBaseRotation;
            boxRoot.localScale = _boxBaseScale;
        }

        private void EnsurePointerRaycastTarget()
        {
            if (boxImage != null && boxRoot != null && boxImage.rectTransform != boxRoot)
                boxImage.raycastTarget = false;

            Graphic rootGraphic = GetComponent<Graphic>();
            if (rootGraphic != null)
            {
                rootGraphic.raycastTarget = true;
                return;
            }

            if (boxImage != null)
            {
                boxImage.raycastTarget = true;
                return;
            }

            if (!_warnedMissingRaycastGraphic)
            {
                Debug.LogWarning("[WebPortBoxStartButtonView] This button has no UI Graphic on the same GameObject. Add an Image to receive mouse hover events.", this);
                _warnedMissingRaycastGraphic = true;
            }
        }

        private void WarnIfRootAndImageAreSame()
        {
            if (_warnedSharedRootAndImage || boxRoot == null || boxImage == null)
                return;

            if (boxRoot == boxImage.rectTransform && !sizeImageFromButtonRect)
            {
                Debug.LogWarning("[WebPortBoxStartButtonView] Box Root and Box Image use the same RectTransform. For non-squashed sprite resizing, use Box Root for the clickable parent and Box Image for a child visual Image.", this);
                _warnedSharedRootAndImage = true;
            }
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource == null || clip == null)
                return;

            audioSource.PlayOneShot(clip);
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseOutBackLight(float t)
        {
            t = Mathf.Clamp01(t);
            const float overshoot = 1.25f;
            float p = t - 1f;
            return 1f + p * p * ((overshoot + 1f) * p + overshoot);
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) <= 0.01f && Mathf.Abs(a.y - b.y) <= 0.01f;
        }
    }
}
