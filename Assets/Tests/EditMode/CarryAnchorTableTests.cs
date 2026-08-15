using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The carry-anchor table's own rules</b>, on synthetic numbers — which anchor a row hangs from,
    /// when the live wrist is used and when the rest pin stands in.
    ///
    /// <para>Deliberately no shipped assets here: <c>CarryAnchorImportTests</c> is where the real table is
    /// checked against the real sidecar. This file pins the DECISIONS, so a failure names the rule rather
    /// than sending you to re-bake art.</para>
    ///
    /// <para><b>The one that matters most is the fallback.</b> A carried thing must never be posed from a
    /// grip offset with no anchor under it — that is a half-metre error, not a small one, because the grip
    /// is measured from the wrist and the wrist is 0.2 m out from the body. So every path that cannot find
    /// a live wrist has to land on the rest pin, and each of those paths is pinned separately below.</para>
    /// </summary>
    public class CarryAnchorTableTests
    {
        const int Directions = 8;

        // Chosen so every quantity is distinguishable in a failure message: the wrists are a metre apart,
        // the grip is a tenth, and the rest pin is nowhere near either.
        static readonly Vector2 LeftWrist = new Vector2(-0.5f, 1.0f);
        static readonly Vector2 RightWrist = new Vector2(0.5f, 1.0f);
        static readonly Vector2 Grip = new Vector2(0.1f, 0.02f);
        static readonly Vector2 RestPin = new Vector2(9f, 9f);

        static CarryAnchorTableDef NewTable(CarryHandSide hand, CarryMountKind mount,
                                            int framesPerDir = 4, bool withWrists = true)
        {
            var t = ScriptableObject.CreateInstance<CarryAnchorTableDef>();
            t.Id = "carryanchors.test";
            t.FacingCount = Directions;

            var rows = new CarryAnchorRow[Directions];
            for (int d = 0; d < Directions; d++)
                rows[d] = new CarryAnchorRow
                {
                    Hand = hand,
                    Mount = mount,
                    GripOffsetMeters = Grip,
                    RestOffsetMeters = RestPin,
                    ItemFacing = d,
                    Behind = d == 3,
                };
            t.Props = new[]
            {
                new CarryPropAnchors { PropKey = "clam", Rig = "Shellfish", ItemScale = 1f, Rows = rows },
            };

            if (!withWrists) { t.Wrists = System.Array.Empty<CarryWristFrames>(); return t; }

            var left = new Vector2[Directions * framesPerDir];
            var right = new Vector2[Directions * framesPerDir];
            for (int d = 0; d < Directions; d++)
                for (int f = 0; f < framesPerDir; f++)
                {
                    // Frame-varying, so a test that reads the wrong frame cannot pass by luck.
                    int i = d * framesPerDir + f;
                    left[i] = LeftWrist + new Vector2(0f, f * 0.01f);
                    right[i] = RightWrist + new Vector2(0f, f * 0.01f);
                }

            // Only the WALK gait is given a grid — so "idle" is this fixture's no-live-anchor case.
            t.Wrists = new[]
            {
                new CarryWristFrames
                {
                    Gait = CharacterGait.Walk, FramesPerDir = framesPerDir, Left = left, Right = right,
                },
            };
            return t;
        }

        // ---- lookup ----------------------------------------------------------------------------------

        [Test]
        public void AnUnknownProp_AnEmptyKey_AndAFacingOffTheEnd_AllAnswerFalse_NotAThrow()
        {
            var t = NewTable(CarryHandSide.Right, CarryMountKind.Hand);

            // Every one of these is an ORDINARY state, not a fault: a jerry can names no prop, the shovel
            // has no baked row, and a four-facing skin would ask for rows this table does not have. All
            // three mean "the carrier keeps its own offset", and none of them may throw on the way there.
            Assert.That(t.TryRow(null, 0, out _), Is.False, "a null key");
            Assert.That(t.TryRow("", 0, out _), Is.False, "an empty key");
            Assert.That(t.TryRow("shovelTrail", 0, out _), Is.False, "a prop with no baked row");
            Assert.That(t.TryRow("clam", Directions, out _), Is.False, "a facing off the end");
            Assert.That(t.TryRow("clam", -1, out _), Is.False, "a negative facing");
        }

        [Test]
        public void AnEmptyTable_ReportsNoRows_SoAConsumerCanTellItApartFromAWiredOne()
        {
            var empty = ScriptableObject.CreateInstance<CarryAnchorTableDef>();
            Assert.That(empty.HasRows, Is.False);
            Assert.That(NewTable(CarryHandSide.Right, CarryMountKind.Hand).HasRows, Is.True);
        }

        // ---- the live anchor -------------------------------------------------------------------------

        [Test]
        public void AHandMountedRow_IsPinnedToTheLiveWrist_PlusItsGrip()
        {
            var t = NewTable(CarryHandSide.Right, CarryMountKind.Hand);
            Assert.That(t.TryRow("clam", 2, out CarryAnchorRow row), Is.True);

            Vector2 pin = t.PinMeters(row, CharacterGait.Walk, facingRow: 2, frame: 3, out bool live);

            Assert.That(live, Is.True, "the walk grid is wired, so this must be the live path");
            Vector2 wrist = RightWrist + new Vector2(0f, 3 * 0.01f);
            Assert.That((pin - (wrist + Grip)).magnitude, Is.LessThan(1e-5f),
                        "the pin is the wrist for THIS frame plus the frame-independent grip");
        }

        [Test]
        public void TheLeftHandRow_ReadsTheLeftWrist_NotTheRight()
        {
            var t = NewTable(CarryHandSide.Left, CarryMountKind.Hand);
            t.TryRow("clam", 5, out CarryAnchorRow row);

            Vector2 pin = t.PinMeters(row, CharacterGait.Walk, 5, 0, out bool live);

            Assert.That(live, Is.True);
            // The two wrists are a whole metre apart: reading the wrong one is not a subtle error, which
            // is exactly why the swap is worth a test — the fish and the clam change hands at three of the
            // eight headings, and taking the unswapped hand would put them through the fisher's chest.
            Assert.That((pin - (LeftWrist + Grip)).magnitude, Is.LessThan(1e-5f));
            Assert.That((pin - (RightWrist + Grip)).magnitude, Is.GreaterThan(0.5f));
        }

        [Test]
        public void ATwoHandedRow_HangsFromTheMidpointOfTheWrists()
        {
            var t = NewTable(CarryHandSide.Both, CarryMountKind.Hand);
            t.TryRow("clam", 0, out CarryAnchorRow row);

            Vector2 pin = t.PinMeters(row, CharacterGait.Walk, 0, 0, out bool live);

            Assert.That(live, Is.True);
            Assert.That((pin - ((LeftWrist + RightWrist) * 0.5f + Grip)).magnitude, Is.LessThan(1e-5f));
        }

        [Test]
        public void AFrameBeyondTheGrid_WRAPS_RatherThanDroppingToTheRestPin()
        {
            var t = NewTable(CarryHandSide.Right, CarryMountKind.Hand, framesPerDir: 4);
            t.TryRow("clam", 1, out CarryAnchorRow row);

            // A body whose sheet is longer than the bake would otherwise fall back for exactly one cell —
            // a single-frame jump to the rest pin and back, which reads as a twitch nobody can source.
            Vector2 wrapped = t.PinMeters(row, CharacterGait.Walk, 1, frame: 5, out bool live);
            Vector2 direct = t.PinMeters(row, CharacterGait.Walk, 1, frame: 1, out _);

            Assert.That(live, Is.True, "a frame past the end still resolves live");
            Assert.That((wrapped - direct).magnitude, Is.LessThan(1e-5f));
        }

        // ---- every road to the rest pin ---------------------------------------------------------------

        [Test]
        public void AGaitWithNoWristGrid_FallsBackToTheRestPin()
        {
            var t = NewTable(CarryHandSide.Right, CarryMountKind.Hand);
            t.TryRow("clam", 0, out CarryAnchorRow row);

            Vector2 pin = t.PinMeters(row, CharacterGait.Idle, 0, 0, out bool live);

            Assert.That(live, Is.False);
            Assert.That(pin, Is.EqualTo(RestPin));
        }

        [Test]
        public void NoWristGridAtAll_FallsBackToTheRestPin()
        {
            var t = NewTable(CarryHandSide.Right, CarryMountKind.Hand, withWrists: false);
            t.TryRow("clam", 0, out CarryAnchorRow row);

            Assert.That(t.PinMeters(row, CharacterGait.Walk, 0, 0, out bool live), Is.EqualTo(RestPin));
            Assert.That(live, Is.False);
        }

        [Test]
        public void ABackMountedRow_NEVER_TracksAWrist_EvenWhenTheGridIsThere()
        {
            var t = NewTable(CarryHandSide.None, CarryMountKind.Back);
            t.TryRow("clam", 0, out CarryAnchorRow row);

            // The sling hangs off a point up the TORSO, and the fraction of the way from hip to head is a
            // rig constant the sidecar does not publish. Tracking it live would mean restating that number
            // here — a tuned constant lifted onto a new lever, which is the mistake the overlay pose paid
            // for. So the back mount takes its rest pin and the table says so out loud.
            Assert.That(t.PinMeters(row, CharacterGait.Walk, 0, 0, out bool live), Is.EqualTo(RestPin));
            Assert.That(live, Is.False);
        }

        [Test]
        public void AShortWristArray_FallsBackRatherThanIndexingPastItsEnd()
        {
            var t = NewTable(CarryHandSide.Right, CarryMountKind.Hand, framesPerDir: 4);
            // A truncated grid is what a half-written import would leave. It must be survivable.
            t.Wrists[0].Right = new Vector2[3];
            t.TryRow("clam", 7, out CarryAnchorRow row);

            Assert.That(t.PinMeters(row, CharacterGait.Walk, 7, 0, out bool live), Is.EqualTo(RestPin));
            Assert.That(live, Is.False);
        }

        // ---- the two fields the carrier reads straight through ---------------------------------------

        [Test]
        public void TheRowCarriesTheItemsOwnTurntableCell_AndItsDrawOrder()
        {
            var t = NewTable(CarryHandSide.Right, CarryMountKind.Hand);

            t.TryRow("clam", 3, out CarryAnchorRow behind);
            t.TryRow("clam", 4, out CarryAnchorRow over);

            Assert.That(behind.ItemFacing, Is.EqualTo(3));
            Assert.That(behind.Behind, Is.True);
            Assert.That(over.Behind, Is.False);
        }

        [Test]
        public void ThePropEntry_CarriesItsArtProvenance()
        {
            CarryPropAnchors prop = NewTable(CarryHandSide.Right, CarryMountKind.Hand).Prop("clam");

            Assert.That(prop, Is.Not.Null);
            Assert.That(prop.Rig, Is.EqualTo("Shellfish"));
            Assert.That(prop.Rows.Length, Is.EqualTo(Directions));
        }
    }
}
