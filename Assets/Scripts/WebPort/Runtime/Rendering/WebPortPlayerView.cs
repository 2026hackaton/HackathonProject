using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortPlayerView
    {
        private const float MoveFacingThreshold = 15f;

        private static Texture2D _idleTexture;
        private static Texture2D _boxTexture;
        private static Texture2D _sideTexture;
        private static Texture2D _sideBoxTexture;

        private readonly bool _isSelf;
        private readonly Transform _root;
        private readonly Transform _spriteTransform;
        private readonly MeshRenderer _spriteRenderer;
        private readonly Material _spriteMaterial;
        private readonly Transform _ringTransform;
        private readonly Transform _pushRingTransform;
        private readonly Transform _highRingTransform;

        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;
        private bool _usingSideFacing;
        private float _horizontalSign = 1f;

        public Transform Transform => _root;

        public WebPortPlayerView(Transform parent, bool isSelf, string name)
        {
            _isSelf = isSelf;
            EnsureTextures();

            _root = new GameObject(name).transform;
            _root.SetParent(parent, false);

            if (isSelf)
            {
                _ringTransform = CreateRing("Self Ring", 24f, 30f, WebPortVisuals.Yellow.WithAlphaCompat(0.85f), 1f, true);
                _pushRingTransform = CreateRing("Push Charge Ring", 32f, 38f, WebPortVisuals.Orange.WithAlphaCompat(0.85f), 1.5f, true);
                _pushRingTransform.gameObject.SetActive(false);
            }

            _highRingTransform = CreateRing("High Package Ring", 14f, 19f, WebPortVisuals.Yellow, 72f, false);
            _highRingTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _highRingTransform.gameObject.SetActive(false);

            GameObject spriteObject = new("Sprite");
            spriteObject.transform.SetParent(_root, false);
            spriteObject.transform.localPosition = new Vector3(0f, 24f, 0f);
            spriteObject.AddComponent<MeshFilter>().sharedMesh = WebPortVisuals.CreateQuadMesh(68f, 68f);
            _spriteRenderer = spriteObject.AddComponent<MeshRenderer>();
            _spriteMaterial = WebPortVisuals.CreateSpriteMaterial(_idleTexture);
            _spriteRenderer.sharedMaterial = _spriteMaterial;
            _spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _spriteRenderer.receiveShadows = false;
            _spriteTransform = spriteObject.transform;
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        public void Update(PlayerState player, IEnumerable<PackageState> packages, UnityEngine.Camera camera, float now)
        {
            Vector3 current = player.RenderPosition;
            _root.position = new Vector3(current.x, 0f, current.z);

            Vector3 move = Vector3.zero;
            if (_hasPreviousPosition)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                move = (current - _previousPosition) / dt;
            }
            _previousPosition = current;
            _hasPreviousPosition = true;

            Vector3 facingDirection = move.sqrMagnitude > MoveFacingThreshold * MoveFacingThreshold
                ? move.normalized
                : new Vector3(Mathf.Cos(player.Angle), 0f, Mathf.Sin(player.Angle));

            Vector2 screenFacing = ToCameraPlane(facingDirection, camera);

            List<PackageState> held = packages.Where(p => p.HeldBy == player.Id).ToList();
            bool holding = held.Count > 0;
            bool holdingHigh = held.Any(p => p.Kind == PackageKind.High);
            bool facingSide = ResolveFacingSide(screenFacing);
            Texture2D texture = holding ? (facingSide ? _sideBoxTexture : _boxTexture) : (facingSide ? _sideTexture : _idleTexture);

            SetTexture(texture);
            SetFrame(now);

            if (Mathf.Abs(screenFacing.x) > WebPortVisuals.Config.diagonalFacingDeadZone)
                _horizontalSign = screenFacing.x < 0f ? -1f : 1f;

            float xScale = _horizontalSign;
            _spriteTransform.localScale = new Vector3(xScale, 1f, 1f);

            if (camera != null)
                _spriteTransform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);

            Color tint = Color.white;
            tint.a = player.Stunned ? 0.45f : 1f;
            WebPortVisuals.SetMaterialColor(_spriteMaterial, tint);

            if (player.Rolling)
                _spriteTransform.Rotate(0f, 0f, -Time.deltaTime * 20f * Mathf.Rad2Deg, Space.Self);

            if (_ringTransform != null)
                _ringTransform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.Self);

            _highRingTransform.gameObject.SetActive(holdingHigh);
            if (holdingHigh)
            {
                float pulse = 1f + Mathf.Sin(now / 0.15f) * 0.12f;
                _highRingTransform.localScale = new Vector3(pulse, pulse, pulse);
                _highRingTransform.Rotate(0f, 0f, -2.5f * Mathf.Rad2Deg * Time.deltaTime, Space.Self);
            }

            if (_pushRingTransform != null)
            {
                _pushRingTransform.gameObject.SetActive(player.PushCharging);
                if (player.PushCharging)
                {
                    float ratio = Mathf.Clamp01((now - player.PushChargeStartedAt) / WebPortConstants.PushMaxChargeSeconds);
                    float scale = 0.6f + ratio * 0.8f;
                    _pushRingTransform.localScale = new Vector3(scale, scale, scale);
                }
            }
        }

        private Transform CreateRing(string name, float inner, float outer, Color color, float y, bool groundAligned)
        {
            GameObject ring = new(name);
            ring.transform.SetParent(_root, false);
            ring.transform.localPosition = Vector3.up * y;
            if (!groundAligned)
                ring.transform.localRotation = Quaternion.identity;

            ring.AddComponent<MeshFilter>().sharedMesh = WebPortVisuals.CreateRingMesh(inner, outer, groundAligned ? 24 : 5);
            MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WebPortVisuals.CreateUnlit(color, color.a < 0.999f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ring.transform;
        }

        private void SetTexture(Texture2D texture)
        {
            if (_spriteMaterial.HasProperty("_BaseMap") && _spriteMaterial.GetTexture("_BaseMap") != texture)
                _spriteMaterial.SetTexture("_BaseMap", texture);
            if (_spriteMaterial.HasProperty("_MainTex") && _spriteMaterial.GetTexture("_MainTex") != texture)
                _spriteMaterial.SetTexture("_MainTex", texture);
        }

        private bool ResolveFacingSide(Vector2 screenFacing)
        {
            float horizontal = Mathf.Abs(screenFacing.x);
            float vertical = Mathf.Abs(screenFacing.y);
            float deadZone = WebPortVisuals.Config.diagonalFacingDeadZone;

            if (horizontal > vertical + deadZone)
            {
                _usingSideFacing = true;
                return true;
            }

            if (vertical > horizontal + deadZone)
            {
                _usingSideFacing = false;
                return false;
            }

            return _usingSideFacing;
        }

        private static Vector2 ToCameraPlane(Vector3 worldDirection, UnityEngine.Camera camera)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
                return Vector2.up;
            worldDirection.Normalize();

            if (camera == null)
                return new Vector2(worldDirection.x, -worldDirection.z);

            Vector3 screenRight = camera.transform.right;
            screenRight.y = 0f;
            if (screenRight.sqrMagnitude < 0.0001f)
                screenRight = Vector3.right;
            else
                screenRight.Normalize();

            Vector3 screenUp = camera.transform.forward;
            screenUp.y = 0f;
            if (screenUp.sqrMagnitude < 0.0001f)
                screenUp = Vector3.back;
            else
                screenUp.Normalize();

            return new Vector2(Vector3.Dot(worldDirection, screenRight), Vector3.Dot(worldDirection, screenUp));
        }

        private void SetFrame(float now)
        {
            WebPortVisualConfig config = WebPortVisuals.Config;
            int columns = Mathf.Max(config.spriteColumns, 1);
            int rows = Mathf.Max(config.spriteRows, 1);
            int frameCount = Mathf.Max(config.spriteFrameCount, 1);
            float frameSeconds = Mathf.Max(config.spriteFrameSeconds, 0.01f);
            int frame = Mathf.FloorToInt(now / frameSeconds) % frameCount;
            int column = frame % columns;
            int row = (frame / columns) % rows;

            Vector2 scale = new(1f / columns, 1f / rows);
            Vector2 offset = new(column / (float)columns, 1f - ((row + 1f) / rows));
            WebPortVisuals.SetTextureOffset(_spriteMaterial, scale, offset);
        }

        private static void EnsureTextures()
        {
            if (_idleTexture != null)
                return;

            WebPortVisualConfig config = WebPortVisuals.Config;
            _idleTexture = LoadTexture(config.idleSprite, "qt_2");
            _boxTexture = LoadTexture(config.holdingBoxSprite, "qt_holding_box");
            _sideTexture = LoadTexture(config.sideSprite, "qt_right_side");
            _sideBoxTexture = LoadTexture(config.sideHoldingBoxSprite, "qt_right_side_holding_box");
        }

        private static Texture2D LoadTexture(Texture2D configured, string fallbackName)
        {
            Texture2D texture = configured != null ? configured : Resources.Load<Texture2D>($"WebPort/Art/{fallbackName}");
            if (texture == null)
            {
                Texture2D fallback = new(2, 2, TextureFormat.RGBA32, false);
                fallback.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                fallback.Apply();
                return fallback;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }

    public static class WebPortColorExtensions
    {
        public static Color WithAlphaCompat(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
