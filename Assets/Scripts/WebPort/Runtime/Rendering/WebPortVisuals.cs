using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    public static class WebPortVisuals
    {
        private static WebPortVisualConfig _config;

        public static WebPortVisualConfig Config => _config != null ? _config : _config = WebPortVisualConfig.LoadOrCreateRuntime();

        public static Color PageBackground => Config.pageBackground;
        public static Color GroundBase => Config.groundBase;
        public static Color StartBlue => Config.startBlue;
        public static Color GoalGreen => Config.goalGreen;
        public static Color TextDark => Config.textDark;
        public static Color Muted => Config.muted;
        public static Color Yellow => Config.yellow;
        public static Color Orange => Config.orange;
        public static Color Red => Config.red;

        public static Color PackageColor(PackageKind kind)
        {
            return Config.GetPackageColor(kind);
        }

        public static Material PackageMaterial(PackageKind kind)
        {
            Material configured = Config.GetPackageMaterial(kind);
            return configured != null ? configured : CreateLit(PackageColor(kind));
        }

        public static Material GroundBaseMaterial()
        {
            return Config.groundBaseMaterial != null ? Config.groundBaseMaterial : CreateLit(GroundBase);
        }

        public static Material CrossGroundMaterial()
        {
            return Config.crossGroundMaterial != null ? Config.crossGroundMaterial : CreateLit(PageBackground);
        }

        public static Material BoundaryWallMaterial()
        {
            return Config.boundaryWallMaterial != null ? Config.boundaryWallMaterial : CreateLit(Config.boundaryWallColor);
        }

        public static Material StartMarkerMaterial(float fallbackAlpha)
        {
            if (Config.startMarkerMaterial != null)
                return Config.startMarkerMaterial;
            return CreateUnlit(StartBlue.WithAlphaCompat(fallbackAlpha), true);
        }

        public static Material GoalFillMaterial(float fallbackAlpha)
        {
            if (Config.goalFillMaterial != null)
                return Config.goalFillMaterial;
            return CreateUnlit(GoalGreen.WithAlphaCompat(fallbackAlpha), true);
        }

        public static Material GoalRingMaterial(float fallbackAlpha)
        {
            if (Config.goalRingMaterial != null)
                return Config.goalRingMaterial;
            return CreateUnlit(GoalGreen.WithAlphaCompat(fallbackAlpha), true);
        }

        public static Material ObstacleMaterial(ObstacleKind kind, bool transparent = true)
        {
            Material configured = Config.GetObstacleMaterial(kind);
            return configured != null ? configured : CreateLit(Config.GetObstacleColor(kind), transparent);
        }

        public static Color Html(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }

        public static Material CreateLit(Color color, bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new(shader);
            SetMaterialColor(material, color);

            if (transparent)
                MakeTransparent(material);

            return material;
        }

        public static Material CreateUnlit(Color color, bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new(shader);
            SetMaterialColor(material, color);

            if (transparent)
                MakeTransparent(material);

            return material;
        }

        public static Material CreateSpriteMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

            Material material = new(shader);
            if (texture == null)
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);

            SetMaterialColor(material, Color.white);
            MakeTransparent(material);
            return material;
        }

        public static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        public static void SetTextureOffset(Material material, Vector2 scale, Vector2 offset)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_MainTex", scale);
                material.SetTextureOffset("_MainTex", offset);
            }
        }

        public static Mesh CreateRingMesh(float innerRadius, float outerRadius, int segments, float startAngle = 0f, float arcAngle = Mathf.PI * 2f)
        {
            segments = Mathf.Max(3, segments);
            List<Vector3> vertices = new((segments + 1) * 2);
            List<int> triangles = new(segments * 6);
            float step = arcAngle / segments;

            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + step * i;
                float c = Mathf.Cos(angle);
                float s = Mathf.Sin(angle);
                vertices.Add(new Vector3(c * innerRadius, 0f, s * innerRadius));
                vertices.Add(new Vector3(c * outerRadius, 0f, s * outerRadius));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
                triangles.Add(a + 3);
                triangles.Add(a + 2);
            }

            Mesh mesh = new();
            mesh.name = "WebPortRing";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        public static Mesh CreateDiscMesh(float radius, int segments)
        {
            segments = Mathf.Max(3, segments);
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;

            for (int i = 0; i < segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i == segments - 1 ? 1 : i + 2;
            }

            Mesh mesh = new();
            mesh.name = "WebPortDisc";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        public static Mesh CreateQuadMesh(float width, float height)
        {
            Mesh mesh = new();
            mesh.name = "WebPortQuad";
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(halfWidth, -halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void MakeTransparent(Material material)
        {
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }
}
