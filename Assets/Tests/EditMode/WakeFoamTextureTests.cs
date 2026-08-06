using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards for the 2026-08-06 BUBBLING-FOAM pass — the owner's *"the wake foam should BUBBLE, not
    /// paint a solid line"*.
    ///
    /// <para><b>Why the fix had to be the matter and not the tuning.</b> The trail's placement laws are
    /// deliberately dense: deposits every 0.55 m, churn puffs sized to overlap, arm crests long enough
    /// to fuse by construction. All three were put there by the PREVIOUS playtest, which called the
    /// wake dotted. Loosen them and that read comes straight back; keep them and a solid disc can only
    /// ever paint a stripe. So the unit of foam changed instead: a raft of bubble films with real holes
    /// through it. These tests pin the STRUCTURAL difference — a raft is not monotone from its centre,
    /// a disc is — rather than any particular opacity, which is tuning and will move.</para>
    ///
    /// <para>Whether the wake LOOKS like foam is the owner's eyeball; no test asserts that, and none
    /// should.</para>
    /// </summary>
    public class WakeFoamTextureTests
    {
        private const int Size = WakeFoamTexture.PuffPixels;

        [Test]
        public void SolidDisc_IsMonotoneFromItsCentre_WhichIsWhyItPaintsAStripe()
        {
            float[] disc = WakeFoamTexture.SolidPuffCoverage(Size);
            int reversals = WakeFoamTexture.RadialReversals(disc, Size);
            Assert.AreEqual(0, reversals,
                "the shipped puff was a plain round falloff; if it now reverses, this baseline has " +
                "stopped being the thing the raft is measured against.");
        }

        [Test]
        public void Raft_IsAerated_ItCrossesRimsAndGapsOnTheWayOut()
        {
            int totalReversals = 0;
            for (int v = 0; v < WakeFoamTexture.VariantCount; v++)
            {
                float[] raft = WakeFoamTexture.BuildRaftCoverage(v, Size, 1f);
                int reversals = WakeFoamTexture.RadialReversals(raft, Size);
                Assert.Greater(reversals, 8,
                    $"raft {v} falls off like a disc — it has no holes, so a dense trail of it is a " +
                    "painted stripe again.");
                totalReversals += reversals;
            }
            Assert.Greater(totalReversals, 60, "the whole set must be aerated, not one lucky variant.");
        }

        [Test]
        public void Raft_StillCovers_ItIsFoamNotConfetti()
        {
            // Aeration is worthless if it thinned the puff to nothing: the churn band has to read as
            // white water, and a raft that covers almost no texels would just be a faint speckle.
            for (int v = 0; v < WakeFoamTexture.VariantCount; v++)
            {
                float[] raft = WakeFoamTexture.BuildRaftCoverage(v, Size, 1f);
                int inked = 0, peak = 0;
                for (int i = 0; i < raft.Length; i++)
                {
                    if (raft[i] > 0.25f) inked++;
                    if (raft[i] > 0.8f) peak++;
                }
                Assert.Greater(inked, raft.Length * 0.20f, $"raft {v} covers almost nothing.");
                Assert.Greater(peak, 4, $"raft {v} has no dense film anywhere — it would read as haze.");
            }
        }

        [Test]
        public void Aeration0_IsTheShippedSolidDisc_BitForBit()
        {
            // The honest off switch: WakeTrailConfig.FoamAeration = 0 must revert the whole change.
            float[] disc = WakeFoamTexture.SolidPuffCoverage(Size);
            for (int v = 0; v < WakeFoamTexture.VariantCount; v++)
            {
                float[] off = WakeFoamTexture.BuildRaftCoverage(v, Size, 0f);
                CollectionAssert.AreEqual(disc, off,
                    "aeration 0 must return the shipped disc EXACTLY, or the A/B is not an A/B.");
            }
        }

        [Test]
        public void Rafts_AreDeterministic_AndDifferFromEachOther()
        {
            for (int v = 0; v < WakeFoamTexture.VariantCount; v++)
            {
                CollectionAssert.AreEqual(WakeFoamTexture.BuildRaftCoverage(v, Size, 1f),
                                          WakeFoamTexture.BuildRaftCoverage(v, Size, 1f),
                                          "the same variant must build the same raft, always (rule 5).");
            }

            for (int a = 0; a < WakeFoamTexture.VariantCount; a++)
            for (int b = a + 1; b < WakeFoamTexture.VariantCount; b++)
            {
                float[] fa = WakeFoamTexture.BuildRaftCoverage(a, Size, 1f);
                float[] fb = WakeFoamTexture.BuildRaftCoverage(b, Size, 1f);
                float diff = 0f;
                for (int i = 0; i < fa.Length; i++) diff += Mathf.Abs(fa[i] - fb[i]);
                Assert.Greater(diff / fa.Length, 0.03f,
                    $"rafts {a} and {b} are near-identical — a long trail would stamp one shape.");
            }
        }

        [Test]
        public void SlotAssignment_SpreadsTheVariants_AndIsStable()
        {
            // Particles recycle round-robin through the pool slots, so consecutive deposits take
            // consecutive slots: if the slot mapping were a plain modulo the trail would march through
            // the variants in a visible cycle. Every variant must still get used.
            var used = new int[WakeFoamTexture.VariantCount];
            for (int slot = 0; slot < 96; slot++)
            {
                int variant = WakeFoamTexture.VariantForSlot(slot);
                Assert.GreaterOrEqual(variant, 0);
                Assert.Less(variant, WakeFoamTexture.VariantCount);
                used[variant]++;
                Assert.AreEqual(variant, WakeFoamTexture.VariantForSlot(slot), "slot mapping must be stable.");
            }
            for (int v = 0; v < used.Length; v++)
                Assert.Greater(used[v], 0, $"variant {v} is never drawn over a 96-slot pool.");

            // And the mirrors must actually vary, or the flip buys no extra variety.
            int flipped = 0;
            for (int slot = 0; slot < 64; slot++)
            {
                WakeFoamTexture.MirrorForSlot(slot, out bool fx, out bool fy);
                if (fx) flipped++;
                if (fy) flipped++;
            }
            Assert.Greater(flipped, 20, "the per-slot mirrors barely vary.");
            Assert.Less(flipped, 108, "the per-slot mirrors barely vary.");
        }

        // ==== the WAKE WAVE's drawn profile ==========================================================

        [Test]
        public void WaveProfile_IsACrestAndAHollow_NotAWhiteBar()
        {
            // The point of replacing the plume decal: displaced water reads as light AND shade laid
            // across the travel direction (the sea shader's own swell idiom), never as one bright bar.
            WakeFoamTexture.WaveProfile(0.95f, out float crestA, out float crestShade);
            WakeFoamTexture.WaveProfile(0.05f, out float troughA, out float troughShade);
            WakeFoamTexture.WaveProfile(WakeFoamTexture.WaveWaterlineV, out float lineA, out float lineShade);

            Assert.Greater(crestShade, 0.5f, "the leading side must read as a LIT crest.");
            Assert.Less(troughShade, -0.5f, "the trailing side must read as a HOLLOW, or it is foam again.");
            Assert.AreEqual(0f, lineShade, 1e-4f, "the waterline is where the shading crosses over.");
            Assert.AreEqual(0f, lineA, 1e-3f, "the profile must be transparent AT the waterline...");
            Assert.Greater(crestA, 0.1f, "...and opaque through the crest...");
            Assert.Greater(troughA, 0.1f, "...and through the hollow.");
        }

        [Test]
        public void WaveProfile_HasSoftFeet_SoOverlappingCrestsFuse()
        {
            // Hard edges at the extremes would stack as visible seams where crests overlap — the exact
            // failure the arm overlap law exists to avoid on the other axis.
            WakeFoamTexture.WaveProfile(1f, out float top, out _);
            WakeFoamTexture.WaveProfile(0f, out float bottom, out _);
            Assert.Less(top, 0.35f, "the crest's lip must soften, not end in a ruled edge.");
            Assert.Less(bottom, 0.05f, "the hollow must fade out, not end in a ruled edge.");
        }

        [Test]
        public void CrestUndulation_IsBounded_AndZeroAmountIsExactlyOne()
        {
            Assert.AreEqual(1f, WakeFoamTexture.CrestUndulation(0.37f, 0f), 1e-6f,
                "amount 0 must be a ruled bar exactly — the A/B.");
            for (int i = 0; i <= 200; i++)
            {
                float u = i / 200f;
                float w = WakeFoamTexture.CrestUndulation(u, 0.3f);
                Assert.GreaterOrEqual(w, 0.7f - 1e-4f, "the undulation must never erase the crest.");
                Assert.LessOrEqual(w, 1.3f + 1e-4f, "the undulation must never blow the crest up.");
            }
        }

        [Test]
        public void LengthTaper_IsSymmetric_FullInTheMiddle_ZeroAtBothEnds()
        {
            Assert.AreEqual(0f, WakeFoamTexture.LengthTaper(0f), 1e-6f);
            Assert.AreEqual(0f, WakeFoamTexture.LengthTaper(1f), 1e-6f);
            Assert.AreEqual(1f, WakeFoamTexture.LengthTaper(0.5f), 1e-6f);
            Assert.AreEqual(WakeFoamTexture.LengthTaper(0.2f), WakeFoamTexture.LengthTaper(0.8f), 1e-6f);
        }
    }
}
