using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A kept picture's little image, as saved</b> (save v28; owner 2026-11-26: "yes photoalbum should
    /// save small pictures as little poloraids"). The ONE image the save carries per picture: a thumbnail a
    /// few art pixels across, the size set by the camera's def (<see cref="PhotoCameraDef.ThumbnailWidthPx"/>,
    /// <see cref="PhotoCameraDef.ThumbnailHeightPx"/>), drawn in the album one kit pixel per thumbnail pixel,
    /// point filtered.
    ///
    /// <para><b>The stored form</b> is one Base64 string: two header bytes (width, height), then each pixel
    /// as RGB565, two bytes little-endian, rows bottom to top (the order <c>Texture2D.GetPixels32</c> gives
    /// and <c>SetPixels32</c> takes). No alpha: a picture is opaque. So a picture costs
    /// <see cref="ByteLength"/> bytes before Base64 and <see cref="EncodedLength"/> characters after.</para>
    ///
    /// <para>Pure and engine-light (only <see cref="Color32"/>); the capture itself is the Player module's.
    /// A string that does not decode is not an image: the load repair clears it and the album draws the
    /// Polaroid's frame with no picture in it, keeping the record.</para>
    /// </summary>
    public static class PhotoThumbnail
    {
        /// <summary>Bytes before the pixels: width, then height.</summary>
        public const int HeaderBytes = 2;

        /// <summary>Bytes per pixel in RGB565.</summary>
        public const int BytesPerPixel = 2;

        /// <summary>The widest or tallest a stored thumbnail can be (a header byte holds the side).</summary>
        public const int MaxSide = byte.MaxValue;

        /// <summary>Raw bytes for a <paramref name="width"/> by <paramref name="height"/> thumbnail.</summary>
        public static int ByteLength(int width, int height) => HeaderBytes + BytesPerPixel * width * height;

        /// <summary>Base64 characters for a <paramref name="width"/> by <paramref name="height"/> thumbnail
        /// (what the save's JSON holds, before the field name and quotes).</summary>
        public static int EncodedLength(int width, int height) => 4 * ((ByteLength(width, height) + 2) / 3);

        /// <summary>
        /// The stored string for <paramref name="pixels"/> (rows bottom to top, <paramref name="width"/> by
        /// <paramref name="height"/>). Null when the sizes are out of range or the array is short.
        /// Allocates the string (once per shutter press).
        /// </summary>
        public static string Encode(Color32[] pixels, int width, int height)
        {
            if (pixels == null || width < 1 || height < 1 || width > MaxSide || height > MaxSide) return null;
            int n = width * height;
            if (pixels.Length < n) return null;
            var bytes = new byte[ByteLength(width, height)];
            bytes[0] = (byte)width;
            bytes[1] = (byte)height;
            for (int i = 0; i < n; i++)
            {
                Color32 c = pixels[i];
                int v = ((c.r >> 3) << 11) | ((c.g >> 2) << 5) | (c.b >> 3);
                bytes[HeaderBytes + 2 * i] = (byte)(v & 0xFF);
                bytes[HeaderBytes + 2 * i + 1] = (byte)(v >> 8);
            }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Read a stored thumbnail's size without decoding the pixels. False for an empty or broken string
        /// (bad Base64, a zero side, or a length that does not match its header).
        /// </summary>
        public static bool TryReadSize(string stored, out int width, out int height)
        {
            width = height = 0;
            byte[] bytes = Bytes(stored);
            if (bytes == null) return false;
            width = bytes[0];
            height = bytes[1];
            return true;
        }

        /// <summary>True when <paramref name="stored"/> is a whole thumbnail.</summary>
        public static bool IsValid(string stored) => Bytes(stored) != null;

        /// <summary>
        /// Decode <paramref name="stored"/> into new pixels (rows bottom to top, opaque). Null for a broken
        /// string. Each 5- or 6-bit channel is widened back to 8 bits by repeating its top bits, so white
        /// stays white and black stays black.
        /// </summary>
        public static Color32[] Decode(string stored, out int width, out int height)
        {
            width = height = 0;
            byte[] bytes = Bytes(stored);
            if (bytes == null) return null;
            width = bytes[0];
            height = bytes[1];
            int n = width * height;
            var pixels = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                int v = bytes[HeaderBytes + 2 * i] | (bytes[HeaderBytes + 2 * i + 1] << 8);
                int r5 = (v >> 11) & 0x1F, g6 = (v >> 5) & 0x3F, b5 = v & 0x1F;
                pixels[i] = new Color32((byte)((r5 << 3) | (r5 >> 2)), (byte)((g6 << 2) | (g6 >> 4)),
                                        (byte)((b5 << 3) | (b5 >> 2)), 255);
            }
            return pixels;
        }

        /// <summary>
        /// Point-sample <paramref name="source"/> (<paramref name="sourceWidth"/> by
        /// <paramref name="sourceHeight"/>, rows bottom to top) down to <paramref name="width"/> by
        /// <paramref name="height"/> into <paramref name="into"/>: each thumbnail pixel takes the one source
        /// pixel at its centre, never a blend (no mixels). False, writing nothing, on a bad size or a short
        /// array.
        /// </summary>
        public static bool PointSample(Color32[] source, int sourceWidth, int sourceHeight,
                                       Color32[] into, int width, int height)
        {
            if (source == null || into == null || sourceWidth < 1 || sourceHeight < 1 || width < 1 || height < 1)
                return false;
            if (source.Length < sourceWidth * sourceHeight || into.Length < width * height) return false;
            for (int y = 0; y < height; y++)
            {
                int sy = Mathf.Min(sourceHeight - 1, (int)((y + 0.5f) * sourceHeight / height));
                for (int x = 0; x < width; x++)
                {
                    int sx = Mathf.Min(sourceWidth - 1, (int)((x + 0.5f) * sourceWidth / width));
                    into[y * width + x] = source[sy * sourceWidth + sx];
                }
            }
            return true;
        }

        private static byte[] Bytes(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(stored); }
            catch (FormatException) { return null; }
            if (bytes.Length < HeaderBytes) return null;
            int w = bytes[0], h = bytes[1];
            if (w < 1 || h < 1 || bytes.Length != ByteLength(w, h)) return null;
            return bytes;
        }
    }
}
