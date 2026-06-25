#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Neighbor.Main.Features.Interaction;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.EditorTools
{
    public static class InteractionPrefabGameReadyUtility
    {
        public const string InteractionItemsRoot = "Assets/Main/Features/Interaction/Items";

        private const string PrimaryInteractionLayerName = "Interactible";
        private const string SecondaryInteractionLayerName = "Interactible2";

        private static readonly HashSet<string> PickupPrefabNames = new()
        {
            "Axe"
        };

        [MenuItem("Tools/Neighbor/Validate Interaction Prefabs")]
        private static void ValidateFromMenu()
        {
            IReadOnlyList<string> issues = ValidateAllPrefabs(true);
            if (issues.Count == 0)
            {
                Debug.Log("Interaction prefab validation passed.");
                return;
            }

            Debug.LogError($"Interaction prefab validation found {issues.Count} issue(s).");
        }

        [MenuItem("Tools/Neighbor/Repair Interaction Prefabs")]
        private static void RepairFromMenu()
        {
            int repairCount = RepairAllPrefabs(true);
            IReadOnlyList<string> issues = ValidateAllPrefabs(true);
            Debug.Log(
                issues.Count == 0
                    ? $"Interaction prefab repair finished with {repairCount} change(s)."
                    : $"Interaction prefab repair finished with {repairCount} change(s), but {issues.Count} issue(s) remain.");
        }

        public static void ValidateFromCommandLine()
        {
            IReadOnlyList<string> issues = ValidateAllPrefabs(true);
            EditorApplication.Exit(issues.Count == 0 ? 0 : 1);
        }

        public static void RepairFromCommandLine()
        {
            int repairCount = RepairAllPrefabs(true);
            IReadOnlyList<string> issues = ValidateAllPrefabs(true);
            Debug.Log(
                issues.Count == 0
                    ? $"Interaction prefab repair finished with {repairCount} change(s)."
                    : $"Interaction prefab repair finished with {repairCount} change(s), but {issues.Count} issue(s) remain.");
            EditorApplication.Exit(issues.Count == 0 ? 0 : 1);
        }

        public static IReadOnlyList<string> ValidateAllPrefabs(bool logIssues = false)
        {
            List<string> issues = new();
            if (!TryGetInteractionLayer(out _, out _))
            {
                issues.Add(
                    $"Project layers '{PrimaryInteractionLayerName}' and '{SecondaryInteractionLayerName}' must exist for interaction targeting.");
                LogIssues(issues, logIssues);
                return issues;
            }

            foreach (string prefabPath in GetInteractionPrefabPaths())
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    issues.Add($"{prefabPath}: failed to load prefab asset.");
                    continue;
                }

                ValidatePrefab(prefab, prefabPath, issues);
            }

            LogIssues(issues, logIssues);
            return issues;
        }

        public static int RepairAllPrefabs(bool logRepairs = false)
        {
            if (!TryGetInteractionLayer(out int interactionLayer, out _))
            {
                Debug.LogError(
                    $"Cannot repair interaction prefabs because layer '{PrimaryInteractionLayerName}' or '{SecondaryInteractionLayerName}' is missing.");
                return 0;
            }

            int repairCount = 0;
            foreach (string prefabPath in GetInteractionPrefabPaths())
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    Debug.LogError($"{prefabPath}: failed to load prefab contents for repair.");
                    continue;
                }

                bool changed = false;
                try
                {
                    changed |= RepairPrefab(root, interactionLayer, logRepairs ? prefabPath : null, ref repairCount);
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool savedSuccessfully);
                        if (!savedSuccessfully)
                        {
                            Debug.LogError($"{prefabPath}: failed to save repaired prefab.");
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return repairCount;
        }

        private static string[] GetInteractionPrefabPaths()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { InteractionItemsRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .ToArray();
        }

        private static void ValidatePrefab(GameObject prefab, string prefabPath, List<string> issues)
        {
            ValidateMissingScripts(prefab, prefabPath, issues);

            if (!HasVisiblePresentation(prefab))
            {
                issues.Add($"{prefabPath}: prefab has no renderer, line renderer, or light presentation.");
            }

            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                issues.Add($"{prefabPath}: prefab has no collider, so it cannot block, trigger, or be targeted reliably.");
            }

            foreach (MonoBehaviour component in GetLiveMonoBehaviours(prefab))
            {
                if (component is IInteractable || component is IHoldInteractable)
                {
                    ValidateTargetableComponent(component, prefabPath, issues);
                }

                if (RequiresItemAudioFeedback(component))
                {
                    ValidateItemAudioFeedback(component, prefabPath, issues);
                }
            }

            foreach (Pickupable pickupable in prefab.GetComponentsInChildren<Pickupable>(true))
            {
                ValidatePickupable(pickupable, prefabPath, issues);
            }

            foreach (AudioSource audioSource in prefab.GetComponentsInChildren<AudioSource>(true))
            {
                ValidateAudioSource(audioSource, prefabPath, issues);
            }
        }

        private static bool RepairPrefab(GameObject root, int interactionLayer, string logPath, ref int repairCount)
        {
            bool changed = false;

            if (PickupPrefabNames.Contains(root.name) && root.GetComponent<Pickupable>() == null)
            {
                changed |= EnsureGameplayCollider(root, false, logPath, "pickup collider", ref repairCount);
                changed |= EnsureComponent<Rigidbody>(root, logPath, ref repairCount);
                changed |= EnsureComponent<Pickupable>(root, logPath, ref repairCount);
            }

            if (root.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                changed |= EnsureGameplayCollider(root, false, logPath, "fallback collider", ref repairCount);
            }

            foreach (Pickupable pickupable in root.GetComponentsInChildren<Pickupable>(true))
            {
                changed |= RepairPickupable(pickupable, interactionLayer, logPath, ref repairCount);
            }

            foreach (MonoBehaviour component in GetLiveMonoBehaviours(root))
            {
                if (component is IInteractable || component is IHoldInteractable)
                {
                    changed |= RepairTargetableComponent(component, interactionLayer, logPath, ref repairCount);
                }

                if (RequiresItemAudioFeedback(component))
                {
                    changed |= EnsureComponent<ItemAudioFeedback>(component.gameObject, logPath, ref repairCount);
                    changed |= EnsureComponent<AudioSource>(component.gameObject, logPath, ref repairCount);
                }
            }

            foreach (AudioSource audioSource in root.GetComponentsInChildren<AudioSource>(true))
            {
                changed |= ConfigureAudioSource(audioSource, logPath, ref repairCount);
            }

            return changed;
        }

        private static void ValidateMissingScripts(GameObject prefab, string prefabPath, List<string> issues)
        {
            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missingCount > 0)
                {
                    issues.Add(
                        $"{prefabPath}: {missingCount} missing script reference(s) on {GetHierarchyPath(transform)}.");
                }
            }
        }

        private static void ValidateTargetableComponent(MonoBehaviour component, string prefabPath, List<string> issues)
        {
            if (!IsActiveEnabled(component))
            {
                return;
            }

            Collider[] colliders = component.GetComponentsInChildren<Collider>(true);
            bool hasEnabledCollider = false;
            bool hasInteractionLayerCollider = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (!IsActiveEnabled(collider))
                {
                    continue;
                }

                hasEnabledCollider = true;
                if (IsInteractionLayer(collider.gameObject.layer))
                {
                    hasInteractionLayerCollider = true;
                }
            }

            string componentPath = GetHierarchyPath(component.transform);
            if (!hasEnabledCollider)
            {
                issues.Add($"{prefabPath}: {component.GetType().Name} on {componentPath} has no enabled collider in its hierarchy.");
            }
            else if (!hasInteractionLayerCollider)
            {
                issues.Add(
                    $"{prefabPath}: {component.GetType().Name} on {componentPath} has no enabled collider on {PrimaryInteractionLayerName}/{SecondaryInteractionLayerName}.");
            }
        }

        private static void ValidatePickupable(Pickupable pickupable, string prefabPath, List<string> issues)
        {
            if (!IsActiveEnabled(pickupable))
            {
                return;
            }

            string pickupPath = GetHierarchyPath(pickupable.transform);
            Rigidbody body = pickupable.GetComponent<Rigidbody>();
            if (body == null)
            {
                issues.Add($"{prefabPath}: Pickupable on {pickupPath} is missing a Rigidbody.");
            }
            else
            {
                if (body.isKinematic)
                {
                    issues.Add($"{prefabPath}: Pickupable on {pickupPath} starts kinematic and will not drop as a physics prop.");
                }

                if (!body.useGravity)
                {
                    issues.Add($"{prefabPath}: Pickupable on {pickupPath} has gravity disabled.");
                }

                if (body.interpolation != RigidbodyInterpolation.Interpolate)
                {
                    issues.Add($"{prefabPath}: Pickupable on {pickupPath} should use RigidbodyInterpolation.Interpolate.");
                }

                if (body.collisionDetectionMode == CollisionDetectionMode.Discrete)
                {
                    issues.Add($"{prefabPath}: Pickupable on {pickupPath} should use continuous collision detection.");
                }
            }

            Collider[] colliders = pickupable.GetComponentsInChildren<Collider>(true);
            if (!colliders.Any(collider => IsActiveEnabled(collider) && !collider.isTrigger))
            {
                issues.Add($"{prefabPath}: Pickupable on {pickupPath} has no enabled non-trigger collider for physics.");
            }

            if (pickupable.GetComponent<PhysicsImpactNoiseEmitter>() == null)
            {
                issues.Add($"{prefabPath}: Pickupable on {pickupPath} is missing PhysicsImpactNoiseEmitter feedback.");
            }
        }

        private static void ValidateItemAudioFeedback(MonoBehaviour component, string prefabPath, List<string> issues)
        {
            if (!IsActiveEnabled(component))
            {
                return;
            }

            if (component.GetComponent<ItemAudioFeedback>() == null)
            {
                issues.Add($"{prefabPath}: {component.GetType().Name} on {GetHierarchyPath(component.transform)} is missing ItemAudioFeedback.");
            }

            if (component.GetComponent<AudioSource>() == null)
            {
                issues.Add($"{prefabPath}: {component.GetType().Name} on {GetHierarchyPath(component.transform)} is missing an AudioSource.");
            }
        }

        private static void ValidateAudioSource(AudioSource audioSource, string prefabPath, List<string> issues)
        {
            if (audioSource == null)
            {
                return;
            }

            string sourcePath = GetHierarchyPath(audioSource.transform);
            if (audioSource.playOnAwake)
            {
                issues.Add($"{prefabPath}: AudioSource on {sourcePath} has Play On Awake enabled.");
            }

            if (audioSource.spatialBlend < 0.99f)
            {
                issues.Add($"{prefabPath}: AudioSource on {sourcePath} should be fully 3D.");
            }

            if (audioSource.rolloffMode != AudioRolloffMode.Logarithmic)
            {
                issues.Add($"{prefabPath}: AudioSource on {sourcePath} should use logarithmic rolloff.");
            }

            if (audioSource.maxDistance < audioSource.minDistance)
            {
                issues.Add($"{prefabPath}: AudioSource on {sourcePath} has max distance below min distance.");
            }
        }

        private static bool RepairPickupable(Pickupable pickupable, int interactionLayer, string logPath, ref int repairCount)
        {
            bool changed = false;
            GameObject gameObject = pickupable.gameObject;

            changed |= EnsureGameplayCollider(gameObject, false, logPath, "pickup physics collider", ref repairCount);

            Rigidbody body = pickupable.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
                LogRepair(logPath, $"Added Rigidbody to {GetHierarchyPath(gameObject.transform)}.", ref repairCount);
                changed = true;
            }

            if (!body.useGravity)
            {
                body.useGravity = true;
                LogRepair(logPath, $"Enabled pickup gravity on {GetHierarchyPath(gameObject.transform)}.", ref repairCount);
                changed = true;
            }

            if (body.isKinematic)
            {
                body.isKinematic = false;
                LogRepair(logPath, $"Made pickup dynamic on {GetHierarchyPath(gameObject.transform)}.", ref repairCount);
                changed = true;
            }

            if (body.interpolation != RigidbodyInterpolation.Interpolate)
            {
                body.interpolation = RigidbodyInterpolation.Interpolate;
                LogRepair(logPath, $"Enabled pickup interpolation on {GetHierarchyPath(gameObject.transform)}.", ref repairCount);
                changed = true;
            }
            if (body.collisionDetectionMode == CollisionDetectionMode.Discrete)
            {
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                LogRepair(logPath, $"Enabled continuous collision on {GetHierarchyPath(gameObject.transform)}.", ref repairCount);
                changed = true;
            }

            if (body.mass <= 0f)
            {
                body.mass = 1f;
                LogRepair(logPath, $"Reset non-positive mass on {GetHierarchyPath(gameObject.transform)}.", ref repairCount);
                changed = true;
            }

            foreach (Collider collider in pickupable.GetComponentsInChildren<Collider>(true))
            {
                changed |= SetLayer(collider.gameObject, interactionLayer, logPath, "pickup collider layer", ref repairCount);
            }

            changed |= EnsureComponent<PhysicsImpactNoiseEmitter>(gameObject, logPath, ref repairCount);
            changed |= EnsureComponent<AudioSource>(gameObject, logPath, ref repairCount);
            return changed;
        }

        private static bool RepairTargetableComponent(MonoBehaviour component, int interactionLayer, string logPath, ref int repairCount)
        {
            bool changed = false;
            Collider[] colliders = component.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                changed |= EnsureGameplayCollider(component.gameObject, true, logPath, "interaction trigger", ref repairCount);
                colliders = component.GetComponentsInChildren<Collider>(true);
            }

            foreach (Collider collider in colliders)
            {
                if (collider != null && collider.enabled)
                {
                    changed |= SetLayer(collider.gameObject, interactionLayer, logPath, "targetable collider layer", ref repairCount);
                }
            }

            return changed;
        }

        private static bool EnsureGameplayCollider(GameObject gameObject, bool trigger, string logPath, string reason, ref int repairCount)
        {
            Collider[] colliders = gameObject.GetComponentsInChildren<Collider>(true);
            Collider existingCollider = colliders.FirstOrDefault(collider => collider != null && collider.enabled && collider.isTrigger == trigger);
            if (existingCollider != null)
            {
                return false;
            }

            BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();
            bool changed = false;
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
                changed = true;
            }

            if (TryGetLocalRendererBounds(gameObject, gameObject.transform, out Bounds bounds))
            {
                if (boxCollider.center != bounds.center)
                {
                    boxCollider.center = bounds.center;
                    changed = true;
                }

                Vector3 desiredSize = Vector3.Max(bounds.size, Vector3.one * 0.05f);
                if (boxCollider.size != desiredSize)
                {
                    boxCollider.size = desiredSize;
                    changed = true;
                }
            }
            else if (boxCollider.size.sqrMagnitude <= 0.0001f)
            {
                boxCollider.size = Vector3.one * 0.5f;
                changed = true;
            }

            if (boxCollider.isTrigger != trigger)
            {
                boxCollider.isTrigger = trigger;
                changed = true;
            }

            if (changed)
            {
                LogRepair(logPath, $"Added or updated {reason} on {GetHierarchyPath(gameObject.transform)}.", ref repairCount);
            }

            return changed;
        }

        private static bool ConfigureAudioSource(AudioSource audioSource, string logPath, ref int repairCount)
        {
            bool changed = false;
            if (audioSource.playOnAwake)
            {
                audioSource.playOnAwake = false;
                LogRepair(logPath, $"Disabled Play On Awake on {GetHierarchyPath(audioSource.transform)}.", ref repairCount);
                changed = true;
            }

            if (!Mathf.Approximately(audioSource.spatialBlend, 1f))
            {
                audioSource.spatialBlend = 1f;
                LogRepair(logPath, $"Made AudioSource 3D on {GetHierarchyPath(audioSource.transform)}.", ref repairCount);
                changed = true;
            }

            if (audioSource.rolloffMode != AudioRolloffMode.Logarithmic)
            {
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                LogRepair(logPath, $"Set logarithmic rolloff on {GetHierarchyPath(audioSource.transform)}.", ref repairCount);
                changed = true;
            }

            if (audioSource.maxDistance < audioSource.minDistance)
            {
                audioSource.maxDistance = audioSource.minDistance;
                LogRepair(logPath, $"Raised AudioSource max distance on {GetHierarchyPath(audioSource.transform)}.", ref repairCount);
                changed = true;
            }

            return changed;
        }

        private static bool EnsureComponent<T>(GameObject gameObject, string logPath, ref int repairCount)
            where T : Component
        {
            if (gameObject.GetComponent<T>() != null)
            {
                return false;
            }

            gameObject.AddComponent<T>();
            LogRepair(logPath, $"Added {typeof(T).Name} to {GetHierarchyPath(gameObject.transform)}.", ref repairCount);
            return true;
        }

        private static bool SetLayer(GameObject gameObject, int layer, string logPath, string reason, ref int repairCount)
        {
            if (gameObject.layer == layer || IsInteractionLayer(gameObject.layer))
            {
                return false;
            }

            gameObject.layer = layer;
            LogRepair(logPath, $"Set {GetHierarchyPath(gameObject.transform)} to {PrimaryInteractionLayerName} for {reason}.", ref repairCount);
            return true;
        }

        private static bool TryGetLocalRendererBounds(GameObject root, Transform reference, out Bounds localBounds)
        {
            localBounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                Vector3[] corners =
                {
                    new(min.x, min.y, min.z),
                    new(max.x, min.y, min.z),
                    new(min.x, max.y, min.z),
                    new(max.x, max.y, min.z),
                    new(min.x, min.y, max.z),
                    new(max.x, min.y, max.z),
                    new(min.x, max.y, max.z),
                    new(max.x, max.y, max.z)
                };

                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 localCorner = reference.InverseTransformPoint(corners[i]);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }

            return hasBounds;
        }

        private static IEnumerable<MonoBehaviour> GetLiveMonoBehaviours(GameObject root)
        {
            return root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(component => component != null);
        }

        private static bool HasVisiblePresentation(GameObject prefab)
        {
            return prefab.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer != null)
                || prefab.GetComponentsInChildren<Light>(true).Any(light => light != null);
        }

        private static bool RequiresItemAudioFeedback(MonoBehaviour component)
        {
            return component is WoodBoardPryTarget
                or WindowBlinds
                or VentCover
                or TomatoSquash
                or LaserGridPowerSwitch
                or LaserGrid
                or SpringLoadedBoxingGloveTrap
                or RaySawBladeTrap
                or FakeFloorTrapDoor
                or Beartrap
                or ClosetDoorPair
                or DoorBlockerChair
                or SlidingCupboardCompartment
                or ReadableBook
                or WritableNotebook;
        }

        private static bool IsActiveEnabled(Component component)
        {
            if (component == null || !component.gameObject.activeInHierarchy)
            {
                return false;
            }

            return component is not Behaviour behaviour || behaviour.enabled;
        }

        private static bool TryGetInteractionLayer(out int primaryLayer, out int secondaryLayer)
        {
            primaryLayer = LayerMask.NameToLayer(PrimaryInteractionLayerName);
            secondaryLayer = LayerMask.NameToLayer(SecondaryInteractionLayerName);
            return primaryLayer >= 0 && secondaryLayer >= 0;
        }

        private static bool IsInteractionLayer(int layer)
        {
            return layer == LayerMask.NameToLayer(PrimaryInteractionLayerName)
                || layer == LayerMask.NameToLayer(SecondaryInteractionLayerName);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private static void LogIssues(IReadOnlyList<string> issues, bool logIssues)
        {
            if (!logIssues)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogError(issues[i]);
            }
        }

        private static void LogRepair(string prefabPath, string message, ref int repairCount)
        {
            repairCount++;
            if (!string.IsNullOrEmpty(prefabPath))
            {
                Debug.Log($"{prefabPath}: {message}");
            }
        }
    }
}
#endif
