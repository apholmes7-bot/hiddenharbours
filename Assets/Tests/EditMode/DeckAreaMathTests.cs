using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The pure geometry M2-37's data half stands on: the deck-frame ↔ screen projection (rotation +
    /// this artwork's iso foreshortening + the lift from deck height), point-in-polygon, nearest point
    /// on an outline, and the fitted height plane. No scene, no assets — closed-form maths, pinned.
    ///
    /// <para>The projection tests are the load-bearing ones. The old deck rectangle rotated with the
    /// drawn heading but never foreshortened, so a hull pointing NORTH let the player walk her full
    /// along-hull length up-screen while the ¾ camera drew only sin(40°) of it — the same defect the
    /// wake plume had. These pin that the squash is applied, that it is applied on the right axis at
    /// every heading, and that a plan-view artwork (elevation 90) still gets exactly the old
    /// transform.</para>
    /// </summary>
    public class DeckAreaMathTests
    {
        const float IsoElevation = 40f;                       // every iso rig bakes here
        const float Tol = 1e-4f;

        static readonly Vector2[] UnitSquare =
        {
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f),
        };

        // ---- projection ---------------------------------------------------------------------------

        [Test]
        public void PlanView_IsExactlyTheOldRectangleTransform()
        {
            // 90° = no camera = no foreshortening. An unmeasured hull must not move a millimetre.
            foreach (float heading in new[] { 0f, 37f, 90f, 180f, 271f })
            {
                Vector2 deck = new Vector2(0.6f, -1.3f);
                Vector2 viaMath = DeckAreaMath.DeckToWorld(deck, 0f, heading,
                                                           DeckAreaMath.PlanViewElevationDegrees);
                Vector2 viaWalk = DeckWalkController.DeckFrameToWorld(deck, heading);
                Assert.AreEqual(viaWalk.x, viaMath.x, Tol, $"x at {heading}°");
                Assert.AreEqual(viaWalk.y, viaMath.y, Tol, $"y at {heading}°");
            }
        }

        [Test]
        public void HeadingNorth_SquashesAlongTheKeelAndLeavesTheBeamAlone()
        {
            // Bow up-screen: along-hull distance is the foreshortened axis, abeam is not.
            Vector2 aft = DeckAreaMath.DeckToWorld(new Vector2(0f, -6f), 0f, 0f, IsoElevation);
            Assert.AreEqual(-6f * Mathf.Sin(IsoElevation * Mathf.Deg2Rad), aft.y, Tol,
                            "6 m aft must draw as 6·sin(40°) m of screen");
            Assert.AreEqual(0f, aft.x, Tol);

            Vector2 abeam = DeckAreaMath.DeckToWorld(new Vector2(1.7f, 0f), 0f, 0f, IsoElevation);
            Assert.AreEqual(1.7f, abeam.x, Tol, "the beam is across the view — never squashed");
            Assert.AreEqual(0f, abeam.y, Tol);
        }

        [Test]
        public void HeadingEast_SquashesTheBeamInstead()
        {
            // Bow to the right: now it is the BEAM that runs into the screen. This is why a single
            // pre-squashed rectangle could never be right — which axis foreshortens turns with her.
            Vector2 aft = DeckAreaMath.DeckToWorld(new Vector2(0f, -6f), 0f, 90f, IsoElevation);
            Assert.AreEqual(-6f, aft.x, Tol, "aft is now straight across the screen — full length");
            Assert.AreEqual(0f, aft.y, Tol);

            // Heading East, starboard is toward the BOTTOM of the screen — and now squashed.
            Vector2 abeam = DeckAreaMath.DeckToWorld(new Vector2(1.7f, 0f), 0f, 90f, IsoElevation);
            Assert.AreEqual(-1.7f * Mathf.Sin(IsoElevation * Mathf.Deg2Rad), abeam.y, Tol);
            Assert.AreEqual(0f, abeam.x, Tol);
        }

        [Test]
        public void DeckHeight_LiftsUpScreenByCosElevation()
        {
            Vector2 sole = DeckAreaMath.DeckToWorld(Vector2.zero, 0.5f, 0f, IsoElevation);
            Vector2 foredeck = DeckAreaMath.DeckToWorld(Vector2.zero, 2.5f, 0f, IsoElevation);
            float expected = 2f * Mathf.Cos(IsoElevation * Mathf.Deg2Rad);
            Assert.AreEqual(expected, foredeck.y - sole.y, Tol,
                            "climbing 2 m must draw 2·cos(40°) m higher on screen");
            Assert.AreEqual(0f, DeckAreaMath.DeckToWorld(Vector2.zero, 2.5f, 0f,
                                                         DeckAreaMath.PlanViewElevationDegrees).y, Tol,
                            "a plan view sees no height at all");
        }

        [Test]
        public void WorldToDeck_IsTheExactInverseAtEveryHeading()
        {
            foreach (float heading in new[] { 0f, 45f, 90f, 135f, 213f, 359f })
            {
                Vector2 deck = new Vector2(-1.1f, 3.4f);
                const float height = 1.7f;
                Vector2 world = DeckAreaMath.DeckToWorld(deck, height, heading, IsoElevation);
                Vector2 back = DeckAreaMath.WorldToDeck(world, height, heading, IsoElevation);
                Assert.AreEqual(deck.x, back.x, 1e-3f, $"x at {heading}°");
                Assert.AreEqual(deck.y, back.y, 1e-3f, $"y at {heading}°");
            }
        }

        [Test]
        public void InputDirection_StillDrawsAlongTheKeyPressed()
        {
            // The walk converts a screen-axis press into the deck direction that DRAWS along it. Project
            // that direction back and it must be parallel to the press — "up moves up-screen" survives
            // the foreshortening at every heading, which is the whole reason the input is transformed
            // rather than the polygon.
            foreach (float heading in new[] { 0f, 45f, 90f, 200f })
            foreach (Vector2 press in new[] { Vector2.up, Vector2.right, new Vector2(1f, 1f).normalized })
            {
                Vector2 deckDir = DeckAreaMath.WorldDirectionToDeck(press, heading, IsoElevation).normalized;
                Vector2 drawn = DeckAreaMath.DeckToWorld(deckDir, 0f, heading, IsoElevation).normalized;
                Assert.AreEqual(0f, Vector2.Distance(drawn, press.normalized), 1e-3f,
                                $"press {press} at {heading}° drew along {drawn}");
            }
        }

        // ---- polygons ------------------------------------------------------------------------------

        [Test]
        public void Contains_ReadsInsideAndOutside()
        {
            Vector4 bounds = DeckAreaMath.BoundsOf(UnitSquare);
            Assert.IsTrue(DeckAreaMath.Contains(UnitSquare, bounds, Vector2.zero));
            Assert.IsTrue(DeckAreaMath.Contains(UnitSquare, bounds, new Vector2(0.9f, -0.9f)));
            Assert.IsFalse(DeckAreaMath.Contains(UnitSquare, bounds, new Vector2(1.1f, 0f)));
            Assert.IsFalse(DeckAreaMath.Contains(UnitSquare, bounds, new Vector2(0f, -4f)));
        }

        [Test]
        public void Contains_IsWindingAgnostic()
        {
            // The sidecars declare CCW-from-above, but the washboard strips are built by offsetting a
            // polyline and can close either way. The even-odd test must not care.
            var cw = new[] { UnitSquare[3], UnitSquare[2], UnitSquare[1], UnitSquare[0] };
            Assert.IsTrue(DeckAreaMath.Contains(cw, DeckAreaMath.BoundsOf(cw), new Vector2(0.2f, 0.2f)));
        }

        [Test]
        public void ClosestPointOnOutline_LandsOnTheNearestEdge()
        {
            Vector2 p = DeckAreaMath.ClosestPointOnOutline(UnitSquare, new Vector2(5f, 0.25f), out float sqr);
            Assert.AreEqual(1f, p.x, Tol);
            Assert.AreEqual(0.25f, p.y, Tol);
            Assert.AreEqual(16f, sqr, 1e-3f);

            Vector2 corner = DeckAreaMath.ClosestPointOnOutline(UnitSquare, new Vector2(4f, 4f), out _);
            Assert.AreEqual(1f, corner.x, Tol);
            Assert.AreEqual(1f, corner.y, Tol);
        }

        [Test]
        public void BoundsOf_IsTheAxisAlignedBox()
        {
            Vector4 b = DeckAreaMath.BoundsOf(new[]
                { new Vector2(-1.5f, 2f), new Vector2(3f, -4f), new Vector2(0f, 0f) });
            Assert.AreEqual(-1.5f, b.x, Tol);
            Assert.AreEqual(-4f, b.y, Tol);
            Assert.AreEqual(3f, b.z, Tol);
            Assert.AreEqual(2f, b.w, Tol);
        }

        // ---- height plane --------------------------------------------------------------------------

        [Test]
        public void FitHeightPlane_IsExactOnAFlatArea()
        {
            var flat = new[]
            {
                new Vector3(-1f, -1f, 0.5f), new Vector3(1f, -1f, 0.5f),
                new Vector3(1f, 1f, 0.5f), new Vector3(-1f, 1f, 0.5f),
            };
            Vector3 plane = DeckAreaMath.FitHeightPlane(flat);
            Assert.AreEqual(0f, plane.x, Tol);
            Assert.AreEqual(0f, plane.y, Tol);
            Assert.AreEqual(0.5f, plane.z, Tol);
            Assert.AreEqual(0f, DeckAreaMath.HeightPlaneResidualMax(flat, plane), Tol);
        }

        [Test]
        public void FitHeightPlane_RecoversASlopedDeckExactly()
        {
            // A sheer-following foredeck rises along the keel: z = 0.25·y + 1.2. The fit must find it,
            // because that is the case a single 'z' cannot express and the whole reason polygon3d exists.
            var sloped = new[]
            {
                new Vector3(-1f, 0f, 1.2f), new Vector3(1f, 0f, 1.2f),
                new Vector3(-1f, 2f, 1.7f), new Vector3(1f, 2f, 1.7f),
            };
            Vector3 plane = DeckAreaMath.FitHeightPlane(sloped);
            Assert.AreEqual(0f, plane.x, 1e-3f);
            Assert.AreEqual(0.25f, plane.y, 1e-3f);
            Assert.AreEqual(1.2f, plane.z, 1e-3f);
            Assert.AreEqual(0f, DeckAreaMath.HeightPlaneResidualMax(sloped, plane), 1e-3f);
            Assert.AreEqual(1.45f, DeckAreaMath.HeightAt(plane, new Vector2(0f, 1f)), 1e-3f);
        }

        [Test]
        public void FitHeightPlane_DegradesToTheMeanWhenThereIsNoPlanAreaToTiltOver()
        {
            var collinear = new[]
            {
                new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 3f), new Vector3(0f, 2f, 2f),
            };
            Vector3 plane = DeckAreaMath.FitHeightPlane(collinear);
            Assert.AreEqual(0f, plane.x, Tol);
            Assert.AreEqual(0f, plane.y, Tol);
            Assert.AreEqual(2f, plane.z, Tol, "the honest answer is the mean height");
        }

        [Test]
        public void NanInputsNeverProduceNan()
        {
            Vector2 p = DeckAreaMath.DeckToWorld(new Vector2(float.NaN, 1f), float.NaN, 30f, IsoElevation);
            Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y));
            Assert.IsFalse(float.IsNaN(DeckAreaMath.LiftZ(float.NaN)));
        }
    }
}
