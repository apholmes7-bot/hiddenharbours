#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The <b>region-agnostic habitat rule</b> the Grass Paint Tool's mask fill uses: given the ONE
    /// height map and a grass floor, is this metre grass — and if so, what KIND of grass ground is it?
    ///
    /// <para><b>⚠ WHY THIS IS NOT <c>StPetersGrass.HabitatAt</c>.</b> That one is the real thing: it
    /// reads St Peters' painted splat materials, so the builder's grass agrees with the exact ground
    /// the owner sees. But it lives in <c>HiddenHarbours.App.Editor</c>, and art-pipeline's tooling
    /// does not reference App — an editor brush that only worked on one island would be a worse tool,
    /// and reaching across the lane to get it would be the coupling CLAUDE.md rule 4 exists to stop.
    /// So this is the same SHAPE of rule expressed in the only terms Core offers: elevation, and how
    /// the elevation behaves a few metres away.</para>
    ///
    /// <para><b>What it can and cannot tell apart.</b> It knows an EDGE (grass giving out nearby), a
    /// SHELF (ground that drops away toward water — the beach side of an edge), and open ground. It
    /// does not know sand from cobble, because Core does not publish materials. So a fill lays fringe
    /// where the builder would lay fringe, and dune where the builder would lay dune along a falling
    /// coast, and it will occasionally call a cliff top a dune. For a hand tool over a rectangle the
    /// owner is looking at, that is the right trade; the builder's pass stays the authority.</para>
    /// </summary>
    public static class GrassMaskProbe
    {
        public const string HabitatSward = "sward";
        public const string HabitatMeadow = "meadow";
        public const string HabitatFringe = "fringe";
        public const string HabitatDune = "dune";

        /// <summary>How far out (m) the edge probes reach. One brush spacing plus a little, for the same
        /// reason the builder's does: the fringe band has to be at least as wide as the gap between two
        /// sites or the boundary shows through it.</summary>
        public const float EdgeProbeMetres = 2.6f;

        /// <summary>How much lower (m) the ground a probe away has to be for the edge to read as a
        /// falling coast rather than a worn patch. Half a metre over 2.6 m is a 1:5 slope, which is the
        /// beach falloff St Peters is built on.</summary>
        public const float ShelfDropMetres = 0.5f;

        static readonly Vector2[] Probes = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

        /// <summary>
        /// The habitat at <paramref name="worldPos"/>, or <c>null</c> when this metre is not grass at
        /// all (below <paramref name="grassFloor"/>) and nothing should plant.
        /// </summary>
        /// <param name="grassFloor">Ground at or above this elevation (m above chart datum) counts as
        /// grass. St Peters' own value is 4.2 — above the highest water of the biggest spring tide.</param>
        public static string HabitatAt(ITidalTerrain terrain, Vector2 worldPos, float grassFloor,
                                       float edgeProbe = EdgeProbeMetres)
        {
            if (terrain == null) return null;

            float here = terrain.ElevationAt(worldPos);
            if (here < grassFloor) return null;

            bool edge = false, falling = false;
            for (int i = 0; i < Probes.Length; i++)
            {
                float there = terrain.ElevationAt(worldPos + Probes[i] * edgeProbe);
                if (there >= grassFloor) continue;
                edge = true;
                if (here - there >= ShelfDropMetres) falling = true;
            }

            if (edge) return falling ? HabitatDune : HabitatFringe;

            // Open ground. Split sward from meadow on a stable spatial hash rather than a per-site roll,
            // so the two interleave in PATCHES — a per-site coin flip would alternate cell by cell and
            // read as noise instead of as two kinds of grass.
            return PatchField(worldPos) > 0f ? HabitatMeadow : HabitatSward;
        }

        /// <summary>A coarse smooth field in [-1,1] at ~24 m per cell — the same grain the builder's
        /// swathe field uses, so a filled rectangle and a built island have the same texture.</summary>
        public static float PatchField(Vector2 worldPos, int salt = 157, float scale = 24f)
        {
            float fx = worldPos.x / scale, fy = worldPos.y / scale;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            float bottom = Mathf.Lerp(Hash01(x0, y0, salt), Hash01(x0 + 1, y0, salt), tx);
            float top = Mathf.Lerp(Hash01(x0, y0 + 1, salt), Hash01(x0 + 1, y0 + 1, salt), tx);
            return Mathf.Lerp(bottom, top, ty) * 2f - 1f;
        }

        /// <summary>The project's stable lattice hash (FNV-1a + a finalizer). Deliberately identical to
        /// <c>StPetersShoreMap.Hash01</c> — same numbers, no <c>System.Random</c>, so the same metre of
        /// ground answers the same on every machine and every rebuild (rule 5).</summary>
        public static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
#endif
