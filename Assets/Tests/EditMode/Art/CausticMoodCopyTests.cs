using System.Reflection;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class CausticMoodCopyTests
    {
        [TestCase("Water")]
        [TestCase("Water_DeepBlue")]
        [TestCase("Water_FoggySmother")]
        [TestCase("Water_GlassyCalm")]
        [TestCase("Water_NorthAtlantic")]
        [TestCase("Water_StirredBrown")]
        [TestCase("Water_StormGrey")]
        [TestCase("Water_Tropical")]
        [TestCase("Water_WarmShelter")]
        public void CausticClarity_PresetApplyRestoresOne(string name)
        {
            string path = "Assets/_Project/Art/Materials/" +
                          (name == "Water" ? "" : "WaterPresets/") + name + ".mat";
            var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(asset, Is.Not.Null, path);
            Assert.That(asset.HasProperty("_CausticClarity"), Is.True);
            Assert.That(asset.GetFloat("_CausticClarity"), Is.EqualTo(1f));
            var scratch = new Material(asset);
            try
            {
                scratch.SetFloat("_CausticClarity", 0f);
                Assert.That(scratch.GetFloat("_CausticClarity"), Is.EqualTo(0f));
                scratch.CopyPropertiesFromMaterial(asset);
                Assert.That(scratch.GetFloat("_CausticClarity"), Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(scratch);
            }
        }

        [Test]
        public void CausticClarity_IsOwnerPolicyOutsideMoodCopy()
        {
            var field = typeof(WaterSurface).GetField("MoodFloatNames",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var names = (string[])field.GetValue(null);
            Assert.That(names, Does.Not.Contain("_CausticClarity"));
            Assert.That(names, Does.Contain("_Turbidity"));
        }

        [Test]
        public void CausticActivation_WeatherBlendCarriesAllCurvatureDials()
        {
            var live = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Water.mat");
            Assert.That(live, Is.Not.Null);
            var baseCopy = new Material(live);
            var stormCopy = new Material(live);
            GameObject sea = null;
            try
            {
                foreach (string name in new[] { "Water", "Water_DeepBlue", "Water_FoggySmother",
                    "Water_GlassyCalm", "Water_NorthAtlantic", "Water_StirredBrown", "Water_StormGrey",
                    "Water_Tropical", "Water_WarmShelter" })
                {
                    string path = "Assets/_Project/Art/Materials/" +
                        (name == "Water" ? "" : "WaterPresets/") + name + ".mat";
                    var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                    Assert.That(asset, Is.Not.Null, path);
                    baseCopy.CopyPropertiesFromMaterial(asset);
                    Assert.That(baseCopy.GetFloat("_CausticCurvatureBlend"), Is.EqualTo(0.5f), name);
                    Assert.That(baseCopy.GetFloat("_CausticCurvatureStep"), Is.EqualTo(0.5f), name);
                    Assert.That(baseCopy.GetFloat("_CausticCurvatureGain"), Is.EqualTo(12f), name);
                    Assert.That(baseCopy.GetFloat("_CausticDayGate"), Is.EqualTo(1f), name);
                }
                // Distinct anchors prove delivery, not just equal defaults on all materials.
                baseCopy.SetFloat("_CausticCurvatureBlend", 0.25f);
                baseCopy.SetFloat("_CausticCurvatureStep", 0.5f);
                baseCopy.SetFloat("_CausticCurvatureGain", 8f);
                baseCopy.SetFloat("_CausticDayGate", 0f);
                stormCopy.SetFloat("_CausticCurvatureBlend", 0.75f);
                stormCopy.SetFloat("_CausticCurvatureStep", 1f);
                stormCopy.SetFloat("_CausticCurvatureGain", 24f);
                stormCopy.SetFloat("_CausticDayGate", 1f);
                sea = new GameObject("CausticMoodCopyTest") { hideFlags = HideFlags.HideAndDontSave };
                sea.SetActive(false); // No scene bake, registration, or persistent asset mutation.
                sea.AddComponent<MeshRenderer>().sharedMaterial = baseCopy;
                var surface = sea.AddComponent<WaterSurface>();
                surface.ConfigureWeatherPalette(true, baseCopy, baseCopy, stormCopy, baseCopy);
                var blend = typeof(WaterSurface).GetMethod("BlendMoodProps",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(blend, Is.Not.Null);
                var block = new MaterialPropertyBlock();
                block.SetFloat("_WaterLevel", -1.25f);
                blend.Invoke(surface, new object[] { new[] { 0.25f, 0f, 0.75f, 0f }, block });
                Assert.That(block.GetFloat("_CausticCurvatureBlend"), Is.EqualTo(0.625f));
                Assert.That(block.GetFloat("_CausticCurvatureStep"), Is.EqualTo(0.875f));
                Assert.That(block.GetFloat("_CausticCurvatureGain"), Is.EqualTo(20f));
                Assert.That(block.GetFloat("_CausticDayGate"), Is.EqualTo(0.75f));
                Assert.That(block.GetFloat("_WaterLevel"), Is.EqualTo(-1.25f));
            }
            finally
            {
                if (sea != null) Object.DestroyImmediate(sea);
                Object.DestroyImmediate(baseCopy);
                Object.DestroyImmediate(stormCopy);
            }
        }
    }
}
