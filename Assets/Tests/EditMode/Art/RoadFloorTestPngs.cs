using System;
using System.IO;
using System.IO.Compression;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// NMC H4: road floor PNGs made in the test, by a writer that is not the decoder under test: RGBA at 8 bits, each
    /// row under the filter the test names (0 to 4, as PNG defines them), the image data in one IDAT or split in two.
    /// The bytes are given top row first, as the file stores them.
    /// </summary>
    static class RoadFloorTestPngs
    {
        public static byte[] Encode(byte[] topDown, int side, Func<int, byte> filterOfRow, bool splitIdat = false,
                                    byte colourType = 6)
        {
            int channels = colourType == 6 ? 4 : 3, stride = side * channels;
            var raw = new byte[side * (stride + 1)];
            for (int r = 0; r < side; r++)
            {
                byte f = filterOfRow(r);
                raw[r * (stride + 1)] = f;
                for (int i = 0; i < stride; i++)
                {
                    int x = topDown[r * stride + i];
                    int a = i >= channels ? topDown[r * stride + i - channels] : 0;
                    int b = r > 0 ? topDown[(r - 1) * stride + i] : 0;
                    int c = i >= channels && r > 0 ? topDown[(r - 1) * stride + i - channels] : 0;
                    int pred = f switch
                    {
                        0 => 0,
                        1 => a,
                        2 => b,
                        3 => (a + b) >> 1,
                        4 => Paeth(a, b, c),
                        _ => throw new ArgumentOutOfRangeException(nameof(filterOfRow)),
                    };
                    raw[r * (stride + 1) + 1 + i] = (byte)(x - pred);
                }
            }

            byte[] zlib;
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var d = new DeflateStream(ms, CompressionLevel.Optimal, true)) d.Write(raw, 0, raw.Length);
                uint s1 = 1, s2 = 0;
                foreach (byte v in raw) { s1 = (s1 + v) % 65521; s2 = (s2 + s1) % 65521; }
                WriteU32(ms, (s2 << 16) | s1);
                zlib = ms.ToArray();
            }

            using (var png = new MemoryStream())
            {
                png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                PutU32(ihdr, 0, (uint)side);
                PutU32(ihdr, 4, (uint)side);
                ihdr[8] = 8;
                ihdr[9] = colourType;
                Chunk(png, "IHDR", ihdr, 0, 13);
                if (splitIdat)
                {
                    int half = zlib.Length / 2;
                    Chunk(png, "IDAT", zlib, 0, half);
                    Chunk(png, "IDAT", zlib, half, zlib.Length - half);
                }
                else Chunk(png, "IDAT", zlib, 0, zlib.Length);
                Chunk(png, "IEND", new byte[0], 0, 0);
                return png.ToArray();
            }
        }

        /// <summary>A map whose every byte names its place: no two neighbours alike, so a misplaced row or a wrong
        /// filter shows.</summary>
        public static byte[] Pattern(int side, int seed)
        {
            var o = new byte[side * side * 4];
            uint h = (uint)seed * 2654435761u + 1u;
            for (int i = 0; i < o.Length; i++)
            {
                h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                o[i] = (byte)(h ^ (uint)(i / 4 % side) ^ (uint)(i / (side * 4)) * 7u);
            }
            return o;
        }

        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        static void Chunk(Stream s, string type, byte[] data, int from, int count)
        {
            var body = new byte[4 + count];
            for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
            Buffer.BlockCopy(data, from, body, 4, count);
            WriteU32(s, (uint)count);
            s.Write(body, 0, body.Length);
            WriteU32(s, Crc(body));
        }

        public static uint Crc(byte[] b)
        {
            uint c = 0xFFFFFFFFu;
            foreach (byte v in b)
            {
                c ^= v;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            return c ^ 0xFFFFFFFFu;
        }

        static void WriteU32(Stream s, uint v)
        {
            s.WriteByte((byte)(v >> 24));
            s.WriteByte((byte)(v >> 16));
            s.WriteByte((byte)(v >> 8));
            s.WriteByte((byte)v);
        }

        public static void PutU32(byte[] b, int at, uint v)
        {
            b[at] = (byte)(v >> 24);
            b[at + 1] = (byte)(v >> 16);
            b[at + 2] = (byte)(v >> 8);
            b[at + 3] = (byte)v;
        }
    }
}
