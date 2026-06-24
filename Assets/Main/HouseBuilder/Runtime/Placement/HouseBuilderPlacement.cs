using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [Serializable]
    public sealed class HouseBuilderPlacementSettings
    {
        [SerializeField] private bool gridSnapping = true;
        [SerializeField] private bool surfaceSnapping = true;
        [SerializeField] private bool edgeSnapping = true;
        [SerializeField] private bool cornerSnapping = true;
        [SerializeField] private bool rotationSnapping = true;
        [SerializeField, Min(0.01f)] private float gridSize = 0.25f;
        [SerializeField, Min(1f)] private float rotationStep = 15f;
        [SerializeField, Min(0.01f)] private float featureSnapDistance = 0.4f;
        [SerializeField, Min(0f)] private float collisionPadding = 0.01f;
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private LayerMask collisionMask = ~0;

        public bool GridSnapping => gridSnapping;
        public bool SurfaceSnapping => surfaceSnapping;
        public bool EdgeSnapping => edgeSnapping;
        public bool CornerSnapping => cornerSnapping;
        public bool RotationSnapping => rotationSnapping;
        public float GridSize => gridSize;
        public float RotationStep => rotationStep;
        public float FeatureSnapDistance => featureSnapDistance;
        public float CollisionPadding => collisionPadding;
        public LayerMask SurfaceMask => surfaceMask;
        public LayerMask CollisionMask => collisionMask;
    }

    public enum HouseSnapKind
    {
        None,
        Grid,
        Surface,
        Edge,
        Corner
    }

    public readonly struct HousePlacementResult
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 SurfaceNormal { get; }
        public HouseSurfaceType SurfaceType { get; }
        public HouseSnapKind SnapKind { get; }
        public bool HasSurface { get; }

        public HousePlacementResult(
            Vector3 position,
            Quaternion rotation,
            Vector3 surfaceNormal,
            HouseSurfaceType surfaceType,
            HouseSnapKind snapKind,
            bool hasSurface)
        {
            Position = position;
            Rotation = rotation;
            SurfaceNormal = surfaceNormal;
            SurfaceType = surfaceType;
            SnapKind = snapKind;
            HasSurface = hasSurface;
        }
    }

    public readonly struct HousePlacementValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }

        public HousePlacementValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }
    }

    public static class HouseBuilderSnapUtility
    {
        private static readonly Vector3[] BoundsCorners = new Vector3[8];

        public static HousePlacementResult Calculate(
            Vector3 fallbackPosition,
            Quaternion requestedRotation,
            bool hasSurface,
            RaycastHit surfaceHit,
            HousePlacementProfile profile,
            HouseBuilderPlacementSettings settings,
            IEnumerable<Bounds> nearbyBounds,
            Vector3 surfaceApproachDirection = default)
        {
            profile ??= new HousePlacementProfile();
            settings ??= new HouseBuilderPlacementSettings();

            Vector3 position = hasSurface && settings.SurfaceSnapping ? surfaceHit.point : fallbackPosition;
            Vector3 normal = hasSurface
                ? ResolveSurfaceNormal(surfaceHit.normal, surfaceApproachDirection)
                : Vector3.up;
            HouseSurfaceType surfaceType = ClassifySurface(normal);
            HouseSnapKind snapKind = hasSurface && settings.SurfaceSnapping ? HouseSnapKind.Surface : HouseSnapKind.None;
            bool hasFeature = false;
            Vector3 featureApproach = default;
            Bounds featureBounds = default;
            if (hasSurface && profile.GroundOnWall && surfaceType == HouseSurfaceType.Wall && surfaceHit.collider != null)
            {
                position.y = surfaceHit.collider.bounds.min.y
                    - profile.BoundsCenter.y
                    + profile.BoundsSize.y * 0.5f;
            }

            if (settings.CornerSnapping || settings.EdgeSnapping)
            {
                if (TryFindFeature(position, nearbyBounds, settings, out Vector3 featurePoint, out HouseSnapKind featureKind, out featureBounds))
                {
                    featureApproach = position - featurePoint;
                    position = featurePoint;
                    snapKind = featureKind;
                    hasFeature = true;
                }
            }

            if (settings.GridSnapping && !hasFeature)
            {
                position = hasSurface && settings.SurfaceSnapping
                    ? SnapOnSurface(position, normal, settings.GridSize)
                    : SnapVector(position, settings.GridSize);
                if (snapKind == HouseSnapKind.None)
                {
                    snapKind = HouseSnapKind.Grid;
                }
            }

            Quaternion rotation = requestedRotation;
            if (hasSurface
                && profile.SurfaceAlignment != HouseSurfaceAlignment.None
                && (profile.SurfaceAlignment is not (HouseSurfaceAlignment.ForwardToNormal or HouseSurfaceAlignment.RightToNormal)
                    || surfaceType == HouseSurfaceType.Wall))
            {
                if (profile.SurfaceAlignment is HouseSurfaceAlignment.ForwardToNormal or HouseSurfaceAlignment.RightToNormal)
                {
                    Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
                    if (up.sqrMagnitude < 0.001f)
                    {
                        up = requestedRotation * Vector3.up;
                    }

                    rotation = Quaternion.LookRotation(normal, up.normalized);
                    if (profile.SurfaceAlignment == HouseSurfaceAlignment.RightToNormal)
                    {
                        rotation *= Quaternion.Euler(0f, -90f, 0f);
                    }
                }
                else
                {
                    Vector3 forward = Vector3.ProjectOnPlane(requestedRotation * Vector3.forward, normal);
                    if (forward.sqrMagnitude < 0.001f)
                    {
                        forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
                    }

                    rotation = Quaternion.LookRotation(forward.normalized, normal);
                }
            }

            if (settings.RotationSnapping)
            {
                Vector3 euler = rotation.eulerAngles;
                euler.x = SnapValue(euler.x, settings.RotationStep);
                euler.y = SnapValue(euler.y, settings.RotationStep);
                euler.z = SnapValue(euler.z, settings.RotationStep);
                rotation = Quaternion.Euler(euler);
            }

            position = hasFeature && profile.SnapBoundsToFeatures
                ? AlignBoundsToFeature(position, featureApproach, featureBounds, rotation, profile, hasSurface && surfaceType == HouseSurfaceType.Ground)
                : position + rotation * profile.PlacementOffset;
            return new HousePlacementResult(position, rotation, normal, surfaceType, snapKind, hasSurface);
        }

        public static Vector3 SnapVector(Vector3 value, float increment)
        {
            return new Vector3(
                SnapValue(value.x, increment),
                SnapValue(value.y, increment),
                SnapValue(value.z, increment));
        }

        public static float SnapValue(float value, float increment)
        {
            return increment > 0f ? Mathf.Round(value / increment) * increment : value;
        }

        public static Vector3 SnapOnSurface(Vector3 value, Vector3 surfaceNormal, float increment)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
            Vector3 snapped = SnapVector(value, increment);
            return snapped + normal * Vector3.Dot(value - snapped, normal);
        }

        public static HouseSurfaceType ClassifySurface(Vector3 normal)
        {
            float up = Vector3.Dot(normal.normalized, Vector3.up);
            if (up > 0.65f)
            {
                return HouseSurfaceType.Ground;
            }

            return up < -0.65f ? HouseSurfaceType.Ceiling : HouseSurfaceType.Wall;
        }

        public static Vector3 ResolveSurfaceNormal(Vector3 normal, Vector3 approachDirection)
        {
            Vector3 resolved = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            if (ClassifySurface(resolved) == HouseSurfaceType.Wall
                && approachDirection.sqrMagnitude > 0.001f
                && Vector3.Dot(resolved, approachDirection.normalized) > 0f)
            {
                resolved = -resolved;
            }

            return resolved;
        }

        private static bool TryFindFeature(
            Vector3 point,
            IEnumerable<Bounds> boundsCollection,
            HouseBuilderPlacementSettings settings,
            out Vector3 featurePoint,
            out HouseSnapKind snapKind,
            out Bounds featureBounds)
        {
            featurePoint = point;
            snapKind = HouseSnapKind.None;
            featureBounds = default;
            float bestDistanceSquared = settings.FeatureSnapDistance * settings.FeatureSnapDistance;
            if (boundsCollection == null)
            {
                return false;
            }

            foreach (Bounds bounds in boundsCollection)
            {
                PopulateCorners(bounds);
                if (settings.CornerSnapping)
                {
                    for (int i = 0; i < BoundsCorners.Length; i++)
                    {
                        float distanceSquared = (BoundsCorners[i] - point).sqrMagnitude;
                        if (distanceSquared <= bestDistanceSquared)
                        {
                            bestDistanceSquared = distanceSquared;
                            featurePoint = BoundsCorners[i];
                            snapKind = HouseSnapKind.Corner;
                            featureBounds = bounds;
                        }
                    }
                }

                if (settings.EdgeSnapping && snapKind != HouseSnapKind.Corner)
                {
                    for (int i = 0; i < EdgeIndices.Length; i += 2)
                    {
                        Vector3 closest = ClosestPointOnSegment(point, BoundsCorners[EdgeIndices[i]], BoundsCorners[EdgeIndices[i + 1]]);
                        float distanceSquared = (closest - point).sqrMagnitude;
                        if (distanceSquared <= bestDistanceSquared)
                        {
                            bestDistanceSquared = distanceSquared;
                            featurePoint = closest;
                            snapKind = HouseSnapKind.Edge;
                            featureBounds = bounds;
                        }
                    }
                }
            }

            return snapKind != HouseSnapKind.None;
        }

        private static Vector3 AlignBoundsToFeature(
            Vector3 featurePoint,
            Vector3 approach,
            Bounds sourceBounds,
            Quaternion rotation,
            HousePlacementProfile profile,
            bool snappedFromGroundSurface)
        {
            Quaternion inverseRotation = Quaternion.Inverse(rotation);
            Vector3 localApproach = inverseRotation * approach;
            Vector3 localSourceSide = inverseRotation * (featurePoint - sourceBounds.center);
            Vector3 halfExtents = profile.BoundsSize * 0.5f;
            Vector3 contact = new(
                ResolveContactAxis(localApproach.x, localSourceSide.x, halfExtents.x),
                snappedFromGroundSurface && profile.GroundOnFeatureSnaps
                    ? -halfExtents.y
                    : ResolveContactAxis(localApproach.y, localSourceSide.y, halfExtents.y),
                ResolveContactAxis(localApproach.z, localSourceSide.z, halfExtents.z));
            return featurePoint - rotation * (profile.BoundsCenter + contact);
        }

        private static float ResolveContactAxis(float approach, float sourceSide, float halfExtent)
        {
            if (Mathf.Abs(approach) > 0.001f)
            {
                return -Mathf.Sign(approach) * halfExtent;
            }

            return Mathf.Abs(sourceSide) > 0.001f
                ? Mathf.Sign(sourceSide) * halfExtent
                : 0f;
        }

        private static readonly int[] EdgeIndices =
        {
            0, 1, 0, 2, 0, 4, 1, 3, 1, 5, 2, 3,
            2, 6, 3, 7, 4, 5, 4, 6, 5, 7, 6, 7
        };

        private static void PopulateCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            BoundsCorners[0] = new Vector3(min.x, min.y, min.z);
            BoundsCorners[1] = new Vector3(max.x, min.y, min.z);
            BoundsCorners[2] = new Vector3(min.x, max.y, min.z);
            BoundsCorners[3] = new Vector3(max.x, max.y, min.z);
            BoundsCorners[4] = new Vector3(min.x, min.y, max.z);
            BoundsCorners[5] = new Vector3(max.x, min.y, max.z);
            BoundsCorners[6] = new Vector3(min.x, max.y, max.z);
            BoundsCorners[7] = new Vector3(max.x, max.y, max.z);
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return start;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return start + segment * t;
        }
    }

    public static class HouseBuilderPlacementValidator
    {
        private static readonly Collider[] OverlapResults = new Collider[128];

        public static HousePlacementValidationResult Validate(
            Vector3 position,
            Quaternion rotation,
            HousePlacementProfile profile,
            HouseBuilderPlacementSettings settings,
            HousePlacementResult placement,
            Transform ignoreRoot = null,
            bool skipCollisionValidation = false)
        {
            profile ??= new HousePlacementProfile();
            settings ??= new HouseBuilderPlacementSettings();

            if (profile.RequireSurface && !placement.HasSurface)
            {
                return new HousePlacementValidationResult(false, "A surface is required.");
            }

            if (placement.HasSurface && (profile.AllowedSurfaces & placement.SurfaceType) == 0)
            {
                return new HousePlacementValidationResult(false, $"Cannot place on {placement.SurfaceType} surfaces.");
            }

            if (skipCollisionValidation || !profile.ValidateCollisions)
            {
                return new HousePlacementValidationResult(true, "Valid");
            }

            Vector3 halfExtents = Vector3.Max(profile.BoundsSize * 0.5f - Vector3.one * settings.CollisionPadding, Vector3.one * 0.005f);
            Vector3 center = position + rotation * profile.BoundsCenter;
            QueryTriggerInteraction triggerInteraction = profile.AllowTriggerOverlap
                ? QueryTriggerInteraction.Ignore
                : QueryTriggerInteraction.Collide;
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, OverlapResults, rotation, settings.CollisionMask, triggerInteraction);
            for (int i = 0; i < count; i++)
            {
                Collider collider = OverlapResults[i];
                if (collider == null || ignoreRoot != null && collider.transform.IsChildOf(ignoreRoot))
                {
                    continue;
                }

                return new HousePlacementValidationResult(false, $"Overlaps {collider.name}.");
            }

            return new HousePlacementValidationResult(true, "Valid");
        }
    }

}
