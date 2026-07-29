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

        // The Def's tuning floats, captured at fight start (a mid-fight asset edit can't tear the
        // fight), with the Strength dial already folded into the run pressure.
        private readonly float _rise, _fall, _fill, _effectiveRunPressure, _steerRelief, _surfaceThreshold;
        private readonly float _giveBack, _maxGiveBack;
        private readonly float _roamScale, _holdsDeep01;

        // The high-water mark of how landed she has ever been. She may be dragged back from it, but never
        // by more than _maxGiveBack — "you always keep most of what you won" (owner's ruling 2026-07-23).
        private float _landingHighWater;

        private float _fightSeconds;     // the choreography clock — runs from the HOOKUP (she fights deep too)
        private float _surfaceSeconds;   // seconds since she broke the surface — the deep→surface roam blend

        public float Tension01 { get; private set; }
        public float Landing01 { get; private set; }
        public FishFightResult Result { get; private set; }
        public bool IsOver => Result != FishFightResult.None;

        /// <summary>Deep (unseen) or Surface (visible) — from <see cref="RodFightMath.PhaseFor"/>, read off
        /// the HIGH-WATER landing rather than the live one. Landing can now fall (she takes line back), so
        /// reading it live would let a surfaced fish sink out of sight and pop up again, flickering the
        /// whole presentation. Once she has shown herself she stays shown: the crossing remains strictly
        /// one-way, which is the long-standing contract the tests pin.</summary>
        public RodFightPhase Phase => RodFightMath.PhaseFor(_landingHighWater, _surfaceThreshold);

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
            _giveBack = def.lineGiveBackPerSec;
            _maxGiveBack = def.maxGiveBack01;
            _roamScale = def.RoamRadiusScale;
            _holdsDeep01 = def.HoldsDeep01;
        }

        /// <summary>
        /// How far she can be dragged back RIGHT NOW: her high-water landing less the Def's
        /// <c>maxGiveBack01</c>, never below 0. The "only a little" promise made concrete — she can take
        /// line during a run, but ground you have truly won is yours (owner's ruling 2026-07-23).
        /// </summary>
        public float LandingFloor01 => Mathf.Max(0f, _landingHighWater - Mathf.Max(0f, _maxGiveBack));

        /// <summary>
        /// <b>How close she has been brought in</b>, 0 (out where she was hooked) → 1 (alongside), which is
        /// the diegetic replacement for the landing bar: the caller walks the fight anchor in along this,
        /// so REELING VISIBLY DRAGS HER TOWARD YOU and a run that takes line pushes her back out.
        ///
        /// <para>Eased by <c>HoldsDeep01</c>, the Def's stubbornness: a bulldog that "holds deep" makes you
        /// win most of the gauge before she gives any real ground, then comes in a rush at the end; a
        /// come-easy fish tracks the gauge almost linearly. Same <see cref="Landing01"/>, very different
        /// journey — this is a big part of what makes the species feel like different animals.</para>
        /// </summary>
        public float Approach01
        {
            get
            {
                float landed = Mathf.Clamp01(Landing01);
                float hold = Mathf.Clamp01(_holdsDeep01);
                // hold = 0 → linear; hold = 1 → she barely moves until the gauge is nearly full.
                return Mathf.Lerp(landed, landed * landed * landed, hold);
            }
        }

        /// <summary>The Def's on-screen footprint multiplier — how much water this species works while
        /// she fights (a bulldog barely travels; a mackerel ranges wide). Applied by the caller to its
        /// roam radius so the dial stays data, not a hard-coded per-species constant.</summary>
        public float RoamRadiusScale => _roamScale;

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
            => Tick(dt, reeling, steerAlignment, deckAnglePressurePerSec, seaPressurePerSec: 0f);

        /// <summary>
        /// The full tick including <b>the sea's own pressure</b> (owner's ruling 2026-07-25 — Pillar 1
        /// finally reaching the rod loop): <paramref name="seaPressurePerSec"/> is the swell working the
        /// rod and snatching at the line, computed by the pure <see cref="SeaFightMath.TensionPerSec"/>
        /// from the LIVE sea state and handed in per tick exactly as the deck term is. It loads the line
        /// only — it never touches landing — and at 0 (a flat calm, or the owner's factor off) this is
        /// bit-for-bit the four-arg tick, which is the A/B baseline the tests pin.
        /// </summary>
        public void Tick(float dt, bool reeling, float steerAlignment, float deckAnglePressurePerSec,
                         float seaPressurePerSec)
        {
            if (IsOver || float.IsNaN(dt) || dt <= 0f) return;

            _rhythm.Tick(dt);
            _fightSeconds += dt;            // she runs from the hookup — the lean has something to read at once
            RodFightPhase phase = Phase;
            float effort = _rhythm.Effort01;

            // The sea rides the SAME additive seam the deck stance does — both are outside forces loading
            // the line, neither is anything the angler did wrong, and summing them is what lets a bad
            // stance in a bad sea be genuinely dangerous while either alone stays manageable.
            float tensionRate = RodFightMath.TensionRatePerSec(reeling, effort, steerAlignment, phase,
                _rise, _fall, _effectiveRunPressure, _steerRelief,
                deckAnglePressurePerSec + Mathf.Max(0f, seaPressurePerSec));
            float landingRate = RodFightMath.LandingRatePerSec(reeling, effort, steerAlignment, phase,
                _fill, _giveBack);

            Tension01 = Mathf.Clamp01(Tension01 + tensionRate * dt);

            // Landing can now FALL (she takes line) — but never below the high-water floor, so a bad run
            // costs you a little ground and never the fight you have already fought.
            Landing01 = Mathf.Clamp(Landing01 + landingRate * dt, LandingFloor01, 1f);
            if (Landing01 > _landingHighWater) _landingHighWater = Landing01;

            if (Phase == RodFightPhase.Surface) _surfaceSeconds += dt;

            if (Tension01 >= 1f) Result = FishFightResult.Snapped;
            else if (Landing01 >= 1f) Result = FishFightResult.Landed;
        }
    }
}
