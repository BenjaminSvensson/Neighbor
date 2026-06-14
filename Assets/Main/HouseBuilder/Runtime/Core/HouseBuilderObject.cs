using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    public interface IHouseBuilderSerializable
    {
        string HouseBuilderTypeId { get; }
        string CaptureHouseBuilderState();
        void RestoreHouseBuilderState(string json);
    }

    [Serializable]
    public sealed class HouseBuilderProperty
    {
        [SerializeField] private string key;
        [SerializeField] private string value;

        public string Key => key;
        public string Value => value;

        public HouseBuilderProperty(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Neighbor/House Builder/Builder Object")]
    public sealed class HouseBuilderObject : MonoBehaviour
    {
        [SerializeField] private string instanceId;
        [SerializeField] private string definitionId;
        [SerializeField] private string categoryId = HouseBuilderCategories.Prop;
        [SerializeField] private List<HouseBuilderProperty> properties = new();

        public string InstanceId => instanceId;
        public string DefinitionId => definitionId;
        public string CategoryId => categoryId;
        public IReadOnlyList<HouseBuilderProperty> Properties => properties;

        public void Initialize(string sourceDefinitionId, string sourceCategoryId, string requestedInstanceId = null)
        {
            definitionId = sourceDefinitionId ?? string.Empty;
            categoryId = string.IsNullOrWhiteSpace(sourceCategoryId) ? HouseBuilderCategories.Prop : sourceCategoryId;
            instanceId = string.IsNullOrWhiteSpace(requestedInstanceId) ? Guid.NewGuid().ToString("N") : requestedInstanceId;
        }

        public void EnsureIdentity()
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                instanceId = Guid.NewGuid().ToString("N");
            }
        }

        public void SetProperties(IEnumerable<HouseBuilderProperty> values)
        {
            properties.Clear();
            if (values != null)
            {
                properties.AddRange(values);
            }
        }

        private void Awake()
        {
            EnsureIdentity();
        }

        private void OnValidate()
        {
            EnsureIdentity();
            if (string.IsNullOrWhiteSpace(categoryId))
            {
                categoryId = HouseBuilderCategories.Prop;
            }
        }
    }
}
