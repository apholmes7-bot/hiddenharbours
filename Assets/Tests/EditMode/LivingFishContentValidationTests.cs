using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    public class LivingFishContentValidationTests
    {
        [Test]
        public void BaselineSpecies_DoNotOptIntoAnUnwiredField()
        {
            string[] guids = AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data/Fish" });
            Assert.IsNotEmpty(guids);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var species = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                Assert.IsFalse(species.LivingBehaviour.Enabled, path + ": field activation needs the habitat and consumer integration slice.");
            }
        }
    }
}
