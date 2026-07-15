using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    [DisallowMultipleComponent]
    public sealed class WebPortScrollingSky : MonoBehaviour
    {
        [SerializeField] private Sprite skySprite;
        [SerializeField] private float pixelsPerSecond = 18f;
        [SerializeField, Min(1f)] private float coverScale = 1f;
        [SerializeField] private float verticalOffset = 70f;
        [SerializeField] private bool mirrorAlternateTiles = true;

        private readonly Image[] _tiles = new Image[3];
        private RectTransform _rect;
        private float _tileWidth = 1f;

        public void Configure(Sprite sprite, float speed, float scale, float yOffset, bool mirrorAlternates)
        {
            skySprite = sprite;
            pixelsPerSecond = speed;
            coverScale = Mathf.Max(1f, scale);
            verticalOffset = yOffset;
            mirrorAlternateTiles = mirrorAlternates;
            RebuildTiles();
            UpdateLayout();
        }

        private void Awake()
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            RebuildTiles();
            UpdateLayout();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _tiles.Length; i++)
                _tiles[i] = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateLayout();
        }

        private void Update()
        {
            if (skySprite == null)
                return;

            UpdateLayout();

            float offset = Mathf.Repeat(Time.unscaledTime * pixelsPerSecond, _tileWidth);
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i] == null)
                    continue;

                RectTransform tileRect = _tiles[i].rectTransform;
                tileRect.anchoredPosition = new Vector2(-_tileWidth + i * _tileWidth - offset, verticalOffset);
            }
        }

        private void RebuildTiles()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            if (_rect == null)
                return;

            Stretch(_rect);

            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i] == null)
                    _tiles[i] = CreateTile(i);

                _tiles[i].sprite = skySprite;
                _tiles[i].raycastTarget = false;
                _tiles[i].preserveAspect = false;
                _tiles[i].color = Color.white;
                _tiles[i].rectTransform.localScale = mirrorAlternateTiles && i % 2 == 1
                    ? new Vector3(-1f, 1f, 1f)
                    : Vector3.one;
            }
        }

        private Image CreateTile(int index)
        {
            GameObject obj = new($"Sky Tile {index + 1}");
            obj.transform.SetParent(transform, false);
            obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            RectTransform tileRect = obj.AddComponent<RectTransform>();
            tileRect.anchorMin = tileRect.anchorMax = tileRect.pivot = new Vector2(0.5f, 0.5f);

            Image image = obj.AddComponent<Image>();
            image.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor;
            image.raycastTarget = false;
            return image;
        }

        private void UpdateLayout()
        {
            if (_rect == null || skySprite == null)
                return;

            Rect rect = _rect.rect;
            if (rect.width <= 0.01f || rect.height <= 0.01f)
                return;

            Vector2 spriteSize = skySprite.rect.size;
            float scale = Mathf.Max(rect.width / spriteSize.x, rect.height / spriteSize.y) * coverScale;
            Vector2 tileSize = spriteSize * scale;
            _tileWidth = Mathf.Max(1f, tileSize.x);

            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i] == null)
                    continue;

                RectTransform tileRect = _tiles[i].rectTransform;
                tileRect.sizeDelta = tileSize;
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
    }
}
