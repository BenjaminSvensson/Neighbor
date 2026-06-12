using System.Collections.Generic;
using System.Reflection;
using Neighbor.Main.Features.Interaction;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Neighbor.Main.Tests
{
    internal sealed class GameplaySmokeTestContext
    {
        private readonly List<GameObject> objects = new();
        private readonly List<Component> initializedComponents = new();

        public GameObject CreateObject(string name = "TestObject")
        {
            GameObject gameObject = new(name);
            objects.Add(gameObject);
            return gameObject;
        }

        public T AddInitializedComponent<T>(string name = null) where T : Component
        {
            return AddInitializedComponent<T>(CreateObject(name ?? typeof(T).Name));
        }

        public T AddInitializedComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.AddComponent<T>();
            GameplaySmokeTestReflection.InvokeIfPresent(component, "Awake");
            GameplaySmokeTestReflection.InvokeIfPresent(component, "OnEnable");
            initializedComponents.Add(component);
            return component;
        }

        public void Dispose()
        {
            for (int i = initializedComponents.Count - 1; i >= 0; i--)
            {
                if (initializedComponents[i] != null)
                {
                    GameplaySmokeTestReflection.InvokeIfPresent(initializedComponents[i], "OnDisable");
                }
            }

            initializedComponents.Clear();
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }

            objects.Clear();
        }
    }

    internal static class GameplaySmokeTestReflection
    {
        public static void SetField<TValue>(object target, string fieldName, TValue value)
        {
            FieldInfo field = FindField(target, fieldName);
            field.SetValue(target, value);
        }

        public static TValue GetField<TValue>(object target, string fieldName)
        {
            FieldInfo field = FindField(target, fieldName);
            return (TValue)field.GetValue(target);
        }

        public static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(target, methodName);
            Assert.That(method, Is.Not.Null, $"Could not find private method '{methodName}'.");
            method.Invoke(target, arguments);
        }

        public static void InvokeIfPresent(object target, string methodName)
        {
            FindMethod(target, methodName)?.Invoke(target, null);
        }

        private static FieldInfo FindField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Could not find private field '{fieldName}'.");
            return field;
        }

        private static MethodInfo FindMethod(object target, string methodName)
        {
            return target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }

    internal sealed class PickupLifecycleProbe : MonoBehaviour, IPickupLifecycleReceiver
    {
        public int PickupStartedCount { get; private set; }
        public int PickupPlacedCount { get; private set; }

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            PickupStartedCount++;
        }

        public void OnPickupPlaced(Pickupable pickupable)
        {
            PickupPlacedCount++;
        }
    }
}
