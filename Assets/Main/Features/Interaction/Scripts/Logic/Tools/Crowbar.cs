using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class Crowbar : MonoBehaviour, IPrimaryUseInteractable
    {
        [SerializeField, Min(0.1f)] private float pryRange = 3f;
        [SerializeField, Min(0f)] private float pryRadius = 0.18f;
        [SerializeField] private LayerMask pryMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        private readonly RaycastHit[] pryHits = new RaycastHit[8];
        private Pickupable pickupable;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return interactor != null && pickupable != null && pickupable.IsHeld;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            if (interactor == null)
            {
                return;
            }

            Ray ray = new Ray(interactor.ViewTransform.position, interactor.ViewTransform.forward);
            if (!TryFindBoard(ray, out WoodBoardPryTarget board, out RaycastHit hit))
            {
                return;
            }

            Vector3 pryDirection = Vector3.ProjectOnPlane(interactor.ViewTransform.forward, Vector3.up);
            if (pryDirection.sqrMagnitude <= 0.0001f)
            {
                pryDirection = interactor.ViewTransform.forward;
            }

            board.PryLoose(hit.point, pryDirection, gameObject);
        }

        private bool TryFindBoard(Ray ray, out WoodBoardPryTarget board, out RaycastHit bestHit)
        {
            int hitCount = pryRadius > 0f
                ? Physics.SphereCastNonAlloc(ray, pryRadius, pryHits, pryRange, pryMask, triggerInteraction)
                : Physics.RaycastNonAlloc(ray, pryHits, pryRange, pryMask, triggerInteraction);

            board = null;
            bestHit = default;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = pryHits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                WoodBoardPryTarget candidate = hit.collider.GetComponentInParent<WoodBoardPryTarget>();
                if (candidate == null || hit.distance >= bestDistance)
                {
                    continue;
                }

                board = candidate;
                bestHit = hit;
                bestDistance = hit.distance;
            }

            return board != null;
        }
    }
}
