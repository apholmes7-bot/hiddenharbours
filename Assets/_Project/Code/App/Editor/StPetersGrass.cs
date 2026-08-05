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
    /// <see cref="StPetersShoreMap.GrassFloorElevation"/>) is the painted LOOK of grassland and STAYS
    /// exactly what it is: the static base layer. These tufts are the moving layer on top of it — they
    /// sway on the shared wind (<c>_WindWorld</c>, published by <c>GrassWindBridge</c>) and bend under
    /// the player (<c>GrassFootstep</c>). The floor below is therefore the BAND's floor, not the tree
    /// or shrub line: tufts belong wherever the ground already reads as grass.</para>
    ///
    /// <para><b>⭐ THE GREEN-OVER (2026-08-05): FIELDS, NOT SWATHES.</b> This layer used to lay ~590
    /// tufts in sweeps with worn ground between, which read as an island with some grass on it. The
    /// owner asked for one that reads GREEN — grass over most of the grassy island. So the grain got
    /// finer (<see cref="GrassStep"/>), the coverage gate got more generous
    /// (<see cref="SwatheThreshold"/>), and the worn ground that survives is now there because the
    /// field says so rather than because the scatter was sparse.</para>
    ///
    /// <para><b>⭐ AND SPECIES BY HABITAT, WHICH IS WHY THE DENSITY IS AFFORDABLE AT ALL.</b> A field
    /// this dense drawn from three tuft silhouettes reads as wallpaper the moment the player walks
    /// along it. Every site now resolves a HABITAT — dune by the sand, fringe at the splat boundary,
    /// straw-tinted headland where the wind scours, lush sward inland — and the planter draws from the
    /// grass library's variants carrying that tag (<c>GrassLibraryCatalog</c>). The habitat is decided
    /// HERE, from the same painted ground everything else on this island reads, and the art is chosen
    /// THERE, from whatever has been baked. Neither knows the other's list.</para>
    ///
    /// <para>Every hash is position/index-stable (rule 5): a rebuild reproduces the meadow exactly, and
    /// a re-run with the same inputs converges rather than piling on.</para>
    /// </summary>
    public static class StPetersGrass
    {
        /// <summary>Grid spacing (m) of candidate sites. <b>2.2 m, down from 4.0.</b> The step sets the
        /// grain of the field: at 4 m the eye reads individual clumps with ground between them, which is
        /// the "some grass on it" look the green-over replaced. Each accepted site plants 1–3 tufts (see
        /// <see cref="TuftsAt"/>), so the tuft count goes roughly as the inverse SQUARE of this — it is
        /// the one number to turn if the island needs to get cheaper, and
        /// <c>StPetersGrassDensityTests</c> pins what it currently costs.</summary>
        public const float GrassStep = 2.2f;

        public const float GrassJitter = 0.9f;

        /// <summary>Feature size (m) of the swathe/worn-ground mosaic — smaller than the stands' 46 m:
        /// a field's texture, not a forest's.</summary>
        public const float SwatheScale = 24f;

        /// <summary>The coverage field (symmetric about 0) must clear this for ground to carry grass.
        /// <b>−0.62, down from −0.15.</b> −0.15 covered a little over half the meadow; this covers the
        /// large majority and leaves worn ground only where the field genuinely dips — paths, hollows,
        /// the odd bald patch. Raising it back toward 0 is how the island gets patchy again.</summary>
        public const float SwatheThreshold = -0.62f;

        /// <summary>Per-cell chance on open meadow — most swathe cells carry grass.</summary>
        public const float ChanceOpen = 0.92f;

        /// <summary>Per-cell chance under a stand — shade-starved sparse, so the woods floor reads as
        /// duff with the odd tuft, not a lawn under trees.</summary>
        public const float ChanceWoods = 0.3f;

        /// <summary>Tuft scale range, hashed per site (mirrors the GrassClump prefab's 0.85–1.25).</summary>
        public const float ScaleMin = 0.85f;
        public const float ScaleMax = 1.25f;

        /// <summary>The straw the coast bleaches toward — multiplied over the sprite's own gradient
        /// (the grass shader multiplies vertex colour), so the drawn shading survives the tint.</summary>
        public static readonly Color StrawTint = new Color(0.86f, 0.78f, 0.52f, 1f);

        // =====================================================================================
        // habitat
        // =====================================================================================

        /// <summary>The habitat tags this island resolves, matching the grass library's own vocabulary.
        /// Nothing here is a sprite name — the planter matches on the tag and takes whatever has been
        /// baked with it.</summary>
        public const string HabitatSward = "sward";
        public const string HabitatMeadow = "meadow";
        public const string HabitatFringe = "fringe";
        public const string HabitatDune = "dune";
        public const string HabitatHeadland = "headland";

        /// <summary>How far (m) from the grass band's edge still counts as FRINGE. One grid step plus a
        /// little: the fringe variants are the low wide ones whose whole job is to hide the splat
        /// boundary where the painted grass hands over to bare ground, so the band has to be at least
        /// as wide as the gap between two sites or the boundary shows through it.</summary>
        public const float FringeBandMetres = 2.6f;

        /// <summary>How far (m) from sand or the marram band still counts as DUNE. Wider than the fringe
        /// band because marram grows back off the beach, not just at its lip.</summary>
        public const float DuneBandMetres = 5f;

        /// <summary>Exposure above which open ground reads as scoured HEADLAND. 0.68 puts the rim just
        /// outside the woods' own WET/EXPOSED marks (0.62 / 0.55) — a coastal band about 14 m deep on
        /// this island, which is a headland. It was 0.5 in the first cut of the green-over and that
        /// classified 38% of the island as headland: on a 240 × 140 m ellipse an exposure ring is a
        /// large fraction of the whole, so this threshold is far more sensitive than it looks.</summary>
        public const float HeadlandExposure = 0.68f;

        /// <summary>
        /// Which habitat a site belongs to.
        ///
        /// <para><b>⚠ THE BOUNDARY TEST COMES FIRST, AND WHAT IT MEETS DECIDES WHICH EDGE IT IS.</b>
        /// The first cut of this asked "is there sand nearby?" before "am I on an edge?", and dune
        /// swallowed the fringe entirely — measured: <b>zero</b> fringe sites on the whole island,
        /// because the grass band's seaward edge always has the marram band within reach, so every
        /// boundary site answered "dune" before the fringe test ever ran. Both edges exist and they
        /// look different: grass giving out onto SAND wears marram, grass giving out onto cobble or a
        /// worn clearing wears low spreading fringe blades. So: find the edge, then ask what is on the
        /// other side of it.</para>
        ///
        /// <para><b>Read off the same painted ground as everything else.</b>
        /// <see cref="StPetersShoreMap.MaterialAt"/> is the splat the owner sees, so the grass agrees
        /// with the floor it stands on by construction — there is no second map of where the beach is.</para>
        /// </summary>
        public static string HabitatAt(ITidalTerrain terrain, Vector2 worldPos)
        {
            if (terrain == null) return HabitatMeadow;

            // On an EDGE? Then which edge — the beach, or bare ground.
            if (!AllGrassWithin(terrain, worldPos, FringeBandMetres))
                return NearMaterial(terrain, worldPos, DuneBandMetres,
                                    ShoreMaterial.Sand,
                                    ShoreMaterial.Marram)
                    ? HabitatDune
                    : HabitatFringe;

            // HEADLAND — scoured open ground. The dry look is the straw tint (see TintAt); this picks
            // the wind-cropped silhouette to carry it.
            if (StPetersWoods.ExposureAt(worldPos) >= HeadlandExposure) return HabitatHeadland;

            // Inland: the short sward carpets, the taller meadow stands in it. Split on the swathe
            // field's own strength so the two interleave in patches instead of alternating per cell.
            return SwatheField(worldPos) > 0.15f ? HabitatMeadow : HabitatSward;
        }

        /// <summary>True when every probe within <paramref name="radius"/> is still on the grass band —
        /// i.e. this site is INSIDE the field rather than on its edge. Four axis probes plus the centre:
        /// enough to catch a boundary at this grain, and cheap enough to run per site (rule 7).</summary>
        public static bool AllGrassWithin(ITidalTerrain terrain, Vector2 p, float radius)
        {
            if (StPetersShoreMap.MaterialAt(terrain, p) != ShoreMaterial.Grass)
                return false;
            for (int i = 0; i < 4; i++)
            {
                Vector2 q = p + Probe(i) * radius;
                if (StPetersShoreMap.MaterialAt(terrain, q) != ShoreMaterial.Grass)
                    return false;
            }
            return true;
        }

        /// <summary>True when any probe within <paramref name="radius"/> lands on one of
        /// <paramref name="wanted"/>.</summary>
        public static bool NearMaterial(ITidalTerrain terrain, Vector2 p, float radius,
                                        params ShoreMaterial[] wanted)
        {
            for (int i = 0; i < 4; i++)
            {
                var m = StPetersShoreMap.MaterialAt(terrain, p + Probe(i) * radius);
                for (int w = 0; w < wanted.Length; w++) if (m == wanted[w]) return true;
            }
            return false;
        }

        static Vector2 Probe(int i) =>
            i == 0 ? Vector2.right : i == 1 ? Vector2.left : i == 2 ? Vector2.up : Vector2.down;

        // =====================================================================================
        // the field
        // =====================================================================================

        /// <summary>The coverage field, symmetric about 0 — where grass sweeps and where it wears
        /// through. One evaluation, used by both the gate and the habitat split, so they cannot
        /// disagree about where a sweep is strongest.</summary>
        public static float SwatheField(Vector2 worldPos) =>
            StPetersShoreMap.Wiggle(
                worldPos * (StPetersShoreMap.BandWiggleScale / SwatheScale), salt: 157);

        /// <summary>True where the swathe field says grass grows (before the per-cell chance).</summary>
        public static bool InSwathe(Vector2 worldPos) => SwatheField(worldPos) > SwatheThreshold;

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
            float strength = Mathf.InverseLerp(SwatheThreshold, 1f, SwatheField(worldPos));
            return 1 + Mathf.Min(2, (int)(strength * (1.5f + hash01)));
        }

        // =====================================================================================
        // the scatter
        // =====================================================================================

        /// <summary>One planted tuft: where, what KIND of ground it is on, how big, and its tint.</summary>
        public struct GrassTuftSite
        {
            public Vector2 Position;

            /// <summary>A habitat TAG (<see cref="HabitatAt"/>), not a sprite index. The planter picks
            /// art carrying this tag from the grass library, so adding a variant is a bake and never a
            /// change here.</summary>
            public string Habitat;

            /// <summary>0..2 — a stable per-site roll the planter uses to pick between the variants
            /// that match the habitat, so the same site always draws the same blade.</summary>
            public int Roll;

            public float Scale;
            public Color Tint;
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
                        (StPetersShoreMap.Hash01(ix * 3 + t, iy, 181) * 2f - 1f) * 0.7f,
                        (StPetersShoreMap.Hash01(ix, iy * 3 + t, 191) * 2f - 1f) * 0.7f);

                    // The offset can spill across a clearing edge or under the band floor — each tuft
                    // re-passes the gate itself, so the invariants hold per BLADE, not per cell.
                    if (!StPetersWoods.IsPlantable(terrain, q, StPetersShoreMap.GrassFloorElevation))
                        continue;

                    float h = StPetersShoreMap.Hash01(ix * 5 + t, iy * 7 + t, 193);
                    sites.Add(new GrassTuftSite
                    {
                        Position = q,
                        Habitat = HabitatAt(terrain, q),
                        Roll = (int)(StPetersShoreMap.Hash01(ix, iy, 197 + t) * 1024f),
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
