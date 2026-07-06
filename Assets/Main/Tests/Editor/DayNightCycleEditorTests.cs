using System.Collections.Generic;
using Neighbor.Main.Features.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Neighbor.Main.Tests
{
    public sealed class DayNightCycleEditorTests
    {
        [Test]
        public void DayNightCycle_SetTimeOfDay_DrivesLightingFogAndPhase()
        {
            RenderSettingsSnapshot renderSettings = RenderSettingsSnapshot.Capture();
            GameObject cycleObject = new("DayNightCycle Test");
            GameObject sunObject = new("Sun Test");
            GameObject moonObject = new("Moon Test");
            List<DayNightPhase> phases = new();

            try
            {
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                Light moon = moonObject.AddComponent<Light>();
                moon.type = LightType.Directional;

                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
                cycle.SetLights(sun, moon);
                cycle.PhaseChanged += phases.Add;

                cycle.SetTimeOfDay(0.5f);

                Assert.That(cycle.CurrentPhase, Is.EqualTo(DayNightPhase.Day));
                Assert.That(cycle.IsDaytime, Is.True);
                Assert.That(RenderSettings.sun, Is.SameAs(sun));
                Assert.That(RenderSettings.fog, Is.True);
                Assert.That(RenderSettings.fogDensity, Is.GreaterThan(0f));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
                Assert.That(sun.enabled, Is.True);
                Assert.That(sun.intensity, Is.GreaterThan(0.8f));
                Assert.That(moon.enabled, Is.False);

                cycle.SetTimeOfDay(0.9f);

                Assert.That(cycle.CurrentPhase, Is.EqualTo(DayNightPhase.Night));
                Assert.That(cycle.IsDaytime, Is.False);
                Assert.That(moon.enabled, Is.True);
                Assert.That(moon.intensity, Is.GreaterThan(0.05f));
                Assert.That(sun.enabled, Is.False);
                Assert.That(phases, Does.Contain(DayNightPhase.Night));
            }
            finally
            {
                renderSettings.Restore();
                Object.DestroyImmediate(cycleObject);
                Object.DestroyImmediate(sunObject);
                Object.DestroyImmediate(moonObject);
            }
        }

        [Test]
        public void DayNightCycle_TimeAndLengthInputsStaySafe()
        {
            GameObject cycleObject = new("DayNightCycle Safety Test");

            try
            {
                DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();

                cycle.SetTimeOfDay(1.25f);
                cycle.SetDayLengthMinutes(-5f);

                Assert.That(cycle.TimeOfDay, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(cycle.DayLengthMinutes, Is.GreaterThan(0f));
                Assert.That(cycle.GetPhase(1.25f), Is.EqualTo(DayNightPhase.Dawn));
            }
            finally
            {
                Object.DestroyImmediate(cycleObject);
            }
        }

        private readonly struct RenderSettingsSnapshot
        {
            private readonly bool fog;
            private readonly Color fogColor;
            private readonly float fogDensity;
            private readonly AmbientMode ambientMode;
            private readonly Color ambientLight;
            private readonly Light sun;
            private readonly Material skybox;

            private RenderSettingsSnapshot(
                bool fog,
                Color fogColor,
                float fogDensity,
                AmbientMode ambientMode,
                Color ambientLight,
                Light sun,
                Material skybox)
            {
                this.fog = fog;
                this.fogColor = fogColor;
                this.fogDensity = fogDensity;
                this.ambientMode = ambientMode;
                this.ambientLight = ambientLight;
                this.sun = sun;
                this.skybox = skybox;
            }

            public static RenderSettingsSnapshot Capture()
            {
                return new RenderSettingsSnapshot(
                    RenderSettings.fog,
                    RenderSettings.fogColor,
                    RenderSettings.fogDensity,
                    RenderSettings.ambientMode,
                    RenderSettings.ambientLight,
                    RenderSettings.sun,
                    RenderSettings.skybox);
            }

            public void Restore()
            {
                RenderSettings.fog = fog;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.sun = sun;
                RenderSettings.skybox = skybox;
            }
        }
    }
}
