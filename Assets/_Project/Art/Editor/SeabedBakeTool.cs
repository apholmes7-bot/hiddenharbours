using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// (ADR 0027 #7) Bake <c>_SeabedTex</c> — the BOTTOM the water shader composites through the
    /// column — over the SAME world rect as <c>_HeightTex</c> (<c>_HeightWorldMin</c> /
    /// <c>_HeightWorldSize</c>, ADR 0014's pattern), so absorption needs no new uniform to place it.
    ///
    /// <para><b>Why a bake and not a sprite behind the sea.</b> §17.1 showed the seabed by LOWERING
    /// <c>col.a</c> so an ungraded sprite bled through the alpha blend. The water shader never saw
    /// that colour, so it could not absorb it — and a scalar alpha cannot carry PER-CHANNEL
    /// transmission at all (ADR 0027 finding 3). Handing the shader the bottom's ALBEDO is what
    /// makes Beer-Lambert possible, and it dissolves §17.3's caustic/see-through cancellation by
    /// construction.</para>
    ///
    /// <para><b>What it reads.</b> The open scene's ground Tilemap — the same canvas the Terrain
    /// Paint Tool's TYPE brush stamps (beach / sandbar / grass / cliff), and the same canvas whose
    /// underwater types (Deep / Channel) deliberately CLEAR their tile. A cleared cell bakes
    /// COVERAGE 0 (alpha), so the shader composites nothing there and open water is unchanged by
    /// construction. Tile sprite pixels are read through a GPU readback (blit → RT → ReadPixels) so
    /// the bake works whether or not the source textures were imported CPU-readable, and it never
    /// mutates an importer behind the owner's back.</para>
    ///
    /// <para><b>Editor-only, deliberately.</b> The readback needs a graphics device; CI has none
    /// (rendering work there CRASHES the editor rather than failing cleanly), so nothing in this
    /// file is reachable from a test. The pure world↔texel mapping the bake and the shader must
    /// agree on lives in <see cref="SeabedBake"/> and IS tested headless.</para>
    ///
    /// <para><b>Budget (rule 7).</b> 512² RGBA32, point-filtered, no mips, uncompressed =
    /// <b>1.0 MB</b> of texture memory for a whole region — against <c>_HeightTex</c>'s 192² R8 =
    /// 36 KB. Over St Peters' 160 × 120 m rect that is 0.31 × 0.23 m per texel (≈ 10 × 7.5 screen
    /// pixels at PPU 32), which the world-grid pixelize and <c>_AbsorptionBands</c> posterize
    /// further downstream — a bottom, not a photograph. 256 halves the axes (0.26 MB) and 1024
    /// quadruples the memory (4.2 MB); the field is exposed so the owner can trade.</para>
    ///
    /// <para>Authored DATA, committed like the painted height map it sits beside: read at runtime,
    /// never written at runtime, no RNG, nothing in the save (rule 5).</para>
    /// </summary>
    public sealed class SeabedBakeTool : EditorWindow
    {
        private const string DataDir = "Assets/_Project/Data/Terrain";

        /// <summary>
        /// Where the bottom's colour comes from.
        ///
        /// <para><b>Why there are two.</b> The tilemap source is the honest one where a bottom has
        /// been PAINTED. It bakes nothing at all where none has — and the cove, the current playable
        /// region, has no tilemap in its committed scene, so a tilemap bake there is a fully
        /// transparent texture and absorption stays invisible. The terrain source needs no painting:
        /// it colours the bottom from the elevation the region ALREADY authors, through
        /// <see cref="SeabedPalette"/>. Use it to make a region judgeable now and re-bake from a
        /// tilemap once the owner has painted one.</para>
        /// </summary>
        public enum Source
        {
            /// <summary>The ground tilemap's painted pixels; no tile = no bottom (coverage 0).</summary>
            GroundTilemap,
            /// <summary>The scene's authored elevation through the seabed ramp; always covered.</summary>
            TerrainElevation,
        }

        [SerializeField] private Source _source = Source.TerrainElevation;
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField] private Vector2 _worldMin = new Vector2(-80f, -60f);
        [SerializeField] private Vector2 _worldSize = new Vector2(160f, 120f);
        [SerializeField] private int _resolution = SeabedBake.DefaultResolution;
        [SerializeField] private string _baseName = "StPetersSeabed";
        [SerializeField] private Color _fill = new Color(0f, 0f, 0f, 0f);   // uncovered = no bottom
        [SerializeField] private Material _targetMaterial;

        private Texture2D _lastBake;

        [MenuItem("Hidden Harbours/Art/Bake Seabed Texture (_SeabedTex)", priority = 24)]
        public static void Open()
        {
            var win = GetWindow<SeabedBakeTool>(true, "Bake Seabed Texture", true);
            win.minSize = new Vector2(430f, 380f);
            win.AutoFillFromOpenScene();
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Bakes the ground below the sea into _SeabedTex over the SAME world rect as " +
                "_HeightTex, so the water shader can composite the bottom itself and absorb it " +
                "per channel (ADR 0027 #7). Cells with no ground tile bake COVERAGE 0 — the sea " +
                "is unchanged there.", MessageType.Info);

            _source = (Source)EditorGUILayout.EnumPopup("Source", _source);
            if (_source == Source.GroundTilemap)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _groundTilemap = (Tilemap)EditorGUILayout.ObjectField(
                        "Ground Tilemap", _groundTilemap, typeof(Tilemap), true);
                    if (GUILayout.Button("Find", GUILayout.Width(48f)))
                        _groundTilemap = FindGroundTilemap();
                }
            }
            else
            {
                EditorGUILayout.LabelField(" ", TerrainSourceLabel(), EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("World rect (must match the height map)", EditorStyles.boldLabel);
            _worldMin = EditorGUILayout.Vector2Field("World min (m)", _worldMin);
            _worldSize = EditorGUILayout.Vector2Field("World size (m)", _worldSize);
            if (GUILayout.Button("Copy rect from the open scene's WaterSurface"))
                AutoFillFromOpenScene();

            EditorGUILayout.Space(4f);
            _resolution = Mathf.Clamp(
                EditorGUILayout.IntPopup("Resolution", _resolution,
                    new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 }), 16, 2048);
            EditorGUILayout.LabelField(" ", BudgetLine(), EditorStyles.miniLabel);
            _fill = EditorGUILayout.ColorField(
                new GUIContent("Uncovered fill", "Alpha 0 (default) = 'no bottom here'."), _fill);
            _baseName = EditorGUILayout.TextField("Output base name", _baseName);
            EditorGUILayout.LabelField(" ", DataDir + "/" + _baseName + "_SeabedTex.png",
                                       EditorStyles.miniLabel);

            EditorGUILayout.Space(8f);
            bool ready = _source == Source.TerrainElevation ? FindTerrain() != null : _groundTilemap != null;
            using (new EditorGUI.DisabledScope(!ready))
                if (GUILayout.Button("Bake", GUILayout.Height(26f)))
                    Bake();
            if (!ready)
                EditorGUILayout.HelpBox(
                    _source == Source.TerrainElevation
                        ? "No ITidalTerrain in the open scene. Open the region scene first."
                        : "No ground tilemap. Click Find, or open the region scene.",
                    MessageType.Warning);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Adopt (explicit — mutates the material asset)",
                                       EditorStyles.boldLabel);
            _targetMaterial = (Material)EditorGUILayout.ObjectField(
                "Water material", _targetMaterial, typeof(Material), false);
            using (new EditorGUI.DisabledScope(_lastBake == null || _targetMaterial == null))
                if (GUILayout.Button("Assign the bake + tick Use Seabed Texture"))
                    AssignToMaterial();
            EditorGUILayout.HelpBox(
                "Assigning only makes the bottom AVAILABLE. It stays invisible until Turbidity " +
                "(sigma) is dialled above 0 — the rule-6 passthrough. Turbidity is mood-eased " +
                "(ADR 0017), so its per-weather values live in Art/Materials/WaterPresets/*.mat, " +
                "not here.", MessageType.None);
        }

        private string BudgetLine()
        {
            long bytes = (long)_resolution * _resolution * 4L;
            float mx = _worldSize.x / Mathf.Max(_resolution, 1);
            float my = _worldSize.y / Mathf.Max(_resolution, 1);
            return $"{bytes / 1024f / 1024f:0.00} MB RGBA32  ·  {mx:0.00} × {my:0.00} m per texel";
        }

        /// <summary>Copy the world rect from the open scene's WaterSurface (the height map's frame),
        /// and pick up its ground tilemap if one has not been chosen.</summary>
        private void AutoFillFromOpenScene()
        {
            var surface = Object.FindAnyObjectByType<WaterSurface>();
            if (surface != null)
            {
                Rect r = surface.HeightWorldRect;
                _worldMin = r.min;
                _worldSize = r.size;
            }
            if (_groundTilemap == null) _groundTilemap = FindGroundTilemap();
        }

        /// <summary>
        /// The open scene's authored terrain, found through the CORE interface rather than a concrete
        /// World type — so this art-lane tool never references the World assembly (rule 4). Every
        /// region's terrain component implements it (the analytic rect ones and the painted one alike),
        /// which is exactly why the interface is the right handle here.
        /// </summary>
        private static ITidalTerrain FindTerrain()
        {
            var all = Object.FindObjectsByType<MonoBehaviour>();
            foreach (var mb in all)
                if (mb is ITidalTerrain terrain && mb.isActiveAndEnabled) return terrain;
            return null;
        }

        private static string TerrainSourceLabel()
        {
            var terrain = FindTerrain();
            return terrain == null
                ? "no ITidalTerrain in the open scene"
                : $"reading {(terrain as MonoBehaviour)?.GetType().Name} through SeabedPalette";
        }

        /// <summary>Prefer the tilemap "Add Paintable Tilemap" creates ("TerrainTilemap"), else any
        /// tilemap in the open scene — the same discovery the Terrain Paint Tool uses.</summary>
        private static Tilemap FindGroundTilemap()
        {
            var all = Object.FindObjectsByType<Tilemap>();
            foreach (var tm in all)
                if (tm != null && tm.gameObject.name.StartsWith("TerrainTilemap",
                        System.StringComparison.Ordinal))
                    return tm;
            return all.Length > 0 ? all[0] : null;
        }


        // ============================ THE HEADLESS BAKE (reproducible, not hand-made) ============================

        /// <summary>
        /// Bake a region's seabed from its authored ELEVATION, with no window and no owner in the loop —
        /// so a committed bake can be regenerated by anyone, byte-for-byte, instead of being a texture
        /// somebody once made and nobody can reproduce. The window's Bake button and this share one path.
        ///
        /// <para>Requires the region's scene to be OPEN (the terrain is a scene component). Returns the
        /// imported texture, or null when no <c>ITidalTerrain</c> is present.</para>
        /// </summary>
        public static Texture2D BakeFromTerrain(Vector2 worldMin, Vector2 worldSize, int resolution,
                                                string baseName)
            => BakeFromTerrain(FindTerrain(), worldMin, worldSize, resolution, baseName);

        /// <summary>
        /// The same bake against an EXPLICIT terrain — for a region whose terrain is builder-authored
        /// rather than scene-authored. The cove is exactly that: its committed scene carries no logic
        /// tree, so its builder hands us the terrain it would have built.
        /// </summary>
        public static Texture2D BakeFromTerrain(ITidalTerrain terrain, Vector2 worldMin,
                                                Vector2 worldSize, int resolution, string baseName)
        {
            if (terrain == null)
            {
                Debug.LogError("[SeabedBakeTool] No ITidalTerrain to bake from. Open the region's " +
                               "scene, or hand this overload the builder's terrain.");
                return null;
            }
            int res = Mathf.Clamp(resolution, 16, 2048);
            Color32[] pixels = SeabedBake.Build(res, res, worldMin, worldSize,
                                                w => SeabedPalette.Sample(terrain.ElevationAt(w)),
                                                new Color(0f, 0f, 0f, 0f));
            Texture2D baked = WritePng(pixels, res, baseName);
            Debug.Log($"[SeabedBakeTool] Baked {res}x{res} _SeabedTex (TerrainElevation) over min {worldMin} " +
                      $"size {worldSize} -> {AssetDatabase.GetAssetPath(baked)}");
            return baked;
        }

        // ============================ THE BAKE ============================

        private void Bake()
        {
            int res = Mathf.Clamp(_resolution, 16, 2048);
            var readable = new Dictionary<Texture2D, Texture2D>();
            ITidalTerrain terrain = _source == Source.TerrainElevation ? FindTerrain() : null;
            if (_source == Source.TerrainElevation && terrain == null) return;
            if (_source == Source.GroundTilemap && _groundTilemap == null) return;

            try
            {
                EditorUtility.DisplayProgressBar("Bake seabed",
                    _source == Source.TerrainElevation
                        ? "Sampling the authored elevation…" : "Sampling the ground tilemap…", 0.1f);
                System.Func<Vector2, Color> sampler = _source == Source.TerrainElevation
                    ? (w => SeabedPalette.Sample(terrain.ElevationAt(w)))
                    : (System.Func<Vector2, Color>)(w => SampleTilemap(w, readable));
                Color32[] pixels = SeabedBake.Build(res, res, _worldMin, _worldSize, sampler, _fill);

                EditorUtility.DisplayProgressBar("Bake seabed", "Writing the PNG…", 0.8f);
                _lastBake = WritePng(pixels, res, _baseName);
            }
            finally
            {
                foreach (var copy in readable.Values)
                    if (copy != null) DestroyImmediate(copy);
                EditorUtility.ClearProgressBar();
            }

            if (_lastBake != null)
            {
                EditorGUIUtility.PingObject(_lastBake);
                Debug.Log($"[SeabedBakeTool] Baked {res}×{res} _SeabedTex ({_source}) over " +
                          $"min {_worldMin} size {_worldSize} → " +
                          AssetDatabase.GetAssetPath(_lastBake) +
                          ". Assign it + tick Use Seabed Texture on the water material, then dial " +
                          "Turbidity above 0 (it is 0 = today's look until you do).");
            }
        }

        /// <summary>
        /// The bottom's albedo at a world position: the ground tile's sprite pixel under that point,
        /// or transparent where no tile is painted. The tile sprite is assumed to fill its cell
        /// (true for the Terrain Paint Tool's type tiles), so the cell-local fraction maps straight
        /// onto the sprite's texture rect.
        /// </summary>
        private Color SampleTilemap(Vector2 world, Dictionary<Texture2D, Texture2D> readable)
        {
            var cell = _groundTilemap.WorldToCell(new Vector3(world.x, world.y, 0f));
            Sprite sprite = _groundTilemap.GetSprite(cell);
            if (sprite == null || sprite.texture == null) return new Color(0f, 0f, 0f, 0f);

            Vector3 centre = _groundTilemap.GetCellCenterWorld(cell);
            Grid grid = _groundTilemap.layoutGrid;
            Vector3 size = grid != null ? grid.cellSize : new Vector3(1f, 1f, 0f);
            float sx = Mathf.Max(size.x, 1e-3f);
            float sy = Mathf.Max(size.y, 1e-3f);
            float u = Mathf.Clamp01((world.x - (centre.x - sx * 0.5f)) / sx);
            float v = Mathf.Clamp01((world.y - (centre.y - sy * 0.5f)) / sy);

            Texture2D src = GetReadable(sprite.texture, readable);
            if (src == null) return new Color(0f, 0f, 0f, 0f);

            Rect tr = sprite.textureRect;
            int px = Mathf.Clamp(Mathf.FloorToInt(tr.x + u * tr.width), 0, src.width - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(tr.y + v * tr.height), 0, src.height - 1);
            Color c = src.GetPixel(px, py);
            // A transparent tile pixel is NOT a bottom — keep coverage honest so cut-out tiles
            // don't bake black holes into the seabed.
            return c.a <= 0.01f ? new Color(0f, 0f, 0f, 0f) : new Color(c.r, c.g, c.b, 1f);
        }

        /// <summary>
        /// A CPU-readable copy of a sprite atlas, obtained by a GPU readback so import settings are
        /// never mutated. Cached per source texture for the duration of one bake, and destroyed
        /// after (a 512² bake touches each atlas once, then reads it a quarter-million times).
        /// </summary>
        private static Texture2D GetReadable(Texture2D src, Dictionary<Texture2D, Texture2D> cache)
        {
            if (src == null) return null;
            if (cache.TryGetValue(src, out var hit)) return hit;

            RenderTexture prev = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0f, 0f, src.width, src.height), 0, 0);
            copy.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            cache[src] = copy;
            return copy;
        }

        /// <summary>
        /// Write the bake as an EXTERNAL, LFS-friendly PNG next to the painted height map (the
        /// <c>_HeightTex</c> convention this mirrors) and import it as a POINT-filtered, clamped,
        /// mip-free, uncompressed COLOUR texture — sRGB ON, unlike the height map, because these
        /// bytes really are colour.
        /// </summary>
        private static Texture2D WritePng(Color32[] pixels, int res, string baseName)
        {
            if (!AssetDatabase.IsValidFolder(DataDir))
            {
                string parent = Path.GetDirectoryName(DataDir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(DataDir));
            }
            string pngPath = DataDir + "/" + baseName + "_SeabedTex.png";

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            DestroyImmediate(tex);

            File.WriteAllBytes(pngPath, png);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSeabedImporter(pngPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        private static void ConfigureSeabedImporter(string pngPath)
        {
            if (AssetImporter.GetAtPath(pngPath) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;            // colour, unlike the height map's linear metres
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;   // alpha is COVERAGE, never blended as opacity
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point; // pixel art: no smearing between bottom cells
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private void AssignToMaterial()
        {
            if (_targetMaterial == null || _lastBake == null) return;
            Undo.RecordObject(_targetMaterial, "Assign seabed texture");
            _targetMaterial.SetTexture("_SeabedTex", _lastBake);
            if (_targetMaterial.HasProperty("_UseSeabedTex"))
                _targetMaterial.SetFloat("_UseSeabedTex", 1f);
            _targetMaterial.EnableKeyword("_USE_SEABEDTEX");
            EditorUtility.SetDirty(_targetMaterial);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SeabedBakeTool] Assigned {_lastBake.name} to {_targetMaterial.name}. " +
                      "The bottom stays invisible until Turbidity (sigma) is above 0.");
        }
    }
}
