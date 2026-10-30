using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Gains 2 (ADR 0027 #7 amendment): the water coloured by place and by rain. <see cref="WaterType"/> is the
    /// law and the C# twin of the shader's water-type terms, <see cref="WaterTypeDef"/> holds a type as data,
    /// and <see cref="WaterSurface"/> carries a region's type and pushes it on the slow tick. Pinned:
    /// <list type="bullet">
    /// <item>the default type, and a surface with no type, draw today's water bit for bit;</item>
    /// <item>stain eats blue first, algae spares green, silt dims every channel;</item>
    /// <item>silt = dry silt + response x the rain averaged over the window, on WaveMemory's grid: it rises
    /// after rain, clears over its window, and is the dry silt in dry weather;</item>
    /// <item>a region's type fades to nothing at its water's edge, and a plume feathers into it;</item>
    /// <item>the body tints toward the type's colour by its stain and silt, posterized;</item>
    /// <item>the slow tick allocates nothing, and the shader carries the twin.</item>
    /// </list>
    /// </summary>
    public class WaterTypeTests
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const double Day = GameConfig.DefaultSecondsPerDay;

        // ===== today's water ==========================================================================

        [Test]
        public void WaterType_Default_IsTodaysSigma_BitForBit()
        {
            WaterTypeValues type = WaterTypeValues.Default;
            Assert.IsTrue(type.IsValid(), "the default type is outside its own ranges");
            // Every mood turbidity the materials ship (Water.mat and the eight presets) and the dial's floor,
            // against the default ratio and a second one: the type's term is + 0 under any mood.
            float[] turbidities = { 0f, 0.12f, 0.25f, 0.3f, 0.5f, 0.6f, 1.2f, 1.6f, 3f };
            Vector3[] ratios = { WaterAbsorption.DefaultRatio, new Vector3(0.9f, 0.3f, 0.12f) };
            foreach (float rain in new[] { 0f, 0.5f, 1f })
            {
                float silt = WaterType.Silt(type, rain);
                SameBits(0f, silt, $"the default silt in rain {rain}");
                WaterType.Uniforms(type, silt, out Vector4 sigma, out Vector4 body);
                AllZeroBits(sigma, "the default type's sigma");
                AllZeroBits(body, "the default type's body");
                var typeSigma = new Vector3(sigma.x, sigma.y, sigma.z);
                foreach (Vector3 ratio in ratios)
                    foreach (float turbidity in turbidities)
                    {
                        Vector3 today = WaterAbsorption.Sigma(turbidity, ratio);
                        Vector3 typed = WaterAbsorption.Sigma(turbidity, ratio, typeSigma);
                        SameBits(today, typed, $"sigma at turbidity {turbidity}, ratio {ratio}");
                        Assert.AreEqual(WaterAbsorption.IsActive(today), WaterAbsorption.IsActive(typed));
                    }
            }
            // The body step passes today's colour straight through, at any depth and banding.
            var col = new Vector3(0.11f, 0.37f, 0.52f);
            foreach (float depth in new[] { 0f, 1f, 12f })
                SameBits(col, WaterType.ApplyBody(col, Vector4.zero, Vector4.zero, depth, 6f), $"the body at {depth} m");
        }

        /// <summary>Acceptance 1: a region with no type draws exactly as today.</summary>
        [Test]
        public void WaterType_NoType_DrawsExactlyAsToday()
        {
            // The law: no type and no plume put zeros in every uniform, and zeros change nothing.
            var empty = new Vector4[WaterType.MaxPlumes];
            WaterType.At(new Vector2(3f, -7f), Vector4.zero, Vector4.zero, new Vector4(-50f, -50f, 50f, 50f), 32f,
                         empty, empty, empty, 0, out Vector4 sigmaAt, out Vector4 bodyAt);
            AllZeroBits(sigmaAt, "sigma with no type");
            AllZeroBits(bodyAt, "body with no type");
            Vector3 today = WaterAbsorption.Sigma(0.25f, WaterAbsorption.DefaultRatio);
            SameBits(today, WaterAbsorption.Sigma(0.25f, WaterAbsorption.DefaultRatio,
                new Vector3(sigmaAt.x, sigmaAt.y, sigmaAt.z)), "sigma with no type");
            var col = new Vector3(0.2f, 0.4f, 0.6f);
            SameBits(col, WaterType.ApplyBody(col, sigmaAt, bodyAt, 8f, 6f), "the body with no type");

            // The surface: with no type and no plume it writes no water-type uniform at all, so the shader's
            // globals stay unset (zeros) and today's frame is drawn.
            var go = new GameObject("WaterTypeTests.NoType", typeof(MeshRenderer));
            go.SetActive(false);
            try
            {
                var surface = go.AddComponent<WaterSurface>();
                var block = new MaterialPropertyBlock();
                Call(surface, "SampleWaterType", 1234.5, null);
                Call(surface, "PushWaterType", block);
                Assert.IsTrue(block.isEmpty, "a surface with no water type wrote a water-type uniform");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WaterSurface_PushesItsType_AndZerosOnceWhenItGoes()
        {
            var go = new GameObject("WaterTypeTests.Typed", typeof(MeshRenderer));
            go.SetActive(false);
            var def = ScriptableObject.CreateInstance<WaterTypeDef>();
            try
            {
                WaterTypeValues creek = Creek();
                def.Id = "water.test_creek";
                def.Water = creek;
                var surface = go.AddComponent<WaterSurface>();
                Set(surface, "_renderer", go.GetComponent<MeshRenderer>());
                Set(surface, "_waterType", def);

                // No environment: the rain is unbound and the type keeps its dry silt.
                WaterType.Uniforms(creek, WaterType.Silt(creek, 0f), out Vector4 sigma, out Vector4 body);
                Assert.Greater(body.w, 0f, "precondition: the test type tints");
                var block = new MaterialPropertyBlock();
                Call(surface, "SampleWaterType", 600.0, null);
                Call(surface, "PushWaterType", block);
                Assert.AreEqual(sigma, block.GetVector("_WaterTypeSigma"));
                Assert.AreEqual(body, block.GetVector("_WaterTypeBody"));
                Assert.AreEqual(0f, block.GetFloat("_WaterPlumeCount"));

                // The type goes: zeros go out once, then the surface writes nothing more.
                Set(surface, "_waterType", null);
                Call(surface, "SampleWaterType", 601.0, null);
                Call(surface, "PushWaterType", block);
                Assert.AreEqual(Vector4.zero, block.GetVector("_WaterTypeSigma"));
                Assert.AreEqual(Vector4.zero, block.GetVector("_WaterTypeBody"));
                var fresh = new MaterialPropertyBlock();
                Call(surface, "SampleWaterType", 602.0, null);
                Call(surface, "PushWaterType", fresh);
                Assert.IsTrue(fresh.isEmpty, "with its type gone the surface still writes the type's uniforms");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(def);
            }
        }

        // ===== the constituents =======================================================================

        [Test]
        public void WaterType_Constituents_ShapeEachChannel()
        {
            WaterTypeValues none = WaterTypeValues.Default;
            WaterTypeValues stainOnly = none;
            stainOnly.Stain = 1f;
            WaterTypeValues algaeOnly = none;
            algaeOnly.Algae = 1f;
            Vector3 stain = WaterType.TypeSigma(stainOnly, 0f);
            Vector3 algae = WaterType.TypeSigma(algaeOnly, 0f);
            Vector3 silt = WaterType.TypeSigma(none, 1f);

            Assert.Greater(stain.z, stain.y, "stain eats blue first");
            Assert.Greater(stain.y, stain.x, "stain eats green before red");
            Assert.Less(algae.y, algae.x, "algae spares green (against red)");
            Assert.Less(algae.y, algae.z, "algae spares green (against blue)");
            Assert.Greater(Mathf.Min(silt.x, Mathf.Min(silt.y, silt.z)), 0f, "silt dims every channel");
            Assert.Greater(Evenness(silt), Evenness(stain), "silt dims more evenly than stain");
            Assert.Greater(Evenness(silt), Evenness(algae), "silt dims more evenly than algae");

            // The constituents add, each in proportion to its own amount.
            WaterTypeValues mixed = none;
            mixed.Stain = 0.3f;
            mixed.Algae = 0.2f;
            Vector3 sum = WaterType.TypeSigma(mixed, 0.4f);
            Vector3 parts = stain * 0.3f + algae * 0.2f + silt * 0.4f;
            for (int c = 0; c < 3; c++) Assert.AreEqual(parts[c], sum[c], 1e-6f, $"channel {c}");

            // The type adds to the mood's sigma and never takes from it.
            Vector3 mood = WaterAbsorption.Sigma(0.6f, WaterAbsorption.DefaultRatio);
            Vector3 total = WaterAbsorption.Sigma(0.6f, WaterAbsorption.DefaultRatio, sum);
            for (int c = 0; c < 3; c++) Assert.AreEqual(mood[c] + sum[c], total[c], 1e-6f, $"channel {c}");
            SameBits(mood, WaterAbsorption.Sigma(0.6f, WaterAbsorption.DefaultRatio, -sum), "a negative type sigma");

            // Seen through a stain alone, the bottom goes amber: blue is gone first.
            Vector3 seen = WaterAbsorption.Transmission(
                WaterAbsorption.Sigma(0f, WaterAbsorption.DefaultRatio, stain), 1f);
            Assert.Less(seen.z, seen.y, "through a stain blue goes before green");
            Assert.Less(seen.y, seen.x, "through a stain green goes before red");
        }

        [Test]
        public void WaterTypeValues_EveryField_NamesItsDefaultAndRange()
        {
            foreach (FieldInfo field in typeof(WaterTypeValues).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var tip = field.GetCustomAttribute<TooltipAttribute>();
                Assert.IsNotNull(tip, $"{field.Name} has no tooltip");
                StringAssert.Contains("Default", tip.tooltip, $"{field.Name} names no default");
                bool ranged = field.GetCustomAttribute<UnityEngine.RangeAttribute>() != null || tip.tooltip.Contains("0 to 1");
                Assert.IsTrue(ranged, $"{field.Name} names no range");
            }
            var def = ScriptableObject.CreateInstance<WaterTypeDef>();
            try
            {
                StringAssert.StartsWith(WaterTypeDef.IdPrefix, def.Id);
                Assert.IsTrue(def.IsValid(), "a new water type is invalid before it is touched");
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        // ===== silt after rain ========================================================================

        [Test]
        public void AverageSignal_IsWaveMemorysAverage_BitForBit()
        {
            var swell = new Swell();
            foreach (int taps in new[] { 3, 16, 24, 65 })
                foreach (float window in new[] { 600f, 3f * (float)Day, 14f * (float)Day })
                    foreach (double t in new[] { -9000.5, 0.0, 17.125, 5400.0, 123456.789 })
                        SameBits(WaveMemory.Average(swell, t, window, taps).SeaState01,
                                 WaveMemory.AverageSignal(swell, t, window, taps),
                                 $"taps {taps}, window {window}, t {t}");
        }

        [Test]
        public void Silt_RisesAfterRain_ClearsOverItsWindow()
        {
            WaterTypeValues creek = Creek();
            double window = creek.SiltWindowDays * Day;
            double step = window / creek.SiltTaps;
            double start = 10.0 * Day;
            double end = 11.0 * Day;   // a one-day squall
            var squall = new Squall(start, end, 1f);
            float dry = WaterType.Silt(creek, 0f);

            SameBits(dry, WaterType.SiltAt(creek, squall, start - 0.5 * step, Day), "before the squall");
            float previous = dry;
            float peak = dry;
            for (double t = start; t <= end + window + step; t += step / 3.0)
            {
                float silt = WaterType.SiltAt(creek, squall, t, Day);
                if (t <= end) Assert.GreaterOrEqual(silt, previous, $"the silt fell while it rained (t {t})");
                else Assert.LessOrEqual(silt, previous, $"the silt rose after the rain stopped (t {t})");
                peak = Mathf.Max(peak, silt);
                previous = silt;
            }
            // A one-day squall in a three-day window: the exact box holds a third of the response at most.
            Assert.AreEqual(dry + creek.SiltResponse / 3f, peak, 1e-5f, "the squall's peak");
            Assert.Greater(WaterType.SiltAt(creek, squall, end + window - step, Day), dry, "cleared before its window");
            SameBits(dry, WaterType.SiltAt(creek, squall, end + window + step, Day), "not clear a window and a tap after");
        }

        [Test]
        public void Silt_DryWeather_IsTheTypesDrySilt()
        {
            WaterTypeValues creek = Creek();
            var neverRains = new Squall(0.0, 0.0, 1f);
            foreach (double t in new[] { -5000.0, 0.0, 1234.5, 99999.875 })
            {
                SameBits(creek.DrySilt, WaterType.SiltAt(creek, neverRains, t, Day), $"dry weather at {t}");
                SameBits(creek.DrySilt, WaterType.SiltAt(creek, null, t, Day), $"no rain source at {t}");
            }
            // A type the rain cannot move keeps its dry silt in any weather.
            WaterTypeValues settled = creek;
            settled.SiltResponse = 0f;
            var always = new Squall(-1e9, 1e9, 1f);
            SameBits(creek.DrySilt, WaterType.SiltAt(settled, always, 4321.0, Day), "no response");
            // And no day length is no window: the dry silt, never a NaN.
            SameBits(creek.DrySilt, WaterType.SiltAt(creek, always, 4321.0, 0.0), "no day length");
        }

        // ===== place ==================================================================================

        [Test]
        public void WaterType_FadesToNothingAtTheSurfacesEdge_NoStep()
        {
            var rect = new Vector4(-100f, -50f, 100f, 50f);
            const float seam = 32f;
            Assert.AreEqual(1f, WaterType.SeamWeight(Vector2.zero, rect, seam), "deep inside");
            Assert.AreEqual(1f, WaterType.SeamWeight(new Vector2(100f - seam, 0f), rect, seam), "a seam in");
            Assert.AreEqual(0f, WaterType.SeamWeight(new Vector2(100f, 0f), rect, seam), "on the east edge");
            Assert.AreEqual(0f, WaterType.SeamWeight(new Vector2(0f, -50f), rect, seam), "on the south edge");
            // Between, it eases with no jump: monotone, never steeper than a smoothstep's 1.5 per span.
            float previous = 0f;
            for (int i = 1; i <= 64; i++)
            {
                float x = 100f - seam * i / 64f;
                float w = WaterType.SeamWeight(new Vector2(x, 0f), rect, seam);
                Assert.GreaterOrEqual(w, previous, $"the seam turned back at x {x}");
                Assert.LessOrEqual(w - previous, 1.5f / 64f + 1e-6f, $"a step at x {x}");
                previous = w;
            }
            // No seam: the whole type everywhere, even on the edge.
            Assert.AreEqual(1f, WaterType.SeamWeight(new Vector2(100f, 50f), rect, 0f));
        }

        [Test]
        public void WaterType_APlume_FeathersIntoTheRegionsWater()
        {
            var shape = new Vector4(10f, 20f, 40f, 16f);   // a mouth at (10, 20), 40 m out, 16 m of feather
            var mouth = new Vector2(10f, 20f);
            Assert.AreEqual(1f, WaterType.PlumeWeight(mouth, shape), "at the mouth");
            Assert.AreEqual(1f, WaterType.PlumeWeight(mouth + new Vector2(24f, 0f), shape), "inside the feather");
            Assert.AreEqual(0f, WaterType.PlumeWeight(mouth + new Vector2(0f, 40f), shape), "at the reach");
            Assert.AreEqual(0f, WaterType.PlumeWeight(mouth + new Vector2(-60f, 0f), shape), "beyond it");
            float previous = 1f;
            for (int i = 0; i <= 32; i++)
            {
                float w = WaterType.PlumeWeight(mouth + new Vector2(24f + 16f * i / 32f, 0f), shape);
                Assert.LessOrEqual(w, previous, "the feather rose outward");
                previous = w;
            }
            Assert.AreEqual(0f, WaterType.PlumeWeight(mouth + new Vector2(40f, 0f), new Vector4(10f, 20f, 40f, 0f)),
                "a zero feather still closes at the reach");
            Assert.AreEqual(0f, WaterType.PlumeWeight(mouth, Vector4.zero), "no reach, no plume");

            // At a point the region's water gives way to the plume's inside its reach; across the feather
            // the plume's colour holds while only its strength thins (premultiplied: never darker).
            WaterTypeValues creek = Creek();
            WaterType.Uniforms(creek, WaterType.Silt(creek, 0f), out Vector4 plumeSigma, out Vector4 plumeBody);
            var shapes = new Vector4[WaterType.MaxPlumes];
            var sigmas = new Vector4[WaterType.MaxPlumes];
            var bodies = new Vector4[WaterType.MaxPlumes];
            shapes[0] = shape;
            sigmas[0] = plumeSigma;
            bodies[0] = plumeBody;
            var rect = new Vector4(-500f, -500f, 500f, 500f);
            WaterType.At(mouth, Vector4.zero, Vector4.zero, rect, 32f, shapes, sigmas, bodies, 1,
                         out Vector4 s, out Vector4 b);
            Assert.AreEqual(plumeSigma, s, "at the mouth: the plume's sigma");
            Assert.AreEqual(plumeBody, b, "at the mouth: the plume's body");
            WaterType.At(mouth + new Vector2(32f, 0f), Vector4.zero, Vector4.zero, rect, 32f, shapes, sigmas, bodies, 1,
                         out s, out b);
            Assert.Greater(b.w, 0f, "inside the reach the plume shows");
            Assert.Less(b.w, plumeBody.w, "across the feather the strength thins");
            for (int c = 0; c < 3; c++)
                Assert.AreEqual(plumeBody[c] / plumeBody.w, b[c] / b.w, 1e-5f, $"the plume's colour moved (channel {c})");
            WaterType.At(mouth + new Vector2(41f, 0f), Vector4.zero, Vector4.zero, rect, 32f, shapes, sigmas, bodies, 1,
                         out s, out b);
            Assert.AreEqual(Vector4.zero, s, "beyond the reach: the region's water");
            WaterType.At(mouth, Vector4.zero, Vector4.zero, rect, 32f, shapes, sigmas, bodies, 0, out s, out b);
            Assert.AreEqual(Vector4.zero, b, "a slot past the count drew");
        }

        // ===== the body ===============================================================================

        [Test]
        public void WaterType_Body_TintsTowardTheTypesColour_ByItsStainAndSilt()
        {
            WaterTypeValues creek = Creek();
            float silt = WaterType.Silt(creek, 0f);
            WaterType.Uniforms(creek, silt, out Vector4 sigma, out Vector4 body);
            Vector3 target = WaterType.BodyColour(creek, silt);
            var col = new Vector3(0.1f, 0.3f, 0.5f);

            // A deeper column takes more of the type's colour, never more than its strength.
            float shallow = WaterType.BodyWeight(sigma.w, body.w, 0.5f, 0f);
            float deep = WaterType.BodyWeight(sigma.w, body.w, 20f, 0f);
            Assert.Greater(shallow, 0f, "a shallow column takes some colour");
            Assert.Greater(deep, shallow, "a deep column takes more");
            Assert.LessOrEqual(deep, body.w, "never more than the strength");
            Vector3 tinted = WaterType.ApplyBody(col, sigma, body, 20f, 0f);
            for (int c = 0; c < 3; c++)
                Assert.AreEqual(col[c] + (target[c] - col[c]) * deep, tinted[c], 1e-5f, $"channel {c}");

            // Rain's silt thickens the scatter, so the same column takes more, leaning toward the silt's colour.
            float wet = WaterType.Silt(creek, 1f);
            WaterType.Uniforms(creek, wet, out Vector4 wetSigma, out Vector4 wetBody);
            Assert.Greater(wetSigma.w, sigma.w, "rain thickens the scatter");
            Assert.Greater(WaterType.BodyWeight(wetSigma.w, wetBody.w, 0.5f, 0f), shallow, "the wet column takes more");
            Color mudLinear = SeaPalette.ToLinear(creek.SiltColour);
            var mud = new Vector3(mudLinear.r, mudLinear.g, mudLinear.b);
            Assert.Less((WaterType.BodyColour(creek, wet) - mud).magnitude, (target - mud).magnitude,
                "rain leans the body toward the silt's colour");

            // Posterized like every layer: on the absorption bands the scatter takes k / bands.
            const float bands = 6f;
            for (int i = 0; i <= 40; i++)
            {
                float w = WaterType.BodyWeight(sigma.w, 1f, i * 0.25f, bands);
                Assert.AreEqual(Mathf.Round(w * bands), w * bands, 1e-4f, $"depth {i * 0.25f} m");
            }

            // Algae alone scatters toward no colour of its own: no strength goes out.
            WaterTypeValues green = WaterTypeValues.Default;
            green.Algae = 1f;
            green.BodyStrength = 1f;
            WaterType.Uniforms(green, 0f, out _, out Vector4 greenBody);
            AllZeroBits(greenBody, "an algae-only body");
        }

        // ===== cost ===================================================================================

        /// <summary>Acceptance 5: sixty-four ticks of the 8 Hz slow tick (eight seconds of play) read the
        /// silt over its whole window, the uniforms, a plume and a pixel, and allocate nothing.</summary>
        [Test]
        public void Silt_SlowTick_AllocatesNothing()
        {
            WaterTypeValues creek = Creek();
            var rain = new Swell();
            var shapes = new Vector4[WaterType.MaxPlumes];
            var sigmas = new Vector4[WaterType.MaxPlumes];
            var bodies = new Vector4[WaterType.MaxPlumes];
            shapes[0] = new Vector4(10f, 20f, 40f, 16f);
            var rect = new Vector4(-500f, -500f, 500f, 500f);
            float sink = Tick(creek, rain, 0.0, shapes, sigmas, bodies, rect);   // warm every path once
            using var probe = new FoamTransportAllocationProbe();
            probe.RequirePositiveControl();
            probe.Begin();
            for (int i = 1; i <= 64; i++) sink += Tick(creek, rain, i * 0.125, shapes, sigmas, bodies, rect);
            long allocations = probe.End();
            Assert.AreEqual(0, allocations, "the water type's slow tick allocated (rule 7)");
            Assert.Greater(sink, 0f, "precondition: the ticks read a type");
        }

        private static float Tick(in WaterTypeValues type, IWorldTimeSignal rain, double now, Vector4[] shapes,
                                  Vector4[] sigmas, Vector4[] bodies, Vector4 rect)
        {
            float silt = WaterType.SiltAt(type, rain, now, Day);
            WaterType.Uniforms(type, silt, out Vector4 sigma, out Vector4 body);
            WaterType.Uniforms(type, silt, out sigmas[0], out bodies[0]);
            WaterType.At(new Vector2(30f, 20f), sigma, body, rect, 32f, shapes, sigmas, bodies, 1,
                         out Vector4 s, out Vector4 b);
            return s.w + b.w;
        }

        // ===== the shader twin ========================================================================

        [Test]
        public void TheShader_CarriesTheWaterTypeTwin()
        {
            string code = ShaderCode();
            string compact = Compact(code);

            // The type's sigma joins the mood's inside AbsorptionSigma (WaterAbsorption.Sigma's type overload).
            StringAssert.Contains("staticfloat3hhWaterTypeSigma=float3(0.0,0.0,0.0);", compact);
            StringAssert.Contains("float3AbsorptionSigma(){returnmax(_Turbidity,0.0)*max(_AbsorptionRatio.rgb,0.0)" +
                                  "+hhWaterTypeSigma;}", compact, "AbsorptionSigma's type term");

            // WaterType's terms, one for one.
            StringAssert.Contains("floatWaterTypeSeamWeight(float2p){if(!(_WaterTypeSeam>0.0))return1.0;" +
                "floatd=min(min(p.x-_WaterTypeRect.x,_WaterTypeRect.z-p.x),min(p.y-_WaterTypeRect.y," +
                "_WaterTypeRect.w-p.y));returnsmoothstep(0.0,_WaterTypeSeam,d);}", compact, "SeamWeight's twin");
            StringAssert.Contains("floatWaterPlumeWeight(float2p,float4shape){floatreach=shape.z;" +
                "if(!(reach>0.0))return0.0;floatinner=max(reach-max(shape.w,WATER_PLUME_MIN_FEATHER),0.0);" +
                "return1.0-smoothstep(inner,reach,distance(p,shape.xy));}", compact, "PlumeWeight's twin");
            StringAssert.Contains("voidWaterTypeAt(float2p,outfloat4sigma,outfloat4body){sigma=_WaterTypeSigma;" +
                "body=_WaterTypeBody;[unroll]for(inti=0;i<WATER_MAX_PLUMES;i++){if((float)i<_WaterPlumeCount){" +
                "floatw=WaterPlumeWeight(p,_WaterPlumeShape[i]);sigma=lerp(sigma,_WaterPlumeSigma[i],w);" +
                "body=lerp(body,_WaterPlumeBody[i],w);}}floats=WaterTypeSeamWeight(p);sigma*=s;body*=s;}",
                compact, "At's twin");
            StringAssert.Contains("floatWaterBodyWeight(floatinscatterSigma,floatstrength,floatdepth){" +
                "floatscatter=1.0-exp(-max(inscatterSigma,0.0)*(ABSORPTION_PATH*max(depth,0.0)));" +
                "returnsaturate(strength)*AbsorptionBand(scatter.xxx,_AbsorptionBands).x;}", compact,
                "BodyWeight's twin");

            // The two constants, parsed against their C# twins.
            Match plumes = Regex.Match(code, @"#define\s+WATER_MAX_PLUMES\s+(\d+)");
            Assert.IsTrue(plumes.Success, "WATER_MAX_PLUMES is gone");
            Assert.AreEqual(WaterType.MaxPlumes, int.Parse(plumes.Groups[1].Value, CultureInfo.InvariantCulture));
            Match feather = Regex.Match(code, @"#define\s+WATER_PLUME_MIN_FEATHER\s+([0-9.eE+-]+)");
            Assert.IsTrue(feather.Success, "WATER_PLUME_MIN_FEATHER is gone");
            Assert.AreEqual(WaterType.MinFeatherMeters,
                float.Parse(feather.Groups[1].Value, CultureInfo.InvariantCulture));

            // The globals sit outside the per-material CBUFFER and out of Properties: no material and no preset
            // can author them; a surface pushes them, and one with no type leaves them at zero.
            int cbStart = compact.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            int cbEnd = compact.IndexOf("CBUFFER_END", cbStart, StringComparison.Ordinal);
            Assert.Greater(cbStart, -1, "the per-material CBUFFER is gone");
            string properties = compact.Substring(0, compact.IndexOf("SubShader", StringComparison.Ordinal));
            foreach (string global in new[] { "_WaterTypeSigma", "_WaterTypeBody", "_WaterTypeRect", "_WaterTypeSeam",
                                              "_WaterPlumeShape", "_WaterPlumeSigma", "_WaterPlumeBody",
                                              "_WaterPlumeCount" })
            {
                Match declared = Regex.Match(compact, @"float4?" + global + @"(\[WATER_MAX_PLUMES\])?;");
                Assert.IsTrue(declared.Success, $"{global} is not declared");
                Assert.IsTrue(declared.Index < cbStart || declared.Index > cbEnd, $"{global} is inside the CBUFFER");
                StringAssert.DoesNotContain(global, properties, $"{global} is a material property");
            }

            // In frag: the type at the pixel, its sigma set once ahead of every AbsorptionSigma() reader (the
            // seabed, the crest glow, the caustic), and the body tint under the palette grade.
            const string setPoint = "WaterTypeAt(Pixelize(worldXY),typeSigma,typeBody);" +
                "hhWaterTypeSigma=max(typeSigma.rgb,0.0);if(typeBody.a>0.0)col.rgb=lerp(col.rgb," +
                "typeBody.rgb/typeBody.a,WaterBodyWeight(typeSigma.a,typeBody.a,depth));";
            int set = compact.IndexOf(setPoint, StringComparison.Ordinal);
            Assert.Greater(set, -1, "the frag's water-type step is gone");
            Assert.AreEqual(set, compact.LastIndexOf(setPoint, StringComparison.Ordinal), "the type is set twice");
            Assert.AreEqual(1, Regex.Matches(compact, @"hhWaterTypeSigma=max\(").Count, "the type sigma is written twice");
            int readers = 0;
            foreach (Match call in Regex.Matches(compact, @"AbsorptionSigma\(\)"))
            {
                if (call.Index >= 6 && compact.Substring(call.Index - 6, 6) == "float3") continue;   // the definition
                readers++;
                Assert.Greater(call.Index, set, "an AbsorptionSigma() reader runs before the type is set");
            }
            Assert.AreEqual(3, readers, "the seabed, the crest glow and the caustic read AbsorptionSigma()");
            int grade = compact.IndexOf("col.rgb=PaletteGrade(col.rgb,dayNightLuma);", StringComparison.Ordinal);
            Assert.Greater(grade, set, "the body tint must land under the palette grade");
        }

        // ===== helpers ================================================================================

        /// <summary>Nine Mile Creek's shape of water: a tea stain, a little algae, dry silt, and a wet-spell
        /// response over the default window.</summary>
        private static WaterTypeValues Creek()
        {
            WaterTypeValues creek = WaterTypeValues.Default;
            creek.Stain = 0.35f;
            creek.Algae = 0.05f;
            creek.DrySilt = 0.05f;
            creek.SiltResponse = 0.8f;
            creek.BodyStrength = 0.5f;
            return creek;
        }

        /// <summary>How evenly a sigma takes the channels: the weakest over the strongest.</summary>
        private static float Evenness(Vector3 s)
            => Mathf.Min(s.x, Mathf.Min(s.y, s.z)) / Mathf.Max(s.x, Mathf.Max(s.y, s.z));

        /// <summary>Rain at a fixed strength over [start, end), dry outside it.</summary>
        private sealed class Squall : IWorldTimeSignal
        {
            private readonly double _start;
            private readonly double _end;
            private readonly float _rain;

            public Squall(double start, double end, float rain)
            {
                _start = start;
                _end = end;
                _rain = rain;
            }

            public float ValueAt(double gameTimeSeconds)
                => gameTimeSeconds >= _start && gameTimeSeconds < _end ? _rain : 0f;
        }

        /// <summary>A smooth, varying signal, and a wind source carrying it as the sea state, so WaveMemory's
        /// two averages can be read side by side.</summary>
        private sealed class Swell : IWorldTimeSignal, IWaveWindSource
        {
            public float ValueAt(double gameTimeSeconds) => 0.5f + 0.5f * Mathf.Sin((float)(gameTimeSeconds * 0.0021));

            public WaveWindSample WaveWindAt(double gameTimeSeconds)
                => new WaveWindSample(Vector2.zero, ValueAt(gameTimeSeconds));
        }

        private static void Set(WaterSurface surface, string field, object value)
            => typeof(WaterSurface).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(surface, value);

        private static void Call(WaterSurface surface, string method, params object[] args)
            => typeof(WaterSurface).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(surface, args);

        /// <summary>The shader's code with its comments taken out, so a pin never matches a comment.</summary>
        private static string ShaderCode()
        {
            string text = File.ReadAllText(ShaderPath);
            text = Regex.Replace(text, @"/\*[\s\S]*?\*/", "");
            return Regex.Replace(text, @"//[^\n]*", "");
        }

        private static string Compact(string s) => Regex.Replace(s, @"\s+", "");

        private static void SameBits(float expected, float got, string what)
            => Assert.AreEqual(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(got),
                $"{what}: {expected:R} vs {got:R}");

        private static void SameBits(Vector3 expected, Vector3 got, string what)
        {
            SameBits(expected.x, got.x, what + " (r)");
            SameBits(expected.y, got.y, what + " (g)");
            SameBits(expected.z, got.z, what + " (b)");
        }

        private static void AllZeroBits(Vector4 v, string what)
        {
            for (int i = 0; i < 4; i++) SameBits(0f, v[i], $"{what} [{i}]");
        }
    }
}
