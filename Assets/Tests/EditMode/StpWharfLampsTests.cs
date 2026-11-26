#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters' pier at night: its two lanterns take their strength and pool from the wharf lighting record
    /// (<c>lighting.stp_wharf</c>), tuned as Nine Mile Creek's wall lanterns are, while the shared lantern preset every
    /// village lantern burns stays as it was. The pier stays lit at both ends and dark in the middle.
    /// </summary>
    public class StpWharfLampsTests
    {
        const string SceneLightScript = "HiddenHarbours.Art.SceneLight";
        const string PresetScript = "HiddenHarbours.Art.PreconfiguredLight";
        /// <summary>A sheet's frame (50 x 28 m at 32 px per metre on a 1600 x 900 plate).</summary>
        static readonly Vector2 ViewMetres = new Vector2(50f, 28f);
        /// <summary>The brightest night the plates measure (a full moon at its peak, clear sky): where a pool lifts least.</summary>
        const float BrightestNightLuma = 0.21f;
        /// <summary>From a hull's near side, lying alongside, to where a person stands in her.</summary>
        const float InboardMetres = 1f;

        static SceneYaml _scene;
        static SceneYaml Scene => _scene ??= SceneYaml.Parse(File.ReadAllText(ScenePath));
        static WharfLightingDef Lighting => WharfLighting();
        static LightPresets.Kind Kind => LampPosts.PresetFor(LampPosts.LanternPost);

        [Test]
        public void TheLightingRecordHasItsStableId_AndLeavesTheSitesToThePiersLayout()
        {
            Assert.AreEqual("lighting.stp_wharf", Lighting.Id);
            Assert.That(Lighting.LanternSites, Is.Empty, "the pier's lanterns stand where StPetersWharf.LampPostSites puts them");
            Assert.That(Lighting.LitPoleIds, Is.Empty, "the pier carries no lamp poles");
            Assert.That(Lighting.LanternIntensityScale, Is.GreaterThan(1f));
            Assert.That(Lighting.LanternReachMetres, Is.GreaterThan(LightPresets.ReachMetres(Kind)));
        }

        [Test]
        public void TheCommittedScene_IsTheStepsPlan_ASecondPlanIsEmpty()
        {
            LayerPatch patch = PlanWharfLighting(Scene);
            Assert.IsTrue(patch.IsEmpty, "the pier lamps differ from the record:\n" + patch.Summary());
        }

        [Test]
        public void EachPierLantern_BurnsTheSharedLanternAtTheRecordsStrengthAndReach()
        {
            var look = LightPresets.For(Kind);
            var lanterns = PierLanterns();
            Assert.That(lanterns, Has.Count.EqualTo(StPetersWharf.LampPostSites().Count), "one lantern per pier site");
            foreach (Doc go in lanterns)
            {
                string name = go.Field("m_Name");
                Doc light = Component(go, SceneLightScript), preset = Component(go, PresetScript);
                Assert.AreEqual(look.Intensity * Lighting.LanternIntensityScale, Float(light.Field("_intensity")), 1e-4f, name + " intensity");
                Assert.AreEqual(Lighting.LanternReachMetres, Float(light.Field("_reachMetres")), 1e-5f, name + " reach");
                Assert.AreEqual(Lighting.LanternsCastShadows ? "1" : "0", light.Field("_castsShadows"), name + " shadows");
                Assert.AreEqual(Lighting.LanternIntensityScale, Float(preset.Field("_intensityScale")), 1e-5f, name + ": the preset component carries the scale it stamps on wake");
                Assert.AreEqual(Lighting.LanternReachMetres, Float(preset.Field("_reachOverrideMetres")), 1e-5f, name + ": and the reach");
            }
        }

        [Test]
        public void TheVillagesLanterns_KeepTheSharedPreset()
        {
            Doc village = Scene.RootNamed("StPetersVillageLights");
            var posts = Children(village).Where(g => g.Field("m_Name").StartsWith("light.st_peters.lantern_post_", StringComparison.Ordinal)).ToList();
            Assert.That(posts, Is.Not.Empty, "the census finds the village's lane lanterns");
            foreach (Doc go in posts)
            {
                Assert.AreEqual(LightPresets.ReachMetres(Kind), Float(Component(go, SceneLightScript).Field("_reachMetres")), 1e-5f,
                    go.Field("m_Name") + " is untouched by the pier's record");
            }
        }

        [Test]
        public void AtNight_EachLanternAtLeastDoublesTheLight_OnAHullAlongsideThePier()
        {
            var profile = Resources.Load<LampShadowProfile>("LampShadowProfile");
            var look = LightPresets.For(Kind);
            float height = LampPosts.HeadHeightMetres(LampPosts.DecorFamily, LampPosts.LanternPost);
            float toHull = StPetersWharf.LampRowY - (StPetersWharf.MooringFaceY - StPetersBuilder.AlongsideFenderGapMetres) + InboardMetres;
            float lift = LightMath.PoolGain(LightMath.PoolBaseGain(profile.PoolStrength, look.Intensity * Lighting.LanternIntensityScale, 1f, BrightestNightLuma),
                height, toHull, Lighting.LanternReachMetres, profile.PoolEdgeSoftness);
            Assert.That(1f + lift, Is.GreaterThanOrEqualTo(2f),
                "a person in a boat alongside, " + toHull.ToString("0.00") + " m off the lamp row, must read at night: the lantern lifts her light by " + (1f + lift));
            float shared = LightMath.PoolGain(LightMath.PoolBaseGain(profile.PoolStrength, look.Intensity, 1f, BrightestNightLuma),
                height, toHull, LightPresets.ReachMetres(Kind), profile.PoolEdgeSoftness);
            Assert.That(shared, Is.LessThan(lift), "the shared preset alone did not reach her");
        }

        [Test]
        public void ThePierStaysLitAtBothEnds_AndDarkInTheMiddle()
        {
            var xs = StPetersWharf.LampPostSites().Select(s => s.Position.x).OrderBy(x => x).ToArray();
            Assert.That(xs, Has.Length.EqualTo(2), "a head lamp and a root lamp, not a run (municipal-infrastructure.md 3.4)");
            Assert.That(xs[1] - xs[0], Is.GreaterThan(2f * Lighting.LanternReachMetres),
                "the two pools must not meet: a community wharf is lit at its ends and dark in its middle");
        }

        [Test]
        public void TheLampsStayInsideTheLightBudget()
        {
            var profile = Resources.Load<LampShadowProfile>("LampShadowProfile");
            var pools = Scene.Docs.Where(d => d.ClassId == 114 && d.IsScript(SceneLightScript) && Float(d.Field("_reachMetres") ?? "0") > 0f)
                .Select(d => World(Scene.Require(d.FieldRef("m_GameObject"), "a light"))).ToList();
            foreach (Doc go in PierLanterns())
            {
                Vector2 centre = World(go);
                int inView = pools.Count(p => Mathf.Abs(p.x - centre.x) <= ViewMetres.x * 0.5f && Mathf.Abs(p.y - centre.y) <= ViewMetres.y * 0.5f);
                Assert.That(inView, Is.LessThanOrEqualTo(profile.MaxPools), "a frame round " + centre + " holds " + inView + " pools");
            }
            var casters = Scene.Docs.Where(d => d.ClassId == 114 && d.IsScript(SceneLightScript) && d.Field("_castsShadows") == "1")
                .Select(d => World(Scene.Require(d.FieldRef("m_GameObject"), "a light"))).ToList();
            foreach (Doc go in PierLanterns())
            {
                Vector2 centre = World(go);
                Assert.That(casters.Count(p => Mathf.Abs(p.x - centre.x) <= ViewMetres.x && Mathf.Abs(p.y - centre.y) <= ViewMetres.y),
                    Is.LessThanOrEqualTo(profile.MaxShadows), "shadow-casting lamps near " + centre);
            }
        }

        [Test]
        public void TheChannelMarksLanterns_AreAlreadyASmallMarkerGlow()
        {
            // The red and green off the pier are the channel buoys' lanterns (NavLight), drawn by the shared navigation
            // preset for every mark on the coast: already the small glow Nine Mile Creek's harbour lights were brought to.
            WharfLightingDef nmc = NineMileCreekWharfLandSide.Lighting();
            Assert.That(NavLightPresets.LanternRangeMetres, Is.InRange(0.4f, 1.6f), "a marker reads as a point from a boat, not a flood");
            Assert.That(NavLightPresets.LanternRangeMetres, Is.EqualTo(nmc.MarkerBloomMetres).Within(0.25f), "the same size as Nine Mile Creek's harbour lights");
            Assert.That(NavLightPresets.LanternIntensity, Is.EqualTo(nmc.MarkerIntensity).Within(0.25f), "and the same strength");
        }

        // ---- the scene, read ----

        static List<Doc> PierLanterns()
        {
            Doc root = Scene.RootNamed(StPetersWharf.RootName);
            Doc lamps = Scene.ChildNamed(Scene.TransformOf(root), "Lamps");
            return Children(lamps).Where(g => g.Field("m_Name").StartsWith("lanternPost_", StringComparison.Ordinal)).ToList();
        }

        static Doc Component(Doc go, string script) => Scene.ComponentsOf(go).Single(d => d.ClassId == 114 && d.IsScript(script));

        static IEnumerable<Doc> Children(Doc go) => Scene.TransformOf(go).FieldRefs("m_Children")
            .Select(id => Scene.Require(id, "a child")).Where(t => !t.Stripped).Select(t => Scene.Require(t.FieldRef("m_GameObject"), "a child object"));

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

        static float Float(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
#endif
