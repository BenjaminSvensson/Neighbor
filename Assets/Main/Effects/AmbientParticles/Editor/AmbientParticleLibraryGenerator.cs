#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class AmbientParticleLibraryGenerator
{
    private const string Root = "Assets/Main/Effects/AmbientParticles";
    private const string TextureFolder = Root + "/Textures";
    private const string MaterialFolder = Root + "/Materials";
    private const string PrefabFolder = Root + "/Prefabs";
    private const int TextureSize = 128;

    [MenuItem("Tools/Neighbor/Create or Refresh Ambient Particle Library")]
    private static void GenerateFromMenu()
    {
        Generate();
        Debug.Log($"Created ambient particle library at '{Root}'.");
    }

    public static void GenerateFromCommandLine()
    {
        Generate();
        EditorApplication.Exit(0);
    }

    private static void Generate()
    {
        EnsureFolder(TextureFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        CreateSoftCircleTexture(TextureFolder + "/SoftParticle.png");
        CreateLeafTexture(TextureFolder + "/Leaf.png");
        CreateStreakTexture(TextureFolder + "/WindStreak.png");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        ConfigureTexture(TextureFolder + "/SoftParticle.png");
        ConfigureTexture(TextureFolder + "/Leaf.png");
        ConfigureTexture(TextureFolder + "/WindStreak.png");

        Material softMaterial = CreateMaterial("SoftParticle", TextureFolder + "/SoftParticle.png", new Color(1f, 1f, 1f, 1f));
        Material leafMaterial = CreateMaterial("Leaf", TextureFolder + "/Leaf.png", new Color(1f, 1f, 1f, 1f));
        Material streakMaterial = CreateMaterial("WindStreak", TextureFolder + "/WindStreak.png", new Color(1f, 1f, 1f, 1f));

        SaveEffect(CreateFallingLeaves(leafMaterial), "FallingLeaves");
        SaveEffect(CreateWindStreaks(streakMaterial), "WindStreaks");
        SaveEffect(CreateDustMotes(softMaterial), "DustMotes");
        SaveEffect(CreatePollen(softMaterial), "Pollen");
        SaveEffect(CreateGroundMist(softMaterial), "GroundMist");
        SaveEffect(CreateChimneySmoke(softMaterial), "ChimneySmoke");
        CreateAmbientRig();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject CreateFallingLeaves(Material material)
    {
        ParticleSystem system = CreateSystem("Falling Leaves", material);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.95f, 0.55f, 0.12f, 1f),
            new Color(0.36f, 0.13f, 0.03f, 1f));
        main.gravityModifier = 0.07f;
        main.maxParticles = 350;

        SetEmission(system, 18f);
        SetBoxShape(system, new Vector3(22f, 2f, 22f));
        SetVelocity(system, new Vector3(-0.5f, -0.8f, -0.3f), new Vector3(0.8f, -1.5f, 0.5f));
        SetNoise(system, 0.9f, 0.32f, 0.55f);

        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        rotation.y = new ParticleSystem.MinMaxCurve(-3.5f, 3.5f);
        rotation.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        return system.gameObject;
    }

    private static GameObject CreateWindStreaks(Material material)
    {
        ParticleSystem system = CreateSystem("Wind Streaks", material);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 0.92f, 1f, 0.04f),
            new Color(0.9f, 0.98f, 1f, 0.22f));
        main.maxParticles = 120;

        SetEmission(system, 9f);
        SetBoxShape(system, new Vector3(16f, 7f, 16f));
        SetVelocity(system, new Vector3(7f, -0.1f, 1f), new Vector3(13f, 0.6f, 3f));
        SetNoise(system, 0.35f, 0.18f, 0.5f);
        SetFade(system);

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5f;
        renderer.velocityScale = 0.35f;
        return system.gameObject;
    }

    private static GameObject CreateDustMotes(Material material)
    {
        ParticleSystem system = CreateSystem("Dust Motes", material);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.085f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 0.68f, 0.45f, 0.18f),
            new Color(1f, 0.91f, 0.65f, 0.55f));
        main.maxParticles = 260;

        SetEmission(system, 25f);
        SetBoxShape(system, new Vector3(8f, 4f, 8f));
        SetVelocity(system, new Vector3(-0.08f, 0.02f, -0.08f), new Vector3(0.08f, 0.18f, 0.08f));
        SetNoise(system, 0.12f, 0.12f, 0.7f);
        SetFade(system);
        return system.gameObject;
    }

    private static GameObject CreatePollen(Material material)
    {
        ParticleSystem system = CreateSystem("Pollen", material);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.75f, 0.08f, 0.25f),
            new Color(1f, 0.96f, 0.4f, 0.8f));
        main.maxParticles = 280;

        SetEmission(system, 22f);
        SetBoxShape(system, new Vector3(14f, 5f, 14f));
        SetVelocity(system, new Vector3(-0.15f, 0.05f, -0.15f), new Vector3(0.3f, 0.35f, 0.3f));
        SetNoise(system, 0.28f, 0.2f, 0.65f);
        SetFade(system);
        return system.gameObject;
    }

    private static GameObject CreateGroundMist(Material material)
    {
        ParticleSystem system = CreateSystem("Ground Mist", material);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 13f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.58f, 0.68f, 0.72f, 0.025f),
            new Color(0.78f, 0.84f, 0.86f, 0.11f));
        main.maxParticles = 90;

        SetEmission(system, 4f);
        SetBoxShape(system, new Vector3(18f, 0.3f, 18f));
        SetVelocity(system, new Vector3(-0.16f, 0.01f, -0.12f), new Vector3(0.22f, 0.08f, 0.18f));
        SetNoise(system, 0.2f, 0.09f, 0.7f);
        SetFade(system);
        SetGrow(system, 0.5f, 1.35f);
        return system.gameObject;
    }

    private static GameObject CreateChimneySmoke(Material material)
    {
        ParticleSystem system = CreateSystem("Chimney Smoke", material);
        ParticleSystem.MainModule main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.12f, 0.13f, 0.14f, 0.3f),
            new Color(0.45f, 0.48f, 0.5f, 0.5f));
        main.maxParticles = 140;

        SetEmission(system, 12f);
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.18f;
        SetVelocity(system, new Vector3(-0.08f, 0.7f, -0.08f), new Vector3(0.08f, 1.4f, 0.08f));
        SetNoise(system, 0.38f, 0.16f, 0.55f);
        SetFade(system);
        SetGrow(system, 0.4f, 3.2f);
        return system.gameObject;
    }

    private static ParticleSystem CreateSystem(string name, Material material)
    {
        GameObject root = new GameObject(name);
        ParticleSystem system = root.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        return system;
    }

    private static void SetEmission(ParticleSystem system, float rate)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
    }

    private static void SetBoxShape(ParticleSystem system, Vector3 scale)
    {
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = scale;
    }

    private static void SetVelocity(ParticleSystem system, Vector3 minimum, Vector3 maximum)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(minimum.x, maximum.x);
        velocity.y = new ParticleSystem.MinMaxCurve(minimum.y, maximum.y);
        velocity.z = new ParticleSystem.MinMaxCurve(minimum.z, maximum.z);
    }

    private static void SetNoise(ParticleSystem system, float strength, float frequency, float scrollSpeed)
    {
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = new ParticleSystem.MinMaxCurve(strength * 0.75f, strength);
        noise.strengthY = new ParticleSystem.MinMaxCurve(strength * 0.35f, strength * 0.7f);
        noise.strengthZ = new ParticleSystem.MinMaxCurve(strength * 0.75f, strength);
        noise.frequency = frequency;
        noise.scrollSpeed = scrollSpeed;
        noise.damping = true;
        noise.octaveCount = 2;
    }

    private static void SetFade(ParticleSystem system)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0.75f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = gradient;
    }

    private static void SetGrow(ParticleSystem system, float start, float end)
    {
        AnimationCurve curve = AnimationCurve.Linear(0f, start, 1f, end);
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static void SaveEffect(GameObject effect, string fileName)
    {
        PrefabUtility.SaveAsPrefabAsset(effect, $"{PrefabFolder}/{fileName}.prefab");
        UnityEngine.Object.DestroyImmediate(effect);
    }

    private static void CreateAmbientRig()
    {
        GameObject rig = new GameObject("Ambient Weather Rig");
        string[] effects = { "FallingLeaves", "WindStreaks", "DustMotes", "Pollen", "GroundMist" };
        for (int i = 0; i < effects.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{effects[i]}.prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(rig.transform, false);
        }

        AmbientParticleWind wind = rig.AddComponent<AmbientParticleWind>();
        wind.RefreshParticleSystems();
        PrefabUtility.SaveAsPrefabAsset(rig, $"{PrefabFolder}/AmbientWeatherRig.prefab");
        UnityEngine.Object.DestroyImmediate(rig);
    }

    private static Material CreateMaterial(string name, string texturePath, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Particles/Standard Unlit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.name = name;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateSoftCircleTexture(string path)
    {
        WriteTexture(path, (x, y) =>
        {
            float distance = Vector2.Distance(new Vector2(x, y), Vector2.one * 0.5f) * 2f;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
            return new Color(1f, 1f, 1f, alpha);
        });
    }

    private static void CreateLeafTexture(string path)
    {
        WriteTexture(path, (x, y) =>
        {
            float px = (x - 0.5f) * 2f;
            float py = (y - 0.5f) * 2f;
            float width = Mathf.Sin(Mathf.Clamp01((py + 1f) * 0.5f) * Mathf.PI) * 0.62f;
            float edge = Mathf.Abs(px) / Mathf.Max(0.001f, width);
            float alpha = Mathf.Clamp01((1f - edge) * 8f);
            float vein = Mathf.Clamp01(1f - Mathf.Abs(px + py * 0.14f) * 18f);
            return new Color(0.88f + vein * 0.12f, 0.72f + vein * 0.12f, 0.42f, alpha);
        });
    }

    private static void CreateStreakTexture(string path)
    {
        WriteTexture(path, (x, y) =>
        {
            float vertical = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y - 0.5f) * 5f), 2f);
            float horizontal = Mathf.Sin(Mathf.Clamp01(x) * Mathf.PI);
            return new Color(1f, 1f, 1f, vertical * horizontal);
        });
    }

    private static void WriteTexture(string path, Func<float, float, Color> pixel)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
                texture.SetPixel(x, y, pixel(x / (TextureSize - 1f), y / (TextureSize - 1f)));
        }

        texture.Apply();
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
