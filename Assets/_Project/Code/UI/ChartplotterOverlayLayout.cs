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
            => ExpandedCardRect(screenW, screenH, NavRigGeometry.Face.Console);

        /// <summary>
        /// The expanded card for a given FACE. The MAX face is a different size natively
        /// (980 × 648 against the console's 760 × 480 — navRig.js:15), so it gets its own budget rather
        /// than being letterboxed into the console's rect and losing the rail's legibility.
        ///
        /// <para><b>The budget is wider for MAX, and deliberately so.</b> The MAX face IS the read —
        /// it is the state you open to work the waypoint list — so it is allowed more of the screen
        /// than the console face, which is a glance you expand for a moment. The integer-scale rule
        /// is unchanged and is the whole point of both (see the remarks above).</para>
        /// </summary>
        public static Rect ExpandedCardRect(float screenW, float screenH, NavRigGeometry.Face face)
        {
            bool max = face == NavRigGeometry.Face.Max;
            int nativeW = max ? NavRigGeometry.MaxW : NavRigGeometry.ConsoleW;
            int nativeH = max ? NavRigGeometry.MaxH : NavRigGeometry.ConsoleH;
            float wFrac = max ? MaxFaceWidthFrac : MaxWidthFrac;
            float hFrac = max ? MaxFaceHeightFrac : MaxHeightFrac;

            int scale = 1;
            while ((scale + 1) * nativeW <= screenW * wFrac && (scale + 1) * nativeH <= screenH * hFrac)
                scale++;

            float w = nativeW * scale, h = nativeH * scale;
            return new Rect((screenW - w) * 0.5f, (screenH - h) * 0.5f, w, h);
        }

        /// <summary>The MAX face's share of the screen. Larger than the console face's because this is
        /// the state the instrument is WORKED in rather than glanced at, and its rail and manager
        /// column are 3×5 text that has to stay readable.</summary>
        private const float MaxFaceWidthFrac = 0.78f, MaxFaceHeightFrac = 0.84f;

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

        // ---- the MAX face's additions (S6 PR 4) --------------------------------------------------------

        /// <summary>A tap on the depth-profile strip. INERT: the strip is a read, and there is nothing
        /// the rig authors for a press on it to mean. Distinguished from <see cref="NoHit"/> so a miss
        /// there is knowably "on the instrument, on nothing" rather than off it.</summary>
        public const int ProfileHit = 8;

        /// <summary>A tap on the manager column that landed on no row and no button — inert, by the
        /// same near-miss rule the chart follows.</summary>
        public const int ManagerHit = 9;

        /// <summary>Base of the rail's hit ids: <c>RailBase + slot</c>, with slot indexing
        /// <see cref="NavRigGeometry.RailIds"/>. The separator slot is never returned.</summary>
        public const int RailBase = 100;

        /// <summary>Base of the manager's waypoint rows: <c>WaypointRowBase + row</c>.</summary>
        public const int WaypointRowBase = 200;

        /// <summary>The manager's NAME button — open the name editor on the selected waypoint.</summary>
        public const int ActionName = 300;

        /// <summary>The manager's DEL button — remove the selected waypoint.</summary>
        public const int ActionDelete = 301;

        /// <summary>The rail slot a hit id names, or −1 if it is not a rail hit.</summary>
        public static int RailSlotOf(int hit)
        {
            int slot = hit - RailBase;
            return slot >= 0 && slot < NavRigGeometry.RailIds.Length ? slot : -1;
        }

        /// <summary>The manager row a hit id names, or −1 if it is not a row hit.</summary>
        public static int WaypointRowOf(int hit)
        {
            int row = hit - WaypointRowBase;
            return row >= 0 && row < NavRigGeometry.ManagerWaypointRows ? row : -1;
        }

        /// <summary>
        /// The console face's layout at native size — the frame every hit box below is measured in.
        /// Recomputed rather than cached: <see cref="NavRigGeometry.ComputeLayout"/> is arithmetic on six
        /// integers, and a cached copy is one more thing that can go stale against the renderer.
        /// </summary>
        public static NavRigGeometry.Layout NativeLayout() => NativeLayout(NavRigGeometry.Face.Console);

        /// <summary>The native-size layout of either face — the frame that face's hit boxes are
        /// measured in. Same reasoning as above: arithmetic on six integers, never cached.</summary>
        public static NavRigGeometry.Layout NativeLayout(NavRigGeometry.Face face)
            => face == NavRigGeometry.Face.Max
                ? NavRigGeometry.ComputeLayout(0, 0, NavRigGeometry.MaxW, NavRigGeometry.MaxH,
                                               NavRigGeometry.Face.Max)
                : NavRigGeometry.ComputeLayout(0, 0, NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH,
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

        /// <summary>
        /// What a click at <paramref name="rigPx"/> lands on, on the MAX FACE.
        ///
        /// <para><paramref name="waypointCount"/> / <paramref name="routeCount"/> / <paramref name="selected"/>
        /// are passed in because the manager column is a running CURSOR, not a grid: how far down the
        /// route section starts depends on how many waypoints are listed above it, and the action
        /// buttons exist only while a row is selected. Feeding the hit map the same three numbers the
        /// renderer was given is what makes the box you press the box you saw
        /// (<see cref="NavRigGeometry.ComputeManager"/>'s remarks).</para>
        ///
        /// <para><b>Order matters, same rule as the console face:</b> the pushers first (their own
        /// column, outside the glass), then the rail, then the manager's buttons before its rows before
        /// its empty space, then the legend inside the data strip before the strip, and the chart last
        /// so a miss inside the glass is inert rather than a collapse.</para>
        /// </summary>
        public static int HitTestMax(Vector2 rigPx, int waypointCount, int routeCount, bool selected)
        {
            NavRigGeometry.Layout L = NativeLayout(NavRigGeometry.Face.Max);

            for (int i = 0; i < 4; i++)
                if (Contains(L.Key(i), rigPx)) return i;

            for (int slot = 0; slot < NavRigGeometry.RailIds.Length; slot++)
                if (NavRigGeometry.TryRailButton(in L, slot, out RectInt btn) && Contains(btn, rigPx))
                    return RailBase + slot;

            if (Contains(L.Right, rigPx))
            {
                NavRigGeometry.Manager m = NavRigGeometry.ComputeManager(in L, waypointCount, routeCount,
                                                                         selected);
                if (m.NameButton.width > 0 && Contains(m.NameButton, rigPx)) return ActionName;
                if (m.DeleteButton.width > 0 && Contains(m.DeleteButton, rigPx)) return ActionDelete;
                for (int i = 0; i < m.WaypointRowCount; i++)
                    if (Contains(m.WaypointRow(i), rigPx)) return WaypointRowBase + i;
                return ManagerHit;
            }

            if (Contains(OrientBox(in L), rigPx)) return OrientHit;
            if (Contains(L.TopBar, rigPx)) return NightHit;
            if (Contains(L.BotBar, rigPx)) return RouteHit;
            if (Contains(L.Profile, rigPx)) return ProfileHit;
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
            => TryChartTapToWorld(rigPx, in st, NavRigGeometry.Face.Console, out world, out radiusMetres);

        /// <summary>
        /// The same conversion for either face. The MAX face's chart box is a different size and sits
        /// in a different place (the rail takes the left of the glass, the manager column the right), so
        /// a tap converted through the console's box would land in the wrong water by hundreds of
        /// metres — which is exactly the class of bug one shared mapper exists to prevent.
        /// </summary>
        public static bool TryChartTapToWorld(Vector2 rigPx, in NavRigState st, NavRigGeometry.Face face,
                                              out Vector2 world, out float radiusMetres)
        {
            world = default;
            radiusMetres = 0f;
            NavRigGeometry.Layout L = NativeLayout(face);
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
        ///
        /// <para><b>Measured on the CONSOLE face for both faces, on purpose.</b> The MAX face's chart
        /// box is NARROWER (the rail and the manager column take a third of the glass), so a console
        /// pixel covers less water than a MAX one. Using the finer of the two quanta everywhere can
        /// only ever cost an extra repaint the MAX face did not strictly owe; using the coarser one
        /// could hold a stale picture on the console face, which is a lying instrument. One quantum,
        /// and it errs in the safe direction.</para>
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
