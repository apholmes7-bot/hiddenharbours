using System;
using HiddenHarbours.Core;
using static HiddenHarbours.World.TerrainPlanMath;
using Rules = HiddenHarbours.World.TerrainPlanRules;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The ground file's bays (terrain PR 5 B, amendment 2 §4.5): CD's paint rule (<c>tools/stpOnePaint.js</c>, its zones
    /// inside the bay) over the bay's surface weight (<c>tools/stpOneBeach.js</c>'s <c>surface</c>), on the imported map.
    /// Distances run in ground metres, a unit north being 1 / <see cref="IsoGround.GroundDepthScale"/> m.
    /// </summary>
    public sealed partial class TerrainPlanDerivation
    {
        /// <summary>A bay's line in ground metres: the nearest point's signed distance (+ on its left, walking the line) and
        /// how far past either end that point lies.</summary>
        sealed class BayLine
        {
            readonly double[] _x, _y;

            public BayLine(PlanPoint[] p)
            {
                if (p == null || p.Length < 2) throw new InvalidOperationException("[TerrainPlan] a bay's line needs two points.");
                _x = new double[p.Length]; _y = new double[p.Length];
                for (int k = 0; k < p.Length; k++) { _x[k] = p[k].X; _y[k] = p[k].Y / IsoGround.GroundDepthScale; }
            }

            public void Near(double x, double y, out double signed, out double past)
            {
                double py = y / IsoGround.GroundDepthScale, best = double.PositiveInfinity;
                signed = 0; past = 0;
                int last = _x.Length - 2;
                for (int k = 0; k <= last; k++)
                {
                    double ax = _x[k], ay = _y[k], vx = _x[k + 1] - ax, vy = _y[k + 1] - ay, l2 = vx * vx + vy * vy;
                    double t = ((x - ax) * vx + (py - ay) * vy) / l2, tc = Clip(t, 0, 1);
                    double d = Hypot(x - (ax + vx * tc), py - (ay + vy * tc));
                    if (!(d < best)) continue;
                    best = d;
                    signed = vx * (py - ay) - vy * (x - ax) > 0 ? d : -d;
                    past = k == 0 && t < 0 ? -t * Math.Sqrt(l2) : k == last && t > 1 ? (t - 1) * Math.Sqrt(l2) : 0;
                }
            }
        }

        /// <summary>
        /// Each cell's zone where a bay paints it, else <see cref="TerrainPlanZones.Unpainted"/>; null when the plan has no bay
        /// or there is no ground file (a bay is the file's). Inside a bay, where its surface weighs over its SurfaceWeight and
        /// the ground stands at or over its From: its back zone at its back, else its recipe's band at the height plus noise.
        /// </summary>
        byte[] BayZones()
        {
            if (_plan.Bays == null || _plan.Bays.Length == 0) return null;
            if (_P == null || _src.Import == null) { Log("part 1", "no ground file: its bays are not painted"); return null; }
            var zones = new byte[_n];
            for (int i = 0; i < _n; i++) zones[i] = TerrainPlanZones.Unpainted;
            var b = _src.Import.Base;
            double step = (Num(_plan.HeightRange.y) - Num(_plan.HeightRange.x)) / TerrainPlanMaps.CodeCount;
            for (int n = 0; n < _plan.Bays.Length; n++)
            {
                var bay = _plan.Bays[n];
                var shore = new BayLine(Num(bay.Shore));
                var rim = new BayLine(Num(bay.Rim));
                var rec = new RecipeTable(bay.Recipe);
                byte back = Zi(bay.BackZone);
                double bx0 = Num(bay.BoxMin.x), by0 = Num(bay.BoxMin.y), bx1 = Num(bay.BoxMax.x), by1 = Num(bay.BoxMax.y);
                double minWidth = Num(bay.MinWidth), endFade = Num(bay.EndFade), rimFade = Num(bay.RimFade);
                double reach = Num(bay.SeaReach.x), seaFade = Num(bay.SeaReach.y);
                double cut0 = Num(bay.EndCut.x), cutW = Num(bay.EndCut.y), fill = Num(bay.EndFill), weigh = Num(bay.SurfaceWeight);
                double backShort = Num(bay.BackShort), backAbove = Num(bay.BackAbove), noise = Num(bay.Noise.x), lam = Num(bay.Noise.y);
                double from = Num(bay.From);
                long seed = _seed + Rules.SeedBay + n;
                if (!Win(bx0, by0, bx1, by1, out int r0, out int r1, out int c0, out int c1)) continue;
                int painted = 0, backs = 0;
                for (int r = r0; r < r1; r++)
                for (int c = c0; c < c1; c++)
                {
                    int i = r * _w + c;
                    double X = _xs[c], Y = _ys[r], g = _P[i];
                    if (X < bx0 || X > bx1 || Y < by0 || Y > by1 || g < from) continue;
                    shore.Near(X, Y, out double d, out double sPast);
                    rim.Near(X, Y, out double rd, out double rPast);
                    double wEnd = 1 - Ss(Math.Min(sPast, rPast) / endFade);
                    if (wEnd <= 0) continue;
                    double width = Math.Max(minWidth, d - rd);   // rd is + on the rim's land side, so the shore to the rim is d - rd
                    if (d > width + rimFade || d < -reach) continue;
                    double w = 1;
                    if (d > width) w *= 1 - Ss((d - width) / rimFade);
                    if (d < 0) w *= 1 - Ss((-d - reach + seaFade) / seaFade);
                    if (wEnd < 1)
                    {
                        // past an end, CD's rule reads the bay's profile against the ground: seaward the profile is a smooth
                        // max with the ground, so it fills; landward it fills where the ask raised the base and cuts elsewhere.
                        // A fill holds over EndFill of the end's weight; a cut's must pass its return
                        if (d < 0 || g > b[i] + step) w *= Ss(wEnd / fill);
                        else w *= Ss((wEnd - (cut0 - cutW / 2)) / cutW);
                    }
                    if (!(w > weigh)) continue;
                    byte z;
                    if (d > width - backShort && g > backAbove) { z = back; backs++; }
                    else z = BandOf(rec, g + noise * 2 * (VNoise(X / lam, Y / lam, seed) - 0.5));
                    zones[i] = z;
                    painted++;
                }
                _r.Note(bay.Id + ".cells", painted);
                _r.Note(bay.Id + ".back_cells", backs);
                Log("part 1", bay.Id + ": " + painted + " cells by its bands on the import (" + backs + " at its back)");
            }
            return zones;
        }

        /// <summary>A recipe's band for a height, with no jitter of its own: the lowest band whose top it sits under.</summary>
        static byte BandOf(RecipeTable recipe, double e)
        {
            byte z = recipe.Zone[recipe.Zone.Length - 1];
            for (int k = recipe.Below.Length - 1; k >= 0; k--)
                if (e < recipe.Below[k]) z = recipe.Zone[k];
            return z;
        }
    }
}
