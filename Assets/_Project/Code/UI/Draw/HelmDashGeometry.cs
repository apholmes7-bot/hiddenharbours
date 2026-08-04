using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The helm dashes' PURE geometry — lifted verbatim from the immutable rig sources. TWO tables,
    /// because the fleet's helms come in two hull families:
    ///
    /// <list type="bullet">
    /// <item><b>Skiff</b> (<see cref="ConsoleRigKind.Console"/>/<see cref="ConsoleRigKind.Sport"/>,
    /// the unprefixed constants) — <c>consoleRig.js:152-169</c> and <c>sportRig.js:113-129</c>,
    /// IDENTICAL BY DESIGN ("geometry IDENTICAL to consoleRig so the game treats it as an upgrade"):
    /// a 600×510 canvas, <see cref="TOPPAD"/> 40.</item>
    /// <item><b>Pilothouse</b> (<see cref="ConsoleRigKind.Novi"/>/<see cref="ConsoleRigKind.Cape"/>,
    /// the <c>Pilot*</c> constants) — <c>noviRig.js:120-145</c> and <c>capeRig.js:122-145</c>,
    /// likewise identical to each other by the rigs' own note (noviRig.js:118-119: "KEPT
    /// dimensionally in step with capeRig so the shared DC pointer-picking and the composited lever
    /// line up exactly across the two rigs"): a TALLER 600×548 canvas, <see cref="PilotTOPPAD"/> 54,
    /// because the pilothouse brow carries a portrait sonar that reaches further above the dash.</item>
    /// </list>
    ///
    /// <para>The golden tests pin that each source really does match its sibling, and that the two
    /// FAMILIES are genuinely distinct (the S4 negative control).</para>
    ///
    /// <para>The composed-dash mounts (where the lever, the grabbable wheel, the compass and the brow
    /// instruments land on the card) live here as pure functions so the layout maths is
    /// EditMode-testable without a canvas — the handoff's "composition contract, not a flattened
    /// picture". Card space is RIG pixels: origin top-left of the canvas, y down; the dash face is
    /// drawn one TOPPAD lower so brow instruments may reach up into the headroom (consoleRig.js:18,
    /// :345; noviRig.js:15, :421). The unprefixed mount helpers keep their S2a signatures and mean
    /// the SKIFF; every one has a rig-taking overload that is the real implementation.</para>
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

        // ---- PILOTHOUSE table (noviRig.js:120-145 == capeRig.js:122-145) --------------------------
        // The Novi and the Cape Islander are the same wheelhouse box in two finishes; only the corner
        // radii and the nameplate's minor axis differ, and those are PAINT, not layout, so they live
        // in the two renderers. Everything the composition layer needs is here, once.
        public const int PilotW = 600, PilotTOPPAD = 54, PilotH = 494 + PilotTOPPAD;
        public const int PilotShellX = 12, PilotShellY = 34, PilotShellW = 576, PilotShellH = 452;
        public const int PilotFaceX = 26, PilotFaceY = 48, PilotFaceW = 548, PilotFaceH = 424;
        public const int PilotWheelCx = 300, PilotWheelCy = 328, PilotWheelR = 104;
        public const int PilotRpmCx = 92, PilotRpmCy = 214, PilotGaugeR = 50;
        public const int PilotFuelCx = 508, PilotFuelCy = 214;
        public const int PilotNameCx = 300, PilotNameCy = 452, PilotNameRx = 78;
        public const int PilotBankX = 34, PilotBankY = 298, PilotBankW = 154, PilotBankH = 178, PilotBankR = 9;
        public const int PilotRockW = 56, PilotRockH = 18, PilotRockRow0 = 322, PilotRockDy = 27, PilotRockRows = 6;
        public const int PilotRockColA = PilotBankX + 16, PilotRockColB = PilotBankX + 82;   // 50, 116
        public const int PilotIgnCx = 400, PilotIgnCy = 454, PilotIgnR = 17;
        public const int PilotIgnOffDeg = -22, PilotIgnRunDeg = 30;
        public const int PilotBinnX = 430, PilotBinnY = 296, PilotBinnW = 154, PilotBinnH = 180, PilotBinnR = 14;
        public const int PilotDrivePx = 507, PilotDrivePivotY = 456, PilotDriveHitR = 46;

        /// <summary>The brow's THREE equal mounts (noviRig.js:128-129): a landscape glass box each,
        /// or a taller PORTRAIT box that rises into the headroom when the mounted instrument wants it
        /// (the colour fish finder, and the radar mount the rigs author portrait).</summary>
        public const int PilotSlotW = 150, PilotSlotY = 18, PilotSlotH = 104, PilotBrowB = 122;
        public const int PilotSlotPortraitH = 150;
        public static readonly int[] PilotSlotX = { 52, 225, 398 };

        /// <summary>Which brow slot each instrument owns (noviRig.js:130 <c>DEFAULT_LAYOUT</c>).</summary>
        public const int PilotSounderSlot = 0, PilotRadarSlot = 1, PilotGpsSlot = 2;

        // The pilothouse compass mounts BOTH ways (noviRig.js:134): a dome binnacle on the crown that
        // DISPLACES the centre brow mount, or a flush Ritchie sunk into the dash face so the brow
        // keeps all three. These two hulls are the first that can take the flush unit.
        public const int PilotDomeBoxX = 226, PilotDomeBoxY = -50, PilotDomeBoxW = 148, PilotDomeBoxH = 182;
        public const int PilotFlushBoxX = 260, PilotFlushBoxY = 132, PilotFlushBoxW = 80, PilotFlushBoxH = 80;

        // ---- angle maps (consoleRig.js:144-149 == noviRig.js:113-115 == capeRig.js:117-119) -------
        public const double FuelSweepDeg = 200.0;

        public static double RpmAngleDeg(double v01) => -135.0 + 270.0 * Clamp01(v01);

        public static double FuelPhiDeg(double v01) => -FuelSweepDeg / 2.0 + FuelSweepDeg * Clamp01(v01);

        // ---- which family a rig belongs to --------------------------------------------------------

        /// <summary>True for the wheelhouse hulls (Novi, Cape) — the taller canvas and the
        /// <c>Pilot*</c> table. False for the skiffs and for a hull with no console at all.</summary>
        public static bool IsPilothouse(ConsoleRigKind rig)
            => rig == ConsoleRigKind.Novi || rig == ConsoleRigKind.Cape;

        /// <summary>The card canvas for a rig — 600 wide in both families, but the pilothouse is
        /// 38 px taller (its brow reaches further up).</summary>
        public static int CanvasW(ConsoleRigKind rig) => IsPilothouse(rig) ? PilotW : W;

        public static int CanvasH(ConsoleRigKind rig) => IsPilothouse(rig) ? PilotH : H;

        /// <summary>The headroom the dash face is drawn below (consoleRig.js:18 / noviRig.js:15).</summary>
        public static int Toppad(ConsoleRigKind rig) => IsPilothouse(rig) ? PilotTOPPAD : TOPPAD;

        // ---- composed mounts, in CARD space (y includes TOPPAD) -----------------------------------

        /// <summary>The lever cell's top-left on the card — the README's own composite
        /// (console-helm/README.md:39-44): <c>round(DRIVE.px − lv.px), round(DRIVE.pivotY + TOPPAD − lv.py)</c>.</summary>
        public static void LeverCellOrigin(out int x, out int y)
            => LeverCellOrigin(ConsoleRigKind.Console, out x, out y);

        public static void LeverCellOrigin(ConsoleRigKind rig, out int x, out int y)
        {
            bool p = IsPilothouse(rig);
            x = DrawSurface.JsRound((p ? PilotDrivePx : DrivePx) - (double)LeverRigGeometry.PX);
            y = DrawSurface.JsRound((p ? PilotDrivePivotY : DrivePivotY) + Toppad(rig)
                                    - (double)LeverRigGeometry.PY);
        }

        /// <summary>The grabbable wheel's hub on the card (WheelRig mounts hub-on-point —
        /// console-wheel/README.md:74; the dash's wheel centre is consoleRig.js:154 / noviRig.js:122
        /// + TOPPAD). The rigs bake their own wheel at r 112 (skiff) / 104 (pilothouse); neither is
        /// ported, because in game the wheel the player grabs is <see cref="WheelRigRender"/> — its
        /// own rig, at its own radius (console-wheel/README.md, the S2a precedent).</summary>
        public static void WheelHub(out int x, out int y)
            => WheelHub(ConsoleRigKind.Console, out x, out y);

        public static void WheelHub(ConsoleRigKind rig, out int x, out int y)
        {
            bool p = IsPilothouse(rig);
            x = p ? PilotWheelCx : WheelCx;
            y = (p ? PilotWheelCy : WheelCy) + Toppad(rig);
        }

        /// <summary>The wheel cell's top-left on the card (hub minus the cell pivot).</summary>
        public static void WheelCellOrigin(out int x, out int y)
            => WheelCellOrigin(ConsoleRigKind.Console, out x, out y);

        public static void WheelCellOrigin(ConsoleRigKind rig, out int x, out int y)
        {
            WheelHub(rig, out int hx, out int hy);
            x = hx - WheelRigGeometry.PVX;
            y = hy - WheelRigGeometry.PVY;
        }

        /// <summary>The dome compass box on the card (consoleRig.js:169 / noviRig.js:134 + TOPPAD):
        /// the crown centre; the dome may reach into the headroom (its box starts above the dash
        /// face).</summary>
        public static void DomeBoxOnCard(out int x, out int y, out int w, out int h)
            => DomeBoxOnCard(ConsoleRigKind.Console, out x, out y, out w, out h);

        public static void DomeBoxOnCard(ConsoleRigKind rig, out int x, out int y, out int w, out int h)
        {
            bool p = IsPilothouse(rig);
            x = p ? PilotDomeBoxX : DomeBoxX;
            y = (p ? PilotDomeBoxY : DomeBoxY) + Toppad(rig);
            w = p ? PilotDomeBoxW : DomeBoxW;
            h = p ? PilotDomeBoxH : DomeBoxH;
        }

        /// <summary>The FLUSH compass box on the card (noviRig.js:134 <c>flushBox</c> + TOPPAD) — the
        /// Ritchie sunk into the dash face, so the brow keeps all three mounts. Pilothouse only: the
        /// skiff helms do not support the flush unit (<c>SupportsFlushCompass: 0</c>), and asking for
        /// one returns their dome box unchanged so a caller can never mount it off-card.</summary>
        public static void FlushBoxOnCard(ConsoleRigKind rig, out int x, out int y, out int w, out int h)
        {
            if (!IsPilothouse(rig)) { DomeBoxOnCard(rig, out x, out y, out w, out h); return; }
            x = PilotFlushBoxX;
            y = PilotFlushBoxY + PilotTOPPAD;
            w = PilotFlushBoxW;
            h = PilotFlushBoxH;
        }

        /// <summary>Where a fitted compass unit lands on the card, by its mount. The two boxes are
        /// different sizes AND different places, so the compositor asks for the one it is drawing.</summary>
        public static void CompassBoxOnCard(ConsoleRigKind rig, CompassMount mount,
                                            out int x, out int y, out int w, out int h)
        {
            if (mount == CompassMount.Flush) FlushBoxOnCard(rig, out x, out y, out w, out h);
            else DomeBoxOnCard(rig, out x, out y, out w, out h);
        }

        /// <summary>
        /// A pilothouse brow mount on the card (noviRig.js:135-136 <c>slotBox</c> + TOPPAD).
        /// <paramref name="portrait"/> is the rig's own per-instrument choice: the tall box rises to
        /// <c>BROWB − 150</c>, up into the headroom, and is what the colour fish finder and the radar
        /// mount use; the short box sits at <c>SLOTY</c>.
        /// </summary>
        public static void SlotBoxOnCard(int slot, bool portrait,
                                         out int x, out int y, out int w, out int h)
        {
            if (slot < 0 || slot >= PilotSlotX.Length) slot = 0;
            x = PilotSlotX[slot];
            w = PilotSlotW;
            h = portrait ? PilotSlotPortraitH : PilotSlotH;
            y = (portrait ? PilotBrowB - PilotSlotPortraitH : PilotSlotY) + PilotTOPPAD;
        }

        /// <summary>True when the dome compass takes the crown and DISPLACES the centre brow mount
        /// (noviRig.js:453 — <c>if (compass==='dome' &amp;&amp; i===1) continue</c>). The flush unit
        /// does not: it sinks into the face and the brow keeps all three.</summary>
        public static bool SlotIsDisplacedByCompass(int slot, CompassMount mount)
            => mount == CompassMount.Dome && slot == PilotRadarSlot;

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
            => IsOnWheel(ConsoleRigKind.Console, cardPx, rimGrabPadPx);

        public static bool IsOnWheel(ConsoleRigKind rig, UnityEngine.Vector2 cardPx, float rimGrabPadPx)
        {
            WheelHub(rig, out int hx, out int hy);
            double dx = cardPx.x - hx, dy = cardPx.y - hy;
            double d2 = dx * dx + dy * dy;
            double rOut = WheelRigGeometry.OUTER + rimGrabPadPx;
            return d2 <= rOut * rOut;
        }

        /// <summary>Pointer angle about the wheel hub, degrees, canvas convention (atan2(y, x) —
        /// the harness's own read, Console Wheel.dc.html:256).</summary>
        public static double WheelPointerAngleDeg(UnityEngine.Vector2 cardPx)
            => WheelPointerAngleDeg(ConsoleRigKind.Console, cardPx);

        public static double WheelPointerAngleDeg(ConsoleRigKind rig, UnityEngine.Vector2 cardPx)
        {
            WheelHub(rig, out int hx, out int hy);
            return System.Math.Atan2(cardPx.y - hy, cardPx.x - hx) * 180.0 / System.Math.PI;
        }

        /// <summary>The binnacle box — clicks in here belong to the lever (grip drag or
        /// travel-guide jump, the S1 semantics), scoped so a stray dash click can't slam the drive.</summary>
        public static bool IsInBinnacle(UnityEngine.Vector2 cardPx)
            => IsInBinnacle(ConsoleRigKind.Console, cardPx);

        public static bool IsInBinnacle(ConsoleRigKind rig, UnityEngine.Vector2 cardPx)
        {
            bool p = IsPilothouse(rig);
            int bx = p ? PilotBinnX : BinnX, bw = p ? PilotBinnW : BinnW;
            int by = p ? PilotBinnY : BinnY, bh = p ? PilotBinnH : BinnH;
            float y = cardPx.y - Toppad(rig);
            return cardPx.x >= bx && cardPx.x <= bx + bw && y >= by && y <= by + bh;
        }

        /// <summary>The lever grip test on the composed dash: distance to the grip's live position
        /// (hub at DRIVE + the S1 handle offset), against the S1 <c>GrabRadiusPx</c>.</summary>
        public static bool IsOnDashLeverGrip(UnityEngine.Vector2 cardPx, float drive, float grabRadiusPx)
            => IsOnDashLeverGrip(ConsoleRigKind.Console, cardPx, drive, grabRadiusPx);

        public static bool IsOnDashLeverGrip(ConsoleRigKind rig, UnityEngine.Vector2 cardPx,
                                             float drive, float grabRadiusPx)
        {
            bool p = IsPilothouse(rig);
            LeverRigGeometry.HandleOffset(drive, out double gx, out double gy);
            double dx = cardPx.x - ((p ? PilotDrivePx : DrivePx) + gx);
            double dy = cardPx.y - ((p ? PilotDrivePivotY : DrivePivotY) + Toppad(rig) + gy);
            return dx * dx + dy * dy <= (double)grabRadiusPx * grabRadiusPx;
        }

        /// <summary>Pointer → lever signal on the composed dash: offset from the DRIVE hub into the
        /// S1 inverse (<see cref="LeverRigGeometry.SigFromOffset"/>), exactly
        /// <c>driveFromPoint</c> (consoleRig.js:269 / noviRig.js:236).</summary>
        public static float DashLeverSigAt(UnityEngine.Vector2 cardPx)
            => DashLeverSigAt(ConsoleRigKind.Console, cardPx);

        public static float DashLeverSigAt(ConsoleRigKind rig, UnityEngine.Vector2 cardPx)
        {
            bool p = IsPilothouse(rig);
            return LeverRigGeometry.SigFromOffset(cardPx.x - (p ? PilotDrivePx : DrivePx),
                                                  cardPx.y - ((p ? PilotDrivePivotY : DrivePivotY) + Toppad(rig)));
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
