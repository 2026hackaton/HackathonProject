using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortWorldView
    {
        private readonly Transform _root;
        private Transform _goalRoot;

        public WebPortWorldView(Transform root)
        {
            _root = root;
            CreateGround();
            CreateBoundaryWalls();
            CreateMarkers();
            CreateObstacles();
        }

        public void SetGoal(Vector3 goal)
        {
            _goalRoot.position = new Vector3(goal.x, 0f, goal.z);
        }

        private void CreateGround()
        {
            Material baseMaterial = WebPortVisuals.GroundBaseMaterial();
            Material crossMaterial = WebPortVisuals.CrossGroundMaterial();

            CreatePlane("Ground Base", new Vector3(0f, -0.02f, 0f), new Vector3(WebPortConstants.ArmLength * 2f, 1f, WebPortConstants.ArmLength * 2f), baseMaterial);
            CreatePlane("Cross Hub", new Vector3(0f, 0.02f, 0f), new Vector3(WebPortConstants.ArmHalfWidth * 2f, 1f, WebPortConstants.ArmHalfWidth * 2f), crossMaterial);
            CreatePlane("Cross Horizontal", new Vector3(0f, 0.03f, 0f), new Vector3(WebPortConstants.ArmLength * 2f, 1f, WebPortConstants.ArmHalfWidth * 2f), crossMaterial);
            CreatePlane("Cross Vertical", new Vector3(0f, 0.04f, 0f), new Vector3(WebPortConstants.ArmHalfWidth * 2f, 1f, WebPortConstants.ArmLength * 2f), crossMaterial);
        }

        private void CreatePlane(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plane.name = name;
            plane.transform.SetParent(_root, false);
            plane.transform.position = position;
            plane.transform.localScale = new Vector3(scale.x, 0.08f, scale.z);
            plane.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.Destroy(plane.GetComponent<BoxCollider>());
        }

        private void CreateBoundaryWalls()
        {
            WebPortVisualConfig config = WebPortVisuals.Config;
            if (!config.createBoundaryWalls)
                return;

            Bounds bounds = CalculateMapBounds();
            float thickness = Mathf.Max(config.boundaryWallThickness, 1f);
            float height = Mathf.Max(config.boundaryWallHeight, 1f);
            float padding = Mathf.Max(config.boundaryWallPadding, 0f);
            float centerY = height * 0.5f;
            float minX = bounds.min.x - padding;
            float maxX = bounds.max.x + padding;
            float minZ = bounds.min.z - padding;
            float maxZ = bounds.max.z + padding;
            float width = maxX - minX + thickness * 2f;
            float depth = maxZ - minZ + thickness * 2f;
            Material material = WebPortVisuals.BoundaryWallMaterial();

            CreateWall("Boundary Wall North", new Vector3(bounds.center.x, centerY, maxZ + thickness * 0.5f), new Vector3(width, height, thickness), material);
            CreateWall("Boundary Wall South", new Vector3(bounds.center.x, centerY, minZ - thickness * 0.5f), new Vector3(width, height, thickness), material);
            CreateWall("Boundary Wall East", new Vector3(maxX + thickness * 0.5f, centerY, bounds.center.z), new Vector3(thickness, height, depth), material);
            CreateWall("Boundary Wall West", new Vector3(minX - thickness * 0.5f, centerY, bounds.center.z), new Vector3(thickness, height, depth), material);
        }

        private static Bounds CalculateMapBounds()
        {
            Bounds bounds = CreateRectBounds(Vector3.zero, WebPortConstants.ArmLength * 2f, WebPortConstants.ArmLength * 2f);
            EncapsulateRect(ref bounds, Vector3.zero, WebPortConstants.ArmHalfWidth * 2f, WebPortConstants.ArmHalfWidth * 2f);
            EncapsulateRect(ref bounds, Vector3.zero, WebPortConstants.ArmLength * 2f, WebPortConstants.ArmHalfWidth * 2f);
            EncapsulateRect(ref bounds, Vector3.zero, WebPortConstants.ArmHalfWidth * 2f, WebPortConstants.ArmLength * 2f);
            return bounds;
        }

        private static Bounds CreateRectBounds(Vector3 center, float width, float depth)
        {
            return new Bounds(center, new Vector3(width, 0.1f, depth));
        }

        private static void EncapsulateRect(ref Bounds bounds, Vector3 center, float width, float depth)
        {
            bounds.Encapsulate(CreateRectBounds(center, width, depth));
        }

        private void CreateWall(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(_root, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.Destroy(wall.GetComponent<BoxCollider>());
        }

        private void CreateMarkers()
        {
            GameObject start = new("Start Marker");
            start.transform.SetParent(_root, false);
            start.transform.position = WebPortConstants.Start + Vector3.up * 0.6f;
            AddMesh(start, WebPortVisuals.CreateRingMesh(70f, 78f, 40), WebPortVisuals.StartMarkerMaterial(0.6f));

            _goalRoot = new GameObject("Goal Marker").transform;
            _goalRoot.SetParent(_root, false);
            _goalRoot.position = WebPortConstants.GoalPositions[0];

            GameObject goalFill = new("Goal Fill");
            goalFill.transform.SetParent(_goalRoot, false);
            goalFill.transform.localPosition = Vector3.up * 0.6f;
            AddMesh(goalFill, WebPortVisuals.CreateDiscMesh(WebPortConstants.GoalRadius, 40), WebPortVisuals.GoalFillMaterial(0.35f));

            GameObject goalRing = new("Goal Ring");
            goalRing.transform.SetParent(_goalRoot, false);
            goalRing.transform.localPosition = Vector3.up * 0.7f;
            AddMesh(goalRing, WebPortVisuals.CreateRingMesh(51f, 55f, 40), WebPortVisuals.GoalRingMaterial(0.9f));
        }

        private void CreateObstacles()
        {
            IReadOnlyList<ObstacleData> obstacles = WebPortConstants.Obstacles;
            for (int i = 0; i < obstacles.Count; i++)
                CreateObstacle(i, obstacles[i]);
        }

        private void CreateObstacle(int index, ObstacleData obstacle)
        {
            GameObject root = new($"Obstacle {index} {obstacle.Kind}");
            root.transform.SetParent(_root, false);
            root.transform.position = obstacle.Position;

            GameObject prefab = WebPortVisuals.Config.GetObstaclePrefab(obstacle.Kind);
            if (prefab != null)
            {
                GameObject instance = Object.Instantiate(prefab, root.transform);
                instance.name = "Visual";
                WebPortVisuals.Config.GetObstacleTransform(obstacle.Kind).ApplyTo(instance.transform);
                RemoveColliders(instance);
                return;
            }

            GameObject visual;
            Material material;

            switch (obstacle.Kind)
            {
                case ObstacleKind.Wall:
                    visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.transform.localScale = new Vector3(obstacle.Radius * 2.1f, obstacle.Radius * 1.1f, obstacle.Radius * 0.9f);
                    visual.transform.localPosition = Vector3.up * obstacle.Radius * 0.55f;
                    material = WebPortVisuals.ObstacleMaterial(ObstacleKind.Wall);
                    break;
                case ObstacleKind.Rock:
                    visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    visual.transform.localScale = Vector3.one * obstacle.Radius * 1.55f;
                    visual.transform.localPosition = Vector3.up * obstacle.Radius * 0.6f;
                    visual.transform.localRotation = Quaternion.Euler(17f, 34f, 6f);
                    material = WebPortVisuals.ObstacleMaterial(ObstacleKind.Rock);
                    break;
                default:
                    visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    visual.transform.localScale = new Vector3(obstacle.Radius * 2f, obstacle.Radius * 0.9f, obstacle.Radius * 2.2f);
                    visual.transform.localPosition = Vector3.up * obstacle.Radius * 0.9f;
                    material = WebPortVisuals.ObstacleMaterial(ObstacleKind.Pillar);
                    break;
            }

            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.Destroy(visual.GetComponent<Collider>());
        }

        private static void RemoveColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                Object.Destroy(collider);
        }

        private static void AddMesh(GameObject target, Mesh mesh, Material material)
        {
            MeshFilter filter = target.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

    }
}
