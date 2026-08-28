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

        public SurfState(float depthMeters, Vector2 shorewardDirection, float breaking01,
                         float whitewater01, float standingHeightMeters, float plungingWeight01)
        {
            DepthMeters = depthMeters;
            ShorewardDirection = shorewardDirection;
            Breaking01 = breaking01;
            Whitewater01 = whitewater01;
            StandingHeightMeters = standingHeightMeters;
            PlungingWeight01 = plungingWeight01;
        }

        /// <summary>True when there is live broken water here to push a hull about.</summary>
        public bool IsWorking => Breaking01 > 0f && Whitewater01 > 0f
                              && ShorewardDirection != Vector2.zero;

        /// <summary>No surf — deep water, calm water, dry ground, or a glass sea. Equivalent to
        /// <c>default</c>, and the state that makes the force exactly zero.</summary>
        public static readonly SurfState Calm = default;
    }
}
