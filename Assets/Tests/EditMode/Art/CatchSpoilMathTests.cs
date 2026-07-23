using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The rot recipe's pure contracts: identity at spoil 0, keylines staying dark, deterministic
    /// green convergence, and the dither mottle actually varying by pixel position. Byte-level
    /// parity with the live rig's <c>tintSpoil</c> is pinned separately in
    /// <c>CatchStorageBakeTests</c> (RigBaking suite, V8 host).
    /// </summary>
    public class CatchSpoilMathTests
    {
        private static byte[] SolidBuffer(int w, int h, byte r, byte g, byte b, byte a = 255)
        {
            var rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                rgba[i * 4] = r; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = b; rgba[i * 4 + 3] = a;
            }
            return rgba;
        }

        [Test]
        public void SpoilZero_ReturnsTheInputUntouched()
        {
            byte[] src = SolidBuffer(8, 8, 200, 180, 160);
            byte[] outp = CatchSpoilMath.Tint(src, 8, 8, 0.0);
            Assert.AreSame(src, outp, "spoil 0 must be a no-op passthrough, like the rig");
        }

        [Test]
        public void Keylines_StayDark()
        {
            // r+g+b < 70 = a keyline pixel; the rot must never wash the linework green.
            byte[] src = SolidBuffer(4, 4, 16, 26, 25);   // the kit's #101a19 key
            byte[] outp = CatchSpoilMath.Tint(src, 4, 4, 1.0);
            CollectionAssert.AreEqual(src, outp, "keyline pixels must survive full rot untouched");
        }

        [Test]
        public void TransparentPixels_StayTransparent()
        {
            byte[] src = SolidBuffer(4, 4, 200, 200, 200, a: 0);
            byte[] outp = CatchSpoilMath.Tint(src, 4, 4, 0.8);
            CollectionAssert.AreEqual(src, outp);
        }

        [Test]
        public void Tint_MovesBrightPixelsTowardTheRotGreen_MonotonicallyWithSpoil()
        {
            // A bright grey pixel: green channel should climb toward 154's blend, red fall toward
            // 125's, and more spoil = more shift.
            byte[] src = SolidBuffer(4, 4, 220, 220, 220);
            byte[] half = CatchSpoilMath.Tint(src, 4, 4, 0.5);
            byte[] full = CatchSpoilMath.Tint(src, 4, 4, 1.0);

            for (int i = 0; i < 16; i++)
            {
                Assert.Less(half[i * 4], src[i * 4], "red must fall toward 125");
                Assert.Less(full[i * 4], half[i * 4], "more spoil, more shift");
                Assert.Less(full[i * 4 + 2], half[i * 4 + 2], "blue must keep falling toward 70");
            }
        }

        [Test]
        public void Mottle_VariesByPixelPosition_AtPartialSpoil()
        {
            // At spoil 0.5 the Bayer threshold (0.275) selects some cells of the 4×4 tile and not
            // others — a uniform input must come out NON-uniform (that speckle is the rot look).
            byte[] src = SolidBuffer(4, 4, 220, 220, 220);
            byte[] outp = CatchSpoilMath.Tint(src, 4, 4, 0.5);

            bool anyDiffer = false;
            for (int i = 1; i < 16 && !anyDiffer; i++)
                anyDiffer = outp[i * 4] != outp[0] || outp[i * 4 + 1] != outp[1];
            Assert.IsTrue(anyDiffer, "partial spoil must dither-mottle, not flat-tint");
        }

        [Test]
        public void Mottle_IsDeterministic()
        {
            byte[] src = SolidBuffer(8, 8, 190, 170, 150);
            CollectionAssert.AreEqual(CatchSpoilMath.Tint(src, 8, 8, 0.7),
                                      CatchSpoilMath.Tint(src, 8, 8, 0.7));
        }

        [Test]
        public void MixAt_FollowsTheRecipeShape()
        {
            Assert.AreEqual(0.0, CatchSpoilMath.MixAt(0, 0, 0.0), 1e-12);
            // Bayer[0][0] = 0.5/16 = 0.03125 — under 0.55·spoil for any spoil > ~0.057, so this
            // pixel carries the mottle bonus: m = spoil·(0.40+0.28).
            Assert.AreEqual(0.5 * 0.68, CatchSpoilMath.MixAt(0, 0, 0.5), 1e-12);
            // Bayer[3][0] (x=3,y=0) = 15.5/16 = 0.96875 — never under 0.55, so base term only.
            Assert.AreEqual(0.5 * 0.40, CatchSpoilMath.MixAt(3, 0, 0.5), 1e-12);
        }

        [Test]
        public void RendererTint_IsWhiteWhenFresh_AndTheUniformTermWhenRotten()
        {
            Assert.AreEqual(Color.white, CatchSpoilMath.RendererTint(0.0));

            // Full rot: m = 0.40 → each channel (1−m) + green01·m — the recipe's uniform term.
            Color full = CatchSpoilMath.RendererTint(1.0);
            Assert.AreEqual(0.6f + 125f / 255f * 0.4f, full.r, 1e-5f);
            Assert.AreEqual(0.6f + 154f / 255f * 0.4f, full.g, 1e-5f);
            Assert.AreEqual(0.6f + 70f / 255f * 0.4f, full.b, 1e-5f);
            Assert.AreEqual(1f, full.a);

            // Out-of-range inputs clamp rather than over-rot.
            Assert.AreEqual(full, CatchSpoilMath.RendererTint(3.0));
            Assert.AreEqual(Color.white, CatchSpoilMath.RendererTint(-1.0));
        }
    }
}
