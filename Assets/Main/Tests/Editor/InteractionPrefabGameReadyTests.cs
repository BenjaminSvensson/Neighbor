using Neighbor.Main.EditorTools;
using NUnit.Framework;

namespace Neighbor.Main.Tests
{
    public sealed class InteractionPrefabGameReadyTests
    {
        [Test]
        public void InteractionItemPrefabs_AreGameReady()
        {
            var issues = InteractionPrefabGameReadyUtility.ValidateAllPrefabs();

            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }
    }
}
