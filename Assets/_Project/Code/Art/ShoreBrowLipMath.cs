using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>The numbers a region's turf lip is laid with on a bank that has NO cliff wall
    /// (<see cref="CliffFaceMixDef"/>, the ShoreBrow fields).</summary>
    public readonly struct ShoreBrowLip
    {
        /// <summary>How far from the traced brow the turf's edge is looked for, metres.</summary>
        public readonly float ReachMetres;
        /// <summary>How far down the screen a brow must face to hang a lip: the least of minus its seaward y.</summary>
        public readonly float MinFacing;
        /// <summary>A run of lip shorter than this is dropped, metres.</summary>
        public readonly float MinRunMetres;
        /// <summary>The line is laid this far inside the turf, metres, so no grass shows above the sod.</summary>
        public readonly float InsetMetres;
        /// <summary>How far the jag bites the line further into the turf, metres.</summary>
        public readonly float JagMetres;
        /// <summary>A run's ends crumble away in whole columns over this many metres.</summary>
        public readonly float EndCrumbleMetres;
        /// <summary>Two neighbouring stations whose edges differ by more than this are not one run, metres.</summary>
        public readonly float JumpMetres;

        public ShoreBrowLip(float reachMetres, float minFacing, float minRunMetres, float insetMetres, float jagMetres,
                            float endCrumbleMetres, float jumpMetres)
        {
            ReachMetres = Mathf.Max(0f, reachMetres);
            MinFacing = Mathf.Clamp01(minFacing);
            MinRunMetres = Mathf.Max(0f, minRunMetres);
            InsetMetres = insetMetres;
            JagMetres = Mathf.Max(0f, jagMetres);
            EndCrumbleMetres = Mathf.Max(0f, endCrumbleMetres);
            JumpMetres = Mathf.Max(0f, jumpMetres);
        }

        public bool On => ReachMetres > 0f;

        public static ShoreBrowLip From(CliffFaceMixDef mix) => mix == null
            ? default
            : new ShoreBrowLip(mix.ShoreBrowReachMetres, mix.ShoreBrowMinFacing, mix.ShoreBrowMinRunMetres,
                               mix.ShoreBrowInsetMetres, mix.ShoreBrowJagMetres, mix.ShoreBrowEndCrumbleMetres,
                               mix.ShoreBrowJumpMetres);
    }

    /// <summary>
    /// The turf lip on a bank with no cliff wall (owner 11 26: the brow needs a real turf lip; no mixels).
    ///
    /// <para>A bank's brow is drawn by the ground alone, so there is no wall mesh to hang a lip on. The lip is laid
    /// instead on the SCREEN'S OWN GRID: one column of the strip per ground texel across the screen, each column
    /// moved up or down by whole texels to follow the turf's edge. A column is never sheared or scaled, so every
    /// texel of the strip is one ground texel, square, wherever the brow runs. That only works where the brow is
    /// crossed by a column once, so only a brow that faces down the screen hangs a lip; one that faces across it or
    /// away has none.</para>
    ///
    /// <para>The edge is the turf's own (the splat's cover crossing a half), looked for near the brow the shore
    /// lines traced, at stations a quarter metre apart. The line is then set a little inside the turf and bitten
    /// further in by the region's jag (<see cref="CliffJagMath.Line"/>), a function of world x, so it never repeats
    /// with the strip. Pure and deterministic: no randomness but hashes of position and the mix's seed.</para>
    /// </summary>
    public static class ShoreBrowLipMath
    {
        public const float TexelsPerMetre = CliffWaterlineMath.TexelsPerMetre;
        /// <summary>Ground texels between two stations the edge is looked for at (a quarter metre).</summary>
        public const int StationTexels = 8;
        const float StationMetres = StationTexels / TexelsPerMetre;
        const float HalfCover = 0.5f;
        const int EndSalt = 733;

        /// <summary>One column of lip: the ground texel column it stands in and the texel row of its line.</summary>
        public readonly struct Column
        {
            public readonly int X, Row;
            public Column(int x, int row) { X = x; Row = row; }
        }

        /// <summary>
        /// Where the turf ends below <paramref name="near"/> on the column at <paramref name="x"/>: the height, within
        /// <paramref name="reach"/> of it, where the cover falls through a half going DOWN the screen, nearest to it.
        /// False where the column has no such edge.
        /// </summary>
        public static bool EdgeBelow(Func<Vector2, float> turf, float x, float near, float reach, out float edge)
        {
            edge = near;
            if (turf == null || reach <= 0f) return false;
            int steps = Mathf.CeilToInt(2f * reach / StationMetres);
            float top = near + reach, best = float.MaxValue;
            bool found = false;
            float upper = turf(new Vector2(x, top));
            for (int i = 1; i <= steps; i++)
            {
                float y = top - i * StationMetres;
                float lower = turf(new Vector2(x, y));
                if (upper >= HalfCover && lower < HalfCover)
                {
                    float hi = y + StationMetres, lo = y;
                    for (int k = 0; k < 5; k++)
                    {
                        float mid = (hi + lo) * 0.5f;
                        if (turf(new Vector2(x, mid)) >= HalfCover) hi = mid; else lo = mid;
                    }
                    float at = (hi + lo) * 0.5f;
                    if (Mathf.Abs(at - near) < best) { best = Mathf.Abs(at - near); edge = at; found = true; }
                }
                upper = lower;
            }
            return found;
        }

        /// <summary>
        /// The runs of lip for a region: each a list of columns, left to right, one per ground texel.
        /// <paramref name="brows"/> are the traced brows that own no wall; only those facing down the screen count.
        /// </summary>
        public static List<Column[]> Runs(IReadOnlyList<ShoreLineDerivation.Segment> brows, Func<Vector2, float> turf,
                                          in ShoreBrowLip lip, in CliffJag jag)
        {
            var runs = new List<Column[]>();
            if (!lip.On || brows == null || turf == null) return runs;

            // The turf's edge at each station (a quarter metre of x) under a qualifying brow.
            var stations = new SortedDictionary<int, List<float>>();
            for (int s = 0; s < brows.Count; s++)
            {
                ShoreLineDerivation.Segment b = brows[s];
                if (-b.Seaward.y < lip.MinFacing) continue;
                float x0 = Mathf.Min(b.A.x, b.B.x), x1 = Mathf.Max(b.A.x, b.B.x);
                if (x1 - x0 < 1e-4f) continue;
                int k0 = Mathf.CeilToInt(x0 / StationMetres), k1 = Mathf.FloorToInt(x1 / StationMetres);
                for (int k = k0; k <= k1; k++)
                {
                    float x = k * StationMetres;
                    float near = Mathf.Lerp(b.A.y, b.B.y, Mathf.InverseLerp(b.A.x, b.B.x, x));
                    if (!EdgeBelow(turf, x, near, lip.ReachMetres, out float edge)) continue;
                    if (!stations.TryGetValue(k, out List<float> at)) stations[k] = at = new List<float>();
                    bool known = false;
                    for (int i = 0; i < at.Count && !known; i++) known = Mathf.Abs(at[i] - edge) <= lip.JumpMetres;
                    if (!known) at.Add(edge);
                }
            }

            // Chain neighbouring stations into runs: an edge joins the run that ended one station back at about
            // its own height, else it starts a run.
            var open = new List<(int last, List<float> edges, int first)>();
            var closed = new List<(int first, List<float> edges)>();
            foreach (KeyValuePair<int, List<float>> station in stations)
            {
                int k = station.Key;
                for (int i = open.Count - 1; i >= 0; i--)
                    if (open[i].last < k - 1) { closed.Add((open[i].first, open[i].edges)); open.RemoveAt(i); }
                foreach (float edge in station.Value)
                {
                    int join = -1; float best = lip.JumpMetres;
                    for (int i = 0; i < open.Count; i++)
                    {
                        if (open[i].last != k - 1) continue;
                        float d = Mathf.Abs(open[i].edges[open[i].edges.Count - 1] - edge);
                        if (d <= best) { best = d; join = i; }
                    }
                    if (join < 0) open.Add((k, new List<float> { edge }, k));
                    else { open[join].edges.Add(edge); open[join] = (k, open[join].edges, open[join].first); }
                }
            }
            foreach (var o in open) closed.Add((o.first, o.edges));

            foreach (var run in closed)
            {
                float length = (run.edges.Count - 1) * StationMetres;
                if (run.edges.Count < 2 || length < lip.MinRunMetres) continue;
                int firstX = run.first * StationTexels, count = (run.edges.Count - 1) * StationTexels;
                var columns = new List<Column>(count);
                for (int c = 0; c < count; c++)
                {
                    int x = firstX + c;
                    float metres = (x + 0.5f) / TexelsPerMetre;
                    // The run's ends crumble away column by column, so a lip never stops on a square end.
                    float fromEnd = Mathf.Min(c + 0.5f, count - c - 0.5f) / TexelsPerMetre;
                    if (lip.EndCrumbleMetres > 0f && fromEnd < lip.EndCrumbleMetres &&
                        CliffJagMath.Hash01(x, jag.Seed, EndSalt) >= fromEnd / lip.EndCrumbleMetres) continue;
                    int station = c / StationTexels;
                    float edge = Mathf.Lerp(run.edges[station], run.edges[station + 1],
                                            (c % StationTexels + 0.5f) / StationTexels);
                    float bite = lip.JagMetres > 0f && jag.On
                        ? lip.JagMetres * Mathf.Clamp01(0.5f + 0.5f * CliffJagMath.Line(in jag, metres, 0))
                        : 0f;
                    columns.Add(new Column(x, Mathf.RoundToInt((edge + lip.InsetMetres + bite) * TexelsPerMetre)));
                }
                if (columns.Count > 0) runs.Add(columns.ToArray());
            }
            return runs;
        }

        /// <summary>
        /// The lip's mesh: one quad for every stretch of neighbouring columns on one row, each exactly the strip's
        /// height tall and whole texels wide, its corners on the ground's texel grid. u counts ground texels across
        /// the screen over the strip's width (the strip repeats), v runs the strip top to bottom, so a strip texel is
        /// a ground texel. <paramref name="lineRow"/> is the strip row, from its top, that lies on the line.
        /// </summary>
        public static void Mesh(IReadOnlyList<Column[]> runs, Vector2Int stripTexels, int lineRow, Vector2 origin,
                                List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            if (runs == null || stripTexels.x <= 0 || stripTexels.y <= 0) return;
            const float texel = 1f / TexelsPerMetre;
            foreach (Column[] run in runs)
            {
                int i = 0;
                while (i < run.Length)
                {
                    int j = i;
                    while (j + 1 < run.Length && run[j + 1].Row == run[i].Row && run[j + 1].X == run[j].X + 1) j++;
                    float x0 = run[i].X * texel - origin.x, x1 = (run[j].X + 1) * texel - origin.x;
                    float top = (run[i].Row + lineRow) * texel - origin.y, bottom = top - stripTexels.y * texel;
                    // u is kept small (the strip repeats) so a far column loses no texel to float precision.
                    int period = Mathf.FloorToInt(run[i].X / (float)stripTexels.x);
                    float u0 = (run[i].X - period * stripTexels.x) / (float)stripTexels.x;
                    float u1 = (run[j].X + 1 - period * stripTexels.x) / (float)stripTexels.x;
                    int v = verts.Count;
                    verts.Add(new Vector3(x0, top, 0f)); uvs.Add(new Vector2(u0, 1f));
                    verts.Add(new Vector3(x1, top, 0f)); uvs.Add(new Vector2(u1, 1f));
                    verts.Add(new Vector3(x1, bottom, 0f)); uvs.Add(new Vector2(u1, 0f));
                    verts.Add(new Vector3(x0, bottom, 0f)); uvs.Add(new Vector2(u0, 0f));
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                    tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
                    i = j + 1;
                }
            }
        }
    }
}
