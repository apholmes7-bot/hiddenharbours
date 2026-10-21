using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>What one piece of a helm band is (H2's greybox band, the owner's 2026-09-24 ruling).</summary>
    public enum HelmBandPieceKind
    {
        Cap = 0,            // an end cap, at the screen's edge
        Switches = 1,       // the dash's switch panel, cut from the chrome (ANCH is live)
        Tach = 2,           // the rpm gauge, cut from the chrome
        SounderPlate = 3,   // the sounder's or the finder's housing (the pilot bands add the depth)
        RadarPlate = 4,     // the radar's housing
        Wheel = 5,          // the grabbable wheel, its top half, hub on the band's bottom edge
        Compass = 6,        // the fitted compass, or a blank plate
        GpsPlate = 7,       // the chartplotter's housing
        Fuel = 8,           // the fuel gauge, cut from the chrome
        Drive = 9,          // the lever through its window, and the gear letter
    }

    /// <summary>Which side of the wheel a piece packs on.</summary>
    public enum HelmBandSide
    {
        Left = -1,
        Centre = 0,
        Right = 1,
    }

    /// <summary>One piece of a band: its kind, its side, its width in rig px (every piece is
    /// <see cref="HelmBandGeometry.H"/> tall), and, for the three pieces cut from the dash's chrome,
    /// where the cut starts in the dash's own coordinates (dash-local: add the rig's TOPPAD for the
    /// chrome raster).</summary>
    public readonly struct HelmBandPiece
    {
        public readonly HelmBandPieceKind Kind;
        public readonly HelmBandSide Side;
        public readonly int W;
        public readonly int CropX;
        public readonly int CropY;

        public HelmBandPiece(HelmBandPieceKind kind, HelmBandSide side, int w, int cropX = 0, int cropY = 0)
        {
            Kind = kind; Side = side; W = w; CropX = cropX; CropY = cropY;
        }

        /// <summary>True for the pieces cut out of the dash chrome raster.</summary>
        public bool IsCrop => Kind == HelmBandPieceKind.Switches || Kind == HelmBandPieceKind.Tach
                              || Kind == HelmBandPieceKind.Fuel;

        /// <summary>True for an instrument's housing: a named plate when fitted, blank when not.</summary>
        public bool IsPlate => Kind == HelmBandPieceKind.SounderPlate || Kind == HelmBandPieceKind.RadarPlate
                               || Kind == HelmBandPieceKind.GpsPlate;
    }

    /// <summary>
    /// The helm bands' PURE geometry (H2): which pieces each band is made of, how wide each is, where
    /// the chrome pieces are cut from the dash, and every piece's hit shape. This file is to the band
    /// rigs what <see cref="HelmDashGeometry"/> is to the dash rigs: one table per
    /// <see cref="ConsoleRigKind"/>, keyed on the rig the hull's helm asset names, so no hull is named
    /// here (rule 2: which hull has which helm is already data).
    ///
    /// <para><b>Interim, by design.</b> The table mirrors the band brief's <c>PIECES {w, side}</c>
    /// (helms-pass §2.1). H3 lifts the pieces and their hit keys out of the band rigs as ADR 0025
    /// requires (hit geometry comes out of the rig as data), and this table goes with the greybox.</para>
    ///
    /// <para><b>Every housing is always drawn</b> (the brief: "draw each housing with its unfitted
    /// plate"), so a band's layout never changes with the fit; only what the plates say does.</para>
    ///
    /// <para>Piece space is rig px: origin top-left of the piece, y down, unscaled. The band shows each
    /// piece at the whole-number scale k (<see cref="HelmBandLayout.K"/>).</para>
    /// </summary>
    public static class HelmBandGeometry
    {
        /// <summary>The band's height in rig px (the ruling: 144·k on screen).</summary>
        public const int H = 144;

        /// <summary>The central span every piece but the end caps sits inside (helms-pass §5 H2:
        /// "every piece inside 1280 rig px"), so each side of the wheel has 512, its cap included.</summary>
        public const int SpanW = 1280;

        public const int CapW = 16;
        public const int PlateW = 104;
        public const int WheelW = 256;
        public const int CompassW = 112;
        public const int DriveW = 120;

        /// <summary>The most pieces any band has (the pilot bands' eleven).</summary>
        public const int MaxPieces = 11;

        /// <summary>The smallest hit a band control is given, each side (the brief's 24×24 floor).</summary>
        public const int HitFloorPx = 24;

        // ---- the wheel piece: the wheel cell's top half, its hub on the band's bottom edge ---------
        /// <summary>The hub in wheel-piece px: centred, on the bottom edge.</summary>
        public const int HubX = WheelW / 2, HubY = H;

        /// <summary>The rows above the wheel cell's top half (144 − 128): panel shows through.</summary>
        public const int WheelTopPad = HubY - WheelRigGeometry.PVY;

        // ---- the drive piece: the lever cell through a window, then the gear letter ----------------
        /// <summary>The lever window: 96×144 of the lever cell, from (68, 118). The grip moves
        /// vertically only (<see cref="LeverRigGeometry.HandleOffset"/> has dx = 0), 146.9 px above the
        /// pivot at full ahead and 58.7 at full astern, so the window shows the whole throw with the
        /// pivot 38 px below the band's edge.</summary>
        public const int DriveWindowW = 96, DriveWindowX = LeverRigGeometry.PX - DriveWindowW / 2, DriveWindowY = 118;

        /// <summary>The gear letter's column, right of the window.</summary>
        public const int GearX = DriveWindowW, GearW = DriveW - DriveWindowW;

        // ---- the compass piece ----------------------------------------------------------------------
        /// <summary>The flush unit's box in the compass piece: its own 80×80, centred.</summary>
        public const int FlushX = 16, FlushY = 32, FlushW = 80, FlushH = 80;

        // ---- the tables -------------------------------------------------------------------------------
        // The skiffs (console and sport share their geometry by design): 382 left of the wheel and 376
        // right, caps included. The cuts are centred on the authored gauge and switch-panel boxes and
        // stop above and below the wheel's station (x 188-412) and the binnacle (from y 284).
        private static readonly HelmBandPiece[] Skiff =
        {
            new HelmBandPiece(HelmBandPieceKind.Cap, HelmBandSide.Left, CapW),
            new HelmBandPiece(HelmBandPieceKind.Switches, HelmBandSide.Left, HelmDashGeometry.SwW,
                              HelmDashGeometry.SwX, HelmDashGeometry.SwY),
            new HelmBandPiece(HelmBandPieceKind.Tach, HelmBandSide.Left, 128,
                              HelmDashGeometry.RpmCx - 64, HelmDashGeometry.RpmCy - H / 2),
            new HelmBandPiece(HelmBandPieceKind.SounderPlate, HelmBandSide.Left, PlateW),
            new HelmBandPiece(HelmBandPieceKind.Wheel, HelmBandSide.Centre, WheelW),
            new HelmBandPiece(HelmBandPieceKind.Compass, HelmBandSide.Right, CompassW),
            new HelmBandPiece(HelmBandPieceKind.Fuel, HelmBandSide.Right, 128,
                              HelmDashGeometry.FuelCx - 64, HelmDashGeometry.FuelCy - H / 2),
            new HelmBandPiece(HelmBandPieceKind.Drive, HelmBandSide.Right, DriveW),
            new HelmBandPiece(HelmBandPieceKind.Cap, HelmBandSide.Right, CapW),
        };

        // The pilothouse bands (Novi and Cape share theirs): 494 left and 468 right of 512. The radar's
        // housing goes LEFT of the wheel: on the right, where the dash's brow order would put it, that
        // side would need 572. The switch cut holds the bank's rows 0-3 whole (DECK, BILGE, NAV, ANCH;
        // SPOT, WIPE, CABIN, INST) and stops at row 4's top.
        private static readonly HelmBandPiece[] Pilot =
        {
            new HelmBandPiece(HelmBandPieceKind.Cap, HelmBandSide.Left, CapW),
            new HelmBandPiece(HelmBandPieceKind.Switches, HelmBandSide.Left, HelmDashGeometry.PilotBankW,
                              HelmDashGeometry.PilotBankX,
                              HelmDashGeometry.PilotRockRow0 + 4 * HelmDashGeometry.PilotRockDy - H),
            new HelmBandPiece(HelmBandPieceKind.Tach, HelmBandSide.Left, 116,
                              HelmDashGeometry.PilotRpmCx - 58, HelmDashGeometry.PilotRpmCy - H / 2),
            new HelmBandPiece(HelmBandPieceKind.SounderPlate, HelmBandSide.Left, PlateW),
            new HelmBandPiece(HelmBandPieceKind.RadarPlate, HelmBandSide.Left, PlateW),
            new HelmBandPiece(HelmBandPieceKind.Wheel, HelmBandSide.Centre, WheelW),
            new HelmBandPiece(HelmBandPieceKind.Compass, HelmBandSide.Right, CompassW),
            new HelmBandPiece(HelmBandPieceKind.GpsPlate, HelmBandSide.Right, PlateW),
            new HelmBandPiece(HelmBandPieceKind.Fuel, HelmBandSide.Right, 116,
                              HelmDashGeometry.PilotFuelCx - 58, HelmDashGeometry.PilotFuelCy - H / 2),
            new HelmBandPiece(HelmBandPieceKind.Drive, HelmBandSide.Right, DriveW),
            new HelmBandPiece(HelmBandPieceKind.Cap, HelmBandSide.Right, CapW),
        };

        private static readonly HelmBandPiece[] NoBand = new HelmBandPiece[0];

        private static HelmBandPiece[] Table(ConsoleRigKind rig)
        {
            if (rig == ConsoleRigKind.None) return NoBand;
            return HelmDashGeometry.IsPilothouse(rig) ? Pilot : Skiff;
        }

        /// <summary>True when this rig draws a band at all (every rig but none).</summary>
        public static bool HasBand(ConsoleRigKind rig) => rig != ConsoleRigKind.None;

        /// <summary>How many pieces this rig's band has, end caps included (0 for none).</summary>
        public static int PieceCount(ConsoleRigKind rig) => Table(rig).Length;

        /// <summary>The <paramref name="index"/>th piece of this rig's band, left to right.</summary>
        public static HelmBandPiece Piece(ConsoleRigKind rig, int index) => Table(rig)[index];

        /// <summary>The index of the wheel in this rig's band (−1 for none).</summary>
        public static int WheelIndex(ConsoleRigKind rig)
        {
            HelmBandPiece[] t = Table(rig);
            for (int i = 0; i < t.Length; i++)
                if (t[i].Kind == HelmBandPieceKind.Wheel) return i;
            return -1;
        }

        /// <summary>The index of the first piece of this kind in this rig's band (−1 when it has none).</summary>
        public static int IndexOf(ConsoleRigKind rig, HelmBandPieceKind kind)
        {
            HelmBandPiece[] t = Table(rig);
            for (int i = 0; i < t.Length; i++)
                if (t[i].Kind == kind) return i;
            return -1;
        }

        /// <summary>The width of one side of the wheel, its end cap included.</summary>
        public static int SideW(ConsoleRigKind rig, HelmBandSide side)
        {
            HelmBandPiece[] t = Table(rig);
            int w = 0;
            for (int i = 0; i < t.Length; i++)
                if (t[i].Side == side) w += t[i].W;
            return w;
        }

        // ---- hit shapes, in piece px (y down) -------------------------------------------------------

        /// <summary>
        /// The anchor switch's hit box in the SWITCHES piece: the dash's own box
        /// (<see cref="HelmDashGeometry.TryAnchorSwitchBox"/>) through the crop's offset, padded about
        /// its centre to the 24-px floor. The skiff bat (22×42) becomes 24×42; the pilot rocker
        /// (56×18) becomes 56×24, inside its 27-px row pitch.
        /// </summary>
        public static bool TryAnchorHitBox(ConsoleRigKind rig, out RectInt box)
        {
            box = default;
            int si = IndexOf(rig, HelmBandPieceKind.Switches);
            if (si < 0 || !HelmDashGeometry.TryAnchorSwitchBox(rig, out int x, out int y, out int w, out int h))
                return false;
            HelmBandPiece sw = Table(rig)[si];
            int lx = x - sw.CropX;
            int ly = y - HelmDashGeometry.Toppad(rig) - sw.CropY;
            if (w < HitFloorPx) { lx -= (HitFloorPx - w + 1) / 2; w = HitFloorPx; }
            if (h < HitFloorPx) { ly -= (HitFloorPx - h + 1) / 2; h = HitFloorPx; }
            box = new RectInt(lx, ly, w, h);
            return true;
        }

        /// <summary>A press on the anchor switch, in switches-piece px: plain point-in-box (a switch is
        /// aimed at, not grabbed).</summary>
        public static bool IsOnAnchorSwitch(ConsoleRigKind rig, Vector2 piecePx)
        {
            if (!TryAnchorHitBox(rig, out RectInt b)) return false;
            return piecePx.x >= b.xMin && piecePx.x < b.xMax && piecePx.y >= b.yMin && piecePx.y < b.yMax;
        }

        /// <summary>
        /// The wheel's grab, in wheel-piece px: the wheel's silhouette plus the data pad
        /// (<see cref="WheelRigGeometry.OUTER"/> + <c>RimGrabPadPx</c>) about the hub, the top half
        /// only (only it is on screen), and never outside the piece, so the pad cannot start eating
        /// presses aimed at the housings either side.
        /// </summary>
        public static bool IsOnWheel(Vector2 piecePx, float rimGrabPadPx)
        {
            if (piecePx.x < 0f || piecePx.x >= WheelW || piecePx.y < 0f || piecePx.y > H) return false;
            double dx = piecePx.x - HubX, dy = piecePx.y - HubY;
            if (dy > 0.0) return false;
            double r = WheelRigGeometry.OUTER + System.Math.Max(0f, rimGrabPadPx);
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>The pointer's angle about the band's hub, degrees, canvas convention
        /// (atan2(y, x), y down), the dash's own read (<see cref="HelmDashGeometry.WheelPointerAngleDeg"/>).</summary>
        public static double WheelPointerAngleDeg(Vector2 piecePx)
            => System.Math.Atan2(piecePx.y - HubY, piecePx.x - HubX) * 180.0 / System.Math.PI;

        /// <summary>A press in the drive piece's lever window (the gear letter's column is inert).</summary>
        public static bool IsInDriveWindow(Vector2 piecePx)
            => piecePx.x >= 0f && piecePx.x < DriveWindowW && piecePx.y >= 0f && piecePx.y <= H;

        /// <summary>Drive-piece px into the lever cell's own px, where the lone lever's grip test and
        /// signal read apply unchanged (<see cref="HelmOverlayLayout.IsOnGrip"/>,
        /// <see cref="HelmOverlayLayout.LeverSigAt"/>).</summary>
        public static Vector2 DriveToLeverCell(Vector2 piecePx)
            => new Vector2(piecePx.x + DriveWindowX, piecePx.y + DriveWindowY);
    }
}
