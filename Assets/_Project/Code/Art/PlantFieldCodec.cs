using System;
using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    // PLF1: decoded length as little-endian uint32, then uint16 run + byte cell.
    public static class PlantFieldCodec
    {
        public const int MaxCells = 4000000;
        public static string Encode(byte[] cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Length > MaxCells) throw new ArgumentException("Field too large");
            var b = new List<byte> { 80, 76, 70, 1 };
            for (int k = 0; k < 4; k++) b.Add((byte)(cells.Length >> (8 * k)));
            for (int i = 0; i < cells.Length;)
            {
                byte v = cells[i]; Validate(v); int n = 1;
                while (i + n < cells.Length && n < 65535 && cells[i + n] == v) n++;
                b.Add((byte)n); b.Add((byte)(n >> 8)); b.Add(v); i += n;
            }
            return Convert.ToBase64String(b.ToArray());
        }
        public static byte[] Decode(string payload, int expectedLength)
        {
            if (expectedLength < 0 || expectedLength > MaxCells) throw new FormatException("Field size out of bounds");
            if (payload == null || payload.Length > 4 * (8 + 3 * expectedLength + 2) / 3)
                throw new FormatException("Field payload too large");
            var b = Convert.FromBase64String(payload);
            if (b.Length < 8 || b[0] != 80 || b[1] != 76 || b[2] != 70 || b[3] != 1) throw new FormatException("PLF1 header required");
            uint n = (uint)b[4] | (uint)b[5] << 8 | (uint)b[6] << 16 | (uint)b[7] << 24;
            if (n != expectedLength || (b.Length - 8) % 3 != 0) throw new FormatException("Field length mismatch");
            var cells = new byte[expectedLength]; int at = 0;
            for (int p = 8; p < b.Length; p += 3)
            {
                int run = b[p] | b[p + 1] << 8; byte v = b[p + 2]; Validate(v);
                if (run == 0 || run > cells.Length - at) throw new FormatException("Invalid field run");
                for (int j = 0; j < run; j++) cells[at++] = v;
            }
            if (at != cells.Length) throw new FormatException("Truncated field");
            return cells;
        }
        static void Validate(byte v)
        {
            if (v != 0 && (PlantFieldScatter.Species(v) == 0 || PlantFieldScatter.Stage(v) > 2))
                throw new FormatException("Reserved cell bits");
        }
    }
}
