using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Gains 3 (ADR 0027 #7 amendment): the crest glow, light through a thin crest when the sun is low. <see cref="CrestGlow"/> is the C# twin of the shader's crest-glow block. Pinned:
    /// <list type="bullet">
    /// <item>strength 0 is an exact passthrough, and every water material ships at 0 (the premise that moves
    /// when the dial is picked by eye);</item>
    /// <item>it lights only the thin top of a crest (the height above the onset), dark at night and at noon on the sun side's own gate, gone in fog (a mood float the fog anchor
    /// reads at 0), posterized;</item>
    /// <item>its colour is the sun's through the water's own sigma: the open sea takes red first, a stain
    /// takes blue;</item>
    /// <item>the shader carries the twin, on the shared elevation gate.</item>
    /// </list>
    /// </summary>
    public class CrestGlowTests
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string FogAnchorPath = "Assets/_Project/Art/Materials/WaterPresets/Water_FoggySmother.mat";
        private const string Dial = "_CrestGlowStrength";
        private const float Top = 0.55f;      // the shader's default onset
        private const float LowSun = 0.3f;

        [Test]
        public void CrestGlow_AtZero_IsAnExactPassthrough()
        {
            var col = new Vector3(0.12f, 0.34f, 0.56f);
            Vector3 colour = CrestGlow.Colour(Vector3.one,
                WaterAbsorption.Sigma(0.6f, WaterAbsorption.DefaultRatio), 1.5f);
            foreach (float face in new[] { -1f, -0.4f, 0f, 0.7f })
                foreach (float height in new[] { 0f, 0.6f, 0.95f, 1f })
                    foreach (float sun in new[] { -0.2f, 0.05f, LowSun, 1f })
                    {
                        float amount = CrestGlow.Amount(0f, height, Top, 3f, 1f, sun);
                        SameBits(0f, amount, $"the amount at face {face}, height {height}, sun {sun}");
                        SameBits(col, CrestGlow.Apply(col, colour, amount), "the colour");
                    }
            // The shader's own threshold: a dial at 0.001 is still off.
            SameBits(0f, CrestGlow.Amount(0.001f, 1f, Top, 3f, 1f, LowSun), "a dial at the threshold");
        }

        [Test]
        public void CrestGlow_LightsOnlyTheThinTop()
        {
            // L1 has no facing input: either side of the crest follows the same height law.
            foreach (float height in new[] { 0f, 0.3f, Top })
                Assert.AreEqual(0f, CrestGlow.Amount(1f, height, Top, 0f, 1f, LowSun),
                    $"lit below the thin top ({height})");
            float lower = CrestGlow.Amount(1f, 0.7f, Top, 0f, 1f, LowSun);
            float upper = CrestGlow.Amount(1f, 0.95f, Top, 0f, 1f, LowSun);
            Assert.Greater(lower, 0f, "the thin top is lit on either facing");
            Assert.Greater(upper, lower, "the thinner the crest, the more light comes through");
            Assert.AreEqual(0f, CrestGlow.Amount(1f, 1f, Top, 0f, 0f, LowSun), "lit on glass (the calm gate)");
        }

        [Test]
        public void CrestGlow_DarkAtNightAndNoon_LitAtLowSun()
        {
            foreach (float sun in new[] { -0.3f, -0.01f, 0f, 1f })
                Assert.AreEqual(0f, CrestGlow.Amount(1f, 1f, Top, 0f, 1f, sun), $"lit at sun elevation {sun}");
            float low = CrestGlow.Amount(1f, 1f, Top, 0f, 1f, 0.25f);
            float high = CrestGlow.Amount(1f, 1f, Top, 0f, 1f, 0.8f);
            Assert.Greater(high, 0f, "a high sun still lights a little");
            Assert.Greater(low, high, "a low sun lights the most");
        }

        [Test]
        public void CrestGlow_ElevationGate_IsTheSunSidesGate()
        {
            // The sun side's gate as the shader writes it: smoothstep(0.0, 0.12, e) * sinSq * sinSq.
            for (int i = 0; i <= 120; i++)
            {
                float e = -0.1f + 1.2f * i / 120f;
                float t = Mathf.Clamp01(e / 0.12f);
                float sinSq = 1f - e * e;
                Assert.AreEqual(t * t * (3f - 2f * t) * sinSq * sinSq, CrestGlow.ElevationGate(e), 1e-6f, $"e {e}");
            }
        }

        [Test]
        public void CrestGlow_Posterizes()
        {
            var levels = new HashSet<float>();
            for (int i = 0; i <= 64; i++)
            {
                float height = Top + (1f - Top) * i / 64f;
                float amount = CrestGlow.Amount(1f, height, Top, 3f, 1f, LowSun);
                Assert.AreEqual(Mathf.Round(amount * 3f), amount * 3f, 1e-5f, $"height {height}: off the bands");
                if (amount > 0f) levels.Add(amount);
            }
            Assert.GreaterOrEqual(levels.Count, 2, "the glow should step through at least two lit bands");
        }

        [Test]
        public void CrestGlow_IsTheSunThroughTheWater()
        {
            var lowSun = new Vector3(1f, 0.85f, 0.6f);   // a warm, low sun's tint
            SameBits(lowSun, CrestGlow.Colour(lowSun, new Vector3(0.3f, 0.2f, 0.1f), 0f), "no sheet of water");

            Vector3 sea = WaterAbsorption.Sigma(0.6f, WaterAbsorption.DefaultRatio);
            Vector3 white = CrestGlow.Colour(Vector3.one, sea, 1.5f);
            Assert.Less(white.x, white.y, "the open sea takes red first (against green)");
            Assert.Less(white.x, white.z, "the open sea takes red first (against blue)");
            Vector3 jade = CrestGlow.Colour(lowSun, sea, 1.5f);
            Assert.Greater(jade.y, jade.x, "a low sun through the open sea glows green-led (against red)");
            Assert.Greater(jade.y, jade.z, "a low sun through the open sea glows green-led (against blue)");

            WaterTypeValues tea = WaterTypeValues.Default;
            tea.Stain = 1f;
            Vector3 stained = CrestGlow.Colour(Vector3.one,
                WaterAbsorption.Sigma(0.6f, WaterAbsorption.DefaultRatio, WaterType.TypeSigma(tea, 0f)), 1.5f);
            Assert.Less(stained.z / stained.y, white.z / white.y, "a stain takes blue behind green");
            Assert.Greater(stained.x / stained.y, white.x / white.y, "through a stain red gains on green");

            Vector3 thick = CrestGlow.Colour(lowSun, sea, 3f);
            Vector3 thin = CrestGlow.Colour(lowSun, sea, 1f);
            for (int c = 0; c < 3; c++) Assert.Less(thick[c], thin[c], $"a thicker sheet passes less (channel {c})");
        }

        /// <summary>The dial ships at 0 on every material drawn with the water shader. This is the premise that
        /// moves, by name, when a strength is picked from Phase B's arms.</summary>
        [Test]
        public void CrestGlow_ShipsAtZero_OnEveryWaterMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader, "the water shader is gone");
            int seen = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader != shader) continue;
                seen++;
                Assert.IsTrue(material.HasProperty(Dial), $"{path} lacks the crest glow");
                Assert.AreEqual(0f, material.GetFloat(Dial), $"{path}: the glow ships at 0 (its strength is picked by eye)");
            }
            Assert.GreaterOrEqual(seen, 9, "Water.mat and the eight presets");
        }

        [Test]
        public void CrestGlow_IsAMoodFloat_AndTheFogAnchorReadsZero()
        {
            FieldInfo field = typeof(WaterSurface).GetField("MoodFloatNames", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "WaterSurface.MoodFloatNames is gone");
            var names = (string[])field.GetValue(null);
            Assert.AreEqual(1, Array.FindAll(names, n => n == Dial).Length, "the glow must ease with the weather, once");
            var fog = AssetDatabase.LoadAssetAtPath<Material>(FogAnchorPath);
            Assert.IsNotNull(fog, FogAnchorPath);
            Assert.AreEqual(0f, fog.GetFloat(Dial), "fog takes the glow away: its anchor must read 0");
        }

        [Test]
        public void TheShader_CarriesTheCrestGlowTwin()
        {
            string code = ShaderCode();
            string compact = Regex.Replace(code, @"\s+", "");

            Match property = Regex.Match(code,
                @"^\s*_CrestGlowStrength\s*\(""[^""]*""\s*,\s*Range\(\s*0\s*,\s*2\s*\)\s*\)\s*=\s*([-0-9.]+)\s*$",
                RegexOptions.Multiline);
            Assert.IsTrue(property.Success, "the _CrestGlowStrength property is gone or reshaped");
            Assert.AreEqual(0f, float.Parse(property.Groups[1].Value, CultureInfo.InvariantCulture),
                "the shader's default must be 0");

            int cbStart = Index(compact, "CBUFFER_START(UnityPerMaterial)");
            int cbEnd = compact.IndexOf("CBUFFER_END", cbStart, StringComparison.Ordinal);
            Assert.Greater(cbEnd, cbStart, "the per-material CBUFFER never closes");
            StringAssert.Contains("float_CrestGlowStrength;float_CrestGlowTop;float_CrestGlowThickness;" +
                "float_CrestGlowBands;", compact.Substring(cbStart, cbEnd - cbStart),
                "the four dials belong in the per-material CBUFFER");

            // One facing, opened by any of its three readers.
            StringAssert.Contains("floatfaceSigned=0.0;if((_SwellFaceShade>0.001||_SunSideStrength>0.001||" +
                "_CrestGlowStrength>0.001)&&swellReadGate>0.001)", compact, "the shared facing's gate");
            Assert.AreEqual(1, Regex.Matches(compact, @"faceSigned=clamp\(").Count, "a second facing is a second normal");

            // One elevation gate, hoisted where the sun side and the glow both read it.
            StringAssert.Contains("floate=_SunElevation;floatsinSq=1.0-e*e;" +
                "floatelevGate=smoothstep(0.0,0.12,e)*sinSq*sinSq;if(_SunSideStrength>0.001&&swellReadGate>0.001)",
                compact, "the hoisted elevation gate");

            const string glow = "if(_CrestGlowStrength>0.001&&swellReadGate>0.001&&trainsLive){" +
                "floatglowTop=smoothstep(min(saturate(_CrestGlowTop),0.99),1.0," +
                "saturate(waveHeight/max(_WaveFieldParams.z,1e-5)));" +
                "floatglowAmt=glowTop*elevGate;" +
                "if(_CrestGlowBands>=1.0)glowAmt=floor(saturate(glowAmt)*_CrestGlowBands+0.5)/_CrestGlowBands;" +
                "glowAmt*=max(_CrestGlowStrength,0.0)*swellReadGate;" +
                "float3glowRGB=_DayNightTint.rgb*exp(-AbsorptionSigma()*max(_CrestGlowThickness,0.0));" +
                "col.rgb+=glowRGB*glowAmt*0.15;}";
            int at = Index(compact, glow);
            Assert.AreEqual(at, compact.LastIndexOf(glow, StringComparison.Ordinal), "the glow is drawn twice");

            // Its constants are the twin's.
            Assert.AreEqual(CrestGlow.SunRiseFade, Parse(compact, @"elevGate=smoothstep\(0\.0,([0-9.]+),e\)"));
            Assert.AreEqual(CrestGlow.MaxTopOnset, Parse(compact, @"min\(saturate\(_CrestGlowTop\),([0-9.]+)\)"));
            Assert.AreEqual(CrestGlow.AddCeiling, Parse(compact, @"col\.rgb\+=glowRGB\*glowAmt\*([0-9.]+);"));

            // Where the trains are known, after the sun side, under the palette grade.
            Assert.Less(Index(compact, "booltrainsLive="), at, "the glow reads trainsLive before it is set");
            Assert.Less(Index(compact, "col.rgb+=sunChroma*amt;"), at, "the glow lands after the sun side");
            Assert.Greater(Index(compact, "col.rgb=PaletteGrade(col.rgb,dayNightLuma);"), at,
                "the glow must land under the palette grade");
        }

        [TestCase("Faint")]
        [TestCase("Readable")]
        [TestCase("Strong")]
        public void ReviewArm_AtNoon_IsAnExactPassthrough_AtAnyStrength(string name)
        {
            CrestGlowDef arm = ReviewArm(name);
            DayNightProfile profile = AssetDatabase.LoadAssetAtPath<DayNightProfile>("Assets/_Project/Resources/DayNightProfile.asset");
            foreach (float hour in new[] { 12f, (profile.SunriseHour + profile.SunsetHour) * 0.5f })
            {
                float e = DayNightMath.SunElevation(hour, profile.SunriseHour, profile.SunsetHour);
                // Even top=1 cannot reach the first band. Strength is multiplied AFTER that zero,
                // so this bound covers every finite strength, not just the sampled dials below.
                Assert.Less(CrestGlow.ElevationGate(e), 0.5f / arm.Bands);
                foreach (float strength in new[] { 0f, 0.001f, arm.Strength, 2f, 1000000f })
                {
                    float amount = CrestGlow.Amount(strength, 1f, arm.Onset, arm.Bands, 1f, e);
                    var col = new Vector3(0.12f, 0.34f, 0.56f);
                    SameBits(col, CrestGlow.Apply(col, Vector3.one, amount), $"{name}, hour {hour}, strength {strength}");
                }
            }
        }

        [TestCase("Faint")]
        [TestCase("Readable")]
        [TestCase("Strong")]
        public void ReviewArm_AtZero_PreservesEveryColourBit(string name)
        {
            CrestGlowDef arm = ReviewArm(name);
            var col = new Vector3(0.12f, 0.34f, 0.56f);
            foreach (float height in new[] { 0f, arm.Onset, 0.7f, 1f })
                foreach (float e in new[] { -0.1f, 0.05f, 0.3f, 1f })
                {
                    float amount = CrestGlow.Amount(0f, height, arm.Onset, arm.Bands, 1f, e);
                    SameBits(col, CrestGlow.Apply(col, Vector3.one, amount), name);
                }
        }

        [TestCase("Faint", 19.5f, 0.342f)]
        [TestCase("Readable", 17.5f, 0.444f)]
        [TestCase("Readable", 19.5f, 0.342f)]
        [TestCase("Strong", 17.5f, 0.444f)]
        [TestCase("Strong", 19.5f, 0.342f)]
        public void ReviewArm_LightsSomeCrests_AndNoTrough(string name, float hour, float seaState)
        {
            CrestGlowDef arm = ReviewArm(name);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            var profile = AssetDatabase.LoadAssetAtPath<DayNightProfile>("Assets/_Project/Resources/DayNightProfile.asset");
            // A steady, unfetched 2 km sea. This is a small reproducible model fixture, not a claim about
            // the live memory field at the plate camera. The plates read the actual environment's seed.
            Vector2 wind = new Vector2(6f, -5.3f).normalized * WindForSeaState(seaState);
            WaveTrains trains = WaveMath.TrainsFrom(wind, seaState, config.WaveField);
            PackedWaveField packed = WaveFieldBridge.Pack(trains);
            float e = DayNightMath.SunElevation(hour, profile.SunriseHour, profile.SunsetHour);
            int lit = 0, troughs = 0, cells = 0;
            float peak = 0f;
            const int HalfDomainMetres = 1000, StepMetres = 2;
            for (int y = -HalfDomainMetres; y < HalfDomainMetres; y += StepMetres)
                for (int x = -HalfDomainMetres; x < HalfDomainMetres; x += StepMetres)
                {
                    WaveSample sample = WaveFieldBridge.ShaderTwinSample(new Vector2(x, y), packed);
                    float height = Mathf.Clamp01(sample.Height / packed.TotalAmplitude);
                    float amount = CrestGlow.Amount(arm.Strength, height, arm.Onset, arm.Bands, 1f, e);
                    float beforeBands = CrestGlow.Amount(1f, height, arm.Onset, 0f, 1f, e);
                    peak = Mathf.Max(peak, beforeBands);
                    cells++;
                    if (amount > 0f) lit++;
                    if (sample.Height <= 0f)
                    {
                        troughs++;
                        if (amount != 0f) Assert.Fail($"{name} lights trough ({x},{y}) at {sample.Height} m");
                    }
                }
            Assert.Greater(troughs, 0, "the fixture must actually contain troughs");
            Assert.Greater(peak, 0.5f / arm.Bands, "some crest must cross the rounded band's threshold");
            Assert.Greater(lit, 0, "the data arm must emit light on some crest cells");
            TestContext.WriteLine($"{name} {hour}: {lit}/{cells} cells lit ({100.0 * lit / cells:F6}%), " +
                $"{troughs} trough cells, none lit; pre-band peak {peak:R}, floor {0.5f / arm.Bands:R}");
        }

        [Test]
        public void ReviewArms_AreValidUniqueData_AndBothWaterTypesShipStrong()
        {
            var ids = new HashSet<string>();
            foreach (string name in new[] { "Faint", "Readable", "Strong" })
            {
                CrestGlowDef arm = ReviewArm(name);
                Assert.IsTrue(arm.IsValid(), name);
                Assert.IsTrue(ids.Add(arm.Id), "each review arm needs its own stable id");
            }
            string[] types = { "NineMileCreek", "StPetersOpenAtlantic" };
            foreach (string name in types)
            {
                var type = AssetDatabase.LoadAssetAtPath<WaterTypeDef>(
                    $"Assets/_Project/Data/Water/Types/WaterType_{name}.asset");
                Assert.IsNotNull(type, name);
                Assert.AreSame(ReviewArm("Strong"), type.CrestGlow, "both typed regions ship Strong");
            }
        }

        [Test]
        public void WaterSurface_ReviewSelection_PushesData_FadesInFog_AndRestoresItsBlock()
        {
            var go = new GameObject("CrestGlowTests.Surface", typeof(MeshRenderer));
            go.SetActive(false);
            var type = ScriptableObject.CreateInstance<WaterTypeDef>();
            var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath));
            try
            {
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var surface = go.AddComponent<WaterSurface>();
                typeof(WaterSurface).GetField("_renderer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(surface, renderer);
                typeof(WaterSurface).GetField("_waterType", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(surface, type);
                MethodInfo push = typeof(WaterSurface).GetMethod("PushCrestGlow", BindingFlags.Instance | BindingFlags.NonPublic);
                var block = new MaterialPropertyBlock();
                push.Invoke(surface, new object[] { block, 1f });
                Assert.IsTrue(block.isEmpty, "no selection must write nothing");
                type.CrestGlow = ReviewArm("Readable");
                push.Invoke(surface, new object[] { block, 1f });
                Assert.AreEqual(type.CrestGlow.Strength, block.GetFloat(Dial));
                Assert.AreEqual(type.CrestGlow.Onset, block.GetFloat("_CrestGlowTop"));
                Assert.AreEqual(type.CrestGlow.ThicknessMeters, block.GetFloat("_CrestGlowThickness"));
                Assert.AreEqual(type.CrestGlow.Bands, block.GetFloat("_CrestGlowBands"));
                push.Invoke(surface, new object[] { block, 0f });
                SameBits(0f, block.GetFloat(Dial), "full fog");
                type.CrestGlow = null;
                push.Invoke(surface, new object[] { block, 1f });
                foreach (string key in new[] { Dial, "_CrestGlowTop", "_CrestGlowThickness", "_CrestGlowBands" })
                    SameBits(material.GetFloat(key), block.GetFloat(key), "restored " + key);
                var fresh = new MaterialPropertyBlock();
                push.Invoke(surface, new object[] { fresh, 1f });
                Assert.IsTrue(fresh.isEmpty, "after restoration, stop writing overrides");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(type);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void CrestGlow_LightsTheRealSea_AtBothLowHours()
        {
            // Captured eight-train fixture: seed 12345, day 1, review centre (-180,136),
            // 16 x 9 m on the shader's 24 cells/metre grid. Rounded train printouts are frozen inputs,
            // not regenerated from the implementation. This catches the former slope-times-top zero
            // that a steep steady-sea fixture missed. Independent double height reconstruction follows
            // the shader's sharpened sine; the float Amount twin supplies the law under test.
            var trains = new[] {
                new double[,] {
                    { 0.5238d, 0.8518d, 0.2094d, 0.04911d, 4.8124d },
                    { 0.7634d, 0.6459d, 0.2696d, 0.02548d, 2.6283d },
                    { 0.9825d, -0.1863d, 0.3776d, 0.0368d, 4.4884d },
                    { 0.9395d, 0.3425d, 0.4271d, 0.04464d, 3.1356d },
                    { 0.9365d, 0.3508d, 0.5572d, 0.05845d, 3.3677d },
                    { 0.8598d, 0.5107d, 0.7666d, 0.03356d, 5.5256d },
                    { -0.6454d, 0.7639d, 0.1571d, 0.10526d, 3.5551d },
                    { -0.6454d, 0.7639d, 0.1939d, 0.05668d, 1.7477d },
                },
                new double[,] {
                    { 0.7123d, 0.7019d, 0.2094d, 0.04761d, 3.4325d },
                    { 0.8957d, 0.4446d, 0.2696d, 0.0241d, 3.7353d },
                    { 0.9367d, -0.3501d, 0.3776d, 0.03192d, 4.8202d },
                    { 0.984d, 0.1783d, 0.4271d, 0.03679d, 3.9601d },
                    { 0.9824d, 0.187d, 0.5572d, 0.05538d, 4.528d },
                    { 0.8866d, 0.4626d, 0.7666d, 0.04297d, 2.588d },
                    { -0.6324d, 0.7746d, 0.1571d, 0.10482d, 5.848d },
                    { -0.6324d, 0.7746d, 0.1939d, 0.05644d, 2.2011d },
                }
            };
            float[] hours = { 17.5f, 19.5f };
            double[] envelopes = { 0.40997d, 0.40002d };
            int[] expectedLit = { 3095, 1003 };
            const int CellsPerMetre = 24, WidthCells = 384, HeightCells = 216;
            const double Sharpening = 2.6d;
            // Four cells cover the independently measured <=1e-6 pre-band rounding neighbourhood
            // of the rounded-input Python table; this is not a GPU-visibility tolerance.
            const int RoundingCells = 4;
            CrestGlowDef arm = ReviewArm("Strong");
            var profile = AssetDatabase.LoadAssetAtPath<DayNightProfile>("Assets/_Project/Resources/DayNightProfile.asset");
            for (int hour = 0; hour < hours.Length; hour++)
            {
                int lit = 0;
                float e = DayNightMath.SunElevation(hours[hour], profile.SunriseHour, profile.SunsetHour);
                for (int iy = 0; iy < HeightCells; iy++)
                    for (int ix = 0; ix < WidthCells; ix++)
                    {
                        double x = -188d + ix / (double)CellsPerMetre;
                        double y = 131.5d + iy / (double)CellsPerMetre;
                        double height = 0d;
                        for (int train = 0; train < 8; train++)
                        {
                            double[,] t = trains[hour];
                            double theta = t[train,2] * (t[train,0] * x + t[train,1] * y) + t[train,4];
                            double sine = Math.Max((Math.Sin(theta) + 1d) * 0.5d, 1e-6d);
                            height += t[train,3] * (2d * Math.Pow(sine, Sharpening) - 1d);
                        }
                        float amount = CrestGlow.Amount(arm.Strength, (float)(height / envelopes[hour]),
                            arm.Onset, arm.Bands, 1f, e);
                        if (amount > 0f) lit++;
                        if (height <= 0d) Assert.AreEqual(0f, amount, "no trough may light");
                    }
                Assert.Greater(lit, 0, $"the real sea must light at {hours[hour]}");
                Assert.That(lit, Is.InRange(expectedLit[hour] - RoundingCells, expectedLit[hour] + RoundingCells),
                    $"{hours[hour]}: rounded printed-train reference");
                TestContext.WriteLine($"Real sea {hours[hour]}: {lit}/{WidthCells * HeightCells} lit cells");
            }
        }

        private static CrestGlowDef ReviewArm(string name)
        {
            var arm = AssetDatabase.LoadAssetAtPath<CrestGlowDef>(
                $"Assets/_Project/Data/Water/CrestGlow/CrestGlow_{name}.asset");
            Assert.IsNotNull(arm, $"missing review arm {name}");
            return arm;
        }

        private static float WindForSeaState(float sea)
        {
            // WeatherModel.WindStrengthFor lives outside Art's assembly references. The two B5 subject
            // states lie on the same published 2..4 and 4..6 m/s bands (WeatherModel.SeaBandEdges).
            return sea < 3f / 7f ? Mathf.Lerp(2f, 4f, sea * 7f - 2f) : Mathf.Lerp(4f, 6f, sea * 7f - 3f);
        }

        // ===== helpers ================================================================================

        /// <summary>The shader's code with its comments taken out, so a pin never matches a comment.</summary>
        private static string ShaderCode()
        {
            string text = File.ReadAllText(ShaderPath);
            text = Regex.Replace(text, @"/\*[\s\S]*?\*/", "");
            return Regex.Replace(text, @"//[^\n]*", "");
        }

        private static int Index(string compact, string pin)
        {
            int at = compact.IndexOf(pin, StringComparison.Ordinal);
            Assert.Greater(at, -1, $"the shader no longer carries {pin}");
            return at;
        }

        private static float Parse(string compact, string pattern)
        {
            Match m = Regex.Match(compact, pattern);
            Assert.IsTrue(m.Success, $"the shader no longer matches {pattern}");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static void SameBits(float expected, float got, string what)
            => Assert.AreEqual(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(got),
                $"{what}: {expected:R} vs {got:R}");

        private static void SameBits(Vector3 expected, Vector3 got, string what)
        {
            SameBits(expected.x, got.x, what + " (r)");
            SameBits(expected.y, got.y, what + " (g)");
            SameBits(expected.z, got.z, what + " (b)");
        }
    }
}
