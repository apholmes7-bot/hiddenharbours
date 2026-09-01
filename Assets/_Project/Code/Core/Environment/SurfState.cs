using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What the broken water is doing where a hull is floating</b> (ADR 0040, PR 3) — the one read
    /// the seakeeping force takes, composed by <see cref="BreakerMath.SurfAt"/> so the shove the boat
    /// feels is assembled from exactly the terms the renderer draws, rather than a lookalike built on
    /// the Boats side. One field, two readers: the same discipline <see cref="SharedWaveField"/> exists
    /// to enforce, one layer out.
    ///
    /// <para><b>⚠️⚠️ Why the surf force does NOT use <c>SeakeepingForcesMath.Exposure01</c>, and must
    /// not.</b> That exposure is a DEPTH RAMP — 0 in shallow water, 1 offshore — because the open sea's
    /// swell is what it models, and a hull tucked into shallow water is genuinely sheltered from swell.
    /// Surf is the opposite phenomenon: it exists <em>only</em> in shallow water. At the shipped tuning
    /// the break depth is 0.92 m and the shelter depth is 1 m, so exposure at the break line is
    /// <b>exactly 0</b> — routing the surf shove through it would multiply the whole feature away
    /// precisely where it is supposed to act.
    ///
    /// <para>The surf term's own place-gate is <see cref="Breaking01"/>, which is a better one: it is 0
    /// everywhere the sea is not breaking, which includes all calm water, all water too deep to break,
    /// and every sheltered corner where the waves never reach the criterion. The charter's requirement —
    /// "calm and sheltered water unchanged" — is met more strictly this way than by a depth ramp, and
    /// glass comes free because a glass sea has no contour at all.</para>
    ///
    /// <para><b>The bore (ADR 0040 revision 3).</b> <see cref="Bore01"/> is the surf's CLOCK: 1 as a
    /// crest's bore front passes this position, falling to a quiet between crests, one pulse per wave
    /// period, running shoreward at the bore speed. It is a read of the field's PUBLISHED phase at the
    /// break line, carried inshore by the march's own travel time — never accumulated, never
    /// reconstructed. Consumers that pulse with the sea (the shove, the deposit, the run-up, audio) read
    /// it; the steady terms above are what they had before it existed. Readable by any lane.</para>
    /// </summary>
    public readonly struct SurfState
    {
        /// <summary>Water depth here (metres). Positive; a dry or aground position never produces a
        /// surf state at all.</summary>
        public readonly float DepthMeters;

        /// <summary>Unit direction the broken water is running — shoreward, from the painted bed's
        /// gradient. This is the direction the bore shoves. Zero only on a flat bed, where
        /// <see cref="BreakerMath.SurfAt"/> returns <see cref="Calm"/> instead.</summary>
        public readonly Vector2 ShorewardDirection;

        /// <summary>0..1: is the sea breaking here? <b>The surf force's place-gate</b> — see the class
        /// note on why this and not the swell's depth-ramp exposure.</summary>
        public readonly float Breaking01;

        /// <summary>0..1: how much of the bore's energy survives here — 1 in the boil at the break line,
        /// decaying shoreward on a real clock. The shove scales with this, so a hull sitting in dead
        /// foam at the top of the beach is barely pushed.</summary>
        public readonly float Whitewater01;

        /// <summary>The height the broken wave actually stands at here (metres) — <c>γ·d</c>, the
        /// depth-limited height. A bore is only as tall as the water it is running over, and it is that
        /// height that does the shoving, not the deep-water one.</summary>
        public readonly float StandingHeightMeters;

        /// <summary>0..1: how plunging this break is. The pocket — young, violent, plunging water — is
        /// where a boat gets slewed, so the broach torque keys on this.</summary>
        public readonly float PlungingWeight01;

        /// <summary>0..1: <b>the bore's pulse</b> — 1 as a crest's bore front passes here, a quiet
        /// between crests, one pulse per wave period, advancing shoreward at <c>√(g·d)</c>. Already
        /// scaled by <see cref="BirthEnergy01"/>, so a set's big wave is a stronger bore. <b>1 everywhere
        /// (steady state) when the read had no phase to consult</b> — the pre-bore surf, exactly.</summary>
        public readonly float Bore01;

        /// <summary>The bore's phase here, degrees in [0, 360): the field's PUBLISHED phase at the break
        /// line this bore was born on, read forward at minus its travel time. 90 is the front (the
        /// crest). Not a reconstruction — <c>atan2</c> of a sampled surface is not a phase.</summary>
        public readonly float BorePhaseDegrees;

        /// <summary>Seconds the bore has been running since it broke — the march's own integral of
        /// <c>Δs / √(g·d)</c> over the same taps that measure the metres. Derived from geometry, never
        /// accumulated (the decaying-quantity law).</summary>
        public readonly float TravelSeconds;

        /// <summary>0..1: how big a crest this bore was born from — the field's crest factor at the break
        /// line at the moment it broke. The groups in the field make sets of big and small bores here.</summary>
        public readonly float BirthEnergy01;

        /// <summary>Metres of LEVEL the wash reaches above still water here — Hunt's run-up from the bore's
        /// remaining energy over the local surf similarity, pulsing with <see cref="Bore01"/>, capped at
        /// the drawn-edge ceiling. The renderer turns it into a contour excursion through the local slope;
        /// the gameplay waterline never reads it.</summary>
        public readonly float RunUpMeters;

        /// <summary>The steady state — the read a consumer without a published field takes. The bore
        /// reads 1 (no pulse) and the run-up 0, so nothing that consumed the surf before revision 3
        /// changes.</summary>
        public SurfState(float depthMeters, Vector2 shorewardDirection, float breaking01,
                         float whitewater01, float standingHeightMeters, float plungingWeight01)
            : this(depthMeters, shorewardDirection, breaking01, whitewater01, standingHeightMeters,
                   plungingWeight01, bore01: 1f, borePhaseDegrees: 0f, travelSeconds: 0f,
                   birthEnergy01: 1f, runUpMeters: 0f)
        {
        }

        public SurfState(float depthMeters, Vector2 shorewardDirection, float breaking01,
                         float whitewater01, float standingHeightMeters, float plungingWeight01,
                         float bore01, float borePhaseDegrees, float travelSeconds,
                         float birthEnergy01, float runUpMeters)
        {
            DepthMeters = depthMeters;
            ShorewardDirection = shorewardDirection;
            Breaking01 = breaking01;
            Whitewater01 = whitewater01;
            StandingHeightMeters = standingHeightMeters;
            PlungingWeight01 = plungingWeight01;
            Bore01 = bore01;
            BorePhaseDegrees = borePhaseDegrees;
            TravelSeconds = travelSeconds;
            BirthEnergy01 = birthEnergy01;
            RunUpMeters = runUpMeters;
        }

        /// <summary>True when there is live broken water here to push a hull about.</summary>
        public bool IsWorking => Breaking01 > 0f && Whitewater01 > 0f
                              && ShorewardDirection != Vector2.zero;

        /// <summary>No surf — deep water, calm water, dry ground, or a glass sea. Equivalent to
        /// <c>default</c>, and the state that makes the force exactly zero (the bore reads 0 too: no
        /// surf, no pulse).</summary>
        public static readonly SurfState Calm = default;
    }
}
