#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The shrub layer</b> — heath on the barrens, gale in the bogs, meadowsweet in the swales, hazel
    /// under the woods, and rose and raspberry along every edge. Pure and deterministic, reusing the same
    /// habitat fields the woods are planted from (<see cref="StPetersWoods"/>) so the two layers agree
    /// about what kind of ground they are standing on.
    ///
    /// <para><b>The habitats are the RIG'S OWN five</b> — <c>barren · woods · edge · bog · swale</c> — and
    /// every species is placed by the habitat <i>the contract itself gives it</i>, never by a table written
    /// here. So this file decides which habitat a patch of island IS; the contract decides what grows in
    /// one. A re-bake that moves a species between habitats moves it on the island too, with no code
    /// change.</para>
    ///
    /// <para><b>Plain sprites, by instruction.</b> No <c>SpriteLightResponse</c>, nothing branching on the
    /// state channel's veil flag, no <c>_WindWorld</c> bridge: the veil/light contract is the art-pipeline
    /// lane's future work and wiring it from here would be building someone else's seam. Snow is out of
    /// scope entirely — M1 is not winter.</para>
    /// </summary>
    public static class StPetersShrubs
    {
        // Shrubs take ground the trees will not: they run right down to the top of the beach, where the
        // woods stop well short. That is the point of a shrub layer on an exposed coast — the heath IS what
        // grows where the forest cannot.
        /// <summary>Shrubs start where the meadow does, a metre below the tree line.</summary>
        public const float ShrubLineElevation = 4.3f;

        /// <summary>Grid spacing (m) of candidate positions. Coarser than the trees: a shrub layer reads
        /// from its clusters, and every one is a GameObject (rule 7).</summary>
        public const float ShrubStep = 8.5f;
        public const float ShrubJitter = 3.4f;

        /// <summary>Fraction of candidate cells that carry a shrub, hashed per cell — so the layer is
        /// patchy rather than a second canopy under the first.</summary>
        public const float ShrubChance = 0.5f;

        /// <summary>Above this wetness the ground is BOG: standing water, peat, sweet gale.</summary>
        public const float BogThreshold = 0.72f;

        /// <summary>Between this and <see cref="BogThreshold"/> the ground is a SWALE — damp, but not
        /// peat.</summary>
        public const float SwaleThreshold = 0.58f;

        /// <summary>Above this exposure the open ground is BARREN: wind-scoured heath.</summary>
        public const float BarrenThreshold = 0.5f;

        /// <summary>How far (m) outside a stand still counts as its EDGE. A thicket of rose and raspberry
        /// grows in the light at a wood's margin, not in its shade and not out in the open field.</summary>
        public const float EdgeReachMetres = 9f;

        /// <summary>
        /// Which of the rig's five habitats a patch of island is. Order matters: wet beats everything (a bog
        /// is a bog whatever the wind does), then the wood and its margin, then exposure decides whether
        /// open ground is barren heath or ordinary edge.
        /// </summary>
        public static string HabitatAt(ITidalTerrain terrain, Vector2 p)
        {
            float wetness = StPetersWoods.WetnessAt(p);
            if (wetness >= BogThreshold) return "bog";
            if (wetness >= SwaleThreshold) return "swale";

            float e = terrain.ElevationAt(p);
            if (StPetersWoods.InStand(p, e)) return "woods";

            // Just outside a stand is its EDGE — sampled on the four compass points, which is enough to
            // catch a margin at this scale and keeps the field cheap over tens of thousands of candidates.
            for (int i = 0; i < 4; i++)
            {
                var probe = p + new Vector2(i == 0 ? EdgeReachMetres : i == 1 ? -EdgeReachMetres : 0f,
                                            i == 2 ? EdgeReachMetres : i == 3 ? -EdgeReachMetres : 0f);
                if (StPetersWoods.InStand(probe, terrain.ElevationAt(probe))) return "edge";
            }

            return StPetersWoods.ExposureAt(p) >= BarrenThreshold ? "barren" : "edge";
        }

        /// <summary>One planted shrub: where, which species, and which drawn individual of it.</summary>
        public struct ShrubSite
        {
            public Vector2 Position;
            public string Species;
            /// <summary>Column of the variant-axis sheet — a different individual of the same species,
            /// which is what stops a thicket reading as one bush repeated.</summary>
            public int Variant;
        }

        /// <summary>
        /// Every shrub on the island. Pure — grid, jitter, habitat and variant are all stable hashes of
        /// position, so a rebuild reproduces the layer exactly (rule 5).
        /// </summary>
        /// <param name="habitatOf">A species' habitat, straight from the kit's contract.</param>
        /// <param name="available">The species actually baked. St Peters ships a six-species slice of a
        /// twenty-species kit, so most of the kit is legitimately absent and the scatter must cope.</param>
        public static List<ShrubSite> Scatter(ITidalTerrain terrain,
                                              IReadOnlyList<string> available,
                                              System.Func<string, string> habitatOf,
                                              int variants)
        {
            var sites = new List<ShrubSite>();
            if (terrain == null || available == null || available.Count == 0) return sites;

            // Group what was baked by the habitat the CONTRACT gives it.
            var byHabitat = new Dictionary<string, List<string>>();
            foreach (string s in available)
            {
                string h = habitatOf != null ? habitatOf(s) : null;
                if (string.IsNullOrEmpty(h)) continue;
                if (!byHabitat.TryGetValue(h, out var list)) byHabitat[h] = list = new List<string>();
                list.Add(s);
            }
            if (byHabitat.Count == 0) return sites;

            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float maxX = StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            float maxY = StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY;

            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / ShrubStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / ShrubStep));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                if (StPetersShoreMap.Hash01(ix, iy, 131) > ShrubChance) continue;

                float cx = minX + (ix + 0.5f) * ShrubStep
                           + (StPetersShoreMap.Hash01(ix, iy, 137) * 2f - 1f) * ShrubJitter;
                float cy = minY + (iy + 0.5f) * ShrubStep
                           + (StPetersShoreMap.Hash01(ix, iy, 139) * 2f - 1f) * ShrubJitter;
                var p = new Vector2(cx, cy);

                // The trees' clearings bind here too — a raspberry thicket across the pier or the crossing
                // would be exactly as wrong as a spruce. But the elevation floor is the SHRUB line, passed
                // in: IsPlantable defaults to the TREE line, and letting it do so here silently threw away
                // every shrub in the metre of beach-top this layer exists to occupy.
                if (!StPetersWoods.IsPlantable(terrain, p, ShrubLineElevation)) continue;

                string habitat = HabitatAt(terrain, p);
                if (!byHabitat.TryGetValue(habitat, out var candidates) || candidates.Count == 0) continue;

                // Where a habitat baked more than one species (edge has rose AND raspberry), a hash picks
                // between them so a margin is mixed rather than one long hedge of the same bush.
                int pick = Mathf.Min(candidates.Count - 1,
                                     (int)(StPetersShoreMap.Hash01(ix, iy, 149) * candidates.Count));

                sites.Add(new ShrubSite
                {
                    Position = p,
                    Species = candidates[pick],
                    Variant = Mathf.Min(Mathf.Max(1, variants) - 1,
                                        (int)(StPetersShoreMap.Hash01(ix, iy, 151) * Mathf.Max(1, variants))),
                });
            }
            return sites;
        }
    }
}
#endif
