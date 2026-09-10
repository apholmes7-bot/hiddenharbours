using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The port is the rig, and the rig is deterministic.</b> <see cref="SeagullFlockMath"/> is a
    /// hand transcription of <c>seagullIsoRig.js</c>'s <c>flock(n, t, {seed, radius})</c> into C#, and
    /// a transcription is exactly the kind of code that looks right and is wrong. These are the claims
    /// that can be made without an engine: the JS number semantics the port had to reproduce, the
    /// nine-draw order that decides which flock you get, and that one seed gives one sky forever.
    ///
    /// <para><b>The arithmetic itself is checked elsewhere.</b> Restating the formulae here would be a
    /// mirror — a fixture that takes its answer from the code it is checking. The authority for the
    /// numbers is <c>SeagullFlockParityTests</c> in <c>Tests/EditMode/RigBaking</c>, which runs the art
    /// director's actual JS in the repo's ClearScript V8 and compares. What lives here is the SHAPE:
    /// determinism, purity, the draw order, and the rock caps.</para>
    ///
    /// <para>🔴 <b>What these would have caught.</b> The size jitter is the LAST of the nine draws
    /// because JS evaluates object-literal properties in source order and <c>scale</c> is the last
    /// property of the record the rig pushes. A port that draws it earlier is a DIFFERENT flock from
    /// bird 1 onward and looks perfectly plausible on screen —
    /// <see cref="TheSizeJitterIsTheNinthDrawOfEveryBirdsBlock"/> is the only thing that tells the two
    /// apart. Likewise <c>opts.seed || 7</c> means seed 0 IS seed 7, and JS breaks halves toward +∞
    /// where <c>Math.Round</c> rounds to even — a facing off by a row at every 22.5° boundary.</para>
    /// </summary>
    public class SeagullFlockMathTests
    {
        /// <summary>What <c>(7 * 2654435761) &gt;&gt;&gt; 0</c> is in JS. Written out rather than
        /// recomputed, so this pins the whitening against an outside answer instead of against the
        /// code under test.</summary>
        const uint DefaultFlockState = 1401181143u;

        /// <summary>The committed def, loaded through the PRODUCTION path — the same
        /// <c>Resources.Load</c> call <see cref="GullFlock"/> makes at Awake. If this comes back null
        /// the harbour has no gulls, so the load is itself the first assertion.</summary>
        static SeagullVisualDef Def()
        {
            var def = Resources.Load<SeagullVisualDef>(SeagullVisualDef.ResourcesPath);
            Assert.IsNotNull(def,
                "Resources.Load<SeagullVisualDef>(\"" + SeagullVisualDef.ResourcesPath + "\") came " +
                "back null — GullFlock.Awake makes exactly this call and stands itself down when it " +
                "fails, so the harbour would simply have no birds");
            return def;
        }

        static SeagullFlockMath.SeagullFlockTuning Tuning() => Def().Behaviour.FlockTuning;

        static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        // =========================================================================================
        //  1. the JS number semantics the port had to carry across
        // =========================================================================================

        /// <summary>
        /// <c>mulberry((opts.seed||7)*2654435761)</c>. Two claims in one: a falsy seed becomes 7, so
        /// seed 0 is not a sky of its own but the DEFAULT sky; and the whitened state is the number JS
        /// actually produces, not merely a number this file agrees with itself about.
        /// </summary>
        [Test]
        public void SeedZeroIsSeedSevenAndSevenWhitensToWhatJsSays()
        {
            Assert.AreEqual(DefaultFlockState, SeagullFlockMath.Mulberry32.ForFlockSeed(7).State,
                "(7 * 2654435761) >>> 0 is " + DefaultFlockState + " in V8");
            Assert.AreEqual(SeagullFlockMath.Mulberry32.ForFlockSeed(SeagullFlockMath.DefaultSeed).State,
                            SeagullFlockMath.Mulberry32.ForFlockSeed(0).State,
                            "the rig writes (opts.seed||7), so seed 0 must whiten exactly as 7 does");
        }

        /// <summary>A seed that is not 0 is its own sky — without this the test above would pass on a
        /// whitener that ignored its argument.</summary>
        [Test]
        public void ASeedThatIsNotZeroIsItsOwnStream()
        {
            Assert.AreNotEqual(SeagullFlockMath.Mulberry32.ForFlockSeed(7).State,
                               SeagullFlockMath.Mulberry32.ForFlockSeed(8).State);
        }

        /// <summary>
        /// <c>&gt;&gt;&gt;0</c> is ECMAScript ToUint32: truncate toward zero, then fold into
        /// [0, 2^32). A cast is not that — <c>(uint)(-1.0)</c> is undefined-ish behaviour in C# and
        /// <c>(uint)2.9</c> and <c>(uint)(-2.9)</c> do not both truncate the way JS does.
        /// </summary>
        [Test]
        public void ToUint32IsEcmascriptsNotCsharpsCast()
        {
            Assert.AreEqual(0u, SeagullFlockMath.ToUint32(0.0));
            Assert.AreEqual(2u, SeagullFlockMath.ToUint32(2.9));
            Assert.AreEqual(4294967294u, SeagullFlockMath.ToUint32(-2.9), "truncates, THEN wraps");
            Assert.AreEqual(4294967295u, SeagullFlockMath.ToUint32(-1.0));
            Assert.AreEqual(0u, SeagullFlockMath.ToUint32(4294967296.0));
            Assert.AreEqual(1u, SeagullFlockMath.ToUint32(4294967297.0));
            Assert.AreEqual(DefaultFlockState,
                            SeagullFlockMath.ToUint32(7.0 * SeagullFlockMath.SeedWhitener));
        }

        /// <summary>
        /// <c>Math.round</c> in JS breaks halves toward +∞: −0.5 rounds to 0, not to −1, and 2.5 rounds
        /// to 3, not to 2. <c>Math.Round</c> in .NET does banker's rounding by default and would
        /// disagree on every other half — which in <see cref="SeagullFlockMath.DirOf"/> is a facing off
        /// by one whole row of the sheet.
        /// </summary>
        [Test]
        public void JsRoundBreaksHalvesTowardPositiveInfinity()
        {
            Assert.AreEqual(1.0, SeagullFlockMath.JsRound(0.5), 0.0);
            Assert.AreEqual(0.0, SeagullFlockMath.JsRound(-0.5), 0.0);
            Assert.AreEqual(2.0, SeagullFlockMath.JsRound(1.5), 0.0);
            Assert.AreEqual(-1.0, SeagullFlockMath.JsRound(-1.5), 0.0,
                            "Math.Round would give -2 here");
            Assert.AreEqual(3.0, SeagullFlockMath.JsRound(2.5), 0.0,
                            "Math.Round would give 2 here — banker's rounding is not the rig's");
            Assert.AreEqual(0.0, SeagullFlockMath.JsRound(0.49999999999999994), 0.0,
                            "the largest double below a half must NOT round up — floor(x + 0.5) " +
                            "double-rounds this one all the way to 1, which is why the port does not " +
                            "add the half first");
        }

        /// <summary>The rig's <c>mod=(a,n)=&gt;((a%n)+n)%n</c> — a frame index is never negative, where
        /// C#'s own <c>%</c> would hand one back and index off the front of the strip.</summary>
        [Test]
        public void ModIsAlwaysPositive()
        {
            Assert.AreEqual(5.0, SeagullFlockMath.Mod(-1.0, 6.0), 1e-12);
            Assert.AreEqual(5.5, SeagullFlockMath.Mod(-0.5, 6.0), 1e-12);
            Assert.AreEqual(0.0, SeagullFlockMath.Mod(-6.0, 6.0), 1e-12);
            Assert.AreEqual(2.0, SeagullFlockMath.Mod(8.0, 6.0), 1e-12);
            Assert.Less(-1.0 % 6.0, 0.0, "the negative control: C#'s own % really does go negative");
        }

        /// <summary>
        /// The sheet's eight rows are CLOCKWISE bearings from North — the seagull is the minority
        /// convention in this repo, measured off the committed pixels during the PR 1 intake, and the
        /// baker does not remap them, so row <c>r</c> IS dir <c>r</c>. A heading of due East must
        /// select row 2; a counter-clockwise reading would select row 6 and fly every bird backwards.
        /// </summary>
        [Test]
        public void DirOfIsClockwiseFromNorth()
        {
            const double deg = Math.PI / 180.0;
            Assert.AreEqual(0, SeagullFlockMath.DirOf(0.0), "0 rad is North");
            Assert.AreEqual(1, SeagullFlockMath.DirOf(45.0 * deg), "north-east");
            Assert.AreEqual(2, SeagullFlockMath.DirOf(90.0 * deg), "due east — clockwise, so row 2");
            Assert.AreEqual(4, SeagullFlockMath.DirOf(180.0 * deg), "due south");
            Assert.AreEqual(6, SeagullFlockMath.DirOf(-90.0 * deg), "due west, reached the short way");
            Assert.AreEqual(0, SeagullFlockMath.DirOf(359.0 * deg), "wraps back onto North");
        }

        /// <summary>Every heading in two full turns, either way, lands on a real row of the sheet. A
        /// negative index is an IndexOutOfRange the first time a bird flies the other way.</summary>
        [Test]
        public void DirOfNeverLeavesTheSheet()
        {
            int rows = Def().Directions;
            for (int i = -720; i <= 720; i++)
            {
                int dir = SeagullFlockMath.DirOf(i * (Math.PI / 180.0));
                Assert.That(dir, Is.InRange(0, rows - 1), "heading " + i + " deg");
            }
        }

        // =========================================================================================
        //  2. one seed, one sky — forever
        // =========================================================================================

        /// <summary>
        /// Rule 5 at the bit level: two runs of the same call are the same flock, field for field, not
        /// "close enough". Nothing in the wheel is allowed to drift.
        /// </summary>
        [Test]
        public void TwoRunsOnOneSeedAgreeBitForBit()
        {
            var tuning = Tuning();
            var a = new SeagullFlockMath.SeagullFlockBird[9];
            var b = new SeagullFlockMath.SeagullFlockBird[9];

            Assert.AreEqual(9, SeagullFlockMath.Flock(9, 12345.0, 7, 0.0, tuning, a));
            Assert.AreEqual(9, SeagullFlockMath.Flock(9, 12345.0, 7, 0.0, tuning, b));

            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(Bits(a[i].X), Bits(b[i].X), "bird " + i + " x");
                Assert.AreEqual(Bits(a[i].Y), Bits(b[i].Y), "bird " + i + " y");
                Assert.AreEqual(Bits(a[i].Z), Bits(b[i].Z), "bird " + i + " z");
                Assert.AreEqual(Bits(a[i].Heading), Bits(b[i].Heading), "bird " + i + " heading");
                Assert.AreEqual(Bits(a[i].Scale), Bits(b[i].Scale), "bird " + i + " scale");
                Assert.AreEqual(a[i].Dir, b[i].Dir, "bird " + i + " dir");
                Assert.AreEqual(a[i].Anim, b[i].Anim, "bird " + i + " anim");
                Assert.AreEqual(a[i].Frame, b[i].Frame, "bird " + i + " frame");
            }
        }

        /// <summary>The negative control for the test above: two seeds are two skies. Without it, a
        /// <c>Flock</c> that wrote nothing at all would pass determinism with full marks.</summary>
        [Test]
        public void TwoSeedsAreTwoFlocks()
        {
            var tuning = Tuning();
            var a = new SeagullFlockMath.SeagullFlockBird[9];
            var b = new SeagullFlockMath.SeagullFlockBird[9];
            SeagullFlockMath.Flock(9, 12345.0, 7, 0.0, tuning, a);
            SeagullFlockMath.Flock(9, 12345.0, 8, 0.0, tuning, b);

            bool anyDifferent = false;
            for (int i = 0; i < a.Length && !anyDifferent; i++)
                anyDifferent = Bits(a[i].X) != Bits(b[i].X) || Bits(a[i].Y) != Bits(b[i].Y);
            Assert.IsTrue(anyDifferent, "seed 7 and seed 8 wheeled over exactly the same ground");
        }

        /// <summary>
        /// The wheel is a PURE function of <c>(count, t, seed, radius, tuning)</c>. Sampling other
        /// times and other seeds in between must leave no trace — if the generator carried state
        /// across calls, a fixture that stepped the flock at a different rate would get a different
        /// sky, and rule 5 would be a comment rather than a property.
        /// </summary>
        [Test]
        public void TheWheelCarriesNoStateBetweenCalls()
        {
            var tuning = Tuning();
            var first = new SeagullFlockMath.SeagullFlockBird[5];
            var later = new SeagullFlockMath.SeagullFlockBird[5];
            var scratch = new SeagullFlockMath.SeagullFlockBird[5];

            SeagullFlockMath.Flock(5, 4000.0, 7, 0.0, tuning, first);
            SeagullFlockMath.Flock(5, 9999.0, 7, 0.0, tuning, scratch);
            SeagullFlockMath.Flock(5, 61234.0, 3, 12.0, tuning, scratch);
            SeagullFlockMath.Flock(5, 4000.0, 7, 0.0, tuning, later);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(Bits(first[i].X), Bits(later[i].X), "bird " + i + " x");
                Assert.AreEqual(Bits(first[i].Z), Bits(later[i].Z), "bird " + i + " z");
                Assert.AreEqual(first[i].Frame, later[i].Frame, "bird " + i + " frame");
            }
        }

        /// <summary>
        /// <b>The draw order is the contract.</b> Nine draws per bird — loop radius, period, phase,
        /// turn direction, cruise altitude, ellipse squash, glide phase, swoop interval, and the size
        /// jitter LAST. Replaying the stream by hand, skipping eight and checking the ninth against the
        /// emitted <c>Scale</c>, pins both the ORDER and the COUNT: a port that spends eight draws on a
        /// bird, or ten, gets bird 1 wrong and every bird after it, while bird 0 stays perfect.
        /// </summary>
        [Test]
        public void TheSizeJitterIsTheNinthDrawOfEveryBirdsBlock()
        {
            var tuning = Tuning();
            var birds = new SeagullFlockMath.SeagullFlockBird[9];
            SeagullFlockMath.Flock(birds.Length, 7777.0, 7, 0.0, tuning, birds);

            var rng = SeagullFlockMath.Mulberry32.ForFlockSeed(7);
            for (int i = 0; i < birds.Length; i++)
            {
                for (int d = 0; d < 8; d++) rng.Next();   // rr, per, ph, ccw, alt, sq, gph, swEvery
                double expected = SeagullFlockMath.ScaleJitterBase
                                + rng.Next() * SeagullFlockMath.ScaleJitterSpan;
                Assert.AreEqual(Bits(expected), Bits(birds[i].Scale),
                    "bird " + i + ": the size jitter is not the ninth draw of its block. JS evaluates " +
                    "object-literal properties in source order and the record the rig pushes names " +
                    "scale LAST — draw it anywhere else and this is a different flock");
            }
        }

        /// <summary>The buffer is the caller's and the count is clamped to it — the presenter hands the
        /// same array down every tick, and nothing here allocates (rule 7).</summary>
        [Test]
        public void TheCallerOwnsTheBuffer()
        {
            var tuning = Tuning();
            var small = new SeagullFlockMath.SeagullFlockBird[4];
            Assert.AreEqual(4, SeagullFlockMath.Flock(20, 100.0, 7, 0.0, tuning, small));
            Assert.AreEqual(0, SeagullFlockMath.Flock(0, 100.0, 7, 0.0, tuning, small));
            Assert.AreEqual(0, SeagullFlockMath.Flock(4, 100.0, 7, 0.0, tuning, null));
        }

        /// <summary>
        /// A non-positive radius means "the rig's own", which is what the shipped
        /// <see cref="GullConfig"/> asks for — <c>WheelRadiusMetres</c> ships at 0. An explicit radius
        /// has to actually widen the wheel, or the owner's knob is decoration.
        /// </summary>
        [Test]
        public void ANonPositiveRadiusTakesTheRigsOwn()
        {
            var tuning = Tuning();
            var zero = new SeagullFlockMath.SeagullFlockBird[6];
            var rigs = new SeagullFlockMath.SeagullFlockBird[6];
            var wide = new SeagullFlockMath.SeagullFlockBird[6];
            SeagullFlockMath.Flock(6, 2500.0, 7, 0.0, tuning, zero);
            SeagullFlockMath.Flock(6, 2500.0, 7, tuning.RadiusMetres, tuning, rigs);
            SeagullFlockMath.Flock(6, 2500.0, 7, tuning.RadiusMetres * 3.0, tuning, wide);

            double near = 0.0, far = 0.0;
            for (int i = 0; i < zero.Length; i++)
            {
                Assert.AreEqual(Bits(zero[i].X), Bits(rigs[i].X), "bird " + i);
                near += Math.Abs(zero[i].X);
                far += Math.Abs(wide[i].X);
            }
            Assert.Greater(near, 0.0, "the rig's own radius produced a wheel of no width at all");
            Assert.Greater(far, near * 2.0, "a wider wheel must actually be wider");
        }

        /// <summary>The def's FLOCK block and the three airborne strips assemble a usable tuning. A
        /// zero frame count or a zero frame time here is a divide-by-zero in the frame index — which is
        /// why the presenter checks <c>IsUsable</c> before it flies anything.</summary>
        [Test]
        public void TheCommittedDefMakesAUsableTuning()
        {
            var tuning = Tuning();
            Assert.IsTrue(tuning.IsUsable, "the committed def does not assemble a usable flock tuning");
            foreach (SeagullFlockMath.SeagullFlockAnim anim in
                     Enum.GetValues(typeof(SeagullFlockMath.SeagullFlockAnim)))
            {
                Assert.Greater(tuning.FramesOf(anim), 0, SeagullFlockMath.AnimId(anim) + " frames");
                Assert.Greater(tuning.MillisecondsOf(anim), 0.0, SeagullFlockMath.AnimId(anim) + " ms");
            }
        }

        /// <summary>Every frame index the wheel hands out is inside its own strip, swept over a long
        /// stretch of time and back before zero. <c>mod</c> is what makes that true; C#'s <c>%</c>
        /// would not.</summary>
        [Test]
        public void EveryFrameIndexLandsInsideItsStrip()
        {
            var tuning = Tuning();
            var birds = new SeagullFlockMath.SeagullFlockBird[9];
            for (double t = -50000.0; t <= 200000.0; t += 137.0)
            {
                SeagullFlockMath.Flock(birds.Length, t, 7, 0.0, tuning, birds);
                for (int i = 0; i < birds.Length; i++)
                    Assert.That(birds[i].Frame, Is.InRange(0, tuning.FramesOf(birds[i].Anim) - 1),
                                "t=" + t + " bird " + i + " " + SeagullFlockMath.AnimId(birds[i].Anim));
            }
        }

        // =========================================================================================
        //  3. the float ROCK, capped by construction
        // =========================================================================================

        /// <summary>
        /// The sidecar's WATER note is the law: <i>roll_deg 2.4 / heave_px 1 max — a gull rides higher
        /// than a hull.</i> Swept over a full turn and every sea an environment service could hand
        /// back, including values outside 0..1, neither channel may pass its cap.
        ///
        /// <para>A cap test that only checks "≤" would pass with full marks on a pose that never moves
        /// at all, so this also asserts the cap is REACHED in the roughest sea. The bird really does
        /// rock, and it really does stop there.</para>
        /// </summary>
        [Test]
        public void TheRockCannotPassTheSidecarsCapsInAnySea()
        {
            var water = Def().Behaviour.Water;
            Assert.Greater(water.RockRollDegreesMax, 0.0, "the def carries no roll cap to test");
            Assert.Greater(water.RockHeavePixelsMax, 0.0, "the def carries no heave cap to test");

            double maxRoll = 0.0, maxHeave = 0.0;
            foreach (double sea in new[] { -1.0, 0.0, 0.25, 0.5, 0.75, 1.0, 2.0 })
            {
                for (int step = 0; step <= 720; step++)
                {
                    double angle = step * (Math.PI / 360.0);
                    SeagullFlockMath.RockPose(angle, sea, water.RockRollDegreesMax,
                                              water.RockHeavePixelsMax,
                                              out double roll, out double heave);
                    Assert.LessOrEqual(Math.Abs(roll), water.RockRollDegreesMax + 1e-12,
                                       "roll at sea " + sea + ", step " + step);
                    Assert.LessOrEqual(Math.Abs(heave), water.RockHeavePixelsMax + 1e-12,
                                       "heave at sea " + sea + ", step " + step);
                    if (sea < 1.0) continue;
                    maxRoll = Math.Max(maxRoll, Math.Abs(roll));
                    maxHeave = Math.Max(maxHeave, Math.Abs(heave));
                }
            }

            Assert.AreEqual(water.RockRollDegreesMax, maxRoll, 1e-6,
                            "the roll never reaches its cap — a bird that does not rock at all would " +
                            "sail through the assertions above");
            Assert.AreEqual(water.RockHeavePixelsMax, maxHeave, 1e-6,
                            "the heave never reaches its cap");
        }

        /// <summary>A dead calm is dead flat. The rock belongs to the sea, not to the bird's idle.</summary>
        [Test]
        public void ADeadCalmDoesNotRockTheBird()
        {
            var water = Def().Behaviour.Water;
            for (int step = 0; step <= 90; step++)
            {
                SeagullFlockMath.RockPose(step * 0.1, 0.0, water.RockRollDegreesMax,
                                          water.RockHeavePixelsMax,
                                          out double roll, out double heave);
                Assert.AreEqual(0.0, roll, 0.0);
                Assert.AreEqual(0.0, heave, 0.0);
            }
        }

        /// <summary>
        /// The rock turns once per cycle, and the cycle the presenter hands it is the <c>float</c>
        /// strip's own duration — so the bob and the wing-settle beat together instead of sliding
        /// against each other. Roll and heave are a quarter cycle apart: the bird leans as it lifts.
        /// </summary>
        [Test]
        public void TheRockTurnsOncePerStripAndLeansAsItLifts()
        {
            var behaviour = Def().Behaviour;
            double cycle = behaviour.Row(behaviour.IndexOf(SeagullStates.Float)).DurationMilliseconds;
            Assert.Greater(cycle, 0.0, "the float strip has no duration to rock on");

            double a0 = SeagullFlockMath.RockAngle(0.0, cycle, 0.4);
            double a1 = SeagullFlockMath.RockAngle(cycle, cycle, 0.4);
            Assert.AreEqual(Math.PI * 2.0, a1 - a0, 1e-12, "one strip must be one full rock");

            var water = behaviour.Water;
            SeagullFlockMath.RockPose(a0, 1.0, water.RockRollDegreesMax, water.RockHeavePixelsMax,
                                      out double roll0, out double heave0);
            SeagullFlockMath.RockPose(a0 + Math.PI / 2.0, 1.0, water.RockRollDegreesMax,
                                      water.RockHeavePixelsMax,
                                      out double roll1, out double heave1);
            Assert.AreEqual(heave0 / water.RockHeavePixelsMax, roll1 / water.RockRollDegreesMax, 1e-12,
                            "roll must lead heave by a quarter cycle");
            Assert.AreEqual(roll0 / water.RockRollDegreesMax, -heave1 / water.RockHeavePixelsMax, 1e-12);
            Assert.AreNotEqual(0.0, roll0, "the sample angle was chosen so neither channel sits at a " +
                                           "zero crossing — otherwise this passes on a flat pose");
        }

        // =========================================================================================
        //  4. nothing in the sim path asks the engine what time it is
        // =========================================================================================

        /// <summary>
        /// <b>The clock lives in exactly one place.</b> The charter's rule for this drop: nothing in
        /// the sim path reads <c>Time.time</c>. <see cref="GullFlock"/> owns the only clock read in the
        /// system and hands a millisecond delta down as an argument — which is what lets a fixture step
        /// a bird a thousand times inside one frame and get the same bird twice.
        ///
        /// <para>Comments are stripped before the scan, because these files' own documentation SAYS
        /// <c>Time.time</c> and <c>UnityEngine</c> while promising not to call either.
        /// <see cref="ThePresenterIsWhereTheOneClockReadLives"/> is what proves the stripper has not
        /// simply eaten the file and painted everything green.</para>
        /// </summary>
        [Test]
        public void NothingInTheSimPathReadsAClock()
        {
            foreach (string file in new[] { "SeagullFlockMath.cs", "SeagullBehaviour.cs",
                                            "SeagullStateMachine.cs" })
            {
                string code = CodeOf(file);
                Assert.IsFalse(Regex.IsMatch(code, @"\bTime\s*\."),
                               file + " reads UnityEngine.Time — the sim path takes its time as an " +
                               "argument, so that a replay and a frame agree");
                Assert.IsFalse(Regex.IsMatch(code, @"\bRandom\b"),
                               file + " reaches for a Random — every draw in the flock is the rig's " +
                               "own mulberry32, seeded from the world seed");
                Assert.IsFalse(Regex.IsMatch(code, @"\bDateTime\b|\bStopwatch\b|\bTickCount\b"),
                               file + " reads a wall clock");
                Assert.IsFalse(Regex.IsMatch(code, @"\bUnityEngine\b"),
                               file + " pulled in UnityEngine — these three are engine-light so they " +
                               "can be stepped without a scene, and ported without one");
            }
        }

        /// <summary>
        /// The positive control for the scan above. <see cref="GullFlock"/> DOES read the clock — it is
        /// the one place allowed to — so the very same scan must still find it there. Without this, a
        /// stripper that returned an empty string would report a clean sim path forever.
        /// </summary>
        [Test]
        public void ThePresenterIsWhereTheOneClockReadLives()
        {
            string code = CodeOf("GullFlock.cs");
            Assert.IsTrue(Regex.IsMatch(code, @"\bTime\s*\.\s*unscaledDeltaTime\b"),
                          "GullFlock no longer reads the clock — either the presenter moved, or the " +
                          "comment stripper the scan above depends on is eating live code");
            Assert.IsTrue(Regex.IsMatch(code, @"\bUnityEngine\b"),
                          "the stripper ate the presenter's using directives too");
        }

        /// <summary>One Art source with its comments removed, so a scan reads what the compiler reads
        /// rather than what the author wrote about it.</summary>
        static string CodeOf(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Code", "Art", fileName);
            Assert.IsTrue(File.Exists(path), "missing source: " + path);
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            text = Regex.Replace(text, @"//[^\r\n]*", " ");
            Assert.Greater(text.Trim().Length, 200, fileName + ": nothing survived the comment strip");
            return text;
        }
    }
}
