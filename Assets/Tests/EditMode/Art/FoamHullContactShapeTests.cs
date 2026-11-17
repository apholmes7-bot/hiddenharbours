using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The foam parts round a hull's WATERLINE as her picture draws it: squashed north-south by her art's
    /// foreshortening and coming to a point at the stem. A plan box stood past her drawn stem and square
    /// across her bow, and the foam held still inside it showed as a pale square slab ahead of her (the
    /// owner, 2026-11-16).
    /// </summary>
    public sealed class FoamHullContactShapeTests
    {
        private sealed class DrawnHull : IHullPresence
        {
            public HullFootprint Footprint { get; set; }
            public float PictureForeshortenY { get; set; } = 1f;
        }

        // The Cape Islander's size, about (Data/Boats/CapeIslander.asset), and the rigs' 40 degree bake.
        const float Length = 12f, HalfBeam = 2.2f;
        static readonly float Squash = WakeRootMath.ForeshortenY(40f);
        static readonly HullFoamSettings Settings = HullFoamSettings.Default;
        static float Cell => FoamBuffer.CellSize;

        static FoamTransportContacts.Shape HullHeading(Vector2 bow, float squash) =>
            FoamTransportContacts.HullShape(HullFootprint.FromBowDirection(Vector2.zero, bow, Length, HalfBeam),
                                            Vector2.zero, squash, in Settings);

        static FoamTransportContacts.Shape PlanBox(Vector2 bow) =>
            new FoamTransportContacts.Shape(Vector2.zero, bow, new Vector2(Length * 0.5f, HalfBeam),
                                            -float.MaxValue, float.MaxValue, false, 0);

        [SetUp] public void SetUp() => HullPresences.Clear();
        [TearDown] public void TearDown() => HullPresences.Clear();

        [Test] public void HeadingDownTheScreen_TheSolidStopsAtHerDrawnStem()
        {
            // Bow to the south: her plan stem is 6 m down, her drawn stem 6 x sin(40) = 3.86 m down.
            var hull = HullHeading(Vector2.down, Squash);
            var box = PlanBox(Vector2.down);
            Vector2 pastDrawnStem = new Vector2(0f, -5f);
            Assert.IsTrue(box.TouchesCell(pastDrawnStem, Cell * 0.5f), "the plan box reaches past her picture (the defect)");
            Assert.IsFalse(hull.TouchesCell(pastDrawnStem, Cell * 0.5f), "open water ahead of her drawn stem");
            Assert.IsTrue(hull.TouchesCell(new Vector2(0f, -3.5f), Cell * 0.5f), "her drawn forefoot is solid");
            Assert.IsTrue(hull.TouchesCell(new Vector2(0f, 3.5f), Cell * 0.5f), "her drawn transom is solid");
            Assert.IsFalse(hull.TouchesCell(new Vector2(0f, 4.2f), Cell * 0.5f), "nothing past her drawn transom");
        }

        [Test] public void HeadingAcrossTheScreen_HerBowIsAPointNotASquareEnd()
        {
            var hull = HullHeading(Vector2.right, Squash);
            var box = PlanBox(Vector2.right);
            Vector2 bowCorner = new Vector2(5.6f, 0.9f * HalfBeam * Squash);
            Vector2 sternCorner = new Vector2(-5.6f, 0.9f * HalfBeam * Squash);
            Assert.IsTrue(box.TouchesCell(bowCorner, Cell * 0.5f), "the plan box is square across her bow");
            Assert.IsFalse(hull.TouchesCell(bowCorner, Cell * 0.5f), "the bow's shoulder is open water");
            Assert.IsTrue(hull.TouchesCell(sternCorner, Cell * 0.5f), "her transom is square, as drawn");
            Assert.IsTrue(hull.TouchesCell(new Vector2(5.9f, 0f), Cell * 0.5f), "her stem itself is solid");
        }

        [Test] public void WaterlineWidth_IsHerBeamAft_AndNarrowsStraightToTheStem()
        {
            var hull = HullHeading(Vector2.up, 1f);
            float hl = Length * 0.5f, taperFrom = hl - Length * Settings.ContactBowTaper;
            Assert.AreEqual(HalfBeam, hull.HalfWidthAt(-hl), 1e-5f);
            Assert.AreEqual(HalfBeam, hull.HalfWidthAt(taperFrom), 1e-5f);
            Assert.AreEqual(HalfBeam * Settings.ContactStemHalfBeams, hull.HalfWidthAt(hl), 1e-5f);
            float mid = (taperFrom + hl) * 0.5f;
            Assert.AreEqual(HalfBeam * (1f + Settings.ContactStemHalfBeams) * 0.5f, hull.HalfWidthAt(mid), 1e-4f);
        }

        [Test] public void EverySolidCell_LiesInsideTheBoundsTheMaskWalks([Values(0f, 30f, 139f, 225f, 300f)] float cw)
        {
            float rad = cw * Mathf.Deg2Rad;
            var hull = HullHeading(new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)), Squash);
            Vector2 aabb = hull.AabbHalf;
            int solid = 0;
            for (float x = -8f; x <= 8f; x += Cell)
                for (float y = -8f; y <= 8f; y += Cell)
                {
                    var p = new Vector2(x, y);
                    if (!hull.TouchesCell(p, Cell * 0.5f)) continue;
                    solid++;
                    Assert.LessOrEqual(Mathf.Abs(x), aabb.x + 1e-4f, $"x {x} at {cw}");
                    Assert.LessOrEqual(Mathf.Abs(y), aabb.y + 1e-4f, $"y {y} at {cw}");
                }
            Assert.Greater(solid, 0);
        }

        [Test] public void Capture_TakesTheHullsOwnForeshortening()
        {
            var drawn = new DrawnHull
            {
                Footprint = HullFootprint.FromBowDirection(new Vector2(40f, 20f), Vector2.down, Length, HalfBeam),
                PictureForeshortenY = Squash,
            };
            HullPresences.Register(drawn);
            var into = new FoamTransportContacts.Shape[FoamTransport.MaxContacts];
            Assert.IsTrue(FoamTransportContacts.Capture(into, new Rect(20f, 0f, 40f, 40f), 0f, out int count, out var fault),
                          fault.ToString());
            Assert.AreEqual(1, count);
            Assert.IsTrue(into[0].IsHull);
            Assert.AreEqual(Squash, into[0].ForeshortenY, 1e-6f);
            Assert.IsFalse(into[0].TouchesCell(new Vector2(40f, 15f), Cell * 0.5f), "5 m ahead is past her drawn stem");
            Assert.IsTrue(into[0].TouchesCell(new Vector2(40f, 17f), Cell * 0.5f));
        }

        [Test] public void ABadForeshortening_IsReadAsPlanView_NeverAPoisonedField()
        {
            foreach (float bad in new[] { 0f, -1f, 2f, float.NaN, float.PositiveInfinity })
            {
                var s = HullHeading(Vector2.up, bad);
                Assert.IsTrue(s.Valid, bad.ToString());
                Assert.AreEqual(1f, s.ForeshortenY, bad.ToString());
            }
        }

        [Test] public void APlainBox_IsUnchanged()
        {
            var box = PlanBox(Vector2.down);
            Assert.IsFalse(box.IsHull);
            Assert.AreEqual(new Vector2(HalfBeam, Length * 0.5f), box.AabbHalf);
            Assert.IsTrue(box.TouchesCell(new Vector2(HalfBeam + Cell * 0.4f, 0f), Cell * 0.5f), "a box still counts a grazed cell");
        }
    }
}
