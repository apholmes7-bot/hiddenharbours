using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Core tidal-exposure seam (ADR 0009): the one shared "submerged or exposed here, now?" rule
    /// the world (terrain) and gameplay (walkability sim) both read for the falling-tide walkable
    /// seabed and the St Peters sandbar. All pure + deterministic — no RNG, no saved tide — so it tests
    /// without Unity's loop. A determinism test pins the invariant CLAUDE.md rule 5 protects.
    /// </summary>
    public class TidalExposureTests
    {
        // ---- WaterDepth: metres above datum, <= 0 means dry/exposed ----------------------------

        [Test]
        public void WaterDepth_IsWaterLevelMinusTerrain()
        {
            Assert.AreEqual(2.5f, TidalExposure.WaterDepth(4.0f, 1.5f), 1e-5f, "depth = waterLevel - terrain");
            Assert.AreEqual(-1.0f, TidalExposure.WaterDepth(1.0f, 2.0f), 1e-5f, "ground above water → negative (dry)");
            Assert.AreEqual(0f, TidalExposure.WaterDepth(3.0f, 3.0f), 1e-5f, "ground exactly at surface → zero depth");
        }

        // ---- IsExposed / IsSubmerged: ground at-or-above surface is exposed ---------------------

        [Test]
        public void IsExposed_WhenTerrainAtOrAboveWaterLevel()
        {
            Assert.IsTrue(TidalExposure.IsExposed(waterLevel: 2.0f, terrainElevation: 3.0f), "ground above water is exposed");
            Assert.IsTrue(TidalExposure.IsExposed(waterLevel: 2.0f, terrainElevation: 2.0f), "ground exactly at the surface counts as exposed");
            Assert.IsFalse(TidalExposure.IsExposed(waterLevel: 2.0f, terrainElevation: 1.99f), "ground just under the surface is submerged");
        }

        [Test]
        public void IsSubmerged_IsTheExactNegationOfIsExposed()
        {
            foreach (var wl in new[] { -1f, 0f, 1.5f, 4f })
            foreach (var te in new[] { -1f, 0f, 1.5f, 4f })
            {
                Assert.AreNotEqual(
                    TidalExposure.IsExposed(wl, te),
                    TidalExposure.IsSubmerged(wl, te),
                    $"exposed and submerged must disagree at wl={wl}, te={te}");
            }
        }

        // ---- The falling-tide reveal: a fixed tile flips exposed as the water level drops -------

        [Test]
        public void FallingTide_ExposesAFixedTileOnceWaterDropsBelowItsElevation()
        {
            // A sandbar tile authored at 3.0 m above datum.
            const float sandbar = 3.0f;
            Assert.IsFalse(TidalExposure.IsExposed(4.0f, sandbar), "high water (4.0 m): sandbar submerged → not walkable");
            Assert.IsFalse(TidalExposure.IsExposed(3.5f, sandbar), "mid-tide (3.5 m): still under → not walkable");
            Assert.IsTrue(TidalExposure.IsExposed(2.5f, sandbar),  "low water (2.5 m): bared → walkable path");
        }

        // ---- Convenience overload reads the deterministic water level from the service ---------

        [Test]
        public void IsExposed_ViaEnvironmentService_ReadsWaterLevelAt()
        {
            var env = new FakeEnv { Level = 2.0f };
            Assert.IsTrue(TidalExposure.IsExposed(env, totalSeconds: 100.0, terrainElevation: 2.5f), "terrain above the service's water level → exposed");
            Assert.IsFalse(TidalExposure.IsExposed(env, totalSeconds: 100.0, terrainElevation: 1.0f), "terrain below it → submerged");
        }

        [Test]
        public void IsExposed_ViaNullEnvironment_DefaultsToSubmerged()
        {
            // Safe default for a walkability gate: no service wired → treat as submerged (not walkable),
            // and never throw in the hot path.
            Assert.IsFalse(TidalExposure.IsExposed(null, totalSeconds: 0.0, terrainElevation: 999f));
        }

        // ---- Determinism: identical inputs → identical answer (rule 5) -------------------------

        [Test]
        public void Exposure_IsDeterministic_ForIdenticalInputs()
        {
            var env = new FakeEnv { Level = 1.7f };
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(
                    TidalExposure.IsExposed(env, 12345.0, 1.8f),
                    TidalExposure.IsExposed(env, 12345.0, 1.8f),
                    "same (water level, time, elevation) must give the same exposure every call");
                Assert.AreEqual(0.1f, TidalExposure.WaterDepth(1.8f, 1.7f), 1e-5f, "and the depth is stable");
            }
        }

        /// <summary>A minimal deterministic stand-in: WaterLevelAt returns a fixed level, exercising the
        /// default-interface-method path (it is left to default to TideHeightAt unless overridden).</summary>
        private sealed class FakeEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level; // WaterLevelAt defaults to this
        }
    }
}
