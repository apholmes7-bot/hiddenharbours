using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The rough-edges pass's tunables for one region's cliffs, read off its <see cref="CliffFaceMixDef"/>.
    /// The default is off: a region with no mix, or a mix with both reaches at 0, draws and collides exactly as before.
    /// </summary>
    public readonly struct CliffJag
    {
        public readonly float BrowMetres, FootMetres, MinBiteMetres;
        public readonly Vector3 CellMetres, Weights;
        public readonly int Seed;
        /// <summary>The facing the roughness fades over (<see cref="CliffJagMath.FacingGain"/>): none at or under x,
        /// all of it at or over y. (0, 0) = every wall takes all of it.</summary>
        public readonly Vector2 Facing;

        public CliffJag(float browMetres, float footMetres, Vector3 cellMetres, Vector3 weights, float minBiteMetres, int seed,
                        Vector2 facing = default)
        {
            Facing = facing;
            BrowMetres = Mathf.Max(0f, browMetres);
            FootMetres = Mathf.Max(0f, footMetres);
            CellMetres = cellMetres;
            Weights = weights;
            MinBiteMetres = Mathf.Max(0f, minBiteMetres);
            Seed = seed;
        }

        public bool On => BrowMetres > 0f || FootMetres > 0f;

        public static CliffJag From(CliffFaceMixDef mix) => mix == null
            ? default
            : new CliffJag(mix.BrowJagMetres, mix.FootJagMetres, mix.JagCellMetres, mix.JagWeights, mix.MinBiteMetres, mix.Seed,
                           mix.RoughnessByFacing);
    }

    /// <summary>
    /// ⭐ The cliff line broken at more than one scale (owner 11 26: "where the jaggedness and rough edges").
    ///
    /// <para>A function of METRES ALONG THE RUN, never of the tile coordinate, so the kit's 12 m tile does not repeat
    /// in the silhouette. Each scale is a run of steps of uneven width (each pair of cells is split at a hashed point),
    /// each step pushed out (a buttress, a chip) or in (a bite, a notch) by a hashed share of its reach. The sum is
    /// rounded to the kit's texel, so the line moves in whole pixels. Brow and foot are separate lines; a point between
    /// them is the lerp, rounded again. Deterministic: integer hashes of the seed and the step, no global random.</para>
    ///
    /// <para>The collision reads the same line through <see cref="Close"/>: a morphological closing over the narrowest
    /// bite a walker may enter, so a bite narrower than that is solid and nobody wedges in it, and every bump stays
    /// exactly as drawn.</para>
    /// </summary>
    public static class CliffJagMath
    {
        const float Texel = 1f / CliffWaterlineMath.TexelsPerMetre;

        /// <summary>Metres the line is pushed OUTWARD (toward the sea) at <paramref name="alongMetres"/> down the run and
        /// <paramref name="t"/> down the face (0 the brow, 1 the foot). 0 when the jag is off.</summary>
        public static float At(in CliffJag jag, float alongMetres, float t)
        {
            if (!jag.On) return 0f;
            float brow = jag.BrowMetres > 0f ? jag.BrowMetres * Line(jag, alongMetres, 0) : 0f;
            float foot = jag.FootMetres > 0f ? jag.FootMetres * Line(jag, alongMetres, 1) : 0f;
            return Snap(Mathf.Lerp(Snap(brow), Snap(foot), Mathf.Clamp01(t)));
        }

        /// <summary>The unit line (about -1 to 1) for the brow (0) or the foot (1).</summary>
        public static float Line(in CliffJag jag, float x, int which)
        {
            float sum = 0f, w = 0f;
            for (int k = 0; k < 3; k++)
            {
                float cell = jag.CellMetres[k], weight = jag.Weights[k];
                if (cell <= 0f || weight <= 0f) continue;
                sum += weight * Step(x, cell * 2f, jag.Seed, which * 8 + k);
                w += weight;
            }
            return w > 0f ? sum : 0f;
        }

        /// <summary>One scale: a pair of cells <paramref name="pair"/> metres long, split at a hashed point, each cell a
        /// level from -1 to 1.</summary>
        static float Step(float x, float pair, int seed, int salt)
        {
            int k = Mathf.FloorToInt(x / pair);
            float split = (k + 0.25f + 0.5f * Hash01(k, seed, salt)) * pair;
            int idx = x < split ? 2 * k : 2 * k + 1;
            return Hash01(idx, seed, salt + 101) * 2f - 1f;
        }

        public static float Snap(float metres) => Mathf.Round(metres / Texel) * Texel;

        /// <summary>
        /// ⭐ How much of the roughness (the jag, and the rib warp's change of depth) a wall takes, 0 to 1, by how it
        /// faces the camera (owner 11 26: no mixels, which outranks roughness).
        ///
        /// <para>The roughness pushes a station along its OUTWARD plan normal. A column is drawn from its brow to its
        /// drawn toe (<see cref="CliffWallGeometry.DrawnDown"/>). Where the two run the same way (a south wall: both
        /// straight down the screen) a push slides the column along itself: the rows move in whole texels and the
        /// rock keeps its texel. Where they do not (a steep east or west wall: outward is across the screen and the
        /// column runs nearly down it) the same push drags a station sideways across a face that is only a sliver
        /// wide on screen, and the quads between stations stretch and fold into shards. The measure is the cosine
        /// between the two, 1 on a south wall and near 0 on a steep east one; the gain is a smooth step of it between
        /// the region's two bounds. With no bounds set every wall takes all of it. Pure.</para>
        /// </summary>
        public static float FacingGain(in CliffJag jag, Vector2 outwardPlan, Vector2 drawnDown)
        {
            float none = jag.Facing.x, full = jag.Facing.y;
            if (full <= 0f) return 1f;
            float alignment = Mathf.Abs(Vector2.Dot(outwardPlan, drawnDown));
            if (full <= none) return alignment >= full ? 1f : 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(none, full, alignment));
        }

        /// <summary>
        /// The whole plan displacement at a point of a station, metres outward: the kit profile's
        /// (<paramref name="profileMetres"/>) at the rib warp's gain, plus the jag, each held back by the station's
        /// <paramref name="facing"/> gain (the warp's gain goes to 1, the profile and the jag to nothing), and the
        /// jag's share rounded to the texel again so the line still moves in whole pixels. A wall at full facing is
        /// not multiplied at all, so a south wall is the floats it always was. The face, its strips, the turf lip and
        /// the blocker all read this one. Pure.
        /// </summary>
        public static float Outward(in CliffJag jag, float profileMetres, float warpGain, float facing,
                                    float alongMetres, float t)
        {
            float gain = Mathf.Lerp(1f, warpGain, facing);
            float metres = profileMetres * gain;
            // The kit profile pushes along the same outward normal as the jag, so across the screen it folds the
            // face into the same shards: it is held back by the same gain.
            if (facing < 1f) metres *= facing;
            if (jag.On && facing > 0f) metres += Snap(At(in jag, alongMetres, t) * gain * facing);
            return metres;
        }

        /// <summary>
        /// ⭐ The hard switch for one chunk's face mapping (owner 11 26: no mixels): true where the chunk faces
        /// ACROSS the screen, under the lower of the region's two facing bounds, on the mean over its stations of
        /// the same measure <see cref="FacingGain"/> reads. Such a chunk's face is mapped on the screen art grid
        /// (<see cref="CliffWallSurface.FaceMesh"/>, planar) and not down its sheared columns. One answer per
        /// chunk, never a blend; with no bounds set no chunk is planar. Pure.
        /// </summary>
        public static bool FacesAcrossTheScreen(in CliffJag jag, CliffWallSample[] samples)
        {
            float none = jag.Facing.x, full = jag.Facing.y;
            if (full <= 0f || none <= 0f || samples == null || samples.Length == 0) return false;
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
                sum += Mathf.Abs(Vector2.Dot(
                    CliffWallGeometry.OutwardPlan(in samples[i]),
                    CliffWallGeometry.DrawnDown(samples[i].BrowPlan, CliffWallGeometry.ToeScreen(in samples[i]))));
            return sum / samples.Length < none;
        }

        /// <summary>
        /// The line the blockers stand on: <paramref name="outward"/> (metres, one per station at
        /// <paramref name="along"/> metres down the run) closed over <paramref name="minBiteMetres"/> — the largest
        /// value within half a bite either side, then the smallest of those, so a bite narrower than that is filled
        /// and nothing else moves.
        /// </summary>
        public static float[] Close(float[] along, float[] outward, float minBiteMetres)
        {
            int n = outward == null ? 0 : outward.Length;
            var result = new float[n];
            if (n == 0) return result;
            if (minBiteMetres <= 0f || along == null || along.Length < n)
            {
                System.Array.Copy(outward, result, n);
                return result;
            }
            float half = minBiteMetres * 0.5f;
            var dilated = new float[n];
            for (int i = 0; i < n; i++)
            {
                float m = outward[i];
                for (int j = i - 1; j >= 0 && along[i] - along[j] <= half; j--) m = Mathf.Max(m, outward[j]);
                for (int j = i + 1; j < n && along[j] - along[i] <= half; j++) m = Mathf.Max(m, outward[j]);
                dilated[i] = m;
            }
            for (int i = 0; i < n; i++)
            {
                float m = dilated[i];
                for (int j = i - 1; j >= 0 && along[i] - along[j] <= half; j--) m = Mathf.Min(m, dilated[j]);
                for (int j = i + 1; j < n && along[j] - along[i] <= half; j++) m = Mathf.Min(m, dilated[j]);
                result[i] = m;
            }
            return result;
        }

        static float Hash01(int i, int seed, int salt)
        {
            unchecked
            {
                uint h = (uint)i * 0x9E3779B1u ^ (uint)seed * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
