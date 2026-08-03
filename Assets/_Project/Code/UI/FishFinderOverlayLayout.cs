using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The fish finder card's PURE placement, cadence and hit maths (ADR 0025 S3b) — split out of its host
    /// so all of it is EditMode-testable without a canvas, exactly as <see cref="SounderOverlayLayout"/> is
    /// for the sounder. Screen space is Unity's (origin bottom-left, y up); RIG space is the rigs'
    /// (origin top-left, y down) and the screen→rig mapping is shared with the control card
    /// (<see cref="HelmOverlayLayout.ScreenToRig"/>) — one mapper, so a hit box can never mean two things.
    ///
    /// <para><b>⚠ It is NOT <see cref="SounderOverlayLayout"/> with a different settings struct.</b> The
    /// finder's rig is PORTRAIT — 480×660, aspect 0.727, against the sounder's 480×330 at 1.455 — so none
    /// of the sounder's rect maths transfers, and its native card would be 61% of a 1080p screen's height
    /// where the sounder's is 31%. Hence its own scales and its own corner inset.</para>
    ///
    /// <para><b>Why the card is a SCALED BLIT of the native rig and not a smaller RENDER.</b> Drawing a
    /// half-size instrument looks like the cheaper, crisper option and is not: the rig picks its font
    /// scale from the MOUNT HEIGHT (<c>fishRig.js:323</c>) while the status strip's height comes from the
    /// GLASS WIDTH (<c>:149</c>). Those are two different scaling laws, and below ~83% of native they
    /// disagree — the 5-character key legends grow wider than the pusher column and the strip's text grows
    /// taller than the strip. The rig is authored for one size. So the surface is always 480×660 and the
    /// card state simply blits it at <c>CardScale</c>; at the shipped 0.5 that is an exact 2:1 decimation
    /// of point-filtered pixel art, which is the best a downscale can do, and the FOCUSED state the player
    /// reads properly is native and pixel-exact.</para>
    /// </summary>
    public static class FishFinderOverlayLayout
    {
        // What a click landed on. 0/1/2 are the three side pushers (MODE / ▲ / ▼).
        /// <summary>The colour sonar column — a tap toggles fish-ID (Ruling A).</summary>
        public const int SonarHit = 3;

        /// <summary>The top chrome strip — a tap toggles the night backlight (Ruling A).</summary>
        public const int StatusHit = 4;

        /// <summary>The right-edge depth scale — a tap toggles feet/metres (Ruling A).</summary>
        public const int RulerHit = 5;

        /// <summary>Smallest the card may shrink to, as a fraction of the rig's native size, so a
        /// mis-dialled scale or a tiny window degrades to "small" rather than to nothing.</summary>
        private const float MinScale = 0.2f;

        /// <summary>The on-screen scale of the native 480×660 blit in a given state, clamped so the card
        /// always fits the screen it has to sit on whatever the owner dials in. One scale drives both
        /// axes — a portrait instrument must never be stretched.</summary>
        public static float CardScale(bool focused, in FishFinderSettings s, float screenW, float screenH)
        {
            float scale = focused ? s.FocusScale : s.CardScale;
            if (!(scale > 0f)) scale = 1f;
            if (screenW > 0f) scale = Mathf.Min(scale, screenW / FishRigGeometry.W);
            if (screenH > 0f)
                scale = Mathf.Min(scale, Mathf.Max(0f, screenH - s.MarginY * 2f) / FishRigGeometry.H);
            return scale < MinScale ? MinScale : scale;
        }

        /// <summary>
        /// The finder card's screen rect: SMALL sits inset from the TOP-RIGHT corner, FOCUSED is centred
        /// on the configured anchor. Same corner as the depth sounder — it is the same brow cutout and the
        /// player only ever has one of the two — but its own scales, because a portrait instrument does
        /// not sit where a landscape one did.
        /// </summary>
        public static Rect CardRect(bool focused, in FishFinderSettings s, float screenW, float screenH)
        {
            float scale = CardScale(focused, in s, screenW, screenH);
            float w = FishRigGeometry.W * scale, h = FishRigGeometry.H * scale;
            if (!focused)
                return new Rect(screenW - s.MarginX - w, screenH - s.MarginY - h, w, h);
            return new Rect(screenW * s.FocusCenterX01 - w * 0.5f,
                            screenH * s.FocusCenterY01 - h * 0.5f, w, h);
        }

        /// <summary>
        /// Hit-test the finder's controls in RIG space against the standalone mount box, so focus scaling
        /// needs no special-casing (the S1 discipline). The pushers win over the glass regions, which are
        /// disjoint anyway.
        /// </summary>
        public static int HitTest(Vector2 rigPx)
        {
            RigRect mount = FishRigGeometry.StandaloneMount();
            FishRigLayout l = FishRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);
            for (int i = 0; i < 3; i++)
                if (l.Button(i).Contains(rigPx.x, rigPx.y)) return i;
            if (l.Status.Contains(rigPx.x, rigPx.y)) return StatusHit;
            if (l.Ruler.Contains(rigPx.x, rigPx.y)) return RulerHit;
            if (l.Sonar.Contains(rigPx.x, rigPx.y)) return SonarHit;
            return -1;
        }

        /// <summary>Overload kept explicit about the rig size the hit boxes are in, for a caller (S6) that
        /// composites the instrument at some other resolution.</summary>
        public static int HitTest(Vector2 rigPx, int rigW, int rigH)
        {
            RigRect mount = FishRigGeometry.StandaloneMount(rigW, rigH);
            FishRigLayout l = FishRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);
            for (int i = 0; i < 3; i++)
                if (l.Button(i).Contains(rigPx.x, rigPx.y)) return i;
            if (l.Status.Contains(rigPx.x, rigPx.y)) return StatusHit;
            if (l.Ruler.Contains(rigPx.x, rigPx.y)) return RulerHit;
            if (l.Sonar.Contains(rigPx.x, rigPx.y)) return SonarHit;
            return -1;
        }

        // ---- the scan cadence (rule 7's whole story for this instrument) -----------------------------

        /// <summary>
        /// Which SCAN BUCKET a moment falls in. The rig's scan phase free-runs, so a port that fed it
        /// <c>Time.time</c> would repaint — thousands of <c>fillRect</c> spans plus a whole-texture upload
        /// — <b>every frame</b>. Quantizing the phase to <c>WaterfallHz</c> and folding the bucket into
        /// the host's change key makes the repaint rate DATA the owner dials (rule 6) instead of a frame
        /// counter, and gives change-detection something that actually stops changing.
        /// </summary>
        public static long PhaseBucket(double timeSeconds, float waterfallHz)
        {
            if (!(waterfallHz > 0f)) return 0L;
            return (long)System.Math.Floor(timeSeconds * waterfallHz);
        }

        /// <summary>The scan phase (seconds — the rig's own unit) a bucket paints at. Deriving the phase
        /// back FROM the bucket is the point: two frames in the same bucket must produce a bit-identical
        /// picture, or the change key is comparing a value the painter does not use.</summary>
        public static double PhaseSeconds(long bucket, float waterfallHz)
            => waterfallHz > 0f ? bucket / (double)waterfallHz : 0.0;
    }
}
