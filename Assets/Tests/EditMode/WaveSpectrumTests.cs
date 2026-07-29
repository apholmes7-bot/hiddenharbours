using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The JONSWAP spectrum (ADR 0027 #5, P2 PR B) — the three mechanisms behind the owner's three
    /// named symptoms, each pinned on its own, plus the properties the whole assembled field must
    /// keep.
    ///
    /// <para>The <b>blend-0 passthrough</b> is deliberately NOT re-proved here:
    /// <c>WaveSpectrumPassthroughTests</c> already asserts the shipped settings (which carry
    /// <c>SpectrumBlend = 0</c>) against a frozen copy of the pre-spectrum derivation across a full
    /// sweep. Duplicating it would make two places to update and one of them would rot.</para>
    /// </summary>
    public class WaveSpectrumTests
    {
        private static WaveFieldSettings Spectrum(float blend = 1f)
        {
            var s = WaveFieldSettings.Default;
            s.SpectrumBlend = blend;
            return s;
        }

        private static readonly Vector2 Wind = new Vector2(8f, 0f);   // ~18 m dominant wavelength
        private const float Sea = 0.7f;

        // ===== (1) THE SHAPE — "variance in sizes" =======================================================

        [Test]
        public void JonswapShape_PeaksAtThePeakFrequency_AndFallsAwayBothSides()
        {
            float peak = WaveSpectrum.JonswapShape(1f, 3.3f, 0.08f);
            foreach (float r in new[] { 0.5f, 0.7f, 0.85f, 0.95f, 1.05f, 1.15f, 1.4f, 2f, 3f })
                Assert.Less(WaveSpectrum.JonswapShape(r, 3.3f, 0.08f), peak,
                    $"ω/ω_p = {r} must carry less energy than the peak");
        }

        [Test]
        public void JonswapShape_HigherGamma_ConcentratesEnergyOnThePeak()
        {
            // γ is the "how dominant is one wave size" dial: 1 = Pierson-Moskowitz (broad swell),
            // 3.3 = the fetch-limited standard. Higher γ must raise the peak RELATIVE to its flanks.
            float FlankRatio(float gamma) =>
                WaveSpectrum.JonswapShape(1.3f, gamma, 0.08f) / WaveSpectrum.JonswapShape(1f, gamma, 0.08f);

            Assert.Less(FlankRatio(6f), FlankRatio(3.3f), "γ 6 concentrates harder than 3.3");
            Assert.Less(FlankRatio(3.3f), FlankRatio(1f), "γ 3.3 concentrates harder than plain PM");
            Assert.AreEqual(FlankRatio(1f), FlankRatio(0.5f), 1e-6f,
                "γ < 1 is clamped to Pierson-Moskowitz, not inverted into a spectral HOLE");
        }

        [Test]
        public void TheField_AtFullBlend_CarriesAContinuousRangeOfSizes_NotThreeFractions()
        {
            // The actual ask. Today's sea has four sizes in fixed ratios 1 / 0.55 / 0.38 / 0.22 of the
            // dominant, three of them much shorter — a primary plus chop. The spectrum must instead
            // populate a band AROUND the dominant, with neighbours close enough to read as the same
            // sea rather than as separate layers.
            WaveTrains field = WaveMath.TrainsFrom(Wind, Sea, Spectrum());
            Assert.AreEqual(WaveTrains.MaxTrains, field.Count, "the full spectrum uses every slot");

            float peakLambda = field[0].Wavelength;
            int neighboursWithinHalfAnOctave = 0;
            for (int i = 1; i < field.Count; i++)
            {
                float ratio = field[i].Wavelength / peakLambda;
                if (ratio > 0.7f && ratio < 1.43f) neighboursWithinHalfAnOctave++;
            }
            Assert.GreaterOrEqual(neighboursWithinHalfAnOctave, 2,
                "at least two trains must sit within half an octave of the peak — that is what makes " +
                "the sea read as ONE body of water with varied waves rather than stacked layers");
        }

        // ===== (2) THE FAN — "moving in different directions" ============================================

        [Test]
        public void DirectionalWeight_NarrowsAsTheExponentRises_AndIsDeadAgainstTheWind()
        {
            const float off = 40f * Mathf.Deg2Rad;
            Assert.AreEqual(1f, WaveSpectrum.DirectionalWeight(0f, 2f), 1e-6f, "downwind is unweighted");
            Assert.Greater(WaveSpectrum.DirectionalWeight(off, 0f), WaveSpectrum.DirectionalWeight(off, 2f),
                "s = 0 spreads energy evenly; s = 2 pulls it toward the wind");
            Assert.Greater(WaveSpectrum.DirectionalWeight(off, 2f), WaveSpectrum.DirectionalWeight(off, 6f),
                "higher s narrows the fan further");
            Assert.AreEqual(1f, WaveSpectrum.DirectionalWeight(off, 0f), 1e-6f,
                "s = 0 is a genuinely uniform fan, not merely a wide one");
            Assert.AreEqual(0f, WaveSpectrum.DirectionalWeight(100f * Mathf.Deg2Rad, 2f), 1e-6f,
                "a train travelling against the wind carries no wind energy");
        }

        [Test]
        public void TheField_AtFullBlend_SpreadsDirections_AndNarrowsWithTheExponent()
        {
            float SpreadDegrees(float exponent)
            {
                var s = Spectrum();
                s.SpectrumSpreadExponent = exponent;
                WaveTrains f = WaveMath.TrainsFrom(Wind, Sea, in s);

                // Amplitude-weighted mean |angle off the wind| — the fan the ENERGY actually occupies,
                // which is the thing the exponent controls (the geometric fan is fixed).
                Vector2 windDir = Wind.normalized;
                float weighted = 0f, total = 0f;
                for (int i = 0; i < f.Count; i++)
                {
                    float angle = Vector2.Angle(windDir, f[i].Direction);
                    weighted += angle * f[i].Amplitude;
                    total += f[i].Amplitude;
                }
                return total > 1e-6f ? weighted / total : 0f;
            }

            float wide = SpreadDegrees(0f);
            float standard = SpreadDegrees(2f);
            float narrow = SpreadDegrees(8f);

            Assert.Greater(wide, 5f, "with no spreading exponent the energy occupies a real fan");
            Assert.Less(narrow, standard, "a higher exponent gathers the energy toward the wind");
            Assert.Less(standard, wide, "and the shipped exponent is already narrower than uniform");

            // And the fan is genuinely WIDE — not seven trains the hash happened to cluster.
            //
            // ⚠️ Deliberately NOT "no two trains share a direction". Two trains running the same way
            // at different wavelengths is not a duplicated axis — it is precisely what a spectrum IS,
            // and the stratified fan puts one slot near dead downwind on purpose (that is where the
            // energy lives). What would be a defect is trains sharing a direction AND a wavelength,
            // which the distinct frequency steps rule out by construction.
            WaveTrains field = WaveMath.TrainsFrom(Wind, Sea, Spectrum());
            float minAngle = float.MaxValue, maxAngle = float.MinValue;
            for (int i = 0; i < field.Count; i++)
            {
                float a = Vector2.SignedAngle(Wind.normalized, field[i].Direction);
                minAngle = Mathf.Min(minAngle, a);
                maxAngle = Mathf.Max(maxAngle, a);
            }
            Assert.Greater(maxAngle - minAngle, 40f,
                $"the trains must occupy a real fan, not one axis (spanned {maxAngle - minAngle:F1}°)");

            for (int i = 0; i < field.Count; i++)
            for (int j = i + 1; j < field.Count; j++)
                Assert.Greater(Mathf.Abs(field[i].Wavelength - field[j].Wavelength), 1e-3f,
                    $"slots {i} and {j} must not be the same wave twice");
        }

        // ===== (3) THE GROUPS — "building and collapsing" ================================================

        [Test]
        public void BeatPeriod_IsReadable_AcrossTheWholeWindRange()
        {
            // The handoff's explicit requirement: verify the beat lands in TENS OF SECONDS, not
            // minutes (a group you never live to see is not a group) and not seconds (that is chop).
            var settings = Spectrum();
            foreach (float windSpeed in new[] { 0f, 4f, 8f, 15f, 30f })
            {
                float lambda = Mathf.Clamp(
                    settings.DominantWavelengthBase + settings.DominantWavelengthPerWindSpeed * windSpeed,
                    WaveTrain.MinWavelengthMeters, settings.DominantWavelengthMax);
                float period = WaveSpectrum.BeatPeriodSeconds(
                    lambda, settings.Gravity, settings.SpectrumFrequencySpacing);

                Assert.Greater(period, 15f, $"wind {windSpeed} m/s: a {period:F1} s group reads as chop");
                Assert.Less(period, 90f, $"wind {windSpeed} m/s: a {period:F1} s group is too rare to read");
            }
        }

        [Test]
        public void BeatPeriod_IsTheGroupDial_LongerAtTighterSpacing()
        {
            float tight = WaveSpectrum.BeatPeriodSeconds(18f, 9.81f, 0.04f);
            float shipped = WaveSpectrum.BeatPeriodSeconds(18f, 9.81f, 0.08f);
            float loose = WaveSpectrum.BeatPeriodSeconds(18f, 9.81f, 0.16f);
            Assert.Greater(tight, shipped, "closer frequencies beat more slowly — longer sets");
            Assert.Greater(shipped, loose);
            Assert.AreEqual(2f, tight / shipped, 1e-3f, "the period is exactly inverse in the spacing");
        }

        [Test]
        public void GroupStrength_IsDrivenByTheSpacing_AndTheReportIsTheTuningEvidence()
        {
            // The measurement behind the shipped SpectrumFrequencySpacing. Grouping is not a free
            // consequence of "having a spectrum" — it is a consequence of the band being NARROW, and
            // this is the test that says by how much. Logged as a table because the numbers are the
            // argument for the default, and the owner's feel verdict may well move it.
            var report = new System.Text.StringBuilder("\nGROUP RUN LENGTH vs FREQUENCY SPACING\n");
            report.AppendLine("  spacing   beat(s)   mean run (waves)");

            float previousRun = float.MaxValue;
            foreach (float spacing in new[] { 0.03f, 0.04f, 0.06f, 0.08f, 0.12f, 0.20f })
            {
                var s = Spectrum();
                s.SpectrumFrequencySpacing = spacing;
                float run = MeanGroupRunLength(WaveMath.TrainsFrom(Wind, Sea, in s));
                float beat = WaveSpectrum.BeatPeriodSeconds(18f, s.Gravity, spacing);
                report.AppendLine($"  {spacing,7:F2}   {beat,7:F1}   {run,6:F2}");

                Assert.LessOrEqual(run, previousRun + 0.35f,
                    $"spacing {spacing}: widening the band must not LENGTHEN the runs — if it does, " +
                    "the metric is measuring something other than grouping");
                previousRun = run;
            }

            float legacy = MeanGroupRunLength(WaveMath.TrainsFrom(Wind, Sea, WaveFieldSettings.Default));
            report.AppendLine($"  (hand-authored 4-train field: {legacy:F2})");
            UnityEngine.Debug.Log(report.ToString());
        }

        [Test]
        public void GroupStrength_IsNotHurtByTheDirectionalFan_MeasuredAgainstTheIntuition()
        {
            // ⚠️ THIS TEST ASSERTED THE OPPOSITE FIRST, AND THE MEASUREMENT SAID NO.
            //
            // The intuition — trains running in different directions cannot beat cleanly at a point,
            // so widening the fan must cost grouping — predicts a trade-off between two things the
            // owner asked for at once ("different directions" AND "building and collapsing").
            // Measured: a 0° fan groups at 1.33 waves and the shipped 55° fan at 2.00. The fan does
            // not cost grouping here; it HELPS.
            //
            // The mechanism is the slot mapping, not anything deep: a slot's index picks both its
            // frequency step and its bin in the fan, so the directional cos^2s weighting also damps
            // whichever frequency steps landed at the fan's edges. Damping outlying frequencies is
            // exactly what narrows the band, and a narrower band groups harder. Recorded as an
            // OBSERVATION rather than a designed property — with 8 trains you cannot sample a 2D
            // frequency × direction spectrum, so some coupling is unavoidable, and this is which way
            // ours happens to fall. If the mapping is ever changed, re-measure before assuming.
            float RunAtSpread(float degrees)
            {
                var s = Spectrum();
                s.SpectrumSpreadDegrees = degrees;
                return MeanGroupRunLength(WaveMath.TrainsFrom(Wind, Sea, in s));
            }

            float following = RunAtSpread(0f);      // every train dead downwind
            float shipped = RunAtSpread(55f);
            UnityEngine.Debug.Log($"\nGROUP RUN vs FAN: 0° = {following:F2} waves, 55° = {shipped:F2} waves");

            Assert.GreaterOrEqual(shipped, following,
                $"the shipped fan must not cost grouping (0° {following:F2}, 55° {shipped:F2}) — " +
                "if this ever flips, the fan/frequency coupling has changed and the owner is being " +
                "asked to trade two of his three asks against each other");
        }

        [Test]
        public void TheField_AtFullBlend_ActuallyGroups_MoreThanTheHandAuthoredSea()
        {
            // The empirical claim: sample the real surface over ten minutes at a fixed point and
            // measure the mean RUN LENGTH — how many big waves arrive back-to-back before a lull.
            // "Three big ones then a lull" is literally a run length of three.
            //
            // ⚠️ THE OBVIOUS METRIC DOES NOT WORK, and it is worth recording why. Measuring the
            // VARIANCE of crest heights (coefficient of variation) separated the two fields by only
            // 17 % — legacy 0.53, spectrum 0.63 — which reads as "the spectrum barely helps". It does
            // not, because the hand-authored field already varies a lot: its four trains sit at
            // frequency ratios 1 / 1.35 / 1.62 / 2.13, so they beat FAST and irregularly. Big and
            // small crests alternate almost every wave. That is high variance and it is still a rigid
            // pattern — which is exactly the owner's complaint, and exactly why variance is the wrong
            // instrument. What the spectrum changes is the TIMESCALE: neighbouring frequencies beat
            // slowly, so the big ones arrive in a run. Run length measures that; variance cannot.
            float legacy = MeanGroupRunLength(WaveMath.TrainsFrom(Wind, Sea, WaveFieldSettings.Default));
            float spectrum = MeanGroupRunLength(WaveMath.TrainsFrom(Wind, Sea, Spectrum()));

            Assert.Greater(spectrum, legacy * 1.2f,
                $"the spectrum must group visibly harder than the hand-authored field " +
                $"(legacy run {legacy:F2} waves, spectrum run {spectrum:F2}) — this is the whole point of #5");
            Assert.Greater(spectrum, 1.75f,
                $"and the runs must be long enough to READ as sets, not merely longer than before " +
                $"(got {spectrum:F2} consecutive big waves)");
        }

        /// <summary>
        /// Mean run length of consecutive above-average crests over a long sample at one point — the
        /// ocean-engineering measure of wave grouping. 1 means big and small alternate (no groups);
        /// 3+ means sets.
        /// </summary>
        private static float MeanGroupRunLength(in WaveTrains trains)
        {
            const float dt = 0.1f;
            const int steps = 6000;                       // 600 s
            var pos = new Vector2(3.5f, -2.25f);

            // Pass 1: collect the crest heights in order.
            var crests = new System.Collections.Generic.List<float>(512);
            float prev = WaveMath.Sample(pos, 0.0, in trains).Height;
            float current = WaveMath.Sample(pos, dt, in trains).Height;
            for (int i = 2; i < steps; i++)
            {
                float next = WaveMath.Sample(pos, i * dt, in trains).Height;
                if (current > prev && current > next && current > 0f) crests.Add(current);
                prev = current;
                current = next;
            }
            Assert.Greater(crests.Count, 30, "sanity: the sample window must contain plenty of crests");

            double sum = 0;
            foreach (float c in crests) sum += c;
            float threshold = (float)(sum / crests.Count);

            // Pass 2: mean length of the runs that clear it.
            int runs = 0, inRun = 0, totalRunLength = 0;
            foreach (float c in crests)
            {
                if (c >= threshold) { inRun++; }
                else if (inRun > 0) { runs++; totalRunLength += inRun; inRun = 0; }
            }
            if (inRun > 0) { runs++; totalRunLength += inRun; }

            return runs > 0 ? totalRunLength / (float)runs : 0f;
        }

        // ===== (4) THE INVARIANTS THE WHOLE FIELD MUST KEEP ==============================================

        [Test]
        public void TheEnvelopeIsPreserved_AtEveryBlendValue()
        {
            // Σ amplitudes is the crest-factor normalizer the whitecap lifecycle divides by AND the
            // bound the watertight hull clamp scans against. If the spectrum moved it, foam and hull
            // ride would both change for reasons that have nothing to do with the spectrum — so the
            // normalization pins it, and this is the test that says so.
            float reference = WaveMath.TrainsFrom(Wind, Sea, WaveFieldSettings.Default).TotalAmplitude;
            Assert.Greater(reference, 0.1f, "sanity: a real sea");

            foreach (float blend in new[] { 0f, 0.15f, 0.4f, 0.75f, 1f })
                Assert.AreEqual(reference,
                    WaveMath.TrainsFrom(Wind, Sea, Spectrum(blend)).TotalAmplitude, 1e-4f,
                    $"blend {blend}: the amplitude envelope must not move");
        }

        [Test]
        public void TheBlendIsContinuous_NoJumpOffZero()
        {
            // The live slot count JUMPS from 4 to 8 the instant the blend leaves 0. That is only
            // acceptable because the four new trains fade in from EXACTLY zero amplitude, so the
            // surface itself is continuous. If they did not, the sea would visibly snap the moment the
            // owner touched the dial.
            var pos = new Vector2(11.25f, -6.5f);
            WaveTrains off = WaveMath.TrainsFrom(Wind, Sea, Spectrum(0f));
            WaveTrains barelyOn = WaveMath.TrainsFrom(Wind, Sea, Spectrum(1e-4f));

            Assert.AreEqual(4, off.Count);
            Assert.AreEqual(8, barelyOn.Count);
            for (int i = off.Count; i < barelyOn.Count; i++)
                Assert.Less(barelyOn[i].Amplitude, 1e-4f,
                    $"slot {i} must fade IN from zero, not appear at its full spectral amplitude");

            // ⚠️ Compared near t = 0, and the reason is the SAME one that scoped the PR-A passthrough
            // proof. A nudge of the blend moves each slot's wavelength a little, which moves its
            // dispersion-derived speed, and θ = k·(d·pos − c·t) multiplies that by t: at t = 28 s the
            // two surfaces differ by 2.3e-3 m, which is not a discontinuity — it is a continuous
            // effect accumulating. Testing far from t = 0 measures phase drift, not the jump.
            //
            // The runtime path is smoothed anyway: WaveFieldAnimator eases the parameters and
            // accumulates phase INCREMENTALLY (ADR 0018 addendum), which exists precisely so a
            // changing wavelength cannot jump the phase under a large running t. Dragging the dial
            // mid-session is covered by TurningTheBlendUpMidSession_… above.
            for (double t = 0; t <= 3.0; t += 1.0)
                Assert.AreEqual(WaveMath.Sample(pos, t, in off).Height,
                                WaveMath.Sample(pos, t, in barelyOn).Height, 1e-3f,
                    $"the surface at t={t} must not jump as the blend leaves zero");
        }

        [Test]
        public void GlassIsStillSacred_AtFullBlend()
        {
            // The ruling the spectrum is most likely to break: a normalized spectral weighting is one
            // careless divide away from putting a FLOOR under the amplitudes, and a floor at sea
            // state 0 means the mirror never arrives.
            WaveTrains field = WaveMath.TrainsFrom(Wind, 0f, Spectrum());
            for (int i = 0; i < field.Count; i++)
                Assert.AreEqual(0, BitConverter.SingleToInt32Bits(field[i].Amplitude),
                    $"slot {i} must be EXACTLY +0 at sea state 0 — glass means glass");
            Assert.AreEqual(0, BitConverter.SingleToInt32Bits(field.TotalAmplitude));
        }

        [Test]
        public void TheDominantIndex_IsTheRealPeak_AndTheConsumersCanFollowIt()
        {
            // Slot 0 carries ω/ω_p = 1 and the maximum directional weight, so it SHOULD be the peak —
            // but DominantIndex is computed by argmax, not asserted, precisely so a future tuning that
            // moved the peak would carry the phase consumers with it instead of silently desyncing
            // the hull's rocking from the foam's breaking.
            foreach (float windSpeed in new[] { 2f, 8f, 25f })
            foreach (float sea in new[] { 0.2f, 0.6f, 1f })
            {
                WaveTrains field = WaveMath.TrainsFrom(new Vector2(windSpeed, 1f), sea, Spectrum());
                int argmax = 0;
                for (int i = 1; i < field.Count; i++)
                    if (field[i].Amplitude > field[argmax].Amplitude) argmax = i;

                Assert.AreEqual(argmax, field.DominantIndex,
                    $"wind {windSpeed} sea {sea}: DominantIndex must BE the largest train");
                Assert.AreEqual(0, field.DominantIndex,
                    "at the shipped tuning the peak is slot 0 — pinned so a tuning change that moves " +
                    "it is a deliberate, visible decision rather than a surprise");
            }
        }

        [Test]
        public void DispersionIsStillCanon_LongerTrainsOutrunShorterOnes()
        {
            // The spectrum authors WAVELENGTHS; speed must still be derived, never authored (ADR 0018
            // §(1)). If a spectrum train ever carried a free-standing speed the mixed sea would stop
            // reading as one body of water — the exact defect #9 was built to remove.
            WaveTrains field = WaveMath.TrainsFrom(Wind, Sea, Spectrum());
            for (int i = 0; i < field.Count; i++)
            for (int j = 0; j < field.Count; j++)
            {
                if (field[i].Wavelength <= field[j].Wavelength) continue;
                Assert.Greater(field[i].PhaseSpeed, field[j].PhaseSpeed,
                    $"slot {i} (λ {field[i].Wavelength:F2}) is longer than slot {j} " +
                    $"(λ {field[j].Wavelength:F2}) so it must travel faster");
            }
        }

        [Test]
        public void TheSpectrum_IsDeterministic_SameInputsSameField()
        {
            var settings = Spectrum();
            WaveTrains a = WaveMath.TrainsFrom(Wind, Sea, in settings);
            WaveTrains b = WaveMath.TrainsFrom(Wind, Sea, in settings);
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a.DominantIndex, b.DominantIndex);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(BitConverter.SingleToInt32Bits(a[i].Amplitude),
                                BitConverter.SingleToInt32Bits(b[i].Amplitude), $"slot {i} amplitude");
                Assert.AreEqual(BitConverter.SingleToInt32Bits(a[i].Wavelength),
                                BitConverter.SingleToInt32Bits(b[i].Wavelength), $"slot {i} wavelength");
                Assert.AreEqual(BitConverter.SingleToInt32Bits(a[i].Direction.x),
                                BitConverter.SingleToInt32Bits(b[i].Direction.x), $"slot {i} dir.x");
            }
        }

        [Test]
        public void TheSeed_MovesTheFan_ButNotTheSpectrum()
        {
            // The angular fan hashes off PhaseSeed, so a different world seed gives a different sea —
            // deterministically. What must NOT move is the spectral shape: the same wind and sea state
            // carry the same energy however the directions fell.
            var a = Spectrum(); a.PhaseSeed = 11;
            var b = Spectrum(); b.PhaseSeed = 20260729;

            WaveTrains fa = WaveMath.TrainsFrom(Wind, Sea, in a);
            WaveTrains fb = WaveMath.TrainsFrom(Wind, Sea, in b);

            Assert.AreEqual(fa.TotalAmplitude, fb.TotalAmplitude, 1e-4f,
                "the envelope is a property of the weather, not of the seed");

            bool anyDirectionMoved = false;
            for (int i = 1; i < fa.Count; i++)
                if (Vector2.Angle(fa[i].Direction, fb[i].Direction) > 1f) anyDirectionMoved = true;
            Assert.IsTrue(anyDirectionMoved, "a different seed must give a different fan");
        }

        [Test]
        public void TurningTheBlendUpMidSession_SnapsTheNewSlots_InsteadOfEasingThemFromNothing()
        {
            // THE WORKFLOW THE OWNER WILL ACTUALLY USE: run the game, drag SpectrumBlend off zero,
            // watch the sea. That takes the live count from 4 to 8 mid-session, and the animator has
            // never eased slots 4-7 — their stored wavelength is 0, which WaveTrain floors to 1 cm
            // (k ≈ 628). Easing from there would arrive as a burst of shrieking high-frequency slope
            // rather than as trains fading in.
            var animator = new WaveFieldAnimator();
            var animSettings = WaveFieldAnimatorSettings.Default;

            var off = Spectrum(0f);
            for (int i = 0; i < 200; i++) animator.Tick(0.016f, Wind, Sea, in off, in animSettings);
            Assert.AreEqual(4, animator.Current.Count, "sanity: the blend-0 field is four trains");

            var on = Spectrum(1f);
            WaveTrains grown = animator.Tick(0.016f, Wind, Sea, in on, in animSettings);

            Assert.AreEqual(8, grown.Count, "the field grew");
            for (int i = 4; i < grown.Count; i++)
            {
                Assert.Greater(grown[i].Wavelength, 1f,
                    $"slot {i} must SNAP to a real wavelength on the tick it becomes live — " +
                    $"got {grown[i].Wavelength:F3} m, which is the never-initialized floor");
                Assert.Less(grown[i].Wavelength, 500f, $"slot {i} wavelength is sane");
            }

            // And the slope stays bounded — the actual symptom a 1 cm wavelength would produce.
            for (int i = 0; i < 20; i++)
            {
                WaveSample s = animator.Sample(new Vector2(i * 1.7f, -i * 0.9f));
                Assert.Less(s.Slope.magnitude, 25f,
                    "the surface must not shriek on the tick the spectrum switches on");
            }
        }

        // ===== (5) SABOTAGE — every assert above must be able to fail =====================================

        [Test]
        public void Sabotage_ZeroFrequencySpacing_WouldCollapseTheSpectrumOntoOneWave()
        {
            // Spacing 0 puts every train at the SAME frequency: one wave with N times the amplitude,
            // no beat, no groups — a MORE rigid pattern than the one being fixed. It is also what a
            // prefab serialized before these fields existed deserializes to, which is why
            // WaveSpectrum falls back to the reference spacing instead of honouring the zero.
            var stale = Spectrum();
            stale.SpectrumFrequencySpacing = 0f;

            WaveTrains field = WaveMath.TrainsFrom(Wind, Sea, in stale);
            float peakLambda = field[0].Wavelength;
            bool anyDistinct = false;
            for (int i = 1; i < field.Count; i++)
                if (Mathf.Abs(field[i].Wavelength - peakLambda) > 0.01f) anyDistinct = true;

            Assert.IsTrue(anyDistinct,
                "a zeroed spacing must fall back to the reference, not collapse every train onto " +
                "one wavelength");
            Assert.AreEqual(WaveSpectrum.BeatPeriodSeconds(18f, 9.81f, WaveSpectrum.DefaultFrequencySpacing),
                            WaveSpectrum.BeatPeriodSeconds(18f, 9.81f, 0f), 1e-3f,
                "and the beat period falls back with it");
        }

        [Test]
        public void Sabotage_TheGroupingMetricCanFail_AFlatSpectrumDoesNotGroup()
        {
            // Proves TheField_AtFullBlend_ActuallyGroups is measuring something real: force every
            // train to the same amplitude and a wide frequency spread (the anti-spectrum) and the
            // crest-height variation must drop well below what the shipped spectrum produces.
            var wide = Spectrum();
            wide.SpectrumFrequencySpacing = 0.5f;      // neighbours far apart: fast beat, no sets
            wide.SpectrumPeakEnhancement = 1f;

            float shipped = MeanGroupRunLength(WaveMath.TrainsFrom(Wind, Sea, Spectrum()));
            float flat = MeanGroupRunLength(WaveMath.TrainsFrom(Wind, Sea, in wide));

            Assert.Greater(shipped, flat,
                $"the shipped spacing must group harder than a wide flat one " +
                $"(shipped {shipped:F2}, wide {flat:F2}) — if these were equal the grouping test " +
                "would be measuring noise");
        }
    }
}
