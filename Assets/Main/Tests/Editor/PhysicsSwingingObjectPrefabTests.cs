using System.Linq;
using Neighbor.Main.Features.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.Tests
{
    public sealed class PhysicsSwingingObjectPrefabTests
    {
        private const string PrefabPath =
            "Assets/Main/Features/Interaction/Items/SwingingAxes/Prefabs/PhysicsSwingingObjectPlaceholder.prefab";

        [Test]
        public void PhysicsSwingingObjectPrefab_UsesDynamicHingePhysics()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab, Is.Not.Null);

            PhysicsSwingingObject swingingObject = prefab.GetComponentInChildren<PhysicsSwingingObject>(true);
            Assert.That(swingingObject, Is.Not.Null);

            Rigidbody body = swingingObject.GetComponent<Rigidbody>();
            HingeJoint hinge = swingingObject.GetComponent<HingeJoint>();

            Assert.That(body, Is.Not.Null);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));
            Assert.That(hinge, Is.Not.Null);
            Assert.That(hinge.useLimits, Is.True);
            Assert.That(hinge.axis.normalized, Is.EqualTo(Vector3.forward));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Any(collider => !collider.isTrigger), Is.True);
        }
    }
}
