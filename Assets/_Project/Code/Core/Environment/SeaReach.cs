using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The one rule for <b>how much of the sea's swell a cell receives</b> at a water level: a pure,
    /// allocation-free function of the level and the cell's sill (the lowest level at which the cell's
    /// water joins the open sea, from <see cref="SeaReachField"/>). The water shader's <c>SeaReach01</c>
    /// (<c>Include/SeaReach.hlsl</c>) is this line in HLSL; change one, change both.
    /// </summary>
    public static class SeaReachMath
    {
        /// <summary>The sill of a cell whose water is always part of the sea (open water, a beach that
        /// drains straight to it): every level gives the full swell.</summary>
        public const float AlwaysOpen = float.NegativeInfinity;

        /// <summary>The sill of a cell no water path ever joins to the sea (a basin with no outlet, an
        /// inland lake): no level gives it swell.</summary>
        public const float Unreachable = float.PositiveInfinity;

        /// <summary>The finite stand-ins the GPU texture stores for the two sentinels (a half float holds
        /// up to 65504). Any real sill is metres, so these sit far outside every tide.</summary>
        public const float TextureAlwaysOpen = -60000f;
        /// <inheritdoc cref="TextureAlwaysOpen"/>
        public const float TextureUnreachable = 60000f;

        /// <summary>
        /// The swell factor, 0..1: 0 at or below the sill (the pool is cut off), 1 once the water stands
        /// <paramref name="blendMeters"/> above it, and a smoothstep between (zero slope at both ends), so
        /// reconnection and cut-off are continuous in time with no history. A non-positive blend is
        /// treated as the smallest usable one rather than a step. A NaN level is "no information": 1, the
        /// older look.
        /// </summary>
        public static float Factor01(float waterLevel, float sill, float blendMeters)
        {
            if (sill == AlwaysOpen) return 1f;
            if (sill == Unreachable) return 0f;
            if (float.IsNaN(waterLevel) || float.IsNaN(sill)) return 1f;
            float blend = blendMeters > 1e-4f ? blendMeters : 1e-4f;
            float u = (waterLevel - sill) / blend;
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            return u * u * (3f - 2f * u);
        }

        /// <summary>
        /// The sea's share of the water at a point where <b>still water</b> (ADR 0046: a pond, a brook's
        /// fresh reach) may stand above the tide: 1 where there is none or the tide stands at or above it,
        /// 0 once the still surface stands <paramref name="bandMeters"/> above the tide, linear between.
        /// Water that stands above the sea is the still water's, not the sea's: it takes no swell, surf,
        /// wash or shore foam, as a cut-off pool takes none. A rising tide that drowns it hands it back to
        /// the sea over the same band. A NaN or missing (−∞) still level is 1, the older look.
        /// Twin: <c>SeaReachUnderStill01</c> in <c>Include/SeaReach.hlsl</c>.
        /// </summary>
        public static float UnderStill01(float waterLevel, float stillLevel, float bandMeters)
        {
            if (!(stillLevel > waterLevel)) return 1f;   // none, NaN, or the tide on top
            float band = bandMeters > 1e-4f ? bandMeters : 1e-4f;
            float u = (stillLevel - waterLevel) / band;
            return u >= 1f ? 0f : 1f - u;
        }

        /// <summary>The value the GPU texture stores for a sill (the sentinels made finite).</summary>
        public static float ToTexture(float sill)
            => sill == AlwaysOpen ? TextureAlwaysOpen : sill == Unreachable ? TextureUnreachable : sill;
    }

    /// <summary>
    /// The region's <b>sea-reach field</b>: for every cell of the seabed grid, the lowest water level at
    /// which that cell's water is joined to the open sea (its <i>sill</i>), built ONCE from the seabed
    /// and the open-sea seeds by a priority flood. A basin behind a bar has the bar's crest as its sill;
    /// open water and any ground that drains straight to the sea are <see cref="SeaReachMath.AlwaysOpen"/>;
    /// a basin with no path to the sea at all is <see cref="SeaReachMath.Unreachable"/>.
    ///
    /// <para><b>Why a threshold field and not a flood per tide.</b> "Is this cell joined to the sea at
    /// level L" is exactly "its sill is below L" (four-neighbour grid), so one flood answers every tide.
    /// The tide changes; the field never does. Nothing is flooded per frame or per tick, nothing is
    /// uploaded at a tide step, and the factor at any instant is a pure function of the deterministic
    /// level (rule 5).</para>
    ///
    /// <para><b>The flood.</b> A bucket priority flood over the seabed quantized to
    /// <see cref="SeaReachSettings.QuantumMeters"/>: O(cells + buckets) time, each cell enters the queue
    /// exactly once (its first assignment is final because buckets are popped in rising order), ties
    /// broken by cell order, so the same seabed and settings give the same field bit for bit. Seeds are
    /// map-edge cells at or below <see cref="SeaReachSettings.SeedMaxElevationMeters"/>: the ocean, never
    /// a pond that happens to touch the edge.</para>
    ///
    /// <para><b>Hollows.</b> A cell whose sill stands less than <see cref="SeaReachSettings.MinPoolDepthMeters"/>
    /// above its own floor is treated as open: ripples in a beach must not freeze the wash.</para>
    ///
    /// <para>Immutable after <see cref="Build"/>; reads are allocation-free.</para>
    /// </summary>
    public sealed class SeaReachField
    {
        /// <summary>Grid width and height in cells.</summary>
        public readonly int Width, Height;
        /// <summary>Bottom-left corner and size of the covered world rectangle (world units).</summary>
        public readonly Vector2 WorldMin, WorldSize;

        /// <summary>The depth (m) over which a cell's weight in the smoothed read ramps from dry to wet.</summary>
        public readonly float WetBandMeters;

        private readonly float[] _sills;
        private readonly float[] _floors;

        private SeaReachField(float[] sills, float[] floors, int width, int height, Vector2 worldMin, Vector2 worldSize,
                              float wetBandMeters)
        {
            _sills = sills;
            _floors = floors;
            Width = width;
            Height = height;
            WorldMin = worldMin;
            WorldSize = worldSize;
            WetBandMeters = wetBandMeters;
        }

        /// <summary>The seabed height of cell <paramref name="index"/> (the flood's input).</summary>
        public float FloorAtIndex(int index) => _floors[index];

        /// <summary>Number of cells (row-major, row 0 at the bottom of the world rectangle).</summary>
        public int CellCount => _sills.Length;

        /// <summary>The sill of cell <paramref name="index"/> (row-major, y outer).</summary>
        public float SillAtIndex(int index) => _sills[index];

        /// <summary>The sill of cell (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public float SillAtCell(int x, int y) => _sills[y * Width + x];

        /// <summary>
        /// The sill under a world position: the cell whose square contains it (point sampled, never
        /// filtered, so a connected cell's sill can never leak into a cut-off basin). Off the rectangle
        /// the water is not this map's to judge: <see cref="SeaReachMath.AlwaysOpen"/>, the older look.
        /// Twin: <c>SeaReach01</c> in <c>Include/SeaReach.hlsl</c> (same cell, same off-map rule).
        /// </summary>
        public float SillAt(Vector2 worldPos)
        {
            float u = (worldPos.x - WorldMin.x) / WorldSize.x;
            float v = (worldPos.y - WorldMin.y) / WorldSize.y;
            if (!(u >= 0f && u < 1f && v >= 0f && v < 1f)) return SeaReachMath.AlwaysOpen;
            int x = Mathf.Min((int)(u * Width), Width - 1);
            int y = Mathf.Min((int)(v * Height), Height - 1);
            return _sills[y * Width + x];
        }

        /// <summary>
        /// The swell factor at a world position for a water level: <see cref="SeaReachMath.Factor01"/> of
        /// the four nearest cells, bilinear between their centres, each weighted by how wet it stands
        /// (<see cref="WetBandMeters"/> of water over its floor gives full weight; dry ground none). Dry
        /// ground never votes, so a cut-off pool's rim stays at the pool's 0 and the sea beside a dry bar
        /// stays at 1; where every voter is dry the containing cell decides. At a cell centre the read is
        /// exactly that cell's factor, and where all four cells are open it is exactly 1.
        /// Off the rectangle: 1. Allocation-free. Twin: <c>SeaReach01</c> in <c>Include/SeaReach.hlsl</c>.
        /// </summary>
        public float FactorAt(Vector2 worldPos, float waterLevel, float blendMeters)
        {
            float u = (worldPos.x - WorldMin.x) / WorldSize.x;
            float v = (worldPos.y - WorldMin.y) / WorldSize.y;
            if (!(u >= 0f && u < 1f && v >= 0f && v < 1f)) return 1f;
            if (float.IsNaN(waterLevel)) return 1f;
            int own = Mathf.Min((int)(v * Height), Height - 1) * Width + Mathf.Min((int)(u * Width), Width - 1);

            float px = u * Width - 0.5f, py = v * Height - 0.5f;
            int x0 = Mathf.FloorToInt(px), y0 = Mathf.FloorToInt(py);
            float fx = SnapFraction(px - x0, ref x0), fy = SnapFraction(py - y0, ref y0);
            int xa = Mathf.Clamp(x0, 0, Width - 1), xb = Mathf.Clamp(x0 + 1, 0, Width - 1);
            int ya = Mathf.Clamp(y0, 0, Height - 1) * Width, yb = Mathf.Clamp(y0 + 1, 0, Height - 1) * Width;
            int i00 = ya + xa, i10 = ya + xb, i01 = yb + xa, i11 = yb + xb;
            if (_sills[i00] == SeaReachMath.AlwaysOpen && _sills[i10] == SeaReachMath.AlwaysOpen
                && _sills[i01] == SeaReachMath.AlwaysOpen && _sills[i11] == SeaReachMath.AlwaysOpen) return 1f;

            float num = 0f, den = 0f;
            Vote(i00, (1f - fx) * (1f - fy), waterLevel, blendMeters, ref num, ref den);
            Vote(i10, fx * (1f - fy), waterLevel, blendMeters, ref num, ref den);
            Vote(i01, (1f - fx) * fy, waterLevel, blendMeters, ref num, ref den);
            Vote(i11, fx * fy, waterLevel, blendMeters, ref num, ref den);
            if (den <= 1e-6f) return SeaReachMath.Factor01(waterLevel, _sills[own], blendMeters);
            return num / den;
        }

        // A fraction within 1e-4 of a cell centre IS the centre: float rounding in the world-to-cell map
        // must not let a neighbour's 0 or 1 creep into a centre read. Twin in the HLSL.
        private static float SnapFraction(float f, ref int cell)
        {
            if (f > 1f - 1e-4f) { cell++; return 0f; }
            return f < 1e-4f ? 0f : f;
        }

        private void Vote(int i, float weight, float level, float blend, ref float num, ref float den)
        {
            if (weight <= 0f) return;
            float wet = (level - _floors[i]) / WetBandMeters;
            float w = weight * (wet <= 0f ? 0f : wet >= 1f ? 1f : wet);
            num += w * SeaReachMath.Factor01(level, _sills[i], blend);
            den += w;
        }

        /// <summary>
        /// Flood <paramref name="elevations"/> (metres above datum, row-major, row 0 at the bottom, one per
        /// cell over the world rectangle) from the open sea. Returns null when the input or the settings
        /// cannot describe a sea (no seed cell at all, a bad size, unusable settings): the caller then
        /// leaves the older look in place rather than guessing where the ocean is.
        /// </summary>
        public static SeaReachField Build(float[] elevations, int width, int height,
                                          Vector2 worldMin, Vector2 worldSize, in SeaReachSettings settings)
        {
            if (elevations == null || width <= 0 || height <= 0 || elevations.Length != width * height) return null;
            if (!(worldSize.x > 0f) || !(worldSize.y > 0f) || !settings.IsUsable) return null;

            int n = width * height;
            float minE = float.PositiveInfinity, maxE = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                float e = elevations[i];
                if (float.IsNaN(e) || float.IsInfinity(e)) return null;
                if (e < minE) minE = e;
                if (e > maxE) maxE = e;
            }
            float q = settings.QuantumMeters;
            long bucketsLong = (long)Math.Ceiling((maxE - minE) / q) + 1;
            if (bucketsLong > 1 << 26) return null;   // a span the quantum cannot hold sensibly
            int buckets = (int)bucketsLong;

            // keys[i]: the cell's quantized floor. reach[i]: its sill key (int.MaxValue = not reached yet).
            var keys = new int[n];
            for (int i = 0; i < n; i++)
                keys[i] = (int)Math.Round((elevations[i] - minE) / q);
            var reach = new int[n];
            for (int i = 0; i < n; i++) reach[i] = int.MaxValue;
            var next = new int[n];
            var head = new int[buckets];
            for (int b = 0; b < buckets; b++) head[b] = -1;

            float seedMax = settings.SeedMaxElevationMeters;
            bool seeded = false;
            // Seeds in cell order (bottom row, top row, then the two side columns): deterministic.
            for (int x = 0; x < width; x++)
            {
                seeded |= Seed(x, elevations, keys, reach, next, head, seedMax);
                if (height > 1) seeded |= Seed((height - 1) * width + x, elevations, keys, reach, next, head, seedMax);
            }
            for (int y = 1; y < height - 1; y++)
            {
                seeded |= Seed(y * width, elevations, keys, reach, next, head, seedMax);
                if (width > 1) seeded |= Seed(y * width + width - 1, elevations, keys, reach, next, head, seedMax);
            }
            if (!seeded) return null;

            // The flood: pop buckets in rising order. A neighbour's sill is the higher of the path's sill
            // and its own floor, which is never lower than the bucket being popped, so the first time a cell
            // is reached is final and every cell is pushed at most once (next[] needs no more room than n).
            for (int b = 0; b < buckets; b++)
            {
                while (head[b] >= 0)
                {
                    int i = head[b];
                    head[b] = next[i];
                    int cx = i % width, cy = i / width;
                    if (cx > 0) Visit(i - 1, b, keys, reach, next, head);
                    if (cx < width - 1) Visit(i + 1, b, keys, reach, next, head);
                    if (cy > 0) Visit(i - width, b, keys, reach, next, head);
                    if (cy < height - 1) Visit(i + width, b, keys, reach, next, head);
                }
            }

            int minPoolKeys = (int)Math.Round(settings.MinPoolDepthMeters / q);
            var sills = new float[n];
            for (int i = 0; i < n; i++)
            {
                int r = reach[i];
                if (r == int.MaxValue) sills[i] = SeaReachMath.Unreachable;
                else if (r - keys[i] <= minPoolKeys) sills[i] = SeaReachMath.AlwaysOpen;
                else sills[i] = minE + r * q;
            }
            return new SeaReachField(sills, (float[])elevations.Clone(), width, height, worldMin, worldSize,
                                     settings.WetBandMeters);
        }

        private static bool Seed(int i, float[] elevations, int[] keys, int[] reach, int[] next, int[] head, float seedMax)
        {
            if (!(elevations[i] <= seedMax) || reach[i] != int.MaxValue) return false;
            reach[i] = keys[i];
            next[i] = head[keys[i]];
            head[keys[i]] = i;
            return true;
        }

        private static void Visit(int j, int b, int[] keys, int[] reach, int[] next, int[] head)
        {
            if (reach[j] != int.MaxValue) return;
            int k = keys[j] > b ? keys[j] : b;
            reach[j] = k;
            next[j] = head[k];
            head[k] = j;
        }
    }

    /// <summary>
    /// The Core seam for the active region's <see cref="SeaReachField"/> (CLAUDE.md rule 4): the Art-side
    /// water publishes the field it built from the seabed it draws, and every CPU reader of the swell
    /// (through <see cref="GameServices.FetchAt"/>: hulls, buoys, the hull foam, the breakers) asks it
    /// how much swell reaches a point. One sea per region: the last publisher wins, and only the current
    /// owner can clear it. Absent a field every factor is exactly 1, the older look bit for bit.
    ///
    /// <para>Presentation and ride only: nothing saved, recomputed from the terrain at load (rule 5).</para>
    /// </summary>
    public static class SeaReach
    {
        private static object s_Owner;
        private static SeaReachField s_Field;
        private static float s_Blend = SeaReachSettings.Default.ReconnectBlendMeters;

        /// <summary>True while a field is published.</summary>
        public static bool IsActive => s_Owner != null && s_Field != null;

        /// <summary>The live field and its blend; false when none is published.</summary>
        public static bool TryGet(out SeaReachField field, out float blendMeters)
        {
            field = s_Field;
            blendMeters = s_Blend;
            return IsActive;
        }

        /// <summary>Publish a field (a null field clears this owner's entry).</summary>
        public static void Publish(object owner, SeaReachField field, float blendMeters)
        {
            if (owner == null) return;
            if (field == null) { Clear(owner); return; }
            s_Owner = owner;
            s_Field = field;
            s_Blend = blendMeters;
        }

        /// <summary>Update the blend of the live field (a config edit in Play); only its owner can.</summary>
        public static void SetBlend(object owner, float blendMeters)
        {
            if (ReferenceEquals(s_Owner, owner)) s_Blend = blendMeters;
        }

        /// <summary>Clear the field, only by its current owner.</summary>
        public static void Clear(object owner)
        {
            if (!ReferenceEquals(s_Owner, owner)) return;
            s_Owner = null;
            s_Field = null;
        }

        /// <summary>
        /// The swell factor at a world position for a water level; exactly 1 with no field. Where the
        /// region's still water (<see cref="GameServices.StillWater"/>) stands above the level, the sea's
        /// share fades out over the field's wet band (<see cref="SeaReachMath.UnderStill01"/>): a brook or a
        /// pond above the tide is still water. Exactly the field's factor wherever there is none.
        /// Twin: <c>SeaReach01</c> in <c>Include/SeaReach.hlsl</c>.
        /// </summary>
        public static float FactorAt(Vector2 worldPos, float waterLevel)
        {
            if (s_Owner == null || s_Field == null) return 1f;
            float reach = s_Field.FactorAt(worldPos, waterLevel, s_Blend);
            return reach * SeaReachMath.UnderStill01(waterLevel, GameServices.StillWater.StillLevelAt(worldPos),
                                                     s_Field.WetBandMeters);
        }
    }
}
