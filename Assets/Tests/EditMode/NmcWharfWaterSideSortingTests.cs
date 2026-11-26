#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The wall berths at Nine Mile Creek must have sea under them. The fixed water-side pictures
    /// (the cribbed wall, the concrete wharf, the apron, the ramps, the breakwater arms) are drawn
    /// with faces about 9 m deep below the lip. They sort on the float's rung (over the sea, under
    /// every hull), and each one with a single straight seaward lip is cut at the waterline by a
    /// <see cref="TidalFaceWaterline"/>, so its face shows above the water and the sea shows below.
    /// A cut face must not throw a sun shadow: the shadow is the UNCUT silhouette, and it boxes the
    /// water the face was cut from.
    /// </summary>
    public class NmcWharfWaterSideSortingTests
    {
        const string FloatingVisual = "HiddenHarbours.World.FloatingPlatformVisual";
        const string GangwayVisual = "HiddenHarbours.World.GangwayVisual";
        const string Waterline = "HiddenHarbours.Art.TidalFaceWaterline";
        // The parts of today's bake with one straight seaward lip, and the slipway's two ramps, whose water's edge climbs
        // them with the tide; the turned east arm has neither.
        static readonly string[] CutParts = { "apron", "crib", "conc", "east", "w0", "w1", "w2", "rampUpper", "rampLower" };

        [Test]
        public void TheFixedWaterSidePicturesSortOverTheSea_AndUnderEveryHull()
        {
            Assert.That(NineMileCreekWharf.FixedWaterSideSortingOrder, Is.GreaterThan(SortingBands.Sea),
                "under the translucent sea the drowned face shows through it as a boxed tone");
            Assert.AreEqual(NineMileCreekWharf.FloatSortingOrder, NineMileCreekWharf.FixedWaterSideSortingOrder,
                "the float's rung is the top of the deck band still under a hull (a moored boat composites at 1)");
        }

        [Test]
        public void ALipIsOneStraightEastWestEdgeOfAFlatDeck()
        {
            var rect = new[] { new Vector2(78f, 86f), new Vector2(111.6f, 86f), new Vector2(111.6f, 96f), new Vector2(78f, 96f) };
            Assert.IsTrue(NineMileCreekWharf.TryWaterlineLip(rect, true, null, out float lip));
            Assert.AreEqual(86f, lip, 1e-5f);
            Assert.IsFalse(NineMileCreekWharf.TryWaterlineLip(rect, false, null, out _), "a ramp has no single lip");
            var diagonal = new[] { new Vector2(183f, 10f), new Vector2(210f, 3f), new Vector2(214f, 17f), new Vector2(187f, 24f) };
            Assert.IsFalse(NineMileCreekWharf.TryWaterlineLip(diagonal, true, null, out _), "a diagonal arm has no single east-west lip");
            var joint = new[] { Rect.MinMaxRect(208f, 20f, 216f, 53.016f) };
            var upper = new[] { new Vector2(208f, 52.984f), new Vector2(216f, 52.984f), new Vector2(216f, 86f), new Vector2(208f, 86f) };
            Assert.IsFalse(NineMileCreekWharf.TryWaterlineLip(upper, true, joint, out _), "a south edge with the next piece in front of it is a joint, not a face");
        }

        [Test]
        public void EveryFixedPictureInTheScene_IsOnItsRung_AndTheStraightLippedOnesAreCutAtTheWaterline()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
            string tideMaterial = File.ReadAllLines(NineMileCreekWharfLandSide.TidalFaceMaterialPath + ".meta")
                .Single(l => l.StartsWith("guid: ", StringComparison.Ordinal)).Substring("guid: ".Length).Trim();
            var lips = Lips();
            Doc root = scene.RootNamed(NineMileCreekWharfWaterSide.RootName);
            var cut = new List<string>();
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
                    string what = harbourName + "/" + name;
                    Doc tide = components.SingleOrDefault(d => d.IsScript(Waterline));
                    if (rides)
                    {
                        Assert.That(order, Is.GreaterThan(SortingBands.Sea), what + " rides the tide and draws over the sea");
                        Assert.IsNull(tide, what + " rides the tide: it is never cut");
                        continue;
                    }
                    fixedCount++;
                    Assert.AreEqual(NineMileCreekWharf.FixedWaterSideSortingOrder, order, what + " is fixed");
                    if (tide == null) continue;
                    cut.Add(name);
                    Assert.IsTrue(lips.TryGetValue(harbourName.Substring("harbour.".Length) + "_" + name, out var expected), what + " is a sidecar part");
                    if (expected.module == NineMileCreekWharf.SlipwayModule)
                    {
                        // A ramp is cut from its top edge, and its cut row climbs the ramp at its run per metre of fall.
                        Assert.AreEqual(expected.top, ParseFloat(tide.Field("_lipWorldY")), 1e-3f, what + " is cut from its ramp's top edge");
                        Assert.AreEqual(expected.elevation, ParseFloat(tide.Field("_lipElevation")), 1e-3f, what + " is cut at its top's elevation");
                        Assert.That(ParseFloat(tide.Field("_slopeRunPerMetre")), Is.GreaterThan(0f), what + " is told its slope");
                        StringAssert.Contains(tideMaterial, renderer.Text, what + " draws through the tidal-face material");
                        continue;
                    }
                    Assert.AreEqual(expected.lip, ParseFloat(tide.Field("_lipWorldY")), 1e-3f, what + " is cut from its walk's seaward lip");
                    Assert.AreEqual(expected.elevation, ParseFloat(tide.Field("_lipElevation")), 1e-3f, what + " is cut at its deck's elevation");
                    Assert.AreEqual("1", tide.Field("_configured"), what + " is told its lip");
                    Assert.AreEqual(((int)NineMileCreekWharf.DressingFor(expected.module)).ToString(), tide.Field("_dressing"),
                        what + " is dressed as its wharf type (" + expected.module + ")");
                    Assert.AreNotEqual("0", tide.Field("_dressing"), what + " is dressed: a cut face is never left a dark slab");
                    StringAssert.Contains(tideMaterial, renderer.Text, what + " draws through the tidal-face material");
                }
            }
            CollectionAssert.IsSubsetOf(new[] { "harbour.west", "harbour.fleet", "harbour.east", "harbour.arm_w", "harbour.arm_e" }, harbours);
            Assert.That(fixedCount, Is.GreaterThan(CutParts.Length), "the scene places fixed water-side pictures");
            CollectionAssert.AreEquivalent(CutParts, cut, "the parts with one straight seaward lip are the ones cut");
        }

        [Test]
        public void ACutFaceThrowsNoSunShadow_NorDoTheQuayCoursesUnderIt()
        {
            var rules = Resources.Load<SunCasterRules>("SunCasterRules");
            Assert.IsNotNull(rules, "Resources/SunCasterRules");
            foreach (string sprite in new[] { "WaterSide_west_apron_0", "WaterSide_fleet_crib_0", "WaterSide_fleet_conc_0",
                         "WaterSide_fleet_conc_1", "WaterSide_east_east_0", "WaterSide_arm_w_w0_0", "WaterSide_arm_w_w2_0", "NorthWall_logCrib_0",
                         "WaterSide_west_rampUpper_0", "WaterSide_west_rampLower_0" })
                Assert.IsTrue(rules.ExcludedNames.Any(p => SunCasterRules.Matches(sprite, p)),
                    sprite + " is cut at the waterline; its shadow is the uncut picture and would box the water");
            foreach (string sprite in new[] { "WaterSide_west_float_0", "WaterSide_fleet_fingerA_body_0", "WaterSide_fleet_gangway0_s1_0" })
                Assert.IsFalse(rules.ExcludedNames.Any(p => SunCasterRules.Matches(sprite, p)), sprite + " is not cut and keeps its shadow");
        }

        static Dictionary<string, (float lip, float top, float elevation, string module)> Lips()
        {
            var lips = new Dictionary<string, (float, float, float, string)>();
            foreach (object part in DeckSidecarJson.AsArray(DeckSidecarJson.Member(NineMileCreekWharfWaterSide.ReadToday(), "parts")))
            {
                object walk = DeckSidecarJson.Member(part, "walkPolygon");
                if (walk == null) continue;
                float lip = DeckSidecarJson.AsArray(walk).Select(q => DeckSidecarJson.Float(DeckSidecarJson.AsArray(q)[1])).Min();
                float top = DeckSidecarJson.AsArray(walk).Select(q => DeckSidecarJson.Float(DeckSidecarJson.AsArray(q)[1])).Max();
                lips[DeckSidecarJson.String(DeckSidecarJson.Member(part, "key"))] = (lip, top, DeckSidecarJson.Float(DeckSidecarJson.Member(part, "elevation")),
                    DeckSidecarJson.String(DeckSidecarJson.Member(DeckSidecarJson.Member(part, "module"), "key")));
            }
            return lips;
        }

        static float ParseFloat(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }
}
#endif
