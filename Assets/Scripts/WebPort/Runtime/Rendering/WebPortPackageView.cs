using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortPackageView
    {
        private readonly Transform _root;
        private GameObject _visual;
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

            _root.position = position + Vector3.up * 12f;
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
                _visual = Object.Instantiate(prefab, _root);
                _visual.name = "Visual";
                WebPortVisuals.Config.GetPackageTransform(kind).ApplyTo(_visual.transform);
                RemoveColliders(_visual);
                return;
            }

            _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _visual.name = "Visual";
            _visual.transform.SetParent(_root, false);
            _visual.transform.localScale = Vector3.one * 24f;
            Object.Destroy(_visual.GetComponent<Collider>());
            _visual.GetComponent<MeshRenderer>().sharedMaterial = WebPortVisuals.PackageMaterial(kind);
        }

        private static void RemoveColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                Object.Destroy(collider);
        }
    }
}
