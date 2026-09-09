using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The grade writes through to URP's overrides and never exceeds the charter's four</b> (juice
    /// PR 1, rule 7). Builds the stack on a throwaway <see cref="VolumeProfile"/> — no camera, no GPU —
    /// and asserts: the six overrides exist with their override flags ON and start inactive; a neutral
    /// grade activates nothing; every field of an authored key lands in the matching parameter; the
    /// shipped profile at four hours × three weathers activates at most four effects and never grain or
    /// chromatic aberration; and a grade that asks for all six is capped at four, dropping chromatic
    /// aberration and grain first, with the dropped count reported.
    /// </summary>
    public class MoodGradeStackTests
    {
        VolumeProfile _profile;
        MoodGradeStack _stack;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _stack = MoodGradeStack.Build(_profile);
        }

        [TearDown]
        public void TearDown()
        {
            if (_profile == null) return;
            foreach (var c in _profile.components) if (c != null) Object.DestroyImmediate(c);
            Object.DestroyImmediate(_profile);
        }

        IEnumerable<VolumeComponent> All()
        {
            yield return _stack.Bloom; yield return _stack.ColorAdjustments; yield return _stack.LiftGammaGain;
            yield return _stack.Vignette; yield return _stack.FilmGrain; yield return _stack.ChromaticAberration;
        }

        [Test]
        public void Build_adds_the_six_overrides_with_their_override_flags_on_and_all_inactive()
        {
            Assert.That(_profile.components.Count, Is.EqualTo(6));
            Assert.That(_profile.Has<Bloom>() && _profile.Has<ColorAdjustments>() && _profile.Has<LiftGammaGain>() &&
                        _profile.Has<Vignette>() && _profile.Has<FilmGrain>() && _profile.Has<ChromaticAberration>());
            foreach (var c in All())
            {
                Assert.That(c.active, Is.False, c.GetType().Name + " starts inactive");
                foreach (var p in c.parameters)
                    Assert.That(p.overrideState, Is.True, c.GetType().Name + " parameter override is on");
            }
            Assert.That(_stack.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Build_on_a_profile_that_already_carries_the_components_reuses_them()
        {
            var again = MoodGradeStack.Build(_profile);
            Assert.That(_profile.components.Count, Is.EqualTo(6), "no duplicates");
            Assert.That(again.Bloom, Is.SameAs(_stack.Bloom));
        }

        [Test]
        public void Write_neutral_activates_nothing()
        {
            int n = _stack.Write(MoodGrade.Neutral);
            Assert.That(n, Is.EqualTo(0));
            foreach (var c in All()) Assert.That(c.active, Is.False, c.GetType().Name);
        }

        [Test]
        public void Write_lands_every_field_of_the_night_key_in_its_parameter()
        {
            var g = MoodGradeProfile.DefaultNight();
            _stack.Write(g);
            Assert.That(_stack.Bloom.intensity.value, Is.EqualTo(g.BloomIntensity));
            Assert.That(_stack.Bloom.threshold.value, Is.EqualTo(g.BloomThreshold));
            Assert.That(_stack.Bloom.scatter.value, Is.EqualTo(g.BloomScatter));
            Assert.That(_stack.LiftGammaGain.lift.value, Is.EqualTo(g.Lift));
            Assert.That(_stack.LiftGammaGain.gamma.value, Is.EqualTo(g.Gamma));
            Assert.That(_stack.LiftGammaGain.gain.value, Is.EqualTo(g.Gain));
            Assert.That(_stack.ColorAdjustments.contrast.value, Is.EqualTo(g.Contrast));
            Assert.That(_stack.ColorAdjustments.saturation.value, Is.EqualTo(g.Saturation));
            Assert.That(_stack.ColorAdjustments.colorFilter.value, Is.EqualTo(g.ColorFilter));
            Assert.That(_stack.Vignette.intensity.value, Is.EqualTo(g.VignetteIntensity));
            Assert.That(_stack.Vignette.smoothness.value, Is.EqualTo(g.VignetteSmoothness));
            Assert.That(_stack.Vignette.color.value, Is.EqualTo(g.VignetteColor));
            Assert.That(_stack.FilmGrain.intensity.value, Is.EqualTo(0f));
            Assert.That(_stack.ChromaticAberration.intensity.value, Is.EqualTo(0f));

            Assert.That(_stack.Bloom.active && _stack.LiftGammaGain.active && _stack.ColorAdjustments.active && _stack.Vignette.active);
            Assert.That(_stack.FilmGrain.active, Is.False);
            Assert.That(_stack.ChromaticAberration.active, Is.False);
            Assert.That(_stack.ActiveCount, Is.EqualTo(4));
            Assert.That(_stack.DroppedCount, Is.EqualTo(0));
        }

        [Test]
        public void Shipped_defaults_over_the_day_and_the_weathers_never_exceed_four_and_never_touch_grain_or_chroma()
        {
            var p = MoodGradeProfile.CreateDefault();
            try
            {
                var j = JuiceSettings.Default;
                float[] hours = { 2f, 6f, 12f, 20f };
                (float vis, float sea)[] weathers = { (1f, 0f), (0.1f, 0.3f), (0.5f, 1f) };
                foreach (var h in hours)
                foreach (var w in weathers)
                {
                    var g = MoodGradeMath.Evaluate(p, null, h, 6f, 20f, w.vis, w.sea, j, out _);
                    int n = _stack.Write(g);
                    Assert.That(n, Is.LessThanOrEqualTo(MoodGradeStack.MaxActiveEffects), $"h {h} vis {w.vis} sea {w.sea}");
                    Assert.That(n, Is.GreaterThan(0), $"the grade does something at h {h} vis {w.vis} sea {w.sea}");
                    Assert.That(_stack.DroppedCount, Is.EqualTo(0), $"nothing had to be cut at h {h} vis {w.vis} sea {w.sea}");
                    Assert.That(_stack.FilmGrain.active, Is.False, "grain ships off");
                    Assert.That(_stack.ChromaticAberration.active, Is.False, "chromatic aberration ships off");
                }
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void Write_caps_at_four_dropping_chromatic_aberration_then_grain_and_reports_the_cut()
        {
            var g = MoodGradeProfile.DefaultNight();
            g.GrainIntensity = 0.3f;
            g.ChromaticAberration = 0.2f;
            int n = _stack.Write(g);
            Assert.That(n, Is.EqualTo(MoodGradeStack.MaxActiveEffects));
            Assert.That(_stack.DroppedCount, Is.EqualTo(2));
            Assert.That(_stack.ChromaticAberration.active, Is.False, "chroma is the first to go");
            Assert.That(_stack.FilmGrain.active, Is.False, "grain is the second");
            Assert.That(_stack.Bloom.active, Is.True, "bloom survives when only two have to go");
            Assert.That(_stack.ColorAdjustments.active && _stack.LiftGammaGain.active && _stack.Vignette.active, "the tone trio is never dropped");

            // With bloom off, grain fits and only chroma is cut.
            g.BloomIntensity = 0f;
            n = _stack.Write(g);
            Assert.That(n, Is.EqualTo(4));
            Assert.That(_stack.DroppedCount, Is.EqualTo(1));
            Assert.That(_stack.FilmGrain.active, Is.True);
            Assert.That(_stack.ChromaticAberration.active, Is.False);
            Assert.That(_stack.Bloom.active, Is.False);
        }

        [Test]
        public void Write_turns_an_effect_back_off_when_a_later_grade_no_longer_asks_for_it()
        {
            _stack.Write(MoodGradeProfile.DefaultNight());
            Assert.That(_stack.Bloom.active, Is.True);
            var day = MoodGradeProfile.DefaultDay();
            day.BloomIntensity = 0f;
            _stack.Write(day);
            Assert.That(_stack.Bloom.active, Is.False);
            Assert.That(_stack.LiftGammaGain.active, Is.False, "day's trackballs are neutral, so LGG is off");
            Assert.That(_stack.ActiveCount, Is.EqualTo(2), "colour adjustments + vignette");
        }
    }
}
