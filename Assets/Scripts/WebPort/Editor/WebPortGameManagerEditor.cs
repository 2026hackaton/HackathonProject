#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Hackathon.WebPort.Editor
{
    [CustomEditor(typeof(WebPortGameManager))]
    public sealed class WebPortGameManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Edit Mode Layout", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Generate persistent WebPort scene objects in Edit Mode. Runtime map generation stays disabled.", MessageType.Info);

                if (GUILayout.Button("Generate Edit Mode Layout", GUILayout.Height(32f)))
                    WebPortEditModeSceneBuilder.Generate((WebPortGameManager)target);

                if (GUILayout.Button("Validate Layout"))
                    WebPortEditModeSceneBuilder.Validate((WebPortGameManager)target);
            }
        }
    }

    public static class WebPortEditModeSceneBuilder
    {
        private const string GeneratedRootName = "WebPort_EditModeLayout";
        private const string VisualConfigPath = "Assets/Resources/WebPort/WebPortVisualConfig.asset";

        [MenuItem("Tools/WebVersion Port/Generate Edit Mode Layout In Current Scene")]
        public static void GenerateFromMenu()
        {
            WebPortGameManager manager = Object.FindAnyObjectByType<WebPortGameManager>();
            if (manager == null)
            {
                GameObject managerObject = new("WebPortGameManager");
                Undo.RegisterCreatedObjectUndo(managerObject, "Create WebPort Game Manager");
                manager = managerObject.AddComponent<WebPortGameManager>();
            }

            Generate(manager);
        }

        public static void Generate(WebPortGameManager manager)
        {
            if (manager == null)
                return;

            WebPortVisualConfig config = EnsureVisualConfigAsset();
            WebPortVisuals.SetConfig(config);
            RemoveLegacy2DSetup();
            RemoveOldGeneratedRoot();

            GameObject root = new(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Generate WebPort Edit Mode Layout");
            WebPortSceneLayout layout = root.AddComponent<WebPortSceneLayout>();

            Transform services = CreateChild(root.transform, "Services");
            Transform world = CreateChild(root.transform, "World");
            Transform staticWorld = CreateChild(world, "Static World");
            Transform players = CreateChild(world, "Runtime Players");
            Transform packages = CreateChild(world, "Runtime Packages");
            Transform effects = CreateChild(world, "Runtime Effects");
            Transform vehicles = CreateChild(world, "Runtime Vehicles");
            Transform aiming = CreateChild(world, "Runtime Aiming");
            Transform anchors = CreateChild(root.transform, "Gameplay Anchors");
            Transform spawnRoot = CreateChild(anchors, "Package Spawn Points");
            Transform goalsRoot = CreateChild(anchors, "Goal Points");
            Transform obstacleRoot = CreateChild(anchors, "Obstacle Anchors");

            UnityEngine.Camera camera = EnsureCamera(services);
            WebPortCameraRig cameraRig = camera.GetComponent<WebPortCameraRig>();
            Light light = EnsureLight(services);
            EventSystem eventSystem = EnsureEventSystem(services);
            WebPortUiController ui = CreateUiController(services);

            Transform startPoint = CreateAnchor(anchors, "Start Point", WebPortConstants.Start);
            Transform[] goalPoints = CreateGoalPoints(goalsRoot);
            Transform[] spawnPoints = CreatePackageSpawnPoints(spawnRoot, startPoint.position);
            WebPortObstacleEntry[] obstacles = CreateObstacles(staticWorld, obstacleRoot);
            Transform goalMarker = CreateGoalMarker(staticWorld, goalPoints[0].position);
            Transform truck = CreateVehicle(vehicles, "Truck", config.truckPrefab, config.truckTransform, new Vector3(50f, 30f, 26f), WebPortVisuals.CreateLit(config.truckColor, true));
            Transform bus = CreateVehicle(vehicles, "Bus", config.busPrefab, config.busTransform, new Vector3(60f, 34f, 30f), WebPortVisuals.CreateLit(WebPortVisuals.Yellow));

            CreateGround(staticWorld);
            CreateBoundaryWalls(staticWorld, config);
            CreateStartMarker(staticWorld, startPoint.position);

            SerializedObject layoutObject = new(layout);
            Set(layoutObject, "visualConfig", config);
            Set(layoutObject, "mainCamera", camera);
            Set(layoutObject, "cameraRig", cameraRig);
            Set(layoutObject, "directionalLight", light);
            Set(layoutObject, "eventSystem", eventSystem);
            Set(layoutObject, "uiController", ui);
            Set(layoutObject, "worldRoot", world);
            Set(layoutObject, "staticWorldRoot", staticWorld);
            Set(layoutObject, "playersRoot", players);
            Set(layoutObject, "packagesRoot", packages);
            Set(layoutObject, "effectsRoot", effects);
            Set(layoutObject, "vehiclesRoot", vehicles);
            Set(layoutObject, "aimingRoot", aiming);
            Set(layoutObject, "startPoint", startPoint);
            SetArray(layoutObject, "goalPoints", goalPoints);
            SetArray(layoutObject, "packageSpawnPoints", spawnPoints);
            SetObstacleArray(layoutObject, obstacles);
            Set(layoutObject, "goalMarker", goalMarker);
            Set(layoutObject, "truck", truck);
            Set(layoutObject, "bus", bus);
            layoutObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject managerObject = new(manager);
            Set(managerObject, "_layout", layout);
            managerObject.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Selection.activeObject = manager;
            Debug.Log("Generated WebPort edit-mode layout. Save the scene to persist the generated objects.");
        }

        public static void Validate(WebPortGameManager manager)
        {
            SerializedObject managerObject = new(manager);
            WebPortSceneLayout layout = managerObject.FindProperty("_layout").objectReferenceValue as WebPortSceneLayout;
            if (layout == null)
            {
                Debug.LogError("WebPortGameManager has no WebPortSceneLayout assigned.");
                return;
            }

            if (layout.HasRequiredReferences(out string message))
                Debug.Log("WebPortSceneLayout validation passed.");
            else
                Debug.LogError(message);
        }

        private static void RemoveLegacy2DSetup()
        {
            foreach (GameObject obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (!obj.scene.IsValid())
                    continue;

                if (obj.name is "DeliverySceneAutoSetup2D" or "Delivery Scene Auto Setup" or "Auto Delivery Runtime")
                    Undo.DestroyObjectImmediate(obj);
            }
        }

        private static void RemoveOldGeneratedRoot()
        {
            GameObject oldRoot = GameObject.Find(GeneratedRootName);
            if (oldRoot != null)
                Undo.DestroyObjectImmediate(oldRoot);
        }

        private static WebPortVisualConfig EnsureVisualConfigAsset()
        {
            WebPortVisualConfig config = AssetDatabase.LoadAssetAtPath<WebPortVisualConfig>(VisualConfigPath);
            if (config != null)
                return config;

            Directory.CreateDirectory(Path.GetDirectoryName(VisualConfigPath));
            config = ScriptableObject.CreateInstance<WebPortVisualConfig>();
            config.frontMoveTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WebPort/Art/qt_2.png");
            config.frontHoldingMoveTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WebPort/Art/qt_holding_box.png");
            config.sideMoveTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WebPort/Art/qt_right_side.png");
            config.sideHoldingMoveTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WebPort/Art/qt_right_side_holding_box.png");
            AssetDatabase.CreateAsset(config, VisualConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static UnityEngine.Camera EnsureCamera(Transform parent)
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                GameObject obj = new("Main Camera");
                Undo.RegisterCreatedObjectUndo(obj, "Create Main Camera");
                obj.tag = "MainCamera";
                camera = obj.AddComponent<UnityEngine.Camera>();
                obj.AddComponent<AudioListener>();
            }

            camera.transform.SetParent(parent, true);
            camera.transform.position = WebPortConstants.Start + new Vector3(0f, 140f, 420f);
            camera.transform.LookAt(WebPortConstants.Start + Vector3.up * 25f);
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2000f;

            WebPortCameraRig rig = camera.GetComponent<WebPortCameraRig>();
            if (rig == null)
                rig = camera.gameObject.AddComponent<WebPortCameraRig>();

            return camera;
        }

        private static Light EnsureLight(Transform parent)
        {
            Light light = Object.FindAnyObjectByType<Light>();
            if (light == null)
            {
                GameObject obj = new("Directional Light");
                Undo.RegisterCreatedObjectUndo(obj, "Create Directional Light");
                light = obj.AddComponent<Light>();
            }

            light.transform.SetParent(parent, true);
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.position = new Vector3(120f, 300f, 160f);
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            return light;
        }

        private static EventSystem EnsureEventSystem(Transform parent)
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject obj = new("EventSystem");
                Undo.RegisterCreatedObjectUndo(obj, "Create EventSystem");
                eventSystem = obj.AddComponent<EventSystem>();
            }

            eventSystem.transform.SetParent(parent, true);
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            return eventSystem;
        }

        private static WebPortUiController CreateUiController(Transform parent)
        {
            GameObject obj = new("WebPort UI");
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<WebPortUiController>();
        }

        private static void CreateGround(Transform parent)
        {
            CreatePlane(parent, "Ground Base", new Vector3(0f, -0.02f, 0f), new Vector3(WebPortConstants.ArmLength * 2f, 0.08f, WebPortConstants.ArmLength * 2f), WebPortVisuals.GroundBaseMaterial());
            CreatePlane(parent, "Cross Hub", new Vector3(0f, 0.02f, 0f), new Vector3(WebPortConstants.ArmHalfWidth * 2f, 0.08f, WebPortConstants.ArmHalfWidth * 2f), WebPortVisuals.CrossGroundMaterial());
            CreatePlane(parent, "Cross Horizontal", new Vector3(0f, 0.03f, 0f), new Vector3(WebPortConstants.ArmLength * 2f, 0.08f, WebPortConstants.ArmHalfWidth * 2f), WebPortVisuals.CrossGroundMaterial());
            CreatePlane(parent, "Cross Vertical", new Vector3(0f, 0.04f, 0f), new Vector3(WebPortConstants.ArmHalfWidth * 2f, 0.08f, WebPortConstants.ArmLength * 2f), WebPortVisuals.CrossGroundMaterial());
        }

        private static void CreatePlane(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject root = CreateChild(parent, name).gameObject;
            root.transform.position = position;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = scale;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }

        private static void CreateBoundaryWalls(Transform parent, WebPortVisualConfig config)
        {
            if (!config.createBoundaryWalls)
                return;

            Bounds bounds = new(Vector3.zero, new Vector3(WebPortConstants.ArmLength * 2f, 0.1f, WebPortConstants.ArmLength * 2f));
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

            CreatePlane(parent, "Boundary Wall North", new Vector3(bounds.center.x, centerY, maxZ + thickness * 0.5f), new Vector3(width, height, thickness), material);
            CreatePlane(parent, "Boundary Wall South", new Vector3(bounds.center.x, centerY, minZ - thickness * 0.5f), new Vector3(width, height, thickness), material);
            CreatePlane(parent, "Boundary Wall East", new Vector3(maxX + thickness * 0.5f, centerY, bounds.center.z), new Vector3(thickness, height, depth), material);
            CreatePlane(parent, "Boundary Wall West", new Vector3(minX - thickness * 0.5f, centerY, bounds.center.z), new Vector3(thickness, height, depth), material);
        }

        private static void CreateStartMarker(Transform parent, Vector3 position)
        {
            GameObject marker = CreateChild(parent, "Start Marker").gameObject;
            marker.transform.position = position + Vector3.up * 0.6f;
            AddMesh(marker, WebPortVisuals.CreateRingMesh(70f, 78f, 40), WebPortVisuals.StartMarkerMaterial(0.6f));
        }

        private static Transform CreateGoalMarker(Transform parent, Vector3 position)
        {
            Transform goalRoot = CreateChild(parent, "Goal Marker");
            goalRoot.position = position;

            GameObject fill = CreateChild(goalRoot, "Goal Fill").gameObject;
            fill.transform.localPosition = Vector3.up * 0.6f;
            AddMesh(fill, WebPortVisuals.CreateDiscMesh(WebPortConstants.GoalRadius, 40), WebPortVisuals.GoalFillMaterial(0.35f));

            GameObject ring = CreateChild(goalRoot, "Goal Ring").gameObject;
            ring.transform.localPosition = Vector3.up * 0.7f;
            AddMesh(ring, WebPortVisuals.CreateRingMesh(51f, 55f, 40), WebPortVisuals.GoalRingMaterial(0.9f));
            return goalRoot;
        }

        private static Transform[] CreateGoalPoints(Transform parent)
        {
            Transform[] goals = new Transform[WebPortConstants.GoalPositions.Length];
            for (int i = 0; i < goals.Length; i++)
                goals[i] = CreateAnchor(parent, $"Goal Point {i}", WebPortConstants.GoalPositions[i]);
            return goals;
        }

        private static Transform[] CreatePackageSpawnPoints(Transform parent, Vector3 start)
        {
            int count = WebPortConstants.SlotColumns * WebPortConstants.SlotRows;
            Transform[] points = new Transform[count];
            for (int row = 0; row < WebPortConstants.SlotRows; row++)
            {
                for (int col = 0; col < WebPortConstants.SlotColumns; col++)
                {
                    int index = row * WebPortConstants.SlotColumns + col;
                    Vector3 position = start + new Vector3(
                        (col - (WebPortConstants.SlotColumns - 1) * 0.5f) * WebPortConstants.SlotSpacing,
                        0f,
                        (row - (WebPortConstants.SlotRows - 1) * 0.5f) * WebPortConstants.SlotSpacing);
                    points[index] = CreateAnchor(parent, $"Package Spawn {index}", position);
                }
            }

            return points;
        }

        private static WebPortObstacleEntry[] CreateObstacles(Transform staticParent, Transform anchorParent)
        {
            WebPortObstacleEntry[] entries = new WebPortObstacleEntry[WebPortConstants.Obstacles.Length];
            for (int i = 0; i < WebPortConstants.Obstacles.Length; i++)
            {
                ObstacleData data = WebPortConstants.Obstacles[i];
                Transform anchor = CreateAnchor(anchorParent, $"Obstacle {i} {data.Kind}", data.Position);
                CreateObstacleVisual(staticParent, anchor, data.Kind, data.Radius, i);
                entries[i] = new WebPortObstacleEntry
                {
                    root = anchor,
                    kind = data.Kind,
                    radius = data.Radius,
                };
            }

            return entries;
        }

        private static void CreateObstacleVisual(Transform parent, Transform anchor, ObstacleKind kind, float radius, int index)
        {
            GameObject root = CreateChild(parent, $"Obstacle Visual {index} {kind}").gameObject;
            root.transform.position = anchor.position;

            GameObject prefab = WebPortVisuals.Config.GetObstaclePrefab(kind);
            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = "Visual";
                WebPortVisuals.Config.GetObstacleTransform(kind).ApplyTo(instance.transform);
                RemoveColliders(instance);
                return;
            }

            GameObject visual = kind switch
            {
                ObstacleKind.Rock => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                ObstacleKind.Wall => GameObject.CreatePrimitive(PrimitiveType.Cube),
                _ => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
            };

            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            if (kind == ObstacleKind.Wall)
                visual.transform.localScale = new Vector3(radius * 2.1f, radius * 1.1f, radius * 0.9f);
            else if (kind == ObstacleKind.Rock)
                visual.transform.localScale = Vector3.one * radius * 1.55f;
            else
                visual.transform.localScale = new Vector3(radius * 2f, radius * 0.9f, radius * 2.2f);
            visual.transform.localPosition = Vector3.up * radius * 0.6f;
            visual.GetComponent<MeshRenderer>().sharedMaterial = WebPortVisuals.ObstacleMaterial(kind);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }

        private static Transform CreateVehicle(Transform parent, string name, GameObject prefab, WebPortVisualConfig.PrefabTransform prefabTransform, Vector3 size, Material fallbackMaterial)
        {
            Transform root = CreateChild(parent, name);
            GameObject visual;
            if (prefab != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                visual.name = "Visual";
                prefabTransform.ApplyTo(visual.transform);
                RemoveColliders(visual);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(root, false);
                visual.transform.localScale = size;
                visual.GetComponent<MeshRenderer>().sharedMaterial = fallbackMaterial;
                Object.DestroyImmediate(visual.GetComponent<Collider>());
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 position)
        {
            Transform anchor = CreateChild(parent, name);
            anchor.position = position;
            return anchor;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject obj = new(name);
            obj.transform.SetParent(parent, false);
            return obj.transform;
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

        private static void RemoveColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        private static void Set(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serializedObject, string propertyName, Transform[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void SetObstacleArray(SerializedObject serializedObject, WebPortObstacleEntry[] values)
        {
            SerializedProperty property = serializedObject.FindProperty("obstacles");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = values[i].root;
                element.FindPropertyRelative("kind").enumValueIndex = (int)values[i].kind;
                element.FindPropertyRelative("radius").floatValue = values[i].radius;
            }
        }
    }
}
#endif
