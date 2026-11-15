using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Pure doorway decisions: no scene, GameObject, clock or editor lifecycle.</summary>
    public class BoatCabinPassageDecisionTests
    {
        [TestCase(0.529f, 0.05f, false)]
        [TestCase(0.529f, 0.10f, false)]
        [TestCase(0.529f, 0.15f, false)]
        [TestCase(0.529f, 0.20f, false)]
        [TestCase(1.6f, 0.05f, false)]
        [TestCase(1.6f, 0.10f, false)]
        [TestCase(1.6f, 0.15f, false)]
        [TestCase(1.6f, 0.20f, false)]
        [TestCase(0.529f, 0.05f, true)]
        [TestCase(0.529f, 0.10f, true)]
        [TestCase(0.529f, 0.15f, true)]
        [TestCase(0.529f, 0.20f, true)]
        [TestCase(1.6f, 0.05f, true)]
        [TestCase(1.6f, 0.10f, true)]
        [TestCase(1.6f, 0.15f, true)]
        [TestCase(1.6f, 0.20f, true)]
        public void TurnBack_LeavingTheBandRestoresTheActualSide(float width, float seconds, bool startInside)
        {
            var walk = new Passage(width, startInside);
            Vector2 through = startInside ? Passage.Out : -Passage.Out;
            Assert.IsTrue(walk.At(Passage.Wall, through, 0f));
            Assert.AreEqual(!startInside, walk.Inside);
            Assert.IsFalse(walk.At(Passage.Wall, -through, seconds * 0.5f),
                "a reversal still on the sill waits for the door's settle");

            Vector2 back = walk.ClearPoint(startInside);
            Assert.IsFalse(BoatCabinThreshold.IsInBand(walk.Door, back));
            Assert.IsTrue(walk.At(back, -through, seconds), "departure resolves before or after the settle");
            Assert.AreEqual(startInside, walk.Inside);
            Assert.IsFalse(walk.At(back, Vector2.zero, seconds + walk.Door.CrossingSettle));
            Assert.AreEqual(startInside, walk.Inside, "no stale state after the settle expires");
            Assert.AreEqual(2, walk.Crossings);
        }

        [TestCase(0.529f)]
        [TestCase(1.6f)]
        public void WalkStraightThrough_EndsInside(float width)
        {
            var walk = new Passage(width);
            Assert.IsTrue(walk.At(Passage.Wall, -Passage.Out, 0f));
            Assert.IsFalse(walk.At(Passage.Wall, -Passage.Out, 0.05f));
            Assert.IsFalse(walk.At(walk.ClearPoint(true), -Passage.Out, 0.10f));
            Assert.IsFalse(walk.At(walk.ClearPoint(true), Vector2.zero, 0.20f));
            Assert.IsTrue(walk.Inside);
            Assert.AreEqual(1, walk.Crossings);
        }

        [TestCase(0.529f, false)]
        [TestCase(0.529f, true)]
        [TestCase(0.74f, false)]
        [TestCase(0.74f, true)]
        [TestCase(1.6f, false)]
        [TestCase(1.6f, true)]
        public void WideEnoughDoor_IdleDepartureDoesNotConsumeTheNextHeldCrossing(float width, bool startInside)
        {
            var walk = new Passage(width, startInside);
            Vector2 through = startInside ? Passage.Out : -Passage.Out;
            Assert.IsTrue(walk.At(Passage.Wall, through, 0f));

            // A lateral position outside the disc is still beside the door, not another crossing.
            Vector2 beside = BoatCabinThreshold.PointOf(walk.Door)
                             + Vector2.right * (BoatCabinThreshold.BandRadiusMetres(walk.Door) + 1f);
            Assert.IsFalse(walk.At(beside, Vector2.zero, walk.Door.CrossingSettle));
            Assert.AreEqual(!startInside, walk.Inside);
            Assert.IsTrue(walk.At(Passage.Wall, -through, walk.Door.CrossingSettle + 0.01f));
            Assert.AreEqual(startInside, walk.Inside);
            Assert.AreEqual(2, walk.Crossings);
        }

        [TestCase(0.529f)]
        [TestCase(0.74f)]
        [TestCase(1.6f)]
        public void WideEnoughDoor_AnIdleWalkerSampleNeverUndoesTheEntry(float width)
        {
            var walk = new Passage(width);
            Assert.IsTrue(walk.At(Passage.Wall, -Passage.Out, 0f));
            Assert.IsFalse(walk.At(walk.ClearPoint(false), Vector2.zero, 0.05f));
            Assert.IsTrue(walk.Inside);
            Assert.IsFalse(walk.At(walk.ClearPoint(false), Vector2.zero, walk.Door.CrossingSettle));
            Assert.IsTrue(walk.Inside);
            Assert.AreEqual(1, walk.Crossings);
        }

        [TestCase(0.529f)]
        [TestCase(1.6f)]
        public void NextTickDeparture_WithKeyStillPointingInDoesNotTurnHerBack(float width)
        {
            var walk = new Passage(width);
            Assert.IsTrue(walk.At(Passage.Wall, -Passage.Out, 0f));
            Assert.IsFalse(walk.At(walk.ClearPoint(false), -Passage.Out, 0.05f),
                "retained doorway contact cannot turn her back against the key she holds");
            Assert.IsTrue(walk.Inside);
            Assert.AreEqual(1, walk.Crossings);
        }

        [Test]
        public void ReversalStillInBand_WaitsForTheExactAuthoredSettle()
        {
            var walk = new Passage(0.529f);
            walk.Door.CrossingSettleSeconds = 0.4f;
            Assert.IsTrue(walk.At(Passage.Wall, -Passage.Out, 0f));
            Assert.IsFalse(walk.At(Passage.Wall, Passage.Out, 0.15f));
            Assert.IsFalse(walk.At(Passage.Wall, Passage.Out, 0.20f));
            Assert.IsTrue(walk.Inside);
            Assert.IsTrue(walk.At(Passage.Wall, Passage.Out, walk.Door.CrossingSettle));
            Assert.IsFalse(walk.Inside);
            Assert.AreEqual(2, walk.Crossings);
        }

        [Test]
        public void SameSideDeparture_DoesNotFollowAKeyPointingBackThroughTheWall()
        {
            var walk = new Passage(0.529f);
            Assert.IsTrue(walk.At(Passage.Wall, -Passage.Out, 0f));
            Assert.IsFalse(walk.At(walk.ClearPoint(true), Passage.Out, 0.20f));
            Assert.IsTrue(walk.Inside);
            Assert.AreEqual(1, walk.Crossings);
        }

        [Test]
        public void NoRecentBandContact_DoesNotCrossAnUnrelatedWall()
        {
            var walk = new Passage(0.529f);
            Assert.IsFalse(walk.At(walk.ClearPoint(true), -Passage.Out, 0f));
            Assert.IsFalse(walk.Inside);
            Assert.IsTrue(walk.At(Passage.Wall, -Passage.Out, 0.1f));
            Assert.IsFalse(walk.At(walk.ClearPoint(true), Vector2.zero, 0.15f));
            Assert.IsFalse(walk.At(walk.ClearPoint(false), Passage.Out, 0.20f),
                "a second tick out of the band has no doorway contact");
            Assert.IsTrue(walk.Inside);
            Assert.AreEqual(1, walk.Crossings);
        }

        [Test]
        public void LateralDepartureOnTheWall_KeepsTheSettleAndKeyGates()
        {
            var walk = new Passage(0.529f);
            Vector2 beside = Passage.Wall + Vector2.right * walk.Door.ClearWidthMeters;
            Assert.IsFalse(BoatCabinThreshold.IsInBand(walk.Door, beside));
            Assert.IsTrue(walk.At(Passage.Wall, -Passage.Out, 0f));
            Assert.IsFalse(walk.At(beside, Passage.Out, 0.05f), "on the wall has no unambiguous side");
            Assert.IsTrue(walk.Inside);

            var settled = new Passage(0.529f);
            Assert.IsFalse(settled.At(Passage.Wall, Vector2.zero, 0f));
            Assert.IsTrue(settled.At(beside, -Passage.Out, 0.05f),
                "the existing one-tick lateral slide onto the wall still crosses");
            Assert.IsTrue(settled.Inside);
        }

        [Test]
        public void NoMeasuredOpening_DoesNotReuseOldContact()
        {
            var walk = new Passage(0.529f);
            Assert.IsTrue(walk.At(Passage.Wall, -Passage.Out, 0f));
            walk.Door.ClearWidthMeters = 0f;
            Assert.IsFalse(walk.At(walk.ClearPoint(false), Passage.Out, 0.05f));
            Assert.IsTrue(walk.Inside);
        }

        private sealed class Passage
        {
            public static readonly Vector2 Wall = new(3f, -5f);
            public static readonly Vector2 Out = Vector2.down;
            public readonly BoatInteriorDoor Door;
            public bool Inside { get; private set; }
            public int Crossings { get; private set; }
            private float _lastCrossing = float.NegativeInfinity;
            private float _past = float.NaN;

            public Passage(float width, bool inside = false)
            {
                Door = new BoatInteriorDoor
                {
                    ClearWidthMeters = width,
                    ThresholdPoint = (Vector3)(Wall + Out * 0.1f),
                };
                Inside = inside;
            }

            public Vector2 ClearPoint(bool inside) => Wall + Out * (inside ? -1f : 1f) * Door.ClearWidthMeters;

            public bool At(Vector2 point, Vector2 key, float time)
            {
                if (!BoatCabinThreshold.ShouldPass(Door, Wall, Out, !Inside, point, key,
                                                  time - _lastCrossing, ref _past)) return false;
                Inside = !Inside;
                _lastCrossing = time;
                Crossings++;
                return true;
            }
        }
    }
}
