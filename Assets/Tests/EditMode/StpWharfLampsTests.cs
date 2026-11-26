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
                Assert.AreEqual(Lighting.LanternCastsShadows(name) ? "1" : "0", light.Field("_castsShadows"), name + " shadows");
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

        // ---- the red and green off the pier head: the channel's inner pair wears the harbour-marker look ----

        static NavLightLookDef Look => Lighting.HarbourMarkerLook as NavLightLookDef;

        [Test]
        public void TheHarbourMarkerLook_IsASmallGlassCoreInASoftLowHalo()
        {
            Assert.That(Look, Is.Not.Null, "the record's marker look is a nav light look");
            Assert.AreEqual("navlight_look.harbour_marker", Look.Id);
            float disc = NavLightPresets.LanternRangeMetres;
            Assert.That(Look.CoreRadiusMetres, Is.GreaterThan(0f).And.LessThanOrEqualTo(disc * 0.3f),
                "the glass core is about a quarter of the coast preset's disc");
            Assert.That(Look.HaloRangeMetres, Is.LessThanOrEqualTo(disc), "the halo does not grow past the old disc");
            Assert.That(Look.HaloStrength, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.3f), "the halo is a low haze, not a disc");
            Assert.That(Look.EdgeSoftness, Is.GreaterThanOrEqualTo(0.8f), "and it falls off smoothly");
        }

        [Test]
        public void TheCore_BurnsItsLensColourFull_WithoutClippingToWhite()
        {
            // The core is additive: colour x strength, pulled toward white by CoreWhiten. The lens's own channel must
            // reach full (so the mark reads from a boat) while the other two stay well short (so it reads red or green,
            // not white: a white light is a different mark).
            foreach (var (colour, main) in new[] { (HiddenHarbours.Core.NavLightColour.Red, 0), (HiddenHarbours.Core.NavLightColour.Green, 1) })
            {
                Color c = Color.Lerp(Look.LensFor(colour), Color.white, Look.CoreWhiten) * Look.Intensity;
                float[] ch = { c.r, c.g, c.b };
                Assert.That(ch[main], Is.GreaterThanOrEqualTo(1f), colour + ": the lens channel burns full");
                for (int i = 0; i < 3; i++)
                    if (i != main) Assert.That(ch[i], Is.LessThan(0.8f), colour + ": channel " + i + " stays short of white");
            }
        }

        [Test]
        public void ItsLensColours_AreDeepTrueLensColours_AndTheCoreIsNotWhitened()
        {
            // The core is one flat disc in the shader (CoreWhiten tints all of it, BandSteps cuts only the halo), so a
            // lighter centre cannot be drawn from data: the core is the lens colour itself, deep, with no whitening.
            Color red = Look.LensFor(HiddenHarbours.Core.NavLightColour.Red), green = Look.LensFor(HiddenHarbours.Core.NavLightColour.Green);
            Assert.That(Saturation(red), Is.GreaterThanOrEqualTo(0.8f), "a deep lens red, not a pink");
            Assert.That(Saturation(green), Is.GreaterThanOrEqualTo(0.8f), "a deep lens green, not a mint");
            Assert.That(red.r, Is.GreaterThan(Mathf.Max(red.g, red.b) * 4f), "the red stays a red");
            Assert.That(green.g, Is.GreaterThan(Mathf.Max(green.r, green.b) * 1.8f), "the green stays a green, a little toward blue");
            Assert.That(Look.CoreWhiten, Is.EqualTo(0f), "the core is not pulled toward white");
        }

        [Test]
        public void TheLook_LaysNoStreak()
        {
            // A one-pixel cone read as a wire on the water; a broken, fading glint needs drawing the light cannot do.
            Assert.That(Look.DrawsStreak, Is.False);
        }

        [Test]
        public void TheEntrancesInnerPair_WearsTheLook_AndNoOtherMarkDoes()
        {
            Assert.That(Lighting.HarbourMarkIds, Is.EquivalentTo(new[] { "channel.stpeters_entrance.s3", "channel.stpeters_entrance.p3" }));
            string text = File.ReadAllText(ScenePath).Replace("\r\n", "\n");
            WithHarbourMarkerLooks(text, out int changed);
            Assert.AreEqual(0, changed, "the committed scene is the marker step's output");
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(Look));
            var looks = Regex.Matches(text, @"propertyPath: _look\n      value: \n      objectReference: \{fileID: 11400000, guid: (\w+)");
            Assert.That(looks.Cast<Match>().Select(m => m.Groups[1].Value), Is.EqualTo(new[] { guid, guid }), "exactly the pair carries a look");
        }

        [Test]
        public void ANavLight_WithTheLook_BurnsACoreAndLaysAStreak_WithoutIt_TheCoastPreset()
        {
            var plain = new GameObject("mark (plain)");
            var dressed = new GameObject("mark (dressed)");
            NavLightLookDef streakLook = null;
            try
            {
                plain.AddComponent<StubNavSource>();
                dressed.AddComponent<StubNavSource>();
                NavLight a = plain.AddComponent<NavLight>(), b = dressed.AddComponent<NavLight>();
                streakLook = UnityEngine.Object.Instantiate(Look);
                streakLook.StreakLengthMetres = 1.4f;   // the wiring, with a streak on (the committed look has none)
                b.Look = streakLook;
                a.Rebuild(); b.Rebuild();

                Assert.That(a.Lamp.CoreRadiusMetres, Is.EqualTo(0f), "a mark without a look is the coast preset's smooth glow");
                Assert.That(a.Lamp.Range, Is.EqualTo(NavLightPresets.LanternRangeMetres));
                Assert.That(a.Lamp.Color, Is.EqualTo(NavLightPresets.For(HiddenHarbours.Core.NavLightColour.Red).Color));
                Assert.That(a.Streak, Is.Null, "and lays no streak");

                Assert.That(b.Lamp.CoreRadiusMetres, Is.EqualTo(Look.CoreRadiusMetres));
                Assert.That(b.Lamp.HaloStrength, Is.EqualTo(Look.HaloStrength));
                Assert.That(b.Lamp.Range, Is.EqualTo(Look.HaloRangeMetres));
                Assert.That(b.Lamp.Color, Is.EqualTo(Look.LensRed));
                Assert.That(b.Lamp.CastsShadows, Is.False, "a mark's lantern still throws no shadows");
                Assert.That(b.Streak, Is.Not.Null, "the look lays a streak on the water");
                Assert.That(b.Streak.Shape, Is.EqualTo(SceneLight.LightShape.Cone));
                Assert.That(b.Streak.Range, Is.EqualTo(streakLook.StreakLengthMetres));
                Assert.That(b.Streak.Intensity, Is.LessThan(b.Lamp.Intensity * 0.5f), "faint beside the lamp");
                Assert.That(b.Streak.ReachMetres, Is.EqualTo(0f), "it lights no pool");
                Assert.That(b.Streak.CastsShadows, Is.False);
                Assert.That(b.Streak.transform.up.y, Is.LessThan(-0.99f), "thrown down the water from the waterline");
                Assert.That(b.Streak.enabled, Is.False, "dark until the character lights the lamp");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plain);
                UnityEngine.Object.DestroyImmediate(dressed);
                if (streakLook != null) UnityEngine.Object.DestroyImmediate(streakLook);
            }
        }

        [Test]
        public void TheHeadLantern_ThrowsNoShadow_BecauseTheRedCanStandsInsideItsReach()
        {
            Assert.That(Lighting.LanternsWithoutShadows, Is.EqualTo(new[] { "lanternPost_0" }), "only the head lantern");
            Vector2 head = StPetersWharf.LampPostSites()[0].Position;
            string text = File.ReadAllText(ScenePath).Replace("\r\n", "\n");
            Vector2 can = MarkAt(text, "channel.stpeters_entrance.s3");
            Assert.That(Vector2.Distance(head, can), Is.LessThan(Lighting.LanternReachMetres),
                "the red can stands inside the head lantern's reach, so a cast shadow would be thrown across open water");
            Assert.That(Lighting.LanternCastsShadows("lanternPost_1"), Is.True, "the root lantern still casts (the 11-25 ruling)");
        }

        static float Saturation(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max <= 0f ? 0f : (max - min) / max;
        }

        static Vector2 MarkAt(string text, string markId)
        {
            int at = text.IndexOf("      value: " + markId + "\n", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), markId + " is in the scene");
            int start = text.LastIndexOf("\n--- !u!1001 &", at, StringComparison.Ordinal);
            string doc = text.Substring(start, at - start);
            float x = Float(Regex.Match(doc, @"propertyPath: m_LocalPosition\.x\n      value: (\S+)").Groups[1].Value);
            float y = Float(Regex.Match(doc, @"propertyPath: m_LocalPosition\.y\n      value: (\S+)").Groups[1].Value);
            return new Vector2(x, y);
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

    /// <summary>A lit red mark with no buoy behind it: just what a <see cref="NavLight"/> reads.</summary>
    public sealed class StubNavSource : MonoBehaviour, HiddenHarbours.Core.INavLightSource
    {
        public HiddenHarbours.Core.NavLightCharacter Character => HiddenHarbours.Core.NavLightCharacter.Parse("Fl R 4s");
        public float PhaseSeconds => 0f;
        public float LanternHeightMetres => 1f;
        public Transform LanternMount => transform;
    }
}
#endif
