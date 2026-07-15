using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hackathon.WebPort
{
    [ExecuteAlways]
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Image))]
    public sealed class WebPortTerminalButtonSkin : MonoBehaviour
    {
        public enum Variant
        {
            PrimaryAmber,
            SecondaryCyan,
            UtilityDark,
        }

        [SerializeField] private Variant variant = Variant.PrimaryAmber;
        [SerializeField] private bool applyOnEnable = false;
        [SerializeField] private bool uppercaseText = true;
        [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.42f;

        private static Sprite _primarySprite;
        private static Sprite _secondarySprite;
        private static Sprite _utilitySprite;

        public Variant Style
        {
            get => variant;
            set
            {
                variant = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            if (applyOnEnable)
                Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && applyOnEnable)
                Apply();
        }
#endif

        public void Apply()
        {
            Button button = GetComponent<Button>();
            Image image = GetComponent<Image>();
            if (button == null || image == null)
                return;

            Palette palette = GetPalette(variant);
            image.sprite = GetSprite(variant, palette);
            image.type = Image.Type.Sliced;
            image.color = palette.Normal;
            image.raycastTarget = true;
            image.pixelsPerUnitMultiplier = 1f;

            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = palette.Normal;
            colors.highlightedColor = palette.Highlighted;
            colors.pressedColor = palette.Pressed;
            colors.selectedColor = palette.Highlighted;
            colors.disabledColor = new Color(palette.Normal.r, palette.Normal.g, palette.Normal.b, disabledAlpha);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            Shadow shadow = GetOrAdd<Shadow>();
            shadow.effectColor = palette.DropShadow;
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;

            Outline outline = GetOrAdd<Outline>();
            outline.effectColor = palette.OuterLine;
            outline.effectDistance = new Vector2(1.25f, -1.25f);
            outline.useGraphicAlpha = true;

            Text label = GetComponentInChildren<Text>(true);
            if (label != null)
            {
                if (uppercaseText)
                    label.text = label.text.ToUpperInvariant();

                label.color = palette.Text;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 11;
                label.resizeTextMaxSize = Mathf.Max(label.fontSize, 18);
                label.raycastTarget = false;
            }

            TMP_Text tmpLabel = GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                if (uppercaseText)
                    tmpLabel.text = tmpLabel.text.ToUpperInvariant();

                tmpLabel.color = palette.Text;
                tmpLabel.fontStyle |= FontStyles.Bold;
                tmpLabel.alignment = TextAlignmentOptions.Center;
                tmpLabel.enableAutoSizing = true;
                tmpLabel.fontSizeMin = 11f;
                tmpLabel.fontSizeMax = Mathf.Max(tmpLabel.fontSize, 18f);
                tmpLabel.raycastTarget = false;
            }
        }

        public static void ApplyTo(Button button, Variant style)
        {
            if (button == null)
                return;

            WebPortTerminalButtonSkin skin = button.GetComponent<WebPortTerminalButtonSkin>();
            if (skin == null)
                skin = button.gameObject.AddComponent<WebPortTerminalButtonSkin>();

            skin.variant = style;
            skin.Apply();
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static Palette GetPalette(Variant style)
        {
            return style switch
            {
                Variant.SecondaryCyan => new Palette
                {
                    Normal = new Color(0.055f, 0.42f, 0.48f, 1f),
                    Highlighted = new Color(0.095f, 0.58f, 0.66f, 1f),
                    Pressed = new Color(0.030f, 0.28f, 0.32f, 1f),
                    Inner = new Color(0.12f, 0.78f, 0.82f, 1f),
                    Edge = new Color(0.58f, 0.94f, 0.93f, 1f),
                    OuterLine = new Color(0.035f, 0.16f, 0.18f, 0.95f),
                    DropShadow = new Color(0f, 0f, 0f, 0.48f),
                    Text = Color.white,
                },
                Variant.UtilityDark => new Palette
                {
                    Normal = new Color(0.115f, 0.135f, 0.145f, 1f),
                    Highlighted = new Color(0.18f, 0.215f, 0.225f, 1f),
                    Pressed = new Color(0.070f, 0.080f, 0.088f, 1f),
                    Inner = new Color(0.36f, 0.40f, 0.39f, 1f),
                    Edge = new Color(0.56f, 0.62f, 0.60f, 1f),
                    OuterLine = new Color(0.020f, 0.025f, 0.028f, 0.95f),
                    DropShadow = new Color(0f, 0f, 0f, 0.44f),
                    Text = new Color(0.88f, 0.94f, 0.92f, 1f),
                },
                _ => new Palette
                {
                    Normal = new Color(0.88f, 0.57f, 0.08f, 1f),
                    Highlighted = new Color(1.00f, 0.72f, 0.18f, 1f),
                    Pressed = new Color(0.58f, 0.35f, 0.035f, 1f),
                    Inner = new Color(1.00f, 0.87f, 0.36f, 1f),
                    Edge = new Color(1.00f, 0.96f, 0.60f, 1f),
                    OuterLine = new Color(0.16f, 0.11f, 0.035f, 0.95f),
                    DropShadow = new Color(0f, 0f, 0f, 0.50f),
                    Text = new Color(0.09f, 0.075f, 0.045f, 1f),
                },
            };
        }

        private static Sprite GetSprite(Variant style, Palette palette)
        {
            switch (style)
            {
                case Variant.SecondaryCyan:
                    return _secondarySprite != null ? _secondarySprite : _secondarySprite = CreateSprite("WebPortTerminalButtonCyan", palette);
                case Variant.UtilityDark:
                    return _utilitySprite != null ? _utilitySprite : _utilitySprite = CreateSprite("WebPortTerminalButtonDark", palette);
                default:
                    return _primarySprite != null ? _primarySprite : _primarySprite = CreateSprite("WebPortTerminalButtonAmber", palette);
            }
        }

        private static Sprite CreateSprite(string name, Palette palette)
        {
            const int size = 64;
            const int radius = 6;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = GetPixelColor(x, y, size, radius, palette, clear);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Color GetPixelColor(int x, int y, int size, int radius, Palette palette, Color clear)
        {
            int max = size - 1;
            int dx = x < radius ? radius - x : x > max - radius ? x - (max - radius) : 0;
            int dy = y < radius ? radius - y : y > max - radius ? y - (max - radius) : 0;
            if (dx * dx + dy * dy > radius * radius)
                return clear;

            bool border = x <= 4 || x >= max - 4 || y <= 4 || y >= max - 4;
            bool highlight = y >= max - 8 && !border;
            bool lowerShade = y <= 8 && !border;
            bool scanLine = y == 22 || y == 23;
            bool diagonalMark = x > size - 18 && x < size - 9 && y > 8 && y < 18 && (x + y) % 5 < 2;

            if (border)
                return palette.Edge;
            if (highlight)
                return Color.Lerp(palette.Normal, palette.Inner, 0.55f);
            if (lowerShade)
                return Color.Lerp(palette.Normal, Color.black, 0.18f);
            if (scanLine)
                return Color.Lerp(palette.Normal, palette.Inner, 0.22f);
            if (diagonalMark)
                return Color.Lerp(palette.Normal, palette.Edge, 0.32f);

            return palette.Normal;
        }

        private struct Palette
        {
            public Color Normal;
            public Color Highlighted;
            public Color Pressed;
            public Color Inner;
            public Color Edge;
            public Color OuterLine;
            public Color DropShadow;
            public Color Text;
        }
    }
}
