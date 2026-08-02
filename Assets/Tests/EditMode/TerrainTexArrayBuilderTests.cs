using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The terrain detail arrays' REBUILD path (2026-08-02). <c>Build()</c> has two save paths:
    /// CreateAsset when the Derived assets are absent (every fresh worktree — agents, CI) and the
    /// GUID-stable CopySerialized-in-place when they exist (the OWNER's machine, every rebuild after
    /// the first). The in-place path destroys the temp arrays it packed, and the old slice-count log
    /// line then read <c>.depth</c> off the destroyed temps — a MissingReferenceException that killed
    /// every St Peters build on the one machine class that matters. This suite runs BOTH paths, which
    /// is exactly what no fresh-checkout run ever did.
    /// </summary>
    public class TerrainTexArrayBuilderTests
    {
        static int ExpectedSlices =>
            (TerrainTexArrayBuilder.Order256.Length + TerrainTexArrayBuilder.Order512.Length)
            * TerrainTexArrayBuilder.LadderSteps.Length;

        [Test]
        public void Build_Twice_TheInPlaceRebuildSurvives_AndTheGuidHolds()
        {
            int first = TerrainTexArrayBuilder.Build();
            if (first == 0)
                Assert.Ignore("terrain kit PNGs absent or mis-sized — nothing to pack here.");

            Assert.AreEqual(ExpectedSlices, first,
                "the first build must pack every material x every ladder step");

            string guid256 = AssetDatabase.AssetPathToGUID(TerrainTexArrayBuilder.Array256Path);
            string guid512 = AssetDatabase.AssetPathToGUID(TerrainTexArrayBuilder.Array512Path);
            Assert.IsNotEmpty(guid256, "the 256 array must exist as an asset after the first build");

            // The second run is the one the owner's machine takes on EVERY rebuild: assets exist, so
            // SaveInPlace goes CopySerialized + DestroyImmediate(temp). This line threw before the fix.
            int second = TerrainTexArrayBuilder.Build();

            Assert.AreEqual(first, second, "the in-place rebuild must repack the same kit");
            Assert.AreEqual(guid256, AssetDatabase.AssetPathToGUID(TerrainTexArrayBuilder.Array256Path),
                "the 256 array's GUID must survive an in-place rebuild — scenes reference it");
            Assert.AreEqual(guid512, AssetDatabase.AssetPathToGUID(TerrainTexArrayBuilder.Array512Path),
                "the 512 array's GUID must survive an in-place rebuild — scenes reference it");

            // And the persisted asset must genuinely carry the new pack, not a husk: depth is readable
            // and right on a fresh load after the in-place save.
            var persisted = AssetDatabase.LoadAssetAtPath<Texture2DArray>(
                TerrainTexArrayBuilder.Array256Path);
            Assert.IsNotNull(persisted, "the persisted 256 array must load after an in-place rebuild");
            Assert.AreEqual(TerrainTexArrayBuilder.Order256.Length
                            * TerrainTexArrayBuilder.LadderSteps.Length,
                persisted.depth, "the persisted 256 array must carry one slice per material per step");
        }
    }
}
