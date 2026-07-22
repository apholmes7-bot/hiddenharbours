using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the mesh-hull maths (ADR 0022 phase 3) — the conventions a GPU test
    /// cannot police on CI. Two convention traps live here and both have bitten before:
    /// the object matrix is a REFLECTION of the rig's right-handed frame (so the transform
    /// decomposition and the light z-flip must agree with it), and the keyline darkening is
    /// RINDEX-faithful (aliased ramps, later-wins), not "two steps down my own ramp".
    /// </summary>
    public class IsoFacetMathTests
    {
        const double Elev = 40.0;

        [Test]
        public void RigToWorld_AtCardinalHeadings_MatchesTheSpikeMatrix()
        {
            // The spike's ObjectMatrix, hand-built and MEASURED against the rig (ADR 0022):
            //   row0 = (ct, -st, 0), row1 = (se·st, se·ct, ce), row2 = (ce·st, ce·ct, -se).
            foreach (double dir in new[] { 0.0, 1.0, 2.5, 5.0, 7.25 })
            {
                float th = (float)(dir * Mathf.PI / 4f);
                float ct = Mathf.Cos(th), st = Mathf.Sin(th);
                float se = Mathf.Sin((float)(Elev * Mathf.Deg2Rad)), ce = Mathf.Cos((float)(Elev * Mathf.Deg2Rad));

                Matrix4x4 m = IsoFacetMath.RigToWorld(dir, Elev);
                AssertRow(m, 0, ct, -st, 0f, dir);
                AssertRow(m, 1, se * st, se * ct, ce, dir);
                AssertRow(m, 2, ce * st, ce * ct, -se, dir);
            }
        }

        static void AssertRow(Matrix4x4 m, int row, float a, float b, float c, double dir)
        {
            Assert.AreEqual(a, m[row, 0], 1e-5f, $"dir {dir} row {row} col 0");
            Assert.AreEqual(b, m[row, 1], 1e-5f, $"dir {dir} row {row} col 1");
            Assert.AreEqual(c, m[row, 2], 1e-5f, $"dir {dir} row {row} col 2");
        }

        [Test]
        public void RigToWorld_IsAReflection_AndTheTransformDecompositionReassemblesIt()
        {
            foreach (var (dir, roll, pitch) in new[]
                     { (0.0, 0.0, 0.0), (1.5, 0.0, 0.0), (3.0, 2.8, 1.6), (6.25, -2.8, -1.6) })
            {
                Matrix4x4 m = IsoFacetMath.RigToWorld(dir, Elev, roll, pitch);
                Assert.AreEqual(-1f, m.determinant, 1e-4f,
                    "The rig→world map must be a REFLECTION (det −1): the rigs are right-handed " +
                    "z-up, Unity's 2D camera frame is left-handed. If this became a proper " +
                    "rotation the hull renders MIRRORED — the exact defect class of the " +
                    "iso-art-baked-counter-clockwise saga.");

                // rotation · diag(1,1,−1) must reassemble the matrix EXACTLY — that is what the
                // component assigns to the transform (localRotation + localScale).
                Matrix4x4 re = Matrix4x4.TRS(
                    Vector3.zero,
                    IsoFacetMath.HullRotation(dir, Elev, roll, pitch),
                    IsoFacetMath.HullScale);
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        Assert.AreEqual(m[r, c], re[r, c], 2e-4f,
                            $"dir {dir} roll {roll} pitch {pitch}: decomposition drifted at [{r},{c}] — " +
                            "the transform no longer reproduces the rig projection.");
            }
        }

        [Test]
        public void ShaderLightVector_NegatesZ_ForTheReflectedFrame()
        {
            var ln = new Vector3(-0.42f, 0.72f, 0.52f);
            Vector4 v = IsoFacetMath.ShaderLightVector(ln);
            Assert.AreEqual(ln.x, v.x, 1e-6f);
            Assert.AreEqual(ln.y, v.y, 1e-6f);
            Assert.AreEqual(-ln.z, v.z, 1e-6f,
                "The spike MEASURED this sign: the object matrix reflects the rig's frame, so " +
                "the third shade term flips. Un-flipping it lights every hull from the wrong side.");
        }

        [Test]
        public void HeaveOffset_IsPixelsOverPpu_Upward()
        {
            Vector3 off = IsoFacetMath.HeaveOffset(1.2, 32);
            Assert.AreEqual(0f, off.x);
            Assert.AreEqual(1.2f / 32f, off.y, 1e-6f, "The rig subtracts heave from SCREEN y (down), " +
                "so positive heave lifts the hull in world y (up).");
            Assert.AreEqual(0f, off.z);
        }

        // ------------------------------------------------------------------ the darkening LUT

        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        [Test]
        public void BuildDarkenedRamps_DarkensTwoStepsDownTheOwnRamp_InTheSimpleCase()
        {
            var ramp = new[] { C(10, 10, 10), C(60, 60, 60), C(120, 120, 120), C(200, 200, 200) };
            var lut = IsoFacetMath.BuildDarkenedRamps(new[] { ramp });

            Assert.AreEqual(ramp[0], lut[0][0], "Index 0 resolves to index 0 — the rig does not darken it.");
            Assert.AreEqual(ramp[0], lut[0][1], "max(0, 1−2) = 0.");
            Assert.AreEqual(ramp[0], lut[0][2]);
            Assert.AreEqual(ramp[1], lut[0][3]);
        }

        [Test]
        public void BuildDarkenedRamps_ResolvesSharedColoursThroughTheLaterRamp_LikeRindex()
        {
            // The shared colour (120,120,120) sits at index 2 of ramp A and index 3 of ramp B.
            // The rig's RINDEX is built colour→(ramp,index) with LATER assignments winning, so a
            // pixel of that colour — whichever material drew it — darkens down ramp B: B[1].
            var rampA = new[] { C(10, 10, 10), C(60, 60, 60), C(120, 120, 120), C(200, 200, 200) };
            var rampB = new[] { C(5, 5, 40), C(30, 30, 80), C(70, 70, 130), C(120, 120, 120) };
            var lut = IsoFacetMath.BuildDarkenedRamps(new[] { rampA, rampB });

            Assert.AreEqual(rampB[1], lut[0][2],
                "Ramp A's pixel at the SHARED colour must darken down ramp B (later wins) — " +
                "this is the rig's RINDEX resolving by COLOUR, not by drawing material. " +
                "'Two steps down my own ramp' is the plausible-looking wrong answer.");
            Assert.AreEqual(rampB[1], lut[1][3], "Ramp B's own pixel darkens down ramp B.");
        }

        [Test]
        public void BuildDarkenedRamps_AliasedRampContent_CollapsesToOneRampLikeTheRig()
        {
            // blk/dark/boot alias one ramp array in the rigs; extraction flattens identity, so
            // dedupe must be by CONTENT. If aliases counted as distinct ramps, later copies would
            // shadow the original at a DIFFERENT ramp index and shared-colour resolution breaks.
            var ramp = new[] { C(10, 10, 10), C(60, 60, 60), C(120, 120, 120) };
            var alias = new[] { C(10, 10, 10), C(60, 60, 60), C(120, 120, 120) };
            var other = new[] { C(200, 0, 0), C(230, 60, 60), C(255, 120, 120) };
            var lut = IsoFacetMath.BuildDarkenedRamps(new[] { ramp, alias, other });

            Assert.AreEqual(ramp[0], lut[1][2], "The alias darkens down the ONE deduped ramp.");
            Assert.AreEqual(other[0], lut[2][2], "Unrelated ramps keep their own neighbourhood.");
        }
    }
}
