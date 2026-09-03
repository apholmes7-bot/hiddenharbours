using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The pure half of <see cref="SurfSprayEmitter"/> (ADR 0040 rev 3, the plunging ledge as an EVENT):
    /// where a lip throws, when, and how hard. Every number here is a read of <see cref="SurfState"/> —
    /// the same physics the water draws — so spray flies off the lip the fragment is drawing and
    /// nowhere else. Allocation-free, deterministic, no RNG (the emitter hashes its own salts).
    /// </summary>
    public static class SurfSprayMath
    {
        /// <summary>
        /// The emission weight (0..1) at a probe. Three gates, all of them the bore's own quantities:
        /// the bed must PLUNGE (Battjes' weight past its gate — a spilling beach throws no lip), the
        /// crest must be ARRIVING (the pulse past its gate: the lip throws AT arrival, and between bores
        /// there is nothing to throw) and the whitewater must be live. The first two ramp so the spray
        /// swells and fades with the event instead of switching; the last is a hard floor.
        /// </summary>
        public static float Emission01(float plungingWeight01, float bore01, float whitewater01,
                                       float plungingGate, float boreGate, float whitewaterGate)
        {
            if (whitewater01 < whitewaterGate) return 0f;
            return Ramp01(plungingWeight01, plungingGate) * Ramp01(bore01, boreGate);
        }

        /// <summary>0 at and below the gate, 1 at 1, linear between — the gate is the onset, not a cliff.</summary>
        public static float Ramp01(float value, float gate)
        {
            gate = Mathf.Clamp(gate, 0f, 0.999f);
            return Mathf.Clamp01((value - gate) / (1f - gate));
        }

        /// <summary>
        /// The launch: SHOREWARD (the lip lands ahead of its base) at a multiple of the bore's own speed
        /// sqrt(g·d) — so a lip on a deep ledge flings and one in the last few centimetres barely
        /// lifts — fanned by a per-wisp spread so the spray tears rather than firing in a line.
        /// </summary>
        public static Vector2 Launch(Vector2 shoreward, float depthMeters, float gravity,
                                     float speedPerBoreSpeed, float spreadDegrees, float hash01)
        {
            float bore = Mathf.Sqrt(Mathf.Max(gravity, 0f) * Mathf.Max(depthMeters, BreakerMath.MinDepthMeters));
            float degrees = (Mathf.Clamp01(hash01) * 2f - 1f) * Mathf.Max(0f, spreadDegrees);
            return Rotate(shoreward, degrees) * (bore * Mathf.Max(0f, speedPerBoreSpeed));
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>The centre of cell (ix, iy) of an n×n probe lattice over the camera's frame — the
        /// emitter samples the bore there, and only there (rule 7: a fixed budget of probes).</summary>
        public static Vector2 ProbePoint(Vector2 centre, Vector2 halfSize, int cells, int ix, int iy)
        {
            int n = Mathf.Max(1, cells);
            return centre + new Vector2(((ix + 0.5f) / n - 0.5f) * 2f * halfSize.x,
                                        ((iy + 0.5f) / n - 0.5f) * 2f * halfSize.y);
        }

        /// <summary>A wisp's spawn point: its cell's centre, jittered within the cell by two hashes so a
        /// cell's spray is a patch, not a point.</summary>
        public static Vector2 SpawnPoint(Vector2 cellCentre, Vector2 halfSize, int cells, float hx, float hy)
        {
            int n = Mathf.Max(1, cells);
            var cell = new Vector2(halfSize.x * 2f / n, halfSize.y * 2f / n);
            return cellCentre + new Vector2((hx - 0.5f) * cell.x, (hy - 0.5f) * cell.y);
        }
    }
}
