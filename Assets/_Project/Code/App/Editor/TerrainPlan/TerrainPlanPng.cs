using System;
using System.IO;
using System.IO.Compression;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>A COMMITTED MAP, READ AS ITS FILE HOLDS IT.</b> A PNG's samples, decoded by hand: no texture, no importer, no
    /// engine call, so a headless run reads exactly what the determinism test reads. The terrain plan reads today's
    /// ground from the committed 8-bit height map and splat (the gatherer), and reads its own 16-bit maps back after
    /// writing them (the writer, the determinism test).
    ///
    /// <para>Grey, grey + alpha, RGB and RGBA at 8 or 16 bits, not interlaced (what Unity's EncodeToPNG writes); every
    /// chunk's CRC and the stream's Adler-32 are checked, so a damaged file is refused rather than read. Samples come
    /// in the file's order, row 0 at the top: the plan grid's order (row 0 north), the flip of Unity's pixel order.</para>
    /// </summary>
    public static class TerrainPlanPng
    {
        /// <summary>A decoded image: [row × Width + column] × Channels + channel, row 0 at the top.</summary>
        public sealed class Image
        {
            public int Width, Height, BitDepth, ColourType, Channels;
            public ushort[] Samples;

            /// <summary>One channel's samples, file order.</summary>
            public ushort[] Channel(int channel)
            {
                if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));
                var o = new ushort[Width * Height];
                for (int i = 0; i < o.Length; i++) o[i] = Samples[i * Channels + channel];
                return o;
            }

            /// <summary>One channel of an 8-bit image as bytes, file order.</summary>
            public byte[] Channel8(int channel)
            {
                if (BitDepth != 8) throw new InvalidDataException("[TerrainPlanPng] a " + BitDepth + "-bit image has no 8-bit channel.");
                var c = Channel(channel);
                var o = new byte[c.Length];
                for (int i = 0; i < o.Length; i++) o[i] = (byte)c[i];
                return o;
            }
        }

        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>The image a PNG file's bytes hold.</summary>
        public static Image Read(byte[] file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (file.Length < 8) throw new InvalidDataException("[TerrainPlanPng] too short for a PNG.");
            for (int i = 0; i < 8; i++)
                if (file[i] != Signature[i]) throw new InvalidDataException("[TerrainPlanPng] not a PNG (the signature differs).");

            var img = new Image();
            var idat = new MemoryStream();
            bool header = false, end = false;
            int at = 8;
            while (at < file.Length && !end)
            {
                if (at + 12 > file.Length) throw new InvalidDataException("[TerrainPlanPng] a chunk runs past the end of the file.");
                int len = (int)U32(file, at);
                if (len < 0 || at + 12 + (long)len > file.Length) throw new InvalidDataException("[TerrainPlanPng] a chunk runs past the end of the file.");
                string type = new string(new[] { (char)file[at + 4], (char)file[at + 5], (char)file[at + 6], (char)file[at + 7] });
                if (Crc(file, at + 4, len + 4) != U32(file, at + 8 + len))
                    throw new InvalidDataException("[TerrainPlanPng] the " + type + " chunk's CRC does not match.");
                int data = at + 8;
                switch (type)
                {
                    case "IHDR":
                        if (len != 13) throw new InvalidDataException("[TerrainPlanPng] IHDR is " + len + " bytes, not 13.");
                        img.Width = (int)U32(file, data);
                        img.Height = (int)U32(file, data + 4);
                        img.BitDepth = file[data + 8];
                        img.ColourType = file[data + 9];
                        if (file[data + 10] != 0 || file[data + 11] != 0) throw new InvalidDataException("[TerrainPlanPng] unknown compression or filter method.");
                        if (file[data + 12] != 0) throw new InvalidDataException("[TerrainPlanPng] interlaced PNGs are not read.");
                        switch (img.ColourType)
                        {
                            case 0: img.Channels = 1; break;
                            case 2: img.Channels = 3; break;
                            case 4: img.Channels = 2; break;
                            case 6: img.Channels = 4; break;
                            default: throw new InvalidDataException("[TerrainPlanPng] colour type " + img.ColourType + " is not read (grey, RGB, grey + alpha, RGBA only).");
                        }
                        if (img.BitDepth != 8 && img.BitDepth != 16) throw new InvalidDataException("[TerrainPlanPng] bit depth " + img.BitDepth + " is not read (8 or 16 only).");
                        if (img.Width <= 0 || img.Height <= 0) throw new InvalidDataException("[TerrainPlanPng] an empty image.");
                        header = true;
                        break;
                    case "IDAT":
                        if (!header) throw new InvalidDataException("[TerrainPlanPng] IDAT before IHDR.");
                        idat.Write(file, data, len);
                        break;
                    case "IEND":
                        end = true;
                        break;
                }
                at += 12 + len;
            }
            if (!header) throw new InvalidDataException("[TerrainPlanPng] no IHDR.");
            if (!end) throw new InvalidDataException("[TerrainPlanPng] no IEND: the file is cut short.");

            int bpp = img.Channels * img.BitDepth / 8;
            int stride = img.Width * bpp;
            var raw = Inflate(idat.ToArray(), (long)img.Height * (stride + 1));
            var prev = new byte[stride];
            var cur = new byte[stride];
            img.Samples = new ushort[img.Width * img.Height * img.Channels];
            int k = 0;
            for (int r = 0; r < img.Height; r++)
            {
                int row = r * (stride + 1);
                byte filter = raw[row];
                Buffer.BlockCopy(raw, row + 1, cur, 0, stride);
                Unfilter(filter, cur, prev, bpp, r);
                if (img.BitDepth == 8)
                    for (int i = 0; i < stride; i++) img.Samples[k++] = cur[i];
                else
                    for (int i = 0; i < stride; i += 2) img.Samples[k++] = (ushort)((cur[i] << 8) | cur[i + 1]);
                var t = prev; prev = cur; cur = t;
            }
            return img;
        }

        /// <summary>The image in a PNG file on disk.</summary>
        public static Image ReadFile(string path) => Read(File.ReadAllBytes(path));

        /// <summary>A file-order buffer (row 0 at the top) in Unity's pixel order (row 0 at the bottom), or back.</summary>
        public static T[] FlipRows<T>(T[] a, int width, int height)
        {
            if (a == null || a.Length != width * height) throw new ArgumentException("[TerrainPlanPng] the buffer is not width × height.");
            var o = new T[a.Length];
            for (int r = 0; r < height; r++) Array.Copy(a, r * width, o, (height - 1 - r) * width, width);
            return o;
        }

        static void Unfilter(byte filter, byte[] cur, byte[] prev, int bpp, int row)
        {
            int n = cur.Length;
            switch (filter)
            {
                case 0: return;
                case 1: for (int i = bpp; i < n; i++) cur[i] = (byte)(cur[i] + cur[i - bpp]); return;
                case 2: for (int i = 0; i < n; i++) cur[i] = (byte)(cur[i] + prev[i]); return;
                case 3:
                    for (int i = 0; i < n; i++) cur[i] = (byte)(cur[i] + (((i >= bpp ? cur[i - bpp] : 0) + prev[i]) >> 1));
                    return;
                case 4:
                    for (int i = 0; i < n; i++)
                    {
                        int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                        cur[i] = (byte)(cur[i] + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c));
                    }
                    return;
                default: throw new InvalidDataException("[TerrainPlanPng] row " + row + " has unknown filter type " + filter + ".");
            }
        }

        static byte[] Inflate(byte[] zlib, long expected)
        {
            if (zlib.Length < 6) throw new InvalidDataException("[TerrainPlanPng] the image data is too short.");
            if ((zlib[0] & 0x0F) != 8 || ((zlib[0] << 8) | zlib[1]) % 31 != 0 || (zlib[1] & 0x20) != 0)
                throw new InvalidDataException("[TerrainPlanPng] the image data is not a zlib stream.");
            var o = new byte[expected];
            int got = 0;
            using (var ms = new MemoryStream(zlib, 2, zlib.Length - 2))
            using (var z = new DeflateStream(ms, CompressionMode.Decompress))
            {
                while (got < o.Length)
                {
                    int n = z.Read(o, got, o.Length - got);
                    if (n <= 0) break;
                    got += n;
                }
                if (got != o.Length) throw new InvalidDataException("[TerrainPlanPng] the image data holds " + got + " bytes, not " + expected + ".");
                if (z.Read(new byte[1], 0, 1) != 0) throw new InvalidDataException("[TerrainPlanPng] the image data runs past the image.");
            }
            uint a = 1, b = 0;
            for (int i = 0; i < o.Length; i++) { a = (a + o[i]) % 65521; b = (b + a) % 65521; }
            if (((b << 16) | a) != U32(zlib, zlib.Length - 4)) throw new InvalidDataException("[TerrainPlanPng] the image data's Adler-32 does not match.");
            return o;
        }

        static uint U32(byte[] b, int at) => (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);

        static uint[] _crc;

        static uint Crc(byte[] b, int at, int len)
        {
            if (_crc == null)
            {
                var t = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    t[n] = c;
                }
                _crc = t;
            }
            uint crc = 0xFFFFFFFFu;
            for (int i = at; i < at + len; i++) crc = _crc[(crc ^ b[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
