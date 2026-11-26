using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A villager angler casts into water, never onto the bared bed</b> (P1, rule 5):
    /// <see cref="AnglerWaterMath.Plan"/> keeps his usual reach where it lands in water, reaches out along the
    /// same bearing when only a longer cast does, stands him down when nothing in reach is wet, and finds the
    /// moment a falling tide leaves his bobber dry, where <see cref="AnglerTimeline"/> reels him in empty. The
    /// ground is a plain slope falling away from him and the tide a level the test chooses, so every answer
    /// can be worked by hand. Equal inputs give the equal plan, and an unset plan changes no beat.
    /// </summary>
    public sealed class AnglerWaterTests
    {
        private const int Seed = 1234;
        private const string Key = "npc.test_angler";
        private const float Heading = 0f;

        // The ground: 1 m above datum under his feet, falling 0.25 m for every metre out (north).
        private const float BedAtStandM = 1f;
        private const float SlopeMPerM = 0.25f;

        // Water levels picked against that slope, with the script's 0.2 m least bobber depth:
        // deep enough from y > (1.2 - level) / 0.25.
        private const float HighLevelM = 1.5f;    // from y > -1.2: the usual 7 m cast is wet
        private const float MidLevelM = -1.0f;    // from y > 8.8: only a cast past 8.8 m (of 12) is wet
        private const float DeadLowLevelM = -2.5f; // from y > 14.8: past the 12 m long cast, dry in reach

        private static uint KeyHash => FigureKeyHash.Of(Key);
        private static readonly ShoreCatch Cod = new ShoreCatch("fish.atlantic_cod", 2.4f, 0.55f);

        private static AnglerScript Script() => new AnglerScript
        {
            SlotSeconds = 60f,
            CastBackSeconds = 1f, CastSeconds = 1.2f, WaitMinSeconds = 4f, WaitMaxSeconds = 9f,
            BiteSeconds = 1f, StrikeSeconds = 0.6f,
            DeepMinSeconds = 3f, DeepMaxSeconds = 6f, SurfaceMinSeconds = 3f, SurfaceMaxSeconds = 5f,
            LandSeconds = 3f, EmptyRetrieveSeconds = 3f, LandLiftSeconds = 1f,
            CastReachM = 7f, CastBearingDegrees = 0f, CastBearingJitterDegrees = 0f,
            RunSwingM = 1.2f, TensionMin01 = 0.35f, TensionMax01 = 0.8f, FightWaveHz = 0.45f,
            LongCastReachM = 12f, MinBobberDepthM = 0.2f, WaterRecheckSeconds = 5f,
        };

        private static AnglerWaterMath.DepthAt Slope(Func<double, float> level)
            => (p, t) => level(t) - (BedAtStandM - SlopeMPerM * p.y);

        private static AnglerWaterMath.DepthAt Still(float level) => Slope(_ => level);

        private static AnglerWater Plan(in AnglerScript script, AnglerWaterMath.DepthAt depth, double t0 = 0d)
            => AnglerWaterMath.Plan(in script, Vector2.zero, Vector2.up, default, t0, depth);

        [Test]
        public void AtHighWater_TheUsualCastIsWet_AndStaysWet()
        {
            AnglerScript script = Script();
            AnglerWater w = Plan(in script, Still(HighLevelM));
            Assert.IsTrue(w.IsSet && w.Casts);
            Assert.AreEqual(script.CastReachM, w.ReachM, "he cast further than he needed to");
            Assert.IsFalse(w.IsLongCast(in script));
            Assert.IsTrue(double.IsPositiveInfinity(w.DryAtSeconds), "a still high tide dried his bobber");
            Assert.IsFalse(w.Waterline.IsSet, "the water reaches in to him, so there is no waterline to hold off");
        }

        [Test]
        public void AtMidWater_OnlyALongerCast_ReachesWater_AndHeTakesTheShortestThatDoes()
        {
            AnglerScript script = Script();
            AnglerWater w = Plan(in script, Still(MidLevelM));
            Assert.IsTrue(w.Casts, "a wet point was in reach, but he did not cast");
            Assert.IsTrue(w.IsLongCast(in script));
            float deepFrom = (BedAtStandM + script.MinBobberDepthM - MidLevelM) / SlopeMPerM;   // 8.8 m
            float step = (script.LongCastReachM - script.CastReachM) / AnglerWaterMath.ReachSamples;
            Assert.Greater(w.ReachM, deepFrom, "the bobber lands short of water deep enough");
            Assert.LessOrEqual(w.ReachM, deepFrom + step + 1e-4f, "he cast further than the shortest wet reach");
            Assert.LessOrEqual(w.ReachM, script.LongCastReachM);

            // The waterline sits between him and the bobber, where the bed is bared.
            Assert.IsTrue(w.Waterline.IsSet);
            float wetFrom = (BedAtStandM - MidLevelM) / SlopeMPerM;   // 8 m: depth > 0 beyond
            float sample = w.ReachM / AnglerWaterMath.ReachSamples;
            Assert.GreaterOrEqual(w.Waterline.OutwardDistance(Vector2.zero), -(wetFrom + sample + 1e-4f));
            Assert.Less(w.Waterline.OutwardDistance(Vector2.up * w.ReachM), 1e-4f + sample * 2f);
            Assert.GreaterOrEqual(w.Waterline.OutwardDistance(Vector2.up * w.ReachM), 0f, "the bobber is on the dry side");
        }

        [Test]
        public void AtDeadLow_NothingInReachIsWet_SoHeDoesNotCast_AndRestsTheSlotOut()
        {
            AnglerScript script = Script();
            AnglerWater w = Plan(in script, Still(DeadLowLevelM));
            Assert.IsTrue(w.IsSet);
            Assert.IsFalse(w.Casts, "he cast onto the bared bed");

            // The timeline rests the whole slot out under that plan, catch or none.
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, 10);
            for (double t = start; t < start + script.SlotSeconds; t += 0.05d)
            {
                AnglerTimeline.Evaluate(in script, in Cod, true, Seed, KeyHash, Heading, t, default, in w, out AnglerBeat b);
                Assert.IsFalse(b.InSession, $"+{t - start:0.00}s: in session with no water in reach");
            }

            // No long cast authored: the mid tide stands him down too, never a cast onto the mud.
            AnglerScript shortRod = Script();
            shortRod.LongCastReachM = 0f;
            Assert.IsFalse(Plan(in shortRod, Still(MidLevelM)).Casts);
        }

        [Test]
        public void OnAFallingTide_TheBobberDriesMidSession_AndHeReelsInEmptyFromTheNextRecheck()
        {
            AnglerScript script = Script();
            // From level 0 (the usual cast is wet: deep enough past 4.8 m) falling 0.1 m/s: at 7 m the depth
            // reaches zero at 7.5 s, so the re-check at 10 s is the first to find it dry.
            const float FallMPerS = 0.1f;
            long slot = 10;
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, slot);
            AnglerWaterMath.DepthAt falling = Slope(t => -(float)(t - start) * FallMPerS);
            AnglerWater w = Plan(in script, falling, start);
            Assert.IsTrue(w.Casts);
            Assert.AreEqual(script.CastReachM, w.ReachM);
            Assert.AreEqual(2d * script.WaterRecheckSeconds, w.DryAtSeconds, "the wrong re-check found it dry");

            AnglerWater still = w;
            still.DryAtSeconds = double.PositiveInfinity;
            bool reeled = false;
            for (double t = start; t < start + script.SlotSeconds; t += 0.05d)
            {
                double into = t - start;
                AnglerTimeline.Evaluate(in script, in Cod, true, Seed, KeyHash, Heading, t, default, in w, out AnglerBeat b);
                AnglerTimeline.Evaluate(in script, in Cod, true, Seed, KeyHash, Heading, t, default, in still, out AnglerBeat a);
                if (into < w.DryAtSeconds)
                {
                    Assert.AreEqual(a.Phase, b.Phase, $"+{into:0.00}s: changed before the bobber dried");
                    continue;
                }
                if (into < w.DryAtSeconds + script.EmptyRetrieveSeconds)
                {
                    Assert.AreEqual(FishingPhase.NoBite, b.Phase, $"+{into:0.00}s: not reeling in off the mud");
                    Assert.IsTrue(string.IsNullOrEmpty(b.State.FishId), $"+{into:0.00}s: still has a fish on");
                    reeled = true;
                }
                else Assert.IsFalse(b.InSession, $"+{into:0.00}s: back in session after reeling in");
            }
            Assert.IsTrue(reeled, "harness: the reel-in was never sampled");
        }

        [Test]
        public void TheFishIsHeldOffTheBaredBed_ThroughTheWholeFight()
        {
            AnglerScript script = Script();
            AnglerWater w = Plan(in script, Still(MidLevelM));
            Assert.IsTrue(w.Waterline.IsSet);
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, 10);
            int fighting = 0;
            for (double t = start; t < start + script.SlotSeconds; t += 0.05d)
            {
                AnglerTimeline.Evaluate(in script, in Cod, true, Seed, KeyHash, Heading, t, default, in w, out AnglerBeat b);
                if (b.Phase != FishingPhase.FightDeep && b.Phase != FishingPhase.FightSurface) continue;
                fighting++;
                var fish = new Vector2(b.State.FishOffsetX, b.State.FishOffsetY);
                Assert.GreaterOrEqual(w.Waterline.OutwardDistance(fish), -1e-4f,
                                      $"+{t - start:0.00}s: the fish at {fish} is over the bared bed");
            }
            Assert.Greater(fighting, 0, "harness: no fight was sampled");
        }

        [Test]
        public void ThePlan_IsAPureFunctionOfItsInputs_AndAnUnsetPlanChangesNoBeat()
        {
            AnglerScript script = Script();
            AnglerWaterMath.DepthAt tide = Slope(t => Mathf.Sin((float)t * 0.01f));
            for (double t0 = 0d; t0 < 600d; t0 += 37d)
            {
                AnglerWater a = Plan(in script, tide, t0), b = Plan(in script, tide, t0);
                Assert.AreEqual(a.Casts, b.Casts);
                Assert.AreEqual(a.ReachM, b.ReachM);
                Assert.AreEqual(a.DryAtSeconds, b.DryAtSeconds);
                Assert.AreEqual(a.Waterline.Point, b.Waterline.Point);
            }
            Assert.IsFalse(AnglerWaterMath.Plan(in script, Vector2.zero, Vector2.up, default, 0d, null).IsSet);

            for (double t = 1000d; t < 1000d + script.SlotSeconds * 3; t += 0.1d)
            {
                AnglerTimeline.Evaluate(in script, in Cod, true, Seed, KeyHash, Heading, t, default, out AnglerBeat old);
                AnglerTimeline.Evaluate(in script, in Cod, true, Seed, KeyHash, Heading, t, default, default(AnglerWater),
                                        out AnglerBeat now);
                Assert.AreEqual(old.Phase, now.Phase);
                Assert.AreEqual(old.State.FishOffsetX, now.State.FishOffsetX);
                Assert.AreEqual(old.State.FishOffsetY, now.State.FishOffsetY);
                Assert.AreEqual(old.State.CastAimX, now.State.CastAimX);
                Assert.AreEqual(old.State.CastAimY, now.State.CastAimY);
            }
        }

        [Test]
        public void TheScript_RefusesANegativeWaterTunable_OrALongCastShorterThanTheUsualOne()
        {
            AnglerScript s = Script();
            Assert.IsTrue(s.IsValid(out string why), why);
            s.LongCastReachM = 5f;
            Assert.IsFalse(s.IsValid(out _));
            s = Script(); s.MinBobberDepthM = -0.1f;
            Assert.IsFalse(s.IsValid(out _));
            s = Script(); s.WaterRecheckSeconds = -1f;
            Assert.IsFalse(s.IsValid(out _));
            s = Script(); s.LongCastReachM = 0f; s.WaterRecheckSeconds = 0f; s.MinBobberDepthM = 0f;
            Assert.IsTrue(s.IsValid(out why), "the water tunables at zero must stay playable: " + why);
        }
    }
}
