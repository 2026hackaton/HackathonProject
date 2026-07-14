#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hackathon.WebPort.Editor
{
    public static class WebPortProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/WebVersionPort.unity";
        private const string VisualConfigPath = "Assets/Resources/WebPort/WebPortVisualConfig.asset";

        [InitializeOnLoadMethod]
        private static void EnsureDefaultAssetsAfterReload()
        {
            EditorApplication.delayCall -= EnsureVisualConfigAssetAfterReload;
            EditorApplication.delayCall += EnsureVisualConfigAssetAfterReload;
        }

        private static void EnsureVisualConfigAssetAfterReload()
        {
            EnsureVisualConfigAsset();
        }

        [MenuItem("Tools/WebVersion Port/Rebuild Scene")]
        public static void RebuildScene()
        {
            ConfigureTextureImports();
            EnsureVisualConfigAsset();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = new("WebVersion Port");
            root.AddComponent<Hackathon.WebPort.WebPortGameManager>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"WebVersion port scene rebuilt: {ScenePath}");
        }

        [MenuItem("Tools/WebVersion Port/Select Visual Config")]
        public static void SelectVisualConfig()
        {
            Selection.activeObject = EnsureVisualConfigAsset();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Tools/WebVersion Port/Configure Sprite Imports")]
        public static void ConfigureTextureImports()
        {
            ConfigureFolder("Assets/Resources/WebPort/Art");
            ConfigureFolder("Assets/Art/Sprites");
            AssetDatabase.SaveAssets();
        }

        private static Hackathon.WebPort.WebPortVisualConfig EnsureVisualConfigAsset()
        {
            Hackathon.WebPort.WebPortVisualConfig config = AssetDatabase.LoadAssetAtPath<Hackathon.WebPort.WebPortVisualConfig>(VisualConfigPath);
            if (config != null)
                return config;

            Directory.CreateDirectory(Path.GetDirectoryName(VisualConfigPath));
            config = ScriptableObject.CreateInstance<Hackathon.WebPort.WebPortVisualConfig>();
            config.idleSprite = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WebPort/Art/qt_2.png");
            config.holdingBoxSprite = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WebPort/Art/qt_holding_box.png");
            config.sideSprite = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WebPort/Art/qt_right_side.png");
            config.sideHoldingBoxSprite = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/WebPort/Art/qt_right_side_holding_box.png");
            AssetDatabase.CreateAsset(config, VisualConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return config;
        }

        private static void ConfigureFolder(string folder)
        {
            if (!Directory.Exists(folder))
                return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        private static void EnsureSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new();
            bool found = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ScenePath)
                    found = true;
                scenes.Add(scene);
            }

            if (!found)
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
