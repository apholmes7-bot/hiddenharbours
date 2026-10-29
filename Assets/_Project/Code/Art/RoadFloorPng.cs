using System;
using System.IO;
using System.IO.Compression;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// NMC H4: one road floor map's PNG, decoded by hand into a slot's bytes, off the main thread (no texture, no
    /// importer, no engine call: <see cref="RoadFloorStreamer"/> decodes on a worker). Only what the bake writes is
    /// read: <see cref="RoadFloorMaps.ChunkTexels"/> square, RGBA at 8 bits, not interlaced, any of the five row
    /// filters. Every chunk's CRC and the stream's Adler-32 are checked, so a damaged file is refused rather than
    /// streamed (TerrainPlanPng's checks, which the editor's plan reader makes the same way).
    ///
    /// <para>Rows come out in Unity's pixel order, row 0 at the BOTTOM: the file's last row (the chunk's south
    /// edge) first. The shader's RF4Texel reads row 255 - y for the chunk's texel y from the north.</para>
    /// </summary>
    public static class RoadFloorPng
    {
        const int Side = RoadFloorMaps.ChunkTexels, Bpp = 4, Stride = Side * Bpp;

        /// <summary>The inflated rows' bytes, a filter byte each: the size of <see cref="Decode"/>'s scratch.</summary>
        public const int ScratchBytes = Side * (Stride + 1);

        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        static readonly uint[] CrcTable = BuildCrcTable();

        /// <summary>
        /// Decodes <paramref name="file"/> into <paramref name="rgba"/> (<see cref="RoadFloorMaps.MapBytes"/>,
        /// bottom row first) using <paramref name="scratch"/> (<see cref="ScratchBytes"/>). Null when decoded, else
        /// why the file is refused. Both buffers are the caller's, so a decode allocates only the inflater.
        /// </summary>
        public static string Decode(byte[] file, byte[] rgba, byte[] scratch)
        {
            if (file == null) return "no file.";
            if (rgba == null || rgba.Length != RoadFloorMaps.MapBytes) return "the output is not one map's bytes.";
            if (scratch == null || scratch.Length != ScratchBytes) return "the scratch is not one map's rows.";
            if (file.Length < 8) return "too short for a PNG.";
            for (int i = 0; i < 8; i++)
                if (file[i] != Signature[i]) return "not a PNG (the signature differs).";

            bool header = false, end = false;
            int at = 8, idatFrom = -1, idatBytes = 0, idatChunks = 0;
            byte[] joined = null;
            while (at < file.Length && !end)
            {
                if (at + 12 > file.Length) return "a chunk runs past the end of the file.";
                long len = U32(file, at);
                if (at + 12 + len > file.Length) return "a chunk runs past the end of the file.";
                int n = (int)len, data = at + 8;
                if (Crc(file, at + 4, n + 4) != U32(file, data + n)) return "a chunk's CRC does not match.";
                uint type = U32(file, at + 4);
                if (type == 0x49484452u)   // IHDR
                {
                    if (n != 13) return "IHDR is not 13 bytes.";
                    if (U32(file, data) != Side || U32(file, data + 4) != Side) return $"not {Side} px square.";
                    if (file[data + 8] != 8 || file[data + 9] != 6) return "not RGBA at 8 bits.";
                    if (file[data + 10] != 0 || file[data + 11] != 0) return "unknown compression or filter method.";
                    if (file[data + 12] != 0) return "interlaced.";
                    header = true;
                }
                else if (type == 0x49444154u)   // IDAT: the bake writes one; more are joined
                {
                    if (!header) return "IDAT before IHDR.";
                    if (idatChunks == 0) idatFrom = data;
                    else
                    {
                        var j = new byte[idatBytes + n];
                        if (joined != null) Buffer.BlockCopy(joined, 0, j, 0, idatBytes);
                        else Buffer.BlockCopy(file, idatFrom, j, 0, idatBytes);
                        Buffer.BlockCopy(file, data, j, idatBytes, n);
                        joined = j;
                    }
                    idatBytes += n;
                    idatChunks++;
                }
                else if (type == 0x49454E44u) end = true;   // IEND
                at += 12 + n;
            }
            if (!header) return "no IHDR.";
            if (idatChunks == 0) return "no image data.";
            if (!end) return "no IEND: the file is cut short.";

            string fault = joined != null ? Inflate(joined, 0, idatBytes, scratch) : Inflate(file, idatFrom, idatBytes, scratch);
            if (fault != null) return fault;

            // Unfilter in place, each row against the one above it in the file, then write it at the flipped row.
            for (int r = 0; r < Side; r++)
            {
                int row = r * (Stride + 1) + 1, prev = row - (Stride + 1);
                byte filter = scratch[row - 1];
                switch (filter)
                {
                    case 0: break;
                    case 1: for (int i = Bpp; i < Stride; i++) scratch[row + i] += scratch[row + i - Bpp]; break;
                    case 2: if (r > 0) for (int i = 0; i < Stride; i++) scratch[row + i] += scratch[prev + i]; break;
                    case 3:
                        for (int i = 0; i < Stride; i++)
                        {
                            int a = i >= Bpp ? scratch[row + i - Bpp] : 0, b = r > 0 ? scratch[prev + i] : 0;
                            scratch[row + i] += (byte)((a + b) >> 1);
                        }
                        break;
                    case 4:
                        for (int i = 0; i < Stride; i++)
                        {
                            int a = i >= Bpp ? scratch[row + i - Bpp] : 0, b = r > 0 ? scratch[prev + i] : 0;
                            int c = i >= Bpp && r > 0 ? scratch[prev + i - Bpp] : 0;
                            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                            scratch[row + i] += (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
                        }
                        break;
                    default: return $"row {r} has unknown filter type {filter}.";
                }
                Buffer.BlockCopy(scratch, row, rgba, (Side - 1 - r) * Stride, Stride);
            }
            return null;
        }

        static string Inflate(byte[] z, int from, int count, byte[] o)
        {
            if (count < 6) return "the image data is too short.";
            if ((z[from] & 0x0F) != 8 || ((z[from] << 8) | z[from + 1]) % 31 != 0 || (z[from + 1] & 0x20) != 0)
                return "the image data is not a zlib stream.";
            int got = 0;
            try
            {
                using (var ms = new MemoryStream(z, from + 2, count - 6, false))
                using (var d = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    while (got < o.Length)
                    {
                        int n = d.Read(o, got, o.Length - got);
                        if (n <= 0) break;
                        got += n;
                    }
                    if (got != o.Length) return $"the image data holds {got} bytes, not {o.Length}.";
                    if (d.Read(new byte[1], 0, 1) != 0) return "the image data runs past the image.";
                }
            }
            catch (InvalidDataException e) { return "the image data does not inflate: " + e.Message; }
            uint a = 1, b = 0;
            for (int i = 0; i < o.Length; i++) { a = (a + o[i]) % 65521; b = (b + a) % 65521; }
            if (((b << 16) | a) != U32(z, from + count - 4)) return "the image data's Adler-32 does not match.";
            return null;
        }

        static uint U32(byte[] b, int at) => (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);

        static uint Crc(byte[] b, int at, int len)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = 0; i < len; i++) c = CrcTable[(c ^ b[at + i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        static uint[] BuildCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }
    }
}
