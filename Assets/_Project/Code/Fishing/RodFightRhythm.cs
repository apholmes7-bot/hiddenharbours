namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The fish's <b>run↔slack rhythm</b> for one rod fight (Rod Fishing v2, design §3/§5) — the
    /// generator of the <c>fishEffort01</c> stream <see cref="RodFightMath"/> reads: <b>1 while she
    /// RUNS</b> (she's fighting — MAINTAIN, steer against her), <b>0 while she goes SLACK</b> (she's
    /// tiring — PULL now to gain). The cadence comes straight from the species' authored
    /// <see cref="StaminaCadence"/>: each run lasts about <c>RunSeconds</c>, each slack about
    /// <c>SlackSeconds</c>, and every phase length is jittered by ±<c>Jitter01</c>·100% so the rhythm
    /// never feels metronomic — the exact jitter shape <see cref="FishFight"/>'s <c>NextSurgeDelay</c>
    /// established (deterministic under a seeded RNG, varied in play).
    ///
    /// <para><b>The FishFight lane discipline:</b> an engine-light, stateful POCO — no MonoBehaviour, no
    /// <c>Time</c>, RNG injected — so a seeded fight replays bit-for-bit in an EditMode test. The fight
    /// is real-time and player-driven, NOT part of the world-sim determinism contract (it never reads the
    /// world seed/clock); the seeding exists so tests can pin the rhythm, exactly as FishFight's surge
    /// timing is pinned.</para>
    ///
    /// <para><b>She's running when hooked.</b> A fight opens mid-RUN — the strike is the moment she
    /// bolts — so the first thing the player does is MAINTAIN, which teaches the loop's core lesson
    /// (a run is a "back off" tell) before the first slack window ever opens.</para>
    /// </summary>
    public sealed class RodFightRhythm
    {
        /// <summary>Floor under any jittered phase length (s) — the same lower bound
        /// <see cref="StaminaCadence"/>'s fields declare (<c>[Min(0.05f)]</c>), applied here so a full
        /// ±100% jitter can never roll a zero-length phase and stall the rhythm. A guard, not a feel dial.</summary>
        public const float MinPhaseSeconds = 0.05f;

        private readonly float _runSeconds;
        private readonly float _slackSeconds;
        private readonly float _jitter01;
        private readonly System.Random _rng;

        private bool _running;
        private float _remaining;   // seconds left in the current phase

        /// <summary>True while she's mid-run (effort 1); false in a slack window (effort 0).</summary>
        public bool IsRunning => _running;

        /// <summary>The effort stream <see cref="RodFightMath"/> reads this tick: 1 = a hard run,
        /// 0 = a slack window. (The binary read keeps the pull/maintain tell crisp — presentation
        /// smooths its own eases; see <see cref="RodFightMath.SlackWindowOpen01"/>.)</summary>
        public float Effort01 => _running ? 1f : 0f;

        /// <summary>Start the rhythm mid-RUN (the strike is the moment she bolts). <paramref name="rng"/>
        /// is injected so a seeded fight replays identically (tests); null falls back to time-seeded.</summary>
        public RodFightRhythm(in StaminaCadence cadence, System.Random rng)
        {
            _runSeconds = cadence.RunSeconds;
            _slackSeconds = cadence.SlackSeconds;
            _jitter01 = cadence.Jitter01 < 0f ? 0f : (cadence.Jitter01 > 1f ? 1f : cadence.Jitter01);
            _rng = rng ?? new System.Random();
            _running = true;
            _remaining = NextPhaseSeconds(_runSeconds);
        }

        /// <summary>Advance the rhythm by <paramref name="dt"/> seconds, flipping run↔slack as phases
        /// expire. Loops within the step so one large dt can cross several phases without losing any
        /// (headless tests inject coarse dt — PlayMode frame counts are not time).</summary>
        public void Tick(float dt)
        {
            if (float.IsNaN(dt) || dt <= 0f) return;
            while (dt >= _remaining)
            {
                dt -= _remaining;
                _running = !_running;
                _remaining = NextPhaseSeconds(_running ? _runSeconds : _slackSeconds);
            }
            _remaining -= dt;
        }

        // The authored length ± Jitter01·100%, uniformly — the FishFight NextSurgeDelay jitter shape
        // widened to the Def's own dial (0 = metronomic, 1 = anywhere in 0..2×). Floored so a full
        // jitter can never produce a degenerate phase.
        private float NextPhaseSeconds(float baseSeconds)
        {
            float span = 1f + _jitter01 * (2f * (float)_rng.NextDouble() - 1f);
            float len = baseSeconds * span;
            return len < MinPhaseSeconds ? MinPhaseSeconds : len;
        }
    }
}
