#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SettingsManagement;
using UnityEngine;
using UnityEngine.ProBuilder;

[InitializeOnLoad]
internal static class ProBuilderDefaultMaterialInitializer
{
    private const string MaterialPath = "Assets/Main/Objects/ProBuilder/ProBuilderDefault_URP.mat";
    private const string ProBuilderPackageName = "com.unity.probuilder";
    private const string UserMaterialPreferenceKey = "mesh.userMaterial";

    private static readonly Settings ProBuilderSettings = new Settings(ProBuilderPackageName);
    private static Material s_DefaultMaterial;
    private static bool s_ApplyingSceneMaterials;

    static ProBuilderDefaultMaterialInitializer()
    {
        EditorApplication.delayCall += ConfigureProBuilderMaterial;
        EditorApplication.hierarchyChanged += ApplyMaterialToDefaultProBuilderObjects;
    }

    private static void ConfigureProBuilderMaterial()
    {
        s_DefaultMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (s_DefaultMaterial == null)
            return;

        ProBuilderSettings.Set(UserMaterialPreferenceKey, s_DefaultMaterial, SettingsScope.Project);
        ProBuilderSettings.Save();

        ApplyMaterialToDefaultProBuilderObjects();
    }

    private static void ApplyMaterialToDefaultProBuilderObjects()
    {
        if (s_ApplyingSceneMaterials)
            return;

        if (s_DefaultMaterial == null)
            s_DefaultMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (s_DefaultMaterial == null)
            return;

        try
        {
            s_ApplyingSceneMaterials = true;

            foreach (var mesh in Object.FindObjectsByType<ProBuilderMesh>(FindObjectsSortMode.None))
            {
                var meshRenderer = mesh.GetComponent<MeshRenderer>();
                if (meshRenderer == null || !ShouldReplace(meshRenderer.sharedMaterial))
                    continue;

                Undo.RecordObject(meshRenderer, "Apply ProBuilder Prototype Material");
                meshRenderer.sharedMaterial = s_DefaultMaterial;
                EditorUtility.SetDirty(meshRenderer);
            }
        }
        finally
        {
            s_ApplyingSceneMaterials = false;
        }
    }

    private static bool ShouldReplace(Material material)
    {
        if (material == null)
            return true;

        if (material == s_DefaultMaterial)
            return false;

        var shaderName = material.shader != null ? material.shader.name : string.Empty;
        return material.name == "Lit"
            || material.name == "Default-Material"
            || shaderName == "Hidden/ProBuilder/Default";
    }
}
#endif
