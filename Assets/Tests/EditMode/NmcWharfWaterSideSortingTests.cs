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
