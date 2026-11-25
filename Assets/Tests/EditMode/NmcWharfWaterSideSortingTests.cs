#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using NUnit.Framework;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The wall berths at Nine Mile Creek must have sea under them. The fixed water-side pictures
    /// (the cribbed wall, the concrete wharf, the apron, the ramps, the breakwater arms) are drawn
    /// with faces about 9 m deep below the lip; when they sorted at the deck band (over the sea
    /// plane) those faces covered the water south of the wall at every tide, and the boats moored
    /// there sat on a dark face with no sea beneath them. Sorted under the sea, the plane covers the
    /// part of each face that stands where the painted bed is below the water, and dry deck still
    /// shows. The floats and the brow are not fixed: they ride, and keep their deck rungs.
    /// </summary>
    public class NmcWharfWaterSideSortingTests
    {
        const string FloatingVisual = "HiddenHarbours.World.FloatingPlatformVisual";
        const string GangwayVisual = "HiddenHarbours.World.GangwayVisual";

        [Test]
        public void TheFixedWaterSidePicturesSortUnderTheSea()
        {
            Assert.That(NineMileCreekWharf.FixedWaterSideSortingOrder, Is.LessThan(SortingBands.Sea),
                "a fixed water-side picture over the sea plane hides the water the wall berths lie in");
            Assert.That(NineMileCreekWharf.FixedWaterSideSortingOrder, Is.GreaterThan(SortingBands.SubmergedShorePlant),
                "and it stays over the submerged shore planting and the painted bed");
        }

        [Test]
        public void TheMooringFaceRunsFromTheFleetWallsWestEnd_SoTheWallShowsAboveTheWaterTheWholeWay()
        {
            // Under the sea, the fleet crib's own face no longer shows; the tidal quay face is the wall.
            // H5 drew the basin west to the crib's west end, past the old apron corner the run began at.
            float west = NineMileCreekDressing.FleetWallFaceWest.x;
            float corner = NineMileCreekDressing.FleetWallFaceEast.x;
            Assert.That(west, Is.LessThan(corner), "the fleet wall no longer reaches west of the old apron corner");
            var pieces = NineMileCreekDressing.FleetWallFacePieces();
            Assert.That(pieces.Count, Is.GreaterThan(0), "no course covers the fleet wall west of the old corner");
            float pitch = pieces.Count > 1 ? pieces[1].Lip.x - pieces[0].Lip.x : corner - west;
            Assert.That(pieces.First().Lip.x - west, Is.EqualTo(pitch * 0.5f).Within(1e-3f), "the first course reaches the crib's west end");
            Assert.That(corner - pieces.Last().Lip.x, Is.EqualTo(pitch * 0.5f).Within(1e-3f), "the last course reaches the old corner");

            // In the scene: the same courses, on the north run's lip and rung, each cut at the waterline.
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
            Doc dressing = scene.RootNamed(NineMileCreekDressing.RootName);
            Doc face = scene.ChildNamed(scene.TransformOf(dressing), NineMileCreekDressing.FaceRootName);
            var byName = scene.TransformOf(face).FieldRefs("m_Children")
                .Select(tr => scene.Require(scene.Require(tr, "a course").FieldRef("m_GameObject"), "a course object"))
                .ToDictionary(go => go.Field("m_Name"));
            Doc firstNorth = byName[NineMileCreekDressing.NorthWallRun + "_" + pieces[0].Key + "_0"];
            float northLip = ParseFloat(scene.ComponentsOf(firstNorth).Single(d => d.IsScript("HiddenHarbours.Art.TidalFaceWaterline")).Field("_lipWorldY"));
            for (int i = 0; i < pieces.Count; i++)
            {
                string name = NineMileCreekLayerRefresh.FleetWallCourseName(pieces[i], i);
                Assert.That(byName.ContainsKey(name), $"{name} is not in the scene: run Apply the fleet wall face");
                var components = scene.ComponentsOf(byName[name]);
                Doc tide = components.Single(d => d.IsScript("HiddenHarbours.Art.TidalFaceWaterline"));
                Assert.That(ParseFloat(tide.Field("_lipWorldY")), Is.EqualTo(northLip).Within(1e-4f), name + " stands on the north courses' lip");
                Assert.AreEqual(NineMileCreekDressing.FaceSortingOrder(NineMileCreekDressing.NorthWallRun),
                    ParseInt(components.Single(d => d.ClassId == 212).Field("m_SortingOrder")), name + " draws on the north run's rung");
                Assert.AreEqual("1", byName[name].Field("m_IsActive"), name + " is live");
            }
        }

        [Test]
        public void EveryFixedPictureInTheSceneSortsUnderTheSea_AndTheRidingOnesKeepTheirRungs()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
            Doc root = scene.RootNamed(NineMileCreekWharfWaterSide.RootName);
            int fixedCount = 0;
            var harbours = new List<string>();
            foreach (long harbourTr in scene.TransformOf(root).FieldRefs("m_Children"))
            {
                Doc harbour = scene.Require(scene.Require(harbourTr, "a harbour").FieldRef("m_GameObject"), "a harbour object");
                string harbourName = harbour.Field("m_Name");
                if (!harbourName.StartsWith("harbour.", StringComparison.Ordinal)) continue;
                harbours.Add(harbourName);
                var parts = scene.TransformOf(harbour).FieldRefs("m_Children")
                    .Select(tr => scene.Require(scene.Require(tr, "a part").FieldRef("m_GameObject"), "a part object")).ToList();
                // A float's back and front motion layers are siblings named after her: they ride with her.
                var floats = parts.Where(p => scene.ComponentsOf(p).Any(d => d.IsScript(FloatingVisual)))
                    .Select(p => p.Field("m_Name")).ToList();
                foreach (Doc part in parts)
                {
                    var components = scene.ComponentsOf(part);
                    Doc renderer = components.SingleOrDefault(d => d.ClassId == 212);
                    if (renderer == null) continue;
                    string name = part.Field("m_Name");
                    bool rides = components.Any(d => d.IsScript(FloatingVisual) || d.IsScript(GangwayVisual)) ||
                                 floats.Any(f => name.StartsWith(f + "_", StringComparison.Ordinal));
                    int order = ParseInt(renderer.Field("m_SortingOrder"));
                    string what = harbourName + "/" + part.Field("m_Name");
                    if (rides)
                    {
                        Assert.That(order, Is.GreaterThan(SortingBands.Sea), what + " rides the tide and draws over the sea");
                        continue;
                    }
                    fixedCount++;
                    Assert.AreEqual(NineMileCreekWharf.FixedWaterSideSortingOrder, order,
                        what + " is fixed: its face reaches below the lip, so over the sea it hides the berths' water");
                }
            }
            CollectionAssert.IsSubsetOf(new[] { "harbour.west", "harbour.fleet", "harbour.east", "harbour.arm_w", "harbour.arm_e" }, harbours);
            Assert.That(fixedCount, Is.GreaterThan(0), "the scene places fixed water-side pictures");
        }
    }
}
#endif
