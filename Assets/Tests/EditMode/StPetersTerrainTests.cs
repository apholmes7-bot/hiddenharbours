using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters opening — the world-content half: the <see cref="TidalTerrain"/> elevation ZONES (the
    /// tide showcase) and the <c>region.st_peters</c> <see cref="RegionDef"/> validity. Drives the terrain
    /// from the BUILDER's authored constants (<see cref="StPetersBuilder"/> — the single source of truth
    /// the scene is built from), composing them with the deterministic exposure rule
    /// (<see cref="TidalExposure"/>) to prove the bar/channel are INVERSE over the tide: walkers cross the
    /// flats at low water, boats cross the channel at high. Pure + engine-light; no scene is loaded.
    /// </summary>
    public class StPetersTerrainTests
    {
        // Mirror the region's authored tide (StPetersBuilder constants): mean 0, amplitude 3.5 → the water
        // swings ≈ -3.5..+3.5 m. We sample a few representative levels rather than the live tide curve.
        private const float HighWater = 2.5f;   // near the top of the swing — bar covered
        private const float MidWater  = 0.0f;   // mid tide
        private const float LowWater  = -2.5f;  // near the bottom — bar well bared

        private TidalTerrain _terrain;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_Test");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);   // the same zones the scene is built with
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        // A point on the sandbar centre-line, away from the channel cut (the walker's path).
        private Vector2 BarFlat()
        {
            // 25% along From→To — comfortably away from the channel crossing at 62%.
            return Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, 0.25f);
        }

        // The channel cut through the bar (the boat passage).
        private Vector2 ChannelCentre()
            => Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo, StPetersBuilder.ChannelAlong);

        // ---- Island: always exposed (you can't tide the home ground under) -------------------------

        [Test]
        public void Island_IsAlwaysExposed_EvenAtHighWater()
        {
            float islandElev = _terrain.ElevationAt(StPetersBuilder.IslandCenter);
            Assert.Greater(islandElev, HighWater,
                "the island plateau must sit above the highest water — always walkable home ground");
            Assert.IsTrue(TidalExposure.IsExposed(HighWater, islandElev),
                "island is exposed at high water");
            Assert.IsTrue(TidalExposure.IsExposed(LowWater, islandElev),
                "island is exposed at low water");
        }

        // ---- Deep harbour: always submerged (never bares; always boatable) -------------------------

        [Test]
        public void DeepHarbour_IsAlwaysSubmerged_EvenAtLowWater()
        {
            // A point well off the bar and the island — open water.
            var openWater = new Vector2(0f, 40f);
            float elev = _terrain.ElevationAt(openWater);
            Assert.Less(elev, LowWater,
                "the deep harbour seabed must stay below the lowest water — it never bares");
            Assert.IsFalse(TidalExposure.IsExposed(LowWater, elev), "deep harbour submerged even at low water");
        }

        // ---- Sandbar flats: the WALKER'S path — covered high, bared low (the showcase) -------------

        [Test]
        public void SandbarFlats_CoverAtHighWater_BareAtLowWater()
        {
            float barElev = _terrain.ElevationAt(BarFlat());

            // Authored crest sits below high water, above low water → it inverts across the tide.
            Assert.IsFalse(TidalExposure.IsExposed(HighWater, barElev),
                "at high water the bar is covered — no walking across");
            Assert.IsTrue(TidalExposure.IsExposed(LowWater, barElev),
                "at low water the bar bares — the walker crosses the flats");
        }

        // ---- Channel: the BOAT'S passage — crossable high, shoals/dries as the tide falls ----------

        [Test]
        public void Channel_DeeperThanTheFlats_CrossableAtHigherTide()
        {
            float channelElev = _terrain.ElevationAt(ChannelCentre());
            float barElev = _terrain.ElevationAt(BarFlat());

            Assert.Less(channelElev, barElev,
                "the channel bed must be cut BELOW the bar crest — a gut through the flats");

            // At high water the channel carries real depth a boat crosses.
            float depthHigh = TidalExposure.WaterDepth(HighWater, channelElev);
            Assert.Greater(depthHigh, 1.0f,
                "at high water the channel has enough depth for the dory to cross");

            // As the tide falls to mid, the channel shoals (less depth) — narrowing the safe gap.
            float depthMid = TidalExposure.WaterDepth(MidWater, channelElev);
            Assert.Less(depthMid, depthHigh, "the channel shoals as the tide falls");
            Assert.Greater(depthMid, 0f, "still a sliver of water at mid tide");
        }

        [Test]
        public void BarAndChannel_AreInverse_OverTheTide()
        {
            // THE SHOWCASE in one assertion: at high water the flats are covered but the channel is deep
            // (boat crosses); at low water the flats are bared (walker crosses) and the channel has lost
            // its boat depth. The two cross-modes swap with the tide.
            float barElev = _terrain.ElevationAt(BarFlat());
            float channelElev = _terrain.ElevationAt(ChannelCentre());

            // HIGH: boat over channel, no walking the flats.
            Assert.Greater(TidalExposure.WaterDepth(HighWater, channelElev), 1.0f, "high: boat crosses channel");
            Assert.IsFalse(TidalExposure.IsExposed(HighWater, barElev), "high: flats covered (no walk)");

            // LOW: walk the flats, channel no longer a boat passage (shoaled to a trickle or dry).
            Assert.IsTrue(TidalExposure.IsExposed(LowWater, barElev), "low: walk the bared flats");
            Assert.Less(TidalExposure.WaterDepth(LowWater, channelElev), 1.0f,
                "low: the channel has lost its boat-crossing depth");
        }

        [Test]
        public void ElevationAt_IsDeterministic_NoRng()
        {
            var p = BarFlat();
            float first = _terrain.ElevationAt(p);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(first, _terrain.ElevationAt(p), 1e-6f,
                    "authored geometry: same position → same elevation every call (no RNG, nothing saved)");
        }

        [Test]
        public void TidalTerrain_IsAnITidalTerrain_UsableThroughTheCoreAccessor()
        {
            // The world publishes its height map through Core so gameplay/UI read it WITHOUT referencing
            // World (CLAUDE.md rule 4). The runtime self-registration is via OnEnable (a PlayMode lifecycle
            // hook that AddComponent does NOT fire in EditMode), so here we verify the CONTRACT it relies on:
            // TidalTerrain IS an ITidalTerrain and round-trips through GameServices.TidalTerrain, returning
            // the same authored elevation the component computes directly.
            ITidalTerrain asInterface = _terrain;
            Assert.IsNotNull(asInterface, "TidalTerrain implements the Core ITidalTerrain seam");

            GameServices.TidalTerrain = _terrain;
            var pos = ChannelCentre();
            Assert.AreSame(_terrain, GameServices.TidalTerrain, "round-trips through the Core accessor");
            Assert.AreEqual(_terrain.ElevationAt(pos), GameServices.TidalTerrain.ElevationAt(pos), 1e-6f,
                "reading through Core yields the same authored elevation as the component");
        }

        // ---- region.st_peters RegionDef validity (the authored asset) ------------------------------

        [Test]
        public void StPetersRegionDef_Exists_AndIsValid()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/StPeters.asset");
            Assert.IsNotNull(region, "the St Peters RegionDef must exist (built by StPetersBuilder)");
            Assert.AreEqual("region.st_peters", region.Id, "stable id");
            Assert.AreEqual("St Peters Island", region.DisplayName, "player-facing name");
            Assert.AreEqual("StPeters", region.SceneName, "names its scene");
            Assert.IsTrue(region.HasScene, "it is a real, reachable region");
            Assert.IsFalse(region.RequiresUnlock, "the opening has no unlock gate");
            Assert.Greater(region.TideAmplitude, 2f,
                "St Peters runs a BIG tide (vs the cove's gentle ~1.6 m) — the bar must visibly bare + flood");
        }

        [Test]
        public void StPetersRegionDef_TideMatchesTheBuilderConstants()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/StPeters.asset");
            Assert.IsNotNull(region);
            Assert.AreEqual(StPetersBuilder.TideMean, region.TideMeanLevel, 1e-4f);
            Assert.AreEqual(StPetersBuilder.TideAmplitude, region.TideAmplitude, 1e-4f);
            Assert.AreEqual(StPetersBuilder.TidePhaseHours, region.TidePhaseHours, 1e-4f);
        }

        [Test]
        public void SoftShellClam_IsGatedToStPeters_NotTheCove()
        {
            var clam = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Fishing.FishSpeciesDef>(
                "Assets/_Project/Data/Fish/SoftShellClam.asset");
            Assert.IsNotNull(clam, "the soft-shell clam must exist");
            CollectionAssert.Contains(clam.RegionIds, "region.st_peters",
                "the clam is re-gated to St Peters (the opening's flats), not the cove placeholder");
            CollectionAssert.DoesNotContain(clam.RegionIds, "region.coddle_cove",
                "the cove placeholder gate is removed");
        }
    }
}
