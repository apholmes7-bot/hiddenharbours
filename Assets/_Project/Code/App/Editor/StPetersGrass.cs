#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The grass layer</b> — wind-reactive tufts over the meadow, the ground cover the splat shader's
    /// grass BAND only paints and this layer makes move. Pure and deterministic, reusing the woods'
    /// habitat fields (<see cref="StPetersWoods"/>) so grass, shrubs and trees agree about the ground;
    /// <see cref="StPetersWoodsPlanter"/> does the Unity work.
    ///
    /// <para><b>Two systems, one meadow.</b> The splat ground's grass band (elevation ≥
    /// <see cref="StPetersShoreMap.GrassFloorElevation"/>) is the painted LOOK of grassland; these tufts
    /// are the moving layer on top of it — they sway on the shared wind (<c>_WindWorld</c>, published by
    /// <c>GrassWindBridge</c>) and bend under the player (<c>GrassFootstep</c>, wired onto the player by
    /// <c>PersistentCoreBuilder</c>). The floor below is therefore the BAND's floor, not the tree or
    /// shrub line: tufts belong wherever the ground already reads as grass.</para>
    ///
    /// <para><b>Swathes, not a carpet.</b> A coarse coverage field gates the scatter so grass comes in
    /// sweeps with worn ground between — the same reverting-island logic as the stands, at a smaller
    /// grain. Under the woods it thins to shade-starved sparse; on the blasted coast it bleaches toward
    /// straw. Every hash is position/index-stable (rule 5): a rebuild reproduces the meadow exactly.</para>
    /// </summary>
    public static class StPetersGrass
    {
        /// <summary>Grid spacing (m) of candidate sites. Fine enough that a swathe reads continuous at
        /// gameplay zoom, coarse enough that the whole meadow stays around a thousand renderers
        /// (rule 7: all static, one shared material, so the SRP batcher eats them — but a rebuild and a
        /// scene load still walk every one) — each accepted site plants 1–3 tufts, see
        /// <see cref="TuftsAt"/>.</summary>
        public const float GrassStep = 4.0f;
        public const float GrassJitter = 1.5f;

        /// <summary>Feature size (m) of the swathe/worn-ground mosaic — smaller than the stands' 46 m:
        /// a field's texture, not a forest's.</summary>
        public const float SwatheScale = 24f;

        /// <summary>The coverage field (symmetric about 0) must clear this for ground to carry grass.
        /// −0.15 covers a little over half the meadow, which reads as grassland with worn patches
        /// rather than either a carpet or a mange.</summary>
        public const float SwatheThreshold = -0.15f;

        /// <summary>Per-cell chance on open meadow — most swathe cells carry grass.</summary>
        public const float ChanceOpen = 0.85f;

        /// <summary>Per-cell chance under a stand — shade-starved sparse, so the woods floor reads as
        /// duff with the odd tuft, not a lawn under trees.</summary>
        public const float ChanceWoods = 0.25f;

        /// <summary>Tuft scale range, hashed per site (mirrors the GrassClump prefab's 0.85–1.25).</summary>
        public const float ScaleMin = 0.85f;
        public const float ScaleMax = 1.25f;

        /// <summary>The straw the coast bleaches toward — multiplied over the sprite's own gradient
        /// (the grass shader multiplies vertex colour), so the drawn shading survives the tint.</summary>
        public static readonly Color StrawTint = new Color(0.86f, 0.78f, 0.52f, 1f);

        /// <summary>One planted tuft: where, which of the three tuft sprites, how big, and its tint.</summary>
        public struct GrassTuftSite
        {
            public Vector2 Position;
            /// <summary>0..2 — GrassTuft, GrassTuft_Short, GrassTuft_Tall.</summary>
            public int Variant;
            public float Scale;
            public Color Tint;
        }

        /// <summary>True where the swathe field says grass grows (before the per-cell chance).</summary>
        public static bool InSwathe(Vector2 worldPos) =>
            StPetersShoreMap.Wiggle(
                worldPos * (StPetersShoreMap.BandWiggleScale / SwatheScale), salt: 157) > SwatheThreshold;

        /// <summary>
        /// The per-tuft tint: green in shelter, bleaching toward straw with exposure, with a small
        /// hashed brightness jitter so a swathe is not one flat colour. Deterministic — the same
        /// position always tints the same (the paint tool's own rule, kept).
        /// </summary>
        public static Color TintAt(Vector2 worldPos, float hash01)
        {
            float straw = Mathf.Clamp01(StPetersWoods.ExposureAt(worldPos) * 0.8f);
            Color baseTint = Color.Lerp(Color.white, StrawTint, straw);
            float v = Mathf.Lerp(0.9f, 1.1f, hash01);
            return new Color(Mathf.Clamp01(baseTint.r * v),
                             Mathf.Clamp01(baseTint.g * v),
                             Mathf.Clamp01(baseTint.b * v), 1f);
        }

        /// <summary>How many tufts an accepted cell plants (1–3): denser where the swathe field is
        /// strongest, so a sweep of grass has a thick heart and thin margins.</summary>
        public static int TuftsAt(Vector2 worldPos, float hash01)
        {
            float field = StPetersShoreMap.Wiggle(
                worldPos * (StPetersShoreMap.BandWiggleScale / SwatheScale), salt: 157);
            float strength = Mathf.InverseLerp(SwatheThreshold, 1f, field);
            return 1 + Mathf.Min(2, (int)(strength * (1.5f + hash01)));
        }

        /// <summary>
        /// Every grass tuft on the island, deterministically. Shares the trees' clearings (nothing grows
        /// on the pier or across the crossing's approach) with the GRASS BAND's own floor — the ground
        /// the splat shader already paints green is exactly the ground that gets blades.
        /// </summary>
        public static List<GrassTuftSite> Scatter(ITidalTerrain terrain)
        {
            var sites = new List<GrassTuftSite>();
            if (terrain == null) return sites;

            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float maxX = StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            float maxY = StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY;

            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / GrassStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / GrassStep));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                float cx = minX + (ix + 0.5f) * GrassStep
                           + (StPetersShoreMap.Hash01(ix, iy, 163) * 2f - 1f) * GrassJitter;
                float cy = minY + (iy + 0.5f) * GrassStep
                           + (StPetersShoreMap.Hash01(ix, iy, 167) * 2f - 1f) * GrassJitter;
                var p = new Vector2(cx, cy);

                // The clearings bind (village, spawn, crossing approach, dock), but the floor is the
                // grass BAND's — passed explicitly, the shrub layer's lesson: defaulting to the tree
                // line would throw away the beach-top metre this layer exists to cover.
                if (!StPetersWoods.IsPlantable(terrain, p, StPetersShoreMap.GrassFloorElevation))
                    continue;

                if (!InSwathe(p)) continue;

                float e = terrain.ElevationAt(p);
                float chance = StPetersWoods.InStand(p, e) ? ChanceWoods : ChanceOpen;
                if (StPetersShoreMap.Hash01(ix, iy, 173) > chance) continue;

                int tufts = TuftsAt(p, StPetersShoreMap.Hash01(ix, iy, 179));
                for (int t = 0; t < tufts; t++)
                {
                    // Sub-tuft offsets hashed on (cell, tuft-index) via distinct salts, inside a ring
                    // small enough that the cluster reads as one clump of growth.
                    var q = p + new Vector2(
                        (StPetersShoreMap.Hash01(ix * 3 + t, iy, 181) * 2f - 1f) * 1.1f,
                        (StPetersShoreMap.Hash01(ix, iy * 3 + t, 191) * 2f - 1f) * 1.1f);

                    // The offset can spill a metre across a clearing edge or under the band floor —
                    // each tuft re-passes the gate itself, so the invariants hold per BLADE, not per cell.
                    if (!StPetersWoods.IsPlantable(terrain, q, StPetersShoreMap.GrassFloorElevation))
                        continue;

                    float h = StPetersShoreMap.Hash01(ix * 5 + t, iy * 7 + t, 193);
                    sites.Add(new GrassTuftSite
                    {
                        Position = q,
                        Variant = Mathf.Min(2, (int)(StPetersShoreMap.Hash01(ix, iy, 197 + t) * 3f)),
                        Scale = Mathf.Lerp(ScaleMin, ScaleMax, h),
                        Tint = TintAt(q, h),
                    });
                }
            }
            return sites;
        }
    }
}
#endif
