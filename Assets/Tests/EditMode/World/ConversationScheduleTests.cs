using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>Determinism: same seed, same day ⇒ the same chat at the same minute</b> (CLAUDE.md rule 5).
    ///
    /// <para>A conversation that drifted between two runs of the same world would be the one thing a
    /// player cannot learn and a tester cannot reproduce. The rule is one pure function, so it is
    /// assertable here with plain numbers instead of by standing in the village at noon.</para>
    /// </summary>
    public class ConversationScheduleTests
    {
        const string Id = "conversation.st_peters.store_midday";

        [Test]
        public void TheSameSeedAndDay_AlwaysGiveTheSameMinute()
        {
            float first = ConversationSchedule.StartHourFor(12345, 3, Id, 12f, 13f);
            for (int i = 0; i < 50; i++)
                Assert.That(ConversationSchedule.StartHourFor(12345, 3, Id, 12f, 13f),
                            Is.EqualTo(first).Within(1e-6f),
                            "the start minute moved between two evaluations of one world-day");
        }

        [Test]
        public void TheStartIsAlwaysInsideTheAuthoredWindow()
        {
            // Every day of a long year, at several seeds: never one minute outside what was authored.
            foreach (int seed in new[] { 0, 1, 12345, -7, int.MaxValue })
                for (int day = 0; day < 200; day++)
                {
                    float at = ConversationSchedule.StartHourFor(seed, day, Id, 12f, 13f);
                    Assert.That(at, Is.InRange(12f, 13f),
                                $"seed {seed}, day {day}: start hour {at} fell outside [12, 13]");
                }
        }

        /// <summary>
        /// ⭐ The choice that separates this from <see cref="RoutineSchedule.RoutineSeed"/>, which
        /// deliberately does NOT fold the day in: a villager's departure jitter is who they are and must
        /// be learnable, whereas two neighbours saying the same words at the same second of every day
        /// would read as a mechanism rather than as a village.
        /// </summary>
        [Test]
        public void DifferentDays_MoveTheMinute()
        {
            var seen = new HashSet<float>();
            for (int day = 0; day < 30; day++)
                seen.Add(ConversationSchedule.StartHourFor(12345, day, Id, 12f, 13f));

            Assert.That(seen.Count, Is.GreaterThan(20),
                        "thirty days produced fewer than twenty distinct start minutes — the day is " +
                        "not really in the seed, or the hash is not spreading");
        }

        [Test]
        public void DifferentSeeds_MoveTheMinute()
        {
            var seen = new HashSet<float>();
            for (int seed = 0; seed < 30; seed++)
                seen.Add(ConversationSchedule.StartHourFor(seed, 0, Id, 12f, 13f));

            Assert.That(seen.Count, Is.GreaterThan(20), "two worlds should not share a timetable");
        }

        [Test]
        public void DifferentConversations_DoNotAllStartTogether()
        {
            // Two exchanges authored in the same window must not stack on one minute, or the village
            // would go quiet and then all talk at once.
            float a = ConversationSchedule.StartHourFor(12345, 0, "conversation.a", 12f, 13f);
            float b = ConversationSchedule.StartHourFor(12345, 0, "conversation.b", 12f, 13f);
            Assert.That(a, Is.Not.EqualTo(b).Within(1e-4f));
        }

        [Test]
        public void TheSpreadCoversTheWholeWindow_NotJustItsMiddle()
        {
            // A hash that clustered would make the "window" a lie. Quarters, over many days.
            var quarters = new int[4];
            for (int day = 0; day < 400; day++)
            {
                float at = ConversationSchedule.StartHourFor(12345, day, Id, 12f, 16f);
                int q = (int)((at - 12f) / 4f * 4f);
                if (q > 3) q = 3;
                quarters[q]++;
            }
            foreach (int count in quarters)
                Assert.That(count, Is.GreaterThan(50),
                            $"400 days fell into quarters [{quarters[0]}, {quarters[1]}, " +
                            $"{quarters[2]}, {quarters[3]}] — the window is not being used evenly");
        }

        // ---- degenerate windows -------------------------------------------------------------------

        [Test]
        public void AnEmptyOrInvertedWindow_CollapsesToItsStart_RatherThanThrowing()
        {
            Assert.That(ConversationSchedule.StartHourFor(1, 1, Id, 12f, 12f),
                        Is.EqualTo(12f).Within(1e-6f));
            Assert.That(ConversationSchedule.StartHourFor(1, 1, Id, 15f, 9f),
                        Is.EqualTo(15f).Within(1e-6f),
                        "a half-authored def should produce a defensible time, not a wild one");
        }

        // ---- the window's upper edge ----------------------------------------------------------------

        [Test]
        public void IsDue_IsTrueOnlyBetweenTheStartAndTheEndOfTheWindow()
        {
            Assert.IsFalse(ConversationSchedule.IsDue(11.9f, 12.4f, 13f), "before the start");
            Assert.IsTrue(ConversationSchedule.IsDue(12.4f, 12.4f, 13f), "exactly at the start");
            Assert.IsTrue(ConversationSchedule.IsDue(12.7f, 12.4f, 13f), "inside");
            Assert.IsTrue(ConversationSchedule.IsDue(13f, 12.4f, 13f), "exactly at the end");
            Assert.IsFalse(ConversationSchedule.IsDue(13.1f, 12.4f, 13f),
                           "past the window — an exchange that could not start must not fire hours " +
                           "later at a moment the author never pictured");
        }
    }
}
