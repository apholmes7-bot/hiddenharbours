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
    /// <para><b>TWO PASSES, the same way the woods have two.</b> <see cref="Scatter"/> is the AMBIENT heath
    /// over the whole island — the layer that takes the ground the forest gives up. <see cref="ScatterUnderstorey"/>
    /// is the layer INSIDE the authored woodland lots, at a grain derived from the canopy standing over it.
    /// The pairing is deliberate and it mirrors <see cref="StPetersWoods.ScatterTrees"/> /
    /// <see cref="StPetersWoods.ScatterLotTrees"/> exactly: an island-wide density and a lot's own density
    /// are two different questions with two different owners' dials.</para>
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
        ///
        /// <para><b>⭐ THE CANOPY QUESTION IS <see cref="StPetersWoods.InWoods"/>, NOT
        /// <c>InStand</c> — and that one word is the meadow floor this pass was written to fix.</b>
        /// <c>InStand</c> is the AMBIENT mosaic alone; since the owner's 2026-08-16 density ruling the
        /// shadiest ground on the island is the AUTHORED lots <see cref="StPetersWoodlandZones"/> declares,
        /// and asking the mosaic alone left every one of them growing open-meadow rose and raspberry under a
        /// closed canopy. <c>InWoods</c> is <i>"is there a canopy over this metre"</i>, which is the question
        /// an understorey actually has — the same reading, and the same sentence, the lady's slipper already
        /// gets in <see cref="StPetersWoods.ScatterFlowers"/>.</para>
        ///
        /// <para><b>⚠ A CUT LANE IS NOT EXCEPTED HERE, AND DOES NOT NEED TO BE.</b> A lane's tread is inside
        /// its lot's capsule, so this would happily call it woods — but nothing is ever planted there to ask:
        /// <see cref="StPetersWoods.IsPlantable"/> routes through
        /// <see cref="StPetersWoodlandZones.IsCleared"/>, and every shrub pass on this island goes through
        /// it. Keeping the exception out of the habitat field keeps ONE gate answering "may anything stand
        /// here", which is what stops a corridor being cut by one pass and grown over by another.</para>
        /// </summary>
        public static string HabitatAt(ITidalTerrain terrain, Vector2 p)
        {
            float wetness = StPetersWoods.WetnessAt(p);
            if (wetness >= BogThreshold) return "bog";
            if (wetness >= SwaleThreshold) return "swale";

            float e = terrain.ElevationAt(p);
            if (StPetersWoods.InWoods(p, e)) return WoodsHabitat;

            // Just outside a wood is its EDGE — sampled on the four compass points, which is enough to
            // catch a margin at this scale and keeps the field cheap over tens of thousands of candidates.
            // ⚠ The probe asks the same question the test above does: a woodland lot's margin is a wood
            // margin, and rose and raspberry belong on it exactly as they belong on the mosaic's.
            for (int i = 0; i < 4; i++)
            {
                var probe = p + new Vector2(i == 0 ? EdgeReachMetres : i == 1 ? -EdgeReachMetres : 0f,
                                            i == 2 ? EdgeReachMetres : i == 3 ? -EdgeReachMetres : 0f);
                if (StPetersWoods.InWoods(probe, terrain.ElevationAt(probe))) return "edge";
            }

            return StPetersWoods.ExposureAt(p) >= BarrenThreshold ? "barren" : "edge";
        }

        /// <summary>
        /// The kit's own key for the UNDERSTOREY habitat — the layer that belongs inside a stand.
        ///
        /// <para>Named once rather than spelled at each site that asks for it, because it is the one habitat
        /// two regions and four tests all have to agree about; it is quoted from the shrub contract's
        /// <c>ShrubCatalog.HabitatKeys</c>, and a test pins that it is still one of them.</para>
        /// </summary>
        public const string WoodsHabitat = "woods";

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

            var byHabitat = GroupByHabitat(available, habitatOf);
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

        /// <summary>
        /// <b>The island's UNDERSTOREY</b> — the shrub layer inside the woodland lots
        /// <see cref="StPetersWoodlandZones"/> declares, at its own grain.
        ///
        /// <para><b>⭐ WHY A SECOND PASS RATHER THAN A DENSER <see cref="Scatter"/>.</b> Exactly the split
        /// the trees already make (<see cref="StPetersWoods.ScatterTrees"/> for the reverting mosaic,
        /// <see cref="StPetersWoods.ScatterLotTrees"/> for the closed stands): the ambient heath is a
        /// property of the whole island and its density is the owner's dial for <i>how scrubby is the
        /// island</i>; the understorey is a property of a LOT and its density is a ratio of the canopy over
        /// it (<see cref="WoodlandZones.UnderstoreyTrunksPerShrub"/>). One grid cannot carry both without
        /// one of the two questions becoming un-tunable.</para>
        ///
        /// <para><b>It INFILLS, like the lot trees do</b> — pass the trunks AND the ambient shrubs already
        /// standing, and nothing new lands within <see cref="WoodlandZones.MinUnderstoreyGapMetres"/> of
        /// them. The ambient heath walks a lot's ground too (it does not know lots exist), so without that
        /// the two layers would grow through each other.</para>
        /// </summary>
        /// <param name="alreadyStanding">Every trunk and shrub already placed on the island.</param>
        public static List<ShrubSite> ScatterUnderstorey(ITidalTerrain terrain,
                                                        IReadOnlyList<string> available,
                                                        System.Func<string, string> habitatOf,
                                                        int variants,
                                                        IEnumerable<Vector2> alreadyStanding)
        {
            var sites = new List<ShrubSite>();
            if (terrain == null || available == null || available.Count == 0) return sites;

            var byHabitat = GroupByHabitat(available, habitatOf);
            if (byHabitat.Count == 0) return sites;

            foreach (var site in WoodlandZones.ScatterUnderstorey(
                         StPetersWoodlandZones.Zones, terrain,
                         // ⚠ The SHRUB line, not the tree line — the same argument IsPlantable's own remarks
                         // make about the ambient pass. It cannot bite inside a lot (all three sit on the
                         // sheltered interior plateau) but passing the tree line here would leave a caller
                         // reading this line believing the understorey is held to the canopy's floor.
                         p => StPetersWoods.IsPlantable(terrain, p, ShrubLineElevation),
                         variants, alreadyStanding))
            {
                // The region decides what kind of ground this is; the contract decides what grows in one.
                // Inside a lot that is normally `woods` — but a wet hollow under a canopy is still a bog,
                // and sweet gale is what actually grows in it, so the habitat field is asked rather than
                // assumed.
                string habitat = HabitatAt(terrain, site.Position);
                if (!byHabitat.TryGetValue(habitat, out var candidates) || candidates.Count == 0) continue;

                sites.Add(new ShrubSite
                {
                    Position = site.Position,
                    Species = candidates[site.Roll % candidates.Count],
                    Variant = site.Variant,
                });
            }
            return sites;
        }

        /// <summary>What was baked, grouped by the habitat the CONTRACT gives it — never by a table written
        /// here. Shared by both passes so a re-bake that moves a species between habitats moves it in the
        /// heath and in the understorey together.</summary>
        static Dictionary<string, List<string>> GroupByHabitat(IReadOnlyList<string> available,
                                                               System.Func<string, string> habitatOf)
        {
            var byHabitat = new Dictionary<string, List<string>>();
            foreach (string s in available)
            {
                string h = habitatOf != null ? habitatOf(s) : null;
                if (string.IsNullOrEmpty(h)) continue;
                if (!byHabitat.TryGetValue(h, out var list)) byHabitat[h] = list = new List<string>();
                list.Add(s);
            }
            return byHabitat;
        }
    }
}
#endif
