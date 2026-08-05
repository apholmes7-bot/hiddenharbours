using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The always-on top band's own geometry, in ONE place (ADR 0025 S4.5). <see cref="HudController"/>
    /// builds the band from these numbers, and the helm overlays keep OUT of them — so the band and
    /// the dash cannot disagree about where the band ends, which is the whole point of extracting it.
    ///
    /// <para><b>Why the dash yields here and the band does not.</b> The owner's ask is that game UI
    /// stop obstructing the boat UI, and everything that CAN move or hide does (see
    /// <see cref="HelmHudSuppression"/>). The five band reads — clock, tide, wind, sea, money — are the
    /// exception: no instrument on any dash shows them, so hiding them at the helm would delete the
    /// tide and the money rather than relocate them. Nothing may be drawn OVER the dash; instead the
    /// dash (and an expanded instrument) simply size and sit themselves in the space below the band.
    /// That is the same clamp <see cref="HelmOverlayLayout.DashCardRect"/> already applies to the
    /// screen edge, told about one more edge.</para>
    ///
    /// <para><b>It really did overlap.</b> At 1920×1080 the shipped focused dash (1.5× of 600×548,
    /// bottom-anchored at MarginY 16) reached screen y 781 while the band's bottom sat at 750 — the
    /// band drew over the top of the wheelhouse, because its canvas sorts at 100 and the helm's at 60.
    /// At 1280×720 the overlap was the whole band. Pinned by test.</para>
    ///
    /// <para>Pure and static: the maths is EditMode-testable without a canvas, and it mirrors Unity's
    /// own <c>CanvasScaler.ScaleWithScreenSize</c> formula rather than guessing at it.</para>
    /// </summary>
    public static class HudBandLayout
    {
        /// <summary>The HUD canvas's reference resolution — a LANDSCAPE reference, so the code-drawn
        /// band scales up uniformly on a desktop window (the PC-first legibility bump).</summary>
        public const float RefW = 1280f, RefH = 720f;

        /// <summary>The scaler's width/height match. 0.5 = the geometric mean of the two ratios.</summary>
        public const float MatchWidthOrHeight = 0.5f;

        /// <summary>Band height in REFERENCE units. The deepest label inside it bottoms out at 196,
        /// so this is the band's own box with a little air, not a guess.</summary>
        public const float BandHeightRef = 220f;

        /// <summary>Side padding taken off the band's width, in reference units (its negative
        /// <c>sizeDelta.x</c>): 16 px each side.</summary>
        public const float SidePaddingRef = 32f;

        /// <summary>
        /// <c>CanvasScaler.ScaleMode.ScaleWithScreenSize</c>'s own factor for
        /// <see cref="MatchWidthOrHeight"/> — <c>(w/refW)^(1−m) · (h/refH)^m</c>. Screen pixels per
        /// reference unit.
        /// </summary>
        public static float ScaleFactor(float screenW, float screenH)
        {
            if (!(screenW > 0f) || !(screenH > 0f)) return 1f;
            return Mathf.Pow(screenW / RefW, 1f - MatchWidthOrHeight)
                 * Mathf.Pow(screenH / RefH, MatchWidthOrHeight);
        }

        /// <summary>
        /// How many SCREEN pixels down from the top of the screen the band occupies, safe-area inset
        /// included (the band is pushed below any notch, so the reserve has to be too). This is the
        /// height the helm overlays keep clear of.
        /// </summary>
        public static float ReservedTopPx(float screenW, float screenH, float safeAreaTopInsetPx)
            => Mathf.Max(0f, safeAreaTopInsetPx) + BandHeightRef * ScaleFactor(screenW, screenH);

        /// <summary>The reserve for the LIVE screen — the one caller-facing entry point, so every
        /// overlay reserves the same strip. The three-argument overload above is the pure one the
        /// tests drive.</summary>
        public static float ReservedTopPx()
        {
            Rect sa = Screen.safeArea;
            float topGap = Screen.height - (sa.y + sa.height);
            return ReservedTopPx(Screen.width, Screen.height, topGap);
        }

        /// <summary>
        /// Move — and only if it still will not fit, shrink — <paramref name="card"/> so it lies
        /// entirely in the screen BELOW the band. Translating first is deliberate: an expanded
        /// instrument is the state the player actually reads the glass in, so it gives up its position
        /// long before it gives up its size.
        ///
        /// <para>A shrink preserves the aspect (the rigs are authored at one shape each) and re-centres
        /// on what is left. With no room at all the result is empty rather than inverted.</para>
        /// </summary>
        public static Rect FitBelowBand(Rect card, float screenW, float screenH, float reservedTopPx)
        {
            float availTop = screenH - Mathf.Max(0f, reservedTopPx);
            if (availTop <= 0f) return new Rect(card.xMin, 0f, 0f, 0f);

            float w = card.width, h = card.height;
            if (h > availTop && h > 0f)
            {
                float k = availTop / h;                 // uniform: never distort a rig
                w *= k;
                h = availTop;
            }
            if (w > screenW && w > 0f)
            {
                float k = screenW / w;
                h *= k;
                w = screenW;
            }

            // Keep the card's centre where it was, then slide it inside the band-free box.
            float cx = card.center.x, cy = card.center.y;
            float x = cx - w * 0.5f, y = cy - h * 0.5f;
            if (y + h > availTop) y = availTop - h;
            if (y < 0f) y = 0f;
            if (x + w > screenW) x = screenW - w;
            if (x < 0f) x = 0f;
            return new Rect(x, y, w, h);
        }
    }
}
