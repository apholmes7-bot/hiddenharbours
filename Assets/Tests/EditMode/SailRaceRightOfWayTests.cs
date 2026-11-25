using System;
using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class SailRaceRightOfWayTests
    {
        // Northbound boat: wind FROM east is over starboard; FROM west is over port.
        private static RaceBoatMotion Boat(string id, Vector2 pos, Vector2? forward = null, Vector2? velocity = null)
            => new RaceBoatMotion(id, pos, forward ?? Vector2.up, velocity ?? Vector2.up);

        [Test]
        public void PortGivesWayToStarboard_UsesDownwindConventionAndIgnoresArgumentOrder()
        {
            var a = Boat("racer.a", Vector2.zero, Vector2.up);
            var b = Boat("racer.b", Vector2.right, Vector2.down);
            Assert.That(SailRaceRightOfWay.GiveWay(a, b, Vector2.left, SailRaceFixtures.Rules), Is.EqualTo("racer.b"));
            Assert.That(SailRaceRightOfWay.GiveWay(b, a, Vector2.left, SailRaceFixtures.Rules), Is.EqualTo("racer.b"));
            Assert.That(SailRaceRightOfWay.GiveWay(a, b, Vector2.right, SailRaceFixtures.Rules), Is.EqualTo("racer.a"));
        }

        [Test]
        public void SameTack_WindwardKeepsClearOfLeeward()
        {
            var a = Boat("racer.a", Vector2.right);
            var b = Boat("racer.b", Vector2.left);
            Assert.That(SailRaceRightOfWay.GiveWay(a, b, Vector2.left, SailRaceFixtures.Rules), Is.EqualTo("racer.a"));
            Assert.That(SailRaceRightOfWay.GiveWay(b, a, Vector2.left, SailRaceFixtures.Rules), Is.EqualTo("racer.a"));
        }

        [Test]
        public void OvertakingBoatKeepsClear_EvenWhenOtherwiseLeeward()
        {
            var follower = Boat("racer.fast", new Vector2(-1, -2), velocity: Vector2.up * 2);
            var leader = Boat("racer.slow", Vector2.zero);
            Assert.That(SailRaceRightOfWay.GiveWay(follower, leader, Vector2.left, SailRaceFixtures.Rules), Is.EqualTo("racer.fast"));
            var notClosing = Boat("racer.fast", new Vector2(-1, -2), velocity: Vector2.up);
            Assert.That(SailRaceRightOfWay.GiveWay(notClosing, leader, Vector2.left, SailRaceFixtures.Rules), Is.EqualTo("racer.slow"));
        }

        [Test]
        public void CalmOrIndeterminateEntitlement_AssignsNoInventedTack()
        {
            var a = Boat("racer.a", Vector2.right); var b = Boat("racer.b", Vector2.left);
            Assert.That(SailRaceRightOfWay.GiveWay(a, b, Vector2.zero, SailRaceFixtures.Rules), Is.Null);
            Assert.That(SailRaceRightOfWay.GiveWay(a, b, Vector2.down, SailRaceFixtures.Rules), Is.Null);
            Assert.That(SailRaceRightOfWay.GiveWay(Boat("racer.a", Vector2.up), Boat("racer.b", Vector2.down),
                Vector2.up, SailRaceFixtures.Rules), Is.Null, "Dead astern needs an actual sail-side fact.");
        }

        [Test]
        public void ContactEpisode_ChargesOnceUntilReleaseAndNeverBeforeGun()
        {
            var judge = new SailRaceRightOfWay(SailRaceFixtures.Rules, 0);
            var a = Boat("racer.a", Vector2.right); var b = Boat("racer.b", Vector2.left);
            Assert.That(judge.Observe(-1, a, b, Vector2.left), Is.Null);
            var first = judge.Observe(0, a, b, Vector2.left);
            Assert.That(first.HasValue, Is.True);
            Assert.That(first.Value.OffenderId, Is.EqualTo("racer.a"));
            Assert.That(first.Value.Seconds, Is.EqualTo(15));
            Assert.That(judge.Observe(0, b, a, Vector2.left), Is.Null);
            Assert.That(judge.Observe(1, b, a, Vector2.left), Is.Null);
            Assert.That(judge.Observe(2, a, Boat("racer.b", new Vector2(-3, 0)), Vector2.left), Is.Null,
                "Outside contact distance but inside release must stay latched.");
            Assert.That(judge.Observe(3, a, Boat("racer.b", new Vector2(-5, 0)), Vector2.left), Is.Null);
            Assert.That(judge.Observe(4, a, b, Vector2.left).HasValue, Is.True);
            Assert.Throws<ArgumentException>(() => judge.Observe(2, a, b, Vector2.left));
        }

        [Test]
        public void ApplyingContact_IsIdempotentAndAddsOnlyToCorrectedTime()
        {
            var race = SailRaceFixtures.Entrant();
            race.Advance(new RaceTrackSample(10, new Vector2(5, 10)));
            SailRaceFixtures.Round(race, 20);
            race.Advance(new RaceTrackSample(30, new Vector2(-1, 0)));
            var judge = new SailRaceRightOfWay(SailRaceFixtures.Rules, 0);
            var penalty = judge.Observe(5, Boat("racer.player", Vector2.right), Boat("racer.npc", Vector2.left), Vector2.left).Value;
            Assert.That(race.ApplyPenalty(penalty), Is.True);
            Assert.That(race.ApplyPenalty(penalty), Is.False);
            Assert.That(race.ElapsedSeconds, Is.EqualTo(28.6).Within(0.00001));
            Assert.That(race.CorrectedSeconds, Is.EqualTo(43.6).Within(0.00001));
            var late = judge.Observe(40, Boat("racer.player", Vector2.right), Boat("racer.other", Vector2.left), Vector2.left).Value;
            Assert.That(race.ApplyPenalty(late), Is.False);
            Assert.That(SailRaceFixtures.Entrant().ApplyPenalty(default), Is.False);
        }
    }
}
