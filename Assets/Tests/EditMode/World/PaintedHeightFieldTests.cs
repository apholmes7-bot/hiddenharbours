using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The PURE painted-seabed sampling math (ADR 0014) — the determinism guard for
    /// <see cref="PaintedHeightField"/>: bilinear interpolation, the world↔texel mapping (matching the water
    /// shader's <c>uv = (worldXY − worldMin)/worldSize</c>), the min/max↔normalized R mapping, out-of-rect
    /// clamp, and reproducibility. Exercises only the POCO sampler (no scene, no texture), so what the sim
    /// reads is verified to come from the same interpolation the render uses — render == sim by construction.
    /// </summary>
    public class PaintedHeightFieldTests
    {
        private const float Eps = 1e-4f;

        // A 2×2 field over a 10×10 m rect centred at origin: corners at distinct elevations so bilinear is
        // observable. Texel centres sit at uv (0.25, 0.75) per axis → world (-2.5/+2.5).
        //   index layout (row-major, y outer): [0]=(x0,y0) [1]=(x1,y0) [2]=(x0,y1) [3]=(x1,y1)
        private static PaintedHeightField Make2x2()
        {
            var elev = new float[] { 0f, 2f, 4f, 6f };   // (BL=0, BR=2, TL=4, TR=6)
            return new PaintedHeightField(elev, 2, 2, Vector2.zero, new Vector2(10f, 10f));
        }

        // ---- texel-centre sampling: each corner texel returns its authored value -------------------

        [Test]
        public void SamplesTexelCentres_ReturnsAuthoredCornerValues()
        {
            var f = Make2x2();
            // Texel centres: x in {-2.5, +2.5}, y in {-2.5, +2.5}.
            Assert.AreEqual(0f, f.ElevationAt(new Vector2(-2.5f, -2.5f)), Eps, "bottom-left texel");
            Assert.AreEqual(2f, f.ElevationAt(new Vector2(2.5f, -2.5f)), Eps, "bottom-right texel");
            Assert.AreEqual(4f, f.ElevationAt(new Vector2(-2.5f, 2.5f)), Eps, "top-left texel");
            Assert.AreEqual(6f, f.ElevationAt(new Vector2(2.5f, 2.5f)), Eps, "top-right texel");
        }

        // ---- bilinear interpolation BETWEEN texels --------------------------------------------------

        [Test]
        public void Bilinear_MidpointBetweenTwoTexels_IsTheirAverage()
        {
            var f = Make2x2();
            // Midway along the bottom edge (x=0, y=-2.5): between BL(0) and BR(2) → 1.
            Assert.AreEqual(1f, f.ElevationAt(new Vector2(0f, -2.5f)), Eps, "bottom edge midpoint = (0+2)/2");
            // Midway up the left edge (x=-2.5, y=0): between BL(0) and TL(4) → 2.
            Assert.AreEqual(2f, f.ElevationAt(new Vector2(-2.5f, 0f)), Eps, "left edge midpoint = (0+4)/2");
        }

        [Test]
        public void Bilinear_Centre_IsAverageOfAllFourCorners()
        {
            var f = Make2x2();
            // Dead centre (0,0): equal weight to all four → (0+2+4+6)/4 = 3.
            Assert.AreEqual(3f, f.ElevationAt(Vector2.zero), Eps, "field centre = mean of the four corners");
        }

        [Test]
        public void Bilinear_QuarterPoint_WeightsNearerTexelMore()
        {
            var f = Make2x2();
            // Along the bottom edge, 1/4 from BL toward BR. Texel centres are at x=-2.5 and +2.5 (span 5 m);
            // a quarter of the way is x = -2.5 + 1.25 = -1.25 → lerp(0,2,0.25) = 0.5.
            Assert.AreEqual(0.5f, f.ElevationAt(new Vector2(-1.25f, -2.5f)), Eps, "quarter weight to BR");
        }

        // ---- out-of-rect clamp: never throws, reads the edge ---------------------------------------

        [Test]
        public void OutOfRect_ClampsToEdgeTexel_NoThrow()
        {
            var f = Make2x2();
            // Far below-left of the rect → clamps to BL(0); far above-right → clamps to TR(6).
            Assert.AreEqual(0f, f.ElevationAt(new Vector2(-1000f, -1000f)), Eps, "below-left clamps to BL");
            Assert.AreEqual(6f, f.ElevationAt(new Vector2(1000f, 1000f)), Eps, "above-right clamps to TR");
            // Off one axis only (far left, mid height) clamps x to the left column → between BL(0)/TL(4) = 2.
            Assert.AreEqual(2f, f.ElevationAt(new Vector2(-1000f, 0f)), Eps, "far-left, mid-height clamps to left column");
        }

        // ---- world ↔ texel round-trip + the shader's world→uv mapping ------------------------------

        [Test]
        public void TexelToWorld_RoundTrips_ThroughElevationSampling()
        {
            // A 4×4 ascending field; sampling at each texel's world centre returns that texel's value.
            int res = 4;
            var elev = new float[res * res];
            for (int i = 0; i < elev.Length; i++) elev[i] = i;   // distinct per texel
            var f = new PaintedHeightField(elev, res, res, new Vector2(20f, -10f), new Vector2(40f, 40f));

            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                Vector2 world = f.TexelToWorld(x, y);
                Assert.AreEqual(elev[y * res + x], f.ElevationAt(world), Eps,
                    $"texel ({x},{y}) world centre samples its own authored value");
            }
        }

        [Test]
        public void WorldRect_IsCentredOnTheGivenCentre()
        {
            var f = new PaintedHeightField(new float[4], 2, 2, new Vector2(100f, 50f), new Vector2(160f, 120f));
            // Min corner = centre − size/2.
            Assert.AreEqual(new Vector2(20f, -10f), f.WorldMin, "world min = centre − size/2");
            Assert.AreEqual(new Vector2(160f, 120f), f.WorldSize, "world size preserved");
        }

        // ---- min/max ↔ normalized R mapping (encode/decode are inverses, clamped) -------------------

        [Test]
        public void DecodeElevation_MapsR0R1ToMinMax()
        {
            Assert.AreEqual(-4f, PaintedHeightField.DecodeElevation(0f, -4f, 6f), Eps, "R=0 → min");
            Assert.AreEqual(6f, PaintedHeightField.DecodeElevation(1f, -4f, 6f), Eps, "R=1 → max");
            Assert.AreEqual(1f, PaintedHeightField.DecodeElevation(0.5f, -4f, 6f), Eps, "R=0.5 → midpoint");
        }

        [Test]
        public void EncodeDecode_AreInverses_WithinRange()
        {
            const float min = -4f, max = 6f;
            foreach (float h in new[] { -4f, -1f, 0f, 1.6f, 3f, 6f })
            {
                float r = PaintedHeightField.EncodeElevation(h, min, max);
                Assert.AreEqual(h, PaintedHeightField.DecodeElevation(r, min, max), Eps,
                    $"encode∘decode round-trips elevation {h}");
            }
        }

        [Test]
        public void EncodeElevation_SaturatesOutsideRange()
        {
            const float min = -4f, max = 6f;
            Assert.AreEqual(0f, PaintedHeightField.EncodeElevation(-100f, min, max), Eps, "below min → 0 (clamped)");
            Assert.AreEqual(1f, PaintedHeightField.EncodeElevation(100f, min, max), Eps, "above max → 1 (clamped)");
        }

        // ---- determinism: same position → same elevation, every call --------------------------------

        [Test]
        public void ElevationAt_IsDeterministic_ForTheSamePosition()
        {
            var f = Make2x2();
            var pos = new Vector2(1.3f, -0.7f);
            float first = f.ElevationAt(pos);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(first, f.ElevationAt(pos), 0f, "no RNG — identical every call");
        }

        // ---- defensive construction: a short/null array doesn't throw -------------------------------

        [Test]
        public void Construction_WithBadInputs_IsSafe()
        {
            Assert.DoesNotThrow(() =>
            {
                var f = new PaintedHeightField(null, 2, 2, Vector2.zero, new Vector2(10f, 10f));
                f.ElevationAt(Vector2.zero);   // zero-filled fallback grid, no throw
            }, "null elevations → safe zero-filled field");

            Assert.DoesNotThrow(() =>
            {
                var f = new PaintedHeightField(new float[1], 4, 4, Vector2.zero, new Vector2(1f, 1f));
                f.ElevationAt(new Vector2(0.5f, 0.5f));
            }, "short elevation array → safe zero-filled field");
        }
    }
}
