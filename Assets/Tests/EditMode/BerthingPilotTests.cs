using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE PHASE MACHINE, WITHOUT A BOAT</b> — design/npc-pilotage.md §2.1's ladder driven over a
    /// fake helm at poses a test states, so the holds, the advances and the aborts are pinned as LOGIC
    /// rather than inferred from whether a rigidbody happened to get there.
    ///
    /// <para><b>Why a fake helm and not a hull.</b> The machine's contract is exactly four reads and one
    /// write (<see cref="IPilotageHelm"/>), which is the seam §2.3 put there so a kinematic backend could
    /// land in S4 without touching the machine. Testing through it is therefore testing the shipping path,
    /// not a test-only one — and it lets a pose be STATED (she is here, on this heading, making this)
    /// instead of steered toward over thirty seconds of PlayMode.</para>
    ///
    /// <para><b>⛔ And it is where "nothing writes a pose" is provable.</b> The fake helm's position and
    /// heading are fields only the TEST assigns. If any future change taught the machine to place a hull,
    /// <see cref="TheMachineNeverWritesAPose"/> fails — which is a stronger guard than reading the source
    /// for an assignment that is not there.</para>
    /// </summary>
    public class BerthingPilotTests
    {
        /// <summary>A helm that answers what the test says and records what it is told. Positioned by the
        /// test, never by the pilot — see the class note.</summary>
        private sealed class FakeHelm : IPilotageHelm
        {
            public Vector2 At;
            public float Heading;
            public Vector2 Way;
            public int Commands;
            public float Throttle;
            public float Steer;

            public Vector2 Position => At;
            public float HeadingDegrees => Heading;
            public Vector2 Velocity => Way;

            public void SetControl(float throttle, float steer)
            {
                Throttle = throttle;
                Steer = steer;
                Commands++;
            }
        }

        // St Peters' own berth and fairway — the numbers the game ships.
        private static readonly Vector2 Berth = new Vector2(211.5f, -5.8f);
        private const float BerthHeading = -90f;
        private static readonly Vector2 Planks = new Vector2(211.5f, -1.9f);
        private const float HullLength = 12.9f;

        private static readonly Vector2[] Route =
        {
            new Vector2(340f, 40f),      // the landfall, out in the bay
            new Vector2(262f, 26f),      // the turn onto the approach
            new Vector2(255f, 0f),       // ⭐ the wharf line: the last authored mark before the berth
            Berth,                       // …which the pilot replaces with the GATE
        };

        private static BerthPilot.Berth TheBerth() =>
            BerthPilot.Berth.FromShorePoint(Berth, BerthHeading, Planks, HullLength);

        private static BerthingPilot Make(BerthPilot.Settings? tuning = null) =>
            new BerthingPilot(Route, TheBerth(), ArrivalPilot.Settings.Default,
                              tuning ?? BerthPilot.Settings.Default);

        /// <summary>Put her at a pose and take one step. Returns the helm she was given.</summary>
        private static FakeHelm StepAt(BerthingPilot pilot, Vector2 at, float heading, Vector2 way)
        {
            var helm = new FakeHelm { At = at, Heading = heading, Way = way };
            pilot.Step(helm);
            return helm;
        }

        /// <summary>Walk her down the authored marks in order, so the mark cursor advances the way it does
        /// on the water. ⚠ Not a convenience: the cursor only steps forward when she is actually inside a
        /// mark's arrive radius, so a fixture that teleports her straight to the wharf line leaves her
        /// still steering for the landfall — which is a fair model of the machine and a terrible
        /// precondition.</summary>
        private static void RunHerInToTheWharfLine(BerthingPilot pilot)
        {
            for (int i = 0; i < Route.Length - 1; i++)
                StepAt(pilot, Route[i], BerthHeading, Vector2.zero);
        }

        // =============================================================================================
        // 1. ⛔ the whole point: no pose is ever written
        // =============================================================================================

        /// <summary>
        /// ⛔ <b>THE MACHINE COMMANDS A HELM AND NOTHING ELSE.</b> The snap this slice deletes was a
        /// position and a rotation written onto a hull; the guarantee replacing it is that the pilot has
        /// no way to write either. Driven through every phase, because a snap hidden in one branch is
        /// exactly how the last one survived.
        /// </summary>
        [Test]
        public void TheMachineNeverWritesAPose()
        {
            BerthingPilot pilot = Make();
            Vector2 gate = pilot.GatePosition;

            Vector2 forward = BerthPilot.Forward(BerthHeading);
            var poses = new[]
            {
                (Route[0], -100f, new Vector2(-4f, -1f)),                    // passage
                (Route[1], BerthHeading, new Vector2(-3f, 0f)),              // passage, next leg
                (Route[2], BerthHeading, new Vector2(-3f, 0f)),              // approach, at the wharf line
                (gate - forward * 5f, BerthHeading, forward * 1.5f),         // gate, lining up
                (gate, BerthHeading, forward * 1f),                          // gate → alongside
                (Berth + pilot.Berth.Seaward * 0.3f, BerthHeading, Vector2.zero), // alongside, on her line
            };

            foreach ((Vector2 at, float heading, Vector2 way) in poses)
            {
                var helm = new FakeHelm { At = at, Heading = heading, Way = way };
                pilot.Step(helm);

                Assert.AreEqual(at, helm.At,
                    $"the pilot moved the boat in {pilot.Phase} — from {at} to {helm.At}. That is the " +
                    "snap, back again, wearing a phase machine.");
                Assert.AreEqual(heading, helm.Heading, 1e-5f,
                    $"the pilot turned the boat in {pilot.Phase} rather than steering her there");
                Assert.Greater(helm.Commands, 0, $"…and it must actually command a helm in {pilot.Phase}");
            }
        }

        // =============================================================================================
        // 2. the wharf line — harbour speed on the last authored leg
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The wharf line is the last authored mark, not a second number.</b> Out on the long legs
        /// she is in PASSAGE and may run at the fairway cruise; once she is on the last leg she is in
        /// APPROACH and the harbour speed caps her. Re-cut the channel and the limit moves with it,
        /// because it is the route saying where the harbour begins.
        /// </summary>
        [Test]
        public void TheWharfLineIsTheLastAuthoredMark_AndSheSlowsAtIt()
        {
            BerthingPilot pilot = Make();
            Assert.AreEqual(PilotagePhase.Passage, pilot.Phase, "she starts on the long legs");

            // Out on the first leg, with 200 m still to run: nothing may be limiting her but the cruise.
            var helm = StepAt(pilot, Route[0], ArrivalPilot.CompassOf(Route[1] - Route[0]),
                              BerthPilot.Forward(ArrivalPilot.CompassOf(Route[1] - Route[0])) * 5f);
            Assert.AreEqual(PilotagePhase.Passage, pilot.Phase);
            Assert.That(helm.Throttle, Is.GreaterThan(-0.05f),
                "at cruise with the whole fairway to run she is neither being slowed nor reversed");

            // Walk her down the marks. She is still PASSAGING at the turn — there is a whole leg left.
            StepAt(pilot, Route[1], BerthHeading, BerthPilot.Forward(BerthHeading) * 5f);
            Assert.AreEqual(PilotagePhase.Passage, pilot.Phase,
                "the turn is not the wharf line — there is still an authored leg between her and the berth");

            // …and the moment she reaches the LAST authored mark, she is approaching.
            StepAt(pilot, Route[2], BerthHeading, BerthPilot.Forward(BerthHeading) * 5f);
            Assert.AreEqual(PilotagePhase.Approach, pilot.Phase,
                "the last authored mark IS the wharf line — past it she is approaching, not passaging");
        }

        /// <summary>Inside the wharf line, still doing the fairway's 5 m/s, she is asked to come down to
        /// the harbour's 3 — i.e. the throttle comes off. This is the Q2 ruling arriving at a helm.</summary>
        [Test]
        public void InsideTheWharfLine_TheFairwaySpeedIsTakenOff()
        {
            BerthingPilot pilot = Make();
            RunHerInToTheWharfLine(pilot);
            Assert.AreEqual(PilotagePhase.Approach, pilot.Phase);

            // A long way off the gate still (so the √(2ad) term is not what is limiting her), at 5 m/s.
            Vector2 wellOut = pilot.GatePosition - BerthPilot.Forward(BerthHeading) * 60f;
            var helm = StepAt(pilot, wellOut, BerthHeading, BerthPilot.Forward(BerthHeading) * 5f);

            Assert.Less(helm.Throttle, 0f,
                $"inside the wharf line at 5 m/s she must be coming down to harbour speed; the helm read " +
                $"{helm.Throttle:F2}");
        }

        // =============================================================================================
        // 3. the gate — capture, hold, advance
        // =============================================================================================

        /// <summary>She captures the gate by RANGE, and from then on she is lining up on the berth
        /// heading rather than steering at a point.</summary>
        [Test]
        public void SheCapturesTheGate_AndLinesUpOnTheBerthHeading()
        {
            BerthingPilot pilot = Make();
            RunHerInToTheWharfLine(pilot);

            Vector2 justOutside = pilot.GatePosition
                                  - BerthPilot.Forward(BerthHeading)
                                    * (BerthPilot.Settings.Default.GateCaptureMetres + 5f);
            StepAt(pilot, justOutside, BerthHeading, BerthPilot.Forward(BerthHeading) * 2f);
            Assert.AreEqual(PilotagePhase.Approach, pilot.Phase, "still outside the capture range");

            StepAt(pilot, pilot.GatePosition - BerthPilot.Forward(BerthHeading) * 5f,
                   BerthHeading - 25f, BerthPilot.Forward(BerthHeading) * 1.5f);
            Assert.AreEqual(PilotagePhase.Gate, pilot.Phase,
                "inside the capture range she is at the gate and lining up");
        }

        /// <summary>
        /// <b>HOLD is the way OFF, not a pause</b> (§2.1). At the gate station but out of pose, she is
        /// asked for a standstill — a boat that keeps running while she is out of pose runs out of berth.
        /// And she does NOT advance: nothing advances on a timer.
        /// </summary>
        [Test]
        public void OutOfPoseAtTheGateStation_SheHoldsWithTheWayOff()
        {
            BerthingPilot pilot = Make();
            RunHerInToTheWharfLine(pilot);
            // Captured, but five metres SHORT of the station — so she is lining up rather than arriving,
            // and the advance below has to be earned by the pose rather than by having got here.
            StepAt(pilot, pilot.GatePosition - BerthPilot.Forward(BerthHeading) * 5f,
                   BerthHeading, Vector2.zero);
            Assert.AreEqual(PilotagePhase.Gate, pilot.Phase);

            // At the station, square-ish but well off the gate's line — inside the abort bound, so this is
            // a HOLD rather than a re-present.
            BerthPilot.Berth berth = pilot.Berth;
            Vector2 offHerLine = pilot.GatePosition + berth.Seaward * 2f;
            var helm = StepAt(pilot, offHerLine, BerthHeading, BerthPilot.Forward(BerthHeading) * 0.8f);

            Assert.AreEqual(PilotagePhase.Gate, pilot.Phase,
                "out of pose she must stay at the gate — she does not advance because time passed");
            Assert.Less(helm.Throttle, 0f,
                $"a hold takes the way OFF; she was given {helm.Throttle:F2} while still making 0.8 m/s");
        }

        /// <summary>In pose at the station she advances — and the come-alongside then asks for the set
        /// rate against the berth's own line rather than the gate's.</summary>
        [Test]
        public void InPoseAtTheGateStation_SheComesAlongside()
        {
            BerthingPilot pilot = Make();
            RunHerInToTheWharfLine(pilot);
            StepAt(pilot, pilot.GatePosition, BerthHeading, BerthPilot.Forward(BerthHeading) * 1f);

            Assert.AreEqual(PilotagePhase.Alongside, pilot.Phase,
                "at the gate station, square and on her line, the come-alongside begins");
        }

        // =============================================================================================
        // 4. 🔴 the abort — §2.1's fourth column
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>THE ABORT PATH: Gate → Approach when the pose cannot be made.</b> She has run a long way
        /// past the gate station still out of pose, so another turn is the honest answer — and the
        /// alternative, advancing anyway, is what a snap used to cover up.
        /// </summary>
        [Test]
        public void MissingThePoseAtTheGate_SheAbortsBackToApproachAndRePresents()
        {
            BerthingPilot pilot = Make();
            RunHerInToTheWharfLine(pilot);
            StepAt(pilot, pilot.GatePosition, BerthHeading - 40f, Vector2.zero);
            Assert.AreEqual(PilotagePhase.Gate, pilot.Phase, "precondition: she reached the gate");
            Assert.AreEqual(0, pilot.Aborts, "precondition: nothing has gone round yet");

            // Forty degrees off her heading — right out of pose — and a long way past the station.
            Vector2 wellPast = pilot.GatePosition
                               + BerthPilot.Forward(BerthHeading)
                                 * (BerthPilot.Settings.Default.AbortOvershootMetres + 4f);
            StepAt(pilot, wellPast, BerthHeading - 40f, BerthPilot.Forward(BerthHeading) * 1f);

            Assert.AreEqual(PilotagePhase.Approach, pilot.Phase,
                "she ran past the gate out of pose — §2.1's Gate row aborts to Approach and takes " +
                "another turn");
            Assert.AreEqual(1, pilot.Aborts, "…and the re-present is counted, so it can be diagnosed");
        }

        /// <summary>The other abort trigger: wide of her line rather than past her station.</summary>
        [Test]
        public void WideOfTheGatesLine_SheAlsoRePresents()
        {
            BerthingPilot pilot = Make();
            RunHerInToTheWharfLine(pilot);
            StepAt(pilot, pilot.GatePosition, BerthHeading, Vector2.zero);
            Assert.AreEqual(PilotagePhase.Alongside, pilot.Phase,
                "precondition: square on her line at the station, so she came alongside");

            BerthPilot.Berth berth = pilot.Berth;
            Vector2 wide = Berth + berth.Seaward
                                   * (BerthPilot.Settings.Default.AbortLateralMetres + 2f);
            StepAt(pilot, wide, BerthHeading, Vector2.zero);

            Assert.AreEqual(PilotagePhase.Gate, pilot.Phase,
                "blown wide of the berth on the come-alongside, she backs off and re-presents at the gate");
        }

        /// <summary>
        /// ⚠ <b>And the going-round is BOUNDED.</b> Rule 10 insurance rather than seamanship: an approach
        /// that can abort without limit in a basin it cannot get square in never ends, and a passenger who
        /// can never be put ashore is a broken build. Past the limit she holds instead.
        /// </summary>
        [Test]
        public void TheRePresentingIsBounded_SoAnArrivalCanAlwaysEnd()
        {
            BerthPilot.Settings tuning = BerthPilot.Settings.Default;
            tuning.MaxAborts = 1;
            BerthingPilot pilot = Make(tuning);

            RunHerInToTheWharfLine(pilot);
            for (int i = 0; i < 12; i++)
            {
                // Out, round, and back astern of the gate — a whole honest re-presentation each time,
                // failing the pose in exactly the same way, forever.
                StepAt(pilot, Route[2], BerthHeading, Vector2.zero);
                StepAt(pilot, pilot.GatePosition, BerthHeading - 40f, Vector2.zero);
                Vector2 hopeless = pilot.GatePosition
                                   + BerthPilot.Forward(BerthHeading) * (tuning.AbortOvershootMetres + 4f);
                StepAt(pilot, hopeless, BerthHeading - 40f, BerthPilot.Forward(BerthHeading) * 1f);
            }

            Assert.LessOrEqual(pilot.Aborts, tuning.MaxAborts,
                $"she re-presented {pilot.Aborts} times against a limit of {tuning.MaxAborts} — an " +
                "unbounded abort loop is a passenger who can never be put ashore");
        }

        // =============================================================================================
        // 5. the lines, and the dead helm
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>She is ready for her lines only when she is alongside, STOPPED, and IN THE POSE.</b> The
        /// old test was velocity alone, which is true of a boat stopped anywhere — including one stopped
        /// ten metres off on the wrong heading, which the snap then made indistinguishable from an
        /// arrival.
        /// </summary>
        [Test]
        public void ReadyForLinesAsksTheWholeQuestion_NotJustTheSpeed()
        {
            BerthingPilot pilot = Make();
            RunHerInToTheWharfLine(pilot);
            StepAt(pilot, pilot.GatePosition, BerthHeading, Vector2.zero);
            Assert.AreEqual(PilotagePhase.Alongside, pilot.Phase, "precondition");

            Assert.IsTrue(pilot.ReadyForLines(Berth, BerthHeading, Vector2.zero),
                "stopped, alongside, in the pose — the line goes over");
            Assert.IsFalse(pilot.ReadyForLines(Berth, BerthHeading, new Vector2(0.5f, 0f)),
                "still carrying way is not arrived");
            Assert.IsFalse(pilot.ReadyForLines(Berth, BerthHeading + 40f, Vector2.zero),
                "stopped across her own berth is not arrived either — and this is the case the snap hid");
            Assert.IsFalse(pilot.ReadyForLines(Berth + pilot.Berth.Seaward * 4f, BerthHeading,
                                               Vector2.zero),
                "…nor is stopped four metres off it");
        }

        /// <summary>
        /// <b>§2.1's Alongside HOLD: closing faster than the set rate takes the way off.</b> The crab is a
        /// function of the lateral ERROR, so it has already stopped asking for speed she has — but a hull
        /// carrying sideways way does not stop because she was re-aimed. Something shoved her (a sea, a
        /// wake, the player), and the answer is astern.
        /// </summary>
        [Test]
        public void ClosingFasterThanTheSetRate_TakesTheWayOff()
        {
            BerthingPilot pilot = Make();
            RunHerInToTheWharfLine(pilot);
            StepAt(pilot, pilot.GatePosition, BerthHeading, Vector2.zero);
            Assert.AreEqual(PilotagePhase.Alongside, pilot.Phase, "precondition");

            BerthPilot.Settings s = BerthPilot.Settings.Default;
            Vector2 here = Berth + pilot.Berth.Seaward * 0.8f
                           - BerthPilot.Forward(BerthHeading) * 4f;   // still short of the berth
            Vector2 shoved = BerthPilot.Forward(BerthHeading) * 1f
                             - pilot.Berth.Seaward * (s.SetRateMetresPerSecond * 4f);

            var helm = StepAt(pilot, here, BerthHeading, shoved);
            Assert.Less(helm.Throttle, 0f,
                $"she is being driven onto her own wharf at " +
                $"{BerthPilot.ClosingRate(shoved, pilot.Berth):F2} m/s against a set rate of " +
                $"{s.SetRateMetresPerSecond:F2}, and the helm read {helm.Throttle:F2}. A hold is the way " +
                "OFF, not a re-aim.");

            // …and the same pose, closing at the set rate, is NOT a hold — or the manoeuvre could never
            // finish, because closing at the set rate is what it is FOR.
            Vector2 proper = BerthPilot.Forward(BerthHeading) * 0.5f
                             - pilot.Berth.Seaward * s.SetRateMetresPerSecond;
            helm = StepAt(pilot, here, BerthHeading, proper);
            Assert.Greater(helm.Throttle, 0f,
                "closing at exactly the set rate must read as a come-alongside going to plan, not as a " +
                "hold — a threshold AT the rate the loop aims for would chatter for the whole manoeuvre");
        }

        /// <summary>Moored, the helm is dead and stays dead: a moored boat's helm is nobody's, and a
        /// stepped machine must not quietly start steering her again.</summary>
        [Test]
        public void OnceMooredTheHelmIsDeadAndStaysDead()
        {
            BerthingPilot pilot = Make();
            var helm = new FakeHelm { At = Berth, Heading = BerthHeading };
            pilot.Moor(helm);

            Assert.AreEqual(PilotagePhase.Moored, pilot.Phase);
            Assert.AreEqual(0f, helm.Throttle, 1e-5f);
            Assert.AreEqual(0f, helm.Steer, 1e-5f);

            int commands = helm.Commands;
            helm.Way = new Vector2(3f, 0f);        // a wave gives her a shove
            pilot.Step(helm);
            Assert.AreEqual(commands, helm.Commands,
                "a moored boat's helm was worked — the sea moves her from here, and her engine does not");
        }

        /// <summary>A null helm is a no-op rather than a throw: an opening that dies halfway through is
        /// worse than one that does not run (rule 10).</summary>
        [Test]
        public void ANullHelmIsANoOp()
        {
            BerthingPilot pilot = Make();
            Assert.DoesNotThrow(() => pilot.Step(null));
            Assert.DoesNotThrow(() => pilot.Moor(null));
        }
    }
}
