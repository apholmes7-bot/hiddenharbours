using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The sounder card's PURE placement maths (ADR 0025 S2) — split out of its host so the small/focused
    /// geometry is EditMode-testable without a canvas, exactly as <see cref="HelmOverlayLayout"/> is for
    /// the piloting control. Screen space is Unity's (origin bottom-left, y up); RIG space is the rigs'
    /// (origin top-left, y down) and the screen→rig mapping is shared with the control card
    /// (<see cref="HelmOverlayLayout.ScreenToRig"/>) — one mapper, so a hit box can never mean two things.
    ///
    /// <para>The sounder is a BROW instrument, so its small card parks in the TOP-right: clear of the
    /// piloting control wherever the owner puts that (bottom-right or bottom-centre), and diegetically
    /// where you'd look up to read a depth.</para>
    /// </summary>
    public static class SounderOverlayLayout
    {
        /// <summary>
        /// The sounder card's screen rect: SMALL sits inset from the TOP-RIGHT corner at
        /// <c>CardScale</c>; FOCUSED is centred on the configured anchor at <c>FocusScale</c>, so the
        /// three pushers — and their hit boxes, which scale with the same rect — are properly clickable.
        /// </summary>
        public static Rect CardRect(bool focused, int rigW, int rigH, in DepthSounderSettings s,
                                    float screenW, float screenH)
        {
            float scale = focused ? s.FocusScale : s.CardScale;
            float w = rigW * scale, h = rigH * scale;
            if (!focused)
                return new Rect(screenW - s.MarginX - w, screenH - s.MarginY - h, w, h);
            return new Rect(screenW * s.FocusCenterX01 - w * 0.5f,
                            screenH * s.FocusCenterY01 - h * 0.5f, w, h);
        }

        /// <summary>What a click at <paramref name="rigPx"/> (rig space) lands on, over the instrument's
        /// own layout. −1 = nothing, 0/1/2 = the three side pushers (SET / alarm up / alarm down),
        /// <see cref="LcdHit"/> = the glass (a tap toggles the night backlight).</summary>
        public const int LcdHit = 3;

        /// <summary>Hit-test the sounder's controls in RIG space against the standalone mount box, so
        /// focus scaling needs no special-casing (the S1 discipline).</summary>
        public static int HitTest(Vector2 rigPx)
        {
            RigRect mount = DepthRigGeometry.StandaloneMount();
            DepthRigLayout layout = DepthRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);
            for (int i = 0; i < 3; i++)
                if (layout.Button(i).Contains(rigPx.x, rigPx.y)) return i;
            return layout.Lcd.Contains(rigPx.x, rigPx.y) ? LcdHit : -1;
        }
    }
}
