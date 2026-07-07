using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class DoorKey : MonoBehaviour, IInteractionTooltipProvider
    {
        [SerializeField] private string keyId = "test_key";
        [SerializeField] private string displayName;

        public string KeyId => keyId;
        public string DisplayName => GetDisplayName(keyId, displayName, "Key");

        public bool Opens(Door door)
        {
            return door != null && !string.IsNullOrWhiteSpace(keyId) && keyId == door.RequiredKeyId;
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;
            if (context != InteractionTooltipContext.FocusedInteractable)
            {
                return false;
            }

            actionText = $"Pick up {DisplayName}";
            keyText = "E";
            return true;
        }

        public static string GetDisplayName(string keyId, string displayNameOverride = null, string fallback = "")
        {
            if (!string.IsNullOrWhiteSpace(displayNameOverride))
            {
                return displayNameOverride.Trim();
            }

            string formattedName = FormatKeyIdForDisplay(keyId);
            return !string.IsNullOrWhiteSpace(formattedName) ? formattedName : fallback;
        }

        public static string FormatKeyIdForDisplay(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                return string.Empty;
            }

            string[] words = keyId.Trim()
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                string lower = words[i].ToLowerInvariant();
                words[i] = lower.Length > 1
                    ? char.ToUpperInvariant(lower[0]) + lower.Substring(1)
                    : lower.ToUpperInvariant();
            }

            return string.Join(" ", words);
        }
    }
}
