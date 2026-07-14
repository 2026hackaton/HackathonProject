using System;
using System.Collections.Generic;
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

        [Serializable]
        public struct PlayerMotionSprites
        {
            [Tooltip("Frames used while this motion is standing still.")]
            public Sprite[] idleFrames;

            [Tooltip("Frames used while this motion is moving.")]
            public Sprite[] moveFrames;

            public Sprite GetFrame(bool moving, float now, float fallbackFrameSeconds)
            {
                Sprite sprite = moving
                    ? GetFrame(moveFrames, now, fallbackFrameSeconds)
                    : GetFrame(idleFrames, now, fallbackFrameSeconds);

                if (sprite != null)
                    return sprite;

                return moving
                    ? GetFrame(idleFrames, now, fallbackFrameSeconds)
                    : GetFrame(moveFrames, now, fallbackFrameSeconds);
            }

            private static Sprite GetFrame(Sprite[] frames, float now, float fallbackFrameSeconds)
            {
                if (frames == null || frames.Length == 0)
                    return null;

                float frameSeconds = Mathf.Max(fallbackFrameSeconds, 0.01f);
                int start = Mathf.FloorToInt(now / frameSeconds) % frames.Length;
                for (int i = 0; i < frames.Length; i++)
                {
                    Sprite sprite = frames[(start + i) % frames.Length];
                    if (sprite != null)
                        return sprite;
                }

                return null;
            }
        }

        [Serializable]
        public struct PlayerSpriteSheet
        {
            public Sprite sheet;
            [Min(1)] public int columns;
            [Min(1)] public int rows;
            [Min(1)] public int frameCount;

            public bool TryGetFrame(float now, float fallbackFrameSeconds, out Texture2D texture, out Vector2 scale, out Vector2 offset)
            {
                texture = null;
                scale = Vector2.one;
                offset = Vector2.zero;

                if (sheet == null || sheet.texture == null)
                    return false;

                int safeColumns = Mathf.Max(columns, 1);
                int safeRows = Mathf.Max(rows, 1);
                int safeFrameCount = Mathf.Max(frameCount, 1);
                float frameSeconds = Mathf.Max(fallbackFrameSeconds, 0.01f);
                int frame = Mathf.FloorToInt(now / frameSeconds) % safeFrameCount;
                int column = frame % safeColumns;
                int row = (frame / safeColumns) % safeRows;

                texture = sheet.texture;
                Rect rect = sheet.textureRect;
                float cellWidth = rect.width / safeColumns;
                float cellHeight = rect.height / safeRows;
                float textureWidth = Mathf.Max(texture.width, 1);
                float textureHeight = Mathf.Max(texture.height, 1);

                float x = rect.x + column * cellWidth;
                float y = rect.y + rect.height - ((row + 1f) * cellHeight);
                scale = new Vector2(cellWidth / textureWidth, cellHeight / textureHeight);
                offset = new Vector2(x / textureWidth, y / textureHeight);
                return true;
            }
        }

        [Serializable]
        public struct PlayerMotionSpriteSheets
        {
            public PlayerSpriteSheet idleSheet;
            public PlayerSpriteSheet moveSheet;

            public bool TryGetFrame(bool moving, float now, float fallbackFrameSeconds, out Texture2D texture, out Vector2 scale, out Vector2 offset)
            {
                if (moving && moveSheet.TryGetFrame(now, fallbackFrameSeconds, out texture, out scale, out offset))
                    return true;

                if (!moving && idleSheet.TryGetFrame(now, fallbackFrameSeconds, out texture, out scale, out offset))
                    return true;

                if (moving && idleSheet.TryGetFrame(now, fallbackFrameSeconds, out texture, out scale, out offset))
                    return true;

                if (!moving && moveSheet.TryGetFrame(now, fallbackFrameSeconds, out texture, out scale, out offset))
                    return true;

                texture = null;
                scale = Vector2.one;
                offset = Vector2.zero;
                return false;
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

        [Header("Player Sprite Animations")]
        [Tooltip("Front-facing frames without a box. Has separate idle and move frame arrays.")]
        public PlayerMotionSprites frontSprites;
        [Tooltip("Front-facing frames while holding one or more boxes.")]
        public PlayerMotionSprites frontHoldingSprites;
        [Tooltip("Side-facing frames without a box. The runtime flips these for left/right.")]
        public PlayerMotionSprites sideSprites;
        [Tooltip("Side-facing frames while holding one or more boxes. The runtime flips these for left/right.")]
        public PlayerMotionSprites sideHoldingSprites;
        [Tooltip("Used by every Sprite frame array unless the legacy texture-sheet fallback is active.")]
        [Min(0.01f)] public float playerSpriteFrameSeconds = 0.13f;

        [Header("Player Sprite Sheet Animations")]
        [Tooltip("Front-facing Sprite sheets without a box. Each sheet is cut by columns/rows/frameCount.")]
        public PlayerMotionSpriteSheets frontSpriteSheets;
        [Tooltip("Front-facing Sprite sheets while holding one or more boxes.")]
        public PlayerMotionSpriteSheets frontHoldingSpriteSheets;
        [Tooltip("Side-facing Sprite sheets without a box. The runtime flips these for left/right.")]
        public PlayerMotionSpriteSheets sideSpriteSheets;
        [Tooltip("Side-facing Sprite sheets while holding one or more boxes. The runtime flips these for left/right.")]
        public PlayerMotionSpriteSheets sideHoldingSpriteSheets;
        [Tooltip("Used by Sprite sheet animations. Leave per-sheet frame count at the number of visible frames.")]
        [Min(0.01f)] public float playerSpriteSheetFrameSeconds = 0.13f;

        [Header("Player Sprite Layout")]
        [Tooltip("Billboard width in world units. Reduce this if imported character sprites look too large.")]
        [Min(1f)] public float playerSpriteWorldWidth = 68f;
        [Tooltip("Billboard height in world units. Reduce this if imported character sprites look too tall.")]
        [Min(1f)] public float playerSpriteWorldHeight = 68f;
        [Tooltip("Local offset from the gameplay position to the visual sprite center.")]
        public Vector3 playerSpriteLocalPosition = new(0f, 24f, 0f);
        [Tooltip("Extra local scale applied after the billboard size. X is also flipped for left/right facing.")]
        public Vector3 playerSpriteLocalScale = Vector3.one;

        [Header("Legacy Player Texture Sheets")]
        [Tooltip("Front-facing idle Texture2D sheet. Drag a PNG here directly.")]
        public Texture2D frontIdleTexture;
        [Tooltip("Front-facing move Texture2D sheet. Drag a PNG here directly.")]
        public Texture2D frontMoveTexture;
        [Tooltip("Front-facing idle Texture2D sheet while holding one or more boxes.")]
        public Texture2D frontHoldingIdleTexture;
        [Tooltip("Front-facing move Texture2D sheet while holding one or more boxes.")]
        public Texture2D frontHoldingMoveTexture;
        [Tooltip("Side-facing idle Texture2D sheet. The runtime flips this for left/right.")]
        public Texture2D sideIdleTexture;
        [Tooltip("Side-facing move Texture2D sheet. The runtime flips this for left/right.")]
        public Texture2D sideMoveTexture;
        [Tooltip("Side-facing idle Texture2D sheet while holding one or more boxes.")]
        public Texture2D sideHoldingIdleTexture;
        [Tooltip("Side-facing move Texture2D sheet while holding one or more boxes.")]
        public Texture2D sideHoldingMoveTexture;
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

        [Header("Package Gameplay Collision")]
        [Tooltip("When enabled, package gameplay collision size is calculated from the replacement prefab bounds after transform overrides.")]
        public bool packageCollisionMatchesPrefabBounds = true;
        [Tooltip("Fallback gameplay collision size when no replacement prefab or measurable bounds exist.")]
        public Vector3 fallbackPackageCollisionSize = new(24f, 24f, 24f);
        [Tooltip("Extra padding added to package gameplay half extents.")]
        [Min(0f)] public float packageCollisionPadding = 0f;

        [Header("Optional Replacement Materials")]
        public Material groundBaseMaterial;
        public Material crossGroundMaterial;
        public Material boundaryWallMaterial;
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

        [Header("Boundary Walls")]
        public bool createBoundaryWalls = true;
        [Min(1f)] public float boundaryWallHeight = 90f;
        [Min(1f)] public float boundaryWallThickness = 28f;
        [Min(0f)] public float boundaryWallPadding = 12f;

        [Header("Fallback Colors")]
        [Tooltip("Optional skybox material for the WebPort camera. If empty, the camera uses pageBackground as a solid color.")]
        public Material skyboxMaterial;
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
        public Color boundaryWallColor = new(0.4549f, 0.5020f, 0.5176f, 1f);

        [NonSerialized] private Dictionary<PackageKind, Vector3> _packageHalfExtentsCache;
        [NonSerialized] private Dictionary<PackageKind, float> _packageGroundOffsetCache;

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
            fallback.frontMoveTexture = Resources.Load<Texture2D>("WebPort/Art/qt_2");
            fallback.frontHoldingMoveTexture = Resources.Load<Texture2D>("WebPort/Art/qt_holding_box");
            fallback.sideMoveTexture = Resources.Load<Texture2D>("WebPort/Art/qt_right_side");
            fallback.sideHoldingMoveTexture = Resources.Load<Texture2D>("WebPort/Art/qt_right_side_holding_box");
            return fallback;
        }

        public Sprite GetPlayerSpriteFrame(bool holding, bool side, bool moving, float now)
        {
            float frameSeconds = Mathf.Max(playerSpriteFrameSeconds, 0.01f);

            Sprite sprite = GetPlayerMotion(holding, side).GetFrame(moving, now, frameSeconds);
            if (sprite != null)
                return sprite;

            if (side)
            {
                sprite = GetPlayerMotion(holding, false).GetFrame(moving, now, frameSeconds);
                if (sprite != null)
                    return sprite;
            }

            if (holding)
            {
                sprite = GetPlayerMotion(false, side).GetFrame(moving, now, frameSeconds);
                if (sprite != null)
                    return sprite;
            }

            if (holding && side)
                return frontSprites.GetFrame(moving, now, frameSeconds);

            return null;
        }

        public bool TryGetPlayerSpriteSheetFrame(bool holding, bool side, bool moving, float now, out Texture2D texture, out Vector2 scale, out Vector2 offset)
        {
            float frameSeconds = Mathf.Max(playerSpriteSheetFrameSeconds, 0.01f);

            if (GetPlayerSheetMotion(holding, side).TryGetFrame(moving, now, frameSeconds, out texture, out scale, out offset))
                return true;

            if (side && GetPlayerSheetMotion(holding, false).TryGetFrame(moving, now, frameSeconds, out texture, out scale, out offset))
                return true;

            if (holding && GetPlayerSheetMotion(false, side).TryGetFrame(moving, now, frameSeconds, out texture, out scale, out offset))
                return true;

            if (holding && side && frontSpriteSheets.TryGetFrame(moving, now, frameSeconds, out texture, out scale, out offset))
                return true;

            texture = null;
            scale = Vector2.one;
            offset = Vector2.zero;
            return false;
        }

        public bool TryGetPlayerTextureSheetFrame(bool holding, bool side, bool moving, float now, out Texture2D texture, out Vector2 scale, out Vector2 offset)
        {
            texture = GetLegacyPlayerTexture(holding, side, moving, out bool useSheetFrames);
            scale = Vector2.one;
            offset = Vector2.zero;

            if (texture == null)
                return false;

            if (useSheetFrames)
                CalculateLegacyTextureSheetUv(now, out scale, out offset);

            return true;
        }

        public Vector2 GetPlayerSpriteWorldSize()
        {
            return new Vector2(
                Mathf.Max(playerSpriteWorldWidth, 1f),
                Mathf.Max(playerSpriteWorldHeight, 1f));
        }

        public Vector3 GetPlayerSpriteLocalScale()
        {
            return playerSpriteLocalScale == Vector3.zero ? Vector3.one : playerSpriteLocalScale;
        }

        private PlayerMotionSprites GetPlayerMotion(bool holding, bool side)
        {
            if (holding)
                return side ? sideHoldingSprites : frontHoldingSprites;
            return side ? sideSprites : frontSprites;
        }

        private PlayerMotionSpriteSheets GetPlayerSheetMotion(bool holding, bool side)
        {
            if (holding)
                return side ? sideHoldingSpriteSheets : frontHoldingSpriteSheets;
            return side ? sideSpriteSheets : frontSpriteSheets;
        }

        private Texture2D GetLegacyPlayerTexture(bool holding, bool side, bool moving, out bool useSheetFrames)
        {
            Texture2D primary;
            Texture2D fallback;

            if (holding)
            {
                if (side)
                {
                    primary = moving ? sideHoldingMoveTexture : sideHoldingIdleTexture;
                    fallback = moving ? sideHoldingIdleTexture : sideHoldingMoveTexture;
                    return ResolveLegacyPlayerTexture(primary, fallback, moving, out useSheetFrames);
                }

                primary = moving ? frontHoldingMoveTexture : frontHoldingIdleTexture;
                fallback = moving ? frontHoldingIdleTexture : frontHoldingMoveTexture;
                return ResolveLegacyPlayerTexture(primary, fallback, moving, out useSheetFrames);
            }

            if (side)
            {
                primary = moving ? sideMoveTexture : sideIdleTexture;
                fallback = moving ? sideIdleTexture : sideMoveTexture;
                return ResolveLegacyPlayerTexture(primary, fallback, moving, out useSheetFrames);
            }

            primary = moving ? frontMoveTexture : frontIdleTexture;
            fallback = moving ? frontIdleTexture : frontMoveTexture;
            return ResolveLegacyPlayerTexture(primary, fallback, moving, out useSheetFrames);
        }

        private static Texture2D ResolveLegacyPlayerTexture(Texture2D primary, Texture2D fallback, bool primaryUsesSheetFrames, out bool useSheetFrames)
        {
            if (primary != null)
            {
                useSheetFrames = primaryUsesSheetFrames;
                return primary;
            }

            useSheetFrames = !primaryUsesSheetFrames;
            return fallback;
        }

        private void CalculateLegacyTextureSheetUv(float now, out Vector2 scale, out Vector2 offset)
        {
            int columns = Mathf.Max(spriteColumns, 1);
            int rows = Mathf.Max(spriteRows, 1);
            int frameCount = Mathf.Max(spriteFrameCount, 1);
            float frameSeconds = Mathf.Max(spriteFrameSeconds, 0.01f);
            int frame = Mathf.FloorToInt(now / frameSeconds) % frameCount;
            int column = frame % columns;
            int row = (frame / columns) % rows;

            scale = new Vector2(1f / columns, 1f / rows);
            offset = new Vector2(column / (float)columns, 1f - ((row + 1f) / rows));
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

        public Vector3 GetPackageCollisionHalfExtents(PackageKind kind)
        {
            _packageHalfExtentsCache ??= new Dictionary<PackageKind, Vector3>();
            if (_packageHalfExtentsCache.TryGetValue(kind, out Vector3 cached))
                return cached;

            Vector3 halfExtents = GetFallbackPackageCollisionHalfExtents();
            if (packageCollisionMatchesPrefabBounds)
            {
                GameObject prefab = GetPackagePrefab(kind);
                if (prefab != null && TryCalculatePackagePrefabBounds(kind, prefab, out Bounds bounds))
                    halfExtents = bounds.extents;
            }

            float padding = Mathf.Max(packageCollisionPadding, 0f);
            halfExtents += Vector3.one * padding;
            halfExtents.x = Mathf.Max(halfExtents.x, 1f);
            halfExtents.y = Mathf.Max(halfExtents.y, 1f);
            halfExtents.z = Mathf.Max(halfExtents.z, 1f);
            _packageHalfExtentsCache[kind] = halfExtents;
            return halfExtents;
        }

        public float GetPackageCollisionRadius(PackageKind kind)
        {
            Vector3 halfExtents = GetPackageCollisionHalfExtents(kind);
            return Mathf.Max(halfExtents.x, halfExtents.z);
        }

        public float GetPackageVisualGroundOffset(PackageKind kind)
        {
            _packageGroundOffsetCache ??= new Dictionary<PackageKind, float>();
            if (_packageGroundOffsetCache.TryGetValue(kind, out float cached))
                return cached;

            float offset = GetFallbackPackageCollisionHalfExtents().y;
            GameObject prefab = GetPackagePrefab(kind);
            if (prefab != null && TryCalculatePackagePrefabBounds(kind, prefab, out Bounds bounds))
                offset = Mathf.Max(-bounds.min.y, 0f);

            _packageGroundOffsetCache[kind] = offset;
            return offset;
        }

        private Vector3 GetFallbackPackageCollisionHalfExtents()
        {
            Vector3 size = fallbackPackageCollisionSize;
            if (size == Vector3.zero)
                size = new Vector3(WebPortConstants.BoxHalf * 2f, WebPortConstants.BoxHalf * 2f, WebPortConstants.BoxHalf * 2f);

            return new Vector3(
                Mathf.Max(size.x * 0.5f, 1f),
                Mathf.Max(size.y * 0.5f, 1f),
                Mathf.Max(size.z * 0.5f, 1f));
        }

        private bool TryCalculatePackagePrefabBounds(PackageKind kind, GameObject prefab, out Bounds bounds)
        {
            Matrix4x4 rootMatrix = CreatePackageVisualMatrix(kind, prefab);

            if (TryCalculateColliderBounds(prefab, rootMatrix, out bounds))
                return true;

            return TryCalculateRendererBounds(prefab, rootMatrix, out bounds);
        }

        private bool TryCalculateRendererBounds(GameObject prefab, Matrix4x4 rootMatrix, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                Bounds localBounds = renderer.localBounds;
                Matrix4x4 localToRoot = rootMatrix * prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                EncapsulateTransformedBounds(ref bounds, ref hasBounds, localBounds, localToRoot);
            }

            return hasBounds;
        }

        private bool TryCalculateColliderBounds(GameObject prefab, Matrix4x4 rootMatrix, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                TryEncapsulateColliderBounds(prefab, colliders[i], rootMatrix, ref bounds, ref hasBounds);

            return hasBounds;
        }

        private Matrix4x4 CreatePackageVisualMatrix(PackageKind kind, GameObject prefab)
        {
            PrefabTransform transformOverride = GetPackageTransform(kind);
            Vector3 scale = transformOverride.localScale == Vector3.zero ? Vector3.one : transformOverride.localScale;
            return Matrix4x4.TRS(transformOverride.localPosition, Quaternion.Euler(transformOverride.localEulerAngles), scale);
        }

        private static void TryEncapsulateColliderBounds(GameObject prefab, Collider collider, Matrix4x4 rootMatrix, ref Bounds bounds, ref bool hasBounds)
        {
            Matrix4x4 localToRoot = rootMatrix * prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;

            if (collider is BoxCollider box)
            {
                EncapsulateTransformedBounds(ref bounds, ref hasBounds, new Bounds(box.center, box.size), localToRoot);
                return;
            }

            if (collider is SphereCollider sphere)
            {
                float diameter = sphere.radius * 2f;
                EncapsulateTransformedBounds(ref bounds, ref hasBounds, new Bounds(sphere.center, Vector3.one * diameter), localToRoot);
                return;
            }

            if (collider is CapsuleCollider capsule)
            {
                Vector3 size = Vector3.one * (capsule.radius * 2f);
                if (capsule.direction == 0)
                    size.x = capsule.height;
                else if (capsule.direction == 1)
                    size.y = capsule.height;
                else
                    size.z = capsule.height;

                EncapsulateTransformedBounds(ref bounds, ref hasBounds, new Bounds(capsule.center, size), localToRoot);
                return;
            }

            if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
                EncapsulateTransformedBounds(ref bounds, ref hasBounds, meshCollider.sharedMesh.bounds, localToRoot);
        }

        private static void EncapsulateTransformedBounds(ref Bounds bounds, ref bool hasBounds, Bounds localBounds, Matrix4x4 localToRoot)
        {
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;

            EncapsulatePoint(ref bounds, ref hasBounds, localToRoot.MultiplyPoint3x4(center + new Vector3(-extents.x, -extents.y, -extents.z)));
            EncapsulatePoint(ref bounds, ref hasBounds, localToRoot.MultiplyPoint3x4(center + new Vector3(-extents.x, -extents.y, extents.z)));
            EncapsulatePoint(ref bounds, ref hasBounds, localToRoot.MultiplyPoint3x4(center + new Vector3(-extents.x, extents.y, -extents.z)));
            EncapsulatePoint(ref bounds, ref hasBounds, localToRoot.MultiplyPoint3x4(center + new Vector3(-extents.x, extents.y, extents.z)));
            EncapsulatePoint(ref bounds, ref hasBounds, localToRoot.MultiplyPoint3x4(center + new Vector3(extents.x, -extents.y, -extents.z)));
            EncapsulatePoint(ref bounds, ref hasBounds, localToRoot.MultiplyPoint3x4(center + new Vector3(extents.x, -extents.y, extents.z)));
            EncapsulatePoint(ref bounds, ref hasBounds, localToRoot.MultiplyPoint3x4(center + new Vector3(extents.x, extents.y, -extents.z)));
            EncapsulatePoint(ref bounds, ref hasBounds, localToRoot.MultiplyPoint3x4(center + new Vector3(extents.x, extents.y, extents.z)));
        }

        private static void EncapsulatePoint(ref Bounds bounds, ref bool hasBounds, Vector3 point)
        {
            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(point);
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
