using Neighbor.Main.Features.Audio;
using UnityEngine;

public sealed class AmbientParticleWind : MonoBehaviour
{
    private const float ManagerSearchInterval = 1f;

    [SerializeField] private Vector3 direction = new Vector3(1f, 0f, 0.25f);
    [SerializeField, Min(0f)] private float strength = 0.7f;
    [SerializeField, Min(0f)] private float gustStrength = 1.1f;
    [SerializeField, Min(0.01f)] private float gustFrequency = 0.18f;
    [Header("Audio Zone Visibility")]
    [SerializeField] private bool showOnlyInOutsideZones = true;
    [SerializeField] private AmbienceManager ambienceManager;
    [SerializeField] private bool clearParticlesWhenHidden = true;
    [SerializeField] private ParticleSystem[] affectedSystems;

    private bool particlesVisible = true;
    private float nextManagerSearchTime;

    private void Reset()
    {
        RefreshParticleSystems();
    }

    private void OnEnable()
    {
        if (affectedSystems == null || affectedSystems.Length == 0)
        {
            RefreshParticleSystems();
        }

        ApplyAudioZoneVisibility(true);
    }

    private void LateUpdate()
    {
        if (!ApplyAudioZoneVisibility(false))
        {
            return;
        }

        if (affectedSystems == null)
        {
            return;
        }

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

    private bool ApplyAudioZoneVisibility(bool force)
    {
        bool shouldShow = true;
        if (showOnlyInOutsideZones)
        {
            ResolveAmbienceManager();
            shouldShow = ambienceManager == null || ambienceManager.CurrentZoneLocation == AmbienceZoneLocation.Outside;
        }

        SetParticleSystemsVisible(shouldShow, force);
        return shouldShow;
    }

    private void ResolveAmbienceManager()
    {
        if (ambienceManager != null || Time.unscaledTime < nextManagerSearchTime)
        {
            return;
        }

        ambienceManager = FindAnyObjectByType<AmbienceManager>();
        nextManagerSearchTime = Time.unscaledTime + ManagerSearchInterval;
    }

    private void SetParticleSystemsVisible(bool visible, bool force)
    {
        if (!force && particlesVisible == visible)
        {
            return;
        }

        particlesVisible = visible;
        if (affectedSystems == null)
        {
            return;
        }

        ParticleSystemStopBehavior stopBehavior = clearParticlesWhenHidden
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        for (int i = 0; i < affectedSystems.Length; i++)
        {
            ParticleSystem system = affectedSystems[i];
            if (system == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = visible;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
            }

            if (visible)
            {
                if (!system.isPlaying)
                {
                    system.Play(false);
                }

                continue;
            }

            system.Stop(false, stopBehavior);
        }
    }
}
