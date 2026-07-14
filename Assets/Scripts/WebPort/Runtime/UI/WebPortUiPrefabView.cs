using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    public sealed class WebPortUiPrefabView : MonoBehaviour
    {
        [Header("Screen Roots")]
        public GameObject screenBackground;
        public GameObject menuPanel;
        public GameObject lobbyPanel;
        public GameObject resultsPanel;
        public GameObject hudRoot;

        [Header("Menu")]
        public Button createRoomButton;
        public Button joinRoomButton;
        public Component roomCodeInput;
        public Component joinErrorText;

        [Header("Lobby")]
        public Component roomCodeText;
        public Component membersText;
        public Button startButton;

        [Header("Results")]
        public Component resultsText;
        public Button restartButton;
        public Button backToLobbyButton;

        [Header("HUD")]
        public Component scoreText;
        public Component goalArrowText;
        public Component goalDistanceText;
        public Component timerText;
        public Image timerPanelImage;
        public GameObject instabilityPanel;
        public Image instabilityFill;
        public Component instabilityText;
        public GameObject truckBanner;

        [Header("Behavior")]
        public bool applyThemeImagesOnInitialize = true;
        public bool recolorTimerPanelWhenDanger = true;
        public bool rotateGoalArrow = true;
        public bool forceInstabilityFillHorizontal = true;

        private TextTarget _joinError;
        private TextTarget _roomCode;
        private TextTarget _members;
        private TextTarget _results;
        private TextTarget _score;
        private TextTarget _goalArrow;
        private TextTarget _goalDistance;
        private TextTarget _timer;
        private TextTarget _instability;
        private InputTarget _roomInput;

        public bool HasRequiredReferences => menuPanel != null && lobbyPanel != null && resultsPanel != null && hudRoot != null;

        public void Initialize(Action onCreateRoom, Action<string> onJoinRoom, Action onStartGame, Action onBackToLobby)
        {
            CacheBindings();

            AddListener(createRoomButton, onCreateRoom);
            AddListener(joinRoomButton, () => onJoinRoom?.Invoke(_roomInput.Text.Trim().ToUpperInvariant()));
            AddListener(startButton, onStartGame);
            AddListener(restartButton, onStartGame);
            AddListener(backToLobbyButton, onBackToLobby);

            if (applyThemeImagesOnInitialize)
                ApplyThemeImages();
        }

        public void ShowMenu(string error)
        {
            SetPanel(menuPanel);
            _joinError.Text = error ?? string.Empty;
        }

        public void ShowLobby(string roomCode, IReadOnlyList<int> memberIds, int hostId, int selfId, bool isHost)
        {
            SetPanel(lobbyPanel);
            _roomCode.Text = roomCode;
            _members.Text = string.Join("\n", memberIds.Select(id => $"{(id == selfId ? "나" : $"#{id}")}{(id == hostId ? "  방장" : string.Empty)}"));

            if (startButton != null)
                startButton.gameObject.SetActive(isHost);
        }

        public void ShowPlaying()
        {
            SetPanel(null);
        }

        public void ShowResults(IReadOnlyList<ScoreEntry> results, int selfId, bool isHost, string roomCode)
        {
            SetPanel(resultsPanel);

            if (restartButton != null)
                restartButton.gameObject.SetActive(isHost);

            List<string> lines = new() { $"방 {roomCode}", string.Empty };
            for (int i = 0; i < results.Count; i++)
            {
                ScoreEntry result = results[i];
                string name = result.PlayerId == selfId ? "나" : $"#{result.PlayerId}";
                string prefix = i == 0 ? "1위" : $"{i + 1}위";
                lines.Add($"{prefix}  {name}     {result.Deliveries}개");
            }

            _results.Text = string.Join("\n", lines);
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
            bool truckBannerVisible)
        {
            WebPortVisualConfig theme = WebPortVisuals.Config;
            _score.Text = "순위 (배달 개수)\n" + string.Join("\n", scores.Select((s, i) => $"{i + 1}. {(s.PlayerId == selfId ? "나" : $"#{s.PlayerId}")}  {s.Deliveries}"));

            if (rotateGoalArrow && _goalArrow.Transform != null)
                _goalArrow.Transform.localRotation = Quaternion.Euler(0f, 0f, -bearingDegrees);

            _goalDistance.Text = $"{goalDistance}m";

            int totalSeconds = Mathf.Max(Mathf.FloorToInt(remainSeconds), 0);
            bool danger = totalSeconds <= 20;
            _timer.Text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            _timer.Color = danger ? Color.white : theme.uiText;

            if (timerPanelImage != null && recolorTimerPanelWhenDanger)
                timerPanelImage.color = danger ? theme.uiDanger : theme.GetHudPanelStyle().ResolveColor();

            bool showInstability = heldCount > stableHoldCount || instability > 0.1f;
            SetActive(instabilityPanel, showInstability);
            if (showInstability)
            {
                float ratio = Mathf.Clamp01(instability / 100f);
                Color color = instability < 33f ? WebPortVisuals.GoalGreen : instability < 66f ? WebPortVisuals.Yellow : WebPortVisuals.Red;

                if (instabilityFill != null)
                {
                    if (forceInstabilityFillHorizontal)
                    {
                        instabilityFill.type = Image.Type.Filled;
                        instabilityFill.fillMethod = Image.FillMethod.Horizontal;
                    }

                    instabilityFill.fillAmount = ratio;
                    instabilityFill.color = color;
                }

                _instability.Text = instability >= 80f
                    ? $"곧 떨어짐  박스 {heldCount}/{maxHold}"
                    : $"불안정도 {Mathf.RoundToInt(instability)}%  박스 {heldCount}/{maxHold}";
                _instability.Color = color;
            }

            SetActive(truckBanner, truckBannerVisible);
        }

        private void CacheBindings()
        {
            _joinError = new TextTarget(joinErrorText);
            _roomCode = new TextTarget(roomCodeText);
            _members = new TextTarget(membersText);
            _results = new TextTarget(resultsText);
            _score = new TextTarget(scoreText);
            _goalArrow = new TextTarget(goalArrowText);
            _goalDistance = new TextTarget(goalDistanceText);
            _timer = new TextTarget(timerText);
            _instability = new TextTarget(instabilityText);
            _roomInput = new InputTarget(roomCodeInput);
        }

        private void ApplyThemeImages()
        {
            WebPortThemedImage[] themedImages = GetComponentsInChildren<WebPortThemedImage>(true);
            foreach (WebPortThemedImage themedImage in themedImages)
                themedImage.Apply(WebPortVisuals.Config);
        }

        private void SetPanel(GameObject activePanel)
        {
            SetActive(screenBackground, activePanel != null);
            SetActive(menuPanel, activePanel == menuPanel);
            SetActive(lobbyPanel, activePanel == lobbyPanel);
            SetActive(resultsPanel, activePanel == resultsPanel);
            SetActive(hudRoot, activePanel == null);
        }

        private static void AddListener(Button button, Action callback)
        {
            if (button == null)
                return;

            button.onClick.AddListener(() => callback?.Invoke());
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private sealed class InputTarget
        {
            private readonly InputField _inputField;
            private readonly Component _component;
            private readonly PropertyInfo _textProperty;

            public InputTarget(Component component)
            {
                _component = component;
                _inputField = component as InputField;
                _textProperty = component != null ? component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public) : null;
            }

            public string Text
            {
                get
                {
                    if (_inputField != null)
                        return _inputField.text ?? string.Empty;
                    if (_component != null && _textProperty != null && _textProperty.PropertyType == typeof(string) && _textProperty.CanRead)
                        return _textProperty.GetValue(_component) as string ?? string.Empty;
                    return string.Empty;
                }
            }
        }

        private sealed class TextTarget
        {
            private readonly Text _text;
            private readonly Graphic _graphic;
            private readonly Component _component;
            private readonly PropertyInfo _textProperty;
            private readonly PropertyInfo _colorProperty;

            public TextTarget(Component component)
            {
                _component = component;
                _text = component as Text;
                _graphic = component as Graphic;
                Type type = component != null ? component.GetType() : null;
                _textProperty = type != null ? type.GetProperty("text", BindingFlags.Instance | BindingFlags.Public) : null;
                _colorProperty = type != null ? type.GetProperty("color", BindingFlags.Instance | BindingFlags.Public) : null;
            }

            public Transform Transform => _component != null ? _component.transform : null;

            public string Text
            {
                set
                {
                    if (_text != null)
                    {
                        _text.text = value;
                        return;
                    }

                    if (_component != null && _textProperty != null && _textProperty.PropertyType == typeof(string) && _textProperty.CanWrite)
                        _textProperty.SetValue(_component, value);
                }
            }

            public Color Color
            {
                set
                {
                    if (_graphic != null)
                    {
                        _graphic.color = value;
                        return;
                    }

                    if (_component != null && _colorProperty != null && _colorProperty.PropertyType == typeof(Color) && _colorProperty.CanWrite)
                        _colorProperty.SetValue(_component, value);
                }
            }
        }
    }
}
