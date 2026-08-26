using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>THE COME-ALONGSIDE — the one genuinely new manoeuvre</b> (design/npc-pilotage.md §2.2).
    ///
    /// <para><b>⭐ Why <see cref="ArrivalPilot"/> is not enough on its own, in one sentence.</b> It steers
    /// <i>to a mark</i>; a berth is not a mark, it is a <b>POSE</b> — a position AND a heading AND a side
    /// to lie on — and the difference between those two is the whole ask. Everything else here is
    /// <see cref="ArrivalPilot"/>'s: the speed curve, the astern, the throttle law and the steering gain
    /// are read off its <see cref="ArrivalPilot.Settings"/> and called through its own helpers, because
    /// <b>S1 sequences the proven primitive, it does not re-derive control.</b></para>
    ///
    /// <para><b>The three additions, all small, all pure:</b></para>
    /// <list type="number">
    ///   <item><b>The approach gate.</b> A berth's route does not end at the berth. It ends
    ///   <see cref="GateStandoffMetres"/> off the berth line, displaced one hull-length astern along the
    ///   berth heading. A skipper arrives <i>parallel and off</i>, then closes sideways.</item>
    ///   <item><b>The set rate.</b> Lateral closing speed is capped (0.25 m/s — a fender's worth of bump).
    ///   ⚠ This is a <b>SECOND speed loop, orthogonal to <see cref="ArrivalPilot"/>'s along-track one</b>,
    ///   and the two are deliberately not merged: one answers "how fast may I still be going?", the other
    ///   "how fast may I close the wall?", and a single loop cannot hold both.</item>
    ///   <item><b>⭐ The last half-metre is the LINES, not the hull.</b> See
    ///   <see cref="LateralEaseMetres"/>. The lateral ease is deliberately the ONE control law here that
    ///   does not arrive under its own power, because it is not supposed to: she stops with a fender's
    ///   gap and <c>MooringLineMath</c> takes the rest.</item>
    /// </list>
    ///
    /// <para><b>Pure and static</b>, for the reason <see cref="ArrivalPilot"/> is: same arguments, same
    /// answer, forever. No clock, no random, no state — so the whole come-alongside is EditMode-testable
    /// with positioned vectors and no scene.</para>
    /// </summary>
    public static class BerthPilot
    {
        /// <summary>
        /// <b>Where a hull lies when she is tied up</b> — the pose the snap used to fake, as data.
        ///
        /// <para><b>⚠ Nothing here is authored twice.</b> The position and heading are the region's own
        /// berth; the seaward side is DERIVED from where the region says the player steps ashore (which is
        /// on the planks by construction, so the other side is the water); the length is the hull's own.
        /// A berth this pilot cannot derive is a berth it declines to plan, never one it invents.</para>
        /// </summary>
        public readonly struct Berth
        {
            /// <summary>Where her keel lies (world XY).</summary>
            public readonly Vector2 Position;

            /// <summary>The compass heading her bow lies on — parallel to the FACE she is tied to.</summary>
            public readonly float HeadingDegrees;

            /// <summary>Unit normal to the berth heading, pointing from the berth toward OPEN WATER — the
            /// side she is presented from and closes across.</summary>
            public readonly Vector2 Seaward;

            /// <summary>Her length overall, metres — the gate's astern displacement is measured in these.</summary>
            public readonly float HullLengthMetres;

            public Berth(Vector2 position, float headingDegrees, Vector2 seaward, float hullLengthMetres)
            {
                Position = position;
                HeadingDegrees = headingDegrees;
                Seaward = seaward.sqrMagnitude > 1e-6f ? seaward.normalized : Vector2.right;
                HullLengthMetres = Mathf.Max(0.5f, hullLengthMetres);
            }

            /// <summary>
            /// The berth as the region states it: a keel position, a heading, the hull that lies there,
            /// and <b>a point known to be ashore</b> — the step-ashore/disembark point, which the region
            /// ratifies as being on the planks. The seaward side is whichever side of the berth line that
            /// point is NOT on.
            ///
            /// <para>⭐ Deriving the side from the shore point rather than authoring it is what keeps this
            /// honest through a re-sited pier: move the wharf and the disembark moves with it, so the side
            /// she presents from turns with the berth instead of staying where a serialized field was
            /// typed. A degenerate input (the shore point ON the berth line) falls back to the starboard
            /// normal — an arbitrary but stable answer, and an authoring fault rather than a
            /// crash.</para>
            /// </summary>
            public static Berth FromShorePoint(Vector2 position, float headingDegrees,
                                               Vector2 shorePoint, float hullLengthMetres)
            {
                Vector2 starboard = Starboard(headingDegrees);
                float towardShore = Vector2.Dot(shorePoint - position, starboard);
                return new Berth(position, headingDegrees,
                                 towardShore > 0f ? -starboard : starboard, hullLengthMetres);
            }
        }

        /// <summary>
        /// The come-alongside's tuning — <b>every number it has, in one place and none of them in the
        /// code</b> (rule 6). Serialized on the sequencer beside <see cref="ArrivalPilot.Settings"/>, so
        /// the owner retunes a docking without a recompile.
        ///
        /// <para>⚠ <b>Read through <see cref="OrDefault"/>, never raw.</b> A YAML-omitted struct
        /// deserialises to C# defaults — all zeros — and a scene serialized before this field existed
        /// omits it entirely. Zeros here would mean a set rate of nothing and a gate on top of the berth.
        /// This is the same guard <c>MooringLineSettings</c> keeps and for the same reason.</para>
        /// </summary>
        [System.Serializable]
        public struct Settings
        {
            [Tooltip("Speed inside the wharf line, m/s — the harbour speed of §2.1's Approach row. " +
                     "⭐ 3 m/s (about six knots) is what ArrivalPilot's own tooltip calls 'harbour speed " +
                     "for a working boat coming in', and at 3 her stop is 13 m against 33 m at the " +
                     "fairway's 5. The fairway cruise is unchanged and stays on ArrivalPilot.Settings; " +
                     "this caps only the last leg in.")]
            [Min(0.1f)] public float HarbourSpeedMetresPerSecond;

            [Tooltip("Speed she comes alongside at, m/s — a slow ahead. The gate is passed THROUGH at " +
                     "this, not stopped at, so she carries steerage into the come-alongside instead of " +
                     "having to gather way again from rest with a twenty-second time constant.")]
            [Min(0.05f)] public float BerthingSpeedMetresPerSecond;

            [Tooltip("How fast she may close the berth sideways, m/s — THE SET RATE, and the number that " +
                     "makes a docking read as competent. 0.25 m/s is a fender's worth of bump.")]
            [Min(0.01f)] public float SetRateMetresPerSecond;

            [Tooltip("How far astern of the berth the approach gate sits, in HULL LENGTHS. One: she " +
                     "arrives parallel and off, then has her own length to run while she closes.")]
            [Min(0f)] public float GateAsternHullLengths;

            [Tooltip("How far OFF the berth line the gate sits, m. ⚠ Measured off the BERTH, not off the " +
                     "wharf face — the berth already sits a half-beam plus a fender's gap out from the " +
                     "face, so this is the extra standoff she closes at the set rate and nothing else.")]
            [Min(0.1f)] public float GateStandoffMetres;

            [Tooltip("Inside this range of the gate (m) she stops steering FOR it and starts holding the " +
                     "BERTH HEADING — the line-up. Wide enough that she has room to swing square before " +
                     "the gate station rather than arriving on the last leg's own bearing.")]
            [Min(0.5f)] public float GateCaptureMetres;

            [Tooltip("Pose tolerance at the gate: how far off the berth heading she may be, degrees " +
                     "(§2.1's Gate row). Outside it she HOLDS at the gate with the way off rather than " +
                     "advancing.")]
            [Min(0.5f)] public float HeadingToleranceDegrees;

            [Tooltip("Pose tolerance at the gate: how far off the gate's own line she may be, m.")]
            [Min(0.05f)] public float LateralToleranceMetres;

            [Tooltip("⭐ THE LINES' HALF-METRE. Inside this lateral distance the commanded closing rate " +
                     "eases off linearly, so she arrives at the berth line asymptotically rather than " +
                     "driving onto it. ⚠ A proportional ease normally NEVER arrives (ArrivalPilot's own " +
                     "warning about a target proportional to distance) — and that is exactly right here, " +
                     "because the hull is not what closes the last of it. The line is.")]
            [Min(0.05f)] public float LateralEaseMetres;

            [Tooltip("The most she may angle off the berth heading to make her set rate good, degrees. " +
                     "⚠ EFFECTIVELY CAPPED BY THE POSE TOLERANCE as well, and that is not belt-and-braces: " +
                     "a crab bigger than the heading tolerance is a boat aiming herself OUT of her own " +
                     "berth, and she would then reach it out of pose and hold there forever. Kept a few " +
                     "degrees inside it so a boat holding her commanded aim is always in pose on heading.")]
            [Min(1f)] public float MaxCrabDegrees;

            [Tooltip("Floor on the along-track speed used to work out the crab angle, m/s. Without it a " +
                     "boat with no way on would be asked for ninety degrees of helm to make good a " +
                     "quarter-knot sideways.")]
            [Min(0.05f)] public float MinTrackSpeedMetresPerSecond;

            [Tooltip("HOLD (§2.1's Alongside row): how much faster than the set rate she may be closing " +
                     "before the come-alongside takes the way off, as a MULTIPLE of the set rate. ⚠ Above " +
                     "1 by necessity rather than taste — the crab aims for EXACTLY the set rate, so a " +
                     "threshold at it would chatter on float noise for the whole manoeuvre. It fires when " +
                     "something SHOVED her (a sea, a wake, the player), which is the only way a boat on " +
                     "this loop closes faster than she was asked to.")]
            [Min(1f)] public float OverSetRateHoldFactor;

            [Tooltip("ABORT: how far past the gate (or the berth) she may run, still out of pose " +
                     "tolerance, before the phase falls back and re-presents. Generous — a normal settle " +
                     "slides past and is walked back astern, and that is not a failed approach.")]
            [Min(0.5f)] public float AbortOvershootMetres;

            [Tooltip("ABORT: how far off her phase's own lateral line she may be before the phase falls " +
                     "back and re-presents, m.")]
            [Min(0.5f)] public float AbortLateralMetres;

            [Tooltip("How many times a single arrival may abort and re-present before it stops going " +
                     "round and simply HOLDS instead. ⚠ Rule 10 insurance, not seamanship: an approach " +
                     "that can abort without bound in a basin it cannot get square in never ends, and a " +
                     "passenger who can never be put ashore is a broken build.")]
            [Min(0)] public int MaxAborts;

            [Tooltip("How far the skipper's heaving line reaches for a shore cleat, m — measured from " +
                     "the BERTH, so the line finds the wharf she is lying at rather than any fitting " +
                     "that happens to be loaded.")]
            [Min(0.5f)] public float LineReachMetres;

            [Tooltip("⭐ WHEEL-OVER: how fast she can turn at full helm, °/s — the one manoeuvring " +
                     "characteristic the pilot needs and cannot measure. A route is a set of marks; a " +
                     "hull is not a point, and the distance she needs to START a turn in is her turning " +
                     "radius (speed ÷ this) times tan(half the course change). Declare it too high and " +
                     "she wheels over late and swings wide; too low and she cuts the corner early. It is " +
                     "a DECLARATION about the hull rather than a taste, so it belongs beside her length " +
                     "and not in a feel slider.")]
            [Min(0.5f)] public float TurnRateDegreesPerSecond;

            /// <summary>
            /// What the come-alongside ships at, and where each number comes from.
            ///
            /// <para><b>The two the owner ruled on.</b> <see cref="HarbourSpeedMetresPerSecond"/> is
            /// design/npc-pilotage.md Q2's recommendation — 3 inside the wharf line, the fairway left at
            /// the shipped 5 — which costs the St Peters passage about six seconds and buys a stop that
            /// fits between berths. <see cref="SetRateMetresPerSecond"/> is §2.2's 0.25 m/s.</para>
            ///
            /// <para><b>The rest are measured against this hull.</b> A 12.9 m cape islander at 1 m/s
            /// stops in <c>v²/2a + StopMetres = 1.25 + 2 = 3.25 m</c>, so the gate's one-hull-length
            /// displacement leaves her about nine metres of slow ahead to close two metres sideways in —
            /// 9 s against the set rate's 8 s, which is why the two loops finish together rather than one
            /// waiting on the other. The ±15° / ±1 m pose tolerance is §2.1's Gate row verbatim.</para>
            /// </summary>
            public static Settings Default => new Settings
            {
                HarbourSpeedMetresPerSecond = 3f,
                BerthingSpeedMetresPerSecond = 1f,
                SetRateMetresPerSecond = 0.25f,
                GateAsternHullLengths = 1f,
                GateStandoffMetres = 2f,
                GateCaptureMetres = 12f,
                HeadingToleranceDegrees = 15f,
                LateralToleranceMetres = 1f,
                LateralEaseMetres = 1f,
                MaxCrabDegrees = 12f,
                MinTrackSpeedMetresPerSecond = 0.5f,
                OverSetRateHoldFactor = 2f,
                AbortOvershootMetres = 6f,
                AbortLateralMetres = 3f,
                MaxAborts = 2,
                LineReachMetres = 12f,
                TurnRateDegreesPerSecond = 12f,
            };

            /// <summary>
            /// This struct if it was authored, <see cref="Default"/> if it was not. The discriminator is
            /// <see cref="SetRateMetresPerSecond"/>: it can never legitimately be zero (a docking that
            /// closes at nothing never touches the wharf), so zero means "nobody wrote this" rather than
            /// "somebody chose it" — the established gate-off shape, never zeros.
            /// </summary>
            public Settings OrDefault() => SetRateMetresPerSecond > 0f ? this : Default;
        }

        // =================================================================================================
        //  the frame — a berth is a POSE, so everything here is measured in the berth's own axes
        // =================================================================================================

        /// <summary>The unit vector a compass heading points along. The inverse of
        /// <see cref="ArrivalPilot.CompassOf"/>, and the only place that inversion is written.</summary>
        public static Vector2 Forward(float headingDegrees)
        {
            float r = headingDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        /// <summary>The unit vector 90° to STARBOARD of a compass heading (compass turns clockwise, so
        /// starboard is <c>(y, −x)</c> of the forward vector).</summary>
        public static Vector2 Starboard(float headingDegrees)
        {
            Vector2 f = Forward(headingDegrees);
            return new Vector2(f.y, -f.x);
        }

        /// <summary>
        /// <b>The approach gate</b>: <see cref="Settings.GateStandoffMetres"/> off the berth line, on the
        /// open-water side, displaced <see cref="Settings.GateAsternHullLengths"/> hull-lengths ASTERN
        /// along the berth heading.
        /// </summary>
        public static Vector2 Gate(in Berth berth, in Settings settings)
            => berth.Position
               - Forward(berth.HeadingDegrees) * (settings.GateAsternHullLengths * berth.HullLengthMetres)
               + berth.Seaward * settings.GateStandoffMetres;

        /// <summary>How far <paramref name="mark"/> still is AHEAD of <paramref name="position"/> along the
        /// berth heading, metres. Negative once she has run past it.</summary>
        public static float AlongTrackTo(Vector2 position, Vector2 mark, float headingDegrees)
            => Vector2.Dot(mark - position, Forward(headingDegrees));

        /// <summary>How far OUTBOARD of the berth line she is, metres — positive to seaward, negative once
        /// she is inboard of it (a fender pressed against the wharf).</summary>
        public static float LateralOffset(Vector2 position, in Berth berth)
            => Vector2.Dot(position - berth.Position, berth.Seaward);

        /// <summary>How fast she is CLOSING the berth line right now, m/s — positive closing, negative
        /// opening. The measured half of the set-rate loop.</summary>
        public static float ClosingRate(Vector2 velocity, in Berth berth)
            => -Vector2.Dot(velocity, berth.Seaward);

        /// <summary>
        /// <b>The set-rate loop's ask</b>: how fast she SHOULD be closing, given how far she still has to
        /// come across. Capped at the set rate, eased linearly to nothing over the last
        /// <see cref="Settings.LateralEaseMetres"/> — see that field for why an ease that never quite
        /// arrives is the correct shape here and not a bug.
        /// </summary>
        /// <param name="maxClosingRate">The cap for THIS phase, m/s. ⚠ It is a parameter and not
        /// <see cref="Settings.SetRateMetresPerSecond"/> read directly, and the difference is load-bearing:
        /// <b>the set rate is the COME-ALONGSIDE's number, not the line-up's</b> (§2.1 puts it in the
        /// Alongside row and nowhere else). Rate-limiting the LINE-UP at a fender's 0.25 m/s is a boat who
        /// cannot cross her own approach: measured on the real St Peters fairway, she reaches the gate
        /// with three metres still to come across and about five seconds to do it in, arrives 1.7 m off
        /// her line, fails the pose, and holds there for ever. Worse, the loop actively UNDOES the useful
        /// crab the last leg's own bearing gave her. So the gate closes at the berthing speed and only the
        /// come-alongside closes at the set rate — with <see cref="CrabDegrees"/>'s cap doing the real
        /// bounding either way.</param>
        public static float WantedClosingRate(float lateralError, float maxClosingRate,
                                              in Settings settings)
        {
            float ease = Mathf.Max(1e-3f, settings.LateralEaseMetres);
            return Mathf.Clamp(lateralError / ease, -1f, 1f) * Mathf.Max(0f, maxClosingRate);
        }

        /// <summary>
        /// <b>How far off the berth heading she must aim to make that closing rate good</b>, degrees,
        /// signed toward the berth.
        ///
        /// <para>⭐ A GEOMETRY, not a gain. A hull with no leeway goes where she points, so a track angled
        /// <c>atan(closing ÷ alongSpeed)</c> off the heading closes at exactly <c>closing</c>. Writing it
        /// as the arctangent rather than as a tuned constant is what makes the crab shrink by itself as
        /// she slows, and vanish as the lateral error does — so she ends the come-alongside PARALLEL
        /// without anybody having to schedule the straightening-up.</para>
        /// </summary>
        public static float CrabDegrees(float wantedClosing, float alongSpeed, in Settings settings)
        {
            float v = Mathf.Max(Mathf.Abs(alongSpeed), settings.MinTrackSpeedMetresPerSecond);
            float degrees = Mathf.Atan2(wantedClosing, v) * Mathf.Rad2Deg;

            // ⚠ The cap is the SMALLER of the tuned one and the pose tolerance she has to satisfy. As she
            // slows, the aim needed for a given closing rate grows — atan2 of a fixed numerator over a
            // shrinking denominator — so without this the last few metres of a come-alongside command a
            // bigger and bigger angle and she arrives lying across her own berth, out of pose, holding
            // forever. The floor on the track speed keeps that growth finite; this keeps it legal.
            float cap = Mathf.Min(settings.MaxCrabDegrees, settings.HeadingToleranceDegrees);
            return Mathf.Clamp(degrees, -cap, cap);
        }

        /// <summary>Which way, in COMPASS degrees, is "toward the wharf" from the berth heading: +1 when
        /// the berth is to starboard of her lie, −1 when it is to port.</summary>
        public static float ShoreSide(in Berth berth)
            => Mathf.Sign(ArrivalPilot.Wrap180(
                   ArrivalPilot.CompassOf(-berth.Seaward) - berth.HeadingDegrees));

        // =================================================================================================
        //  the helm
        // =================================================================================================

        /// <summary>
        /// <b>The come-alongside helm for this instant.</b> Two loops, run side by side and never merged:
        ///
        /// <list type="bullet">
        ///   <item><b>Along track</b> — <paramref name="wantedSpeed"/> against her way over the ground,
        ///   through <see cref="ArrivalPilot.ThrottleFor"/>. That is the same throttle law the approach
        ///   uses, which is what makes her go ASTERN to take the last of the way off rather than coasting
        ///   two hundred metres past the wharf.</item>
        ///   <item><b>Across track</b> — the set rate, turned into a crab angle off the berth heading and
        ///   then into helm through <see cref="ArrivalPilot.Settings.SteerPerDegree"/>. The same gain, so
        ///   a boat that steers well down a channel steers well alongside a wharf.</item>
        /// </list>
        ///
        /// <param name="lateralTargetMetres">Which line she is closing: the gate's standoff during the
        /// line-up, zero once she is coming alongside.</param>
        /// <param name="maxClosingRate">How fast she may close it — see
        /// <see cref="WantedClosingRate"/> for why this is per-phase and not the set rate everywhere.</param>
        /// </summary>
        public static ArrivalPilot.Helm Command(Vector2 position, float headingDegrees, Vector2 velocity,
                                                in Berth berth, float lateralTargetMetres,
                                                float maxClosingRate, float wantedSpeed,
                                                in Settings settings, in ArrivalPilot.Settings pilot)
        {
            float lateralError = LateralOffset(position, berth) - lateralTargetMetres;
            float wantedClosing = WantedClosingRate(lateralError, maxClosingRate, settings);
            float alongSpeed = Vector2.Dot(velocity, Forward(berth.HeadingDegrees));

            float aim = berth.HeadingDegrees
                        + ShoreSide(berth) * CrabDegrees(wantedClosing, alongSpeed, settings);

            float steer = ArrivalPilot.Wrap180(aim - headingDegrees) * pilot.SteerPerDegree;
            float throttle = ArrivalPilot.ThrottleFor(wantedSpeed, velocity.magnitude, pilot);
            return new ArrivalPilot.Helm(throttle, steer);
        }

        /// <summary>
        /// <b>Is she in the pose this phase demands?</b> Heading within
        /// <see cref="Settings.HeadingToleranceDegrees"/> of the berth's, and within
        /// <see cref="Settings.LateralToleranceMetres"/> of the line she is supposed to be on (§2.1's Gate
        /// row). Nothing about along-track: that is the station's business, not the pose's.
        /// </summary>
        public static bool WithinPose(Vector2 position, float headingDegrees, in Berth berth,
                                      float lateralTargetMetres, in Settings settings)
            => Mathf.Abs(ArrivalPilot.Wrap180(headingDegrees - berth.HeadingDegrees))
                   <= settings.HeadingToleranceDegrees
               && Mathf.Abs(LateralOffset(position, berth) - lateralTargetMetres)
                   <= settings.LateralToleranceMetres;

        /// <summary>
        /// How much run-out to ADD to the distance a route still has, so that the approach's speed curve
        /// bottoms out at <see cref="Settings.BerthingSpeedMetresPerSecond"/> at the gate instead of at
        /// zero. <c>v²/2a</c> plus the pilot's own stop band — i.e. exactly the distance
        /// <see cref="ArrivalPilot.TargetSpeed"/> would want in order to still be making the berthing
        /// speed here.
        ///
        /// <para>⭐ This is how the come-alongside reuses the approach curve rather than replacing it: the
        /// pilot is asked the same question it always answers, about a berth a little further off than
        /// the gate really is.</para>
        /// </summary>
        public static float BerthingRunoutMetres(in Settings settings, in ArrivalPilot.Settings pilot)
        {
            float v = Mathf.Max(0f, settings.BerthingSpeedMetresPerSecond);
            float a = Mathf.Max(0.01f, pilot.ApproachDecelMetresPerSecondSquared);
            return v * v / (2f * a) + Mathf.Max(0f, pilot.StopMetres);
        }

        /// <summary>The widest course change a wheel-over is planned for, degrees. <c>tan(θ/2)</c> runs
        /// away at 180°, and a route that doubles back on itself is not a corner to be cut but a mark to
        /// be gone round. Clamping here keeps the distance finite without a special case.</summary>
        public const float MaxWheelOverTurnDegrees = 150f;

        /// <summary>
        /// ⭐ <b>THE WHEEL-OVER.</b> How far BEFORE a mark she must put the helm over, so that the arc
        /// she actually turns through comes out tangent to the next leg: <c>R · tan(Δ/2)</c>, with
        /// <c>R = speed ÷ turn rate</c>. Textbook pilotage, and the number every paper passage plan
        /// carries beside its course changes.
        ///
        /// <para>🔴 <b>Why a route without it cannot be steered.</b> A pursuit controller turns AT the
        /// mark, so a hull whose turning circle is wider than the arrive radius leaves the corner
        /// displaced by most of a diameter — and pursuit then hauls her back toward a mark she has
        /// already passed. Measured on the real St Peters fairway: the last corner turns 61° at a 24 m
        /// radius, which threw her eleven metres off the channel's line and left the come-alongside
        /// trying to make up seven metres of lateral in a berth twelve metres long. She was doing exactly
        /// what she was told; what she was told did not account for her being a boat.</para>
        ///
        /// <para><b>It scales with SPEED, which is the point.</b> That same 61° corner needs 14 m of
        /// anticipation at the fairway's 5 m/s and 8 m at harbour speed — so a boat slowing into the
        /// harbour cuts less, exactly as a real one does.</para>
        /// </summary>
        /// <param name="speedMetresPerSecond">Her speed over the ground, m/s.</param>
        /// <param name="turnDegrees">The course change she has left to make onto the next leg.</param>
        public static float WheelOverMetres(float speedMetresPerSecond, float turnDegrees,
                                            in Settings settings)
        {
            float turn = Mathf.Min(Mathf.Abs(turnDegrees), MaxWheelOverTurnDegrees);
            if (turn <= 0f) return 0f;

            float rate = Mathf.Max(0.5f, settings.TurnRateDegreesPerSecond) * Mathf.Deg2Rad;
            float radius = Mathf.Max(0f, speedMetresPerSecond) / rate;
            return radius * Mathf.Tan(turn * 0.5f * Mathf.Deg2Rad);
        }
    }
}
