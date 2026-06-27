using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the FOAM DENSITY + WHITECAP LIFECYCLE pass (the dense-solid-core + form/peak/collapse
    /// refinement on top of #100/#101). The shader's evolving foam FIELD is GPU value-noise (not unit-testable
    /// headless), but the three shaping functions added in this pass are pure functions of the material uniforms
    /// and the swell-crest factor — exactly the part that turns the milky #101 look into a CONDITION-APPROPRIATE
    /// dense-core-plus-milky-edge with a natural wave lifecycle. They are mirrored here as faithful C# twins of the
    /// shader's <c>FoamDensity()</c> / <c>SolidCore()</c> / <c>WhitecapLifecycle()</c> so the mechanism is locked
    /// without opening Unity:
    ///
    ///   • RESTORE DENSITY — a SOLID-WHITE CORE (full opacity) where the field is WELL above threshold, with the
    ///     milky soft band kept ONLY near the threshold boundary (a dense heart + soft milky edges, not milky
    ///     everywhere). The core leverages the painted solid-white _FoamTex at the heart.
    ///   • CONDITION-DRIVEN DENSITY — sea-state (wind/_Roughness) raises density AND widens the solid zone, so
    ///     CALM reads sparse + milky and ROUGH reads dense + solid + widespread (the owner's "milky for some
    ///     conditions, dense for others" happens automatically with the weather).
    ///   • WAVE LIFECYCLE — foam FORMS as the swell crest builds, PEAKS into a dense solid whitecap near the crest
    ///     maximum (the breaking crest), then COLLAPSES into milky residual as the crest passes.
    ///
    /// These are NOT pushed to the material and NOT added to WaterSurface.cs — they are local mirrors for the
    /// determinism/feel guard only. Everything here is VISUAL-ONLY dressing: in the shader these drive only
    /// col.rgb/col.a, never depth/clip/_WaterLevel — they save nothing and feed no sim (P1 integrity, rule 5).
    /// (Reuses <see cref="WaterSurface.Smoothstep"/> so the twins share the exact smoothstep the shader uses.)
    /// </summary>
    public class FoamDensityLifecycleTests
    {
        // ===== C# twins of the shader's foam-density / lifecycle helpers ==================================
        // Kept byte-for-byte aligned with HiddenHarboursWater.shader's FoamDensity()/SolidCore()/
        // WhitecapLifecycle(). If the shader math changes, update these together (they are the headless
        // contract for the behaviour the owner is steering).

        /// <summary>Twin of the shader <c>FoamDensity()</c>: master density lifted by wind/roughness, 0..1.</summary>
        private static float FoamDensity(float density, float densityWind, float roughness)
        {
            return Mathf.Clamp01(density + roughness * densityWind);
        }

        /// <summary>
        /// Twin of the shader <c>SolidCore(field, thr, density)</c>: the dual-zone SOLID-CORE weight. 1 where the
        /// field is WELL above <paramref name="thr"/> (the dense heart), 0 near the threshold boundary (the milky
        /// band). Density slides the solid level DOWN toward the threshold so a rough sea turns more solid; the
        /// solid level is always kept above the threshold so the soft milky band never collapses.
        /// </summary>
        private static float SolidCore(float field, float thr, float density, float solidThreshold)
        {
            float d = Mathf.Clamp01(density);
            float solidLvl = Mathf.Lerp(Mathf.Clamp01(solidThreshold), thr + 0.02f, d);
            solidLvl = Mathf.Max(solidLvl, thr + 0.01f);
            return WaterSurface.Smoothstep(thr, solidLvl, field);
        }

        /// <summary>
        /// Twin of the shader <c>WhitecapLifecycle(crest, density)</c>: a 0..1 density scale that is BORN dense on
        /// the breaking crest (the form/peak band, sharpened by <paramref name="formSharpness"/> and scaled by
        /// peak density) and AGES into milky residual away from the crest (<c>crest^collapseRate</c>). The caller
        /// multiplies it into the solid-core lift, so the cap reads dense+solid on the crest and milky off it.
        /// </summary>
        private static float WhitecapLifecycle(
            float crest, float density, float formSharpness, float peakDensity, float collapseRate)
        {
            float c = Mathf.Clamp01(crest);
            float breakLo = Mathf.Lerp(0f, 0.9f, Mathf.Clamp01(formSharpness));
            float breakBand = WaterSurface.Smoothstep(breakLo, 1f, c);
            float newborn = breakBand * Mathf.Clamp01(peakDensity) * Mathf.Clamp01(density);
            float aged = Mathf.Pow(c, Mathf.Max(collapseRate, 0.05f));
            return Mathf.Clamp01(Mathf.Max(newborn, aged * Mathf.Clamp01(density)));
        }

        // Shader defaults (kept in sync with the Properties block / Water.mat) so the tests exercise the
        // shipped configuration, not an invented one.
        private const float DefThr = 0.55f;            // _FoamThreshold
        private const float DefSolid = 0.78f;          // _FoamSolidThreshold
        private const float DefDensity = 0.6f;         // _FoamDensity
        private const float DefDensityWind = 0.5f;     // _FoamDensityWind
        private const float DefFormSharp = 0.5f;       // _WhitecapFormSharpness
        private const float DefPeak = 0.95f;           // _WhitecapPeakDensity
        private const float DefCollapse = 1.5f;        // _WhitecapCollapseRate

        // ===== CONDITION-DRIVEN DENSITY (FoamDensity) =====================================================

        [Test]
        public void FoamDensity_RisesWithWind_CalmIsSparse_RoughIsDense()
        {
            float calm = FoamDensity(DefDensity, DefDensityWind, 0f);   // no wind
            float mid  = FoamDensity(DefDensity, DefDensityWind, 0.5f);
            float gale = FoamDensity(DefDensity, DefDensityWind, 1f);   // full wind

            Assert.AreEqual(DefDensity, calm, 1e-5f, "with no wind the density is just the master (the calm/milky floor)");
            Assert.Greater(mid, calm, "wind RAISES density (a building sea gets denser foam)");
            Assert.Greater(gale, mid, "monotonic in wind — rougher => denser");
            Assert.AreEqual(1f, gale, 1e-5f, "a full-wind gale saturates density at 1 (solid, widespread caps)");
        }

        [Test]
        public void FoamDensity_Saturates_AndZeroMasterZeroWindIsZero()
        {
            Assert.AreEqual(1f, FoamDensity(DefDensity, DefDensityWind, 4f), 1e-5f, "density clamps at 1 past a gale");
            Assert.AreEqual(0f, FoamDensity(0f, DefDensityWind, 0f), 1e-5f, "zero master + calm => zero density (fully milky, like before)");
            Assert.AreEqual(0f, FoamDensity(0f, 0f, 1f), 1e-5f, "with the wind coupling off, density stays at the (zero) master regardless of wind");
        }

        // ===== RESTORE DENSITY: the dual-zone SOLID CORE (SolidCore) ======================================

        [Test]
        public void SolidCore_IsZeroNearThreshold_AndOneWellAboveIt()
        {
            // Right at / just above the threshold => NO solid core (the milky soft band owns the boundary).
            Assert.AreEqual(0f, SolidCore(DefThr, DefThr, DefDensity, DefSolid), 1e-4f,
                "exactly at the threshold the core is 0 — the milky soft band, not solid white");
            // Well above the solid level => FULL solid core (the dense white heart the painted _FoamTex shows).
            Assert.AreEqual(1f, SolidCore(0.99f, DefThr, DefDensity, DefSolid), 1e-4f,
                "a field well above the solid level reads as a FULL solid-white core");
        }

        [Test]
        public void SolidCore_KeepsAMilkyBand_TheCoreNeverSwallowsTheBoundary()
        {
            // The headline of the pass: a DENSE heart + a SOFT milky edge — NOT milky-everywhere and NOT
            // solid-everywhere. So a field value just above the threshold (inside the soft band) must read as
            // PARTIAL core (a soft edge), never a hard 0/1, even at full density.
            float justAbove = DefThr + 0.015f;   // inside the guaranteed milky band (solidLvl >= thr+0.01)
            float coreCalm = SolidCore(justAbove, DefThr, 0f, DefSolid);
            float coreFull = SolidCore(justAbove, DefThr, 1f, DefSolid);
            Assert.That(coreCalm, Is.InRange(0f, 1f), "just above the threshold stays a fractional soft edge (calm)");
            Assert.Less(coreFull, 1f, "even at FULL density the soft band just above the threshold is not fully solid (a milky edge survives)");
            Assert.Greater(coreFull, 0f, "…but it does begin to lift toward solid (the dual zone, not all-milky)");
        }

        [Test]
        public void SolidCore_DensityWidensTheSolidZone_RoughTurnsMoreFieldSolid()
        {
            // CONDITION coupling on the CORE: at a fixed mid field value, a rougher sea (higher density) makes
            // MORE of the field read solid — the solid level slides down toward the threshold. So the same foam
            // shape is milky when calm and dense when rough (exactly the owner's ask). The field sits in the calm
            // soft band (between thr 0.55 and the calm solid level 0.78), so calm reads mostly milky.
            float field = 0.62f;   // above threshold, low in the calm soft band => milky when calm
            float calm = SolidCore(field, DefThr, 0f, DefSolid);
            float rough = SolidCore(field, DefThr, 1f, DefSolid);
            Assert.Less(calm, rough, "a field low in the calm soft band is MORE solid when the sea roughens (density widens the solid zone)");
            Assert.Less(calm, 0.5f, "when calm this mid field reads mostly milky (sparse solid)");
            Assert.Greater(rough, 0.9f, "when rough the same field reads essentially solid (dense, widespread)");
        }

        [Test]
        public void SolidCore_IsMonotonicInField_AndStaysAWeight()
        {
            // A rising field only ever adds solid coverage (the basis for a blob's core growing as the field
            // rises) and the result is always a clean 0..1 weight (no NaN from the guarded solid level).
            float prev = SolidCore(0f, DefThr, DefDensity, DefSolid);
            for (float f = 0.02f; f <= 1f; f += 0.02f)
            {
                float c = SolidCore(f, DefThr, DefDensity, DefSolid);
                Assert.GreaterOrEqual(c + 1e-5f, prev, "the solid core never decreases as the field rises");
                Assert.That(c, Is.InRange(0f, 1f), "the core stays a 0..1 weight");
                prev = c;
            }
        }

        // ===== WAVE LIFECYCLE: form -> peak -> collapse (WhitecapLifecycle) ===============================

        [Test]
        public void Lifecycle_PeaksOnTheBreakingCrest_CollapsesInTheTrough()
        {
            // The headline lifecycle property. On the breaking crest (crest ~ 1) the cap is BORN dense (a high
            // density scale); in the trough (crest ~ 0) it has COLLAPSED to ~nothing (milky residual only).
            float crestTop = WhitecapLifecycle(1f, /*density*/1f, DefFormSharp, DefPeak, DefCollapse);
            float trough   = WhitecapLifecycle(0f, /*density*/1f, DefFormSharp, DefPeak, DefCollapse);
            Assert.Greater(crestTop, 0.8f, "a newborn cap on the breaking crest is DENSE (near peak density)");
            Assert.AreEqual(0f, trough, 1e-5f, "in the trough the cap has fully collapsed (no solid density — milky residual only)");
            Assert.Greater(crestTop, trough, "the lifecycle concentrates dense foam on the crest, dissipating off it");
        }

        [Test]
        public void Lifecycle_Ages_DensityFallsOffAsTheCrestPasses()
        {
            // COLLAPSE: walking DOWN from the crest (1.0 -> 0.0) the density scale only ever falls (the cap ages
            // into milky residual as the crest passes — it never re-intensifies off-crest).
            float prev = WhitecapLifecycle(1f, 1f, DefFormSharp, DefPeak, DefCollapse);
            for (float crest = 0.95f; crest >= 0f; crest -= 0.05f)
            {
                float life = WhitecapLifecycle(crest, 1f, DefFormSharp, DefPeak, DefCollapse);
                Assert.LessOrEqual(life, prev + 1e-5f, "density only decreases as the crest drops (ages to residual, never re-peaks)");
                Assert.That(life, Is.InRange(0f, 1f), "the lifecycle scale stays a 0..1 weight");
                prev = life;
            }
        }

        [Test]
        public void Lifecycle_FormSharpness_NarrowsTheBreakingBand()
        {
            // _WhitecapFormSharpness controls how ABRUPTLY foam breaks at the crest. At a point just below the
            // crest top, a SHARPER setting (narrow break) gives LESS newborn density than a SOFT setting (the
            // break spreads down the crest face). Tested at full density with collapse pushed off so the break
            // band dominates the comparison.
            float belowTop = 0.92f;   // just under the crest top
            float soft  = WhitecapLifecycle(belowTop, 1f, /*sharp*/0.1f, DefPeak, /*collapse*/4f);
            float sharp = WhitecapLifecycle(belowTop, 1f, /*sharp*/0.9f, DefPeak, /*collapse*/4f);
            Assert.Greater(soft, sharp, "a SOFT form spreads the break further down the crest than a SHARP (narrow) break");
        }

        [Test]
        public void Lifecycle_CollapseRate_HigherRateLeavesMoreMilkyResidualOffCrest()
        {
            // _WhitecapCollapseRate controls how fast the aged residual falls off. At a mid crest value, a HIGHER
            // collapse rate => LESS residual density (faster collapse to milky), a LOWER rate => MORE residual
            // (foam lingers as it dissipates). Form band pushed off (sharp + below its band) so AGING dominates.
            float midCrest = 0.5f;
            float slow = WhitecapLifecycle(midCrest, 1f, /*sharp*/1f, DefPeak, /*collapse*/0.5f);
            float fast = WhitecapLifecycle(midCrest, 1f, /*sharp*/1f, DefPeak, /*collapse*/3f);
            Assert.Greater(slow, fast, "a SLOWER collapse leaves MORE foam residual off the crest; a faster one ages to milky sooner");
        }

        [Test]
        public void Lifecycle_DensityGatesTheWholeLook_CalmSeaHasNoSolidLift()
        {
            // CONDITION coupling on the LIFECYCLE: with density 0 (a calm sea) there is NO solid lift anywhere —
            // even on the crest the cap stays milky (the dissipating end), which is exactly #101's calm look. As
            // density rises the crest gains its dense solid peak. So calm => milky everywhere, rough => dense crests.
            float crestCalm = WhitecapLifecycle(1f, /*density*/0f, DefFormSharp, DefPeak, DefCollapse);
            float crestRough = WhitecapLifecycle(1f, /*density*/1f, DefFormSharp, DefPeak, DefCollapse);
            Assert.AreEqual(0f, crestCalm, 1e-5f, "a calm sea (density 0) has NO solid lift — milky everywhere, the #101 end");
            Assert.Greater(crestRough, crestCalm, "as the sea roughens the crest gains its dense solid whitecap (the restored density)");
        }

        // ===== INTEGRATION: the dual-zone + lifecycle compose into the owner's two ends ===================

        [Test]
        public void DualZoneAndLifecycle_Compose_CalmMilky_RoughDenseOnCrests()
        {
            // The two ends the owner asked for, composed exactly as the shader does for the open-water cap:
            //   final solid lift = SolidCore(capField, capThr, density) * WhitecapLifecycle(crest, density) * peak
            // (the shader then maxes this with the milky residual and multiplies by the cap mask). Here we check
            // the SOLID-LIFT term directly: on a rough breaking crest it is dense; on a calm sea it is ~nothing.
            float capField = 0.9f;   // a strong cap field maximum
            float crest = 1f;        // on the breaking crest

            float densCalm = FoamDensity(density: 0f, densityWind: DefDensityWind, roughness: 0f);   // fully calm
            float densRough = FoamDensity(DefDensity, DefDensityWind, 1f);                            // gale

            float solidCalm = SolidCore(capField, DefThr, densCalm, DefSolid)
                            * WhitecapLifecycle(crest, densCalm, DefFormSharp, DefPeak, DefCollapse) * DefPeak;
            float solidRough = SolidCore(capField, DefThr, densRough, DefSolid)
                             * WhitecapLifecycle(crest, densRough, DefFormSharp, DefPeak, DefCollapse) * DefPeak;

            Assert.AreEqual(0f, solidCalm, 1e-4f, "calm sea: no solid-white lift (the foam reads milky, the #101 dissipating look kept)");
            Assert.Greater(solidRough, 0.7f, "rough breaking crest: a DENSE solid-white whitecap (the restored density)");
        }
    }
}
