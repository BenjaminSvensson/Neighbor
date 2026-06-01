namespace Neighbor.Main.Features.Interaction
{
    public static class InteractionOverlayState
    {
        public static bool IsGameplayInputBlocked => BookReaderOverlay.IsOpen || NotebookWriterOverlay.IsOpen;
    }
}
