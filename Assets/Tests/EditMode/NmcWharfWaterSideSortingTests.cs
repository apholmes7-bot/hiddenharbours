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
        // The parts of today's bake with one straight seaward lip; the ramps and the turned east arm have none.
        static readonly string[] CutParts = { "apron", "crib", "conc", "east", "w0", "w1", "w2" };

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
                         "WaterSide_fleet_conc_1", "WaterSide_east_east_0", "WaterSide_arm_w_w0_0", "WaterSide_arm_w_w2_0", "NorthWall_logCrib_0" })
                Assert.IsTrue(rules.ExcludedNames.Any(p => SunCasterRules.Matches(sprite, p)),
                    sprite + " is cut at the waterline; its shadow is the uncut picture and would box the water");
            foreach (string sprite in new[] { "WaterSide_west_float_0", "WaterSide_fleet_fingerA_body_0", "WaterSide_fleet_gangway0_s1_0" })
                Assert.IsFalse(rules.ExcludedNames.Any(p => SunCasterRules.Matches(sprite, p)), sprite + " is not cut and keeps its shadow");
        }

        [Test]
        public void TheGangwayRungIsOverTheFixedWaterSideRung_AndUnderTheDecorBand()
        {
            Assert.That(NineMileCreekWharf.GangwaySortingOrder, Is.GreaterThan(NineMileCreekWharf.FixedWaterSideSortingOrder),
                "a gangway lies ON the quay it bridges: on one rung with the quay it ties, and the tie is broken by draw order, not by design");
            Assert.That(NineMileCreekWharf.GangwaySortingOrder, Is.LessThan(SortingBands.DecorFloor), "whoever walks the gangway draws over it");
        }

        [Test]
        public void EveryGangwayPictureInTheScene_DrawsOverEveryQuayBodyItOverlaps()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
            Doc root = scene.RootNamed(NineMileCreekWharfWaterSide.RootName);
            var gangways = new List<(string what, Rect rect, int layer, int order)>();
            var bodies = new List<(string what, Rect rect, int layer, int order)>();
            foreach (long harbourTr in scene.TransformOf(root).FieldRefs("m_Children"))
            {
                Doc harbour = scene.Require(scene.Require(harbourTr, "a harbour").FieldRef("m_GameObject"), "a harbour object");
                string harbourName = harbour.Field("m_Name");
                if (!harbourName.StartsWith("harbour.", StringComparison.Ordinal)) continue;
                var parts = scene.TransformOf(harbour).FieldRefs("m_Children")
                    .Select(tr => scene.Require(scene.Require(tr, "a part").FieldRef("m_GameObject"), "a part object")).ToList();
                var floats = parts.Where(p => scene.ComponentsOf(p).Any(d => d.IsScript(FloatingVisual))).Select(p => p.Field("m_Name")).ToList();
                foreach (Doc part in parts)
                {
                    var components = scene.ComponentsOf(part);
                    Doc renderer = components.SingleOrDefault(d => d.ClassId == 212);
                    if (renderer == null) continue;
                    string name = part.Field("m_Name");
                    Doc gangway = components.SingleOrDefault(d => d.IsScript(GangwayVisual));
                    bool rides = gangway != null || components.Any(d => d.IsScript(FloatingVisual)) ||
                                 floats.Any(f => name.StartsWith(f + "_", StringComparison.Ordinal));
                    if (rides && gangway == null) continue;
                    // A gangway draws every rung of its slope through the one renderer: it covers the union of them.
                    var sprites = gangway == null
                        ? new[] { ObjRef.Parse(renderer.Field("m_Sprite")) }
                        : RungRefs(gangway.Text).Append(ObjRef.Parse(renderer.Field("m_Sprite"))).ToArray();
                    Rect rect = WorldRect(scene, part, sprites);
                    var row = (harbourName + "/" + name, rect, SortingLayer.GetLayerValueFromID(ParseInt(renderer.Field("m_SortingLayerID"))),
                        ParseInt(renderer.Field("m_SortingOrder")));
                    (gangway != null ? gangways : bodies).Add(row);
                }
            }
            Assert.That(gangways.Count, Is.EqualTo(4), "the four native gangways: west, fleet 0, fleet 1, east");
            Assert.That(bodies.Count, Is.GreaterThan(CutParts.Length), "the quay bodies are read");
            foreach (var g in gangways)
            {
                var under = bodies.Where(b => b.rect.Overlaps(g.rect)).ToList();
                Assert.That(under, Is.Not.Empty, g.what + " bridges a quay: its picture overlaps one");
                foreach (var b in under)
                    Assert.That(g.layer > b.layer || (g.layer == b.layer && g.order > b.order), Is.True,
                        $"{g.what} ({g.layer}:{g.order}) lies on {b.what} ({b.layer}:{b.order}) and must draw strictly over it");
            }
            Assert.That(gangways.Single(g => g.what == "harbour.fleet/gangway0_s0").rect
                    .Overlaps(bodies.Single(b => b.what == "harbour.fleet/crib").rect), Is.True,
                "the fleet's west gangway crosses the crib wall, the pair that tied");
        }

        static IEnumerable<ObjRef> RungRefs(string gangwayVisualText)
        {
            bool inRungs = false;
            foreach (string line in gangwayVisualText.Split('\n'))
            {
                string l = line.TrimEnd('\r');
                if (l.StartsWith("  _rungs:", StringComparison.Ordinal)) { inRungs = true; continue; }
                if (!inRungs) continue;
                if (!l.StartsWith("  - ", StringComparison.Ordinal)) yield break;
                yield return ObjRef.Parse(l.Substring(4).Trim());
            }
        }

        /// <summary>The world rect a part's pictures cover, from its transform chain (no rotation) and its sprites' bounds.</summary>
        static Rect WorldRect(SceneYaml scene, Doc go, IEnumerable<ObjRef> sprites)
        {
            Vector3 position = Vector3.zero, scale = Vector3.one;
            var chain = new List<Doc>();
            for (Doc tr = scene.TransformOf(go); tr != null;)
            {
                chain.Add(tr);
                long father = tr.FieldRef("m_Father");
                tr = father == 0 ? null : scene.Require(father, "a parent transform");
            }
            foreach (Doc tr in Enumerable.Reverse(chain))
            {
                StringAssert.Contains("{x: 0, y: 0, z: 0, w: 1}", tr.Field("m_LocalRotation"), "the water-side parts are not rotated");
                Vector3 local = ParseVec3(tr.Field("m_LocalPosition")), s = ParseVec3(tr.Field("m_LocalScale"));
                position += Vector3.Scale(scale, local);
                scale = Vector3.Scale(scale, s);
            }
            Rect? rect = null;
            foreach (ObjRef r in sprites)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(r.Guid);
                Sprite sprite = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                    .Single(s => UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out _, out long local) && local == r.FileId);
                Bounds b = sprite.bounds;
                Vector2 min = (Vector2)position + Vector2.Scale(scale, b.min), max = (Vector2)position + Vector2.Scale(scale, b.max);
                var here = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
                rect = rect == null ? here : Rect.MinMaxRect(Mathf.Min(rect.Value.xMin, here.xMin), Mathf.Min(rect.Value.yMin, here.yMin),
                    Mathf.Max(rect.Value.xMax, here.xMax), Mathf.Max(rect.Value.yMax, here.yMax));
            }
            Assert.IsNotNull(rect, "a picture has a sprite");
            return rect.Value;
        }

        static Dictionary<string, (float lip, float elevation, string module)> Lips()
        {
            var lips = new Dictionary<string, (float, float, string)>();
            foreach (object part in DeckSidecarJson.AsArray(DeckSidecarJson.Member(NineMileCreekWharfWaterSide.ReadToday(), "parts")))
            {
                object walk = DeckSidecarJson.Member(part, "walkPolygon");
                if (walk == null) continue;
                float lip = DeckSidecarJson.AsArray(walk).Select(q => DeckSidecarJson.Float(DeckSidecarJson.AsArray(q)[1])).Min();
                lips[DeckSidecarJson.String(DeckSidecarJson.Member(part, "key"))] = (lip, DeckSidecarJson.Float(DeckSidecarJson.Member(part, "elevation")),
                    DeckSidecarJson.String(DeckSidecarJson.Member(DeckSidecarJson.Member(part, "module"), "key")));
            }
            return lips;
        }

        static float ParseFloat(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }
}
#endif
