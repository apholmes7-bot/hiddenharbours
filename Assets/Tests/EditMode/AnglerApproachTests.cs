using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The fish coming to a villager's bait: named only while a bite is coming in this wait, counting down to
    /// exactly that bite, a pure function of the clock; and her path starts off the line, ends on the bait,
    /// and never moves her backwards.
    /// </summary>
    public sealed class AnglerApproachTests
    {
        private const int Seed = 1234;
        private const string Key = "npc.test_angler";
        private const string Cod = "fish.atlantic_cod";
        private const float Heading = 90f;
        private const double Step = 0.01d;

        private static AnglerScript Script() => new AnglerScript
        {
            SlotSeconds = 120f,
            CastBackSeconds = 1f, CastSeconds = 1.5f, WaitMinSeconds = 3f, WaitMaxSeconds = 8f,
            BiteSeconds = 1.2f, StrikeSeconds = 0.6f,
            DeepMinSeconds = 4f, DeepMaxSeconds = 8f, SurfaceMinSeconds = 3f, SurfaceMaxSeconds = 6f,
            LandSeconds = 2.5f, EmptyRetrieveSeconds = 3f, LandLiftSeconds = 1f,
            CastReachM = 6f, CastBearingDegrees = 0f, CastBearingJitterDegrees = 20f,
            RunSwingM = 1.5f, TensionMin01 = 0.3f, TensionMax01 = 0.8f, FightWaveHz = 0.4f,
        };

        private const float Roll = 0.52f;

        private static AnglerApproachLook Look() => new AnglerApproachLook
        {
            Seconds = 5f, FadeInSeconds = 0.6f, FromM = 4f, OffLineDegrees = 35f, SwingM = 0.8f,
            CircleM = 0.9f, CircleDegrees = 270f, CircleSeconds = 1.6f, PauseSeconds = 0.85f, TakeSeconds = 0.35f,
            PauseBeatRate = 0.5f, TakeBeatRate = 2f, HookBlendSeconds = 0.6f, SortingOrder = -3, Alpha01 = 1f,
        };

        private static readonly ShoreCatch CodCatch = new ShoreCatch(Cod, 2.4f, 0.55f);
        private static uint KeyHash => FigureKeyHash.Of(Key);

        private static AnglerBeat At(in AnglerScript script, double now, bool hasCatch = true)
        {
            AnglerTimeline.Evaluate(in script, CodCatch, hasCatch, Seed, KeyHash, Heading, now, out AnglerBeat beat);
            return beat;
        }

        [Test]
        public void WhileHeWaits_TheSlotsFishIsNamed_AndCountsDownToHerBite()
        {
            AnglerScript script = Script();
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, 7);
            double? biteAt = null;
            int named = 0;
            for (double t = start; t < start + script.SlotSeconds; t += Step)
            {
                AnglerBeat b = At(in script, t);
                if (b.Phase == FishingPhase.Waiting)
                {
                    Assert.AreEqual(Cod, b.ApproachFishId, "a waiting slot with a catch names her");
                    Assert.AreEqual(CodCatch.WeightKg, b.ApproachKg);
                    biteAt ??= t + b.SecondsToBite;
                    Assert.AreEqual(biteAt.Value, t + b.SecondsToBite, 1e-3, "she counts down to one instant");
                    named++;
                }
                else
                {
                    Assert.IsNull(b.ApproachFishId, $"named outside the wait ({b.Phase})");
                    Assert.IsTrue(float.IsNaN(b.SecondsToBite));
                }
                if (b.Phase == FishingPhase.Bite && biteAt.HasValue)
                    Assert.GreaterOrEqual(t, biteAt.Value - 1e-3, "the bite came before her countdown ran out");
            }
            Assert.Greater(named, 0, "the slot never waited");
        }

        [Test]
        public void AnEmptySlot_NamesNoFish()
        {
            AnglerScript script = Script();
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, 7);
            for (double t = start; t < start + script.SlotSeconds; t += Step)
                Assert.IsNull(At(in script, t, hasCatch: false).ApproachFishId);
        }

        [Test]
        public void EqualClocks_GiveTheBitIdenticalApproach()
        {
            AnglerScript script = Script();
            double start = AnglerTimeline.SlotStartSeconds(in script, Seed, KeyHash, 11);
            for (double t = start; t < start + 20d; t += 0.37d)
            {
                AnglerBeat a = At(in script, t), b = At(in script, t);
                Assert.AreEqual(a.ApproachFishId, b.ApproachFishId);
                Assert.AreEqual(a.SecondsToBite, b.SecondsToBite);
            }
        }

        [Test]
        public void HerPath_SwimsInOffTheLine_CirclesTheBait_HangsFacingIt_ThenTakesIt()
        {
            AnglerApproachLook look = Look();
            var bobber = new Vector2(1.5f, 6f);
            int seed = FishFightSeed.For(Cod, 2.4f);
            Assert.IsFalse(AnglerApproachMath.At(in look, bobber, look.Seconds + 0.1f, seed, Roll, out _),
                           "drawn before her approach began");

            Assert.IsTrue(AnglerApproachMath.At(in look, bobber, look.Seconds, seed, Roll, out ApproachPose first));
            Assert.AreEqual(look.FromM, Vector2.Distance(first.Offset, bobber), 1e-3f, "she starts her distance out");
            Assert.AreEqual(0f, first.FadeIn01, 1e-4f, "she fades in");
            Assert.AreEqual(look.OffLineDegrees, Vector2.Angle(bobber, first.Offset - bobber), 0.5f,
                            "she starts off the line by the authored angle");

            float tIn = AnglerApproachMath.SwimInSeconds(in look);
            float circleEnd = tIn + look.CircleSeconds;
            for (int i = 0; i < 32; i++)
            {
                float tau = tIn + 0.025f + i * 0.05f;
                AnglerApproachMath.At(in look, bobber, look.Seconds - tau, seed, Roll, out ApproachPose p);
                Assert.AreEqual(look.CircleM, Vector2.Distance(p.Offset, bobber), 1e-3f, $"off the circle at {tau:0.00} s");
            }

            AnglerApproachMath.At(in look, bobber, look.Seconds - (circleEnd + 0.7f), seed, Roll, out ApproachPose hang);
            Vector2 toBait = bobber - hang.Offset;
            Assert.AreEqual(Mathf.Atan2(toBait.x, toBait.y), hang.HeadingRadians, 1e-3f, "she does not hang facing the bait");
            Assert.AreEqual(SwimFishPose.Swim, hang.Pose);

            AnglerApproachMath.At(in look, bobber, 0f, seed, Roll, out ApproachPose end);
            Assert.AreEqual(0f, Vector2.Distance(end.Offset, bobber), 1e-3f, "she reaches the bait at the bite");
            Assert.AreEqual(1f, end.FadeIn01, 1e-4f);
        }

        [Test]
        public void HerFlankFlashes_OnTheTwoTurns_AndNowhereElse()
        {
            AnglerApproachLook look = Look();
            var bobber = new Vector2(-2f, 5f);
            float tIn = AnglerApproachMath.SwimInSeconds(in look), circleEnd = tIn + look.CircleSeconds;
            for (int i = 0; i < 500; i++)
            {
                float tau = 0.005f + i * 0.01f;
                AnglerApproachMath.At(in look, bobber, look.Seconds - tau, 7, Roll, out ApproachPose p);
                bool turning = (tau >= tIn && tau < tIn + Roll) || (tau >= circleEnd && tau < circleEnd + Roll);
                Assert.AreEqual(turning ? SwimFishPose.Roll : SwimFishPose.Swim, p.Pose, $"at {tau:0.000} s");
            }
            AnglerApproachMath.At(in look, bobber, look.Seconds - tIn - 0.1f, 7, 0f, out ApproachPose none);
            Assert.AreEqual(SwimFishPose.Swim, none.Pose, "no roll art, no flash");
        }

        [Test]
        public void HerTailBeat_NeverRunsBackwards_AndSlowsAsSheHangs()
        {
            AnglerApproachLook look = Look();
            var bobber = new Vector2(0f, 6f);
            float circleEnd = AnglerApproachMath.SwimInSeconds(in look) + look.CircleSeconds;
            double last = double.MinValue;
            for (int i = 0; i <= 250; i++)
            {
                float tau = i * 0.02f;
                AnglerApproachMath.At(in look, bobber, look.Seconds - tau, 3, 0f, out ApproachPose p);
                Assert.GreaterOrEqual(p.PoseSeconds, last - 1e-5, $"the tail beat ran backwards at {tau:0.00} s");
                last = p.PoseSeconds;
            }
            AnglerApproachMath.At(in look, bobber, look.Seconds - (circleEnd + 0.2f), 3, 0f, out ApproachPose a);
            AnglerApproachMath.At(in look, bobber, look.Seconds - (circleEnd + 0.4f), 3, 0f, out ApproachPose b);
            Assert.AreEqual(0.2f * look.PauseBeatRate, (float)(b.PoseSeconds - a.PoseSeconds), 1e-3f);
        }

        [Test]
        public void AtTheBait_SheHoldsOnIt_FacingAsSheTookIt()
        {
            AnglerApproachLook look = Look();
            var bobber = new Vector2(1f, 6f);
            AnglerApproachMath.At(in look, bobber, 0f, 5, Roll, out ApproachPose took);
            ApproachPose held = AnglerApproachMath.AtTheBait(in look, bobber, 5, 0.4f);
            Assert.AreEqual(bobber, held.Offset);
            Assert.AreEqual(took.HeadingRadians, held.HeadingRadians, 1e-4f);
            Assert.AreEqual(took.PoseSeconds + 0.4f * look.TakeBeatRate, held.PoseSeconds, 1e-3);
        }

        [Test]
        public void AnUnsetLook_DrawsNoApproach()
        {
            Assert.IsFalse(AnglerApproachMath.At(default, Vector2.up * 5f, 0.5f, 1, Roll, out _));
        }
    }
}
