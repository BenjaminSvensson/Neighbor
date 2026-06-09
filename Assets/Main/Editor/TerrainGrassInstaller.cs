#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class TerrainGrassInstaller
{
    private const string MenuRoot = "Tools/Neighbor/Terrain Grass/";
    private const string GrassFolder = "Assets/Main/Art/Terrain/Grass";
    private const string GrassTexturePath = GrassFolder + "/NeighborGrass.png";
    private const int TextureSize = 256;

    [InitializeOnLoadMethod]
    private static void ScheduleGrassAssetCreation()
    {
        EditorApplication.delayCall += EnsureGrassAsset;
    }

    [MenuItem(MenuRoot + "Create or Refresh Grass Texture")]
    private static void CreateOrRefreshGrassTexture()
    {
        GenerateGrassTexture();
        Debug.Log($"Created terrain grass texture at '{GrassTexturePath}'.");
    }

    [MenuItem(MenuRoot + "Add Grass to Selected Terrain")]
    private static void AddGrassToSelectedTerrain()
    {
        Terrain terrain = GetSelectedTerrain();
        if (terrain == null)
        {
            Debug.LogWarning("Select a Terrain object before adding grass.");
            return;
        }

        EnsureGrassAsset();
        Texture2D grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
        if (grassTexture == null)
        {
            Debug.LogError($"Could not load the grass texture at '{GrassTexturePath}'.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        DetailPrototype[] prototypes = terrainData.detailPrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prototypeTexture == grassTexture)
            {
                Debug.Log($"'{terrain.name}' already contains Neighbor Grass as detail layer {i}.", terrain);
                return;
            }
        }

        Undo.RegisterCompleteObjectUndo(terrainData, "Add Neighbor Grass");

        DetailPrototype grass = new DetailPrototype
        {
            prototypeTexture = grassTexture,
            minWidth = 0.55f,
            maxWidth = 0.95f,
            minHeight = 0.7f,
            maxHeight = 1.25f,
            noiseSeed = 47321,
            noiseSpread = 0.18f,
            healthyColor = new Color(0.82f, 1f, 0.78f, 1f),
            dryColor = new Color(1f, 0.84f, 0.58f, 1f),
            renderMode = DetailRenderMode.GrassBillboard,
            usePrototypeMesh = false,
            useInstancing = false
        };

        Array.Resize(ref prototypes, prototypes.Length + 1);
        prototypes[prototypes.Length - 1] = grass;
        terrainData.detailPrototypes = prototypes;
        terrain.detailObjectDistance = Mathf.Max(terrain.detailObjectDistance, 90f);
        terrain.detailObjectDensity = Mathf.Max(terrain.detailObjectDensity, 0.8f);

        EditorUtility.SetDirty(terrainData);
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Added Neighbor Grass to '{terrain.name}'. Use Paint Details in the Terrain inspector to paint it.",
            terrain);
    }

    [MenuItem(MenuRoot + "Add Grass to Selected Terrain", true)]
    private static bool CanAddGrassToSelectedTerrain()
    {
        return GetSelectedTerrain() != null;
    }

    public static void GenerateFromCommandLine()
    {
        GenerateGrassTexture();
        EditorApplication.Exit(0);
    }

    private static Terrain GetSelectedTerrain()
    {
        if (Selection.activeGameObject == null)
            return null;

        return Selection.activeGameObject.GetComponent<Terrain>();
    }

    private static void EnsureGrassAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath) == null)
            GenerateGrassTexture();
    }

    private static void GenerateGrassTexture()
    {
        EnsureFolderExists(GrassFolder);

        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.name = "NeighborGrass";
        Color32[] pixels = new Color32[TextureSize * TextureSize];
        var random = new System.Random(47321);

        for (int clump = 0; clump < 34; clump++)
        {
            float rootX = 18f + (float)random.NextDouble() * (TextureSize - 36f);
            float rootY = 4f + (float)random.NextDouble() * 24f;
            float height = 90f + (float)random.NextDouble() * 150f;
            float lean = ((float)random.NextDouble() - 0.5f) * 75f;
            float width = 2.2f + (float)random.NextDouble() * 4.2f;
            Color32 color = RandomGrassColor(random);

            DrawBlade(pixels, rootX, rootY, rootX + lean, Mathf.Min(rootY + height, TextureSize - 3f), width, color);
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        string absolutePath = Path.GetFullPath(GrassTexturePath);
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(GrassTexturePath, ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporter();
        AssetDatabase.SaveAssets();
    }

    private static void DrawBlade(
        Color32[] pixels,
        float rootX,
        float rootY,
        float tipX,
        float tipY,
        float rootWidth,
        Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(rootX, tipX) - rootWidth - 1f));
        int maxX = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(Mathf.Max(rootX, tipX) + rootWidth + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(rootY - 1f));
        int maxY = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(tipY + 1f));
        Vector2 root = new Vector2(rootX, rootY);
        Vector2 direction = new Vector2(tipX - rootX, tipY - rootY);
        float lengthSquared = direction.sqrMagnitude;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                float progress = Mathf.Clamp01(Vector2.Dot(point - root, direction) / lengthSquared);
                Vector2 center = root + direction * progress;
                float halfWidth = Mathf.Lerp(rootWidth, 0.2f, progress);
                float distance = Vector2.Distance(point, center);
                if (distance > halfWidth)
                    continue;

                byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(halfWidth - distance + 0.35f));
                int index = y * TextureSize + x;
                if (alpha > pixels[index].a)
                    pixels[index] = new Color32(color.r, color.g, color.b, alpha);
            }
        }
    }

    private static Color32 RandomGrassColor(System.Random random)
    {
        byte red = (byte)random.Next(54, 105);
        byte green = (byte)random.Next(115, 185);
        byte blue = (byte)random.Next(30, 78);
        return new Color32(red, green, blue, 255);
    }

    private static void ConfigureTextureImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(GrassTexturePath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = 0.35f;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void EnsureFolderExists(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, parts[i]);

            currentPath = nextPath;
        }
    }
}
#endif
