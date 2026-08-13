#if UNITY_EDITOR
using NUnit.Framework;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A splat map belongs to ONE region, and the asset layer has to be able to say which.</b>
    ///
    /// <para>Every entry point on <see cref="TerrainSplatAssets"/> used to resolve to the St Peters stem
    /// as a <i>constant</i>. That was correct while St Peters was the only painted region and becomes
    /// wrong the moment there is a second one: a splat map covers one region's world rect at that
    /// region's texel grid, so pointing Nine Mile Creek at the old pipeline would have had it minting,
    /// painting and committing over the ISLAND's maps — on the island's frame, at the island's
    /// resolution. Nothing would have errored. The creek's ground would simply have landed on St Peters.</para>
    ///
    /// <para>These pin the seam that stops it: the stem is a parameter, the no-stem overloads still mean
    /// St Peters, and a lookup actually consults the stem it was handed rather than a constant.</para>
    ///
    /// <para>Pure path arithmetic — no assets are read, written or minted.</para>
    /// </summary>
    public class TerrainSplatStemTests
    {
        const string CreekStem = "NineMileCreekSplat";

        [Test]
        public void TheNoStemOverload_StillMeansStPeters()
        {
            // The whole point of keeping it: existing callers (the paint tool, the island's builder and
            // starter splat, their tests) must not have to change, and must not quietly move.
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
                Assert.AreEqual(TerrainSplatAssets.PathOf(i, TerrainSplatAssets.StPetersBaseName),
                                TerrainSplatAssets.PathOf(i),
                                $"the bare PathOf({i}) must still resolve to the island's own map.");
        }

        [Test]
        public void EachStem_GetsItsOwnFiveMaps_AndNeverAnotherRegions()
        {
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
            {
                string island = TerrainSplatAssets.PathOf(i, TerrainSplatAssets.StPetersBaseName);
                string creek = TerrainSplatAssets.PathOf(i, CreekStem);

                Assert.AreNotEqual(island, creek,
                    $"map {TerrainSplatBrush.TextureSuffixes[i]} must differ between regions — two regions " +
                    "sharing one PNG is one region painting over the other.");
                Assert.IsTrue(creek.StartsWith(TerrainSplatAssets.Dir + "/" + CreekStem, System.StringComparison.Ordinal),
                    "a region's maps live beside it in Data/Terrain, stemmed by name.");
                Assert.IsTrue(creek.EndsWith(TerrainSplatBrush.TextureSuffixes[i] + ".png", System.StringComparison.Ordinal),
                    "the A..E suffix is the channel-block index and must survive the stem change.");
            }

            // And the five are distinct from each other, which is what makes them five maps and not one
            // written over four times.
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
                Assert.IsTrue(seen.Add(TerrainSplatAssets.PathOf(i, CreekStem)),
                              "every splat index must map to its own file.");
        }

        [Test]
        public void ALookup_ConsultsTheStemItWasHanded_NotAConstant()
        {
            // ⚠ THE ONE THAT CATCHES THE REAL REGRESSION. Every other assertion here passes even if
            // AllExist ignores its argument and keeps asking about St Peters — this is the one that does
            // not. A stem nothing has ever been minted for must come back false; if St Peters' committed
            // maps can answer for it, the parameter is decorative and the creek would silently "already
            // have" a painted ground it has never had.
            Assert.IsFalse(TerrainSplatAssets.AllExist("NoRegionIsStemmedThisWay"),
                "AllExist must answer about the stem it was given.");
        }
    }
}
#endif
