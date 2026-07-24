using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// One live <b>v2 rod fight</b> — the deep→surface arc (design §3), as a stateful, engine-light POCO
    /// (the <see cref="FishFight"/> twin, same lane discipline: no MonoBehaviour, no <c>Time</c>, RNG
    /// injected, fully EditMode-testable; the fight is real-time and player-driven, NOT part of the
    /// world-sim determinism contract — seeding exists so tests replay a fight bit-for-bit).
    ///
    /// <para><b>What it owns.</b> The two accumulators (<see cref="Tension01"/> toward the snap,
    /// <see cref="Landing01"/> toward aboard), integrated each <see cref="Tick"/> from
    /// <see cref="RodFightMath"/>'s signed rates — <c>accum = clamp01(accum + rate·dt)</c>, exactly the
    /// caller contract that class documents; the fish's run rhythm (<see cref="RodFightRhythm"/>, from
    /// the Def's authored <see cref="StaminaCadence"/>); the surface choreography clock and seed
    /// (<see cref="RodFightMotionMath"/> is pure — this object just carries <c>(seed, t)</c> for it);
    /// and the <see cref="RodFightStrength"/>-scaled effective run pressure, so the Def's Strength dial
    /// is applied in exactly one place.</para>
    ///
    /// <para><b>What it doesn't.</b> No input reading (the caller passes reel/lean), no publishing
    /// (the controller emits Core <c>FishingState</c>), no presentation. The phase is passed straight
    /// through to <see cref="RodFightMath"/>, which no longer gates anything on it — the lean is live from
    /// the hookup (owner's ruling 2026-07-23); the phase now says only whether she can be SEEN.</para>
    /// </summary>
    public sealed class RodFightSim
    {
        private readonly RodFightRhythm _rhythm;
        private readonly RodFightMovement _pattern;
        private readonly int _motionSeed;

        // The Def's six tuning floats, captured at fight start (a mid-fight asset edit can't tear the
        // fight), with the Strength dial already folded into the run pressure.
        private readonly float _rise, _fall, _fill, _effectiveRunPressure, _steerRelief, _surfaceThreshold;

        private float _fightSeconds;     // the choreography clock — runs from the HOOKUP (she fights deep too)
        private float _surfaceSeconds;   // seconds since she broke the surface — the deep→surface roam blend

        public float Tension01 { get; private set; }
        public float Landing01 { get; private set; }
        public FishFightResult Result { get; private set; }
        public bool IsOver => Result != FishFightResult.None;

        /// <summary>Deep (timing only) or Surface (steer live) — from <see cref="RodFightMath.PhaseFor"/>;
        /// landing never falls, so the crossing is one-way.</summary>
        public RodFightPhase Phase => RodFightMath.PhaseFor(Landing01, _surfaceThreshold);

        /// <summary>Her effort THIS tick: 1 = a hard run (MAINTAIN), 0 = a slack window (PULL).</summary>
        public float Effort01 => _rhythm.Effort01;

        /// <summary>Seconds she has been up on the surface — 0 until the crossing. Drives the roam blend
        /// from her cramped deep working to her full surface choreography.</summary>
        public float SurfaceSeconds => _surfaceSeconds;

        /// <summary>Seconds since the hook was set — the choreography clock
        /// <see cref="RodFightMotionMath"/> reads. Runs through BOTH phases: she is running from the
        /// moment she's hooked, and the player is leaning against that run from the moment it starts.</summary>
        public float FightSeconds => _fightSeconds;

        /// <summary>The direction she is running RIGHT NOW (unit vector) — what the player's lean is
        /// measured against. Live in both phases (owner's call: lean against her, always). While she's
        /// deep this is what the rod's load and the line's entry point are telling you; once she's up you
        /// can see it.</summary>
        public Vector2 DartDir => RodFightMotionMath.DartDir(_pattern, _motionSeed, _fightSeconds);

        /// <summary>
        /// Where the far end of the line is right now, as a world-metre offset from the fight anchor.
        /// While she's DEEP this is the line's ENTRY POINT working around the anchor —
        /// <paramref name="deepRadiusFraction"/> of the full roam, because a fish forty feet down moves
        /// the surface entry a little, not a lot; it is the honest read of which way she's pulling, and
        /// the reason the deep half can be fought at all. Once she's up it grows to the full
        /// <paramref name="roamRadiusM"/> choreography over <see cref="RodFightMotionMath.SurfaceRampSeconds"/>,
        /// so the crossing swells rather than pops. One continuous curve either side — same pattern, same
        /// seed, same clock.
        /// </summary>
        public Vector2 FishOffset(float roamRadiusM, float deepRadiusFraction)
        {
            float deep = roamRadiusM * Mathf.Clamp01(deepRadiusFraction);
            float radius = Phase == RodFightPhase.Surface
                ? Mathf.Lerp(deep, roamRadiusM,
                             Mathf.Clamp01(_surfaceSeconds / RodFightMotionMath.SurfaceRampSeconds))
                : deep;
            return RodFightMotionMath.Offset(_pattern, _motionSeed, _fightSeconds, radius);
        }

        /// <summary>Start a fight from a species' authored personality. <paramref name="rng"/> seeds
        /// both the run rhythm and the surface choreography — a seeded controller replays the whole
        /// fight identically (tests); null falls back to time-seeded.</summary>
        public RodFightSim(RodFightDef def, System.Random rng)
        {
            rng ??= new System.Random();
            _rhythm = new RodFightRhythm(def.StaminaCadence, rng);
            _pattern = def.MovementPattern;
            _motionSeed = rng.Next();

            _rise = def.tensionRisePerSec;
            _fall = def.tensionFallPerSec;
            _fill = def.landingFillPerSec;
            _effectiveRunPressure = RodFightStrength.EffectiveRunPressure(def.runTensionPressure, def.Strength);
            _steerRelief = def.counterSteerRelief;
            _surfaceThreshold = def.surfaceThreshold01;
        }

        /// <summary>
        /// Advance the fight by <paramref name="dt"/> seconds. <paramref name="reeling"/> is the held
        /// action (REEL vs EASE OFF); <paramref name="steerAlignment"/> is the rod-vs-run alignment
        /// (−1 leaning against her … +1 going with her, from
        /// <see cref="RodFightMotionMath.SteerAlignment"/>) and counts in both phases. Resolves to Snapped
        /// at tension 1, Landed at landing 1 — snap checked first, the <see cref="FishFight"/> precedent.
        /// </summary>
        public void Tick(float dt, bool reeling, float steerAlignment)
            => Tick(dt, reeling, steerAlignment, deckAnglePressurePerSec: 0f);

        /// <summary>
        /// The full tick including the <b>deck-angle pressure</b> (Rod Fishing v2 Wave 4 — design §4.2):
        /// the caller measures the stance (<see cref="DeckAngleMath"/> against the live deck frame) and
        /// hands the resulting ≥ 0 rate in; the maths adds it to the tension side only. Passing 0 (the
        /// dock, a clean rail, the owner's factor at 0) makes this bit-for-bit the three-arg tick.
        /// </summary>
        public void Tick(float dt, bool reeling, float steerAlignment, float deckAnglePressurePerSec)
        {
            if (IsOver || float.IsNaN(dt) || dt <= 0f) return;

            _rhythm.Tick(dt);
            _fightSeconds += dt;            // she runs from the hookup — the lean has something to read at once
            RodFightPhase phase = Phase;
            float effort = _rhythm.Effort01;

            float tensionRate = RodFightMath.TensionRatePerSec(reeling, effort, steerAlignment, phase,
                _rise, _fall, _effectiveRunPressure, _steerRelief, deckAnglePressurePerSec);
            float landingRate = RodFightMath.LandingRatePerSec(reeling, effort, steerAlignment, phase,
                _fill);

            Tension01 = Mathf.Clamp01(Tension01 + tensionRate * dt);
            Landing01 = Mathf.Clamp01(Landing01 + landingRate * dt);

            if (Phase == RodFightPhase.Surface) _surfaceSeconds += dt;

            if (Tension01 >= 1f) Result = FishFightResult.Snapped;
            else if (Landing01 >= 1f) Result = FishFightResult.Landed;
        }
    }
}
