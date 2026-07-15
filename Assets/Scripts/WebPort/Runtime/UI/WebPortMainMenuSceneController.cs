using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    public sealed class WebPortMainMenuSceneController : MonoBehaviour
    {
        private enum StartButtonMode
        {
            CreateRoomAndStart,
            ShowRoomOptions,
        }

        [Header("Scene Flow")]
        [SerializeField] private string gameplaySceneName = "GamePlayTestScene";
        [SerializeField] private StartButtonMode startButtonMode = StartButtonMode.CreateRoomAndStart;
        [SerializeField] private bool enableMainMenuRoomRequests = true;
        [SerializeField] private bool autoStartCreatedRoom = true;
        [SerializeField] private bool wireButtonsOnAwake = true;

        [Header("Main Screen Effects")]
        [SerializeField] private bool enableMenuEffects = true;
        [SerializeField] private bool enableStartButtonHoverEffect = true;
        [SerializeField] private bool disableBoxStartButtonAnimation = true;
        [SerializeField] private bool enableScrollingSky = true;
        [SerializeField] private Sprite scrollingSkySprite;
        [SerializeField] private float scrollingSkyPixelsPerSecond = 18f;
        [SerializeField, Min(1f)] private float scrollingSkyCoverScale = 1f;
        [SerializeField] private float scrollingSkyVerticalOffset = 70f;
        [SerializeField] private bool mirrorScrollingSkyAlternates = true;
        [SerializeField] private bool enableDoorwayFlicker = true;
        [SerializeField] private Vector2 doorwayFlickerPosition = new(-20f, -86f);
        [SerializeField] private Vector2 doorwayFlickerSize = new(130f, 210f);
        [SerializeField] private bool enableDustParticles = true;
        [SerializeField, Range(0, 64)] private int dustParticleCount = 22;
        [SerializeField] private RectTransform logoMotionTarget;
        [SerializeField] private RectTransform startButtonMotionTarget;

        [Header("Audio")]
        [SerializeField] private bool enableMenuAudio = true;
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip buttonHoverSound;
        [SerializeField] private AudioClip buttonClickSound;

        [Header("Tutorial Overlay")]
        [SerializeField] private bool showTutorialBeforeStart = true;
        [SerializeField] private Sprite tutorialFirstImageSprite;
        [SerializeField] private Sprite tutorialSecondImageSprite;
        [SerializeField] private Color tutorialBackdropColor = new(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color tutorialFallbackImageColor = new(0.08f, 0.10f, 0.11f, 0.94f);
        [SerializeField] private Vector2 tutorialImageSize = new(840f, 520f);
        [SerializeField, Min(0f)] private float tutorialFadeInSeconds = 0.18f;
        [SerializeField, Min(0f)] private float tutorialPageFadeSeconds = 0.12f;
        [SerializeField, Min(0f)] private float tutorialFadeOutSeconds = 0.18f;

        [Header("Custom Main Screen")]
        [SerializeField] private bool useGeneratedFallbackIfMissingStartButton = true;
        [SerializeField] private bool generateRoomOptionsIfMissing = true;
        [SerializeField] private GameObject background;
        [SerializeField] private GameObject logo;
        [SerializeField] private Button gameStartButton;

        [Header("Optional Room Options")]
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button backButton;
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private Text errorText;

        [Header("Generated Fallback")]
        [SerializeField] private string fallbackTitle = "PARCEL PANIC";
        [SerializeField] private string fallbackSubtitle = "DELIVERY TERMINAL";

        private Button _gameStartButton;
        private GameObject _roomPanel;
        private GameObject _scrollingSkyRoot;
        private GameObject _ambientEffectsRoot;
        private WebPortMenuAudioController _menuAudio;
        private InputField _roomCodeInput;
        private Text _errorText;
        private GameObject _tutorialOverlay;
        private CanvasGroup _tutorialCanvasGroup;
        private Image _tutorialImage;
        private Action _pendingTutorialAction;
        private int _tutorialPageIndex;
        private bool _tutorialAccepted;
        private bool _tutorialTransitioning;
        private Coroutine _tutorialCoroutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForMainMenuScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.name.Equals("MainMenu", StringComparison.OrdinalIgnoreCase) &&
                !scene.name.Equals("mainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindAnyObjectByType<WebPortMainMenuSceneController>(FindObjectsInactive.Include) != null)
                return;

            GameObject obj = new("WebPort Main Menu Scene Controller");
            obj.AddComponent<WebPortMainMenuSceneController>();
        }

        private void Awake()
        {
            if (!TryBindCustomMenu())
                BuildGeneratedFallback();

            SetupScrollingSky();
            SetupAmbientEffects();
            SetupMenuAudio();
            SetupStartButtonHoverEffect();
            SetupMenuEffects();
            ShowMain();
        }

        private bool TryBindCustomMenu()
        {
            if (gameStartButton == null)
                return false;

            _gameStartButton = gameStartButton;
            _roomPanel = roomPanel;
            _roomCodeInput = roomCodeInput;
            _errorText = errorText;

            if (enableMainMenuRoomRequests && generateRoomOptionsIfMissing && startButtonMode == StartButtonMode.ShowRoomOptions && !HasRoomOptions())
                BuildGeneratedRoomOptionsForCustomMenu();

            if (wireButtonsOnAwake)
            {
                _gameStartButton.onClick.AddListener(OnGameStartClicked);

                if (createRoomButton != null)
                    createRoomButton.onClick.AddListener(OnCreateRoomClicked);
                if (joinRoomButton != null)
                    joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
                if (backButton != null)
                    backButton.onClick.AddListener(ShowMain);
            }

            RemoveGeneratedButtonSkins(_gameStartButton, createRoomButton, joinRoomButton, backButton);
            return true;
        }

        private void SetupMenuAudio()
        {
            if (!enableMenuAudio)
                return;

            _menuAudio = GetComponent<WebPortMenuAudioController>();
            if (_menuAudio == null)
                _menuAudio = gameObject.AddComponent<WebPortMenuAudioController>();

            _menuAudio.Configure(backgroundMusic, buttonHoverSound, buttonClickSound);
            RegisterMenuButtonAudio(_gameStartButton);
            RegisterMenuButtonAudio(createRoomButton);
            RegisterMenuButtonAudio(joinRoomButton);
            RegisterMenuButtonAudio(backButton);
        }

        private void RegisterMenuButtonAudio(Button button)
        {
            if (_menuAudio != null && button != null)
                _menuAudio.RegisterButton(button);
        }

        private void BuildGeneratedFallback()
        {
            if (!useGeneratedFallbackIfMissingStartButton)
            {
                Debug.LogWarning("[WebPortMainMenuSceneController] Game Start Button is not assigned and generated fallback UI is disabled.");
                enabled = false;
                return;
            }

            Canvas canvas = new GameObject("Main Menu Runtime Canvas").AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            canvas.gameObject.AddComponent<GraphicRaycaster>();
            Stretch(canvas.GetComponent<RectTransform>());

            AddFallbackBackground(canvas.transform);
            AddText(canvas.transform, fallbackTitle, 58, FontStyle.Bold, new Vector2(0f, 148f), new Vector2(720f, 72f), new Color(1f, 0.86f, 0.34f, 1f));
            AddText(canvas.transform, fallbackSubtitle, 16, FontStyle.Bold, new Vector2(0f, 92f), new Vector2(420f, 28f), new Color(0.54f, 0.84f, 0.84f, 1f));

            _gameStartButton = AddButton(canvas.transform, "GAME START", new Vector2(0f, -70f), new Vector2(340f, 56f), new Color(0.95f, 0.68f, 0.16f, 1f));
            _gameStartButton.onClick.AddListener(OnGameStartClicked);

            _roomPanel = CreatePanel(canvas.transform, "Room Options Panel", new Vector2(520f, 310f), new Vector2(0f, -28f));
            AddText(_roomPanel.transform, "ROOM OPTIONS", 24, FontStyle.Bold, new Vector2(0f, 96f), new Vector2(400f, 36f), new Color(1f, 0.86f, 0.34f, 1f));

            createRoomButton = AddButton(_roomPanel.transform, "CREATE ROOM", new Vector2(0f, 36f), new Vector2(330f, 48f), new Color(0.95f, 0.68f, 0.16f, 1f));
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);

            _roomCodeInput = AddInput(_roomPanel.transform, "ROOM CODE", new Vector2(0f, -24f), new Vector2(330f, 42f));

            joinRoomButton = AddButton(_roomPanel.transform, "JOIN ROOM", new Vector2(0f, -82f), new Vector2(330f, 46f), new Color(0.10f, 0.65f, 0.72f, 1f));
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);

            backButton = AddButton(_roomPanel.transform, "BACK", new Vector2(0f, -134f), new Vector2(140f, 32f), new Color(0.18f, 0.22f, 0.23f, 1f));
            backButton.onClick.AddListener(ShowMain);

            _errorText = AddText(_roomPanel.transform, string.Empty, 12, FontStyle.Normal, new Vector2(0f, -168f), new Vector2(380f, 22f), new Color(1f, 0.38f, 0.30f, 1f));
        }

        private void BuildGeneratedRoomOptionsForCustomMenu()
        {
            Transform parent = _gameStartButton.transform.parent != null ? _gameStartButton.transform.parent : transform;

            _roomPanel = CreatePanel(parent, "Room Options Popup", new Vector2(470f, 278f), new Vector2(0f, -24f));
            roomPanel = _roomPanel;

            AddText(_roomPanel.transform, "ROOM OPTIONS", 22, FontStyle.Bold, new Vector2(0f, 88f), new Vector2(360f, 34f), new Color(1f, 0.86f, 0.34f, 1f));

            createRoomButton = AddButton(_roomPanel.transform, "CREATE ROOM", new Vector2(0f, 32f), new Vector2(330f, 48f), new Color(0.95f, 0.68f, 0.16f, 1f));

            _roomCodeInput = AddInput(_roomPanel.transform, "ROOM CODE", new Vector2(0f, -26f), new Vector2(330f, 42f));
            roomCodeInput = _roomCodeInput;

            joinRoomButton = AddButton(_roomPanel.transform, "JOIN ROOM", new Vector2(0f, -82f), new Vector2(330f, 46f), new Color(0.10f, 0.65f, 0.72f, 1f));
            backButton = AddButton(_roomPanel.transform, "BACK", new Vector2(0f, -130f), new Vector2(140f, 32f), new Color(0.18f, 0.22f, 0.23f, 1f));

            _errorText = AddText(_roomPanel.transform, string.Empty, 12, FontStyle.Normal, new Vector2(0f, -162f), new Vector2(380f, 22f), new Color(1f, 0.38f, 0.30f, 1f));
            errorText = _errorText;

            _roomPanel.SetActive(false);
        }

        private void SetupMenuEffects()
        {
            if (!enableMenuEffects)
                return;

            WebPortMainMenuEffects effects = GetComponent<WebPortMainMenuEffects>();
            if (effects == null)
                effects = gameObject.AddComponent<WebPortMainMenuEffects>();

            RectTransform resolvedLogo = logoMotionTarget != null ? logoMotionTarget : logo != null ? logo.GetComponent<RectTransform>() : null;
            RectTransform resolvedButton = ResolveStartButtonMotionTarget();

            effects.Configure(resolvedLogo, resolvedButton);
        }

        private void SetupScrollingSky()
        {
            if (!enableScrollingSky || scrollingSkySprite == null || background == null)
                return;

            Transform parent = background.transform.parent != null ? background.transform.parent : transform;
            _scrollingSkyRoot = new GameObject("Scrolling Sky");
            _scrollingSkyRoot.transform.SetParent(parent, false);
            _scrollingSkyRoot.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;

            RectTransform skyRect = _scrollingSkyRoot.AddComponent<RectTransform>();
            Stretch(skyRect);

            WebPortScrollingSky scrollingSky = _scrollingSkyRoot.AddComponent<WebPortScrollingSky>();
            scrollingSky.Configure(
                scrollingSkySprite,
                scrollingSkyPixelsPerSecond,
                scrollingSkyCoverScale,
                scrollingSkyVerticalOffset,
                mirrorScrollingSkyAlternates);
        }

        private void SetupAmbientEffects()
        {
            if ((!enableDoorwayFlicker && !enableDustParticles) || background == null)
                return;

            Transform parent = background.transform.parent != null ? background.transform.parent : transform;
            _ambientEffectsRoot = new GameObject("Main Menu Ambient Effects");
            _ambientEffectsRoot.transform.SetParent(parent, false);
            _ambientEffectsRoot.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;

            RectTransform ambientRect = _ambientEffectsRoot.AddComponent<RectTransform>();
            Stretch(ambientRect);

            WebPortMainMenuAmbientEffects ambient = _ambientEffectsRoot.AddComponent<WebPortMainMenuAmbientEffects>();
            ambient.Configure(
                enableDoorwayFlicker,
                doorwayFlickerPosition,
                doorwayFlickerSize,
                enableDustParticles,
                dustParticleCount);
        }

        private void SetupStartButtonHoverEffect()
        {
            if (!enableStartButtonHoverEffect || _gameStartButton == null)
                return;

            WebPortBoxStartButtonView boxView = _gameStartButton.GetComponent<WebPortBoxStartButtonView>();
            if (disableBoxStartButtonAnimation && boxView != null)
                boxView.enabled = false;

            WebPortStartButtonHoverEffect hover = _gameStartButton.GetComponent<WebPortStartButtonHoverEffect>();
            if (hover == null)
                hover = _gameStartButton.gameObject.AddComponent<WebPortStartButtonHoverEffect>();

            hover.Configure(ResolveStartButtonHoverTarget(), _gameStartButton.targetGraphic);
        }

        private RectTransform ResolveStartButtonHoverTarget()
        {
            if (_gameStartButton == null)
                return null;

            return _gameStartButton.GetComponent<RectTransform>();
        }

        private RectTransform ResolveStartButtonMotionTarget()
        {
            if (startButtonMotionTarget != null)
                return startButtonMotionTarget;
            return null;
        }

        public void OnGameStartClicked()
        {
            RunAfterTutorial(ContinueGameStartClicked);
        }

        private void ContinueGameStartClicked()
        {
            if (!enableMainMenuRoomRequests)
            {
                LoadGameplayScene();
                return;
            }

            if (startButtonMode == StartButtonMode.ShowRoomOptions && HasRoomOptions())
            {
                ShowRoomOptionsPanel();
                return;
            }

            OnCreateRoomClicked();
        }

        private bool HasRoomOptions()
        {
            return _roomPanel != null && (createRoomButton != null || joinRoomButton != null || _roomCodeInput != null);
        }

        public void OnCreateRoomClicked()
        {
            RunAfterTutorial(ContinueCreateRoomClicked);
        }

        private void ContinueCreateRoomClicked()
        {
            if (!enableMainMenuRoomRequests)
            {
                LoadGameplayScene();
                return;
            }

            WebPortMenuSceneRequest.LoadAndCreateRoom(gameplaySceneName, autoStartCreatedRoom);
        }

        public void OnJoinRoomClicked()
        {
            RunAfterTutorial(ContinueJoinRoomClicked);
        }

        private void ContinueJoinRoomClicked()
        {
            if (!enableMainMenuRoomRequests)
            {
                LoadGameplayScene();
                return;
            }

            string code = _roomCodeInput != null ? _roomCodeInput.text.Trim().ToUpperInvariant() : string.Empty;
            if (code.Length < 4)
            {
                if (_errorText != null)
                    _errorText.text = "Room code must be 4 characters.";
                return;
            }

            WebPortMenuSceneRequest.LoadAndJoinRoom(gameplaySceneName, code);
        }

        private void RunAfterTutorial(Action action)
        {
            if (action == null)
                return;

            if (!showTutorialBeforeStart || _tutorialAccepted)
            {
                action.Invoke();
                return;
            }

            ShowTutorialOverlay(action);
        }

        private void ShowTutorialOverlay(Action continueAction)
        {
            _pendingTutorialAction = continueAction;
            _tutorialPageIndex = 0;

            if (_tutorialOverlay == null)
                _tutorialOverlay = CreateTutorialOverlay();

            ApplyTutorialPage();
            _tutorialOverlay.SetActive(true);
            _tutorialOverlay.transform.SetAsLastSibling();
            PlayTutorialCoroutine(FadeTutorialIn());
        }

        private GameObject CreateTutorialOverlay()
        {
            Transform parent = ResolveTutorialOverlayParent();

            GameObject root = new("Tutorial Overlay");
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            Stretch(rootRect);

            Image backdrop = root.AddComponent<Image>();
            backdrop.color = tutorialBackdropColor;
            backdrop.raycastTarget = true;

            _tutorialCanvasGroup = root.AddComponent<CanvasGroup>();
            _tutorialCanvasGroup.alpha = 0f;
            _tutorialCanvasGroup.interactable = true;
            _tutorialCanvasGroup.blocksRaycasts = true;

            TutorialOverlayClickCatcher clickCatcher = root.AddComponent<TutorialOverlayClickCatcher>();
            clickCatcher.Initialize(AdvanceTutorialOverlay);

            GameObject imageObject = new("Tutorial Image");
            imageObject.transform.SetParent(root.transform, false);
            RectTransform imageRect = imageObject.AddComponent<RectTransform>();
            imageRect.anchorMin = imageRect.anchorMax = imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = tutorialImageSize;
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.anchoredPosition3D = Vector3.zero;
            imageRect.localPosition = Vector3.zero;
            imageRect.localScale = Vector3.one;

            _tutorialImage = imageObject.AddComponent<Image>();
            _tutorialImage.raycastTarget = false;

            return root;
        }

        private void ApplyTutorialPage()
        {
            if (_tutorialImage == null)
                return;

            Sprite pageSprite = GetTutorialPageSprite(_tutorialPageIndex);
            _tutorialImage.sprite = pageSprite;
            _tutorialImage.color = pageSprite != null ? Color.white : tutorialFallbackImageColor;
            _tutorialImage.preserveAspect = pageSprite != null;
        }

        private IEnumerator FadeTutorialIn()
        {
            _tutorialTransitioning = true;
            SetTutorialAlpha(0f);
            yield return FadeTutorialCanvas(0f, 1f, tutorialFadeInSeconds);
            _tutorialTransitioning = false;
        }

        private IEnumerator FadeTutorialPage(int nextPageIndex)
        {
            _tutorialTransitioning = true;
            yield return FadeTutorialImage(1f, 0f, tutorialPageFadeSeconds);
            _tutorialPageIndex = nextPageIndex;
            ApplyTutorialPage();
            yield return FadeTutorialImage(0f, 1f, tutorialPageFadeSeconds);
            _tutorialTransitioning = false;
        }

        private IEnumerator FadeTutorialOutAndContinue()
        {
            _tutorialTransitioning = true;
            yield return FadeTutorialCanvas(1f, 0f, tutorialFadeOutSeconds);
            _tutorialTransitioning = false;
            CompleteTutorialOverlay();
        }

        private IEnumerator FadeTutorialCanvas(float from, float to, float seconds)
        {
            if (_tutorialCanvasGroup == null || seconds <= 0f)
            {
                SetTutorialAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                SetTutorialAlpha(Mathf.Lerp(from, to, Smooth01(t)));
                yield return null;
            }

            SetTutorialAlpha(to);
        }

        private IEnumerator FadeTutorialImage(float from, float to, float seconds)
        {
            if (_tutorialImage == null || seconds <= 0f)
            {
                SetTutorialImageAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                SetTutorialImageAlpha(Mathf.Lerp(from, to, Smooth01(t)));
                yield return null;
            }

            SetTutorialImageAlpha(to);
        }

        private void SetTutorialAlpha(float alpha)
        {
            if (_tutorialCanvasGroup != null)
                _tutorialCanvasGroup.alpha = alpha;
        }

        private void SetTutorialImageAlpha(float alpha)
        {
            if (_tutorialImage == null)
                return;

            Color color = _tutorialImage.color;
            color.a = alpha;
            _tutorialImage.color = color;
        }

        private void PlayTutorialCoroutine(IEnumerator routine)
        {
            if (_tutorialCoroutine != null)
                StopCoroutine(_tutorialCoroutine);

            _tutorialCoroutine = StartCoroutine(routine);
        }

        private static float Smooth01(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private Sprite GetTutorialPageSprite(int index)
        {
            if (index == 0 && tutorialFirstImageSprite != null)
                return tutorialFirstImageSprite;
            if (index == 0 && tutorialSecondImageSprite != null)
                return tutorialSecondImageSprite;
            if (index == 1 && tutorialFirstImageSprite != null && tutorialSecondImageSprite != null)
                return tutorialSecondImageSprite;
            return null;
        }

        private int GetTutorialPageCount()
        {
            return 2;
        }

        private Transform ResolveTutorialOverlayParent()
        {
            Canvas canvas = null;

            if (_gameStartButton != null)
                canvas = _gameStartButton.GetComponentInParent<Canvas>(true);
            if (canvas == null && background != null)
                canvas = background.GetComponentInParent<Canvas>(true);
            if (canvas == null && logo != null)
                canvas = logo.GetComponentInParent<Canvas>(true);
            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

            if (canvas != null)
            {
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();

                return canvas.transform;
            }

            return transform;
        }

        private void AdvanceTutorialOverlay()
        {
            if (_tutorialTransitioning)
                return;

            int lastPageIndex = GetTutorialPageCount() - 1;
            if (_tutorialPageIndex < lastPageIndex)
            {
                PlayTutorialCoroutine(FadeTutorialPage(_tutorialPageIndex + 1));
                return;
            }

            PlayTutorialCoroutine(FadeTutorialOutAndContinue());
        }

        private void CompleteTutorialOverlay()
        {
            _tutorialAccepted = true;
            SetActive(_tutorialOverlay, false);

            Action action = _pendingTutorialAction;
            _pendingTutorialAction = null;
            action?.Invoke();
        }

        private void LoadGameplayScene()
        {
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void ShowMain()
        {
            SetActive(background, true);
            SetActive(logo, true);
            SetActive(_gameStartButton != null ? _gameStartButton.gameObject : null, true);
            SetActive(_roomPanel, false);
            BringMainScreenUiToFront();
            if (_errorText != null)
                _errorText.text = string.Empty;
        }

        public void ShowRoomOptionsPanel()
        {
            SetActive(background, true);
            SetActive(logo, true);
            SetActive(_gameStartButton != null ? _gameStartButton.gameObject : null, false);
            SetActive(_roomPanel, true);
            BringMainScreenUiToFront();
            if (_errorText != null)
                _errorText.text = string.Empty;
        }

        private static void AddFallbackBackground(Transform parent)
        {
            GameObject bg = new("Main Menu Background");
            bg.transform.SetParent(parent, false);
            Stretch(bg.AddComponent<RectTransform>());
            Image image = bg.AddComponent<Image>();
            image.color = new Color(0.035f, 0.041f, 0.047f, 1f);

            AddDecorRect(parent, "Top Rail", new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(1280f, 84f), new Color(0.91f, 0.66f, 0.14f, 0.96f));
            AddDecorRect(parent, "Bottom Conveyor", new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(1280f, 96f), new Color(0.07f, 0.12f, 0.14f, 0.96f));
            AddDecorRect(parent, "Left Dock", new Vector2(0f, 0.5f), new Vector2(96f, 0f), new Vector2(192f, 720f), new Color(0.14f, 0.16f, 0.18f, 0.84f));
            AddDecorRect(parent, "Scanner Strip", new Vector2(1f, 0.5f), new Vector2(-116f, 0f), new Vector2(14f, 720f), new Color(0.11f, 0.65f, 0.72f, 0.82f));
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject panel = new(name);
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.09f, 0.12f, 0.13f, 0.96f);

            Shadow shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
            shadow.effectDistance = new Vector2(0f, -6f);
            return panel;
        }

        private static Button AddButton(Transform parent, string label, Vector2 position, Vector2 size, Color color)
        {
            GameObject obj = new(label);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = obj.AddComponent<Image>();
            image.color = color;

            Button button = obj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
            button.colors = colors;

            AddText(obj.transform, label, size.y >= 48f ? 18 : 14, FontStyle.Bold, Vector2.zero, new Vector2(size.x - 24f, size.y - 6f), Color.white);
            return button;
        }

        private static InputField AddInput(Transform parent, string placeholder, Vector2 position, Vector2 size)
        {
            GameObject obj = new("Room Code Input");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = obj.AddComponent<Image>();
            image.color = new Color(0.045f, 0.055f, 0.060f, 0.98f);

            InputField input = obj.AddComponent<InputField>();
            input.characterLimit = 4;
            input.contentType = InputField.ContentType.Alphanumeric;

            Text text = AddText(obj.transform, string.Empty, 19, FontStyle.Bold, Vector2.zero, new Vector2(size.x - 20f, size.y - 8f), new Color(0.95f, 0.98f, 0.95f, 1f));
            input.textComponent = text;

            Text placeholderText = AddText(obj.transform, placeholder, 15, FontStyle.Normal, Vector2.zero, new Vector2(size.x - 20f, size.y - 8f), new Color(0.60f, 0.68f, 0.66f, 0.8f));
            input.placeholder = placeholderText;
            return input;
        }

        private static Text AddText(Transform parent, string value, int size, FontStyle style, Vector2 position, Vector2 rectSize, Color color)
        {
            GameObject obj = new("Text");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = rectSize;

            Text text = obj.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.7f));
            text.resizeTextMaxSize = size;
            return text;
        }

        private static void AddDecorRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            GameObject obj = new(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = obj.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private void BringMainScreenUiToFront()
        {
            if (background != null)
                background.transform.SetAsFirstSibling();

            if (_scrollingSkyRoot != null)
                _scrollingSkyRoot.transform.SetAsFirstSibling();

            if (_ambientEffectsRoot != null)
            {
                _ambientEffectsRoot.transform.SetAsLastSibling();
                if (logo != null)
                    _ambientEffectsRoot.transform.SetSiblingIndex(Mathf.Max(logo.transform.GetSiblingIndex(), 0));
            }

            if (logo != null)
                logo.transform.SetAsLastSibling();

            Transform startVisualRoot = startButtonMotionTarget != null
                ? startButtonMotionTarget
                : _gameStartButton != null
                    ? _gameStartButton.transform
                    : null;

            if (startVisualRoot != null)
                startVisualRoot.SetAsLastSibling();

            if (_roomPanel != null && _roomPanel.activeSelf)
                _roomPanel.transform.SetAsLastSibling();
        }

        private static void RemoveGeneratedButtonSkins(params Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                WebPortTerminalButtonSkin[] skins = button.GetComponents<WebPortTerminalButtonSkin>();
                foreach (WebPortTerminalButtonSkin skin in skins)
                {
                    if (skin == null)
                        continue;

                    if (Application.isPlaying)
                        Destroy(skin);
                    else
                        DestroyImmediate(skin);
                }
            }
        }

        private sealed class TutorialOverlayClickCatcher : MonoBehaviour, IPointerDownHandler
        {
            private Action _onClicked;
            private int _enabledFrame;
            private int _lastClickFrame = -1;

            public void Initialize(Action onClicked)
            {
                _onClicked = onClicked;
            }

            private void OnEnable()
            {
                _enabledFrame = Time.frameCount;
            }

            private void Update()
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Time.frameCount <= _enabledFrame)
                    return;

                bool mouseClicked = Input.GetMouseButtonDown(0);
                bool touchStarted = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
                if (mouseClicked || touchStarted)
                    InvokeClick();
#endif
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                InvokeClick();
            }

            private void InvokeClick()
            {
                if (Time.frameCount <= _enabledFrame)
                    return;

                if (_lastClickFrame == Time.frameCount)
                    return;

                _lastClickFrame = Time.frameCount;
                _onClicked?.Invoke();
            }
        }
    }
}
