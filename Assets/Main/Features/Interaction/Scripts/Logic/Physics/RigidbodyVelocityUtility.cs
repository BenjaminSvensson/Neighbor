using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    internal static class RigidbodyVelocityUtility
    {
        public static void ClearIfDynamic(Rigidbody body)
        {
            SetIfDynamic(body, Vector3.zero, Vector3.zero);
        }

        public static void SetLinearIfDynamic(Rigidbody body, Vector3 linearVelocity)
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            body.linearVelocity = linearVelocity;
        }

        public static void SetIfDynamic(Rigidbody body, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;
        }
    }
}
