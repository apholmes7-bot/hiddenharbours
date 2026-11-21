using System.Collections.Generic;
using HiddenHarbours.Art;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>How a chest freezer shows what is in it</b>, as plain arithmetic the art and the tests share: which
    /// fill rung its bed is drawn at, and where each fish lies in the chest's opening.
    ///
    /// <para><b>The rung</b>: empty with nothing in it, full from <see cref="FreezerDef.FullFromFraction"/> of its
    /// capacity, some in between.</para>
    ///
    /// <para><b>The lay</b>: the newest fish first, row by row from the back of the opening to the front, left to
    /// right, each at its drawn length times the Def's scale (shrunk only when it is longer than the opening is
    /// wide). A fish's top never rises above the opening's back edge and it never runs past either end; the
    /// front row may dip below the front edge, where the chest's own front rim is drawn over it. Fish that do
    /// not fit are under the ones shown, which the full bed stands for.</para>
    /// </summary>
    public static class FreezerLayout
    {
        /// <summary>The bed rung for <paramref name="used"/> of <paramref name="capacity"/> units.</summary>
        public static FreezerFillRung Rung(int used, int capacity, float fullFromFraction)
        {
            if (used <= 0) return FreezerFillRung.Empty;
            if (capacity <= 0) return FreezerFillRung.Full;
            float full = Mathf.Clamp(fullFromFraction, 1e-4f, 1f);
            return (float)used / capacity >= full - 1e-6f ? FreezerFillRung.Full : FreezerFillRung.Some;
        }

        /// <summary>One fish laid in the chest.</summary>
        public struct Placement
        {
            /// <summary>The index of the item in the list given.</summary>
            public int Item;

            /// <summary>The fish's middle, in metres from the freezer's pivot.</summary>
            public Vector2 Centre;

            /// <summary>The sprite's scale (1 = as drawn on the deck).</summary>
            public float Scale;

            /// <summary>The fish's length and height as laid, in metres.</summary>
            public Vector2 Size;

            /// <summary>Which row from the back (0) it lies in.</summary>
            public int Row;
        }

        /// <summary>How many rows of fish the opening holds.</summary>
        public static int RowsIn(Rect opening, float rowPitch)
            => rowPitch > 0f ? Mathf.Max(1, Mathf.FloorToInt(opening.height / rowPitch + 1e-4f)) : 1;

        /// <summary>
        /// Lay the fish (<paramref name="lengths"/>: each one's drawn length in metres at scale 1) in
        /// <paramref name="opening"/>; writes into <paramref name="into"/> and returns how many were laid. No
        /// allocation.
        /// </summary>
        public static int Pack(IReadOnlyList<float> lengths, int count, Rect opening, float rowPitch, float drawScale,
                               float heightOfLength, float gap, Placement[] into)
        {
            if (lengths == null || into == null || opening.width <= 0f || opening.height <= 0f) return 0;
            int rows = RowsIn(opening, rowPitch);
            float pitch = rowPitch > 0f ? rowPitch : opening.height;
            float scaleOf = drawScale > 0f ? drawScale : 1f;
            float hOf = Mathf.Max(0f, heightOfLength);
            int laid = 0, row = 0;
            float cursor = opening.xMin;
            for (int i = Mathf.Min(count, lengths.Count) - 1; i >= 0 && laid < into.Length; i--)
            {
                float drawn = lengths[i];
                if (drawn <= 0f) continue;
                float len = Mathf.Min(drawn * scaleOf, opening.width);
                if (cursor > opening.xMin && cursor + len > opening.xMax + 1e-5f)
                {
                    row++;
                    cursor = opening.xMin;
                }
                if (row >= rows) break;
                float h = Mathf.Min(len * hOf, opening.height);
                float top = opening.yMax - row * pitch;
                into[laid++] = new Placement
                {
                    Item = i,
                    Centre = new Vector2(cursor + len * 0.5f, top - h * 0.5f),
                    Scale = len / drawn,
                    Size = new Vector2(len, h),
                    Row = row,
                };
                cursor += len + gap;
            }
            return laid;
        }
    }
}
