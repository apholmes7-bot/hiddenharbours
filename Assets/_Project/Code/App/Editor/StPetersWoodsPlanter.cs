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
        public const string GrassRootName = "IslandGrass";

        /// <summary>Where the tidal coast's planting hangs — its own root, separate from the meadow's,
        /// so the owner can hide one and look at the other.</summary>
        public const string ShorePlantRootName = "ShorePlants";

        /// <summary>Grass is the lowest decor layer — the same pre-sort default the decor prefabs use
        /// (<c>DecorPrefabBuilder.GrassSortingOrder</c>); <see cref="YSortSprite"/> owns the final order,
        /// and its floor of 2 is what keeps a tuft above the sea plane.</summary>
        public const int GrassSortingOrder = 2;

        /// <summary>⚠ The three literal tuft paths that used to live here are GONE. They were the same
        /// three <c>GrassPaintTool</c> carried, index-aligned to a variant int, and keeping two copies
        /// of one list in lockstep is exactly what <see cref="GrassLibraryCatalog"/> exists to stop.
        /// The shipped three are still in the library, at their own paths, and still get planted —
        /// they are simply no longer named here.</summary>
        public const string GrassMaterialPath = GrassLibraryCatalog.GrassMaterialPath;

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

        /// <summary>The shared lit-sprite materials (<c>HiddenHarbours/LitSprite</c>). Two of them, not
        /// one, so the owner can tune the shore apart from the uplands without editing code — they SHIP
        /// IDENTICAL, and diverging them is a slider, not a change here.</summary>
        public const string LitShorePlantMaterialPath = "Assets/_Project/Art/Materials/LitShorePlant.mat";

        /// <summary>See <see cref="LitShorePlantMaterialPath"/>.</summary>
        public const string LitShrubMaterialPath = "Assets/_Project/Art/Materials/LitShrub.mat";

        /// <summary>
        /// How tall a shoreline plant must STAND (metres, from its own Def) before it is given a
        /// projected shadow. Every shrub casts; on the shore it is a judgment call, and this is where
        /// the call is written down rather than left in a reviewer's head. See
        /// <see cref="SubtidalZone"/> for the other half of it.
        ///
        /// <para>0.6 m puts the line above the mats and below the stands: sea lettuce, glasswort and
        /// beach pea fall out; cordgrass, threesquare, marram, bayberry, sweet fern and cattail fall
        /// in.</para>
        ///
        /// <para><b>⚠ Measured against the height a plant is DRAWN at</b>
        /// (<see cref="ShorePlantDef.PlantedStandingHeightM"/>), not the full-growth height the rig
        /// baked. When the 2026-08-05 retune turned the oversized species down to typical individuals,
        /// saltmeadow hay (0.75 → 0.52 m) and black rush (0.66 → 0.45 m) crossed below this line and
        /// stopped casting — correctly: at half a metre they are the same tussocky mat as the beach pea
        /// this line was drawn to exclude, and a shadow sheared off one reads as a smudge.</para>
        /// </summary>
        public const float ShadowCasterMinHeightM = 0.6f;

        /// <summary>
        /// The one zone whose plants never cast, whatever they measure: the contract's <b>subtidal
        /// fringe</b> (zone base 0.15 m — under water at all five baked tide states).
        ///
        /// <para>⚠️ <b>Algae alone is not the test, and assuming it was is a bug this nearly shipped.</b>
        /// Eelgrass is a true vascular plant standing 1.44 m, so an algae check passes it straight
        /// through — and it lives permanently submerged, where a hard ground shadow is both physically
        /// wrong (the light is refracting through moving water) and drawn into the seabed band the plant
        /// sorts into. The zone is what actually answers "is this thing ever standing in air".</para>
        /// </summary>
        public const string SubtidalZone = "fringe";

        public sealed class Result
        {
            public int Trees, Flowers, Shrubs, GrassTufts, ShorePlants;
            public readonly Dictionary<string, int> PerSpecies = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PerFlower = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PerShrub = new Dictionary<string, int>();
            /// <summary>Grass tufts per habitat tag — the number worth reading when the island stops
            /// looking right, because it says WHICH ground got which art.</summary>
            public readonly Dictionary<string, int> PerHabitat = new Dictionary<string, int>();
            /// <summary>Shore plants per tidal zone.</summary>
            public readonly Dictionary<string, int> PerZone = new Dictionary<string, int>();

            public string TreeSummary() => Summarise(PerSpecies);
            public string FlowerSummary() => Summarise(PerFlower);
            public string ShrubSummary() => Summarise(PerShrub);
            public string HabitatSummary() => Summarise(PerHabitat);
            public string ZoneSummary() => Summarise(PerZone);

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
            PlantGrass(terrain, result);
            PlantShorePlants(terrain, result);

            Debug.Log($"[StPetersWoodsPlanter] Planted {result.Trees} trees ({result.TreeSummary()}), " +
                      $"{result.Shrubs} shrubs ({result.ShrubSummary()}), " +
                      $"{result.Flowers} wildflowers ({result.FlowerSummary()}), " +
                      $"{result.GrassTufts} grass tufts ({result.HabitatSummary()}) and " +
                      $"{result.ShorePlants} shore plants ({result.ZoneSummary()}) — stands, heath, " +
                      "meadow and sward by habitat and the tidal coast by zone. The TREE line keeps " +
                      "the village, the spawn, the crossing's approach and the dock clear; the GRASS " +
                      "keeps only the buildings, the wharf and the walked tread clear, because a " +
                      "meadow grows up to a doorstep (StPetersGrass.IsPlantableMeadow).");
            return result;
        }

        // =====================================================================================
        //  GRASS
        // =====================================================================================

        /// <summary>
        /// <b>Which baked blade a scattered site grows</b> — the one definition of it, so the build and
        /// anything that MEASURES the build (the renderer/batch budget in
        /// <c>StPetersGroundCoverBudgetTests</c>) cannot disagree about what the island is drawn with.
        ///
        /// <para>The split of concerns is the habitat tag's, one level down. The SCATTER decides that a
        /// metre of ground wants a broad clump — a density judgment it can make from the field alone.
        /// The LIBRARY decides which art IS broad, and it answers in the only currency that cannot go
        /// stale: the baked WIDTH. Anything at least two cells across covers twice the ground for one
        /// SpriteRenderer, which is what pays for a 1 m grid.</para>
        ///
        /// <para>⚠ A habitat with no wide bake gets its normal list back rather than nothing — sparser
        /// art beats a bald patch the owner has to diagnose. Today that is SWARD and HEADLAND: the wide
        /// clumps carry <c>meadow,verge</c>, the saltmeadow pair <c>dune</c>, and FringeB <c>fringe</c>.
        /// Tagging the ClumpWide pair <c>sward</c> in <c>grassSpeciesRig.js</c> — a manifest retag, no
        /// new pixels — would cut the sward's renderer count by about a quarter. Flagged for the
        /// art-director lane rather than fudged here.</para>
        /// </summary>
        public sealed class GrassArtChooser
        {
            readonly List<GrassLibraryCatalog.Entry> _imported;
            readonly Dictionary<string, List<GrassLibraryCatalog.Entry>> _byHabitat =
                new Dictionary<string, List<GrassLibraryCatalog.Entry>>();
            readonly Dictionary<string, List<GrassLibraryCatalog.Entry>> _broadByHabitat =
                new Dictionary<string, List<GrassLibraryCatalog.Entry>>();

            public GrassArtChooser(List<GrassLibraryCatalog.Entry> imported) =>
                _imported = imported ?? new List<GrassLibraryCatalog.Entry>();

            /// <summary>Everything baked for a habitat. <c>Library.Choose</c> falls back to the whole
            /// library rather than returning nothing, which is what keeps an untagged habitat planted
            /// instead of bald.</summary>
            public List<GrassLibraryCatalog.Entry> For(string habitat)
            {
                if (_byHabitat.TryGetValue(habitat, out var list)) return list;
                var narrowed = new GrassLibraryCatalog.Library();
                narrowed.Entries.AddRange(_imported);
                list = narrowed.Choose(new[] { habitat }, null);
                _byHabitat[habitat] = list;
                return list;
            }

            /// <summary>The broad art of a habitat — at least two cells wide — or its normal list when
            /// nothing baked for that ground is broad.</summary>
            public List<GrassLibraryCatalog.Entry> BroadFor(string habitat)
            {
                if (_broadByHabitat.TryGetValue(habitat, out var list)) return list;
                list = For(habitat).Where(e => e.Width >= GrassLibraryCatalog.Ppu * 2).ToList();
                if (list.Count == 0) list = For(habitat);
                _broadByHabitat[habitat] = list;
                return list;
            }

            /// <summary>The entry a site grows, or null when the library has nothing at all. The site's
            /// own stable roll picks between the matching variants, so the same metre of ground always
            /// grows the same blade across rebuilds (rule 5).</summary>
            public GrassLibraryCatalog.Entry Choose(StPetersGrass.GrassTuftSite site)
            {
                var choices = site.Broad ? BroadFor(site.Habitat) : For(site.Habitat);
                return choices.Count == 0 ? null : choices[site.Roll % choices.Count];
            }
        }

        /// <summary>
        /// The moving meadow: wind-reactive tufts over the ground the splat shader already paints as
        /// grass. Same object shape as the owner's Grass Paint Tool makes (SpriteRenderer + Grass.mat +
        /// YSortSprite) so painted and planted grass are the same thing — but every scale, variant and
        /// tint here is HASHED, never rolled: the builder's grass must reproduce exactly (rule 5),
        /// where the paint tool's jitter is the owner's live brush.
        ///
        /// <para><b>⭐ THE ART COMES FROM THE LIBRARY, BY HABITAT TAG.</b> This method used to carry
        /// three literal sprite paths index-aligned to a variant int, the same three
        /// <c>GrassPaintTool</c> carried — so a new tuft meant editing both in lockstep. It now reads
        /// <see cref="GrassLibraryCatalog"/> and asks for whatever is baked carrying the habitat the
        /// scatter decided (dune by the sand, fringe at the splat boundary, wind-cropped headland,
        /// lush sward inland). The scatter knows the GROUND and the library knows the ART, and neither
        /// holds the other's list.</para>
        /// </summary>
        static void PlantGrass(ITidalTerrain terrain, Result result)
        {
            var library = GrassLibraryCatalog.Load();
            var imported = GrassLibraryCatalog.Imported(library);
            if (imported.Count == 0)
            {
                Debug.LogWarning(
                    "[StPetersWoodsPlanter] the grass library resolved no imported sprites — the " +
                    "meadow ships unmoving (splat ground only). Bake it (Hidden Harbours ▸ Dev ▸ Bake " +
                    "Grass Library), and `git lfs pull` if the PNGs are still pointers.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            if (material == null)
                Debug.LogWarning($"[StPetersWoodsPlanter] {GrassMaterialPath} missing — the tufts will " +
                                 "stand still instead of swaying on the shared wind.");

            var chooser = new GrassArtChooser(imported);

            var root = new GameObject(GrassRootName);
            foreach (var site in StPetersGrass.Scatter(terrain))
            {
                var entry = chooser.Choose(site);
                if (entry == null) continue;
                var sprite = GrassLibraryCatalog.LoadSprite(entry);
                if (sprite == null) continue;

                var go = new GameObject(entry.Name);
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);
                go.transform.localScale = new Vector3(site.Scale, site.Scale, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (material != null) sr.sharedMaterial = material;
                sr.sortingOrder = GrassSortingOrder;
                // ⚠ flipX, never a negative localScale.x. A mirrored tuft is free variety — the pivot is
                // bottom-CENTRE so it mirrors in place, the wind is world-space, and the shader's bend
                // reads sprite uv.y, which a horizontal flip does not touch. A negative scale would do
                // the same picture and quietly invert the winding for anything that later reads this
                // transform.
                sr.flipX = site.Mirror;
                // Multiplied over the sprite's own gradient by the grass shader, so shading survives.
                sr.color = site.Tint;
                go.AddComponent<YSortSprite>();

                result.GrassTufts++;
                result.PerHabitat[site.Habitat] =
                    result.PerHabitat.TryGetValue(site.Habitat, out int n) ? n + 1 : 1;
            }
        }

        // =====================================================================================
        //  SHORE PLANTS
        // =====================================================================================

        /// <summary>
        /// The tidal coast's planting — rockweed on the band the tide bares, marsh above it, and beds
        /// on the shallow shelf below. <see cref="StPetersShorePlants"/> decides where and what;
        /// this hangs the objects and gives each one a <see cref="ShorePlantTideView"/> so it follows
        /// the water from then on.
        ///
        /// <para><b>⚠ NO <see cref="YSortSprite"/> on a shore plant, and that is deliberate.</b> A
        /// submerged plant draws BELOW the Sea plane (−5) and YSortSprite's band is 2…40 — the two
        /// disagree by construction, and the view stands YSortSprite down whenever it is under water.
        /// Since almost every plant this scatter places is submerged for most of the tide, the
        /// component would spend its life disabled. The view sets the order directly instead.</para>
        /// </summary>
        static void PlantShorePlants(ITidalTerrain terrain, Result result)
        {
            var defs = new Dictionary<string, ShorePlantDef>();
            ShorePlantDef Def(string key)
            {
                if (defs.TryGetValue(key, out var d)) return d;
                d = AssetDatabase.LoadAssetAtPath<ShorePlantDef>(
                    $"{ShorePlantDefBuilder.DefFolder}/{key}.asset");
                defs[key] = d;
                return d;
            }

            var sites = StPetersShorePlants.Scatter(terrain);
            if (sites.Count == 0) return;

            var root = new GameObject(ShorePlantRootName);
            var missing = new HashSet<string>();
            var plantMat = AssetDatabase.LoadAssetAtPath<Material>(LitShorePlantMaterialPath);

            foreach (var site in sites)
            {
                var def = Def(site.SpeciesKey);
                if (def == null || !def.IsComplete()) { missing.Add(site.SpeciesKey); continue; }

                var go = new GameObject(site.SpeciesKey);
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);

                // ⭐ THE SPECIES' OWN SCALE × this site's jitter, and in that order for a reason. The rig
                // bakes every species at FULL GROWTH, which is botanically honest and reads oversized
                // when a whole shore of it is planted at once (owner, first playtest: the sea plants
                // "stand out a little too much in their size"). PlantedScale is the authored answer —
                // per species, on the Def, where the owner can turn it, and where a hand-placed plant
                // picks it up too. It is folded into the TRANSFORM rather than kept on the side because
                // that is what ShorePlantTideView reads to decide submergence: one drawn size, one
                // height, no way for them to disagree.
                float drawn = site.Scale * Mathf.Max(0.01f, def.PlantedScale);
                go.transform.localScale = new Vector3(drawn, drawn, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = def.SpriteFor(0);          // the view replaces this on its first refresh
                sr.sortingOrder = def.SubmergedSortingOrder;
                if (plantMat != null) sr.sharedMaterial = plantMat;

                // The binder goes on BEFORE the view, because setting Def is what publishes the sheets
                // into it — a binder added afterwards would sit there empty until something else
                // refreshed the plant.
                if (def.HasLightChannels) go.AddComponent<SpriteLightBinder>();

                var view = go.AddComponent<ShorePlantTideView>();
                view.Def = def;                         // setting Def refreshes, so a built scene looks right

                // ⚠️ SHADOWS ONLY WHERE THE SILHOUETTE EARNS ONE. A projected shadow is a sheared copy
                // of the sprite laid on the ground away from the light, so it reads only where there is
                // a silhouette to shear AND dry ground to lay it on. Three conditions, each excluding a
                // real species: not algae (weed and kelp drape, they do not stand), not the subtidal
                // fringe (permanently under water — this is what catches EELGRASS, which is no alga and
                // stands 1.44 m), and tall enough to throw something. 8 of the 16 species cast.
                //
                // ⚠ The height it is DRAWN at, not the height it was baked at — a species turned down
                // to two thirds that no longer clears the floor must stop casting, or it throws a
                // shadow bigger than itself.
                if (!def.Algae && def.Zone != SubtidalZone &&
                    def.PlantedStandingHeightM >= ShadowCasterMinHeightM)
                    go.AddComponent<SpriteShadow>();

                result.ShorePlants++;
                result.PerZone[site.Zone] =
                    result.PerZone.TryGetValue(site.Zone, out int n) ? n + 1 : 1;
            }

            if (missing.Count > 0)
                Debug.LogWarning(
                    $"[StPetersWoodsPlanter] {missing.Count} shore plant species have no complete Def " +
                    $"and were skipped: {string.Join(", ", missing)}. Bake the sheets and build the " +
                    "Defs (Hidden Harbours ▸ Dev ▸ Bake Shore Plant Sheets, then Build Shore Plant Defs).");
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

            // The shared lit material and this kit's baked light channels. ⚠️ The shrub rig ALREADY bakes
            // both — `<stem>_light.png` and `<stem>_calendar.png` have been committed since the kit
            // landed; nothing here needs a re-bake, only a consumer. Loaded once per species, not per
            // shrub: the sheets are per SPECIES and a thicket is hundreds of instances.
            var shrubMat = AssetDatabase.LoadAssetAtPath<Material>(LitShrubMaterialPath);
            var lightCache = new Dictionary<string, (Texture2D light, Texture2D state)>();
            (Texture2D light, Texture2D state) LightFor(string species)
            {
                if (lightCache.TryGetValue(species, out var pair)) return pair;
                string stem = sheets[species];
                pair = (
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.Light)),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.State)));
                lightCache[species] = pair;
                return pair;
            }

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
                if (shrubMat != null) sr.sharedMaterial = shrubMat;
                go.AddComponent<YSortSprite>();

                // The shared lit path. ⚠️ RED, not blue: the shrub rig's no-rim flag is the calendar
                // sheet's R (255 on veil pixels); the shoreline plants use their tide sheet's B. Both
                // committed contracts say the same sentence about it — "This is the branch. Read it, do
                // not infer it." No normal sheet: this rig resolves its normals at bake, and every light
                // term already falls back to the baked mask.
                var (lightSheet, stateSheet) = LightFor(site.Species);
                if (lightSheet != null)
                    go.AddComponent<SpriteLightBinder>().SetSheets(
                        lightSheet, normalSheet: null, rimGateSheet: stateSheet,
                        rimGateChannel: SpriteLightBinder.RimGateChannel.Red);

                // A shrub is a metre-ish mass with a real silhouette standing on open ground — the exact
                // caster this component was written for. Contrast the GRASS below it, which gets none:
                // thousands of tufts each pushing a sheared quad is a rule-7 violation bought for a
                // shadow the size of the tuft. Trees are the same call the other way and will want one
                // when the owner rules on it; shrubs are where the read is highest per caster.
                go.AddComponent<SpriteShadow>();

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
