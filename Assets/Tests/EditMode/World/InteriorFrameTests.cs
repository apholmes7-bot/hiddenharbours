using System.Linq;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <see cref="InteriorFrame"/>: a doorway on a side wall is the same room described from a frame
    /// turned a quarter. The world rectangle and the world doorway must not move.
    /// </summary>
    public class InteriorFrameTests
    {
        const float W = 6.72f, L = 8.26f, Depth = 0.6427876f;

        static InteriorFootprint Footprint(InteriorFrame.Frame f) =>
            new InteriorFootprint(new Vector2(-6.25f, 46.125f), f.WidthMetres, f.LengthMetres, f.Facing, 8,
                                  Depth, f.DoorSign, f.DoorAcrossMetres);

        static InteriorFootprint Room(int facing) =>
            new InteriorFootprint(new Vector2(-6.25f, 46.125f), W, L, facing, 8, Depth, 1f, 0f);

        [Test]
        public void AGableDoorwayKeepsTheRoomsOwnFrame()
        {
            for (int facing = 0; facing < 8; facing++)
            {
                InteriorFrame.Frame plus = InteriorFrame.For(W, L, facing, 8, new Vector2(0.4f, L * 0.5f));
                Assert.AreEqual(W, plus.WidthMetres);
                Assert.AreEqual(L, plus.LengthMetres);
                Assert.AreEqual(facing, plus.Facing);
                Assert.AreEqual(1f, plus.DoorSign);
                Assert.AreEqual(0.4f, plus.DoorAcrossMetres);

                InteriorFrame.Frame minus = InteriorFrame.For(W, L, facing, 8, new Vector2(-1f, -L * 0.5f));
                Assert.AreEqual(facing, minus.Facing);
                Assert.AreEqual(-1f, minus.DoorSign);
                Assert.AreEqual(-1f, minus.DoorAcrossMetres);
            }
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void ASideDoorwayTurnsTheFrameAQuarter_AndNothingMovesInTheWorld(float side)
        {
            for (int facing = 0; facing < 8; facing++)
            {
                var door = new Vector2(side * W * 0.5f, 0.7f);
                InteriorFrame.Frame f = InteriorFrame.For(W, L, facing, 8, door);
                Assert.AreEqual(L, f.WidthMetres, "width and length swap");
                Assert.AreEqual(W, f.LengthMetres);
                Assert.AreEqual(1f, f.DoorSign, "the doorway is on the frame's +y wall");
                Assert.AreEqual(((facing + (side > 0 ? 2 : -2)) % 8 + 8) % 8, f.Facing);

                InteriorFootprint room = Room(facing), framed = Footprint(f);
                Vector2 want = room.ModelToWorld(door);
                Assert.That(Vector2.Distance(want, framed.DoorWorld), Is.LessThan(1e-3f),
                            $"facing {facing}: the doorway stays where the room draws it");

                Vector2[] a = Corners(room, W, L), b = Corners(framed, L, W);
                foreach (Vector2 p in a)
                    Assert.That(b.Min(q => Vector2.Distance(p, q)), Is.LessThan(1e-3f),
                                $"facing {facing}: corner {p} of the room is a corner of the turned frame");
            }
        }

        [Test]
        public void TheHarbourGablesRoomFacesItsDoorwaySouth()
        {
            // The board's harbour gable: its door in the cross gable on +x, turned to face the lane at
            // facing 2. Its room stands at the same facing, doorway on the room's +x wall.
            InteriorFrame.Frame f = InteriorFrame.For(W, L, 2, 8, new Vector2(W * 0.5f, 0f));
            InteriorFootprint framed = Footprint(f);
            Vector2 outward = framed.DoorWorld - framed.ModelToWorld(Vector2.zero);
            Assert.That(outward.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(outward.y, Is.LessThan(0f), "the doorway faces south, toward the lane");
            Assert.That(-outward.y, Is.EqualTo(W * 0.5f * Depth).Within(1e-3f));
        }

        [Test]
        public void AFacingCountThatCannotTurnAQuarterIsLeftAlone()
        {
            InteriorFrame.Frame f = InteriorFrame.For(W, L, 1, 6, new Vector2(W * 0.5f, 0f));
            Assert.AreEqual(W, f.WidthMetres);
            Assert.AreEqual(1, f.Facing);
        }

        static Vector2[] Corners(InteriorFootprint fp, float w, float l) => new[]
        {
            fp.ModelToWorld(new Vector2(-w * 0.5f, -l * 0.5f)), fp.ModelToWorld(new Vector2(w * 0.5f, -l * 0.5f)),
            fp.ModelToWorld(new Vector2(w * 0.5f, l * 0.5f)), fp.ModelToWorld(new Vector2(-w * 0.5f, l * 0.5f)),
        };
    }
}
