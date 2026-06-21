namespace Neighbor.Main.Features.Interaction
{
    using System.Collections.Generic;

    public static class InteractionOverlayState
    {
        private static readonly HashSet<object> ExternalGameplayInputBlockers = new();

        public static bool IsGameplayInputBlocked =>
            BookReaderOverlay.IsOpen || NotebookWriterOverlay.IsOpen || ExternalGameplayInputBlockers.Count > 0;

        public static void SetExternalGameplayInputBlocked(object blocker, bool blocked)
        {
            if (blocker == null)
            {
                return;
            }

            if (blocked)
            {
                ExternalGameplayInputBlockers.Add(blocker);
                return;
            }

            ExternalGameplayInputBlockers.Remove(blocker);
        }
    }
}
