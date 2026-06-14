#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder.Editor
{
    public enum HouseBooleanOperation
    {
        Subtract,
        Intersect,
        Union
    }

    public static class HouseBuilderBooleanUtility
    {
        public static GameObject Perform(GameObject left, GameObject right, HouseBooleanOperation operation)
        {
            ValidateOperand(left, nameof(left));
            ValidateOperand(right, nameof(right));

            Type csgType = Type.GetType("UnityEngine.ProBuilder.Csg.CSG, Unity.ProBuilder");
            if (csgType == null)
            {
                throw new InvalidOperationException("The ProBuilder CSG implementation is unavailable.");
            }

            string methodName = operation switch
            {
                HouseBooleanOperation.Subtract => "Subtract",
                HouseBooleanOperation.Intersect => "Intersect",
                _ => "Union"
            };

            MethodInfo operationMethod = csgType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            object model = operationMethod?.Invoke(null, new object[] { left, right });
            if (model == null)
            {
                throw new InvalidOperationException($"ProBuilder CSG {methodName} did not produce geometry.");
            }

            Type modelType = model.GetType();
            Mesh mesh = modelType.GetProperty("mesh", BindingFlags.Instance | BindingFlags.Public)?.GetValue(model) as Mesh;
            IEnumerable materialEnumerable = modelType.GetProperty("materials", BindingFlags.Instance | BindingFlags.Public)?.GetValue(model) as IEnumerable;
            Material[] materials = materialEnumerable?.Cast<Material>().ToArray() ?? Array.Empty<Material>();
            if (mesh == null)
            {
                throw new InvalidOperationException("ProBuilder CSG returned an invalid mesh.");
            }

            HouseGeometryDescriptor descriptor = new(
                HouseGeometryKind.BakedBoolean,
                mesh.bounds.size,
                bakedMesh: HouseMeshData.Capture(mesh));
            GameObject result = HouseGeometryFactory.Create(descriptor);
            result.name = $"Boolean_{methodName}";
            result.transform.position = Vector3.zero;
            result.GetComponent<MeshRenderer>().sharedMaterials = materials;
            result.GetComponent<HouseGeometryObject>().BakeCurrentMesh(HouseGeometryKind.BakedBoolean);
            Undo.RegisterCreatedObjectUndo(result, $"House Builder {methodName}");
            return result;
        }

        private static void ValidateOperand(GameObject operand, string argumentName)
        {
            if (operand == null || operand.GetComponent<MeshFilter>()?.sharedMesh == null)
            {
                throw new ArgumentException("Boolean operands must have a MeshFilter with a mesh.", argumentName);
            }
        }
    }
}
#endif
