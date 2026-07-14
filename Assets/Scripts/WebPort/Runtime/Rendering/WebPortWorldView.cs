using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class WebPortWorldView
    {
        // 장식용(비게임플레이) 오브젝트는 개별 material을 복제/보존할 필요 없이, 카메라에서
        // 플레이어 발 쪽으로 쏜 레이에 실제로 맞으면 미리 만들어둔 반투명 material로 통째로
        // 바꿔치기했다가, 안 맞으면 원래 material로 되돌리는 단순한 방식으로 처리한다. 근접
        // 판정이 아니라 실제 레이캐스트라 시야를 진짜로 가리는 오브젝트만 반투명해진다 -
        // 그래서 decorationsRoot 밑 오브젝트에는 콜라이더가 있어야 한다(레이가 맞아야 하니까).
        private sealed class FadeableDecoration
        {
            public Renderer[] Renderers;
            public Material[][] OriginalMaterials;
            public bool IsFaded;
        }

        private const int MaxDecorationRayHits = 8;

        private readonly Transform _root;
        private readonly List<FadeableDecoration> _fadeableDecorations = new();
        private readonly Dictionary<Collider, FadeableDecoration> _decorationByCollider = new();
        private readonly RaycastHit[] _decorationRayHits = new RaycastHit[MaxDecorationRayHits];
        private Material _decorationFadeMaterial;
        private Transform _goalRoot;

        public WebPortWorldView(Transform root)
        {
            _root = root;
            CreateGround();
            CreateBoundaryWalls();
            CreateMarkers();
        }

        public WebPortWorldView(Transform root, Transform goalRoot, WebPortSceneLayout layout = null)
        {
            _root = root;
            _goalRoot = goalRoot;
            if (layout != null)
                BuildFadeableDecorations(layout);
        }

        public void SetGoal(Vector3 goal)
        {
            if (_goalRoot != null)
                _goalRoot.position = new Vector3(goal.x, 0f, goal.z);
        }

        public void UpdateDecorationFade(UnityEngine.Camera camera, PlayerState self, IReadOnlyDictionary<int, PlayerState> players)
        {
            if (camera == null || _fadeableDecorations.Count == 0)
                return;

            HashSet<FadeableDecoration> hit = new();
            if (self != null)
                CastToward(camera.transform.position, self.Position, hit);
            foreach (PlayerState player in players.Values)
            {
                if (self != null && player.Id == self.Id)
                    continue;

                CastToward(camera.transform.position, player.RenderPosition, hit);
            }

            foreach (FadeableDecoration decoration in _fadeableDecorations)
            {
                bool shouldFade = hit.Contains(decoration);
                if (shouldFade == decoration.IsFaded)
                    continue;

                decoration.IsFaded = shouldFade;
                for (int r = 0; r < decoration.Renderers.Length; r++)
                {
                    if (shouldFade)
                    {
                        Material[] faded = new Material[decoration.OriginalMaterials[r].Length];
                        for (int m = 0; m < faded.Length; m++)
                            faded[m] = _decorationFadeMaterial;
                        decoration.Renderers[r].sharedMaterials = faded;
                    }
                    else
                    {
                        decoration.Renderers[r].sharedMaterials = decoration.OriginalMaterials[r];
                    }
                }
            }
        }

        private void CastToward(Vector3 origin, Vector3 target, HashSet<FadeableDecoration> result)
        {
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (distance < 0.001f)
                return;

            int count = Physics.RaycastNonAlloc(origin, delta / distance, _decorationRayHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_decorationByCollider.TryGetValue(_decorationRayHits[i].collider, out FadeableDecoration decoration))
                    result.Add(decoration);
            }
        }

        private void BuildFadeableDecorations(WebPortSceneLayout layout)
        {
            Transform decorationsRoot = layout.DecorationsRoot;
            if (decorationsRoot == null || decorationsRoot.childCount == 0)
                return;

            _decorationFadeMaterial = WebPortVisuals.CreateUnlit(Color.white.WithAlphaCompat(WebPortConstants.OcclusionFadedOpacity), true);

            for (int i = 0; i < decorationsRoot.childCount; i++)
            {
                Transform child = decorationsRoot.GetChild(i);
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
                Collider[] colliders = child.GetComponentsInChildren<Collider>();
                if (renderers.Length == 0 || colliders.Length == 0)
                    continue;

                Material[][] originalMaterials = new Material[renderers.Length][];
                for (int r = 0; r < renderers.Length; r++)
                    originalMaterials[r] = renderers[r].sharedMaterials;

                FadeableDecoration decoration = new()
                {
                    Renderers = renderers,
                    OriginalMaterials = originalMaterials,
                };
                _fadeableDecorations.Add(decoration);
                foreach (Collider collider in colliders)
                    _decorationByCollider[collider] = decoration;
            }
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
