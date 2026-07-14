using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    [CreateAssetMenu(menuName = "Hackathon/WebPort Visual Config", fileName = "WebPortVisualConfig")]
    public sealed class WebPortVisualConfig : ScriptableObject
    {
        [Serializable]
        public struct PrefabTransform
        {
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public Vector3 localScale;

            public static PrefabTransform Identity => new()
            {
                localPosition = Vector3.zero,
                localEulerAngles = Vector3.zero,
                localScale = Vector3.one,
            };

            public void ApplyTo(Transform target)
            {
                target.localPosition = localPosition;
                target.localRotation = Quaternion.Euler(localEulerAngles);
                target.localScale = localScale == Vector3.zero ? Vector3.one : localScale;
            }
        }

        public enum UiSpriteMode
        {
            Auto,
            Simple,
            Sliced,
            Tiled,
        }

        public enum UiColorMode
        {
            Tint,
            PreserveSprite,
        }

        [Serializable]
        public struct UiImageStyle
        {
            public Sprite sprite;
            public UiSpriteMode mode;
            public UiColorMode colorMode;
            public Color color;
            public bool preserveAspect;
            public bool raycastTarget;
            [Min(0.01f)] public float pixelsPerUnitMultiplier;
            public Material material;

            public static UiImageStyle Create(Color color, UiSpriteMode mode = UiSpriteMode.Auto, bool raycastTarget = false)
            {
                return new UiImageStyle
                {
                    mode = mode,
                    colorMode = UiColorMode.Tint,
                    color = color,
                    preserveAspect = false,
                    raycastTarget = raycastTarget,
                    pixelsPerUnitMultiplier = 1f,
                };
            }

            public UiImageStyle WithLegacy(Sprite legacySprite, Color legacyColor)
            {
                UiImageStyle copy = this;
                if (copy.sprite == null)
                    copy.sprite = legacySprite;
                if (copy.color.a <= 0f && legacyColor.a > 0f)
                    copy.color = legacyColor;
                if (copy.pixelsPerUnitMultiplier <= 0f)
                    copy.pixelsPerUnitMultiplier = 1f;
                return copy;
            }

            public UiImageStyle WithColor(Color overrideColor)
            {
                UiImageStyle copy = this;
                copy.color = overrideColor;
                if (copy.pixelsPerUnitMultiplier <= 0f)
                    copy.pixelsPerUnitMultiplier = 1f;
                return copy;
            }

            public Color ResolveColor()
            {
                if (sprite != null && colorMode == UiColorMode.PreserveSprite)
                    return Color.white;
                return color;
            }

            public Image.Type ResolveImageType(Sprite fallbackSprite)
            {
                Sprite resolvedSprite = sprite != null ? sprite : fallbackSprite;
                if (resolvedSprite == null)
                    return Image.Type.Simple;

                return mode switch
                {
                    UiSpriteMode.Simple => Image.Type.Simple,
                    UiSpriteMode.Sliced => HasBorder(resolvedSprite) ? Image.Type.Sliced : Image.Type.Simple,
                    UiSpriteMode.Tiled => Image.Type.Tiled,
                    _ => HasBorder(resolvedSprite) ? Image.Type.Sliced : Image.Type.Simple,
                };
            }

            private static bool HasBorder(Sprite sprite)
            {
                Vector4 border = sprite.border;
                return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
            }
        }

        [Header("Player Sprite Sheets")]
        public Texture2D idleSprite;
        public Texture2D holdingBoxSprite;
        public Texture2D sideSprite;
        public Texture2D sideHoldingBoxSprite;
        [Min(1)] public int spriteColumns = 3;
        [Min(1)] public int spriteRows = 4;
        [Min(1)] public int spriteFrameCount = 10;
        [Min(0.01f)] public float spriteFrameSeconds = 0.13f;
        [Range(0f, 1f)] public float diagonalFacingDeadZone = 0.18f;

        [Header("Optional Replacement Prefabs")]
        public GameObject normalPackagePrefab;
        public GameObject highPackagePrefab;
        public GameObject bombPackagePrefab;
        public GameObject gravityPackagePrefab;
        public GameObject pillarObstaclePrefab;
        public GameObject wallObstaclePrefab;
        public GameObject rockObstaclePrefab;
        public GameObject truckPrefab;
        public GameObject busPrefab;

        [Header("Prefab Transform Overrides")]
        public PrefabTransform normalPackageTransform = PrefabTransform.Identity;
        public PrefabTransform highPackageTransform = PrefabTransform.Identity;
        public PrefabTransform bombPackageTransform = PrefabTransform.Identity;
        public PrefabTransform gravityPackageTransform = PrefabTransform.Identity;
        public PrefabTransform pillarObstacleTransform = PrefabTransform.Identity;
        public PrefabTransform wallObstacleTransform = PrefabTransform.Identity;
        public PrefabTransform rockObstacleTransform = PrefabTransform.Identity;
        public PrefabTransform truckTransform = PrefabTransform.Identity;
        public PrefabTransform busTransform = PrefabTransform.Identity;

        [Header("Optional Replacement Materials")]
        public Material groundBaseMaterial;
        public Material crossGroundMaterial;
        public Material startMarkerMaterial;
        public Material goalFillMaterial;
        public Material goalRingMaterial;
        public Material normalPackageMaterial;
        public Material highPackageMaterial;
        public Material bombPackageMaterial;
        public Material gravityPackageMaterial;
        public Material pillarMaterial;
        public Material wallMaterial;
        public Material rockMaterial;
        public Material truckMaterial;
        public Material busMaterial;

        [Header("Fallback Colors")]
        public Color pageBackground = new(0.8745f, 0.9020f, 0.9137f, 1f);
        public Color groundBase = new(0.7255f, 0.7765f, 0.7882f, 1f);
        public Color startBlue = new(0.2039f, 0.5961f, 0.8588f, 1f);
        public Color goalGreen = new(0.1804f, 0.8000f, 0.4431f, 1f);
        public Color textDark = new(0.1765f, 0.2039f, 0.2118f, 1f);
        public Color muted = new(0.3882f, 0.4314f, 0.4471f, 1f);
        public Color yellow = new(0.9451f, 0.7686f, 0.0588f, 1f);
        public Color orange = new(0.8824f, 0.4392f, 0.3333f, 1f);
        public Color red = new(0.9059f, 0.2980f, 0.2353f, 1f);
        public Color normalPackageColor = new(0.6275f, 0.4471f, 0.3020f, 1f);
        public Color highPackageColor = new(0.9451f, 0.7686f, 0.0588f, 1f);
        public Color bombPackageColor = new(0.2f, 0.2f, 0.2f, 1f);
        public Color gravityPackageColor = new(0.5569f, 0.2667f, 0.6784f, 1f);
        public Color pillarColor = new(0.4980f, 0.5490f, 0.5529f, 1f);
        public Color wallColor = new(0.7529f, 0.4745f, 0.2471f, 1f);
        public Color rockColor = new(0.5529f, 0.4314f, 0.3882f, 1f);
        public Color truckColor = new(0.2039f, 0.2863f, 0.3686f, 1f);

        [Header("UI Theme")]
        [Tooltip("Optional root prefab for designer-authored UI. If empty, WebPort builds the default runtime UI.")]
        public WebPortUiPrefabView uiPrefab;
        public Font uiFont;
        public Sprite screenBackgroundSprite;
        public Sprite panelSprite;
        public Sprite hudPanelSprite;
        public Sprite buttonSprite;
        public Sprite inputSprite;
        public Sprite progressBackgroundSprite;
        public Sprite progressFillSprite;
        public Color uiBackground = new(0.8745f, 0.9020f, 0.9137f, 1f);
        public Color uiPanel = new(1f, 1f, 1f, 0.94f);
        public Color uiHudPanel = new(1f, 1f, 1f, 0.88f);
        public Color uiInput = new(1f, 1f, 1f, 1f);
        public Color uiText = new(0.1765f, 0.2039f, 0.2118f, 1f);
        public Color uiMutedText = new(0.3882f, 0.4314f, 0.4471f, 1f);
        public Color uiButtonText = Color.white;
        public Color uiPrimaryButton = new(0.2039f, 0.5961f, 0.8588f, 1f);
        public Color uiSecondaryButton = new(0.9451f, 0.7686f, 0.0588f, 1f);
        public Color uiSuccessButton = new(0.1804f, 0.8000f, 0.4431f, 1f);
        public Color uiDanger = new(0.9059f, 0.2980f, 0.2353f, 1f);
        public Color uiProgressBackground = new(0.8745f, 0.9020f, 0.9137f, 1f);
        public Color uiShadow = new(0f, 0f, 0f, 0.16f);
        [Range(0f, 1f)] public float uiPanelOpacity = 0.94f;
        [Range(0f, 1f)] public float uiHudOpacity = 0.88f;

        [Header("UI Safe Image Styles")]
        public UiImageStyle screenBackgroundImage = UiImageStyle.Create(new Color(0.8745f, 0.9020f, 0.9137f, 1f));
        public UiImageStyle panelImage = UiImageStyle.Create(new Color(1f, 1f, 1f, 0.94f));
        public UiImageStyle hudPanelImage = UiImageStyle.Create(new Color(1f, 1f, 1f, 0.88f));
        public UiImageStyle buttonImage = UiImageStyle.Create(Color.white, UiSpriteMode.Auto, true);
        public UiImageStyle inputImage = UiImageStyle.Create(Color.white, UiSpriteMode.Auto, true);
        public UiImageStyle progressBackgroundImage = UiImageStyle.Create(new Color(0.8745f, 0.9020f, 0.9137f, 1f));
        public UiImageStyle progressFillImage = UiImageStyle.Create(Color.white);

        public static WebPortVisualConfig LoadOrCreateRuntime()
        {
            WebPortVisualConfig config = Resources.Load<WebPortVisualConfig>("WebPort/WebPortVisualConfig");
            if (config != null)
                return config;

            WebPortVisualConfig fallback = CreateInstance<WebPortVisualConfig>();
            fallback.idleSprite = Resources.Load<Texture2D>("WebPort/Art/qt_2");
            fallback.holdingBoxSprite = Resources.Load<Texture2D>("WebPort/Art/qt_holding_box");
            fallback.sideSprite = Resources.Load<Texture2D>("WebPort/Art/qt_right_side");
            fallback.sideHoldingBoxSprite = Resources.Load<Texture2D>("WebPort/Art/qt_right_side_holding_box");
            return fallback;
        }

        public Texture2D GetPlayerTexture(bool holding, bool side)
        {
            if (holding)
                return side ? sideHoldingBoxSprite : holdingBoxSprite;
            return side ? sideSprite : idleSprite;
        }

        public GameObject GetPackagePrefab(PackageKind kind)
        {
            return kind switch
            {
                PackageKind.High => highPackagePrefab,
                PackageKind.Bomb => bombPackagePrefab,
                PackageKind.Gravity => gravityPackagePrefab,
                _ => normalPackagePrefab,
            };
        }

        public PrefabTransform GetPackageTransform(PackageKind kind)
        {
            return kind switch
            {
                PackageKind.High => highPackageTransform,
                PackageKind.Bomb => bombPackageTransform,
                PackageKind.Gravity => gravityPackageTransform,
                _ => normalPackageTransform,
            };
        }

        public Material GetPackageMaterial(PackageKind kind)
        {
            return kind switch
            {
                PackageKind.High => highPackageMaterial,
                PackageKind.Bomb => bombPackageMaterial,
                PackageKind.Gravity => gravityPackageMaterial,
                _ => normalPackageMaterial,
            };
        }

        public Color GetPackageColor(PackageKind kind)
        {
            return kind switch
            {
                PackageKind.High => highPackageColor,
                PackageKind.Bomb => bombPackageColor,
                PackageKind.Gravity => gravityPackageColor,
                _ => normalPackageColor,
            };
        }

        public GameObject GetObstaclePrefab(ObstacleKind kind)
        {
            return kind switch
            {
                ObstacleKind.Wall => wallObstaclePrefab,
                ObstacleKind.Rock => rockObstaclePrefab,
                _ => pillarObstaclePrefab,
            };
        }

        public PrefabTransform GetObstacleTransform(ObstacleKind kind)
        {
            return kind switch
            {
                ObstacleKind.Wall => wallObstacleTransform,
                ObstacleKind.Rock => rockObstacleTransform,
                _ => pillarObstacleTransform,
            };
        }

        public Material GetObstacleMaterial(ObstacleKind kind)
        {
            return kind switch
            {
                ObstacleKind.Wall => wallMaterial,
                ObstacleKind.Rock => rockMaterial,
                _ => pillarMaterial,
            };
        }

        public Color GetObstacleColor(ObstacleKind kind)
        {
            return kind switch
            {
                ObstacleKind.Wall => wallColor,
                ObstacleKind.Rock => rockColor,
                _ => pillarColor,
            };
        }

        public UiImageStyle GetScreenBackgroundStyle()
        {
            return screenBackgroundImage.WithLegacy(screenBackgroundSprite, uiBackground);
        }

        public UiImageStyle GetPanelStyle()
        {
            return panelImage.WithLegacy(panelSprite, WithAlpha(uiPanel, uiPanelOpacity));
        }

        public UiImageStyle GetHudPanelStyle()
        {
            return hudPanelImage.WithLegacy(hudPanelSprite, WithAlpha(uiHudPanel, uiHudOpacity));
        }

        public UiImageStyle GetButtonStyle(Color color)
        {
            return buttonImage.WithLegacy(buttonSprite, color).WithColor(color);
        }

        public UiImageStyle GetInputStyle()
        {
            return inputImage.WithLegacy(inputSprite, uiInput);
        }

        public UiImageStyle GetProgressBackgroundStyle()
        {
            return progressBackgroundImage.WithLegacy(progressBackgroundSprite, uiProgressBackground);
        }

        public UiImageStyle GetProgressFillStyle(Color color)
        {
            return progressFillImage.WithLegacy(progressFillSprite, color).WithColor(color);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
