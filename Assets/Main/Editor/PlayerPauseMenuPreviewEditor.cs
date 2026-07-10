using System.Reflection;
using Neighbor.Main.Features.Player;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.Editor
{
    internal static class PlayerPauseMenuPreviewEditor
    {
        private const string MenuPath = "Tools/Neighbor/Toggle Pause Menu Preview";

        [MenuItem(MenuPath, priority = 25)]
        private static void TogglePauseMenuPreview()
        {
            PlayerPauseMenu pauseMenu = Object.FindAnyObjectByType<PlayerPauseMenu>(FindObjectsInactive.Include);
            if (pauseMenu == null)
            {
                Debug.LogWarning("Pause menu preview requires Play Mode and an active player.");
                return;
            }

            FieldInfo isOpenField = typeof(PlayerPauseMenu).GetField(
                "isOpen",
                BindingFlags.Instance | BindingFlags.NonPublic);
            bool isOpen = isOpenField != null && (bool)isOpenField.GetValue(pauseMenu);
            MethodInfo toggleMethod = typeof(PlayerPauseMenu).GetMethod(
                isOpen ? "Close" : "Open",
                BindingFlags.Instance | BindingFlags.NonPublic);
            toggleMethod?.Invoke(pauseMenu, null);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanTogglePauseMenuPreview()
        {
            return EditorApplication.isPlaying;
        }
    }
}
