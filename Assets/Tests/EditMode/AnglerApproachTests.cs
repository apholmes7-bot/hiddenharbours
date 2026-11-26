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

        private static AnglerLook Look() => new AnglerLook
        {
            ApproachSeconds = 3.5f, ApproachFadeSeconds = 0.8f, ApproachFromM = 4f,
            ApproachOffLineDegrees = 35f, ApproachSwingM = 0.8f, ApproachSortingOrder = -3, ApproachAlpha01 = 0.8f,
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
        public void HerPath_StartsOffTheLine_EndsOnTheBait_AndAlwaysCloses()
        {
            AnglerLook look = Look();
            var bobber = new Vector2(1.5f, 6f);
            int seed = FishFightSeed.For(Cod, 2.4f);
            Assert.IsFalse(AnglerApproachMath.At(in look, bobber, look.ApproachSeconds + 0.1f, seed, out _, out _, out _),
                           "drawn before her approach began");

            Assert.IsTrue(AnglerApproachMath.At(in look, bobber, look.ApproachSeconds, seed, out Vector2 first, out _, out float a0));
            Assert.AreEqual(look.ApproachFromM, Vector2.Distance(first, bobber), 1e-3f, "she starts her distance out");
            Assert.AreEqual(0f, a0, 1e-4f, "she fades in");
            float off = Vector2.Angle(bobber, first - bobber);
            Assert.AreEqual(look.ApproachOffLineDegrees, off, 0.5f, "she starts off the line by the authored angle");

            float lastDist = float.MaxValue;
            for (float toBite = look.ApproachSeconds; toBite >= 0f; toBite -= 0.05f)
            {
                Assert.IsTrue(AnglerApproachMath.At(in look, bobber, toBite, seed, out Vector2 p, out _, out _));
                float d = Vector2.Distance(p, bobber);
                Assert.LessOrEqual(d, lastDist + 1e-4f, $"she backed off the bait at {toBite:0.00} s");
                lastDist = d;
            }
            AnglerApproachMath.At(in look, bobber, 0f, seed, out Vector2 end, out _, out float aEnd);
            Assert.AreEqual(0f, Vector2.Distance(end, bobber), 1e-3f, "she reaches the bait at the bite");
            Assert.AreEqual(look.ApproachAlpha01, aEnd, 1e-4f);
        }

        [Test]
        public void AnUnsetLook_DrawsNoApproach()
        {
            Assert.IsFalse(AnglerApproachMath.At(default, Vector2.up * 5f, 0.5f, 1, out _, out _, out _));
        }
    }
}
