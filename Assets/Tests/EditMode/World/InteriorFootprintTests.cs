using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// Guards <see cref="InteriorFootprint"/> — where a baked room actually IS in world metres.
    ///
    /// <para>These are the tests that matter most in the interiors pilot, because every failure mode of
    /// this arithmetic is SILENT: a room whose walls are half a metre out, or whose depth was never
    /// squashed, still draws perfectly and simply stops the player somewhere that looks like nothing.
    /// The art can never show you that the collision is wrong.</para>
    ///
    /// <para>The squash is the headline. The world XY plane is the SQUASHED ground plane — one metre of
    /// northward ground travel draws <c>sin 40° ≈ 0.643</c> world units up the screen — so a footprint
    /// built without it is half again too deep in Y.</para>
    /// </summary>
    public class InteriorFootprintTests
    {
        const float Depth = 0.6427876f;        // sin 40°, the shared bake camera
        const float Wd = 6.6f;                 // the sage cottage: 6 + 0.25*2.4
        const float Ln = 8.05f;                // 7 + 0.25*4.2

        static InteriorFootprint Room(int facing = 0, Vector2 centre = default) =>
            new InteriorFootprint(centre, Wd, Ln, facing, 8, Depth);

        // ---- the squash --------------------------------------------------------------------------

        [Test]
        public void FacingNorth_SquashesTheDepthAxisAndLeavesTheWidthAlone()
        {
            var room = Room();

            // A metre across the room is a full world unit; a metre toward the back wall is sin(40°).
            Vector2 across = room.ModelToWorld(new Vector2(1f, 0f));
            Vector2 back = room.ModelToWorld(new Vector2(0f, 1f));

            Assert.AreEqual(1f, across.x, 1e-4f, "one metre across draws a full world unit");
            Assert.AreEqual(0f, across.y, 1e-4f);
            Assert.AreEqual(0f, back.x, 1e-4f);
            Assert.AreEqual(Depth, back.y, 1e-4f,
                            "one metre of NORTHWARD ground travel draws sin(40°) up the screen — a " +
                            "footprint that skips this is half again too deep and stops the player " +
                            "three metres past the drawn back wall");
        }

        [Test]
        public void TheDrawnDepthIsShorterThanTheGroundDepth_ByExactlyTheCameraTerm()
        {
            Vector2[] c = Room().Corners();
            float worldDepth = Mathf.Abs(c[3].y - c[0].y);      // front-left to back-left
            float worldWidth = Mathf.Abs(c[1].x - c[0].x);

            Assert.AreEqual(Wd, worldWidth, 1e-3f, "width is unforeshortened");
            Assert.AreEqual(Ln * Depth, worldDepth, 1e-3f,
                            $"an {Ln} m room draws {Ln * Depth:0.00} world units deep, not {Ln}");
        }

        // ---- round trip --------------------------------------------------------------------------

        [Test]
        public void WorldToModel_InvertsModelToWorld_AtEveryFacing()
        {
            var probe = new[]
            {
                new Vector2(0f, 0f), new Vector2(2.1f, -3.4f),
                new Vector2(-3.2f, 3.9f), new Vector2(1f, 1f),
            };

            for (int facing = 0; facing < 8; facing++)
            {
                var room = Room(facing, new Vector2(120f, -45f));
                foreach (Vector2 p in probe)
                {
                    Vector2 back = room.WorldToModel(room.ModelToWorld(p));
                    Assert.AreEqual(p.x, back.x, 1e-3f, $"x round-trip at facing {facing}");
                    Assert.AreEqual(p.y, back.y, 1e-3f, $"y round-trip at facing {facing}");
                }
            }
        }

        // ---- inside / outside --------------------------------------------------------------------

        [Test]
        public void Contains_UsesTheINNERRectangle_SoTheWallIsNotInside()
        {
            var room = Room();
            const float wall = 0.3f;

            // Dead centre of the floor: inside at any inset.
            Assert.IsTrue(room.Contains(room.ModelToWorld(Vector2.zero), wall));

            // Standing IN the front wall (just inside the footprint but not past the wall).
            Vector2 inWall = room.ModelToWorld(new Vector2(0f, -Ln * 0.5f + wall * 0.5f));
            Assert.IsFalse(room.Contains(inWall, wall),
                           "you are inside once you are PAST the wall — the same moment the doorway " +
                           "lets you through. Counting the wall itself as inside opens the house the " +
                           "instant you touch it from outside.");

            // Past it.
            Vector2 pastWall = room.ModelToWorld(new Vector2(0f, -Ln * 0.5f + wall * 1.5f));
            Assert.IsTrue(room.Contains(pastWall, wall));
        }

        [Test]
        public void Contains_RejectsAPointOutsideAtEveryFacing_EvenTheDiagonals()
        {
            // Two metres beyond the back-right corner in the model frame — outside at every rotation,
            // and the diagonals are where a footprint that forgot the shear starts accepting points.
            for (int facing = 0; facing < 8; facing++)
            {
                var room = Room(facing);
                Vector2 outside = room.ModelToWorld(new Vector2(Wd * 0.5f + 2f, Ln * 0.5f + 2f));
                Assert.IsFalse(room.Contains(outside, 0.3f), $"facing {facing}");
            }
        }

        // ---- the threshold -----------------------------------------------------------------------

        [Test]
        public void TheDoorIsOnTheFrontWall_AndMovesRoundWithTheFacing()
        {
            // Facing 0: the front (−y) wall is nearest the camera, so the door is BELOW the centre.
            var north = Room();
            Assert.Less(north.DoorWorld.y, north.Centre.y,
                        "at facing 0 the doorway is the near wall — below the floor centre on screen");
            Assert.AreEqual(0f, north.DoorWorld.x, 1e-3f, "and centred across the room");

            // Facing 4 turns the room 180°, so the doorway swings to the far side.
            var south = Room(4);
            Assert.Greater(south.DoorWorld.y, south.Centre.y,
                           "half a turn puts the doorway on the far wall");

            // The distance from the centre is the same at both — it is the same door.
            Assert.AreEqual(Mathf.Abs(north.DoorWorld.y - north.Centre.y),
                            Mathf.Abs(south.DoorWorld.y - south.Centre.y), 1e-3f);
        }

        [Test]
        public void TheDoorIsAlwaysExactlyHalfTheRoomLengthFromTheCentre_OnTheGround()
        {
            for (int facing = 0; facing < 8; facing++)
            {
                var room = Room(facing, new Vector2(-30f, 12f));
                Vector2 model = room.WorldToModel(room.DoorWorld);
                Assert.AreEqual(0f, model.x, 1e-3f, $"facing {facing}: centred across the front wall");
                Assert.AreEqual(-Ln * 0.5f, model.y, 1e-3f, $"facing {facing}: on the front wall");
            }
        }

        // ---- the walls ---------------------------------------------------------------------------

        [Test]
        public void WallQuads_AreFiveQuads_WithTheFrontWallSplitRoundTheDoorway()
        {
            Vector2[][] walls = Room().WallQuads(0.3f, 1.4f);

            Assert.AreEqual(5, walls.Length,
                            "back, left, right, and the front wall in two pieces round the doorway");
            foreach (Vector2[] quad in walls) Assert.AreEqual(4, quad.Length);
        }

        [Test]
        public void TheDoorwayIsTheOneGap_AndTheDroppedNearWallStillBlocks()
        {
            var room = Room();
            const float wall = 0.3f, gap = 1.4f;
            Vector2[][] walls = room.WallQuads(wall, gap);

            // The gap: no wall quad covers the middle of the front wall.
            Assert.IsFalse(AnyQuadContains(walls, room.ModelToWorld(new Vector2(0f, -Ln * 0.5f + wall * 0.5f))),
                           "the doorway is a real hole in the front wall");

            // ⭐ Half a metre either side of the gap IS wall — and at this facing the FRONT wall is one
            // of the two the open-dollhouse cutaway drops, so there are no pixels there at all. It
            // still blocks: the cutaway is a courtesy to the camera, not a hole in the house.
            float justOutside = gap * 0.5f + 0.4f;
            Assert.IsTrue(AnyQuadContains(walls, room.ModelToWorld(new Vector2(justOutside, -Ln * 0.5f + wall * 0.5f))),
                          "the DROPPED front wall resumes beside the doorway and still blocks");
            Assert.IsTrue(AnyQuadContains(walls, room.ModelToWorld(new Vector2(-justOutside, -Ln * 0.5f + wall * 0.5f))));

            Assert.IsTrue(AnyQuadContains(walls, room.ModelToWorld(new Vector2(Wd * 0.5f - wall * 0.5f, 0f))),
                          "and the drawn side walls block too");
        }

        [Test]
        public void WallQuads_DoNotOverlap_SoMultiPathCollidersCannotOpenACornerHole()
        {
            // Several paths on one PolygonCollider2D turn their overlaps into HOLES. Four walls sharing
            // their corners would leave a gap at every corner of the house, and the only symptom would
            // be a player who occasionally slips through one.
            Vector2[][] walls = Room().WallQuads(0.3f, 1.4f);

            for (int i = 0; i < walls.Length; i++)
                for (int j = i + 1; j < walls.Length; j++)
                    Assert.IsFalse(QuadsOverlap(walls[i], walls[j]),
                                   $"wall {i} and wall {j} overlap");
        }

        // ---- props -------------------------------------------------------------------------------

        [Test]
        public void PropQuad_IsCentredOnItsFloorPoint_WhichIsWhyAPropNeedsNoOffsetMaths()
        {
            var room = Room(3);
            var at = new Vector2(1.2f, -0.6f);

            Vector2[] quad = room.PropQuad(at, width: 2.0f, depth: 0.8f, propFacingOffset: 0);

            Vector2 centre = Vector2.zero;
            foreach (Vector2 p in quad) centre += p;
            centre /= quad.Length;

            Vector2 expected = room.ModelToWorld(at);
            Assert.AreEqual(expected.x, centre.x, 1e-3f);
            Assert.AreEqual(expected.y, centre.y, 1e-3f,
                            "the prop rig's origin is the floor CENTRE of its own footprint — the same " +
                            "convention as the room's pivot, which is the whole reason a prop drops " +
                            "onto a room floor point with no offset maths");
        }

        [Test]
        public void PropQuad_TurnsWithItsFacingOffset()
        {
            var room = Room();
            var at = new Vector2(0f, 0f);

            Vector2[] straight = room.PropQuad(at, 2.0f, 0.5f, 0);
            Vector2[] quarter = room.PropQuad(at, 2.0f, 0.5f, 2);

            Assert.AreEqual(2.0f, Width(straight), 1e-3f, "unturned, the 2 m axis is across the screen");
            Assert.AreEqual(0.5f, Width(quarter), 1e-3f,
                            "a quarter turn swaps them — which is how a dresser sits with its back to " +
                            "a side wall");
        }

        // ---- helpers -----------------------------------------------------------------------------

        static float Width(Vector2[] quad)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Vector2 p in quad) { lo = Mathf.Min(lo, p.x); hi = Mathf.Max(hi, p.x); }
            return hi - lo;
        }

        static bool AnyQuadContains(Vector2[][] quads, Vector2 p)
        {
            foreach (Vector2[] q in quads) if (PolygonContains(q, p)) return true;
            return false;
        }

        /// <summary>Even-odd ray cast — the quads are convex but may be sheared, so a cross-product
        /// test would need a consistent winding this does not want to assume.</summary>
        static bool PolygonContains(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (poly[i].y > p.y == poly[j].y > p.y) continue;
                float x = (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x;
                if (p.x < x) inside = !inside;
            }
            return inside;
        }

        /// <summary>True if the two quads share any interior area — sampled on a grid over their
        /// bounding-box intersection, which is enough to catch a whole shared corner.</summary>
        static bool QuadsOverlap(Vector2[] a, Vector2[] b)
        {
            Bounds ba = BoundsOf(a), bb = BoundsOf(b);
            if (!ba.Intersects(bb)) return false;

            float x0 = Mathf.Max(ba.min.x, bb.min.x), x1 = Mathf.Min(ba.max.x, bb.max.x);
            float y0 = Mathf.Max(ba.min.y, bb.min.y), y1 = Mathf.Min(ba.max.y, bb.max.y);

            const int N = 12;
            for (int i = 1; i < N; i++)
                for (int j = 1; j < N; j++)
                {
                    var p = new Vector2(Mathf.Lerp(x0, x1, i / (float)N), Mathf.Lerp(y0, y1, j / (float)N));
                    if (PolygonContains(a, p) && PolygonContains(b, p)) return true;
                }
            return false;
        }

        static Bounds BoundsOf(Vector2[] quad)
        {
            var b = new Bounds(quad[0], Vector3.zero);
            foreach (Vector2 p in quad) b.Encapsulate(p);
            return b;
        }
    }
}
