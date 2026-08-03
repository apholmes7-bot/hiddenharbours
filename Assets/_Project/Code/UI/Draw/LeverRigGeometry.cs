namespace HiddenHarbours.UI
{
    /// <summary>
    /// The binnacle lever's PURE geometry, ported exactly from the immutable rig source
    /// <c>docs/art/rigs/ui/lever-rig/Art/leverRig.js</c> (ADR 0025 — geometry lifted OUT of the rig,
    /// never hand-invented; every constant cites its source line). The swing plane is fore/aft, so
    /// the projected grip offset always has <c>dx == 0</c> — the lever travels vertically on screen.
    ///
    /// <para>Drift-guarded by numeric goldens (<c>LeverTillerGeometryGoldenTests</c>): a committed
    /// table of <c>sig → hub angle → handleOffset</c> derived independently from the JS source,
    /// asserted against this port, negative-controlled. Engine-free maths (<see cref="System.Math"/>)
    /// so it runs in EditMode.</para>
    /// </summary>
    public static class LeverRigGeometry
    {
        // ---- rig canvas + hub pivot (leverRig.js:22) --------------------------------------------
        public const int W = 232, H = 344, PX = 116, PY = 300;

        // ---- swing / camera (leverRig.js:25-29) -------------------------------------------------
        public const double TH0 = 23.0;      // neutral lean toward the operator (deg)
        public const double THROW = 33.0;    // throw either side of neutral (deg)
        public const double PITCH_DEG = 17.0;// camera looks down over the console (deg)
        public const double CAMD = 760.0;    // perspective distance
        public const double ARM = 178.0;     // world length of the lever

        // ---- the grip's centreline point (leverRig.js:67 GRIP_H; :276 the -6.0 z) -----------------
        public const double GRIP_H = 145.0;
        public const double GRIP_Z = -6.0;

        /// <summary>Hub swing angle for a signal (leverRig.js:31): degrees off vertical, + toward the
        /// operator. Neutral leans +23°, full ahead −10°, full astern +56°.</summary>
        public static double ThetaDeg(double sig) => TH0 - sig * THROW;

        /// <summary>
        /// Grip-centre offset from the hub pivot in rig pixels, y-down (leverRig.js:274-278
        /// <c>gripPt</c> / :292 <c>handleOffset</c>). <paramref name="dx"/> is always 0 — the swing
        /// projects onto the vertical axis; kept for contract fidelity with the JS.
        /// </summary>
        public static void HandleOffset(double sig, out double dx, out double dy)
            => HandleOffsetWith(TH0, THROW, PITCH_DEG, CAMD, GRIP_H, GRIP_Z, sig, out dx, out dy);

        /// <summary>
        /// The parameterised core of <see cref="HandleOffset"/> — public so the golden tests can run
        /// their NEGATIVE CONTROL (perturb one constant, prove the goldens fail). Production always
        /// goes through <see cref="HandleOffset"/> with the pinned constants.
        /// </summary>
        public static void HandleOffsetWith(double th0, double throwDeg, double pitchDeg, double camD,
                                            double gripH, double gripZ, double sig,
                                            out double dx, out double dy)
        {
            double deg = System.Math.PI / 180.0;
            double th = (th0 - sig * throwDeg) * deg;
            double c = System.Math.Cos(th), s = System.Math.Sin(th);
            double cA = System.Math.Cos(pitchDeg * deg), sA = System.Math.Sin(pitchDeg * deg);
            // toView(0, gripH, gripZ, c, s)  (leverRig.js:38-41)
            double y0 = gripH * c - gripZ * s;
            double z0 = gripH * s + gripZ * c;
            double vy = y0 * cA - z0 * sA;
            double vz = y0 * sA + z0 * cA;
            // toScreen (leverRig.js:42-45): x = PX + v.x*k with v.x == 0 → dx = 0.
            double k = camD / (camD - vz);
            dx = 0.0;
            dy = -vy * k;
        }

        // ---- the drag inverse (leverRig.js:294-300): nearest sample in a 201-entry table ----------
        private static double[] _tableX, _tableY;

        private static void EnsureTable()
        {
            if (_tableX != null) return;
            var tx = new double[201];
            var ty = new double[201];
            for (int i = -100; i <= 100; i++)
            {
                HandleOffset(i / 100.0, out double x, out double y);
                tx[i + 100] = x;
                ty[i + 100] = y;
            }
            _tableX = tx;   // publish fully built (no torn reads if racing, though UI is main-thread)
            _tableY = ty;
        }

        /// <summary>Invert a pointer offset from the pivot (rig px, y-down) back to the nearest
        /// signal — the drag-to-set inverse, by nearest of 201 samples exactly like the JS.</summary>
        public static float SigFromOffset(double dx, double dy)
        {
            EnsureTable();
            double best = 0.0, bd = double.MaxValue;
            for (int i = 0; i < 201; i++)
            {
                double ex = _tableX[i] - dx, ey = _tableY[i] - dy;
                double d = ex * ex + ey * ey;
                if (d < bd) { bd = d; best = (i - 100) / 100.0; }
            }
            return (float)best;
        }

        /// <summary>The frame-cache key the rig itself uses (leverRig.js:282): the signal quantised to
        /// 1/48ths. Repaint only when THIS changes (rule 7 change-detection), so the C# lever redraws
        /// exactly as often as the JS one would fetch a new cached frame.</summary>
        public static int QuantizeKey(float sig)
        {
            if (sig > 1f) sig = 1f;
            else if (sig < -1f) sig = -1f;
            return DrawSurface.JsRound(sig * 48.0);
        }
    }
}
