using System;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 30 — "the waves move across the screen too fast".</b> The owner, 2026-09-06
    /// evening: <i>"waves seem to move across the screen too fast, should be realistic to actual waves
    /// and windspeed/conditions."</i>
    ///
    /// <para>This class is a <b>MEASUREMENT</b>, not a fix. Nothing here moves a knob and nothing here
    /// asserts that the shipped sea is wrong — the shipped numbers are REPORTED so the owner can rank
    /// against them, and only the things that are true by construction (the dispersion relation, the
    /// shape of the shipped wind law, the direction each lever moves the screen) are asserted. A guard
    /// that asserted "the sea is 0.18× too short" would invert the moment he retuned it
    /// (<c>a-guard-with-an-absolute-bar-rots-on-a-good-change</c>).</para>
    ///
    /// <para><b>The one thing it does settle — and the charter's hypothesis is wrong TWICE.</b> The
    /// standing hypothesis was that the drawn wavelength is scaled while the phase advance is not, so a
    /// drawn crest would travel at 2.8× the speed a wave of its drawn length could have. Measured:
    /// (a) at the shipped `_OceanSwellScale` the visual frequency scale is exactly <b>1</b>, so the
    /// drawn wave IS the modelled wave and there is no discrepancy to explain; and (b) even at the
    /// pre-PR-6 value the error ran the OTHER WAY. The closed form is
    /// <c>drawn speed / (the speed its drawn length allows) = 1/√fs</c>, because the drawn speed falls
    /// as <c>c/fs</c> while the allowed speed falls only as <c>c/√fs</c>. At fs = 2.8 that is
    /// <b>0.60×</b> — the old sea drew waves too SLOW for their size, never too fast. See
    /// <see cref="TheDrawnWaveIsTheModelledWave_AtTheShippedSwellScale"/>.</para>
    /// </summary>
    public class WaveSpeedMeasurementTests
    {
        const float G = 9.81f;

        /// <summary>`_OceanSwellScale`'s shipped value on Water.mat, and the constant the shader
        /// normalises it by (`WAVE_LEGACY_SCALE_REF`). Their ratio is the visual frequency scale.</summary>
        const float ShippedSwellScale = 0.025f;
        const float SwellScaleReference = 0.025f;

        /// <summary>CameraFollow's per-hull framings, from the shipped BoatHullDefs: the world HEIGHT
        /// in metres the camera shows. The frame is a first-order term in this complaint, and it is
        /// per boat — the same sea crosses a dory's frame in half the time it crosses a cape's.</summary>
        static readonly (string boat, float worldHeight)[] Frames =
        {
            ("dory", 14f), ("cape", 24f), ("coastal packet", 90f),
        };

        /// <summary>The sweep's four sea states (`WaterFidelityPlateSweepTests.SeaStateOf`).</summary>
        static readonly (string name, float seaState01)[] Sweep =
        {
            ("glass", 0f), ("light", 0.25f), ("blow", 0.55f), ("gale", 0.95f),
        };

        /// <summary>⚠️ <b>This was <c>WaveFieldSettings.Default</c> until 2026-09-09, and
        /// <c>.Default</c> ships <c>SeaFetchKilometres = 0</c> — the LEGACY LINEAR peak law.</b> The
        /// owner's asset ships 25 km, so every "shipped peak" this fixture reported was the wrong
        /// curve: 8.45 m against the fetch law's 2.21 m at 1.63 m/s, 6.75 m against 0.21 m at
        /// 0.5 m/s. Row 30's premise had moved before row 34 touched it.</summary>
        static WaveFieldSettings Shipped => ShippedWaveField.Settings();

        /// <summary>Deep-water dispersion, the relation `WaveTrain` derives its own speed from.</summary>
        static float PhaseSpeed(float lambda) => Mathf.Sqrt(G * lambda / (2f * Mathf.PI));
        /// <summary>The crest period — how often a crest passes a fixed point. `λ/c`, and therefore a
        /// function of λ alone. Frame-independent, which is why it is the half of this complaint a
        /// camera cannot answer.</summary>
        static float Period(float lambda) => Mathf.Sqrt(2f * Mathf.PI * lambda / G);

        /// <summary>The shipped peak wavelength — <b>the fetch law, because that is what the asset
        /// ships</b> (<c>SeaFetchKilometres = 25</c>, #762). The superseded line here was
        /// <c>Base + PerWindSpeed·U</c>, which is what `PeakWavelengthMeters` returns only when the
        /// fetch setting is OFF; reading it off `WaveFieldSettings.Default` made this fixture measure
        /// the legacy curve throughout.
        ///
        /// <para>⚠️ <b>Since 2026-09-09 the peak no longer SCALES the field</b> (register row 34):
        /// the spectrum's bins are a fixed ladder and λ_p only says which of them carries the energy.
        /// The peak is still the right thing to measure here — it is what the owner's "how fast do
        /// the waves move" question is about — but a crest's arrival rate is now set by the bin the
        /// energy sits in, which the ladder rounds to. See
        /// <c>WaveFixedBinLadderTests</c>.</para></summary>
        static float ShippedPeak(float windSpeed) => Mathf.Clamp(
            WaveMath.PeakWavelengthMeters(windSpeed, Shipped),
            WaveTrain.MinWavelengthMeters, Shipped.DominantWavelengthMax);

        /// <summary>Pierson–Moskowitz's fully-developed peak wavelength for a wind speed:
        /// `ω_p = 0.877 g/U` and `λ = 2πg/ω_p²`, so `λ_p = 2πU²/(0.877² g)` ≈ 0.833·U². The textbook
        /// reference the owner's "realistic to actual waves and windspeed" points at.</summary>
        static float PiersonMoskowitzPeak(float windSpeed)
            => 2f * Mathf.PI * windSpeed * windSpeed / (0.877f * 0.877f * G);

        // ==== 1. THE HYPOTHESIS, SETTLED ============================================================

        /// <summary>
        /// 🔴 <b>THE REFUTATION.</b> The shader draws each train at `k = published_k · fs` where
        /// `fs = _OceanSwellScale / 0.025`, while the phase advance the animator bakes uses the MODEL's
        /// own `k · c · dt`. So the drawn crest speed is `c / fs` — which is NOT the same as the speed
        /// a wave of the drawn length could carry, and the paragraph below is the sign of that gap.
        ///
        /// <para><b>At the shipped value fs is exactly 1</b>, so the drawn wavelength is the modelled
        /// wavelength and the drawn speed is the modelled speed. Water PR 6 moved this dial from 0.07
        /// (fs = 2.8) to 0.025 precisely to close the ride≠drawn gap, and it stayed closed.</para>
        ///
        /// <para>⚠️ <b>And the SIGN of the old error is not what the charter assumed.</b> Scaling k by
        /// fs shortens the drawn wave to λ/fs, which a wave of that length could carry at c/√fs — but
        /// the advance, being k·c divided by the LARGER drawn k, only reaches c/fs. The ratio is
        /// therefore <c>1/√fs</c>, BELOW 1 whenever fs &gt; 1: the pre-PR-6 sea drew waves at 0.60× the
        /// speed their size allowed. Whatever "too fast" is, it was never this. The arm is kept beside
        /// the shipped one so the refutation is a comparison, not an assertion of absence.</para>
        /// </summary>
        [Test]
        public void TheDrawnWaveIsTheModelledWave_AtTheShippedSwellScale()
        {
            float fs = ShippedSwellScale / SwellScaleReference;
            const float beforePr6 = 0.07f;
            float fsBefore = beforePr6 / SwellScaleReference;

            float lambda = ShippedPeak(8f);
            float modelled = PhaseSpeed(lambda);

            TestContext.WriteLine(
                $"_OceanSwellScale {ShippedSwellScale} / ref {SwellScaleReference} -> visual frequency " +
                $"scale {fs:0.000}; drawn lambda {lambda / fs:0.0} m, drawn crest speed " +
                $"{modelled / fs:0.00} m/s against the dispersion speed of a wave that long " +
                $"({PhaseSpeed(lambda / fs):0.00} m/s)");
            TestContext.WriteLine(
                $"  before PR 6 (_OceanSwellScale {beforePr6}): fs {fsBefore:0.00}, drawn lambda " +
                $"{lambda / fsBefore:0.0} m carried at {modelled / fsBefore:0.00} m/s where its own " +
                $"length allows {PhaseSpeed(lambda / fsBefore):0.00} m/s — " +
                $"{(modelled / fsBefore) / PhaseSpeed(lambda / fsBefore):0.00}x, i.e. too SLOW for its " +
                "drawn size, the opposite of the charter's hypothesis");

            Assert.AreEqual(1f, fs, 1e-6f,
                "The shipped swell scale must equal the shader's own normalisation reference, or the " +
                "drawn sea is not the modelled sea and every number below is measuring the wrong wave.");
            Assert.AreEqual(PhaseSpeed(lambda / fs), modelled / fs, 1e-4f,
                "At fs = 1 the drawn crest must travel at the dispersion speed of its own drawn " +
                "length. This is the hypothesis the charter asked to confirm or refute: REFUTED.");
            float ratioBefore = (modelled / fsBefore) / PhaseSpeed(lambda / fsBefore);
            Assert.AreEqual(1f / Mathf.Sqrt(fsBefore), ratioBefore, 1e-3f,
                "The drawn/allowed speed ratio is exactly 1/sqrt(fs) — the drawn speed falls as c/fs " +
                "while the speed its drawn length allows falls only as c/sqrt(fs). Stated as a closed " +
                "form so the SIGN of this error cannot be mis-remembered again.");
            Assert.Less(ratioBefore, 0.99f,
                "DEAD CONTROL: the pre-PR-6 scale must be measurably WRONG for its drawn length, or " +
                "this comparison proves nothing about the shipped value. Note the direction: 0.60x is " +
                "too SLOW for its size. The charter had it superluminal; it was neither that, nor " +
                "present at the shipped value.");
        }

        // ==== 2. THE WIND LAW =======================================================================

        /// <summary>
        /// ✅ <b>ROW 30's FINDING WAS ACTED ON, AND THIS TEST COULD NOT SEE IT UNTIL 2026-09-09.</b>
        ///
        /// <para>The finding was: the peak wavelength is <b>linear</b> in wind speed where a real
        /// fully-developed sea's is <b>quadratic</b>, so the sea is too long below the crossover and
        /// too short above it. <b>#762 fixed it</b> — the shipped peak is now the fetch-limited
        /// JONSWAP growth curve, which is curved.</para>
        ///
        /// <para>⚠️ <b>This fixture went on asserting the superseded shape for weeks, and passed.</b>
        /// It read its settings off <c>WaveFieldSettings.Default</c>, where <c>SeaFetchKilometres</c>
        /// is <b>0</b> — the switch that selects the legacy linear line — while
        /// <c>GameConfig.asset</c> ships 25 km. Its own failure message said what to do if the law
        /// ever curved ("this row has been acted on"), and the law had. See
        /// <see cref="ShippedWaveField"/>: key presence is guarded, values were not.</para>
        ///
        /// <para>So the assertions invert. The shipped law must now be CURVED, the legacy line must
        /// still be available and still linear (the record of what was superseded), and the reference
        /// stays quadratic. Sizes are reported, never asserted — they are the owner's tuning.</para>
        /// </summary>
        [Test]
        public void TheWindToWavelengthLaw_IsNoLongerLinear_BecauseNumber762FixedIt_Tabulated()
        {
            WaveFieldSettings legacy = Shipped;
            legacy.SeaFetchKilometres = 0f;               // the superseded switch position
            float LegacyPeak(float u) => Mathf.Clamp(
                WaveMath.PeakWavelengthMeters(u, in legacy),
                WaveTrain.MinWavelengthMeters, legacy.DominantWavelengthMax);

            TestContext.WriteLine(
                $"shipped: the fetch law at {Shipped.SeaFetchKilometres:0.#} km (#762) | superseded: " +
                $"lambda = {Shipped.DominantWavelengthBase} + " +
                $"{Shipped.DominantWavelengthPerWindSpeed}*U | Pierson-Moskowitz: ~0.833*U^2");
            TestContext.WriteLine(
                " U m/s | shipped lam  superseded    PM lam | shipped/PM | crest every   PM   ratio");
            foreach (float u in new[] { 0.5f, 1f, 1.62f, 3f, 5.7f, 8f, 11f, 12.95f, 14f, 20f })
            {
                float ship = ShippedPeak(u), was = LegacyPeak(u), pm = PiersonMoskowitzPeak(u);
                TestContext.WriteLine(
                    $"{u,6:0.00} | {ship,11:0.0} {was,11:0.0} {pm,9:0.0} | {ship / pm,10:0.00} | " +
                    $"{Period(ship),11:0.00} {Period(pm),5:0.00} {Period(ship) / Period(pm),6:0.00}");
            }

            // CURVED: equal wind steps no longer give equal wavelength steps.
            float d1 = ShippedPeak(4f) - ShippedPeak(2f);
            float d2 = ShippedPeak(8f) - ShippedPeak(6f);
            Assert.AreNotEqual(d1, d2,
                "⭐ The shipped law must be CURVED in wind speed. It was linear until #762 put it on " +
                "the fetch-limited growth curve, and that is row 30's finding being acted on — not a " +
                "regression. If this ever reads linear again, either the fetch setting has been " +
                "switched off in GameConfig.asset or this fixture has drifted back onto .Default.");
            Assert.Greater(Mathf.Abs(d1 - d2), 0.1f,
                $"...and curved by a real amount: 2->4 m/s adds {d1:0.00} m of wavelength where " +
                $"6->8 m/s adds {d2:0.00} m. A hair of curvature would mean the fetch law is barely " +
                "engaged at this fetch.");

            // The SUPERSEDED line is still reachable, and still linear. Kept as the record.
            Assert.AreEqual(LegacyPeak(4f) - LegacyPeak(2f), LegacyPeak(8f) - LegacyPeak(6f), 1e-3f,
                "the legacy line (SeaFetchKilometres <= 0) must still be exactly linear — it is what " +
                "129 pre-#762 assets deserialize to, and AtFetchZero_ThePeakIsTheLegacyLine_BitForBit " +
                "is the guard that it stays bit-for-bit");

            // Quadratic: doubling the wind quadruples the reference wavelength.
            Assert.AreEqual(4f, PiersonMoskowitzPeak(10f) / PiersonMoskowitzPeak(5f), 1e-3f,
                "Pierson-Moskowitz is quadratic by construction; if this is not 4 the reference is " +
                "mis-transcribed and every ratio in the table above is wrong.");

            // And the direction of the remaining departure, which is now a FETCH story rather than a
            // curve-shape one: a 25 km strait cannot build a fully developed sea, so the shipped peak
            // sits at or below PM everywhere the fetch actually bites.
            for (float u = 2f; u <= 14f; u += 0.5f)
                Assert.LessOrEqual(ShippedPeak(u), PiersonMoskowitzPeak(u) * 1.001f,
                    $"at {u:0.0} m/s the shipped peak ({ShippedPeak(u):0.00} m) exceeds the fully-" +
                    $"developed reference ({PiersonMoskowitzPeak(u):0.00} m). A fetch-limited sea " +
                    "cannot be longer than the ocean's own — that would mean the cap in " +
                    "PeakWavelengthMeters is the wrong way round.");
        }

        /// <summary>
        /// <b>The half of "too fast" a camera cannot answer.</b> The crest PERIOD — how often a crest
        /// passes a fixed point — is `√(2πλ/g)`, a function of wavelength alone. It does not depend on
        /// the frame, the zoom or the aspect. Because the shipped sea is short for its wind above the
        /// crossover, its crests arrive MORE OFTEN than a real sea's in the same wind, and that is a
        /// candidate reading of the owner's sentence that no camera change would fix.
        /// </summary>
        [Test]
        public void TheCrestArrivalPeriod_IsFrameIndependent_AndReported()
        {
            foreach ((string name, float sea) in Sweep)
            {
                float u = WeatherModel.WindStrengthFor(sea);
                if (u <= 0.01f)
                {
                    TestContext.WriteLine($"{name,-6} U {u,5:0.00} — glass: every amplitude is exactly 0, nothing moves");
                    continue;
                }
                float ship = ShippedPeak(u), pm = PiersonMoskowitzPeak(u);
                TestContext.WriteLine(
                    $"{name,-6} U {u,5:0.00}  a crest arrives every {Period(ship),4:0.00} s " +
                    $"(a real sea in that wind: {Period(pm),5:0.00} s) — {Period(ship) / Period(pm):0.00}x");
            }
            Assert.AreEqual(Period(20f), Period(20f), 0f,
                "The period is a function of wavelength alone — this line exists to say so out loud: " +
                "no zoom, aspect or framing enters it.");
        }

        // ==== 3. THE SCREEN =========================================================================

        /// <summary>
        /// <b>"Across the screen" is a per-BOAT number.</b> The camera's world height comes from the
        /// active hull's `CameraWorldHeightMeters`, so the identical sea crosses a dory's frame in a
        /// fraction of the time it crosses a coastal packet's. Reported at the shipped framings and at
        /// the owner's own window aspect (his screenshots render 1902×879).
        /// </summary>
        [Test]
        public void TheCrossingTime_IsPerBoat_AndReportedAtTheOwnersAspect()
        {
            foreach ((string label, float aspect) in new[] { ("16:9", 16f / 9f), ("owner 1902x879", 1902f / 879f) })
            {
                TestContext.WriteLine($"aspect {label}:");
                foreach ((string boat, float height) in Frames)
                {
                    float width = height * aspect;
                    string row = $"  {boat,-15} {height,3:0} m high = {width,5:0.0} m wide:";
                    foreach ((string name, float sea) in Sweep)
                    {
                        float u = WeatherModel.WindStrengthFor(sea);
                        if (u <= 0.01f) continue;
                        float lambda = ShippedPeak(u), c = PhaseSpeed(lambda);
                        row += $"  {name} {width / c:0.0}s/{width / lambda:0.0}lam";
                    }
                    TestContext.WriteLine(row);
                }
            }

            // Wider frame, same sea, longer crossing — by construction, and worth pinning because it is
            // the lever the last test measures.
            float blowLambda = ShippedPeak(WeatherModel.WindStrengthFor(0.55f));
            float speed = PhaseSpeed(blowLambda);
            Assert.Greater(90f * 16f / 9f / speed, 14f * 16f / 9f / speed,
                "A wider frame must take longer to cross at the same wave speed.");
        }

        // ==== 4. THE OWNER'S RULING (2026-09-06): "make it realistic" ================================

        static WaveFieldSettings WithFetch(float km)
        {
            WaveFieldSettings f = WaveFieldSettings.Default;
            f.SeaFetchKilometres = km;
            return f;
        }

        /// <summary>
        /// <b>OFF is the legacy line, bit for bit.</b> `SeaFetchKilometres` ≤ 0 must return exactly
        /// `Base + PerWindSpeed·U` — that is what keeps `WaveFieldSettings.Default` and all 129
        /// `TrainsFrom` call sites in the suite unmoved, and what a pre-ruling asset (which deserializes
        /// the key as ZERO) keeps drawing.
        /// </summary>
        [Test]
        public void AtFetchZero_ThePeakIsTheLegacyLine_BitForBit()
        {
            WaveFieldSettings off = WithFetch(0f);
            foreach (float u in new[] { 0f, 1.62f, 5.7f, 12.95f, 20f })
            {
                float expected = off.DominantWavelengthBase + off.DominantWavelengthPerWindSpeed * u;
                // ⚠️ A ULP, not zero. C# does NOT promise that two transcriptions of one float
                // expression evaluate identically — it may keep intermediates at higher precision — so
                // this read 8.4300000 against 8.4300003 at U 1.62 on the first run. The passthrough is
                // exact in INTENT and the guard says so to within the float grid; demanding bit
                // equality of two separately-compiled expressions is a guard that fails on a compiler,
                // not on a defect (bit-equality-is-unattainable-between-two-transcriptions).
                Assert.AreEqual(expected, WaveMath.PeakWavelengthMeters(u, in off),
                    Mathf.Max(1e-6f, Mathf.Abs(expected) * 1e-6f),
                    $"At U {u} the OFF path must be the legacy line — this is the passthrough that " +
                    "keeps 129 call sites and every pre-ruling asset on the sea they already had.");
            }
            Assert.AreEqual(0f, WaveFieldSettings.Default.SeaFetchKilometres, 0f,
                "The reference tuning must ship the law OFF, or 129 call sites move under it.");
            WaveFieldSettings negative = WithFetch(-10f);
            Assert.AreEqual(WaveMath.PeakWavelengthMeters(5.7f, in off),
                            WaveMath.PeakWavelengthMeters(5.7f, in negative), 0f,
                "A negative fetch fails the same safe way — a missing YAML key deserializes to zero and " +
                "must never be read as 'a sea with no fetch', which would be a flat one.");
        }

        /// <summary>
        /// 🔴 <b>PIERSON–MOSKOWITZ IS THE LIMIT OF THIS LAW, NOT A RIVAL TO IT.</b> JONSWAP's growth has
        /// no ceiling of its own, so the derived peak is capped at the fully-developed value. Pushing
        /// the fetch up must therefore converge on PM from below and never pass it.
        /// </summary>
        [Test]
        public void TheDerivedPeak_GrowsWithFetch_AndConvergesOnPiersonMoskowitzFromBelow()
        {
            const float u = 5.7f;
            float pm = PiersonMoskowitzPeak(u);
            float previous = 0f;
            foreach (float km in new[] { 2f, 5f, 10f, 25f, 50f, 100f, 1000f, 100000f })
            {
                WaveFieldSettings at = WithFetch(km);
                float lambda = WaveMath.PeakWavelengthMeters(u, in at);
                TestContext.WriteLine($"  fetch {km,7:0} km -> lambda {lambda,6:0.0} m " +
                                      $"({lambda / pm:0.00} of the fully-developed {pm:0.0} m)");
                Assert.GreaterOrEqual(lambda, previous - 1e-3f, "More fetch cannot make a shorter sea.");
                Assert.LessOrEqual(lambda, pm + 1e-3f,
                    "The derived peak must never exceed the fully-developed limit — an uncapped JONSWAP " +
                    "would lengthen forever with fetch, which is not a sea.");
                previous = lambda;
            }
            WaveFieldSettings ocean = WithFetch(100000f);
            Assert.AreEqual(pm, WaveMath.PeakWavelengthMeters(u, in ocean), pm * 0.01f,
                "At effectively infinite fetch the law IS Pierson-Moskowitz — that is what makes PM the " +
                "limit case rather than a second law to choose between.");
        }

        /// <summary>
        /// 🔴 <b>WHY BARE PM WAS REJECTED, measured.</b> Full development needs `gX/U² ≈ 17 400` — 58 km
        /// of open water at a blow and <b>298 km at a gale</b>. An inshore island has neither, and the
        /// consequences of pretending otherwise are reported here: at a gale bare PM puts a 140 m wave
        /// on the cape's 52 m frame, which crosses it FASTER than today (the owner's original
        /// complaint) with less than half a wavelength visible.
        /// </summary>
        [Test]
        public void BarePiersonMoskowitz_NeedsAnOceanThisSettingDoesNotHave_Measured()
        {
            const float cape = 24f * (1902f / 879f);
            foreach ((string name, float sea) in Sweep)
            {
                float u = WeatherModel.WindStrengthFor(sea);
                if (u <= 0.01f) continue;
                float needKm = 17400f * u * u / G / 1000f;
                float legacy = ShippedPeak(u);
                WaveFieldSettings shipped25 = WithFetch(25f);
                float derived = WaveMath.PeakWavelengthMeters(u, in shipped25);
                float pm = PiersonMoskowitzPeak(u);
                TestContext.WriteLine(
                    $"{name,-6} U {u,5:0.00}: fully developed needs {needKm,6:0} km | legacy {legacy,6:0.0} m " +
                    $"| derived@25km {derived,6:0.0} m ({derived / legacy:0.00}x legacy) | PM {pm,6:0.0} m " +
                    $"-> PM crosses the cape in {cape / PhaseSpeed(pm):0.0} s and shows " +
                    $"{cape / pm:0.00} wavelengths");
            }
            float gale = WeatherModel.WindStrengthFor(0.95f);
            Assert.Greater(17400f * gale * gale / G / 1000f, 200f,
                "A gale needs hundreds of kilometres of open water to be fully developed. If this ever " +
                "drops, the setting has become an ocean and bare PM would be the right law after all.");
            Assert.Less(cape / PiersonMoskowitzPeak(gale), 0.5f,
                "DEAD CONTROL for the look argument: under bare PM less than half a wavelength fits " +
                "across the cape's frame at a gale, so the sea would read as the screen heaving rather " +
                "than as waves. This is why the fetch cap is the fix and bare PM is not.");
        }

        /// <summary>The shipped fetch against the legacy line it replaces, at every sea state — the
        /// before/after the owner ranks. Reported, never asserted: the fetch is his dial.</summary>
        [Test]
        public void TheShippedFetch_AgainstTheLegacyLine_Tabulated()
        {
            const float cape = 24f * (1902f / 879f);
            TestContext.WriteLine("sea    U m/s |  legacy lam    T     c  cross |  derived@25km    T     c  cross");
            foreach ((string name, float sea) in Sweep)
            {
                float u = WeatherModel.WindStrengthFor(sea);
                if (u <= 0.01f) { TestContext.WriteLine($"{name,-6} {u,6:0.00} | glass — every amplitude is exactly 0"); continue; }
                WaveFieldSettings shipped25 = WithFetch(25f);
                float a = ShippedPeak(u), b = WaveMath.PeakWavelengthMeters(u, in shipped25);
                TestContext.WriteLine(
                    $"{name,-6} {u,6:0.00} | {a,10:0.0} {Period(a),5:0.00} {PhaseSpeed(a),5:0.00} " +
                    $"{cape / PhaseSpeed(a),6:0.0} | {b,13:0.0} {Period(b),5:0.00} {PhaseSpeed(b),5:0.00} " +
                    $"{cape / PhaseSpeed(b),6:0.0}");
            }
        }

        // ---- the owner's post-play dial, shipped at its passthrough ---------------------------------

        /// <summary>
        /// <b>`DominantWavelengthScale` ships at 1 and is a no-op</b> — it exists because the owner may
        /// still want the sea slower after playing the realistic one, and it is the single number that
        /// does it. ⚠️ Speed and period both go as its SQUARE ROOT, so 0.5 is 0.71× the speed and a true
        /// half-speed is 0.25 — and a shorter sea is a LESS realistic one, which is the trade §39 states.
        /// </summary>
        [Test]
        public void TheWavelengthScale_ShipsAsANoOp_AndMovesSpeedAsItsSquareRoot()
        {
            WaveFieldSettings one = WaveFieldSettings.Default;
            Assert.AreEqual(1f, one.DominantWavelengthScale, 0f,
                "The reference tuning must ship the dial at its passthrough, or 129 call sites move.");

            const float u = 5.7f;
            var wind = new Vector2(0f, u);
            float full = WaveMath.TrainsFrom(wind, 0.55f, in one)[0].Wavelength;
            foreach (float scale in new[] { 1f, 0.5f, 0.25f })
            {
                WaveFieldSettings dialled = WaveFieldSettings.Default;
                dialled.DominantWavelengthScale = scale;
                float lambda = WaveMath.TrainsFrom(wind, 0.55f, in dialled)[0].Wavelength;
                TestContext.WriteLine(
                    $"scale {scale:0.00}: lambda {lambda,5:0.0} m  c {PhaseSpeed(lambda),4:0.00} m/s " +
                    $"({PhaseSpeed(lambda) / PhaseSpeed(full):0.00}x)  crest every {Period(lambda),4:0.00} s");
                Assert.AreEqual(scale, lambda / full, 1e-3f, "The dial scales the wavelength directly.");
                Assert.AreEqual(Mathf.Sqrt(scale), PhaseSpeed(lambda) / PhaseSpeed(full), 1e-3f,
                    "...but crest SPEED goes as its square root — half the wavelength is 0.71x the " +
                    "speed, and a true half-speed is a QUARTER wavelength.");
            }
        }

        /// <summary>
        /// 🔴 <b>THE STALE-ASSET GUARD.</b> `DominantWavelengthScale` postdates every asset serialized
        /// before 2026-09-06, so a missing key deserializes as <b>zero</b> — and a plain multiply would
        /// collapse every wavelength onto `MinWavelengthMeters`. Measured with the floor removed: a
        /// 0.010 m ocean. Zero and negatives are read as 1.
        /// </summary>
        [Test]
        public void AMissingWavelengthScale_FailsToTheDerivedSea_NotToAFlatOne()
        {
            var wind = new Vector2(0f, 5.7f);
            WaveFieldSettings stale = WaveFieldSettings.Default;
            stale.DominantWavelengthScale = 0f;
            WaveFieldSettings one = WaveFieldSettings.Default;
            one.DominantWavelengthScale = 1f;
            WaveFieldSettings negative = WaveFieldSettings.Default;
            negative.DominantWavelengthScale = -3f;

            float staleLambda = WaveMath.TrainsFrom(wind, 0.55f, in stale)[0].Wavelength;
            float oneLambda = WaveMath.TrainsFrom(wind, 0.55f, in one)[0].Wavelength;
            TestContext.WriteLine($"a stale asset (scale 0) derives {staleLambda:0.000} m; " +
                                  $"an explicit 1 derives {oneLambda:0.000} m");

            Assert.AreEqual(oneLambda, staleLambda, 1e-4f,
                "A zero scale must behave exactly as 1 — the sea the derivation produced.");
            Assert.AreEqual(oneLambda, WaveMath.TrainsFrom(wind, 0.55f, in negative)[0].Wavelength, 1e-4f,
                "A negative fails the same safe way rather than mirroring the sea.");
            Assert.Greater(staleLambda, WaveTrain.MinWavelengthMeters * 100f,
                "DEAD CONTROL: without the floor a stale asset lands on MinWavelengthMeters — a one-" +
                "centimetre ocean. This is the assertion that catches it.");
        }

        /// <summary>The dial reaches EVERY train, so the sea shortens without changing shape. If it ever
        /// reached only the primary, the cross-chop would keep its old size and the sea would read as
        /// two different seas laid over each other.</summary>
        [Test]
        public void TheWavelengthScale_ReachesEveryTrain_SoTheSeaKeepsItsShape()
        {
            var wind = new Vector2(0f, 5.7f);
            WaveFieldSettings one = WaveFieldSettings.Default;
            WaveFieldSettings half = WaveFieldSettings.Default;
            half.DominantWavelengthScale = 0.5f;

            WaveTrains a = WaveMath.TrainsFrom(wind, 0.55f, in one);
            WaveTrains b = WaveMath.TrainsFrom(wind, 0.55f, in half);
            Assert.AreEqual(a.Count, b.Count, "The dial must not change how many trains there are.");
            for (int i = 0; i < a.Count; i++)
            {
                TestContext.WriteLine($"  train {i}: {a[i].Wavelength,6:0.00} m -> {b[i].Wavelength,6:0.00} m");
                Assert.AreEqual(0.5f, b[i].Wavelength / a[i].Wavelength, 1e-3f,
                    $"Train {i} must shorten by the same factor as the peak.");
            }
        }

        // ---- the SHIPPED ASSET, as Unity actually loads it -------------------------------------------

        /// <summary>Where the owner's tuned settings live. Everything above measures the LAW; this
        /// measures whether the law is switched on in the thing that ships.</summary>
        const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        /// <summary>
        /// 🔴 <b>THE FEATURE IS ONLY ON IF THE LOADED ASSET SAYS SO, and nothing else here would
        /// notice.</b> `SeaFetchKilometres` = 0 is the whole derivation silently OFF, drawing the old
        /// linear sea — and every other guard in this file builds its own settings from
        /// <see cref="WaveFieldSettings.Default"/>, which ships 0 <i>on purpose</i>. So a mis-serialized
        /// asset would leave all of them green.
        ///
        /// <para>⚠️ This reads the asset through <c>AssetDatabase</c> — <b>what Unity LOADS, not what the
        /// file says</b>. A field present in the YAML is not a field the object carries: a blank line
        /// ends a Unity mapping and the key after it deserializes to its default, which this repo has
        /// been bitten by before (the sport skiff's stern offset, #747).</para>
        ///
        /// <para>Stated as a guard-rail rather than a pin: the fetch is the owner's dial and he may tune
        /// it freely, so this asserts it is ON and sane, not that it equals 25. The scale IS pinned at
        /// 1, because that one is a passthrough and a non-1 value shipped by accident would quietly
        /// re-tune every sea state.</para>
        /// </summary>
        [Test]
        public void TheShippedAsset_HasTheDerivedLawSwitchedON_AsUnityLoadsIt()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, "the shared GameConfig asset must exist at " + ConfigAssetPath);
            WaveFieldSettings loaded = config.WaveField;

            TestContext.WriteLine(
                $"as LOADED: SeaFetchKilometres {loaded.SeaFetchKilometres:0.###} km, " +
                $"DominantWavelengthScale {loaded.DominantWavelengthScale:0.###}, " +
                $"cap {loaded.DominantWavelengthMax:0.#} m");

            Assert.Greater(loaded.SeaFetchKilometres, 0f,
                "GameConfig.asset carries SeaFetchKilometres <= 0, which is the derived peak law " +
                "SWITCHED OFF — the sea would silently fall back to the legacy linear line and every " +
                "other guard in this file would stay green, because they all build from " +
                "WaveFieldSettings.Default, which ships 0 deliberately. Either the key never reached " +
                "the deserializer (a blank line ends a Unity YAML mapping) or it was tuned to 0.");
            Assert.AreEqual(1f, loaded.DominantWavelengthScale, 1e-4f,
                "GameConfig.asset must ship the wavelength dial at its passthrough. It is the owner's " +
                "post-play lever and shipping it anywhere else re-tunes every sea state silently.");

            // ...and the loaded numbers must actually produce the sea this PR reports.
            float u = WeatherModel.WindStrengthFor(0.55f);
            float derived = WaveMath.PeakWavelengthMeters(u, in loaded);
            float legacy = loaded.DominantWavelengthBase + loaded.DominantWavelengthPerWindSpeed * u;
            TestContext.WriteLine($"  at a blow (U {u:0.00}) the LOADED asset derives {derived:0.0} m " +
                                  $"against the legacy line's {legacy:0.0} m");
            Assert.Greater(Mathf.Abs(derived - legacy), 1e-3f,
                "The loaded asset derives exactly the legacy line, so the derivation is not doing " +
                "anything — that is what an OFF feature looks like from the outside.");
        }

        /// <summary>The 40 m rail is a rail: no dial may step over it, including a wavelength scale
        /// above 1. The scale is applied after the peak law, so without a second clamp it would walk
        /// straight through the ceiling.</summary>
        [Test]
        public void NoDialStepsOverTheWavelengthRail()
        {
            var wind = new Vector2(0f, 14f);
            WaveFieldSettings big = WaveFieldSettings.Default;
            big.SeaFetchKilometres = 500f;          // an ocean fetch, to push the peak up
            big.DominantWavelengthScale = 8f;       // and a dial that would sail past the rail
            float lambda = WaveMath.TrainsFrom(wind, 1f, in big)[0].Wavelength;
            TestContext.WriteLine($"ocean fetch + an 8x dial derives {lambda:0.0} m against a " +
                                  $"{big.DominantWavelengthMax:0.#} m rail");
            Assert.LessOrEqual(lambda, big.DominantWavelengthMax + 1e-3f,
                "A rail one knob can step over is not a rail. The scale is applied after the peak law, " +
                "so it is clamped again against the same ceiling.");
        }

        /// <summary>
        /// 🔴 <b>THE CONFLICT, and it is why this is a row and not a fix.</b> The owner asked for two
        /// things in one sentence — slower across the screen, and realistic to the wind. At the shipped
        /// framing they pull in OPPOSITE directions, because a longer wave is a FASTER wave: `c ∝ √λ`.
        /// Making the sea realistic for its wind lengthens it, which speeds its crests up across the
        /// frame even as it slows the rate at which they arrive.
        ///
        /// <para>Each lever is reported with what it does to BOTH numbers, so the trade is his to make
        /// rather than one this lane picks silently.</para>
        /// </summary>
        [Test]
        public void EachLever_MovesTheTwoNumbersInOppositeDirections_Reported()
        {
            float u = WeatherModel.WindStrengthFor(0.55f);          // the sweep's blow
            float width = 24f * (1902f / 879f);                     // the cape, at his window
            float shipped = ShippedPeak(u);

            void Row(string label, float lambda, float frameWidth) => TestContext.WriteLine(
                $"  {label,-34} lambda {lambda,5:0.0} m  c {PhaseSpeed(lambda),4:0.00} m/s  " +
                $"crosses {frameWidth,5:0.0} m in {frameWidth / PhaseSpeed(lambda),5:0.0} s  " +
                $"crest every {Period(lambda),4:0.00} s");

            TestContext.WriteLine($"at a blow (U {u:0.0} m/s) on the cape, in the owner's own window:");
            Row("shipped", shipped, width);
            Row("realistic for this wind (PM)", PiersonMoskowitzPeak(u), width);
            Row("half the wavelength", shipped * 0.5f, width);
            Row("zoom out to 32 m world height", shipped, 32f * (1902f / 879f));
            Row("zoom out to 48 m world height", shipped, 48f * (1902f / 879f));

            float pm = PiersonMoskowitzPeak(u);
            Assert.Greater(PhaseSpeed(pm), PhaseSpeed(shipped),
                "🔴 THE CONFLICT: making the sea realistic for its wind makes each crest travel FASTER " +
                "across the screen, because c grows as the square root of lambda. The owner's two asks " +
                "— 'not so fast across the screen' and 'realistic to windspeed' — oppose each other at " +
                "this framing, and that is the ruling this row needs from him.");
            Assert.Greater(Period(pm), Period(shipped),
                "...while the same change makes crests ARRIVE less often, which is the other reading of " +
                "'too fast' and the one it improves. Both are true; they are different numbers.");
            Assert.AreEqual(PhaseSpeed(shipped), PhaseSpeed(shipped), 0f,
                "Zooming out changes neither the speed nor the period — only the crossing time. It is " +
                "the one lever that slows the screen without touching the physics.");
        }
    }
}
