#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The pixels of a baked fish sheet, read off disk</b>: where a fish actually is inside its padded
    /// cell, and how long it is drawn. Two builders ask this (the tight icons, the held drawing length
    /// on the catch art table) and the tests ask it again of the committed files, so the numbers in the
    /// assets can never drift from the art.
    ///
    /// <para><b>Why the PNG bytes and not the imported texture.</b> The fish sheets import with
    /// <c>isReadable</c> off (no CPU copy, the memory rule), so <see cref="Texture2D.GetPixels32()"/>
    /// on the imported asset throws. Decoding the file into a throwaway texture reads the very same
    /// pixels without changing any import setting.</para>
    ///
    /// <para>Pixel rows are Unity's: origin bottom-left, the same space a sprite's
    /// <see cref="Sprite.rect"/> is in, so a sliced cell's rect indexes the array directly.</para>
    /// </summary>
    public static class FishSheetPixels
    {
        /// <summary>An image decoded from a PNG: row-major, bottom row first.</summary>
        public readonly struct Image
        {
            public readonly Color32[] Pixels;
            public readonly int Width;
            public readonly int Height;

            public Image(Color32[] pixels, int width, int height)
            {
                Pixels = pixels;
                Width = width;
                Height = height;
            }

            public bool IsEmpty => Pixels == null || Width <= 0 || Height <= 0;

            public Color32 At(int x, int y) => Pixels[y * Width + x];
        }

        /// <summary>Decode the PNG at <paramref name="assetPath"/>, or an empty image when the file is
        /// missing or is an LFS pointer that was never pulled.</summary>
        public static Image Read(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return default;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(assetPath), markNonReadable: false)) return default;
                return new Image(tex.GetPixels32(), tex.width, tex.height);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// The smallest rect inside <paramref name="cell"/> holding every pixel with any alpha, or a
        /// zero-size rect when the cell is empty. Any alpha counts: the rig's anti-aliased fin tips are
        /// part of the fish, and trimming them would shave the silhouette the player reads.
        /// </summary>
        public static RectInt OpaqueBounds(in Image img, RectInt cell)
        {
            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
            int x0 = Mathf.Max(0, cell.xMin), x1 = Mathf.Min(img.Width, cell.xMax);
            int y0 = Mathf.Max(0, cell.yMin), y1 = Mathf.Min(img.Height, cell.yMax);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    if (img.At(x, y).a == 0) continue;
                    if (x < xMin) xMin = x;
                    if (x > xMax) xMax = x;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
            return xMax < xMin ? new RectInt(cell.x, cell.y, 0, 0)
                               : new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        /// <summary>The pixels of <paramref name="rect"/> as their own image (same row order).</summary>
        public static Image Crop(in Image img, RectInt rect)
        {
            var px = new Color32[rect.width * rect.height];
            for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                    px[y * rect.width + x] = img.At(rect.x + x, rect.y + y);
            return new Image(px, rect.width, rect.height);
        }

        /// <summary>The cell rect of the sliced sprite named <paramref name="spriteName"/> in the sheet at
        /// <paramref name="sheetPath"/>, read from the importer (no texture load).</summary>
        public static bool TryCellRect(string sheetPath, string spriteName, out RectInt rect)
        {
            rect = default;
            Sprite s = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                                    .FirstOrDefault(x => x.name == spriteName);
            if (s == null) return false;
            Rect r = s.rect;
            rect = new RectInt(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y),
                               Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height));
            return true;
        }

        /// <summary>
        /// How long the fish on a HELD sheet is drawn, in pixels: the longest side of its opaque bounds
        /// over every facing's still frame (<c>_d&lt;n&gt;_f0</c>, the frame the hands draw). 0 when the
        /// sheet is missing or has no still frames, which the readability floor reads as "unmeasured"
        /// and leaves alone.
        /// </summary>
        public static int HeldLengthPx(string heldSheetPath)
        {
            Image img = Read(heldSheetPath);
            if (img.IsEmpty) return 0;
            int best = 0;
            foreach (Sprite s in AssetDatabase.LoadAllAssetsAtPath(heldSheetPath).OfType<Sprite>())
            {
                if (!s.name.EndsWith("_f0", System.StringComparison.Ordinal)) continue;
                Rect r = s.rect;
                RectInt b = OpaqueBounds(img, new RectInt(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y),
                                                          Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height)));
                best = Mathf.Max(best, Mathf.Max(b.width, b.height));
            }
            return best;
        }

        /// <summary>
        /// How much of the fish each facing of a HELD sheet shows: the count of pixels with any alpha in
        /// each facing's still frame (<paramref name="held"/> is facing-major, <paramref name="facings"/>
        /// facings, frame 0 first, the order <c>CatchItemLibrary.KindEntry.Held</c> is wired in). A flatfish
        /// hung by its tail shows its broad side at some facings and a 2-5 px sliver at others, and this is
        /// the number that tells them apart. Null when the sheet cannot be read.
        /// </summary>
        public static int[] HeldFacingAreas(string heldSheetPath, Sprite[] held, int facings)
        {
            if (held == null || facings <= 0 || held.Length < facings || held.Length % facings != 0) return null;
            Image img = Read(heldSheetPath);
            if (img.IsEmpty) return null;
            int per = held.Length / facings;
            var areas = new int[facings];
            for (int d = 0; d < facings; d++)
            {
                Sprite s = held[d * per];
                if (s == null) continue;
                Rect r = s.rect;
                int x0 = Mathf.Max(0, Mathf.RoundToInt(r.x)), y0 = Mathf.Max(0, Mathf.RoundToInt(r.y));
                int x1 = Mathf.Min(img.Width, Mathf.RoundToInt(r.xMax)), y1 = Mathf.Min(img.Height, Mathf.RoundToInt(r.yMax));
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        if (img.At(x, y).a != 0) areas[d]++;
            }
            return areas;
        }

        /// <summary>
        /// <b>The face-on pick</b>: each facing draws whichever of itself and its two neighbours shows the
        /// most of the fish (<see cref="HeldFacingAreas"/>). A fish hung by its tail hangs nearly upright, so
        /// the neighbouring facing is the same fish turned 45 degrees about its length: an edge-on facing
        /// takes its broad neighbour, and a facing already showing its broad side keeps itself unless a
        /// neighbour shows strictly more. Ties keep the facing itself, then the lower neighbour, so the
        /// map is the same on every machine. Null for no areas.
        /// </summary>
        public static int[] BroadSideFacingMap(int[] areas)
        {
            if (areas == null || areas.Length == 0) return null;
            int n = areas.Length;
            var map = new int[n];
            for (int d = 0; d < n; d++)
            {
                int best = d;
                int lower = (d - 1 + n) % n, upper = (d + 1) % n;
                if (areas[lower] > areas[best]) best = lower;
                if (areas[upper] > areas[best]) best = upper;
                map[d] = best;
            }
            return map;
        }
    }
}
#endif
