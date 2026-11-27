using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A surfaced fish cut at her waterline, on her own art grid.</b> One baked frame is split into two
    /// sprites on the same texture: the whole art rows above the line and the rows below it. Both keep the
    /// frame's pivot, so drawn at one transform they put every art pixel back where the frame had it; the
    /// owner then draws the lower one tinted toward the sea. No shader, no mask and no pixel is read (the
    /// sheets need not be readable): only the frame's rect is divided.
    ///
    /// <para><b>Where the line is.</b> The fish rig's pivot is the water-surface point under the body's
    /// centre (catch pass 2, cell 64x80, pivot (32,38) from the top). The cut is that row plus
    /// <c>GameConfig.FishFeel.WaterlineRowsAbovePivot</c>, in whole art pixels.</para>
    ///
    /// <para><b>Made once per frame of art.</b> The halves are cached by source frame and cut row, so a
    /// fight allocates nothing after each frame's first draw (rule 7); the cache is bounded by the baked
    /// frames. <see cref="SourceOf"/> maps a half back to its frame, so the mouth table keyed by the baked
    /// frames still answers for what is drawn.</para>
    /// </summary>
    public static class FishWaterlineCut
    {
        /// <summary>The suffix the lower half's name carries; the upper half keeps the frame's name.</summary>
        public const string UnderSuffix = "~under";

        private struct Halves
        {
            public Sprite Above;
            public Sprite Below;
        }

        private static readonly Dictionary<(Sprite frame, int cut), Halves> s_halves =
            new Dictionary<(Sprite frame, int cut), Halves>();
        private static readonly Dictionary<Sprite, Sprite> s_source = new Dictionary<Sprite, Sprite>();

        /// <summary>
        /// The cut row, counted in art pixels from the frame's BOTTOM: the pivot's row plus
        /// <paramref name="rowsAbovePivot"/>, clamped into the frame. Pure.
        /// </summary>
        public static int CutRow(float pivotYPx, int rowsAbovePivot, int heightPx)
        {
            if (heightPx <= 0 || float.IsNaN(pivotYPx)) return 0;
            int row = Mathf.RoundToInt(pivotYPx) + rowsAbovePivot;
            return Mathf.Clamp(row, 0, heightPx);
        }

        /// <summary>
        /// The two halves of <paramref name="frame"/> cut at its waterline. False (draw the frame as baked)
        /// with no frame or texture, or when the line falls on or outside the frame's bottom or top row, so
        /// there is nothing on one side of it.
        /// </summary>
        public static bool TrySplit(Sprite frame, int rowsAbovePivot, out Sprite above, out Sprite below)
        {
            above = null;
            below = null;
            if (frame == null || frame.texture == null) return false;
            Rect r = frame.rect;
            int h = Mathf.RoundToInt(r.height);
            int cut = CutRow(frame.pivot.y, rowsAbovePivot, h);
            if (cut <= 0 || cut >= h) return false;

            var key = (frame, cut);
            if (s_halves.TryGetValue(key, out Halves made) && made.Above != null && made.Below != null)
            {
                above = made.Above;
                below = made.Below;
                return true;
            }

            float ppu = frame.pixelsPerUnit;
            Vector2 pivot = frame.pivot;   // px from the rect's bottom-left
            var upperRect = new Rect(r.x, r.y + cut, r.width, r.height - cut);
            var lowerRect = new Rect(r.x, r.y, r.width, cut);
            above = Sprite.Create(frame.texture, upperRect,
                                  new Vector2(pivot.x / r.width, (pivot.y - cut) / upperRect.height),
                                  ppu, 0, SpriteMeshType.FullRect);
            below = Sprite.Create(frame.texture, lowerRect,
                                  new Vector2(pivot.x / r.width, pivot.y / lowerRect.height),
                                  ppu, 0, SpriteMeshType.FullRect);
            above.name = frame.name;
            below.name = frame.name + UnderSuffix;
            above.hideFlags = HideFlags.DontSave;
            below.hideFlags = HideFlags.DontSave;
            s_halves[key] = new Halves { Above = above, Below = below };
            s_source[above] = frame;
            s_source[below] = frame;
            return true;
        }

        /// <summary>The baked frame <paramref name="drawn"/> was cut from, or <paramref name="drawn"/> itself
        /// when it is no half.</summary>
        public static Sprite SourceOf(Sprite drawn)
        {
            if (drawn == null) return null;
            return s_source.TryGetValue(drawn, out Sprite frame) && frame != null ? frame : drawn;
        }
    }
}
