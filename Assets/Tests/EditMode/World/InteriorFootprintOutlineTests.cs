using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The polygon floor <see cref="InteriorFootprint"/> gains for mesh dwellings (ADR 0060 section 3, PR 4):
    /// the rectangle is its four-point case, a concave outline is honoured where a rectangle would not be,
    /// and the threshold is the published door point. The rectangle path itself is untouched, which the
    /// existing <see cref="InteriorFootprintTests"/> keep holding.
    /// </summary>
    public class InteriorFootprintOutlineTests
    {
        const float Depth = 0.6427876f;   // sin 40°, the shared bake camera
        const float Wd = 6.6f, Ln = 8.05f;

        static Vector2[] RectangleOutline(float w, float l) => new[]
        {
            new Vector2(-w * 0.5f, -l * 0.5f), new Vector2(w * 0.5f, -l * 0.5f),
            new Vector2(w * 0.5f, l * 0.5f), new Vector2(-w * 0.5f, l * 0.5f),
        };

        /// <summary>An L: a 2 x 6 m floor with its far right quarter missing (x 0..1, y 1..3).</summary>
        static readonly Vector2[] Ell =
        {
            new Vector2(-1f, -3f), new Vector2(1f, -3f), new Vector2(1f, 1f),
            new Vector2(0f, 1f), new Vector2(0f, 3f), new Vector2(-1f, 3f),
        };

        [Test]
        public void TheRectangleConstructor_HasNoOutline()
        {
            var room = new InteriorFootprint(Vector2.zero, Wd, Ln, 0, 8, Depth);
            Assert.That(room.HasFloorOutline, Is.False);
            Assert.That(room.FloorOutline, Is.Null);
        }

        [Test]
        public void AFourPointOutline_AnswersContainsExactlyAsTheRectangle_AtEveryFacing([Values(0.3f, 0.15f, 0f)] float inset)
        {
            var centre = new Vector2(12.5f, -40.25f);
            for (int facing = 0; facing < 8; facing++)
            {
                var rect = new InteriorFootprint(centre, Wd, Ln, facing, 8, Depth);
                var poly = new InteriorFootprint(centre, RectangleOutline(Wd, Ln), rect.WorldToModel(rect.DoorWorld),
                                                 facing, 8, Depth);

                // An off-grid step, so no sample lands exactly on an edge, where both tests are undefined.
                for (float mx = -4.1f; mx <= 4.1f; mx += 0.173f)
                for (float my = -4.9f; my <= 4.9f; my += 0.191f)
                {
                    Vector2 world = rect.ModelToWorld(new Vector2(mx, my));
                    Assert.That(poly.Contains(world, inset), Is.EqualTo(rect.Contains(world, inset)),
                        $"facing {facing}, inset {inset}, model ({mx:0.###}, {my:0.###})");
                }
            }
        }

        [Test]
        public void ANegativeInset_GrowsTheOutline_AsItGrowsTheRectangle_AwayFromTheCorners()
        {
            var rect = new InteriorFootprint(Vector2.zero, Wd, Ln, 0, 8, Depth);
            var poly = new InteriorFootprint(Vector2.zero, RectangleOutline(Wd, Ln), Vector2.zero, 0, 8, Depth);
            const float inset = -0.15f;

            foreach (var m in new[] { new Vector2(3.4f, 0f), new Vector2(3.46f, 0f), new Vector2(0f, -4.1f),
                                      new Vector2(0f, -4.2f), new Vector2(-3.44f, 1f), new Vector2(1f, 4.17f) })
            {
                Vector2 world = rect.ModelToWorld(m);
                Assert.That(poly.Contains(world, inset), Is.EqualTo(rect.Contains(world, inset)), $"model {m}");
            }
        }

        [Test]
        public void AConcaveOutline_KeepsTheNotchOutside_WhereItsEnclosingRectangleWouldNot()
        {
            var poly = new InteriorFootprint(Vector2.zero, Ell, new Vector2(1f, -1f), 0, 8, Depth);
            var enclosing = new InteriorFootprint(Vector2.zero, poly.WidthMetres, poly.LengthMetres, 0, 8, Depth);
            Vector2 notch = poly.ModelToWorld(new Vector2(0.5f, 2f));
            Vector2 floor = poly.ModelToWorld(new Vector2(-0.5f, 2f));

            Assert.That(enclosing.Contains(notch, 0.3f), Is.True, "the rectangle would let him into the notch");
            Assert.That(poly.Contains(notch, 0.3f), Is.False, "the outline does not");
            Assert.That(poly.Contains(floor, 0.3f), Is.True, "the floor beside the notch is inside");
            Assert.That(poly.Contains(poly.ModelToWorld(new Vector2(-0.9f, 2f)), 0.3f), Is.False,
                "inside the outline but within the wall is not yet inside");
        }

        [Test]
        public void TheOutlineIsHeldInTheModelFrame_SoItTurnsAndSquashesWithTheFacing()
        {
            var poly = new InteriorFootprint(new Vector2(5f, 5f), Ell, new Vector2(1f, -1f), 2, 8, Depth);
            Vector2 world = poly.ModelToWorld(new Vector2(-0.5f, 2f));
            Assert.That(poly.Contains(world, 0.3f), Is.True);
            Assert.That(poly.WorldToModel(world).x, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(poly.WorldToModel(world).y, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void WithAnOutline_TheDoorIsThePublishedThresholdPoint()
        {
            var door = new Vector2(1f, -1.25f);
            var poly = new InteriorFootprint(new Vector2(3f, 2f), Ell, door, 0, 8, Depth);
            Vector2 expected = poly.ModelToWorld(door);
            Assert.That(poly.DoorWorld.x, Is.EqualTo(expected.x).Within(1e-5f));
            Assert.That(poly.DoorWorld.y, Is.EqualTo(expected.y).Within(1e-5f));
            Assert.That(poly.DoorModelMetres, Is.EqualTo(door));
        }

        [Test]
        public void TheOutline_IsHeldNotCopied()
        {
            var poly = new InteriorFootprint(Vector2.zero, Ell, Vector2.zero, 0, 8, Depth);
            Assert.That(ReferenceEquals(poly.FloorOutline, Ell), Is.True,
                "the footprint is rebuilt every frame; copying the outline would allocate every frame");
        }

        [Test]
        public void AnOutlineThatDoesNotClose_IsRefused()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new InteriorFootprint(Vector2.zero, new[] { Vector2.zero, Vector2.one }, Vector2.zero, 0, 8, Depth));
            Assert.Throws<System.ArgumentException>(() =>
                new InteriorFootprint(Vector2.zero, (Vector2[])null, Vector2.zero, 0, 8, Depth));
        }
    }
}
