using System;
using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>How a grass field becomes a line of text and back.</b> Run-length encoding over a byte plane, then
    /// Base64 — because the alternative is what this whole change exists to undo.
    ///
    /// <para><b>⚠ WHY NOT JUST SERIALIZE A <c>byte[]</c>.</b> Unity's text serializer writes an array element
    /// PER LINE (<c>- 12</c>), so a 93,000-byte plane lands in the scene as 93,000 lines and roughly 400 KB
    /// — and, worse, as 93,000 lines that a merge tool has to reconcile. A Base64 string is ONE line: it
    /// diffs as a single changed line, merges as a single conflict, and costs 4 characters per 3 bytes. The
    /// RLE in front of it is what makes the number small in the first place — the sea, the walked paths, the
    /// building plots and the woods floor are long runs of one value, and St Peters' field is mostly
    /// nothing.</para>
    ///
    /// <para><b>The format.</b> A short header, then runs of <c>(count varint, value byte)</c>. The varint is
    /// the ordinary 7-bits-plus-continuation form, so a run of 46,000 empty sites costs three bytes rather
    /// than 180 two-byte pairs. Decoding is a single pass into a pre-sized array — no allocation per run, and
    /// nothing to go quadratic on.</para>
    ///
    /// <para><b>It fails loudly.</b> The header carries a magic, a version and the decoded length. A payload
    /// that is truncated, stale, or from a later format throws with a sentence saying so, rather than
    /// decoding into a half-grown meadow that somebody then has to diagnose from a screenshot.</para>
    ///
    /// <para>Pure and headless: no Unity types, no scene, no editor. Rules 5 and 7 — nothing here allocates
    /// per frame because nothing here runs per frame; it runs once, at load.</para>
    /// </summary>
    public static class GrassFieldCodec
    {
        /// <summary>Format magic — 'G','R','F' and a version. Bumping the version is how a layout change
        /// announces itself instead of silently mis-decoding an old field.</summary>
        const byte Magic0 = (byte)'G', Magic1 = (byte)'R', Magic2 = (byte)'F';

        /// <summary>Payload version. <b>Bump this when the RUN FORMAT changes</b>, not when the meaning of a
        /// value byte changes — the slot byte's own bit layout is <see cref="GrassFieldScatter"/>'s
        /// business.</summary>
        public const byte Version = 1;

        /// <summary>Encode a byte plane to the Base64 payload the scene stores. An empty or null plane
        /// encodes to the empty string, which is what an unbaked field looks like.</summary>
        public static string Encode(byte[] plane)
        {
            if (plane == null || plane.Length == 0) return string.Empty;

            var bytes = new List<byte>(Math.Max(16, plane.Length / 8));
            bytes.Add(Magic0); bytes.Add(Magic1); bytes.Add(Magic2); bytes.Add(Version);
            WriteVarint(bytes, (uint)plane.Length);

            int i = 0;
            while (i < plane.Length)
            {
                byte v = plane[i];
                int run = 1;
                while (i + run < plane.Length && plane[i + run] == v) run++;
                WriteVarint(bytes, (uint)run);
                bytes.Add(v);
                i += run;
            }
            return Convert.ToBase64String(bytes.ToArray());
        }

        /// <summary>
        /// Decode a payload back into its byte plane. Returns an empty array for an empty payload (an
        /// unbaked field draws nothing, which is the honest answer, not a crash).
        /// </summary>
        /// <exception cref="FormatException">The payload is not a grass field, is a version this build does
        /// not know, or does not decode to the length its own header claims.</exception>
        public static byte[] Decode(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return Array.Empty<byte>();

            byte[] bytes;
            try { bytes = Convert.FromBase64String(payload); }
            catch (FormatException e)
            {
                throw new FormatException(
                    "The grass field payload is not valid Base64 — the scene's field data is corrupt. " +
                    "Re-run the region's grass pass to rewrite it.", e);
            }

            if (bytes.Length < 5 || bytes[0] != Magic0 || bytes[1] != Magic1 || bytes[2] != Magic2)
                throw new FormatException(
                    "The grass field payload has no GRF header — this is not a grass field. Re-run the " +
                    "region's grass pass.");

            if (bytes[3] != Version)
                throw new FormatException(
                    $"The grass field payload is format version {bytes[3]}, and this build reads version " +
                    $"{Version}. Re-run the region's grass pass to rewrite it in the current format.");

            int p = 4;
            uint length = ReadVarint(bytes, ref p);
            var plane = new byte[length];

            int w = 0;
            while (p < bytes.Length)
            {
                uint run = ReadVarint(bytes, ref p);
                if (p >= bytes.Length)
                    throw new FormatException(
                        "The grass field payload ends mid-run (a count with no value) — it is truncated. " +
                        "Re-run the region's grass pass.");
                byte v = bytes[p++];
                if (w + run > plane.Length)
                    throw new FormatException(
                        $"The grass field payload decodes to more than the {plane.Length} bytes its header " +
                        "claims — it is corrupt. Re-run the region's grass pass.");
                for (uint k = 0; k < run; k++) plane[w++] = v;
            }

            if (w != plane.Length)
                throw new FormatException(
                    $"The grass field payload decoded {w} of the {plane.Length} bytes its header claims — " +
                    "it is truncated. Re-run the region's grass pass.");

            return plane;
        }

        /// <summary>How many bytes the payload occupies as stored (Base64 characters). What the scene pays,
        /// which is the number worth logging after a bake.</summary>
        public static int StoredSize(string payload) => payload?.Length ?? 0;

        static void WriteVarint(List<byte> into, uint value)
        {
            while (value >= 0x80) { into.Add((byte)(value | 0x80)); value >>= 7; }
            into.Add((byte)value);
        }

        static uint ReadVarint(byte[] bytes, ref int p)
        {
            uint value = 0;
            int shift = 0;
            while (true)
            {
                if (p >= bytes.Length)
                    throw new FormatException(
                        "The grass field payload ends mid-number — it is truncated. Re-run the region's " +
                        "grass pass.");
                byte b = bytes[p++];
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value;
                shift += 7;
                if (shift > 28)
                    throw new FormatException(
                        "The grass field payload holds an over-long number — it is corrupt. Re-run the " +
                        "region's grass pass.");
            }
        }
    }
}
