using System;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Water fidelity PR 11b — the wake DISPERSES. The owner, 2026-09-04 and 09-06:
    /// <i>"foam fades behind boat but doesnt disperse in width"</i> ·
    /// <i>"the foam always stays to the original foam path, it doesnt widen over time and fade
    /// away."</i>
    ///
    /// <para><b>What these pin, and what they refuse to pin.</b> Whether the trail READS as
    /// dispersing is the owner's eye and no test here asserts it. What is testable is the arithmetic
    /// the charter names: that widening the footprint changes the DISTRIBUTION of a hull's churn and
    /// never its total; that the naive skirt fails the same statement; that the width is monotone and
    /// inside the Kelvin envelope; and that the dial at 0 is the shipped stamp exactly.</para>
    ///
    /// <para><b>No absolute bars.</b> Every threshold is either exact (an identity, an off switch) or
    /// a RATIO between two arms shot in the same run — <c>a-guard-with-an-absolute-bar-rots-on-a-good-change</c>.
    /// The sabotage arm is the calibration, and it is computed beside the honest one so the two can
    /// never drift apart.</para>
    /// </summary>
    public class WakeDispersalStampTests
    {
        // The cape at 8 kn: CapeIslanderIsoHullMesh.WatertightHalfBeamMeters is what the presentation
        // service hands the injector as its band half-width, and 8 kn is the charter's plate speed.
        const float CapeHalfBeam = 2.4f;
        const float EightKnots = 8f * 0.514444f;
        const float DepositRate = 2.5f;      // FoamInjector._depositPerSecond at full churn
        const float Dt = 1f / 60f;
        const float Kelvin = 0.8f;           // FoamInjector._spreadKelvinFraction, shipped
        const float Envelope = 2.5f;         // FoamInjector._spreadEnvelopeHalfBeams, shipped
        const float ComposeThreshold = 0.12f; // _WakeFoamThreshold on the water material

        // ==== the constants, re-derived rather than trusted =========================================

        /// <summary>
        /// <see cref="FoamBuffer.StampAreaIntegral"/> is the AREA integral of the shipped stamp, and
        /// it is the whole reason the conserved quantity is what it is: a parcel of sea is stamped for
        /// its entire DWELL under a passing hull, not once. Reaching for the 1.5·r₀ lateral
        /// cross-section instead — the intuitive number — makes an "area-preserving" term lay 1.4× the
        /// shipped foam. Quadrature, against the closed form.
        /// </summary>
        [Test]
        public void StampAreaIntegral_IsTheDiscsOwnQuadrature_NotTheLateralCrossSection()
        {
            const int n = 200001;
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                double rho = i / (double)(n - 1);
                double w = (i == 0 || i == n - 1) ? 1 : (i % 2 == 1 ? 4 : 2);
                sum += w * FoamBuffer.Profile((float)rho, 1f) * rho;
            }
            double quad = 2 * Math.PI * sum / (3.0 * (n - 1));
            Assert.AreEqual(quad, FoamBuffer.StampAreaIntegral, 1e-5,
                "StampAreaIntegral must be the disc's own area integral (0.575*pi).");
            Assert.AreNotEqual(FoamBuffer.ProfileLateralIntegral, FoamBuffer.StampAreaIntegral,
                "The lateral cross-section and the area integral are different numbers — using the " +
                "first where the second belongs is the 1.4x over-lay this term was measured into.");
        }

        /// <summary>The envelope's peak is a root of a cubic, not a fitted number: found by search
        /// here, hard-coded there.</summary>
        [Test]
        public void EnvelopePeak_IsFoundBySearch_NotTakenOnTrust()
        {
            float bestX = 0f, best = 0f;
            for (int i = 0; i <= 2000000; i++)
            {
                float x = i / 2000000f;
                float h = x * FoamBuffer.Profile(x, 1f);
                if (h > best) { best = h; bestX = x; }
            }
            Assert.AreEqual(bestX, FoamBuffer.EnvelopePeakX, 1e-4f, "EnvelopePeakX drifted.");
            Assert.AreEqual(best, FoamBuffer.EnvelopePeak, 1e-4f, "EnvelopePeak drifted.");
        }

        /// <summary>The running integral's closed form against a direct quadrature of the same
        /// profile — the two are independent computations of one quantity.</summary>
        [Test]
        public void ProfileRunningIntegral_ClosedForm_MatchesQuadrature()
        {
            for (float x = 0f; x <= 1.0001f; x += 0.05f)
            {
                const int n = 20001;
                double sum = 0;
                for (int i = 0; i < n; i++)
                {
                    double u = x * i / (n - 1);
                    double w = (i == 0 || i == n - 1) ? 1 : (i % 2 == 1 ? 4 : 2);
                    sum += w * FoamBuffer.Profile((float)u, 1f);
                }
                double quad = x * sum / (3.0 * (n - 1));
                Assert.AreEqual(quad, FoamBuffer.ProfileRunningIntegral(x), 1e-5,
                    $"ProfileRunningIntegral({x:0.00}) disagrees with its own quadrature.");
            }
            Assert.AreEqual(0.75f, FoamBuffer.ProfileRunningIntegral(1f), 1e-6f,
                "F(1) is half of ProfileLateralIntegral, exactly.");
        }

        /// <summary>The envelope is a MAX over the family; the closed form must reproduce the search
        /// that defines it, at every distance including both ends.</summary>
        [Test]
        public void EnvelopeShape_ClosedForm_MatchesTheMaxOverTheFamily()
        {
            const float r0 = CapeHalfBeam;
            float wMax = r0 * Envelope;
            for (float d = 0f; d <= wMax + 0.2f; d += 0.05f)
            {
                float search = 0f;
                for (int i = 0; i <= 4096; i++)
                {
                    float w = Mathf.Lerp(r0, wMax, i / 4096f);
                    search = Mathf.Max(search, FoamBuffer.Profile(d, w) / w);
                }
                Assert.AreEqual(search, FoamBuffer.EnvelopeShape(d, r0, wMax), 1e-4f,
                    $"EnvelopeShape({d:0.00}) is not the family's own maximum.");
            }
        }

        // ==== the dial at 0 is the shipped stamp, exactly ===========================================

        /// <summary>
        /// 🔴 <b>THE PASSTHROUGH.</b> At a spread of 0 nothing about a hull's injection differs from
        /// what PR 11a shipped: no slope, no dispersing share taken out of the stamp, no edge. Exact
        /// zeros, not small ones — the injector multiplies its amount by <c>1 − share</c> only when
        /// the share is positive, so a share of exactly 0 leaves that line untouched bit for bit.
        /// </summary>
        [Test]
        public void AtSpreadZero_NothingIsTakenFromTheStamp_AndThereIsNoEdge()
        {
            Assert.AreEqual(0f, FoamBuffer.SpreadSlope(0f), "A spread of 0 must be no slope at all.");
            Assert.AreEqual(0f, FoamBuffer.DispersingShare(CapeHalfBeam, CapeHalfBeam),
                "An envelope equal to the band is no dispersal, so no share may leave the stamp.");
            Assert.AreEqual(0f, FoamBuffer.EdgeGain(DepositRate, CapeHalfBeam, CapeHalfBeam * Envelope,
                                                    0f, Dt, 0.25f),
                "A spread of 0 must gate the edge off however wide the envelope is dialled.");
            Assert.AreEqual(0f, FoamBuffer.EdgeGain(DepositRate, CapeHalfBeam, CapeHalfBeam, Kelvin,
                                                    Dt, 0.25f),
                "An envelope of 1 half-beam must gate the edge off however hard the spread is dialled.");
        }

        /// <summary>
        /// 🔴 <b>A HULL WITH NO WAY ON MAKES NO EDGE.</b> The edge lays the envelope ONCE, as its rim
        /// sweeps past a parcel — and the rim only moves because she does. With no way on it would
        /// stand still and paint the same ring into the same water every frame until it saturated,
        /// which is a burnt-in circle round a moored boat.
        ///
        /// <para>So the injector feeds the edge the <b>wake channel alone</b> and takes the dispersing
        /// share out of only that part of the stamp: a hull slapping at anchor churns in place, exactly
        /// as she does today, and her bob channel is untouched. Pinned here at the two ends the maths
        /// owns — the shaping curve at rest, and the gain's refusal of a zero rate.</para>
        /// </summary>
        [Test]
        public void WithNoWayOn_TheWakeChannelIsSilent_AndTheEdgeRefusesToLay()
        {
            const float knee = 3f, exponent = 1f;    // FoamInjector's shipped wake channel
            Assert.AreEqual(0f, FoamBuffer.Shape01(0f, knee, exponent),
                "At rest the wake channel contributes nothing, so the edge is fed nothing.");
            Assert.AreEqual(0f, FoamBuffer.EdgeGain(0f, CapeHalfBeam, CapeHalfBeam * Envelope,
                                                    Kelvin, Dt, 0.25f),
                "A zero deposit rate must gate the edge off outright — never a small ring standing " +
                "still in the water.");
            Assert.Greater(FoamBuffer.Shape01(EightKnots, knee, exponent), 0f,
                "DEAD CONTROL: under way the same channel must be loud, or the test above passes " +
                "because the curve is broken rather than because she is stopped.");
        }

        /// <summary>The share is DERIVED from the envelope, not dialled — so it cannot be tuned into
        /// disagreeing with the geometry it is supposed to describe.</summary>
        [Test]
        public void DispersingShare_IsTheProfilesOwnGeometry_AndRisesWithTheEnvelope()
        {
            Assert.AreEqual(1f / 3f, FoamBuffer.DispersingShare(1f, 2f), 1e-5f,
                "At a 2x envelope exactly a third of the spread profile lies outside the band.");
            Assert.AreEqual(7f / 15f, FoamBuffer.DispersingShare(1f, 2.5f), 1e-5f,
                "At a 2.5x envelope it is 7/15.");
            float previous = -1f;
            for (float ratio = 1f; ratio <= 6f; ratio += 0.1f)
            {
                float share = FoamBuffer.DispersingShare(1f, ratio);
                Assert.GreaterOrEqual(share, previous, "The share must never fall as the envelope grows.");
                Assert.Less(share, 1f, "The band can never give up all of its churn.");
                previous = share;
            }
        }

        // ==== the width: monotone, and inside the Kelvin envelope ===================================

        /// <summary>
        /// <c>width(age)</c> is monotone non-decreasing and never outside the Kelvin envelope
        /// (<c>speed · tan 19.5°</c>) at any age — the charter's own bound, and the one thing about
        /// this term that is physics rather than art.
        /// </summary>
        [Test]
        public void Width_IsMonotone_AndNeverOutsideTheKelvinEnvelope()
        {
            float slope = FoamBuffer.SpreadSlope(Kelvin);
            Assert.Less(slope, FoamBuffer.KelvinSlope,
                "The churn inside the arms spreads slower than the arms themselves.");
            Assert.AreEqual(FoamBuffer.KelvinSlope, FoamBuffer.SpreadSlope(1f), 1e-6f,
                "A fraction of 1 is the Kelvin slope exactly.");
            Assert.AreEqual(FoamBuffer.KelvinSlope, FoamBuffer.SpreadSlope(4f), 1e-6f,
                "Above 1 it must CLAMP to the envelope, never exceed it.");

            float previous = CapeHalfBeam - 1f;
            for (float age = 0f; age <= 8f; age += 0.05f)
            {
                float s = EightKnots * age;
                float w = CapeHalfBeam + slope * s;
                Assert.GreaterOrEqual(w, previous, "width(age) went backwards.");
                Assert.LessOrEqual(w, CapeHalfBeam + FoamBuffer.KelvinSlope * s + 1e-4f,
                    $"width at age {age:0.00}s is outside the Kelvin envelope.");
                previous = w;
            }
        }

        // ==== conservation, per AGE BIN, at INJECTION ===============================================

        /// <summary>How much foam each arm lays on one parcel of sea over its whole life, binned by
        /// the parcel's age. A whole-trail total is forbidden as the guard (it always favours the
        /// shipped arm), so everything below is per bin and per arm.</summary>
        struct Lay
        {
            public double[] Bins;      // lateral integral of the deposit, per 1-second age bin
            public double Total;
            public float DrawnHalfWidth;    // outermost cell whose accumulated coverage draws
            public float EdgeAmountAtRim;   // per-frame value the edge writes at that outermost cell
        }

        enum Arm { Shipped, AreaPreserving, NaiveSkirt }

        /// <summary>
        /// One parcel of sea, walked through its life under a hull passing at a steady speed. The
        /// parcel's clock starts BEFORE the transom reaches it, because the shipped disc laps a parcel
        /// from a radius ahead to a radius astern and counting only the astern half would halve the
        /// very arm everything is conserved against.
        /// </summary>
        static Lay Walk(Arm arm, float r0, float envelopeRatio, float kelvin, float speed,
                        float rate, float dt, int binCount = 6, float lifeSeconds = 6f,
                        float lateralMetres = 26f)
        {
            int nd = Mathf.RoundToInt(lateralMetres / FoamBuffer.CellSize);
            var coverage = new double[nd];
            var lay = new Lay { Bins = new double[binCount] };

            float slope = FoamBuffer.SpreadSlope(kelvin);
            float wMax = arm == Arm.Shipped ? r0 : r0 * envelopeRatio;
            float reach = slope > 0f && wMax > r0 ? (wMax - r0) / slope : 0f;
            float share = arm == Arm.AreaPreserving ? FoamBuffer.DispersingShare(r0, wMax) : 0f;
            float edgeWidth = FoamBuffer.EdgeWidth(slope * speed, dt);
            float gain = arm == Arm.AreaPreserving
                ? FoamBuffer.EdgeGain(rate, r0, wMax, kelvin, dt, edgeWidth) : 0f;
            float decay = FoamBuffer.DecayFactor(6f, dt);   // IsoFacetHullFeature._foamHalfLifeSeconds

            int leadSteps = Mathf.CeilToInt(r0 / Mathf.Max(speed, 1e-3f) / dt) + 1;
            int steps = Mathf.CeilToInt(lifeSeconds / dt);
            for (int k = -leadSteps; k < steps; k++)
            {
                float age = k * dt;
                float astern = speed * age;
                double binSum = 0;
                for (int i = 0; i < nd; i++)
                {
                    float d = (i + 0.5f) * FoamBuffer.CellSize;
                    double deposit = 0;

                    // the stamp itself — the capsule PR 11a ships, minus whatever disperses
                    if (Mathf.Abs(astern) <= r0)
                        deposit += rate * dt * (1f - share)
                                 * FoamBuffer.Profile(Mathf.Sqrt(astern * astern + d * d), r0);

                    if (age >= 0f && reach > 0f && astern <= reach)
                    {
                        float u = astern / reach;
                        float edge = Mathf.Lerp(r0, wMax, u);
                        if (arm == Arm.AreaPreserving)
                        {
                            float bump = FoamBuffer.EdgeBump(d, edge, edgeWidth);
                            deposit += gain * FoamBuffer.EnvelopeShape(d, r0, wMax) * bump;
                        }
                        else if (arm == Arm.NaiveSkirt)
                        {
                            // 🔴 THE SABOTAGE ARM: widen the stamp and keep the amount — the thing
                            // PR 11a measured multiplying the foam (9356 -> 13052 drawn integral).
                            deposit += rate * dt * FoamBuffer.Profile(d, edge);
                        }
                    }

                    coverage[i] = Math.Min(1.0, coverage[i] * decay + deposit);
                    binSum += deposit;
                }
                int bin = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(age, 0f)), 0, binCount - 1);
                lay.Bins[bin] += 2.0 * binSum * FoamBuffer.CellSize;   // both sides of the track
                lay.Total += 2.0 * binSum * FoamBuffer.CellSize;
            }

            for (int i = nd - 1; i >= 0; i--)
            {
                if (coverage[i] < ComposeThreshold) continue;
                lay.DrawnHalfWidth = (i + 0.5f) * FoamBuffer.CellSize;
                break;
            }
            // The per-frame value the edge writes where the band's VISIBLE rim is. Not the smallest
            // value it ever writes — that is the taper running out past the rim, where nothing draws
            // and nothing is owed. This one is the number the 8-bit buffer has to be able to hold.
            lay.EdgeAmountAtRim = gain * FoamBuffer.EnvelopeShape(lay.DrawnHalfWidth, r0, wMax);
            return lay;
        }

        /// <summary>
        /// 🔴 <b>THE CHARTER'S GUARD.</b> Foam laid per metre of track, per AGE BIN. The dispersing
        /// arm redistributes the churn and must lay the same TOTAL as the shipped stamp; the naive
        /// skirt must fail the identical statement, in the same run, or the control is dead.
        /// </summary>
        [Test]
        public void ConservationPerAgeBin_TheAreaPreservingStampHolds_TheNaiveSkirtDoesNot()
        {
            Lay shipped = Walk(Arm.Shipped, CapeHalfBeam, Envelope, Kelvin, EightKnots, DepositRate, Dt);
            Lay area = Walk(Arm.AreaPreserving, CapeHalfBeam, Envelope, Kelvin, EightKnots, DepositRate, Dt);
            Lay naive = Walk(Arm.NaiveSkirt, CapeHalfBeam, Envelope, Kelvin, EightKnots, DepositRate, Dt);

            double analytic = FoamBuffer.ShippedFoamPerMetre(DepositRate, CapeHalfBeam, EightKnots);
            Assert.AreEqual(analytic, shipped.Total, analytic * 0.02,
                "The walked shipped arm must reproduce ShippedFoamPerMetre — if it does not, the " +
                "harness is measuring something other than the quantity being conserved.");

            double areaRatio = area.Total / shipped.Total;
            double naiveRatio = naive.Total / shipped.Total;
            TestContext.WriteLine(
                $"laid per metre of track: shipped {shipped.Total:0.0000} · " +
                $"area-preserving {area.Total:0.0000} ({areaRatio:0.000}x) · " +
                $"naive skirt {naive.Total:0.0000} ({naiveRatio:0.000}x)");
            for (int b = 0; b < shipped.Bins.Length; b++)
                TestContext.WriteLine(
                    $"  age {b}-{b + 1}s: shipped {shipped.Bins[b]:0.0000} · " +
                    $"area {area.Bins[b]:0.0000} · naive {naive.Bins[b]:0.0000}");

            Assert.AreEqual(1.0, areaRatio, 0.03,
                "The area-preserving stamp must change the DISTRIBUTION of the churn and not its " +
                "total; conservation is stated at injection, so this is arithmetic, not tuning.");
            Assert.Greater(naiveRatio, 2.0,
                "DEAD CONTROL: the naive skirt is supposed to multiply the foam. If it no longer " +
                "does, the two arms have converged and this guard proves nothing.");
            Assert.Greater(naiveRatio / areaRatio, 2.0,
                "The honest and sabotage arms must stay separated by the mechanism, not by a bar.");
        }

        /// <summary>
        /// The dispersing arm must lay foam in bins the shipped stamp never reaches — that IS the
        /// widening, stated as arithmetic rather than as an adjective. The shipped stamp finishes
        /// with a parcel within a radius of the transom; the edge is still working on it seconds
        /// later.
        /// </summary>
        [Test]
        public void TheDispersingArm_KeepsWorkingOnWaterTheShippedStampHasFinishedWith()
        {
            Lay shipped = Walk(Arm.Shipped, CapeHalfBeam, Envelope, Kelvin, EightKnots, DepositRate, Dt);
            Lay area = Walk(Arm.AreaPreserving, CapeHalfBeam, Envelope, Kelvin, EightKnots, DepositRate, Dt);

            Assert.AreEqual(0.0, shipped.Bins[1], 1e-9,
                "The shipped stamp is done with a parcel inside its own radius — under a second at 8 kn.");
            Assert.Greater(area.Bins[1], 0.0,
                "The dispersal must still be laying on that parcel a second later, or nothing widens.");

            Assert.Greater(area.DrawnHalfWidth / shipped.DrawnHalfWidth, 1.3f,
                "A RATIO, not a width: the dispersing band must draw meaningfully wider than the " +
                "shipped one under the same compose threshold, and both are measured in this run.");
            TestContext.WriteLine(
                $"drawn half-width: shipped {shipped.DrawnHalfWidth:0.00} m · " +
                $"dispersing {area.DrawnHalfWidth:0.00} m " +
                $"({area.DrawnHalfWidth / shipped.DrawnHalfWidth:0.00}x)");
        }

        // ==== the edge itself ========================================================================

        /// <summary>
        /// Sweeping the edge once past a point deposits exactly the envelope value there — no more,
        /// which is what stops a re-stamped skirt accumulating, and no less, which is what makes the
        /// conservation above true rather than approximately true.
        /// </summary>
        [Test]
        public void OneSweepOfTheEdge_LaysExactlyTheEnvelope()
        {
            const float r0 = CapeHalfBeam;
            float wMax = r0 * Envelope;
            float slope = FoamBuffer.SpreadSlope(Kelvin);
            float edgeWidth = FoamBuffer.EdgeWidth(slope * EightKnots, Dt);
            float gain = FoamBuffer.EdgeGain(DepositRate, r0, wMax, Kelvin, Dt, edgeWidth);
            float step = slope * EightKnots * Dt;      // how far the edge moves in a frame

            // What the envelope's amplitude has to be for the annulus to receive exactly the
            // dispersing share of the shipped foam. Computed here from the SPEED, which EdgeGain has
            // already cancelled out — two independent routes to one number.
            float amplitude = FoamBuffer.DispersingShare(r0, wMax)
                            * FoamBuffer.ShippedFoamPerMetre(DepositRate, r0, EightKnots)
                            / FoamBuffer.EnvelopeIntegral(r0, wMax);

            foreach (float d in new[] { r0 + 0.4f, r0 + 1.2f, r0 + 2.4f })
            {
                double sum = 0;
                for (float e = r0 - edgeWidth; e <= wMax + edgeWidth; e += step)
                    sum += gain * FoamBuffer.EnvelopeShape(d, r0, wMax)
                         * FoamBuffer.EdgeBump(d, e, edgeWidth);
                double want = amplitude * FoamBuffer.EnvelopeShape(d, r0, wMax);
                Assert.AreEqual(want, sum, want * 0.02,
                    $"The edge's single pass at d={d:0.0} did not integrate to the envelope.");
            }
        }

        /// <summary>
        /// 🔴 <b>THE SPEED CANCELS.</b> The envelope's amplitude carries 1/v and the edge's advance
        /// carries v, so the term has no speed dependence to get wrong: the same share of the churn
        /// disperses whether she is idling out of the harbour or running at hull speed. Measured on
        /// the walk itself rather than asserted about the signature.
        /// </summary>
        [Test]
        public void TheSameShareDisperses_AtEverySpeed()
        {
            foreach (float knots in new[] { 3f, 8f, 14f })
            {
                float speed = knots * 0.514444f;
                Lay shipped = Walk(Arm.Shipped, CapeHalfBeam, Envelope, Kelvin, speed, DepositRate, Dt);
                Lay area = Walk(Arm.AreaPreserving, CapeHalfBeam, Envelope, Kelvin, speed, DepositRate, Dt);
                double ratio = area.Total / shipped.Total;
                TestContext.WriteLine($"{knots:0} kn: laid {ratio:0.000}x the shipped stamp; " +
                                      $"drawn rim {area.DrawnHalfWidth:0.00} m " +
                                      $"(shipped {shipped.DrawnHalfWidth:0.00} m), " +
                                      $"edge writes {area.EdgeAmountAtRim * 255f:0.0} codes/frame there");
                Assert.AreEqual(1.0, ratio, 0.04,
                    $"Conservation must not depend on how fast she is going ({knots:0} kn).");
            }
            // And she can lose way entirely without anything dividing by nothing: the edge simply
            // stops advancing, and its width falls back to the world grid rather than to zero.
            Assert.AreEqual(2f * FoamBuffer.CellSize, FoamBuffer.EdgeWidth(0f, Dt), 1e-6f,
                "At rest the edge's width must fall back to the world grid, never to zero.");
        }

        /// <summary>
        /// 🔴 <b>THE EDGE IS LAID ASTERN.</b> The shader finds a texel's place on the track by
        /// projecting onto the trail polyline — and that projection <b>clamps</b>, so without a gate
        /// every texel forward of the transom lands on node 0 and the <c>u = 0</c> ring closes into a
        /// full circle around her, laying a second time on water she has not reached yet.
        ///
        /// <para>Both arms shot here, exactly as the shader computes them: the gate-off arm lays
        /// <b>1.65×</b> what the edge is entitled to, which is +30 % of the whole stamp and straight
        /// through the conservation this term exists to hold. (The TAIL needs no such gate: the
        /// envelope is exactly 0 at its own outer edge, so the ring past the oldest node lays
        /// nothing — which is a property of the family, not a special case.)</para>
        /// </summary>
        [Test]
        public void TheEdgeIsLaidAstern_AnUngatedRingClosesRoundHerAndOverLays()
        {
            const float r0 = CapeHalfBeam;
            float wMax = r0 * Envelope;
            float slope = FoamBuffer.SpreadSlope(Kelvin);
            float reach = (wMax - r0) / slope;
            float edgeWidth = FoamBuffer.EdgeWidth(slope * EightKnots, Dt);
            float gain = FoamBuffer.EdgeGain(DepositRate, r0, wMax, Kelvin, Dt, edgeWidth);

            double Ring(bool asternOnly)
            {
                int nd = Mathf.RoundToInt(26f / FoamBuffer.CellSize);
                double total = 0;
                int lead = Mathf.CeilToInt((r0 + edgeWidth + 1f) / EightKnots / Dt) + 1;
                for (int k = -lead; k < Mathf.CeilToInt(8f / Dt); k++)
                {
                    float astern = EightKnots * (k * Dt);
                    double frame = 0;
                    for (int i = 0; i < nd; i++)
                    {
                        float d = (i + 0.5f) * FoamBuffer.CellSize;
                        float bestD, bestU;
                        if (astern >= 0f)
                        {
                            if (astern > reach) continue;
                            bestD = d;                       // abeam of a straight track
                            bestU = astern / reach;
                        }
                        else
                        {
                            if (asternOnly) continue;        // the gate
                            bestD = Mathf.Sqrt(astern * astern + d * d);   // the projection CLAMPS
                            bestU = 0f;
                        }
                        float edge = Mathf.Lerp(r0, wMax, bestU);
                        frame += gain * FoamBuffer.EnvelopeShape(bestD, r0, wMax)
                               * FoamBuffer.EdgeBump(bestD, edge, edgeWidth);
                    }
                    total += 2.0 * frame * FoamBuffer.CellSize;
                }
                return total;
            }

            double entitled = FoamBuffer.DispersingShare(r0, wMax)
                            * FoamBuffer.ShippedFoamPerMetre(DepositRate, r0, EightKnots);
            double gated = Ring(true);
            double ungated = Ring(false);
            TestContext.WriteLine(
                $"the edge is entitled to {entitled:0.0000} per metre of track; " +
                $"astern-gated {gated:0.0000} ({gated / entitled:0.000}x), " +
                $"gate off {ungated:0.0000} ({ungated / entitled:0.000}x)");

            Assert.AreEqual(1.0, gated / entitled, 0.03,
                "The gated edge must lay exactly the dispersing share of the shipped foam.");
            Assert.Greater(ungated / gated, 1.3,
                "DEAD CONTROL: an ungated ring is supposed to close round her bow and over-lay. If " +
                "it no longer does, this comparison has stopped testing the clamp.");
        }

        /// <summary>
        /// The edge's soft width is floored on the world grid so the ring can never fall between
        /// texels, and on its own advance so it cannot outrun itself at a low frame rate.
        /// </summary>
        [Test]
        public void TheEdgeWidth_IsFlooredOnTheGrid_AndOnItsOwnAdvance()
        {
            Assert.GreaterOrEqual(FoamBuffer.EdgeWidth(0.3f, Dt), 2f * FoamBuffer.CellSize);
            float fast = FoamBuffer.EdgeWidth(4f, 1f / 15f);      // a 15 fps hitch
            Assert.GreaterOrEqual(fast, 4f * 4f * (1f / 15f) - 1e-5f,
                "At a low frame rate the edge advances further than two cells, and the ring has to " +
                "be at least as wide or it steps over water it should have marked.");
        }

        // ==== the age the dispersed foam carries =====================================================

        /// <summary>
        /// 🔴 <b>PRE-CHECK 2 (the charter's).</b> The dispersal lands foam on water the hull never
        /// churned, where the freshness channel was never marked — and an unmarked texel reads
        /// <c>age01 = 1</c>, the far end of #724's colour walk. So the band would be born the deepest
        /// blue right beside a white core. The mark is what keeps the widened band WALKING the blues
        /// instead of jumping to their end.
        /// </summary>
        [Test]
        public void DispersedWater_CarriesTheAgeItWasLaidAt_NotTheEndOfTheColourWalk()
        {
            const float ageHalfLife = 4f;    // IsoFacetHullFeature._foamAgeHalfLifeSeconds
            Assert.AreEqual(1f, FoamBuffer.AgeMark(0f, ageHalfLife), 1e-6f,
                "Foam laid at the transom is fresh churn.");
            Assert.AreEqual(0.5f, FoamBuffer.AgeMark(ageHalfLife, ageHalfLife), 1e-6f,
                "One half-life old reads half fresh, by definition of the channel it is written to.");

            float slope = FoamBuffer.SpreadSlope(Kelvin);
            float reach = (CapeHalfBeam * Envelope - CapeHalfBeam) / slope;
            float tailAge = reach / EightKnots;
            float mark = FoamBuffer.AgeMark(tailAge, ageHalfLife);
            // WakeFoamAgeing.Age01FromFreshness(f, freshFloor: 1) = 1 - f
            float age01 = 1f - mark;
            TestContext.WriteLine(
                $"at the envelope: {reach:0.00} m astern = {tailAge:0.00} s old; " +
                $"mark {mark:0.000} -> age01 {age01:0.000} (unmarked water would read 1.000)");
            Assert.Less(age01, 0.9f,
                "The widest dispersed foam must not be born at the end of the ramp — that is the " +
                "hard white-to-deep-blue seam this mark exists to prevent.");
            Assert.Greater(age01, 0f, "Nor may it be born white: it is genuinely older than the core.");

            float previous = -1f;
            for (float age = 0f; age <= 12f; age += 0.1f)
            {
                float a01 = 1f - FoamBuffer.AgeMark(age, ageHalfLife);
                Assert.GreaterOrEqual(a01, previous, "The walk must be monotone in age.");
                previous = a01;
            }
        }

        // ==== the two halves of the seam =============================================================

        /// <summary>The advect shader is the other half of every function above; a source scrape keeps
        /// the two constants that cannot be inferred from behaviour honest on CPU-only CI.</summary>
        [Test]
        public void TheShader_CarriesTheSameDispersalConstants()
        {
            const string path = "Assets/_Project/Art/Shaders/HiddenHarboursFoamBufferAdvect.shader";
            string code = File.ReadAllText(path);

            Match nodes = Regex.Match(code, @"#define\s+FOAM_DISPERSAL_NODES\s+(\d+)");
            Assert.IsTrue(nodes.Success, "FOAM_DISPERSAL_NODES is missing from the advect shader.");
            Assert.AreEqual(FoamDispersal.Nodes, int.Parse(nodes.Groups[1].Value),
                "The track node count is a compile-time bound on both sides of the seam.");

            Match peak = Regex.Match(code, @"#define\s+FOAM_ENVELOPE_PEAK_X\s+([0-9.]+)");
            Assert.IsTrue(peak.Success, "FOAM_ENVELOPE_PEAK_X is missing from the advect shader.");
            Assert.AreEqual(FoamBuffer.EnvelopePeakX,
                            float.Parse(peak.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                            1e-6f, "The envelope's peak drifted between the twin and the shader.");

            Assert.IsTrue(code.Contains("_HHFoamDispShape"),
                "The dispersal's shape array is what gates the block off; it must be read.");
            Assert.IsTrue(Regex.IsMatch(code, @"if\s*\(\s*disp\.x\s*>\s*0\.0\s*\)"),
                "The dispersal must be gated on its own gain, so a spread of 0 adds bit-exactly " +
                "nothing rather than a small something.");
            Assert.IsTrue(Regex.IsMatch(code, @"float\s+astern\s*=\s*step\s*\(\s*0\.0\s*,\s*dot\("),
                "The ring must be gated ASTERN of the transom. Without it the polyline projection " +
                "clamps for every texel forward of her and the u = 0 ring closes into a circle, " +
                "laying 1.65x what the edge is entitled to — see the guard above.");
        }
    }
}
