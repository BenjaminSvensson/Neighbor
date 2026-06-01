using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class SprayRevealTarget : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color revealColor = new Color(0.1f, 0.85f, 1f, 0.95f);

        private MaterialPropertyBlock propertyBlock;
        private float revealedUntilTime;

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void Update()
        {
            if (revealedUntilTime > 0f && Time.time >= revealedUntilTime)
            {
                revealedUntilTime = 0f;
            }
        }

        public void RevealFor(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            revealedUntilTime = Mathf.Max(revealedUntilTime, Time.time + duration);
            ApplyReveal();
        }

        private void ApplyReveal()
        {
            if (renderers == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.enabled = true;
                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", revealColor);
                propertyBlock.SetColor("_Color", revealColor);
                propertyBlock.SetColor("_EmissionColor", revealColor * 0.45f);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
