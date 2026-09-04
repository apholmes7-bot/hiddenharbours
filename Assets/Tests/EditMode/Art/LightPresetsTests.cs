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
                Assert.Greater(c.Range, 1f, $"{kind} should reach beyond its own footprint");
                Assert.LessOrEqual(c.Range, 12f, $"{kind} range should stay in a sane band");
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
            Assert.Greater(work.Range, window.Range, "worklight should reach further than a window spill");
        }

        [Test]
        public void WindowGlow_IsSofterAndDimmerThanLamppost()
        {
            var window = LightPresets.For(LightPresets.Kind.WindowGlow);
            var lamp   = LightPresets.For(LightPresets.Kind.Lightpost);

            // A window is a gentle spill; a street lamp is a stronger, wider pool.
            Assert.Less(window.Intensity, lamp.Intensity, "a window spill should be dimmer than a lamp pool");
            Assert.Less(window.Range, lamp.Range, "a window spill should reach less far than a lamp pool");
            Assert.GreaterOrEqual(window.EdgeSoftness, lamp.EdgeSoftness, "a window spill should be at least as soft");
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

            Assert.Greater(flood.Range, lamp.Range * 1.5f,
                "a 7-8 m mast must throw a materially bigger pool than a 2-4 m post");
            Assert.Greater(flood.Color.b, lamp.Color.b,
                "electric flood is cooler (bluer) than a warm sodium lamp post");
            Assert.AreEqual(0f, flood.FlickerAmount, 1e-6f, "a flood mast is rock steady");
        }

        /// <summary>
        /// The three shipped presets are UNMOVED by the Floodlight's arrival. Adding a fourth kind is a
        /// cheap change; quietly retuning a lamp the owner has already looked at is not, and Ginny's
        /// cottage is lit by <see cref="LightPresets.Kind.WindowGlow"/>.
        /// </summary>
        [Test]
        public void TheThreeOriginalPresets_AreUnchanged()
        {
            var window = LightPresets.For(LightPresets.Kind.WindowGlow);
            Assert.AreEqual(0.95f, window.Intensity, 1e-6f);
            Assert.AreEqual(3.4f, window.Range, 1e-6f);

            var lamp = LightPresets.For(LightPresets.Kind.Lightpost);
            Assert.AreEqual(1.15f, lamp.Intensity, 1e-6f);
            Assert.AreEqual(4.6f, lamp.Range, 1e-6f);

            var work = LightPresets.For(LightPresets.Kind.Worklight);
            Assert.AreEqual(1.35f, work.Intensity, 1e-6f);
            Assert.AreEqual(5.2f, work.Range, 1e-6f);
        }

    }
}
