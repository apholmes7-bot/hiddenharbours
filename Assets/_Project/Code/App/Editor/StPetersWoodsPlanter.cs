#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;                 // YSortSprite (flowers)
using HiddenHarbours.Art.Editor;          // AcadianTreeCatalog, TreeKitCatalog, FlowerCatalog

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Plants what <see cref="StPetersWoods"/> decided: the island's stands of Acadian trees and the
    /// wildflowers on the meadow between them.
    ///
    /// <para><b>Trees go through <c>AcadianTreeCatalog.Configure</c>, deliberately.</b> That method is the
    /// ONE place an Acadian tree is built — the prefab builder and the owner's Tree Paint Tool both come
    /// through it — so a tree the builder plants and a tree the owner paints are the same object: same wind
    /// material, same per-species trunk anchor, same Y-sorting, same reflection component. Reimplementing
    /// any of that here would create a second kind of tree that drifts from the first.</para>
    ///
    /// <para><b>⚠ The species list comes from the kit, never from a literal here.</b> Tamarack is held back
    /// by ruling and is absent from <c>Trees.json</c>; because the planter only ever plants what
    /// <c>Scan()</c> returns, it cannot be reached even by accident, and a later bake that adds it appears
    /// without a code change.</para>
    /// </summary>
    public static class StPetersWoodsPlanter
    {
        public const string TreeRootName = "IslandWoods";
        public const string FlowerRootName = "IslandFlowers";
        public const string ShrubRootName = "IslandShrubs";

        /// <summary>Shrubs sit between the ground cover and the trees. Like both, this is only the
        /// pre-sort default — <see cref="YSortSprite"/> owns the order at runtime.</summary>
        public const int ShrubSortingOrder = 4;

        /// <summary>
        /// Which sway row a planted shrub is drawn on. <b>Row 0 is the shrub standing still</b> — the rig's
        /// sway curve is <c>sin(frame/SWAY × 2π)</c>, so rows 1 and 3 are the two extremes of a lean and
        /// row 2 is the neutral pose again. Nothing here animates (plain sprites, no <c>_WindWorld</c>
        /// bridge by instruction), so a static shrub has to be one standing still rather than one frozen
        /// mid-gust. The other three rows are baked and unused for now — they are what a later wind pass
        /// would read, which is why the slice still bakes the whole sheet the contract describes.
        /// </summary>
        public const int RestSwayRow = 0;

        /// <summary>Flowers sit with the grass, just above it — the same order the decor prefabs use. They
        /// also carry a <see cref="YSortSprite"/>, which OWNS the order at runtime and recomputes it from
        /// world Y, so this is only the pre-sort default.</summary>
        public const int FlowerSortingOrder = 3;

        public sealed class Result
        {
            public int Trees, Flowers, Shrubs;
            public readonly Dictionary<string, int> PerSpecies = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PerFlower = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PerShrub = new Dictionary<string, int>();

            public string TreeSummary() => Summarise(PerSpecies);
            public string FlowerSummary() => Summarise(PerFlower);
            public string ShrubSummary() => Summarise(PerShrub);

            static string Summarise(Dictionary<string, int> d)
            {
                var parts = d.Select(kv => $"{kv.Key} {kv.Value}").ToList();
                parts.Sort();
                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// Plant the island. Safe before the kits are imported: it warns and plants nothing rather than
        /// half a forest.
        /// </summary>
        public static Result Plant(ITidalTerrain terrain)
        {
            var result = new Result();
            if (terrain == null) return result;

            PlantTrees(terrain, result);
            PlantShrubs(terrain, result);
            PlantFlowers(terrain, result);

            Debug.Log($"[StPetersWoodsPlanter] Planted {result.Trees} trees ({result.TreeSummary()}), " +
                      $"{result.Shrubs} shrubs ({result.ShrubSummary()}) and " +
                      $"{result.Flowers} wildflowers ({result.FlowerSummary()}) — stands, heath and meadow " +
                      "by habitat, with the village, the spawn, the crossing's approach and the dock left clear.");
            return result;
        }

        // =====================================================================================
        //  TREES
        // =====================================================================================

        static void PlantTrees(ITidalTerrain terrain, Result result)
        {
            // Every placeable tree the committed contract claims — species x stage x season. The kit bakes
            // mature/summer only today, so this is one entry per species.
            var placements = AcadianTreeCatalog.Scan();
            if (placements.Count == 0)
            {
                Debug.LogWarning("[StPetersWoodsPlanter] the Acadian tree contract is missing or claims no " +
                                 "trees — the island was left unwooded. Import the pass-2 tree kit.");
                return;
            }

            // Index by species so the habitat model can ask for one by name. If a later bake ships more
            // than one season, the first is taken — a summer island is what M1 is.
            var bySpecies = new Dictionary<string, AcadianTreeCatalog.Placement>();
            foreach (var p in placements)
                if (!bySpecies.ContainsKey(p.Species)) bySpecies[p.Species] = p;

            var material = AcadianTreeCatalog.LoadMaterial();
            if (material == null)
                Debug.LogWarning($"[StPetersWoodsPlanter] {AcadianTreeCatalog.MaterialPath} missing — the " +
                                 "woods will stand still instead of swaying on the shared wind.");

            var root = new GameObject(TreeRootName);
            var sites = StPetersWoods.ScatterTrees(
                terrain, bySpecies.Keys,
                species => bySpecies.TryGetValue(species, out var pl)
                           ? AcadianTreeCatalog.VariantCount(pl.Entry) : 1);

            // One sprite lookup per (species, variant) rather than per tree: a stand of 700 trees resolves
            // a couple of dozen distinct sprites, and an AssetDatabase call each would dominate the build.
            var spriteCache = new Dictionary<(string, int), Sprite>();

            foreach (var site in sites)
            {
                if (!bySpecies.TryGetValue(site.Species, out var placement)) continue;

                var key = (site.Species, site.Variant);
                if (!spriteCache.TryGetValue(key, out Sprite sprite))
                {
                    sprite = AcadianTreeCatalog.LoadVariant(placement, site.Variant);
                    spriteCache[key] = sprite;
                }
                if (sprite == null) continue;

                var go = new GameObject($"{site.Species}_{site.Variant}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                // The rig's pivot is the TRUNK FOOT (ADR 0026), so the position IS where the tree is
                // planted — no offset, and no rescaling (the bake is already at honest metric size).
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);
                AcadianTreeCatalog.Configure(go, placement, sprite, material);

                result.Trees++;
                result.PerSpecies.TryGetValue(site.Species, out int n);
                result.PerSpecies[site.Species] = n + 1;
            }
        }

        // =====================================================================================
        //  SHRUBS
        // =====================================================================================

        /// <summary>
        /// The shrub layer, as PLAIN SPRITES by instruction: no sprite-light response, nothing branching on
        /// the state channel's veil flag, no shared-wind bridge. That contract is the art-pipeline lane's
        /// future work and wiring it from here would be building someone else's seam. Snow is out of scope
        /// — M1 is not winter.
        ///
        /// <para>Only the slice <see cref="StPetersShrubBake"/> baked exists, so every lookup is
        /// null-tolerant: a species that was not baked simply has no sheet and its habitat goes unplanted
        /// rather than throwing halfway through a region build.</para>
        /// </summary>
        static void PlantShrubs(ITidalTerrain terrain, Result result)
        {
            var contract = ShrubCatalog.Load();
            if (contract?.Species == null || contract.Species.Count == 0)
            {
                Debug.LogWarning("[StPetersWoodsPlanter] the shrub contract is unreadable — no shrub layer. " +
                                 "The kit ships to order; run 'Bake St Peters Shrub Slice' first.");
                return;
            }

            // Habitat comes from the CONTRACT, never from a table here.
            var habitatOf = new Dictionary<string, string>();
            foreach (var e in contract.Species) habitatOf[e.Key] = e.Habitat;

            // Which of the baked species actually has pixels on disk. The kit ships nothing by default, so
            // "declared in the contract" and "baked" are different questions, and this asks the second.
            var sheets = new Dictionary<string, string>();      // species -> sheet stem
            foreach (string species in StPetersShrubBake.Species)
            {
                string stem = ShrubCatalog.VariantSheetStem(species, StPetersShrubBake.Stage,
                                                            StPetersShrubBake.Phase);
                string path = ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.Albedo);
                if (AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Any()) sheets[species] = stem;
            }

            if (sheets.Count == 0)
            {
                Debug.LogWarning("[StPetersWoodsPlanter] none of St Peters' shrub sheets are on disk — no " +
                                 "shrub layer. Run 'Hidden Harbours ▸ Art ▸ Bake St Peters Shrub Slice'.");
                return;
            }

            var root = new GameObject(ShrubRootName);
            var spriteCache = new Dictionary<(string, int), Sprite>();

            foreach (var site in StPetersShrubs.Scatter(
                         terrain, sheets.Keys.ToList(),
                         s => habitatOf.TryGetValue(s, out string h) ? h : null,
                         ShrubCatalog.Variants))
            {
                var key = (site.Species, site.Variant);
                if (!spriteCache.TryGetValue(key, out Sprite sprite))
                {
                    // Slices are named <stem>_c<col>_f<row>. On the VARIANT sheet the column is the
                    // individual — which is what stops a thicket reading as one bush repeated — and the row
                    // is the sway frame, of which a static planting takes the one at rest.
                    string want = $"{sheets[site.Species]}_c{site.Variant}_f{RestSwayRow}";
                    sprite = AssetDatabase
                        .LoadAllAssetsAtPath(ShrubCatalog.SheetPath(sheets[site.Species],
                                                                    ShrubCatalog.Channel.Albedo))
                        .OfType<Sprite>().FirstOrDefault(sp => sp.name == want);
                    spriteCache[key] = sprite;
                }
                if (sprite == null) continue;

                var go = new GameObject($"{site.Species}_{site.Variant}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                // The kit's pivot is the ROOT CROWN — the ground-contact point — so the position IS
                // where the shrub is planted. No offset, no rescale: the bake is already at metric size.
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = ShrubSortingOrder;
                go.AddComponent<YSortSprite>();

                result.Shrubs++;
                result.PerShrub.TryGetValue(site.Species, out int n);
                result.PerShrub[site.Species] = n + 1;
            }
        }

        // =====================================================================================
        //  FLOWERS
        // =====================================================================================

        static void PlantFlowers(ITidalTerrain terrain, Result result)
        {
            var species = FlowerCatalog.Scan();
            if (species.Count == 0)
            {
                Debug.LogWarning("[StPetersWoodsPlanter] no flower sheets found — the meadow was left bare.");
                return;
            }

            // Only species that ship at least one of the two tiers we plant are candidates.
            var byKey = new Dictionary<string, FlowerCatalog.FlowerSpecies>();
            foreach (var s in species)
                if (s.Has(FlowerCatalog.Tier.Single) || s.Has(FlowerCatalog.Tier.Clump))
                    byKey[s.Key] = s;

            if (byKey.Count == 0)
            {
                Debug.LogWarning("[StPetersWoodsPlanter] no flower species ship a Single or Clump sheet — " +
                                 "the meadow was left bare (the Patch tier is deliberately unused).");
                return;
            }

            var root = new GameObject(FlowerRootName);
            var spriteCache = new Dictionary<(string, FlowerCatalog.Tier), Sprite>();
            var materialCache = new Dictionary<FlowerCatalog.Tier, Material>();

            foreach (var site in StPetersWoods.ScatterFlowers(terrain, byKey.Keys))
            {
                if (!byKey.TryGetValue(site.Species, out var flower)) continue;

                // Clump where the scatter asked for one and the species ships it, else a single stem — and
                // fall the other way if THAT is the one it ships. The PATCH tier is never reached: the
                // owner still owes a verdict on it (#215), and planting hundreds of patches would be
                // committing his taste for him.
                var tier = site.Clump ? FlowerCatalog.Tier.Clump : FlowerCatalog.Tier.Single;
                if (!flower.Has(tier))
                    tier = tier == FlowerCatalog.Tier.Clump
                        ? FlowerCatalog.Tier.Single : FlowerCatalog.Tier.Clump;
                if (!flower.Has(tier)) continue;

                var key = (site.Species, tier);
                if (!spriteCache.TryGetValue(key, out Sprite sprite))
                {
                    // The sheet STEM comes from the species, never rebuilt by concatenation: the shared
                    // LupinPatch is the standing proof that stem != key + tier.
                    sprite = FlowerCatalog.LoadNeutral(flower.SheetFor(tier), tier, 0);
                    spriteCache[key] = sprite;
                }
                if (sprite == null) continue;

                if (!materialCache.TryGetValue(tier, out Material material))
                {
                    material = FlowerCatalog.MaterialFor(tier);
                    materialCache[tier] = material;
                }

                var go = new GameObject($"{site.Species}_{tier}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (material != null) sr.sharedMaterial = material;
                sr.sortingOrder = FlowerSortingOrder;
                // Y-sorted like the grass and the trees, so a bloom in front of the player draws in front.
                go.AddComponent<YSortSprite>();

                result.Flowers++;
                result.PerFlower.TryGetValue(site.Species, out int n);
                result.PerFlower[site.Species] = n + 1;
            }
        }
    }
}
#endif
