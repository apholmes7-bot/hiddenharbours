#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// ⭐ <b>THE MAINLAND COAST STANDS UP.</b> Nine Mile Creek's cliff walls, drawn from their Defs: the
    /// same Cliff Face kit, the same <see cref="CliffWallSurface"/>, the same decor sorting as St Peters,
    /// for a coast that is an open RUN rather than a ring.
    ///
    /// <para><b>⚠ THE WALLS ARE DATA (NMC H3).</b> One <see cref="CliffWallDef"/> per wall, by its real id
    /// (<c>wall.nmc_NNN</c>), under <see cref="WallsFolder"/>, written from the third-issue ground (G3) by
    /// <c>tools/nmc-walls</c>. A retired wall keeps its Def and its id and spawns nothing; a held wall is
    /// a face G3 asks for that the scene does not wear yet. <see cref="Build"/> draws the live ones through
    /// <see cref="StPetersCliffWalls.ChunksOfDefs"/> (each Def one chunk, named by
    /// <see cref="StPetersCliffWalls.WallName"/>), and the scene's walls are written from the same Defs by
    /// text in N1 step <see cref="NineMileCreekGroundSteps.WallsStep"/>, so a rebuild and the step agree.</para>
    ///
    /// <para><b>⚠ AND NEVER THE ANALYTIC WALK.</b> The walls used to be sampled off the builder's premise
    /// (<see cref="MainlandTidalTerrain"/>), the coast plan before G3. That ground no longer stands: G3 moved
    /// or retired half its walls, and a rebuild that walked the premise would put them back. There is no
    /// path from the premise to a wall any more.</para>
    /// </summary>
    public static class NineMileCreekCliffWalls
    {
        /// <summary>The scene object every generated chunk parents under. Rebuilding destroys it whole,
        /// which is what makes a Refresh converge rather than stack a second coast on the first.</summary>
        public const string RootName = "CliffWalls";

        /// <summary>Where this region's wall Defs live, one per file, by real id.</summary>
        public const string WallsFolder = "Assets/_Project/Data/Terrain/NineMileCreekWalls";

        /// <inheritdoc cref="StPetersCliffWalls.CliffFaceMat"/>
        public const string CliffFaceMat = StPetersCliffWalls.CliffFaceMat;

        /// <inheritdoc cref="StPetersCliffWalls.StationMetres"/>
        public const float StationMetres = StPetersCliffWalls.StationMetres;

        /// <inheritdoc cref="StPetersCliffWalls.ChunkMetres"/>
        public const float ChunkMetres = StPetersCliffWalls.ChunkMetres;

        /// <inheritdoc cref="StPetersCliffWalls.ChunkToeSpanMetres"/>
        public const float ChunkToeSpanMetres = StPetersCliffWalls.ChunkToeSpanMetres;

        /// <inheritdoc cref="StPetersCliffWalls.MinFaceDropMetres"/>
        public const float MinFaceDropMetres = StPetersCliffWalls.MinFaceDropMetres;

        /// <inheritdoc cref="StPetersCliffWalls.MinBatterDegrees"/>
        public static float MinBatterDegrees => StPetersCliffWalls.MinBatterDegrees;

        /// <summary>The rock this coast is built from. Nine Mile Creek is the same low RED SANDSTONE coast
        /// as the island across the bar — the owner's photographs are of red banks — so it takes the kit's
        /// shipped default, and the till overburden over it.</summary>
        public const string Rock = StPetersCliffWalls.Rock;

        /// <inheritdoc cref="StPetersCliffWalls.OverburdenRock"/>
        public const string OverburdenRock = StPetersCliffWalls.OverburdenRock;

        /// <summary>
        /// The soil horizon on this coast's faces, in metres of TRUE HEIGHT.
        ///
        /// <para><b>⭐ IT IS THIS REGION'S OWN MEADOW BAND, and it happens to equal the island's.</b> St
        /// Peters derives 1.8 m from "the plateau stands at 6 m and grass reaches down to 4.2 m". Nine Mile
        /// Creek's fields stand at <see cref="NineMileCreekMainland.LandElevation"/> (6 m) and its grass
        /// reaches down to <see cref="NineMileCreekShoreMap.GrassFloorElevation"/> (4.2 m) — the same two
        /// numbers, from this region's own plan. Derived rather than copied, so a re-tune of either moves
        /// the soil on the face with it.</para>
        ///
        /// <para><b>⚠ St Peters deliberately does NOT derive it</b>, and states why: the owner still owes a
        /// ruling on how far grass may reach over a brow, and that ruling must be free to move the island's
        /// GRASS without repainting every island cliff. That reasoning is about a ruling on ST PETERS. This
        /// coast is 27% cliff against a 93% ceiling and its bands were authored together in one pass, so
        /// the coupling is the honest one here — and if the ruling lands the other way, this is one line.</para>
        /// </summary>
        public static float OverburdenMetres =>
            NineMileCreekMainland.LandElevation - NineMileCreekShoreMap.GrassFloorElevation;

        /// <summary>Every Nine Mile Creek wall Def, live, retired and held, by id.</summary>
        public static List<CliffWallDef> LoadDefs()
        {
            var defs = new List<CliffWallDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CliffWallDef), new[] { WallsFolder }))
            {
                var d = AssetDatabase.LoadAssetAtPath<CliffWallDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null) defs.Add(d);
            }
            defs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return defs;
        }

        // =============================================================================================
        //  the scene
        // =============================================================================================

        /// <summary>
        /// Build (or rebuild) this region's cliff walls under a single <see cref="RootName"/> object, from
        /// its walls' Defs (<see cref="LoadDefs"/>): each live Def is one chunk, named by its real id.
        /// Destroys any previous root first, so a builder Refresh converges: running twice gives the same
        /// coast as running once.
        ///
        /// <para>⚠ It draws what the Defs say, which is what N1 step
        /// <see cref="NineMileCreekGroundSteps.WallsStep"/> writes into the scene, cut at the span the Defs
        /// were cut at (<see cref="ChunkToeSpanMetres"/>), so Defs that no longer meet it are refused.</para>
        /// </summary>
        public static int Build(IReadOnlyList<CliffWallDef> defs)
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            List<StPetersCliffWalls.Chunk> chunks =
                StPetersCliffWalls.ChunksOfDefs(defs, ChunkToeSpanMetres, out List<CliffWallDef> owners);
            if (chunks.Count == 0) return 0;

            var material = AssetDatabase.LoadAssetAtPath<Material>(CliffFaceMat);
            if (material == null)
            {
                Debug.LogError($"[nmc-cliff-walls] {CliffFaceMat} is missing — the coast will build " +
                               "without faces. The material is committed; its TEXTURES are not (the bake " +
                               "root is gitignored by design).");
                return 0;
            }

            // The colour slot's file by the material's look — v10 colour, or the px index the bake
            // moved into it keeping the GUID. Asked once per build, of the catalog, never spelled here.
            string colour = CliffCatalog.ColourChannel(CliffBakeMenu.IsPxLook);

            var root = new GameObject(RootName);
            int built = 0, stratified = 0;
            for (int k = 0; k < chunks.Count; k++)
            {
                StPetersCliffWalls.Chunk chunk = chunks[k];
                if (!TryLoadBands(chunk, colour, out CliffFaceBand[] bands, out Texture2D profile)) continue;
                if (bands.Length > 1) stratified++;

                LoadDecals(chunk, out Texture2D browStrip, out Texture2D toeStrip);

                StPetersCliffWalls.ChunkFields fields = StPetersCliffWalls.FieldsOf(in chunk);
                Vector2[] brow = fields.BrowPlan;

                var go = new GameObject(StPetersCliffWalls.WallName(owners[k]));
                go.transform.SetParent(root.transform, worldPositionStays: false);
                // Park the chunk at its own first brow so its vertices stay small and local — a mesh
                // authored at absolute world coordinates hundreds of metres out loses float precision in
                // exactly the sub-texel range the kit's 32 px/m relies on.
                go.transform.position = new Vector3(brow[0].x, brow[0].y, 0f);

                var surface = go.AddComponent<CliffWallSurface>();
                surface.Configure(brow, fields.ToePlan, fields.DropMetres, material, bands, profile,
                                  browStrip, toeStrip,
                                  fields.AlongOffsetMetres, fields.RowsBasisSurfaceMetres,
                                  fields.WallAzimuth,
                                  fields.Batter,
                                  CliffCatalog.AspectBakeLights[chunk.AspectIndex],
                                  CliffCatalog.FaceMetresS, CliffCatalog.FaceMetresT,
                                  CliffCatalog.ProfileSubdivideMetres, CliffCatalog.ProfileMetres,
                                  CliffCatalog.StripMetresT, CliffCatalog.BrowLineAt,
                                  fields.ToeElevations,
                                  fields.TextureBrowPlan, fields.SliceAt, fields.SliceBefore, fields.SliceAfter);
                built++;
            }

            int runs = 0;
            foreach (StPetersCliffWalls.Chunk c in chunks) runs = Mathf.Max(runs, c.RunIndex + 1);
            Debug.Log($"[nmc-cliff-walls] {built} chunks of standing cliff over {runs} runs, from " +
                      $"{chunks.Count} live of {defs.Count} wall Defs (stations every {StationMetres} m). " +
                      $"{stratified} carry a {OverburdenMetres:F1} m topsoil band over the {Rock}. " +
                      "Sorted into the decor band by each chunk's own toe (ADR 0032).");
            return built;
        }

        static int CatalogBatter(StPetersCliffWalls.Chunk c) =>
            StPetersCliffWalls.BakedBatterToCatalogIndex(c.BatterIndex);

        /// <summary>A face's rock bands, brow-downward: the eroded topsoil, then the sandstone under it.
        /// One profile serves both — it is the LANDFORM's displacement and the bands are materials lying
        /// on it. The fallback to one unstratified band is deliberate: a hole in the coast is a far worse
        /// failure than an unstratified cliff.</summary>
        static bool TryLoadBands(StPetersCliffWalls.Chunk chunk, string colour, out CliffFaceBand[] bands,
                                 out Texture2D profile)
        {
            bands = new CliffFaceBand[0];
            int catalogBatter = CatalogBatter(chunk);
            string aspect = CliffCatalog.Aspects[chunk.AspectIndex];

            profile = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Profile)}/" +
                $"{CliffCatalog.ProfileName(Rock, catalogBatter)}.png");

            if (!TryLoadFaceSet(Rock, aspect, catalogBatter, colour, out CliffFaceBand rock))
            {
                Debug.LogWarning(
                    $"[nmc-cliff-walls] no baked face for {Rock} {aspect} " +
                    $"{CliffCatalog.Batters[catalogBatter]} — that stretch of coast will not stand up. " +
                    "The kit ships as the rig; run the builder (it bakes on missing) or " +
                    "'Hidden Harbours ▸ Dev ▸ Bake Cliff Face Kit'.");
                return false;
            }

            // The soil horizon is a vertical depth; the face is addressed along its own surface, so the
            // conversion uses the SNAPPED batter — the angle the pixels were baked at, not the station's
            // true one, or the band sits at a depth the texture disagrees with.
            float overburden = CliffWallGeometry.OverburdenSurfaceMetres(
                OverburdenMetres, CliffCatalog.BatterAngles[catalogBatter]);

            if (!TryLoadFaceSet(OverburdenRock, aspect, catalogBatter, colour, out CliffFaceBand soil))
            {
                Debug.LogWarning(
                    $"[nmc-cliff-walls] no baked {OverburdenRock} face for {aspect} " +
                    $"{CliffCatalog.Batters[catalogBatter]} — this face will stand as bare {Rock} with " +
                    "no topsoil band. Re-run the builder (it bakes on missing) or " +
                    $"'Hidden Harbours ▸ Dev ▸ Bake Cliff Face Kit — {OverburdenRock}'.");
                rock.Label = "Rock";
                bands = new[] { rock };
                return true;
            }

            soil.Label = "Overburden";
            soil.StartSurfaceMetres = 0f;
            soil.EndSurfaceMetres = overburden;
            rock.Label = "Rock";
            rock.StartSurfaceMetres = overburden;
            rock.EndSurfaceMetres = 0f;             // ...to the toe
            bands = new[] { soil, rock };
            return true;
        }

        static bool TryLoadFaceSet(string rock, string aspect, int catalogBatter, string colour,
                                   out CliffFaceBand band)
        {
            band = CliffFaceBand.WholeFace(
                LoadFace(rock, aspect, catalogBatter, colour),
                LoadFace(rock, aspect, catalogBatter, "_normal"),
                LoadFace(rock, aspect, catalogBatter, "_mask"),
                rock);
            return band.HasChannels;
        }

        /// <summary>The kit's own finisher for a face's ends — the hanging sod lip at the brow, the sea's
        /// undercut at the toe. Loaded as TEXTURES, not sprites: the strips import as
        /// <c>SpriteImportMode.Multiple</c> and an unsliced Multiple-mode asset carries ZERO sprites, so
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> hands back null on a freshly baked checkout. Missing decals
        /// are NOT an error — the wall stands without them.</summary>
        static void LoadDecals(StPetersCliffWalls.Chunk chunk, out Texture2D brow, out Texture2D toe)
        {
            int catalogBatter = CatalogBatter(chunk);
            string aspect = CliffCatalog.Aspects[chunk.AspectIndex];
            string folder = CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Strip);

            brow = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{folder}/Brow_{CliffCatalog.BrowName(aspect, CliffCatalog.BaseStep)}.png");

            string feature = CliffCatalog.ToeFeatureForBatter[catalogBatter];
            toe = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{folder}/Toe_{CliffCatalog.ToeName(aspect, CliffCatalog.BaseStep, feature)}.png");
        }

        static Texture2D LoadFace(string rock, string aspect, int catalogBatter, string channel) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Face)}/" +
                $"{CliffCatalog.FaceName(rock, aspect, catalogBatter, CliffCatalog.BaseStep, channel)}.png");
    }
}
#endif
