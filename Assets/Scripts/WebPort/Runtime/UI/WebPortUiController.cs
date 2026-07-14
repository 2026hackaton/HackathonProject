using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    public sealed class WebPortUiController : MonoBehaviour
    {
        private Font _font;
        private Sprite _panelFallbackSprite;
        private Sprite _hudFallbackSprite;
        private Sprite _buttonFallbackSprite;
        private Sprite _inputFallbackSprite;
        private Sprite _progressFallbackSprite;
        private Sprite _progressFillFallbackSprite;

        private GameObject _screenBackground;
        private GameObject _menuPanel;
        private GameObject _lobbyPanel;
        private GameObject _resultsPanel;
        private GameObject _hudRoot;
        private InputField _roomCodeInput;
        private Text _joinErrorText;
        private Text _roomCodeText;
        private Text _membersText;
        private Button _startButton;
        private Text _resultsText;
        private Button _restartButton;
        private Text _scoreText;
        private Text _goalArrowText;
        private Text _goalDistanceText;
        private Text _timerText;
        private Image _timerPanelImage;
        private GameObject _instabilityPanel;
        private Image _instabilityFill;
        private Text _instabilityText;
        private GameObject _truckBanner;
        private WebPortUiPrefabView _prefabView;

        public event Action CreateRoomRequested;
        public event Action<string> JoinRoomRequested;
        public event Action StartGameRequested;
        public event Action BackToLobbyRequested;

        private WebPortVisualConfig Theme => WebPortVisuals.Config;

        public void Build()
        {
            _font = Theme.uiFont != null ? Theme.uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            CreateFallbackSprites();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            if (TryBuildPrefabUi())
            {
                ShowMenu(null);
                return;
            }

            CreateScreenBackground();
            CreateMenu();
            CreateLobby();
            CreateResults();
            CreateHud();
            ShowMenu(null);
        }

        public void ShowMenu(string error)
        {
            if (_prefabView != null)
            {
                _prefabView.ShowMenu(error);
                return;
            }

            SetPanel(_menuPanel);
            _joinErrorText.text = error ?? string.Empty;
        }

        public void ShowLobby(string roomCode, IReadOnlyList<int> memberIds, int hostId, int selfId, bool isHost)
        {
            if (_prefabView != null)
            {
                _prefabView.ShowLobby(roomCode, memberIds, hostId, selfId, isHost);
                return;
            }

            SetPanel(_lobbyPanel);
            _roomCodeText.text = roomCode;
            _membersText.text = string.Join("\n", memberIds.Select(id => $"{(id == selfId ? "나" : $"#{id}")}{(id == hostId ? "  방장" : string.Empty)}"));
            _startButton.gameObject.SetActive(isHost);
        }

        public void ShowPlaying()
        {
            if (_prefabView != null)
            {
                _prefabView.ShowPlaying();
                return;
            }

            SetPanel(null);
            _hudRoot.SetActive(true);
        }

        public void ShowResults(IReadOnlyList<ScoreEntry> results, int selfId, bool isHost, string roomCode)
        {
            if (_prefabView != null)
            {
                _prefabView.ShowResults(results, selfId, isHost, roomCode);
                return;
            }

            SetPanel(_resultsPanel);
            _restartButton.gameObject.SetActive(isHost);

            List<string> lines = new() { $"방 {roomCode}", string.Empty };
            for (int i = 0; i < results.Count; i++)
            {
                ScoreEntry result = results[i];
                string name = result.PlayerId == selfId ? "나" : $"#{result.PlayerId}";
                string prefix = i == 0 ? "1위" : $"{i + 1}위";
                lines.Add($"{prefix}  {name}     {result.Deliveries}개");
            }

            _resultsText.text = string.Join("\n", lines);
        }

        public void UpdateHud(
            IReadOnlyList<ScoreEntry> scores,
            int selfId,
            float bearingDegrees,
            int goalDistance,
            int heldCount,
            int maxHold,
            int stableHoldCount,
            float instability,
            float remainSeconds,
            bool truckBanner)
        {
            if (_prefabView != null)
            {
                _prefabView.UpdateHud(scores, selfId, bearingDegrees, goalDistance, heldCount, maxHold, stableHoldCount, instability, remainSeconds, truckBanner);
                return;
            }

            _scoreText.text = "순위 (배달 개수)\n" + string.Join("\n", scores.Select((s, i) => $"{i + 1}. {(s.PlayerId == selfId ? "나" : $"#{s.PlayerId}")}  {s.Deliveries}"));
            _goalArrowText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -bearingDegrees);
            _goalDistanceText.text = $"{goalDistance}m";

            int totalSeconds = Mathf.Max(Mathf.FloorToInt(remainSeconds), 0);
            _timerText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            bool danger = totalSeconds <= 20;
            _timerText.color = danger ? Color.white : Theme.uiText;
            WebPortVisualConfig.UiImageStyle timerStyle = Theme.GetHudPanelStyle();
            ApplyImage(_timerPanelImage, timerStyle.WithColor(danger ? Theme.uiDanger : timerStyle.color), _hudFallbackSprite);

            bool showInstability = heldCount > stableHoldCount || instability > 0.1f;
            _instabilityPanel.SetActive(showInstability);
            if (showInstability)
            {
                float ratio = Mathf.Clamp01(instability / 100f);
                Color color = instability < 33f ? WebPortVisuals.GoalGreen : instability < 66f ? WebPortVisuals.Yellow : WebPortVisuals.Red;
                _instabilityFill.fillAmount = ratio;
                _instabilityFill.color = color;
                _instabilityText.text = instability >= 80f ? $"곧 떨어짐  박스 {heldCount}/{maxHold}" : $"불안정도 {Mathf.RoundToInt(instability)}%  박스 {heldCount}/{maxHold}";
                _instabilityText.color = color;
            }

            _truckBanner.SetActive(truckBanner);
        }

        private void CreateMenu()
        {
            _menuPanel = CreateCenteredPanel("Menu Panel", 400f, 312f);
            AddText(_menuPanel.transform, "택배 대작전", 30, FontStyle.Bold, new Vector2(0f, 110f), new Vector2(340f, 42f), Theme.uiText);
            AddText(_menuPanel.transform, "방을 만들거나 받은 코드로 참가하세요.", 14, FontStyle.Normal, new Vector2(0f, 78f), new Vector2(340f, 28f), Theme.uiMutedText);

            Button create = AddButton(_menuPanel.transform, "방 만들기", Theme.uiSecondaryButton, Theme.uiText, new Vector2(0f, 30f));
            create.onClick.AddListener(() => CreateRoomRequested?.Invoke());

            AddSeparator(_menuPanel.transform, new Vector2(0f, -24f), 310f);
            _roomCodeInput = AddInput(_menuPanel.transform, "방 코드 4자리", new Vector2(0f, -66f));

            Button join = AddButton(_menuPanel.transform, "코드로 참가", Theme.uiPrimaryButton, Theme.uiButtonText, new Vector2(0f, -116f));
            join.onClick.AddListener(() => JoinRoomRequested?.Invoke(_roomCodeInput.text.ToUpperInvariant()));

            _joinErrorText = AddText(_menuPanel.transform, string.Empty, 12, FontStyle.Normal, new Vector2(0f, -146f), new Vector2(330f, 24f), Theme.uiDanger);
        }

        private void CreateLobby()
        {
            _lobbyPanel = CreateCenteredPanel("Lobby Panel", 410f, 328f);
            AddText(_lobbyPanel.transform, "방 코드", 13, FontStyle.Normal, new Vector2(0f, 122f), new Vector2(320f, 24f), Theme.uiMutedText);
            _roomCodeText = AddText(_lobbyPanel.transform, "LOCAL", 42, FontStyle.Bold, new Vector2(0f, 78f), new Vector2(340f, 52f), Theme.uiText);
            _membersText = AddText(_lobbyPanel.transform, string.Empty, 16, FontStyle.Normal, new Vector2(0f, 6f), new Vector2(300f, 100f), Theme.uiText);
            _membersText.alignment = TextAnchor.UpperLeft;

            _startButton = AddButton(_lobbyPanel.transform, "게임 시작 (3분)", Theme.uiSuccessButton, Theme.uiButtonText, new Vector2(0f, -122f), 320f, 48f);
            _startButton.onClick.AddListener(() => StartGameRequested?.Invoke());
        }

        private void CreateResults()
        {
            _resultsPanel = CreateCenteredPanel("Results Panel", 410f, 348f);
            AddText(_resultsPanel.transform, "결과", 30, FontStyle.Bold, new Vector2(0f, 132f), new Vector2(340f, 40f), Theme.uiText);
            _resultsText = AddText(_resultsPanel.transform, string.Empty, 18, FontStyle.Normal, new Vector2(0f, 34f), new Vector2(320f, 160f), Theme.uiText);
            _resultsText.alignment = TextAnchor.UpperCenter;

            _restartButton = AddButton(_resultsPanel.transform, "다시 시작 (3분)", Theme.uiSuccessButton, Theme.uiButtonText, new Vector2(0f, -106f), 320f, 48f);
            _restartButton.onClick.AddListener(() => StartGameRequested?.Invoke());

            Button back = AddButton(_resultsPanel.transform, "로비로", Theme.uiPanel, Theme.uiPrimaryButton, new Vector2(0f, -154f), 160f, 34f);
            back.onClick.AddListener(() => BackToLobbyRequested?.Invoke());
        }

        private void CreateHud()
        {
            _hudRoot = new GameObject("HUD");
            _hudRoot.transform.SetParent(transform, false);
            Stretch(_hudRoot.AddComponent<RectTransform>());

            GameObject scorePanel = CreateHudPanel("Score", new Vector2(12f, -12f), new Vector2(184f, 138f), TextAnchor.UpperLeft);
            _scoreText = AddText(scorePanel.transform, string.Empty, 14, FontStyle.Normal, new Vector2(0f, -4f), new Vector2(158f, 116f), Theme.uiText);
            _scoreText.alignment = TextAnchor.UpperLeft;

            GameObject directionPanel = CreateHudPanel("Direction", new Vector2(0f, -12f), new Vector2(156f, 98f), TextAnchor.UpperCenter);
            RectTransform directionRect = directionPanel.GetComponent<RectTransform>();
            directionRect.anchorMin = directionRect.anchorMax = directionRect.pivot = new Vector2(0.5f, 1f);
            AddText(directionPanel.transform, "배달 지점 방향", 12, FontStyle.Normal, new Vector2(0f, -16f), new Vector2(132f, 20f), Theme.uiMutedText);
            _goalArrowText = AddText(directionPanel.transform, "▲", 28, FontStyle.Bold, new Vector2(0f, -47f), new Vector2(60f, 34f), Theme.uiText);
            _goalDistanceText = AddText(directionPanel.transform, "0m", 12, FontStyle.Normal, new Vector2(0f, -78f), new Vector2(132f, 18f), Theme.uiMutedText);

            GameObject timerPanel = CreateHudPanel("Timer", new Vector2(-12f, -12f), new Vector2(112f, 50f), TextAnchor.UpperRight);
            _timerPanelImage = timerPanel.GetComponent<Image>();
            _timerText = AddText(timerPanel.transform, "03:00", 20, FontStyle.Bold, Vector2.zero, new Vector2(96f, 40f), Theme.uiText);

            GameObject controls = CreateHudPanel("Controls", new Vector2(12f, 12f), new Vector2(704f, 36f), TextAnchor.LowerLeft);
            Text controlsText = AddText(controls.transform, "WASD 이동  /  Shift 조심히 걷기  /  E 줍기  /  마우스 차징 던지기  /  Q 잡기  /  Space 차징 밀치기", 13, FontStyle.Normal, Vector2.zero, new Vector2(674f, 28f), Theme.uiText);
            controlsText.alignment = TextAnchor.MiddleLeft;

            _instabilityPanel = CreateHudPanel("Instability", new Vector2(-12f, 12f), new Vector2(196f, 76f), TextAnchor.LowerRight);
            _instabilityText = AddText(_instabilityPanel.transform, "불안정도 0%", 12, FontStyle.Bold, new Vector2(0f, 18f), new Vector2(172f, 24f), WebPortVisuals.GoalGreen);

            GameObject bar = new("Instability Bar");
            bar.transform.SetParent(_instabilityPanel.transform, false);
            RectTransform barRect = bar.AddComponent<RectTransform>();
            barRect.sizeDelta = new Vector2(164f, 10f);
            barRect.anchoredPosition = new Vector2(0f, -12f);
            Image bg = bar.AddComponent<Image>();
            ApplyImage(bg, Theme.GetProgressBackgroundStyle(), _progressFallbackSprite);

            _instabilityFill = new GameObject("Fill").AddComponent<Image>();
            _instabilityFill.transform.SetParent(bar.transform, false);
            RectTransform fillRect = _instabilityFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            ApplyImage(_instabilityFill, Theme.GetProgressFillStyle(WebPortVisuals.GoalGreen), _progressFillFallbackSprite);
            _instabilityFill.type = Image.Type.Filled;
            _instabilityFill.fillMethod = Image.FillMethod.Horizontal;

            _truckBanner = CreateHudPanel("Truck Banner", new Vector2(-12f, -66f), new Vector2(144f, 42f), TextAnchor.UpperRight);
            ApplyImage(_truckBanner.GetComponent<Image>(), Theme.GetHudPanelStyle().WithColor(WebPortVisuals.GoalGreen), _hudFallbackSprite);
            AddText(_truckBanner.transform, "트럭 출발!", 15, FontStyle.Bold, Vector2.zero, new Vector2(120f, 28f), Color.white);

            _hudRoot.SetActive(false);
        }

        private bool TryBuildPrefabUi()
        {
            WebPortUiPrefabView prefab = Theme.uiPrefab;
            if (prefab == null)
                return false;

            WebPortUiPrefabView view = Instantiate(prefab, transform);
            view.name = prefab.name;

            RectTransform rect = view.GetComponent<RectTransform>();
            if (rect != null)
                Stretch(rect);

            if (!view.HasRequiredReferences)
            {
                Debug.LogWarning("WebPort UI prefab is missing one or more required panel roots. Falling back to the generated default UI.");
                Destroy(view.gameObject);
                return false;
            }

            view.Initialize(
                () => CreateRoomRequested?.Invoke(),
                code => JoinRoomRequested?.Invoke(code),
                () => StartGameRequested?.Invoke(),
                () => BackToLobbyRequested?.Invoke());

            _prefabView = view;
            return true;
        }

        private void CreateScreenBackground()
        {
            _screenBackground = new GameObject("Screen Background");
            _screenBackground.transform.SetParent(transform, false);
            Stretch(_screenBackground.AddComponent<RectTransform>());
            Image image = _screenBackground.AddComponent<Image>();
            ApplyImage(image, Theme.GetScreenBackgroundStyle(), null);
        }

        private GameObject CreateCenteredPanel(string name, float width, float height)
        {
            GameObject panel = new(name);
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = panel.AddComponent<Image>();
            ApplyImage(image, Theme.GetPanelStyle(), _panelFallbackSprite);

            Shadow shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = Theme.uiShadow;
            shadow.effectDistance = new Vector2(0f, -6f);
            return panel;
        }

        private GameObject CreateHudPanel(string name, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor)
        {
            GameObject panel = new(name);
            panel.transform.SetParent(_hudRoot.transform, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            switch (anchor)
            {
                case TextAnchor.UpperRight:
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                    break;
                case TextAnchor.UpperCenter:
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
                    break;
                case TextAnchor.LowerLeft:
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
                    break;
                case TextAnchor.LowerRight:
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
                    break;
                default:
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                    break;
            }

            Image image = panel.AddComponent<Image>();
            ApplyImage(image, Theme.GetHudPanelStyle(), _hudFallbackSprite);
            return panel;
        }

        private Button AddButton(Transform parent, string label, Color background, Color textColor, Vector2 position, float width = 300f, float height = 42f)
        {
            GameObject obj = new(label);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = position;

            Image image = obj.AddComponent<Image>();
            ApplyImage(image, Theme.GetButtonStyle(background), _buttonFallbackSprite);

            Button button = obj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(background.r, background.g, background.b, 0.45f);
            button.colors = colors;

            AddText(obj.transform, label, 16, FontStyle.Bold, Vector2.zero, new Vector2(width - 20f, height - 4f), textColor);
            return button;
        }

        private InputField AddInput(Transform parent, string placeholder, Vector2 position)
        {
            GameObject obj = new("Room Code Input");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 40f);
            rect.anchoredPosition = position;

            Image image = obj.AddComponent<Image>();
            ApplyImage(image, Theme.GetInputStyle(), _inputFallbackSprite);

            InputField input = obj.AddComponent<InputField>();
            input.characterLimit = 4;
            input.contentType = InputField.ContentType.Alphanumeric;

            Text text = AddText(obj.transform, string.Empty, 18, FontStyle.Bold, Vector2.zero, new Vector2(280f, 34f), Theme.uiText);
            text.alignment = TextAnchor.MiddleCenter;
            input.textComponent = text;

            Text placeholderText = AddText(obj.transform, placeholder, 15, FontStyle.Normal, Vector2.zero, new Vector2(280f, 34f), Theme.uiMutedText.WithAlphaCompat(0.65f));
            placeholderText.alignment = TextAnchor.MiddleCenter;
            input.placeholder = placeholderText;
            return input;
        }

        private Text AddText(Transform parent, string value, int size, FontStyle style, Vector2 position, Vector2 rectSize, Color color)
        {
            GameObject obj = new("Text");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = rectSize;
            rect.anchoredPosition = position;

            Text text = obj.AddComponent<Text>();
            text.text = value;
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.72f));
            text.resizeTextMaxSize = size;
            return text;
        }

        private void AddSeparator(Transform parent, Vector2 position, float width)
        {
            GameObject obj = new("Separator");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 1f);
            rect.anchoredPosition = position;
            Image image = obj.AddComponent<Image>();
            image.color = Theme.uiProgressBackground;
        }

        private void SetPanel(GameObject activePanel)
        {
            _screenBackground.SetActive(activePanel != null);
            _menuPanel.SetActive(activePanel == _menuPanel);
            _lobbyPanel.SetActive(activePanel == _lobbyPanel);
            _resultsPanel.SetActive(activePanel == _resultsPanel);
            _hudRoot.SetActive(activePanel == null);
        }

        private void CreateFallbackSprites()
        {
            _panelFallbackSprite = CreateRoundedSprite("WebPortPanel", 64, 12);
            _hudFallbackSprite = CreateRoundedSprite("WebPortHudPanel", 48, 8);
            _buttonFallbackSprite = CreateRoundedSprite("WebPortButton", 48, 8);
            _inputFallbackSprite = CreateRoundedSprite("WebPortInput", 48, 8);
            _progressFallbackSprite = CreateRoundedSprite("WebPortProgress", 32, 5);
            _progressFillFallbackSprite = CreateRoundedSprite("WebPortProgressFill", 32, 5);
        }

        private static void ApplyImage(Image image, WebPortVisualConfig.UiImageStyle style, Sprite fallbackSprite)
        {
            Sprite sprite = style.sprite != null ? style.sprite : fallbackSprite;
            image.color = style.ResolveColor();
            image.sprite = sprite;
            image.type = style.ResolveImageType(fallbackSprite);
            image.preserveAspect = style.preserveAspect;
            image.raycastTarget = style.raycastTarget;
            image.pixelsPerUnitMultiplier = Mathf.Max(style.pixelsPerUnitMultiplier, 0.01f);
            image.material = style.material;
        }

        private static Sprite CreateRoundedSprite(string name, int size, int radius)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float dx = px - cx;
                    float dy = py - cy;
                    pixels[y * size + x] = dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * radius);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
