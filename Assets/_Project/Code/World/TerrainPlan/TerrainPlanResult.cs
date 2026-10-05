using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;

namespace HiddenHarbours.World
{
    /// <summary>A pool the plan cut, as the manifest records it (terrain pass 9 part 2's pan record).</summary>
    [Serializable]
    public struct TerrainPlanPoolRecord
    {
        public string Id;
        public string Source;
        public double CentreX, CentreY, RadiusA, RadiusB, RotationDeg, Depth;
        public double Spill, Bed, AreaM2, MaxWater;
    }

    /// <summary>A frozen piece (tier 2) and how much of it pass 9's protected set already held.</summary>
    [Serializable]
    public struct TerrainPlanPieceRecord
    {
        public string Id;
        public int Cells;
        public int AlreadyHeld;
        public bool CentreHeld;
        public double MaxMove;
    }

    /// <summary>
    /// <b>WHAT THE PLAN DERIVES.</b> The maps on the seabed grid (row 0 north), the records the manifest carries, and
    /// the order the steps ran in. A pure function of the plan and its sources: the same inputs give the same
    /// <see cref="Sha256"/>, on every machine.
    /// </summary>
    public sealed class TerrainPlanResult
    {
        public TerrainPlanGrid Grid;

        /// <summary>Today's ground (the analytic terrain) and the plan's ground (m): the ground file's import when the plan has one.</summary>
        public double[] Base, E;

        /// <summary>
        /// With a ground file, the ground the plan's own steps derived: no longer the map's, kept as the check against the
        /// import (amendment 1 §4.6). Null without one, when <see cref="E"/> is the derived ground.
        /// </summary>
        public double[] EDerived;

        /// <summary>
        /// With a ground file, its import's R16 codes (Unity order, row 0 south): the height map writes them as they are, and
        /// <see cref="E"/> is their decode. Null without one, when the map encodes <see cref="E"/>. Not in <see cref="Sha256"/>.
        /// </summary>
        public ushort[] GroundCodes;

        /// <summary>Part 1's ground and paint, before the crossing and the key scenes (the tier-2 heights).</summary>
        public double[] E1;
        public byte[] Zone1;

        /// <summary>Part 1's coast: each cell's section (its index in the plan's Sections, -1 off the coast) and its weight.</summary>
        public short[] SectionOf;
        public double[] SectionWeight;

        /// <summary>The ground after the crossing, before the key scenes.</summary>
        public double[] E2;

        /// <summary>
        /// Which key-scene piece last moved each cell's ground by more than the plan's rule (index into
        /// <see cref="KeyOwners"/>, 0 = none): the Head's pieces, the platform's rock pools, the fall.
        /// </summary>
        public byte[] KeyOwner;
        public readonly List<string> KeyOwners = new List<string> { "" };

        /// <summary>The still-water surface (m; NaN = none).</summary>
        public double[] Still;

        /// <summary>The ground zone per cell (TerrainPlanZones; every cell is painted).</summary>
        public byte[] Zone;

        /// <summary>The path slot's weight where a light path (worn grass) lies over the zone; 0 elsewhere.</summary>
        public double[] PathWeight;

        /// <summary>The biome per cell (0 = none, then the plan's biomes in order, then the woods floor).</summary>
        public byte[] Biome;

        /// <summary>Channel and tidal-channel masks; the distance to fresh water's edge (∞ = none).</summary>
        public bool[] Chan, Tidal;
        public double[] Bank;

        /// <summary>Part 1's protection (p_elev, p_paint) and the crossing's.</summary>
        public double[] Pe1, Pp1, Pe2, Pp2;

        /// <summary>The frozen tiers: 1 = pass 9's frozen mask (tier 1), 2 = the key scenes' frozen pieces (tier 2).</summary>
        public byte[] Frozen;

        /// <summary>
        /// Why each cell is frozen, as bits of the keep's parts at the frozen level: 1 the crossing's keep, 2 part 1's keep
        /// less its bar, 4 the far west strip, 8 a key scene's frozen piece (tier 2); and part 1's keep by part: 16 the
        /// common keep, 32 the south sector, 64 the cliffs, 128 the cliffs outside the south sector. Not in <see cref="Sha256"/>.
        /// </summary>
        public byte[] FrozenWhy;

