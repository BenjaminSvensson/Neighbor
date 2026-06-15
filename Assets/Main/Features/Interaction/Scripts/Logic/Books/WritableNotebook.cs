using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class WritableNotebook : MonoBehaviour, IPrimaryUseInteractable
    {
        [SerializeField] private string title = "Placeholder Notebook";
        [SerializeField, TextArea(3, 8)] private string[] pages =
        {
            "",
            "",
            ""
        };
        private ItemAudioFeedback audioFeedback;

        private void Awake()
        {
            audioFeedback = ItemAudioFeedback.Resolve(gameObject);
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return interactor != null
                && interactor.HeldPickup != null
                && interactor.HeldPickup.GetComponentInChildren<WritableNotebook>() == this
                && !BookReaderOverlay.IsOpen
                && !NotebookWriterOverlay.IsOpen;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            audioFeedback?.Play(ItemSoundProfile.BookOpen, 0.35f);
            NotebookWriterOverlay.Open(title, pages, SavePage);
        }

        private void SavePage(int pageIndex, string pageText)
        {
            if (pages == null || pageIndex < 0 || pageIndex >= pages.Length)
            {
                return;
            }

            pages[pageIndex] = pageText ?? string.Empty;
        }
    }
}
