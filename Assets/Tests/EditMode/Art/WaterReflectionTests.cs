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

        // ===== SKY CONTENT: moon direction, night gate, per-element strength ==============================

        [Test]
        public void MoonDirection_IsOppositeTheSun_AndUnitLength()
        {
            // The moon sits roughly opposite the sun, so its reflected ground direction is the negated sun dir.
            Vector2 sun = new Vector2(0.6f, 0.8f);          // an arbitrary sun direction (already ~unit)
            Vector2 moon = WaterReflection.MoonDirection(sun.x, sun.y);
            Assert.AreEqual(1f, moon.magnitude, 1e-4f, "moon direction is normalized (unit length)");
            // opposite: the dot with the sun direction is strongly negative (pointing the other way).
            Assert.Less(Vector2.Dot(moon, sun.normalized), -0.99f,
                "the moon reflection sits opposite the sun (negated, normalized direction)");
        }

        [Test]
        public void MoonDirection_FallsBackToNightArc_OnZeroSunDir()
        {
            // Cycle not running / unset sun dir => a fixed, believable night arc (+Y), never a NaN axis.
            Vector2 moon = WaterReflection.MoonDirection(0f, 0f);
            Assert.AreEqual(new Vector2(0f, 1f), moon, "zero sun dir falls back to the fixed +Y night arc");
            Assert.AreEqual(1f, moon.magnitude, 1e-5f, "the fallback is unit length (no NaN)");
        }

        [Test]
        public void NightFactor_IsZeroByDay_OneAtDeepNight_AndRampsAtDusk()
        {
            const float thr = 0.4f;    // _NightStart
            const float soft = 0.25f;  // _NightSoftness
            // Full daylight: tint luma ~1 => darkness ~0 => no night content.
            Assert.AreEqual(0f, WaterReflection.NightFactor(1f, thr, soft), 1e-5f,
                "by day (bright tint) the moon/stars do not read");
            // Deep night: tint luma ~0 => darkness ~1 => full night content.
            Assert.AreEqual(1f, WaterReflection.NightFactor(0f, thr, soft), 1e-5f,
                "at deep night (dark tint) the moon/stars read fully");
            // Dusk is between: a partial gate, and monotonic non-decreasing as the sky darkens.
            float prev = 0f;
            for (float luma = 1f; luma >= 0f; luma -= 0.05f)
            {
                float n = WaterReflection.NightFactor(luma, thr, soft);
                Assert.That(n, Is.InRange(0f, 1f), "the night factor stays a 0..1 gate");
                Assert.GreaterOrEqual(n + 1e-5f, prev, "the night factor only rises as the sky darkens");
                prev = n;
            }
        }

        [Test]
        public void SkyElement_CloudsAreAllDay_MoonAndStarsAreNightGated()
        {
            // The sea-state master (calm) and per-element strengths fixed; vary only day vs night.
            float seaCalm = WaterReflection.ReflectionStrength(0f, 0f, DefFadeChop, DefWindFade, DefMaster);
            float day = WaterReflection.NightFactor(1f, 0.4f, 0.25f);   // ~0
            float night = WaterReflection.NightFactor(0f, 0.4f, 0.25f); // ~1

            // Clouds (nightGated:false) read by day AND night — never gated out by daylight.
            float cloudsDay = WaterReflection.SkyElementStrength(seaCalm, 0.5f, day, nightGated: false);
            float cloudsNight = WaterReflection.SkyElementStrength(seaCalm, 0.5f, night, nightGated: false);
            Assert.Greater(cloudsDay, 0f, "clouds reflect during the day (all-day element)");
            Assert.AreEqual(cloudsDay, cloudsNight, 1e-5f, "clouds are not night-gated (read day and night the same)");

            // Moon (nightGated:true) is ~off by day and full at night.
            float moonDay = WaterReflection.SkyElementStrength(seaCalm, 0.5f, day, nightGated: true);
            float moonNight = WaterReflection.SkyElementStrength(seaCalm, 0.5f, night, nightGated: true);
            Assert.AreEqual(0f, moonDay, 1e-5f, "the moon does not reflect by day (night-gated)");
            Assert.Greater(moonNight, 0f, "the moon reflects at night");
        }

        [Test]
        public void SkyElement_InheritsTheSeaStateFade_GoneInAStorm()
        {
            // All sky content (clouds/moon/stars) inherits the master sea-state fade — strong on calm, gone in chop.
            float night = WaterReflection.NightFactor(0f, 0.4f, 0.25f);
            float seaCalm = WaterReflection.ReflectionStrength(0f, 0f, DefFadeChop, DefWindFade, DefMaster);
            float seaStorm = WaterReflection.ReflectionStrength(DefFadeChop, 0f, DefFadeChop, DefWindFade, DefMaster);

            float moonCalm = WaterReflection.SkyElementStrength(seaCalm, 0.8f, night, nightGated: true);
            float moonStorm = WaterReflection.SkyElementStrength(seaStorm, 0.8f, night, nightGated: true);
            Assert.Greater(moonCalm, 0f, "the moon glitter reads on CALM night water (the money shot)");
            Assert.AreEqual(0f, moonStorm, 1e-5f, "a storm does not mirror — the moon glitter is gone in chop");
        }

        // ===== SUN GLITTER PATH: the golden-hour gate (dawn/dusk column, gone at noon and at night) =======

        [Test]
        public void SunGlitterGate_IsZeroAtAndBelowTheHorizon()
        {
            // Below the horizon the sun casts nothing — the moon's glitter owns the night. AT the horizon
            // (elevation exactly 0, also the "cycle not running" unset value) the gate is 0 too, so a bare
            // art scene / editor preview shows no phantom sun glitter.
            foreach (float e in new[] { -1f, -0.5f, -0.05f, 0f })
                Assert.AreEqual(0f, WaterReflection.SunGlitterGate(e), 1e-6f,
                    $"no sun glitter at or below the horizon (elevation {e})");
        }

        [Test]
        public void SunGlitterGate_PeaksThroughTheGoldenHourWindow()
        {
            // The gate holds its full peak across the LOW-but-UP band (rise end .. fall start) — the long
            // warm glitter column of dawn and dusk.
            for (float e = WaterReflection.SunGlitterRiseEnd; e <= WaterReflection.SunGlitterFallStart; e += 0.03f)
                Assert.AreEqual(1f, WaterReflection.SunGlitterGate(e), 1e-5f,
                    $"the gate peaks through the golden-hour window (elevation {e})");
        }

        [Test]
        public void SunGlitterGate_IsGoneByHighSun()
        {
            // By the fall end (and all the way to noon) the column is gone — a high sun glints via the
            // specular layer, not a glitter path. So MIDDAY water is effectively unchanged by this feature.
            foreach (float e in new[] { WaterReflection.SunGlitterFallEnd, 0.7f, 1f })
                Assert.AreEqual(0f, WaterReflection.SunGlitterGate(e), 1e-6f,
                    $"no sun glitter at high sun (elevation {e})");
        }

        [Test]
        public void SunGlitterGate_RisesMonotonicallyAtDawn_AndFallsMonotonicallyTowardNoon()
        {
            // Dawn: the gate only rises as the sun climbs from the horizon to the window.
            float prev = WaterReflection.SunGlitterGate(0f);
            for (float e = 0.002f; e <= WaterReflection.SunGlitterRiseEnd + 1e-4f; e += 0.002f)
            {
                float g = WaterReflection.SunGlitterGate(e);
                Assert.GreaterOrEqual(g + 1e-5f, prev, "the gate only rises as the sun climbs out of the horizon");
                prev = g;
            }
            // Toward noon: the gate only falls as the sun climbs past the window.
            prev = WaterReflection.SunGlitterGate(WaterReflection.SunGlitterFallStart);
            for (float e = WaterReflection.SunGlitterFallStart; e <= WaterReflection.SunGlitterFallEnd + 1e-4f; e += 0.005f)
            {
                float g = WaterReflection.SunGlitterGate(e);
                Assert.LessOrEqual(g, prev + 1e-5f, "the gate only falls as the sun climbs toward noon");
                prev = g;
            }
        }

        [Test]
        public void SunGlitterGate_StaysAWeight_AcrossTheWholeElevationRange()
        {
            // The full −1..1 elevation sweep stays a clamped 0..1 weight — no negative or >1 excursions the
            // compensated post-grade add could amplify.
            for (float e = -1f; e <= 1f; e += 0.01f)
                Assert.That(WaterReflection.SunGlitterGate(e), Is.InRange(0f, 1f),
                    $"the sun glitter gate stays a 0..1 weight (elevation {e})");
        }

        [Test]
        public void SunGlitter_InheritsTheSeaStateFade_GoneInAStorm()
        {
            // Like every sky-content element the sun glitter multiplies the master sea-state fade in-shader:
            // strong on calm golden-hour water, gone in chop (a storm doesn't mirror a sun path either).
            float gate = WaterReflection.SunGlitterGate(0.1f);                 // golden hour, gate = 1
            float seaCalm = WaterReflection.ReflectionStrength(0f, 0f, DefFadeChop, DefWindFade, DefMaster);
            float seaStorm = WaterReflection.ReflectionStrength(DefFadeChop, 0f, DefFadeChop, DefWindFade, DefMaster);
            Assert.Greater(gate * seaCalm, 0f, "the sun glitter reads on calm golden-hour water");
            Assert.AreEqual(0f, gate * seaStorm, 1e-6f, "a storm does not mirror — the sun glitter dies in chop");
        }

        // ===== the MOONLIT night-cloud gate (owner playtest 2026-07-23 — the white-veil fix) =============

        [Test]
        public void MoonlitClouds_ReadFaintUnderAFullMoon_AndVanishOnAMoonlessNight()
        {
            const float vis = WaterReflection.DefaultCloudMoonlitVisibility;

            // Full moon high at deep night: the clouds' night share reads — but FAINT (the dial), never the
            // pre-fix full strength that veiled the dimmed sea white.
            float fullMoon = WaterReflection.MoonlitCloudVisibility(1f, 1f, 1f, vis);
            Assert.AreEqual(vis, fullMoon, 1e-5f, "a full high moon lights the night clouds at the faint dial");
            Assert.Less(fullMoon, 1f, "the moonlit night share is FAINT — full strength was the white veil");

            // New moon / moon below the horizon: no light source, no night clouds (the veil's kill switch).
            Assert.AreEqual(0f, WaterReflection.MoonlitCloudVisibility(1f, 0f, 1f, vis), 1e-6f,
                "a moon below the horizon lights nothing — the night clouds vanish");
            Assert.AreEqual(0f, WaterReflection.MoonlitCloudVisibility(1f, 1f, 0f, vis), 1e-6f,
                "a new moon lights nothing — the night clouds vanish");

            // Daylight: the night share is 0 by the night factor (the day share is untouched by this gate).
            Assert.AreEqual(0f, WaterReflection.MoonlitCloudVisibility(0f, 1f, 1f, vis), 1e-6f,
                "by day the night share is 0 — daylight clouds are the day share's business");
        }

        [Test]
        public void MoonlitClouds_VisibilityOneIsTheLegacyPassthrough()
        {
            // _CloudMoonlitVis = 1 with the no-MoonCycle fallbacks (presence = brightness = 1) restores the
            // pre-fix night share weight EXACTLY: nightFactor × 1 — the passthrough contract of the fix.
            foreach (float night in new[] { 0f, 0.3f, 0.7f, 1f })
                Assert.AreEqual(night, WaterReflection.MoonlitCloudVisibility(night, 1f, 1f, 1f), 1e-6f,
                    $"visibility 1 must be the exact pre-fix night-share weight (night={night})");
        }

        [Test]
        public void SkyElement_MasterZeroTurnsAllSkyContentOff()
        {
            // _ReflectionStrength = 0 => the whole reflection (sky colour AND sky content) is off (today's look).
            float seaOff = WaterReflection.ReflectionStrength(0f, 0f, DefFadeChop, DefWindFade, /*master*/0f);
            float night = WaterReflection.NightFactor(0f, 0.4f, 0.25f);
            Assert.AreEqual(0f, WaterReflection.SkyElementStrength(seaOff, 1f, night, nightGated: true), 1e-6f,
                "master 0 => no moon");
            Assert.AreEqual(0f, WaterReflection.SkyElementStrength(seaOff, 1f, night, nightGated: false), 1e-6f,
                "master 0 => no clouds");
        }
    }
}
