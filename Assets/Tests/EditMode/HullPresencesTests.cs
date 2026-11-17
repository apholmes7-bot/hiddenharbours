using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE REGISTRY OF HULLS ON THE WATER</b> — the seam that tells the wading model a boat is
    /// there, so the boat-only soft wall can step aside far enough for a swimmer to reach her rail
    /// (the owner, 2026-09-02: <i>"a player should be able to swim up to a hull and climb aboard
    /// anywhere"</i>).
    ///
    /// <para>Pure: every case drives the list-taking overloads with hand-built footprints, so nothing
    /// here needs a scene, a boat or a GameObject. The live <c>Now</c> twins are exercised through
    /// <see cref="HullPresences.Register"/> so the registry and the pure rule cannot drift apart.</para>
    /// </summary>
    public class HullPresencesTests
    {
        /// <summary>A hull that is exactly where the test says it is. The whole seam is one property.</summary>
        private sealed class FakeHull : IHullPresence
        {
            public HullFootprint Footprint { get; set; }

            public static FakeHull At(Vector2 centre, float headingDegrees, float length, float halfBeam)
                => new FakeHull { Footprint = HullFootprint.FromHeading(centre, headingDegrees, length, halfBeam) };
        }

        // The starter dory's real numbers (Data/Boats/Dory.asset + DoryIsoHullMesh.asset), and the cape
        // islander's, so the cases below are about boats this game actually floats.
        const float DoryLength = 4.5f, DoryHalfBeam = 0.85f;
        const float CapeLength = 12.9f, CapeHalfBeam = 2.4f;

        [SetUp]
        public void SetUp() => HullPresences.Clear();

        [TearDown]
        public void TearDown() => HullPresences.Clear();

        // ---- the registry's contract ----------------------------------------------------------------

        [Test]
        public void AnEmptyRegistryIsAnAnswer_NotAFault()
        {
            Assert.AreEqual(0, HullPresences.Count, "nothing registered");
            Assert.AreEqual(float.PositiveInfinity, HullPresences.DistanceToNearestOutlineNow(Vector2.zero),
                "no hulls anywhere means infinitely far from one — a region with no boats is valid");
            Assert.IsFalse(HullPresences.WithinReachNow(Vector2.zero, 6f),
                "…and nothing is alongside anything, so the boat-only wall stands everywhere");
        }

        [Test]
        public void RegisteringTwiceCountsHerOnce_AndUnregisteringAStrangerIsHarmless()
        {
            var dory = FakeHull.At(new Vector2(10f, 0f), 90f, DoryLength, DoryHalfBeam);
            HullPresences.Register(dory);
            HullPresences.Register(dory);
            Assert.AreEqual(1, HullPresences.Count, "one boat enabled twice is still one boat");

            HullPresences.Unregister(FakeHull.At(Vector2.zero, 0f, DoryLength, DoryHalfBeam));
            Assert.AreEqual(1, HullPresences.Count, "removing something that was never in it is a no-op");

            HullPresences.Register(null);
            Assert.AreEqual(1, HullPresences.Count, "a null registrant is a no-op, not an entry");

            HullPresences.Unregister(dory);
            Assert.AreEqual(0, HullPresences.Count, "she relinquishes on disable");
        }

        [Test]
        public void AHullThatGoesAway_TakesHerHoleInTheWallWithHer()
        {
            var dory = FakeHull.At(Vector2.zero, 0f, DoryLength, DoryHalfBeam);
            HullPresences.Register(dory);
            Assert.IsTrue(HullPresences.WithinReachNow(new Vector2(4f, 0f), 6f), "alongside her");

            HullPresences.Unregister(dory);
            Assert.IsFalse(HullPresences.WithinReachNow(new Vector2(4f, 0f), 6f),
                "a boat that has left must not leave a swimmable hole behind her — this is the leak that " +
                "would let a player swim out through a berth after the region unloaded the boat in it");
        }

        // ---- the measurement: to the OUTLINE, never to the root --------------------------------------

        /// <summary>
        /// ⭐ <b>The law this arc has already paid for twice.</b> A 12.9 m cape measured to her ROOT is
        /// metres away from a swimmer holding on to her stern. The same swimmer measured to her OUTLINE
        /// is touching her.
        /// </summary>
        [Test]
        public void DistanceIsToTheOutline_SoALongHullIsNotFarFromHerOwnStern()
        {
            // Bow north, so her stern is 6.45 m south of her centre.
            var cape = FakeHull.At(Vector2.zero, 0f, CapeLength, CapeHalfBeam);
            var hulls = new IHullPresence[] { cape };

            Vector2 atHerStern = new Vector2(0f, -CapeLength * 0.5f - 0.2f);   // 20 cm off her transom
            Assert.AreEqual(0.2f, HullPresences.DistanceToNearestOutline(hulls, atHerStern), 1e-4f,
                "20 cm off her transom is 20 cm from the boat");
            Assert.AreEqual(6.65f, Vector2.Distance(Vector2.zero, atHerStern), 1e-4f,
                "…while the ROOT reading — the one this replaces — calls the same swimmer 6.65 m away");
        }

        [Test]
        public void InsideHerOutlineIsZero_NotSomeNegativeNumber()
        {
            var dory = FakeHull.At(new Vector2(5f, 5f), 45f, DoryLength, DoryHalfBeam);
            Assert.AreEqual(0f, HullPresences.DistanceToNearestOutline(new IHullPresence[] { dory },
                                                                      new Vector2(5f, 5f)), 1e-5f,
                "amidships is 0 m from the hull, and a reach test must read that as 'alongside'");
        }

        [Test]
        public void TheNearestHullWins_AndANullEntryIsSkippedRatherThanThrownOn()
        {
            var far = FakeHull.At(new Vector2(100f, 0f), 0f, CapeLength, CapeHalfBeam);
            var near = FakeHull.At(new Vector2(10f, 0f), 0f, DoryLength, DoryHalfBeam);
            var hulls = new IHullPresence[] { far, null, near };

            // 10 m east, dory bow-north: her outline reaches 0.85 m abeam, so 4 m away is 5.15 m off her.
            Assert.AreEqual(5.15f, HullPresences.DistanceToNearestOutline(hulls, new Vector2(4f, 0f)), 1e-4f,
                "the nearest outline is the answer, and a destroyed registrant must not take the model down");
        }

        [Test]
        public void ANullListIsInfinitelyFarFromAHull()
            => Assert.AreEqual(float.PositiveInfinity,
                               HullPresences.DistanceToNearestOutline(null, Vector2.zero));

        // ---- the reach predicate: a mis-authored tunable must CLOSE the wall, never open it -----------

        [Test]
        public void ReachIsInclusive_AtExactlyTheReachSheIsAlongside()
        {
            var dory = FakeHull.At(Vector2.zero, 0f, DoryLength, DoryHalfBeam);
            var hulls = new IHullPresence[] { dory };

            // Off her beam: the outline is 0.85 m out, so 6.85 m from her centre is exactly 6.00 m off her.
            Assert.IsTrue(HullPresences.WithinReachOf(hulls, new Vector2(6.85f, 0f), 6f),
                "exactly at the reach counts — the boundary belongs to the boat");
            Assert.IsFalse(HullPresences.WithinReachOf(hulls, new Vector2(6.87f, 0f), 6f),
                "…and a couple of centimetres past it does not");
        }

        [Test]
        public void AZeroOrNonsenseReachClosesTheWall_ItNeverOpensItEverywhere()
        {
            var dory = FakeHull.At(Vector2.zero, 0f, DoryLength, DoryHalfBeam);
            var hulls = new IHullPresence[] { dory };
            Vector2 alongside = new Vector2(1.0f, 0f);   // 0.15 m off her rail, abeam

            Assert.IsFalse(HullPresences.WithinReachOf(hulls, alongside, 0f),
                "a reach of zero is 'the wall stands', not 'touching counts'");
            Assert.IsFalse(HullPresences.WithinReachOf(hulls, alongside, -1f), "negative likewise");
            Assert.IsFalse(HullPresences.WithinReachOf(hulls, alongside, float.NaN),
                "NaN is a mis-authored tunable — it must not open the sea");
            Assert.IsFalse(HullPresences.WithinReachOf(hulls, alongside, float.PositiveInfinity),
                "⭐ and an infinite reach must NOT make the whole ocean swimmable — the one failure mode " +
                "of this seam that would quietly delete the owner's boats-only rule");
        }

        [Test]
        public void TheLiveTwinsReadTheSameRuleAsThePureOnes()
        {
            var dory = FakeHull.At(new Vector2(213.5f, 4.25f), 90f, DoryLength, DoryHalfBeam);
            HullPresences.Register(dory);

            var probe = new Vector2(213.5f, 9f);
            Assert.AreEqual(HullPresences.DistanceToNearestOutline(HullPresences.Active, probe),
                            HullPresences.DistanceToNearestOutlineNow(probe), 1e-6f);
            Assert.AreEqual(HullPresences.WithinReachOf(HullPresences.Active, probe, 6f),
                            HullPresences.WithinReachNow(probe, 6f));
        }
    }
}
