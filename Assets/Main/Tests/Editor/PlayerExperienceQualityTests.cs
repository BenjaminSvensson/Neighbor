using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Neighbor.Main.Tests
{
    public sealed class PlayerExperienceQualityTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void DeathController_DefaultsCheckpointsToSessionOnly()
        {
            PlayerDeathController deathController = CreateObject("Session Checkpoint Test").AddComponent<PlayerDeathController>();

            Assert.That(GetField<bool>(deathController, "loadSavedCheckpointOnAwake"), Is.False);
            Assert.That(GetField<bool>(deathController, "persistCheckpoints"), Is.False);
        }

        [Test]
        public void DeathOverlay_ScalesConsistentlyAcrossScreenResolutions()
        {
            PlayerDeathController deathController = CreateObject("Player").AddComponent<PlayerDeathController>();

            Invoke(deathController, "EnsureFadeOverlay");

            CanvasScaler scaler = deathController.GetComponentInChildren<CanvasScaler>(true);
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void MovementOnboardingPrompt_UsesCurrentBindingsAndReadableGuidance()
        {
            PlayerOnboardingDirector director = CreateObject("Player").AddComponent<PlayerOnboardingDirector>();
            SetField(director, "movementPrompt", "Stay quiet near the house.");

            string prompt = InvokeResult<string>(director, "BuildMovementPrompt");
            string expected =
                $"{PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.Forward)}/" +
                $"{PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.Left)}/" +
                $"{PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.Backward)}/" +
                $"{PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.Right)}: move. " +
                $"Hold {PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.Run)}: run. " +
                $"Hold {PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.Crouch)}: crouch. " +
                "Stay quiet near the house.";

            Assert.That(prompt, Is.EqualTo(expected));
        }

        [Test]
        public void InventoryHud_ShowsCleanSelectedItemNameAboveHotbar()
        {
            PlayerInteractor interactor = CreateObject("PlayerInteractor").AddComponent<PlayerInteractor>();
            GameObject pickupObject = CreateObject("PlaceholderPhotoCamera(Clone)");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickupable = pickupObject.AddComponent<Pickupable>();
            SetField(
                interactor,
                "inventorySlots",
                new[] { pickupable, null, null, null, null, null });
            SetField(interactor, "activeInventorySlot", 0);

            GameObject hudObject = CreateObject("Inventory HUD", typeof(RectTransform));
            PlayerInventoryHudView hud = hudObject.AddComponent<PlayerInventoryHudView>();
            hud.SetInteractor(interactor);

            Text selectedItemName = GetField<Text>(hud, "selectedItemNameText");
            RectTransform panel = GetField<RectTransform>(hud, "panelRectTransform");
            Assert.That(selectedItemName, Is.Not.Null);
            Assert.That(selectedItemName.text, Is.EqualTo("Photo Camera"));
            Assert.That(selectedItemName.GetComponent<Outline>(), Is.Not.Null);
            Assert.That(
                selectedItemName.rectTransform.anchoredPosition.y,
                Is.GreaterThan(panel.anchoredPosition.y + panel.sizeDelta.y));
        }

        [Test]
        public void PcRenderer_PreservesStrongAnalogIdentityWithoutLowResolutionPixelation()
        {
            string renderer = File.ReadAllText("Assets/Settings/Renderer/PC_Renderer.asset");
            string pipeline = File.ReadAllText("Assets/Settings/Renderer/PC_RPAsset.asset");

            Assert.That(renderer, Does.Contain("m_Name: VHS Recording"));
            Assert.That(renderer, Does.Contain("intensity: 0.55"));
            Assert.That(renderer, Does.Contain("scanlineIntensity: 0.16"));
            Assert.That(renderer, Does.Contain("m_Name: Seventies Film"));
            Assert.That(renderer, Does.Contain("warmth: 0.32"));
            Assert.That(renderer, Does.Contain("pixelSize: 2"));
            Assert.That(renderer, Does.Contain("colorLevels: 26"));
            Assert.That(renderer, Does.Contain("ditherStrength: 0.018"));
            Assert.That(pipeline, Does.Contain("m_MainLightShadowmapResolution: 4096"));
            Assert.That(pipeline, Does.Contain("m_ShadowDistance: 140"));
            Assert.That(pipeline, Does.Contain("m_MSAA: 4"));
        }

        private GameObject CreateObject(string name, params System.Type[] components)
        {
            GameObject gameObject = components == null || components.Length == 0
                ? new GameObject(name)
                : new GameObject(name, components);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetField<TValue>(object target, string fieldName, TValue value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not find private field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static TValue GetField<TValue>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not find private field '{fieldName}'.");
            return (TValue)field.GetValue(target);
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not find private method '{methodName}'.");
            method.Invoke(target, null);
        }

        private static TResult InvokeResult<TResult>(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not find private method '{methodName}'.");
            return (TResult)method.Invoke(target, null);
        }
    }
}
