using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The helm band's PURE screen layout (H2, the owner's 2026-09-24 ruling): the band is the full
    /// width of the bottom edge and 144·k tall, k the largest whole number with 1080·k ≤ the screen's
    /// height (never below 1). The end caps sit at the screen's edges, the wheel is centred, and the
    /// other pieces pack outward from it on their own sides, all inside the central 1280·k, with the
    /// panel in every gap. Screen space is Unity's: origin bottom-left, y up; piece space is the
    /// rigs': origin top-left of the piece, y down, unscaled.
    ///
    /// <para>Also here, because it is the band's other half: where a RAISED instrument card goes
    /// (<see cref="RaisedCardRect"/>), between the band's top and the top HUD band.</para>
    ///
    /// <para>The 144 and the 1080 step are the ruling's constants, not tunables (rule 6 governs
    /// balance; this is the band's frame).</para>
    /// </summary>
    public static class HelmBandLayout
    {
        /// <summary>The screen height of one whole step of the band's scale (the ruling).</summary>
        public const int StepH = 1080;

        /// <summary>The band's whole-number scale: max(1, H div 1080). 1 at 720p to 1440p, 2 at 4K.</summary>
        public static int K(int screenH) => Mathf.Max(1, screenH / StepH);

        /// <summary>The band's height on screen, 144·k.</summary>
        public static int BandHeightPx(int screenH) => HelmBandGeometry.H * K(screenH);

        /// <summary>The band's rect: the full width of the bottom edge, 144·k tall.</summary>
        public static Rect BandRect(int screenW, int screenH)
            => new Rect(0f, 0f, Mathf.Max(0, screenW), BandHeightPx(screenH));

        /// <summary>The wheel's left edge on screen: centred, floor(W/2) − 128·k.</summary>
        public static int WheelX(int screenW, int k) => screenW / 2 - (HelmBandGeometry.WheelW / 2) * k;

        /// <summary>
        /// The <paramref name="index"/>th piece's left edge on screen. The caps sit at the screen's
        /// edges; the left pieces pack leftward from the wheel in reverse listed order, the right ones
        /// rightward in listed order. Pure summation over at most eleven pieces, no allocation.
        /// </summary>
        public static int PieceX(ConsoleRigKind rig, int index, int screenW, int k)
        {
            HelmBandPiece p = HelmBandGeometry.Piece(rig, index);
            int n = HelmBandGeometry.PieceCount(rig);
            if (p.Kind == HelmBandPieceKind.Cap)
                return p.Side == HelmBandSide.Left ? 0 : screenW - p.W * k;
            int wheel = HelmBandGeometry.WheelIndex(rig);
            int wheelX = WheelX(screenW, k);
            if (index == wheel) return wheelX;
            if (index < wheel)
            {
                int x = wheelX;
                for (int i = wheel - 1; i >= index; i--) x -= HelmBandGeometry.Piece(rig, i).W * k;
                return x;
            }
            int right = wheelX + HelmBandGeometry.WheelW * k;
            for (int i = wheel + 1; i < index && i < n; i++) right += HelmBandGeometry.Piece(rig, i).W * k;
            return right;
        }

        /// <summary>The <paramref name="index"/>th piece's rect on screen (its width ×k, the band's
        /// height).</summary>
        public static Rect PieceRect(ConsoleRigKind rig, int index, int screenW, int screenH)
        {
            int k = K(screenH);
            HelmBandPiece p = HelmBandGeometry.Piece(rig, index);
            return new Rect(PieceX(rig, index, screenW, k), 0f, p.W * k, HelmBandGeometry.H * k);
        }

        /// <summary>A screen point in a piece's own px (origin top-left, y down, unscaled). The point
        /// is mapped whether or not it lies inside, so a drag that leaves the piece still tracks;
        /// the return says whether it is inside.</summary>
        public static bool ToPieceLocal(Vector2 screen, Rect piece, int k, out Vector2 local)
        {
            float s = Mathf.Max(1, k);
            local = new Vector2((screen.x - piece.xMin) / s, (piece.yMax - screen.y) / s);
            return piece.width > 0f && piece.height > 0f && piece.Contains(screen);
        }

        // ---- raised cards ---------------------------------------------------------------------------

        /// <summary>
        /// The largest whole-number scale at which a raised card fits BETWEEN the band's top and the
        /// top HUD band, and inside the screen's width. 0 when no whole scale fits.
        /// </summary>
        public static int RaisedCardWholeScale(int nativeW, int nativeH, float screenW, float screenH,
                                               float reservedTopPx)
        {
            if (nativeW <= 0 || nativeH <= 0) return 0;
            float gap = screenH - Mathf.Max(0f, reservedTopPx) - BandHeightPx((int)screenH);
            if (gap < nativeH) return 0;
            int byH = Mathf.FloorToInt(gap / nativeH);
            int byW = Mathf.FloorToInt(screenW / nativeW);
            return Mathf.Max(0, Mathf.Min(byH, byW));
        }

        /// <summary>
        /// Where a raised instrument card goes, by the owner's Q1 answer (B), 2026-10-17. The card is
        /// the instrument's own native raster, centred horizontally:
        ///
        /// <list type="bullet">
        /// <item>at the largest WHOLE scale that fits between the band's top and the top HUD band,
        /// centred in that gap; or</item>
        /// <item>where no whole scale fits there, at today's scale: native, or less where the screen
        /// below the top HUD band is shorter than the card, its top edge on the top band's bottom, so
        /// it reaches down OVER the band while it is raised (the wheel included). That is today's fit,
        /// <see cref="HudBandLayout.FitBelowBand"/>, applied to the native card. Keys still steer, and
        /// Esc, a click away or a second press on its housing lowers it.</item>
        /// </list>
        ///
        /// <para>Never into the top band. A zero rect when there is no room at all.</para>
        /// </summary>
        public static Rect RaisedCardRect(int nativeW, int nativeH, float screenW, float screenH,
                                          float reservedTopPx)
        {
            if (nativeW <= 0 || nativeH <= 0 || screenW <= 0f || screenH <= 0f) return Rect.zero;
            float top = screenH - Mathf.Max(0f, reservedTopPx);
            float bandH = BandHeightPx((int)screenH);
            int n = RaisedCardWholeScale(nativeW, nativeH, screenW, screenH, reservedTopPx);
            if (n >= 1)
            {
                float w = nativeW * n, h = nativeH * n;
                float x = Mathf.Floor((screenW - w) / 2f);
                float y = Mathf.Floor(bandH + (top - bandH - h) / 2f);
                return new Rect(x, y, w, h);
            }
            // No whole scale fits: today's scale (Q1: B). The native card, centred across with its top
            // on the top band's bottom, through the fit today's expanded cards used: shrunk undistorted
            // only where the screen below the top band, or its width, is smaller than the card.
            if (top <= 0f) return Rect.zero;
            var native = new Rect(Mathf.Floor((screenW - nativeW) / 2f), top - nativeH, nativeW, nativeH);
            return HudBandLayout.FitBelowBand(native, screenW, screenH, reservedTopPx);
        }

        /// <summary>The raised card's scale (its width over the native width).</summary>
        public static float RaisedCardScale(int nativeW, int nativeH, float screenW, float screenH,
                                            float reservedTopPx)
        {
            Rect r = RaisedCardRect(nativeW, nativeH, screenW, screenH, reservedTopPx);
            return nativeW > 0 ? r.width / nativeW : 0f;
        }
    }
}
