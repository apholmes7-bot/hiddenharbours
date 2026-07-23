using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>SEE==FEEL — the sea's push matches what you see</b> (ADR 0023 phase 3; owner ruling
    /// 2026-07-23, verbatim "Yes seas push should match"): while the displaced sea is active, the
    /// seakeeping FORCE reads the same displaced (exaggerated + shore-faded) height the player sees
    /// — <see cref="SeakeepingForcesMath.DisplacedForceScale"/> on the surface's published state,
    /// applied to the resolved force. These pin the LAWS, SharedHeaveTests-style, without
    /// re-deriving the field:
    /// <list type="bullet">
    /// <item>OFF byte-identity — no active sea ⇒ scale exactly 1 (the A/B contract extends to physics);</item>
    /// <item>open water ⇒ the force is the sim force × the published exaggeration, exactly;</item>
    /// <item>the shallows ⇒ × the surface's own <see cref="ShoreFadeMath.Fade01"/> factor — the boat
    ///   FEELS the calm it can SEE inside the shore-fade band;</item>
    /// <item>linearity in the published exaggeration (how a live owner tune reaches handling);</item>
    /// <item>the equivalence that makes output-scaling legitimate: <see cref="SeakeepingForcesMath.Resolve"/>
    ///   is linear in the sample's amplitude, so force × scale == Resolve(displaced sample).</item>
    /// </list>
    /// Pure, headless, deterministic — no scene, no physics step, no RNG (rule 5).
    /// </summary>
    public class SeeEqualsFeelForcesTests
    {
        private const float Eps = 1e-5f;
        private const float Exag = 1.5f;
        private const float Band = 0.6f;

        private static readonly Vector2 North = Vector2.up;
        private static readonly SeakeepingResponse RefResponse = SeakeepingForcesMath.ResponseFrom(1f, 1f, 0f);

        private static SeakeepingSettings Settings() => SeakeepingSettings.Default;

        /// <summary>A generic sample exercising every branch at once: head + beam slope components.</summary>
        private static WaveSample MixedWave(float scale = 1f)
            => new WaveSample(0.4f * scale, new Vector2(0.35f, 0.2f) * scale, 0f);

        // ---- OFF byte-identity (the A/B contract extends to physics) --------------------------------

        [Test]
        public void DisplacedOff_ScaleIsExactlyOne_ForceByteIdentical()
        {
            // displacedActive false must return EXACTLY 1 whatever the (stale) state carries — the
            // raw-sim force path untouched, bit-for-bit.
            var stale = new DisplacedSeaState(3f, 2f);
            Assert.AreEqual(1f, SeakeepingForcesMath.DisplacedForceScale(false, 5f, in stale), 0f,
                "displaced OFF must scale by exactly 1 — the A/B contract extends to physics");
            Assert.AreEqual(1f, SeakeepingForcesMath.DisplacedForceScale(false, float.PositiveInfinity, in stale), 0f);
            Assert.AreEqual(1f, SeakeepingForcesMath.DisplacedForceScale(false, float.NaN, in stale), 0f,
                "even a NaN depth must not disturb the OFF side — the scale is not consulted there");
        }

        // ---- ON: open water feels the full exaggeration ---------------------------------------------

        [Test]
        public void DisplacedOn_OpenWater_ForceIsSimForceTimesExaggeration()
        {
            // Open water (no seabed map ⇒ depth +∞ ⇒ fade 1): the push is the sim push × the
            // published exaggeration, exactly — the ×1.5 drama offshore, felt as well as seen.
            var state = new DisplacedSeaState(Exag, Band);
            float scale = SeakeepingForcesMath.DisplacedForceScale(true, float.PositiveInfinity, in state);
            Assert.AreEqual(Exag, scale, 0f, "open water = full fade = the bare exaggeration");

            // And past the band's depth the fade is exactly 1 too — the seam is invisible offshore.
            Assert.AreEqual(Exag, SeakeepingForcesMath.DisplacedForceScale(true, Band, in state), Eps);
            Assert.AreEqual(Exag, SeakeepingForcesMath.DisplacedForceScale(true, Band * 10f, in state), Eps);
        }

        // ---- ON: the shallows feel the calm they show -----------------------------------------------

        [Test]
        public void DisplacedOn_InTheShallows_ScaleIsTheSurfacesOwnShoreFade()
        {
            // Strictly inside the band the force factor must be exaggeration × the EXACT Fade01 the
            // surface's vertex stage (and the visual ride) fades with — no second seam, ever.
            var state = new DisplacedSeaState(Exag, Band);
            float depth = Band / 4f;
            float fade = ShoreFadeMath.Fade01(depth, Band);
            Assert.Greater(fade, 0.01f);
            Assert.Less(fade, 0.99f, "pick a depth strictly inside the band or this is vacuous");

            Assert.AreEqual(Exag * fade, SeakeepingForcesMath.DisplacedForceScale(true, depth, in state), Eps,
                "the shallow-water push must fade by the surface's own Fade01 — the boat FEELS the calm it SEES");
        }

        [Test]
        public void DisplacedOn_AtOrBeyondTheWaterline_NoPushAtAll()
        {
            var state = new DisplacedSeaState(Exag, Band);
            Assert.AreEqual(0f, SeakeepingForcesMath.DisplacedForceScale(true, 0f, in state), 0f,
                "at the waterline the visible sea is flat — the felt sea must be too");
            Assert.AreEqual(0f, SeakeepingForcesMath.DisplacedForceScale(true, -1f, in state), 0f,
                "dry ground never pushes");
        }

        // ---- linearity in the published exaggeration (the live-tune path) ---------------------------

        [Test]
        public void DisplacedOn_LinearInThePublishedExaggeration()
        {
            // scale(2E) = 2 × scale(E) at every depth — exactly how an owner's live exaggeration
            // edit reaches handling: the surface re-publishes, the boat's next tick pushes harder.
            var e1 = new DisplacedSeaState(Exag, Band);
            var e2 = new DisplacedSeaState(Exag * 2f, Band);
            foreach (float depth in new[] { Band / 8f, Band / 3f, Band, 5f, float.PositiveInfinity })
                Assert.AreEqual(
                    SeakeepingForcesMath.DisplacedForceScale(true, depth, in e1) * 2f,
                    SeakeepingForcesMath.DisplacedForceScale(true, depth, in e2), Eps,
                    $"depth {depth}: the force factor must be linear in the ONE published exaggeration");
        }

        [Test]
        public void DisplacedOn_ZeroExaggeration_ZeroPush_NegativeClamps()
        {
            // Exaggeration 0 = a visually flat displaced sea = no push (see==feel at the limit);
            // a negative published value clamps to 0, never a sign-flipped force.
            var zero = new DisplacedSeaState(0f, Band);
            var neg = new DisplacedSeaState(-2f, Band);
            Assert.AreEqual(0f, SeakeepingForcesMath.DisplacedForceScale(true, 5f, in zero), 0f);
            Assert.AreEqual(0f, SeakeepingForcesMath.DisplacedForceScale(true, 5f, in neg), 0f,
                "a negative exaggeration must clamp to 0, never reverse the sea");
        }

        [Test]
        public void NaNDepth_FailsSafeToNoForce()
        {
            var state = new DisplacedSeaState(Exag, Band);
            float scale = SeakeepingForcesMath.DisplacedForceScale(true, float.NaN, in state);
            Assert.AreEqual(0f, scale, 0f, "a NaN depth never leaks into the force — fail safe, like Exposure01");
        }

        // ---- the equivalence law: scaling the output IS reading the displaced height ----------------

        [Test]
        public void ScalingTheResolvedForce_EqualsResolvingTheDisplacedField()
        {
            // Resolve is linear in the sample's amplitude (force and torque are proportional to the
            // slope, and the displaced field's slope is the sim slope × the pointwise factor). So
            // Resolve(sim) × factor must equal Resolve(displaced sample), componentwise — this is
            // what makes the controller's output-scaling exactly "the forces read the displaced
            // height". Checked below and above 1, on a heading that mixes head + beam + yaw terms.
            Vector2 heading = new Vector2(0.6f, 0.8f);
            foreach (float factor in new[] { 0.25f, 0.75f, 1.5f, 3f })
            {
                var sim = MixedWave();
                var displaced = MixedWave(factor);
                var scaled = SeakeepingForcesMath.Resolve(in sim, heading, 1f, 0.75f, in RefResponse, Settings());
                var direct = SeakeepingForcesMath.Resolve(in displaced, heading, 1f, 0.75f, in RefResponse, Settings());

                Assert.AreEqual(scaled.Force.x * factor, direct.Force.x, Eps,
                    $"factor {factor}: force.x must be linear in the field amplitude");
                Assert.AreEqual(scaled.Force.y * factor, direct.Force.y, Eps,
                    $"factor {factor}: force.y must be linear in the field amplitude");
                Assert.AreEqual(scaled.Torque * factor, direct.Torque, Eps,
                    $"factor {factor}: torque must be linear in the field amplitude");
            }
        }

        [Test]
        public void FullPipeline_DisplacedForce_IsDeterministic()
        {
            // (wind, seaState, field) → trains → sample → resolve → displaced scale, twice:
            // bit-identical (rule 5 — the scale is recomputed from published state, nothing saved).
            var state = new DisplacedSeaState(Exag, Band);
            Vector2 wind = new Vector2(-4.2f, 7.1f);
            Vector2 pos = new Vector2(310.5f, -12.25f);
            const float sea = 0.65f;
            const double t = 12345.678;
            const float depth = 0.35f;

            float ForceX()
            {
                var trains = WaveMath.TrainsFrom(wind, sea, WaveFieldSettings.Default);
                var wave = WaveMath.Sample(pos, t, in trains);
                var f = SeakeepingForcesMath.Resolve(in wave, North, 0.8f, sea, in RefResponse, Settings());
                return f.Force.x * SeakeepingForcesMath.DisplacedForceScale(true, depth, in state);
            }

            Assert.AreEqual(ForceX(), ForceX(), 0f, "same inputs, same displaced force, bit-exact");
        }
    }
}
