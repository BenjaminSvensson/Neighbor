using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    public sealed class PlayerHidingState : MonoBehaviour
    {
        public bool IsHidden { get; private set; }

        public void SetHidden(bool hidden)
        {
            IsHidden = hidden;
        }
    }
}
