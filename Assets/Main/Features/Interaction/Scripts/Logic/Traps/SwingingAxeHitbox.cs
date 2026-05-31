using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class SwingingAxeHitbox : MonoBehaviour
    {
        [SerializeField] private SwingingAxeTrap trap;

        private void Awake()
        {
            if (trap == null)
            {
                trap = GetComponentInParent<SwingingAxeTrap>();
            }

            Collider hitbox = GetComponent<Collider>();
            hitbox.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            trap?.HitFromAxe(other);
        }

        private void OnTriggerStay(Collider other)
        {
            trap?.HitFromAxe(other);
        }
    }
}
