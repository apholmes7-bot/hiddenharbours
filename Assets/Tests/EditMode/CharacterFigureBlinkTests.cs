using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE BLINK'S SCHEDULE</b> (<see cref="CharacterFigureBlink"/>): seeded per figure, the steps in
    /// order, every wait inside the def's interval, the first blink inside a first wait, a double after
    /// its gap and never a third, a reversal or a jump restarting from the seed, and nothing drawn from
    /// <see cref="UnityEngine.Random"/>.
    ///
    /// <para>Synthetic defs with numbers of their own, so these hold the schedule's MECHANICS. That the
    /// shipped defs carry the rig's own <c>BLINK</c>, and play its exported blink clip frame for frame,
    /// is <c>CharacterSkinBakeGuardTests.V9_TheBlinkIsTheRigsBlink</c>'s, against the rig in V8.</para>
    /// </summary>
    public sealed class CharacterFigureBlinkTests
    {
        const int Open = 1, Half = 2, Shut = 3;
        const float StepA = 0.05f, StepB = 0.1f, WaitLo = 1f, WaitHi = 3f, Gap = 0.2f;

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        CharacterSkinDef Def(float doubleChance)
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _made.Add(def);
            def.Id = "skin.blink_test";
            def.FaceGroups = new[] { "eyes.open", "eyes.half", "eyes.shut" };
            def.BlinkSteps = new[]
            {
                new CharacterSkinDef.BlinkStep { Group = Half, Seconds = StepA },
                new CharacterSkinDef.BlinkStep { Group = Shut, Seconds = StepB },
                new CharacterSkinDef.BlinkStep { Group = Half, Seconds = StepA },
            };
            def.BlinkIntervalSeconds = new Vector2(WaitLo, WaitHi);
            def.BlinkDoubleChance = doubleChance;
            def.BlinkDoubleGapSeconds = Gap;
            def.BlinkSkipGroups = new[] { Shut };
            return def;
        }

        /// <summary>Every blink start the schedule reaches over <paramref name="seconds"/>, sampled at
        /// <paramref name="step"/>, from <paramref name="from"/>.</summary>
        static List<double> Starts(CharacterFigureBlink blink, double from, double seconds, double step)
        {
            var starts = new List<double>();
            for (double t = from; t <= from + seconds; t += step)
            {
                blink.EyesAt(t);
                if (starts.Count == 0 || blink.NextStart != starts[starts.Count - 1]) starts.Add(blink.NextStart);
            }
            return starts;
        }

        static List<int> Eyes(CharacterFigureBlink blink, double seconds, double step)
        {
            var eyes = new List<int>();
            for (double t = 0; t <= seconds; t += step) eyes.Add(blink.EyesAt(t));
            return eyes;
        }

        [Test]
        public void TheSameSeedBlinksTheSameWay()
        {
            CharacterSkinDef def = Def(0.3f);
            uint seed = CharacterFigureBlink.SeedFor(def.Id, "npc.harbour_master");
            var a = new CharacterFigureBlink();
            var b = new CharacterFigureBlink();
            a.Reset(def, seed);
            b.Reset(def, seed);
            List<int> ea = Eyes(a, 60, 1.0 / 60.0);
            CollectionAssert.AreEqual(ea, Eyes(b, 60, 1.0 / 60.0), "One seed blinked two ways.");
            CollectionAssert.Contains(ea, Shut, "A minute went by without a blink.");
        }

        [Test]
        public void TwoFiguresOfOneDefBlinkApart()
        {
            CharacterSkinDef def = Def(0.3f);
            uint sa = CharacterFigureBlink.SeedFor(def.Id, "npc.skipper_a");
            uint sb = CharacterFigureBlink.SeedFor(def.Id, "npc.skipper_b");
            Assert.AreNotEqual(sa, sb, "Two figures of one def share a seed.");
            var a = new CharacterFigureBlink();
            var b = new CharacterFigureBlink();
            a.Reset(def, sa);
            b.Reset(def, sb);
            CollectionAssert.AreNotEqual(Starts(a, 0, 30, 0.01), Starts(b, 0, 30, 0.01),
                "Two figures of one def blink together.");
        }

        [Test]
        public void TheSeedHashesTheDefAndTheKeyApart()
        {
            Assert.AreEqual(CharacterFigureBlink.SeedFor("skin.fisher", "a"), CharacterFigureBlink.SeedFor("skin.fisher", "a"));
            Assert.AreNotEqual(CharacterFigureBlink.SeedFor("ab", "c"), CharacterFigureBlink.SeedFor("a", "bc"),
                "The def id and the figure key run together.");
            Assert.AreNotEqual(CharacterFigureBlink.SeedFor("skin.fisher", "a"), CharacterFigureBlink.SeedFor("skin.nan", "a"),
                "Two defs give one figure key the same seed.");
            Assert.DoesNotThrow(() => CharacterFigureBlink.SeedFor(null, null));
        }

        [Test]
        public void TheFirstBlinkFallsInsideTheFirstWait()
        {
            CharacterSkinDef def = Def(0.3f);
            var blink = new CharacterFigureBlink();
            double lo = double.MaxValue, hi = double.MinValue;
            for (int k = 0; k < 400; k++)
            {
                blink.Reset(def, CharacterFigureBlink.SeedFor(def.Id, "figure " + k));
                const double t0 = 100.0;
                blink.EyesAt(t0);
                double first = blink.NextStart - t0;
                Assert.That(first, Is.InRange(0.0, (double)WaitHi), $"Seed {k}: the first blink is {first} s away.");
                lo = System.Math.Min(lo, first);
                hi = System.Math.Max(hi, first);
            }
            Assert.Less(lo, WaitLo, "No first blink fell before the shortest wait: the first is not a point in a wait.");
            Assert.Greater(hi, WaitLo, "Every first blink fell inside the shortest wait.");
        }

        [Test]
        public void EveryWaitLiesInTheIntervalAndADoubleFollowsItsGapButNeverAThird()
        {
            CharacterSkinDef def = Def(0.5f);
            var blink = new CharacterFigureBlink();
            blink.Reset(def, CharacterFigureBlink.SeedFor(def.Id, "npc.packer"));
            List<double> starts = Starts(blink, 0, 900, 1.0 / 120.0);
            Assert.Greater(starts.Count, 200, "Too few blinks to judge the waits by.");

            int doubles = 0, singles = 0;
            bool lastWasDouble = false;
            for (int i = 1; i < starts.Count; i++)
            {
                double wait = starts[i] - (starts[i - 1] + blink.Length);
                if (System.Math.Abs(wait - Gap) < 1e-6)
                {
                    Assert.IsFalse(lastWasDouble, $"Blink {i} is a third in a row.");
                    doubles++;
                    lastWasDouble = true;
                    continue;
                }
                Assert.That(wait, Is.InRange((double)WaitLo - 1e-6, (double)WaitHi + 1e-6), $"Blink {i} waited {wait} s.");
                singles++;
                lastWasDouble = false;
            }
            Assert.Greater(doubles, 0, "A 50% double never came.");
            Assert.Greater(singles, 0, "Every blink was a double.");
        }

        [Test]
        public void TheStepsPlayInOrderAndTheEyesComeBack()
        {
            CharacterSkinDef def = Def(0f);
            var blink = new CharacterFigureBlink();
            blink.Reset(def, 7u);
            blink.EyesAt(0);
            double s = blink.NextStart;
            Assert.AreEqual(StepA + StepB + StepA, blink.Length, 1e-6, "The blink's length is not its steps'.");
            Assert.AreEqual(CharacterSkinDef.NoFaceGroup, blink.EyesAt(s - 0.001), "A blink showed before it began.");
            Assert.AreEqual(Half, blink.EyesAt(s + StepA * 0.5));
            Assert.AreEqual(Shut, blink.EyesAt(s + StepA + StepB * 0.5));
            Assert.AreEqual(Half, blink.EyesAt(s + StepA + StepB + StepA * 0.5));
            Assert.AreEqual(CharacterSkinDef.NoFaceGroup, blink.EyesAt(s + blink.Length + 0.001),
                "The eyes did not come back after the blink.");
        }

        [Test]
        public void AReversalOrAJumpRestartsFromTheSeed()
        {
            CharacterSkinDef def = Def(0.3f);
            const uint seed = 0xC0FFEEu;
            var blink = new CharacterFigureBlink();
            var fresh = new CharacterFigureBlink();

            blink.Reset(def, seed);
            Starts(blink, 0, 40, 0.02);
            blink.EyesAt(12.5);
            fresh.Reset(def, seed);
            fresh.EyesAt(12.5);
            Assert.AreEqual(fresh.NextStart, blink.NextStart, "A seek back did not restart the schedule from the seed.");

            double far = 12.5 + WaitHi + blink.Length + 5.0;
            blink.EyesAt(far);
            fresh.Reset(def, seed);
            fresh.EyesAt(far);
            Assert.AreEqual(fresh.NextStart, blink.NextStart, "A jump past the longest wait did not restart the schedule.");
        }

        [Test]
        public void TheScheduleNeverDrawsFromUnityRandom()
        {
            CharacterSkinDef def = Def(0.3f);
            uint seed = CharacterFigureBlink.SeedFor(def.Id, "npc.nan");
            Random.State saved = Random.state;
            try
            {
                var blink = new CharacterFigureBlink();
                Random.InitState(1);
                blink.Reset(def, seed);
                List<int> a = Eyes(blink, 30, 1.0 / 30.0);
                Random.State after = Random.state;
                Random.InitState(1);
                Assert.AreEqual(Random.state, after, "The schedule drew from UnityEngine.Random.");
                Random.InitState(987654);
                blink.Reset(def, seed);
                CollectionAssert.AreEqual(a, Eyes(blink, 30, 1.0 / 30.0), "The blink follows UnityEngine.Random's seed.");
            }
            finally
            {
                Random.state = saved;
            }
        }

        [Test]
        public void ADefWithoutABlinkNeverBlinks()
        {
            CharacterSkinDef def = Def(0.3f);
            def.BlinkSteps = new CharacterSkinDef.BlinkStep[0];
            var blink = new CharacterFigureBlink();
            blink.Reset(def, 1u);
            Assert.AreEqual(CharacterSkinDef.NoFaceGroup, blink.EyesAt(3.0));
            blink.Reset(null, 1u);
            Assert.AreEqual(CharacterSkinDef.NoFaceGroup, blink.EyesAt(3.0));

            CharacterSkinDef faceless = Def(0.3f);
            faceless.FaceGroups = new string[0];
            blink.Reset(faceless, 1u);
            Assert.IsFalse(faceless.HasBlink);
            for (double t = 0; t < 20; t += 0.05)
                Assert.AreEqual(CharacterSkinDef.NoFaceGroup, blink.EyesAt(t), "A def with no face blinked.");
        }
    }
}
