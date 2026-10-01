using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
// ---- the rig's usings
using System.Reflection;
using UnityEditor;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;
// ---- end of the rig's usings

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE INTRO'S CABIN DOORWAY, WALKED THE WAY A PLAYER WALKS IT</b> — one input HELD for the whole
    /// run, never re-aimed (the owner's playtest of 2026-09-30).
    ///
    /// <para>The doorway's older tests re-aim her every frame, allow 20 s and bar a jump only at 0.5 m, so a
    /// player who presses one key and waits never appears in them. This fixture holds one key, or one stick
    /// angle, from the first tick to the last. It replays the real arrival: <see cref="ArrivalOpening"/>'s own
    /// WalkTheCabin and WalkTheDeck, and its FollowTheCabin and SeatThePlayer, in its frame order, on the
    /// cape's two shipped assets, through the door's real passage. The hull moves between frames the way the
    /// pilot moves her.</para>
    ///
    /// <para><b>The ruling</b> (the owner, 2026-09-30): D1 (a), within one clear width a key held within 30° of
    /// the doorway's axis carries her through, round the helm seat on the side her key leans to; D2 (a), the
    /// doorway is every cabin's, so S1, S4 and S6 also run on every hull with a measured door opening, on a
    /// smaller matrix (the four cardinals under way, 1/60 and 1/30 s, the key, the stick on the axis and 30°
    /// off it).</para>
    ///
    /// <para><b>The matrix.</b> Headings: the intro's three legs under way and its berth moored, plus the four
    /// cardinals, moored and under way. Ticks: 1/60, 1/30 and 1/144 s. Routes: out from the opening pose; out
    /// from 1 m inside, along each lane past the helm seat; in from 1.5 m out on deck; back through from 0.1,
    /// 0.25 and 0.5 m past the doorway, on either side. Inputs: the one of the eight keys nearest the
    /// doorway's axis on screen, and the stick on that axis and ±15° and ±30° off it.</para>
    ///
    /// <para>The bars are §5 of HANDOFF-2026-09-30-intro-cabin-doorway-and-skipper (S1 to S6). S7 and S8
    /// read drawn figures, so they live in PlayMode.</para>
    /// </summary>
    public class IntroCabinDoorwayReplayTests
    {
        // ---- §5's bars, as the charter states them
        private const float StallFraction = 0.2f;        // S1: never under 20% of her commanded step…
        private const int StallTicks = 3;                // …for 3 ticks running
        private const float PassBeyondMetres = 1f;       // S1: 1 m beyond the doorway…
        private const float PassSlack = 1.5f;            // …within 1.5 × (route length ÷ speed)…
        private const float PassGraceSeconds = 0.25f;    // …+ 0.25 s
        private const float WallConeDegrees = 10f;       // S1's one exception: a key within 10° of into a wall
        private const float LeanConeDegrees = 30f;       // D1 (a): a key within 30° of the doorway's axis,
                                                         // within one clear width of it, carries her through
        private const float SpreadSeconds = 0.2f;        // S2: the speed change spread over at least 0.2 s
        private const float JumpSlackMetres = 0.02f;     // S3: her commanded step + 0.02 m
        private const float BackSlackSeconds = 0.1f;     // S4: (distance back ÷ speed) + 0.1 s
        private const float OpeningSeconds = 0.25f;      // S5
        private const float IdleSeconds = 5f;            // S6
        private const float CrossingGapSeconds = 0.15f;  // S6

        // ---- this fixture's own readings of those bars
        private const float ReadoutWobbleMetresPerSecond = 0.05f;  // S2: a readout step no crossing explains
        private const float ContactMetres = 0.012f;      // S1: "against" a face is within 12 mm of it
        private const float MinimumRunSeconds = 4f;
        private const string HelmSeatId = "helm_seat";   // D1 (a): the helm seat just inside is never a wall
        private const float DeckRouteMetres = 1.5f;
        private const float LaneRouteMetres = 1f;
        private static readonly float[] Rates = { 60f, 30f, 144f };
        private static readonly float[] BackDistances = { 0.1f, 0.25f, 0.5f };
        private static readonly float[] StickOffsets = { 15f, -15f, 30f, -30f };
        private static readonly float[] Cardinals = { 0f, 90f, 180f, 270f };
        private static readonly int[] Dithers = { 0, 1, 2, 4, 8 };
        // D1 (a) adds routes from 1.5 m straight aft with "every direction inside D1's 30°", on the floor.
        private static readonly float[] LeanOffsets = { 0f, 10f, -10f, 20f, -20f, 29f, -29f };
        // D2 (a)'s fleet matrix: the four cardinals under way, two tick rates, the stick on the axis and 30° off.
        private static readonly float[] FleetRates = { 60f, 30f };
        private static readonly float[] FleetStickOffsets = { 30f, -30f };
        private const float OnTheFloorMetres = 0.02f;    // a route's start its floor moves further is not on it
        private const float StoppedMetres = 0.001f;      // S2: a probe step her floor moves further is stopped

        private static Hull _cape;
        private static List<Replay> _matrix;
        private static List<Hull> _fleet;
        private static List<Replay> _fleetWalks;

        private static Hull TheCape => _cape ??= LoadTheCape();

        private static List<Replay> TheMatrix
            => _matrix ??= WalkTheMatrix(TheCape, Conditions(), Rates, StickOffsets);

        /// <summary>Every other hull whose interior's door has a measured opening and which carries a deck.</summary>
        private static List<Hull> TheFleet => _fleet ??= LoadTheFleet().Where(h => h.Name != TheCape.Name).ToList();

        private static List<Replay> TheFleetsWalks
            => _fleetWalks ??= TheFleet.Where(h => h.Sole != null)
                                       .SelectMany(h => WalkTheMatrix(h, FleetConditions(), FleetRates,
                                                                      FleetStickOffsets))
                                       .ToList();

        [OneTimeSetUp]
        public void OpenTheFixture() => OpenTheRig();

        [OneTimeTearDown]
        public void CloseTheFixture()
        {
            _matrix = null;
            _cape = null;
            _fleetWalks = null;
            _fleet = null;
            CloseTheRig();
        }

        // =====================================================================================
        //  §5 · the bars
        // =====================================================================================

        [Test]
        public void S1_OneHeldKeyCarriesHerThroughTheDoorway_KeysWithinThirtyDegreesLean_D1a()
        {
            List<Replay> runs = TheMatrix;
            Assert.IsEmpty(TheCape.Skipped, "every route starts on the cape's own floors; these do not: " +
                                            string.Join(", ", TheCape.Skipped));
            Verdict("S1 under D1 (a), the lean", runs, r => r.S1, WhyS1);
        }

        [Test]
        public void S2_HerWalkSpeedChangesOverAFifthOfASecondAtTheDoorway_AndHerGaitNeverReadsStill()
            => Verdict("S2", TheMatrix.Where(r => !r.R.LeanOnly), r => r.S2, r =>
                           !float.IsNaN(r.ZeroReadoutAt)
                               ? $"her speed readout read 0 at {r.ZeroReadoutAt:0.000} s while she walked through"
                               : $"her speed readout stepped {r.WorstReadoutStep:0.00} m/s in one tick, against " +
                                 $"{r.WorstReadoutStep - r.WorstReadoutStepExcess:0.00} m/s for a change spread " +
                                 $"over {SpreadSeconds} s");

        [Test]
        public void S3_HerFigureNeverMovesFurtherThanHerStepInOneTick_TheCrossingTickIncluded()
        {
            List<Replay> runs = TheMatrix.Where(r => !r.R.LeanOnly).ToList();
            Replay far = runs.OrderByDescending(r => r.FarJumpExcess).First();
            if (far.FarJumpExcess > 0f)
                Report($"S3, more than {PassBeyondMetres} m from the doorway (not judged: her deck's own floors): " +
                       $"{runs.Count(r => r.FarJumpExcess > 0f)} runs; the worst, {Describe(far)}, moved " +
                       $"{far.FarJump:0.000} m in one tick at {V(far.FarJumpAt)}");
            Verdict("S3", runs, r => r.S3, r =>
                        $"her figure moved {r.WorstJump:0.000} m in the tick ending {r.WorstJumpAt:0.000} s" +
                        $"{(r.WorstJumpCrossing ? " (the crossing tick)" : "")}, " +
                        $"{r.WorstJumpExcess:0.000} m over her step + {JumpSlackMetres} m");
        }

        [Test]
        public void S4_TheKeyReversedTakesHerStraightBackThrough()
            => Verdict("S4", TheMatrix.Where(r => !r.R.LeanOnly), r => r.S4, WhyS4);

        [Test]
        public void S5_FromTheOpeningPoseTheHeldKeyWalksHerOutAtOnce()
            => Verdict("S5", TheMatrix.Where(r => !r.R.LeanOnly), r => r.S5, r =>
                           float.IsNaN(r.FirstCross)
                               ? $"never walked out in {r.EndT:0.00} s; held at {V(r.StallAt)} on {r.StallOn}"
                               : $"walked out at {r.FirstCross:0.000} s against {OpeningSeconds} s");

        [Test]
        public void S6_TheDoorwayNeverCrossesHerByItself_AndNeverTwiceInAFifteenthOfASecond()
        {
            // The held-input runs: a key held one way crosses her at most once, and never twice in 0.15 s.
            Verdict("S6 (held)", TheMatrix.Where(r => !r.R.LeanOnly), r => r.S6,
                    r => $"{r.Crossings} crossings, the closest two {r.MinGap:0.000} s apart");

            var reds = new List<string>();
            int judged = StandAndDither(TheCape, Conditions(), Rates, reds);
            string report = $"S6 (idle and dithered): {reds.Count} of {judged} runs red." +
                            (reds.Count > 0 ? "\n  " + string.Join("\n  ", reds.Take(16)) : "");
            Report(report);
            Assert.IsEmpty(reds, report);
        }

        // =====================================================================================
        //  D2 (a) · the same doorway on every hull that has one
        // =====================================================================================

        [Test]
        public void D2_S1_OnEveryHullWithAMeasuredDoorway_OneHeldKeyCarriesHerThrough()
        {
            ReportTheFleet();
            Verdict("S1 on the fleet", TheFleetsWalks, r => r.S1, WhyS1);
        }

        [Test]
        public void D2_S4_OnEveryHullWithAMeasuredDoorway_TheKeyReversedTakesHerStraightBackThrough()
            => Verdict("S4 on the fleet", TheFleetsWalks.Where(r => !r.R.LeanOnly), r => r.S4, WhyS4);

        [Test]
        public void D2_S6_OnEveryHullWithAMeasuredDoorway_TheDoorwayNeverCrossesHerByItself()
        {
            Verdict("S6 (held) on the fleet", TheFleetsWalks.Where(r => !r.R.LeanOnly), r => r.S6,
                    r => $"{r.Crossings} crossings, the closest two {r.MinGap:0.000} s apart");

            var reds = new List<string>();
            int judged = 0;
            foreach (Hull hull in TheFleet.Where(h => h.Sole != null))
                judged += StandAndDither(hull, FleetConditions(), FleetRates, reds);
            Assert.Greater(judged, 0, "S6 on the fleet: no run is judged, so this bar is asleep");
            string report = $"S6 (idle and dithered) on the fleet: {reds.Count} of {judged} runs red." +
                            (reds.Count > 0 ? "\n  " + string.Join("\n  ", reds.Take(16)) : "");
            Report(report);
            Assert.IsEmpty(reds, report);
        }

        /// <summary>The hulls D2 (a) guards, and any whose arrival would not open below (no room at the sill).
        /// </summary>
        private static void ReportTheFleet()
        {
            var sb = new StringBuilder($"D2 (a): {TheFleet.Count} hulls besides the cape carry a measured doorway " +
                                       "and a deck.\n");
            foreach (Hull h in TheFleet)
                sb.AppendLine(h.Sole == null
                    ? $"  {h.Name}: no room stands at the sill's height, so the arrival would stay topside"
                    : $"  {h.Name}: room '{h.Sole.Id}', out {V(h.Out)}, wall {V(h.Wall)}, threshold {V(h.Threshold)}" +
                      (h.Skipped.Count > 0 ? $"; routes off its floors: {string.Join(", ", h.Skipped)}" : ""));
            Report(sb.ToString());
            Assert.Greater(TheFleet.Count(h => h.Sole != null), 0, "D2 (a): no other hull has a doorway to walk");
        }

        /// <summary>S6's standing runs on one hull. Standing idle in the doorway for 5 s: no crossing. Dithering
        /// the key toward and away from it every 1, 2, 4 or 8 ticks: crossings only with the key reversed, never
        /// two within 0.15 s. And after a real crossing with the key released on its tick: no crossing back.
        /// Returns how many runs it judged; each red is added to <paramref name="reds"/>.</summary>
        private static int StandAndDither(Hull hull, List<Condition> conditions, float[] rates, List<string> reds)
        {
            int judged = 0;
            var starts = new List<(string name, Action<IDoorwayRig> setup)>
            {
                ("the opening pose", rig => rig.StartBelow(null)),
            };
            foreach (float d in new[] { BackDistances[0], BackDistances[1] })
            {
                float past = d;
                starts.Add(($"on deck {past} m past, spent", rig =>
                {
                    rig.StartOnDeck(hull.Threshold + hull.Out * past);
                    rig.SpendTheLatch(SinceItsCrossing(hull, rig.Point, hull.DeckSpeed));
                }));
                starts.Add(($"on the sole {past} m past, spent", rig =>
                {
                    rig.StartBelow(hull.Threshold - hull.Out * past);
                    rig.SpendTheLatch(SinceItsCrossing(hull, rig.Point, hull.CabinSpeed));
                }));
            }

            using (Quiet())
                foreach (Condition c in conditions)
                    foreach (float hz in rates)
                    {
                        float dt = 1f / hz;
                        foreach (var (name, setup) in starts)
                            foreach (int dither in Dithers)
                            {
                                using IDoorwayRig rig = NewRig(hull, c);
                                setup(rig);
                                Vector2 toward = ScreenOf(hull, rig.Below ? hull.Out : -hull.Out, c.Heading,
                                                          rig.Below).normalized;
                                var crossings = new List<float>();
                                int ticks = Mathf.RoundToInt(IdleSeconds / dt);
                                for (int i = 0; i < ticks; i++)
                                {
                                    Vector2 move = dither == 0 ? Vector2.zero
                                                 : (i / dither) % 2 == 0 ? toward : -toward;
                                    bool below = rig.Below;
                                    rig.Step(move, dt);
                                    if (rig.Below != below) crossings.Add(rig.Time);
                                }
                                judged++;
                                float gap = MinGap(crossings);
                                if (dither == 0 ? crossings.Count > 0 : gap < CrossingGapSeconds)
                                    reds.Add($"{hull.Name} {c.Name} {hz:0} Hz, {name}, " +
                                             (dither == 0 ? "idle" : $"dithered every {dither} ticks") +
                                             $": {crossings.Count} crossings, the closest two {gap:0.000} s apart");
                            }

                        foreach (Route r in Routes(hull).Where(r => r.Name == "deck_1.5" || r.Name.StartsWith("lane")))
                        {
                            Held key = Inputs(hull, r, c.Heading, StickOffsets)[0];
                            using IDoorwayRig rig = NewRig(hull, c);
                            if (r.Below) rig.StartBelow(r.Start); else rig.StartOnDeck(r.Start);
                            float crossedAt = float.NaN;
                            int after = 0;
                            while (rig.Time < 2f * IdleSeconds)
                            {
                                bool crossed = !float.IsNaN(crossedAt);
                                bool below = rig.Below;
                                rig.Step(crossed ? Vector2.zero : key.Screen, dt);
                                if (rig.Below != below)
                                {
                                    if (crossed) after++;
                                    else crossedAt = rig.Time;
                                }
                                if (!float.IsNaN(crossedAt) && rig.Time - crossedAt >= IdleSeconds) break;
                            }
                            judged++;
                            if (after > 0)
                                reds.Add($"{hull.Name} {c.Name} {hz:0} Hz, released after crossing on {r.Name}: " +
                                         $"{after} crossings back, standing still");
                        }
                    }
            return judged;
        }

        // =====================================================================================
        //  the replay
        // =====================================================================================

        private static List<Replay> WalkTheMatrix(Hull hull, List<Condition> conditions, float[] rates,
                                                  float[] stickOffsets)
        {
            var runs = new List<Replay>();
            using (Quiet())
                foreach (Condition c in conditions)
                    foreach (float hz in rates)
                        foreach (Route r in Routes(hull))
                            foreach (Held input in Inputs(hull, r, c.Heading, stickOffsets))
                                runs.Add(Walk(hull, c, 1f / hz, r, input));
            return runs;
        }

        /// <summary>One run: one route, one input held from the first tick until she is 1 m beyond the doorway,
        /// or the time is up. Everything is read the way the pose reads it: the walker's hull-local point and
        /// floor height, and its speed readout.</summary>
        private static Replay Walk(Hull hull, Condition c, float dt, Route r, Held input)
        {
            using IDoorwayRig rig = NewRig(hull, c);
            if (r.Below) rig.StartBelow(r.Opening ? (Vector2?)null : r.Start);
            else rig.StartOnDeck(r.Start);
            Vector2 th = hull.Threshold;
            float toDoor = Vector2.Distance(rig.Point, th);
            float near = r.Below ? hull.CabinSpeed : hull.DeckSpeed;
            float far = r.Below ? hull.DeckSpeed : hull.CabinSpeed;
            // The state a crossing leaves, as long after it as walking here from the wall line (where the
            // doorway crosses her) takes.
            float since = SinceItsCrossing(hull, rig.Point, near);
            if (r.Spent) rig.SpendTheLatch(since);
            var res = new Replay
            {
                H = hull, C = c, Dt = dt, R = r, I = input,
                PassBar = PassSlack * (toDoor / near + PassBeyondMetres / far) + PassGraceSeconds,
                // S4's bar, never earlier than S6 lets the doorway take her again: the settle's release, on
                // the tick after it (a turn-back started nearer the wall line than 4 cm meets it first).
                BackBar = r.Spent
                    ? Mathf.Max(
                        DistanceBack(hull, rig.Point, FloorOf(hull, input.Screen, c.Heading, r.Below), r.Below) / near
                        + BackSlackSeconds,
                        CrossingGapSeconds - since + dt)
                    : float.NaN,
            };
            float limit = Mathf.Max(MinimumRunSeconds, 2f * res.PassBar);
            float magnitude = Mathf.Min(1f, input.Screen.magnitude);
            float fastest = Mathf.Max(hull.CabinSpeed, hull.DeckSpeed);
            res.OwedByLine = r.Opening || RunsThroughTheOpening(hull, rig.Point,
                                                                FloorOf(hull, input.Screen, c.Heading, r.Below));

            Vector3 prev = rig.Drawn;
            float prevReadout = rig.Readout;
            bool prevWalking = false, prevInReach = false, prevInBand = false, prevStopped = false;
            float prevFromDoor = Vector2.Distance(rig.Point, th);
            float firstReadout = float.NaN, lastReadout = float.NaN, worstReadoutStep = 0f;
            int stall = 0;
            float lastCross = float.NaN;

            while (rig.Time < limit)
            {
                bool stepperBelow = rig.Below;
                float paceBefore = rig.Pace;
                rig.Step(input.Screen, dt);
                float t = rig.Time;
                bool below = rig.Below;
                int crossing = below == stepperBelow ? 0 : below ? +1 : -1;
                Vector3 drawn = rig.Drawn;
                float readout = rig.Readout;
                Vector2 at = drawn;
                float fromDoor = Vector2.Distance(at, th);
                bool inReach = fromDoor <= hull.Reach;
                bool inBand = BoatCabinThreshold.IsInBand(hull.Door, at);

                // Her commanded step is her walker's pace × dt × the input's size: its walk speed, or after a
                // crossing the blend S2 asks for — the pace before this tick's step or after it, whichever is
                // the faster (a blend only falls or rises toward the walk speed, and the crossing tick hands her
                // from one walker to the other). The blend runs 0.2 s or more, past the band at the deck's
                // speed. Inside the door's band (half a clear width) the two walkers' clamps meet, so S3 allows
                // the faster walker's step there too, and nowhere else.
                float pace = Mathf.Max(paceBefore, rig.Pace);
                float commanded = pace * dt * magnitude;
                float allowed = (inBand || prevInBand ? Mathf.Max(fastest, pace) : pace) * dt * magnitude;
                float moved3 = (drawn - prev).magnitude;
                float moved2 = ((Vector2)drawn - (Vector2)prev).magnitude;

                // S3, within PassBeyondMetres of the doorway: farther off, a jump is her deck's own floors
                // (the cape's foredeck stands 1.31 m over the cockpit, 2.6 m from the door) and is reported.
                float excess = moved3 - (allowed + JumpSlackMetres);
                if (fromDoor > PassBeyondMetres && prevFromDoor > PassBeyondMetres)
                {
                    if (excess > res.FarJumpExcess)
                    {
                        res.FarJumpExcess = excess;
                        res.FarJump = moved3;
                        res.FarJumpAt = at;
                    }
                }
                else if (excess > res.WorstJumpExcess)
                {
                    res.WorstJumpExcess = excess;
                    res.WorstJump = moved3;
                    res.WorstJumpAt = t;
                    res.WorstJumpCrossing = crossing != 0;
                }

                // S4, S5, S6
                if (crossing != 0)
                {
                    res.Crossings++;
                    if (float.IsNaN(res.FirstCross)) res.FirstCross = t;
                    if (!float.IsNaN(lastCross)) res.MinGap = Mathf.Min(res.MinGap, t - lastCross);
                    lastCross = t;
                }

                // S2: while she walks through (inside the doorway's reach, the key held, moving, or on the
                // crossing tick whatever she moved) her readout never reads 0, and it never steps on one tick by
                // more than a change spread over 0.2 s would. A tick her floor clamps (the key pressed into a
                // wall or a piece of furniture, not carried by the doorway) changes her speed by the room's own
                // collision, not the doorway's, and is not compared; neither is a turn-back route before its
                // crossing, which S6's 0.15 s may hold at the wall line (the report names it), nor its crossing
                // tick's step out of that hold.
                Vector2 keyNow = FloorOf(hull, input.Screen, c.Heading, below);
                bool carried = inReach && Vector2.Angle(keyNow, below ? hull.Out : -hull.Out) <= LeanConeDegrees;
                bool stopped = Stopped(hull, at, keyNow, below, commanded);
                bool clamped = crossing == 0 && !carried && (stopped || prevStopped);
                bool walking = commanded > 0f && (crossing != 0 || moved2 >= StallFraction * commanded);
                bool compared = walking && !clamped && !(r.Spent && res.Crossings == 0);
                if (inReach && walking)
                {
                    if (readout <= 1e-4f && float.IsNaN(res.ZeroReadoutAt)) res.ZeroReadoutAt = t;
                    if (float.IsNaN(firstReadout)) firstReadout = prevWalking ? prevReadout : readout;
                    lastReadout = readout;
                    if (prevInReach && prevWalking && compared)
                        worstReadoutStep = Mathf.Max(worstReadoutStep, Mathf.Abs(readout - prevReadout));
                }

                // S1
                if (res.Crossings == 0 && below == r.Below)
                {
                    Vector2 through = below ? hull.Out : -hull.Out;
                    bool pointing = Vector2.Angle(FloorOf(hull, input.Screen, c.Heading, below), through)
                                    <= LeanConeDegrees;
                    if (pointing && inReach) res.OwedInReach = true;
                }
                if (below != r.Below && fromDoor >= PassBeyondMetres && float.IsNaN(res.Pass)) res.Pass = t;
                if (float.IsNaN(res.Pass))
                {
                    stall = commanded > 0f && moved2 < StallFraction * commanded ? stall + 1 : 0;
                    if (stall > res.Stall)
                    {
                        res.Stall = stall;
                        res.StallAt = at;
                        res.StallBeforeCrossing = res.Crossings == 0;
                        if (below)
                        {
                            Vector2 key = FloorOf(hull, input.Screen, c.Heading, true);
                            Contact k = Against(hull, at, key, true);
                            res.StallOn = k == null ? "open floor" : $"{k.Face} ({k.AngleOff:0}° off)";
                            bool wall = k != null && !k.Doorway && k.Into;
                            res.StallExempt = wall && !(k.Best == HelmSeatId && hull.SeatJustInside)
                                              && !(inReach && Vector2.Angle(key, hull.Out) <= LeanConeDegrees);
                        }
                        else
                        {
                            Vector2 key = FloorOf(hull, input.Screen, c.Heading, false);
                            Contact k = Against(hull, at, key, false);
                            res.StallOn = k == null ? "open deck" : $"{k.Face} ({k.AngleOff:0}° off)";
                            res.StallExempt = k != null && !k.Doorway && k.Into
                                              && !(inReach && Vector2.Angle(key, -hull.Out) <= LeanConeDegrees);
                        }
                    }
                }

                prev = drawn;
                prevReadout = readout;
                prevWalking = compared;
                prevInReach = inReach;
                prevInBand = inBand;
                prevStopped = stopped;
                prevFromDoor = fromDoor;
                if (!float.IsNaN(res.Pass)) break;
            }

            if (res.Crossings > 0 && !float.IsNaN(firstReadout))
            {
                float bar = Mathf.Abs(lastReadout - firstReadout) * dt / SpreadSeconds + ReadoutWobbleMetresPerSecond;
                res.WorstReadoutStep = worstReadoutStep;
                res.WorstReadoutStepExcess = worstReadoutStep - bar;
            }
            res.EndT = rig.Time;
            res.EndAt = rig.Point;
            res.EndBelow = rig.Below;
            if (res.Stall < StallTicks) res.StallOn = "";
            return res;
        }

        private static float MinGap(List<float> times)
        {
            float gap = float.PositiveInfinity;
            for (int i = 1; i < times.Count; i++) gap = Mathf.Min(gap, times[i] - times[i - 1]);
            return gap;
        }

        private static void Verdict(string bar, IEnumerable<Replay> runs, Func<Replay, bool?> green,
                                    Func<Replay, string> why)
        {
            var judged = runs.Select(r => (run: r, green: green(r))).Where(x => x.green.HasValue).ToList();
            Assert.Greater(judged.Count, 0, $"{bar}: no run is judged, so this bar is asleep");
            var reds = judged.Where(x => !x.green.Value).Select(x => x.run).ToList();

            var sb = new StringBuilder($"{bar}: {reds.Count} of {judged.Count} held-input runs red.\n");
            foreach (var g in judged.GroupBy(x => (x.run.H.Name, x.run.R.Name, x.run.I.Name)))
            {
                int red = g.Count(x => !x.green.Value);
                if (red > 0)
                    sb.AppendLine($"  {g.Key.Item1} {g.Key.Item2,-14} {g.Key.Item3,-8} {red,4} red of {g.Count(),4}");
            }
            if (reds.Count > 0)
            {
                sb.AppendLine("  one of each:");
                foreach (Replay r in reds.GroupBy(r => (r.H.Name, r.R.Name, r.I.Name)).Select(g => g.First()).Take(16))
                    sb.AppendLine($"    {Describe(r)}: {why(r)}");
            }
            Report(sb.ToString());
            if (reds.Count > 0) Assert.Fail(sb.ToString());
        }

        private static string WhyS1(Replay r)
            => float.IsNaN(r.Pass)
                ? $"never 1 m beyond in {r.EndT:0.00} s (bar {r.PassBar:0.00} s); " +
                  (r.Stall >= StallTicks ? $"held {r.Stall} ticks at {V(r.StallAt)} on {r.StallOn}; " : "") +
                  $"ended {V(r.EndAt)} {(r.EndBelow ? "below" : "on deck")}"
                : $"1 m beyond at {r.Pass:0.00} s against {r.PassBar:0.00} s" +
                  (r.Stall >= StallTicks ? $", held {r.Stall} ticks at {V(r.StallAt)} on {r.StallOn}" : "");

        private static string WhyS4(Replay r)
            => float.IsNaN(r.FirstCross)
                ? $"never crossed back in {r.EndT:0.00} s; ended {V(r.EndAt)} {(r.EndBelow ? "below" : "on deck")}"
                : $"crossed back at {r.FirstCross:0.000} s against {r.BackBar:0.000} s";

        private static string Describe(Replay r)
            => $"{r.H.Name} {r.C.Name} {1f / r.Dt:0} Hz {r.R.Name} {r.I.Name} " +
               $"(screen {r.I.ScreenOff:+0;-0;0}°, floor {r.I.FloorOff:+0;-0;0}°)";

        private static string V(Vector2 v) => $"({v.x:0.000}, {v.y:0.000})";

        // =====================================================================================
        //  the matrix's axes
        // =====================================================================================

        private static List<Condition> Conditions()
        {
            var (route, berthHeading, cruise) = TheIntrosPassage();
            Assert.GreaterOrEqual(route.Length, 2, "the intro publishes no route to take her headings from");
            var list = new List<Condition>();
            for (int i = 1; i < route.Length; i++)
            {
                float h = Compass(route[i] - route[i - 1]);
                list.Add(new Condition { Name = $"leg{i}_{h:0.0}@{cruise:0}", Heading = h, BoatSpeed = cruise });
            }
            float berth = Mathf.Repeat(berthHeading, 360f);
            list.Add(new Condition { Name = $"berth_{berth:0}@0", Heading = berth, BoatSpeed = 0f });
            foreach (float h in Cardinals)
            {
                list.Add(new Condition { Name = $"{h:0}@0", Heading = h, BoatSpeed = 0f });
                list.Add(new Condition { Name = $"{h:0}@{cruise:0}", Heading = h, BoatSpeed = cruise });
            }
            return list;
        }

        /// <summary>D2 (a)'s headings: the four cardinals, under way at the intro's cruise.</summary>
        private static List<Condition> FleetConditions()
        {
            float cruise = TheIntrosPassage().cruise;
            return Cardinals.Select(h => new Condition { Name = $"{h:0}@{cruise:0}", Heading = h, BoatSpeed = cruise })
                            .ToList();
        }

        /// <summary>The routes, each started where its own floor holds her. A start its floor would move by more
        /// than <see cref="OnTheFloorMetres"/> is not on this hull (a lane past a seat that is not beside the
        /// doorway, a deck that ends short of 1.5 m out): it is left out and named (<see cref="Hull.Skipped"/>).
        /// </summary>
        private static List<Route> Routes(Hull hull)
        {
            Vector2 th = hull.Threshold, outward = hull.Out, inward = -hull.Out;
            var routes = new List<Route>
            {
                new Route { Name = "open", Below = true, Opening = true, Aim = outward },
            };
            foreach (var (name, lateral) in Lanes(hull))
            {
                // 1 m from the threshold, on the lane's centre line past the seat, aimed at the threshold.
                float offset = lateral - Vector2.Dot(th, hull.Side);
                if (Mathf.Abs(offset) >= LaneRouteMetres) { hull.Skip(name); continue; }
                Vector2 start = th + hull.Side * offset
                                + inward * Mathf.Sqrt(Mathf.Max(0f, LaneRouteMetres * LaneRouteMetres - offset * offset));
                routes.Add(new Route { Name = name, Below = true, Start = start, Aim = (th - start).normalized });
            }
            routes.Add(new Route { Name = "deck_1.5", Below = false, Start = th + outward * DeckRouteMetres, Aim = inward });
            foreach (float d in BackDistances)
            {
                // d past the wall line, where the doorway crosses her, on its axis. Where furniture stands
                // there she starts at its face nearest the door, as far past as walking on along the axis would
                // have taken her: 0.5 m in from the cape's door is inside the helm seat. The bar is timed from
                // where she actually starts.
                routes.Add(new Route { Name = $"back_deck_{d:0.##}", Below = false, Spent = true,
                                       Start = hull.PastTheLine(d, false), Aim = inward });
                routes.Add(new Route { Name = $"back_sole_{d:0.##}", Below = true, Spent = true,
                                       Start = hull.PastTheLine(d, true), Aim = outward });
            }
            routes.Add(new Route { Name = "lean_deck_1.5", Below = false, LeanOnly = true,
                                   Start = th + outward * DeckRouteMetres, Aim = inward });
            return routes.Where(r => r.Opening || hull.Holds(r) || hull.Skip(r.Name)).ToList();
        }

        /// <summary>The lanes past the helm seat: the centre of the clear floor between the seat and the nearest
        /// blocking box beside it, on each side, read from the sole's own measurements.</summary>
        private static IEnumerable<(string name, float lateral)> Lanes(Hull hull)
        {
            BoatInteriorObstruction seat = hull.Sole.Obstructions?.FirstOrDefault(o => o != null && o.Id == HelmSeatId);
            if (seat == null) yield break;
            (float min, float max) Span(Vector2[] poly, Vector2 axis)
                => (poly.Min(p => Vector2.Dot(p, axis)), poly.Max(p => Vector2.Dot(p, axis)));
            Vector2 inward = -hull.Out;
            var (seatMin, seatMax) = Span(seat.Footprint, hull.Side);
            var (seatNear, seatFar) = Span(seat.Footprint, inward);
            var beside = hull.Sole.Obstructions
                .Where(o => o != null && o != seat && BoatCabinWalkMath.Blocks(o))
                .Select(o => (lat: Span(o.Footprint, hull.Side), dep: Span(o.Footprint, inward)))
                .Where(o => o.dep.max > seatNear && o.dep.min < seatFar)
                .ToList();
            var (soleMin, soleMax) = Span(hull.Sole.Outline, hull.Side);
            float stbd = beside.Where(o => o.lat.min >= seatMax).Select(o => o.lat.min).DefaultIfEmpty(soleMax).Min();
            float port = beside.Where(o => o.lat.max <= seatMin).Select(o => o.lat.max).DefaultIfEmpty(soleMin).Max();
            yield return ("lane_stbd", 0.5f * (seatMax + stbd));
            yield return ("lane_port", 0.5f * (seatMin + port));
        }

        private static List<Held> Inputs(Hull hull, Route r, float heading, float[] stickOffsets)
        {
            Vector2 aim = ScreenOf(hull, r.Aim, heading, r.Below).normalized;
            var list = new List<(string name, Vector2 screen)>();
            if (r.LeanOnly)
            {
                foreach (float off in LeanOffsets)
                    list.Add(($"floor{off:+0;-0;0}", ScreenOf(hull, Rotate(r.Aim, off), heading, r.Below).normalized));
            }
            else
            {
                float keyDegrees = Mathf.Round(Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg / 45f) * 45f;
                list.Add(("key", Rotate(Vector2.right, keyDegrees)));
                list.Add(("stick0", aim));
                foreach (float off in stickOffsets) list.Add(($"stick{off:+0;-0}", Rotate(aim, off)));
            }
            return list.Select(p => new Held
            {
                Name = p.name,
                Screen = p.screen,
                ScreenOff = Vector2.SignedAngle(aim, p.screen),
                FloorOff = Vector2.SignedAngle(r.Aim, FloorOf(hull, p.screen, heading, r.Below)),
            }).ToList();
        }

        // =====================================================================================
        //  geometry
        // =====================================================================================

        /// <summary>A hull-local direction as the screen draws it, on the floor she stands on.</summary>
        private static Vector2 ScreenOf(Hull hull, Vector2 hullDirection, float heading, bool onSole)
            => onSole
                ? BoatCabinWalkMath.ToWorldOffset(hullDirection, 0f, heading, hull.Elevation, hull.Ccw)
                  - BoatCabinWalkMath.ToWorldOffset(Vector2.zero, 0f, heading, hull.Elevation, hull.Ccw)
                : DeckAreaMath.DeckToWorld(hullDirection, 0f, heading, hull.Elevation)
                  - DeckAreaMath.DeckToWorld(Vector2.zero, 0f, heading, hull.Elevation);

        /// <summary>A held screen input as the walker on that floor reads it: its hull-local direction.</summary>
        private static Vector2 FloorOf(Hull hull, Vector2 screen, float heading, bool onSole)
            => DeckAreaMath.WorldDirectionToDeck(
                   screen, onSole ? BoatCabinWalkMath.TurntableHeading(heading, hull.Ccw) : heading,
                   hull.Elevation).normalized;

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private static float Compass(Vector2 d)
        {
            float deg = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            return deg < 0f ? deg + 360f : deg;
        }

        private static Vector2 Bow(float compass)
            => new Vector2(Mathf.Sin(compass * Mathf.Deg2Rad), Mathf.Cos(compass * Mathf.Deg2Rad));

        /// <summary>How long ago a walker standing at <paramref name="at"/> would have crossed, walking away
        /// from the doorway at <paramref name="speed"/>: her distance from the wall line, where it crosses her.
        /// </summary>
        private static float SinceItsCrossing(Hull hull, Vector2 at, float speed)
            => Mathf.Abs(Vector2.Dot(at - hull.Wall, hull.Out)) / Mathf.Max(1e-4f, speed);

        /// <summary>How far <paramref name="p"/> stands from <paramref name="poly"/>: 0 inside it, else the
        /// nearest of its edges.</summary>
        private static float DistanceToOutline(Vector2[] poly, Vector2 p)
        {
            if (poly == null || poly.Length < 3) return float.PositiveInfinity;
            if (DeckAreaMath.Contains(poly, p)) return 0f;
            float best = float.PositiveInfinity;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], ab = poly[(i + 1) % poly.Length] - a;
                float t = ab.sqrMagnitude <= 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>S4's "distance back": how far she walks from <paramref name="at"/> to where the doorway
        /// crosses her, its wall line. A key D1 (a) carries (within the reach, within 30° of the way through)
        /// walks her straight at the opening's centre on that line; any other key walks its own straight line,
        /// longer than the axis by the angle it holds off it.</summary>
        private static float DistanceBack(Hull hull, Vector2 at, Vector2 key, bool below)
        {
            Vector2 through = below ? hull.Out : -hull.Out;
            if (Vector2.Distance(at, hull.Threshold) <= hull.Reach && Vector2.Angle(key, through) <= LeanConeDegrees)
                return Vector2.Distance(at, hull.Wall);
            float toLine = Mathf.Abs(Vector2.Dot(at - hull.Wall, hull.Out));
            float along = key.sqrMagnitude > 1e-8f ? Vector2.Dot(key.normalized, through) : 0f;
            return along > 1e-4f ? toLine / along : toLine;
        }

        /// <summary>Does <paramref name="key"/>'s straight line from <paramref name="from"/> run through the
        /// opening — cross the doorway's wall line inside its band, going the way through from her side? The
        /// half of S1's population D1 (a) does not bend is owed the crossing only then.</summary>
        private static bool RunsThroughTheOpening(Hull hull, Vector2 from, Vector2 key)
        {
            float along = Vector2.Dot(key, hull.Out);
            float past = Vector2.Dot(from - hull.Wall, hull.Out);
            if (Mathf.Abs(along) <= 1e-6f || past * along >= 0f) return false;   // parallel, or walking away
            return BoatCabinThreshold.IsInBand(hull.Door, from + key * (-past / along));
        }

        /// <summary>Does her floor stop a step of <paramref name="probe"/> metres along the key from
        /// <paramref name="p"/>? On the sole, its clamp moves the step's end; on deck, the deck's clamp does, or
        /// the end stands in the room. A step through the wall line inside the band is the doorway's, and is
        /// never a stop.</summary>
        private static bool Stopped(Hull hull, Vector2 p, Vector2 key, bool below, float probe)
        {
            if (key.sqrMagnitude <= 1e-8f || probe <= 0f) return false;
            Vector2 to = p + key.normalized * probe;
            float past = Vector2.Dot(to - hull.Wall, hull.Out);
            if (BoatCabinThreshold.IsInBand(hull.Door, to) && (below ? past >= 0f : past <= 0f)) return false;
            if (below) return Vector2.Distance(BoatCabinWalkMath.ClampToSole(hull.Sole, to, p), to) > StoppedMetres;
            int hint = -1;
            return Vector2.Distance(hull.Deck.ClampToWalkable(to, ref hint, out _), to) > StoppedMetres
                   || DeckAreaMath.Contains(hull.Sole.Outline, to);
        }

        /// <summary>What she stands against, within <see cref="ContactMetres"/>. Below: every edge of the sole's
        /// outline, and of each blocking box on it. On deck: every edge of the deck's walkable areas, and the
        /// house's walls (the room's outline, which the deck walk keeps her out of). Each face gets a normal
        /// pointing INTO it. A room edge inside the door's band is the doorway: an open door, never a wall.
        /// The key goes "into" a face when it is within 10° of that face's normal, or points into a corner
        /// between two faces she touches.</summary>
        private static Contact Against(Hull hull, Vector2 p, Vector2 key, bool below)
        {
            var hits = new List<(string face, Vector2 normal, bool doorway)>();
            Collect(hull, hull.Sole.Outline, p, null, below, hits);
            if (below && hull.Sole.Obstructions != null)
                foreach (BoatInteriorObstruction o in hull.Sole.Obstructions)
                    if (o != null && BoatCabinWalkMath.Blocks(o)) Collect(hull, o.Footprint, p, o.Id, false, hits);
            if (!below && hull.Deck.Areas != null)
                foreach (DeckArea a in hull.Deck.Areas)
                    if (a != null && a.Kind == DeckAreaKind.Deck && a.IsUsable())
                        Collect(hull, a.Outline, p, $"the edge of {a.Id}", true, hits);
            if (hits.Count == 0) return null;

            float best = float.PositiveInfinity;
            string bestFace = hits[0].face;
            foreach (var h in hits)
            {
                float a = Vector2.Angle(key, h.normal);
                if (a < best) { best = a; bestFace = h.face; }
            }
            bool into = best <= WallConeDegrees;
            for (int i = 0; i < hits.Count && !into; i++)
                for (int j = i + 1; j < hits.Count && !into; j++)
                {
                    float span = Vector2.Angle(hits[i].normal, hits[j].normal);
                    if (span < 1f) continue;
                    if (Vector2.Angle(key, hits[i].normal) + Vector2.Angle(key, hits[j].normal) <= span + 0.5f)
                        into = true;
                }
            var names = hits.Select(h => h.face).Distinct().ToList();
            return new Contact
            {
                Face = names.Count > 1 ? "the corner of " + string.Join(" and ", names) : names[0],
                Best = bestFace,
                AngleOff = best,
                Into = into,
                Doorway = hits.Any(h => h.doorway),
            };
        }

        /// <summary>The faces of <paramref name="poly"/> within <see cref="ContactMetres"/> of p, each with its
        /// normal INTO the face, from the polygon's own winding — a floor she walks on
        /// (<paramref name="walkable"/>) is left through its edges, a footprint is entered — because a clamp
        /// can stand her exactly on an edge, where which side of it she is on is not a question.</summary>
        private static void Collect(Hull hull, Vector2[] poly, Vector2 p, string id, bool walkable,
                                    List<(string, Vector2, bool)> hits)
        {
            if (poly == null) return;
            float winding = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 v = poly[i], w = poly[(i + 1) % poly.Length];
                winding += v.x * w.y - w.x * v.y;
            }
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length], ab = b - a;
                if (ab.sqrMagnitude <= 1e-12f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                Vector2 q = a + ab * t;
                if ((q - p).magnitude > ContactMetres) continue;
                Vector2 outward = new Vector2(ab.y, -ab.x).normalized * (winding > 0f ? 1f : -1f);
                Vector2 n = walkable ? outward : -outward;
                string face = id;
                bool doorway = false;
                if (id == null)
                {
                    doorway = BoatCabinThreshold.IsInBand(hull.Door, q);
                    face = doorway ? "the doorway"
                         : Mathf.Abs(n.y) >= Mathf.Abs(n.x) ? (n.y < 0f ? "the aft bulkhead" : "the forward bulkhead")
                         : n.x > 0f ? "the starboard side" : "the port side";
                }
                if (!hits.Any(h => h.Item1 == face && Vector2.Angle(h.Item2, n) < 1f)) hits.Add((face, n, doorway));
            }
        }

        // =====================================================================================
        //  types
        // =====================================================================================

        /// <summary>The hull under test: its two assets, its drawing's camera, and the arrival's two walk
        /// speeds.</summary>
        private sealed class Hull
        {
            public string Name;
            public BoatInteriorDef Interior;
            public BoatDeckDef Deck;
            public BoatInteriorLevel Sole;
            public BoatInteriorDoor Door;
            public Vector2 Threshold;
            public float Elevation;
            public bool Ccw;
            public float CabinSpeed;
            public float DeckSpeed;

            /// <summary>The way out through the door, hull-local: the outward normal of the edge of her room's
            /// outline nearest the threshold — the wall the doorway is cut in.</summary>
            public Vector2 Out;

            /// <summary>That edge's nearest point to the threshold.</summary>
            public Vector2 Wall;

            /// <summary>The routes left out because their start is not on this hull's floors.</summary>
            public readonly List<string> Skipped = new List<string>();

            /// <summary>Across the doorway: starboard for a door in the aft face.</summary>
            public Vector2 Side => new Vector2(-Out.y, Out.x);

            /// <summary>One clear width: the door's reach under D1 (a).</summary>
            public float Reach => Door.ClearWidthMeters;

            /// <summary>Does the helm seat come within the reach of the threshold — "just inside", where D1 (a)
            /// takes her round it? On the cape it stands 0.37 m in; on the Northumberland lobster boats, 0.89 m in
            /// with a 0.53 m reach.</summary>
            public bool SeatJustInside
            {
                get
                {
                    if (Sole.Obstructions == null) return false;
                    foreach (BoatInteriorObstruction o in Sole.Obstructions)
                        if (o != null && o.Id == HelmSeatId && BoatCabinWalkMath.Blocks(o)
                            && DistanceToOutline(o.Footprint, Threshold) <= Reach)
                            return true;
                    return false;
                }
            }

            /// <summary>Where S4's turn-back starts: <paramref name="d"/> past the wall line on the doorway's
            /// axis, on the deck (out) or the sole (in); where her floor does not stand there, the nearest point
            /// of the axis back toward the door that it does.</summary>
            public Vector2 PastTheLine(float d, bool below)
            {
                Vector2 through = below ? -Out : Out;
                for (float s = d; s >= 0f; s -= OnTheFloorMetres * 0.5f)
                {
                    Vector2 p = Wall + through * s;
                    Vector2 q = OnItsFloor(p, below);
                    if (Vector2.Distance(q, p) <= OnTheFloorMetres * 0.5f) return q;
                }
                return OnItsFloor(Wall + through * d, below);
            }

            public void Measure()
            {
                Threshold = BoatCabinThreshold.PointOf(Door);
                Vector2[] outline = Sole.Outline;
                float winding = 0f;
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 p = outline[i], q = outline[(i + 1) % outline.Length];
                    winding += p.x * q.y - q.x * p.y;
                }
                float best = float.PositiveInfinity;
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 a = outline[i], ab = outline[(i + 1) % outline.Length] - a;
                    if (ab.sqrMagnitude <= 1e-12f) continue;
                    Vector2 q = a + ab * Mathf.Clamp01(Vector2.Dot(Threshold - a, ab) / ab.sqrMagnitude);
                    float d = (Threshold - q).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    Wall = q;
                    Vector2 right = new Vector2(ab.y, -ab.x).normalized;   // out of an anticlockwise outline
                    Out = winding > 0f ? right : -right;
                }
            }

            /// <summary>Does the route's start stand on its own floor, within <see cref="OnTheFloorMetres"/>?</summary>
            public bool Holds(Route r)
            {
                if (r.Below) return Vector2.Distance(BoatCabinWalkMath.ClampToSole(Sole, r.Start, r.Start), r.Start)
                                    <= OnTheFloorMetres;
                int hint = -1;
                return Vector2.Distance(Deck.ClampToWalkable(r.Start, ref hint, out _), r.Start) <= OnTheFloorMetres;
            }

            /// <summary>The nearest point of the sole (<paramref name="below"/>) or of the deck she may stand on.
            /// </summary>
            public Vector2 OnItsFloor(Vector2 p, bool below)
            {
                if (below) return BoatCabinWalkMath.ClampToSole(Sole, p, p);
                int hint = -1;
                return Deck.ClampToWalkable(p, ref hint, out _);
            }

            /// <summary>Names a route left out; false, so it reads as a filter.</summary>
            public bool Skip(string route)
            {
                if (!Skipped.Contains(route)) Skipped.Add(route);
                return false;
            }
        }

        private struct Condition
        {
            public string Name;
            public float Heading;
            public float BoatSpeed;
        }

        private sealed class Route
        {
            public string Name;
            public bool Below;       // she starts on the sole
            public bool Opening;     // from the opening pose, the latch as the arrival leaves it
            public bool Spent;       // the latch as a crossing leaves it (the turn-back routes)
            public bool LeanOnly;    // D1 (a)'s extra routes
            public Vector2 Start;    // hull-local
            public Vector2 Aim;      // hull-local unit direction: the way through
        }

        private struct Held
        {
            public string Name;
            public Vector2 Screen;   // the held move, screen axes
            public float ScreenOff;  // degrees off the route's aim, on screen
            public float FloorOff;   // degrees off the route's aim, on the floor
        }

        private sealed class Contact
        {
            public string Face;
            public string Best;
            public float AngleOff;
            public bool Into;
            public bool Doorway;
        }

        private sealed class Replay
        {
            public Hull H;
            public Condition C;
            public float Dt;
            public Route R;
            public Held I;
            public int Crossings;
            public float FirstCross = float.NaN;
            public float MinGap = float.PositiveInfinity;
            public float Pass = float.NaN;
            public float PassBar;
            public float BackBar = float.NaN;
            public int Stall;
            public Vector2 StallAt;
            public string StallOn = "";
            public bool StallExempt;
            public bool OwedInReach;
            public bool StallBeforeCrossing;
            public bool OwedByLine;
            public float FarJumpExcess = float.NegativeInfinity;
            public float FarJump;
            public Vector2 FarJumpAt;
            public float WorstJumpExcess = float.NegativeInfinity;
            public float WorstJump;
            public float WorstJumpAt = float.NaN;
            public bool WorstJumpCrossing;
            public float ZeroReadoutAt = float.NaN;
            public float WorstReadoutStep;
            public float WorstReadoutStepExcess = float.NegativeInfinity;
            public float EndT;
            public Vector2 EndAt;
            public bool EndBelow;

            private bool PassedInTime => !float.IsNaN(Pass) && Pass <= PassBar && Stall < StallTicks;

            /// <summary>Is the crossing owed at all? Under D1 (a) a key within 30° of the axis, within one clear
            /// width, is carried through (<see cref="OwedInReach"/>); farther off nothing is bent, so a key is owed
            /// it only when its own straight line runs through the opening (<see cref="OwedByLine"/>). The stick
            /// 30° off on screen is up to 44° off on the floor: a key that is neither walks her past the doorway,
            /// which is not the doorway's to judge.</summary>
            public bool Owed => OwedByLine || OwedInReach;

            /// <summary>S1: she passes 1 m beyond in time, never held 3 ticks. The one exception is a key held
            /// into a wall she can see for the whole of her stop. Under D1 (a) the seat just inside is not a wall
            /// (it carries her round it; a seat beyond the reach is furniture like any other, "farther off,
            /// nothing is bent"), and a key she holds within 30° of the way through, within one clear width of
            /// the door, is owed the crossing, so a stop short of it is never exempt then; once through, a stop
            /// is any other stop. A stop is judged whether or not the crossing is owed. The
            /// turn-back routes are S4's: S6 keeps a doorway from taking her twice in 0.15 s, so one she crossed a
            /// moment ago may hold her at its wall line, and S4's own bar is the one that times her back.</summary>
            public bool? S1
                => R.Spent ? (bool?)null
                 : Stall >= StallTicks ? StallExempt && !(OwedInReach && StallBeforeCrossing)
                 : Owed ? PassedInTime : (bool?)null;

            public bool? S2 => Crossings == 0 ? (bool?)null
                             : float.IsNaN(ZeroReadoutAt) && WorstReadoutStepExcess <= 0f;

            public bool? S3 => WorstJumpExcess <= 0f;

            public bool? S4 => R.Spent && Owed ? !float.IsNaN(FirstCross) && FirstCross <= BackBar : (bool?)null;

            public bool? S5 => R.Opening ? !float.IsNaN(FirstCross) && FirstCross <= OpeningSeconds : (bool?)null;

            public bool? S6 => MinGap >= CrossingGapSeconds && Crossings <= 1;
        }

        /// <summary>The arrival, one frame at a time, seen the way her pose sees it.</summary>
        private interface IDoorwayRig : IDisposable
        {
            bool Below { get; }
            Vector2 Point { get; }
            Vector3 Drawn { get; }
            float Readout { get; }
            /// <summary>The walker's pace, m/s: what her key's full length walks her at this tick — its walk
            /// speed, or after a crossing the blend toward it.</summary>
            float Pace { get; }
            float Time { get; }
            void StartBelow(Vector2? at);
            void StartOnDeck(Vector2 at);
            /// <summary>The state a crossing leaves, <paramref name="secondsSince"/> after it.</summary>
            void SpendTheLatch(float secondsSince);
            void Step(Vector2 screenMove, float deltaSeconds);
        }

        // ===== the rig: the real arrival, on the cape's two shipped assets =====

        private const string CapeVisualPath = "Assets/_Project/Data/Boats/Visuals/CapeIslanderIso.asset";
        private const string VisualsFolder = "Assets/_Project/Data/Boats/Visuals";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Texture2D _cellTexture;
        private static Sprite[] _cells;

        private static void OpenTheRig()
        {
            _cellTexture = new Texture2D(4, 4);
            _cells = new Sprite[8];
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = Sprite.Create(_cellTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                _cells[i].name = $"cell_{i}";
            }
        }

        private static void CloseTheRig()
        {
            if (_cells != null)
                foreach (Sprite s in _cells)
                    if (s != null) Object.DestroyImmediate(s);
            if (_cellTexture != null) Object.DestroyImmediate(_cellTexture);
            _cells = null;
            _cellTexture = null;
        }

        private static IDisposable Quiet() => new LogFilter(LogType.Warning);

        private static void Report(string text) => Debug.Log("[intro-doorway] " + text);

        private static (Vector2[] route, float berthHeading, float cruise) TheIntrosPassage()
            => (StPetersArrivalOpening.Route(), StPetersArrivalOpening.BerthHeadingDegrees(),
                ArrivalPilot.Settings.Default.CruiseSpeedMetresPerSecond);

        private static Hull LoadTheCape()
        {
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(CapeVisualPath);
            Assert.IsNotNull(visual, $"the cape's visual def is missing at {CapeVisualPath}");
            Assert.IsNotNull(visual.Interior, "the cape carries no measured interior");
            Assert.IsNotNull(visual.Deck, "the cape carries no measured deck");
            Assert.IsTrue(BoatCabinThreshold.HasBand(visual.Interior.Door), "the cape's door has no measured opening");
            Assert.AreNotEqual(DeckAreaMath.PlanViewElevationDegrees, BoatInteriorInstaller.BakeElevationDegrees(visual),
                               "the cape is drawn by a baked camera; a plan-view elevation would replay a floor " +
                               "the player never walks");
            Hull hull = HullOf(visual);
            Assert.IsNotNull(hull.Sole, "her door's sill resolves to no drawable level");
            return hull;
        }

        /// <summary>D2 (a): every visual def whose interior's door has a measured opening and which carries a
        /// deck, by name.</summary>
        private static List<Hull> LoadTheFleet()
            => AssetDatabase.FindAssets("t:BoatVisualDef", new[] { VisualsFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<BoatVisualDef>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(v => v != null && v.Interior != null && v.Deck != null && BoatCabinThreshold.HasBand(v.Interior.Door))
                .Select(HullOf)
                .OrderBy(h => h.Name, StringComparer.Ordinal)
                .ToList();

        private static Hull HullOf(BoatVisualDef visual)
        {
            var hull = new Hull
            {
                Name = visual.name,
                Interior = visual.Interior,
                Deck = visual.Deck,
                Door = visual.Interior.Door,
                Elevation = BoatInteriorInstaller.BakeElevationDegrees(visual),
                Ccw = BoatInteriorInstaller.ExteriorAzimuthCounterClockwise(visual),
            };
            using (NewRig(hull, new Condition { Name = "probe" })) { }   // reads her room and the walk speeds
            if (hull.Sole != null) hull.Measure();
            return hull;
        }

        private static IDoorwayRig NewRig(Hull hull, Condition c) => new ArrivalRig(hull, c);

        /// <summary>The arrival as the intro builds it below decks (GoBelowDecks), minus what the walk never
        /// reads: a hull root with the cape's interior, door, deck areas and a drawn heading; a passenger
        /// transform; an <see cref="ArrivalOpening"/> holding the two walkers its own code builds.</summary>
        private sealed class ArrivalRig : IDoorwayRig
        {
            private static readonly MethodInfo FollowTheCabin = typeof(ArrivalOpening).GetMethod("FollowTheCabin", Members);
            private static readonly MethodInfo SeatThePlayer = typeof(ArrivalOpening).GetMethod("SeatThePlayer", Members);
            private static readonly MethodInfo SetPresenter =
                typeof(BoatHullPresenterHost).GetProperty(nameof(BoatHullPresenterHost.Presenter))?.GetSetMethod(true);
            private static readonly ConstructorInfo NewCabinWalk = typeof(ArrivalCabinWalk).GetConstructor(
                Members, null,
                new[]
                {
                    typeof(BoatInterior), typeof(BoatCabinDoor), typeof(BoatCutaway), typeof(int),
                    typeof(BoatInteriorLevel), typeof(float), typeof(bool), typeof(float),
                }, null);

            private readonly Hull _hull;
            private readonly Condition _c;
            private readonly GameObject _root;
            private readonly GameObject _passenger;
            private readonly GameObject _arrivalObject;
            private readonly BoatCabinDoor _door;
            private readonly ArrivalOpening _arrival;
            private readonly ArrivalCabinWalk _cabin;
            private readonly ArrivalDeckWalk _deckWalk;

            public ArrivalRig(Hull hull, Condition c)
            {
                Assert.IsNotNull(FollowTheCabin, "ArrivalOpening.FollowTheCabin is gone: re-point this replay");
                Assert.IsNotNull(SeatThePlayer, "ArrivalOpening.SeatThePlayer is gone: re-point this replay");
                Assert.IsNotNull(SetPresenter, "BoatHullPresenterHost.Presenter has no setter to stand a hull in");
                Assert.IsNotNull(NewCabinWalk, "ArrivalCabinWalk's constructor moved: re-point this replay");
                _hull = hull;
                _c = c;

                _root = new GameObject("IntroDoorwayReplayHull");
                _root.transform.rotation = Quaternion.Euler(0f, 0f, -c.Heading);
                var exterior = Child<SpriteRenderer>("House");
                var room = Child<SpriteRenderer>("Room");
                var interior = _root.AddComponent<BoatInterior>();
                interior.Configure(hull.Interior, exterior, room, null, room.transform, _root.transform,
                                   _cells, _cells.Length, hull.Ccw, 0f, 0f, 0f, 0f);
                _door = Child<BoatCabinDoor>("CabinDoor");
                _door.Configure(interior, "fixture.intro_doorway.cabin_door", -1, 1.2f,
                                "Open the door", "Close the door");
                _door.SetOpen(true);                                   // GoBelowDecks: _aftDoorOpenOnArrival
                BoatDeckAreas.Write(_root, hull.Deck);
                SetPresenter.Invoke(_root.AddComponent<BoatHullPresenterHost>(),
                                    new object[] { new DrawnHull(c.Heading, hull.Elevation, hull.Ccw) });

                _passenger = new GameObject("IntroDoorwayReplayPassenger");
                _arrivalObject = new GameObject("IntroDoorwayReplayArrival");
                _arrival = _arrivalObject.AddComponent<ArrivalOpening>();
                hull.CabinSpeed = (float)Get(_arrival, "_cabinWalkSpeed");
                hull.DeckSpeed = (float)Get(_arrival, "_deckWalkSpeed");

                // ArrivalCabinWalk.TryOpen: the room the doorway is cut into, else the level its sill resolves to.
                int level = -1;
                if (level < 0) level = interior.LevelIndexAtHeight(hull.Door.ThresholdPoint.z);
                if (level < 0 || !interior.IsUsableLevel(level)) return;
                hull.Sole ??= hull.Interior.Levels[level];
                _cabin = (ArrivalCabinWalk)NewCabinWalk.Invoke(new object[]
                {
                    interior, _door, null, level, hull.Interior.Levels[level], hull.Elevation, hull.Ccw,
                    hull.CabinSpeed,
                });
                _deckWalk = new ArrivalDeckWalk(_root, hull.DeckSpeed);
                Set(_arrival, "_boatRoot", _root.transform);
                Set(_arrival, "_player", _passenger.transform);
                Set(_arrival, "_cabin", _cabin);
                Set(_arrival, "_deckWalk", _deckWalk);
            }

            public bool Below => _arrival.IsBelowDecks;

            public Vector2 Point => Below ? _cabin.LocalPosition : _deckWalk.LocalPosition;

            public Vector3 Drawn => Below
                ? new Vector3(_cabin.LocalPosition.x, _cabin.LocalPosition.y, _cabin.SoleHeightMetres)
                : new Vector3(_deckWalk.LocalPosition.x, _deckWalk.LocalPosition.y, _deckWalk.HeightMetres);

            public float Readout => Below ? _cabin.SpeedMetresPerSecond : _deckWalk.SpeedMetresPerSecond;

            public float Pace => Below ? _hull.CabinSpeed : _hull.DeckSpeed;

            public float Time { get; private set; }

            public void StartBelow(Vector2? at)
            {
                Assert.IsTrue(_cabin.GoBelow(), "her cabin refused the entry");
                if (at.HasValue) Set(_cabin, "_local", BoatCabinWalkMath.ClampToSole(_hull.Sole, at.Value, at.Value));
                Set(_arrival, "_wasBelow", true);
                SeatThePlayer.Invoke(_arrival, null);
            }

            public void StartOnDeck(Vector2 at)
            {
                _deckWalk.SeedFromDeckPoint(at, _c.Heading, _c.Heading);
                Assert.IsTrue(_deckWalk.IsSeated, $"the deck would not seat her at {at}");
                Set(_arrival, "_wasBelow", false);
                SeatThePlayer.Invoke(_arrival, null);
            }

            public void SpendTheLatch(float secondsSince)
            {
                Set(_door, "_passageArmed", false);
                Set(_door, "_passageSeeded", true);
            }

            /// <summary>One frame, in the arrival's order: the hull moves (FixedUpdate); FollowTheCabin, one
            /// read of the held input into WalkTheCabin or WalkTheDeck, FollowTheCabin again, and the pose reads
            /// (Update); FollowTheCabin and SeatThePlayer (LateUpdate).</summary>
            public void Step(Vector2 screenMove, float deltaSeconds)
            {
                Time += deltaSeconds;
                _root.transform.position += (Vector3)(Bow(_c.Heading) * (_c.BoatSpeed * deltaSeconds));
                FollowTheCabin.Invoke(_arrival, null);
                if (_arrival.IsBelowDecks) _arrival.WalkTheCabin(screenMove, deltaSeconds);
                else _arrival.WalkTheDeck(screenMove, deltaSeconds);
                FollowTheCabin.Invoke(_arrival, null);
                FollowTheCabin.Invoke(_arrival, null);
                SeatThePlayer.Invoke(_arrival, null);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_arrivalObject);
                Object.DestroyImmediate(_passenger);
                Object.DestroyImmediate(_root);
            }

            private T Child<T>(string name) where T : Component
            {
                var child = new GameObject(name).AddComponent<T>();
                child.transform.SetParent(_root.transform, false);
                return child;
            }

            private static object Get(object target, string field)
                => target.GetType().GetField(field, Members)?.GetValue(target)
                   ?? throw new MissingFieldException(target.GetType().Name, field);

            private static void Set(object target, string field, object value)
            {
                FieldInfo f = target.GetType().GetField(field, Members);
                Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone: re-point this replay");
                f.SetValue(target, value);
            }
        }

        private sealed class LogFilter : IDisposable
        {
            private readonly LogType _was;

            public LogFilter(LogType least)
            {
                _was = Debug.unityLogger.filterLogType;
                Debug.unityLogger.filterLogType = least;
            }

            public void Dispose() => Debug.unityLogger.filterLogType = _was;
        }

        /// <summary>A mesh hull's drawing, as the walk reads it: a continuous heading and her camera.</summary>
        private sealed class DrawnHull : IBoatHullPresenter
        {
            private readonly float _heading;
            private readonly float _elevation;
            private readonly bool _ccw;

            public DrawnHull(float heading, float elevation, bool ccw)
            {
                _heading = heading;
                _elevation = elevation;
                _ccw = ccw;
            }

            public BoatHullVariant Variant => default;
            public float DrawnHeadingDegrees() => _heading;
            public int FacingCellIndex => 0;
            public int FacingCount => 0;
            public bool FacingsAreCounterClockwise => _ccw;
            public float BakeElevationDegrees => _elevation;
            public float WakeSternOffsetMeters => 0f;
            public float WatertightHalfBeamMeters => 0f;
            public float DesignWaterlineMeters => 0f;
            public bool HasRockGrid => false;
            public int RockFrame { get; set; }
            public bool SupportsContinuousRock => false;
            public void SetRockPhaseDegrees(float phaseDegrees) { }
            public float VisualTiltDegrees { get; set; }
            public void SetDisplacedHeaveMeters(float heaveMeters) { }
            public float DrawnRideMeters => 0f;
            public float AppliedRollDegrees => 0f;
            public float AppliedPitchDegrees => 0f;
            public float AppliedHeaveMeters => 0f;
            public void SetDrawnRideMeters(float rideMeters) { }
            public void SetStormRock(float amplitudeScale, float extraRollDegrees, float extraPitchDegrees) { }
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active) { }
            public float DeckOccluderId => 0f;
            public IDeckOccupantSlots DeckOccupants => null;
            public Transform Visual => null;
            public IBoatHullAnchors Anchors => null;
        }

        // ===== end of the rig =====
    }
}
