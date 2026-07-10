using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Neighbor.Main.Features.Interaction
{
    /// <summary>
    /// Ensures code-generated runtime UI has a working input event system, even in
    /// scenes that do not contain an authored EventSystem object.
    /// </summary>
    public static class RuntimeUiEventSystem
    {
        public static EventSystem EnsureExists()
        {
            EventSystem current = EventSystem.current;
            if (current != null && current.isActiveAndEnabled)
            {
                EnsureInputModule(current.gameObject);
                return current;
            }

            EventSystem[] existingSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            for (int i = 0; i < existingSystems.Length; i++)
            {
                EventSystem existing = existingSystems[i];
                if (existing == null || !existing.isActiveAndEnabled)
                {
                    continue;
                }

                EnsureInputModule(existing.gameObject);
                return existing;
            }

            GameObject eventSystemObject = new("Runtime EventSystem");
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            EnsureInputModule(eventSystemObject);
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(eventSystemObject);
            }

            return eventSystem;
        }

        private static void EnsureInputModule(GameObject eventSystemObject)
        {
            if (eventSystemObject.GetComponent<BaseInputModule>() != null)
            {
                return;
            }

            InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
    }
}
