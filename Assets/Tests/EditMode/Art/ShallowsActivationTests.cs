using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// S1: preset copying must keep the bottom enabled, and each surface must keep its own bake.
    /// The committed-region cases verify the granted-slot bakes and named scene bindings.
    /// No Ignore or fallback to a different region.
    /// These tests do not establish visual ground/seabed continuity.
    /// </summary>
    public class ShallowsActivationTests
    {
        private const string LivePath = "Assets/_Project/Art/Materials/Water.mat";
        private const string PresetFolder = "Assets/_Project/Art/Materials/WaterPresets/";
        private const string TerrainFolder = "Assets/_Project/Data/Terrain/";
        private const string SurfaceGuid = "720419e734bb47b19ff0a4ce8281ed56";
        private const int RequestedResolution = 1520;
        private static readonly Vector4 Ratio = new Vector4(1f, 0.18f, 0.08f, 0f);

        [TestCase("Water_DeepBlue", 0.3f)]
        [TestCase("Water_FoggySmother", 1.2f)]
        [TestCase("Water_GlassyCalm", 0.25f)]
        [TestCase("Water_NorthAtlantic", 0.6f)]
        [TestCase("Water_StirredBrown", 3f)]
        [TestCase("Water_StormGrey", 1.6f)]
        [TestCase("Water_Tropical", 0.12f)]
        [TestCase("Water_WarmShelter", 0.5f)]
        public void ApplyingEachPreset_PreservesSeabedActivation(string name, float turbidity)
        {
            Material live = LoadMaterial(LivePath);
            Material preset = LoadMaterial(PresetFolder + name + ".mat");
            AssertActivation(live);
            var copy = new Material(live);
            try
            {
                // Same operation as WaterPresetMenu.ApplyVariant, without modifying an asset.
                copy.CopyPropertiesFromMaterial(preset);
                AssertActivation(copy);
                Assert.That(copy.GetFloat("_Turbidity"), Is.EqualTo(turbidity).Within(1e-6f),
                    "Activation must not retune the preset's optical density.");
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void ReplacingAndClearingOneSurface_DoesNotChangeAnotherRegionsBottom()
        {
            var material = new Material(LoadMaterial(LivePath));
            var firstBake = new Texture2D(2, 2);
            var secondBake = new Texture2D(3, 3);
            GameObject first = null, second = null;
            try
            {
                first = MakeInactiveSea(material);
                second = MakeInactiveSea(material);
                var a = first.GetComponent<WaterSurface>();
                var b = second.GetComponent<WaterSurface>();
                Texture originalMaterialTexture = material.GetTexture("_SeabedTex");
                a.ConfigureSeabedTexture(firstBake);
                b.ConfigureSeabedTexture(secondBake);
                Assert.That(BoundBottom(first.GetComponent<Renderer>()), Is.SameAs(firstBake));
                Assert.That(BoundBottom(second.GetComponent<Renderer>()), Is.SameAs(secondBake));

                var block = new MaterialPropertyBlock();
                first.GetComponent<Renderer>().GetPropertyBlock(block);
                block.SetFloat("_WaterLevel", 1.234f);
                first.GetComponent<Renderer>().SetPropertyBlock(block);
                a.ConfigureSeabedTexture(null);

                Texture fallback = BoundBottom(first.GetComponent<Renderer>());
                Assert.That(fallback, Is.Not.Null);
                Assert.That(fallback.width, Is.EqualTo(1));
                Assert.That(fallback, Is.Not.SameAs(firstBake));
                Assert.That(BoundBottom(second.GetComponent<Renderer>()), Is.SameAs(secondBake));
                first.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat("_WaterLevel"), Is.EqualTo(1.234f),
                    "Changing albedo must not replace the gameplay water-level binding.");
                Assert.That(material.GetTexture("_SeabedTex"), Is.SameAs(originalMaterialTexture));
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
                Object.DestroyImmediate(firstBake);
                Object.DestroyImmediate(secondBake);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void DisplacedChunks_ReceiveSurfaceSeabedTextureAfterReplacementAndClear()
        {
            Assert.That(DisplacedWaterRegistry.Count, Is.Zero, "Use an isolated EditMode fixture.");
            Assert.That(DisplacedSea.IsActive, Is.False, "Do not displace a live scene in this fixture.");
            var material = new Material(LoadMaterial(LivePath));
            var firstBake = new Texture2D(2, 2);
            var secondBake = new Texture2D(3, 3);
            GameObject sea = null;
            DisplacedWaterSurface displaced = null;
            try
            {
                sea = MakeInactiveSea(material);
                var surface = sea.GetComponent<WaterSurface>();
                displaced = sea.AddComponent<DisplacedWaterSurface>();
                // Exercise the real construction/copy path on two small chunks, without Play or a draw.
                // SetDisplaced intentionally does nothing in EditMode; invoke that private path here.
                var serialized = new SerializedObject(displaced);
                serialized.FindProperty("_gridPixels").intValue = 4;
                serialized.FindProperty("_pixelsPerUnit").floatValue = 4f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                displaced.Configure(Vector2.zero,
                    new Vector2(DisplacedWaterMath.MaxChunkCells + 1, 2),
                    LoadMaterial("Assets/_Project/Art/Materials/WaterOverlay.mat"));
                Invoke(displaced, "Awake");
                surface.ConfigureSeabedTexture(firstBake);
                Invoke(displaced, "Activate");
                Transform root = sea.transform.Find("DisplacedWaterMesh");
                Assert.That(root, Is.Not.Null);
                var chunks = root.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(chunks.Length, Is.EqualTo(2));

                foreach (Texture2D texture in new[] { firstBake, secondBake, null })
                {
                    surface.ConfigureSeabedTexture(texture);
                    Invoke(displaced, "SyncUniforms", true);
                    Texture expected = BoundBottom(sea.GetComponent<Renderer>());
                    foreach (MeshRenderer chunk in chunks)
                    {
                        Assert.That(BoundBottom(chunk), Is.SameAs(expected), chunk.name);
                        AssertActivation(chunk.sharedMaterial);
                        Assert.That(chunk.sharedMaterial.GetShaderPassEnabled("Universal2D"), Is.False);
                    }
                }
            }
            finally
            {
                // The parent deliberately never entered an active scene; release explicitly as well
                // as OnDestroy so no owned meshes or published frame survive an assertion failure.
                if (displaced != null) Invoke(displaced, "ReleaseOwned");
                if (sea != null) Object.DestroyImmediate(sea);
                Object.DestroyImmediate(firstBake);
                Object.DestroyImmediate(secondBake);
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("StPeters", "520")]
        [TestCase("NineMileCreek", "560")]
        public void PlayedRegions_HaveTheirOwnSeabedBindingAndUnchangedWorldRect(
            string region, string worldHeight)
        {
            string surface = SceneSurface(region);
            Assert.That(Field(surface, "_heightWorldCenter"), Is.EqualTo("{x: 0, y: 0}"));
            Assert.That(Field(surface, "_heightWorldSize"),
                Is.EqualTo("{x: 760, y: " + worldHeight + "}"));
            string texturePath = TerrainFolder + region + "Seabed_SeabedTex.png";
            Assert.That(File.Exists(texturePath), Is.True,
                "Pending the granted-slot seabed bake: " + texturePath);
            string guid = AssetDatabase.AssetPathToGUID(texturePath);
            Assert.That(guid, Has.Length.EqualTo(32));
            Assert.That(Field(surface, "_seabedTexture"),
                Is.EqualTo("{fileID: 2800000, guid: " + guid + ", type: 3}"),
                "Patch this surface's named YAML field after baking; do not assign Water.mat.");
        }

        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        public void CommittedBakes_PreserveRequestedResolutionAndImportSettings(string region)
        {
            string path = TerrainFolder + region + "Seabed_SeabedTex.png";
            Assert.That(File.Exists(path), Is.True, "The authorized editor-slot bake is still pending.");
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                    reader.ReadBytes(8), "A Git LFS pointer is not a baked texture.");
                stream.Position = 16;
                Assert.That(ReadBigEndianInt(reader), Is.EqualTo(RequestedResolution), "PNG width");
                Assert.That(ReadBigEndianInt(reader), Is.EqualTo(RequestedResolution), "PNG height");
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(texture, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(RequestedResolution), "No silent import downsize.");
            Assert.That(texture.height, Is.EqualTo(RequestedResolution));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.sRGBTexture, Is.True, "Albedo is colour; height is a different input.");
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.False, "Alpha is coverage.");
        }

        private static void AssertActivation(Material material)
        {
            Assert.That(material.IsKeywordEnabled("_USE_SEABEDTEX"), Is.True, material.name);
            Assert.That(material.GetFloat("_UseSeabedTex"), Is.EqualTo(1f), material.name);
            Assert.That(material.GetFloat("_AbsorptionBands"), Is.EqualTo(6f),
                "S1 preserves six bands; S2 needs the owner's separate look ruling.");
            Assert.That(material.GetVector("_AbsorptionRatio"), Is.EqualTo(Ratio));
        }

        private static Material LoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null, path);
            return material;
        }

        private static GameObject MakeInactiveSea(Material material)
        {
            var sea = new GameObject("ShallowsActivationTest") { hideFlags = HideFlags.HideAndDontSave };
            sea.SetActive(false); // No terrain bake, service registration or shared-material edits.
            sea.AddComponent<MeshRenderer>().sharedMaterial = material;
            sea.AddComponent<WaterSurface>();
            return sea;
        }

        private static Texture BoundBottom(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetTexture("_SeabedTex");
        }

        private static void Invoke(DisplacedWaterSurface surface, string name, params object[] args)
        {
            MethodInfo method = typeof(DisplacedWaterSurface).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(surface, args);
        }

        private static string SceneSurface(string region)
        {
            string yaml = File.ReadAllText("Assets/_Project/Scenes/" + region + ".unity");
            string[] candidates = Regex.Split(yaml, @"(?m)^--- !u!")
                .Where(block => block.Contains("guid: " + SurfaceGuid + ",")).ToArray();
            Assert.That(candidates.Length, Is.EqualTo(1), "Identify WaterSurface by script, not line number.");
            return candidates[0];
        }

        private static string Field(string block, string key)
        {
            Match match = Regex.Match(block, @"(?m)^  " + Regex.Escape(key) + @": ([^\r\n]+)");
            Assert.That(match.Success, Is.True, key);
            return match.Groups[1].Value;
        }

        private static int ReadBigEndianInt(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            Assert.That(bytes.Length, Is.EqualTo(4), "Truncated PNG header.");
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }
    }
}
