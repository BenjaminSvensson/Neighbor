using UnityEngine;

namespace Neighbor.Main.Features.Environment
{
    public sealed class AtmosphereDressingAnchor : MonoBehaviour
    {
        public enum DressingKind
        {
            DirtyDecal,
            PropDressing
        }

        [SerializeField] private DressingKind kind = DressingKind.PropDressing;
        [SerializeField, Range(0f, 1f)] private float intensity = 0.6f;

        public DressingKind Kind => kind;
        public float Intensity => intensity;

        public void Configure(DressingKind anchorKind, float anchorIntensity)
        {
            kind = anchorKind;
            intensity = Mathf.Clamp01(anchorIntensity);
        }
    }
}
