using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The chartplotter card's PURE placement and hit maths (ADR 0025 S6) — split out of its host so the
    /// geometry is EditMode-testable without a canvas, exactly as <see cref="SounderOverlayLayout"/> is
    /// for the sounder. Screen space is Unity's (origin bottom-left, y up); RIG space is the rigs' (origin
    /// top-left, y down) and the screen→rig mapping is the shared
    /// <see cref="HelmOverlayLayout.ScreenToRig"/> — one mapper, so a hit box can never mean two things.
    ///
    /// <para><b>Everything here is measured against the CONSOLE face at native size</b>
    /// (<see cref="NavRigGeometry.ConsoleW"/> × <see cref="NavRigGeometry.ConsoleH"/>), so expanding the
    /// instrument needs no special-casing: the expanded card is the same face at a bigger scale (§ the
    /// sounder/finder precedent), and a hit test in rig space is scale-free.</para>
    /// </summary>
    public static class ChartplotterOverlayLayout
    {
        // ---- the expanded card ------------------------------------------------------------------------

        /// <summary>Most of the screen the expanded card may occupy. The plotter is READ when expanded —
        /// it wants to be big — but it is not a full-screen mode, and leaving a margin is what makes
        /// "click away to collapse" a reachable gesture rather than a trick.</summary>
        private const float MaxWidthFrac = 0.62f, MaxHeightFrac = 0.72f;

        /// <summary>
        /// The expanded card's screen rect: the console face at the largest INTEGER scale that still fits
        /// the budget above.
        ///
        /// <para><b>Integer, and that is the whole point.</b> These rigs are pixel art rastered at native
        /// size and blitted (<see cref="HelmInstrumentMountLayout"/>); a fractional scale resamples the
        /// 3×5 font into mush, which is the defect <see cref="FishFinderOverlayLayout"/> already
        /// documents below ~83% of native. A 1× card on a small screen is honest and crisp; a 1.7× card
        /// would be neither. The flush brow mount is free to be fractional because it is a glance, not a
        /// read.</para>
        /// </summary>
        public static Rect ExpandedCardRect(float screenW, float screenH)
        {
            int scale = 1;
            while ((scale + 1) * NavRigGeometry.ConsoleW <= screenW * MaxWidthFrac
                   && (scale + 1) * NavRigGeometry.ConsoleH <= screenH * MaxHeightFrac)
                scale++;

            float w = NavRigGeometry.ConsoleW * scale, h = NavRigGeometry.ConsoleH * scale;
            return new Rect((screenW - w) * 0.5f, (screenH - h) * 0.5f, w, h);
        }

        // ---- the hit map ------------------------------------------------------------------------------

        /// <summary>Nothing live under the pointer.</summary>
        public const int NoHit = -1;

        /// <summary>The MAX/CNSL pusher — expand or collapse (navRig.js:518 <c>keys[0]</c>).</summary>
        public const int KeyMax = 0;

        /// <summary>The MARK pusher — drop a waypoint (navRig.js:519 <c>keys[1]</c>).</summary>
        public const int KeyMark = 1;

        /// <summary>The IN pusher — a CLOSER range (navRig.js:520 <c>keys[2]</c>).</summary>
        public const int KeyIn = 2;

        /// <summary>The OUT pusher — a WIDER range (navRig.js:521 <c>keys[3]</c>).</summary>
        public const int KeyOut = 3;

        /// <summary>A tap on the chart body — removes the waypoint under the fingertip, if any.</summary>
        public const int ChartHit = 4;

        /// <summary>A tap on the orientation legend at the data strip's right end — north-up/head-up.
        /// You click the word that names the thing, which is the only self-explaining place for it on a
        /// face whose four pushers are already spoken for.</summary>
        public const int OrientHit = 5;

        /// <summary>A tap on the rest of the data strip — the amber backlight's early-on override, the
        /// same status-strip gesture the fish finder uses.</summary>
        public const int NightHit = 6;

        /// <summary>A tap on the route strip — build the route through this region's waypoints, or clear
        /// it if one is already laid.</summary>
        public const int RouteHit = 7;

        /// <summary>
        /// The console face's layout at native size — the frame every hit box below is measured in.
        /// Recomputed rather than cached: <see cref="NavRigGeometry.ComputeLayout"/> is arithmetic on six
        /// integers, and a cached copy is one more thing that can go stale against the renderer.
        /// </summary>
        public static NavRigGeometry.Layout NativeLayout()
            => NavRigGeometry.ComputeLayout(0, 0, NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH,
                                            NavRigGeometry.Face.Console);

        /// <summary>
        /// The orientation legend's hit box: the right end of the data strip, sized from the WIDEST label
        /// the strip can print there rather than from a guessed fraction. <c>NavRigRender.TopBar</c>
        /// right-aligns "NORTH-UP"/"HEAD-UP" at <c>x + width − 6</c> and the range readout directly under
        /// it, so this box covers the legend, its number, and a finger's worth of slack.
        /// </summary>
        public static RectInt OrientBox(in NavRigGeometry.Layout layout)
        {
            RectInt b = layout.TopBar;
            int w = Mathf.Min(b.width, RigDrawUtil.TextW("NORTH-UP", 1) + 12);
            return new RectInt(b.x + b.width - w, b.y, w, b.height);
        }

        /// <summary>
        /// What a click at <paramref name="rigPx"/> (rig space, console face) lands on.
        ///
        /// <para><b>Order matters:</b> the pushers sit in their own column outside the glass, but the
        /// orientation legend is INSIDE the data strip, so it is tested before the strip that contains
        /// it. The chart is tested before nothing-at-all so a miss inside the glass is inert rather than
        /// a collapse (the sounder's near-miss rule).</para>
        /// </summary>
        public static int HitTest(Vector2 rigPx)
        {
            NavRigGeometry.Layout L = NativeLayout();

            for (int i = 0; i < 4; i++)
                if (Contains(L.Key(i), rigPx)) return i;              // KeyMax…KeyOut are 0…3 by design

            if (Contains(OrientBox(in L), rigPx)) return OrientHit;
            if (Contains(L.TopBar, rigPx)) return NightHit;
            if (Contains(L.BotBar, rigPx)) return RouteHit;
            if (Contains(L.Chart, rigPx)) return ChartHit;
            return NoHit;
        }

        /// <summary>Float-precision containment for a <see cref="RectInt"/> hit box. <c>RectInt</c>'s own
        /// <c>Contains</c> takes a <c>Vector2Int</c>, and rounding a rig-space pointer to integers before
        /// the test would move it by up to half a pixel — which at the brow mount's scale is several
        /// screen pixels.</summary>
        public static bool Contains(RectInt r, Vector2 p)
            => p.x >= r.x && p.x < r.x + r.width && p.y >= r.y && p.y < r.y + r.height;

        // ---- the chart tap ----------------------------------------------------------------------------

        /// <summary>How close (in CHART PIXELS) a tap must land to a waypoint to remove it. Measured in
        /// screen pixels rather than metres on purpose: the fingertip's accuracy is a property of the
        /// glass, not of the range, so the same gesture works at every rung of the ladder.</summary>
        public const float TapRadiusChartPx = 12f;

        /// <summary>
        /// Turn a chart tap into a WORLD position and the world-space radius that the same fingertip
        /// covers at this range — the two things <see cref="NavLocker.RemoveWaypointNear"/> needs.
        /// Returns false when the tap is not on the chart at all.
        /// </summary>
        public static bool TryChartTapToWorld(Vector2 rigPx, in NavRigState st, out Vector2 world,
                                              out float radiusMetres)
        {
            world = default;
            radiusMetres = 0f;
            NavRigGeometry.Layout L = NativeLayout();
            if (!Contains(L.Chart, rigPx)) return false;

            Vector2 centre = st.HasBoat ? st.Boat : Vector2.zero;
            NavRigGeometry.View v = NavRigGeometry.MakeView(L.Chart, centre, st.RangeNM, st.Orient,
                                                            st.HeadingDeg);
            world = NavRigGeometry.ScreenToWorld(in v, rigPx);
            // v.Scale is screen px per world metre, so its reciprocal converts the fingertip's radius
            // back into metres at whatever range the glass is on.
            radiusMetres = v.Scale > 0.0 ? (float)(TapRadiusChartPx / v.Scale) : 0f;
            return radiusMetres > 0f && NavMath.IsFinite(world);
        }

        // ---- the repaint key --------------------------------------------------------------------------

        /// <summary>
        /// How many world METRES one chart pixel covers at a range — the quantum the own-ship layer's
        /// repaint key is measured in (§ rule 7). A boat that has not moved a whole chart pixel cannot
        /// have moved the picture, so it must not cost a raster.
        /// </summary>
        public static float MetresPerChartPixel(float rangeNM)
        {
            NavRigGeometry.Layout L = NativeLayout();
            int w = Mathf.Max(1, L.Chart.width);
            return NavMath.NMToMetres(rangeNM > 0f ? rangeNM : 0.05f) / w;
        }

        /// <summary>
        /// The own-ship position quantized to whole chart pixels. Returned as a long pair packed into one
        /// value so the host's change key stays a single comparison and allocates nothing.
        /// </summary>
        public static long QuantizePosition(Vector2 world, float rangeNM)
        {
            float q = MetresPerChartPixel(rangeNM);
            if (q <= 0f || !NavMath.IsFinite(world)) return 0L;
            // Clamped into a range that cannot overflow the pack below: a region is a few thousand
            // metres across, so this only ever bites on a garbage position.
            long x = (long)Mathf.Clamp(Mathf.Floor(world.x / q), -1_000_000f, 1_000_000f);
            long y = (long)Mathf.Clamp(Mathf.Floor(world.y / q), -1_000_000f, 1_000_000f);
            return (x << 21) ^ y;
        }
    }
}
