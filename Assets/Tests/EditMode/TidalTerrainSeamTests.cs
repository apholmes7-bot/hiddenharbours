using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Core terrain-elevation seam (ADR 0009 follow-on): <see cref="ITidalTerrain"/> +
    /// the <see cref="GameServices.TidalTerrain"/> accessor — the shared "height map" the world
    /// publishes and gameplay/UI read WITHOUT referencing World. Pure + engine-light: an accessor
    /// round-trip, the null-as-open-water default, and the documented composition with
    /// <see cref="TidalExposure"/> (walkable-now + boat-cross depth). Exercises only Core, no scene.
    /// </summary>
    public class TidalTerrainSeamTests
    {
        [TearDown]
        public void TearDown() => GameServices.Reset();

        // ---- Accessor: round-trip, and NOT part of the Ready gate -------------------------------

        [Test]
        public void TidalTerrain_RoundTripsThroughGameServices()
        {
            var terrain = new FakeTerrain { Elevation = 2.5f };
            GameServices.TidalTerrain = terrain;
            Assert.AreSame(terrain, GameServices.TidalTerrain, "the accessor returns what was registered");
        }

        [Test]
        public void TidalTerrain_IsOptional_NotPartOfReady()
        {
            // Like Wallet/Licenses/ActiveBoat, the (optional, scene-scoped) terrain must not gate Ready —
            // Ready is Clock + Environment only.
            GameServices.TidalTerrain = new FakeTerrain { Elevation = 0f };
            Assert.IsFalse(GameServices.Ready, "registering terrain alone must not flip Ready true");
        }

        [Test]
        public void Reset_ClearsTidalTerrain()
        {
            GameServices.TidalTerrain = new FakeTerrain { Elevation = 1f };
            GameServices.Reset();
            Assert.IsNull(GameServices.TidalTerrain, "Reset clears the scene-scoped terrain");
        }

        // ---- Null is the "open water" default -----------------------------------------------------

        [Test]
        public void NoTerrainRegistered_AccessorIsNull()
        {
            GameServices.Reset();
            Assert.IsNull(GameServices.TidalTerrain,
                "before a region wires itself the terrain is null — callers treat that as open water");
        }

        // ---- The documented composition: ElevationAt + WaterLevelAt → exposure / depth -----------

        [Test]
        public void ElevationAt_FeedsTidalExposure_WalkableAndDepth()
        {
            // A sandbar authored at 3.0 m above datum.
            var terrain = new FakeTerrain { Elevation = 3.0f };
            var pos = new Vector2(10f, 4f);

            // Low water (2.5 m): ground above the surface → exposed/walkable, depth negative (dry).
            const float lowWater = 2.5f;
            Assert.IsTrue(TidalExposure.IsExposed(lowWater, terrain.ElevationAt(pos)),
                "ground (3.0) above low water (2.5) → walkable on foot");
            Assert.AreEqual(-0.5f, TidalExposure.WaterDepth(lowWater, terrain.ElevationAt(pos)), 1e-5f,
                "boat-cross depth = waterLevel - elevation; <= 0 means dry");

            // High water (4.0 m): submerged, positive cross depth.
            const float highWater = 4.0f;
            Assert.IsFalse(TidalExposure.IsExposed(highWater, terrain.ElevationAt(pos)),
                "ground (3.0) under high water (4.0) → not walkable");
            Assert.AreEqual(1.0f, TidalExposure.WaterDepth(highWater, terrain.ElevationAt(pos)), 1e-5f,
                "1.0 m of water over the bar — a boat grounds if its draught exceeds this");
        }

        [Test]
        public void ElevationAt_IsDeterministic_ForTheSamePosition()
        {
            var terrain = new FakeTerrain { Elevation = 1.7f };
            var pos = new Vector2(-3f, 8f);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(1.7f, terrain.ElevationAt(pos), 1e-5f,
                    "same world position must give the same elevation every call (no RNG)");
        }

        /// <summary>A minimal deterministic stand-in: a flat height map at a fixed elevation.</summary>
        private sealed class FakeTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }
    }
}
