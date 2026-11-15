using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Where a seam fade is (<see cref="SeamCrossingFade"/>).</summary>
    public enum SeamFadePhase
    {
        /// <summary>No crossing near: the cover is clear.</summary>
        Idle,
        /// <summary>The mover is about to cross: the screen is going black.</summary>
        Out,
        /// <summary>Black, waiting for the mover to reach the crossing point (bounded by the max black).</summary>
        Black,
        /// <summary>The hop has been made and the new region is becoming live: held black.</summary>
        Arriving,
        /// <summary>Arrived (or given up): the screen is clearing.</summary>
        In,
    }

    /// <summary>
    /// ⭐ <b>The momentary fade over a world-frame crossing</b> (owner ruling on PR 1223, ADR 0057): "the
    /// camera should stop as the player runs towards the edge then crosses at the edge with a momentary
    /// fade out and screen change". A PURE state machine, stepped by its owner with a delta time, so the
    /// whole sequence is provable headless; the travel rig drives it and publishes <see cref="Alpha"/> to
    /// the one screen fade through <see cref="GameServices.SeamCoverAlpha"/>.
    ///
    /// <para><b>The sequence.</b> <see cref="Begin"/> (the mover is about to cross) → <see cref="SeamFadePhase.Out"/>
    /// ramps to black over the fade-out → <see cref="SeamFadePhase.Black"/> waits, at most the max black, for
    /// the hop to be allowed (<see cref="ReadyToHop"/>) → <see cref="Hopped"/> → <see cref="SeamFadePhase.Arriving"/>
    /// holds black while the scene changes → <see cref="Arrived"/> → <see cref="SeamFadePhase.In"/> clears over
    /// the fade-in → <see cref="SeamFadePhase.Idle"/>. <see cref="Cancel"/> (the mover turned away or stopped,
    /// or the hop failed) clears from wherever the ramp had got to.</para>
    ///
    /// <para><b>The skipped step.</b> The hop's frame is the expensive one (the neighbour's roots switching
    /// on), so the step AFTER <see cref="Arrived"/> carries that whole frame's delta. Fed to the fade-in, it
    /// would clear the screen in one frame and the fade-in would never be seen; it is dropped, and the
    /// fade-in starts counting on the first ordinary frame.</para>
    ///
    /// <para>Nothing here reads a clock or the world: the delta is the caller's (scaled time, so a paused
    /// game pauses the fade with the mover), and the sim clock never stops for it.</para>
    /// </summary>
    public sealed class SeamCrossingFade
    {
        float _outSeconds = GameConfig.DefaultRegionSeamFadeOutSeconds;
        float _inSeconds = GameConfig.DefaultRegionSeamFadeInSeconds;
        float _maxBlackSeconds = GameConfig.DefaultRegionSeamMaxBlackSeconds;
        float _blackElapsed;
        bool _skipNextStep;

        /// <summary>Where the sequence is.</summary>
        public SeamFadePhase Phase { get; private set; } = SeamFadePhase.Idle;

        /// <summary>How black the cover is (0 clear .. 1 black).</summary>
        public float Alpha { get; private set; }

        /// <summary>True when the last clear began because the black waited past its limit.</summary>
        public bool TimedOut { get; private set; }

        /// <summary>The screen is fully black and nothing has hopped yet: the crossing may be made now.</summary>
        public bool ReadyToHop => Phase == SeamFadePhase.Black;

        /// <summary>A fade is going black or holding black (the hop has not been made).</summary>
        public bool IsCovering => Phase == SeamFadePhase.Out || Phase == SeamFadePhase.Black;

        /// <summary>Set the timings (s). Negative values read as zero (an instant ramp).</summary>
        public void Configure(float outSeconds, float inSeconds, float maxBlackSeconds)
        {
            _outSeconds = Mathf.Max(0f, outSeconds);
            _inSeconds = Mathf.Max(0f, inSeconds);
            _maxBlackSeconds = Mathf.Max(0f, maxBlackSeconds);
        }

        /// <summary>Start going black, from the current alpha (a clearing fade turns round without a flash).
        /// Only from <see cref="SeamFadePhase.Idle"/> or <see cref="SeamFadePhase.In"/>; returns whether it
        /// started.</summary>
        public bool Begin()
        {
            if (Phase != SeamFadePhase.Idle && Phase != SeamFadePhase.In) return false;
            Phase = SeamFadePhase.Out;
            TimedOut = false;
            _skipNextStep = false;
            _blackElapsed = 0f;
            if (_outSeconds <= 0f) GoBlack();
            return true;
        }

        /// <summary>The crossing has been made under the black: hold it while the scene changes. Only from
        /// <see cref="SeamFadePhase.Black"/>; returns whether it took.</summary>
        public bool Hopped()
        {
            if (Phase != SeamFadePhase.Black) return false;
            Phase = SeamFadePhase.Arriving;
            Alpha = 1f;
            return true;
        }

        /// <summary>The new region is live: clear, dropping the next step's delta (the hop frame's).
        /// Only from <see cref="SeamFadePhase.Arriving"/>; returns whether it took.</summary>
        public bool Arrived()
        {
            if (Phase != SeamFadePhase.Arriving) return false;
            Phase = SeamFadePhase.In;
            Alpha = 1f;
            _skipNextStep = true;
            return true;
        }

        /// <summary>Give up (the mover turned away or stopped, or the hop could not be made): clear from the
        /// current alpha. From <see cref="SeamFadePhase.Out"/>, <see cref="SeamFadePhase.Black"/> or
        /// <see cref="SeamFadePhase.Arriving"/>; returns whether it took.</summary>
        public bool Cancel()
        {
            if (!IsCovering && Phase != SeamFadePhase.Arriving) return false;
            Phase = SeamFadePhase.In;
            _skipNextStep = false;
            if (_inSeconds <= 0f) Clear();
            return true;
        }

        /// <summary>Drop everything, at once: clear and idle (the owner was switched off or torn down).</summary>
        public void Reset()
        {
            Phase = SeamFadePhase.Idle;
            Alpha = 0f;
            TimedOut = false;
            _skipNextStep = false;
            _blackElapsed = 0f;
        }

        /// <summary>Advance by <paramref name="dt"/> seconds (negative reads as zero).</summary>
        public void Tick(float dt)
        {
            dt = Mathf.Max(0f, dt);
            switch (Phase)
            {
                case SeamFadePhase.Out:
                    Alpha = _outSeconds <= 0f ? 1f : Mathf.Min(1f, Alpha + dt / _outSeconds);
                    if (Alpha >= 1f) GoBlack();
                    break;
                case SeamFadePhase.Black:
                    _blackElapsed += dt;
                    if (_blackElapsed > _maxBlackSeconds)
                    {
                        Cancel();
                        TimedOut = true;
                    }
                    break;
                case SeamFadePhase.Arriving:
                    Alpha = 1f;
                    break;
                case SeamFadePhase.In:
                    if (_skipNextStep) { _skipNextStep = false; break; }
                    Alpha = _inSeconds <= 0f ? 0f : Mathf.Max(0f, Alpha - dt / _inSeconds);
                    if (Alpha <= 0f) Clear();
                    break;
            }
        }

        void GoBlack()
        {
            Alpha = 1f;
            Phase = SeamFadePhase.Black;
            _blackElapsed = 0f;
        }

        void Clear()
        {
            Alpha = 0f;
            Phase = SeamFadePhase.Idle;
        }

        /// <summary>
        /// Where a mover will be, for the purpose of starting the fade: its position, plus the distance it
        /// covers during the fade-out, plus a <paramref name="leadMetres"/> margin along its heading for its
        /// size (a boat is long). False, with <paramref name="ahead"/> = the position, when it is slower than
        /// <paramref name="minSpeed"/>: a mover standing at the edge starts nothing. PURE.
        ///
        /// <para>The rig asks the world frame whether <paramref name="ahead"/> would cross. If it would, the
        /// fade starts now, while the mover is still short of the crossing point by
        /// <c>speed × fadeOut + lead</c>, so the screen is black by the time it gets there.</para>
        /// </summary>
        public static bool TryLookAhead(Vector2 position, Vector2 velocity, float fadeOutSeconds,
                                        float leadMetres, float minSpeed, out Vector2 ahead)
        {
            float speed = velocity.magnitude;
            if (speed <= 0f || speed < minSpeed)
            {
                ahead = position;
                return false;
            }
            ahead = position + velocity * Mathf.Max(0f, fadeOutSeconds) + velocity / speed * Mathf.Max(0f, leadMetres);
            return true;
        }

        /// <summary>
        /// How far INSIDE the region's edge a mover heading straight out at <paramref name="speed"/> is
        /// when its fade starts: the look-ahead (<c>speed × fadeOut + lead</c>) less the hysteresis band it
        /// must still pass beyond the edge. Positive = its centre is still on screen (the camera stops at
        /// the edge). PURE; the number the PR's measurements are checked against.
        /// </summary>
        public static float FadeStartInsetMetres(float speed, float fadeOutSeconds, float leadMetres,
                                                 float hysteresisMetres)
            => Mathf.Max(0f, speed) * Mathf.Max(0f, fadeOutSeconds) + Mathf.Max(0f, leadMetres)
               - Mathf.Max(0f, hysteresisMetres);
    }
}
