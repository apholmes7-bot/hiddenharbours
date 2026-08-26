using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>THE PHASE MACHINE</b> (design/npc-pilotage.md §2.1) — one boat's passage from the fairway to her
    /// lines, sequenced over an abstract helm.
    ///
    /// <para><b>⭐ It SEQUENCES; it does not steer.</b> Every command it issues comes out of
    /// <see cref="ArrivalPilot"/> (the along-track speed curve, the astern, the throttle law, the steering
    /// gain) or <see cref="BerthPilot"/> (the gate, the set rate, the crab). What this class adds is the
    /// order, the holds and the aborts — and nothing else. That split is the S1 constraint verbatim:
    /// <i>ArrivalPilot is the proven primitive; S1 sequences it, it does not re-derive control.</i></para>
    ///
    /// <para><b>The five phases, and what each actually commands here:</b></para>
    /// <list type="table">
    ///   <item><term>Passage</term><description>seek the next route mark at CRUISE; the long legs.</description></item>
    ///   <item><term>Approach</term><description>the last authored leg, at HARBOUR speed, easing onto the
    ///   gate. ⭐ The wharf line is not a second authored number: it is <b>the last route mark before the
    ///   berth</b>, which at St Peters is the dredged channel's own mouth. Re-cut the channel and the
    ///   speed limit moves with it.</description></item>
    ///   <item><term>Gate</term><description>hold the BERTH HEADING and close onto the gate's line; run
    ///   through the gate station at the berthing speed. HOLDS with the way off when the pose is out of
    ///   tolerance; ABORTS back to Approach when she has run past it or drifted wide.</description></item>
    ///   <item><term>Alongside</term><description>the same law with the lateral target moved to the berth
    ///   line — the come-alongside at the set rate, with <see cref="ArrivalPilot.TargetSpeed"/> taking the
    ///   last of the way off ASTERN. ABORTS back to Gate.</description></item>
    ///   <item><term>Moored</term><description>helm dead. Entered only when the owner says the lines are
    ///   fast (<see cref="Moor"/>) — this class does not tie knots, and the module that does is not its
    ///   business.</description></item>
    /// </list>
    ///
    /// <para><b>⚠ Nothing here writes a pose.</b> The machine's whole reason for existing is that the pose
    /// is <i>produced</i> — by holding a heading and closing a line — rather than assigned. If she cannot
    /// make the pose she holds or she aborts; there is no third branch, and there is deliberately nowhere
    /// in this file that a position or a rotation is assigned.</para>
    ///
    /// <para><b>A plain C# class, not a MonoBehaviour</b>, so the whole ladder is EditMode-testable with a
    /// fake helm and positioned vectors — no scene, no rigidbody, no frames.</para>
    /// </summary>
    public sealed class BerthingPilot
    {
        /// <summary>She is STOPPED below this speed, in (m/s)². 0.1 m/s — slower than a walk, and well
        /// inside the drift the mooring lines take up anyway. <b>One source</b>: the sequencer's fallback
        /// reads the same number, because two ideas of "stopped" is how a boat gets tied up twice.</summary>
        public const float StoppedSpeedSquared = 0.01f;

        /// <summary>How many hull-lengths off the gate she counts as COMMITTED, and the closest-approach
        /// guard starts watching. Three — far enough out that a wide pass still latches, close enough in
        /// that the turn onto the last leg does not.</summary>
        private const float CommittedHullLengths = 3f;

        /// <summary>The most of the INCOMING leg a wheel-over may eat, as a fraction of its length. Half:
        /// a turn begun before the mark she is turning FROM is two corners overlapping, and a passage plan
        /// that does that has legs too short for the hull running them. It is also what bounds how far
        /// inside a corner she may cut — half a leg back, at the widest turn, keeps her inside a marked
        /// fairway's own half-width.</summary>
        private const float MaxWheelOverLegFraction = 0.5f;

        /// <summary>How far the range must OPEN again before "this is as close as she gets" is called (m).
        /// A metre of hysteresis, so a boat holding station in a chop does not latch on measurement
        /// noise.</summary>
        private const float RangeOpeningMetres = 1f;

        private readonly Vector2[] _route;
        private readonly BerthPilot.Berth _berth;
        private readonly BerthPilot.Settings _alongside;
        private readonly ArrivalPilot.Settings _pilot;
        private readonly ArrivalPilot.Settings _harbour;   // the pilot, capped at harbour speed
        private readonly ArrivalPilot.Settings _berthing;  // the pilot, capped at the berthing speed
        private readonly Vector2 _gate;

        private int _leg;
        private float _closestToGate = float.MaxValue;
        private bool _standOff;      // aborted out of the gate: she must get astern of it again first

        /// <summary>Which phase she is in — the thing a test asserts on.</summary>
        public PilotagePhase Phase { get; private set; } = PilotagePhase.Passage;

        /// <summary>How many times this arrival has fallen back and re-presented. Exposed because an
        /// approach that keeps aborting is a diagnosis, not a mystery.</summary>
        public int Aborts { get; private set; }

        /// <summary>The approach gate — the pose she presents from, one hull-length astern of the berth
        /// and a standoff off its line.</summary>
        public Vector2 GatePosition => _gate;

        /// <summary>The berth she is bound for, as a pose.</summary>
        public BerthPilot.Berth Berth => _berth;

        /// <summary>The come-alongside tuning actually in force (already defaulted).</summary>
        public BerthPilot.Settings Alongside => _alongside;

        /// <summary>
        /// <paramref name="route"/> is the authored fairway, seaward first, ending AT the berth — the
        /// region's own channel. ⚠ Its last mark is replaced by the <b>gate</b> for steering purposes: a
        /// berth is where she stops, not a mark she runs to, and the route that reaches it is one mark
        /// short of the manoeuvre that finishes it.
        /// </summary>
        public BerthingPilot(Vector2[] route, in BerthPilot.Berth berth,
                             in ArrivalPilot.Settings pilot, in BerthPilot.Settings alongside)
        {
            _berth = berth;
            _alongside = alongside.OrDefault();
            _pilot = pilot;
            _harbour = WithCruise(pilot, _alongside.HarbourSpeedMetresPerSecond);
            _berthing = WithCruise(pilot, _alongside.BerthingSpeedMetresPerSecond);
            _gate = BerthPilot.Gate(berth, _alongside);

            // The EFFECTIVE route: the authored marks, with the berth swapped for the gate. Copied rather
            // than mutated — the caller's array is the region's, and a pilot has no business editing it.
            Vector2[] marks = route != null && route.Length > 0 ? route : new[] { berth.Position };
            _route = new Vector2[marks.Length];
            System.Array.Copy(marks, _route, marks.Length);
            _route[_route.Length - 1] = _gate;

            Phase = _route.Length > 1 ? PilotagePhase.Passage : PilotagePhase.Approach;
        }

        /// <summary>The mark she is steering for right now (diagnostics, and the sequencer's log).</summary>
        public Vector2 CurrentMark => _route[Mathf.Clamp(_leg, 0, _route.Length - 1)];

        /// <summary>How far she still has to run along the route to the GATE — the distance the ease-down
        /// is measured against.</summary>
        public float MetresToGate(Vector2 position) => ArrivalPilot.MetresToBerth(position, _route, _leg);

        /// <summary>Is this way off? One source, so the machine and the sequencer's fallback cannot come
        /// to mean different things by "stopped".</summary>
        public static bool IsStopped(Vector2 velocity) => velocity.sqrMagnitude < StoppedSpeedSquared;

        /// <summary>
        /// <b>She is ready for her lines</b>: alongside, stopped, and in the berth's own pose. The last
        /// half-metre is not hers to close — see <see cref="BerthPilot.Settings.LateralEaseMetres"/> — so
        /// this is the moment the heaving line goes over and <c>MooringLineMath</c> takes over.
        /// </summary>
        public bool ReadyForLines(Vector2 position, float headingDegrees, Vector2 velocity)
            => Phase == PilotagePhase.Alongside
               && IsStopped(velocity)
               && BerthPilot.WithinPose(position, headingDegrees, _berth, 0f, _alongside);

        /// <summary>The lines are fast: the helm goes dead and she is a moored boat from here. Called by
        /// whoever actually made them fast — the machine does not reach into the mooring module.</summary>
        public void Moor(IPilotageHelm helm)
        {
            Phase = PilotagePhase.Moored;
            if (helm != null) helm.SetControl(0f, 0f);
        }

        // =================================================================================================
        //  the step
        // =================================================================================================

        /// <summary>
        /// One fixed step: read the pose, decide the phase, command the helm. A no-op once
        /// <see cref="PilotagePhase.Moored"/> — a moored boat's helm is nobody's.
        /// </summary>
        public void Step(IPilotageHelm helm)
        {
            if (helm == null || Phase == PilotagePhase.Moored) return;

            Vector2 here = helm.Position;
            float heading = helm.HeadingDegrees;
            Vector2 velocity = helm.Velocity;

            AdvanceMarks(here, heading, velocity);

            // The capture is read BEFORE the command so the gate's own law drives the very step she is
            // captured on — otherwise one step of route-pursuit steering is issued for a boat that is
            // already supposed to be lining up.
            if (Phase == PilotagePhase.Approach && Captured(here)) EnterGate(here);

            ArrivalPilot.Helm command;
            switch (Phase)
            {
                case PilotagePhase.Gate:
                    command = CommandGate(here, heading, velocity);
                    break;
                case PilotagePhase.Alongside:
                    command = CommandAlongside(here, heading, velocity);
                    break;
                default:
                    command = RunTheRoute(here, heading, velocity);
                    break;
            }

            helm.SetControl(command.Throttle, command.Steer);
        }

        /// <summary>Walk the mark cursor forward, and with it the Passage → Approach boundary: the last
        /// authored mark before the berth IS the wharf line (see the class note).</summary>
        private void AdvanceMarks(Vector2 here, float headingDegrees, Vector2 velocity)
        {
            while (_leg < _route.Length - 1 && Reached(here, headingDegrees, velocity, _leg))
                _leg++;

            if (Phase == PilotagePhase.Passage && _leg >= _route.Length - 1)
                Phase = PilotagePhase.Approach;
        }

        /// <summary>
        /// ⭐ <b>The wheel-over distance for the turn off <paramref name="leg"/> onto the next one</b> —
        /// <see cref="BerthPilot.WheelOverMetres"/> asked about the course change she has LEFT to make.
        ///
        /// <para>Measured against her current heading rather than the incoming leg's bearing, which is
        /// the dynamic form and the honest one: a boat already half way round the corner has half the
        /// turn left and needs half the room. It also makes the rule self-limiting — once she is lined up
        /// on the next leg the anticipation is zero and only the arrive radius is left.</para>
        /// </summary>
        private float WheelOverFor(int leg, float headingDegrees, Vector2 velocity)
        {
            if (leg + 1 >= _route.Length) return 0f;      // there is nothing after the gate to turn onto

            Vector2 next = _route[leg + 1] - _route[leg];
            if (next.sqrMagnitude < 1e-4f) return 0f;

            float turn = ArrivalPilot.Wrap180(ArrivalPilot.CompassOf(next) - headingDegrees);
            float wheelOver = BerthPilot.WheelOverMetres(velocity.magnitude, turn, _alongside);

            // …and never further back than half the leg she is turning off (see the const's note).
            if (leg > 0)
                wheelOver = Mathf.Min(wheelOver,
                                      (_route[leg] - _route[leg - 1]).magnitude * MaxWheelOverLegFraction);
            return wheelOver;
        }

        /// <summary>
        /// 🔴 <b>A mark is done with when she must WHEEL OVER for it, when she is inside it, or when she
        /// is PAST it</b> — three arms, and the first two are what a route means to a hull rather than to
        /// a point.
        ///
        /// <para>The wheel-over (<see cref="WheelOverFor"/>) is the planned one: a corner is turned by
        /// putting the helm over BEFORE the mark, by <c>R·tan(Δ/2)</c>, so the arc comes out on the next
        /// leg. Without it a pursuit controller turns AT the mark and leaves the corner most of a turning
        /// diameter wide — which is what the real fairway measured.</para>
        ///
        /// <para>The passed-mark arm is the RECOVERY, for the corner the anticipation still did not
        /// quite cover. The arrive radius alone assumes she can always be steered inside it; on a corner
        /// she cannot, and a pursuit controller then turns her BACK toward a mark she has already left
        /// astern, which is a circle — measured on the real fairway as exactly that: round and round the
        /// channel mouth, never inside four metres of it, while the berth waited fifty metres
        /// away.</para>
        ///
        /// <para>So the last arm is what a skipper does when the anticipation was not enough: <b>once the
        /// buoy is abeam, you are on to the next one.</b> A mark astern of her nose is a mark she has
        /// rounded, however wide.</para>
        ///
        /// <para>⚠ <b>Gated on being COMMITTED to it, and that guard is load-bearing.</b> Mid-turn she can
        /// be pointing away from a mark that is still a hundred metres ahead — dead astern of her nose and
        /// nowhere near passed. Requiring her to be within a few hull-lengths of it first means the arm
        /// can only ever retire a mark she has actually been to.</para>
        /// </summary>
        private bool Reached(Vector2 here, float headingDegrees, Vector2 velocity, int leg)
        {
            Vector2 toMark = _route[leg] - here;

            // ⭐ THE WHEEL-OVER: a mark with a corner at it is done with when she must START turning, not
            // when she is on top of it. See WheelOverFor — this is what keeps a 12.9 m hull with a 24 m
            // turning circle on a fairway whose corners are 60° apart.
            float turnIn = Mathf.Max(_pilot.ArriveRadiusMetres,
                                     WheelOverFor(leg, headingDegrees, velocity));
            if (toMark.sqrMagnitude <= turnIn * turnIn) return true;

            float committed = CommittedHullLengths * _berth.HullLengthMetres;
            if (toMark.sqrMagnitude > committed * committed) return false;

            return Vector2.Dot(toMark, BerthPilot.Forward(headingDegrees)) <= 0f;
        }

        /// <summary>
        /// Passage and Approach: <see cref="ArrivalPilot"/>, unchanged, steering for the next mark — with
        /// two differences from a plain run to a berth, both of them arithmetic on its own inputs rather
        /// than a second control law.
        ///
        /// <list type="number">
        ///   <item>The cruise cap is the harbour speed once she is on the last leg (§2.1's Approach row).</item>
        ///   <item>The distance she is easing against is the run to the gate PLUS
        ///   <see cref="BerthPilot.BerthingRunoutMetres"/>, so the curve bottoms out at the berthing speed
        ///   AT the gate rather than at zero. She passes through the gate with steerage on; a boat that
        ///   stopped there would have to gather way again against a twenty-second time constant.</item>
        /// </list>
        /// </summary>
        private ArrivalPilot.Helm RunTheRoute(Vector2 here, float heading, Vector2 velocity)
        {
            ArrivalPilot.Settings settings = Phase == PilotagePhase.Approach ? _harbour : _pilot;
            float toRun = MetresToGate(here) + BerthPilot.BerthingRunoutMetres(_alongside, _pilot);
            return ArrivalPilot.Command(here, heading, velocity, CurrentMark, toRun, settings);
        }

        /// <summary>
        /// 🔴 <b>Is the gate captured?</b> Inside the capture range — <i>or past her closest approach to
        /// it</i>, which is the arm that keeps a docking from becoming an ORBIT.
        ///
        /// <para>A radius alone assumes she can always be steered inside it. She cannot: her turning
        /// circle at approach speed is wider than the arrive radius, so a track that misses by a few
        /// metres sails on, comes round, and misses again — forever. Measured on the real fairway before
        /// this arm existed: closest approach 5.5 m, then a 50 m loop south, helm hard over the whole way.
        /// The boat was doing everything it was told; what it was told had no way to end.</para>
        ///
        /// <para>So the second arm is the one a skipper actually uses: <b>the range stopped
        /// shortening</b>. This is the same guard the arrival has always carried, moved onto the GATE —
        /// which is where the manoeuvre now begins.</para>
        /// </summary>
        private bool Captured(Vector2 here)
        {
            // ⭐ AND AFTER AN ABORT SHE MUST ACTUALLY GO ROUND. Without this, "take another turn" is a
            // phase flip and nothing else: she falls back to Approach still sitting inside the capture
            // range, is re-captured on the very next step, fails the same pose and aborts again — a
            // ping-pong that spends the abort budget without her ever having presented a second time.
            // The gate is capturable only from ASTERN of it, which is the seamanship as well as the fix:
            // you cannot arrive at a gate you are already past, you come back round behind it.
            if (_standOff)
            {
                if (BerthPilot.AlongTrackTo(here, _gate, _berth.HeadingDegrees) <= 0f) return false;
                _standOff = false;
            }

            float range = Vector2.Distance(here, _gate);
            if (range <= _alongside.GateCaptureMetres) return true;

            // Only once she is committed — otherwise the first metre of the passage, where the range to a
            // gate 150 m away momentarily grows on the turn, would read as "as close as she gets".
            if (range > CommittedHullLengths * _berth.HullLengthMetres)
            {
                _closestToGate = float.MaxValue;
                return false;
            }

            if (range < _closestToGate) { _closestToGate = range; return false; }
            return range > _closestToGate + RangeOpeningMetres;
        }

        private void EnterGate(Vector2 here)
        {
            Phase = PilotagePhase.Gate;
            Debug.Log($"[pilotage] gate — presenting off the berth at ({_gate.x:F1}, {_gate.y:F1}) on " +
                      $"{_berth.HeadingDegrees:F0}°, from ({here.x:F1}, {here.y:F1}).");
        }

        /// <summary>
        /// <b>GATE.</b> Hold the berth heading and close onto the gate's own line; run through the gate
        /// station at the berthing speed. Three outcomes and never a fourth: advance when the pose is
        /// made, HOLD with the way off when it is not, ABORT when she has run past the station or
        /// wandered wide.
        /// </summary>
        private ArrivalPilot.Helm CommandGate(Vector2 here, float heading, Vector2 velocity)
        {
            float standoff = _alongside.GateStandoffMetres;
            float toStation = BerthPilot.AlongTrackTo(here, _gate, _berth.HeadingDegrees);
            bool atStation = toStation <= 0f;
            bool posed = BerthPilot.WithinPose(here, heading, _berth, standoff, _alongside);

            if (atStation && posed)
            {
                Phase = PilotagePhase.Alongside;
                Debug.Log($"[pilotage] alongside — square on {heading:F0}° against the berth's " +
                          $"{_berth.HeadingDegrees:F0}°, {BerthPilot.LateralOffset(here, _berth):F2} m " +
                          $"off her line, making {velocity.magnitude:F2} m/s. Closing at the set rate.");
                return CommandAlongside(here, heading, velocity);
            }

            if (!posed && OutOfBounds(here, standoff, toStation))
            {
                Abort(PilotagePhase.Approach, here, "could not get square at the gate");
                if (Phase == PilotagePhase.Approach) return RunTheRoute(here, heading, velocity);
            }

            // HOLD is the way OFF, not a pause: a boat that keeps running while she is out of pose runs
            // out of berth. Otherwise she carries the berthing speed through the station.
            //
            // ⚠ AND THE LINE-UP IS NOT RATE-LIMITED AT THE SET RATE. The set rate is the COME-ALONGSIDE's
            // number (§2.1 puts it in the Alongside row and nowhere else); asking a boat to cross her own
            // approach at a fender's 0.25 m/s is asking her to arrive off her line and hold there. See
            // BerthPilot.WantedClosingRate for the measurement. She closes onto the gate's line at the
            // berthing speed, and CrabDegrees's cap is what actually bounds her.
            float wanted = atStation && !posed ? 0f : _alongside.BerthingSpeedMetresPerSecond;
            return BerthPilot.Command(here, heading, velocity, _berth, standoff,
                                      _alongside.BerthingSpeedMetresPerSecond, wanted,
                                      _alongside, _pilot);
        }

        /// <summary>
        /// <b>ALONGSIDE.</b> The same law with the lateral target on the berth line itself, and the
        /// along-track speed handed back to <see cref="ArrivalPilot.TargetSpeed"/> — which is what puts
        /// her ASTERN for the last of it rather than letting a six-tonne hull with a twenty-second time
        /// constant coast past her own berth.
        /// </summary>
        private ArrivalPilot.Helm CommandAlongside(Vector2 here, float heading, Vector2 velocity)
        {
            float toBerth = BerthPilot.AlongTrackTo(here, _berth.Position, _berth.HeadingDegrees);

            if (!BerthPilot.WithinPose(here, heading, _berth, 0f, _alongside)
                && OutOfBounds(here, 0f, toBerth))
            {
                Abort(PilotagePhase.Gate, here, "lost the berth on the come-alongside");
                // ⚠ ONE fall-back per step, and this is why it does not re-enter CommandGate: a boat far
                // enough off the berth to lose it is usually also past the gate's station, so a cascade
                // would drop her two phases on one reading of one pose. She is given the way OFF for this
                // step and the gate's own law picks her up on the next one, with a fresh pose to judge.
                return BerthPilot.Command(here, heading, velocity, _berth,
                                          _alongside.GateStandoffMetres,
                                          _alongside.BerthingSpeedMetresPerSecond, 0f,
                                          _alongside, _pilot);
            }

            // §2.1's Alongside HOLD: closing faster than the set rate. The aim has already come off — the
            // crab is a function of the error and is asking for less — but a hull carrying lateral way
            // does not stop because she has been re-aimed, so the way comes OFF too. That is what a hold
            // IS here: astern, and let the sideways drift die against her own lateral drag.
            bool closingTooFast = BerthPilot.ClosingRate(velocity, _berth)
                                  > _alongside.SetRateMetresPerSecond
                                    * Mathf.Max(1f, _alongside.OverSetRateHoldFactor);

            float wanted = closingTooFast
                ? 0f
                : ArrivalPilot.TargetSpeed(Mathf.Max(0f, toBerth), _berthing);
            return BerthPilot.Command(here, heading, velocity, _berth, 0f,
                                      _alongside.SetRateMetresPerSecond, wanted, _alongside, _pilot);
        }

        /// <summary>Has she run past the station, or wandered off its line, far enough that another turn
        /// is the honest answer? Overshoot is measured generously on purpose: a normal settle slides past
        /// the mark and is walked back astern, and that is an arrival, not a failure.</summary>
        private bool OutOfBounds(Vector2 here, float lateralTarget, float toStation)
            => -toStation > _alongside.AbortOvershootMetres
               || Mathf.Abs(BerthPilot.LateralOffset(here, _berth) - lateralTarget)
                      > _alongside.AbortLateralMetres;

        /// <summary>
        /// Fall back a phase and re-present. The mark cursor is NOT rewound: the effective route already
        /// ends at the gate, so "take another turn" is simply steering for it again from wherever she has
        /// ended up — she turns, gathers the way the growing distance asks for, and comes back. Rewinding
        /// to an authored mark forty metres astern would ask a stopped hull to turn with no steerage.
        ///
        /// <para>⚠ <b>And it is bounded.</b> Past <see cref="BerthPilot.Settings.MaxAborts"/> she stops
        /// going round and simply holds. That is rule 10 insurance rather than seamanship: an approach
        /// that can abort without limit in a basin it cannot get square in never ends, and a passenger who
        /// can never be put ashore is a broken build.</para>
        /// </summary>
        private void Abort(PilotagePhase to, Vector2 here, string why)
        {
            if (Aborts >= _alongside.MaxAborts)
            {
                Debug.LogWarning($"[pilotage] {why} at ({here.x:F1}, {here.y:F1}) — and she has already " +
                                 $"re-presented {Aborts} times, which is the limit. Holding where she is " +
                                 "rather than going round again.");
                return;
            }

            Aborts++;
            _closestToGate = float.MaxValue;      // a fresh pass may not inherit a stale minimum
            _standOff = to == PilotagePhase.Approach;
            Phase = to;
            Debug.Log($"[pilotage] abort → {to}: {why} at ({here.x:F1}, {here.y:F1}). " +
                      $"Re-presenting (attempt {Aborts + 1}).");
        }

        /// <summary>The pilot settings with a different cruise cap — the ONE thing harbour speed and the
        /// berthing speed change about the approach curve. Everything else (the deceleration, the throttle
        /// gain, the stop band, the steering gain, the arrive radius) is the owner's one set of numbers.</summary>
        private static ArrivalPilot.Settings WithCruise(ArrivalPilot.Settings settings, float cruise)
        {
            settings.CruiseSpeedMetresPerSecond = Mathf.Max(0.1f, cruise);
            return settings;
        }
    }
}
