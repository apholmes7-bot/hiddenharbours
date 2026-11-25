using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The door wall read off a building's baked door anchors. A gable door keeps the old reading; an
    /// eave door (the harbour gable's, in its cross gable on +X) is read as an eave and turned the
    /// right way, not a quarter-turn off.
    /// </summary>
    public class BuildingFacingDoorWallTests
    {
        const float PivotX = 200f, PivotY = 300f, Ppm = 32f, W = 6.72f, L = 8.26f;

        /// <summary>Screen anchors for a door at model (x, y), turned through 8 facings the way the
        /// bake turns them (cell i = model rotated by −45°·i), height ignored: screen x is all the wall
        /// read needs, screen y only says which gable.</summary>
        static void Anchors(float x, float y, out float[] doorX, out float[] doorY)
        {
            doorX = new float[8];
            doorY = new float[8];
            for (int i = 0; i < 8; i++)
            {
                float a = -45f * i * Mathf.Deg2Rad;
                float rx = x * Mathf.Cos(a) - y * Mathf.Sin(a);
                float ry = x * Mathf.Sin(a) + y * Mathf.Cos(a);
                doorX[i] = PivotX + rx * Ppm;
                doorY[i] = PivotY - ry * Ppm * 0.5f - 40f;   // screen y grows down; the door stands up off the ground
            }
        }

        static int WallOf(float x, float y)
        {
            Anchors(x, y, out float[] dx, out float[] dy);
            return BuildingFacing.DoorWall(dx, dy, PivotX, PivotY, 8, W, L);
        }

        [Test]
        public void AGableDoorIsReadAsAGable()
        {
            Assert.AreEqual(BuildingFacing.WallPlusY, WallOf(0f, L * 0.5f));
            Assert.AreEqual(BuildingFacing.WallPlusY, WallOf(0.8f, L * 0.5f), "an off-centre gable door is still the gable");
        }

        [Test]
        public void AnEaveDoorIsReadAsTheEave()
        {
            Assert.AreEqual(BuildingFacing.WallPlusX, WallOf(W * 0.5f + 0.6f, 0f), "the harbour gable: cross-gable bay on +X");
            Assert.AreEqual(BuildingFacing.WallPlusX, WallOf(W * 0.5f, 0f));
            Assert.AreEqual(BuildingFacing.WallMinusX, WallOf(-W * 0.5f, 0f));
        }

        [Test]
        public void TheOldReadingIsKeptWhenTheQuarterTurnCannotBeRead()
        {
            Anchors(W * 0.5f, 0f, out float[] dx, out float[] dy);
            int fromSign = BuildingFacing.DoorSign(dy, PivotY) > 0f ? BuildingFacing.WallPlusY : BuildingFacing.WallMinusY;
            Assert.AreEqual(fromSign, BuildingFacing.DoorWall(dx, dy, PivotX, PivotY, 6, W, L), "6 facings: no quarter-turn cell");
            Assert.AreEqual(fromSign, BuildingFacing.DoorWall(dx, dy, PivotX, PivotY, 8, 0f, 0f), "no footprint");
            Assert.AreEqual(fromSign, BuildingFacing.DoorWall(null, dy, PivotX, PivotY, 8, W, L), "no x anchors");
        }

        [Test]
        public void AnEaveDoorFacesSouthAtCellTwo_AndAGableDoorAtCellFour()
        {
            float[] eave = BuildingFacing.DoorBearings(BuildingFacing.WallPlusX, 8);
            float[] gable = BuildingFacing.DoorBearings(BuildingFacing.WallPlusY, 8);
            Assert.AreEqual(2, BuildingFacing.FrontFacing(eave));
            Assert.AreEqual(4, BuildingFacing.FrontFacing(gable));

            Vector2 house = new Vector2(-6.25f, 46.125f), south = house + Vector2.down * 4f;
            Assert.AreEqual(2, BuildingFacing.FacingToward(BuildingFacing.WallPlusX, 8, 0.643f, house, south));
            Assert.AreEqual(0f, BuildingFacing.DoorErrorDegrees(BuildingFacing.WallPlusX, 8, 0.643f, house, south, 2), 1e-3f);
            Assert.AreEqual(90f, BuildingFacing.DoorErrorDegrees(BuildingFacing.WallPlusY, 8, 0.643f, house, south, 2), 1e-3f,
                            "read as a gable, the same cell would point the door a quarter-turn off");
        }

        [Test]
        public void TheGableOverloadsAgreeWithTheWallOverloads()
        {
            Anchors(0f, L * 0.5f, out float[] dx, out float[] dy);
            Vector2 from = Vector2.zero, to = new Vector2(3f, -2f);
            for (int f = 0; f < 8; f++)
                Assert.AreEqual(BuildingFacing.DoorErrorDegrees(dy, PivotY, 8, 0.643f, from, to, f),
                                BuildingFacing.DoorErrorDegrees(BuildingFacing.WallPlusY, 8, 0.643f, from, to, f), 1e-4f);
            Assert.AreEqual(BuildingFacing.FacingToward(dy, PivotY, 8, 0.643f, from, to),
                            BuildingFacing.FacingToward(BuildingFacing.WallPlusY, 8, 0.643f, from, to));
        }
    }
}
