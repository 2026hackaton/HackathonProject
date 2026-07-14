using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortVehicleView
    {
        private readonly Transform _truck;
        private readonly Material _truckMaterial;
        private readonly Transform _bus;
        private readonly Material _busMaterial;

        private float _truckStartedAt = -1f;
        private float _busStartedAt = -1f;

        public WebPortVehicleView(Transform parent)
        {
            WebPortVisualConfig config = WebPortVisuals.Config;
            _truckMaterial = config.truckMaterial != null ? Object.Instantiate(config.truckMaterial) : WebPortVisuals.CreateLit(config.truckColor, true);
            _busMaterial = config.busMaterial != null ? Object.Instantiate(config.busMaterial) : WebPortVisuals.CreateLit(WebPortVisuals.Yellow);
            _truck = CreateVehicle("Truck", config.truckPrefab, config.truckTransform, new Vector3(50f, 30f, 26f), _truckMaterial, parent);
            _bus = CreateVehicle("Bus", config.busPrefab, config.busTransform, new Vector3(60f, 34f, 30f), _busMaterial, parent);
            _truck.gameObject.SetActive(false);
            _bus.gameObject.SetActive(false);
        }

        public void PlayTruck()
        {
            _truckStartedAt = Time.time;
        }

        public void PlayBus()
        {
            _busStartedAt = Time.time;
        }

        public void Update(Vector3 start, Vector3 goal)
        {
            UpdateTruck(goal);
            UpdateBus(start);
        }

        private void UpdateTruck(Vector3 goal)
        {
            if (_truckStartedAt < 0f)
            {
                _truck.gameObject.SetActive(false);
                return;
            }

            const float duration = 2.2f;
            float p = (Time.time - _truckStartedAt) / duration;
            if (p >= 1f)
            {
                _truckStartedAt = -1f;
                _truck.gameObject.SetActive(false);
                return;
            }

            _truck.gameObject.SetActive(true);
            _truck.position = goal + new Vector3(p * 260f, 16f, 0f);
            if (_truckMaterial != null)
                WebPortVisuals.SetMaterialColor(_truckMaterial, WebPortVisuals.Config.truckColor.WithAlphaCompat(Mathf.Max(1f - p, 0f)));
        }

        private void UpdateBus(Vector3 start)
        {
            if (_busStartedAt < 0f)
            {
                _bus.gameObject.SetActive(false);
                return;
            }

            const float offset = 260f;
            const float duration = 2.5f;
            float p = (Time.time - _busStartedAt) / duration;
            if (p >= 1f)
            {
                _busStartedAt = -1f;
                _bus.gameObject.SetActive(false);
                return;
            }

            float x;
            if (p < 0.4f)
                x = start.x - offset + (p / 0.4f) * offset;
            else if (p < 0.7f)
                x = start.x;
            else
                x = start.x - ((p - 0.7f) / 0.3f) * offset;

            _bus.gameObject.SetActive(true);
            _bus.position = new Vector3(x, 18f, start.z);
        }

        private static Transform CreateVehicle(string name, GameObject prefab, WebPortVisualConfig.PrefabTransform prefabTransform, Vector3 size, Material material, Transform parent)
        {
            GameObject obj = new(name);
            obj.transform.SetParent(parent, false);

            GameObject visual = prefab != null ? Object.Instantiate(prefab, obj.transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(obj.transform, false);
            if (prefab == null)
            {
                visual.transform.localScale = size;
                visual.GetComponent<MeshRenderer>().sharedMaterial = material;
                Object.Destroy(visual.GetComponent<Collider>());
            }
            else
            {
                prefabTransform.ApplyTo(visual.transform);
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>())
                    Object.Destroy(collider);
            }
            return obj.transform;
        }
    }
}
