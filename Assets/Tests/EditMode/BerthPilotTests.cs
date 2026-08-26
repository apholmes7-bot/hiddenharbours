using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE COME-ALONGSIDE, AS ARITHMETIC</b> — design/npc-pilotage.md §2.2's three additions held
    /// against positioned vectors, with no scene, no rigidbody and no frames.
    ///
    /// <para><b>Why these belong in EditMode.</b> A berth is a POSE, and everything that makes a pose
    /// different from a mark — which side the water is on, where the gate sits, how fast she may close,
    /// how far off the heading she may aim — is pure geometry. Pinning it here means the PlayMode fixtures
    /// are left to answer the one question only a running sim can ("does a real hull actually get there?")
    /// rather than doubling as a slow way to check an arctangent.</para>
    /// </summary>
    public class BerthPilotTests
    {
        // St Peters' own berth, so the numbers under test are the ones the game ships: she lies on the
        // pier's axis pointing in, the planks are north of her, and the water is south.
        private static readonly Vector2 Berth = new Vector2(211.5f, -5.8f);
        private const float BerthHeading = -90f;                     // due west, along the pier
        private static readonly Vector2 Planks = new Vector2(211.5f, -1.9f);
        private const float HullLength = 12.9f;                      // the cape islander

        private static BerthPilot.Berth AtStPeters() =>
            BerthPilot.Berth.FromShorePoint(Berth, BerthHeading, Planks, HullLength);

        private static BerthPilot.Settings Tuning => BerthPilot.Settings.Default;

        // =============================================================================================
        // 1. the frame — a berth has a side, and it is DERIVED
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Which side the water is on is read off the PLANKS, never authored.</b> The region already
        /// ratifies a step-ashore point, and a step-ashore point is on the wharf by construction — so the
        /// open water is whichever side of the berth line it is NOT on. Asserted from both sides, because
        /// a derivation that only works for the sign St Peters happens to have is a constant in disguise.
        /// </summary>
        [Test]
        public void TheSeawardSideIsReadOffThePlanks_NotAuthored()
        {
            BerthPilot.Berth berth = AtStPeters();
            Assert.Less(berth.Seaward.y, -0.99f,
                $"the planks are north of the berth at St Peters, so the water is SOUTH — the derivation " +
                $"answered {berth.Seaward}");

            // The mirror: same berth and heading, planks moved to the other side. Nothing else changes.
            BerthPilot.Berth mirrored = BerthPilot.Berth.FromShorePoint(
                Berth, BerthHeading, new Vector2(Planks.x, Berth.y - 3.9f), HullLength);
            Assert.Greater(mirrored.Seaward.y, 0.99f,
                "put the wharf on the other side and she must present from the other side. If this fails " +
                "the 'derivation' is a hard-coded sign that happens to suit St Peters.");
        }

        /// <summary>The berth's own axes agree with the one compass conversion the whole codebase
        /// uses — otherwise every distance measured along them is measured in a different world from
        /// the one <see cref="ArrivalPilot"/> steers in.</summary>
        [Test]
        public void TheBerthAxesAgreeWithTheOneCompassConversion()
        {
            Vector2 forward = BerthPilot.Forward(BerthHeading);
            Assert.Less(Mathf.Abs(ArrivalPilot.Wrap180(ArrivalPilot.CompassOf(forward) - BerthHeading)),
                        0.01f, "Forward and CompassOf must be exact inverses");

            // Starboard is 90° clockwise of the bow: a hull heading west has north on her right hand.
            Vector2 starboard = BerthPilot.Starboard(BerthHeading);
            Assert.Less(Mathf.Abs(ArrivalPilot.Wrap180(
                            ArrivalPilot.CompassOf(starboard) - (BerthHeading + 90f))), 0.01f,
                        "starboard must be 90° clockwise of the heading");
            Assert.Less(Mathf.Abs(Vector2.Dot(forward, starboard)), 1e-5f, "…and perpendicular to it");
        }

        // =============================================================================================
        // 2. ⭐ the approach gate
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The gate is one hull-length astern of the berth and a standoff off her line</b> (§2.2).
        /// Measured in the berth's own axes rather than as a coordinate, so the assertion survives the
        /// pier being re-sited or re-angled — which is the whole reason the berth carries a frame.
        /// </summary>
        [Test]
        public void TheGateIsOneHullLengthAstern_AndAStandoffOffHerLine()
        {
            BerthPilot.Berth berth = AtStPeters();
            BerthPilot.Settings s = Tuning;
            Vector2 gate = BerthPilot.Gate(berth, s);

            // From the gate, the berth is still AHEAD along the berth heading — by one hull-length, which
            // is the run she has to close in.
            float berthAhead = BerthPilot.AlongTrackTo(gate, berth.Position, berth.HeadingDegrees);
            Assert.AreEqual(s.GateAsternHullLengths * HullLength, berthAhead, 0.01f,
                $"the gate must lie {s.GateAsternHullLengths:F1} hull-lengths ASTERN of the berth so she " +
                "has her own length to run while she closes; it lies at " + gate);

            Assert.AreEqual(s.GateStandoffMetres, BerthPilot.LateralOffset(gate, berth), 0.01f,
                "…and exactly the standoff OUT from her line, on the water side");

            // And the standoff is measured off the BERTH, which already sits a half-beam and a fender's
            // gap out from the wharf face — so the gate can never be inside the planks.
            Assert.Less(gate.y, Berth.y, "at St Peters the gate is seaward (south) of the berth");
        }

        // =============================================================================================
        // 3. ⭐ the set rate — the second, lateral speed loop
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The set rate caps the closing speed, and eases to nothing at her line.</b> 0.25 m/s is
        /// §2.2's "a fender's worth of bump", and the number that makes a docking read as competent.
        /// </summary>
        [Test]
        public void TheSetRateCapsTheClosing_AndEasesToNothingAtHerLine()
        {
            BerthPilot.Settings s = Tuning;

            Assert.AreEqual(s.SetRateMetresPerSecond, BerthPilot.WantedClosingRate(50f, s), 1e-4f,
                "a long way out she closes at the set rate and no faster — that is what the cap IS");
            Assert.AreEqual(0f, BerthPilot.WantedClosingRate(0f, s), 1e-4f,
                "on her line she is asked to close at nothing");
            Assert.AreEqual(-s.SetRateMetresPerSecond, BerthPilot.WantedClosingRate(-50f, s), 1e-4f,
                "and pressed inboard of her line she is asked to come OFF it, at the same rate");

            // Monotone through the ease, so nothing pumps.
            float previous = 0f;
            for (float error = 0f; error <= 4f; error += 0.05f)
            {
                float wanted = BerthPilot.WantedClosingRate(error, s);
                Assert.GreaterOrEqual(wanted + 1e-5f, previous,
                    $"the ask must never fall as the error grows; at {error:F2} m it gave {wanted:F3}");
                Assert.LessOrEqual(wanted, s.SetRateMetresPerSecond + 1e-5f,
                    $"the set rate is a CAP; at {error:F2} m it asked for {wanted:F3} m/s");
                previous = wanted;
            }
        }

        /// <summary>
        /// ⭐ <b>The last half-metre is the LINES, not the hull</b> — and the ease is where that shows up
        /// in the arithmetic. Inside <see cref="BerthPilot.Settings.LateralEaseMetres"/> the ask decays
        /// proportionally, which normally NEVER arrives (<see cref="ArrivalPilot.Settings"/>'s own warning
        /// about a target proportional to distance). Here that is correct rather than a defect: she is not
        /// what closes the last of it.
        /// </summary>
        [Test]
        public void TheEaseIsProportional_BecauseTheLineClosesTheLastOfIt()
        {
            BerthPilot.Settings s = Tuning;
            float half = s.LateralEaseMetres * 0.5f;

            Assert.AreEqual(s.SetRateMetresPerSecond * 0.5f, BerthPilot.WantedClosingRate(half, s), 1e-4f,
                "halfway through the ease she is asked for half the set rate — a linear ease, not a step");
            Assert.Greater(BerthPilot.WantedClosingRate(0.01f, s), 0f,
                "and it never reaches zero short of the line, which is what makes it an ASYMPTOTE the " +
                "mooring line finishes rather than a curve that arrives");
        }

        // =============================================================================================
        // 4. ⭐ the crab — a geometry, not a gain
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The crab angle IS the track geometry.</b> A hull with no leeway goes where she points, so
        /// aiming <c>atan(closing ÷ alongSpeed)</c> off the berth heading makes exactly that closing rate
        /// good. Written as the arctangent rather than as a tuned constant, the crab shrinks by itself as
        /// she slows and vanishes as the error does — which is what leaves her PARALLEL at the end without
        /// anybody scheduling the straightening-up.
        /// </summary>
        [Test]
        public void TheCrabIsTheTrackGeometry_AndVanishesWithTheError()
        {
            BerthPilot.Settings s = Tuning;

            float crab = BerthPilot.CrabDegrees(0.25f, 1.5f, s);
            Assert.AreEqual(Mathf.Atan2(0.25f, 1.5f) * Mathf.Rad2Deg, crab, 0.01f,
                "the aim is the arctangent of the two rates, not a gain on the error");

            Assert.AreEqual(0f, BerthPilot.CrabDegrees(0f, 1.5f, s), 1e-4f,
                "asked to close at nothing she aims straight down the berth — this is what makes her end " +
                "the come-alongside parallel");

            Assert.AreEqual(-BerthPilot.CrabDegrees(0.25f, 1.5f, s),
                            BerthPilot.CrabDegrees(-0.25f, 1.5f, s), 1e-4f,
                "and it is signed: pressed inboard she aims OUT by the same amount");
        }

        /// <summary>The crab is clamped, so a come-alongside can never turn into a turn across her own
        /// berth — and a boat with no way on is not asked for ninety degrees of helm to make good a
        /// quarter-knot sideways.</summary>
        [Test]
        public void TheCrabIsCapped_AndAStoppedBoatIsNotAskedToTurnAcrossHerBerth()
        {
            BerthPilot.Settings s = Tuning;

            Assert.LessOrEqual(Mathf.Abs(BerthPilot.CrabDegrees(5f, 0.01f, s)),
                               s.MaxCrabDegrees + 1e-4f,
                "an enormous closing ask against no way at all must still be capped");

            float dead = Mathf.Abs(BerthPilot.CrabDegrees(s.SetRateMetresPerSecond, 0f, s));
            Assert.Less(dead, 45f,
                $"with no way on the track speed is floored at {s.MinTrackSpeedMetresPerSecond} m/s, so " +
                $"the aim stays sane; it asked for {dead:F0}°");
        }

        /// <summary>
        /// ⚠ <b>THE INVARIANT THE BUILD FOUND: a boat may not aim herself out of the pose she is trying to
        /// reach.</b> The crab is <c>atan(closing ÷ alongSpeed)</c>, so it GROWS as she slows — the
        /// denominator shrinks — and the last few metres of a come-alongside are exactly where she is
        /// slowest. Left uncapped she arrives lying across her own berth, out of pose, and holds there
        /// for ever. So the effective cap is the SMALLER of the tuned one and the heading tolerance, and
        /// the default is set a few degrees inside it.
        /// </summary>
        [Test]
        public void TheCrabCanNeverExceedThePoseToleranceItMustSatisfy()
        {
            BerthPilot.Settings s = Tuning;
            Assert.Less(s.MaxCrabDegrees, s.HeadingToleranceDegrees,
                $"the shipped crab cap ({s.MaxCrabDegrees:F0}°) must sit INSIDE the pose tolerance " +
                $"({s.HeadingToleranceDegrees:F0}°), or a boat holding her commanded aim is out of pose " +
                "by construction");

            // …and even a mis-tune cannot break it: the cap is enforced at the point of use.
            BerthPilot.Settings reckless = s;
            reckless.MaxCrabDegrees = 80f;
            for (float speed = 0f; speed <= 4f; speed += 0.25f)
                Assert.LessOrEqual(Mathf.Abs(BerthPilot.CrabDegrees(5f, speed, reckless)),
                                   reckless.HeadingToleranceDegrees + 1e-3f,
                    $"at {speed:F2} m/s an 80° crab cap let the aim out of the {reckless.HeadingToleranceDegrees:F0}° " +
                    "pose tolerance");
        }

        // =============================================================================================
        // 5. the helm — the two loops, and the astern
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>She goes ASTERN when she is carrying more way than the berth can absorb</b> — the same
        /// rule the approach has always run, reached through the same
        /// <see cref="ArrivalPilot.ThrottleFor"/>. If the come-alongside had grown a second throttle law
        /// this is the assertion that would have caught it.
        /// </summary>
        [Test]
        public void SheGoesAsternWhenSheIsCarryingTooMuchWayForTheBerth()
        {
            BerthPilot.Berth berth = AtStPeters();
            Vector2 velocity = BerthPilot.Forward(BerthHeading) * 3f;   // three metres a second, at the berth

            ArrivalPilot.Helm helm = BerthPilot.Command(
                berth.Position, BerthHeading, velocity, berth,
                lateralTargetMetres: 0f, wantedSpeed: 0f, Tuning, ArrivalPilot.Settings.Default);

            Assert.Less(helm.Throttle, -0.1f,
                $"asked for a standstill while making 3 m/s she must reverse the propeller; she was given " +
                $"{helm.Throttle:F2}. A hull with a twenty-second time constant does not coast to a stop.");
        }

        /// <summary>
        /// The helm swings TOWARD the wharf when she still has to close, and away when she is pressed
        /// inboard of her line — on both sides of a berth, because a sign that only suits St Peters is a
        /// hard-coded side wearing arithmetic.
        /// </summary>
        [Test]
        public void TheHelmSwingsTowardTheLineSheMustClose_OnEitherSideOfTheWharf()
        {
            var pilot = ArrivalPilot.Settings.Default;

            foreach (float shoreOffset in new[] { 3.9f, -3.9f })
            {
                BerthPilot.Berth berth = BerthPilot.Berth.FromShorePoint(
                    Berth, BerthHeading, new Vector2(Berth.x, Berth.y + shoreOffset), HullLength);

                // Two metres OUTBOARD of her line, square on the berth heading, making a slow ahead.
                Vector2 here = berth.Position + berth.Seaward * 2f;
                Vector2 way = BerthPilot.Forward(BerthHeading) * 1f;

                ArrivalPilot.Helm helm = BerthPilot.Command(here, BerthHeading, way, berth, 0f, 1f,
                                                            Tuning, pilot);

                float toward = BerthPilot.ShoreSide(berth);
                Assert.Greater(helm.Steer * toward, 0f,
                    $"with the wharf {shoreOffset:F1} m to her {(shoreOffset > 0 ? "north" : "south")} and " +
                    $"two metres still to close, the helm must go toward it; it went {helm.Steer:F3} " +
                    $"against a shore side of {toward:F0}");
            }
        }

        // =============================================================================================
        // 6. the pose tolerance — §2.1's Gate row, verbatim
        // =============================================================================================

        /// <summary>The Gate row's tolerance is heading ±15° and lateral ±1 m, and it is a POSE test:
        /// nothing about how far along the berth she is, which is the station's business.</summary>
        [Test]
        public void ThePoseToleranceIsTheGateRow()
        {
            BerthPilot.Berth berth = AtStPeters();
            BerthPilot.Settings s = Tuning;

            Assert.AreEqual(15f, s.HeadingToleranceDegrees, 1e-4f, "§2.1's Gate row: heading ±15°");
            Assert.AreEqual(1f, s.LateralToleranceMetres, 1e-4f, "§2.1's Gate row: lateral ±1 m");

            Assert.IsTrue(BerthPilot.WithinPose(berth.Position, BerthHeading, berth, 0f, s),
                "dead on the berth on her own heading is in the pose");
            Assert.IsTrue(BerthPilot.WithinPose(
                              berth.Position + BerthPilot.Forward(BerthHeading) * 50f,
                              BerthHeading + 14f, berth, 0f, s),
                "fifty metres along the berth line, 14° off, is still IN POSE — the pose says nothing " +
                "about the station, and conflating the two is how a hold becomes an abort");

            Assert.IsFalse(BerthPilot.WithinPose(berth.Position, BerthHeading + 16f, berth, 0f, s),
                "16° off her heading is out of pose");
            Assert.IsFalse(BerthPilot.WithinPose(berth.Position + berth.Seaward * 1.1f,
                                                 BerthHeading, berth, 0f, s),
                "1.1 m off her line is out of pose");
        }

        // =============================================================================================
        // 7. the tuning — the owner's rulings, and the zeros guard
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Q2 as an assertion.</b> The owner did not rule on harbour speed, so the doc's own
        /// recommendation ships: 3 m/s inside the wharf line, the fairway left at the 5 m/s
        /// <see cref="ArrivalPilot.Settings.Default"/> has always carried. This test is what will fail
        /// loudly if a later change quietly makes the two the same again.
        /// </summary>
        [Test]
        public void HarbourSpeedIsSlowerThanTheFairwayCruise_AndCostsTheStPetersPassageAboutSixSeconds()
        {
            BerthPilot.Settings s = Tuning;
            var pilot = ArrivalPilot.Settings.Default;

            Assert.AreEqual(3f, s.HarbourSpeedMetresPerSecond, 1e-4f,
                "the doc's Q2 recommendation: 3 m/s (≈ 6 kn) inside the wharf line");
            Assert.AreEqual(5f, pilot.CruiseSpeedMetresPerSecond, 1e-4f,
                "…and the FAIRWAY is untouched at the shipped 5 m/s");

            // The wharf line is the last authored mark before the berth — at St Peters, the dredged
            // channel's own mouth. So the cost of the ruling is that leg, run at 3 instead of 5.
            Vector2[] route = StPetersArrivalOpening.Route();
            float lastLeg = Vector2.Distance(route[route.Length - 2], route[route.Length - 1]);
            float cost = lastLeg / s.HarbourSpeedMetresPerSecond
                         - lastLeg / pilot.CruiseSpeedMetresPerSecond;

            Assert.That(cost, Is.EqualTo(6f).Within(1.5f),
                $"the last leg is {lastLeg:F1} m, so harbour speed costs the passage {cost:F1} s. The PR " +
                "body quotes ~6 s for the owner's eyeball; if this drifts, the number he was given is " +
                "wrong and one of the two must move.");
        }

        /// <summary>
        /// 🔴 <b>A struct the scene never serialized reads as ZEROS, and zeros here are a docking that
        /// never closes and a gate on top of the berth.</b> The committed St Peters scene predates this
        /// field, so its key is simply absent — the exact case <c>MooringLineSettings</c> already carries
        /// a fallback for. Pinned rather than trusted, because it fails silently and only in a build.
        /// </summary>
        [Test]
        public void AnUnauthoredSettingsStructFallsBackToTheDefault()
        {
            var unauthored = default(BerthPilot.Settings);
            Assert.AreEqual(0f, unauthored.SetRateMetresPerSecond,
                "precondition: an omitted struct really does deserialise to zeros");

            BerthPilot.Settings resolved = unauthored.OrDefault();
            Assert.AreEqual(BerthPilot.Settings.Default.SetRateMetresPerSecond,
                            resolved.SetRateMetresPerSecond, 1e-4f);
            Assert.AreEqual(BerthPilot.Settings.Default.HarbourSpeedMetresPerSecond,
                            resolved.HarbourSpeedMetresPerSecond, 1e-4f);

            // …and an AUTHORED one is left exactly alone, or the owner's tuning would be silently
            // overwritten by the guard that exists to protect it.
            BerthPilot.Settings mine = BerthPilot.Settings.Default;
            mine.SetRateMetresPerSecond = 0.4f;
            Assert.AreEqual(0.4f, mine.OrDefault().SetRateMetresPerSecond, 1e-4f);
        }

        /// <summary>The run-out is exactly the distance <see cref="ArrivalPilot.TargetSpeed"/> needs in
        /// order to still be asking for the berthing speed AT the gate — which is how the come-alongside
        /// reuses the approach curve instead of replacing it.</summary>
        [Test]
        public void TheRunoutMakesTheApproachCurveBottomOutAtTheBerthingSpeed()
        {
            BerthPilot.Settings s = Tuning;
            var pilot = ArrivalPilot.Settings.Default;
            var harbour = pilot;
            harbour.CruiseSpeedMetresPerSecond = s.HarbourSpeedMetresPerSecond;

            float atTheGate = ArrivalPilot.TargetSpeed(BerthPilot.BerthingRunoutMetres(s, pilot), harbour);
            Assert.AreEqual(s.BerthingSpeedMetresPerSecond, atTheGate, 0.01f,
                "at the gate the approach must still be asking for the berthing speed — a boat that " +
                "stopped there would have to gather way again against a twenty-second time constant");
        }
    }
}
