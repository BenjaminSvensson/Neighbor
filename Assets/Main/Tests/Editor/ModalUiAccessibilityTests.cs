using System.Collections.Generic;
using System.Reflection;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Neighbor.Main.Tests
{
    public sealed class ModalUiAccessibilityTests
    {
        [Test]
        public void RuntimeUiEventSystem_ProvidesInputSystemModule()
        {
            EventSystem eventSystem = RuntimeUiEventSystem.EnsureExists();

            try
            {
                Assert.That(eventSystem, Is.Not.Null);
                Assert.That(eventSystem.isActiveAndEnabled, Is.True);
                Assert.That(eventSystem.GetComponent<BaseInputModule>(), Is.Not.Null);
                Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            }
            finally
            {
                DestroyGeneratedEventSystem(eventSystem);
            }
        }

        [Test]
        public void PauseMenu_GeneratedUiIsInteractiveFitsScreenAndDoesNotOverlapResume()
        {
            float previousTimeScale = Time.timeScale;
            CursorLockMode previousLockState = Cursor.lockState;
            bool previousCursorVisible = Cursor.visible;
            GameObject playerObject = new("Pause Menu Accessibility Test");
            EventSystem eventSystem = null;

            try
            {
                PlayerPauseMenu pauseMenu = playerObject.AddComponent<PlayerPauseMenu>();
                InvokePrivate(pauseMenu, "Awake");
                Canvas canvas = playerObject.GetComponentInChildren<Canvas>(true);
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);

                RectTransform panel = FindRectTransform(playerObject.transform, "Panel");
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.rect.height, Is.LessThanOrEqualTo(1080f));

                Button resumeButton = GetPrivateField<Button>(pauseMenu, "resumeButton");
                Dictionary<PlayerInputBindingAction, Text> bindingTexts =
                    GetPrivateField<Dictionary<PlayerInputBindingAction, Text>>(pauseMenu, "bindingValueTexts");
                RectTransform lastBinding = bindingTexts[PlayerInputBindingAction.Inventory6].transform.parent as RectTransform;
                RectTransform resumeRect = resumeButton.transform as RectTransform;

                Assert.That(resumeButton, Is.Not.Null);
                Assert.That(lastBinding, Is.Not.Null);
                Assert.That(resumeRect, Is.Not.Null);
                float lastBindingBottom = lastBinding.anchoredPosition.y + lastBinding.rect.yMin;
                float resumeTop = resumeRect.anchoredPosition.y + resumeRect.rect.yMax;
                Assert.That(resumeTop, Is.LessThanOrEqualTo(lastBindingBottom));

                InvokePrivate(pauseMenu, "Open");
                eventSystem = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
                Assert.That(eventSystem, Is.Not.Null);
                Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(resumeButton.gameObject));
                Assert.That(InteractionOverlayState.IsGameplayInputBlocked, Is.True);
                InvokePrivate(pauseMenu, "Close");
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                DestroyGeneratedEventSystem(eventSystem);
                Time.timeScale = previousTimeScale;
                Cursor.lockState = previousLockState;
                Cursor.visible = previousCursorVisible;
            }
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
            method.Invoke(target, null);
        }

        private static RectTransform FindRectTransform(Transform root, string objectName)
        {
            RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name == objectName)
                {
                    return rects[i];
                }
            }

            return null;
        }

        private static void DestroyGeneratedEventSystem(EventSystem eventSystem)
        {
            if (eventSystem != null && eventSystem.gameObject.name == "Runtime EventSystem")
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }
    }
}
