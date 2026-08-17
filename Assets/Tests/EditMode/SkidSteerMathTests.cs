using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>SKID STEER IS NOT ACKERMANN</b> (ADR 0035's amphibian follow-up) — the arithmetic that
    /// turns a machine with no steering axle, pinned against the Otter's own published numbers so it
    /// is a decision on the record rather than something felt for once at a stick.
    ///
    /// <para><b>Why the truck's math could not simply be reused</b>, and why that is worth a fixture
    /// rather than a comment. <c>VehicleSteeringMath</c> is a kinematic bicycle: it is CORRECT, it is
    /// pinned, and every one of its headline properties is <b>false</b> for a differential —</para>
    /// <list type="bullet">
    ///   <item>a stopped truck cannot turn; a stopped skid machine spins on the spot;</item>
    ///   <item>a reversing truck steers the other way for the same lock; a reversing skid machine does
    ///   not, because the sides split the same way whichever way she is going;</item>
    ///   <item>a truck's turn is free of her top speed; a skid machine's turn SPENDS the transmission
    ///   she was using to go forward.</item>
    /// </list>
    ///
    /// <para>Every value below is either the Otter's own published figure or derived from her
    /// geometry, and the one number the whole model is solved against — her rig's
    /// <c>G.revPerYaw45</c> — is asserted here so nobody can later "fix" it toward something they
    /// remembered. Pure floats, no rendering, no scene: headless by construction.</para>
    /// </summary>
    public class SkidSteerMathTests
    {
        // ---- HER numbers, from the 2026-08-17 rig and sidecar (docs/art/rigs/otter-iso-kit) -------

        /// <summary>WHEELS.track_width_m / G.trackWidth — the differential's lever arm.</summary>
        const float TrackWidth = 1.26f;

        /// <summary>WHEELS.radius_m / G.wheelR.</summary>
        const float WheelRadius = 0.32f;

        /// <summary>G.revPerYaw45, and YAW.skid_arithmetic.rev_of_split_per_45deg — 4 decimals.</summary>
        const float PublishedRevPerYaw45 = 0.2461f;

        /// <summary>WHEELS.roll.distance_per_rev_m — 4 significant figures of 2π·0.32.</summary>
        const float PublishedDistancePerRev = 2.011f;

        // The Dually's, for the side-by-side contrasts (her 2026-08-17 sidecar).
        const float DuallyWheelbase = 4.30f;
        const float DuallyFrontTrack = 1.80f;
        const float DuallyMaxInnerLock = 30f;

        // =============================================================================================
        //  1. THE RIG'S OWN ARITHMETIC
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The number the model is solved against.</b> A 45° skid turn costs the split her rig
        /// publishes as <c>revPerYaw45</c> — DERIVED from her track and her wheels, not transcribed,
        /// exactly as the Dually's full-lock radius is derived from her published locks rather than
        /// from her sidecar's rounded copy of it.
        ///
        /// <para>The π cancels: <c>yaw_rad·track / (2·2π·r)</c> at <c>yaw = π/4</c> collapses to
        /// <c>track / (16·r)</c> = <c>1.26 / 5.12</c> = <b>0.24609375</b>, against her published
        /// 0.2461. Both are asserted — the closed form so a refactor cannot quietly change the shape,
        /// and the published figure so an art re-derivation that moved her track or her wheels is
        /// caught here rather than at a shoreline.</para>
        /// </summary>
        [Test]
        public void A45DegreeTurnCostsHerPublishedSplit()
        {
            float split = SkidSteerMath.SplitRevolutionsForYawDegrees(45f, TrackWidth, WheelRadius);

            Assert.That(split, Is.EqualTo(TrackWidth / (16f * WheelRadius)).Within(1e-6f),
                "the closed form moved: split for 45° is identically track/(16·r), the π having " +
                "cancelled out of yaw_rad·track/(2·2πr).");

            Assert.That(split, Is.EqualTo(PublishedRevPerYaw45).Within(5e-5f),
                "the derived split no longer matches the rig's own G.revPerYaw45 to the four " +
                "decimals she publishes it at. Either her track or her wheel radius moved in the " +
                "art — do not adjust this number, re-read hers.");
        }

        /// <summary>The rig's published FORMULA, read the other way:
        /// <c>yaw_rad = 2·split·distance_per_rev/track</c> at her published split really does return a
        /// 45° turn.</summary>
        [Test]
        public void HerPublishedSplitBuysHerPublishedFortyFiveDegrees()
        {
            Assert.That(
                SkidSteerMath.YawDegreesForSplitRevolutions(PublishedRevPerYaw45, TrackWidth,
                                                            WheelRadius),
                Is.EqualTo(45f).Within(0.01f));
        }

        /// <summary>Split → yaw → split is the identity, so the drawn tracks and the physics yaw can
        /// be solved from one number without either drifting.</summary>
        [Test]
        public void TheSplitAndTheYawAreExactInverses()
        {
            foreach (float yaw in new[] { -180f, -45f, -7.5f, 0f, 12.25f, 45f, 360f })
            {
                float split = SkidSteerMath.SplitRevolutionsForYawDegrees(yaw, TrackWidth, WheelRadius);
                Assert.That(SkidSteerMath.YawDegreesForSplitRevolutions(split, TrackWidth, WheelRadius),
                            Is.EqualTo(yaw).Within(1e-3f), $"round trip failed at {yaw}°");
            }
        }

        /// <summary>
        /// One revolution carries a track her published <c>distance_per_rev_m</c> — <c>2π·r</c>, NOT
        /// <c>r</c>. The same 2π ≈ 6.3× trap <c>VehicleSteeringMath.RollRevolutions</c> documents, and
        /// it is worth re-pinning on the machine whose wheels the player watches turning in the water.
        /// </summary>
        [Test]
        public void OneRevolutionCarriesHerHerPublishedDistance()
        {
            Assert.That(SkidSteerMath.DistancePerRevolutionMeters(WheelRadius),
                        Is.EqualTo(PublishedDistancePerRev).Within(5e-4f));
            Assert.That(SkidSteerMath.DistancePerRevolutionMeters(WheelRadius),
                        Is.GreaterThan(6f * WheelRadius),
                        "someone has read the roll axis as radians. It is revolutions, so one turn " +
                        "covers 2π·r and not r — the 6.3× that spins a wheel like a cartoon.");
        }

        /// <summary>
        /// ⚠️ <b>Her sidecar's <c>deg_per_rev_of_split: 182.89</c> is a hand-transcription rounding</b>
        /// — the same geometry gives 182.857 (<c>45 / 0.24609375</c>). Harmless at 0.02%, and
        /// harmless precisely because nothing reads it: this asserts the DERIVED value and bounds the
        /// disagreement, so a real drift in her published numbers still fails here while the known
        /// rounding does not.
        /// </summary>
        [Test]
        public void TheDerivedDegreesPerRevolutionOfSplitAgreesWithHerSidecarsRounding()
        {
            const float published = 182.89f;
            float derived = SkidSteerMath.YawDegreesForSplitRevolutions(1f, TrackWidth, WheelRadius);

            Assert.That(derived, Is.EqualTo(45f / (TrackWidth / (16f * WheelRadius))).Within(1e-2f),
                        "the derivation and the 45° case disagree with each other.");
            Assert.That(derived, Is.EqualTo(published).Within(0.05f),
                        "her published deg_per_rev_of_split has moved by more than its own rounding. " +
                        "That is an art change, not an arithmetic one.");
        }

        // =============================================================================================
        //  2. WHAT A DIFFERENTIAL DOES THAT A BICYCLE MODEL CANNOT
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>THE HEADLINE: she turns at a standstill and the truck does not.</b> Both claims in one
        /// test, because the point is the CONTRAST — this is the property that makes forcing her
        /// through <c>VehicleSteeringMath</c> not a simplification but a machine that cannot be
        /// manoeuvred in a yard.
        /// </summary>
        [Test]
        public void SheSpinsOnTheSpotWhereAnAckermannTruckCannotTurnAtAll()
        {
            Assert.That(
                VehicleSteeringMath.YawRateDegreesPerSecond(
                    0f, 1f, DuallyMaxInnerLock, DuallyWheelbase, DuallyFrontTrack),
                Is.EqualTo(0f).Within(1e-6f),
                "the truck's model must still refuse to turn a stopped machine — if this changed, " +
                "the amphibian's work has leaked into the road vehicle's.");

            float spin = SkidSteerMath.YawRateDemandDegreesPerSecond(
                steer: 1f, spinRateDegreesPerSecond: 75f, maxTrackSpeedMetersPerSecond: 8f,
                trackWidthMeters: TrackWidth);
            Assert.That(spin, Is.EqualTo(75f).Within(1e-4f),
                        "full stick IS the spin rate for a differential — it is not a ceiling she " +
                        "works up to with speed.");
        }

        /// <summary>A spin on the spot is the two sides running exactly opposite — the rig's own
        /// note, <i>"rollL = +t, rollR = −t is a spin on the spot"</i>.</summary>
        [Test]
        public void ASpinOnTheSpotCounterRotatesTheTwoSides()
        {
            SkidSteerMath.TrackSpeeds(0f, 60f, TrackWidth, out float left, out float right);

            Assert.That(left, Is.EqualTo(-right).Within(1e-6f), "the sides must mirror at v = 0");
            Assert.That(right, Is.GreaterThan(0f),
                        "a LEFT turn (+yaw, the fleet's counter-clockwise convention) runs the CURB " +
                        "side forward. Flipping this drives her the wrong way round every corner.");
            Assert.That(SkidSteerMath.BodySpeedMetersPerSecond(left, right), Is.EqualTo(0f).Within(1e-6f),
                        "a spin must not walk her forwards.");
        }

        /// <summary>Track speeds ⇄ (body speed, yaw rate) is an exact round trip — the coupling the
        /// rig warns nobody but us can supply.</summary>
        [Test]
        public void TrackSpeedsAndTheYawRateAreExactInverses()
        {
            foreach (float v in new[] { -3f, 0f, 1.6f, 8f })
                foreach (float w in new[] { -90f, -12f, 0f, 33f, 75f })
                {
                    SkidSteerMath.TrackSpeeds(v, w, TrackWidth, out float left, out float right);
                    Assert.That(SkidSteerMath.BodySpeedMetersPerSecond(left, right),
                                Is.EqualTo(v).Within(1e-4f), $"body speed lost at ({v}, {w})");
                    Assert.That(SkidSteerMath.YawRateDegreesPerSecond(left, right, TrackWidth),
                                Is.EqualTo(w).Within(1e-3f), $"yaw rate lost at ({v}, {w})");
                }
        }

        /// <summary>
        /// ⭐ <b>The stick means the same thing astern.</b> A truck backs the other way for the same
        /// lock because her yaw carries the sign of her speed; a differential's split does not care
        /// which way she is travelling, so left stick yaws her left going forward AND going astern —
        /// which is what backing a skid machine actually does.
        /// </summary>
        [Test]
        public void TheStickMeansTheSameThingAsternUnlikeATruck()
        {
            SkidSteerMath.TrackSpeeds(+2f, 40f, TrackWidth, out float aheadL, out float aheadR);
            SkidSteerMath.TrackSpeeds(-2f, 40f, TrackWidth, out float asternL, out float asternR);

            Assert.That(aheadR - aheadL, Is.EqualTo(asternR - asternL).Within(1e-6f),
                        "the SPLIT must be identical ahead and astern for the same stick.");
            Assert.That(SkidSteerMath.YawRateDegreesPerSecond(asternL, asternR, TrackWidth),
                        Is.EqualTo(40f).Within(1e-3f), "she must not mirror her own stick in reverse.");

            // And the truck genuinely does mirror, so this is a real difference and not a tautology.
            float duallyAhead = VehicleSteeringMath.YawRateDegreesPerSecond(
                +2f, 1f, DuallyMaxInnerLock, DuallyWheelbase, DuallyFrontTrack);
            float duallyAstern = VehicleSteeringMath.YawRateDegreesPerSecond(
                -2f, 1f, DuallyMaxInnerLock, DuallyWheelbase, DuallyFrontTrack);
            Assert.That(duallyAstern, Is.EqualTo(-duallyAhead).Within(1e-4f));
        }

        // =============================================================================================
        //  3. THE TRADE — turning spends the transmission
        // =============================================================================================

        /// <summary>
        /// ⭐ Turning costs forward speed by EXACTLY the differential it spends: the outer track is
        /// the one that saturates, so the body's ceiling is <c>v_track − |ω|·track/2</c>.
        ///
        /// <para>At the Otter's tuning that is a 0.82 m/s give-back at full stick out of 8 — about a
        /// tenth, which is a machine that leans into a turn rather than one that stalls in it. Zeroing
        /// her instead would stutter; letting the outer track quietly exceed its own ceiling would be
        /// cheating.</para>
        /// </summary>
        [Test]
        public void TurningSpendsForwardSpeedByExactlyTheDifferential()
        {
            const float trackCeiling = 8f;
            const float spin = 75f;

            float cap = SkidSteerMath.MaxBodySpeedMetersPerSecond(spin, TrackWidth, trackCeiling);
            float expected = trackCeiling - 0.5f * TrackWidth * spin * Mathf.Deg2Rad;
            Assert.That(cap, Is.EqualTo(expected).Within(1e-5f));
            Assert.That(cap, Is.LessThan(trackCeiling), "a turn must cost something");

            // …and at the cap the OUTER track sits exactly on the ceiling, which is the whole claim.
            SkidSteerMath.TrackSpeeds(cap, spin, TrackWidth, out float left, out float right);
            Assert.That(Mathf.Max(Mathf.Abs(left), Mathf.Abs(right)),
                        Is.EqualTo(trackCeiling).Within(1e-4f),
                        "the ceiling is the point of the cap: the faster side must land ON it, " +
                        "neither short of it nor through it.");

            // Straight ahead there is nothing to spend, so the ceiling is untouched.
            Assert.That(SkidSteerMath.MaxBodySpeedMetersPerSecond(0f, TrackWidth, trackCeiling),
                        Is.EqualTo(trackCeiling).Within(1e-6f));
        }

        /// <summary>A tuned spin rate can never ask for a differential the transmission does not
        /// have: the demand is bounded by both sides at full opposite speed. Without the bound, a
        /// generous tune would drive the body cap to zero and pin her to the spot.</summary>
        [Test]
        public void TheYawDemandCannotExceedWhatTheTracksCanDifferentiate()
        {
            const float trackCeiling = 1.6f;   // her AFLOAT ceiling — the tight case
            float ceiling = SkidSteerMath.MaxYawRateDegreesPerSecond(trackCeiling, TrackWidth);

            Assert.That(ceiling,
                        Is.EqualTo(2f * trackCeiling / TrackWidth * Mathf.Rad2Deg).Within(1e-4f));

            float absurd = SkidSteerMath.YawRateDemandDegreesPerSecond(
                steer: 1f, spinRateDegreesPerSecond: 10000f,
                maxTrackSpeedMetersPerSecond: trackCeiling, trackWidthMeters: TrackWidth);
            Assert.That(absurd, Is.EqualTo(ceiling).Within(1e-3f),
                        "an over-generous tune must be held at the transmission's own limit.");
            Assert.That(SkidSteerMath.MaxBodySpeedMetersPerSecond(absurd, TrackWidth, trackCeiling),
                        Is.EqualTo(0f).Within(1e-4f),
                        "and at that limit she is spinning on the spot, which is the honest answer " +
                        "rather than a negative speed cap.");
        }

        /// <summary>The stick is clamped, so an input past the stops is not extra authority.</summary>
        [Test]
        public void TheStickIsClampedAtItsStops()
        {
            float atLock = SkidSteerMath.YawRateDemandDegreesPerSecond(1f, 75f, 8f, TrackWidth);
            float pastLock = SkidSteerMath.YawRateDemandDegreesPerSecond(4f, 75f, 8f, TrackWidth);
            Assert.That(pastLock, Is.EqualTo(atLock).Within(1e-6f));
            Assert.That(SkidSteerMath.YawRateDemandDegreesPerSecond(-4f, 75f, 8f, TrackWidth),
                        Is.EqualTo(-atLock).Within(1e-6f));
        }

        // =============================================================================================
        //  4. THE DEGENERATE DEFS
        // =============================================================================================

        /// <summary>A def with no track or no wheels is INERT, not a divide by zero — the same
        /// discipline every guard in <c>VehicleSteeringMath</c> keeps, because an unbaked or
        /// half-wired def reaching the drive model must idle rather than throw.</summary>
        [Test]
        public void AnUnbakedDefIsInertRatherThanADivideByZero()
        {
            Assert.That(SkidSteerMath.DistancePerRevolutionMeters(0f), Is.EqualTo(0f));
            Assert.That(SkidSteerMath.SplitRevolutionsForYawDegrees(45f, TrackWidth, 0f), Is.EqualTo(0f));
            Assert.That(SkidSteerMath.SplitRevolutionsForYawDegrees(45f, 0f, WheelRadius), Is.EqualTo(0f));
            Assert.That(SkidSteerMath.YawDegreesForSplitRevolutions(0.25f, 0f, WheelRadius),
                        Is.EqualTo(0f));
            Assert.That(SkidSteerMath.YawRateDegreesPerSecond(1f, -1f, 0f), Is.EqualTo(0f));
            Assert.That(SkidSteerMath.MaxYawRateDegreesPerSecond(8f, 0f), Is.EqualTo(0f));

            SkidSteerMath.TrackSpeeds(3f, 60f, 0f, out float left, out float right);
            Assert.That(left, Is.EqualTo(3f).Within(1e-6f), "no track means no differential");
            Assert.That(right, Is.EqualTo(3f).Within(1e-6f));
        }

        /// <summary>Per-side roll is the SAME revolutions arithmetic the whole fleet's wheels use —
        /// reused rather than re-derived, so the two vehicles can never disagree about what a
        /// revolution is.</summary>
        [Test]
        public void PerSideRollIsTheFleetsOwnRevolutionsArithmetic()
        {
            SkidSteerMath.TrackRollRevolutions(PublishedDistancePerRev, -PublishedDistancePerRev,
                                               WheelRadius, out float left, out float right);

            Assert.That(left, Is.EqualTo(1f).Within(1e-3f),
                        "one published distance_per_rev must be exactly one revolution");
            Assert.That(right, Is.EqualTo(-1f).Within(1e-3f),
                        "and the other side, driven back, must unwind by exactly one");
            Assert.That(left,
                        Is.EqualTo(VehicleSteeringMath.RollRevolutions(PublishedDistancePerRev,
                                                                       WheelRadius)).Within(1e-6f),
                        "the per-side helper must BE the fleet's helper, not a second copy of it.");
        }
    }
}
