using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards for the REAL WAVES FROM HULLS (the owner, 2026-11-17: the wake was "static drawn in lines";
    /// he wants real waves from the stern and from the bow).
    ///
    /// <para>Three kinds of guard: the LAW (the Kelvin wedge, the stationary wavelength, the decay, the
    /// speed and length scaling), the POOL (a hard budget per chain, weakest-first eviction, frame-rate
    /// independence, exact zeros when nothing is on the water) and the PLUMBING (the shader's compile-time
    /// bound, its [loop], its twin of the source law, and the older lift stepping aside only while hull
    /// waves are on). None of these certify the look; that is an editor run.</para>
    /// </summary>
    public class HullWaveMathTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const int Owner = 7;

        static HullWaveSettings S => HullWaveSettings.Default;

        // ==== 1. THE LAW =============================================================================

        [Test]
        public void TheRings_MakeTheKelvinWedge_AtEverySpeed()
        {
            foreach (float v in new[] { 0.5f, 2f, 5f, 12f })
            {
                float u = HullWaveMath.RingSpeed(v, S.KelvinHalfAngleDegrees);
                Assert.AreEqual(19.47f, HullWaveMath.WedgeHalfAngleDegrees(u, v), 1e-3f,
                    $"at {v} m/s the line of rings must bound a V of 19.47 degrees each side");
                Assert.Less(u, v, "a ring that outran her would draw a circle round her, not a V");
            }
            Assert.AreEqual(0f, HullWaveMath.RingSpeed(0f, 19.47f), "at rest no ring spreads");
            Assert.AreEqual(19.47f, S.KelvinHalfAngleDegrees, 1e-4f, "the default is the deep-water Kelvin angle");
        }

        [Test]
        public void TheStationaryWavelength_KeepsPaceWithHer()
        {
            foreach (float v in new[] { 0.5f, 1f, 3f, 7f, 15f })
            {
                float lambda = HullWaveMath.StationaryWavelength(v);
                Assert.AreEqual(v, HullWaveMath.PhaseSpeed(lambda), v * 1e-4f,
                    $"the crests behind her at {v} m/s must travel at her speed, so they stand still on her");
            }
            Assert.AreEqual(S.MinWavelengthMetres, HullWaveMath.Wavelength(0.1f, S), 1e-6f, "held at the owner's floor");
            Assert.AreEqual(S.MaxWavelengthMetres, HullWaveMath.Wavelength(50f, S), 1e-6f, "held at the owner's ceiling");
        }

        [Test]
        public void TheFade_FallsAllItsLife_AndIsExactlyZeroAtTheEnd()
        {
            const float life = 12f, decay = 5f;
            Assert.AreEqual(1f, HullWaveMath.TimeFade(0f, life, decay), 1e-6f, "full height at birth");
            float last = 2f;
            for (int i = 0; i < 120; i++)
            {
                float f = HullWaveMath.TimeFade(i * life / 120f, life, decay);
                Assert.Less(f, last, $"the fade must fall at every step (step {i})");
                Assert.GreaterOrEqual(f, 0f);
                last = f;
            }
            Assert.AreEqual(0f, HullWaveMath.TimeFade(life, life, decay), "exactly 0 at the end: retiring never pops");
            Assert.AreEqual(0f, HullWaveMath.TimeFade(life + 3f, life, decay));
            Assert.AreEqual(0f, HullWaveMath.TimeFade(1f, 0f, decay), "no life, no height");
        }

        [Test]
        public void TheSpread_LosesHeightOutward()
        {
            Assert.AreEqual(1f, HullWaveMath.Spread(0f, 4f), 1e-6f);
            float last = 1f;
            for (int r = 1; r < 60; r++)
            {
                float f = HullWaveMath.Spread(r, 4f);
                Assert.Less(f, last, "farther out, lower");
                last = f;
            }
        }

        [Test]
        public void TheCrestHeight_ScalesWithSpeedAndLength()
        {
            Assert.AreEqual(0f, HullWaveMath.CrestHeight(S.MinSpeed, 20f, S), "at or below the onset: nothing");
            Assert.AreEqual(0f, HullWaveMath.CrestHeight(0f, 20f, S));
            Assert.AreEqual(0f, HullWaveMath.CrestHeight(5f, 0f, S), "no length, no waves");

            float last = 0f;
            for (int i = 1; i <= 80; i++)
            {
                float h = HullWaveMath.CrestHeight(i * 0.15f, 30f, S);
                Assert.GreaterOrEqual(h, last, "faster never makes smaller waves");
                last = h;
            }

            float doryRowed = HullWaveMath.CrestHeight(1.2f, 5f, S);
            float dory = HullWaveMath.CrestHeight(4f, 5f, S);
            float cargo = HullWaveMath.CrestHeight(4f, 40f, S);
            float cargoFast = HullWaveMath.CrestHeight(6f, 40f, S);
            Assert.Greater(doryRowed, 0f, "a rowed dory still ripples");
            Assert.Less(doryRowed, 0.05f, "a rowed dory makes ripples, centimetres");
            Assert.Less(dory, cargo * 0.6f, "at the same speed a dory makes far less than a cargo vessel");
            Assert.Greater(cargoFast, 0.2f, "a cargo vessel at speed makes rollers");
            Assert.LessOrEqual(HullWaveMath.CrestHeight(40f, 200f, S), S.MaxHeightMetres, "capped by the owner's dial");
        }

        [Test]
        public void TheDepositInterval_HonoursTheBudget()
        {
            Assert.IsTrue(float.IsPositiveInfinity(HullWaveMath.DepositInterval(0f, 2f, 10f, 10, 0.3f)),
                "a hull at rest lays nothing");
            foreach (int budget in new[] { 1, 5, 18, 28 })
            foreach (float life in new[] { 4f, 16f, 30f })
            {
                float interval = HullWaveMath.DepositInterval(5f, 16f, life, budget, 0.3f);
                Assert.GreaterOrEqual(interval * budget, life,
                    $"budget {budget}, life {life}: more than the budget could be alive at once");
            }
        }

        [Test]
        public void TheSourceSlope_IsTheDerivativeOfTheSourceHeight()
        {
            const float amp = 0.2f, front = 5f, k = 2f, phase = 0.7f, invW = 0.4f, invR = 0.25f, step = 1e-3f;
            for (float r = 2.6f; r <= 7.4f; r += 0.37f)
            {
                float fd = (HullWaveMath.SourceHeight(r + step, amp, front, k, phase, invW, invR) -
                            HullWaveMath.SourceHeight(r - step, amp, front, k, phase, invW, invR)) / (2f * step);
                float an = HullWaveMath.SourceSlope(r, amp, front, k, phase, invW, invR);
                Assert.AreEqual(fd, an, 2e-3f + Mathf.Abs(fd) * 0.01f, $"slope at r = {r}");
            }
            Assert.AreEqual(0f, HullWaveMath.SourceHeight(front + 3.01f / invW, amp, front, k, phase, invW, invR),
                "past the band's reach the ring draws nothing");
        }

        [Test]
        public void ThePhase_IsWrapped_SoTheShaderSeesSmallAngles()
        {
            float p = HullWaveMath.Phase(3f, 9f, 1e6);
            Assert.GreaterOrEqual(p, 0f);
            Assert.Less(p, 2f * Mathf.PI + 1e-5f);
        }

        // ==== 2. THE POOL ============================================================================

        [Test]
        public void AChain_NeverHoldsMoreThanItsBudget()
        {
            var field = new HullWaveField();
            var chain = new HullWaveChain();
            const int budget = 10;
            int most = 0;
            for (int i = 0; i <= 60 * 60; i++)
            {
                double now = i / 60.0;
                chain.Step(field, new Vector2(5f * (float)now, 0f), now, 5f, 20f, 1f, budget, Owner, S);
                field.Advance(now);
                int owned = field.CountOwnedBy(Owner);
                Assert.LessOrEqual(owned, budget, $"over budget at {now:0.00} s");
                most = Mathf.Max(most, owned);
            }
            Assert.AreEqual(budget, most, "a long run at speed should fill the budget it was given");
        }

        [Test]
        public void AChain_AfterAHitch_StillHoldsItsBudget()
        {
            var field = new HullWaveField();
            var chain = new HullWaveChain();
            const int budget = 6;
            chain.Step(field, Vector2.zero, 0.0, 5f, 20f, 1f, budget, Owner, S);
            int laid = chain.Step(field, new Vector2(500f, 0f), 100.0, 5f, 20f, 1f, budget, Owner, S);
            field.Advance(100.0);
            Assert.LessOrEqual(laid, budget, "a long frame lays at most the budget");
            Assert.LessOrEqual(field.CountOwnedBy(Owner), budget);
        }

        [Test]
        public void TheSameVoyage_LaysTheSameWaves_AtAnyFrameRate()
        {
            HullWaveField slow = Voyage(30), fast = Voyage(144);
            Assert.Greater(slow.Count, 0, "premise: the voyage made waves");
            Assert.AreEqual(slow.Count, fast.Count, "30 and 144 fps must lay the same number of sources");
            // Per chain: within one chain the pool holds its sources in birth order, while how the two
            // chains interleave depends on which frame each was laid in, which is no part of the water.
            foreach (int owner in new[] { Owner, Owner + 1 })
            {
                Assert.Greater(slow.CountOwnedBy(owner), 0, $"premise: chain {owner} made waves");
                Assert.AreEqual(slow.CountOwnedBy(owner), fast.CountOwnedBy(owner), $"chain {owner}: count");
                int i = 0, j = 0;
                while (true)
                {
                    while (i < slow.Count && slow[i].Owner != owner) i++;
                    while (j < fast.Count && fast[j].Owner != owner) j++;
                    if (i >= slow.Count || j >= fast.Count) break;
                    Assert.AreEqual(slow[i].BirthSeconds, fast[j].BirthSeconds, 1e-9, $"chain {owner}: birth");
                    Assert.AreEqual(slow[i].Position.x, fast[j].Position.x, 1e-3f, $"chain {owner}: x");
                    Assert.AreEqual(slow[i].Position.y, fast[j].Position.y, 1e-3f, $"chain {owner}: y");
                    Assert.AreEqual(slow[i].Amplitude, fast[j].Amplitude, 1e-6f, $"chain {owner}: height");
                    i++;
                    j++;
                }
            }
        }

        static HullWaveField Voyage(int fps)
        {
            var field = new HullWaveField();
            var stern = new HullWaveChain();
            var bow = new HullWaveChain();
            for (int i = 0; i <= 20 * fps; i++)
            {
                double now = (double)i / fps;
                var p = new Vector2(4f * (float)now, 1.5f * (float)now);
                stern.Step(field, p, now, 4.27f, 12f, 1f, 18, Owner, S);
                bow.Step(field, p + new Vector2(11f, 4f), now, 4.27f, 12f, S.BowShare, 10, Owner + 1, S);
                field.Advance(now);
            }
            return field;
        }

        [Test]
        public void ThePool_EvictsTheWeakestFirst()
        {
            var field = new HullWaveField(4);
            foreach (float a in new[] { 0.1f, 0.2f, 0.3f, 0.4f })
                Assert.IsTrue(field.Add(Source(Vector2.zero, a, 0.0)));
            Assert.IsFalse(field.Add(Source(Vector2.zero, 0.05f, 0.0)), "weaker than everything: dropped");
            Assert.AreEqual(4, field.Count);
            Assert.IsTrue(field.Add(Source(Vector2.zero, 0.5f, 0.0)), "stronger: the weakest gives way");
            Assert.AreEqual(4, field.Count);
            for (int i = 0; i < field.Count; i++)
                Assert.AreNotEqual(0.1f, field[i].Amplitude, "the weakest is the one that went");

            field.Limit = 2;
            Assert.AreEqual(2, field.Count, "lowering the limit drops at once");
            Assert.AreEqual(0.4f, field[0].Amplitude);
            Assert.AreEqual(0.5f, field[1].Amplitude);
        }

        [Test]
        public void ThePool_RetiresTheDead_AndForgetsAWorldTheClockLeft()
        {
            var field = new HullWaveField();
            field.Advance(10.0);
            field.Add(Source(Vector2.zero, 0.2f, 10.0));
            field.Advance(11.0);
            Assert.AreEqual(1, field.Count);
            field.Advance(20.0);
            Assert.AreEqual(0, field.Count, "past its life it is gone");

            field.Add(Source(Vector2.zero, 0.2f, 20.0));
            field.Advance(15.0);
            Assert.AreEqual(0, field.Count, "a clock that runs backwards (a load) clears the water");
        }

        [Test]
        public void NothingOnTheWater_ReadsExactlyZero()
        {
            var field = new HullWaveField();
            Assert.AreEqual(0f, field.SampleHeight(new Vector2(3f, 4f), 4f));
            Assert.AreEqual(Vector2.zero, field.SampleSlope(new Vector2(3f, 4f), 4f));

            HullWaveField before = HullWaves.Field;
            try
            {
                HullWaves.Uninstall(before);
                Assert.IsFalse(HullWaves.IsActive);
                Assert.AreEqual(0f, HullWaves.HeightAt(Vector2.zero), "no field, no height");
                Assert.IsFalse(HullWaves.AddRipple(Vector2.zero, 0.05f, 0.5f), "no field, nothing laid");
            }
            finally
            {
                if (before != null) HullWaves.Install(before);
            }
        }

        [Test]
        public void AHull_DoesNotRideHerOwnWaves()
        {
            var field = new HullWaveField();
            field.Add(Source(Vector2.zero, 0.1f, 0.0));
            field.Advance(2.0);
            var at = new Vector2(2f, 0f);   // on the band's centre, where the crest is
            Assert.AreNotEqual(0f, field.SampleHeight(at, 4f), "premise: the ring is under the point");
            Assert.AreEqual(0f, field.SampleHeight(at, 4f, Owner), "her own waves are left out of her ride");
            Assert.AreNotEqual(0f, field.SampleHeight(at, 4f, Owner + 1), "another hull's ride reads them");
        }

        [Test]
        public void TheFieldsSlope_IsTheGradientOfItsHeight()
        {
            var field = new HullWaveField();
            field.Add(Source(Vector2.zero, 0.1f, 0.0));
            field.Add(Source(new Vector2(1.5f, -0.5f), 0.08f, 0.4));
            field.Advance(2.0);
            const float e = 1e-3f;
            foreach (var p in new[] { new Vector2(1.7f, 0.6f), new Vector2(-1.2f, 1.4f), new Vector2(2.3f, -1.1f) })
            {
                float gx = (field.SampleHeight(p + new Vector2(e, 0f), 4f) - field.SampleHeight(p - new Vector2(e, 0f), 4f)) / (2f * e);
                float gy = (field.SampleHeight(p + new Vector2(0f, e), 4f) - field.SampleHeight(p - new Vector2(0f, e), 4f)) / (2f * e);
                Vector2 g = field.SampleSlope(p, 4f);
                Assert.AreEqual(gx, g.x, 3e-3f + Mathf.Abs(gx) * 0.02f, $"d/dx at {p}");
                Assert.AreEqual(gy, g.y, 3e-3f + Mathf.Abs(gy) * 0.02f, $"d/dy at {p}");
            }
        }

        [Test]
        public void ThePack_SendsOnlyWhatTheViewCanSee_AlreadyAged()
        {
            var field = new HullWaveField();
            field.Add(Source(Vector2.zero, 0.1f, 0.0));
            field.Add(Source(new Vector2(500f, 500f), 0.1f, 0.0));
            field.Advance(1.0);
            var a = new Vector4[HullWaveField.ShaderCapacity];
            var b = new Vector4[HullWaveField.ShaderCapacity];
            var view = new Rect(-20f, -20f, 40f, 40f);
            Assert.AreEqual(1, field.Pack(a, b, view, true), "the far ring is not sent");
            Assert.AreEqual(2, field.Pack(a, b, view, false));
            Assert.AreEqual(field[0].AmplitudeAt(1.0), a[0].z, 1e-6f, "height already aged");
            Assert.AreEqual(field[0].RingSpeed * 1f, a[0].w, 1e-5f, "band radius already grown");
            Assert.GreaterOrEqual(b[0].y, 0f);
            Assert.Less(b[0].y, 2f * Mathf.PI + 1e-5f, "phase already wrapped");
        }

        [Test]
        public void ARipple_SpreadsAtHalfItsCrestSpeed()
        {
            HullWaveSource r = HullWaves.MakeRipple(new Vector2(1f, 2f), 0.03f, 0.4f, 5.0, S);
            Assert.AreEqual(HullWaveMath.PhaseSpeed(0.4f), r.PhaseSpeed, 1e-6f);
            Assert.AreEqual(0.5f * r.PhaseSpeed, r.RingSpeed, 1e-6f, "a stone's ripples: the group at half the crests");
            Assert.AreEqual(S.RippleLifetimeSeconds, r.Lifetime, 1e-6f);
            Assert.AreEqual(0, r.Owner, "a ripple belongs to no hull, so every hull rides it");
            Assert.AreEqual(5.0, r.BirthSeconds);
        }

        [Test]
        public void TheDefaults_FitTheShadersBound()
        {
            Assert.LessOrEqual(S.MaxSources, HullWaveField.ShaderCapacity);
            Assert.LessOrEqual(S.SourcesPerHull, S.MaxSources);
            Assert.IsTrue(S.Enabled);

            string yaml = File.ReadAllText(Path.Combine(Application.dataPath, "..", ConfigPath));
            StringAssert.Contains("\n  HullWaves:\n", yaml.Replace("\r\n", "\n"), "the owner's dials are in GameConfig");
            StringAssert.Contains("KelvinHalfAngleDegrees: 19.47", yaml);
        }

        static HullWaveSource Source(Vector2 at, float amp, double birth) => new HullWaveSource
        {
            Position = at,
            BirthSeconds = birth,
            Amplitude = amp,
            Wavenumber = Mathf.PI,      // 2 m crests
            PhaseSpeed = 1f,
            RingSpeed = 1f,
            PacketWidth = 2f,
            Lifetime = 8f,
            DecaySeconds = 4f,
            Owner = Owner,
        };

        // ==== 3. THE PLUMBING ========================================================================

        static string WaterSource() =>
            File.ReadAllText(Path.Combine(Application.dataPath, "..", ShaderPath)).Replace("\r\n", "\n");

        static float Define(string src, string name)
        {
            Match m = Regex.Match(src, @"(?m)^\s*#define\s+" + name + @"\s+([0-9.]+)");
            Assert.IsTrue(m.Success, name + " is not defined in the water shader");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        static string Body(string src, string signature)
        {
            int at = src.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.Greater(at, -1, "not found in the water shader: " + signature);
            int open = src.IndexOf('{', at);
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}' && --depth == 0) return src.Substring(open, i - open + 1);
            }
            Assert.Fail("unbalanced body: " + signature);
            return null;
        }

        [Test]
        public void TheShadersBound_IsTheFieldsCapacity_AndItsReachIsTheLaws()
        {
            string src = WaterSource();
            Assert.AreEqual(HullWaveField.ShaderCapacity, (int)Define(src, "HH_HULL_WAVE_MAX"));
            Assert.AreEqual(HullWaveMath.PacketReach, Define(src, "HH_HULL_WAVE_PACKET_REACH"), 1e-6f);
            foreach (string decl in new[]
                     {
                         "float4 _HHHullWaveA[HH_HULL_WAVE_MAX];", "float4 _HHHullWaveB[HH_HULL_WAVE_MAX];",
                         "float4 _HHHullWaveParams;", "float4 _HHHullWaveShade;", "float4 _HHHullWaveFoam;",
                     })
                StringAssert.Contains(decl, src);
            Assert.AreEqual(PropertyId("_HHHullWaveA"), HullWaveBridge.IdA);
            Assert.AreEqual(PropertyId("_HHHullWaveParams"), HullWaveBridge.IdParams);
        }

        static int PropertyId(string name) => Shader.PropertyToID(name);

        [Test]
        public void TheSourceLoop_RunsAsALoop_AndBreaksAtTheLiveCount()
        {
            string body = Body(WaterSource(), "void HullWaveSample(float2 worldXY, out float height, out float2 slope)");
            StringAssert.Contains("[loop]\n                for (int i = 0; i < HH_HULL_WAVE_MAX; i++)", body,
                "a runtime-bounded loop must be [loop] with a compile-time bound");
            StringAssert.Contains("if (i >= count) break;", body);
            StringAssert.DoesNotContain("[unroll]", body, "an [unroll] over the sources is the magenta trap");
            StringAssert.Contains("if (_HHHullWaveParams.w < 0.5 || _HHHullWaveParams.x < 0.5) return;", body,
                "off or empty returns before a slot is read");
        }

        [Test]
        public void TheShadersSourceLaw_IsTheTwinOfTheCSharp()
        {
            string body = Regex.Replace(Body(WaterSource(),
                "void HullWaveSourceHeight(float r, float amp, float front, float k, float phase,"), @"\s+", "");
            foreach (string piece in new[]
                     {
                         "floatx=(r-front)*invWidth;",
                         "if(x<=-HH_HULL_WAVE_PACKET_REACH||x>=HH_HULL_WAVE_PACKET_REACH)return;",
                         "floatenv=exp(-x*x);",
                         "floatspread=1.0/sqrt(1.0+r*invSpread);",
                         "floatph=k*r-phase;",
                         "h=amp*env*spread*c;",
                         "floatdEnv=-2.0*x*invWidth*env;",
                         "floatdSpread=-0.5*invSpread*spread*spread*spread;",
                         "dhdr=amp*(dEnv*spread*c+env*dSpread*c-env*spread*k*sn);",
                     })
                StringAssert.Contains(piece, body, "the shader's source law moved away from HullWaveMath");

            // …and the numbers agree: the shader's law, transcribed, against the C# at sample points.
            const float amp = 0.15f, front = 3f, k = 2.5f, phase = 1.1f, invW = 0.5f, invR = 0.25f;
            for (float r = 0.2f; r < 9f; r += 0.61f)
            {
                float x = (r - front) * invW;
                float expected = Mathf.Abs(x) >= 3f ? 0f
                    : amp * Mathf.Exp(-x * x) / Mathf.Sqrt(1f + r * invR) * Mathf.Cos(k * r - phase);
                Assert.AreEqual(expected, HullWaveMath.SourceHeight(r, amp, front, k, phase, invW, invR), 1e-6f);
            }
        }

        [Test]
        public void TheOlderLift_StepsAsideOnlyWhileHullWavesAreOn()
        {
            string src = WaterSource();
            Assert.IsTrue(Regex.IsMatch(src,
                    @"if \(_HHHullWaveParams\.w < 0\.5\)\s*\n\s*lift \+= WakeLiftHeight\(ground\) \* fade;"),
                "with hull waves off the older train must draw exactly as before");
            StringAssert.Contains("lift += hullWave * _HHHullWaveParams.y * fade;", src,
                "the hull waves lift the water beside the swell, faded at the shore with it");
        }
    }
}
