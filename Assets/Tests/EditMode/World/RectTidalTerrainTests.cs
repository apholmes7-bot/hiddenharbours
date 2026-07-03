using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The rectangular-plateau analytic terrain (<see cref="RectTidalTerrain"/> — the height source that
    /// converges the cove/Greywick onto the St Peters tide-driven water model, ADR 0012 rec. 4): the pure
    /// zone composition (plateau inside, smoothstep falloff outside, max-compose, deep floor), the
    /// out-of-zone clamp, and determinism (authored geometry, no RNG — CLAUDE.md rule 5). Engine-light,
    /// no scene.
    /// </summary>
    public class RectTidalTerrainTests
    {
        const float Deep = -4f;
        const float Land = 6f;

        static RectTidalTerrain.LandZone Zone(float cx, float cy, float hx, float hy,
                                              float elevation = Land, float falloff = 5f)
            => new RectTidalTerrain.LandZone(new Vector2(cx, cy), new Vector2(hx, hy), elevation, falloff);

        // ---- the plateau profile ---------------------------------------------------------------------

        [Test]
        public void InsideTheRect_IsTheFlatPlateau()
        {
            var zones = new[] { Zone(0f, 0f, 10f, 5f) };
            Assert.AreEqual(Land, RectTidalTerrain.ElevationAtZones(new Vector2(0f, 0f), Deep, zones), 1e-5f);
            Assert.AreEqual(Land, RectTidalTerrain.ElevationAtZones(new Vector2(9.9f, -4.9f), Deep, zones), 1e-5f,
                "flat right up to the rectangle edge");
        }

        [Test]
        public void BeyondTheFalloff_IsTheDeepFloor()
        {
            var zones = new[] { Zone(0f, 0f, 10f, 5f, falloff: 5f) };
            Assert.AreEqual(Deep, RectTidalTerrain.ElevationAtZones(new Vector2(0f, -10.01f), Deep, zones), 1e-4f,
                "5 m past the south edge (y=-5) the falloff has fully shelved to the deep floor");
            Assert.AreEqual(Deep, RectTidalTerrain.ElevationAtZones(new Vector2(200f, 200f), Deep, zones), 1e-4f,
                "far offshore reads the deep floor (out-of-zone clamp — a boat far out never throws)");
        }

        [Test]
        public void TheFalloffBand_IsMonotonic_LandDownToDeep()
        {
            // Marching south off the plateau edge, the ground must only ever descend — the beach profile
            // the waterline sweeps. A non-monotonic shelf would make the tide bare disconnected puddles.
            var zones = new[] { Zone(0f, 0f, 10f, 5f, falloff: 5f) };
            float prev = RectTidalTerrain.ElevationAtZones(new Vector2(0f, -5f), Deep, zones);
            Assert.AreEqual(Land, prev, 1e-5f, "the band starts at the plateau");
            for (float d = 0.25f; d <= 5.5f; d += 0.25f)
            {
                float e = RectTidalTerrain.ElevationAtZones(new Vector2(0f, -5f - d), Deep, zones);
                Assert.LessOrEqual(e, prev + 1e-5f, $"descending at {d} m off the edge");
                prev = e;
            }
            Assert.AreEqual(Deep, prev, 1e-4f, "and ends at the deep floor");
        }

        [Test]
        public void TheFalloffBand_CrossesTheIntertidal_SoTheShorelineMoves()
        {
            // The convergence point of the whole feature: somewhere in the falloff band the ground sits
            // BETWEEN low and high water, so the waterline visibly advances/retreats with the tide (P1).
            var zones = new[] { Zone(0f, 0f, 10f, 5f, falloff: 5f) };
            const float lowWater = -3.5f, highWater = 3.5f;   // the live St Peters swing
            bool foundIntertidal = false;
            for (float d = 0.1f; d <= 5f; d += 0.1f)
            {
                float e = RectTidalTerrain.ElevationAtZones(new Vector2(0f, -5f - d), Deep, zones);
                if (e > lowWater && e < highWater) { foundIntertidal = true; break; }
            }
            Assert.IsTrue(foundIntertidal,
                "the beach band must contain intertidal ground — ground that covers at high and bares at low");
        }

        // ---- composition -----------------------------------------------------------------------------

        [Test]
        public void OverlappingZones_MaxCompose_TheHighestFeatureWins()
        {
            var zones = new[]
            {
                Zone(0f, 0f, 10f, 5f, elevation: 2f, falloff: 5f),   // a low flat
                Zone(0f, 0f, 2f, 2f, elevation: 6f, falloff: 1f),    // a high spur on top of it
            };
            Assert.AreEqual(6f, RectTidalTerrain.ElevationAtZones(Vector2.zero, Deep, zones), 1e-5f,
                "where features overlap the highest ground wins (mirrors TidalTerrain's composition)");
            Assert.AreEqual(2f, RectTidalTerrain.ElevationAtZones(new Vector2(8f, 0f), Deep, zones), 1e-5f,
                "outside the spur the low flat stands");
        }

        [Test]
        public void NullOrEmptyZones_AreTheDeepFloorEverywhere()
        {
            Assert.AreEqual(Deep, RectTidalTerrain.ElevationAtZones(Vector2.zero, Deep, null), 1e-6f);
            Assert.AreEqual(Deep, RectTidalTerrain.ElevationAtZones(Vector2.zero, Deep,
                new RectTidalTerrain.LandZone[0]), 1e-6f);
        }

        [Test]
        public void ZeroFalloff_IsAHardCliffEdge()
        {
            var zones = new[] { Zone(0f, 0f, 10f, 5f, falloff: 0f) };
            Assert.AreEqual(Land, RectTidalTerrain.ElevationAtZones(new Vector2(0f, -5f), Deep, zones), 1e-5f);
            Assert.AreEqual(Deep, RectTidalTerrain.ElevationAtZones(new Vector2(0f, -5.001f), Deep, zones), 1e-4f,
                "falloff 0 = a sheer wall (a dredged dock face)");
        }

        // ---- the rect distance helper ----------------------------------------------------------------

        [Test]
        public void DistanceOutsideRect_ZeroInside_EuclideanAtCorners()
        {
            var c = Vector2.zero; var h = new Vector2(10f, 5f);
            Assert.AreEqual(0f, RectTidalTerrain.DistanceOutsideRect(new Vector2(3f, -2f), c, h), 1e-6f);
            Assert.AreEqual(2f, RectTidalTerrain.DistanceOutsideRect(new Vector2(12f, 0f), c, h), 1e-6f,
                "straight off an edge = perpendicular distance");
            Assert.AreEqual(5f, RectTidalTerrain.DistanceOutsideRect(new Vector2(13f, 9f), c, h), 1e-5f,
                "off a corner = euclidean corner distance (3-4-5)");
        }

        // ---- determinism + the Core seam -------------------------------------------------------------

        [Test]
        public void ElevationAt_IsDeterministic_NoRng()
        {
            var go = new GameObject("RectTidalTerrain_Test");
            try
            {
                var t = go.AddComponent<RectTidalTerrain>();
                t.Configure(Deep, new[] { Zone(0f, 0f, 10f, 5f) });
                var p = new Vector2(3.7f, -7.2f);
                float first = t.ElevationAt(p);
                for (int i = 0; i < 8; i++)
                    Assert.AreEqual(first, t.ElevationAt(p), 1e-6f,
                        "authored geometry: same position → same elevation every call (no RNG, nothing saved)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RectTidalTerrain_IsAnITidalTerrain_UsableThroughTheCoreAccessor()
        {
            // Same contract check as TidalTerrain (StPetersTerrainTests): the component IS the Core seam
            // and round-trips through GameServices.TidalTerrain — so walkability / grounding / the water
            // bake all read the rect coast without referencing World (rule 4).
            var go = new GameObject("RectTidalTerrain_Test");
            try
            {
                var t = go.AddComponent<RectTidalTerrain>();
                t.Configure(Deep, new[] { Zone(0f, 0f, 10f, 5f) });
                ITidalTerrain asInterface = t;
                Assert.IsNotNull(asInterface, "RectTidalTerrain implements the Core ITidalTerrain seam");

                GameServices.TidalTerrain = t;
                var pos = new Vector2(0f, -7f);
                Assert.AreSame(t, GameServices.TidalTerrain, "round-trips through the Core accessor");
                Assert.AreEqual(t.ElevationAt(pos), GameServices.TidalTerrain.ElevationAt(pos), 1e-6f,
                    "reading through Core yields the same authored elevation as the component");
            }
            finally
            {
                Object.DestroyImmediate(go);
                GameServices.Reset();
            }
        }
    }
}
