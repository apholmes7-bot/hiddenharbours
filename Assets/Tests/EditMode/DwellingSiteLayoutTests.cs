using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0060 PR 5: where a placed mesh dwelling's walls and fixtures go (<see cref="DwellingSiteLayout"/>),
    /// and what its site does at enable (<see cref="DwellingSite"/>): turn the footprint, cut the wall line at
    /// the door, stand the fixtures on their anchors, and skin and sort the mesh once.
    /// </summary>
    public class DwellingSiteLayoutTests
    {
        const string Clipper = "Assets/_Project/Data/Dwellings/Meshes/CamperClipperDwellingMesh.asset";
        const float Eps = 1e-4f;

        // A 4 x 2 m floor, counter-clockwise, door in the middle of the +x wall.
        static readonly Vector2[] Box =
        {
            new Vector2(-1f, -2f), new Vector2(1f, -2f), new Vector2(1f, 2f), new Vector2(-1f, 2f),
        };

        static float Length(Vector2[] run)
        {
            float l = 0f;
            for (int i = 1; i < run.Length; i++) l += Vector2.Distance(run[i - 1], run[i]);
            return l;
        }

        // ----------------------------------------------------------------------- the wall line

        [Test]
        public void WallRun_LeavesExactlyTheGap_CentredOnTheDoor()
        {
            Vector2[] run = DwellingSiteLayout.WallRun(Box, new Vector2(1f, 0f), 0.8f);

            Assert.That(Vector2.Distance(run[0], new Vector2(1f, 0.4f)), Is.LessThan(Eps), "one jamb");
            Assert.That(Vector2.Distance(run[run.Length - 1], new Vector2(1f, -0.4f)), Is.LessThan(Eps), "the other jamb");
            Assert.That(Length(run), Is.EqualTo(12f - 0.8f).Within(Eps), "every metre of floor edge but the door is wall");
            foreach (Vector2 corner in Box)
                Assert.That(run.Count(p => Vector2.Distance(p, corner) < Eps), Is.EqualTo(1), $"corner {corner} once");
        }

        [Test]
        public void WallRun_MeasuresTheGapAlongTheOutline_SoAShortEdgeDoesNotNarrowTheDoor()
        {
            // The door point sits on a corner: half the gap runs up one edge and half along the next.
            Vector2[] run = DwellingSiteLayout.WallRun(Box, new Vector2(1f, 2f), 1f);
            Assert.That(Length(run), Is.EqualTo(11f).Within(Eps));
            Assert.That(Vector2.Distance(run[0], new Vector2(0.5f, 2f)), Is.LessThan(Eps));
            Assert.That(Vector2.Distance(run[run.Length - 1], new Vector2(1f, 1.5f)), Is.LessThan(Eps));
        }

        [Test]
        public void WallRun_IsEmpty_ForAnOpenOutline_OrAGapAsLongAsTheFloor()
        {
            Assert.That(DwellingSiteLayout.WallRun(null, Vector2.zero, 0.5f), Is.Empty);
            Assert.That(DwellingSiteLayout.WallRun(new[] { Vector2.zero, Vector2.one }, Vector2.zero, 0.5f), Is.Empty);
            Assert.That(DwellingSiteLayout.WallRun(Box, new Vector2(1f, 0f), 12f), Is.Empty);
        }

        [Test]
        public void WallRun_DoesNotCareWhichWayTheOutlineRuns()
        {
            Vector2[] cw = Box.Reverse().ToArray();
            Vector2[] run = DwellingSiteLayout.WallRun(cw, new Vector2(1f, 0f), 0.8f);
            Assert.That(Length(run), Is.EqualTo(11.2f).Within(Eps));
            Assert.That(run.Any(p => Mathf.Abs(p.x - 1f) < Eps && Mathf.Abs(p.y) < 0.4f - Eps), Is.False,
                "nothing of the wall line stands in the doorway");
        }

        // ----------------------------------------------------------------------- the compass

        [Test]
        public void CompassOf_TurnsTheModelFrame_ByTheSiteBearing()
        {
            // Nose (+y) east: the curb (+x) faces south and rig -x faces north (the camper lot's parking).
            Assert.That(DwellingSiteLayout.CompassOf(Vector2.up, 90), Is.EqualTo(90f).Within(Eps));
            Assert.That(DwellingSiteLayout.CompassOf(Vector2.right, 90), Is.EqualTo(180f).Within(Eps));
            Assert.That(DwellingSiteLayout.CompassOf(Vector2.left, 90), Is.EqualTo(0f).Within(Eps));
            Assert.That(DwellingSiteLayout.CompassOf(Vector2.up, 0), Is.EqualTo(0f).Within(Eps));
        }

        [TestCase(0f, DwellingSiteLayout.Side.North)]
        [TestCase(44f, DwellingSiteLayout.Side.North)]
        [TestCase(45f, DwellingSiteLayout.Side.East)]
        [TestCase(180f, DwellingSiteLayout.Side.South)]
        [TestCase(270f, DwellingSiteLayout.Side.West)]
        [TestCase(330f, DwellingSiteLayout.Side.North)]
        [TestCase(-10f, DwellingSiteLayout.Side.North)]
        public void SideOf_NamesTheNearestSide(float compass, DwellingSiteLayout.Side side) =>
            Assert.That(DwellingSiteLayout.SideOf(compass), Is.EqualTo(side));

        [Test]
        public void OutwardNormal_PointsOutOfTheFloor_EitherWinding()
        {
            Vector2 nearPlusX = new Vector2(0.9f, 0f);
            Assert.That(DwellingSiteLayout.OutwardNormalNearest(Box, nearPlusX), Is.EqualTo(Vector2.right));
            Assert.That(DwellingSiteLayout.OutwardNormalNearest(Box.Reverse().ToArray(), nearPlusX), Is.EqualTo(Vector2.right));
            Assert.That(DwellingSiteLayout.WallSide(Box, new Vector2(-0.9f, 0f), 90), Is.EqualTo(DwellingSiteLayout.Side.North));
        }

        [Test]
        public void NormalizeBearing_FoldsIntoOneTurn()
        {
            Assert.That(DwellingSiteLayout.NormalizeBearing(370), Is.EqualTo(10));
            Assert.That(DwellingSiteLayout.NormalizeBearing(-90), Is.EqualTo(270));
            Assert.That(DwellingSiteLayout.NormalizeBearing(360), Is.EqualTo(0));
        }

        // ----------------------------------------------------------------------- the site, on the Clipper

        IDwellingMeshPresentationService _savedService;
        GameObject _go;

        [SetUp] public void SetUp() => _savedService = DwellingMeshPresentation.Service;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                DwellingMeshPresentation.Service?.Remove(_go);
                Object.DestroyImmediate(_go);
            }
            DwellingMeshPresentation.Service = _savedService;
        }

        static DwellingMeshDef Def()
        {
            var def = AssetDatabase.LoadAssetAtPath<DwellingMeshDef>(Clipper);
            Assert.That(def, Is.Not.Null, Clipper);
            return def;
        }

        DwellingSite MakeSite(DwellingMeshDef def, Vector2 at, out EdgeCollider2D walls, out Transform freezer)
        {
            _go = new GameObject("site");
            _go.transform.position = at;
            var interior = _go.AddComponent<BuildingInterior>();
            interior.Configure(null, null, null, 2.4f, 6f, 0, 8, 0.6427876f, 0.05f, 0.82f);
            Assert.That(interior.ConfigureDwelling(def), Is.True);
            _go.AddComponent<DwellingCutaway>();
            var site = _go.AddComponent<DwellingSite>();
            var w = new GameObject("Walls");
            w.transform.SetParent(_go.transform, false);
            walls = w.AddComponent<EdgeCollider2D>();
            freezer = new GameObject("freezer").transform;
            freezer.SetParent(_go.transform, false);
            site.Configure(walls, new[] { new DwellingSite.AnchorPoint("freezer", freezer) });
            return site;
        }

        [Test]
        public void Layout_TurnsTheFootprint_StandsTheFixture_AndCutsTheWallAtTheDoor()
        {
            DwellingMeshDef def = Def();
            DwellingSite site = MakeSite(def, new Vector2(10f, 20f), out EdgeCollider2D walls, out Transform freezer);
            site.SetSite("ginny_lot_camper", 90);
            var interior = site.GetComponent<BuildingInterior>();

            Assert.That(interior.Facing, Is.EqualTo(90));
            Assert.That(interior.Facings, Is.EqualTo(DwellingSiteLayout.FacingsPerTurn));
            Assert.That(site.SiteKey, Is.EqualTo("ginny_lot_camper"));

            InteriorFootprint ground = interior.Footprint;
            Assert.That(def.TryGetAnchor("freezer", out DwellingFixtureAnchor a), Is.True);
            Vector2 expected = ground.ModelToWorld(new Vector2(a.PositionMeters.x, a.PositionMeters.y));
            Assert.That(Vector2.Distance(freezer.position, expected), Is.LessThan(Eps), "the freezer stands on its anchor");

            Assert.That(walls.enabled, Is.True);
            Assert.That(walls.pointCount, Is.GreaterThan(def.Levels[0].Floor.Length / 2), "the wall follows the outline");
            Assert.That(walls.edgeRadius, Is.EqualTo(interior.WallThicknessMetres).Within(Eps));

            // The doorway is clear: nothing of the wall line within the door's half-width of the door point.
            Vector2 door = interior.DoorWorld - (Vector2)walls.transform.position;
            float clearHalf = def.Threshold.ClearWidthMeters * 0.5f;
            Vector2[] pts = walls.points;
            for (int i = 1; i < pts.Length; i++)
                Assert.That(GroundPolygon.DistanceToSegment(door, pts[i - 1], pts[i]), Is.GreaterThan(clearHalf * 0.6427876f - Eps),
                            $"wall segment {i} crosses the doorway");
        }

        [Test]
        public void Layout_AtAnotherBearing_MovesTheFixture_AndKeepsItInsideTheFloor()
        {
            DwellingMeshDef def = Def();
            DwellingSite site = MakeSite(def, Vector2.zero, out _, out Transform freezer);
            site.SetSite("x", 0);
            Vector2 north = freezer.position;
            site.SetSite("x", 180);
            Vector2 south = freezer.position;
            Assert.That(Vector2.Distance(north, -south), Is.LessThan(Eps), "a half turn mirrors it through the pivot");
            Assert.That(site.GetComponent<BuildingInterior>().Footprint.Contains(south, 0f), Is.True,
                        "the fixture stands on the turned floor");
        }

        [Test]
        public void Skin_InstallsTheMesh_TurnsItToTheBearing_AndWritesTheOrderOnce()
        {
            IsoFacetDwellingPresentationService.EnsureRegistered();
            DwellingMeshDef def = Def();
            DwellingSite site = MakeSite(def, new Vector2(3f, 7.3f), out _, out _);
            site.SetSite("ginny_lot_camper", 90);

            IHullMeshRenderer renderer = site.Skin();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.HeadingDirUnits,
                        Is.EqualTo(HullMeshMath.HeadingToDirUnits(90f, def.ZeroHeadingDegrees, def.AzimuthCounterClockwise)).Within(Eps));
            Assert.That(site.SortingOrder, Is.EqualTo(DwellingSorting.OrderAt(new Vector2(3f, 7.3f))));
            var group = _go.GetComponent<SortingGroup>();
            Assert.That(group, Is.Not.Null, "the root sorts as one group");
            Assert.That(group.sortingOrder, Is.EqualTo(site.SortingOrder));
        }

        [Test]
        public void Skin_WithNoService_InstallsNothing()
        {
            DwellingMeshPresentation.Service = null;
            DwellingSite site = MakeSite(Def(), Vector2.zero, out _, out _);
            Assert.That(site.Skin(), Is.Null);
            Assert.That(_go.GetComponent<SortingGroup>(), Is.Null);
        }
    }
}
