#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Nine Mile Creek's wharf at night: the lantern posts along the wall and the lit lamp poles are placed by the
    /// wharf-lamps step from the region's lighting record, as St Peters' wharf lanterns are; the harbour lights are a
    /// small marker glow on the lens; the slipway is cut at the waterline as it runs into the sea.
    /// </summary>
    public class NmcWharfLampsTests
    {
        const string SceneLightScript = "HiddenHarbours.Art.SceneLight";
        const string StPetersScenePath = "Assets/_Project/Scenes/StPeters.unity";
        /// <summary>The old harbour light: a 6 m disc of saturated green on the tower's foot.</summary>
        const float OldMarkerRadiusMetres = 6f;
        /// <summary>A sheet's frame (50 x 28 m at 32 px per metre on a 1600 x 900 plate).</summary>
        static readonly Vector2 ViewMetres = new Vector2(50f, 28f);
        /// <summary>A lantern stands this far back from the lip: on the deck, with its pool reaching over it.</summary>
        const float MinInsetMetres = 0.5f, MaxInsetMetres = 2f;
        /// <summary>And this far clear of a cleat, ladder, brow, ring or tide staff, in plan.</summary>
        const float ClearanceMetres = 1f;

        static SceneYaml _scene;
        static SceneYaml Scene => _scene ??= SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));

        static WharfLightingDef Lighting => NineMileCreekWharfLandSide.Lighting();

        [Test]
        public void TheLightingRecordHasItsStableId()
        {
            Assert.AreEqual("lighting.nmc_wharf", Lighting.Id);
            Assert.That(Lighting.LanternSites, Has.Length.EqualTo(9), "seven along the fleet and east walls, two on the west apron");
            Assert.That(Lighting.LitPoleIds, Has.Length.EqualTo(6), "the wharf's six lamp poles");
        }

        [Test]
        public void TheCommittedScene_IsTheStepsPlan_ASecondPlanIsEmpty()
        {
            List<LayerPatch> patches = NineMileCreekWharfLandSide.PlanLamps(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath), out _);
            foreach (LayerPatch p in patches) Assert.IsTrue(p.IsEmpty, "the scene differs from the step's plan:\n" + p.Summary());
        }

        [Test]
        public void EveryLanternStandsOnADeck_BackFromItsLip_ClearOfTheWharfsFittings()
        {
            var decks = Decks();
            var fittings = Scene.Docs.Where(d => d.ClassId == 114 &&
                    (d.IsScript("HiddenHarbours.World.ShoreCleat") || d.IsScript("HiddenHarbours.World.WharfLadder")))
                .Select(d => (name: Scene.Require(d.FieldRef("m_GameObject"), "a fitting").Field("m_Name"), at: World(Scene.Require(d.FieldRef("m_GameObject"), "a fitting"))))
                .Concat(Scene.Docs.Where(d => d.ClassId == 1 && !d.Stripped && Regex.IsMatch(d.Field("m_Name") ?? "",
                        @"^(gangway\d?_s0|prop\.nmc_ring_[we]|tideStaff_\d+|ringStation_\d+|structure\.nmc_wharf_crane)$"))
                    .Select(d => (name: d.Field("m_Name"), at: World(d))))
                .ToList();
            Assert.That(fittings.Count, Is.GreaterThan(20), "the census finds the wharf's fittings");
            foreach (Vector2 site in Lighting.LanternSites)
            {
                var deck = decks.Where(r => r.rect.Contains(site)).ToList();
                Assert.That(deck, Has.Count.EqualTo(1), site + " stands on one deck");
                float inset = site.y - deck[0].rect.yMin;
                Assert.That(inset, Is.InRange(MinInsetMetres, MaxInsetMetres), site + " stands just back from " + deck[0].key + "'s lip");
                foreach (var f in fittings)
                    Assert.That(Vector2.Distance(site, f.at), Is.GreaterThanOrEqualTo(ClearanceMetres), site + " is clear of " + f.name + " at " + f.at);
            }
        }

        [Test]
        public void EachLantern_IsStPetersWharfLantern_ComponentForComponent()
        {
            SceneYaml stp = SceneYaml.Parse(File.ReadAllText(StPetersScenePath));
            Doc stpLight = LightOf(stp, stp.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == "lanternPost_0"));
            Doc root = Scene.RootNamed(NineMileCreekWharfLandSide.LampsRootName);
            var lanterns = Children(Scene, root).Where(g => g.Field("m_Name").StartsWith("lanternPost_", StringComparison.Ordinal)).ToList();
            Assert.That(lanterns, Has.Count.EqualTo(Lighting.LanternSites.Length));
            for (int i = 0; i < lanterns.Count; i++)
            {
                Doc go = lanterns[i];
                Assert.AreEqual(Lighting.LanternSites[i], World(go), go.Field("m_Name") + " stands where the record says");
                var kinds = Scene.ComponentsOf(go).Select(c => c.ClassId == 114 ? c.Field("m_EditorClassIdentifier").Split('.').Last() : c.ClassId.ToString()).ToArray();
                CollectionAssert.AreEqual(new[] { "4", "212", "YSortSprite", "SpriteShadow", "SceneLight", "PreconfiguredLight" }, kinds);
                Doc light = LightOf(Scene, go);
                foreach (string field in new[] { "_shape", "_range", "_color", "_intensity", "_edgeSoftness", "_flickerAmount",
                             "_lampHeightMeters", "_bloomLiftMetres", "_reachMetres", "_gateThreshold" })
                    Assert.AreEqual(stpLight.Field(field), light.Field(field), go.Field("m_Name") + " " + field + " is St Peters' lantern's");
                Assert.AreEqual(Lighting.LanternsCastShadows ? "1" : "0", light.Field("_castsShadows"));
                Assert.That(Float(light.Field("_reachMetres")), Is.GreaterThan(0f), "a lantern throws a pool");
            }
        }

        [Test]
        public void EachLitPole_CarriesTheFuelStopsFloodlightAtItsHead()
        {
            Doc root = Scene.RootNamed(NineMileCreekWharfLandSide.LampsRootName);
            var heads = NineMileCreekWharfLandSide.LitPoleHeads(Lighting).ToList();
            var rule = NineMileCreekFuelStopPlan.LampRuleFor("lampPole");
            var look = LightPresets.For(rule.Kind);
            foreach (var (id, at) in heads)
            {
                Doc go = Children(Scene, root).Single(g => g.Field("m_Name") == "lamp_" + id);
                Assert.AreEqual(at, World(go), id + "'s light hangs at its drawn head");
                Doc light = LightOf(Scene, go);
                Assert.AreEqual(LightPresets.BloomForFitting(rule.FittingWidth), Float(light.Field("_range")), 1e-5f);
                Assert.AreEqual(look.Intensity, Float(light.Field("_intensity")), 1e-5f);
                Assert.AreEqual(LightPresets.ReachMetres(rule.Kind), Float(light.Field("_reachMetres")), 1e-5f);
                Assert.AreEqual(rule.Height, Float(light.Field("_lampHeightMeters")), 1e-5f);
            }
        }

        [Test]
        public void TheLampsStayInsideTheLightBudget()
        {
            var profile = Resources.Load<LampShadowProfile>("LampShadowProfile");
            Assert.IsNotNull(profile, "Resources/LampShadowProfile");
            var pools = Scene.Docs.Where(d => d.ClassId == 114 && d.IsScript(SceneLightScript) && Float(d.Field("_reachMetres") ?? "0") > 0f)
                .Select(d => World(Scene.Require(d.FieldRef("m_GameObject"), "a light"))).ToList();
            int added = Lighting.LanternSites.Length + Lighting.LitPoleIds.Length;
            Assert.That(pools.Count, Is.GreaterThanOrEqualTo(added));
            // Wherever a sheet frames the wharf, every lamp in view gets its pool: never more than the pool cap in one frame.
            foreach (Vector2 centre in Lighting.LanternSites)
            {
                int inView = pools.Count(p => Mathf.Abs(p.x - centre.x) <= ViewMetres.x * 0.5f && Mathf.Abs(p.y - centre.y) <= ViewMetres.y * 0.5f);
                Assert.That(inView, Is.LessThanOrEqualTo(profile.MaxPools), "a frame round " + centre + " holds " + inView + " pools");
            }
            var casters = Scene.Docs.Where(d => d.ClassId == 114 && d.IsScript(SceneLightScript) && d.Field("_castsShadows") == "1")
                .Select(d => World(Scene.Require(d.FieldRef("m_GameObject"), "a light"))).ToList();
            foreach (Vector2 centre in Lighting.LanternSites)
                Assert.That(casters.Count(p => Mathf.Abs(p.x - centre.x) <= ViewMetres.x && Mathf.Abs(p.y - centre.y) <= ViewMetres.y),
                    Is.LessThanOrEqualTo(profile.MaxShadows), "shadow-casting lamps near " + centre);
        }

        [Test]
        public void TheHarbourLights_AreASmallMarkerGlowOnTheLens_FromTheRecord()
        {
            Assert.That(Lighting.MarkerBloomMetres, Is.InRange(0.4f, 1.6f), "a marker reads as a point from a boat, not a flood");
            Assert.That(Lighting.MarkerBloomMetres, Is.LessThan(OldMarkerRadiusMetres / 4f));
            Assert.That(Lighting.MarkerIntensity, Is.InRange(1f, 2f));
            Assert.That(Lighting.MarkerEdgeSoftness, Is.InRange(0.4f, 0.8f));
            Assert.That(Lighting.WestGreen.g, Is.GreaterThan(Lighting.WestGreen.r).And.GreaterThan(Lighting.WestGreen.b), "the west head is green");
            Assert.That(Lighting.EastRed.r, Is.GreaterThan(Lighting.EastRed.g).And.GreaterThan(Lighting.EastRed.b), "the east head is red");
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>("Assets/_Project/Data/SetPieces/SetPiece_harbourLight.asset");
            Assert.IsTrue(def.TryGetAnchor("lamp", out Vector3 lens));
            foreach (var (id, colour) in new[] { ("structure.nmc_light_west", Lighting.WestGreen), ("structure.nmc_light_east", Lighting.EastRed) })
            {
                Doc go = Scene.Docs.Single(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == id);
                Doc light = LightOf(Scene, go);
                Assert.AreEqual(Lighting.MarkerBloomMetres, Float(light.Field("_range")), 1e-5f, id + " radius");
                Assert.AreEqual(Lighting.MarkerIntensity, Float(light.Field("_intensity")), 1e-5f, id + " intensity");
                Assert.AreEqual(Lighting.MarkerEdgeSoftness, Float(light.Field("_edgeSoftness")), 1e-5f, id + " falloff");
                Assert.AreEqual(0f, Float(light.Field("_reachMetres")), id + " throws no ground pool");
                Assert.AreEqual(lens.z * IsoGround.HeightScale, Float(light.Field("_bloomLiftMetres")), 1e-4f, id + " glows on its lens");
                Assert.AreEqual(Colour(colour), light.Field("_color"), id + " colour");
            }
        }

        [Test]
        public void TheSlipwayIsCutAtTheWaterline_ItsEdgeClimbingTheRamp()
        {
            foreach (var (name, lip, elevation) in new[] { ("rampUpper", 86f, 3.4f), ("rampLower", 66f, 0.4f) })
            {
                Doc harbour = Scene.Docs.Single(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == "harbour.west");
                Doc part = Children(Scene, harbour).Single(g => g.Field("m_Name") == name);
                Doc tide = Scene.ComponentsOf(part).Single(d => d.IsScript("HiddenHarbours.Art.TidalFaceWaterline"));
                Assert.AreEqual(lip, Float(tide.Field("_lipWorldY")), 1e-3f, name + " is cut from its top edge");
                Assert.AreEqual(elevation, Float(tide.Field("_lipElevation")), 1e-3f, name + " at its top's elevation");
                Assert.AreEqual(20f / 3f, Float(tide.Field("_slopeRunPerMetre")), 1e-4f, name + " runs 20 m for its 3 m fall");
            }
            // One ramp line: the upper's foot and the lower's top meet the water on the same row at the joint's tide.
            float upper = TidalFaceWaterline.WaterlineWorldY(86f, 3.4f, 0.4f, 20f / 3f), lower = TidalFaceWaterline.WaterlineWorldY(66f, 0.4f, 0.4f, 20f / 3f);
            Assert.AreEqual(66f - 3f * IsoGround.HeightScale, upper, 1e-3f, "at a 0.4 m tide the water reaches the upper ramp's foot");
            Assert.AreEqual(66f, lower, 1e-3f, "and the lower ramp's top");
            Assert.AreEqual(TidalFaceWaterline.WaterlineWorldY(76f, 3.4f, 1.3f), TidalFaceWaterline.WaterlineWorldY(76f, 3.4f, 1.3f, 0f), 1e-6f, "0 is a wall");
        }

        // ---- the scene, read ----

        static List<(string key, Rect rect)> Decks()
        {
            var decks = new List<(string, Rect)>();
            foreach (object part in DeckSidecarJson.AsArray(DeckSidecarJson.Member(NineMileCreekWharfWaterSide.ReadToday(), "parts")))
            {
                string key = DeckSidecarJson.String(DeckSidecarJson.Member(part, "key"));
                if (key != "west_apron" && key != "fleet_crib" && key != "fleet_conc" && key != "east_east") continue;
                var walk = DeckSidecarJson.AsArray(DeckSidecarJson.Member(part, "walkPolygon")).Select(q => DeckSidecarJson.AsArray(q))
                    .Select(q => new Vector2(DeckSidecarJson.Float(q[0]), DeckSidecarJson.Float(q[1]))).ToArray();
                decks.Add((key, Rect.MinMaxRect(walk.Min(v => v.x), walk.Min(v => v.y), walk.Max(v => v.x), walk.Max(v => v.y))));
            }
            Assert.That(decks, Has.Count.EqualTo(4));
            return decks;
        }

        static IEnumerable<Doc> Children(SceneYaml scene, Doc go) => scene.TransformOf(go).FieldRefs("m_Children")
            .Select(id => scene.Require(id, "a child")).Where(t => !t.Stripped).Select(t => scene.Require(t.FieldRef("m_GameObject"), "a child object"));

        static Doc LightOf(SceneYaml scene, Doc go) => scene.ComponentsOf(go).Single(d => d.ClassId == 114 && d.IsScript(SceneLightScript));

        static Vector2 World(Doc go)
        {
            Vector2 at = Vector2.zero;
            for (Doc tr = Scene.TransformOf(go); tr != null; tr = tr.FieldRef("m_Father") == 0 ? null : Scene.Require(tr.FieldRef("m_Father"), "a parent"))
            {
                Match m = Regex.Match(tr.Field("m_LocalPosition") ?? "", @"x: ([-\d.eE+]+), y: ([-\d.eE+]+)");
                if (m.Success) at += new Vector2(Float(m.Groups[1].Value), Float(m.Groups[2].Value));
            }
            return at;
        }

        static string Colour(Color c) => "{r: " + G(c.r) + ", g: " + G(c.g) + ", b: " + G(c.b) + ", a: " + G(c.a) + "}";
        static string G(float v) => v == 0f ? "0" : v.ToString("G9", CultureInfo.InvariantCulture);
        static float Float(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
#endif
