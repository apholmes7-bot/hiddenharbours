using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>Where one bite sequence is right now — what presentation renders and the strike judges
    /// against. ONE shared state: the tell that shows a phase and the resolver that judges a strike
    /// read this same enum off the same sim (the flick-cast lesson — never two computations of one
    /// quantity).</summary>
    public enum BitePhase
    {
        /// <summary>She's near the hook but showing nothing — the quiet between events. Striking now
        /// is EARLY (you struck at nothing) and ends the pass.</summary>
        Approach = 0,
        /// <summary>A teasing FALSE NIBBLE is showing (the small dip). Striking now is EARLY — the
        /// false-nibble game is not striking on these.</summary>
        NibbleDip = 1,
        /// <summary>THE TRUE TAKE — the strike window is open. Strike now and she's hooked.</summary>
        TakeOpen = 2,
        /// <summary>A missed pass: she's circling back for another look (the owner's "It keeps
        /// nibbling"). No tell shows; the next pass starts when she returns.</summary>
        Away = 3,
        /// <summary>Hooked — hand off to the fight. Terminal.</summary>
        Hooked = 4,
        /// <summary>She's lost interest (passes exhausted, or she didn't return). Terminal.</summary>
        Gone = 5,
    }

    /// <summary>What the player's last mistimed strike was — feedback for the presentation/toast
    /// (the tell of WHY the pass ended), never a judgement input.</summary>
    public enum BiteMiss
    {
        None = 0,
        /// <summary>Struck outside the take — at nothing, or on a teasing false nibble.</summary>
        Early = 1,
        /// <summary>Slept through the open take window.</summary>
        Late = 2,
    }

    /// <summary>
    /// One live BITE SEQUENCE — the nibble/take timing core of the §10 owner drop, as a stateful,
    /// engine-light POCO (the <see cref="RodFightSim"/> twin: no MonoBehaviour, no <c>Time</c>, RNG
    /// injected, fully EditMode-testable; real-time and player-driven, NOT part of the world-sim
    /// determinism contract — seeding exists so tests replay a sequence bit-for-bit).
    ///
    /// <para><b>The shape of a pass.</b> Quiet approach → 0..N teasing false nibbles (each a brief
    /// dip, gaps rolled per event) → the TRUE take, strikeable for the species' window. Strike inside
    /// the window → <see cref="BitePhase.Hooked"/>. Strike anywhere else (EARLY — the false-nibble
    /// game) or let the window lapse (LATE) and the pass ends — then, per the owner's ruling, "It
    /// keeps nibbling": she may return for another pass (<c>ReturnChance01</c>, up to
    /// <c>MaxPasses</c>), else <see cref="BitePhase.Gone"/>.</para>
    ///
    /// <para><b>What it doesn't own.</b> No input reading (the caller passes the strike edge — WHAT
    /// gesture makes a strike is slice 3's business), no publishing, no presentation, no bait economy
    /// (the spend stays the controller's). All schedule randomness comes from the injected rng in
    /// event order, so same seed + same strike times = the identical sequence.</para>
    /// </summary>
    public sealed class BiteSequenceSim
    {
        private readonly BiteProfile _profile;
        private readonly System.Random _rng;

        private int _passIndex;          // 0-based; bounded by _profile.MaxPasses
        private int _nibblesThisPass;    // rolled at pass start
        private int _nibbleIndex;        // how many teases have SHOWN this pass
        private float _phaseSeconds;     // time inside the current phase
        private float _phaseLength;      // current phase's duration (gap / dip / window / return delay)

        public BitePhase Phase { get; private set; }

        /// <summary>The last mistimed strike (feedback only). Reset to None when a new pass starts.</summary>
        public BiteMiss LastMiss { get; private set; }

        /// <summary>0-based pass number (0 = the first approach). Bounded by the profile's MaxPasses.</summary>
        public int PassIndex => _passIndex;

        /// <summary>How many false nibbles have shown this pass (the presentation's dip counter).</summary>
        public int NibbleIndex => _nibbleIndex;

        /// <summary>Seconds inside the current phase — presentation reads dips/windows off this.</summary>
        public float PhaseSeconds => _phaseSeconds;

        /// <summary>How far through the OPEN take window we are, 0..1 (1 = about to lapse). 0 outside
        /// <see cref="BitePhase.TakeOpen"/>. An urgency read for audio/presentation.</summary>
        public float TakeWindow01
            => Phase == BitePhase.TakeOpen ? Mathf.Clamp01(_phaseSeconds / _profile.TakeWindowSeconds) : 0f;

        public bool IsOver => Phase == BitePhase.Hooked || Phase == BitePhase.Gone;

        /// <summary>Start a sequence. <paramref name="rng"/> seeds every schedule roll — a seeded
        /// caller replays the whole sequence identically (tests); null falls back to time-seeded.</summary>
        public BiteSequenceSim(in BiteProfile profile, System.Random rng)
        {
            _profile = profile;
            _rng = rng ?? new System.Random();
            BeginPass();
        }

        /// <summary>
        /// Advance the sequence. <paramref name="strike"/> is the strike EDGE this tick (pressed this
        /// frame — the caller owns what gesture that is). The strike is judged BEFORE time advances,
        /// so a press inside a window is honoured even on the tick the window would lapse.
        /// </summary>
        public void Tick(float dt, bool strike)
        {
            if (IsOver) return;

            if (strike && Phase != BitePhase.Away)
            {
                if (Phase == BitePhase.TakeOpen) { Phase = BitePhase.Hooked; return; }
                EndPass(BiteMiss.Early);                     // struck at nothing, or on a tease
                return;
            }

            _phaseSeconds += Mathf.Max(0f, dt);
            if (_phaseSeconds < _phaseLength) return;

            switch (Phase)
            {
                case BitePhase.Approach:
                    // The gap is over: the next event shows — another tease, or the true take.
                    if (_nibbleIndex < _nibblesThisPass) EnterPhase(BitePhase.NibbleDip, _profile.NibbleDipSeconds);
                    else EnterPhase(BitePhase.TakeOpen, _profile.TakeWindowSeconds);
                    break;

                case BitePhase.NibbleDip:
                    _nibbleIndex++;
                    EnterPhase(BitePhase.Approach, RollGap());
                    break;

                case BitePhase.TakeOpen:
                    EndPass(BiteMiss.Late);                  // she spat it — the window lapsed
                    break;

                case BitePhase.Away:
                    BeginPass();                             // she's back for another look
                    break;
            }
        }

        // ---- pass lifecycle -----------------------------------------------------------------------

        private void BeginPass()
        {
            LastMiss = BiteMiss.None;
            _nibbleIndex = 0;
            _nibblesThisPass = _rng.Next(_profile.FalseNibblesMin, _profile.FalseNibblesMax + 1);
            EnterPhase(BitePhase.Approach, RollGap());
        }

        private void EndPass(BiteMiss miss)
        {
            LastMiss = miss;
            _passIndex++;

            // "It keeps nibbling" — she may come back for another pass, bounded by MaxPasses.
            bool returns = _passIndex < _profile.MaxPasses
                           && _rng.NextDouble() < _profile.ReturnChance01;
            if (returns)
                EnterPhase(BitePhase.Away, RollRange(_profile.ReturnDelayMinSeconds,
                                                     _profile.ReturnDelayMaxSeconds));
            else
                Phase = BitePhase.Gone;
        }

        private void EnterPhase(BitePhase phase, float length)
        {
            Phase = phase;
            _phaseSeconds = 0f;
            _phaseLength = Mathf.Max(0.01f, length);
        }

        private float RollGap() => RollRange(_profile.NibbleGapMinSeconds, _profile.NibbleGapMaxSeconds);

        private float RollRange(float min, float max)
            => max <= min ? min : min + (float)_rng.NextDouble() * (max - min);
    }
}
