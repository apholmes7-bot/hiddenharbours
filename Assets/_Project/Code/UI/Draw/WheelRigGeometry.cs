namespace HiddenHarbours.UI
{
    /// <summary>
    /// The steering wheel's PURE spin model + angle maps, ported exactly from the immutable rig
    /// source <c>docs/art/rigs/ui/console-wheel/Art/wheelRig.js</c> (ADR 0025 S2a — the ONE rig that
    /// also owns physics: <c>step(state, dt)</c> is stateless, the game holds the state). The same
    /// model runs the preview harness, this port, and any future bake, so a given wheel angle means
    /// the same steer everywhere. Engine-free (<see cref="System.Math"/>) so it runs in EditMode.
    ///
    /// <para>Drift-guarded by numeric goldens (<c>WheelSpinGoldenTests</c>): a scripted spin's
    /// deg/vel sequence transcribed independently from the JS (Python, float64), asserted at 1e-6,
    /// negative-controlled through <see cref="StepWith"/>.</para>
    ///
    /// <para><b>One state, one owner:</b> the authoritative steer stays
    /// <c>BoatController._steer</c>. The {deg, vel} pair here is the input DEVICE's transient state
    /// (like a drag anchor), held by the overlay while the wheel is focused, never saved.</para>
    /// </summary>
    public static class WheelRigGeometry
    {
        // ---- cell + hub pivot (wheelRig.js:26-31) -------------------------------------------------
        public const int W = 256, H = 256, PVX = 128, PVY = 128;
        public const int R = 92, RIMW = 15;
        public const int KNOBS = 8, SPOKES = 8, KNOBL = 19;
        public const int HUBR = 24;

        /// <summary>rI/rO/outer — wheelRig.js:30 &amp; :82: rim section bounds and the knob-tip
        /// radius; OUTER is the whole wheel's silhouette radius (the grab surface's edge).</summary>
        public static readonly int RI = R - DrawSurface.JsRound(RIMW / 2.0);      // 84
        public static readonly int RO = R + DrawSurface.JsRound(RIMW / 2.0);      // 100
        public static readonly int OUTER = R + DrawSurface.JsRound(RIMW / 2.0) + KNOBL + 3;   // 122

        // ---- spin model (wheelRig.js:181-202) -----------------------------------------------------
        public const double MaxRudder = 32.0;          // deg — matches the punt's outboard swing
        public const double DefaultTurns = 1.5;        // lock-to-lock each way (cable steer, 7 m skiff)
        public const double DefaultFriction = 2.4;
        public const double BounceRestitution = -0.10; // vel *= -0.10 at the hard stop
        public const double VelDeadband = 0.7;         // deg/s below which the coast dies

        // The preview harness's own dt clamps (Console Wheel.dc.html:230 loop / :265 drag) — pinned
        // here so a hitch never teleports the wheel and a 1000 Hz mouse never divides by ~zero.
        public const double StepDtMax = 0.05;
        public const double DragDtMin = 0.008;

        public static double LockDeg(double turns) => turns * 360.0;

        public static double DegFromSteer(double steer, double turns)
            => Clamp(steer, -1.0, 1.0) * LockDeg(turns);

        public static double SteerFromDeg(double deg, double turns)
            => Clamp(deg / LockDeg(turns), -1.0, 1.0);

        public static double RudderDeg(double steer, double max = MaxRudder)
            => Clamp(steer, -1.0, 1.0) * max;

        /// <summary>Knobs passed since centre (wheelRig.js:187) — the per-knob audio tick.</summary>
        public static int Clicks(double deg)
            => (int)System.Math.Floor(System.Math.Abs(deg) / (360.0 / KNOBS));

        /// <summary>The wheel's transient spin state — deg and deg/s. NEVER saved (rule 5).</summary>
        public struct SpinState
        {
            public double Deg;
            public double Vel;
        }

        /// <summary>
        /// One tick of the rig-owned spin model (wheelRig.js:189-202): exponential friction coast,
        /// optional self-centre spring, HARD STOP at lock with a small bounce, a velocity deadband,
        /// and the spring's centre snap. Returns the new state; <paramref name="hit"/> = ±1 on the
        /// tick the lock is struck (the thunk sound's cue). Callers clamp dt to
        /// <see cref="StepDtMax"/> first (the harness's own loop discipline).
        /// </summary>
        public static SpinState Step(SpinState s, double dt, double turns, double friction,
                                     double selfCentre, out int hit)
            => StepWith(s, dt, turns, friction, selfCentre, BounceRestitution, VelDeadband, out hit);

        /// <summary>
        /// The parameterised core of <see cref="Step"/> — public so the golden tests can run their
        /// NEGATIVE CONTROL (perturb one constant, prove the goldens fail). Production always goes
        /// through <see cref="Step"/> with the pinned constants.
        /// </summary>
        public static SpinState StepWith(SpinState s, double dt, double turns, double friction,
                                         double selfCentre, double bounce, double deadband, out int hit)
        {
            double lock_ = LockDeg(turns);
            double deg = s.Deg, vel = s.Vel;
            hit = 0;
            vel *= System.Math.Exp(-friction * dt);
            if (selfCentre > 0.0) vel += -deg * selfCentre * dt;
            deg += vel * dt;
            if (deg > lock_) { deg = lock_; if (vel > 0.0) hit = 1; vel *= bounce; }
            if (deg < -lock_) { deg = -lock_; if (vel < 0.0) hit = -1; vel *= bounce; }
            if (System.Math.Abs(vel) < deadband) vel = 0.0;
            if (selfCentre > 0.0 && System.Math.Abs(deg) < 0.6 && System.Math.Abs(vel) < 2.0)
            {
                deg = 0.0;
                vel = 0.0;
            }
            return new SpinState { Deg = deg, Vel = vel };
        }

        /// <summary>
        /// One tick of the harness's GRAB model (Console Wheel.dc.html:263-271): the pointer's
        /// angle-about-hub delta (wrapped to ±180) walks the wheel directly and sets the release
        /// velocity from the same delta; hitting the lock zeroes the velocity. dt is clamped up to
        /// <see cref="DragDtMin"/> before dividing.
        /// </summary>
        public static SpinState Drag(SpinState s, double pointerDeltaDeg, double dt, double turns, out int hit)
        {
            double lock_ = LockDeg(turns);
            double d = WrapDelta180(pointerDeltaDeg);
            double next = Clamp(s.Deg + d, -lock_, lock_);
            hit = 0;
            if (next != s.Deg)
                return new SpinState { Deg = next, Vel = d / System.Math.Max(DragDtMin, dt) };
            if (System.Math.Abs(s.Deg) >= lock_) hit = s.Deg > 0 ? 1 : -1;
            return new SpinState { Deg = s.Deg, Vel = 0.0 };
        }

        /// <summary>Wrap an angle delta into (−180, 180] (Console Wheel.dc.html:265).</summary>
        public static double WrapDelta180(double d)
        {
            while (d > 180.0) d -= 360.0;
            while (d < -180.0) d += 360.0;
            return d;
        }

        /// <summary>The bake cache key the rig itself uses — the angle quantised to HALF DEGREES
        /// (wheelRig.js:167: <c>Math.round(deg*2)/2</c>). Repaint only when THIS changes.</summary>
        public static int QuantizeKey(double deg) => DrawSurface.JsRound(deg * 2.0);

        private static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);
    }
}
