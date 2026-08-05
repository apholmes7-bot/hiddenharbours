using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Where a brow instrument's glass lands ON THE DASH, in screen pixels (ADR 0025 S4.5). Pure, so
    /// the flush geometry is EditMode-testable without a canvas — the S1/S2/S3b discipline.
    ///
    /// <para><b>Why the flush face is the SAME texture, not a second drawing.</b> The instrument hosts
    /// already raster their rig once at native size and hand it to a <c>RawImage</c> that the card rect
    /// scales; "mounted on the dash" is therefore nothing but a different destination rect for that
    /// same image. That buys the arc's honesty invariant outright — the flush face and the expanded
    /// card are literally one raster, so they cannot disagree, and the shallow alarm's flash
    /// (Ruling E) is visible in both states because it is the same pixels. It also costs NOTHING:
    /// no extra raster, no software downscale, and the dash card does not repaint when an
    /// instrument's picture changes, so the measured four-dash repaint budget is untouched (rule 7).
    /// A software composite into the dash card would have paid for all three.</para>
    ///
    /// <para><b>Letterboxed, never stretched.</b> The rigs are authored at one aspect each
    /// (depth 480×330, finder 480×660) and the authored mounts are their own shape; the instrument is
    /// scaled to FIT its box and centred, so a portrait finder in a landscape skiff cutout shows a
    /// smaller whole instrument rather than a distorted one. Fonts disagree below ~83% of native
    /// (<see cref="FishFinderOverlayLayout"/>), which is why this scales the one raster rather than
    /// re-rendering the rig small — and why illegible-at-a-glance is expected and fine: the marks
    /// still light up, and the expanded state is the read.</para>
    /// </summary>
    public static class HelmInstrumentMountLayout
    {
        /// <summary>
        /// The screen rect a fit's brow sounder occupies flush on a dash card. False when the fit
        /// mounts nothing, when there is no dash, or when the card has collapsed to nothing.
        ///
        /// <para><paramref name="dashCard"/> is the live card rect (screen space, y UP);
        /// the mount box is card space (rig px, y DOWN), so the vertical flip happens here, once.</para>
        /// </summary>
        public static bool TryBrowSounderRect(in HelmFit fit, Rect dashCard, out Rect mount)
        {
            mount = default;
            if (dashCard.width <= 0f || dashCard.height <= 0f) return false;
            if (!HelmDashGeometry.TryBrowSounderBox(in fit, out int bx, out int by, out int bw, out int bh))
                return false;

            InstrumentNativeSize(fit.Sounder, out int instW, out int instH);
            if (instW <= 0 || instH <= 0) return false;

            int rigW = HelmDashGeometry.CanvasW(fit.Rig), rigH = HelmDashGeometry.CanvasH(fit.Rig);
            mount = Letterbox(dashCard, rigW, rigH, bx, by, bw, bh, instW, instH);
            return mount.width > 0f && mount.height > 0f;
        }

        /// <summary>The rig resolution an instrument rasters at — the size the letterbox preserves the
        /// aspect of. Zero for a slot with no renderer (S5/S6): a caller then mounts nothing rather
        /// than guessing a shape for an instrument that does not exist.</summary>
        public static void InstrumentNativeSize(SounderKind sounder, out int w, out int h)
        {
            switch (sounder)
            {
                case SounderKind.Depth: w = DepthRigRender.W; h = DepthRigRender.H; break;
                case SounderKind.Fish: w = FishRigRender.W; h = FishRigRender.H; break;
                default: w = 0; h = 0; break;
            }
        }

        /// <summary>
        /// Map a CARD-SPACE box (rig px, origin top-left, y down) onto <paramref name="card"/>
        /// (screen px, origin bottom-left, y up) and fit an <paramref name="instW"/>×<paramref name="instH"/>
        /// picture inside it, aspect preserved and centred.
        /// </summary>
        public static Rect Letterbox(Rect card, int rigW, int rigH,
                                     int boxX, int boxY, int boxW, int boxH,
                                     int instW, int instH)
        {
            if (rigW <= 0 || rigH <= 0 || instW <= 0 || instH <= 0) return default;

            float sx = card.width / rigW, sy = card.height / rigH;
            float bx = card.xMin + boxX * sx;
            // Card space measures y DOWN from the card's top; screen space measures y UP from its
            // bottom. The box's screen BOTTOM is therefore its far edge measured down from the top.
            float byScreen = card.yMax - (boxY + boxH) * sy;
            float bw = boxW * sx, bh = boxH * sy;
            if (bw <= 0f || bh <= 0f) return default;

            float fit = Mathf.Min(bw / instW, bh / instH);
            float w = instW * fit, h = instH * fit;
            return new Rect(bx + (bw - w) * 0.5f, byScreen + (bh - h) * 0.5f, w, h);
        }
    }
}
