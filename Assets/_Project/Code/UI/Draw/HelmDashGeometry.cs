namespace HiddenHarbours.UI
{
    /// <summary>
    /// The skiff helm dashes' PURE geometry — lifted verbatim from the immutable rig sources
    /// <c>docs/art/rigs/ui/console-helm/Art/consoleRig.js:152-169</c> and
    /// <c>docs/art/rigs/ui/sport-skiff-helm/Art/sportRig.js:113-129</c>, which are IDENTICAL BY
    /// DESIGN ("geometry IDENTICAL to consoleRig so the game treats it as an upgrade") — one table
    /// serves both renderers, and the golden tests pin that the sport source really does match.
    ///
    /// <para>The composed-dash mounts (where the lever, the grabbable wheel and the dome compass
    /// land on the card) live here as pure functions so the layout maths is EditMode-testable
    /// without a canvas — the handoff's "composition contract, not a flattened picture".
    /// Card space is RIG pixels: origin top-left of the 600×510 canvas, y down; the dash face is
    /// drawn <see cref="TOPPAD"/> lower so brow instruments may reach up into the headroom
    /// (consoleRig.js:18, :345).</para>
    /// </summary>
    public static class HelmDashGeometry
    {
        // ---- canvas (consoleRig.js:18) ------------------------------------------------------------
        public const int W = 600, TOPPAD = 40, H = 470 + TOPPAD;

        // ---- dash-local geometry (consoleRig.js:153-169; add TOPPAD for card space) ---------------
        public const int ConsoleX = 14, ConsoleY = 44, ConsoleW = 572, ConsoleH = 396, ConsoleR = 20;
        public const int WheelCx = 300, WheelCy = 256, WheelR = 112;
        public const int RpmCx = 104, RpmCy = 148, GaugeR = 56;
        public const int FuelCx = 496, FuelCy = 148;
        public const int SwX = 30, SwY = 288, SwW = 134, SwH = 148, SwR = 12;
        public const int StartCx = 97, StartCy = 328, StartR = 18, StartOffDeg = -20, StartRunDeg = 32;
        public const int DeckX = 58, DeckY = 362, DeckW = 22, DeckH = 42, DeckCx = 69, DeckLampY = 416;
        public const int SpotX = 116, SpotY = 362, SpotW = 22, SpotH = 42, SpotCx = 127, SpotLampY = 416;
        public const int BinnX = 430, BinnY = 284, BinnW = 154, BinnH = 156, BinnR = 16;
        public const int DrivePx = 505, DrivePivotY = 416, DriveHitR = 46;
        public const int SpotcanX = 330, SpotcanY = 22, SpotcanW = 46, SpotcanH = 16;
        public const int DomeBoxX = 308, DomeBoxY = -42, DomeBoxW = 124, DomeBoxH = 176;
        public const float MaxSteerDeg = 45f;            // consoleRig.js:474 (hit-geometry contract)

        // ---- angle maps (consoleRig.js:144-149) ---------------------------------------------------
        public const double FuelSweepDeg = 200.0;

        public static double RpmAngleDeg(double v01) => -135.0 + 270.0 * Clamp01(v01);

        public static double FuelPhiDeg(double v01) => -FuelSweepDeg / 2.0 + FuelSweepDeg * Clamp01(v01);

        // ---- composed mounts, in CARD space (y includes TOPPAD) -----------------------------------

        /// <summary>The lever cell's top-left on the card — the README's own composite
        /// (console-helm/README.md:39-44): <c>round(DRIVE.px − lv.px), round(DRIVE.pivotY + TOPPAD − lv.py)</c>.</summary>
        public static void LeverCellOrigin(out int x, out int y)
        {
            x = DrawSurface.JsRound(DrivePx - (double)LeverRigGeometry.PX);
            y = DrawSurface.JsRound(DrivePivotY + TOPPAD - (double)LeverRigGeometry.PY);
        }

        /// <summary>The grabbable wheel's hub on the card (WheelRig mounts hub-on-point —
        /// console-wheel/README.md:74; the dash's wheel centre is consoleRig.js:154 + TOPPAD).</summary>
        public static void WheelHub(out int x, out int y)
        {
            x = WheelCx;
            y = WheelCy + TOPPAD;
        }

        /// <summary>The wheel cell's top-left on the card (hub minus the cell pivot).</summary>
        public static void WheelCellOrigin(out int x, out int y)
        {
            WheelHub(out int hx, out int hy);
            x = hx - WheelRigGeometry.PVX;
            y = hy - WheelRigGeometry.PVY;
        }

        /// <summary>The dome compass box on the card (consoleRig.js:169 + TOPPAD): the crown centre;
        /// the dome may reach into the headroom (its box starts above the dash face).</summary>
        public static void DomeBoxOnCard(out int x, out int y, out int w, out int h)
        {
            x = DomeBoxX;
            y = DomeBoxY + TOPPAD;
            w = DomeBoxW;
            h = DomeBoxH;
        }

        /// <summary>The brow sounder cutout on the card (consoleRig.js:389-401, TOPPAD applied):
        /// slides to port and narrows when the dome compass shares the crown. S2a draws NOTHING in
        /// it (the DepthRig renderer is S2's port) — this rect exists for S2's mount and the tests.</summary>
        public static void SounderCutout(bool domeFitted, out int x, out int y, out int w, out int h)
        {
            w = domeFitted ? 126 : 148;
            h = 86;
            x = domeFitted ? 170 : 226;
            y = 56 + TOPPAD;
        }

        // ---- hit geometry (card space, y down) — console-helm/README.md:47-56 ---------------------

        /// <summary>The wheel grab test: within the wheel's silhouette (knob tips) + the data pad,
        /// but outside the hub boss (the hub is not a grab surface — the rim and spokes are).</summary>
        public static bool IsOnWheel(UnityEngine.Vector2 cardPx, float rimGrabPadPx)
        {
            WheelHub(out int hx, out int hy);
            double dx = cardPx.x - hx, dy = cardPx.y - hy;
            double d2 = dx * dx + dy * dy;
            double rOut = WheelRigGeometry.OUTER + rimGrabPadPx;
            return d2 <= rOut * rOut;
        }

        /// <summary>Pointer angle about the wheel hub, degrees, canvas convention (atan2(y, x) —
        /// the harness's own read, Console Wheel.dc.html:256).</summary>
        public static double WheelPointerAngleDeg(UnityEngine.Vector2 cardPx)
        {
            WheelHub(out int hx, out int hy);
            return System.Math.Atan2(cardPx.y - hy, cardPx.x - hx) * 180.0 / System.Math.PI;
        }

        /// <summary>The binnacle box — clicks in here belong to the lever (grip drag or
        /// travel-guide jump, the S1 semantics), scoped so a stray dash click can't slam the drive.</summary>
        public static bool IsInBinnacle(UnityEngine.Vector2 cardPx)
        {
            float y = cardPx.y - TOPPAD;
            return cardPx.x >= BinnX && cardPx.x <= BinnX + BinnW && y >= BinnY && y <= BinnY + BinnH;
        }

        /// <summary>The lever grip test on the composed dash: distance to the grip's live position
        /// (hub at DRIVE + the S1 handle offset), against the S1 <c>GrabRadiusPx</c>.</summary>
        public static bool IsOnDashLeverGrip(UnityEngine.Vector2 cardPx, float drive, float grabRadiusPx)
        {
            LeverRigGeometry.HandleOffset(drive, out double gx, out double gy);
            double dx = cardPx.x - (DrivePx + gx);
            double dy = cardPx.y - (DrivePivotY + TOPPAD + gy);
            return dx * dx + dy * dy <= (double)grabRadiusPx * grabRadiusPx;
        }

        /// <summary>Pointer → lever signal on the composed dash: offset from the DRIVE hub into the
        /// S1 inverse (<see cref="LeverRigGeometry.SigFromOffset"/>), exactly
        /// <c>driveFromPoint</c> (consoleRig.js:269).</summary>
        public static float DashLeverSigAt(UnityEngine.Vector2 cardPx)
            => LeverRigGeometry.SigFromOffset(cardPx.x - DrivePx, cardPx.y - (DrivePivotY + TOPPAD));

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
