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

        // ---- The wade model: IsWalkable is a strict SUPERSET of IsExposed at wadeDepth=0 -------

        [Test]
        public void IsWalkable_AtZeroWadeDepth_ExactlyMatchesIsExposed()
        {
            // The superset invariant that keeps every existing walkability behaviour intact.
            foreach (var wl in new[] { -1f, 0f, 1.5f, 3f, 4f })
            foreach (var te in new[] { -1f, 0f, 1.49f, 1.5f, 1.51f, 4f })
                Assert.AreEqual(
                    TidalExposure.IsExposed(wl, te),
                    TidalExposure.IsWalkable(wl, te, wadeDepth: 0f),
                    $"IsWalkable(wade=0) must equal IsExposed at wl={wl}, te={te}");
        }

        [Test]
        public void IsWalkable_WithWadeDepth_AdmitsShallowWater_ButNotDeeper()
        {
            // Water surface 5.0 m. wadeDepth 0.5 m → walkable down to ground 4.5 m (0.5 m of water).
            const float wl = 5.0f;
            Assert.IsTrue(TidalExposure.IsWalkable(wl, terrainElevation: 5.0f, wadeDepth: 0.5f), "dry ground walkable");
            Assert.IsTrue(TidalExposure.IsWalkable(wl, terrainElevation: 4.6f, wadeDepth: 0.5f), "0.4 m of water: wadeable");
            Assert.IsTrue(TidalExposure.IsWalkable(wl, terrainElevation: 4.5f, wadeDepth: 0.5f), "exactly wadeDepth: still wadeable (inclusive)");
            Assert.IsFalse(TidalExposure.IsWalkable(wl, terrainElevation: 4.4f, wadeDepth: 0.5f), "0.6 m of water: too deep to walk");
        }

        // ---- Depth bands: dry/wade/swim/deep at 0, WadeDepth, SwimLimit ------------------------

        [Test]
        public void BandForDepth_TilesTheThreeBands_AtTheBoundaries()
        {
            const float wade = 0.5f, swim = 2.0f;
            Assert.AreEqual(DepthBand.Dry,  TidalExposure.BandForDepth(-0.1f, wade, swim), "negative depth is dry");
            Assert.AreEqual(DepthBand.Dry,  TidalExposure.BandForDepth(0f,    wade, swim), "exactly 0 is dry (≤ 0)");
            Assert.AreEqual(DepthBand.Wade, TidalExposure.BandForDepth(0.01f, wade, swim), "just wet → wade");
            Assert.AreEqual(DepthBand.Wade, TidalExposure.BandForDepth(0.5f,  wade, swim), "exactly WadeDepth → wade (inclusive)");
            Assert.AreEqual(DepthBand.Swim, TidalExposure.BandForDepth(0.51f, wade, swim), "just past WadeDepth → swim");
            Assert.AreEqual(DepthBand.Swim, TidalExposure.BandForDepth(2.0f,  wade, swim), "exactly SwimLimit → swim (inclusive)");
            Assert.AreEqual(DepthBand.Deep, TidalExposure.BandForDepth(2.01f, wade, swim), "past SwimLimit → boat-only deep");
        }

        // ---- The speed-by-depth curve: full at 0, WadeSlowFactor at WadeDepth, SwimSlowFactor --

        [Test]
        public void MoveScaleForDepth_FollowsTheOwnerCurve()
        {
            const float wade = 0.5f, swim = 2.0f, wadeF = 0.6f, swimF = 0.25f;

            Assert.AreEqual(1f,    TidalExposure.MoveScaleForDepth(0f,   wade, swim, wadeF, swimF), 1e-5f, "dry → full speed");
            Assert.AreEqual(1f,    TidalExposure.MoveScaleForDepth(-1f,  wade, swim, wadeF, swimF), 1e-5f, "above water → full speed");
            Assert.AreEqual(0.8f,  TidalExposure.MoveScaleForDepth(0.25f,wade, swim, wadeF, swimF), 1e-5f, "mid-wade → halfway 1→0.6");
            Assert.AreEqual(wadeF, TidalExposure.MoveScaleForDepth(0.5f, wade, swim, wadeF, swimF), 1e-5f, "at WadeDepth → WadeSlowFactor");
            // Mid-swim (depth 1.25 = halfway 0.5→2.0) → halfway 0.6→0.25 = 0.425.
            Assert.AreEqual(0.425f,TidalExposure.MoveScaleForDepth(1.25f,wade, swim, wadeF, swimF), 1e-5f, "mid-swim → halfway wadeF→swimF");
            Assert.AreEqual(swimF, TidalExposure.MoveScaleForDepth(2.0f, wade, swim, wadeF, swimF), 1e-5f, "at SwimLimit → SwimSlowFactor");
            Assert.AreEqual(swimF, TidalExposure.MoveScaleForDepth(5.0f, wade, swim, wadeF, swimF), 1e-5f, "deep → holds at SwimSlowFactor (escape crawl)");
        }

        [Test]
        public void MoveScaleForDepth_IsMonotonicNonIncreasing_WithDepth()
        {
            const float wade = 0.5f, swim = 2.0f, wadeF = 0.6f, swimF = 0.25f;
            float prev = 2f;
            for (float d = -0.5f; d <= 3f; d += 0.05f)
            {
                float s = TidalExposure.MoveScaleForDepth(d, wade, swim, wadeF, swimF);
                Assert.LessOrEqual(s, prev + 1e-5f, $"speed must never increase with depth (d={d})");
                prev = s;
            }
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

        [Test]
        public void Band_And_MoveScale_AreDeterministic_ForIdenticalInputs()
        {
            const float wade = 0.5f, swim = 2.0f, wadeF = 0.6f, swimF = 0.25f;
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(TidalExposure.BandForDepth(0.9f, wade, swim),
                                TidalExposure.BandForDepth(0.9f, wade, swim),
                                "same depth+thresholds → same band every call");
                Assert.AreEqual(TidalExposure.MoveScaleForDepth(0.9f, wade, swim, wadeF, swimF),
                                TidalExposure.MoveScaleForDepth(0.9f, wade, swim, wadeF, swimF), 0f,
                                "same depth+tunables → same speed scale every call");
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
