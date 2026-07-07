#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class TerrainGrassInstaller
{
    private const string MenuRoot = "Tools/Neighbor/Terrain/";
    private const string GrassFolder = "Assets/Main/Art/Terrain/Grass";
    private const string GrassTexturePath = GrassFolder + "/NeighborGrass.png";
    private const string GrassLayerPath = "Assets/Main/Art/Terrain/Layers/Grass005.terrainlayer";
    private const string DirtLayerPath = "Assets/Main/Art/Terrain/Layers/Ground048.terrainlayer";
    private const string BasicTreeSourcePrefabPath = "Assets/Main/Art/Models/TreeObjects/BasicTree.prefab";
    private const string BasicTreePrefabPath = "Assets/Main/Art/Models/TreeObjects/BasicTreeTerrain.prefab";
    private const string BasicTreeMeshPath = "Assets/Main/Art/Models/TreeObjects/BasicTreeTerrainMesh.asset";
    private const string BasicTreeBarkMaterialPath = "Assets/Main/Art/Models/TreeObjects/bark02.mat";
    private const string BasicTreeLeafMaterialPath = "Assets/Main/Art/Models/TreeObjects/leaf.mat";
    private const string BasicTreeBarkAlbedoTexturePath = "Assets/Main/Art/Models/TreeObjects/GangstaTree/bark02.png";
    private const string BasicTreeBarkNormalTexturePath = "Assets/Main/Art/Models/TreeObjects/GangstaTree/bark02_normal.png";
    private const string BasicTreeLeafAlbedoTexturePath = "Assets/Main/Art/Models/TreeObjects/GangstaTree/leaf maple.png";
    private const string BasicTreeLeafNormalTexturePath = "Assets/Main/Art/Models/TreeObjects/GangstaTree/leaf maple_normal.png";
    private const string BasicTreeLeafTransmissionTexturePath = "Assets/Main/Art/Models/TreeObjects/GangstaTree/leaf maple_transmission.png";
    private const string TerrainTreeBarkShaderName = "Nature/Soft Occlusion Bark";
    private const string TerrainTreeLeafShaderName = "Nature/Soft Occlusion Leaves";
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

    public static void RepairBasicTreeTerrainFromCommandLine()
    {
        GameObject prefab = EnsureTerrainCompatibleBasicTreePrefab();
        AssetDatabase.SaveAssets();

        if (prefab == null || !HasTerrainCompatibleTreeRenderer(prefab))
        {
            Debug.LogError("BasicTreeTerrain repair failed.");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("BasicTreeTerrain repair completed.");
        EditorApplication.Exit(0);
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
        terrain.detailObjectDistance = Mathf.Clamp(
            terrain.detailObjectDistance <= 0f ? 70f : terrain.detailObjectDistance,
            45f,
            90f);
        terrain.detailObjectDensity = Mathf.Clamp(
            terrain.detailObjectDensity <= 0f ? 0.85f : terrain.detailObjectDensity,
            0.55f,
            1f);

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
        terrain.treeDistance = Mathf.Clamp(
            terrain.treeDistance <= 0f ? 360f : terrain.treeDistance,
            220f,
            550f);
        terrain.treeBillboardDistance = Mathf.Clamp(
            terrain.treeBillboardDistance <= 0f ? 45f : terrain.treeBillboardDistance,
            32f,
            70f);
        terrain.treeCrossFadeLength = Mathf.Clamp(
            terrain.treeCrossFadeLength <= 0f ? 5f : terrain.treeCrossFadeLength,
            4f,
            8f);
        terrain.treeMaximumFullLODCount = Mathf.Clamp(
            terrain.treeMaximumFullLODCount <= 0 ? 32 : terrain.treeMaximumFullLODCount,
            16,
            60);

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
        EnsureTerrainCompatibleBasicTreePrefab();
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
        GameObject basicTreePrefab = EnsureTerrainCompatibleBasicTreePrefab();
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

            string existingPath = AssetDatabase.GetAssetPath(existingPrototype.prefab);
            if (existingPath == BasicTreeSourcePrefabPath
                || existingPath == BasicTreePrefabPath
                || existingPrototype.prefab.name == "BasicTree")
            {
                continue;
            }

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

    internal static bool HasTerrainCompatibleTreeRenderer(GameObject prefab)
    {
        if (prefab == null)
            return false;

        MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = prefab.GetComponent<MeshRenderer>();
        if (meshFilter == null || meshRenderer == null || !meshRenderer.enabled)
            return false;

        if (meshFilter.sharedMesh == null || meshFilter.sharedMesh.vertexCount == 0)
            return false;

        Material[] materials = meshRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
            return false;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || material.shader == null)
                return false;

            if (!IsRenderableTerrainTreeMaterial(material))
                return false;
        }

        return true;
    }

    internal static bool IsRenderableTerrainTreeMaterial(Material material)
    {
        string materialPath = AssetDatabase.GetAssetPath(material);
        return (materialPath == BasicTreeBarkMaterialPath || materialPath == BasicTreeLeafMaterialPath)
            && UsesTerrainTreeShader(material)
            && HasTerrainTreeAlbedoTexture(material);
    }

    internal static bool HasTerrainTreeAlbedoTexture(Material material)
    {
        return GetMainTexture(material) != null;
    }

    internal static bool UsesTerrainTreeShader(Material material)
    {
        return material != null
            && material.shader != null
            && material.shader.name.StartsWith("Nature/Soft Occlusion", StringComparison.Ordinal);
    }

    private static GameObject EnsureTerrainCompatibleBasicTreePrefab()
    {
        ConfigureBasicTreeTerrainMaterials();

        GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasicTreePrefabPath);
        if (HasTerrainCompatibleTreeRenderer(terrainPrefab))
            return terrainPrefab;

        GameObject source = PrefabUtility.LoadPrefabContents(BasicTreeSourcePrefabPath);
        if (source == null)
        {
            Debug.LogError($"Could not create terrain tree prefab because '{BasicTreeSourcePrefabPath}' is missing.");
            return null;
        }

        Mesh combinedMesh = null;
        Material[] materials = Array.Empty<Material>();
        try
        {
            combinedMesh = BuildTerrainTreeMesh(source, out materials);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(source);
        }

        if (combinedMesh == null || materials.Length == 0)
        {
            Debug.LogError($"Could not create terrain tree prefab because '{BasicTreeSourcePrefabPath}' has no mesh renderer data.");
            return null;
        }

        SaveMeshAsset(combinedMesh, BasicTreeMeshPath);
        Material[] terrainMaterials = EnsureTerrainTreeMaterials(materials);
        Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BasicTreeMeshPath);
        if (savedMesh == null)
        {
            Debug.LogError($"Could not load generated terrain tree mesh at '{BasicTreeMeshPath}'.");
            return null;
        }

        SaveTerrainTreePrefab(savedMesh, terrainMaterials);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<GameObject>(BasicTreePrefabPath);
    }

    private static Mesh BuildTerrainTreeMesh(GameObject source, out Material[] materials)
    {
        List<Material> materialSlots = new List<Material>();
        List<List<CombineInstance>> combinesByMaterial = new List<List<CombineInstance>>();
        MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
        Matrix4x4 rootWorldToLocal = source.transform.worldToLocalMatrix;

        for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
        {
            MeshFilter filter = filters[filterIndex];
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            Mesh mesh = filter.sharedMesh;
            if (renderer == null || mesh == null || !renderer.enabled)
                continue;

            Material[] rendererMaterials = renderer.sharedMaterials;
            if (rendererMaterials == null || rendererMaterials.Length == 0)
                continue;

            int subMeshCount = Mathf.Min(mesh.subMeshCount, rendererMaterials.Length);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = rendererMaterials[subMeshIndex];
                if (material == null)
                    continue;

                int materialIndex = materialSlots.IndexOf(material);
                if (materialIndex < 0)
                {
                    materialIndex = materialSlots.Count;
                    materialSlots.Add(material);
                    combinesByMaterial.Add(new List<CombineInstance>());
                }

                combinesByMaterial[materialIndex].Add(
                    new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = subMeshIndex,
                        transform = rootWorldToLocal * filter.transform.localToWorldMatrix
                    });
            }
        }

        materials = materialSlots.ToArray();
        if (materials.Length == 0)
            return null;

        Mesh[] materialMeshes = new Mesh[combinesByMaterial.Count];
        CombineInstance[] finalCombines = new CombineInstance[combinesByMaterial.Count];
        try
        {
            for (int i = 0; i < combinesByMaterial.Count; i++)
            {
                Mesh materialMesh = new Mesh
                {
                    name = $"BasicTreeTerrain_{i}",
                    indexFormat = IndexFormat.UInt32
                };
                materialMesh.CombineMeshes(combinesByMaterial[i].ToArray(), true, true, false);
                materialMeshes[i] = materialMesh;
                finalCombines[i] = new CombineInstance
                {
                    mesh = materialMesh,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                };
            }

            Mesh combinedMesh = new Mesh
            {
                name = "BasicTreeTerrainMesh",
                indexFormat = IndexFormat.UInt32
            };
            combinedMesh.CombineMeshes(finalCombines, false, false, false);
            combinedMesh.RecalculateBounds();
            return combinedMesh;
        }
        finally
        {
            for (int i = 0; i < materialMeshes.Length; i++)
            {
                if (materialMeshes[i] != null)
                    UnityEngine.Object.DestroyImmediate(materialMeshes[i]);
            }
        }
    }

    private static void SaveMeshAsset(Mesh mesh, string path)
    {
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existingMesh == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return;
        }

        EditorUtility.CopySerialized(mesh, existingMesh);
        EditorUtility.SetDirty(existingMesh);
        UnityEngine.Object.DestroyImmediate(mesh);
    }

    private static Material[] EnsureTerrainTreeMaterials(Material[] sourceMaterials)
    {
        ConfigureBasicTreeTerrainMaterials();
        Material barkMaterial = AssetDatabase.LoadAssetAtPath<Material>(BasicTreeBarkMaterialPath);
        Material leafMaterial = AssetDatabase.LoadAssetAtPath<Material>(BasicTreeLeafMaterialPath);

        Material[] terrainMaterials = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material sourceMaterial = sourceMaterials[i];
            bool isLeaf = IsLeafMaterial(sourceMaterial);
            Material terrainMaterial = isLeaf ? leafMaterial : barkMaterial;
            terrainMaterials[i] = terrainMaterial != null ? terrainMaterial : sourceMaterial;
        }

        return terrainMaterials;
    }

    private static void ConfigureBasicTreeTerrainMaterials()
    {
        ConfigureTerrainTreeMaterial(AssetDatabase.LoadAssetAtPath<Material>(BasicTreeBarkMaterialPath), false);
        ConfigureTerrainTreeMaterial(AssetDatabase.LoadAssetAtPath<Material>(BasicTreeLeafMaterialPath), true);
    }

    private static void ConfigureTerrainTreeMaterial(Material material, bool isLeaf)
    {
        if (material == null)
            return;

        Texture mainTexture = GetMainTexture(material) ?? LoadTreeTexture(isLeaf ? BasicTreeLeafAlbedoTexturePath : BasicTreeBarkAlbedoTexturePath);
        Shader treeShader = Shader.Find(isLeaf ? TerrainTreeLeafShaderName : TerrainTreeBarkShaderName);
        if (treeShader == null)
        {
            Debug.LogError($"Required terrain tree shader is missing: {(isLeaf ? TerrainTreeLeafShaderName : TerrainTreeBarkShaderName)}");
            return;
        }

        bool changed = false;
        if (material.shader != treeShader)
        {
            material.shader = treeShader;
            changed = true;
        }

        changed |= SetTexture(material, "_MainTex", mainTexture);
        changed |= SetTexture(material, "_BaseMap", mainTexture);
        changed |= SetTexture(
            material,
            "_BumpMap",
            LoadTreeTexture(isLeaf ? BasicTreeLeafNormalTexturePath : BasicTreeBarkNormalTexturePath));

        if (isLeaf)
        {
            changed |= SetTexture(material, "_TranslucencyMap", LoadTreeTexture(BasicTreeLeafTransmissionTexturePath));
            changed |= SetFloat(material, "_AlphaClip", 1f);
            changed |= SetFloat(material, "_AlphaToMask", 1f);
        }

        changed |= SetColor(material, "_Color", Color.white);
        changed |= SetColor(material, "_BaseColor", Color.white);

        changed |= SetFloat(material, "_Cutoff", isLeaf ? 0.35f : 0.5f);
        changed |= SetFloat(material, "_BumpScale", isLeaf ? 0.72f : 0.85f);
        changed |= SetFloat(material, "_Smoothness", isLeaf ? 0.08f : 0.18f);
        changed |= SetFloat(material, "_Metallic", 0f);

        changed |= SetColor(
            material,
            "_TranslucencyColor",
            isLeaf ? new Color(0.52f, 0.72f, 0.38f, 1f) : new Color(0.28f, 0.2f, 0.13f, 1f));

        changed |= SetFloat(material, "_ShadowStrength", isLeaf ? 0.65f : 0.8f);

        if (changed)
            EditorUtility.SetDirty(material);
    }

    private static Texture GetMainTexture(Material material)
    {
        if (material == null)
            return null;

        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
            return material.GetTexture("_MainTex");

        if (material.HasProperty("_BaseMap"))
            return material.GetTexture("_BaseMap");

        return material.mainTexture;
    }

    private static Texture2D LoadTreeTexture(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static bool SetTexture(Material material, string propertyName, Texture texture)
    {
        if (material == null || texture == null || !material.HasProperty(propertyName))
            return false;

        if (material.GetTexture(propertyName) == texture)
            return false;

        material.SetTexture(propertyName, texture);
        return true;
    }

    private static bool SetColor(Material material, string propertyName, Color color)
    {
        if (material == null || !material.HasProperty(propertyName))
            return false;

        if (material.GetColor(propertyName) == color)
            return false;

        material.SetColor(propertyName, color);
        return true;
    }

    private static bool SetFloat(Material material, string propertyName, float value)
    {
        if (material == null || !material.HasProperty(propertyName))
            return false;

        if (Mathf.Approximately(material.GetFloat(propertyName), value))
            return false;

        material.SetFloat(propertyName, value);
        return true;
    }

    private static bool IsLeafMaterial(Material material)
    {
        return material != null
            && material.name.IndexOf("leaf", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SaveTerrainTreePrefab(Mesh mesh, Material[] materials)
    {
        bool loadedPrefabContents = AssetDatabase.LoadAssetAtPath<GameObject>(BasicTreePrefabPath) != null;
        GameObject root = loadedPrefabContents
            ? PrefabUtility.LoadPrefabContents(BasicTreePrefabPath)
            : new GameObject("BasicTreeTerrain");
        try
        {
            root.name = "BasicTreeTerrain";
            MeshFilter meshFilter = EnsureComponent<MeshFilter>(root);
            MeshRenderer meshRenderer = EnsureComponent<MeshRenderer>(root);
            CapsuleCollider capsuleCollider = EnsureComponent<CapsuleCollider>(root);

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            meshRenderer.enabled = true;

            Bounds bounds = mesh.bounds;
            capsuleCollider.center = bounds.center;
            capsuleCollider.height = Mathf.Max(bounds.size.y, 0.1f);
            capsuleCollider.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.35f;

            PrefabUtility.SaveAsPrefabAsset(root, BasicTreePrefabPath, out bool savedSuccessfully);
            if (!savedSuccessfully)
                Debug.LogError($"{BasicTreePrefabPath}: failed to save terrain-compatible tree prefab.");
        }
        finally
        {
            if (loadedPrefabContents)
                PrefabUtility.UnloadPrefabContents(root);
            else
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static T EnsureComponent<T>(GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
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
