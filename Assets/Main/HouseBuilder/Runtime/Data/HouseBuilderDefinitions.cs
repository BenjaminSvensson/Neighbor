using System;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    public static class HouseBuilderCategories
    {
        public const string Structure = "structure";
        public const string Wall = "wall";
        public const string Floor = "floor";
        public const string Ceiling = "ceiling";
        public const string Door = "door";
        public const string Window = "window";
        public const string Furniture = "furniture";
        public const string Prop = "prop";
        public const string SearchSpot = "ai.search_spot";
        public const string TaskSpot = "ai.task_spot";
        public const string PatrolPoint = "ai.patrol_point";
        public const string ReinforcementTrigger = "ai.reinforcement_trigger";
        public const string Reinforcement = "ai.reinforcement";
        public const string ReinforcementLocation = "ai.reinforcement_location";
        public const string NeighborSpawnPoint = "ai.neighbor_spawn_point";
        public const string Wiring = "wiring";
    }

    [Flags]
    public enum HouseSurfaceType
    {
        None = 0,
        Ground = 1 << 0,
        Wall = 1 << 1,
        Ceiling = 1 << 2,
        Any = ~0
    }

    public enum HouseSurfaceAlignment
    {
        None,
        UpToNormal,
        ForwardToNormal,
        RightToNormal
    }

    [Serializable]
    public sealed class HousePlacementProfile
    {
        [SerializeField] private HouseSurfaceType allowedSurfaces = HouseSurfaceType.Any;
        [SerializeField] private HouseSurfaceAlignment surfaceAlignment;
        [SerializeField] private bool requireSurface;
        [SerializeField] private bool groundOnWall;
        [SerializeField] private bool snapBoundsToFeatures;
        [SerializeField] private bool groundOnFeatureSnaps;
        [SerializeField] private bool validateCollisions = true;
        [SerializeField] private bool allowTriggerOverlap = true;
        [SerializeField] private Vector3 boundsSize = Vector3.one;
        [SerializeField] private Vector3 boundsCenter;
        [SerializeField] private Vector3 placementOffset;

        public HouseSurfaceType AllowedSurfaces => allowedSurfaces;
        public HouseSurfaceAlignment SurfaceAlignment => surfaceAlignment;
        public bool RequireSurface => requireSurface;
        public bool GroundOnWall => groundOnWall;
        public bool SnapBoundsToFeatures => snapBoundsToFeatures;
        public bool GroundOnFeatureSnaps => groundOnFeatureSnaps;
        public bool ValidateCollisions => validateCollisions;
        public bool AllowTriggerOverlap => allowTriggerOverlap;
        public Vector3 BoundsSize => boundsSize;
        public Vector3 BoundsCenter => boundsCenter;
        public Vector3 PlacementOffset => placementOffset;
    }

    [Serializable]
    public sealed class HouseWallOpeningProfile
    {
        [SerializeField] private bool enabled;
        [SerializeField] private Vector3 size = new(1.2f, 2.1f, 0.5f);
        [SerializeField] private Vector3 center = new(0f, 1.05f, 0f);
        [SerializeField, Min(0f)] private float margin = 0.02f;
        [SerializeField] private bool centerPlacedObjectInWall;

        public bool Enabled => enabled;
        public Vector3 Size => size;
        public Vector3 Center => center;
        public float Margin => margin;
        public bool CenterPlacedObjectInWall => centerPlacedObjectInWall;

        public HouseWallOpeningProfile()
        {
        }

        public HouseWallOpeningProfile(
            Vector3 size,
            Vector3 center,
            float margin = 0.02f,
            bool enabled = true,
            bool centerPlacedObjectInWall = false)
        {
            this.enabled = enabled;
            this.size = size;
            this.center = center;
            this.margin = Mathf.Max(0f, margin);
            this.centerPlacedObjectInWall = centerPlacedObjectInWall;
        }
    }

    [Serializable]
    public sealed class HouseWirePortTemplate
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName = "Port";
        [SerializeField] private HouseWirePortDirection direction;
        [SerializeField] private HouseSignalKind signalKind = HouseSignalKind.Any;
        [SerializeField, Min(0)] private int maximumConnections;
        [SerializeField] private Vector3 visualOffset;

        public string Id => id;
        public string DisplayName => displayName;
        public HouseWirePortDirection Direction => direction;
        public HouseSignalKind SignalKind => signalKind;
        public int MaximumConnections => maximumConnections;
        public Vector3 VisualOffset => visualOffset;
    }

}
