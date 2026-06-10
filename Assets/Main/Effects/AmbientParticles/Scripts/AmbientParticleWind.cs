using UnityEngine;

public sealed class AmbientParticleWind : MonoBehaviour
{
    [SerializeField] private Vector3 direction = new Vector3(1f, 0f, 0.25f);
    [SerializeField, Min(0f)] private float strength = 0.7f;
    [SerializeField, Min(0f)] private float gustStrength = 1.1f;
    [SerializeField, Min(0.01f)] private float gustFrequency = 0.18f;
    [SerializeField] private ParticleSystem[] affectedSystems;

    private void Reset()
    {
        RefreshParticleSystems();
    }

    private void OnEnable()
    {
        if (affectedSystems == null || affectedSystems.Length == 0)
            RefreshParticleSystems();
    }

    private void LateUpdate()
    {
        Vector3 windDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
        float gust = Mathf.PerlinNoise(Time.time * gustFrequency, 0.37f) * gustStrength;
        Vector3 force = windDirection * (strength + gust);

        for (int i = 0; i < affectedSystems.Length; i++)
        {
            ParticleSystem system = affectedSystems[i];
            if (system == null)
                continue;

            ParticleSystem.ForceOverLifetimeModule forceModule = system.forceOverLifetime;
            forceModule.enabled = true;
            forceModule.space = ParticleSystemSimulationSpace.World;
            forceModule.x = force.x;
            forceModule.y = force.y;
            forceModule.z = force.z;
        }
    }

    [ContextMenu("Refresh Particle Systems")]
    public void RefreshParticleSystems()
    {
        affectedSystems = GetComponentsInChildren<ParticleSystem>(true);
    }
}
