#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;                 // GrassField, YSortSprite, SpriteLightBinder, SpriteShadow,
                                          // ShorePlantDef, ShorePlantTideView
using HiddenHarbours.Art.Editor;          // AcadianTreeCatalog, ShrubCatalog, GrassLibraryCatalog
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Dresses the hinterland of Nine Mile Creek.</b> <see cref="NineMileCreekFields"/> decides every
    /// position, species and habitat; this class only does the Unity work — the same split
    /// <see cref="StPetersWoods"/> / <see cref="StPetersWoodsPlanter"/> uses on the island, and the
    /// reason the fields are testable without a scene.
    ///
    /// <para><b>⭐ FIELDS, NOT FOREST.</b> Four layers and no fifth: the meadow on the strips, hedgerows
    /// on the field boundaries and the two gravel roads, scattered trees standing mostly IN those hedges,
    /// and salt marsh round the barachois and the marsh pool. There is no stand field, no woods
    /// understorey and no flower carpet — the mainland doc's first photograph is of a farmed coast, and
    /// the layers this pass leaves out are what keeps it one.</para>
    ///
    /// <para><b>Null-tolerant throughout</b>, the <see cref="NineMileCreekDressing"/> arrangement: a kit
    /// that has not baked warns once and is skipped, and the rest of the planting still lands. "Declared
    /// in the contract" and "has pixels on disk" are different questions, and a partial art state should
    /// still dress what it can.</para>
    ///
    /// <para><b>⚠ THE ART IS BORROWED, NOT BAKED HERE.</b> The tree kit, the shrub slice, the grass
    /// library and the shore-plant Defs are all art-pipeline's, already committed for St Peters, and this
    /// coast is the same coast — the same Acadian species in the same salt wind. Where a kit has not
    /// baked what this region wants, the gap is reported in the console and in the PR body rather than
    /// baked from here.</para>
    /// </summary>
    public static class NineMileCreekFieldPlanter
    {
        /// <summary>The roots everything hangs under — one object per layer, so the owner can hide the
        /// hedges without hiding the meadow.</summary>
        public const string GrassRootName = "CreekFields";
        /// <inheritdoc cref="GrassRootName"/>
        public const string HedgeRootName = "CreekHedgerows";
        /// <inheritdoc cref="GrassRootName"/>
        public const string TreeRootName = "CreekTrees";
        /// <inheritdoc cref="GrassRootName"/>
        public const string MarshRootName = "CreekMarsh";

        /// <summary>Sorting order of a hedge shrub. The island's, so a shrub is a shrub in both
        /// regions.</summary>
        public const int ShrubSortingOrder = StPetersWoodsPlanter.ShrubSortingOrder;

        /// <summary>The sway frame a static planting takes — the one at rest.</summary>
        public const int RestSwayRow = StPetersWoodsPlanter.RestSwayRow;

        /// <summary>What one planting pass produced, for the console and for a test that wants to hold a
        /// layer to a budget.</summary>
        public sealed class Result
        {
            public int GrassTufts, Shrubs, Trees, MarshPlants;
            public int GrassPayloadCharacters;
            public readonly Dictionary<string, int> PerGrassHabitat = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PerShrubHabitat = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PerTreeSpecies = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PerMarshZone = new Dictionary<string, int>();

            public int HedgeTrees, FieldTrees;

            public string GrassSummary() => Summarise(PerGrassHabitat);
            public string ShrubSummary() => Summarise(PerShrubHabitat);
            public string TreeSummary() => Summarise(PerTreeSpecies);
            public string MarshSummary() => Summarise(PerMarshZone);

            static string Summarise(Dictionary<string, int> d)
            {
                var parts = d.Select(kv => $"{kv.Key} {kv.Value}").ToList();
                parts.Sort();
                return string.Join(", ", parts);
            }
        }

        /// <summary>Plant the hinterland. Safe before the kits are imported.</summary>
        public static Result Plant(ITidalTerrain terrain)
        {
            var result = new Result();
            if (terrain == null)
            {
                Debug.LogWarning("[NineMileCreekFieldPlanter] no terrain — the fields were left bare.");
                return result;
            }

            PlantGrass(terrain, result);
            PlantHedges(terrain, result);
            PlantTrees(terrain, result);
            PlantMarsh(terrain, result);

            Debug.Log(
                $"[NineMileCreekFieldPlanter] Dressed the fields of Nine Mile Creek: " +
                $"{result.GrassTufts:N0} grass tufts ({result.GrassSummary()}) baked as a FIELD into " +
                $"{result.GrassPayloadCharacters:N0} characters of scene, {result.Shrubs:N0} hedgerow " +
                $"shrubs ({result.ShrubSummary()}), {result.Trees:N0} trees " +
                $"({result.HedgeTrees} standing in hedges, {result.FieldTrees} alone in fields — " +
                $"{result.TreeSummary()}), and {result.MarshPlants:N0} marsh plants round the two ponds " +
                $"({result.MarshSummary()}).\n" +
                "⭐ FIELDS, NOT FOREST — there is no stand field in this region and there must not be " +
                "one: the mainland doc's first photograph is of a farmed coast. The strips come off a " +
                $"{NineMileCreekFields.FieldStripMetres:0} m lattice, the hedges walk those boundaries " +
                "and the two GRAVEL roads' own published corridors, the trees stand mostly in the hedges, " +
                "and nothing at all stands on the wharf's made ground.");

            return result;
        }

        // =============================================================================================
        //  THE MEADOW
        // =============================================================================================

        /// <summary>
        /// The fields' grass, <b>as a FIELD rather than a crowd of GameObjects</b> — the same
        /// <see cref="GrassField"/> path #485 built for the island, for the same reason it was built:
        /// a GameObject per tuft is about 3.1 KB of scene YAML, and this region's plantable land is
        /// roughly seven times the island's.
        ///
        /// <para><b>⭐ THE BATCHING WORK IS ON MAIN, so this pass can lean on it.</b> #485 ("grass becomes
        /// a field, not a crowd") landed the byte-plane payload AND the chunked-mesh renderer — a few
        /// dozen draw calls instead of tens of thousands of SpriteRenderers, with the chunks carrying
        /// <c>HideFlags.DontSave</c> so grass cannot serialize into a scene again even by accident. What
        /// this region still has to budget is the FIELD's own size, which is one byte per candidate site:
        /// <see cref="NineMileCreekFields.GrassStepMetres"/> and its single slot are that budget, and
        /// <c>NineMileCreekFieldsTests</c> pins it.</para>
        /// </summary>
        static void PlantGrass(ITidalTerrain terrain, Result result)
        {
            var library = GrassLibraryCatalog.Load();
            var imported = GrassLibraryCatalog.Imported(library);
            if (imported.Count == 0)
            {
                Debug.LogWarning(
                    "[NineMileCreekFieldPlanter] the grass library resolved no imported sprites — the " +
                    "fields ship unmoving (splat ground only). Bake it (Hidden Harbours ▸ Dev ▸ Bake " +
                    "Grass Library), and `git lfs pull` if the PNGs are still pointers.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                StPetersWoodsPlanter.GrassMaterialPath);
            if (material == null)
                Debug.LogWarning(
                    $"[NineMileCreekFieldPlanter] {StPetersWoodsPlanter.GrassMaterialPath} missing — the " +
                    "tufts will stand still instead of swaying on the shared wind.");

            var layout = NineMileCreekFields.FieldLayout();
            var baked = StPetersGrassField.Encode(
                layout,
                NineMileCreekFields.ScatterGrass(terrain),
                NineMileCreekFields.StrawCellMetres,
                NineMileCreekFields.StrawAt);

            if (baked.Sites == null || baked.Tufts == 0)
            {
                Debug.LogWarning(
                    "[NineMileCreekFieldPlanter] the grass field baked no sites — the region has no " +
                    "ground the meadow is allowed to plant on. Check the terrain is configured before " +
                    "the planter runs.");
                return;
            }

            var chooser = new StPetersWoodsPlanter.GrassArtChooser(imported);
            var pools = StPetersWoodsPlanter.ResolveGrassPools(chooser, out Sprite[] variants);

            // ⚠ FIND, then create — a field is written IN PLACE (ADR 0019). A second run must rewrite the
            // same bytes rather than leave two meadows standing on one set of fields.
            var root = GameObject.Find("/" + GrassRootName) ?? new GameObject(GrassRootName);
            var field = root.GetComponent<GrassField>() ?? root.AddComponent<GrassField>();

            field.SetField(
                new Vector2(baked.Layout.OriginX, baked.Layout.OriginY),
                baked.Layout.CellSize, baked.Layout.JitterMetres, baked.Layout.SpreadMetres,
                baked.Layout.CellsX, baked.Layout.CellsY, baked.Layout.Slots, baked.Layout.Seed,
                baked.Sites,
                baked.StrawCellSize, baked.StrawCellsX, baked.StrawCellsY, baked.Straw,
                NineMileCreekFields.StrawTint,
                variants, pools, material);

            result.GrassTufts = baked.Tufts;
            result.GrassPayloadCharacters = field.PayloadCharacters;
            foreach (var kv in baked.PerHabitat) result.PerGrassHabitat[kv.Key] = kv.Value;
        }

        // =============================================================================================
        //  THE HEDGEROWS
        // =============================================================================================

        /// <summary>
        /// The hedges, from the shrub kit's committed slice. Plain sprites plus the kit's own baked light
        /// channels — the same treatment the island's shrubs get, and for the same reason: the veil/light
        /// contract is art-pipeline's lane and wiring anything more from here would be building someone
        /// else's seam.
        ///
        /// <para><b>⚠ THE SLICE IS ST PETERS' BAKE, AND THAT IS DELIBERATE.</b> The kit ships nothing by
        /// default; six species were baked and committed for the island, chosen to cover all five of the
        /// rig's habitats. This is the same coast with the same species in the same salt wind, so this
        /// region reads what is on disk rather than asking for a second bake of the same bushes. Where a
        /// habitat has no baked species, that habitat's stations simply go unplanted and the console says
        /// so — a reported gap, not a substitution.</para>
        /// </summary>
        static void PlantHedges(ITidalTerrain terrain, Result result)
        {
            var contract = ShrubCatalog.Load();
            if (contract?.Species == null || contract.Species.Count == 0)
            {
                Debug.LogWarning("[NineMileCreekFieldPlanter] the shrub contract is unreadable — no " +
                                 "hedgerows. The kit ships to order; run 'Bake St Peters Shrub Slice'.");
                return;
            }

            // Habitat comes from the CONTRACT, never from a table here.
            var habitatOf = new Dictionary<string, string>();
            foreach (var e in contract.Species) habitatOf[e.Key] = e.Habitat;

            // Which baked species actually has pixels, grouped by the habitat the contract gives it.
            var byHabitat = new Dictionary<string, List<string>>();
            var stems = new Dictionary<string, string>();
            foreach (string species in StPetersShrubBake.Species)
            {
                string stem = ShrubCatalog.VariantSheetStem(species, StPetersShrubBake.Stage,
                                                            StPetersShrubBake.Phase);
                string path = ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.Albedo);
                if (!AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Any()) continue;
                if (!habitatOf.TryGetValue(species, out string habitat) ||
                    string.IsNullOrEmpty(habitat)) continue;

                stems[species] = stem;
                if (!byHabitat.TryGetValue(habitat, out var list)) byHabitat[habitat] = list = new List<string>();
                list.Add(species);
            }

            if (byHabitat.Count == 0)
            {
                Debug.LogWarning("[NineMileCreekFieldPlanter] none of the shrub sheets are on disk — no " +
                                 "hedgerows. Run 'Hidden Harbours ▸ Art ▸ Bake St Peters Shrub Slice'.");
                return;
            }

            var root = new GameObject(HedgeRootName);
            var shrubMat = AssetDatabase.LoadAssetAtPath<Material>(
                StPetersWoodsPlanter.LitShrubMaterialPath);
            var spriteCache = new Dictionary<(string, int), Sprite>();
            var lightCache = new Dictionary<string, (Texture2D light, Texture2D state)>();
            var unservedHabitats = new HashSet<string>();

            foreach (var site in NineMileCreekFields.ScatterHedges(terrain, ShrubCatalog.Variants))
            {
                if (!byHabitat.TryGetValue(site.Habitat, out var pool) || pool.Count == 0)
                {
                    unservedHabitats.Add(site.Habitat);
                    continue;
                }

                // A habitat with more than one baked species mixes them on the site's own roll, so a
                // hedge is rose AND raspberry rather than one long run of the same bush.
                string species = pool[NineMileCreekFields.HashOfLine(site.Line + site.Variant) % pool.Count];
                string stem = stems[species];

                var key = (species, site.Variant);
                if (!spriteCache.TryGetValue(key, out Sprite sprite))
                {
                    // Slices are <stem>_c<col>_f<row>: the column is the individual, the row the sway
                    // frame, of which a static planting takes the one at rest.
                    string want = $"{stem}_c{site.Variant}_f{RestSwayRow}";
                    sprite = AssetDatabase
                        .LoadAllAssetsAtPath(ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.Albedo))
                        .OfType<Sprite>().FirstOrDefault(sp => sp.name == want);
                    spriteCache[key] = sprite;
                }
                if (sprite == null) continue;

                var go = new GameObject($"{species}_{site.Variant}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                // The kit's pivot is the ROOT CROWN — the ground-contact point — so the site position IS
                // where the shrub is planted. No offset, no rescale: the bake is at metric size already.
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = ShrubSortingOrder;
                if (shrubMat != null) sr.sharedMaterial = shrubMat;
                go.AddComponent<YSortSprite>();

                if (!lightCache.TryGetValue(species, out var sheets))
                {
                    sheets = (
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.Light)),
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.State)));
                    lightCache[species] = sheets;
                }
                // ⚠️ RED, not blue — the shrub rig's no-rim flag is the calendar sheet's R. The shoreline
                // plants use their tide sheet's B. Both committed contracts say so; do not infer it.
                if (sheets.light != null)
                    go.AddComponent<SpriteLightBinder>().SetSheets(
                        sheets.light, normalSheet: null, rimGateSheet: sheets.state,
                        rimGateChannel: SpriteLightBinder.RimGateChannel.Red);

                result.Shrubs++;
                result.PerShrubHabitat[site.Habitat] =
                    result.PerShrubHabitat.TryGetValue(site.Habitat, out int n) ? n + 1 : 1;
            }

            if (unservedHabitats.Count > 0)
                Debug.LogWarning(
                    "[NineMileCreekFieldPlanter] the hedgerows asked for habitats the committed shrub " +
                    $"slice does not bake: {string.Join(", ", unservedHabitats)}. Those stations are " +
                    "unplanted rather than filled with the wrong bush — a species standing in the wrong " +
                    "habitat looks finished while being wrong. Art ask: extend the bake, or accept the " +
                    "gap.");
        }

        // =============================================================================================
        //  THE TREES
        // =============================================================================================

        /// <summary>
        /// The trees, from the Acadian kit. <b>Scattered — never a stand</b>: most of them stand in a
        /// hedgerow (which is what the commonest tree in farmland anywhere actually is) and the rest
        /// alone in a field.
        /// </summary>
        static void PlantTrees(ITidalTerrain terrain, Result result)
        {
            var placements = AcadianTreeCatalog.Scan();
            if (placements.Count == 0)
            {
                Debug.LogWarning("[NineMileCreekFieldPlanter] the Acadian tree contract is missing or " +
                                 "claims no trees — the fields were left treeless. Import the tree kit.");
                return;
            }

            var bySpecies = new Dictionary<string, AcadianTreeCatalog.Placement>();
            foreach (var p in placements)
                if (!bySpecies.ContainsKey(p.Species)) bySpecies[p.Species] = p;

            var material = AcadianTreeCatalog.LoadMaterial();
            if (material == null)
                Debug.LogWarning($"[NineMileCreekFieldPlanter] {AcadianTreeCatalog.MaterialPath} missing " +
                                 "— the trees will stand still instead of swaying on the shared wind.");

            var root = new GameObject(TreeRootName);
            var spriteCache = new Dictionary<(string, int), Sprite>();

            foreach (var site in NineMileCreekFields.ScatterTrees(
                         terrain, bySpecies.Keys,
                         species => bySpecies.TryGetValue(species, out var pl)
                                    ? AcadianTreeCatalog.VariantCount(pl.Entry) : 1))
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
                // The rig's pivot is the TRUNK FOOT (ADR 0026), so the site position IS where the tree
                // is planted — no offset, and no rescale: the bake is at honest metric size.
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);
                AcadianTreeCatalog.Configure(go, placement, sprite, material);

                result.Trees++;
                if (site.InHedge) result.HedgeTrees++; else result.FieldTrees++;
                result.PerTreeSpecies[site.Species] =
                    result.PerTreeSpecies.TryGetValue(site.Species, out int n) ? n + 1 : 1;
            }
        }

        // =============================================================================================
        //  THE MARSH
        // =============================================================================================

        /// <summary>
        /// Salt marsh round the barachois and the marsh pool, from the shore-plant kit's committed Defs.
        /// Each plant gets a <see cref="ShorePlantTideView"/>, so the marsh follows the water exactly as
        /// the foreshore's does — which on a barachois is the point: it is a mirror at high water and a
        /// red mudflat at low, and the cordgrass at its edge stands in both.
        ///
        /// <para><b>⚠ NO <see cref="YSortSprite"/></b>, the island's own note: a submerged plant draws
        /// BELOW the Sea plane and the Y-sort band starts above it, so the view sets the order
        /// directly.</para>
        /// </summary>
        static void PlantMarsh(ITidalTerrain terrain, Result result)
        {
            var sites = NineMileCreekFields.ScatterMarsh(terrain);
            if (sites.Count == 0) return;

            var defs = new Dictionary<string, ShorePlantDef>();
            ShorePlantDef Def(string key)
            {
                if (defs.TryGetValue(key, out var d)) return d;
                d = AssetDatabase.LoadAssetAtPath<ShorePlantDef>(
                    $"{ShorePlantDefBuilder.DefFolder}/{key}.asset");
                defs[key] = d;
                return d;
            }

            var root = new GameObject(MarshRootName);
            var plantMat = AssetDatabase.LoadAssetAtPath<Material>(
                StPetersWoodsPlanter.LitShorePlantMaterialPath);
            var missing = new HashSet<string>();

            foreach (var site in sites)
            {
                var def = Def(site.SpeciesKey);
                if (def == null || !def.IsComplete()) { missing.Add(site.SpeciesKey); continue; }

                var go = new GameObject(site.SpeciesKey);
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);

                float drawn = site.Scale * Mathf.Max(0.01f, def.PlantedScale);
                go.transform.localScale = new Vector3(drawn, drawn, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = def.SpriteFor(0);      // the view replaces this on its first refresh
                sr.sortingOrder = def.SubmergedSortingOrder;
                if (plantMat != null) sr.sharedMaterial = plantMat;

                // The binder BEFORE the view: setting Def is what publishes the sheets into it.
                if (def.HasLightChannels) go.AddComponent<SpriteLightBinder>();

                var view = go.AddComponent<ShorePlantTideView>();
                view.Def = def;

                if (!def.Algae && def.Zone != StPetersWoodsPlanter.SubtidalZone &&
                    def.PlantedStandingHeightM >= StPetersWoodsPlanter.ShadowCasterMinHeightM)
                    go.AddComponent<SpriteShadow>();

                result.MarshPlants++;
                result.PerMarshZone[site.Zone] =
                    result.PerMarshZone.TryGetValue(site.Zone, out int n) ? n + 1 : 1;
            }

            if (missing.Count > 0)
                Debug.LogWarning(
                    $"[NineMileCreekFieldPlanter] {missing.Count} marsh species have no complete Def and " +
                    $"were skipped: {string.Join(", ", missing)}. Bake the sheets and build the Defs " +
                    "(Hidden Harbours ▸ Dev ▸ Bake Shore Plant Sheets, then Build Shore Plant Defs).");
        }
    }
}
#endif
