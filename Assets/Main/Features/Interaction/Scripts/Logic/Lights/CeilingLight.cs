using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class CeilingLight : MonoBehaviour
    {
        private static readonly Dictionary<string, List<CeilingLight>> LightsByCircuit = new();

        [SerializeField] private string circuitId = "default_room";
        [SerializeField] private Light controlledLight;
        [SerializeField] private Renderer bulbRenderer;
        [SerializeField] private bool startsOn = true;
        [SerializeField] private Color offColor = new Color(0.18f, 0.17f, 0.14f, 1f);
        [SerializeField] private Color onColor = new Color(1f, 0.92f, 0.62f, 1f);
        [SerializeField] private Color emissionColor = new Color(1f, 0.78f, 0.32f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private bool isOn;

        public string CircuitId => circuitId;
        public bool IsOn => isOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCircuitLights()
        {
            LightsByCircuit.Clear();
        }

        private void Awake()
        {
            if (controlledLight == null)
            {
                controlledLight = GetComponentInChildren<Light>(true);
            }

            if (bulbRenderer == null)
            {
                bulbRenderer = GetComponentInChildren<Renderer>();
            }

            SetOn(startsOn);
        }

        private void OnEnable()
        {
            Register(this);
        }

        private void OnDisable()
        {
            Unregister(this);
        }

        public void Toggle()
        {
            SetOn(!isOn);
        }

        public void SetOn(bool on)
        {
            isOn = on;

            if (controlledLight != null)
            {
                controlledLight.enabled = isOn;
            }

            ApplyBulbState();
        }

        public static IReadOnlyList<CeilingLight> GetCircuitLights(string circuit)
        {
            string key = NormalizeCircuit(circuit);
            return LightsByCircuit.TryGetValue(key, out List<CeilingLight> lights)
                ? lights
                : System.Array.Empty<CeilingLight>();
        }

        private void ApplyBulbState()
        {
            if (bulbRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            bulbRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", isOn ? onColor : offColor);
            propertyBlock.SetColor("_Color", isOn ? onColor : offColor);
            propertyBlock.SetColor("_EmissionColor", isOn ? emissionColor : Color.black);
            bulbRenderer.SetPropertyBlock(propertyBlock);
        }

        private static void Register(CeilingLight ceilingLight)
        {
            if (ceilingLight == null)
            {
                return;
            }

            string key = NormalizeCircuit(ceilingLight.circuitId);
            if (!LightsByCircuit.TryGetValue(key, out List<CeilingLight> lights))
            {
                lights = new List<CeilingLight>();
                LightsByCircuit.Add(key, lights);
            }

            if (!lights.Contains(ceilingLight))
            {
                lights.Add(ceilingLight);
            }
        }

        private static void Unregister(CeilingLight ceilingLight)
        {
            if (ceilingLight == null)
            {
                return;
            }

            string key = NormalizeCircuit(ceilingLight.circuitId);
            if (!LightsByCircuit.TryGetValue(key, out List<CeilingLight> lights))
            {
                return;
            }

            lights.Remove(ceilingLight);
            if (lights.Count == 0)
            {
                LightsByCircuit.Remove(key);
            }
        }

        private static string NormalizeCircuit(string circuit)
        {
            return string.IsNullOrWhiteSpace(circuit) ? "default_room" : circuit.Trim();
        }
    }
}
