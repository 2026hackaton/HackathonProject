using UnityEngine;
using UnityEngine.EventSystems;

namespace Hackathon.WebPort
{
    [System.Serializable]
    public sealed class WebPortObstacleEntry
    {
        public Transform root;
        public ObstacleKind kind;
        [Min(1f)] public float radius = 48f;
    }

    public sealed class WebPortSceneLayout : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private WebPortVisualConfig visualConfig;

        [Header("Scene Services")]
        [SerializeField] private UnityEngine.Camera mainCamera;
        [SerializeField] private WebPortCameraRig cameraRig;
        [SerializeField] private Light directionalLight;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private WebPortUiController uiController;
        [SerializeField] private PackageSupplySequence supplySequence;
        [SerializeField] private PackageRefillNotification refillNotification;
        [SerializeField] private DeliveryDoorController deliveryDoorController;

        [Header("World Roots")]
        [SerializeField] private Transform worldRoot;
        [SerializeField] private Transform staticWorldRoot;
        [SerializeField] private Transform playersRoot;
        [SerializeField] private Transform packagesRoot;
        [SerializeField] private Transform effectsRoot;
        [SerializeField] private Transform vehiclesRoot;
        [SerializeField] private Transform aimingRoot;

        [Header("Gameplay Anchors")]
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform[] goalPoints = new Transform[3];
        [SerializeField] private Transform[] packageSpawnPoints = new Transform[0];
        [SerializeField] private WebPortObstacleEntry[] obstacles = new WebPortObstacleEntry[0];
        [SerializeField] private Transform goalMarker;
        [SerializeField] private Transform truck;
        [SerializeField] private Transform bus;

        public WebPortVisualConfig VisualConfig => visualConfig;
        public UnityEngine.Camera MainCamera => mainCamera;
        public WebPortCameraRig CameraRig => cameraRig;
        public Light DirectionalLight => directionalLight;
        public EventSystem EventSystem => eventSystem;
        public WebPortUiController UiController => uiController;
        public PackageSupplySequence SupplySequence => supplySequence;
        public PackageRefillNotification RefillNotification => refillNotification;
        public DeliveryDoorController DeliveryDoorController => deliveryDoorController;
        public Transform WorldRoot => worldRoot;
        public Transform StaticWorldRoot => staticWorldRoot;
        public Transform PlayersRoot => playersRoot;
        public Transform PackagesRoot => packagesRoot;
        public Transform EffectsRoot => effectsRoot;
        public Transform VehiclesRoot => vehiclesRoot;
        public Transform AimingRoot => aimingRoot;
        public Transform GoalMarker => goalMarker;
        public Transform Truck => truck;
        public Transform Bus => bus;
        public int PackageSpawnPointCount => packageSpawnPoints?.Length ?? 0;
        public int ObstacleCount => obstacles?.Length ?? 0;

        public Vector3 StartPosition => startPoint != null ? startPoint.position : WebPortConstants.Start;

        public Vector3 GetGoalPosition(int index)
        {
            if (goalPoints != null && index >= 0 && index < goalPoints.Length && goalPoints[index] != null)
                return goalPoints[index].position;

            if (index >= 0 && index < WebPortConstants.GoalPositions.Length)
                return WebPortConstants.GoalPositions[index];

            return WebPortConstants.GoalPositions[0];
        }

        public Vector3 GetPackageSpawnPosition(int index)
        {
            if (packageSpawnPoints != null && index >= 0 && index < packageSpawnPoints.Length && packageSpawnPoints[index] != null)
                return packageSpawnPoints[index].position;

            int row = index / WebPortConstants.SlotColumns;
            int col = index % WebPortConstants.SlotColumns;
            return StartPosition + new Vector3(
                (col - (WebPortConstants.SlotColumns - 1) * 0.5f) * WebPortConstants.SlotSpacing,
                0f,
                (row - (WebPortConstants.SlotRows - 1) * 0.5f) * WebPortConstants.SlotSpacing);
        }

        public ObstacleData GetObstacle(int index)
        {
            if (obstacles != null && index >= 0 && index < obstacles.Length)
            {
                WebPortObstacleEntry obstacle = obstacles[index];
                if (obstacle != null && obstacle.root != null)
                    return new ObstacleData(obstacle.root.position, Mathf.Max(obstacle.radius, 1f), obstacle.kind);
            }

            if (index >= 0 && index < WebPortConstants.Obstacles.Length)
                return WebPortConstants.Obstacles[index];

            return WebPortConstants.Obstacles[0];
        }

        public bool HasRequiredReferences(out string message)
        {
            if (mainCamera == null)
            {
                message = "WebPortSceneLayout is missing Main Camera.";
                return false;
            }

            if (cameraRig == null)
            {
                message = "WebPortSceneLayout is missing WebPortCameraRig on the Main Camera.";
                return false;
            }

            if (eventSystem == null)
            {
                message = "WebPortSceneLayout is missing EventSystem.";
                return false;
            }

            if (uiController == null)
            {
                message = "WebPortSceneLayout is missing WebPortUiController.";
                return false;
            }

            if (supplySequence == null)
            {
                message = "WebPortSceneLayout is missing PackageSupplySequence.";
                return false;
            }

            if (deliveryDoorController == null)
            {
                message = "WebPortSceneLayout is missing DeliveryDoorController.";
                return false;
            }

            if (worldRoot == null || staticWorldRoot == null || playersRoot == null || packagesRoot == null || effectsRoot == null || vehiclesRoot == null || aimingRoot == null)
            {
                message = "WebPortSceneLayout is missing one or more world roots.";
                return false;
            }

            if (goalMarker == null)
            {
                message = "WebPortSceneLayout is missing Goal Marker.";
                return false;
            }

            if (PackageSpawnPointCount <= 0)
            {
                message = "WebPortSceneLayout has no package spawn points.";
                return false;
            }

            message = null;
            return true;
        }
    }
}
