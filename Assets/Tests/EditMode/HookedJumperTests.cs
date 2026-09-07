using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// A HOOKED JUMPER JUMPS (owner's ruling 2026-09-06, PR 2b) — the shared jump law, the Core seam
    /// that carries "does this fish clear the water" to a presenter that may not know what a Def is,
    /// and the authored cadences.
    ///
    /// <para><b>The load-bearing claim is that there is only ONE jump.</b> The shoal's fish and the fish
    /// on the line leave the water on the same arc, over the same six frames, travelling the same
    /// 0.55 m — because both read <see cref="FishJumpArc"/> and <c>ShoalEventMath</c> now forwards to
    /// it rather than keeping a second copy. The first test below is what would go red if anyone
    /// re-transcribed either half.</para>
    /// </summary>
    public class HookedJumperTests
    {
        [SetUp]
        public void SetUp() { GameServices.Reset(); FishSpeciesRegistry.Reset(); }

        [TearDown]
        public void TearDown() { GameServices.Reset(); FishSpeciesRegistry.Reset(); }

        private static FishSpeciesDef Species(string id, FishFlags flags, float period = 0f)
        {
            var d = ScriptableObject.CreateInstance<FishSpeciesDef>();
            d.Id = id;
            d.BehaviorFlags = flags;
            d.FightJumpPeriodSeconds = period;
            return d;
        }

        // ---- one law, two drawers -----------------------------------------------------------------

        /// <summary>The shoal's jump and the hooked fish's are the SAME jump, term for term. Both sides
        /// are read here; a second transcription of either would part them.</summary>
        [Test]
        public void TheShoalAndTheLine_ShareOneJumpLaw()
        {
            Assert.AreEqual(FishJumpArc.Frames, ShoalEventMath.JumpFrames, "frames");
            Assert.AreEqual(FishJumpArc.FrameMs, ShoalEventMath.JumpFrameMs, 1e-9, "frame ms");
            Assert.AreEqual(FishJumpArc.TravelMetres, ShoalEventMath.JumpTravelMetres, 1e-6f, "travel");

            // ...and the arc itself, sampled across a whole jump.
            double d = FishJumpArc.DurationSeconds;
            for (int i = 0; i <= 20; i++)
            {
                double t = d * i / 20.0;
                Assert.AreEqual(FishJumpArc.Arc01(0.0, t), ShoalEventMath.JumpArc01(0.0, t), 1e-6f,
                    $"the two drawers disagree about the arc at t={t:F3}");
            }
        }

        /// <summary>The rig is the oracle: 6 frames at 95 ms and 0.55 m of travel are
        /// <c>fishIsoRig2.js</c>'s own <c>ANIMS.jump</c> / <c>MOTION.jump.travel</c>, restated as
        /// literals so a drifted port goes red rather than quietly redrawing the breach.</summary>
        [Test]
        public void TheJumpIsTheRigs()
        {
            Assert.AreEqual(6, FishJumpArc.Frames);
            Assert.AreEqual(95.0, FishJumpArc.FrameMs, 1e-9);
            Assert.AreEqual(0.55f, FishJumpArc.TravelMetres, 1e-6f);
            Assert.AreEqual(0.570, FishJumpArc.DurationSeconds, 1e-9, "6 x 95 ms");
        }

        // ---- the arc peaks and lands inside the strip ---------------------------------------------

        /// <summary>
        /// She leaves the water and comes back to it: the arc is 0 at both ends, peaks once in the
        /// middle, and is never negative (a fish does not go DOWN through the surface on a jump).
        /// </summary>
        [Test]
        public void TheArcPeaksAndLandsInsideTheStrip()
        {
            double d = FishJumpArc.DurationSeconds;

            Assert.AreEqual(0f, FishJumpArc.Arc01(0.0, 0.0), 1e-6f, "at the surface when it starts");
            Assert.AreEqual(0f, FishJumpArc.Arc01(0.0, d), 1e-6f, "back in the water when it ends");
            Assert.AreEqual(0f, FishJumpArc.Arc01(0.0, d * 2.0), 1e-6f, "and stays there afterwards");
            Assert.AreEqual(0f, FishJumpArc.Arc01(0.0, -1.0), 1e-6f, "and was there before");

            Assert.AreEqual(1f, FishJumpArc.Arc01(0.0, d * 0.5), 1e-5f, "the top of the arc is mid-jump");

            float peak = 0f;
            int peakIndex = -1;
            for (int i = 0; i <= 100; i++)
            {
                float a = FishJumpArc.Arc01(0.0, d * i / 100.0);
                Assert.GreaterOrEqual(a, 0f, "a jump never goes below the surface");
                if (a > peak) { peak = a; peakIndex = i; }
            }
            Assert.AreEqual(50, peakIndex, 1, "exactly one peak, in the middle");

            // Every frame the strip has is reached, and none past it.
            Assert.AreEqual(0, FishJumpArc.FrameAt(0.0, 0.0));
            Assert.AreEqual(FishJumpArc.Frames - 1, FishJumpArc.FrameAt(0.0, d * 0.99));
            Assert.AreEqual(FishJumpArc.Frames - 1, FishJumpArc.FrameAt(0.0, d * 9.0),
                "past the end she holds the landing pose rather than wrapping to the take-off");
        }

        /// <summary>She goes SOMEWHERE: the travel term is monotonic across the jump and reaches the
        /// rig's full 0.55 m — a breach played on the spot is the defect this guards.</summary>
        [Test]
        public void SheTravelsAcrossTheJump_NotOnTheSpot()
        {
            double d = FishJumpArc.DurationSeconds;
            float last = -1f;
            for (int i = 0; i <= 50; i++)
            {
                float u = FishJumpArc.Travel01(0.0, d * i / 50.0);
                Assert.GreaterOrEqual(u, last, "she never travels backwards mid-jump");
                last = u;
            }
            Assert.AreEqual(0f, FishJumpArc.Travel01(0.0, 0.0), 1e-6f);
            Assert.AreEqual(1f, FishJumpArc.Travel01(0.0, d), 1e-6f);
            Assert.AreEqual(1f, FishJumpArc.Travel01(0.0, d * 3.0), 1e-6f, "clamped, not wrapped");
        }

        // ---- the cadence is deterministic and never doubles up ------------------------------------

        /// <summary>
        /// One jump per period, at a hashed moment inside it, and always with room for the whole jump —
        /// so two jumps can never overlap and none straddles a period boundary. Deterministic from the
        /// fight's own seed (rule 5): the same fight replays the same jumps.
        /// </summary>
        [Test]
        public void TheCadenceIsDeterministic_AndNeverOverlaps()
        {
            const float period = 4.5f;
            double d = FishJumpArc.DurationSeconds;

            double prevStart = double.NegativeInfinity;
            for (int p = 0; p < 40; p++)
            {
                double inPeriod = p * period + period * 0.5;
                Assert.IsTrue(FishJumpArc.TryNextJump(1234, inPeriod, period, out double start));

                Assert.GreaterOrEqual(start, p * period, $"period {p}: the jump starts inside its period");
                Assert.LessOrEqual(start + d, (p + 1) * period + 1e-6,
                    $"period {p}: the whole jump must fit inside the period it belongs to");
                Assert.Greater(start, prevStart, "starts advance");
                prevStart = start;

                // Same seed, same period, same answer — asked again from a different instant.
                Assert.IsTrue(FishJumpArc.TryNextJump(1234, p * period + period * 0.9, period,
                                                      out double again));
                Assert.AreEqual(start, again, 1e-9, "the schedule is a function of the period, not of now");
            }

            // A different fight is a different schedule.
            FishJumpArc.TryNextJump(1234, 10.0, period, out double a);
            FishJumpArc.TryNextJump(9876, 10.0, period, out double b);
            Assert.Greater(System.Math.Abs(a - b), 1e-9, "two fights must not jump in lockstep");
        }

        /// <summary>A cadence of 0 (or negative) is the off switch — no jumps at all, which is exactly
        /// how the fight behaved before this ruling.</summary>
        [Test]
        public void ACadenceOfZero_IsNoJumps()
        {
            Assert.IsFalse(FishJumpArc.TryNextJump(1, 10.0, 0f, out _));
            Assert.IsFalse(FishJumpArc.TryNextJump(1, 10.0, -3f, out _));
        }

        // ---- the Core seam carries the ruling ------------------------------------------------------

        /// <summary>
        /// THE SEAM. The fight drawer lives in Player, which does not reference Fishing — so "does a
        /// bass jump" reaches it through <see cref="GameServices.FishBehaviour"/>, published by the
        /// Fishing registry. This is the contract, exercised end to end.
        /// </summary>
        [Test]
        public void TheBehaviourSeam_CarriesTheJumperRuling()
        {
            Assert.IsFalse(GameServices.FishBehaviour.Jumps("fish.striped_bass"),
                "before anything registers, nothing jumps — the safe direction");

            FishSpeciesRegistry.Register(Species("fish.striped_bass", FishFlags.Jumps, 3.5f));
            FishSpeciesRegistry.Register(Species("fish.atlantic_cod", FishFlags.Bottom));

            IFishBehaviourFacts facts = GameServices.FishBehaviour;
            Assert.IsTrue(facts.Jumps("fish.striped_bass"), "the owner ruled the bass a jumper");
            Assert.IsFalse(facts.Jumps("fish.atlantic_cod"), "a cod never leaves the water");
            Assert.IsFalse(facts.Jumps("fish.nothing_here"), "an unknown id does not jump");
            Assert.IsFalse(facts.Jumps(null), "and a null id does not throw");

            Assert.AreEqual(3.5f, facts.FightJumpPeriodSeconds("fish.striped_bass"), 1e-6f);
            Assert.AreEqual(0f, facts.FightJumpPeriodSeconds("fish.atlantic_cod"), 1e-6f,
                "unstated is 0 — the presenter's own fallback, not a period of zero");
        }

        /// <summary>The empty implementation is what a bare scene gets, and it must be safe rather than
        /// absent — a fight in a scene with no species library draws a fish that does not jump.</summary>
        [Test]
        public void WithNoRegistrar_TheSeamIsEmptyRatherThanNull()
        {
            Assert.IsNotNull(GameServices.FishBehaviour);
            Assert.IsFalse(GameServices.FishBehaviour.Jumps("anything"));
            Assert.AreEqual(0f, GameServices.FishBehaviour.FightJumpPeriodSeconds("anything"), 0f);
        }

        // ---- the authored cadences -----------------------------------------------------------------

        /// <summary>
        /// Every species the owner ruled a jumper states a fight cadence, and no species that cannot
        /// jump states one — a period on a cod would be a number that can never be read, which is the
        /// kind of dead data a content agent later "fixes" by making the cod jump.
        /// </summary>
        [Test]
        public void OnlyJumpers_StateAFightCadence()
        {
            int jumpers = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef",
                                                             new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                if (def == null) continue;

                if (def.Jumps)
                {
                    jumpers++;
                    Assert.Greater(def.FightJumpPeriodSeconds, 0f,
                        $"{path}: a jumper must say how often she jumps on the line (owner 2026-09-06)");
                    Assert.Less(def.FightJumpPeriodSeconds, 60f,
                        $"{path}: a cadence this long is indistinguishable from never jumping");
                    Assert.Greater(def.FightJumpPeriodSeconds, FishJumpArc.DurationSeconds,
                        $"{path}: a period shorter than the jump itself would jump continuously");
                }
                else
                {
                    Assert.AreEqual(0f, def.FightJumpPeriodSeconds, 1e-6f,
                        $"{path}: {def.Id} does not clear the water, so a fight cadence is dead data");
                }
            }

            Assert.AreEqual(3, jumpers, "bass, mackerel and herring — the owner's three");
        }
    }
}
