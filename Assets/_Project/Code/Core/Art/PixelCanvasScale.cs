using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The canvas scale that keeps a pixel-art panel on whole screen pixels.</b>
    ///
    /// <para>A canvas laid out in kit pixels drawn <c>kit</c> reference units wide (the bubble kit's
    /// <see cref="DialogueBubbleKit.ArtScale"/>) shows one kit pixel as <c>scale x kit</c> screen pixels.
    /// Unity's ScaleWithScreenSize picks <c>scale</c> from the window alone, so at 1920x1080 over a
    /// 1280x720 reference it is 1.5 and a kit pixel is 4.5 screen pixels: every other glyph column of
    /// the pixel face comes out a pixel wider than its neighbour. This keeps the same scale the
    /// window would pick, rounded DOWN to the nearest value at which a kit pixel is a whole number of
    /// screen pixels (never below one), so the face and the panel ink keep their shape on any
    /// window.</para>
    ///
    /// <para>Pure, so a test can drive every window size without a canvas.</para>
    /// </summary>
    public static class PixelCanvasScale
    {
        /// <summary>The scale Unity's ScaleWithScreenSize / MatchWidthOrHeight picks: the
        /// log-weighted mean of the width and height ratios. 1 for a degenerate input.</summary>
        public static float MatchWidthOrHeight(float screenW, float screenH, float refW, float refH, float match)
        {
            if (!(screenW > 0f) || !(screenH > 0f) || !(refW > 0f) || !(refH > 0f)) return 1f;
            float logW = Mathf.Log(screenW / refW, 2f);
            float logH = Mathf.Log(screenH / refH, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logW, logH, match));
        }

        /// <summary>
        /// <paramref name="rawScale"/> rounded down so that one kit pixel (<paramref name="kit"/>
        /// reference units) covers a whole number of screen pixels, and at least one. A
        /// <paramref name="kit"/> of zero or less returns the raw scale unchanged.
        /// </summary>
        public static float Snap(float rawScale, int kit)
        {
            if (kit <= 0) return rawScale;
            if (!(rawScale > 0f)) return 1f / kit;
            // The small bias keeps an exact tier (1.5 x 2 = 3.0000001 or 2.9999998) on its own step.
            int screenPixelsPerKitPixel = Mathf.Max(1, Mathf.FloorToInt(rawScale * kit + 1e-3f));
            return (float)screenPixelsPerKitPixel / kit;
        }

        /// <summary>Screen pixels one kit pixel covers at <paramref name="scale"/>.</summary>
        public static float ScreenPixelsPerKitPixel(float scale, int kit) => scale * kit;
    }
}
