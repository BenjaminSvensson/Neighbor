#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder.Editor
{
    [CustomEditor(typeof(HouseGeometryObject))]
    public sealed class HouseGeometryObjectEditor : UnityEditor.Editor
    {
        private bool showAdvanced;

        public override void OnInspectorGUI()
        {
            HouseGeometryObject geometry = (HouseGeometryObject)target;
            EditorGUILayout.LabelField(FriendlyName(geometry.Descriptor.Kind), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Resize here, with the Scene handles, or from House Builder > Draw. Linked door and window holes are preserved.", MessageType.None);

            Vector3 size = geometry.Descriptor.Size;
            EditorGUI.BeginChangeCheck();
            size.x = Mathf.Max(0.05f, EditorGUILayout.FloatField("Width", size.x));
            if (geometry.Descriptor.Kind is HouseGeometryKind.Floor or HouseGeometryKind.Ceiling)
            {
                size.z = Mathf.Max(0.05f, EditorGUILayout.FloatField("Depth", size.z));
                size.y = Mathf.Max(0.05f, EditorGUILayout.FloatField("Thickness", size.y));
            }
            else if (geometry.Descriptor.Kind is HouseGeometryKind.Wall or HouseGeometryKind.Doorway or HouseGeometryKind.Window)
            {
                size.y = Mathf.Max(0.05f, EditorGUILayout.FloatField("Height", size.y));
                size.z = Mathf.Max(0.05f, EditorGUILayout.FloatField("Thickness", size.z));
            }
            else
            {
                size.y = Mathf.Max(0.05f, EditorGUILayout.FloatField("Height", size.y));
                size.z = Mathf.Max(0.05f, EditorGUILayout.FloatField("Depth", size.z));
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(geometry, "Resize House Builder Geometry");
                geometry.Resize(size);
                EditorUtility.SetDirty(geometry);
                if (geometry.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(geometry.gameObject.scene);
                }
            }

            if (GUILayout.Button("Reset to Suggested Size"))
            {
                Undo.RecordObject(geometry, "Reset House Builder Geometry Size");
                geometry.Resize(HouseBuilderEditorInteractionUtility.SuggestedSize(geometry.Descriptor.Kind));
                EditorUtility.SetDirty(geometry);
                if (geometry.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(geometry.gameObject.scene);
                }
            }

            if (geometry.Descriptor.WallOpenings.Count > 0)
            {
                EditorGUILayout.LabelField($"Linked Openings: {geometry.Descriptor.WallOpenings.Count}", EditorStyles.miniLabel);
            }

            if (GUILayout.Button("Open House Builder"))
            {
                HouseBuilderLevelEditorWindow.Open();
            }

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Geometry Data", true);
            if (showAdvanced)
            {
                DrawDefaultInspector();
            }
        }

        private static string FriendlyName(HouseGeometryKind kind)
        {
            return kind == HouseGeometryKind.Cube ? "Builder Block" : $"Builder {kind}";
        }
    }
}
#endif
