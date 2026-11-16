using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Where a seam fade is (<see cref="SeamCrossingFade"/>).</summary>
    public enum SeamFadePhase
    {
        /// <summary>No crossing near: the cover is clear.</summary>
        Idle,
        /// <summary>The mover is closing on a seam: the cover follows how near its leading edge is.</summary>
        Out,
        /// <summary>Black: the mover's leading edge has reached the edge of the screen. The hop may be made.</summary>
        Black,
        /// <summary>The hop has been made and the new region is becoming live: held black.</summary>
        Arriving,
        /// <summary>Arrived, stopped, turned back or given up: the screen is clearing.</summary>
        In,
    }

    /// <summary>The seam a mover is closing on, as <see cref="SeamCrossingFade.TryApproach"/> reads it from the
    /// region's rectangle and the mover's extent. PURE data.</summary>
    public readonly struct SeamApproach
    {
        /// <summary>The edge of the region the mover is heading out through.</summary>
        public readonly RegionEdge Edge;
        /// <summary>How far (m) the mover's LEADING edge still is from the region's edge (≤ 0: at or past it).</summary>
        public readonly float GapMetres;
        /// <summary>How far (m) to carry the mover on across the seam so its TRAILING edge lands the given
        /// inset inside the neighbour (≥ 0).</summary>
        public readonly float CarryMetres;
        /// <summary>The outward unit direction of <see cref="Edge"/>.</summary>
        public readonly Vector2 Outward;
        /// <summary>A point just past <see cref="Edge"/>, level with the mover, to ask the world frame what is there.</summary>
        public readonly Vector2 Probe;
        /// <summary>How fast (m/s) the mover is heading out through <see cref="Edge"/>.</summary>
        public readonly float OutSpeed;

        public SeamApproach(RegionEdge edge, float gap, float carry, Vector2 outward, Vector2 probe, float outSpeed)
        {
            Edge = edge; GapMetres = gap; CarryMetres = carry; Outward = outward; Probe = probe; OutSpeed = outSpeed;
        }
    }

    /// <summary>
    /// ⭐ <b>The fade over a world-frame crossing, driven by distance</b> (owner ruling on PR 1223, ADR 0057:
    /// "the camera should stop as the player runs towards the edge then crosses at the edge with a
    /// momentary fade out and screen change"). A PURE state machine, stepped by its owner, so the whole
    /// sequence is provable headless; the travel rig drives it and publishes <see cref="Alpha"/> to the one
    /// screen fade through <see cref="GameServices.SeamCoverAlpha"/>.
    ///
    /// <para><b>Why distance, not a timer.</b> The camera stops at the region edge, so a mover's leading edge
    /// (a walker's body, a boat's bow) meets the edge of the SCREEN exactly when it meets the edge of the
    /// region. A fixed-time fade started by a look-ahead cannot hit that moment at every speed: at rowing
    /// speed a third of the dory was cut by the screen edge while the screen was still clear, and then the
    /// black was held almost a second waiting for her to get across. Here the target darkness is
    /// <see cref="ApproachAlpha"/> of the leading edge's gap to the edge, so at any speed the screen is black
    /// exactly as the leading edge arrives, and the hop is made at once, under that black.</para>
    ///
    /// <para><b>The steps.</b> Each physics step the owner reports <see cref="Approach"/> (heading out, with a
    /// target) or <see cref="NoApproach"/> (standing, turned back, no seam). <see cref="Tick"/> moves the alpha
    /// toward the target: up no faster than the quickest fade-out allows (a mover teleported next to a seam
    /// still gets a fade, not a cut), down at the fade-in rate (stopping or turning back clears smoothly from
    /// wherever it had got to). At full black <see cref="ReadyToHop"/>; <see cref="Hopped"/> holds black while
    /// the scene changes; <see cref="Arrived"/> clears, dropping the next step's delta, which carries the hop
    /// frame's whole cost and would otherwise clear the screen in one frame. <see cref="Cancel"/> (no mover,
    /// the hop could not be made) clears and ignores the approach until it is lost once.</para>
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
        float _target;
        bool _approaching;
        bool _latched;          // given up on this approach: ignore it until the mover stops approaching
        bool _skipNextStep;

        /// <summary>Where the sequence is.</summary>
        public SeamFadePhase Phase { get; private set; } = SeamFadePhase.Idle;

        /// <summary>How black the cover is (0 clear .. 1 black).</summary>
        public float Alpha { get; private set; }

        /// <summary>True when the last clear began because the black waited past its limit.</summary>
        public bool TimedOut { get; private set; }

        /// <summary>The screen is fully black and nothing has hopped yet: the crossing may be made now.</summary>
        public bool ReadyToHop => Phase == SeamFadePhase.Black;

        /// <summary>The approach in hand was given up (<see cref="Cancel"/>) and is ignored until the mover
        /// stops approaching once (<see cref="NoApproach"/>).</summary>
        public bool GaveUp => _latched;

        /// <summary>Set the timings (s): the QUICKEST a fade may go black, the fade-in, and the longest black
        /// held without a hop. Negative values read as zero (instant).</summary>
        public void Configure(float outSeconds, float inSeconds, float maxBlackSeconds)
        {
            _outSeconds = Mathf.Max(0f, outSeconds);
            _inSeconds = Mathf.Max(0f, inSeconds);
            _maxBlackSeconds = Mathf.Max(0f, maxBlackSeconds);
        }

        /// <summary>The mover is heading out through a seam; <paramref name="target"/> is how black the screen
        /// should be for where it is (<see cref="ApproachAlpha"/>, clamped to 0..1).</summary>
        public void Approach(float target)
        {
            _approaching = true;
            _target = Mathf.Clamp01(target);
        }

        /// <summary>No seam ahead (standing still, turned back, nothing there): the fade clears.</summary>
        public void NoApproach()
        {
            _approaching = false;
            _target = 0f;
            _latched = false;
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

        /// <summary>The new region is live: clear, dropping the next step's delta (the hop frame's). The
        /// approach that led here is spent. Only from <see cref="SeamFadePhase.Arriving"/>.</summary>
        public bool Arrived()
        {
            if (Phase != SeamFadePhase.Arriving) return false;
            Phase = SeamFadePhase.In;
            Alpha = 1f;
            _approaching = false;
            _target = 0f;
            _skipNextStep = true;
            return true;
        }

        /// <summary>Give up (no mover, the hop could not be made): clear from the current alpha, and ignore
        /// the approach until it is lost once. Returns whether there was anything to give up.</summary>
        public bool Cancel()
        {
            bool covering = Alpha > 0f || Phase == SeamFadePhase.Arriving;
            _latched = _latched || _approaching;
            _approaching = false;
            _target = 0f;
            _skipNextStep = false;
            if (!covering) return false;
            Phase = SeamFadePhase.In;
            if (_inSeconds <= 0f) Clear();
            return true;
        }

        /// <summary>Drop everything, at once: clear and idle (the owner was switched off or torn down).</summary>
        public void Reset()
        {
            Phase = SeamFadePhase.Idle;
            Alpha = 0f;
            TimedOut = false;
            _approaching = false;
            _latched = false;
            _target = 0f;
            _skipNextStep = false;
            _blackElapsed = 0f;
        }

        /// <summary>Advance by <paramref name="dt"/> seconds (negative reads as zero).</summary>
        public void Tick(float dt)
        {
            dt = Mathf.Max(0f, dt);
            if (Phase == SeamFadePhase.Arriving) { Alpha = 1f; return; }
            if (_skipNextStep) { _skipNextStep = false; return; }

            if (Phase == SeamFadePhase.Black)
            {
                _blackElapsed += dt;
                if (_blackElapsed > _maxBlackSeconds)
                {
                    Cancel();
                    TimedOut = true;
                    return;
                }
            }

            float target = _approaching && !_latched ? _target : 0f;
            if (Alpha < target)
                Alpha = _outSeconds <= 0f ? target : Mathf.Min(target, Alpha + dt / _outSeconds);
            else if (Alpha > target)
                Alpha = _inSeconds <= 0f ? target : Mathf.Max(target, Alpha - dt / _inSeconds);

            if (Alpha >= 1f)
            {
                if (Phase != SeamFadePhase.Black) { Phase = SeamFadePhase.Black; _blackElapsed = 0f; TimedOut = false; }
            }
            else if (Alpha <= 0f) Clear();
            else Phase = Alpha < target || (Alpha == target && target > 0f) ? SeamFadePhase.Out : SeamFadePhase.In;
        }

        void Clear()
        {
            Alpha = 0f;
            Phase = SeamFadePhase.Idle;
        }

        /// <summary>How far (m) past the landing inset <see cref="SeamApproach.Probe"/> sits, so a world-frame
        /// lookup there (with no hysteresis) lands unambiguously inside the neighbour. Geometry, not a tunable.</summary>
        public const float ProbeBeyondMetres = 0.05f;

        /// <summary>
        /// How black the screen should be for a leading edge <paramref name="gapMetres"/> short of the region's
        /// edge: clear at <paramref name="fadeDistanceMetres"/> or further, black at the edge, linear between.
        /// A zero fade distance is a step at the edge. PURE.
        /// </summary>
        public static float ApproachAlpha(float gapMetres, float fadeDistanceMetres)
        {
            if (fadeDistanceMetres <= 0f) return gapMetres <= 0f ? 1f : 0f;
            return Mathf.Clamp01(1f - gapMetres / fadeDistanceMetres);
        }

        /// <summary>
        /// The fade distance to use for a mover heading out at <paramref name="outSpeed"/>: the tuned
        /// <paramref name="fadeDistanceMetres"/>, or the distance it covers in the quickest fade
        /// (<paramref name="quickestFadeSeconds"/>) if that is further. So the target darkness never rises faster
        /// than the cover may follow it, and a fast mover is black as its leading edge arrives, not after. PURE.
        /// </summary>
        public static float FadeDistanceFor(float fadeDistanceMetres, float outSpeed, float quickestFadeSeconds) =>
            Mathf.Max(Mathf.Max(0f, fadeDistanceMetres), Mathf.Max(0f, outSpeed) * Mathf.Max(0f, quickestFadeSeconds));

        /// <summary>
        /// The seam a mover is closing on, read from the region's local rectangle <paramref name="region"/>, the
        /// mover's extent <paramref name="body"/> (its local bounds, plus the drawn margin the caller adds) and
        /// its velocity. Of the edges it is heading out through faster than <paramref name="minSpeed"/>, the one
        /// its leading edge will reach first. The gap is from the leading edge to that region edge; the carry is
        /// how far the mover must go on so its trailing edge lands <paramref name="landingInsetMetres"/> inside
        /// the neighbour (the hop puts it there, under the black, so the first clear frame shows all of it
        /// inside the new screen). False when it is heading out through no edge. PURE.
        /// </summary>
        public static bool TryApproach(Rect region, Rect body, Vector2 velocity, float minSpeed,
                                       float landingInsetMetres, out SeamApproach approach)
        {
            SeamApproach best = default;   // a local function may not touch an out parameter (CS1628)
            float bestTime = float.MaxValue;
            float speedFloor = Mathf.Max(minSpeed, 1e-6f);
            float inset = Mathf.Max(0f, landingInsetMetres);
            Vector2 c = body.center;
            Consider(RegionEdge.East, Vector2.right, velocity.x, region.xMax - body.xMax, region.xMax + inset - body.xMin,
                     new Vector2(region.xMax + inset + ProbeBeyondMetres, c.y));
            Consider(RegionEdge.West, Vector2.left, -velocity.x, body.xMin - region.xMin, body.xMax - (region.xMin - inset),
                     new Vector2(region.xMin - inset - ProbeBeyondMetres, c.y));
            Consider(RegionEdge.North, Vector2.up, velocity.y, region.yMax - body.yMax, region.yMax + inset - body.yMin,
                     new Vector2(c.x, region.yMax + inset + ProbeBeyondMetres));
            Consider(RegionEdge.South, Vector2.down, -velocity.y, body.yMin - region.yMin, body.yMax - (region.yMin - inset),
                     new Vector2(c.x, region.yMin - inset - ProbeBeyondMetres));
            approach = best;
            return bestTime < float.MaxValue;

            void Consider(RegionEdge edge, Vector2 outward, float outSpeed, float gap, float carry, Vector2 probe)
            {
                if (outSpeed < speedFloor) return;
                float time = Mathf.Max(0f, gap) / outSpeed;
                if (time >= bestTime) return;
                bestTime = time;
                best = new SeamApproach(edge, gap, Mathf.Max(0f, carry), outward, probe, outSpeed);
            }
        }

        /// <summary>
        /// A mover's velocity from one physics step (m/s), or zero when the step is no motion at all: a non-positive
        /// step, or a jump faster than <paramref name="maxSpeed"/> (a teleport: a test staging the walker, an
        /// arrival placing the boat), reported in <paramref name="jumped"/>. A teleport is not speed: read as speed
        /// it made a walker staged beside the Rice Point strand door "head" east at 21 km/s and loaded the creek
        /// (PR 1223). <paramref name="maxSpeed"/> of 0 or less keeps every step.
        /// </summary>
        public static Vector2 PlausibleVelocity(Vector2 from, Vector2 to, float dt, float maxSpeed, out bool jumped)
        {
            jumped = false;
            if (!(dt > 0f)) return Vector2.zero;
            Vector2 v = (to - from) / dt;
            if (maxSpeed > 0f && v.sqrMagnitude > maxSpeed * maxSpeed)
            {
                jumped = true;
                return Vector2.zero;
            }
            return v;
        }
    }

    /// <summary>
    /// ⭐ <b>When to warm a neighbour, and when to let it go</b> (PR 1223). Pure and stepped once per physics step
    /// with the neighbour the mover is heading for (null for none). A neighbour is warmed only after the mover has
    /// kept heading for that SAME one for <c>headingSeconds</c>, so one odd step never loads a region; and a warm
    /// neighbour is released once no neighbour has been headed for during <c>releaseSeconds</c>.
    /// </summary>
    public sealed class NeighbourWarmGate
    {
        string _heading;
        float _headingFor;
        float _idleFor;

        /// <summary>The neighbour the mover has been heading for, or null.</summary>
        public string Heading => _heading;

        /// <summary>How long (s) the mover has kept heading for <see cref="Heading"/>.</summary>
        public float HeadingSeconds => _headingFor;

        /// <summary>How long (s) the mover has headed for no neighbour at all.</summary>
        public float IdleSeconds => _idleFor;

        /// <summary>
        /// One physics step: <paramref name="heading"/> is the neighbour the mover heads for now (null or empty for
        /// none). True when it has been the same one for at least <paramref name="headingSeconds"/>, i.e. warm it.
        /// </summary>
        public bool Step(string heading, float dt, float headingSeconds)
        {
            dt = Mathf.Max(0f, dt);
            if (string.IsNullOrEmpty(heading))
            {
                _heading = null;
                _headingFor = 0f;
                _idleFor += dt;
                return false;
            }
            _idleFor = 0f;
            if (!string.Equals(heading, _heading, System.StringComparison.Ordinal))
            {
                _heading = heading;
                _headingFor = 0f;   // the first step of a heading only starts the count
                return headingSeconds <= 0f;
            }
            _headingFor += dt;
            return _headingFor >= headingSeconds;
        }

        /// <summary>True once no neighbour has been headed for during <paramref name="releaseSeconds"/>.</summary>
        public bool ApproachEnded(float releaseSeconds) => _heading == null && _idleFor >= releaseSeconds;

        /// <summary>Forget the heading (a teleport, a change of mover): the count starts again.</summary>
        public void Interrupt()
        {
            _heading = null;
            _headingFor = 0f;
        }

        /// <summary>Back to nothing headed for and nothing idle.</summary>
        public void Reset()
        {
            Interrupt();
            _idleFor = 0f;
        }
    }
}
