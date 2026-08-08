using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>What a press over a window's chrome landed on.</summary>
    public enum BoatUiChromeHit
    {
        None = 0,
        Bar = 1,        // the title strip — grab to MOVE
        Collapse = 2,   // the collapse-tier button
        Hide = 3,       // the × button — hides this window
        Grip = 4,       // the corner grip — grab to RESIZE
    }

    /// <summary>
    /// The boat-UI windows' PURE geometry (the owner's 2026-08-07 windowing ruling), split out so the
    /// whole windowing model — placement, resize, collapse, chrome hit boxes, drag maths — is
    /// EditMode-testable without a canvas (the S1/S2/S3b discipline). Screen space is Unity's:
    /// origin bottom-left, y up.
    ///
    /// <para><b>The one-raster invariant survives resize.</b> A window's size only ever re-targets
    /// the DESTINATION rect of the instrument's one native raster (the
    /// <see cref="HelmInstrumentMountLayout"/> contract) — nothing here re-renders a rig at a new
    /// size, so the fish finder's two font laws (which disagree below ~83% of native when
    /// RE-RENDERED, <see cref="FishFinderOverlayLayout"/>) are never in play. The size floor
    /// (<see cref="BoatUiWindowSettings.MinScale"/>) exists for grabbability, not legibility.</para>
    ///
    /// <para><b>The default state is the shipped layout, bit-exact.</b> A window nobody has touched
    /// returns its base rect UNCHANGED — no float round-trip — so shipping the windowing changes
    /// nothing until the player reaches for a bar.</para>
    /// </summary>
    public static class BoatUiWindowLayout
    {
        /// <summary>Is this state the untouched default (shipped placement, dialled size, Full)?</summary>
        public static bool IsDefault(in BoatUiWindowState st)
            => !st.HasCustomCenter
               && st.Collapse == BoatUiCollapse.Full
               && (st.Scale <= 0f || st.Scale == 1f);

        /// <summary>The window's effective scale multiplier on its base rect: the player's resize,
        /// clamped, times the collapse tier's factor. BAR is handled by the caller (the card is not
        /// drawn at all), so it scales like FULL here — the bar keeps the full footprint's width.</summary>
        public static float EffectiveScale(in BoatUiWindowState st, in BoatUiWindowSettings cfg)
        {
            float s = st.Scale > 0f ? st.Scale : 1f;
            float min = cfg.MinScale > 0f ? cfg.MinScale : 0.1f;
            float max = cfg.MaxScale > min ? cfg.MaxScale : min;
            s = Mathf.Clamp(s, min, max);
            if (st.Collapse == BoatUiCollapse.Compact)
                s *= cfg.CompactScale > 0f ? cfg.CompactScale : 1f;
            return s;
        }

        /// <summary>
        /// The window's CARD rect this frame: the base rect (whatever the shipped layout maths
        /// produced, band clamp included) re-centred and re-scaled by the window state, then kept
        /// entirely on screen — below the band's reserve, with headroom for the title bar above it.
        /// BAR collapse keeps the (scaled) width and collapses the height to zero: the caller draws
        /// the bar and nothing else.
        ///
        /// <para>Aspect is always preserved (one scale, both axes — a rig must never be stretched),
        /// and like <see cref="HudBandLayout.FitBelowBand"/> it translates before it shrinks.</para>
        /// </summary>
        public static Rect WindowRect(Rect baseRect, in BoatUiWindowState st,
                                      in BoatUiWindowSettings cfg,
                                      float screenW, float screenH, float reservedTopPx)
        {
            if (IsDefault(in st)) return baseRect;   // untouched = the shipped layout, bit-exact

            float k = EffectiveScale(in st, in cfg);
            float w = baseRect.width * k;
            float h = st.Collapse == BoatUiCollapse.Bar ? 0f : baseRect.height * k;

            Vector2 center = st.HasCustomCenter
                ? new Vector2(st.Center01.x * screenW, st.Center01.y * screenH)
                : baseRect.center;

            // The whole window — card plus the bar above it — must fit under the band's reserve.
            float barPx = Mathf.Max(0f, cfg.TitleBarPx);
            float availTop = screenH - Mathf.Max(0f, reservedTopPx) - barPx;
            if (availTop <= 0f) return new Rect(center.x - w * 0.5f, 0f, 0f, 0f);

            // Shrink (aspect preserved) only if the scaled card cannot fit at all.
            if (h > availTop && h > 0f)
            {
                float f = availTop / h;
                w *= f; h = availTop;
            }
            if (w > screenW && w > 0f)
            {
                float f = screenW / w;
                h *= f; w = screenW;
            }

            float x = center.x - w * 0.5f;
            float y = center.y - h * 0.5f;
            if (y + h > availTop) y = availTop - h;
            if (y < 0f) y = 0f;
            if (x + w > screenW) x = screenW - w;
            if (x < 0f) x = 0f;
            return new Rect(x, y, w, h);
        }

        // ---- chrome geometry ----------------------------------------------------------------------

        /// <summary>The title bar: a strip sitting directly ON TOP of the card (screen y up, so above
        /// its yMax). For a BAR-collapsed window (card height 0) it is all there is.</summary>
        public static Rect BarRect(Rect card, in BoatUiWindowSettings cfg)
            => new Rect(card.xMin, card.yMax, card.width, Mathf.Max(0f, cfg.TitleBarPx));

        /// <summary>The window's whole chrome bounds — the strip published for the press routing.
        /// The grip is inside the card and stays the card's own press (chrome-first handling), so
        /// only the bar needs publishing.</summary>
        public static Rect ChromeBounds(Rect card, in BoatUiWindowSettings cfg)
            => BarRect(card, in cfg);

        /// <summary>The × (hide) button: the bar's rightmost square.</summary>
        public static Rect HideButtonRect(Rect bar, in BoatUiWindowSettings cfg)
        {
            float b = Mathf.Min(Mathf.Max(0f, cfg.ChromeButtonPx), bar.width);
            return new Rect(bar.xMax - b, bar.yMin, b, bar.height);
        }

        /// <summary>The collapse-tier button: the square left of the × button.</summary>
        public static Rect CollapseButtonRect(Rect bar, in BoatUiWindowSettings cfg)
        {
            float b = Mathf.Min(Mathf.Max(0f, cfg.ChromeButtonPx), bar.width);
            return new Rect(bar.xMax - b * 2f, bar.yMin, b, bar.height);
        }

        /// <summary>The resize grip: a square inside the card's bottom-right corner. Zero for a
        /// BAR-collapsed window (there is no card to size).</summary>
        public static Rect GripRect(Rect card, in BoatUiWindowSettings cfg)
        {
            if (card.height <= 0f) return default;
            float g = Mathf.Min(Mathf.Max(0f, cfg.GripPx), Mathf.Min(card.width, card.height));
            return new Rect(card.xMax - g, card.yMin, g, g);
        }

        /// <summary>What a press at <paramref name="pos"/> lands on, buttons before bar (they sit in
        /// it), grip before the card body (the caller falls through to the card on None).</summary>
        public static BoatUiChromeHit HitChrome(Vector2 pos, Rect card, in BoatUiWindowSettings cfg)
        {
            Rect bar = BarRect(card, in cfg);
            if (bar.Contains(pos))
            {
                if (HideButtonRect(bar, in cfg).Contains(pos)) return BoatUiChromeHit.Hide;
                if (CollapseButtonRect(bar, in cfg).Contains(pos)) return BoatUiChromeHit.Collapse;
                return BoatUiChromeHit.Bar;
            }
            Rect grip = GripRect(card, in cfg);
            if (grip.width > 0f && grip.Contains(pos)) return BoatUiChromeHit.Grip;
            return BoatUiChromeHit.None;
        }

        // ---- drag maths ---------------------------------------------------------------------------

        /// <summary>Where a MOVE drag puts the window's centre: the centre at grab time plus the
        /// pointer's travel, as a screen fraction, clamped 0..1 so a window can never be thrown
        /// entirely off screen (WindowRect clamps the rect itself on top of this).</summary>
        public static Vector2 DragCenter01(Vector2 startCenterPx, Vector2 startPointer, Vector2 pointer,
                                           float screenW, float screenH)
        {
            if (!(screenW > 0f) || !(screenH > 0f)) return new Vector2(0.5f, 0.5f);
            Vector2 c = startCenterPx + (pointer - startPointer);
            return new Vector2(Mathf.Clamp01(c.x / screenW), Mathf.Clamp01(c.y / screenH));
        }

        /// <summary>
        /// Where a RESIZE drag (the bottom-right grip) puts the window's scale: the dominant axis of
        /// the pointer's travel away from the window (right or down = grow, since the window grows
        /// about its centre) sweeps the width, and the scale follows proportionally. Clamped into
        /// the settings' Min/MaxScale.
        /// </summary>
        public static float DragScale(float startScale, float startCardW,
                                      Vector2 startPointer, Vector2 pointer,
                                      in BoatUiWindowSettings cfg)
        {
            if (startScale <= 0f) startScale = 1f;
            if (!(startCardW > 0f)) return startScale;
            // The dominant-magnitude axis, signed — so a pull straight left (or up) SHRINKS even
            // though the other axis never moved.
            float dx = pointer.x - startPointer.x;
            float dy = startPointer.y - pointer.y;
            float growPx = Mathf.Abs(dx) >= Mathf.Abs(dy) ? dx : dy;
            float k = (startCardW + growPx * 2f) / startCardW;   // ×2: growth is centred
            float min = cfg.MinScale > 0f ? cfg.MinScale : 0.1f;
            float max = cfg.MaxScale > min ? cfg.MaxScale : min;
            return Mathf.Clamp(startScale * k, min, max);
        }
    }
}
