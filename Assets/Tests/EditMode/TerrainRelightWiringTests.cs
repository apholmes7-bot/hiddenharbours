using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// PR 5a: the relight is wired where the regions are made. The shared loader
    /// (TerrainArrayAssets.LoadRelightRequired, ADR 0047) returns the committed recipe's four maps, a surface
    /// given them turns the relight on, and both region builders hand them to their surface right after its
    /// detail array, so a region a builder makes comes out relit. The committed regions are
    /// RegionRelightSceneTests'; the binding of maps in general is TerrainRelightBindingTests'.
    ///
    /// <para>The bars: the recipe's committed path and the guid its .meta holds, read as text, and the shader
    /// include's tile and ramp width, written out by hand as in TerrainRelightBindingTests.</para>
    /// </summary>
    public class TerrainRelightWiringTests
    {
        const string RecipePath = "Assets/_Project/Art/Terrain/TerrainRelight.hhterrain";
        const string BuildersRoot = "Assets/_Project/Code/App/Editor/";
        const int Tile = 256;      // TL6_TILE
        const int RampWidth = 81;  // sixteen palettes of five, then the parameters (TL6_RAMP_PARAMS = 80)

        [Test]
        public void TheSharedLoader_ReturnsTheCommittedRecipesFourMaps_AndEachFitsTheDetailArray()
        {
            Match meta = Regex.Match(File.ReadAllText(RecipePath + ".meta"), @"(?m)^guid: (?<guid>[0-9a-f]{32})\r?$");
            Assert.IsTrue(meta.Success, RecipePath + ".meta holds no guid");

            TerrainArrayAssets.RelightSet set = TerrainArrayAssets.LoadRelightRequired();
            Object[] maps = { set.Normal, set.Light, set.Detail, set.Ramp };
            string[] names = { "normal", "light", "detail", "ramp" };
            for (int i = 0; i < maps.Length; i++)
            {
                Assert.IsNotNull(maps[i], "the loader returned no " + names[i] + " map");
                Assert.AreEqual(RecipePath, AssetDatabase.GetAssetPath(maps[i]),
                    "the " + names[i] + " map is not the committed recipe's");
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(maps[i], out string guid, out long _),
                    "the " + names[i] + " map has no identity in the AssetDatabase");
                Assert.AreEqual(meta.Groups["guid"].Value, guid, "the " + names[i] + " map is not under the recipe's guid");
            }
            Assert.AreEqual(maps.Length, maps.Distinct().Count(), "the loader returned one map for two of the four");

            Texture2DArray detail = TerrainArrayAssets.LoadDetailRequired();
            foreach (Texture2DArray map in new[] { set.Normal, set.Light, set.Detail })
            {
                Assert.AreEqual(Tile, map.width, map.name + ": slices not TL6_TILE wide");
                Assert.AreEqual(Tile, map.height, map.name + ": slices not TL6_TILE tall");
                Assert.AreEqual(detail.depth, map.depth, map.name + ": not as deep as the detail array");
            }
            Assert.AreEqual(RampWidth, set.Ramp.width, "the ramp has no column for the slice's parameters");
            Assert.AreEqual(detail.depth, set.Ramp.height, "the ramp has not one row per slice");
        }

        [Test]
        public void AScratchSurfaceGivenTheLoadersSet_TurnsTheRelightOn()
        {
            RequireTextureArrays();
            TerrainArrayAssets.RelightSet set = TerrainArrayAssets.LoadRelightRequired();
            var go = new GameObject("RelightWiring");
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.ConfigureDetail(TerrainArrayAssets.LoadDetailRequired(), null);
                splat.ConfigureRelight(set.Normal, set.Light, set.Detail, set.Ramp);
                go.SetActive(true);   // OnEnable builds the quad and pushes the block, as a builder's region does

                Assert.IsTrue(splat.RelightLoaded, "the surface refused the loader's set: a built region keeps the albedo");
                var mpb = Block(go);
                Assert.AreEqual(1f, mpb.GetFloat("_RelightLoaded"), "the loader's set did not turn the relight on");
                Assert.AreSame(set.Normal, mpb.GetTexture("_RelightNormal"), "the normal map did not reach the ground");
                Assert.AreSame(set.Light, mpb.GetTexture("_RelightLight"), "the light map did not reach the ground");
                Assert.AreSame(set.Detail, mpb.GetTexture("_RelightDetail"), "the detail map did not reach the ground");
                Assert.AreSame(set.Ramp, mpb.GetTexture("_RelightRamp"), "the ramp did not reach the ground");
            }
            finally { Object.DestroyImmediate(go); }   // the maps are the recipe's imported objects: never destroyed here
        }

        /// <summary>The builders are never run by a test (St Peters' would wipe the hand-authored scene), so
        /// their wiring is read from their source: the loader's set, handed to the surface right after its
        /// detail array (comments between allowed), and the relight configured nowhere else.</summary>
        [TestCase("StPetersBuilder")]
        [TestCase("NineMileCreekBuilder")]
        public void TheBuilder_RelightsTheGroundItBuilds_WithTheLoadersSet(string builder)
        {
            string path = BuildersRoot + builder + ".cs";
            string src = File.ReadAllText(path);
            Match wired = Regex.Match(src,
                @"(?<splat>\w+)\.ConfigureDetail\(\s*(?:\w+\.)*TerrainArrayAssets\.LoadDetailRequired\(\),\s*null\);" +
                @"(?:\s*//[^\r\n]*)*" +
                @"\s*var (?<set>\w+) = (?:\w+\.)*TerrainArrayAssets\.LoadRelightRequired\(\);" +
                @"\s*\k<splat>\.ConfigureRelight\(\k<set>\.Normal, \k<set>\.Light, \k<set>\.Detail, \k<set>\.Ramp\);");
            Assert.IsTrue(wired.Success, path + " does not hand its surface the loader's relight set right after the " +
                                         "detail array: a region it builds would come out unrelit");
            Assert.AreEqual(1, Regex.Matches(src, @"\.ConfigureRelight\(").Count,
                path + " configures the relight more than once, so the loader's set may not be the last word");
        }

        static void RequireTextureArrays()
        {
            if (!SystemInfo.supports2DArrayTextures)
                Assert.Ignore("this graphics device has no texture arrays, so the relight maps cannot be bound here.");
        }

        static MaterialPropertyBlock Block(GameObject host)
        {
            var renderer = host.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(renderer, "the surface built no ground quad, so it pushed nothing to read");
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            return mpb;
        }
    }
}
