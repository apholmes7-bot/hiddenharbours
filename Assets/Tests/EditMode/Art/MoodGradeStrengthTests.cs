using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>THE MASTER DIAL — the owner's 2026-09-09 ruling in numbers.</b>
    /// <i>"the juice lane added a very blue filter at night and very noticeable yellow filter before and
    /// after, its far too noticeable, i do like it on the intro maybe toned down a little though, but it
    /// completely washes everything out on the screen except the colour"</i>
    ///
    /// <para>Two things stack on the frame: <c>DayNightProfile</c>'s whole-screen MULTIPLY and this
    /// grade's URP Volume. The tone-down settles the argument twice over — <b>the multiply owns the
    /// time-of-day COLOUR, the grade owns TONE</b> (docs/design/lighting-and-daynight.md §8.2), and the
    /// grade gets a strength it never had. These cases guard both halves:</para>
    /// <list type="bullet">
    ///   <item><b>the dial is a real lerp with exact ends</b> — 0 is the pre-juice frame (nothing goes
    ///   active on the volume at all), 1 is the look that shipped before the dial existed, to the bit,
    ///   and in between the WHOLE look comes on together, tone and hue residue alike;</item>
    ///   <item><b>the intro keeps its own strength</b> — <c>MoodGradeDirector.StrengthFor</c> picks off
    ///   the Core fact <c>GameServices.OpeningCinematicRunning</c>, never a scene name (rule 4), and a
    ///   <c>Reset</c> puts that fact down;</item>
    ///   <item><b>the numbers live in the ASSETS the owner can move</b> (rule 6) — and a serialized field
    ///   absent from a <c>.asset</c> reads ZERO, which on these two fields is no grade at all, so the
    ///   shipped YAML is read directly as well as through the loader.</item>
    /// </list>
    ///
    /// <para>⚠ <b>The VALUES here are the owner's, not this file's.</b> Every bar below is the RULING
    /// (a fraction rather than full; the intro at least as strong as play; a golden window narrower than
    /// an hour either side of the crossing) and never his exact number — he rules on those from the
    /// plates in the PR, and must be able to move them without reddening a test.</para>
    /// </summary>
    public class MoodGradeStrengthTests
    {
        const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const float Sunrise = 6f, Sunset = 20f;

        static JuiceSettings J => JuiceSettings.Default;

        bool _factWasUp;

        [SetUp]
        public void RememberTheOpeningFact() => _factWasUp = GameServices.OpeningCinematicRunning;

        /// <summary>The Core fact is a static: a case that raises it and walks away would hand the next
        /// fixture an opening that never ends.</summary>
        [TearDown]
        public void PutTheOpeningFactBack() => GameServices.OpeningCinematicRunning = _factWasUp;

        static void AssertGradeEqual(in MoodGrade a, in MoodGrade b, string why, float eps = 1e-5f)
        {
            Assert.That(b.BloomIntensity, Is.EqualTo(a.BloomIntensity).Within(eps), why + " bloom");
            Assert.That(b.BloomThreshold, Is.EqualTo(a.BloomThreshold).Within(eps), why + " threshold");
            Assert.That(b.BloomScatter, Is.EqualTo(a.BloomScatter).Within(eps), why + " scatter");
            Assert.That(b.Lift, Is.EqualTo(a.Lift), why + " lift");
            Assert.That(b.Gamma, Is.EqualTo(a.Gamma), why + " gamma");
            Assert.That(b.Gain, Is.EqualTo(a.Gain), why + " gain");
            Assert.That(b.PostExposure, Is.EqualTo(a.PostExposure).Within(eps), why + " exposure");
            Assert.That(b.Contrast, Is.EqualTo(a.Contrast).Within(eps), why + " contrast");
            Assert.That(b.Saturation, Is.EqualTo(a.Saturation).Within(eps), why + " saturation");
            Assert.That(b.ColorFilter, Is.EqualTo(a.ColorFilter), why + " filter");
            Assert.That(b.VignetteIntensity, Is.EqualTo(a.VignetteIntensity).Within(eps), why + " vignette");
            Assert.That(b.VignetteSmoothness, Is.EqualTo(a.VignetteSmoothness).Within(eps), why + " vignette smoothness");
            Assert.That(b.VignetteColor, Is.EqualTo(a.VignetteColor), why + " vignette colour");
            Assert.That(b.GrainIntensity, Is.EqualTo(a.GrainIntensity).Within(eps), why + " grain");
            Assert.That(b.ChromaticAberration, Is.EqualTo(a.ChromaticAberration).Within(eps), why + " chroma");
        }

        // ---- the dial itself -------------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>Strength 0 is the frame the owner had before the juice lane touched it</b> — not "a very
        /// faint grade", the identity. And the stronger claim underneath it: at 0 the stack finds NOTHING
        /// to activate, so the volume costs the frame nothing either (rule 7).
        /// </summary>
        [Test]
        public void AtStrength_zero_is_the_identity_and_activates_nothing_on_the_volume()
        {
            var off = MoodGradeMath.AtStrength(MoodGradeProfile.DefaultNight(), 0f);
            AssertGradeEqual(MoodGrade.Neutral, off, "strength 0 on the night key", 0f);

            Assert.IsTrue(off.BloomIsIdentity, "bloom is still lit at strength 0");
            Assert.IsTrue(off.LiftGammaGainIsIdentity, "the trackballs still push at strength 0");
            Assert.IsTrue(off.ColorAdjustmentsIsIdentity, "contrast/saturation/filter still bite at strength 0");
            Assert.IsTrue(off.VignetteIsIdentity, "the vignette still closes at strength 0");
            Assert.IsTrue(off.GrainIsIdentity, "grain at strength 0");
            Assert.IsTrue(off.ChromaticAberrationIsIdentity, "chromatic aberration at strength 0");
        }

        /// <summary>
        /// ⭐ <b>Strength 1 is EXACTLY the authored look</b>, to the bit — <see cref="MoodGradeMath.AtStrength"/>
        /// returns the operand itself rather than a lerp that lands within a float of it, so "full
        /// strength is what shipped" is a fact and not a tolerance. Asserted at eps 0 on purpose.
        /// </summary>
        [Test]
        public void AtStrength_one_is_the_authored_grade_itself_to_the_bit()
        {
            foreach (var authored in new[] { MoodGradeProfile.DefaultNight(), MoodGradeProfile.DefaultGoldenHour(),
                                             MoodGradeProfile.DefaultDay(), MoodGradeProfile.DefaultStorm() })
                AssertGradeEqual(authored, MoodGradeMath.AtStrength(authored, 1f), "strength 1", 0f);
        }

        /// <summary>
        /// <b>One dial, no carve-outs.</b> Half strength is half the bloom, half the vignette, half the
        /// saturation pull — and the little hue residue left in the trackballs comes halfway back to
        /// neutral with them. Nothing is exempt, and nothing overshoots either end on the way up.
        /// </summary>
        [Test]
        public void AtStrength_in_between_brings_the_whole_look_on_together()
        {
            var night = MoodGradeProfile.DefaultNight();
            var half = MoodGradeMath.AtStrength(night, 0.5f);

            Assert.That(half.BloomIntensity, Is.EqualTo(0.5f * night.BloomIntensity).Within(1e-5f), "bloom");
            Assert.That(half.VignetteIntensity, Is.EqualTo(0.5f * night.VignetteIntensity).Within(1e-5f), "vignette");
            Assert.That(half.Saturation, Is.EqualTo(0.5f * night.Saturation).Within(1e-5f), "saturation");
            Assert.That(half.Contrast, Is.EqualTo(0.5f * night.Contrast).Within(1e-5f), "contrast");
            Assert.That(half.Lift.z, Is.EqualTo(1f + 0.5f * (night.Lift.z - 1f)).Within(1e-6f),
                "the cold residue in the shadows must fade with the rest of the look, not sit at full");

            float lastBloom = -1f, lastVignette = -1f;
            for (float t = 0f; t <= 1.0001f; t += 0.1f)
            {
                var g = MoodGradeMath.AtStrength(night, t);
                Assert.That(g.BloomIntensity, Is.GreaterThanOrEqualTo(lastBloom - 1e-6f), $"bloom fell back at t={t:F1}");
                Assert.That(g.VignetteIntensity, Is.GreaterThanOrEqualTo(lastVignette - 1e-6f), $"vignette fell back at t={t:F1}");
                Assert.That(g.BloomIntensity, Is.LessThanOrEqualTo(night.BloomIntensity + 1e-6f), $"bloom overshot at t={t:F1}");
                lastBloom = g.BloomIntensity; lastVignette = g.VignetteIntensity;
            }
        }

        // ---- the dial through the whole model ---------------------------------------------------------

        /// <summary>
        /// <b>The dial is the ONLY thing the tone-down added to <see cref="MoodGradeMath.Evaluate"/>.</b>
        /// At strength 1 it is still weights, blend, region, clamp — so a regression in the dial cannot
        /// hide behind a changed blend, and the existing <c>MoodGradeMathTests</c> that now pass
        /// <c>1f</c> keep measuring what they always measured.
        /// </summary>
        [Test]
        public void Evaluate_at_full_strength_is_the_blend_that_shipped_before_the_dial()
        {
            var p = MoodGradeProfile.CreateDefault();
            foreach (var (hour, vis, sea) in new[] { (2f, 1f, 0f), (Sunset, 1f, 0f), (12f, 0.05f, 0f), (3f, 0.4f, 0.95f) })
            {
                var w = MoodGradeMath.Weights(hour, Sunrise, Sunset, vis, sea, J);
                var expected = MoodGradeMath.ApplyRegion(MoodGradeMath.Blend(p, w), null).Clamped();
                var got = MoodGradeMath.Evaluate(p, null, hour, Sunrise, Sunset, vis, sea, J, 1f, out _);
                AssertGradeEqual(expected, got, $"strength 1 at {hour:F1}h vis {vis:F2} sea {sea:F2}", 0f);
            }
        }

        /// <summary>At strength 0 there is no hour of the day and no weather that puts anything on the
        /// frame — the owner's "far too noticeable" has a zero.</summary>
        [Test]
        public void Evaluate_at_zero_strength_is_the_pre_juice_frame_at_every_hour()
        {
            var p = MoodGradeProfile.CreateDefault();
            for (float h = 0f; h < 24f; h += 0.5f)
            {
                var g = MoodGradeMath.Evaluate(p, null, h, Sunrise, Sunset, 0.3f, 0.6f, J, 0f, out _);
                AssertGradeEqual(MoodGrade.Neutral, g, $"strength 0 at {h:F1}h", 0f);
            }
        }

        // ---- which dial, and who says so --------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>"i do like it on the intro maybe toned down a little though."</b> The intro's strength is
        /// chosen off a CORE FACT and nothing else — no scene name, no cinematic class reached into from
        /// Art (rule 4). One place makes the choice, so there is one place to be wrong.
        /// </summary>
        [Test]
        public void StrengthFor_reads_the_intro_dial_only_while_the_opening_is_running()
        {
            var juice = J;
            juice.GradeStrength = 0.3f;
            juice.GradeIntroStrength = 0.8f;
            Assert.AreNotEqual(juice.GradeStrength, juice.GradeIntroStrength,
                "harness: the two dials must differ here or this case cannot tell them apart");

            GameServices.OpeningCinematicRunning = false;
            Assert.That(MoodGradeDirector.StrengthFor(juice), Is.EqualTo(0.3f).Within(1e-6f),
                "in PLAY the grade must use GradeStrength");

            GameServices.OpeningCinematicRunning = true;
            Assert.That(MoodGradeDirector.StrengthFor(juice), Is.EqualTo(0.8f).Within(1e-6f),
                "while the arrival opening runs the grade must use GradeIntroStrength");
        }

        /// <summary>A torn-down world must not leave the intro's strength standing over the game that
        /// follows it.</summary>
        [Test]
        public void Reset_puts_the_opening_fact_down()
        {
            GameServices.OpeningCinematicRunning = true;
            GameServices.Reset();
            Assert.IsFalse(GameServices.OpeningCinematicRunning,
                "GameServices.Reset left the opening fact up — the next run would wear the intro grade in play");
        }

        // ---- the numbers the owner ships --------------------------------------------------------------

        static GameConfig ShippedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, $"the shipped config {ConfigAssetPath} must exist — every scene wires it");
            return config;
        }

        /// <summary>
        /// ⭐ <b>The dials are IN the asset, and they are alive.</b>
        ///
        /// <para>⚠ This is not a mirror of the constructor. A serialized field ABSENT from a
        /// <c>.asset</c> reads ZERO rather than its code default, and zero on these two fields is no
        /// grade at all — in play and on the intro alike. So the loaded values are asserted live, and
        /// the ruling's SHAPE with them: a fraction rather than full (the grade is a mood, not a
        /// filter), and the intro at least as strong as play.</para>
        /// </summary>
        [Test]
        public void TheShippedConfig_CarriesBothStrengths_AndKeepsTheIntroTheStrongerOne()
        {
            var juice = ShippedConfig().Juice;

            Assert.That(juice.GradeStrength, Is.GreaterThan(0f),
                "GradeStrength is 0 or missing from GameConfig.asset — the play grade is off entirely");
            Assert.That(juice.GradeIntroStrength, Is.GreaterThan(0f),
                "GradeIntroStrength is 0 or missing from GameConfig.asset — the intro loses the grade the owner kept");
            Assert.That(juice.GradeStrength, Is.LessThanOrEqualTo(1f));
            Assert.That(juice.GradeIntroStrength, Is.LessThanOrEqualTo(1f));

            Assert.That(juice.GradeStrength, Is.LessThan(1f),
                "the owner played the grade at FULL on 2026-09-09 and ruled it a filter over the world " +
                "(\"its far too noticeable\") — what ships in play must be a fraction of it");
            Assert.That(juice.GradeIntroStrength, Is.GreaterThanOrEqualTo(juice.GradeStrength),
                "the intro is the one place he liked it — its dial must not sit below the play dial");
        }

        /// <summary>
        /// The same two numbers read from the FILE, not through the loader. This is the guard the memory
        /// law asks for: the loader can only report what the YAML carries, and a key dropped by a stray
        /// Unity save would come back through it as a plausible-looking zero.
        /// </summary>
        [Test]
        public void TheShippedConfigYaml_CarriesTheTwoStrengthKeysWithLiveValues()
        {
            Assert.IsTrue(File.Exists(ConfigAssetPath), ConfigAssetPath);
            float play = float.NaN, intro = float.NaN;
            foreach (var raw in File.ReadAllLines(ConfigAssetPath))
            {
                string line = raw.Trim();
                if (line.StartsWith("GradeStrength:")) play = ParseAfterColon(line);
                else if (line.StartsWith("GradeIntroStrength:")) intro = ParseAfterColon(line);
            }

            Assert.IsFalse(float.IsNaN(play), "GradeStrength is not written in " + ConfigAssetPath);
            Assert.IsFalse(float.IsNaN(intro), "GradeIntroStrength is not written in " + ConfigAssetPath);
            Assert.That(play, Is.GreaterThan(0f), "GradeStrength ships as zero — no grade in play");
            Assert.That(intro, Is.GreaterThan(0f), "GradeIntroStrength ships as zero — no grade on the intro");
        }

        static float ParseAfterColon(string line)
        {
            string v = line.Substring(line.IndexOf(':') + 1).Trim();
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : float.NaN;
        }

        /// <summary>
        /// ⭐ <b>"very noticeable yellow filter before and after."</b> He was right about the width: at
        /// the old 1.5 h the golden key was live from an hour and a half before sunset to an hour and a
        /// half after — most of the evening, twice a day. The window ships narrower, and the bar is his
        /// sentence rather than a number: an hour off the crossing must be clear of it.
        /// </summary>
        [Test]
        public void TheShippedConfig_NarrowsTheGoldenWindowSoAnHourOffTheCrossingIsClear()
        {
            var juice = ShippedConfig().Juice;

            Assert.That(juice.GradeGoldenHourWidthHours, Is.GreaterThan(0f),
                "a zero width kills the golden key outright — the owner asked for it toned down, not removed");
            Assert.That(juice.GradeGoldenHourWidthHours, Is.LessThan(1f),
                "the golden wash still reaches a full hour either side of the crossing, which is exactly " +
                "the \"before and after\" the owner complained about");

            MoodGradeMath.TimeOfDay(Sunset - 1f, Sunrise, Sunset,
                juice.GradeGoldenHourWidthHours, juice.GradeNightBlendHours, out _, out float g, out _);
            Assert.That(g, Is.EqualTo(0f).Within(1e-6f), "an hour before sunset is still golden");

            MoodGradeMath.TimeOfDay(Sunset, Sunrise, Sunset,
                juice.GradeGoldenHourWidthHours, juice.GradeNightBlendHours, out _, out float onIt, out _);
            Assert.That(onIt, Is.EqualTo(1f).Within(1e-6f), "and the crossing itself lost its golden hour");
        }

        /// <summary>
        /// ⭐⭐ <b>THE HUE-OWNER RULE.</b> <c>DayNightProfile</c>'s multiply already paints the whole
        /// screen blue at midnight and orange at dusk. When the grade painted a hue over that, the two
        /// stacked and the world's own colours went under them — "it completely washes everything out on
        /// the screen except the colour". So the grade's two time-of-day keys keep TONE (bloom, contrast,
        /// saturation, vignette, the black point) and hand the COLOUR back: the filter is white and the
        /// trackball channels carry a residue, not a tint. Never both systems. Asserted on the SHIPPED
        /// asset, because that is the one the game loads.
        /// </summary>
        [Test]
        public void TheShippedProfile_LeavesTheTimeOfDayColourToTheMultiply()
        {
            var p = Resources.Load<MoodGradeProfile>("MoodGradeProfile");
            Assert.IsNotNull(p, "Resources/MoodGradeProfile.asset is missing — the director would fall back to code");

            AssertOwnsToneNotHue("Night", p.Night);
            AssertOwnsToneNotHue("GoldenHour", p.GoldenHour);
        }

        const float MaxHueResidue = 0.02f;

        static void AssertOwnsToneNotHue(string which, in MoodGrade g)
        {
            Assert.That(g.ColorFilter.r, Is.EqualTo(1f).Within(1e-4f), which + ": the colour filter must be WHITE — the multiply owns the hue");
            Assert.That(g.ColorFilter.g, Is.EqualTo(1f).Within(1e-4f), which + ": colour filter green");
            Assert.That(g.ColorFilter.b, Is.EqualTo(1f).Within(1e-4f), which + ": colour filter blue");

            AssertResidueOnly(which + " Lift", g.Lift);
            AssertResidueOnly(which + " Gamma", g.Gamma);
            AssertResidueOnly(which + " Gain", g.Gain);

            Assert.That(g.VignetteColor.r, Is.LessThanOrEqualTo(MaxHueResidue), which + ": the vignette must darken, not tint");
            Assert.That(g.VignetteColor.g, Is.LessThanOrEqualTo(MaxHueResidue), which + ": vignette green");
            Assert.That(g.VignetteColor.b, Is.LessThanOrEqualTo(MaxHueResidue), which + ": vignette blue");
        }

        /// <summary>The RGB of a trackball may lean, by a breath — the <c>.w</c> is the black point and
        /// is TONE, so it is deliberately not checked here.</summary>
        static void AssertResidueOnly(string what, Vector4 v)
        {
            Assert.That(Mathf.Abs(v.x - 1f), Is.LessThanOrEqualTo(MaxHueResidue + 1e-5f), what + ".r is a tint, not a residue");
            Assert.That(Mathf.Abs(v.y - 1f), Is.LessThanOrEqualTo(MaxHueResidue + 1e-5f), what + ".g is a tint, not a residue");
            Assert.That(Mathf.Abs(v.z - 1f), Is.LessThanOrEqualTo(MaxHueResidue + 1e-5f), what + ".b is a tint, not a residue");
        }
    }
}
