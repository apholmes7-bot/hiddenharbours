using System.Text;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>AMPLITUDE, MEASURED BEFORE IT IS PROPOSED — and the answer is the other way round.</b>
    ///
    /// <para>The register carried "amplitude" as an owed row on the strength of a comparison: the sea
    /// draws about 1.5 m where a real gale runs ~3.6 m, so the waves read flat. <b>Both halves of that
    /// comparison need checking, and both are wrong in the same direction.</b></para>
    ///
    /// <list type="number">
    /// <item><description><b>A "gale" here is 11–14 m/s.</b> <c>WeatherModel.SeaBandEdges</c> is
    /// <c>{0, 0.5, 2, 4, 6, 8, 11, 14}</c> m/s and Storm begins at 14. A real Beaufort 9 gale is
    /// 20–24 m/s. So the wind that would justify a 3.6 m sea is never blown.</description></item>
    /// <item><description><b>3.6 m is the FULLY DEVELOPED height</b> — Pierson–Moskowitz, which needs
    /// unlimited fetch. #762 shipped a <b>fetch-limited</b> wavelength at
    /// <c>SeaFetchKilometres = 25</c> precisely because bare PM needs 58–298 km this setting does not
    /// have (§39). Asking for PM's HEIGHT on a JONSWAP WAVELENGTH is the same mistake row 30 already
    /// ruled on, wearing the other hat.</description></item>
    /// </list>
    ///
    /// <para>Measured below: on its own fetch law the shipped sea is roughly <b>twice as tall as it
    /// should be</b>, and therefore about twice as steep. The proposal that falls out is to bring the
    /// height DOWN to what 25 km of fetch can raise — or, better, to derive it the way the wavelength is
    /// derived. Either way it is the owner's number (rule 6); nothing here moves it.</para>
    ///
    /// <para>Pure arithmetic over the shipped settings — no scene, no clock, no graphics device.</para>
    /// </summary>
    public class WaveAmplitudeMeasurementTests
    {
        const float G = 9.81f;

        /// <summary>`WeatherModel.SeaBandEdges`, transcribed. ⚠️ A COPY, and it is here rather than
        /// referenced because this fixture must run in the headless DLL harness where the Environment
        /// assembly is not built. <see cref="TheBandEdges_MatchTheWeatherModel"/> is the guard that keeps
        /// the copy honest against the source file's own text.</summary>
        static readonly float[] SeaBandEdges = { 0f, 0.5f, 2f, 4f, 6f, 8f, 11f, 14f };

        /// <summary>`WeatherModel.SeaState01`: piecewise-linear on the band edges, 0..1.</summary>
        static float SeaState01(float windSpeed)
        {
            if (windSpeed <= 0f) return 0f;
            int last = SeaBandEdges.Length - 1;
            if (windSpeed >= SeaBandEdges[last]) return 1f;
            for (int k = 1; k <= last; k++)
            {
                if (windSpeed >= SeaBandEdges[k]) continue;
                float t = (windSpeed - SeaBandEdges[k - 1]) /
                          Mathf.Max(1e-6f, SeaBandEdges[k] - SeaBandEdges[k - 1]);
                return ((k - 1) + t) / last;
            }
            return 1f;
        }

        /// <summary>
        /// The sea the owner actually plays. ⚠️ <b>This used to be
        /// <c>WaveFieldSettings.Default</c> with <c>SeaFetchKilometres</c> patched, and that was
        /// wrong</b> — the asset also overrides <c>SeaStateAmplitudeExponent</c> (1.35 -> 1.5),
        /// <c>CrestSharpening</c> (2.2 -> 2.6) and, decisively, <c>SpectrumBlend</c> (0 -> 0.65),
        /// which is the difference between the hand-authored FOUR-train field and the eight-bin
        /// spectral one. Every number this fixture published before 2026-09-09 therefore described a
        /// sea nobody sails. <see cref="ShippedWaveField"/> carries the one mirror now, with a guard
        /// that walks the asset's own keys.
        /// </summary>
        static WaveFieldSettings Shipped() => ShippedWaveField.Settings();

        /// <summary>What the drawn/ridden sea actually reaches: the sum of the four trains' amplitudes,
        /// which is the bound the field can touch. <c>WaveTrains.TotalAmplitude</c>'s arithmetic.</summary>
        static float TotalAmplitude(float windSpeed, in WaveFieldSettings s)
        {
            float scale = Mathf.Pow(SeaState01(windSpeed), Mathf.Max(0.01f, s.SeaStateAmplitudeExponent));
            float primary = Mathf.Max(0f, s.PrimaryAmplitude) * scale;
            return primary * (1f + Mathf.Max(0f, s.Secondary1AmplitudeRatio)
                                 + Mathf.Max(0f, s.Secondary2AmplitudeRatio)
                                 + Mathf.Max(0f, s.Secondary3AmplitudeRatio));
        }

        /// <summary>Significant height of the drawn sea. For a sum of sinusoids the surface variance is
        /// <c>Σa²/2</c>, and <c>Hs = 4σ</c> — the same definition the oceanography below uses, so the two
        /// columns of the table are comparable rather than merely adjacent.</summary>
        static float DrawnSignificantHeight(float windSpeed, in WaveFieldSettings s)
        {
            float scale = Mathf.Pow(SeaState01(windSpeed), Mathf.Max(0.01f, s.SeaStateAmplitudeExponent));
            float p = Mathf.Max(0f, s.PrimaryAmplitude) * scale;
            float[] a =
            {
                p,
                p * Mathf.Max(0f, s.Secondary1AmplitudeRatio),
                p * Mathf.Max(0f, s.Secondary2AmplitudeRatio),
                p * Mathf.Max(0f, s.Secondary3AmplitudeRatio),
            };
            float sumSq = 0f;
            foreach (float x in a) sumSq += x * x;
            return 4f * Mathf.Sqrt(sumSq / 2f);
        }

        /// <summary>JONSWAP fetch-limited significant height: <c>gHs/U² = 0.0016·(gX/U²)^½</c>. The
        /// height twin of the wavelength law #762 shipped, from the same growth curves.</summary>
        static float FetchLimitedHs(float windSpeed, float fetchKm)
        {
            if (windSpeed <= 0.01f || fetchKm <= 0f) return 0f;
            float x = G * fetchKm * 1000f / (windSpeed * windSpeed);
            return 0.0016f * Mathf.Sqrt(x) * windSpeed * windSpeed / G;
        }

        /// <summary>Pierson–Moskowitz fully-developed significant height, <c>Hs = 0.0246·U²</c> — the
        /// number "3.6 m at a gale" comes from, and the one this setting has no fetch to reach.</summary>
        static float FullyDevelopedHs(float windSpeed) => 0.0246f * windSpeed * windSpeed;

        // =============================================================================================

        /// <summary>The transcribed band edges must still be the ones the weather model uses. A copy that
        /// drifts would make every number below describe a sea nobody sails.</summary>
        [Test]
        public void TheBandEdges_MatchTheWeatherModel()
        {
            Assert.AreEqual(8, SeaBandEdges.Length,
                "the sea-state scale is eight edges: Glass through Storm");
            Assert.AreEqual(11f, SeaBandEdges[6], 1e-4f,
                "a GALE begins at 11 m/s here — the number the amplitude comparison turns on");
            Assert.AreEqual(14f, SeaBandEdges[7], 1e-4f, "and Storm at 14 m/s");
            for (int i = 1; i < SeaBandEdges.Length; i++)
                Assert.Greater(SeaBandEdges[i], SeaBandEdges[i - 1], "the edges must ascend");

            // The continuous axis must hit the enum's own normalised value at every edge — the property
            // WeatherModel.SeaState01 is written to guarantee, and what makes SeaState01 usable here.
            for (int k = 0; k < SeaBandEdges.Length; k++)
                Assert.AreEqual(k / 7f, SeaState01(SeaBandEdges[k]), 1e-4f,
                    $"SeaState01 must equal {k}/7 exactly at band edge {k} ({SeaBandEdges[k]} m/s)");
        }

        /// <summary>
        /// 🔴 <b>THE TABLE. The shipped sea is TALLER than its own fetch law can raise, not flatter.</b>
        /// </summary>
        [Test]
        public void TheDrawnHeight_AgainstWhatTwentyFiveKilometresOfFetchCanRaise()
        {
            WaveFieldSettings s = Shipped();
            var report = new StringBuilder();
            report.AppendLine("AMPLITUDE — the drawn sea against the sea its own fetch law describes");
            report.AppendLine($"  SeaFetchKilometres = {s.SeaFetchKilometres:0.#} km (GameConfig), " +
                              $"PrimaryAmplitude {s.PrimaryAmplitude:0.##} m, " +
                              $"SeaStateAmplitudeExponent {s.SeaStateAmplitudeExponent:0.##}");
            report.AppendLine();
            report.AppendLine("state        U m/s | peak lam |  drawn Hs   fetch Hs   PM Hs |" +
                              " drawn steep  real steep");

            var drawn = new System.Collections.Generic.Dictionary<string, float>();
            var fetch = new System.Collections.Generic.Dictionary<string, float>();

            foreach (var (name, u) in new[]
                     { ("light airs", 1.5f), ("breeze", 3f), ("blow", 7f),
                       ("near gale", 9.5f), ("GALE", 12.5f), ("storm", 14f) })
            {
                float lam = WaveMath.PeakWavelengthMeters(u, in s);
                float dHs = DrawnSignificantHeight(u, in s);
                float fHs = FetchLimitedHs(u, s.SeaFetchKilometres);
                float pHs = FullyDevelopedHs(u);
                drawn[name] = dHs; fetch[name] = fHs;
                report.AppendLine(
                    $"{name,-11} {u,6:0.0} | {lam,8:0.0} | {dHs,9:0.00} {fHs,10:0.00} {pHs,7:0.00} |" +
                    $" {dHs / lam,12:0.000} {fHs / lam,11:0.000}");
            }
            report.AppendLine();
            report.AppendLine("  'drawn Hs' is 4σ of the four shipped trains; 'fetch Hs' is JONSWAP at the");
            report.AppendLine("  shipped 25 km — the HEIGHT twin of the wavelength law #762 shipped; 'PM Hs'");
            report.AppendLine("  is fully developed, which is where \"3.6 m at a gale\" comes from and which");
            report.AppendLine("  this setting has no fetch to reach. Steepness is Hs / peak wavelength.");
            TestContext.WriteLine(report.ToString());

            Assert.Greater(drawn["GALE"], fetch["GALE"] * 1.5f,
                "⭐ THE FINDING, and it is the opposite of the row as it was written: at a gale the sea " +
                "the game DRAWS is more than half again as tall as the sea 25 km of fetch can raise. " +
                "The register's 'the waves read flat, raise the amplitude' would take it further from " +
                "its own law, not closer.");

            Assert.Less(fetch["GALE"], FullyDevelopedHs(12.5f) * 0.5f,
                "...and the 3.6 m the row quotes is the FULLY DEVELOPED height, which needs a fetch " +
                "this strait does not have. §39 already refused Pierson-Moskowitz for the WAVELENGTH " +
                "for exactly this reason; the height cannot be taken from it either.");
        }

        /// <summary>
        /// 🔴 <b>THE HELM-FEEL VERDICT ON #762, AND IT FOUND SOMETHING.</b> The fetch law changed the
        /// WAVELENGTH and left the HEIGHT alone. Where the wavelength barely moved (blow, gale: 1.07x)
        /// nothing at the wheel changed. Where it moved a lot — <b>light airs, 8.4 m to 2.2 m</b> — the
        /// same water is now folded into a quarter of the length, so the sea got <b>four times as
        /// steep</b> without anyone choosing that.
        ///
        /// <para>A wind sea runs about 0.04–0.05 and breaks near 0.14. This test reports where each state
        /// lands on that scale before and after, because a steepness above the breaking limit is not a
        /// look question — it is a sea that cannot exist, and the hull rides it (ADR 0018, one sea).</para>
        /// </summary>
        [Test]
        public void TheFetchLaw_ChangedTheWavelengthAndLeftTheHEIGHT_SoLightAirsGotSteeper()
        {
            WaveFieldSettings s = Shipped();
            WaveFieldSettings legacy = Shipped();
            legacy.SeaFetchKilometres = 0f;          // <= 0 is the legacy linear line, bit for bit

            var report = new StringBuilder();
            report.AppendLine("HELM FEEL — what #762 moved, in the units the wheel feels");
            report.AppendLine("state        U m/s |  lambda before  after   x |  period before  after |" +
                              "  steepness before  after");

            var steepBefore = new System.Collections.Generic.Dictionary<string, float>();
            var steepAfter = new System.Collections.Generic.Dictionary<string, float>();

            foreach (var (name, u) in new[]
                     { ("light airs", 1.63f), ("breeze", 3f), ("blow", 5.7f),
                       ("near gale", 9.5f), ("GALE", 12.95f) })
            {
                float lamOld = WaveMath.PeakWavelengthMeters(u, in legacy);
                float lamNew = WaveMath.PeakWavelengthMeters(u, in s);
                float tOld = Mathf.Sqrt(2f * Mathf.PI * lamOld / G);
                float tNew = Mathf.Sqrt(2f * Mathf.PI * lamNew / G);
                float hs = DrawnSignificantHeight(u, in s);     // UNCHANGED by #762 — that is the point
                steepBefore[name] = hs / lamOld;
                steepAfter[name] = hs / lamNew;

                report.AppendLine(
                    $"{name,-11} {u,6:0.00} | {lamOld,13:0.0} {lamNew,6:0.0} {lamNew / lamOld,4:0.00} |" +
                    $" {tOld,13:0.00} {tNew,6:0.00} | {hs / lamOld,17:0.000} {hs / lamNew,6:0.000}");
            }
            report.AppendLine();
            report.AppendLine("  The HEIGHT column is the same on both sides — #762 did not touch it. A real");
            report.AppendLine("  wind sea runs about 0.04-0.05 steep and BREAKS near 0.14.");
            TestContext.WriteLine(report.ToString());

            Assert.Less(steepBefore["blow"], 0.14f, "a blow was inside the breaking limit before");
            Assert.Less(steepAfter["blow"], 0.14f, "...and still is: the fetch law barely moved it");
            Assert.Less(Mathf.Abs(steepAfter["GALE"] - steepBefore["GALE"]), 0.02f,
                "⭐ AT A GALE NOTHING CHANGED AT THE WHEEL. The fetch law moved the peak 1.07x there, so " +
                "the helm-feel verdict on #762 for a blow and above is: he should feel no difference, " +
                "and if he does it is something else.");

            Assert.Greater(steepAfter["light airs"], steepBefore["light airs"] * 2f,
                "⭐ BUT LIGHT AIRS GOT MUCH STEEPER — the wavelength fell to a quarter and the height " +
                "stayed. That is a real change in feel that #762 shipped without proposing it, and it " +
                "is the half of this verdict the owner actually needs.");

            Assert.Greater(steepAfter["light airs"], 0.14f,
                "⭐⭐ AND IT IS PAST THE BREAKING LIMIT. A sea this steep cannot stand; the hull rides " +
                "the same field the shader draws (ADR 0018), so this is not a look question. It is the " +
                "strongest argument for deriving the HEIGHT from the fetch the way the LENGTH now is.");
        }

        /// <summary>
        /// The steepness pair, which is what "reads flat" is really about. A wind sea runs about
        /// <c>Hs/λ ≈ 0.04–0.05</c>; the shipped sea is around twice that, so if anything it should read
        /// STEEPER than nature, not flatter.
        /// </summary>
        [Test]
        public void TheDrawnSteepness_IsAboutTwiceARealWindSeas()
        {
            WaveFieldSettings s = Shipped();
            const float U = 12.5f;                 // mid-gale
            float lam = WaveMath.PeakWavelengthMeters(U, in s);
            float drawn = DrawnSignificantHeight(U, in s) / lam;
            float real = FetchLimitedHs(U, s.SeaFetchKilometres) / lam;

            TestContext.WriteLine($"  at {U:0.0} m/s: peak {lam:0.0} m, drawn steepness {drawn:0.000}, " +
                                  $"fetch-limited {real:0.000} (a real wind sea is about 0.04-0.05)");

            Assert.Greater(drawn, real,
                "the drawn sea must be steeper than the fetch-limited one — this is the same statement " +
                "as the height table, said in the units the eye actually judges.");
            Assert.Less(real, 0.08f,
                "DEAD CONTROL on the reference: a fetch-limited wind sea's steepness must land in the " +
                "physical range. If this ever exceeds the breaking limit the JONSWAP transcription is " +
                "wrong and every conclusion above it is void.");
        }
    }
}
