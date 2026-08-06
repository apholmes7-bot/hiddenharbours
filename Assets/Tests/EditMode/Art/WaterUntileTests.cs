using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards for the 2026-08-06 SHORE-FOAM FINE-LINE defect — the owner's *"thin, non-organic
    /// straight lines"* in otherwise-good shore foam.
    ///
    /// <para><b>What it actually was.</b> Not the seabed's 8-bit quantization, not the swash, not the
    /// foam-edge dither — those were all investigated when the same symptom appeared on 2026-08-01
    /// and each got its own instrument. This one is the texture UNTILER. Its blend picked two
    /// variants per repeat-cell (the cell's hashed translation and the diagonal neighbour's) and
    /// mixed them by a weight that reaches neither 0 nor 1 at a cell edge. Crossing that edge
    /// replaces BOTH variants and steps the weight, so the blended value jumps — a hard seam every
    /// <c>1 / _PaintScale</c> metres (4 m as shipped), in both axes, on every painted water slot.
    /// The domain warp applied before the cell lookup BENDS those seams, which is why they read as
    /// wandering thin lines instead of as an obvious tile grid.</para>
    ///
    /// <para><b>How these tests are built to stay honest.</b> Continuity is a property of the
    /// LATTICE, not of any one texture, so the twin takes an arbitrary per-cell variant field and
    /// the tests hand it a hash. That means nothing here has to reproduce the shader's
    /// <c>Hash22</c>, and a test cannot pass by agreeing with a formula it also wrote. The old blend
    /// is kept in the twin as <c>LegacyTwoVariantWeight</c>/<c>LegacyBlend</c> — the
    /// <c>FoamBuffer.CameraRelativeOrigin</c> idiom — so the seam is reported as a MEASURED jump,
    /// not as an adjective, and a regression to it fails loudly. A source tripwire pins that the
    /// shipped shader carries the four-corner form.</para>
    /// </summary>
    public class WaterUntileTests
    {
        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        /// <summary>An arbitrary, strongly-varying per-cell variant field. It stands in for "what the
        /// tile looks like translated by this corner's hash" — decorrelated between neighbours, which
        /// is the worst case for a seam and the whole reason untiling works at all.</summary>
        private static float Variant(Vector2Int cell)
        {
            unchecked
            {
                uint h = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663);
                h ^= h >> 13;
                h *= 0x9E3779B1u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0xFFFFFF;
            }
        }

        private static string ReadRepoText(string repoRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", repoRelative);
            Assert.IsTrue(File.Exists(path), $"missing: {repoRelative}");
            return File.ReadAllText(path);
        }

        /// <summary>The largest jump either blend makes stepping across a cell boundary, sampled along
        /// the whole edge. Epsilon is far below the texel scale the shader works at, so a continuous
        /// blend reports ~0 and a discontinuous one reports the size of the seam it draws.</summary>
        private static float MaxBoundaryJump(Func<Vector2, float> blend, bool crossX)
        {
            const float eps = 1e-4f;
            float worst = 0f;
            // Walk several cells of the lattice, and the whole length of each boundary.
            for (int cell = -3; cell <= 3; cell++)
            {
                for (int s = 0; s <= 40; s++)
                {
                    float along = s / 40f;
                    Vector2 left = crossX ? new Vector2(cell + 1f - eps, along + cell * 0.13f)
                                          : new Vector2(along + cell * 0.13f, cell + 1f - eps);
                    Vector2 right = crossX ? new Vector2(cell + 1f + eps, along + cell * 0.13f)
                                           : new Vector2(along + cell * 0.13f, cell + 1f + eps);
                    worst = Mathf.Max(worst, Mathf.Abs(blend(left) - blend(right)));
                }
            }
            return worst;
        }

        // ==== the fix: the blend is continuous across every repeat-cell boundary =====================

        [Test]
        public void FourCornerBlend_IsContinuous_AcrossBothCellBoundaries()
        {
            float jumpX = MaxBoundaryJump(uv => WaterUntile.Blend(uv, Variant), crossX: true);
            float jumpY = MaxBoundaryJump(uv => WaterUntile.Blend(uv, Variant), crossX: false);

            // The only residual is the 1e-4 step itself running through a C1 ramp, which is orders
            // below anything a screen pixel could show.
            Assert.Less(jumpX, 1e-3f, $"x-boundary seam of {jumpX:F5} — the untiler is drawing lines again.");
            Assert.Less(jumpY, 1e-3f, $"y-boundary seam of {jumpY:F5} — the untiler is drawing lines again.");
        }

        [Test]
        public void TheShippedTwoVariantBlend_Jumped_WhichIsTheDefect()
        {
            // The measurement, not an adjective: the old blend's worst boundary jump. The variant
            // field is 0..1, so a jump of this size is a large fraction of the slot's whole range —
            // which is exactly why it survived every foam noise layered on top of it.
            float jumpX = MaxBoundaryJump(uv => WaterUntile.LegacyBlend(uv, Variant), crossX: true);
            float jumpY = MaxBoundaryJump(uv => WaterUntile.LegacyBlend(uv, Variant), crossX: false);

            Assert.Greater(jumpX, 0.2f, "the legacy blend is meant to be the WRONG answer; if it no " +
                                        "longer jumps, this guard has stopped measuring anything.");
            Assert.Greater(jumpY, 0.2f, "the legacy blend is meant to be the WRONG answer; if it no " +
                                        "longer jumps, this guard has stopped measuring anything.");

            // And the fix must be strictly better on the same field, by a wide margin.
            float fixedX = MaxBoundaryJump(uv => WaterUntile.Blend(uv, Variant), crossX: true);
            Assert.Less(fixedX * 100f, jumpX, "the four-corner blend must be orders quieter at the seam.");
        }

        // ==== the weights themselves ==================================================================

        [Test]
        public void CornerWeights_ArePartitionOfUnity_AndNonNegative()
        {
            for (int i = 0; i <= 10; i++)
            for (int j = 0; j <= 10; j++)
            {
                var frac = new Vector2(i / 10f, j / 10f);
                WaterUntile.CornerWeights(frac, out float w00, out float w10, out float w01, out float w11);

                Assert.GreaterOrEqual(w00, 0f); Assert.GreaterOrEqual(w10, 0f);
                Assert.GreaterOrEqual(w01, 0f); Assert.GreaterOrEqual(w11, 0f);
                // Sums to 1 everywhere: the blend can never brighten or darken the painted slot.
                Assert.AreEqual(1f, w00 + w10 + w01 + w11, 1e-5f, $"weights do not sum to 1 at {frac}.");
            }
        }

        [Test]
        public void CornerWeights_Vanish_OnTheEdgesTheirCornerDoesNotTouch()
        {
            // On the x = 0 edge, the two (i+1,.) corners must contribute NOTHING — that is the
            // property that lets two cells agree along the edge they share.
            for (int j = 0; j <= 10; j++)
            {
                WaterUntile.CornerWeights(new Vector2(0f, j / 10f),
                                          out _, out float w10, out _, out float w11);
                Assert.AreEqual(0f, w10, 1e-6f);
                Assert.AreEqual(0f, w11, 1e-6f);

                WaterUntile.CornerWeights(new Vector2(1f, j / 10f),
                                          out float w00, out _, out float w01, out _);
                Assert.AreEqual(0f, w00, 1e-6f);
                Assert.AreEqual(0f, w01, 1e-6f);
            }

            for (int i = 0; i <= 10; i++)
            {
                WaterUntile.CornerWeights(new Vector2(i / 10f, 0f),
                                          out _, out _, out float w01, out float w11);
                Assert.AreEqual(0f, w01, 1e-6f);
                Assert.AreEqual(0f, w11, 1e-6f);
            }
        }

        [Test]
        public void Blend_StillVaries_AcrossTheLattice_SoTheRepeatIsStillHidden()
        {
            // Continuity is worthless if it was bought by flattening the blend into one value — that
            // would be the tile repeat back, unbroken. Sample a spread of cells and demand real spread.
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 24; i++)
            for (int j = 0; j < 24; j++)
            {
                float v = WaterUntile.Blend(new Vector2(i * 0.37f, j * 0.41f), Variant);
                lo = Mathf.Min(lo, v);
                hi = Mathf.Max(hi, v);
            }
            Assert.Greater(hi - lo, 0.35f, "the untile blend has collapsed toward a constant — the " +
                                           "repeat would read straight through it.");
        }

        [Test]
        public void CellAndFrac_AgreeWithHlslFloorAndFrac_IncludingNegatives()
        {
            // The sea has negative world coordinates; HLSL floor/frac run away from zero there and a
            // C# twin that used truncation would disagree exactly where the owner sails.
            Assert.AreEqual(new Vector2Int(-2, -1), WaterUntile.CellOf(new Vector2(-1.25f, -0.5f)));
            Assert.AreEqual(0.75f, WaterUntile.FracOf(new Vector2(-1.25f, -0.5f)).x, 1e-5f);
            Assert.AreEqual(0.5f, WaterUntile.FracOf(new Vector2(-1.25f, -0.5f)).y, 1e-5f);
        }

        // ==== the source tripwire: the shipped shader must carry the fix ==============================

        [Test]
        public void WaterShader_UsesTheFourCornerBlend_AndNotTheTwoVariantOne()
        {
            string src = ReadRepoText(WaterShaderPath);

            StringAssert.Contains("float2 off00 = Hash22(iuv + float2(0.0, 0.0))", src,
                "the untiler must sample the four CELL CORNERS.");
            StringAssert.Contains("float2 off10 = Hash22(iuv + float2(1.0, 0.0))", src);
            StringAssert.Contains("float2 off01 = Hash22(iuv + float2(0.0, 1.0))", src);
            StringAssert.Contains("float2 off11 = Hash22(iuv + float2(1.0, 1.0))", src);
            StringAssert.Contains("lerp(lerp(v00, v10, bx), lerp(v01, v11, bx), by)", src,
                "the four corner taps must be combined BILINEARLY, or the seam comes back.");

            // The exact line that drew the lines. Its return would be silent on CI (no graphics
            // device sees a seam), so it is named here.
            StringAssert.DoesNotContain("smoothstep(0.2, 0.8, fuv.x) * 0.5 + smoothstep(0.2, 0.8, fuv.y) * 0.5",
                src, "the two-variant untile weight is the 2026-08-06 shore-foam seam. Do not restore it.");
        }
    }
}