        /// <summary>The bits of <see cref="FrozenWhy"/>.</summary>
        public const byte WhyCrossing = 1, WhyPart1 = 2, WhyWest = 4, WhyTier2 = 8, WhyCommon = 16, WhySouth = 32, WhyCliffs = 64, WhyCliffsOut = 128;

        /// <summary>
        /// The keep's parts a ground file's import holds at today's ground (amendment 1 §4.7): the common keep, the cliffs
        /// outside the south sector and the far west strip. The south sector and its cliffs are the file's.
        /// </summary>
        public const byte WhyHeldOnTheImport = WhyCommon | WhyCliffsOut | WhyWest;

        /// <summary>
        /// With a ground file, the zone each of its bays paints (TerrainPlanZones; Unpainted where none does), before the paths
        /// and the keep paint over it. Null without a bay. Not in <see cref="Sha256"/>.
        /// </summary>
        public byte[] BayPaint;

        /// <summary>Part 2's south (terrain PR 5w): its masks and what it painted. Null without part 2's sections. Not in <see cref="Sha256"/>.</summary>
        public TerrainPlanSouth South;

        /// <summary>The distance to the nearest painted path's centre, its half width, its index in the plan's list.</summary>
        public double[] PathD, PathH;
        public int[] PathW;

        /// <summary>The key scenes' paths and lots (1 = on one), for the cull.</summary>
        public bool[] KeyPaint;

        public readonly List<TerrainPlanPoolRecord> Pools = new List<TerrainPlanPoolRecord>();

        /// <summary>With a ground file, the hollows that keep water on the import (part 2 §7.4): area in m² of ground.</summary>
        public readonly List<TerrainPlanPoolRecord> Hollows = new List<TerrainPlanPoolRecord>();
        public readonly List<TerrainPlanPieceRecord> Pieces = new List<TerrainPlanPieceRecord>();

        /// <summary>Each step, in the order it ran, with what it changed.</summary>
        public readonly List<string> Order = new List<string>();

        /// <summary>Counts and measures the steps recorded (name → value, invariant culture).</summary>
        public readonly SortedDictionary<string, string> Numbers = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public void Note(string key, double v) => Numbers[key] = v.ToString("R", CultureInfo.InvariantCulture);
        public void Note(string key, long v) => Numbers[key] = v.ToString(CultureInfo.InvariantCulture);
        public void Note(string key, string v) => Numbers[key] = v;

        /// <summary>
        /// SHA-256 of the derived maps in a fixed layout: E, Still (NaN as −9999, pass 9's arr_sha rule) and PathWeight as
        /// little-endian float64, then Zone, Biome, Chan, Tidal and Frozen as bytes.
        /// </summary>
        public string Sha256()
        {
            using (var sha = SHA256.Create())
            {
                var buf = new byte[8 * 4096];
                void Doubles(double[] a)
                {
                    int k = 0;
                    for (int i = 0; i < a.Length; i++)
                    {
                        double v = double.IsNaN(a[i]) ? -9999.0 : a[i];
                        long bits = BitConverter.DoubleToInt64Bits(v);
                        for (int b = 0; b < 8; b++) buf[k++] = (byte)(bits >> (8 * b));
                        if (k == buf.Length) { sha.TransformBlock(buf, 0, k, null, 0); k = 0; }
                    }
                    if (k > 0) sha.TransformBlock(buf, 0, k, null, 0);
                }
                void Bytes(byte[] a) => sha.TransformBlock(a, 0, a.Length, null, 0);
                void Bools(bool[] a)
                {
                    var o = new byte[a.Length];
                    for (int i = 0; i < a.Length; i++) o[i] = a[i] ? (byte)1 : (byte)0;
                    Bytes(o);
                }
                Doubles(E); Doubles(Still); Doubles(PathWeight);
                Bytes(Zone); Bytes(Biome); Bools(Chan); Bools(Tidal); Bytes(Frozen);
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return Hex(sha.Hash);
            }
        }

        public static string Hex(byte[] h)
        {
            var c = new char[h.Length * 2];
            const string d = "0123456789abcdef";
            for (int i = 0; i < h.Length; i++) { c[2 * i] = d[h[i] >> 4]; c[2 * i + 1] = d[h[i] & 15]; }
            return new string(c);
        }
    }
}
