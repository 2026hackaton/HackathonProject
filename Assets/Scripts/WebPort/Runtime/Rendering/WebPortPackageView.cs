using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortPackageView
    {
        private readonly Transform _root;
        private GameObject _visual;
        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private Quaternion _visualBaseRotation = Quaternion.identity;
        private PackageKind _currentKind;

        public int Id { get; }

        public WebPortPackageView(Transform parent, int id)
        {
            Id = id;
            _root = new GameObject($"Package {id}").transform;
            _root.SetParent(parent, false);
            _currentKind = PackageKind.Normal;
            RebuildVisual(_currentKind);
        }

        public void Update(PackageState package, PlayerState holder)
        {
            if (package.Kind != _currentKind)
            {
                _currentKind = package.Kind;
                RebuildVisual(_currentKind);
            }

            _root.gameObject.SetActive(!package.Delivered);
            if (package.Delivered)
                return;

            Vector3 position = package.RenderPosition;
            if (holder != null)
                position = new Vector3(holder.RenderPosition.x, package.RenderPosition.y, holder.RenderPosition.z);

            if (_visual != null)
                _visual.transform.localRotation = _visualBaseRotation * Quaternion.Euler(package.RenderRotation);

            _root.position = position + Vector3.up * CalculateVisualGroundOffset(package.Kind);
        }

        public void Destroy()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        private void RebuildVisual(PackageKind kind)
        {
            if (_visual != null)
                Object.Destroy(_visual);

            GameObject prefab = WebPortVisuals.Config.GetPackagePrefab(kind);
            if (prefab != null)
            {
                _visual = Object.Instantiate(prefab, _root, false);
                _visual.name = "Visual";
                WebPortVisualConfig.PrefabTransform transformOverride = WebPortVisuals.Config.GetPackageTransform(kind);
                transformOverride.ApplyRelativeTo(_visual.transform, prefab.transform);
                _visualBaseRotation = _visual.transform.localRotation;
                _renderers = _visual.GetComponentsInChildren<Renderer>();
                ConfigureRuntimeColliders(_visual, _renderers);
                return;
            }

            _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _visual.name = "Visual";
            _visual.transform.SetParent(_root, false);
            _visual.transform.localScale = Vector3.one * 24f;
            _visualBaseRotation = _visual.transform.localRotation;
            _renderers = _visual.GetComponentsInChildren<Renderer>();
            _visual.GetComponent<BoxCollider>().isTrigger = true;
            _visual.GetComponent<MeshRenderer>().sharedMaterial = WebPortVisuals.PackageMaterial(kind);
        }

        private float CalculateVisualGroundOffset(PackageKind kind)
        {
            return TryCalculateRendererBounds(_root, _renderers, out Bounds bounds)
                ? Mathf.Max(-bounds.min.y, 0f)
                : WebPortVisuals.Config.GetPackageVisualGroundOffset(kind);
        }

        private static void ConfigureRuntimeColliders(GameObject visual, Renderer[] renderers)
        {
            bool hasUsableCollider = false;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                if (collider is MeshCollider meshCollider && !meshCollider.convex)
                {
                    collider.enabled = false;
                    continue;
                }

                collider.enabled = true;
                collider.isTrigger = true;
                hasUsableCollider = true;
            }

            if (hasUsableCollider || !TryCalculateRendererBounds(visual.transform, renderers, out Bounds bounds))
                return;

            BoxCollider generatedCollider = visual.AddComponent<BoxCollider>();
            generatedCollider.center = bounds.center;
            generatedCollider.size = new Vector3(
                Mathf.Max(bounds.size.x, 0.01f),
                Mathf.Max(bounds.size.y, 0.01f),
                Mathf.Max(bounds.size.z, 0.01f));
            generatedCollider.isTrigger = true;
        }

        private static bool TryCalculateRendererBounds(Transform relativeTo, Renderer[] renderers, out Bounds result)
        {
            result = default;
            if (relativeTo == null || renderers == null || renderers.Length == 0)
                return false;

            bool hasBounds = false;
            Matrix4x4 worldToRelative = relativeTo.worldToLocalMatrix;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Bounds bounds = renderer.localBounds;
                Matrix4x4 matrix = worldToRelative * renderer.transform.localToWorldMatrix;
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                            Vector3 point = matrix.MultiplyPoint3x4(corner);
                            if (!hasBounds)
                            {
                                result = new Bounds(point, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                result.Encapsulate(point);
                            }
                        }
                    }
                }
            }

            return hasBounds;
        }
    }
}
