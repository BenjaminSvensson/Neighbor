using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Neighbor/House Builder/Material Controller")]
    public sealed class HouseBuilderMaterialController : MonoBehaviour
    {
        [SerializeField] private List<HouseMaterialBinding> bindings = new();

        public IReadOnlyList<HouseMaterialBinding> Bindings => bindings;

        public void SetBinding(HouseFaceRole role, string rendererPath, int materialIndex, string materialId)
        {
            rendererPath ??= string.Empty;
            int index = bindings.FindIndex(binding =>
                binding.FaceRole == role
                && binding.RendererPath == rendererPath
                && binding.MaterialIndex == materialIndex);

            HouseMaterialBinding replacement = new(role, rendererPath, materialIndex, materialId);
            if (index >= 0)
            {
                bindings[index] = replacement;
            }
            else
            {
                bindings.Add(replacement);
            }
        }

        public void SetBindings(IEnumerable<HouseMaterialBinding> values)
        {
            bindings.Clear();
            if (values != null)
            {
                bindings.AddRange(values);
            }
        }

        public void Apply(HouseBuilderCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                HouseMaterialBinding binding = bindings[i];
                if (binding == null || !catalog.TryGetMaterial(binding.MaterialId, out Material material))
                {
                    continue;
                }

                Transform target = string.IsNullOrEmpty(binding.RendererPath)
                    ? transform
                    : transform.Find(binding.RendererPath);
                Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
                if (renderer != null
                    && !renderer.enabled
                    && string.IsNullOrEmpty(binding.RendererPath)
                    && transform.Find(HouseGeometryObject.PhysicalObjectName) is Transform physical)
                {
                    renderer = physical.GetComponent<Renderer>();
                }

                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (binding.MaterialIndex >= materials.Length)
                {
                    Array.Resize(ref materials, binding.MaterialIndex + 1);
                }

                materials[binding.MaterialIndex] = material;
                renderer.sharedMaterials = materials;
            }
        }

        public void ApplyFromWorld()
        {
            HouseBuilderWorld world = GetComponentInParent<HouseBuilderWorld>();
            if (world != null)
            {
                Apply(world.Catalog);
            }
        }

        private void OnEnable() => ApplyFromWorld();
        private void Start() => ApplyFromWorld();
    }
}
