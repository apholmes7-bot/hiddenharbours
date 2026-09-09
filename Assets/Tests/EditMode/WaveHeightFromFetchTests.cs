using System;
using System.Text;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 33 — #762 moved the WAVELENGTH onto a fetch law and left the HEIGHT behind,
    /// so the two halves of one sea drifted apart. ✅ RULED AND SHIPPED (owner, 2026-09-09): "height
    /// from fetch".</b>
    ///
    /// <para>The superseded height was <c>PrimaryAmplitude 0.8 × SeaState01^exponent</c> — a tuned
    /// pair with no relation to the wavelength law at all. Measured against the same JONSWAP
    /// fetch-limited growth curves the wavelength already uses (25 km, the shipped strait):</para>
    /// <code>
    ///   U m/s   drawn Hs   JONSWAP Hs   drawn/real      after this PR
    ///    2.00      0.157        0.098        1.59x   ->      1.00x
    ///    4.00      0.435        0.323        1.35x   ->      1.00x
    ///    5.70      0.778        0.460        1.69x   ->      1.00x
    /// </code>
    ///
    /// <para><b>The law</b> is the height twin of #762's wavelength law, from the same family:
    /// <c>g·Hs/U² = 0.0016·√(g·X/U²)</c>, capped by the fully-developed Pierson–Moskowitz ceiling
    /// <c>Hs = 0.0246·U²</c> — a strait cannot raise a bigger sea than the open ocean would. It is a
    /// law with tunables (<c>FetchHeightCoefficient</c>, <c>FullyDevelopedHeightCoefficient</c>,
    /// <c>HeightStyleScale</c>), not a table, so the two halves cannot drift again.</para>
    ///
    /// <para>⚠️ <b>Row 33's OTHER headline was withdrawn in PR B, and this file does not restore it.</b>
    /// The register's <i>"steepness 0.182, past the ~0.14 breaking limit"</i> was measured on
    /// <c>WaveFieldSettings.Default</c> — a sea nobody sails (see <see cref="ShippedWaveField"/>). On
    /// the shipped config the superseded steepness never exceeded 0.105 and never breached the limit.
    /// So the steepness assertions below are a <b>guard against a future regression</b>, not evidence
    /// of a defect that was there. The defect this PR fixes is the height being 1.3–1.8× too tall.</para>
    ///
    /// <para>Pure arithmetic over the shipped derivation: no scene, no clock, no graphics device.</para>
    /// </summary>
    public class WaveHeightFromFetchTests
    {
        const float G = 9.81f;

        /// <summary>The breaking limit for a wind sea. Steeper than about this and a wave is not a
        /// wave any more, it is whitewater — so a field that draws past it is drawing something that
        /// cannot exist.</summary>
        const float BreakingSteepness = 0.14f;

        static WaveFieldSettings Shipped() => ShippedWaveField.Settings();

        static WaveFieldSettings Superseded()
        {
            WaveFieldSettings s = ShippedWaveField.Settings();
            s.HeightFromFetch = false;
            return s;
        }

        static float SeaState01(float windSpeed) => Mathf.Clamp01(windSpeed / 10f);

        static WaveTrains At(float windSpeed, in WaveFieldSettings s)
            => WaveMath.TrainsFrom(new Vector2(0f, windSpeed), SeaState01(windSpeed), in s);

        static float PeakLambda(float windSpeed, in WaveFieldSettings s)
            => Mathf.Clamp(WaveMath.PeakWavelengthMeters(windSpeed, in s),
                           WaveTrain.MinWavelengthMeters, s.DominantWavelengthMax);

        /// <summary>#797's measured band: the shipped wind law cannot leave [0.50, 5.70] m/s.</summary>
        static readonly float[] ReachableWinds =
        { 0.50f, 1.00f, 1.63f, 2.00f, 3.00f, 4.00f, 5.00f, 5.70f };

        /// <summary>The register's gale and storm rows. ⚠️ NOT reachable under the shipped wind law
        /// (#797) — asserted anyway, because the owner has ruled the cap is coming off (PR D) and a
        /// height law that only holds inside today's band would be a trap waiting for that PR.</summary>
        static readonly float[] UnreachableButComingWinds =
        { 8f, 10f, 12.95f, 17f, 20f, 25f };

        // =============================================================================================

        /// <summary>
        /// ✅ <b>THE RULING, AT ITS ROOT: the drawn sea's significant height IS the fetch-limited
        /// JONSWAP height.</b> Not "close to", not "scaled from" — the same number, because the field
        /// is scaled onto it after it is built.
        /// </summary>
        [Test]
        public void TheDrawnHeight_IsTheFetchLimitedJonswapHeight_AtEveryWind()
        {
            WaveFieldSettings s = Shipped();
            var report = new StringBuilder();
            report.AppendLine("  U m/s |  drawn Hs | JONSWAP Hs | drawn/real | superseded/real");

            foreach (float u in Concat(ReachableWinds, UnreachableButComingWinds))
            {
                float drawn = WaveMath.SignificantHeightMeters(At(u, in s));
                float law = WaveMath.FetchLimitedSignificantHeightMeters(u, in s);
                var old = Superseded();
                float was = WaveMath.SignificantHeightMeters(At(u, in old));
                report.AppendLine($"  {u,5:0.00} | {drawn,9:0.000} | {law,10:0.000} | " +
                                  $"{(law > 1e-6f ? drawn / law : 0f),10:0.00}x | " +
                                  $"{(law > 1e-6f ? was / law : 0f),14:0.00}x");

                Assert.AreEqual(law, drawn, Mathf.Max(1e-4f, law * 0.01f),
                    $"⭐ at {u:0.00} m/s the field draws Hs {drawn:0.000} m where the fetch law asks " +
                    $"for {law:0.000} m. The whole of row 33 is that these two are ONE number: the " +
                    "height and the wavelength now come from the same JONSWAP curve, so they cannot " +
                    "drift apart the way #762 left them.");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// ⚠️ <b>AND THE SUPERSEDED PAIR REALLY WAS TALLER — kept as the record of why the law moved.</b>
        /// Written about the OLD value against the reference rather than the new one against it, so it
        /// cannot redden on its own fix.
        /// </summary>
        [Test]
        public void TheSupersededPair_DrewASeaHalfAgainTooTall_WhichIsWhyTheLawMoved()
        {
            WaveFieldSettings old = Superseded();
            foreach (var (u, atLeast) in new[] { (2f, 1.4f), (4f, 1.25f), (5.7f, 1.5f) })
            {
                float was = WaveMath.SignificantHeightMeters(At(u, in old));
                float law = WaveMath.FetchLimitedSignificantHeightMeters(u, in old);
                Assert.Greater(was / law, atLeast,
                    $"⭐ ROW 33's DEFECT, kept: at {u:0.0} m/s the superseded tuned pair drew " +
                    $"{was / law:0.00}x the height 25 km of fetch can raise. That is the drift #762 " +
                    "left when it moved the wavelength and not the height.");
            }
        }

        /// <summary>
        /// ⚠️ <b>THE STEEPNESS GUARD — a rail for the future, not a record of a defect.</b> H/λ past
        /// about 0.14 is whitewater, not a wave. Checked at every reachable wind AND at the register's
        /// gale/storm rows, because the owner has ruled the wind cap is coming off (PR D) and a law
        /// that only behaves inside today's band would fail exactly when that lands.
        /// </summary>
        [Test]
        public void TheSteepness_StaysUnderTheBreakingLimit_IncludingTheWindsPRDWillUnlock()
        {
            WaveFieldSettings s = Shipped();
            var report = new StringBuilder("  U m/s |     lambda |     Hs |   H/lam | reachable today?\n");

            foreach (float u in Concat(ReachableWinds, UnreachableButComingWinds))
            {
                float lam = PeakLambda(u, in s);
                float hs = WaveMath.SignificantHeightMeters(At(u, in s));
                float steepness = hs / lam;
                bool reachable = u <= 5.70f;
                report.AppendLine($"  {u,5:0.00} | {lam,10:0.00} | {hs,6:0.000} | {steepness,7:0.0000} | " +
                                  (reachable ? "yes" : "not until PR D"));

                Assert.LessOrEqual(steepness, BreakingSteepness,
                    $"at {u:0.00} m/s the drawn sea is H/lambda = {steepness:0.0000}, past the ~0.14 " +
                    "breaking limit — a wave that steep does not exist, it has already broken. Since " +
                    "both halves now come from the same fetch curve this should be structural; if it " +
                    "ever fires, the height law and the wavelength law have come apart again.");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// The height must rise with the wind at a fixed fetch, everywhere — a sea that got smaller as
        /// it blew harder would be a sign-flip somewhere in the growth curve or its Pierson–Moskowitz
        /// cap, and it would read at the helm as the weather working backwards.
        /// </summary>
        [Test]
        public void TheHeight_IsMonotoneInWind_AtFixedFetch()
        {
            WaveFieldSettings s = Shipped();
            float previous = -1f;
            for (float u = 0f; u <= 30f; u += 0.1f)
            {
                float hs = WaveMath.FetchLimitedSignificantHeightMeters(u, in s);
                Assert.GreaterOrEqual(hs, previous - 1e-6f,
                    $"the sea got SHORTER between {u - 0.1f:0.0} and {u:0.0} m/s — the fetch curve and " +
                    "its fully-developed cap must meet without a step, or the sea shrinks as the " +
                    "weather builds");
                previous = hs;
            }
            Assert.Greater(previous, 0f, "…and it must actually have grown");
        }

        /// <summary>
        /// ✅ <b>Zero fetch is zero sea</b> — the law's own lee floor. This is the end the spatial
        /// envelope is heading for, and it must be reached by the LAW rather than by a clamp, or a
        /// sheltered corner would still carry a swell that had nowhere to come from.
        /// </summary>
        [Test]
        public void AtZeroFetch_ThereIsNoSeaAtAll()
        {
            WaveFieldSettings s = Shipped();
            s.SeaFetchKilometres = 0f;
            foreach (float u in Concat(ReachableWinds, UnreachableButComingWinds))
                Assert.AreEqual(0f, WaveMath.FetchLimitedSignificantHeightMeters(u, in s), 0f,
                    $"at {u:0.00} m/s with no fetch the law must give exactly no height");
        }

        /// <summary>
        /// ✅ <b>GLASS IS STILL SACRED, and it needed saying twice.</b> The height is now a function of
        /// the WIND, so the sea-state term no longer carries the mirror by itself — <c>GlassGate</c>
        /// does, and this is the guard that it does. Bit-exact zero, not "very small": ADR 0018's
        /// ruling is that at sea state 0 the field is a full mirror for the reflection layers.
        /// </summary>
        [Test]
        public void GlassIsStillExactlyGlass_HoweverHardTheWindIsBlowing()
        {
            WaveFieldSettings s = Shipped();
            foreach (float u in new[] { 0f, 2f, 5.7f, 12.95f, 25f })
            {
                WaveTrains field = WaveMath.TrainsFrom(new Vector2(u, 1f), 0f, in s);
                for (int i = 0; i < field.Count; i++)
                    Assert.AreEqual(0, BitConverter.SingleToInt32Bits(field[i].Amplitude),
                        $"wind {u:0.00} m/s, sea state 0: slot {i} must be EXACTLY +0. The height law " +
                        "reads the wind, so without GlassGate a dead-calm sea state with any wind on " +
                        "it would still draw waves — and row 5's mirror would never arrive.");
                Assert.AreEqual(0, BitConverter.SingleToInt32Bits(field.TotalAmplitude));
            }

            // …and the gate is fully open well below the sea state the wind law can actually reach,
            // so in play the height is purely the fetch law's and the gate is invisible.
            Assert.AreEqual(1f, WaveMath.GlassGate(0.143f, in s), 1e-6f,
                "⭐ at #797's floor — the calmest sea the wind law can produce — the glass gate must " +
                "already be fully open, or it would be quietly scaling the height of every sea the " +
                "player ever sees instead of only guarding the mirror.");
            Assert.Less(WaveMath.GlassGate(0.01f, in s), 1f,
                "…while still ramping below that, so the approach to glass is continuous");
        }

        /// <summary>
        /// ⚠️ <b>THE LEE ENVELOPE IS NOT FOLDED IN, and this is load-bearing.</b>
        /// <c>WaveFetch.EnvelopeAt</c> is a SPATIAL multiplier applied at sample time. Folding it into
        /// the trains' amplitudes would put it inside <see cref="WaveTrains.TotalAmplitude"/> — which
        /// is the whitecap crest-factor normalizer AND the bound the watertight hull clamp scans
        /// against — so a sheltered corner would quietly change how foam is scored and how high every
        /// hull sits. <c>WaveFetch.cs</c> §"What falls out for free" is the long form.
        /// </summary>
        [Test]
        public void TheLeeEnvelope_IsNotFoldedIntoTheTrains_SoTotalAmplitudeStaysTheOpenWaterBound()
        {
            WaveFieldSettings s = Shipped();
            WaveTrains field = At(5.70f, in s);
            float openWater = field.TotalAmplitude;

            // The envelope enters at SAMPLE time, and only there.
            var p = new Vector2(12.5f, -3.25f);
            float full = WaveMath.Sample(p, 41.0, in field, 1f).Height;
            float lee = WaveMath.Sample(p, 41.0, in field, 0.15f).Height;

            Assert.AreEqual(openWater, field.TotalAmplitude, 0f,
                "sampling must not mutate the field");
            Assert.AreEqual(full * 0.15f, lee, Mathf.Max(1e-5f, Mathf.Abs(full) * 1e-3f),
                "the envelope must scale the SAMPLE, linearly, and nothing else");

            // And the height law itself is the open-water one: the envelope is not in it.
            Assert.AreEqual(WaveMath.FetchLimitedSignificantHeightMeters(5.70f, in s),
                            WaveMath.SignificantHeightMeters(in field),
                            Mathf.Max(1e-4f, openWater * 0.01f),
                "⭐ the field's own height is the OPEN-WATER fetch height. If the lee envelope had " +
                "been folded into the derivation this would read low, and TotalAmplitude — the foam " +
                "normalizer and the hull clamp's bound — would have moved with the shoreline.");
        }

        /// <summary>
        /// ⚠️ <b>The superseded pair is still reachable, and is what an old asset gets.</b>
        /// <c>HeightFromFetch</c> is a bool, and an asset serialized before 2026-09-09 has no such key
        /// — which deserializes to <b>false</b>, i.e. straight to the tuned pair it shipped with. That
        /// is the safe direction, and it is asserted rather than assumed.
        /// </summary>
        [Test]
        public void AnAssetWithoutTheKey_KeepsTheSeaItShippedWith()
        {
            WaveFieldSettings old = Superseded();
            foreach (float u in ReachableWinds)
            {
                WaveTrains field = At(u, in old);
                float sea = SeaState01(u);
                float expectedPrimary = Mathf.Max(0f, old.PrimaryAmplitude)
                                      * Mathf.Pow(sea, Mathf.Max(0.01f, old.SeaStateAmplitudeExponent));
                float expectedTotal = expectedPrimary * (1f + old.Secondary1AmplitudeRatio
                                                            + old.Secondary2AmplitudeRatio
                                                            + old.Secondary3AmplitudeRatio);
                Assert.AreEqual(expectedTotal, field.TotalAmplitude,
                                Mathf.Max(1e-4f, expectedTotal * 0.02f),
                    $"at {u:0.00} m/s with HeightFromFetch off, the envelope must be the superseded " +
                    "PrimaryAmplitude x SeaState01^exponent product, unchanged. The spectrum " +
                    "normalizes onto that envelope, so this is the whole of the old height law.");
            }
        }

        /// <summary>
        /// ⚠️ <b>The style dial is a dial, and it is the only stylised height left.</b> Once a law is
        /// doing the deriving, "make it taller than nature" must live in one named place instead of
        /// creeping back into the coefficients — this is that place, and it ships at 1 (the physical
        /// sea) until the owner says otherwise.
        /// </summary>
        [Test]
        public void TheStyleScale_IsLinear_AndShipsAtThePhysicalSea()
        {
            Assert.AreEqual(1f, ShippedWaveField.Settings().HeightStyleScale, 1e-6f,
                "the shipped sea is the physical one; a stylised height is an owner ruling, not a default");

            WaveFieldSettings s = Shipped();
            float baseline = WaveMath.SignificantHeightMeters(At(5.70f, in s));
            foreach (float k in new[] { 0.5f, 1.5f, 2f })
            {
                WaveFieldSettings t = Shipped();
                t.HeightStyleScale = k;
                Assert.AreEqual(baseline * k, WaveMath.SignificantHeightMeters(At(5.70f, in t)),
                                Mathf.Max(1e-4f, baseline * k * 0.01f),
                    $"HeightStyleScale {k} must scale the drawn height linearly and nothing else");
            }
        }

        static float[] Concat(float[] a, float[] b)
        {
            var r = new float[a.Length + b.Length];
            Array.Copy(a, r, a.Length);
            Array.Copy(b, 0, r, a.Length, b.Length);
            return r;
        }
    }
}
