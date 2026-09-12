using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The guards for THE WAKE LIFT (water PR F, register row 27).
    ///
    /// <para>The owner, 2026-09-11: <i>"i do want the wake to lift the water and create visual
    /// waves."</i></para>
    ///
    /// <para>🔴 The lift is <b>DRAWN ONLY</b>. It is added inside <c>vertDisplaced</c> and reaches nothing
    /// else — not <c>DisplacedSea</c>, not <c>DisplacedSeaState</c>, nothing any hull, deck rider or force
    /// path can read. Whether other boats RIDE a wash is a simulation question under ADR 0018 (one sea,
    /// one force path) and needs its own PR and the owner's word. <see cref="TheLift_IsDrawnOnly_AndNothingInCoreKnowsItsName"/>
    /// is that fence, machine-checked, so the next lane cannot hook it up by accident.</para>
    ///
    /// <para>Three kinds of guard live here, and they are deliberately different kinds:</para>
    /// <list type="number">
    /// <item>THE LAW — values, exact passthroughs, symmetry, determinism, and the two places the wake
    /// touches physics the project already ships (the sea's own dispersion relation, and the Kelvin
    /// half-angle the foam edge already spreads at).</item>
    /// <item>THE TWIN — the six functions of the law exist twice, in C# and in HLSL. These scrape BOTH
    /// files and compare the bodies after nothing but a declared language mapping. A twin compared against
    /// itself is worthless (this repo has paid for one), so the comparison is always source against
    /// source, and every mapped CONSTANT's value is pinned separately by parsing the shader's own literal
    /// — a rename cannot hide a changed number, and a changed number cannot hide behind a rename.</item>
    /// <item>THE PLUMBING — the dials are declared in both halves of the shader, the passthrough
    /// short-circuits before a slot is read, the lift enters the vertex stage beside the swell and nowhere
    /// else, and the registry's slot pool claims, publishes, releases and refuses at its cap.</item>
    /// </list>
    ///
    /// <para>CPU-only by construction: no GPU, no render, no device — so CI adjudicates every one of them.
    /// The photograph (surface elevation across the track, in world metres, lift arm vs dial-0 arm) is the
    /// acceptance instrument and lives in <c>WakeCentrePhotographPlayTests</c>; these are what keep it
    /// honest between plates.</para>
    /// </summary>
    public class WakeLiftMathTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string TwinPath = "Assets/_Project/Code/Art/WakeLiftMath.cs";
        const string FoamPath = "Assets/_Project/Code/Art/FoamBuffer.cs";
        const string CoreDir = "Assets/_Project/Code/Core";

        // A working boat's numbers: the shipped amplitude dial, the shipped decay, a cape islander's
        // churned half-beam, and the wavelength that keeps station at a 6 m/s passage speed.
        const float Amp = 0.25f;
        const float Decay = 20f;
        const float Beam = 1.1f;
        const float Speed = 6f;
        static readonly float Lambda = WakeLiftMath.TransverseWavelength(Speed);

        static string Read(string projectRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", projectRelative);
            Assert.IsTrue(File.Exists(path), $"Not found: {projectRelative}");
            return File.ReadAllText(path);
        }

        static float Train(float astern, float lateral)
        {
            return WakeLiftMath.TrainHeight(astern, lateral, Amp, Lambda, Beam, Decay);
        }

        // ==== 1. THE LAW =============================================================================

        /// <summary>
        /// The train is not a look: its wavelength is the one that KEEPS STATION at the hull's speed,
        /// which is the sea's own deep-water dispersion relation read backwards. If this ever drifts, the
        /// wake has stopped being the same water as the swell — and the round trip is exact, so there is
        /// no judgement call in the failure.
        /// </summary>
        [Test]
        public void TransverseWavelength_IsTheInverseOfTheSeasOwnDispersionRelation()
        {
            foreach (float v in new[] { 0.5f, 1f, 2f, 3.5f, 6f, 9f, 12f })
            {
                float lambda = WakeLiftMath.TransverseWavelength(v);
                float back = WaterDispersion.DeepPhaseSpeed(lambda);
                Assert.AreEqual(v, back, 1e-3f,
                    $"A wave of {lambda:F3} m travels at {back:F4} m/s, not the {v} m/s hull it is " +
                    "supposed to keep station with. TransverseWavelength and " +
                    "WaterDispersion.DeepPhaseSpeed are exact inverses — one of them has moved.");
            }
        }

        /// <summary>At rest the wavelength is EXACTLY zero, which is one of the shader's two per-slot
        /// skips: a boat lying to her mooring draws no train and costs nothing to skip.</summary>
        [Test]
        public void TransverseWavelength_AtRestAndBelow_IsExactlyZero()
        {
            Assert.AreEqual(0f, WakeLiftMath.TransverseWavelength(0f),
                "At rest the wavelength must be EXACTLY 0, not nearly 0 — the at-rest arm of the " +
                "acceptance plate is an equality, and the shader's slot skip is `shape.y <= 0`.");
            Assert.AreEqual(0f, WakeLiftMath.TransverseWavelength(-3f),
                "Sternway is still no way on for this purpose; a negative speed must read as 0.");
        }

        /// <summary>
        /// The divergent arms sit at the STATIONARY-PHASE maximum, and that is checkable rather than
        /// asserted: theta_c = acos(sqrt(2/3)) is the angle that maximises the wedge half-angle
        /// beta = atan(sin.cos/(1+sin^2)), whose tangent is the Kelvin slope this project already ships in
        /// FoamBuffer — so the lift's wedge and the foam's wedge are the same wedge, by derivation.
        /// </summary>
        [Test]
        public void TheDivergentMember_IsTheStationaryPhaseMaximum()
        {
            Assert.AreEqual(35.264390f, WakeLiftMath.DivergentNormalDegrees, 1e-3f,
                "theta_c must be acos(sqrt(2/3)) = 35.2644 deg — the crest normal that keeps station " +
                "at the widest angle.");

            float ratio = 1f / (WakeLiftMath.DivergentCos * WakeLiftMath.DivergentCos);
            Assert.AreEqual(ratio, WakeLiftMath.DivergentWavenumberRatio, 1e-5f,
                "The divergent wavenumber is k0/cos^2(theta_c), which at theta_c is 3/2 EXACTLY. It is a " +
                "derivation, not a dial, and 1.5 is why no dial appears for it.");

            float sin = WakeLiftMath.DivergentSin, cos = WakeLiftMath.DivergentCos;
            float kelvinFromTheory = sin * cos / (1f + sin * sin);
            Assert.That(Mathf.Abs(kelvinFromTheory - FoamBuffer.KelvinSlope) / kelvinFromTheory,
                Is.LessThan(0.005f),
                $"The Kelvin half-angle derived at theta_c is {Mathf.Atan(kelvinFromTheory) * Mathf.Rad2Deg:F4} " +
                $"deg (slope {kelvinFromTheory:F6}); FoamBuffer.KelvinSlope ships " +
                $"{FoamBuffer.KelvinSlope:F6} (tan 19.5 deg). They must stay within half a percent, or " +
                "the lift's wedge and the foam's wedge have parted company and the churn will show a " +
                "lifted edge outside the foam that made it.");
        }

        /// <summary>
        /// ⚠️ THE PASSTHROUGH, all four of it. Each is an EXACT zero rather than a small number — the
        /// charter's requirement is a dial-0 arm that is bit-identical to no feature at all, and an
        /// arithmetic near-zero cannot deliver that.
        /// </summary>
        [Test]
        public void EveryDialAtZero_IsAnExactPassthrough()
        {
            Assert.AreEqual(0f, WakeLiftMath.TrainHeight(4f, 0.5f, 0f, Lambda, Beam, Decay),
                "Amplitude 0 must be an EXACT 0 — this is the dial the owner turns down to nothing.");
            Assert.AreEqual(0f, WakeLiftMath.TrainHeight(4f, 0.5f, Amp, Lambda, Beam, 0f),
                "Decay 0 must be an EXACT 0: an e-folding length of nothing is a train that never " +
                "leaves the transom, which is indistinguishable from no train.");
            Assert.AreEqual(0f, WakeLiftMath.TrainHeight(4f, 0.5f, Amp, 0f, Beam, Decay),
                "Wavelength 0 is a hull at rest and must be an EXACT 0 — the at-rest plate arm.");
            Assert.AreEqual(0f, WakeLiftMath.TrainHeight(4f, 0.5f, Amp, Lambda, 0f, Decay),
                "A hull with no beam churns nothing and must be an EXACT 0 (it is also the guard that " +
                "keeps the wedge from collapsing to a division by nothing).");
        }

        /// <summary>At her transom, on her track, both members are at cos 0 and the two halves add back
        /// to the whole dial — so the number the owner types IS the crest height a plate measures.</summary>
        [Test]
        public void AtTheTransomOnHerTrack_TheTrainIsTheWholeDialledAmplitude()
        {
            Assert.AreEqual(Amp, Train(0f, 0f), 1e-6f,
                "At the transom the root ramp is 1, the wedge window is 1, the decay is 1 and both " +
                "members are at cos 0, so the height must be exactly the dial. If the two members were " +
                "not sharing one amplitude (MemberShare 0.5) this would read double, and the dial's " +
                "label would be a lie.");
        }

        /// <summary>Nothing stands AHEAD of her transom: the root ramp is the curve read backwards over
        /// her own half-beam, so the sea is continuous everywhere it is visible and there is no step.</summary>
        [Test]
        public void AheadOfHerTransom_NothingStands()
        {
            Assert.AreEqual(0f, Train(-Beam, 0f),
                "A full half-beam ahead of the transom the ramp must be an EXACT 0.");
            Assert.AreEqual(0f, Train(-40f, 0f),
                "Far ahead of the bow there must be no train at all — a lift under the bow would be a " +
                "second, unasked-for bow wave.");

            // Continuity: no step anywhere across the root, at 1 cm resolution.
            float previous = 0f;
            for (float s = -Beam - 0.5f; s <= 2f; s += 0.01f)
            {
                float h = Train(s, 0f);
                Assert.That(Mathf.Abs(h - previous), Is.LessThan(0.05f * Amp),
                    $"A step of {Mathf.Abs(h - previous):F4} m appeared at {s:F2} m astern. The root " +
                    "ramp exists precisely so the transom line is not a visible seam in the sea.");
                previous = h;
            }
        }

        /// <summary>Outside the Kelvin wedge the sea is bare — the lift can never reach where the foam
        /// that made it cannot.</summary>
        [Test]
        public void OutsideTheKelvinWedge_TheSeaIsBare()
        {
            foreach (float astern in new[] { 0f, 5f, 20f, 60f })
            {
                float edge = WakeLiftMath.HalfWidth(astern, Beam);
                Assert.AreEqual(0f, Train(astern, edge),
                    $"At the caustic itself ({astern} m astern, {edge:F2} m out) the window must close " +
                    "to an EXACT 0.");
                Assert.AreEqual(0f, Train(astern, edge + 5f),
                    "Five metres outside the wedge there must be nothing at all.");
                Assert.That(Mathf.Abs(Train(astern, edge * 0.4f)), Is.GreaterThan(0f),
                    "Well inside the wedge there must be SOMETHING, or the window has closed early and " +
                    "the whole train is invisible.");
            }
        }

        /// <summary>The amplitude never exceeds its dial, and its envelope falls astern at the dialled
        /// e-folding length. This is what makes "0.25 m at the transom, gone by 80 m" a true sentence.</summary>
        [Test]
        public void TheEnvelope_NeverExceedsTheDialAndFallsAstern()
        {
            for (float s = 0f; s <= 120f; s += 0.25f)
            {
                float bound = Amp * WakeLiftMath.Fall(s, Decay) + 1e-6f;
                for (float y = 0f; y <= 40f; y += 0.25f)
                {
                    float h = Train(s, y);
                    Assert.That(Mathf.Abs(h), Is.LessThanOrEqualTo(bound),
                        $"At {s:F2} m astern, {y:F2} m off the track, the lift is {h:F4} m — outside its " +
                        $"own envelope of {bound:F4} m. The dial has stopped meaning what it says.");
                }
            }

            Assert.AreEqual(1f, WakeLiftMath.Fall(0f, Decay), 1e-6f, "Full amplitude at the transom.");
            Assert.AreEqual(1f / Mathf.Exp(1f), WakeLiftMath.Fall(Decay, Decay), 1e-6f,
                "One e-folding length astern the train must be down to 1/e — the dial's own definition.");
            Assert.That(WakeLiftMath.Fall(4f * Decay, Decay), Is.LessThan(0.02f),
                "Four e-folding lengths astern the wake must be visually gone.");
        }

        /// <summary>The wake is symmetric about her track — both arms, always, in both frames.</summary>
        [Test]
        public void TheTrain_IsSymmetricAboutHerTrack()
        {
            for (float s = 0f; s <= 60f; s += 1.3f)
            {
                for (float y = 0.1f; y <= 12f; y += 0.7f)
                {
                    Assert.AreEqual(Train(s, y), Train(s, -y), 1e-7f,
                        "The two arms of a wake are mirror images. A one-sided train is the row-38 " +
                        "off-centre defect all over again, this time in geometry.");
                }
            }

            Vector2 heading = new Vector2(0.6f, 0.8f);   // unit, off-axis on purpose
            Vector2 root = new Vector2(-12f, 37f);
            Vector2 across = new Vector2(heading.y, -heading.x);
            Vector2 back = -heading * 18f;
            float port = WakeLiftMath.HeightAt(root + back + across * 4f, root, heading,
                                               Amp, Lambda, Beam, Decay);
            float starboard = WakeLiftMath.HeightAt(root + back - across * 4f, root, heading,
                                                    Amp, Lambda, Beam, Decay);
            Assert.AreEqual(port, starboard, 1e-6f,
                "In the WORLD frame too: the same distance either side of her track is the same height.");
        }

        /// <summary>The train lies ASTERN of her heading, not ahead of it and not off to one side — the
        /// frame change is where a sign error would hide, and a sign error here is a wake that runs
        /// ahead of the boat.</summary>
        [Test]
        public void HeightAt_PutsTheTrainAsternOfHerHeading()
        {
            Vector2 root = Vector2.zero;
            Vector2 heading = Vector2.up;

            Assert.That(Mathf.Abs(WakeLiftMath.HeightAt(new Vector2(0f, -6f), root, heading,
                                                        Amp, Lambda, Beam, Decay)),
                Is.GreaterThan(0f), "Six metres BEHIND her there must be a train.");
            Assert.AreEqual(0f, WakeLiftMath.HeightAt(new Vector2(0f, 6f), root, heading,
                                                      Amp, Lambda, Beam, Decay),
                "Six metres AHEAD of her there must be nothing. A sign slip here draws the wake in " +
                "front of the boat, which is the one defect a plate would catch late and an owner " +
                "immediately.");

            // The same point, with her turned 90 degrees, must be bare; turned to face it, lifted.
            Vector2 point = new Vector2(0f, -6f);
            Assert.AreEqual(0f, WakeLiftMath.HeightAt(point, root, Vector2.right,
                                                      Amp, Lambda, Beam, Decay),
                "Steaming east, the sea six metres SOUTH of her is outside her wedge.");
            Assert.That(Mathf.Abs(WakeLiftMath.HeightAt(point, root, Vector2.down,
                                                        Amp, Lambda, Beam, Decay)),
                Is.GreaterThan(0f), "Steaming south, that same sea is her own wake.");
        }

        /// <summary>Rule 5: recomputed every frame from published state, never saved and never random.
        /// Same inputs, same bits — and no RNG anywhere in the law's own source.</summary>
        [Test]
        public void TheLaw_IsDeterministic_AndCarriesNoRandomness()
        {
            for (int i = 0; i < 64; i++)
            {
                float s = i * 0.77f, y = i * 0.31f;
                Assert.AreEqual(Train(s, y), Train(s, y),
                    "Bit-for-bit determinism: the same point must give the same height, every call.");
            }

            string source = StripComments(Read(TwinPath));
            foreach (string banned in new[] { "Random", "Time.time", "Time.deltaTime", "DateTime" })
            {
                Assert.IsFalse(source.Contains(banned),
                    $"'{banned}' appears in WakeLiftMath. The wake is a pure function of published " +
                    "state (rule 5) — a clock or an RNG inside the law would make the plate " +
                    "unrepeatable and the sea undeterministic.");
            }
        }

        // ==== 2. THE FENCE ===========================================================================

        /// <summary>
        /// 🔴 THE FENCE, machine-checked. The charter: <i>"the ride is NOT touched — whether other hulls
        /// ride the wash is a SIM change (ADR 0018 one-sea rule, one force path law) needing its own PR
        /// and a helm verdict."</i> Nothing in Core may so much as know the lift's name, and the law
        /// itself must not reach the <c>DisplacedSea</c> seam the ride reads.
        /// </summary>
        [Test]
        public void TheLift_IsDrawnOnly_AndNothingInCoreKnowsItsName()
        {
            string lawCode = StripComments(Read(TwinPath));
            Assert.IsFalse(lawCode.Contains("DisplacedSea"),
                "WakeLiftMath references DisplacedSea. That seam is what BoatWaveMotion and the deck " +
                "riders read — putting the lift on it turns a drawn wave into a force, which is a " +
                "simulation change under ADR 0018 and needs its own PR and the owner's word.");

            string root = Path.Combine(Application.dataPath, "..", CoreDir);
            Assert.IsTrue(Directory.Exists(root), $"Not found: {CoreDir}");
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string text = StripComments(File.ReadAllText(file));
                if (text.Contains("WakeLift") || text.Contains("_HHWakeLift"))
                    offenders.Add(Path.GetFileName(file));
            }
            Assert.IsEmpty(offenders,
                "Core now names the wake lift (" + string.Join(", ", offenders) + "). The lift is DRAWN " +
                "ONLY: it lives in Art and in the water shader's vertex stage, and Core is precisely " +
                "where the sim could reach it. If the ride is meant to read a wash, that is a separate " +
                "PR with a helm verdict — not a reference that appeared quietly.");
        }

        // ==== 3. THE TWIN ============================================================================

        static string StripComments(string source)
        {
            string s = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(s, @"//[^\n]*", " ");
        }

        /// <summary>
        /// Reduce one function body to the shape both languages share, so a real difference in the MATHS
        /// fails and a difference in spelling does not. HLSL's <c>max</c>/<c>abs</c>/<c>cos</c>/<c>exp</c>
        /// are <c>Mathf.</c>'s, its literals carry no <c>f</c> suffix and often a redundant <c>.0</c>.
        /// Everything else — the comparisons, the knots, the divisors, the local NAMES — must match
        /// character for character after that.
        /// </summary>
        static string Normalize(string body)
        {
            string s = StripComments(body);
            s = s.Replace("Mathf.Max", "max").Replace("Mathf.Min", "min");
            s = s.Replace("Mathf.Abs", "abs").Replace("Mathf.Cos", "cos").Replace("Mathf.Exp", "exp");
            s = s.Replace("Mathf.Clamp01", "saturate");
            s = Regex.Replace(s, @"\s+", "");
            s = Regex.Replace(s, @"([0-9.])f(?![A-Za-z0-9_])", "$1");   // 1f -> 1, 1e-4f -> 1e-4
            s = Regex.Replace(s, @"(\d)\.0(?![0-9])", "$1");            // 1.0 -> 1, 2.0 -> 2
            return s.ToLowerInvariant();
        }

        /// <summary>
        /// The C# side additionally carries the names the HLSL spells differently: the two namespaced
        /// helpers, the Wake* prefix the shader needs to keep its global namespace legible, and the five
        /// constants. THESE ARE RENAMES ONLY — every one of the mapped constants has its VALUE pinned by
        /// <see cref="EveryShaderConstant_CarriesTheValueItMirrors"/>, so this mapping cannot launder a
        /// number. Applied to the C# alone, never to both, so it can never be self-cancelling.
        /// </summary>
        static string NormalizeCSharp(string body)
        {
            string s = body;
            s = s.Replace("2f * Mathf.PI", "HH_WAKE_TWO_PI");
            s = s.Replace("FoamBuffer.KelvinSlope", "HH_KELVIN_SLOPE");
            s = s.Replace("DivergentWavenumberRatio", "HH_WAKE_DIV_K_RATIO");
            s = s.Replace("DivergentCos", "HH_WAKE_DIV_COS");
            s = s.Replace("DivergentSin", "HH_WAKE_DIV_SIN");
            s = s.Replace("MemberShare", "HH_WAKE_MEMBER_SHARE");
            s = s.Replace("FoamBuffer.Profile(", "WakeProfile01(");
            s = s.Replace("HalfWidth(", "WakeHalfWidth(");
            s = s.Replace("RootRamp01(", "WakeRootRamp01(");
            s = s.Replace("Fall(", "WakeFall(");
            s = s.Replace("TrainHeight(", "WakeTrainHeight(");
            s = s.Replace("Vector2", "float2");
            return Normalize(s);
        }

        /// <summary>Pull one function's body (between its first { and its matching }) out of a source file.</summary>
        static string Body(string source, string signatureStart)
        {
            int at = source.IndexOf(signatureStart, StringComparison.Ordinal);
            Assert.Greater(at, -1,
                $"'{signatureStart}' is gone. It is one half of a twin seam — if the law moved, BOTH " +
                "halves move in the SAME commit, and this guard is how that is enforced.");
            int open = source.IndexOf('{', at);
            Assert.Greater(open, -1);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open + 1, i - open - 1);
                }
            }
            Assert.Fail($"Unbalanced braces after '{signatureStart}'.");
            return null;
        }

        static void AssertTwin(string csharpSignature, string csharpPath,
                               string hlslSignature, string what)
        {
            string csharp = Body(Read(csharpPath), csharpSignature);
            string hlsl = Body(Read(ShaderPath), hlslSignature);
            Assert.AreEqual(NormalizeCSharp(csharp), Normalize(hlsl),
                $"{what} has drifted between the C# and the HLSL. The wake lift exists twice by " +
                "necessity — the shader draws it and the tests (and the acceptance plate's predicted " +
                "profile) compute it — and two halves of one law is exactly the shape that drifts " +
                "silently. Move BOTH halves in the same commit.");
        }

        [Test]
        public void TheFalloffCurve_IsTranscribedLineForLine()
        {
            AssertTwin("public static float Profile(float distance, float halfWidth)", FoamPath,
                       "float WakeProfile01(float distance, float halfWidth)",
                       "The falloff curve (FoamBuffer.Profile / WakeProfile01)");
        }

        [Test]
        public void TheKelvinWedge_IsTranscribedLineForLine()
        {
            AssertTwin("public static float HalfWidth(float asternMetres, float halfBeamMetres)", TwinPath,
                       "float WakeHalfWidth(float asternMetres, float halfBeamMetres)",
                       "The wedge (WakeLiftMath.HalfWidth / WakeHalfWidth)");
        }

        [Test]
        public void TheDecayAstern_IsTranscribedLineForLine()
        {
            AssertTwin("public static float Fall(float asternMetres, float decayMetres)", TwinPath,
                       "float WakeFall(float asternMetres, float decayMetres)",
                       "The decay (WakeLiftMath.Fall / WakeFall)");
        }

        [Test]
        public void TheRootRamp_IsTranscribedLineForLine()
        {
            AssertTwin("public static float RootRamp01(float asternMetres, float halfBeamMetres)", TwinPath,
                       "float WakeRootRamp01(float asternMetres, float halfBeamMetres)",
                       "The root ramp (WakeLiftMath.RootRamp01 / WakeRootRamp01)");
        }

        [Test]
        public void TheWholeTrain_IsTranscribedLineForLine()
        {
            AssertTwin("public static float TrainHeight(float asternMetres, float lateralMetres,", TwinPath,
                       "float WakeTrainHeight(float asternMetres, float lateralMetres,",
                       "The train (WakeLiftMath.TrainHeight / WakeTrainHeight)");
        }

        [Test]
        public void TheFrameChange_IsTranscribedLineForLine()
        {
            AssertTwin("public static float HeightAt(Vector2 worldXY, Vector2 rootXY, Vector2 heading,", TwinPath,
                       "float WakeHeightAt(float2 worldXY, float2 rootXY, float2 heading,",
                       "The frame change (WakeLiftMath.HeightAt / WakeHeightAt)");
        }

        static float Define(string shader, string name)
        {
            Match m = Regex.Match(shader, @"#define\s+" + name + @"\s+([0-9.eE+\-]+)");
            Assert.IsTrue(m.Success,
                $"#define {name} is gone from the water shader. The twin guard maps a C# name onto it, " +
                "so its disappearance would silently stop pinning a number.");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// ⚠️ The other half of the twin guard: every constant the name-mapping papers over is pinned
        /// HERE, by parsing the shader's own literal and asserting it against the C# it mirrors. Without
        /// this, a rename mapping would let the shader carry a different number than the law.
        /// </summary>
        [Test]
        public void EveryShaderConstant_CarriesTheValueItMirrors()
        {
            string shader = Read(ShaderPath);

            Assert.AreEqual(2f * Mathf.PI, Define(shader, "HH_WAKE_TWO_PI"), 1e-6f,
                "HH_WAKE_TWO_PI stands in for `2f * Mathf.PI` in the twin comparison.");
            Assert.AreEqual(FoamBuffer.KelvinSlope, Define(shader, "HH_KELVIN_SLOPE"), 1e-6f,
                "HH_KELVIN_SLOPE stands in for FoamBuffer.KelvinSlope — the wedge the foam already " +
                "spreads at. A shader-side drift here widens the lift's wedge past the foam's.");
            Assert.AreEqual(WakeLiftMath.DivergentCos, Define(shader, "HH_WAKE_DIV_COS"), 1e-6f,
                "HH_WAKE_DIV_COS = cos(theta_c) = sqrt(2/3).");
            Assert.AreEqual(WakeLiftMath.DivergentSin, Define(shader, "HH_WAKE_DIV_SIN"), 1e-6f,
                "HH_WAKE_DIV_SIN = sin(theta_c) = sqrt(1/3).");
            Assert.AreEqual(WakeLiftMath.DivergentWavenumberRatio,
                            Define(shader, "HH_WAKE_DIV_K_RATIO"), 1e-6f,
                "HH_WAKE_DIV_K_RATIO = 1/cos^2(theta_c) = 3/2, exact.");
            Assert.AreEqual(WakeLiftMath.MemberShare, Define(shader, "HH_WAKE_MEMBER_SHARE"), 1e-6f,
                "HH_WAKE_MEMBER_SHARE is what makes the dial mean the CREST HEIGHT rather than half of " +
                "it: two members, one amplitude between them.");
        }

        /// <summary>
        /// ⚠️ The loop bound is COMPILE-TIME and equals the injector cap. It must never become a runtime
        /// uniform: an <c>[unroll]</c> over a runtime bound is a named magenta trap in this project
        /// (WaterShaderCompileGuardTests), and a bound smaller than the pool would silently drop the
        /// last boats' wakes.
        /// </summary>
        [Test]
        public void TheShadersLoopBound_IsFoamBuffersInjectorCap()
        {
            string shader = Read(ShaderPath);
            Match m = Regex.Match(shader, @"#define\s+HH_WAKE_LIFT_MAX\s+(\d+)");
            Assert.IsTrue(m.Success, "#define HH_WAKE_LIFT_MAX is gone from the water shader.");
            Assert.AreEqual(FoamBuffer.MaxInjectors, int.Parse(m.Groups[1].Value),
                "The shader's wake-lift slot count must equal FoamBuffer.MaxInjectors. Smaller and the " +
                "last hulls to join draw no wake at all; larger and it reads uninitialised slots.");

            Assert.IsTrue(shader.Contains("for (int i = 0; i < HH_WAKE_LIFT_MAX; i++)"),
                "The loop must be bounded by the #define, not by a uniform. A runtime bound under " +
                "[unroll] is the magenta trap this project has already paid for once.");
        }

        // ==== 4. THE PLUMBING ========================================================================

        /// <summary>Rule 6: the dials are owner-facing, named, and declared in BOTH halves of the shader.
        /// A property with no CBUFFER entry reads as 0 in a pass and is invisible until someone
        /// photographs it.</summary>
        [Test]
        public void BothDials_AreDeclaredInThePropertiesBlockAndTheCBuffer()
        {
            string shader = Read(ShaderPath);
            foreach (string dial in new[] { "_WakeLiftMetres", "_WakeLiftDecayMetres" })
            {
                Assert.That(Regex.Matches(shader, Regex.Escape(dial) + @"\s*\(""").Count, Is.EqualTo(1),
                    $"{dial} must appear exactly once in the Properties block, with a human label — " +
                    "rule 6, no silent look constants.");
                Assert.IsTrue(Regex.IsMatch(shader, @"float\s+" + Regex.Escape(dial) + @"\s*;"),
                    $"{dial} is in Properties but not in the CBUFFER. It would read 0 in every pass " +
                    "and the dial would do nothing — with no error anywhere.");
            }
        }

        /// <summary>
        /// ⚠️ THE BIT-EXACT PASSTHROUGH, made structural. Either dial at 0 returns before a single slot
        /// is read, exactly as the fetch march skips on <c>_WaveFetchParams.x == 0</c>. The dial-0 arm of
        /// the acceptance plate is an md5 equality against the unmodified sea, and only a structural
        /// early-out can deliver that.
        /// </summary>
        [Test]
        public void EitherDialAtZero_ShortCircuitsBeforeASlotIsRead()
        {
            string lift = Body(Read(ShaderPath), "float WakeLiftHeight(float2 worldXY)");
            string opens = Normalize(lift);
            string required =
                Normalize("if (_WakeLiftMetres <= 0.0 || _WakeLiftDecayMetres <= 0.0) return 0.0;");
            Assert.IsTrue(opens.StartsWith(required, StringComparison.Ordinal),
                "WakeLiftHeight must open with the two-dial early-out and nothing else, so a dial-0 " +
                "plate is bit-identical to no feature at all. It opens instead with: " +
                opens.Substring(0, Mathf.Min(140, opens.Length)));

            Assert.IsTrue(lift.Contains("if (shape.x <= 0.0 || shape.y <= 0.0) continue;"),
                "The per-slot skip is the OTHER exact zero: an unused slot, and a hull at rest whose " +
                "gate and wavelength both reach 0. Without it a moored boat would draw a train.");
        }

        /// <summary>
        /// The lift enters the sea in ONE place: beside the swell, in the same frame, on the same height,
        /// carrying the same shore fade — and the swell's own expression is untouched, operation for
        /// operation, which is the other half of the bit-exact passthrough.
        /// </summary>
        [Test]
        public void TheVertexStage_AddsTheLiftBesideTheSwell_AndNowhereElse()
        {
            string shader = Read(ShaderPath);

            Assert.IsTrue(shader.Contains("float lift = vHeight * _WaveExaggeration * fade;"),
                "The swell's displacement must stay the SAME expression it was (ShoreFade01 hoisted " +
                "into `fade`, nothing more). Any re-association here changes the sea at dial 0 and the " +
                "passthrough stops being bit-exact.");
            Assert.IsTrue(shader.Contains("lift += WakeLiftHeight(ground) * fade;"),
                "The lift must be ADDED to the same height the swell displaces, at the same ground " +
                "position, faded by the same shore seam — anything else tears the coast that " +
                "ShoreFade01 exists to keep whole.");

            string code = StripComments(shader);
            Assert.AreEqual(2, Regex.Matches(code, @"WakeLiftHeight\s*\(").Count,
                "WakeLiftHeight must appear exactly twice in code: its definition and the ONE call in " +
                "vertDisplaced. A second call site (the fragment stage, or the flat Universal2D pass) " +
                "would put the lift somewhere this PR has not measured and the charter has not fenced.");
            Assert.IsFalse(Regex.IsMatch(code, @"WakeLiftHeight[^;]*_WaveExaggeration"),
                "The amplitude must NOT be multiplied by _WaveExaggeration: exaggeration means 'draw " +
                "the simulated sea taller than it is', and this train has no simulated twin to be " +
                "taller than. The dial is in DRAWN metres, which is what makes a plate readable.");
        }

        // ==== 5. THE SABOTAGE ARM ====================================================================

        /// <summary>
        /// The acceptance VERDICT, exercised against a correct lift and against two wrong ones. The
        /// charter requires a sabotage arm; the trap it must avoid is a knob that feeds both sides, so
        /// the verdict function here is ONE function, applied unchanged to all three laws, and it is not
        /// wired into production at all — it is the same set of properties the plate fixture asserts, at
        /// a resolution CI can afford.
        /// </summary>
        static bool LiftVerdict(Func<float, float, float> law)
        {
            // (a) at her transom the sea must be lifted
            if (Mathf.Abs(law(0f, 0f)) < 0.5f * Amp) return false;
            // (b) nothing may stand ahead of her transom
            if (Mathf.Abs(law(-Beam, 0f)) > 1e-6f) return false;
            // (c) outside the Kelvin wedge the sea is bare
            float far = 40f;
            if (Mathf.Abs(law(far, WakeLiftMath.HalfWidth(far, Beam) + 1f)) > 1e-6f) return false;
            // (d) the train dies astern
            if (Mathf.Abs(law(4f * Decay, 0f)) > 0.05f * Amp) return false;
            return true;
        }

        [Test]
        public void TheAcceptanceVerdict_ReddensForAWrongSignedOrUnrootedLift()
        {
            float k0 = 2f * Mathf.PI / Lambda;
            Func<float, float, float> bare = (s, y) => Amp * WakeLiftMath.MemberShare *
                (Mathf.Cos(k0 * s) + Mathf.Cos(k0 * WakeLiftMath.DivergentWavenumberRatio *
                    (s * WakeLiftMath.DivergentCos + Mathf.Abs(y) * WakeLiftMath.DivergentSin)));

            Assert.IsTrue(LiftVerdict((s, y) => Train(s, y)),
                "The shipped law must PASS its own verdict, or the verdict is measuring something else.");

            Assert.IsFalse(LiftVerdict((s, y) => Train(-s, y)),
                "A WRONG-SIGNED lift — the train ahead of the boat instead of astern — must redden the " +
                "guard. This is the defect the frame change would produce and a photograph would " +
                "otherwise have to catch by eye.");

            Assert.IsFalse(LiftVerdict(bare),
                "An UNROOTED lift — the raw wave train with no wedge window and no root ramp — must " +
                "redden the guard. It is the same crests, everywhere on the sea: a swell, not a wake.");

            Assert.IsFalse(LiftVerdict((s, y) =>
                    WakeLiftMath.TrainHeight(s, y, Amp, Lambda, Beam, 1e6f)),
                "A lift that never DECAYS must redden the guard: a wake that reaches the horizon is the " +
                "'static lines' complaint of 2026-07-23 with amplitude added.");
        }

        // ==== 6. THE SLOT POOL =======================================================================

        /// <summary>
        /// The publish path is the one FoamInjector already owns — one slot per hull, every writer
        /// uploading the whole buffer (the GrassFootstep pattern), so the last writer's upload carries
        /// every writer's frame and no ordering is assumed. These walk the pool's lifecycle without an
        /// injector, and return it exactly as they found it.
        /// </summary>
        [Test]
        public void TheSlotPool_ClaimsReleasesAndRefusesAtItsCap()
        {
            var mine = new List<int>();
            try
            {
                for (int i = 0; i < FoamBuffer.MaxInjectors + 2; i++)
                {
                    int slot = FoamInjectionRegistry.ClaimLiftSlot();
                    if (slot < 0) break;
                    Assert.That(slot, Is.InRange(0, FoamBuffer.MaxInjectors - 1));
                    Assert.IsFalse(mine.Contains(slot),
                        "Two hulls were handed the SAME slot. Every writer must own its own, or one " +
                        "boat's wake overwrites another's every frame.");
                    mine.Add(slot);
                }

                Assert.AreEqual(FoamBuffer.MaxInjectors, mine.Count,
                    "The pool must hand out exactly MaxInjectors slots before it refuses.");
                Assert.AreEqual(-1, FoamInjectionRegistry.ClaimLiftSlot(),
                    "Past the cap the pool must return -1 and the injector must simply publish no lift " +
                    "— never wrap around onto another hull's slot, and never grow the array the shader " +
                    "unrolls over.");
            }
            finally
            {
                for (int i = 0; i < mine.Count; i++)
                {
                    int slot = mine[i];
                    FoamInjectionRegistry.ReleaseLiftSlot(ref slot);
                    Assert.AreEqual(-1, slot, "ReleaseLiftSlot must clear the caller's handle too.");
                }
            }

            int again = FoamInjectionRegistry.ClaimLiftSlot();
            Assert.That(again, Is.InRange(0, FoamBuffer.MaxInjectors - 1),
                "After releasing, the pool must hand slots out again — a leaked slot is a wake standing " +
                "on the sea with no boat under it.");
            FoamInjectionRegistry.ReleaseLiftSlot(ref again);
        }

        [Test]
        public void APublishedHull_ReachesTheGlobalArraysTheShaderReads()
        {
            int slot = FoamInjectionRegistry.ClaimLiftSlot();
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            try
            {
                var root = new Vector2(-14.5f, 62.25f);
                var heading = new Vector2(0.6f, 0.8f);
                FoamInjectionRegistry.PublishWakeLift(slot, root, heading, Lambda, Beam, 0.75f);

                var cpuRoot = new Vector4[FoamBuffer.MaxInjectors];
                var cpuShape = new Vector4[FoamBuffer.MaxInjectors];
                FoamInjectionRegistry.ReadWakeLift(cpuRoot, cpuShape);
                Assert.AreEqual(new Vector4(root.x, root.y, heading.x, heading.y), cpuRoot[slot],
                    "The transom and her heading must be published as they were given — the frame " +
                    "change in the shader assumes a UNIT heading and a world-metre root.");
                Assert.AreEqual(new Vector4(0.75f, Lambda, Beam, 0f), cpuShape[slot],
                    "The gate, the wavelength and the churned half-beam must arrive unmodified.");

                // ⚠️ The array reaching the GLOBAL is the thing the shader reads — a CPU array that
                // never uploads is exactly the "N identical plates" failure the charter warns about.
                Vector4[] uploaded = Shader.GetGlobalVectorArray(FoamShaderIds.WakeLiftShape);
                Assert.IsNotNull(uploaded,
                    "_HHWakeLiftShape never reached the global uniform state. Nothing would draw, and " +
                    "nothing would say so.");
                Assert.AreEqual(FoamBuffer.MaxInjectors, uploaded.Length,
                    "The whole buffer must upload, every publish: that is what lets every writer own a " +
                    "slot without assuming who uploads last.");
                Assert.AreEqual(0.75f, uploaded[slot].x, 1e-6f, "The gate must reach the shader.");
                Assert.AreEqual(Lambda, uploaded[slot].y, 1e-3f, "The wavelength must reach the shader.");
            }
            finally
            {
                FoamInjectionRegistry.ReleaseLiftSlot(ref slot);
            }
        }

        [Test]
        public void AReleasedSlot_IsZeroedSoNoWakeStandsWhereSheLeft()
        {
            int slot = FoamInjectionRegistry.ClaimLiftSlot();
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            FoamInjectionRegistry.PublishWakeLift(slot, new Vector2(3f, 4f), Vector2.up,
                                                 Lambda, Beam, 1f);
            int mine = slot;
            FoamInjectionRegistry.ReleaseLiftSlot(ref slot);

            var cpuRoot = new Vector4[FoamBuffer.MaxInjectors];
            var cpuShape = new Vector4[FoamBuffer.MaxInjectors];
            FoamInjectionRegistry.ReadWakeLift(cpuRoot, cpuShape);
            Assert.AreEqual(Vector4.zero, cpuShape[mine],
                "A released slot must be ZEROED and re-uploaded. Leaving the last frame's values in " +
                "place freezes a wake on the sea where the boat despawned — the frozen-last-frame trap " +
                "this project has paid for in the foam buffer already.");
        }
    }
}
