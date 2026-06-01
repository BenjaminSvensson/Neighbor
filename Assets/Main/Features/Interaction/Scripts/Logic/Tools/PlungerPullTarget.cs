using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class PlungerPullTarget : MonoBehaviour
    {
        [SerializeField] private bool allowPull = true;
        [SerializeField] private bool allowHeavyPull;
        [SerializeField, Min(0f)] private float maximumPullMass = 3f;

        public bool AllowPull => allowPull;
        public bool AllowHeavyPull => allowHeavyPull;
        public float MaximumPullMass => maximumPullMass;

        private void OnValidate()
        {
            maximumPullMass = Mathf.Max(0f, maximumPullMass);
        }
    }
}
