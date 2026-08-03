using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The helm overlay's PURE layout + pointer-mapping maths (ADR 0025 S1), split out of the host so
    /// the small-card / click-to-FOCUS geometry and the screen→rig mapping are EditMode-testable
    /// without a canvas. All dimensions come from <see cref="HelmOverlaySettings"/> (rule 6 — scale
    /// factors and anchors are data, not literals). Screen space is Unity's: origin bottom-left,
    /// y up; RIG space is the rigs': origin top-left, y down.
    /// </summary>
    public static class HelmOverlayLayout
    {
        /// <summary>
        /// The instrument card's screen rect for a state — BOTH states anchor CENTRED AT THE BOTTOM
        /// of the screen (the owner's placement ruling, 2026-08-03, replacing S1's bottom-right
        /// placeholder): the small dash card sits at <c>SmallCenterX01</c>/<c>MarginY</c>, and the
        /// FOCUSED state rises from the same bottom anchor at <c>FocusScale</c>, so the rig's
        /// controls — and its hit geometry, which scales with the same rect — are properly
        /// clickable. All anchors are data (rule 6).
        /// </summary>
        public static Rect CardRect(bool focused, int rigW, int rigH, in HelmOverlaySettings s,
                                    float screenW, float screenH)
        {
            float scale = focused ? s.FocusScale : s.SmallScale;
            float w = rigW * scale, h = rigH * scale;
            float cx = screenW * (focused ? s.FocusCenterX01 : s.SmallCenterX01);
            return new Rect(cx - w * 0.5f, s.MarginY, w, h);
        }

        /// <summary>
        /// The tiller handle's Z rotation (deg, Unity CCW-positive) for a helm steer — the
        /// PHYSICAL tiller sense (owner fix 2026-08-03: "it should match the outboard tiller's
        /// position"): on a real outboard the whole motor pivots about the clamp, so steering to
        /// STARBOARD (+1) pushes the handle to PORT. The rig's own README
        /// (docs/art/rigs/ui/outboard-tiller/README.md, "Steering is not a render parameter":
        /// <c>ctx.rotate(steer * maxSteer)</c>, canvas clockwise-positive) swings the handle TOWARD
        /// the turn — a display-joystick convention its preview harness shares, which is exactly
        /// the backwards feel the owner flagged. So this deliberately INVERTS the harness's
        /// cosmetic sense: +steer → +Z (counter-clockwise on screen, y-up) → handle tip to port.
        /// Hardcoded, not a data knob — a tiller's geometry is physics, not taste; pinned by test.
        /// </summary>
        public static float TillerHandleAngleZDeg(float steer)
            => Mathf.Clamp(steer, -1f, 1f) * TillerRigRender.MaxSteerDeg;

        /// <summary>
        /// Map a screen point into RIG pixels (top-left origin, y down — the rigs' own space, so hit
        /// boxes and <c>sigFromOffset</c> apply unscaled). False when outside the card.
        /// </summary>
        public static bool ScreenToRig(Vector2 screen, Rect card, int rigW, int rigH, out Vector2 rigPx)
        {
            rigPx = default;
            if (card.width <= 0f || card.height <= 0f) return false;
            float u = (screen.x - card.xMin) / card.width;
            float v = (screen.y - card.yMin) / card.height;
            rigPx = new Vector2(u * rigW, (1f - v) * rigH);
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        /// <summary>The lever drag decision: a press within <c>GrabRadiusPx</c> (rig px) of the grip
        /// starts a CONTINUOUS drag; a press further out on the card is a travel-guide click
        /// (jump-to-sig, no drag session). Rig-space, so focus scaling needs no special-casing.</summary>
        public static bool IsOnGrip(Vector2 rigPx, float drive, float grabRadiusPx)
        {
            LeverRigGeometry.HandleOffset(drive, out double gx, out double gy);
            double dx = rigPx.x - (LeverRigGeometry.PX + gx);
            double dy = rigPx.y - (LeverRigGeometry.PY + gy);
            return dx * dx + dy * dy <= (double)grabRadiusPx * grabRadiusPx;
        }

        /// <summary>The lever pointer→signal read: offset from the hub pivot into
        /// <see cref="LeverRigGeometry.SigFromOffset"/>.</summary>
        public static float LeverSigAt(Vector2 rigPx)
            => LeverRigGeometry.SigFromOffset(rigPx.x - LeverRigGeometry.PX, rigPx.y - LeverRigGeometry.PY);

        /// <summary>
        /// The tiller's throttle drag: a vertical screen drag sweeps the drive across its full range
        /// over <c>TillerDragFullDrivePx</c> RIG pixels (up = ahead). The screen delta is divided by
        /// the card scale so the felt travel is the same in small and focused states.
        /// </summary>
        public static float TillerDragDrive(float driveAtGrab, float screenDeltaY, float cardScale,
                                            float fullDrivePx)
        {
            if (cardScale <= 0f || fullDrivePx <= 0f) return driveAtGrab;
            float rigDelta = screenDeltaY / cardScale;
            return Mathf.Clamp(driveAtGrab + rigDelta * 2f / fullDrivePx, -1f, 1f);
        }
    }
}
