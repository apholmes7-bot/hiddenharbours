using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The bird flies the table, and the table is the drop.</b> <see cref="SeagullStateMachine"/> is
    /// the whole of a gull's mind: it may only take edges the art director declared, it chains the
    /// oneshots on their own strip lengths, and it puts the bird down on a spot without ever reading a
    /// clock of its own.
    ///
    /// <para>🔴 <b>What these would have caught.</b> Two things the charter turns on. The first is the
    /// descent: <c>swoop</c>'s band floor is 1.0 m and <c>land</c> is entered at 0.6 m, so a machine
    /// that treats the band floor as a deck stops the bird 0.4 m — thirteen pixels — above its own
    /// flare and drops it the rest of the way in one frame. A commanded descent has to be let below
    /// its cruise floor, and <see cref="ACommandedDescentIsLetBelowItsCruiseFloor"/> is what says so.
    /// The second is the pivot: the flare's travel is spent on the run-in, so once the bird is
    /// <c>land</c>ing the pivot must not move at all — the shadow is drawn there, and a pivot that
    /// creeps is a shadow the bird never quite meets.
    /// <see cref="TheFlareComesDownWithoutMovingThePivot"/> holds that shut in arithmetic, and the
    /// PlayMode fixture holds the same thing shut in a scene.</para>
    /// </summary>
    public class SeagullStateMachineTests
    {
        const double Eps = 1e-9;

        static SeagullBehaviour Behaviour()
        {
            var def = Resources.Load<SeagullVisualDef>(SeagullVisualDef.ResourcesPath);
            Assert.IsNotNull(def, "the committed SeagullVisualDef did not load");
            var b = def.Behaviour;
            Assert.IsNotNull(b, "the def produced no behaviour table");
            return b;
        }

        /// <summary>A bird in the air, off the wheel, with no landing intent. The explicit
        /// <c>IntentState = -1</c> matters: a defaulted struct reads intent 0, which is <c>fly</c>.</summary>
        static SeagullSimBird Airborne(SeagullBehaviour b, string state,
                                       double x = 0.0, double y = 0.0,
                                       double altitude = 10.0, double heading = 0.0)
        {
            var bird = new SeagullSimBird
            {
                X = x, Y = y, AltitudeMetres = altitude, Heading = heading,
                IntentState = -1, FlockBound = false, RejoinMilliseconds = 0.0,
            };
            SeagullStateMachine.Enter(ref bird, b.IndexOf(state), b);
            bird.AltitudeMetres = altitude;
            return bird;
        }

        static void Step(ref SeagullSimBird bird, SeagullBehaviour b, double milliseconds)
        {
            var nowhere = new SeagullFlockMath.SeagullFlockBird(
                0, 0, 0, 0, 0, SeagullFlockMath.SeagullFlockAnim.Fly, 0, 1.0);
            SeagullStateMachine.Step(ref bird, milliseconds, b, in nowhere, 0.0, 0.0);
        }

        static double Wrap(double radians)
        {
            double t = Math.PI * 2.0;
            return ((radians % t) + t) % t;
        }

        // =========================================================================================
        //  1. the checked door
        // =========================================================================================

        /// <summary>The charter: "state transitions follow the table". A declared edge is taken, an
        /// undeclared one is refused — and a refusal changes NOTHING, so a caller that guessed wrong
        /// finds out by the return value rather than by watching a gull teleport into a pose.</summary>
        [Test]
        public void TryTransitionTakesADeclaredEdgeAndLeavesTheBirdAloneOnTheRest()
        {
            var b = Behaviour();
            var bird = Airborne(b, SeagullStates.Fly);

            Assert.IsTrue(SeagullStateMachine.TryTransition(ref bird, b.IndexOf(SeagullStates.Glide), b),
                          "fly -> glide is in the drop");
            Assert.AreEqual(b.IndexOf(SeagullStates.Glide), bird.State);

            var before = bird;
            Assert.IsFalse(SeagullStateMachine.TryTransition(ref bird, b.IndexOf(SeagullStates.Stand), b),
                           "glide -> stand is not in the drop — a gull cannot blink onto the shingle");
            Assert.AreEqual(before.State, bird.State);
            Assert.AreEqual(before.StateMilliseconds, bird.StateMilliseconds);
            Assert.AreEqual(before.Frame, bird.Frame);
            Assert.AreEqual(before.AltitudeMetres, bird.AltitudeMetres);

            Assert.IsFalse(SeagullStateMachine.TryTransition(ref bird, 999, b), "out of range");
            Assert.IsFalse(SeagullStateMachine.TryTransition(ref bird, -1, b), "out of range");
        }

        /// <summary>A oneshot hands on at the end of its own strip, not on a number someone typed: the
        /// dive runs 4 frames x 85 ms and becomes a splash, the splash runs 5 x 110 and becomes a
        /// float. Retune the drop and the hand-off moves with it.</summary>
        [Test]
        public void AOneshotChainsAtTheEndOfItsOwnStrip()
        {
            var b = Behaviour();
            var bird = Airborne(b, SeagullStates.Swoop, altitude: 8.0);
            Assert.IsTrue(SeagullStateMachine.TryTransition(ref bird, b.IndexOf(SeagullStates.Dive), b));

            double dive = b.Row(b.IndexOf(SeagullStates.Dive)).DurationMilliseconds;
            Step(ref bird, b, dive - 1.0);
            Assert.AreEqual(SeagullStates.Dive, b.IdOf(bird.State), "chained one frame early");

            Step(ref bird, b, 2.0);
            Assert.AreEqual(SeagullStates.Splash, b.IdOf(bird.State), "the dive did not chain");

            Step(ref bird, b, b.Row(bird.State).DurationMilliseconds + 1.0);
            Assert.AreEqual(SeagullStates.Float, b.IdOf(bird.State), "the splash did not chain");
        }

        /// <summary>A tick longer than the strip it lands in must not lose the overrun at the boundary,
        /// or a slow frame quietly steals animation time from whatever comes next.</summary>
        [Test]
        public void TheOverrunAtAChainBoundaryIsCarriedIntoTheNextState()
        {
            var b = Behaviour();
            var bird = Airborne(b, SeagullStates.Swoop, altitude: 8.0);
            SeagullStateMachine.TryTransition(ref bird, b.IndexOf(SeagullStates.Dive), b);

            var splash = b.Row(b.IndexOf(SeagullStates.Splash));
            double dive = b.Row(b.IndexOf(SeagullStates.Dive)).DurationMilliseconds;
            Step(ref bird, b, dive + splash.FrameMilliseconds * 2.0);

            Assert.AreEqual(SeagullStates.Splash, b.IdOf(bird.State));
            Assert.AreEqual(2, bird.Frame,
                            "the two frames of splash the long tick paid for were dropped on the floor");
        }

        /// <summary>A cycles-limited state remembers where it came from and goes back there. The drop
        /// gives cycles only to <c>preen</c> and <c>peck</c>, and both hand back to <c>float</c>.</summary>
        [Test]
        public void APreeningBirdGoesBackToTheWaterAfterItsRolledCycles()
        {
            var b = Behaviour();
            int floatState = b.IndexOf(SeagullStates.Float);
            var bird = Airborne(b, SeagullStates.Float, altitude: 0.0);

            Assert.IsTrue(SeagullStateMachine.TryTransition(ref bird, b.IndexOf(SeagullStates.Preen), b));
            Assert.AreEqual(floatState, bird.ReturnState, "preen forgot where it came from");

            var rng = SeagullFlockMath.Mulberry32.ForFlockSeed(11);
            SeagullStateMachine.RollCycles(ref bird, b, ref rng);
            int target = bird.CycleTarget;
            Assert.GreaterOrEqual(target, 2, "the drop rolls preen 2..5 cycles");
            Assert.LessOrEqual(target, 5);

            double cycle = b.Row(bird.State).DurationMilliseconds;
            Step(ref bird, b, cycle * target - 1.0);
            Assert.AreEqual(SeagullStates.Preen, b.IdOf(bird.State), "it gave up a cycle early");
            Step(ref bird, b, 2.0);
            Assert.AreEqual(SeagullStates.Float, b.IdOf(bird.State), "it never went back to the water");
        }

        // =========================================================================================
        //  2. coming down on a spot
        // =========================================================================================

        /// <summary>
        /// 🔴 <c>swoop</c> cruises no lower than 1.0 m and <c>land</c> is entered at 0.6 m. Read the
        /// band floor as a deck and the descent stops 0.4 m — thirteen pixels — short of its own flare,
        /// then teleports the rest in one frame. A bird that has been TOLD to come down is allowed
        /// below its cruise floor, as far as the arrival state's entry height and no further.
        /// </summary>
        [Test]
        public void ACommandedDescentIsLetBelowItsCruiseFloor()
        {
            var b = Behaviour();
            int swoop = b.IndexOf(SeagullStates.Swoop);
            int land = b.IndexOf(SeagullStates.Land);
            double cruiseFloor = b.AltitudeMin(swoop);
            double entry = b.EntryAltitude(land);
            Assert.Greater(cruiseFloor, entry,
                           "the drop no longer puts swoop's floor above land's entry — this test's " +
                           "whole premise was that it does");

            var bird = Airborne(b, SeagullStates.Fly, x: 0.0, y: 40.0, altitude: 12.0);
            Assert.IsTrue(SeagullStateMachine.CommandAlight(ref bird, land, 0.0, 0.0, 0.7, b));
            Assert.AreEqual(SeagullStates.Swoop, b.IdOf(bird.State),
                            "a fly has no edge to land; the command should have swooped it first");

            for (int i = 0; i < 4000 && bird.AltitudeMetres > entry + 1e-6; i++) Step(ref bird, b, 16.0);

            Assert.AreEqual(SeagullStates.Swoop, b.IdOf(bird.State), "it left the descent on its own");
            Assert.LessOrEqual(bird.AltitudeMetres, entry + SeagullStateMachine.AltitudeEpsilonMetres,
                               "the swoop stopped at its cruise floor instead of at the flare");
            Assert.GreaterOrEqual(bird.AltitudeMetres, entry - 1e-6, "it sank past the flare");
            Assert.IsTrue(SeagullStateMachine.ReadyToAlight(in bird, b), "and it never became ready");
        }

        /// <summary>The sidecar says <c>approach_into_wind</c>. Nobody steers the bird there: the run-in
        /// leg is laid out downwind of the spot, so flying it IS flying into the wind.</summary>
        [Test]
        public void TheRunInIsFlownIntoTheWind()
        {
            var b = Behaviour();
            int land = b.IndexOf(SeagullStates.Land);
            const double windBlowsToward = 0.7;

            var bird = Airborne(b, SeagullStates.Fly, x: 0.0, y: 40.0, altitude: 12.0);
            Assert.IsTrue(SeagullStateMachine.CommandAlight(ref bird, land, 0.0, 0.0, windBlowsToward, b));

            double run = SeagullStateMachine.ApproachRunMetres(b, land);
            Assert.Greater(run, 0.0, "a zero run-in has no direction to fly");
            Assert.AreEqual(Math.Sin(windBlowsToward) * run, bird.AlightApproachX, 1e-9,
                            "the turn point is not downwind of the spot");
            Assert.AreEqual(Math.Cos(windBlowsToward) * run, bird.AlightApproachY, 1e-9);

            for (int i = 0; i < 4000 && !SeagullStateMachine.ReadyToAlight(in bird, b); i++)
                Step(ref bird, b, 16.0);

            Assert.AreEqual(Wrap(windBlowsToward + Math.PI), Wrap(bird.Heading), 1e-3,
                            "the last leg is not flown into the wind");
        }

        /// <summary>
        /// 🔴 The pivot is where the shadow is drawn. The flare's own travel was already spent getting
        /// the bird to the spot, so from the moment <c>land</c> begins the pivot must not move a
        /// millimetre while the sprite comes down onto it — and when <c>stand</c> arrives the bird is at
        /// altitude exactly zero, on frame zero, on the commanded spot.
        /// </summary>
        [Test]
        public void TheFlareComesDownWithoutMovingThePivot()
        {
            var b = Behaviour();
            int land = b.IndexOf(SeagullStates.Land);
            const double spotX = -3.25, spotY = 1.75;

            var bird = Airborne(b, SeagullStates.Fly, x: 0.0, y: 40.0, altitude: 12.0);
            Assert.IsTrue(SeagullStateMachine.CommandAlight(ref bird, land, spotX, spotY, 0.7, b));

            for (int i = 0; i < 4000 && !SeagullStateMachine.ReadyToAlight(in bird, b); i++)
                Step(ref bird, b, 16.0);
            Assert.IsTrue(SeagullStateMachine.TryAlight(ref bird, b), "the bird never took the flare");
            Assert.AreEqual(SeagullStates.Land, b.IdOf(bird.State));

            double previous = bird.AltitudeMetres;
            Assert.AreEqual(b.EntryAltitude(land), previous, Eps, "the flare began off its entry height");

            int guard = 0;
            while (b.IdOf(bird.State) == SeagullStates.Land && guard++ < 4000)
            {
                Step(ref bird, b, 16.0);
                Assert.IsTrue(bird.X == spotX && bird.Y == spotY,
                              "the pivot moved during the flare — the shadow is drawn there, and a " +
                              "pivot that creeps is a shadow the bird never quite meets");
                Assert.LessOrEqual(bird.AltitudeMetres, previous + Eps, "the flare climbed");
                previous = bird.AltitudeMetres;
            }

            Assert.AreEqual(SeagullStates.Stand, b.IdOf(bird.State), "the flare did not chain to stand");
            Assert.AreEqual(0, bird.Frame, "stand should arrive on frame 0");
            Assert.IsTrue(bird.AltitudeMetres == 0.0,
                          "a bird on the ground is at exactly zero, not about zero — the shadow test " +
                          "reads this number; got " + bird.AltitudeMetres.ToString("R"));
            Assert.IsTrue(bird.X == spotX && bird.Y == spotY, "it did not stand where it was sent");
            Assert.AreEqual(-1, bird.IntentState, "the landing intent outlived the landing");
        }

        /// <summary>A bird already on a surface cannot be commanded down again, and only the two states
        /// that arrive on a surface can be asked for.</summary>
        [Test]
        public void ALandingCommandIsRefusedWhenItMakesNoSense()
        {
            var b = Behaviour();
            var standing = Airborne(b, SeagullStates.Stand, altitude: 0.0);
            Assert.IsFalse(SeagullStateMachine.CommandAlight(
                ref standing, b.IndexOf(SeagullStates.Land), 0.0, 0.0, 0.0, b),
                "a standing bird was told to land again");

            var flying = Airborne(b, SeagullStates.Fly);
            Assert.IsFalse(SeagullStateMachine.CommandAlight(
                ref flying, b.IndexOf(SeagullStates.Glide), 0.0, 0.0, 0.0, b),
                "glide is not a state you arrive on a surface in");
            Assert.AreEqual(-1, flying.IntentState, "a refused command still left an intent behind");
        }

        // =========================================================================================
        //  3. leaving, rejoining, and repeating
        // =========================================================================================

        /// <summary>A bird off the ground rejoins the wheel over the sidecar's own regroup time. Until
        /// it gets there it is NOT flock-bound — a bird snapped straight onto a wheel cruising at 3-14 m
        /// from a 1.2 m climb-out is a pop.</summary>
        [Test]
        public void LeavingTheGroundArmsTheRejoinRampAndArrivingOnTheWheelDisarmsIt()
        {
            var b = Behaviour();
            var bird = Airborne(b, SeagullStates.Stand, x: 5.0, y: -2.0, altitude: 0.0);

            Assert.IsTrue(SeagullStateMachine.CommandDepart(ref bird, b));
            Assert.AreEqual(SeagullStates.Takeoff, b.IdOf(bird.State));
            Assert.IsFalse(bird.FlockBound, "it was put back on the wheel before it had climbed");
            Assert.Greater(SeagullStateMachine.RejoinBlend01(in bird, b), 0.0, "the ramp never armed");

            var wheel = new SeagullFlockMath.SeagullFlockBird(
                1.5, -0.5, 9.0, 0.25, 2, SeagullFlockMath.SeagullFlockAnim.Glide, 1, 1.0);

            double ramp = b.Flock.RegroupSeconds * 1000.0;
            Assert.Greater(ramp, 0.0, "the sidecar stopped naming a regroup time");

            double blend = SeagullStateMachine.RejoinBlend01(in bird, b);
            for (double t = 0.0; t < ramp * 1.5 && !bird.FlockBound; t += 16.0)
            {
                SeagullStateMachine.Step(ref bird, 16.0, b, in wheel, 20.0, 30.0);
                double now = SeagullStateMachine.RejoinBlend01(in bird, b);
                Assert.GreaterOrEqual(now, blend - 1e-12, "the rejoin ramp ran backwards");
                blend = now;
            }

            Assert.IsTrue(bird.FlockBound, "the bird never rejoined the flock");
            Assert.AreEqual(20.0 + wheel.X, bird.X, 1e-9, "rejoined off the wheel's own offset");
            Assert.AreEqual(30.0 + wheel.Y, bird.Y, 1e-9);
            Assert.AreEqual(wheel.Z, bird.AltitudeMetres, 1e-9);
            Assert.AreEqual(SeagullStates.Glide, b.IdOf(bird.State), "it did not adopt the wheel's pose");
        }

        /// <summary>Only the four states with an edge to <c>takeoff</c> can be told to go. A bird in the
        /// air has nowhere to take off from.</summary>
        [Test]
        public void OnlyABirdOnSomethingCanBeToldToLeaveIt()
        {
            var b = Behaviour();
            foreach (string id in new[] { SeagullStates.Stand, SeagullStates.Walk,
                                          SeagullStates.Perch, SeagullStates.Float })
            {
                var bird = Airborne(b, id, altitude: 0.0);
                Assert.IsTrue(SeagullStateMachine.CommandDepart(ref bird, b), id + " could not leave");
            }

            var flying = Airborne(b, SeagullStates.Fly);
            Assert.IsFalse(SeagullStateMachine.CommandDepart(ref flying, b),
                           "a flying bird took off from the sky");
            Assert.AreEqual(SeagullStates.Fly, b.IdOf(flying.State));
        }

        /// <summary>The charter: "two runs on one seed agree". The machine is arithmetic on doubles with
        /// no clock and no ambient randomness in it, so the same script run twice is the same bird —
        /// bit for bit, not nearly.</summary>
        [Test]
        public void TwoRunsOfTheSameScriptAgreeBitForBit()
        {
            var b = Behaviour();
            int firstRoll, secondRoll;
            var first = RunTheScript(b, 4242, out firstRoll);
            var second = RunTheScript(b, 4242, out secondRoll);

            Assert.AreEqual(firstRoll, secondRoll, "rolled cycles");
            Assert.AreEqual(first.State, second.State, "state");
            Assert.AreEqual(first.Frame, second.Frame, "frame");
            Assert.AreEqual(first.Dir, second.Dir, "dir");
            Assert.AreEqual(first.CycleTarget, second.CycleTarget, "cycle target");
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(first.X),
                            BitConverter.DoubleToInt64Bits(second.X), "x");
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(first.Y),
                            BitConverter.DoubleToInt64Bits(second.Y), "y");
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(first.AltitudeMetres),
                            BitConverter.DoubleToInt64Bits(second.AltitudeMetres), "altitude");
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(first.Heading),
                            BitConverter.DoubleToInt64Bits(second.Heading), "heading");

            // Two controls, so agreeing above is a property of the machine and not of a script that
            // has nothing in it to disagree about. The first: the stream it is handed is actually
            // read. The second: three more milliseconds of flying really do move the bird, so the
            // bit-for-bit comparison had something to catch.
            int otherRoll;
            RunTheScript(b, 99, out otherRoll);
            Assert.AreNotEqual(firstRoll, otherRoll,
                               "a different stream rolled the same cycles — the seed is being ignored");

            int nudgedRoll;
            var nudged = RunTheScript(b, 4242, out nudgedRoll, extraSteps: 3);
            Assert.AreNotEqual(BitConverter.DoubleToInt64Bits(first.X),
                               BitConverter.DoubleToInt64Bits(nudged.X),
                               "three more steps left the bird in exactly the same place — the " +
                               "script has stopped moving and every assertion above is vacuous");
        }

        /// <summary>One scripted life: wheel off, swoop, dive to a splash, float, preen for a rolled
        /// number of cycles, then off again. Everything a bird can do that has state in it.</summary>
        static SeagullSimBird RunTheScript(SeagullBehaviour b, int seed, out int rolledCycles,
                                           int extraSteps = 0)
        {
            var rng = SeagullFlockMath.Mulberry32.ForFlockSeed(seed);
            var bird = Airborne(b, SeagullStates.Fly, x: 2.0, y: -7.0, altitude: 11.0, heading: 1.1);

            SeagullStateMachine.TryTransition(ref bird, b.IndexOf(SeagullStates.Swoop), b);
            for (int i = 0; i < 40; i++) Step(ref bird, b, 17.0);

            SeagullStateMachine.TryTransition(ref bird, b.IndexOf(SeagullStates.Dive), b);
            for (int i = 0; i < 120; i++) Step(ref bird, b, 17.0);   // dive -> splash -> float

            SeagullStateMachine.TryTransition(ref bird, b.IndexOf(SeagullStates.Preen), b);
            SeagullStateMachine.RollCycles(ref bird, b, ref rng);
            rolledCycles = bird.CycleTarget;
            for (int i = 0; i < 400; i++) Step(ref bird, b, 17.0);

            SeagullStateMachine.CommandDepart(ref bird, b);
            for (int i = 0; i < 60 + extraSteps; i++) Step(ref bird, b, 17.0);
            return bird;
        }

        /// <summary>Whatever the script did, a bird on a surface is at altitude exactly zero and a bird
        /// in the air is not. The PlayMode shadow test turns on the first half of that sentence.</summary>
        [Test]
        public void ABirdOnASurfaceIsAtExactlyZeroAndABirdInTheAirIsNot()
        {
            var b = Behaviour();
            foreach (string id in new[] { SeagullStates.Stand, SeagullStates.Walk,
                                          SeagullStates.Float, SeagullStates.Perch })
            {
                var bird = Airborne(b, id, altitude: 4.0);   // hand it a wrong altitude on purpose
                Step(ref bird, b, 33.0);
                Assert.IsTrue(bird.AltitudeMetres == 0.0,
                              id + " is on a surface but sits at " + bird.AltitudeMetres.ToString("R"));
            }

            var flying = Airborne(b, SeagullStates.Fly, altitude: 9.0);
            Step(ref flying, b, 33.0);
            Assert.Greater(flying.AltitudeMetres, 1.0,
                           "a flying bird was flattened onto the ground — the surface rule is being " +
                           "applied to states that are not on one");
        }
    }
}
