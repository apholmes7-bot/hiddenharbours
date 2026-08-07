using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The tide-gap ladder maths</b> — the threshold at which a step aboard becomes a climb, and the
    /// DESCENT STAIR the climb is driven by.
    ///
    /// <para>The load-bearing test here is <see cref="TheStair_ReproducesTheBakedDescendTable"/>. The
    /// stair in <see cref="LadderBoardingMath"/> is a TWIN of the rig's own <c>ladderCurve()</c>, in the
    /// repo's established twin-constant idiom, and a twin is worth nothing unless something pins it: this
    /// parses the SHIPPED <c>FisherFightAnchors.json</c> and asserts the C# against every baked frame of
    /// the real sidecar. A rig revision that re-authors the climb fails the build here rather than
    /// quietly animating the fisher's feet between the rungs.</para>
    ///
    /// <para>And <see cref="TheStair_IsNotARamp_AndTheDifferenceIsVisible"/> is the one that stops a
    /// future simplification: it measures what driving the sprite at a constant rate would actually cost,
    /// in PIXELS at the locked 32 px = 1 m, so "surely a multiply would do" is answered with a number.</para>
    /// </summary>
    public class LadderBoardingMathTests
    {
        /// <summary>Relative to <c>Application.dataPath</c> (the project's <c>Assets</c>), the way the
        /// other source-scraping fixtures in this suite reach their files.</summary>
        private const string AnchorsPath = "_Project/Art/Characters/Iso/FisherFightAnchors.json";

        /// <summary>The locked art scale (ADR: 32 px = 1 m) — what turns a metre of error into a visible one.</summary>
        private const float PixelsPerMetre = 32f;

        private static readonly float Rung = LadderBoardingMath.RigRungMetres;
        private static readonly float Swing = LadderBoardingMath.RigSwingFraction;
        private static readonly float Lead = LadderBoardingMath.RigLeadFootPhase;
        private static readonly float Trail = LadderBoardingMath.RigTrailFootPhase;

        private static float Descend(double phase)
            => LadderBoardingMath.DescendMetresAt(phase, Rung, Swing, Lead, Trail);

        // ---- the sidecar, scraped ------------------------------------------------------------------

        /// <summary>
        /// The baked <c>descend</c> table for direction row 0 of <c>ladderDown</c>, straight out of the
        /// shipped sidecar.
        ///
        /// <para>Scraped rather than deserialized: the sidecar's <c>clipPins</c> is an array OF arrays
        /// (per direction, then per frame) and <c>JsonUtility</c> cannot express that shape. The scrape is
        /// safe because it is unambiguous — <c>"descend"</c> occurs in this file <b>only</b> inside the
        /// <c>ladderDown</c> state (the other clips' pins carry <c>rail</c>/<c>landing</c>/<c>tension</c>
        /// instead), which <see cref="TheSidecar_CarriesTheLadderPinsAndNothingElseCarriesDescend"/>
        /// asserts rather than assumes.</para>
        /// </summary>
        private static List<float> BakedDescendRow0(out int frames, out int milliseconds)
        {
            string text = ReadAnchors();
            int at = text.IndexOf("\"ladderDown\"", System.StringComparison.Ordinal);
            Assert.Greater(at, 0, "the sidecar carries no ladderDown state — has the 6.2 bake been run?");
            string block = text.Substring(at);

            Match head = Regex.Match(block, "\"frames\":\\s*(\\d+),\\s*\"ms\":\\s*(\\d+)");
            Assert.IsTrue(head.Success, "ladderDown declares no frames/ms");
            frames = int.Parse(head.Groups[1].Value, CultureInfo.InvariantCulture);
            milliseconds = int.Parse(head.Groups[2].Value, CultureInfo.InvariantCulture);

            var all = new List<float>();
            foreach (Match m in Regex.Matches(block, "\"descend\":\\s*(-?[0-9.eE+]+)"))
                all.Add(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));

            Assert.GreaterOrEqual(all.Count, frames,
                "the sidecar carries fewer descend samples than ladderDown has frames");
            return all.GetRange(0, frames);          // direction row 0
        }

        private static string ReadAnchors()
        {
            string path = Path.Combine(Application.dataPath, AnchorsPath);
            Assert.IsTrue(File.Exists(path), "the fisher's anchors sidecar is missing at " + path);
            return File.ReadAllText(path);
        }

        // ---- (1) THE PIN: the twin against the real bake ---------------------------------------------

        /// <summary>
        /// <b>The one that matters.</b> Every baked frame of the shipped <c>ladderDown</c> descend table,
        /// reproduced by <see cref="LadderBoardingMath.DescendMetresAt"/>.
        ///
        /// <para>Tolerance is 1 mm — a thirtieth of a pixel at 32 px = 1 m, and comfortably tighter than
        /// anything a viewer could see. It is not zero only because the sidecar rounds its numbers to four
        /// decimal places on the way out of the baker.</para>
        /// </summary>
        [Test]
        public void TheStair_ReproducesTheBakedDescendTable()
        {
            List<float> baked = BakedDescendRow0(out int frames, out _);

            for (int f = 0; f < frames; f++)
            {
                double phase = f / (double)frames;
                float twin = Descend(phase);
                Assert.AreEqual(baked[f], twin, 1e-3f,
                    $"frame {f}/{frames}: the rig baked descend {baked[f]:0.0000} m but the C# twin says " +
                    $"{twin:0.0000} m. LadderBoardingMath.DescendMetresAt has drifted from " +
                    "characterIsoRig6.js ladderCurve() — the fisher's soles will not sit on the rungs.");
            }
        }

        /// <summary>The scrape's own premise, asserted rather than assumed: <c>descend</c> is a
        /// ladder-only pin, so taking the first <c>frames</c> of them really is direction row 0 of
        /// <c>ladderDown</c>.</summary>
        [Test]
        public void TheSidecar_CarriesTheLadderPinsAndNothingElseCarriesDescend()
        {
            string text = ReadAnchors();
            int at = text.IndexOf("\"ladderDown\"", System.StringComparison.Ordinal);
            Assert.Greater(at, 0);

            Assert.AreEqual(0, Regex.Matches(text.Substring(0, at), "\"descend\"").Count,
                "'descend' appeared BEFORE the ladderDown state — the scrape in this fixture would be " +
                "reading another clip's pins. Parse the sidecar properly if the baker's shape changed.");

            StringAssert.Contains("\"clipPinSource\": \"ladderMount\"", text.Substring(at),
                "ladderDown's pins must come from the rig's own ladderMount()");
            StringAssert.Contains("\"standoff\"", text.Substring(at));
            StringAssert.Contains("\"stepZ\"", text.Substring(at));
        }

        /// <summary>The rig's published loop length and the config's <c>ClimbLoopSeconds</c> must agree, or
        /// the clip and the descent it drives run at different speeds — the picture would show a rung
        /// being taken while the body had moved a rung and a half.</summary>
        [Test]
        public void TheConfigLoopLength_MatchesTheBakedClip()
        {
            BakedDescendRow0(out int frames, out int ms);
            float baked = frames * ms / 1000f;

            Assert.AreEqual(baked, LadderBoardingMath.RigLoopSeconds, 1e-4f,
                $"the bake says {frames} f × {ms} ms = {baked:0.000} s per loop");
            Assert.AreEqual(baked, LadderBoardingSettings.Default.ClimbLoopSeconds, 1e-4f,
                "the shipped default must be the clip's own loop length — the climb is NOT rate-scaled, " +
                "so this is the number that keeps picture and position in lockstep");
        }

        /// <summary>The rig's measured standoff and rung reach the config unchanged. #454's contract is
        /// explicit that the standoff is not to be eyeballed.</summary>
        [Test]
        public void TheMeasuredRigGeometry_ReachesTheConfigDefaults()
        {
            var d = LadderBoardingSettings.Default;
            Assert.AreEqual(0.275f, LadderBoardingMath.RigStandoffMetres, 1e-6f,
                "ladderMount().standoff is 0.275 m — a measured quantity, not a nudge");
            Assert.AreEqual(LadderBoardingMath.RigStandoffMetres, d.StandoffMetres, 1e-6f);
            Assert.AreEqual(0.30f, LadderBoardingMath.RigRungMetres, 1e-6f,
                "WharfIso.FIT.ladder.rung is 0.30 m");
            Assert.AreEqual(LadderBoardingMath.RigRungMetres, d.RungMetres, 1e-6f);
            Assert.AreEqual(0.60f, LadderBoardingMath.StepZFor(d.RungMetres), 1e-6f,
                "one loop takes a step on each foot, so it descends TWO rungs — the rig's stepZ");
        }

        // ---- (2) the stair is a stair --------------------------------------------------------------

        /// <summary>
        /// <b>Why this file exists.</b> Driving the sprite at the clip's average rate instead of off the
        /// stair slides a planted sole up and down the rung it is supposed to be resting on. This measures
        /// that error against the BEST constant-rate line (the most generous version of the shortcut) and
        /// asserts it is big enough to see.
        /// </summary>
        [Test]
        public void TheStair_IsNotARamp_AndTheDifferenceIsVisible()
        {
            List<float> baked = BakedDescendRow0(out int frames, out int ms);
            float loop = frames * ms / 1000f;
            float rate = LadderBoardingMath.StepZFor(Rung) / loop;      // the "constant 0.55 m/s" shortcut

            // The best constant-rate fit is free to choose its offset, so centre the error before
            // measuring it — otherwise this would flatter the stair by counting a fixed bias.
            float sum = 0f;
            var error = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                error[f] = baked[f] - rate * (f / (float)frames) * loop;
                sum += error[f];
            }
            float mean = sum / frames;

            float worst = 0f;
            for (int f = 0; f < frames; f++) worst = Mathf.Max(worst, Mathf.Abs(error[f] - mean));

            Assert.Greater(worst * PixelsPerMetre, 2f,
                $"a constant rate tracks the stair to within {worst * PixelsPerMetre:0.0} px — if that is " +
                "really all it costs, this whole curve could be a multiply. Re-read the rig before " +
                "deleting it.");
            Assert.Greater(worst / Rung, 0.2f,
                $"the creep is {worst / Rung:0.00} of a rung — the rig warns it approaches a third");
        }

        /// <summary>The treads are DEAD FLAT while both feet are planted — that is what a stair is, and
        /// what a ramp is not. Between the two foot swings the body must not sink at all.</summary>
        [Test]
        public void TheStair_IsFlatWhileBothFeetArePlanted()
        {
            // The right foot's swing runs [0.00, 0.24); the left's [0.50, 0.74). Between them nothing moves.
            float a = Descend(0.30);
            float b = Descend(0.45);
            Assert.AreEqual(a, b, 1e-5f,
                "the body sank between foot swings — a climber drops when a LEG EXTENDS, and both of hers " +
                "are planted here");
            Assert.AreEqual(Rung, a, 1e-4f, "one foot has swung, so exactly one rung has been taken");
        }

        /// <summary>One whole loop takes one step on each foot, so it descends exactly two rungs.</summary>
        [Test]
        public void AWholeLoop_DescendsTwoRungs()
        {
            Assert.AreEqual(LadderBoardingMath.StepZFor(Rung), Descend(1.0), 1e-4f);
            Assert.AreEqual(2f * LadderBoardingMath.StepZFor(Rung), Descend(2.0), 1e-4f);
        }

        /// <summary>The stair is CONTINUOUS across the loop wrap — the rig defines its curve past both
        /// ends for exactly this reason. A foot planted across the seam must not snap back to zero.</summary>
        [Test]
        public void TheStair_IsContinuousAcrossTheWrap()
        {
            float before = Descend(0.999);
            float after = Descend(1.001);
            Assert.Less(Mathf.Abs(after - before), 0.01f,
                $"the descent jumped {(after - before):0.000} m across the wrap — a planted foot would " +
                "snap a rung at the loop seam");
            Assert.Greater(after, before, "and it must still be going DOWN across the seam");
        }

        /// <summary>Monotonic: a climber never rises mid-descent.</summary>
        [Test]
        public void TheStair_NeverGoesBackUp()
        {
            float previous = -1f;
            for (int i = 0; i <= 400; i++)
            {
                float now = Descend(i / 100.0);
                Assert.GreaterOrEqual(now, previous - 1e-5f, $"the body rose at phase {i / 100.0:0.00}");
                previous = now;
            }
        }

        // ---- (3) the threshold ----------------------------------------------------------------------

        [Test]
        public void TheGap_IsTheWharfTopLessTheBoatDeck_AndIsSigned()
        {
            Assert.AreEqual(2.45f, LadderBoardingMath.VerticalGap(3.0f, 0.55f), 1e-4f);
            Assert.AreEqual(-0.20f, LadderBoardingMath.VerticalGap(3.0f, 3.2f), 1e-4f,
                "a deck riding ABOVE the planks is a step UP, and must read as a negative gap rather " +
                "than as a big one");
        }

        [Test]
        public void NeedsLadder_OnlyPastTheClamp_AndNeverForAStepUp()
        {
            Assert.IsFalse(LadderBoardingMath.NeedsLadder(1.19f, 1.2f), "inside the clamp — step across");
            Assert.IsFalse(LadderBoardingMath.NeedsLadder(1.2f, 1.2f), "exactly at it is still a step");
            Assert.IsTrue(LadderBoardingMath.NeedsLadder(1.21f, 1.2f), "past it, the ladder is the answer");
            Assert.IsFalse(LadderBoardingMath.NeedsLadder(-4f, 1.2f),
                "a deck above the wharf must never send the fisher down a ladder");
        }

        /// <summary>
        /// The threshold TRACKS THE TIDE, which is the whole mechanic — the same wharf and the same boat
        /// are a step at high water and a climb at low. Sized on Nine Mile Creek's real numbers: a +3.0 m
        /// deck, a dory's 0.55 m sheer, and a tide running 0 → 2.2 m.
        /// </summary>
        [Test]
        public void TheSameWharf_IsAStepAtHighWaterAndAClimbAtLow()
        {
            const float wharf = 3.0f, sheer = 0.55f, draught = 0f;
            float clamp = LadderBoardingSettings.Default.BoardClampMetres;

            float high = LadderBoardingMath.VerticalGap(
                wharf, LadderBoardingMath.BoatDeckElevation(2.2f, sheer, draught));
            float low = LadderBoardingMath.VerticalGap(
                wharf, LadderBoardingMath.BoatDeckElevation(0f, sheer, draught));

            Assert.IsFalse(LadderBoardingMath.NeedsLadder(high, clamp),
                $"at high water the gap is {high:0.00} m — a step aboard, or the tide has stopped mattering");
            Assert.IsTrue(LadderBoardingMath.NeedsLadder(low, clamp),
                $"at low water the gap is {low:0.00} m — that is a climb");
        }

        /// <summary>A deck's height above datum is the water plus its own freeboard, and the draught is
        /// SUBTRACTED from the keel-relative height — the same split the mooring lines use, because it is
        /// literally the same function.</summary>
        [Test]
        public void TheBoatDeck_RidesTheTideAndSitsHerDraughtLower()
        {
            Assert.AreEqual(1.5f + 0.9f, LadderBoardingMath.BoatDeckElevation(1.5f, 0.9f, 0f), 1e-4f);
            Assert.AreEqual(1.5f + 0.5f, LadderBoardingMath.BoatDeckElevation(1.5f, 0.9f, 0.4f), 1e-4f);
            Assert.AreEqual(
                MooringLineMath.BoatCleatElevation(1.5f, MooringLineMath.CleatHeightAboveWaterline(0.9f, 0.4f)),
                LadderBoardingMath.BoatDeckElevation(1.5f, 0.9f, 0.4f), 1e-6f,
                "the ladder and the ropes must measure the same deck with the same function");
        }

        // ---- (4) how long the climb takes -----------------------------------------------------------

        /// <summary>A deeper gap takes LONGER — the climb is never rate-scaled to a fixed duration the way
        /// the boarding vault is. Real rungs are a real distance apart, and that is what keeps the tide
        /// legible on the way down.</summary>
        [Test]
        public void ADeeperGap_TakesLonger_RatherThanTheSameTimeFaster()
        {
            float loop = LadderBoardingSettings.Default.ClimbLoopSeconds;
            float shallow = LadderBoardingMath.ClimbSeconds(0.6f, Rung, loop);
            float deep = LadderBoardingMath.ClimbSeconds(2.4f, Rung, loop);

            Assert.AreEqual(loop, shallow, 1e-4f, "0.6 m is exactly one loop of two rungs");
            Assert.AreEqual(4f * loop, deep, 1e-4f, "2.4 m is four of them");
            Assert.Greater(deep, shallow);
        }

        [Test]
        public void TheLoopCount_IsTheGapOverTwoRungs()
        {
            Assert.AreEqual(1f, LadderBoardingMath.LoopsForGap(0.6f, Rung), 1e-4f);
            Assert.AreEqual(2.5f, LadderBoardingMath.LoopsForGap(1.5f, Rung), 1e-4f);
            Assert.AreEqual(0f, LadderBoardingMath.LoopsForGap(-3f, Rung), 1e-4f, "a step up is no climb");
        }

        /// <summary>Phase and descent are driven off the SAME clock, so after N whole loop-lengths of
        /// climbing the body has taken exactly N × stepZ. This is the property that keeps the picture and
        /// the position from drifting apart over a long climb.</summary>
        [Test]
        public void AfterNLoopLengths_TheBodyHasTakenExactlyNStepZ()
        {
            float loop = LadderBoardingSettings.Default.ClimbLoopSeconds;
            for (int n = 1; n <= 4; n++)
            {
                double phase = LadderBoardingMath.PhaseAt(n * loop, loop);
                Assert.AreEqual(n * LadderBoardingMath.StepZFor(Rung), Descend(phase), 1e-3f,
                    $"after {n} loop(s) of climbing");
            }
        }

        // ---- (5) seating, facing, and totality -------------------------------------------------------

        /// <summary>The climber sits the MEASURED standoff off the ladder plane, along the face's own
        /// heading. A south-facing quay wall (compass 180) puts them south of the ladder.</summary>
        [Test]
        public void TheClimber_SitsTheMeasuredStandoffOffTheLadderPlane()
        {
            Vector2 south = LadderBoardingMath.SeatOffset(180f, 0.275f);
            Assert.AreEqual(0f, south.x, 1e-4f);
            Assert.AreEqual(-0.275f, south.y, 1e-4f, "compass 180 is −Y — out over the water");

            Vector2 east = LadderBoardingMath.SeatOffset(90f, 0.275f);
            Assert.AreEqual(0.275f, east.x, 1e-4f);
            Assert.AreEqual(0f, east.y, 1e-4f);

            Assert.AreEqual(0.275f, LadderBoardingMath.SeatOffset(37f, 0.275f).magnitude, 1e-4f,
                "the standoff is a DISTANCE — it must not change with the wall's bearing");
        }

        /// <summary>A climber has their back to the water: they face the reciprocal of the ladder's
        /// face.</summary>
        [Test]
        public void TheClimber_FacesIntoTheWall()
        {
            Assert.AreEqual(0f, LadderBoardingMath.ClimberHeadingFor(180f), 1e-4f);
            Assert.AreEqual(180f, LadderBoardingMath.ClimberHeadingFor(0f), 1e-4f);
            Assert.AreEqual(270f, LadderBoardingMath.ClimberHeadingFor(90f), 1e-4f);
            Assert.AreEqual(90f, LadderBoardingMath.ClimberHeadingFor(-90f), 1e-4f,
                "and it must come back inside 0..360 however the caller expressed the facing");
        }

        /// <summary>Going up reuses the descent stair sign-flipped — the kit has no <c>ladderUp</c>, and
        /// the rung QUANTIZATION is the half of the climb that carries over unchanged.</summary>
        [Test]
        public void GoingUp_ClimbsTheSameStairTheOtherWay()
        {
            const float gap = 3f;   // far enough away not to clamp anything at this phase
            float down = LadderBoardingMath.TravelMetresAt(0.7, Rung, Swing, Lead, Trail, gap, ascending: false);
            float up = LadderBoardingMath.TravelMetresAt(0.7, Rung, Swing, Lead, Trail, gap, ascending: true);

            Assert.Less(down, 0f, "descending is a fall in height");
            Assert.Greater(up, 0f, "ascending is a rise");
            Assert.AreEqual(-down, up, 1e-5f);
            Assert.AreEqual(Descend(0.7), up, 1e-5f, "and it is the same stair, tread for tread");
        }

        /// <summary>
        /// The travel is CLAMPED to the gap. A tide reading almost never lands on a whole number of rungs,
        /// so without this the last tread carries the fisher through the deck she is climbing to — by up
        /// to most of a rung.
        /// </summary>
        [Test]
        public void TheTravel_NeverCarriesTheClimberPastTheGap()
        {
            const float gap = 0.7f;                       // one loop and a bit — an awkward tide, on purpose
            Assert.Greater(Descend(1.5), gap, "the fixture must be asking for an overshoot");

            float down = LadderBoardingMath.TravelMetresAt(1.5, Rung, Swing, Lead, Trail, gap,
                                                          ascending: false);
            Assert.AreEqual(-gap, down, 1e-5f, "the descent stops at the deck, not a rung past it");

            float up = LadderBoardingMath.TravelMetresAt(1.5, Rung, Swing, Lead, Trail, gap, ascending: true);
            Assert.AreEqual(gap, up, 1e-5f, "and the climb stops at the planks");

            Assert.AreEqual(0f, LadderBoardingMath.TravelMetresAt(2.0, Rung, Swing, Lead, Trail, -1f,
                                                                 ascending: false), 1e-6f,
                "a negative gap is a step up, and there is nothing to climb");
        }

        /// <summary>Totality: every degenerate input reads as the neutral value rather than propagating a
        /// NaN into the fisher's transform. The house guard.</summary>
        [Test]
        public void EveryDegenerateInput_ReadsAsNeutral()
        {
            Assert.AreEqual(0f, Descend(-1.0), 1e-6f, "a climb that has not started has not descended");
            Assert.AreEqual(0f, Descend(0.0), 1e-6f);
            Assert.AreEqual(0f, Descend(double.NaN), 1e-6f);
            Assert.AreEqual(0f, LadderBoardingMath.DescendMetresAt(0.5, 0f, Swing, Lead, Trail), 1e-6f,
                "a ladder with no rungs cannot be climbed");
            Assert.AreEqual(0f, LadderBoardingMath.DescendMetresAt(0.5, float.NaN, Swing, Lead, Trail), 1e-6f);

            Assert.IsFalse(float.IsNaN(LadderBoardingMath.VerticalGap(float.NaN, 1f)));
            Assert.IsFalse(float.IsNaN(LadderBoardingMath.SeatOffset(float.NaN, float.NaN).x));
            Assert.IsFalse(LadderBoardingMath.NeedsLadder(float.NaN, 1.2f));

            Assert.AreEqual(0f, LadderBoardingMath.ClimbSeconds(2f, Rung, 0f), 1e-6f);
            Assert.AreEqual(0.0, LadderBoardingMath.PhaseAt(3f, 0f), 1e-6,
                "a zero-length loop must read as phase 0, never as infinity");
            Assert.AreEqual(0f, LadderBoardingMath.LoopsForGap(1f, 0f), 1e-6f);
        }
    }
}
