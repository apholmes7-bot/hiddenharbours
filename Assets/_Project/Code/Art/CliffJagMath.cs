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

        /// <summary>The screen grid rule along a run (<see cref="CliffJagMath.PlanarInRun"/>): x the facing a wall is
        /// laid on the screen grid under (0 = the lower facing bound), y how far either side of a chunk its facing is
        /// averaged over (metres of brow), z how far a change of mapping may be moved to reach a corner (metres),
        /// w the least turn that counts as a corner (degrees).</summary>
        public readonly Vector4 Run;

        /// <summary>The facing under which a wall is laid on the screen grid.</summary>
        public float PlanarUnder => Run.x > 0f ? Run.x : Facing.x;

        public CliffJag(float browMetres, float footMetres, Vector3 cellMetres, Vector3 weights, float minBiteMetres, int seed,
                        Vector2 facing = default, Vector4 run = default)
        {
            Facing = facing;
            Run = run;
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
                           mix.RoughnessByFacing,
                           new Vector4(mix.ScreenGridUnderFacing, mix.FacingRunWindowMetres, mix.FacingCornerReachMetres,
                                       mix.FacingCornerDegrees));
    }

    /// <summary>A run of cliff chunks as <see cref="CliffJagMath.PlanarInRun"/> reads it: who joins whom, how long
    /// each chunk's brow is, which way it runs, and how it faces the camera.</summary>
    public interface ICliffRun
    {
        /// <summary>The chunk joined to this one's first station, or -1.</summary>
        int Before(int chunk);
        /// <summary>The chunk joined to this one's last station, or -1.</summary>
        int After(int chunk);
        /// <summary>Metres of brow.</summary>
        float Length(int chunk);
        /// <summary>The unit direction from the chunk's first brow station to its last.</summary>
        Vector2 Heading(int chunk);
        /// <summary><see cref="CliffJagMath.MeanFacing"/> of the chunk's stations.</summary>
        float Facing(int chunk);
        int Count { get; }
    }

    /// <summary>A run read from its chunks' brow lines, with each chunk's facing asked for once and kept.</summary>
    public sealed class CliffRun : ICliffRun
    {
        readonly System.Collections.Generic.IReadOnlyList<Vector2[]> _brows;
        readonly System.Func<int, float> _facing;
        readonly int[] _before, _after;
        readonly float[] _facingKept;
        const int Unread = -2;

        public CliffRun(System.Collections.Generic.IReadOnlyList<Vector2[]> brows, System.Func<int, float> facing)
        {
            _brows = brows;
            _facing = facing;
            int n = brows == null ? 0 : brows.Count;
            _before = new int[n];
            _after = new int[n];
            _facingKept = new float[n];
            for (int i = 0; i < n; i++) { _before[i] = Unread; _after[i] = Unread; _facingKept[i] = float.NaN; }
        }

        public int Count => _before.Length;
        bool Usable(int i) => i >= 0 && i < Count && _brows[i] != null && _brows[i].Length >= 2;
        public int Before(int i)
        {
            if (!Usable(i)) return -1;
            if (_before[i] == Unread) _before[i] = CliffWallGeometry.JoinedBefore(_brows, i);
            return _before[i];
        }
        public int After(int i)
        {
            if (!Usable(i)) return -1;
            if (_after[i] == Unread) _after[i] = CliffWallGeometry.JoinedAfter(_brows, i);
            return _after[i];
        }
        public float Length(int i) => Usable(i) ? CliffWallGeometry.BrowLengthMetres(_brows[i]) : 0f;
        public Vector2 Heading(int i)
        {
            if (!Usable(i)) return Vector2.right;
            Vector2 d = _brows[i][_brows[i].Length - 1] - _brows[i][0];
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.right;
        }
        public float Facing(int i)
        {
            if (!Usable(i)) return 1f;
            if (float.IsNaN(_facingKept[i])) _facingKept[i] = _facing(i);
            return _facingKept[i];
        }
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
            // Under the lower bound the face is laid on the screen grid (FacesAcrossTheScreen): a push there moves
            // the outline only, never a texel, so the roughness comes back over the same width. The gain is a notch
            // with its floor on the bound, which is where the two mappings meet: the seam sits on a flat of wall.
            if (alignment < none) return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(none, none - (full - none), alignment));
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(none, full, alignment));
        }

        /// <summary>
        /// How much of an outward push is seen ACROSS a station's drawn column, 0 to 1: the sine of the angle whose
        /// cosine <see cref="FacingGain"/> measures. 0 on a wall at or over the region's upper facing bound (a push
        /// there slides along the column and a step in the brow has no riser to see), and 0 with no bounds set. Pure.
        /// </summary>
        public static float AcrossTheColumn(in CliffJag jag, Vector2 outwardPlan, Vector2 drawnDown)
        {
            float full = jag.Facing.y;
            if (full <= 0f) return 0f;
            float alignment = Mathf.Abs(Vector2.Dot(outwardPlan, drawnDown));
            return alignment >= full ? 0f : Mathf.Sqrt(Mathf.Max(0f, 1f - alignment * alignment));
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
            float under = jag.PlanarUnder, full = jag.Facing.y;
            if (full <= 0f || under <= 0f || samples == null || samples.Length == 0) return false;
            return MeanFacing(samples) < under;
        }

        /// <summary>A chunk's facing: the mean over its stations of the measure <see cref="FacingGain"/> reads, 1 on a
        /// south wall and near 0 on a steep east or west one. 1 for no stations. Pure.</summary>
        public static float MeanFacing(CliffWallSample[] samples)
        {
            if (samples == null || samples.Length == 0) return 1f;
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
                sum += Mathf.Abs(Vector2.Dot(
                    CliffWallGeometry.OutwardPlan(in samples[i]),
                    CliffWallGeometry.DrawnDown(samples[i].BrowPlan, CliffWallGeometry.ToeScreen(in samples[i]))));
            return sum / samples.Length;
        }

        /// <summary>
        /// A chunk's facing AVERAGED ALONG ITS RUN: the brow-length weighted mean of the chunks whose near end lies
        /// within the run window either side of it. A coast's facing wanders a few hundredths chunk to chunk; the
        /// average does not, so a wall takes one mapping along its length. Pure.
        /// </summary>
        public static float RunFacing(in CliffJag jag, ICliffRun run, int chunk)
        {
            float window = Mathf.Max(0f, jag.Run.y);
            float w = Mathf.Max(run.Length(chunk), 1e-4f);
            float sum = run.Facing(chunk) * w, weight = w;
            for (int side = 0; side < 2; side++)
            {
                float dist = 0f;
                int cur = chunk;
                for (int guard = 0; guard < run.Count && dist < window; guard++)
                {
                    cur = side == 0 ? run.Before(cur) : run.After(cur);
                    if (cur < 0 || cur == chunk) break;
                    float len = Mathf.Max(run.Length(cur), 1e-4f);
                    sum += run.Facing(cur) * len;
                    weight += len;
                    dist += len;
                }
            }
            return sum / weight;
        }

        /// <summary>
        /// ⭐ Whether a chunk's face is laid on the SCREEN GRID, decided along its run and not chunk by chunk (owner
        /// 11 26: one pixel size on screen, so no wall under the bound may keep sheared bedding, and a wall must not
        /// flicker between mappings). A chunk is planar where its <see cref="RunFacing"/> is under
        /// <see cref="CliffJag.PlanarUnder"/>. Where that answer changes between two joined chunks, the change is
        /// moved to the sharpest corner of the outline within the corner reach of it (a join whose two chunks turn
        /// by at least the corner angle), so a wall changes mapping where it turns and not mid-wall: the chunks
        /// between the change and the corner take the answer from the far side of the change. With no corner in
        /// reach it stays where it fell. A function of the run alone, so a chunk and its neighbours agree on each
        /// other's answer. Pure.
        /// </summary>
        public static bool PlanarInRun(in CliffJag jag, ICliffRun run, int chunk)
        {
            float under = jag.PlanarUnder;
            if (jag.Facing.y <= 0f || under <= 0f || run == null || chunk < 0 || chunk >= run.Count) return false;
            bool own = RunFacing(in jag, run, chunk) < under;
            float reach = Mathf.Max(0f, jag.Run.z);
            if (reach <= 0f) return own;

            // Look back, then on, for a change of answer whose corner lies past this chunk.
            for (int side = 0; side < 2; side++)
            {
                bool back = side == 0;
                float dist = 0f;
                int cur = chunk;
                for (int guard = 0; guard < run.Count; guard++)
                {
                    int far = back ? run.Before(cur) : run.After(cur);
                    if (far < 0 || far == chunk) break;
                    bool farAnswer = RunFacing(in jag, run, far) < under;
                    bool nearAnswer = cur == chunk ? own : RunFacing(in jag, run, cur) < under;
                    if (farAnswer != nearAnswer)
                    {
                        // The change stands at the join between far and cur. Its corner, if past this chunk on
                        // this chunk's side, pulls this chunk over to the far answer.
                        // Looking back, this chunk lies on from the change; looking on, back from it.
                        if (CornerIsPast(in jag, run, back ? far : cur, chunk, back)) return farAnswer;
                        break;      // the nearest change on this side decides; none further does
                    }
                    dist += run.Length(far);
                    if (dist > reach) break;
                    cur = far;
                }
            }
            return own;
        }

        /// <summary>The turn at the join after <paramref name="chunk"/>, 0 (straight on) to 2 (back on itself).</summary>
        static float TurnAfter(ICliffRun run, int chunk)
        {
            int next = run.After(chunk);
            return next < 0 ? 0f : 1f - Vector2.Dot(run.Heading(chunk), run.Heading(next));
        }

        /// <summary>
        /// Whether the corner of the change at the join after <paramref name="joinChunk"/> lies at or past the far
        /// end of <paramref name="past"/>, walking on (<paramref name="forward"/>) or back along the run from that
        /// join. The corner is the sharpest join within the corner reach either side of the change, the change's
        /// own join included and preferred on a tie; a join that turns less than the corner angle is no corner.
        /// </summary>
        static bool CornerIsPast(in CliffJag jag, ICliffRun run, int joinChunk, int past, bool forward)
        {
            float reach = jag.Run.z;
            float least = 1f - Mathf.Cos(Mathf.Clamp(jag.Run.w, 0f, 180f) * Mathf.Deg2Rad);
            float best = TurnAfter(run, joinChunk);
            bool bestPast = false;

            // On from the change: the join after joinChunk's successor, and so on.
            float dist = 0f;
            int cur = run.After(joinChunk);
            bool passed = false;
            for (int guard = 0; guard < run.Count && cur >= 0 && cur != joinChunk; guard++)
            {
                dist += run.Length(cur);
                if (dist > reach) break;
                if (cur == past) passed = true;
                float turn = TurnAfter(run, cur);
                if (run.After(cur) >= 0 && turn > best) { best = turn; bestPast = forward && passed; }
                cur = run.After(cur);
            }
            // Back from the change: the join before joinChunk, and so on.
            dist = 0f;
            cur = joinChunk;
            passed = false;
            for (int guard = 0; guard < run.Count && cur >= 0; guard++)
            {
                dist += run.Length(cur);
                if (dist > reach) break;
                if (cur == past) passed = true;
                int prev = run.Before(cur);
                if (prev < 0 || prev == joinChunk) break;
                float turn = TurnAfter(run, prev);
                if (turn > best) { best = turn; bestPast = !forward && passed; }
                cur = prev;
            }
            return bestPast && best >= least;
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

        internal static float Hash01(int i, int seed, int salt)
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
