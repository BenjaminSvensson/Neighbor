using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class ReadableBook : MonoBehaviour, IPrimaryUseInteractable
    {
        [SerializeField] private string title = "Placeholder Book";
        [SerializeField, TextArea(3, 8)] private string[] pages =
        {
            "Page 1\n\nThis is placeholder readable text. Use this book to test table or desk readability.",
            "Page 2\n\nThe overlay supports flipping forward and backward without needing a scene UI prefab.",
            "Page 3\n\nReplace these serialized page strings with clue text, notes, diary entries, or instructions."
        };

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return interactor != null
                && interactor.HeldPickup != null
                && interactor.HeldPickup.GetComponentInChildren<ReadableBook>() == this
                && !BookReaderOverlay.IsOpen
                && !NotebookWriterOverlay.IsOpen;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            BookReaderOverlay.Open(title, pages);
        }
    }
}
