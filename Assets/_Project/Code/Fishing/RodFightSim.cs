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
    /// <para><b>What it doesn't.</b> No input reading (the caller passes reel/steer), no publishing
    /// (the controller emits Core <c>FishingState</c>), no presentation. The Deep phase ignores steer by
    /// construction — <see cref="RodFightMath"/> already gates the steer terms on
    /// <see cref="RodFightPhase.Surface"/>, so this class simply passes the phase through.</para>
    /// </summary>
    public sealed class RodFightSim
    {
        private readonly RodFightRhythm _rhythm;
        private readonly RodFightMovement _pattern;
        private readonly int _motionSeed;

        // The Def's six tuning floats, captured at fight start (a mid-fight asset edit can't tear the
        // fight), with the Strength dial already folded into the run pressure.
        private readonly float _rise, _fall, _fill, _effectiveRunPressure, _steerRelief, _surfaceThreshold;

        private float _surfaceSeconds;   // the choreography clock — starts when she breaks the surface

        public float Tension01 { get; private set; }
        public float Landing01 { get; private set; }
        public FishFightResult Result { get; private set; }
        public bool IsOver => Result != FishFightResult.None;

        /// <summary>Deep (timing only) or Surface (steer live) — from <see cref="RodFightMath.PhaseFor"/>;
        /// landing never falls, so the crossing is one-way.</summary>
        public RodFightPhase Phase => RodFightMath.PhaseFor(Landing01, _surfaceThreshold);

        /// <summary>Her effort THIS tick: 1 = a hard run (MAINTAIN), 0 = a slack window (PULL).</summary>
        public float Effort01 => _rhythm.Effort01;

        /// <summary>Seconds she has been up on the surface — the choreography clock
        /// <see cref="RodFightMotionMath"/> reads. 0 until the crossing.</summary>
        public float SurfaceSeconds => _surfaceSeconds;

        /// <summary>The direction of her current dart (unit vector; Surface phase choreography). In the
        /// Deep phase there is nothing to steer against — returns zero so a caller's alignment reads
        /// neutral by construction.</summary>
        public Vector2 DartDir => Phase == RodFightPhase.Surface
            ? RodFightMotionMath.DartDir(_pattern, _motionSeed, _surfaceSeconds)
            : Vector2.zero;

        /// <summary>Where she is right now, as a world-metre offset from the surface anchor (the line's
        /// entry point), roaming a disc of <paramref name="roamRadiusM"/>. Zero while Deep — the line
        /// runs straight down at the anchor until she's up.</summary>
        public Vector2 FishOffset(float roamRadiusM) => Phase == RodFightPhase.Surface
            ? RodFightMotionMath.Offset(_pattern, _motionSeed, _surfaceSeconds, roamRadiusM)
            : Vector2.zero;

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
        /// action (PULL vs MAINTAIN); <paramref name="steerAlignment"/> is the rod-vs-dart alignment
        /// (−1 opposite … +1 with, from <see cref="RodFightMotionMath.SteerAlignment"/>; ignored while
        /// Deep by the maths itself). Resolves to Snapped at tension 1, Landed at landing 1 — snap
        /// checked first, the <see cref="FishFight"/> precedent.
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
