using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The sounder card's PURE hit maths (ADR 0025 S2) — split out of its host so the controls are
    /// EditMode-testable without a canvas, exactly as <see cref="HelmOverlayLayout"/> is for the
    /// piloting control. RIG space is the rigs' (origin top-left, y down) and the screen→rig mapping is
    /// shared with the control card (<see cref="HelmOverlayLayout.ScreenToRig"/>) — one mapper, so a hit
    /// box can never mean two things.
    ///
    /// <para>Where the card goes is no longer this file's: since H2 the sounder's place on the helm is
    /// its housing in the band, and its card is raised by the one rule every instrument shares
    /// (<see cref="HelmBandLayout.RaisedCardRect"/>).</para>
    /// </summary>
    public static class SounderOverlayLayout
    {
        /// <summary>What a click at <paramref name="rigPx"/> (rig space) lands on, over the instrument's
        /// own layout. −1 = nothing, 0/1/2 = the three side pushers (SET / alarm up / alarm down),
        /// <see cref="LcdHit"/> = the glass (a tap toggles the night backlight).</summary>
        public const int LcdHit = 3;

        /// <summary>Hit-test the sounder's controls in RIG space against the standalone mount box, so
        /// the card's scale needs no special-casing (the S1 discipline).</summary>
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
