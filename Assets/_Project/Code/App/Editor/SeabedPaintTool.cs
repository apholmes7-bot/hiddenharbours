#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Art;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The <b>Seabed Paint Tool</b> (ADR 0014) — a Scene-view brush that lets the owner DESIGN a coast by
    /// PAINTING elevation onto a <see cref="PaintedHeightMap"/>, and SEE it while editing. What he paints
    /// becomes BOTH the water render's depth source AND the tide sim's elevation source (the one-height-map
    /// / three-consumers invariant, ADR 0009/0010/0012) — painted == sailed/walked (P1).
    ///
    /// <para><b>What it does.</b> Open <c>Hidden Harbours ▸ Tools ▸ Seabed Paint Tool</c>. Create or assign
    /// a <see cref="PaintedHeightMap"/>, paint over the scene with Raise / Lower / Set-height / Smooth
    /// brushes (tunable radius / strength / target height — rule 6), or one-click STAMP the canon St Peters
    /// zones (Land / Sandbar / Channel / Deep at the heights in <see cref="StPetersBuilder"/>). Each dab
    /// writes the map's texture and rebuilds its decoded field; because <see cref="WaterSurface"/> is
    /// <c>[ExecuteAlways]</c> with an edit-mode tide preview, the Scene view updates LIVE (colour-by-depth
    /// via the real water shader, scrub the preview tide on the WaterSurface to watch the bar bare/flood).</para>
    ///
    /// <para><b>Seed from today's coast.</b> "Export analytic St Peters → painted map" samples the shipped
    /// <see cref="TidalTerrain.ElevationAtZones"/> (the St Peters constants) across the bake rect into a new
    /// painted map, so the owner paints FROM the existing coast — not a blank canvas. It does NOT change the
    /// shipped St Peters look; adopting the painted map is the explicit "Adopt on the open scene" step.</para>
    ///
    /// <para><b>Lane &amp; seams.</b> tools-editor; writes ONLY the painted-height DATA asset (never the
    /// analytic terrain or scene LOGIC), so it composes with the ADR-0011 single-author rule. Talks to the
    /// sim/render through Core/World/Art types, not across feature internals (rule 4). The painted map is
    /// authored data read at runtime; nothing new is saved (rule 5).</para>
    /// </summary>
    public sealed class SeabedPaintTool : EditorWindow
    {
        private const string DataDir = "Assets/_Project/Data/Terrain";

        private enum Brush { Raise, Lower, SetHeight, Smooth }

        [SerializeField] private PaintedHeightMap _map;
        [SerializeField] private Brush _brush = Brush.Raise;
        [SerializeField] private float _radius = 4f;        // world-metre brush radius (tunable — rule 6)
        [SerializeField] private float _strength = 1.0f;    // metres of change per stroke-step at the centre
        [SerializeField] private float _targetHeight = 1.6f;// the height SetHeight paints / the stamp height
        [SerializeField] private bool _painting = true;     // whether scene clicks paint (off = normal select)

        private bool _strokeActive;
        private Texture2D _tex;       // cached working texture (the map's), kept readable
        private Color[] _pixels;      // working CPU pixel buffer (R = normalized elevation)

        [MenuItem("Hidden Harbours/Tools/Seabed Paint Tool", priority = 40)]
        public static void Open()
        {
            var w = GetWindow<SeabedPaintTool>("Seabed Paint");
            w.minSize = new Vector2(320f, 420f);
            w.Show();
        }

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGui;
        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGui;

        // ============================ WINDOW UI ============================

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Painted Seabed Heights (ADR 0014)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Paint a coast by raising/lowering ELEVATION. What you paint is BOTH what the water shows AND " +
                "what the tide bares/floods. The Scene view updates live (the water shader reads the same map). " +
                "Scrub the WaterSurface's Preview Tide Level to see any tide without pressing Play.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _map = (PaintedHeightMap)EditorGUILayout.ObjectField("Height Map", _map, typeof(PaintedHeightMap), false);
            if (EditorGUI.EndChangeCheck()) CacheTexture();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Blank Map…")) CreateBlankMap();
                if (GUILayout.Button("Export analytic St Peters → painted map")) ExportStPeters();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_map == null))
            {
                _painting = EditorGUILayout.ToggleLeft("Paint in Scene view (hold mouse over the water plane)", _painting);
                _brush = (Brush)EditorGUILayout.EnumPopup("Brush", _brush);
                _radius = EditorGUILayout.Slider("Brush radius (m)", _radius, 0.5f, 40f);
                _strength = EditorGUILayout.Slider("Strength (m/step)", _strength, 0.05f, 5f);
                _targetHeight = EditorGUILayout.Slider("Target / stamp height (m)", _targetHeight,
                    _map != null ? _map.MinElevation : -4f, _map != null ? _map.MaxElevation : 6f);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Quick zone stamps (canon St Peters heights)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"Land ({StPetersBuilder.IslandElevation:0.#} m)"))
                        SetStampHeight(StPetersBuilder.IslandElevation);
                    if (GUILayout.Button($"Sandbar ({StPetersBuilder.SandbarCrestElevation:0.#} m)"))
                        SetStampHeight(StPetersBuilder.SandbarCrestElevation);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"Channel ({StPetersBuilder.ChannelBedElevation:0.#} m)"))
                        SetStampHeight(StPetersBuilder.ChannelBedElevation);
                    if (GUILayout.Button($"Deep ({StPetersBuilder.DeepHarbourElevation:0.#} m)"))
                        SetStampHeight(StPetersBuilder.DeepHarbourElevation);
                }
                EditorGUILayout.HelpBox("A stamp sets the brush to SetHeight at that elevation — then paint to " +
                                        "block out that zone. Or use the brushes directly.", MessageType.None);

                EditorGUILayout.Space();
                if (GUILayout.Button("Adopt this map on the OPEN scene (swap terrain + water source)"))
                    AdoptOnOpenScene();
            }

            if (_map != null && _map.HeightTexture == null)
                EditorGUILayout.HelpBox("This map has no texture — create a blank map or export St Peters.",
                    MessageType.Warning);
        }

        private void SetStampHeight(float h)
        {
            _brush = Brush.SetHeight;
            _targetHeight = h;
            Repaint();
        }

        // ============================ SCENE-VIEW BRUSH ============================

        private void OnSceneGui(SceneView view)
        {
            if (!_painting || _map == null || _tex == null) return;

            Event e = Event.current;
            Vector2 world = WorldUnderMouse(e);

            // Draw the brush footprint so the owner sees where it'll land.
            Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Handles.DrawWireDisc(new Vector3(world.x, world.y, 0f), Vector3.forward, _radius);

            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            bool leftButton = e.button == 0 && !e.alt;
            if (leftButton && e.type == EventType.MouseDown)
            {
                _strokeActive = true;
                BeginStrokeUndo();
                Dab(world);
                e.Use();
            }
            else if (leftButton && e.type == EventType.MouseDrag && _strokeActive)
            {
                Dab(world);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _strokeActive)
            {
                _strokeActive = false;
                CommitTexture();
                e.Use();
            }

            view.Repaint();
        }

        /// <summary>The world XY under the mouse, projected onto the z=0 plane (the 2D water plane).</summary>
        private static Vector2 WorldUnderMouse(Event e)
        {
            Ray r = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            // Intersect with z=0 (our 2D world); fall back to the ray origin if parallel.
            float t = Mathf.Abs(r.direction.z) > 1e-5f ? -r.origin.z / r.direction.z : 0f;
            Vector3 p = r.origin + r.direction * t;
            return new Vector2(p.x, p.y);
        }

        /// <summary>Apply one brush dab centred at a world position, writing the working pixel buffer.</summary>
        private void Dab(Vector2 worldCenter)
        {
            if (_pixels == null || _tex == null) return;
            int w = _tex.width, h = _tex.height;
            Vector2 size = _map.WorldSize;
            Vector2 min = _map.WorldCenter - size * 0.5f;
            float min0 = _map.MinElevation, max0 = _map.MaxElevation;

            // Brush radius in texels (per-axis, so a non-square map paints a true circle in world space).
            float metresPerTexelX = size.x / w;
            float metresPerTexelY = size.y / h;

            // The texel box the brush touches.
            int cx = Mathf.RoundToInt((worldCenter.x - min.x) / size.x * w - 0.5f);
            int cy = Mathf.RoundToInt((worldCenter.y - min.y) / size.y * h - 0.5f);
            int rxTexels = Mathf.CeilToInt(_radius / Mathf.Max(metresPerTexelX, 1e-4f));
            int ryTexels = Mathf.CeilToInt(_radius / Mathf.Max(metresPerTexelY, 1e-4f));

            for (int y = cy - ryTexels; y <= cy + ryTexels; y++)
            for (int x = cx - rxTexels; x <= cx + rxTexels; x++)
            {
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                // World distance from the brush centre (falloff in world metres → round footprint).
                float wx = min.x + (x + 0.5f) / w * size.x;
                float wy = min.y + (y + 0.5f) / h * size.y;
                float dist = Vector2.Distance(new Vector2(wx, wy), worldCenter);
                if (dist > _radius) continue;
                float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dist / Mathf.Max(_radius, 1e-3f))); // 1 centre → 0 edge

                int idx = y * w + x;
                float elev = PaintedHeightField.DecodeElevation(_pixels[idx].r, min0, max0);
                float next = ApplyBrush(elev, fall, idx, w, h, min0, max0);
                float r01 = PaintedHeightField.EncodeElevation(next, min0, max0);
                _pixels[idx] = new Color(r01, r01, r01, 1f);
            }

            // Push to the working texture + rebuild the decoded field so the live preview updates this frame.
            _tex.SetPixels(_pixels);
            _tex.Apply(false, false);
            _map.Rebuild();
            RefreshOpenWaterSurfaces();
        }

        private float ApplyBrush(float elev, float fall, int idx, int w, int h, float min0, float max0)
        {
            switch (_brush)
            {
                case Brush.Raise:  return elev + _strength * fall;
                case Brush.Lower:  return elev - _strength * fall;
                case Brush.SetHeight: return Mathf.Lerp(elev, _targetHeight, fall);
                case Brush.Smooth:
                {
                    // Average the 4-neighbourhood (clamped), ease toward it by the falloff*strength fraction.
                    int x = idx % w, y = idx / w;
                    float sum = elev, n = 1f;
                    void Acc(int xx, int yy)
                    {
                        if (xx < 0 || xx >= w || yy < 0 || yy >= h) return;
                        sum += PaintedHeightField.DecodeElevation(_pixels[yy * w + xx].r, min0, max0); n += 1f;
                    }
                    Acc(x - 1, y); Acc(x + 1, y); Acc(x, y - 1); Acc(x, y + 1);
                    float avg = sum / n;
                    return Mathf.Lerp(elev, avg, Mathf.Clamp01(fall * _strength));
                }
                default: return elev;
            }
        }

        // ============================ ASSET / TEXTURE PLUMBING ============================

        private void CacheTexture()
        {
            _tex = _map != null ? _map.HeightTexture : null;
            _pixels = (_tex != null && _tex.isReadable) ? _tex.GetPixels() : null;
            if (_tex != null && !_tex.isReadable)
                Debug.LogWarning("[SeabedPaintTool] The map's texture is not readable — re-create it via " +
                                 "'New Blank Map' or the St Peters export so the brush + sim can read it.");
        }

        private void BeginStrokeUndo()
        {
            // Re-snapshot the pixels at stroke start so a stroke is one undo step (the texture is an external
            // .png asset; we keep the buffer authoritative in-memory and re-encode the PNG on mouse-up).
            if (_pixels == null) CacheTexture();
        }

        /// <summary>
        /// Persist the stroke (F1): the height texture is now an EXTERNAL <c>.png</c>, so SetDirty/SaveAssets
        /// alone won't write the painted pixels back to disk — re-encode the working buffer to the source PNG
        /// and re-import so the file (and the sim's next decode) reflects the brush. Mark the map dirty too.
        /// </summary>
        private void CommitTexture()
        {
            if (_tex == null || _pixels == null) return;
            string pngPath = AssetDatabase.GetAssetPath(_tex);
            if (!string.IsNullOrEmpty(pngPath) && pngPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                var enc = new Texture2D(_tex.width, _tex.height, TextureFormat.R8, false, true);
                enc.SetPixels(_pixels);
                enc.Apply(false, false);
                File.WriteAllBytes(pngPath, enc.EncodeToPNG());
                Object.DestroyImmediate(enc);
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureHeightTextureImporter(pngPath);   // keep isReadable/linear after the re-import
                _tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            }
            else
            {
                // Defensive fallback for an unexpected non-PNG (e.g. a hand-wired sub-asset texture).
                EditorUtility.SetDirty(_tex);
            }
            if (_map != null) { _map.Rebuild(); EditorUtility.SetDirty(_map); }
            AssetDatabase.SaveAssets();
        }

        private void CreateBlankMap()
        {
            const int res = 192;   // matches the ADR-0012 bake resolution
            var map = CreatePaintedMapAsset("PaintedSeabed", res, new Vector2(0f, 0f), new Vector2(160f, 120f),
                                            -4f, 6f, fillElevation: -4f);
            if (map != null) { _map = map; CacheTexture(); }
        }

        /// <summary>
        /// Seed a painted map from TODAY'S analytic St Peters coast: sample the shipped
        /// <see cref="TidalTerrain.ElevationAtZones"/> (configured with the St Peters constants) across the
        /// bake rect. The owner then paints FROM the existing coast. Does NOT touch the shipped scene.
        /// </summary>
        private void ExportStPeters()
        {
            const int res = 192;
            Vector2 center = new Vector2(0f, 0f);
            Vector2 worldSize = new Vector2(160f, 120f);
            const float min0 = -4f, max0 = 6f;

            // (F1) Overwrite the committed seed in place rather than minting a "StPetersSeabed 1.asset"
            // duplicate that would orphan the committed PNG. Confirm before clobbering existing edits.
            string existing = DataDir + "/StPetersSeabed.asset";
            if (AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(existing) != null &&
                !EditorUtility.DisplayDialog("Export analytic St Peters",
                    "StPetersSeabed already exists. Overwrite it (and its height PNG) with a fresh export " +
                    "from the analytic coast? Any painting you did on it will be replaced.",
                    "Overwrite", "Cancel"))
                return;

            // Build a transient TidalTerrain configured with the canon St Peters zones (single source of
            // truth — the same constants the builder mirrors), sample it per texel, then discard it.
            var go = EditorUtility.CreateGameObjectWithHideFlags("~SeabedExport", HideFlags.HideAndDontSave,
                                                                 typeof(TidalTerrain));
            var terrain = go.GetComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);

            Vector2 min = center - worldSize * 0.5f;
            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = min.x + (x + 0.5f) / res * worldSize.x;
                float wy = min.y + (y + 0.5f) / res * worldSize.y;
                float elev = terrain.ElevationAtZones(new Vector2(wx, wy));
                float r01 = PaintedHeightField.EncodeElevation(elev, min0, max0);
                pixels[y * res + x] = new Color(r01, r01, r01, 1f);
            }
            Object.DestroyImmediate(go);

            var map = CreatePaintedMapAsset("StPetersSeabed", res, center, worldSize, min0, max0,
                                            pixels: pixels, overwrite: true);
            if (map != null)
            {
                _map = map; CacheTexture();
                Debug.Log("[SeabedPaintTool] Exported analytic St Peters → " +
                          AssetDatabase.GetAssetPath(map) + ". Paint FROM this coast, then 'Adopt this map on " +
                          "the OPEN scene' (open StPeters.unity first) to make the sim + water read it.");
                EditorGUIUtility.PingObject(map);
            }
        }

        /// <summary>
        /// Create (or OVERWRITE in place) a <see cref="PaintedHeightMap"/> at <c>DataDir/baseName.asset</c>
        /// backed by an EXTERNAL, LFS-friendly, smart-mergeable <c>baseName_HeightTex.png</c> written NEXT TO
        /// it (F1). The PNG is imported CPU-readable + linear + single-R-usable so the sim can decode it; the
        /// <c>.asset</c> references it by GUID (NOT an embedded <c>AddObjectToAsset</c> sub-object — that
        /// produced a self-contained file that drifted from the committed external-PNG seed and orphaned the
        /// PNG). When <paramref name="overwrite"/> is true an existing same-named map is updated in place
        /// (no <c>StPetersSeabed 1.asset</c> duplicate); otherwise a unique path is minted (blank maps).
        /// </summary>
        private static PaintedHeightMap CreatePaintedMapAsset(string baseName, int res, Vector2 center,
            Vector2 worldSize, float minElev, float maxElev, float fillElevation = 0f, Color[] pixels = null,
            bool overwrite = false)
        {
            if (!AssetDatabase.IsValidFolder(DataDir))
            {
                string parent = Path.GetDirectoryName(DataDir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(DataDir));
            }

            // Resolve the .asset + .png paths. Overwrite reuses the existing names (update in place);
            // otherwise mint unique sibling names so the .asset and its .png stay paired.
            string assetPath = DataDir + "/" + baseName + ".asset";
            string pngPath   = DataDir + "/" + baseName + "_HeightTex.png";
            if (!overwrite)
            {
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                string uniqueBase = Path.GetFileNameWithoutExtension(assetPath);
                pngPath = DataDir + "/" + uniqueBase + "_HeightTex.png";
            }

            // Build the R8 pixel buffer (R = normalized elevation; G=B=R so 8-bit grayscale PNG round-trips).
            var tex = new Texture2D(res, res, TextureFormat.R8, false, true);
            if (pixels == null)
            {
                float r01 = PaintedHeightField.EncodeElevation(fillElevation, minElev, maxElev);
                var c = new Color(r01, r01, r01, 1f);
                var fill = new Color[res * res];
                for (int i = 0; i < fill.Length; i++) fill[i] = c;
                tex.SetPixels(fill);
            }
            else tex.SetPixels(pixels);
            tex.Apply(false, false);

            // Write the PNG to disk (external — LFS-friendly, smart-mergeable .asset alongside) and import it
            // with the data-texture settings the sim needs: CPU-readable, LINEAR (elevation is data, not
            // colour), single-channel R usable, point-able wrap=Clamp. Then re-import so isReadable sticks.
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(pngPath, png);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureHeightTextureImporter(pngPath);

            var importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

            // Create or update the map .asset, pointing _heightTexture at the EXTERNAL png.
            var map = overwrite ? AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(assetPath) : null;
            bool created = map == null;
            if (created) map = ScriptableObject.CreateInstance<PaintedHeightMap>();

            var so = new SerializedObject(map);
            so.FindProperty("_heightTexture").objectReferenceValue = importedTex;
            so.FindProperty("_worldCenter").vector2Value = center;
            so.FindProperty("_worldSize").vector2Value = worldSize;
            so.FindProperty("_minElevation").floatValue = minElev;
            so.FindProperty("_maxElevation").floatValue = maxElev;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (created) AssetDatabase.CreateAsset(map, assetPath);
            else EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(assetPath);
        }

        /// <summary>
        /// Import a painted-height PNG as a DATA texture the sim can decode (F1): CPU-readable
        /// (<c>isReadable</c>), LINEAR (no sRGB gamma — the R channel is metres-of-elevation, not colour),
        /// no mipmaps, Clamp wrap, and the single-channel R kept usable. Matches the committed
        /// <c>StPetersSeabed_HeightTex.png</c> import so the seed and freshly-exported maps decode identically.
        /// </summary>
        private static void ConfigureHeightTextureImporter(string pngPath)
        {
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;          // linear — elevation is data, not colour
            importer.isReadable = true;            // the sim MUST be able to GetPixels()
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // keep the R byte exact
            importer.SaveAndReimport();
        }

        // ============================ LIVE PREVIEW / ADOPTION ============================

        /// <summary>Re-feed the painted map to every WaterSurface in the open scene so the brush is live.</summary>
        private void RefreshOpenWaterSurfaces()
        {
            if (_map == null || _map.HeightTexture == null) return;
            foreach (var s in Object.FindObjectsByType<WaterSurface>(FindObjectsSortMode.None))
                s.ConfigurePaintedHeightMap(_map.HeightTexture, _map.WorldCenter, _map.WorldSize,
                                            _map.MinElevation, _map.MaxElevation);
        }

        /// <summary>
        /// Adopt the painted map on the OPEN scene: point every WaterSurface at it AND swap the scene's
        /// analytic <see cref="TidalTerrain"/> for a <see cref="PaintedTidalTerrain"/> (so the SIM reads the
        /// painted heights too — painted == sailed). The explicit opt-in step (the export only seeds the
        /// canvas). Disables the analytic terrain rather than deleting it, so the swap is reversible.
        /// </summary>
        private void AdoptOnOpenScene()
        {
            if (_map == null || _map.HeightTexture == null)
            {
                EditorUtility.DisplayDialog("Seabed Paint", "Assign or create a painted height map first.", "OK");
                return;
            }

            // 1) Water render reads it. (F2) Record Undo + SetDirty on each WaterSurface so the render-half
            // swap is undoable in lockstep with the sim half below — Ctrl-Z reverts the WHOLE adoption, not a
            // partial state. (RefreshOpenWaterSurfaces, used by the live brush, deliberately records no Undo.)
            foreach (var s in Object.FindObjectsByType<WaterSurface>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(s, "Adopt painted seabed");
                s.ConfigurePaintedHeightMap(_map.HeightTexture, _map.WorldCenter, _map.WorldSize,
                                            _map.MinElevation, _map.MaxElevation);
                EditorUtility.SetDirty(s);
            }

            // 2) Sim reads it: disable the analytic TidalTerrain (if any) and add a PaintedTidalTerrain.
            var analytic = Object.FindFirstObjectByType<TidalTerrain>();
            GameObject host;
            if (analytic != null)
            {
                Undo.RecordObject(analytic, "Adopt painted seabed");
                analytic.enabled = false;
                host = analytic.gameObject;
            }
            else
            {
                host = new GameObject("PaintedTidalTerrain");
                Undo.RegisterCreatedObjectUndo(host, "Adopt painted seabed");
            }

            var painted = host.GetComponent<PaintedTidalTerrain>();
            if (painted == null) painted = Undo.AddComponent<PaintedTidalTerrain>(host);
            painted.Map = _map;
            var so = new SerializedObject(painted);
            so.FindProperty("_map").objectReferenceValue = _map;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(painted);

            EditorUtility.DisplayDialog("Seabed Paint",
                "Adopted the painted map on the open scene:\n• Every WaterSurface now reads it (render).\n• " +
                "A PaintedTidalTerrain now feeds the sim (walkability / clams / boat-cross).\nThe analytic " +
                "TidalTerrain was DISABLED (not deleted) so this is reversible.\n\nSave the scene to keep it.",
                "Fair winds");
        }
    }
}
#endif
