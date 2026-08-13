#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Reads <c>road.contract.json</c> — the 47 neighbour masks in atlas order, exported from the rig
    /// at bake time — and answers the one question a placement pass actually has: <b>given which of my
    /// neighbours are road, which atlas cell do I draw?</b>
    ///
    /// <para><b>⭐ THIS IS A LOOKUP, NOT A DERIVATION, AND THAT IS THE POINT.</b> The mapping from a
    /// raw 8-bit neighbourhood to one of 47 tiles involves the rig's <c>canon()</c> — which clears
    /// each diagonal bit whose two adjacent cardinals are not both set — and then an index into
    /// <c>BLOB47</c> sorted by mask. Writing that twice, once in JS and once in C#, is how a blob kit
    /// goes wrong: the tile renders perfectly in isolation and tears at every junction, so the defect
    /// survives every per-tile check and only a rendered NETWORK shows it. Hence the proof sheet, and
    /// hence this file reads the answer instead of recomputing it.</para>
    ///
    /// <para>Hand-parsed rather than <c>JsonUtility</c>'d: the contract is a flat array of four
    /// integer/string fields and a parser that fails loudly on a shape change is worth more here than
    /// one that silently deserialises defaults. (<c>JsonUtility</c> returns zeroed fields for a
    /// mismatched schema — a contract full of mask 0 would resolve every cell to "isolated" and paint
    /// a village of disconnected stubs.)</para>
    /// </summary>
    public static class RoadKitContract
    {
        /// <summary>One atlas cell: where it is, and the canonical neighbourhood it depicts.</summary>
        public readonly struct Entry
        {
            public readonly int Index, Mask;
            public readonly string Label, Axis;

            public Entry(int index, int mask, string label, string axis)
            { Index = index; Mask = mask; Label = label; Axis = axis; }

            public override string ToString() => $"[{Index}] mask {Mask} {Label}/{Axis}";
        }

        static Entry[] _cache;
        static Dictionary<int, int> _maskToIndex;

        /// <summary>The 47 entries in atlas order. Throws if the contract is missing or malformed.</summary>
        public static Entry[] Entries
        {
            get { EnsureLoaded(); return _cache; }
        }

        /// <summary>Drop the cache so a re-bake is picked up without a domain reload.</summary>
        public static void Invalidate() { _cache = null; _maskToIndex = null; }

        /// <summary>
        /// The atlas index to draw for a raw neighbour mask. Canonicalises first — clearing any
        /// diagonal whose two cardinals are not both present — exactly as the rig does, using the rig's
        /// own bit assignment.
        /// </summary>
        public static int IndexForMask(int rawMask)
        {
            EnsureLoaded();
            int canon = Canon(rawMask);
            if (_maskToIndex.TryGetValue(canon, out int index)) return index;

            throw new InvalidOperationException(
                $"[RoadKitContract] neighbour mask {rawMask} canonicalised to {canon}, which is not " +
                "one of the 47. That is not a content error — it means Canon() and the rig's canon() " +
                "have drifted apart. Re-bake the contract before touching anything else.");
        }

        /// <summary>
        /// The rig's <c>canon()</c>, and the ONE piece of its maths this file restates. It is here
        /// rather than in the contract because a mask is an input, not a row: exporting all 256
        /// pre-canonicalised masks would work too, and this is the smaller of the two evils. It is
        /// pinned against the rig, all 256 inputs, by <c>RoadKitContractTests</c>.
        /// </summary>
        public static int Canon(int mask)
        {
            int m = mask;
            if (!((m & RoadKitV3.BitN) != 0 && (m & RoadKitV3.BitE) != 0)) m &= ~RoadKitV3.BitNE;
            if (!((m & RoadKitV3.BitS) != 0 && (m & RoadKitV3.BitE) != 0)) m &= ~RoadKitV3.BitSE;
            if (!((m & RoadKitV3.BitS) != 0 && (m & RoadKitV3.BitW) != 0)) m &= ~RoadKitV3.BitSW;
            if (!((m & RoadKitV3.BitN) != 0 && (m & RoadKitV3.BitW) != 0)) m &= ~RoadKitV3.BitNW;
            return m;
        }

        static void EnsureLoaded()
        {
            if (_cache != null) return;

            string path = Path.Combine(RepoRoot, RoadKitV3.ContractPath);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"[RoadKitContract] '{RoadKitV3.ContractPath}' is missing — run " +
                    "Hidden Harbours ▸ Dev ▸ Bake Road Kit v3.", path);

            var entries = new List<Entry>();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (!line.StartsWith("{ \"index\"", StringComparison.Ordinal)) continue;
                entries.Add(new Entry(
                    ReadInt(line, "index"), ReadInt(line, "mask"),
                    ReadString(line, "label"), ReadString(line, "axis")));
            }

            if (entries.Count != RoadKitV3.BlobCount)
                throw new InvalidOperationException(
                    $"[RoadKitContract] '{RoadKitV3.ContractPath}' lists {entries.Count} tiles, the " +
                    $"kit is {RoadKitV3.BlobCount}. A truncated contract paints a village of stubs " +
                    "rather than failing, so this refuses instead.");

            var byMask = new Dictionary<int, int>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Index != i)
                    throw new InvalidOperationException(
                        $"[RoadKitContract] entry {i} declares index {entries[i].Index} — the " +
                        "contract is not in atlas order and every tile after this point would be " +
                        "drawn from the wrong cell.");
                if (byMask.ContainsKey(entries[i].Mask))
                    throw new InvalidOperationException(
                        $"[RoadKitContract] mask {entries[i].Mask} appears twice (indices " +
                        $"{byMask[entries[i].Mask]} and {i}).");
                byMask[entries[i].Mask] = i;
            }

            _cache = entries.ToArray();
            _maskToIndex = byMask;
        }

        /// <summary>Same definition as <c>RigCatalog.RepoRoot</c>; this assembly cannot see that one.</summary>
        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static int ReadInt(string line, string key)
        {
            string s = Field(line, key);
            if (!int.TryParse(s, out int v))
                throw new InvalidOperationException(
                    $"[RoadKitContract] '{key}' is not an integer in: {line}");
            return v;
        }

        static string ReadString(string line, string key) => Field(line, key).Trim('"');

        static string Field(string line, string key)
        {
            int k = line.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (k < 0)
                throw new InvalidOperationException(
                    $"[RoadKitContract] no '{key}' field in: {line}");
            int colon = line.IndexOf(':', k);
            int end = line.IndexOfAny(new[] { ',', '}' }, colon + 1);
            return line.Substring(colon + 1, end - colon - 1).Trim();
        }
    }
}
#endif
