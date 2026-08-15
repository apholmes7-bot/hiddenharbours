#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using HiddenHarbours.Art;                 // YSortSprite, ShorePlantTideView, SpriteLightBinder, SpriteShadow
using HiddenHarbours.Art.Editor;          // ShorelineIsoCatalog (the rock sheet)
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Hangs Nine Mile Creek's shore</b> — the tidal planting and the coast's scattered rock.
    /// <see cref="NineMileCreekShore"/> makes every decision and this class only does the Unity work, so
    /// the coast itself is testable without a scene (the <see cref="StPetersShorePainter"/> /
    /// <see cref="StPetersWoodsPlanter"/> split, in one file because this region needs one pass).
    ///
    /// <para><b>⭐ NO GROUND OR FRINGE TILEMAP, AND THAT IS #527 ALREADY DONE.</b> St Peters' painter
    /// stamps three tilemaps; on this region two of them would be dead weight. ADR 0028's splat shader
    /// renders the ground and fringe LOOK, <c>NineMileCreekBuilder</c> already stands a
    /// <c>TerrainSplatSurface</c> over the whole region with <c>NineMileCreekStarterSplat</c>'s seeded
    /// maps, and <see cref="NineMileCreekShoreMap.MaterialAt"/> is the classifier behind it. So the bands
    /// this pass owes the coast are the ones the shader cannot draw: things that STAND — plants that ride
    /// the tide, rock that sorts against the player, and the contact shade that seats each rock. The one
    /// tilemap here carries that shade and nothing else.</para>
    ///
    /// <para><b>⚠ NO <see cref="YSortSprite"/> on a shore plant, and that is deliberate</b> — St Peters'
    /// finding, and it holds here for the same reason. A submerged plant draws BELOW the Sea plane (−5)
    /// and YSortSprite's band starts at 2 (ADR 0032), so the two disagree by construction and the view
    /// stands YSortSprite down whenever the plant is under water. Almost every plant this scatter places
    /// is submerged for most of the tide, so the component would spend its life disabled; the view sets
    /// the order directly instead. ROCKS do carry one — a rock stands on the foreshore and a player walks
    /// past it.</para>
    /// </summary>
    public static class NineMileCreekShorePainter
    {
        /// <summary>The root the whole shore hangs under, so it is one object to find in the Hierarchy —
        /// and one object to destroy, which is what makes a rebuild converge instead of stacking a second
        /// coast on the first.</summary>
        public const string RootName = "Shoreline";

        /// <inheritdoc cref="StPetersWoodsPlanter.ShorePlantRootName"/>
        public const string ShorePlantRootName = StPetersWoodsPlanter.ShorePlantRootName;

        /// <inheritdoc cref="StPetersShorePainter.RocksRootName"/>
        public const string RocksRootName = StPetersShorePainter.RocksRootName;

        /// <inheritdoc cref="StPetersShorePainter.ContactLayerName"/>
        public const string ContactLayerName = StPetersShorePainter.ContactLayerName;

        /// <inheritdoc cref="StPetersShorePainter.ContactSortingOrder"/>
        public const int ContactSortingOrder = StPetersShorePainter.ContactSortingOrder;

        /// <inheritdoc cref="StPetersShorePainter.RockSheetPath"/>
        public const string RockSheetPath = StPetersShorePainter.RockSheetPath;

        /// <inheritdoc cref="StPetersWoodsPlanter.LitShorePlantMaterialPath"/>
        public const string LitShorePlantMaterialPath = StPetersWoodsPlanter.LitShorePlantMaterialPath;

        /// <summary>What one pass laid down — logged by the builder and asserted by the layout test so
        /// the footprint cannot quietly grow into a scene nobody can open.</summary>
        public sealed class Result
        {
            public int ShorePlants;
            public int Rocks;
            public int CliffChunks;
            public readonly Dictionary<string, int> PerZone = new Dictionary<string, int>();
            public readonly Dictionary<NineMileCreekShore.ShoreTreatment, int> PerTreatment =
                new Dictionary<NineMileCreekShore.ShoreTreatment, int>();

            public string ZoneSummary() => Summarise(PerZone);

            public string TreatmentSummary()
            {
                var byName = new Dictionary<string, int>();
                foreach (var kv in PerTreatment) byName[kv.Key.ToString()] = kv.Value;
                return Summarise(byName);
            }

            static string Summarise(Dictionary<string, int> counts)
            {
                var parts = new List<string>();
                foreach (var kv in counts) parts.Add($"{kv.Key} {kv.Value:N0}");
                parts.Sort();
                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// Paint the shore into the open scene and return what it laid down. Idempotent: the root is
        /// destroyed and rebuilt, so the owner's second Build click reproduces the first exactly.
        /// </summary>
        public static Result Paint(MainlandTidalTerrain terrain)
        {
            var result = new Result();
            if (terrain == null)
            {
                Debug.LogWarning("[NineMileCreekShorePainter] no terrain — shore not painted.");
                return result;
            }

            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            var grid = root.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);   // 1 m cells = a 32 px tile at the locked PPU 32

            Tilemap contactMap = MakeLayer(root.transform, ContactLayerName, ContactSortingOrder);

            PlantShorePlants(terrain, root.transform, result);
            result.Rocks = PlaceRocks(terrain, root.transform, contactMap);

            Debug.Log($"[NineMileCreekShorePainter] the shore of Nine Mile Creek: " +
                      $"{result.ShorePlants:N0} plants ({result.ZoneSummary()}) across " +
                      $"{result.TreatmentSummary()}, and {result.Rocks} rocks on the foreshore. " +
                      "Plants ride the tide (ShorePlantTideView); the ground/fringe LOOK is the splat " +
                      "shader's (ADR 0028, #527).");
            return result;
        }

        // =====================================================================================
        //  the planting
        // =====================================================================================

        static void PlantShorePlants(MainlandTidalTerrain terrain, Transform parent, Result result)
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

            var sites = NineMileCreekShore.Scatter(terrain);
            if (sites.Count == 0) return;

            var root = new GameObject(ShorePlantRootName);
            root.transform.SetParent(parent, worldPositionStays: false);
            var missing = new HashSet<string>();
            var plantMat = AssetDatabase.LoadAssetAtPath<Material>(LitShorePlantMaterialPath);

            foreach (var site in sites)
            {
                var def = Def(site.SpeciesKey);
                if (def == null || !def.IsComplete()) { missing.Add(site.SpeciesKey); continue; }

                var go = new GameObject(site.SpeciesKey);
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);

                // The species' own PlantedScale × this site's jitter, folded into the TRANSFORM — which is
                // what ShorePlantTideView reads to decide submergence. One drawn size, one height, no way
                // for the two to disagree.
                float drawn = site.Scale * Mathf.Max(0.01f, def.PlantedScale);
                go.transform.localScale = new Vector3(drawn, drawn, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = def.SpriteFor(0);          // the view replaces this on its first refresh
                sr.sortingOrder = def.SubmergedSortingOrder;
                if (plantMat != null) sr.sharedMaterial = plantMat;

                // The binder goes on BEFORE the view: setting Def is what publishes the sheets into it,
                // and a binder added afterwards would sit empty until something else refreshed the plant.
                if (def.HasLightChannels) go.AddComponent<SpriteLightBinder>();

                var view = go.AddComponent<ShorePlantTideView>();
                view.Def = def;                         // setting Def refreshes, so a built scene looks right

                // Shadows only where the silhouette earns one: not algae (weed drapes, it does not stand),
                // not the permanently-submerged fringe, and tall enough AS DRAWN to throw something.
                if (!def.Algae && def.Zone != StPetersWoodsPlanter.SubtidalZone &&
                    def.PlantedStandingHeightM >= StPetersWoodsPlanter.ShadowCasterMinHeightM)
                    go.AddComponent<SpriteShadow>();

                result.ShorePlants++;
                result.PerZone[site.Zone] =
                    result.PerZone.TryGetValue(site.Zone, out int n) ? n + 1 : 1;
                result.PerTreatment[site.Treatment] =
                    result.PerTreatment.TryGetValue(site.Treatment, out int t) ? t + 1 : 1;
            }

            if (missing.Count > 0)
                Debug.LogWarning(
                    $"[NineMileCreekShorePainter] {missing.Count} shore plant species have no complete " +
                    $"Def and were skipped: {string.Join(", ", missing)}. Bake the sheets and build the " +
                    "Defs (Hidden Harbours ▸ Dev ▸ Bake Shore Plant Sheets, then Build Shore Plant Defs).");
        }

        // =====================================================================================
        //  the rock
        // =====================================================================================

        /// <summary>The coast's boulders as plain sprites, each carrying a <see cref="YSortSprite"/> so it
        /// interleaves with the player by world Y and can never sink behind the water plane. A
        /// contact-shade tile is stamped on the ground cell NORTH of each one, which is what
        /// <c>Contact.png</c> is for: it seats the rock into the ground instead of letting it float.</summary>
        static int PlaceRocks(MainlandTidalTerrain terrain, Transform parent, Tilemap contactMap)
        {
            var sites = NineMileCreekShore.ScatterRocks(terrain);
            if (sites.Count == 0) return 0;

            var rockSprites = LoadRockSprites();
            if (rockSprites.Count == 0)
            {
                Debug.LogWarning($"[NineMileCreekShorePainter] no rock sprites at {RockSheetPath} — the " +
                                 "foreshore has no visible tell. Import/slice the shoreline-ISO v7 sheet.");
                return 0;
            }

            // The tiles are DERIVED (sheet + catalog), generated on demand and never committed, so make
            // sure they exist before stamping with them — every run.
            ShoreIso2TileLibrary.Build(ShoreIso2TileLibrary.DefaultStyle);
            var contactTile = ShoreIso2TileLibrary.Load(
                ShoreIso2TileLibrary.DefaultStyle, ShoreIso2TileLibrary.ContactName("n"));

            var rocksRoot = new GameObject(RocksRootName);
            rocksRoot.transform.SetParent(parent, worldPositionStays: false);

            int placed = 0;
            foreach (var site in sites)
            {
                if (!rockSprites.TryGetValue(site.Sprite, out Sprite sprite) || sprite == null) continue;

                var go = new GameObject($"Rock_{site.Sprite}");
                go.transform.SetParent(rocksRoot.transform, worldPositionStays: false);
                go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                go.AddComponent<YSortSprite>();

                if (contactTile != null && contactMap != null)
                    contactMap.SetTile(
                        new Vector3Int(Mathf.FloorToInt(site.Position.x),
                                       Mathf.FloorToInt(site.Position.y) + 1, 0),
                        contactTile);
                placed++;
            }
            return placed;
        }

        /// <summary>The rock sheet's named sub-sprites, keyed by the kit's own item name. They are pivoted
        /// at their base on import, so a rock placed at a world position stands ON it rather than through
        /// it.</summary>
        static Dictionary<string, Sprite> LoadRockSprites()
        {
            var byName = new Dictionary<string, Sprite>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(RockSheetPath))
            {
                if (!(obj is Sprite s)) continue;
                foreach (string item in ShorelineIsoCatalog.RockSprites)
                    if (s.name.EndsWith("_" + item, System.StringComparison.Ordinal)) byName[item] = s;
            }
            return byName;
        }

        static Tilemap MakeLayer(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            return map;
        }
    }
}
#endif
