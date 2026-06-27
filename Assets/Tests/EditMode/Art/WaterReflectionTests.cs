using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the WATER REFLECTION sea-state response curve (the calm→stormy behaviour of the
    /// faked sky-reflection layer added to HiddenHarboursWater.shader). The reflection itself — the in-shader
    /// mirror sheen that stamps the day/night sky + a sun streak down the surface — is GPU maths and can't be
    /// evaluated headless, but the two scalars that decide HOW it reads as the sea changes mood are pure
    /// functions of the already-pushed sea-state uniforms (<c>_Chop</c>, <c>_Roughness</c>), mirrored in
    /// <see cref="WaterReflection"/> and locked here without opening Unity:
    ///
    ///   • STRENGTH — STRONG on glassy/CALM water, FADING to ~0 by a tunable sea-state (a storm doesn't mirror),
    ///     and additionally dimmed by wind. The master dial (<c>_ReflectionStrength</c> = 0) turns it fully off.
    ///   • SHARPNESS — a clean MIRROR at CALM (≈1), SMEARING/scattering toward 0 as chop + wind rise.
    ///
    /// These twins are NOT pushed to the material and NOT added to WaterSurface.cs — they read the existing
    /// sea-state uniforms, so there is no new C# uniform push. Everything the reflection drives in the shader is
    /// VISUAL-ONLY (col.rgb), never depth/clip/_WaterLevel — it saves nothing and feeds no sim (P1, rule 5).
    /// </summary>
    public class WaterReflectionTests
    {
        // Shader defaults (kept in sync with the Properties block / Water.mat) so the tests exercise the
        // shipped configuration, not an invented one.
        private const float DefMaster = 0.6f;       // _ReflectionStrength
        private const float DefFadeChop = 0.6f;     // _ReflectionFadeChop
        private const float DefWindFade = 0.5f;     // _ReflectionWindFade
        private const float DefChopScatter = 1.5f;  // _ReflectionChopScatter
        private const float DefWindScatter = 0.8f;  // _ReflectionWindScatter

        // ===== STRENGTH: strong on calm, gone in a storm ==================================================

        [Test]
        public void Strength_IsStrongOnGlass_AndFadesToZeroByTheFadeChop()
        {
            float glass = WaterReflection.ReflectionStrength(
                chop: 0f, roughness: 0f, DefFadeChop, DefWindFade, DefMaster);
            float storm = WaterReflection.ReflectionStrength(
                chop: DefFadeChop, roughness: 0f, DefFadeChop, DefWindFade, DefMaster);

            Assert.AreEqual(DefMaster, glass, 1e-5f,
                "on glassy CALM water (no chop, no wind) the reflection reads at FULL master strength (a clean mirror)");
            Assert.AreEqual(0f, storm, 1e-5f,
                "by the fade-out sea-state the reflection has fully faded to nothing (a storm doesn't mirror)");
        }

        [Test]
        public void Strength_IsMonotonicNonIncreasingInChop()
        {
            // As the sea-state rises the reflection only ever weakens (calm strong -> stormy gone), never re-strengthens.
            float prev = WaterReflection.ReflectionStrength(0f, 0f, DefFadeChop, DefWindFade, DefMaster);
            for (float chop = 0.02f; chop <= 1f; chop += 0.02f)
            {
                float s = WaterReflection.ReflectionStrength(chop, 0f, DefFadeChop, DefWindFade, DefMaster);
                Assert.LessOrEqual(s, prev + 1e-5f, "reflection strength never increases as the sea roughens");
                Assert.That(s, Is.InRange(0f, 1f), "strength stays a 0..1 weight");
                prev = s;
            }
        }

        [Test]
        public void Strength_StaysZeroPastTheFadeChop_NoNegativeOrRevival()
        {
            // Beyond the fade-out sea-state it stays at zero (a gale never resurrects the mirror).
            for (float chop = DefFadeChop; chop <= 1.5f; chop += 0.1f)
            {
                float s = WaterReflection.ReflectionStrength(chop, 0f, DefFadeChop, DefWindFade, DefMaster);
                Assert.AreEqual(0f, s, 1e-5f, "past the fade chop the reflection is gone and stays gone");
            }
        }

        [Test]
        public void Strength_WindAdditionallyDimsIt()
        {
            // At a fixed (low) chop, wind whitecaps dim the reflection further — a breezy sea mirrors less.
            float calmAir = WaterReflection.ReflectionStrength(0.2f, 0f, DefFadeChop, DefWindFade, DefMaster);
            float breezy  = WaterReflection.ReflectionStrength(0.2f, 0.5f, DefFadeChop, DefWindFade, DefMaster);
            float gale    = WaterReflection.ReflectionStrength(0.2f, 1f, DefFadeChop, DefWindFade, DefMaster);
            Assert.Less(breezy, calmAir, "wind whitecaps dim the reflection (a breezy sea mirrors less than still air)");
            Assert.Less(gale, breezy, "monotonic in wind — more wind dims it more");
        }

        [Test]
        public void Strength_MasterDialZeroTurnsItFullyOff_TodaysLook()
        {
            // The headline tunable: _ReflectionStrength = 0 => no reflection at ANY sea-state (the pre-feature look).
            for (float chop = 0f; chop <= 1f; chop += 0.25f)
                for (float wind = 0f; wind <= 1f; wind += 0.5f)
                    Assert.AreEqual(0f,
                        WaterReflection.ReflectionStrength(chop, wind, DefFadeChop, DefWindFade, /*master*/0f), 1e-6f,
                        "master 0 => reflections fully off (today's look) regardless of sea-state");
        }

        [Test]
        public void Strength_MasterScalesTheCalmPeak()
        {
            // The master is a linear opacity scale on the calm peak (so the owner dials the overall intensity).
            float half = WaterReflection.ReflectionStrength(0f, 0f, DefFadeChop, DefWindFade, 0.5f);
            float full = WaterReflection.ReflectionStrength(0f, 0f, DefFadeChop, DefWindFade, 1.0f);
            Assert.AreEqual(0.5f, half, 1e-5f, "half master => half the calm reflection strength");
            Assert.AreEqual(1.0f, full, 1e-5f, "full master => the full calm reflection strength");
        }

        // ===== SHARPNESS: mirror on calm, smeared on chop =================================================

        [Test]
        public void Sharpness_IsAMirrorOnGlass_AndSmearsWithChopAndWind()
        {
            float glass = WaterReflection.ReflectionSharpness(0f, 0f, DefChopScatter, DefWindScatter);
            Assert.AreEqual(1f, glass, 1e-5f,
                "on glassy CALM water the reflection is a clean SHARP mirror (sharpness 1)");

            float lively = WaterReflection.ReflectionSharpness(0.3f, 0.3f, DefChopScatter, DefWindScatter);
            Assert.Less(lively, glass, "a livelier sea smears the reflection (sharpness drops below the mirror)");

            float stormy = WaterReflection.ReflectionSharpness(0.8f, 0.8f, DefChopScatter, DefWindScatter);
            Assert.Less(stormy, lively, "a stormier sea smears it further (sharpness keeps dropping toward 0)");
        }

        [Test]
        public void Sharpness_IsMonotonicNonIncreasingInChop_AndStaysAWeight()
        {
            float prev = WaterReflection.ReflectionSharpness(0f, 0f, DefChopScatter, DefWindScatter);
            for (float chop = 0.02f; chop <= 1f; chop += 0.02f)
            {
                float s = WaterReflection.ReflectionSharpness(chop, 0f, DefChopScatter, DefWindScatter);
                Assert.LessOrEqual(s, prev + 1e-5f, "sharpness never increases as the chop rises");
                Assert.That(s, Is.InRange(0f, 1f), "sharpness stays a 0..1 weight (clamped, no negative smear)");
                prev = s;
            }
        }

        [Test]
        public void Sharpness_WindScattersItIndependentlyOfChop()
        {
            // Wind alone (no chop) still smears the mirror — whitecaps break it up.
            float still  = WaterReflection.ReflectionSharpness(0f, 0f, DefChopScatter, DefWindScatter);
            float breezy = WaterReflection.ReflectionSharpness(0f, 0.5f, DefChopScatter, DefWindScatter);
            Assert.Less(breezy, still, "wind alone scatters the reflection (sharpness drops even with zero chop)");
        }

        [Test]
        public void Sharpness_ClampsAtZero_NeverGoesNegative()
        {
            // A full storm drives the agitation past 1; sharpness floors at 0 (a fully smeared reflection), no NaN.
            float storm = WaterReflection.ReflectionSharpness(1f, 1f, DefChopScatter, DefWindScatter);
            Assert.AreEqual(0f, storm, 1e-5f, "a full storm fully smears the reflection (sharpness clamps at 0)");
        }
    }
}
