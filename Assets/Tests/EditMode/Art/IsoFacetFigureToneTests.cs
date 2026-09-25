using System;
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
    /// <b>THE V9 TONE, PINNED WITHOUT A GPU</b> — <see cref="IsoFacetFigureTone"/> and the facet shader's
    /// <c>HH_FIGURE</c> variant.
    ///
    /// <para>Every expected number below is worked in the test from the v9.1 character kit's own rule,
    /// and each tone row was also run through the kit's reference painter, <c>refPaint()</c> in
    /// <c>tools/check-shading.cjs</c>, whose tone lines are kit rule 6 in code. The kit is read from the
    /// seat's verified copy and never copied into this repo:
    /// <c>C:/hh-gauntlet/seat/v28-colour/kit-v9.1</c>, whose <c>SHA256SUMS.txt</c> has sha256
    /// <c>9cab771c7f4926e3038141208e167372852b2fe0845a7328d29c05afc3fccc3b</c>. The rules are the
    /// <c>rule</c> list of its <c>data/shading.v9.json</c>; "kit rule N" is its N-th entry.</para>
    ///
    /// <para>Three parts. (1) THE FOLD: the kit's key and form term become the shader's one light vector
    /// and each material's bias, and the figure's own frame turns that back into the kit's shade. (2)
    /// THE TONE: kit rule 6 on worked cases. (3) THE TWIN: the shader's <c>HH_FIGURE</c> line and the C#
    /// are one expression, and the shader's tables are as wide as the def's cap. What none of this can
    /// say is what the GPU draws: the plate against the kit's goldens is owed to an editor slot.</para>
    /// </summary>
    public sealed class IsoFacetFigureToneTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacet.shader";
        const string TonePath = "Assets/_Project/Code/Art/IsoFacetFigureTone.cs";

        // The kit's globals (shading.v9.json "globals"), as TEST data. A def carries them as data
        // (CharacterSkinDef.KeyScreen, Form, FormMid); no C# outside the tests spells them.
        static readonly Vector3 Key = new Vector3(0f, 0.8106792283998809f, 0.5854905538443586f);
        const float Form = 0.5f;
        const float FormMid = 0.45f;
        const double ElevationDeg = 40.0;   // the kit's camera.elev_deg, and CharacterSkinDef's default

        // ==== 1. THE FOLD ===========================================================================

        [Test]
        public void TheFoldedBias_PinsTheKitsTwoToneClasses()
        {
            // bias' = bias − gain·form·formMid, where gain·form·formMid = 2.4 · 0.5 · 0.45 = 0.54.
            //   T6 (gain 2.4, bias 2.7): 2.7 − 0.54 = 2.16
            //   T5 (gain 2.4, bias 1.8): 1.8 − 0.54 = 1.26
            Assert.AreEqual(2.16f, IsoFacetFigureTone.FoldBias(2.4f, 2.7f, Form, FormMid), 1e-5f, "T6");
            Assert.AreEqual(1.26f, IsoFacetFigureTone.FoldBias(2.4f, 1.8f, Form, FormMid), 1e-5f, "T5");
        }

        [Test]
        public void TheFoldedKey_IsTheKeyWithFormOnToward_NotUnitLength_NotTurned()
        {
            // LN' = (k0, k1, k2 + form) = (0, 0.8106792, 0.5854906 + 0.5) = (0, 0.8106792, 1.0854906).
            // |LN'| = sqrt(0.6572008 + 1.1782897) = sqrt(1.8354905) = 1.3548.
            Vector3 ln = IsoFacetFigureTone.FoldLight(Key, Form);

            Assert.AreEqual(0f, ln.x, 1e-6f, "LN'.x");
            Assert.AreEqual(0.8106792f, ln.y, 1e-6f,
                "LN'.y is the key's own up. A fold turned by the elevation mixes y and z here (it would " +
                "read 0.8106792·sin 40° − 1.0854906·cos 40° = −0.31), and counts the figure's tilt twice.");
            Assert.AreEqual(1.0854906f, ln.z, 1e-6f, "LN'.z");
            Assert.AreEqual(1.3548f, ln.magnitude, 1e-4f,
                "|LN'| — NOT unit length, on purpose: ShaderLightVector only negates z, the shader never " +
                "normalises _LN, and the form term lives in the extra length.");
        }

        [Test]
        public void TheFold_ThroughTheFiguresOwnFrame_IsTheKitsShade()
        {
            // The figure's facet child wears HullRotation(dir, e) and HullScale, which compose to
            // RigToWorld(dir, e) (IsoFacetMath's class doc), and the shader takes sh = dot(normalize(M·n),
            // _LN). The kit takes the camera-space normal c — the rig normal after the heading turn only —
            // and s = c.x·k0 + up·k1 + toward·k2 + form·(toward − formMid), with up = c.y·se + c.z·ce and
            // toward = −c.y·ce + c.z·se (kit rules 3 and 5). At elevation 0, RigToWorld's rows are c's
            // (x, z, y), which reads c out of the same heading turn without re-deriving it here.
            Vector4 v = IsoFacetMath.ShaderLightVector(IsoFacetFigureTone.FoldLight(Key, Form));
            var light = new Vector3(v.x, v.y, v.z);

            double e = ElevationDeg * Math.PI / 180.0, se = Math.Sin(e), ce = Math.Cos(e);
            // The control: the same fold turned into the rig frame at e, the charter's first reading.
            float k2 = Key.z + Form;
            Vector4 t = IsoFacetMath.ShaderLightVector(new Vector3(
                Key.x, (float)(Key.y * se - k2 * ce), (float)(Key.y * ce + k2 * se)));
            var turned = new Vector3(t.x, t.y, t.z);
            double worstTurned = 0.0;

            Vector3[] normals =
            {
                new Vector3(0f, -1f, 0f),                          // level, toward the viewer at dir 0
                new Vector3(0f, 0f, 1f),                           // straight up: a hat top
                new Vector3(1f, 0f, 0f),                           // the right side
                new Vector3(0f, (float)-ce, (float)se),            // square to the eye
                new Vector3(0.3f, -0.5f, 0.8f).normalized,         // oblique, lit
                new Vector3(-0.6f, 0.2f, 0.77f).normalized,        // oblique, away from the key
            };

            foreach (double dir in new[] { 0.0, 1.0, 2.5, 4.0, 6.75 })
            {
                Matrix4x4 frame = IsoFacetMath.RigToWorld(dir, ElevationDeg);
                Matrix4x4 level = IsoFacetMath.RigToWorld(dir, 0.0);
                foreach (Vector3 n in normals)
                {
                    Vector3 w0 = level.MultiplyVector(n);
                    double cx = w0.x, cy = w0.z, cz = w0.y;
                    double toward = -cy * ce + cz * se, up = cy * se + cz * ce;
                    double s = cx * Key.x + up * Key.y + toward * Key.z + Form * (toward - FormMid);

                    Vector3 wn = frame.MultiplyVector(n).normalized;
                    double sh = Vector3.Dot(wn, light);
                    Assert.AreEqual(s, sh - Form * FormMid, 1e-5,
                        $"dir {dir}, rig normal {n}: dot(M·n, ShaderLightVector(LN')) − form·formMid is not the " +
                        "kit's s. The fold and the figure's frame disagree about the basis the key lives in.");

                    worstTurned = Math.Max(worstTurned, Math.Abs(Vector3.Dot(wn, turned) - Form * FormMid - s));
                }
            }

            Assert.Greater(worstTurned, 0.1,
                "the control: a fold ALSO turned by the elevation must miss the kit's s somewhere in this set, " +
                "or the normals above cannot tell a right fold from a wrong one");
        }

        // ==== 2. THE TONE ===========================================================================
        //
        // Kit rule 6: tone = min(len − 1, max(0, clamp(round(s·gain + bias + b), lo, hi) + off)), with
        // round(x) = floor(x + 0.5) (kit rule 7) and s first rounded to 1e-9 (kit rule 5). Tone takes the
        // kit's own, unfolded s and bias. The kit's classes: T6 gain 2.4, bias 2.7, lo 2, hi 5 on a
        // six-colour ramp; T5 gain 2.4, bias 1.8, lo 1, hi 4 on a five-colour ramp.

        // Window A: gain 2, bias 0.5, lo −4, hi 4, off +4 on a nine-colour ramp. It is wider than any def
        // may carry (IsUsable refuses lo < 0), on purpose: here the round shows through both clamps, so a
        // half-to-even or a half-away-from-zero round fails where the kit's own windows would hide it.
        static int WindowA(double s) => IsoFacetFigureTone.Tone(s, 2.0, 0.5, 0.0, 9, 4, -4, 4);

        [Test]
        public void HalvesRoundUp_OnBothSigns()
        {
            //   s =  1.0:  2·1 + 0.5    =  2.5 → floor( 3.0) =  3 → +4 = 7   (half-to-even: 2 → 6)
            //   s = −1.5:  2·−1.5 + 0.5 = −2.5 → floor(−2.0) = −2 → +4 = 2   (half-away-from-zero: −3 → 1)
            //   s = −2.0:  2·−2 + 0.5   = −3.5 → floor(−3.0) = −3 → +4 = 1   (either of those: −4 → 0)
            Assert.AreEqual(7, WindowA(1.0), "+2.5 must round up, to 3");
            Assert.AreEqual(2, WindowA(-1.5), "−2.5 must round up, to −2");
            Assert.AreEqual(1, WindowA(-2.0), "−3.5 must round up, to −3");
        }

        [Test]
        public void TheShadeIsRoundedToTheNanoFirst()
        {
            // s = 1 − 4e-10 is exactly 1 after round(s·1e9)/1e9, so 2·1 + 0.5 = 2.5 → 3 → +4 = 7.
            // Unrounded it is 2.4999999992 → floor(2.9999999992) = 2 → +4 = 6.
            Assert.AreEqual(7, WindowA(1.0 - 4e-10),
                "kit rule 5 rounds s to 1e-9 BEFORE the tone; without it this face drops a tone");
        }

        [Test]
        public void ABandMinusOneFace_SitsOneToneDown()
        {
            // T6 skin (len 6, off 0), s = 0.5:
            //   b =  0: 2.4·0.5 + 2.7     = 3.9 → floor(4.4) = 4 → clamp 2..5 = 4 → +0 = 4
            //   b = −1: 2.4·0.5 + 2.7 − 1 = 2.9 → floor(3.4) = 3 → clamp 2..5 = 3 → +0 = 3
            Assert.AreEqual(4, IsoFacetFigureTone.Tone(0.5, 2.4, 2.7, 0.0, 6, 0, 2, 5), "b = 0");
            Assert.AreEqual(3, IsoFacetFigureTone.Tone(0.5, 2.4, 2.7, -1.0, 6, 0, 2, 5), "b = −1");
        }

        [Test]
        public void TheWindowClamps_FromBelowAndFromAbove()
        {
            // T5 boot (len 5, off 0), window 1..4:
            //   s = −1:  2.4·−1 + 1.8  = −0.6 → floor(−0.1) = −1 → clamp 1..4 = 1   (from below)
            //   s = 1.5: 2.4·1.5 + 1.8 =  5.4 → floor( 5.9) =  5 → clamp 1..4 = 4   (from above)
            //   s = 0.5: 2.4·0.5 + 1.8 =  3.0 → floor( 3.5) =  3 → inside the window: 3
            Assert.AreEqual(1, IsoFacetFigureTone.Tone(-1.0, 2.4, 1.8, 0.0, 5, 0, 1, 4), "below lo");
            Assert.AreEqual(4, IsoFacetFigureTone.Tone(1.5, 2.4, 1.8, 0.0, 5, 0, 1, 4), "above hi");
            Assert.AreEqual(3, IsoFacetFigureTone.Tone(0.5, 2.4, 1.8, 0.0, 5, 0, 1, 4), "inside");
        }

        [Test]
        public void TheRampLengthClamps_AfterANonZeroOffset()
        {
            // T6 collar (len 6, off +1), s = 1.5: 2.4·1.5 + 2.7 = 6.3 → 6 → clamp 2..5 = 5 → +1 = 6
            //   → min(len − 1, 6) = 5
            // T5 sole (len 5, off −2), s = −1: −0.6 → −1 → clamp 1..4 = 1 → −2 = −1 → max(0, −1) = 0
            Assert.AreEqual(5, IsoFacetFigureTone.Tone(1.5, 2.4, 2.7, 0.0, 6, 1, 2, 5), "collar, past the top");
            Assert.AreEqual(0, IsoFacetFigureTone.Tone(-1.0, 2.4, 1.8, 0.0, 5, -2, 1, 4), "sole, past the bottom");
        }

        [Test]
        public void AOneColourFixedMaterial_IsToneZero_WhateverTheShade()
        {
            // Kit rule 6: "Fixed material: tone 0 (its one colour)". As data it is a one-colour ramp with
            // lo = hi = 0 and off 0 (CharacterSkinDef.Material.ToneLo), and the same formula lands on 0.
            // With T6's gain and bias:
            //   s = 2:            7.5 → 8 → clamp 0..0 = 0 → 0
            //   s = −2:          −2.1 → −2 → clamp 0..0 = 0 → 0
            //   s = 0.3, b = −1:  2.42 → 2 → clamp 0..0 = 0 → 0
            Assert.AreEqual(0, IsoFacetFigureTone.Tone(2.0, 2.4, 2.7, 0.0, 1, 0, 0, 0), "s = 2");
            Assert.AreEqual(0, IsoFacetFigureTone.Tone(-2.0, 2.4, 2.7, 0.0, 1, 0, 0, 0), "s = −2");
            Assert.AreEqual(0, IsoFacetFigureTone.Tone(0.3, 2.4, 2.7, -1.0, 1, 0, 0, 0), "s = 0.3, b = −1");
        }

        // ==== 3. THE TWIN ===========================================================================

        [Test]
        public void TheShadersV9ToneLine_IsTheCSharpTwin()
        {
            string csharp = Twin(Read(TonePath), "IsoFacetFigureTone.cs");
            string hlsl = Normalize(Twin(Read(ShaderPath), "HiddenHarboursIsoFacet.shader"));

            StringAssert.Contains("floor(fidx+0.5)", hlsl,
                "the shader's TWIN A block no longer rounds half up (kit rule 7), or it is empty");
            Assert.AreEqual(hlsl, NormalizeCSharp(csharp),
                "The v9 tone has drifted between HiddenHarboursIsoFacet.shader (HH_FIGURE) and " +
                "IsoFacetFigureTone.Tone. The shader draws it and the tests pin it here, so two halves of " +
                "one rule are exactly the shape that drifts silently. Move BOTH in the same commit.");

            // The control: this compare can see a drift. Dropping the half from the C# side must fail it.
            string drifted = csharp.Replace("fidx + 0.5", "fidx");
            Assert.AreNotEqual(csharp, drifted, "the control changed nothing, so it proves nothing");
            Assert.AreNotEqual(hlsl, NormalizeCSharp(drifted), "the normalised compare cannot see a drift");
        }

        [Test]
        public void EachHalfOfTheTwin_NamesTheOther()
        {
            StringAssert.Contains("VERBATIM in IsoFacetFigureTone.cs", BeginLine(Read(ShaderPath)),
                "the shader's TWIN A marker must say where its other half lives");
            StringAssert.Contains("VERBATIM in HiddenHarboursIsoFacet.shader", BeginLine(Read(TonePath)),
                "the C# TWIN A marker must say where its other half lives");
        }

        [Test]
        public void TheShadersFigureTables_AreAsWideAsTheV9Cap()
        {
            string code = StripComments(Read(ShaderPath));
            foreach (string table in new[] { "_RampMetaFigure", "_RampToneFigure" })
            {
                Match m = Regex.Match(code, @"float4\s+" + table + @"\s*\[\s*(\d+)\s*\]");
                Assert.IsTrue(m.Success, $"the shader declares no float4 {table}[]");
                Assert.AreEqual(CharacterSkinDef.V9RampSlots, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                    $"{table} is not CharacterSkinDef.V9RampSlots wide. The renderer writes V9RampSlots entries " +
                    "and Unity fixes an array's size at its first set, so the two must be one number.");
            }
        }

        // ---- helpers ---------------------------------------------------------------------------

        const string Begin = "// ==== TWIN A (begin)";
        const string End = "// ==== TWIN A (end) ====";

        static string Read(string projectRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", projectRelative);
            Assert.IsTrue(File.Exists(path), $"Not found: {projectRelative}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        static string BeginLine(string src)
        {
            int i = src.IndexOf(Begin, StringComparison.Ordinal);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), "no TWIN A begin marker");
            int eol = src.IndexOf('\n', i);
            return eol < 0 ? src.Substring(i) : src.Substring(i, eol - i);
        }

        /// <summary>The text between the TWIN A markers, which must each appear exactly once.</summary>
        static string Twin(string src, string where)
        {
            int i = src.IndexOf(Begin, StringComparison.Ordinal);
            Assert.That(i, Is.GreaterThanOrEqualTo(0), $"TWIN A has no begin marker in {where}");
            Assert.That(src.IndexOf(Begin, i + 1, StringComparison.Ordinal), Is.LessThan(0),
                $"TWIN A begins twice in {where}");
            i = src.IndexOf('\n', i) + 1;
            int j = src.IndexOf(End, i, StringComparison.Ordinal);
            Assert.That(j, Is.GreaterThan(i), $"TWIN A has no end marker in {where}");
            j = src.LastIndexOf('\n', j) + 1;
            return src.Substring(i, j - i);
        }

        static string StripComments(string source)
        {
            string s = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(s, @"//[^\n]*", " ");
        }

        /// <summary>Comments and whitespace out, lower case: a real difference in the rule fails, a
        /// difference in layout does not. Every name must match.</summary>
        static string Normalize(string body) =>
            Regex.Replace(StripComments(body), @"\s+", "").ToLowerInvariant();

        /// <summary>The one spelling the C# needs changed: <c>Math.Floor</c> is HLSL's <c>floor</c>. The
        /// C# <c>Clamp</c> is a private helper with HLSL's <c>clamp</c> semantics; lower-casing maps its
        /// NAME, and the tone rows above pin its behaviour. Applied to the C# alone, so it can never
        /// cancel itself out.</summary>
        static string NormalizeCSharp(string body) => Normalize(body.Replace("Math.Floor", "floor"));
    }
}
