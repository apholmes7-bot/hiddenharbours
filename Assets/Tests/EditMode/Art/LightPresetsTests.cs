using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Determinism + correctness guard for the PRECONFIGURED light-source PRESETS (ADR 0016, CLAUDE.md rules 5 &amp;
    /// 6). These run headless — no scene, no GPU — and pin the fixed, tunable look of each placed light kind
    /// (window glow / lamppost / worklight): every placed preset is a soft RADIAL warm-to-cool pool, with sane
    /// bounded intensity/range/softness/flicker, and <see cref="LightPresets.For"/> is a PURE function (same kind
    /// ⇒ same config, always). The night-GATE itself is the shared additive-light machinery pinned in
    /// <see cref="LightMathTests"/> (every light gates off the same published tint), so a preset only changes the
    /// shape/colour/size/flicker — never the gate — which is exactly what these assert. Mirrors
    /// <see cref="LightMathTests"/>'s style.
    /// </summary>
    public class LightPresetsTests
    {
        private const float Eps = 1e-4f;

        // ---- purity ---------------------------------------------------------------------------------------

        [Test]
        public void For_IsPure_SameKindSameConfig()
        {
            foreach (LightPresets.Kind kind in System.Enum.GetValues(typeof(LightPresets.Kind)))
            {
                var a = LightPresets.For(kind);
                var b = LightPresets.For(kind);
                Assert.AreEqual(a.Shape, b.Shape, $"{kind} shape not stable");
                Assert.AreEqual(a.Intensity, b.Intensity, Eps, $"{kind} intensity not stable");
                Assert.AreEqual(a.Range, b.Range, Eps, $"{kind} range not stable");
                Assert.AreEqual(a.Color, b.Color, $"{kind} colour not stable");
            }
        }

        // ---- every placed preset is a soft, sane RADIAL warm/cool pool -------------------------------------

        [Test]
        public void EveryPreset_IsRadial_WithSaneBoundedTunables()
        {
            foreach (LightPresets.Kind kind in System.Enum.GetValues(typeof(LightPresets.Kind)))
            {
                var c = LightPresets.For(kind);
                Assert.AreEqual(SceneLight.LightShape.Radial, c.Shape, $"{kind} should be a radial pool, not a cone");
                Assert.Greater(c.Intensity, 0f, $"{kind} must actually emit light");
                Assert.LessOrEqual(c.Intensity, 3f, $"{kind} intensity should stay in a sane band");
                // The REACH is the pool the lamp lights — the number that has to be room-sized.
                Assert.Greater(LightPresets.ReachMetres(kind), 1f, $"{kind} should light beyond its own footprint");
                Assert.LessOrEqual(LightPresets.ReachMetres(kind), 12f, $"{kind} reach should stay in a sane band");
                // The BLOOM is how big the SOURCE looks, and it can never exceed what the source lights.
                Assert.Greater(c.Range, 0f, $"{kind} must have some visible source");
                Assert.LessOrEqual(c.Range, LightPresets.ReachMetres(kind),
                    $"{kind} cannot glow bigger than the ground it lights");
                Assert.GreaterOrEqual(c.EdgeSoftness, 0.5f, $"{kind} placed glow should be soft, not a hard disc");
                Assert.LessOrEqual(c.EdgeSoftness, 1f);
                Assert.GreaterOrEqual(c.FlickerAmount, 0f);
                Assert.LessOrEqual(c.FlickerAmount, 0.2f, $"{kind} flicker should be subtle, not strobing");
            }
        }

        [Test]
        public void WindowAndLamp_AreWarm_WorklightIsCoolerAndBrighter()
        {
            var window = LightPresets.For(LightPresets.Kind.WindowGlow);
            var lamp   = LightPresets.For(LightPresets.Kind.Lightpost);
            var work   = LightPresets.For(LightPresets.Kind.Worklight);

            // Warm = red channel dominant over blue (an amber interior / sodium lamp).
            Assert.Greater(window.Color.r, window.Color.b + 0.2f, "window glow should read warm amber");
            Assert.Greater(lamp.Color.r,   lamp.Color.b + 0.1f,   "lamp pool should read warm");

            // The worklight is cooler (blue much closer to red) AND the brightest/furthest (a flood work lamp).
            Assert.Greater(work.Color.b, lamp.Color.b, "worklight should be cooler (less warm) than the lamp");
            Assert.Greater(work.Intensity, window.Intensity, "worklight should be brighter than a window spill");
            Assert.Greater(LightPresets.ReachMetres(LightPresets.Kind.Worklight),
                           LightPresets.ReachMetres(LightPresets.Kind.WindowGlow),
                "worklight should LIGHT further than a window spill — on the reach, which is the number that " +
                "means 'how far does this lamp throw'. It was asserted on Range until 2026-09-04, when Range " +
                "became the size of the fitting: a work lamp's fitting is smaller than a lit window, and " +
                "always was.");
        }

        [Test]
        public void WindowGlow_IsSofterAndDimmerThanLamppost()
        {
            var window = LightPresets.For(LightPresets.Kind.WindowGlow);
            var lamp   = LightPresets.For(LightPresets.Kind.Lightpost);

            // A window is a gentle spill; a street lamp is a stronger, wider pool.
            Assert.Less(window.Intensity, lamp.Intensity, "a window spill should be dimmer than a lamp pool");
            Assert.Less(LightPresets.ReachMetres(LightPresets.Kind.WindowGlow),
                        LightPresets.ReachMetres(LightPresets.Kind.Lightpost),
                "a window spill should LIGHT less far than a lamp pool");
            Assert.GreaterOrEqual(window.EdgeSoftness, lamp.EdgeSoftness, "a window spill should be at least as soft");

            // ⚠ And on the BLOOM the order is the other way round, deliberately. WindowGlow is the one
            // land light the owner has seen and likes, so this ruling left it alone: it is still drawn at
            // its whole 3.4 m pool while a lamp post is drawn at its 0.40 m lantern. Pinned, so that the
            // asymmetry is a decision on the record rather than something someone "tidies up".
            Assert.Greater(window.Range, lamp.Range,
                "WindowGlow is deliberately still drawn as a pool — the owner's one seen light, left alone " +
                "by the 2026-09-04 fitting ruling");
        }

        [Test]
        public void Worklight_IsSteady_WindowFlickers()
        {
            // The worklight is electric work light — dead steady (no flicker). The window has a living hearth/lamp
            // within, so a tiny deterministic flicker; the lamppost barely hums.
            Assert.AreEqual(0f, LightPresets.For(LightPresets.Kind.Worklight).FlickerAmount, Eps,
                "a work lamp should not flicker");
            Assert.Greater(LightPresets.For(LightPresets.Kind.WindowGlow).FlickerAmount, 0f,
                "a window glow should have a subtle living flicker");
        }

        // ---- Apply stamps the config onto a SceneLight (the ONE mapping the component + menu share) ---------

        [Test]
        public void Apply_StampsTheConfigOntoTheLight()
        {
            var go = new GameObject("ApplyTest");
            try
            {
                var light = go.AddComponent<SceneLight>();
                LightPresets.Apply(light, LightPresets.Kind.Lightpost);
                var c = LightPresets.For(LightPresets.Kind.Lightpost);

                Assert.AreEqual(c.Shape, light.Shape, "shape not applied");
                Assert.AreEqual(c.Color, light.Color, "colour not applied");
                Assert.AreEqual(c.Intensity, light.Intensity, Eps, "intensity not applied");
                Assert.AreEqual(c.Range, light.Range, Eps, "range not applied");
                Assert.AreEqual(c.EdgeSoftness, light.EdgeSoftness, Eps, "edge softness not applied");
                Assert.AreEqual(c.FlickerAmount, light.FlickerAmount, Eps, "flicker not applied");
                Assert.AreEqual(c.OriginOffset, light.OriginOffset, "origin offset not applied");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Apply_IsNullSafe()
        {
            Assert.DoesNotThrow(() => LightPresets.Apply(null, LightPresets.Kind.WindowGlow),
                "applying to a null light must be a harmless no-op");
        }

        /// <summary>
        /// <b>The tall poles' preset reaches further than the posts'.</b> A lamp lights a circle of roughly
        /// twice its head height, and the four pieces <c>LampPosts</c> places split cleanly on that:
        /// <c>lanternPost</c> 2.46 m and <c>streetLamp</c> 4.48 m under <see cref="LightPresets.Kind.Lightpost"/>,
        /// <c>yardLight</c> 7.26 m and <c>floodMast</c> 7.8 m under <see cref="LightPresets.Kind.Floodlight"/>.
        /// A Floodlight that did not out-reach a Lightpost would leave a 7.8 m mast lighting a smaller
        /// circle than the lamp on the road below it.
        /// </summary>
        [Test]
        public void Floodlight_ReachesFurtherAndIsCoolerThanALampPost()
        {
            var lamp = LightPresets.For(LightPresets.Kind.Lightpost);
            var flood = LightPresets.For(LightPresets.Kind.Floodlight);

            Assert.Greater(LightPresets.ReachMetres(LightPresets.Kind.Floodlight),
                           LightPresets.ReachMetres(LightPresets.Kind.Lightpost) * 1.5f,
                "a 7-8 m mast must LIGHT a materially bigger circle than a 2-4 m post");
            Assert.Greater(flood.Color.b, lamp.Color.b,
                "electric flood is cooler (bluer) than a warm sodium lamp post");
            Assert.AreEqual(0f, flood.FlickerAmount, 1e-6f, "a flood mast is rock steady");
        }

        /// <summary>
        /// <b>Ginny's window does not move; every other land preset is now drawn at its FITTING.</b>
        /// <see cref="LightPresets.Kind.WindowGlow"/> lights Aunt Ginny's cottage, the owner has seen it and
        /// likes it, and the 2026-09-04 ruling left it alone — it is still the one land light drawn at its
        /// whole pool. The other three came down to the size of the thing that actually glows, with the
        /// intensity lifted to carry what the radius gave up.
        /// </summary>
        [Test]
        public void ThePresetsThatShipped_AreUnchanged_AndTheLampPostIsPinnedAtItsMeasuredValues()
        {
            var window = LightPresets.For(LightPresets.Kind.WindowGlow);
            Assert.AreEqual(0.95f, window.Intensity, 1e-6f, "Ginny's cottage does not move");
            Assert.AreEqual(3.4f, window.Range, 1e-6f, "and neither does the one bloom the owner likes");

            var work = LightPresets.For(LightPresets.Kind.Worklight);
            Assert.AreEqual(1.7f, work.Intensity, 1e-6f);
            Assert.AreEqual(0.50f, work.Range, 1e-6f,
                "a bulkhead work lamp — the one fitting here with no art to measure, reasoned between the " +
                "street lantern's 0.40 and the cobra head's 0.58");

            var lamp = LightPresets.For(LightPresets.Kind.Lightpost);
            Assert.AreEqual(1.3f, lamp.Intensity, 1e-6f,
                "lifted from 1.0 so the shrunken fitting still reads, and still above WindowGlow's 0.95");
            Assert.AreEqual(0.40f, lamp.Range, 1e-6f, "streetLamp's own lantern lens (utilityIsoRig.js:361)");

            var flood = LightPresets.For(LightPresets.Kind.Floodlight);
            Assert.AreEqual(1.45f, flood.Intensity, 1e-6f);
            Assert.AreEqual(0.58f, flood.Range, 1e-6f, "yardLight's cobra-head lens (utilityIsoRig.js:339)");
        }

        // ---- the bloom / reach split (the owner's ruling, 2026-09-04) --------------------------------------

        /// <summary>
        /// <b>The REACH is the number that shipped as <c>Range</c>, to the decimal.</b> This is the whole
        /// safety of the split: the builders site their lamps by the reach, so if a single one of these four
        /// had been "tidied" while the bloom was being shrunk, lamp posts would silently move on the owner's
        /// next Build and the plates that tuned them would be describing a wharf that no longer exists.
        /// </summary>
        [Test]
        public void TheReach_IsTheNumberThatShippedAsRange()
        {
            Assert.AreEqual(3.4f, LightPresets.ReachMetres(LightPresets.Kind.WindowGlow), 1e-6f);
            Assert.AreEqual(3.6f, LightPresets.ReachMetres(LightPresets.Kind.Lightpost), 1e-6f,
                "the 02:00 plate retune of 4.6 -> 3.6 m, carried through the split untouched");
            Assert.AreEqual(5.2f, LightPresets.ReachMetres(LightPresets.Kind.Worklight), 1e-6f);
            Assert.AreEqual(7f, LightPresets.ReachMetres(LightPresets.Kind.Floodlight), 1e-6f,
                "7 m, not the 9.5 m it shipped at for one commit");
        }

        /// <summary>
        /// <b>The guard the ruling asks for, as a RATIO rather than an absolute.</b> The owner's complaint
        /// was that a dock light is <i>"just a round glow"</i> — a bloom drawn at the size of the pool. An
        /// absolute ceiling would rot the moment somebody retunes a reach; a ratio says the thing that is
        /// actually true: what glows is a fitting on a lamp, and a fitting is a small fraction of the ground
        /// its lamp lights. A quarter is generous — the three shrunken presets sit at 0.11, 0.10 and 0.08 —
        /// and it is deliberately loose, because this is a floor under a regression, not a tuning knob.
        ///
        /// <para><see cref="LightPresets.Kind.WindowGlow"/> is the named exception and the reason the sweep
        /// is not over every kind: it is exempt by the owner's own preference, and a test that quietly
        /// included it would be a test that forced a change he refused.</para>
        /// </summary>
        [Test]
        public void EveryLandLampPreset_BloomsAtItsFitting_NotAtItsPool()
        {
            var shrunk = new[]
            {
                LightPresets.Kind.Lightpost,
                LightPresets.Kind.Worklight,
                LightPresets.Kind.Floodlight,
            };

            foreach (LightPresets.Kind kind in shrunk)
            {
                float bloom = LightPresets.For(kind).Range;
                float reach = LightPresets.ReachMetres(kind);
                Assert.Less(bloom / reach, 0.25f,
                    $"{kind} blooms at {bloom:0.00} m over a {reach:0.0} m pool ({bloom / reach:0.000} of it) " +
                    "— a lamp drawn at anything approaching its own pool is the flat disc the owner refused");
                Assert.LessOrEqual(bloom, LightPresets.MaxBloomRadiusMetres,
                    $"{kind} bloom is past the fitting backstop");
                Assert.GreaterOrEqual(bloom, LightPresets.MinBloomRadiusMetres,
                    $"{kind} bloom is below the visible floor — that is a stray pixel, not a lamp");
            }
        }

        /// <summary>
        /// <b>The bloom is the fitting's own width, clamped only at the ends.</b> Pinned because the ratio
        /// is the design: half a fitting-width of halo all round, which is what a bright piece of glass
        /// does to the dark next to it — and the same ratio the fleet's lamps took under the same ruling a
        /// day earlier.
        /// </summary>
        [Test]
        public void BloomForFitting_IsTheWidthItself_AndClampsAtBothEnds()
        {
            Assert.AreEqual(0.40f, LightPresets.BloomForFitting(0.40f), 1e-6f, "the width itself, untouched");
            Assert.AreEqual(1.49f, LightPresets.BloomForFitting(1.49f), 1e-6f, "floodMast's array, untouched");

            Assert.AreEqual(LightPresets.MinBloomRadiusMetres, LightPresets.BloomForFitting(0.01f), 1e-6f,
                "a pilot lamp still has to be visible at the game's framing");
            Assert.AreEqual(LightPresets.MaxBloomRadiusMetres, LightPresets.BloomForFitting(40f), 1e-6f,
                "hand it a building and it gives back a lamp, not the disc this ruling retired");
        }

        /// <summary>
        /// <b>Two placements of one preset glow at their own sizes.</b> A wharf lantern is a smaller lamp
        /// than a road lamp; before this they were the same 3.6 m disc. Everything except the bloom stays
        /// the preset's verbatim, which is what keeps "a lamp post looks like a lamp post" true.
        /// </summary>
        [Test]
        public void ApplyFitting_SizesTheBloomToThePiece_AndChangesNothingElse()
        {
            var go = new GameObject("ApplyFittingTest");
            try
            {
                var light = go.AddComponent<SceneLight>();
                var c = LightPresets.For(LightPresets.Kind.Lightpost);

                LightPresets.ApplyFitting(light, LightPresets.Kind.Lightpost, 0.14f);
                Assert.AreEqual(0.14f, light.Range, Eps, "the wharf lantern's own glazed box");
                Assert.AreEqual(c.Intensity, light.Intensity, Eps, "the LOOK is still the preset's");
                Assert.AreEqual(c.Color, light.Color);
                Assert.AreEqual(c.EdgeSoftness, light.EdgeSoftness, Eps);
                Assert.AreEqual(c.FlickerAmount, light.FlickerAmount, Eps);
                Assert.AreEqual(c.OriginOffset, light.OriginOffset);

                LightPresets.ApplyFitting(light, LightPresets.Kind.Lightpost, 0.40f);
                Assert.AreEqual(0.40f, light.Range, Eps, "the road lamp's own lens — same preset, bigger lamp");

                // A caller that cannot measure its fitting gets the archetype rather than nothing.
                LightPresets.ApplyFitting(light, LightPresets.Kind.Lightpost, 0f);
                Assert.AreEqual(c.Range, light.Range, Eps, "zero width falls back to the preset's own bloom");

                Assert.DoesNotThrow(() => LightPresets.ApplyFitting(null, LightPresets.Kind.Lightpost, 0.4f),
                    "applying to a null light must be a harmless no-op");
            }
            finally { Object.DestroyImmediate(go); }
        }

    }
}
