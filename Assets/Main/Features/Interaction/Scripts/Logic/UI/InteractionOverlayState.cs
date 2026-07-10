namespace Neighbor.Main.Features.Interaction
{
    using System.Collections.Generic;
    using UnityEngine;

    public static class InteractionOverlayState
    {
        private static readonly HashSet<object> ExternalGameplayInputBlockers = new();
        private static int gameplayInputConsumedFrame = -1;

        public static bool IsGameplayInputBlocked =>
            BookReaderOverlay.IsOpen
            || NotebookWriterOverlay.IsOpen
            || ExternalGameplayInputBlockers.Count > 0
            || gameplayInputConsumedFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            ExternalGameplayInputBlockers.Clear();
            gameplayInputConsumedFrame = -1;
        }

        public static void ConsumeGameplayInputForCurrentFrame()
        {
            gameplayInputConsumedFrame = Time.frameCount;
        }

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
