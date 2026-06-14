#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder.Editor
{
    internal sealed class HouseReinforcementPickerPopup : PopupWindowContent
    {
        private readonly IReadOnlyList<HousePlaceableDefinition> definitions;
        private readonly bool[] selected;
        private readonly Action<IReadOnlyList<HousePlaceableDefinition>> confirmed;
        private readonly Action cancelled;
        private Vector2 scroll;
        private bool completed;

        private HouseReinforcementPickerPopup(
            IReadOnlyList<HousePlaceableDefinition> definitions,
            Action<IReadOnlyList<HousePlaceableDefinition>> confirmed,
            Action cancelled)
        {
            this.definitions = definitions;
            this.confirmed = confirmed;
            this.cancelled = cancelled;
            selected = new bool[definitions.Count];
            for (int i = 0; i < selected.Length; i++)
            {
                selected[i] = true;
            }
        }

        public static void Show(
            Vector2 screenPosition,
            IReadOnlyList<HousePlaceableDefinition> definitions,
            Action<IReadOnlyList<HousePlaceableDefinition>> confirmed,
            Action cancelled)
        {
            PopupWindow.Show(
                new Rect(screenPosition, Vector2.one),
                new HouseReinforcementPickerPopup(definitions, confirmed, cancelled));
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(360f, Mathf.Clamp(150f + definitions.Count * 58f, 240f, 520f));
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField("Choose Reinforcements", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each location can spawn any checked reinforcement. After confirming, click repeatedly in Scene view to place locations.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < definitions.Count; i++)
            {
                HousePlaceableDefinition definition = definitions[i];
                Texture preview = definition.Preview != null
                    ? definition.Preview
                    : AssetPreview.GetAssetPreview(definition.Prefab) ?? AssetPreview.GetMiniThumbnail(definition.Prefab);
                GUIContent content = new(definition.DisplayName, preview);
                selected[i] = GUILayout.Toggle(selected[i], content, GUI.skin.button, GUILayout.Height(52f), GUILayout.ExpandWidth(true));
            }

            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel"))
                {
                    completed = true;
                    editorWindow.Close();
                    cancelled?.Invoke();
                }

                using (new EditorGUI.DisabledScope(!HasSelection()))
                {
                    if (GUILayout.Button("Place Locations", GUILayout.Height(28f)))
                    {
                        List<HousePlaceableDefinition> result = new();
                        for (int i = 0; i < definitions.Count; i++)
                        {
                            if (selected[i])
                            {
                                result.Add(definitions[i]);
                            }
                        }

                        completed = true;
                        editorWindow.Close();
                        confirmed?.Invoke(result);
                    }
                }
            }
        }

        public override void OnClose()
        {
            if (!completed)
            {
                cancelled?.Invoke();
            }
        }

        private bool HasSelection()
        {
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i])
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
