using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Environment;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE REFLECTION LADDER — why the sea goes out at a blow, in arithmetic, with no GPU.</b>
    /// (water-fidelity charter 2026-09-10, "the body of the sea"; register rows 6 and 25.)
    ///
    /// <para><b>The defect this measures.</b> At noon on open water at mean tide the plate sweep reads mean
    /// wet luma <c>glass 0.331 → light 0.176 → blow 0.012 → gale 0.021</c>. A <b>15× cliff</b> between light
    /// airs and a blow, at the sea state the player actually sails. Row 6 named the reflection as the
    /// suspect; this fixture prices it, on the CPU, through the shipped twin
    /// <see cref="WaterReflection"/> — which mirrors <c>HiddenHarboursWater.shader</c>'s own
    /// <c>ReflectionStrength()</c> / <c>ReflectionSharpness()</c> line for line.</para>
    ///
    /// <para><b>Every input is read, never typed.</b> The four weather anchors come from the path constants
    /// <see cref="WaterSceneTemplate"/> itself binds (base = <c>null</c> on purpose, so the live
    /// <c>Water.mat</c> is the calm baseline — ADR 0017); the blend shape comes off a real
    /// <see cref="WaterSurface"/> through <see cref="SerializedObject"/>, so no tunable is retyped here
    /// (rule 6); the wind comes from the sim's own inverse <see cref="WeatherModel.WindStrengthFor"/>; the
    /// chop and the roughness come from <see cref="WaterSurface"/>'s own public statics — the SAME two
    /// values the component pushes every frame, which are never read off a <c>.mat</c>.</para>
    ///
    /// <para><b>What it asserts is SHAPE, never a number.</b> A rougher sea must never mirror MORE, and a
    /// dead-calm sea must suffer no reduction at all. The cliff itself is <b>reported, not asserted</b>: a
    /// guard that pinned today's 0.0095 would go red on the day the owner rules the fix.</para>
    ///
    /// <para>The table lands in <c>artifacts/water-plates/ladder/LADDER.txt</c> (gitignored) and in the
    /// console, and is transcribed into the PR body — a number that is not in the body did not happen.</para>
    /// </summary>
    [TestFixture]
    public class WaterReflectionLadderTests
    {
        const string OutDir = "artifacts/water-plates/ladder";

        /// <summary>The measured mean wet luma of the open-water control at mean tide and noon, from the
        /// plate sweep's own manifests (register row 6). Quoted here so the arithmetic can be held against
        /// the picture in one table; nothing is asserted against it.</summary>
        static readonly float[] MeasuredWetLuma = { 0.331f, 0.176f, 0.012f, 0.021f };

        /// <summary>Row 5's reflection-off diagnostic: the calm sea's own BODY reads 0.05 where the same
        /// plate with the reflection on reads 0.331. One point, so it fixes the scale of a single linear
        /// term and nothing more — which is all the decomposition below claims.</summary>
        const float CalmBodyLumaWithTheReflectionOff = 0.05f;

        static string PathConst(string name)
        {
            FieldInfo f = typeof(WaterSceneTemplate).GetField(
                name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(f, name + " is no longer a constant of WaterSceneTemplate — the ladder would be " +
                                "reading anchors the shipped template no longer binds");
            return (string)f.GetRawConstantValue();
        }

        static Material LoadAnchor(string constName)
        {
            string path = PathConst(constName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(m, path + " must exist — it is one of the four weather anchors");
            return m;
        }

        /// <summary>The blend shape as the component ships it. Read off a real WaterSurface rather than
        /// retyped, so this table can never quote a threshold the component no longer uses.</summary>
        struct BlendShape
        {
            public float WindForFullRoughness, SeaThreshold, SeaCurve, FogThreshold, FogCurve, CalmReach;
        }

        static BlendShape ReadBlendShape(out string provenance)
        {
            var go = new GameObject("ladder-probe");
            try
            {
                var surface = go.AddComponent<WaterSurface>();
                var so = new SerializedObject(surface);
                float Get(string field)
                {
                    SerializedProperty p = so.FindProperty(field);
                    Assert.IsNotNull(p, "WaterSurface no longer serialises " + field);
                    return p.floatValue;
                }
                var shape = new BlendShape
                {
                    WindForFullRoughness = Get("_windForFullRoughness"),
                    SeaThreshold = Get("_seaStateThreshold"),
                    SeaCurve = Get("_seaStateCurve"),
                    FogThreshold = Get("_fogThreshold"),
                    FogCurve = Get("_fogCurve"),
                    CalmReach = Get("_calmReach"),
                };
                provenance = $"windForFullRoughness {shape.WindForFullRoughness:0.###} · " +
                             $"seaThreshold {shape.SeaThreshold:0.###} · seaCurve {shape.SeaCurve:0.###} · " +
                             $"fogThreshold {shape.FogThreshold:0.###} · fogCurve {shape.FogCurve:0.###} · " +
                             $"calmReach {shape.CalmReach:0.###}";
                return shape;
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>One rung: everything the shader multiplies together at one sea state, priced apart.</summary>
        struct Rung
        {
            public float Sea, Visibility, Wind, Roughness, Chop;
            public float[] W;
            public float Master, FadeChop, WindFade, ChopScatter, WindScatter;
            public float ChopFalloff, WindDim, Strength, Sharpness;
        }

        static Rung Climb(float sea, float visibility, BlendShape shape, Material[] anchor, Vector2 heading)
        {
            float windMag = WeatherModel.WindStrengthFor(sea);
            Vector2 wind = heading * windMag;
            var r = new Rung
            {
                Sea = sea,
                Visibility = visibility,
                Wind = windMag,
                Roughness = WaterSurface.Roughness(wind, shape.WindForFullRoughness),
                Chop = WaterSurface.Choppiness(sea),
                W = WeatherWaterPalette.BlendWeights(sea, visibility, shape.SeaThreshold, shape.SeaCurve,
                                                     shape.FogThreshold, shape.FogCurve, shape.CalmReach),
            };

            float Blend(string key)
            {
                float sum = 0f;
                for (int a = 0; a < anchor.Length; a++)
                {
                    Assert.IsTrue(anchor[a].HasProperty(key), anchor[a].name + " has no " + key);
                    sum += r.W[a] * anchor[a].GetFloat(key);
                }
                return sum;
            }

            r.Master = Blend("_ReflectionStrength");
            r.FadeChop = Blend("_ReflectionFadeChop");
            r.WindFade = Blend("_ReflectionWindFade");
            r.ChopScatter = Blend("_ReflectionChopScatter");
            r.WindScatter = Blend("_ReflectionWindScatter");

            // The shader's ReflectionStrength(), factored into its two multiplies so they can be priced
            // apart — the chop falloff, and the wind dimming. The product of the three IS the twin's
            // answer, which the assertions below check rung by rung.
            r.ChopFalloff = 1f - WaterSurface.Smoothstep(0f, Mathf.Max(r.FadeChop, 1e-3f), Mathf.Max(r.Chop, 0f));
            r.WindDim = 1f - Mathf.Clamp01(r.Roughness) * Mathf.Clamp01(r.WindFade);

            r.Strength = WaterReflection.ReflectionStrength(r.Chop, r.Roughness, r.FadeChop, r.WindFade, r.Master);
            r.Sharpness = WaterReflection.ReflectionSharpness(r.Chop, r.Roughness, r.ChopScatter, r.WindScatter);
            return r;
        }

        [Test]
        public void TheReflectionLadder_PricedRungByRung()
        {
            Material[] anchor =
            {
                LoadAnchor("ArtWaterMat"),          // base — the live Water.mat (ADR 0017: null in the template)
                LoadAnchor("ArtWaterCalmMood"),
                LoadAnchor("ArtWaterStormMood"),
                LoadAnchor("ArtWaterFogMood"),
            };
            string[] anchorName = { "Water.mat (base)", "GlassyCalm", "StormGrey", "FoggySmother" };
            string[] keys =
            {
                "_ReflectionStrength", "_ReflectionFadeChop", "_ReflectionWindFade",
                "_ReflectionChopScatter", "_ReflectionWindScatter",
            };

            BlendShape shape = ReadBlendShape(out string provenance);
            Vector2 heading = WaterFidelityPlateSweepTests.WindHeading;
            float[] seas = WaterFidelityPlateSweepTests.SeaStateOf;
            string[] seaName = WaterFidelityPlateSweepTests.WeatherName;

            var sb = new StringBuilder();
            sb.AppendLine("# THE REFLECTION LADDER — register rows 6 / 25, water charter 2026-09-10.");
            sb.AppendLine("# Pure arithmetic through the shipped twin WaterReflection; no GPU, no scene.");
            sb.AppendLine($"# blend shape, read off WaterSurface: {provenance}");
            sb.AppendLine($"# wind heading (the sweep's own): ({heading.x:0.###}, {heading.y:0.###})");
            sb.AppendLine();

            sb.AppendLine("## the four weather anchors, as shipped");
            sb.AppendLine($"{"knob",-24}" + string.Join("", Array.ConvertAll(anchorName, n => $"{n,18}")));
            foreach (string key in keys)
            {
                var cells = new string[anchor.Length];
                for (int a = 0; a < anchor.Length; a++) cells[a] = $"{anchor[a].GetFloat(key),18:0.###}";
                sb.AppendLine($"{key,-24}" + string.Concat(cells));
            }
            sb.AppendLine();

            // ⭐ The finding the charter did not expect: _ReflectionFadeChop is the SAME on every anchor, so
            // the chop falloff is mood-INVARIANT. Only the master and the wind fade walk with the weather.
            float fade0 = anchor[0].GetFloat("_ReflectionFadeChop");
            bool fadeIsMoodInvariant = true;
            for (int a = 1; a < anchor.Length; a++)
                if (Mathf.Abs(anchor[a].GetFloat("_ReflectionFadeChop") - fade0) > 1e-6f) fadeIsMoodInvariant = false;
            sb.AppendLine(fadeIsMoodInvariant
                ? $"_ReflectionFadeChop is {fade0:0.###} on ALL FOUR anchors — the chop falloff is mood-INVARIANT; " +
                  "the master and the wind fade are the terms that walk with the weather."
                : "_ReflectionFadeChop now DIFFERS between anchors — the falloff walks with the mood too; " +
                  "register rows 6/25 describe a sea that no longer ships.");
            sb.AppendLine();

            // ------------------------------------------------------------------ the ladder, four sweep seas
            var rung = new Rung[seas.Length];
            foreach (float visibility in new[] { 1f, 0.5f })
            {
                sb.AppendLine($"## the ladder at visibility {visibility:0.##}" +
                              (visibility >= 1f
                                  ? "  (clear — the register's plates)"
                                  : "  (thick — the fog anchor in play)"));
                sb.AppendLine($"{"sea",-7}{"state",7}{"wind m/s",10}{"rough",8}{"chop",7}" +
                              $"{"w.base",8}{"w.calm",8}{"w.storm",8}{"w.fog",8}" +
                              $"{"master",9}{"fadeChop",10}{"windFade",10}{"chopFall",10}{"windDim",9}" +
                              $"{"STRENGTH",10}{"vs glass",9}{"SHARP",8}");
                float glass = 0f;
                for (int i = 0; i < seas.Length; i++)
                {
                    Rung r = Climb(seas[i], visibility, shape, anchor, heading);
                    if (visibility >= 1f) rung[i] = r;
                    if (i == 0) glass = Mathf.Max(r.Strength, 1e-9f);
                    sb.AppendLine($"{seaName[i],-7}{r.Sea,7:0.00}{r.Wind,10:0.00}{r.Roughness,8:0.000}{r.Chop,7:0.00}" +
                                  $"{r.W[0],8:0.000}{r.W[1],8:0.000}{r.W[2],8:0.000}{r.W[3],8:0.000}" +
                                  $"{r.Master,9:0.0000}{r.FadeChop,10:0.0000}{r.WindFade,10:0.0000}" +
                                  $"{r.ChopFalloff,10:0.0000}{r.WindDim,9:0.0000}" +
                                  $"{r.Strength,10:0.00000}{r.Strength / glass,9:0.0000}{r.Sharpness,8:0.000}");
                }
                sb.AppendLine();
            }

            // ------------------------------------------------------- held against the picture the sweep shot
            float glassStrength = Mathf.Max(rung[0].Strength, 1e-9f);
            float lumaPerUnit = (MeasuredWetLuma[0] - CalmBodyLumaWithTheReflectionOff) / glassStrength;
            sb.AppendLine("## the arithmetic against the measured plates (ww-open, mean tide, noon)");
            sb.AppendLine($"{"sea",-7}{"luma",9}{"luma/glass",12}{"refl/glass",12}{"body = luma - k*refl",22}");
            for (int i = 0; i < seas.Length; i++)
                sb.AppendLine($"{seaName[i],-7}{MeasuredWetLuma[i],9:0.000}" +
                              $"{MeasuredWetLuma[i] / MeasuredWetLuma[0],12:0.0000}" +
                              $"{rung[i].Strength / glassStrength,12:0.0000}" +
                              $"{MeasuredWetLuma[i] - lumaPerUnit * rung[i].Strength,22:0.0000}");
            sb.AppendLine($"k = {lumaPerUnit:0.0000} luma per unit of reflection strength " +
                          $"(from row 5's reflection-off diagnostic: body {CalmBodyLumaWithTheReflectionOff:0.###} " +
                          $"against a lit {MeasuredWetLuma[0]:0.###})");
            sb.AppendLine("⚠️ The residual BODY column is what the sea would read with the reflection removed. " +
                          "It is ~0.02 at every sea above glass: the water has essentially no light of its own, " +
                          "so the reflection collapse is not PART of the cliff — it is the whole of it.");
            sb.AppendLine();

            // ------------------------------------------------------------------- what option (a) would buy
            sb.AppendLine("## option (a) priced: move _ReflectionFadeChop, nothing else");
            sb.AppendLine("⚠️ STRENGTH is a function of sea state and visibility ONLY — the tide and the hour " +
                          "enter the picture through the wet extent and the day/night tint, never through this " +
                          "term. One column per sea therefore prices every tide and every hour at once.");
            sb.AppendLine($"{"fadeChop",10}" + string.Join("", Array.ConvertAll(seaName, n => $"{n,12}")) +
                          $"{"blow luma*",13}");
            foreach (float fc in new[] { 0.6f, 0.8f, 1.0f, 1.2f, 1.5f, 2.0f })
            {
                var cells = new string[seas.Length];
                float blowStrength = 0f;
                for (int i = 0; i < seas.Length; i++)
                {
                    Rung r = rung[i];
                    float st = WaterReflection.ReflectionStrength(r.Chop, r.Roughness, fc, r.WindFade, r.Master);
                    cells[i] = $"{st,12:0.0000}";
                    if (i == 2) blowStrength = st;
                }
                float body = MeasuredWetLuma[2] - lumaPerUnit * rung[2].Strength;
                sb.AppendLine($"{fc,10:0.0}" + string.Concat(cells) + $"{body + lumaPerUnit * blowStrength,13:0.000}");
            }
            sb.AppendLine("* predicted mean wet luma at the blow = that sea's own residual body + k × strength. " +
                          "A LINEAR extrapolation from one measured point; the plate is the proof, not this column.");
            sb.AppendLine("⚠️ The shader declares _ReflectionFadeChop as Range(0,1): anything above 1.0 needs the " +
                          "Range widened before a material can hold it (the HLSL itself has no upper clamp).");
            sb.AppendLine();
            sb.AppendLine("⭐ What option (a) costs the CALM sea: exactly nothing, and not by tuning. At a dead " +
                          "calm _Chop is 0, and smoothstep(0, fadeChop, 0) is 0 for every positive fadeChop, so " +
                          "the falloff is 1 whatever this knob holds. Row 5's mirror cannot be moved by (a). " +
                          "What (a) DOES cost is the shipped intent at the top of the scale \u2014 'a storm does not " +
                          "mirror' \u2014 because the gale column above rises with it. And it must NOT be applied to " +
                          "ReflectionSharpness: that is a separate curve (_ReflectionChopScatter / " +
                          "_ReflectionWindScatter) and a storm that mirrors SHARPLY is a different picture from a " +
                          "storm that carries a broad smear of sky.");
            sb.AppendLine();

            // -------------------------------------------------------------- what options (b) and (c) would buy
            // (b) a sky-scatter floor that RISES as the image falls: col.rgb += floor * fall, where fall is the
            //     fraction of the mirror this sea state has already taken away. Free at a calm BY CONSTRUCTION.
            // (c) lift the body itself, in the two forms that price differently:
            //     (c1) the SHARED body (_PaletteDeep / _DeepBlueStrength on the base) \u2014 a multiplier on the
            //          residual body column, which is what the sea reads with the reflection gone;
            //     (c2) the STORM ANCHOR's own lift \u2014 reaching each sea only through its storm blend weight.
            var fall = new float[seas.Length];
            var bodyOf = new float[seas.Length];
            var stormW = new float[seas.Length];
            for (int i = 0; i < seas.Length; i++)
            {
                fall[i] = 1f - rung[i].ChopFalloff * rung[i].WindDim;
                bodyOf[i] = MeasuredWetLuma[i] - lumaPerUnit * rung[i].Strength;
                stormW[i] = rung[i].W[(int)WeatherWaterPalette.Anchor.Storm];
            }

            sb.AppendLine("## options (b) and (c) priced on the same axis");
            sb.AppendLine($"{"per sea",-24}" + string.Join("", Array.ConvertAll(seaName, n => $"{n,12}")));
            sb.AppendLine($"{"measured luma",-24}" +
                          string.Concat(Array.ConvertAll(MeasuredWetLuma, v => $"{v,12:0.000}")));
            sb.AppendLine($"{"residual body",-24}" + string.Concat(Array.ConvertAll(bodyOf, v => $"{v,12:0.000}")));
            sb.AppendLine($"{"(b) fall = 1-chop*wind",-24}" + string.Concat(Array.ConvertAll(fall, v => $"{v,12:0.0000}")));
            sb.AppendLine($"{"(c2) storm blend weight",-24}" + string.Concat(Array.ConvertAll(stormW, v => $"{v,12:0.0000}")));
            sb.AppendLine("(b) adds floor x fall; (c1) multiplies the residual body; (c2) adds lift x storm weight.");
            sb.AppendLine();

            // ------------------------------------------------------------ THE TABLE. Three targets, four prices.
            sb.AppendLine("## ⭐ THE TABLE — what each option costs, at three recoveries of the blow");
            sb.AppendLine("Target = the mean wet luma the blow would read. 0.176 is what LIGHT AIRS reads today, " +
                          "so 'half' is halfway back to a breeze and 'all' is a blow as bright as light airs.");
            sb.AppendLine($"{"target at the blow",-22}{"option",-10}{"knob move",-30}" +
                          $"{"glass",10}{"light",10}{"blow",10}{"gale",10}");
            foreach (float target in new[] { 0.25f * MeasuredWetLuma[1], 0.5f * MeasuredWetLuma[1], MeasuredWetLuma[1] })
            {
                // (a) invert ReflectionStrength for the fadeChop that lands the blow on the target.
                float want = (target - bodyOf[2]) / Mathf.Max(lumaPerUnit, 1e-9f);
                float lo = 0.6f, hi = 64f;
                for (int it = 0; it < 60; it++)
                {
                    float mid = 0.5f * (lo + hi);
                    float st = WaterReflection.ReflectionStrength(rung[2].Chop, rung[2].Roughness, mid,
                                                                  rung[2].WindFade, rung[2].Master);
                    if (st < want) lo = mid; else hi = mid;
                }
                float fadeChop = 0.5f * (lo + hi);
                bool reachableByA = want <= rung[2].Master * rung[2].WindDim + 1e-6f;
                var aCells = new string[seas.Length];
                for (int i = 0; i < seas.Length; i++)
                {
                    float st = WaterReflection.ReflectionStrength(rung[i].Chop, rung[i].Roughness, fadeChop,
                                                                  rung[i].WindFade, rung[i].Master);
                    aCells[i] = $"{bodyOf[i] + lumaPerUnit * st,10:0.000}";
                }
                sb.AppendLine($"{target,-22:0.000}{"(a)",-10}" +
                              (reachableByA ? $"{"_ReflectionFadeChop " + fadeChop.ToString("0.00"),-30}"
                                            : $"{"UNREACHABLE by (a) alone",-30}") +
                              string.Concat(aCells));

                float floorB = (target - MeasuredWetLuma[2]) / Mathf.Max(fall[2], 1e-9f);
                var bCells = new string[seas.Length];
                for (int i = 0; i < seas.Length; i++) bCells[i] = $"{MeasuredWetLuma[i] + floorB * fall[i],10:0.000}";
                sb.AppendLine($"{"",-22}{"(b)",-10}{"new _SkyScatterFloor " + floorB.ToString("0.000"),-30}" +
                              string.Concat(bCells));

                float mC1 = (target - lumaPerUnit * rung[2].Strength) / Mathf.Max(bodyOf[2], 1e-9f);
                var c1Cells = new string[seas.Length];
                for (int i = 0; i < seas.Length; i++)
                    c1Cells[i] = $"{lumaPerUnit * rung[i].Strength + mC1 * bodyOf[i],10:0.000}";
                sb.AppendLine($"{"",-22}{"(c1)",-10}{"shared body x " + mC1.ToString("0.0"),-30}" +
                              string.Concat(c1Cells));

                float liftC2 = (target - MeasuredWetLuma[2]) / Mathf.Max(stormW[2], 1e-9f);
                var c2Cells = new string[seas.Length];
                for (int i = 0; i < seas.Length; i++) c2Cells[i] = $"{MeasuredWetLuma[i] + liftC2 * stormW[i],10:0.000}";
                sb.AppendLine($"{"",-22}{"(c2)",-10}{"storm anchor + " + liftC2.ToString("0.000"),-30}" +
                              string.Concat(c2Cells));
                sb.AppendLine();
            }
            sb.AppendLine("How to read the four rows. (a) and (c2) and (b) all cost the GLASS column exactly " +
                          "nothing \u2014 (a) because a calm has no chop to fade, (b) because its rise is keyed to the " +
                          "fall it is compensating, (c2) because the storm anchor's blend weight at a calm is " +
                          "0.000. (c1) is the only one that cannot be made free to the calm sea: it multiplies a " +
                          "body the calm sea also has, and the glass column is what that costs. Against that, " +
                          "(c1) is the ONLY one that needs no new uniform and no widened Range \u2014 it is two " +
                          "existing knobs. The GALE column is the mood price: it is where (a) undoes 'a storm " +
                          "does not mirror' and where (c2) lands hardest, 2.6x whatever it gives the blow.");
            sb.AppendLine("⚠️ Every predicted column is a LINEAR extrapolation from ONE measured point per sea. " +
                          "It is arithmetic for ranking the options, not a plate. The plate is the proof.");
            sb.AppendLine();

            // ------------------------------------------------------------------- the table goes to disk FIRST
            // ⚠ The table IS this fixture's deliverable, so it is written BEFORE the first Assert: a red
            // run must still leave the numbers behind to diagnose from, and an assertion that throws takes
            // everything after it with it. The Debug.Log stays at the END on purpose — a report EMITTED
            // ahead of its assertions is attributed to no test and reads as SKIPPED in the results.
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, "LADDER.txt"), sb.ToString());
            Assert.IsTrue(File.Exists(Path.Combine(OutDir, "LADDER.txt")), "the ladder table must be written");

            // ------------------------------------------------------------------------------- the assertions
            // SHAPE only. A rougher sea must never mirror MORE, and a dead calm must suffer no reduction.
            const float Step = 0.02f;
            float prevStrength = float.MaxValue, prevSharpness = float.MaxValue;
            for (float sea = 0f; sea <= 1.0001f; sea += Step)
            {
                Rung r = Climb(Mathf.Clamp01(sea), 1f, shape, anchor, heading);
                Assert.IsFalse(float.IsNaN(r.Strength) || float.IsInfinity(r.Strength),
                               $"reflection strength is not finite at sea {sea:0.00}");
                Assert.That(r.Strength, Is.InRange(0f, 1f), $"reflection strength left [0,1] at sea {sea:0.00}");
                Assert.That(r.Sharpness, Is.InRange(0f, 1f), $"reflection sharpness left [0,1] at sea {sea:0.00}");
                Assert.LessOrEqual(r.Strength, prevStrength + 1e-5f,
                                   $"a ROUGHER sea mirrored MORE at sea {sea:0.00} — the reflection is not monotone");
                Assert.LessOrEqual(r.Sharpness, prevSharpness + 1e-5f,
                                   $"a ROUGHER sea reflected more SHARPLY at sea {sea:0.00}");
                Assert.AreEqual(r.Master * r.ChopFalloff * r.WindDim, r.Strength, 1e-5f,
                                $"the twin's answer is not the product of its own three terms at sea {sea:0.00}");
                prevStrength = r.Strength; prevSharpness = r.Sharpness;
            }

            Rung calm = Climb(0f, 1f, shape, anchor, heading);
            Assert.AreEqual(1f, calm.ChopFalloff, 1e-6f, "a dead calm has no chop and must lose nothing to the falloff");
            Assert.AreEqual(1f, calm.WindDim, 1e-6f, "a dead calm has no wind and must lose nothing to the wind fade");
            // Option (a)'s whole case rests on this: the fadeChop knob cannot reach a dead calm at all.
            foreach (float fc in new[] { 0.6f, 1f, 2f, 8f, 64f })
                Assert.AreEqual(calm.Master,
                                WaterReflection.ReflectionStrength(calm.Chop, calm.Roughness, fc,
                                                                   calm.WindFade, calm.Master), 1e-6f,
                                $"_ReflectionFadeChop {fc:0.##} moved the DEAD CALM's reflection. It must not: " +
                                "at chop 0 the falloff is 1 for every positive fadeChop, and that is the whole " +
                                "reason option (a) can be offered as free to row 5's mirror.");

            Assert.AreEqual(calm.Master, calm.Strength, 1e-6f,
                            "on a glassy calm the reflection must be the blended master, undiminished — " +
                            "row 5's mirror is the one thing this ladder guards");

            Debug.Log("[water-ladder]\n" + sb);
        }
    }
}
