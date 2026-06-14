using System;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    public enum HouseFaceRole
    {
        Default = 0,
        Interior = 1,
        Exterior = 2,
        Top = 3,
        Underside = 4,
        Left = 5,
        Right = 6,
        Front = 7,
        Back = 8,
        Trim = 9
    }

    [Serializable]
    public sealed class HouseMaterialBinding
    {
        [SerializeField] private HouseFaceRole faceRole;
        [SerializeField] private string rendererPath;
        [SerializeField, Min(0)] private int materialIndex;
        [SerializeField] private string materialId;

        public HouseFaceRole FaceRole => faceRole;
        public string RendererPath => rendererPath;
        public int MaterialIndex => materialIndex;
        public string MaterialId => materialId;

        public HouseMaterialBinding(HouseFaceRole faceRole, string rendererPath, int materialIndex, string materialId)
        {
            this.faceRole = faceRole;
            this.rendererPath = rendererPath ?? string.Empty;
            this.materialIndex = Mathf.Max(0, materialIndex);
            this.materialId = materialId ?? string.Empty;
        }
    }

}
