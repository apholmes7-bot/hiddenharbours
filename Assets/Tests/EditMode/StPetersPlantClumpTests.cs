using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The clump law on the committed St Peters field: off is the island's scatter exactly as it shipped,
    /// and on it moves only the recipe scatter — the ground payload and the authored plants are read, never
    /// written (the assets are copied before a number is changed).
    /// </summary>
    public sealed class StPetersPlantClumpTests
    {
        [Test]
        public void AtStrengthZero_TheIslandIsItsFieldWithNoLaw_AndTheLawKeepsEveryAuthoredPlant()
        {
            var real = StPetersGrassFieldTests.Definition();
            Assert.IsNotNull(real); Assert.IsNotNull(real.Clump, "the St Peters field binds no clump law");
            var groundData = AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>(StPetersGrassFieldTests.GroundPath);
            Assert.IsNotNull(groundData);
            var definition = Object.Instantiate(real); var law = Object.Instantiate(real.Clump);
            var host = new GameObject("PlantClumpStPeters") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var ground = host.AddComponent<StPetersPlantGround>(); ground.Data = groundData;
                var field = host.AddComponent<PlantField>(); field.Definition = definition; field.GroundSource = ground;

                definition.Clump = null; field.Rebuild(false);
                string shipped = field.EncodeCurrentField(); int shippedCount = field.PlantCount;
                var shippedGrass = new List<Vector2>(); field.CopyGrassRootsTo(shippedGrass);

                law.Strength = 0; definition.Clump = law; field.Rebuild(false);
                Assert.AreEqual(shipped, field.EncodeCurrentField(), "strength 0 moved a St Peters plant");
                Assert.AreEqual(shippedCount, field.PlantCount);
                var grass = new List<Vector2>(); field.CopyGrassRootsTo(grass);
                CollectionAssert.AreEqual(shippedGrass, grass);

                law.Strength = real.Clump.Strength; field.Rebuild(false);
                int authored = definition.Authored.Length;
                int before = shippedCount - authored, after = field.PlantCount - authored;
                TestContext.WriteLine($"St Peters scatter: {before} plants as shipped, {after} under the law; {authored} authored either way");
                Assert.AreEqual(9115, authored);
                Assert.AreNotEqual(shipped, field.EncodeCurrentField(), "the shipped law changed nothing");
                Assert.That(after, Is.InRange(before * 6 / 10, before * 11 / 10),
                    "the law rearranges the island's growth; it does not strip it or flood it");
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(definition); Object.DestroyImmediate(law); }
        }
    }
}
