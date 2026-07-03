#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class TerrainGrassInstaller
{
    private const string MenuRoot = "Tools/Neighbor/Terrain/";
    private const string GrassFolder = "Assets/Main/Art/Terrain/Grass";
    private const string GrassTexturePath = GrassFolder + "/NeighborGrass.png";
    private const string GrassLayerPath = "Assets/Main/Art/Terrain/Layers/Grass005.terrainlayer";
    private const string DirtLayerPath = "Assets/Main/Art/Terrain/Layers/Ground048.terrainlayer";
    private const string BasicTreePrefabPath = "Assets/Main/Art/Models/TreeObjects/BasicTree.prefab";
    private const int TextureSize = 256;

    [InitializeOnLoadMethod]
    private static void ScheduleGrassAssetCreation()
    {
        EditorApplication.delayCall += EnsureTerrainAssets;
    }

    [MenuItem(MenuRoot + "Create or Refresh Grass Detail Texture")]
    private static void CreateOrRefreshGrassTexture()
    {
        GenerateGrassTexture();
        Debug.Log($"Created terrain grass texture at '{GrassTexturePath}'.");
    }

    [MenuItem(MenuRoot + "Install Grass and Dirt Layers")]
    private static void InstallGrassAndDirtLayers()
    {
        EnsureTerrainAssets();
    }

    [MenuItem(MenuRoot + "Install Basic Tree Painting Prototype")]
    private static void InstallBasicTreePaintingPrototype()
    {
        InstallBasicTreePrototypeOnAllTerrainData();
        AssetDatabase.SaveAssets();
    }

    [MenuItem(MenuRoot + "Add Grass Detail to Selected Terrain")]
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
        Undo.RegisterCompleteObjectUndo(terrainData, "Add Neighbor Grass");

        int detailIndex = EnsureGrassDetailPrototype(terrainData, grassTexture, out _);
        SeedGrassDetailLayerIfEmpty(terrainData, detailIndex);
        terrain.detailObjectDistance = Mathf.Max(terrain.detailObjectDistance, 90f);
        terrain.detailObjectDensity = Mathf.Max(terrain.detailObjectDensity, 0.8f);

        EditorUtility.SetDirty(terrainData);
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Added Neighbor Grass to '{terrain.name}'. Use Paint Details in the Terrain inspector to paint it.",
            terrain);
    }

    [MenuItem(MenuRoot + "Add Grass Detail to Selected Terrain", true)]
    private static bool CanAddGrassToSelectedTerrain()
    {
        return GetSelectedTerrain() != null;
    }

    [MenuItem(MenuRoot + "Add Basic Tree to Selected Terrain")]
    private static void AddBasicTreeToSelectedTerrain()
    {
        Terrain terrain = GetSelectedTerrain();
        if (terrain == null)
        {
            Debug.LogWarning("Select a Terrain object before adding the BasicTree paint prototype.");
            return;
        }

        GameObject basicTreePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasicTreePrefabPath);
        if (basicTreePrefab == null)
        {
            Debug.LogError($"Could not load the BasicTree prefab at '{BasicTreePrefabPath}'.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(terrainData, "Add Basic Tree Paint Prototype");

        bool changed = EnsureBasicTreePrototype(terrainData, basicTreePrefab);
        terrain.drawTreesAndFoliage = true;
        terrain.treeDistance = Mathf.Max(terrain.treeDistance, 350f);
        terrain.treeBillboardDistance = Mathf.Max(terrain.treeBillboardDistance, 80f);
        terrain.treeCrossFadeLength = Mathf.Max(terrain.treeCrossFadeLength, 15f);
        terrain.treeMaximumFullLODCount = Mathf.Max(terrain.treeMaximumFullLODCount, 80);

        if (changed)
        {
            terrainData.RefreshPrototypes();
            EditorUtility.SetDirty(terrainData);
        }

        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Added BasicTree to '{terrain.name}'. Use Paint Trees in the Terrain inspector to paint it.",
            terrain);
    }

    [MenuItem(MenuRoot + "Add Basic Tree to Selected Terrain", true)]
    private static bool CanAddBasicTreeToSelectedTerrain()
    {
        return GetSelectedTerrain() != null;
    }

    public static void GenerateFromCommandLine()
    {
        EnsureTerrainAssets();
        EditorApplication.Exit(0);
    }

    private static void EnsureTerrainAssets()
    {
        EnsureGrassAsset();
        EnsureTerrainLayerSettings();
        InstallTerrainLayersOnAllTerrainData();
        InstallGrassDetailsOnAllTerrainData();
        InstallBasicTreePrototypeOnAllTerrainData();
        AssetDatabase.SaveAssets();
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

    private static void EnsureTerrainLayerSettings()
    {
        ConfigureTerrainLayer(
            DirtLayerPath,
            tileSize: new Vector2(5f, 5f),
            normalScale: 0.75f,
            smoothness: 0.16f);

        ConfigureTerrainLayer(
            GrassLayerPath,
            tileSize: new Vector2(4f, 4f),
            normalScale: 0.65f,
            smoothness: 0.08f);
    }

    private static void ConfigureTerrainLayer(
        string layerPath,
        Vector2 tileSize,
        float normalScale,
        float smoothness)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (layer == null)
        {
            Debug.LogError($"Could not load terrain layer at '{layerPath}'.");
            return;
        }

        bool changed = false;
        if (layer.tileSize != tileSize)
        {
            layer.tileSize = tileSize;
            changed = true;
        }

        if (layer.tileOffset != Vector2.zero)
        {
            layer.tileOffset = Vector2.zero;
            changed = true;
        }

        if (layer.specular != Color.black)
        {
            layer.specular = Color.black;
            changed = true;
        }

        if (!Mathf.Approximately(layer.metallic, 0f))
        {
            layer.metallic = 0f;
            changed = true;
        }

        if (!Mathf.Approximately(layer.smoothness, smoothness))
        {
            layer.smoothness = smoothness;
            changed = true;
        }

        if (!Mathf.Approximately(layer.normalScale, normalScale))
        {
            layer.normalScale = normalScale;
            changed = true;
        }

        if (!changed)
            return;

        EditorUtility.SetDirty(layer);
    }

    private static void InstallTerrainLayersOnAllTerrainData()
    {
        TerrainLayer grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerPath);
        TerrainLayer dirtLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DirtLayerPath);
        if (grassLayer == null || dirtLayer == null)
        {
            Debug.LogError("Could not install terrain layers because grass or dirt layer assets are missing.");
            return;
        }

        string[] terrainDataGuids = AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets" });
        int changedCount = 0;
        foreach (string terrainDataGuid in terrainDataGuids)
        {
            string terrainDataPath = AssetDatabase.GUIDToAssetPath(terrainDataGuid);
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
            if (terrainData == null)
                continue;

            TerrainLayer[] updatedLayers = BuildTerrainLayerSet(terrainData.terrainLayers, dirtLayer, grassLayer);
            if (LayersMatch(terrainData.terrainLayers, updatedLayers))
                continue;

            terrainData.terrainLayers = updatedLayers;
            EditorUtility.SetDirty(terrainData);
            changedCount++;
            Debug.Log($"Installed grass and dirt terrain layers on '{terrainDataPath}'.");
        }

        if (changedCount > 0)
            Debug.Log($"Installed grass and dirt terrain layers on {changedCount} TerrainData asset(s).");
    }

    private static void InstallGrassDetailsOnAllTerrainData()
    {
        Texture2D grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath);
        if (grassTexture == null)
        {
            Debug.LogError($"Could not load the grass detail texture at '{GrassTexturePath}'.");
            return;
        }

        string[] terrainDataGuids = AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets" });
        int changedCount = 0;
        foreach (string terrainDataGuid in terrainDataGuids)
        {
            string terrainDataPath = AssetDatabase.GUIDToAssetPath(terrainDataGuid);
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
            if (terrainData == null)
                continue;

            int detailIndex = EnsureGrassDetailPrototype(terrainData, grassTexture, out bool addedPrototype);
            bool seededDetails = SeedGrassDetailLayerIfEmpty(terrainData, detailIndex);
            if (!addedPrototype && !seededDetails)
                continue;

            EditorUtility.SetDirty(terrainData);
            changedCount++;
            Debug.Log($"Installed Neighbor Grass detail on '{terrainDataPath}'.");
        }

        if (changedCount > 0)
            Debug.Log($"Installed Neighbor Grass detail on {changedCount} TerrainData asset(s).");
    }

    private static void InstallBasicTreePrototypeOnAllTerrainData()
    {
        GameObject basicTreePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasicTreePrefabPath);
        if (basicTreePrefab == null)
        {
            Debug.LogError($"Could not install tree painting because '{BasicTreePrefabPath}' is missing.");
            return;
        }

        string[] terrainDataGuids = AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets" });
        int changedCount = 0;
        foreach (string terrainDataGuid in terrainDataGuids)
        {
            string terrainDataPath = AssetDatabase.GUIDToAssetPath(terrainDataGuid);
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
            if (terrainData == null)
                continue;

            if (!EnsureBasicTreePrototype(terrainData, basicTreePrefab))
                continue;

            terrainData.RefreshPrototypes();
            EditorUtility.SetDirty(terrainData);
            changedCount++;
            Debug.Log($"Installed BasicTree paint prototype on '{terrainDataPath}'.");
        }

        if (changedCount > 0)
            Debug.Log($"Installed BasicTree paint prototype on {changedCount} TerrainData asset(s).");
    }

    private static bool EnsureBasicTreePrototype(TerrainData terrainData, GameObject basicTreePrefab)
    {
        TreePrototype[] currentPrototypes = terrainData.treePrototypes ?? Array.Empty<TreePrototype>();
        TreePrototype[] updatedPrototypes = BuildTreePrototypeSet(currentPrototypes, basicTreePrefab);
        if (TreePrototypesMatch(currentPrototypes, updatedPrototypes))
            return false;

        terrainData.treePrototypes = updatedPrototypes;
        return true;
    }

    private static int EnsureGrassDetailPrototype(
        TerrainData terrainData,
        Texture2D grassTexture,
        out bool addedPrototype)
    {
        DetailPrototype[] prototypes = terrainData.detailPrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prototypeTexture == grassTexture)
            {
                addedPrototype = false;
                return i;
            }
        }

        Array.Resize(ref prototypes, prototypes.Length + 1);
        prototypes[prototypes.Length - 1] = CreateNeighborGrassPrototype(grassTexture);
        terrainData.detailPrototypes = prototypes;
        addedPrototype = true;
        return prototypes.Length - 1;
    }

    private static DetailPrototype CreateNeighborGrassPrototype(Texture2D grassTexture)
    {
        return new DetailPrototype
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
    }

    private static bool SeedGrassDetailLayerIfEmpty(TerrainData terrainData, int detailIndex)
    {
        if (detailIndex < 0)
            return false;

        if (terrainData.detailResolution <= 0)
            terrainData.SetDetailResolution(512, 16);

        int resolution = terrainData.detailResolution;
        int[,] currentDetails = terrainData.GetDetailLayer(0, 0, resolution, resolution, detailIndex);
        if (HasAnyDetailDensity(currentDetails))
            return false;

        int[,] grassDetails = new int[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (x + 0.5f) / resolution;
                float normalizedY = (y + 0.5f) / resolution;
                float slope = terrainData.GetSteepness(normalizedX, normalizedY);
                if (slope > 34f)
                    continue;

                float largeNoise = Mathf.PerlinNoise(normalizedX * 8.5f + 37.2f, normalizedY * 8.5f + 19.7f);
                float fineNoise = Mathf.PerlinNoise(normalizedX * 43f + 5.9f, normalizedY * 43f + 82.4f);
                float coverage = largeNoise * 0.75f + fineNoise * 0.25f;
                if (coverage < 0.42f)
                    continue;

                grassDetails[y, x] = Mathf.Clamp(Mathf.RoundToInt((coverage - 0.36f) * 8f), 1, 5);
            }
        }

        terrainData.SetDetailLayer(0, 0, detailIndex, grassDetails);
        return true;
    }

    private static bool HasAnyDetailDensity(int[,] details)
    {
        int height = details.GetLength(0);
        int width = details.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (details[y, x] > 0)
                    return true;
            }
        }

        return false;
    }

    private static TerrainLayer[] BuildTerrainLayerSet(
        TerrainLayer[] existingLayers,
        TerrainLayer dirtLayer,
        TerrainLayer grassLayer)
    {
        List<TerrainLayer> layers = new List<TerrainLayer> { dirtLayer, grassLayer };
        if (existingLayers == null)
            return layers.ToArray();

        foreach (TerrainLayer existingLayer in existingLayers)
        {
            if (existingLayer == null)
                continue;

            if (existingLayer == dirtLayer || existingLayer == grassLayer)
                continue;

            layers.Add(existingLayer);
        }

        return layers.ToArray();
    }

    private static bool LayersMatch(TerrainLayer[] currentLayers, TerrainLayer[] expectedLayers)
    {
        if (currentLayers == null || currentLayers.Length != expectedLayers.Length)
            return false;

        for (int i = 0; i < currentLayers.Length; i++)
        {
            if (currentLayers[i] != expectedLayers[i])
                return false;
        }

        return true;
    }

    private static TreePrototype[] BuildTreePrototypeSet(
        TreePrototype[] existingPrototypes,
        GameObject basicTreePrefab)
    {
        List<TreePrototype> prototypes = new List<TreePrototype>
        {
            CreateBasicTreePrototype(basicTreePrefab)
        };

        if (existingPrototypes == null)
            return prototypes.ToArray();

        foreach (TreePrototype existingPrototype in existingPrototypes)
        {
            if (existingPrototype == null || existingPrototype.prefab == null)
                continue;

            if (existingPrototype.prefab == basicTreePrefab)
                continue;

            prototypes.Add(existingPrototype);
        }

        return prototypes.ToArray();
    }

    private static TreePrototype CreateBasicTreePrototype(GameObject basicTreePrefab)
    {
        return new TreePrototype
        {
            prefab = basicTreePrefab,
            bendFactor = 0.15f
        };
    }

    private static bool TreePrototypesMatch(
        TreePrototype[] currentPrototypes,
        TreePrototype[] expectedPrototypes)
    {
        if (currentPrototypes == null || currentPrototypes.Length != expectedPrototypes.Length)
            return false;

        for (int i = 0; i < currentPrototypes.Length; i++)
        {
            TreePrototype currentPrototype = currentPrototypes[i];
            TreePrototype expectedPrototype = expectedPrototypes[i];
            if (currentPrototype == null || expectedPrototype == null)
                return currentPrototype == expectedPrototype;

            if (currentPrototype.prefab != expectedPrototype.prefab)
                return false;

            if (!Mathf.Approximately(currentPrototype.bendFactor, expectedPrototype.bendFactor))
                return false;
        }

        return true;
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
