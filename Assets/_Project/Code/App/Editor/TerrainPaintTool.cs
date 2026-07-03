#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using HiddenHarbours.World;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;   // TileAssetBuilder (tile names/paths), PaintableTilemapMenu (create canvas)
using Object = UnityEngine.Object;  // disambiguate Object (we add `using System` for Serializable/StringComparison)

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The <b>Terrain Paint Tool (height + look)</b> (ADR 0014) — a Scene-view brush that lets the owner
    /// DESIGN a coast by PAINTING a terrain TYPE. ONE stroke sets BOTH the ground LOOK (a tile on the ground
    /// tilemap) AND the HEIGHT (the cells of a <see cref="PaintedHeightMap"/>), and a toggleable edit-mode
    /// HEIGHT COLOUR OVERLAY lets him SEE the elevation he's shaping (a "hidden height map" he can see while
    /// adjusting, that never renders in the game). What he paints becomes BOTH the water render's depth source
    /// AND the tide sim's elevation source (the one-height-map / three-consumers invariant, ADR 0009/0010/0012)
    /// — painted == sailed/walked (P1). The tile he paints is authored VISUAL content (like normal tilemap
    /// painting), not sim.
    ///
    /// <para><b>What it does.</b> Open <c>Hidden Harbours ▸ Tools ▸ Terrain Paint Tool (height + look)</c>.
    /// Create or assign a <see cref="PaintedHeightMap"/>, pick a terrain TYPE (Deep / Channel / Beach /
    /// Sandbar / Grass / Cliff — a tunable list, rule 6) and paint: each dab (a) sets the height-map cells to
    /// the type's elevation AND (b) stamps the type's tile onto the ground tilemap (underwater types CLEAR the
    /// tile so the water shows). The height brushes (Raise / Lower / Set-height / Smooth) remain for
    /// fine-tuning. Toggle the HEIGHT COLOUR OVERLAY to see the elevation false-coloured in the Scene view
    /// (deep blue → sand → green → rock), with a legend + the current preview-tide waterline. Because
    /// <see cref="WaterSurface"/> is <c>[ExecuteAlways]</c> with an edit-mode tide preview, the water + coast
    /// also update LIVE.</para>
    ///
    /// <para><b>Seed from today's coast.</b> "Export analytic St Peters → painted map" samples the shipped
    /// <see cref="TidalTerrain.ElevationAtZones"/> (the St Peters constants) across the bake rect into a new
    /// painted map, so the owner paints FROM the existing coast — not a blank canvas. It does NOT change the
    /// shipped St Peters look; adopting the painted map is the explicit "Adopt on the open scene" step.</para>
    ///
    /// <para><b>Lane &amp; seams.</b> tools-editor; writes the painted-height DATA asset (the height side, the
    /// single source of truth for water + tide — UNCHANGED here) and stamps tiles on the scene's ground
    /// tilemap (authored visual content), never the analytic terrain or scene LOGIC, so it composes with the
    /// ADR-0011 single-author rule. Talks to the sim/render through Core/World/Art types (rule 4). The
    /// overlay is EDIT-MODE-ONLY (Handles/GL draw in the Scene view; it never serializes and never renders in
    /// Play or a build). The painted map is authored data read at runtime; nothing new is saved (rule 5).</para>
    /// </summary>
    public sealed class TerrainPaintTool : EditorWindow
    {
        private const string DataDir = "Assets/_Project/Data/Terrain";

        private enum Brush { TerrainType, Raise, Lower, SetHeight, Smooth }

        /// <summary>
        /// A TUNABLE terrain TYPE (rule 6 — no magic numbers; the owner edits the list in the tool): a name, the
        /// ground tile its stroke stamps (null = height-only / clears any tile so water shows), and the
        /// elevation (m above datum) its stroke sets. <see cref="clearTile"/> forces the underwater "show the
        /// water" behaviour even if a tile is somehow assigned.
        /// </summary>
        [Serializable]
        private sealed class TerrainTypePreset
        {
            public string name = "Type";
            [Tooltip("The ground tile this type paints. Empty = height-only (no land tile; clears any tile so " +
                     "the water shows — for Deep / Channel).")]
            public TileBase groundTile;
            [Tooltip("Elevation (m above datum) this type sets under the brush. The HEIGHT side — drives both " +
                     "the water render and the tide sim (painted == sailed).")]
            public float elevation;
            [Tooltip("Force-clear any land tile at the painted cells (underwater types). On for Deep / Channel.")]
            public bool clearTile;
        }

        [SerializeField] private PaintedHeightMap _map;
        [SerializeField] private Tilemap _groundTilemap;          // the LOOK target (auto-found / create-on-demand)
        [SerializeField] private Brush _brush = Brush.TerrainType;
        [SerializeField] private int _typeIndex;                  // selected terrain TYPE in the preset list
        [SerializeField] private List<TerrainTypePreset> _presets;
        [SerializeField] private float _radius = 4f;        // world-metre brush radius (tunable — rule 6)
        [SerializeField] private float _strength = 1.0f;    // metres of change per stroke-step at the centre
        [SerializeField] private float _targetHeight = 1.6f;// the height SetHeight paints
        [SerializeField] private bool _painting = true;     // whether scene clicks paint (off = normal select)
        [SerializeField] private bool _showOverlay;         // the edit-mode height colour overlay (off by default)
        [SerializeField] private bool _showPresetEditor;    // fold-out for editing the preset list

        private bool _strokeActive;
        private Texture2D _tex;       // cached working texture (the map's), kept readable
        private Color[] _pixels;      // working CPU pixel buffer (R = normalized elevation)
        private Vector2 _presetScroll;

        // ---- the height overlay's draw resources (edit-mode only; never serialized, never shipped) ----------
        private Texture2D _overlayTex;     // a small false-colour texture drawn over the painted rect
        private Material _overlayMat;       // an unlit material to blit the overlay tex onto a GL quad
        private bool _overlayDirty = true;  // rebuild the overlay tex when the field changes / first show

        [MenuItem("Hidden Harbours/Tools/Terrain Paint Tool (height + look)", priority = 40)]
        public static void Open()
        {
            var w = GetWindow<TerrainPaintTool>("Terrain Paint");
            w.minSize = new Vector2(340f, 560f);
            w.EnsurePresets();
            w.Show();
        }

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGui;

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            DestroyOverlayResources();
        }

        // ============================ TERRAIN TYPE PRESETS (tunable — rule 6) ============================

        /// <summary>
        /// The DEFAULT terrain types (owner-editable in the tool). Names → closest generated terrain tile via
        /// <see cref="ResolveDefaultTileName"/> (height-only when no suitable tile exists). Elevations match the
        /// canon St Peters zones where they correspond. This is the tunable set the owner can extend / retune.
        /// </summary>
        private static List<TerrainTypePreset> DefaultPresets() => new()
        {
            new TerrainTypePreset { name = "Deep",    elevation = StPetersBuilder.DeepHarbourElevation,   clearTile = true },   // -4, no tile
            new TerrainTypePreset { name = "Channel", elevation = StPetersBuilder.ChannelBedElevation,    clearTile = true },   // -0.6, no tile
            new TerrainTypePreset { name = "Beach",   elevation = 0.3f },                                                       // sand
            new TerrainTypePreset { name = "Sandbar", elevation = StPetersBuilder.SandbarCrestElevation },                      // 1.6, wet-sand-ish
            new TerrainTypePreset { name = "Grass",   elevation = StPetersBuilder.IslandElevation },                            // 6, grass
            new TerrainTypePreset { name = "Cliff",   elevation = 8f },                                                         // rock
        };

        /// <summary>
        /// PURE mapping: a default terrain-type name → the closest GENERATED terrain tile name (the
        /// <see cref="TileAssetBuilder.Terrain"/> set: Sand / Rock / Grass / Dirt / WharfDeck / Foam / Water),
        /// or null for the height-only underwater types. Foam is the nearest "wet sand" the tile set has; if a
        /// better tile is added later, retune here (or just reassign the preset's tile in the tool).
        /// Headless-testable (no Unity asset lookup) — the resolution rule, not the asset binding.
        /// </summary>
        public static string ResolveDefaultTileName(string typeName)
        {
            switch (typeName)
            {
                case "Deep":    return null;     // height-only — water shows
                case "Channel": return null;     // height-only — water shows
                case "Beach":   return "Sand";
                case "Sandbar": return "Foam";   // closest "wet sand" in the generated set
                case "Grass":   return "Grass";
                case "Cliff":   return "Rock";
                default:        return null;     // unknown / custom type → height-only unless the owner assigns a tile
            }
        }

        /// <summary>
        /// PURE: is this preset a HEIGHT-ONLY (underwater) type — no land tile, and the stroke clears any tile so
        /// the water shows? True when it has no ground tile OR is flagged <c>clearTile</c>. Testable headless.
        /// </summary>
        public static bool IsHeightOnly(bool hasGroundTile, bool clearTile) => clearTile || !hasGroundTile;

        private void EnsurePresets()
        {
            if (_presets == null || _presets.Count == 0)
            {
                _presets = DefaultPresets();
                ResolveDefaultPresetTiles();
            }
            _typeIndex = Mathf.Clamp(_typeIndex, 0, Mathf.Max(0, _presets.Count - 1));
        }

        /// <summary>
        /// Bind each default preset to its resolved generated tile asset (if present). Leaves a preset's tile
        /// EMPTY (height-only) when the tile doesn't exist yet — the UI then surfaces the "run Build
        /// Scene-Painting Toolkit" hint rather than failing.
        /// </summary>
        private void ResolveDefaultPresetTiles()
        {
            foreach (var p in _presets)
            {
                if (p.clearTile) { p.groundTile = null; continue; }   // underwater: never a tile
                if (p.groundTile != null) continue;                    // owner already assigned one — keep it
                string tileName = ResolveDefaultTileName(p.name);
                if (string.IsNullOrEmpty(tileName)) continue;
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(TileAssetBuilder.TilePath(tileName));
                if (tile != null) p.groundTile = tile;
            }
        }

        /// <summary>True if any non-underwater preset is missing its tile (so the "Build toolkit" hint shows).</summary>
        private bool AnyPresetMissingTile()
        {
            if (_presets == null) return false;
            foreach (var p in _presets)
                if (!p.clearTile && p.groundTile == null && !string.IsNullOrEmpty(ResolveDefaultTileName(p.name)))
                    return true;
            return false;
        }

        // ============================ WINDOW UI ============================

        private void OnGUI()
        {
            EnsurePresets();

            EditorGUILayout.LabelField("Terrain Paint Tool — height + look (ADR 0014)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Paint a terrain TYPE: ONE stroke sets the ground LOOK (a tile) AND the HEIGHT. What you paint " +
                "is BOTH what the water shows AND what the tide bares/floods. Toggle the HEIGHT OVERLAY to SEE " +
                "the elevation while you shape it (it never renders in the game). The Scene view updates live; " +
                "scrub the WaterSurface's Preview Tide Level to see any tide without pressing Play.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _map = (PaintedHeightMap)EditorGUILayout.ObjectField("Height Map", _map, typeof(PaintedHeightMap), false);
            if (EditorGUI.EndChangeCheck()) { CacheTexture(); _overlayDirty = true; }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Blank Map…")) CreateBlankMap();
                if (GUILayout.Button("Export analytic St Peters → painted map")) ExportStPeters();
            }

            DrawGroundTilemapRow();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_map == null))
            {
                _painting = EditorGUILayout.ToggleLeft("Paint in Scene view (hold mouse over the water plane)", _painting);
                _brush = (Brush)EditorGUILayout.EnumPopup("Brush", _brush);

                if (_brush == Brush.TerrainType) DrawTerrainTypePicker();

                _radius = EditorGUILayout.Slider("Brush radius (m)", _radius, 0.5f, 40f);
                if (_brush == Brush.Raise || _brush == Brush.Lower || _brush == Brush.Smooth)
                    _strength = EditorGUILayout.Slider("Strength (m/step)", _strength, 0.05f, 5f);
                if (_brush == Brush.SetHeight)
                    _targetHeight = EditorGUILayout.Slider("Target height (m)", _targetHeight,
                        _map != null ? _map.MinElevation : -4f, _map != null ? _map.MaxElevation : 6f);

                DrawOverlaySection();

                DrawPresetEditor();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Quick height stamps (canon St Peters heights)", EditorStyles.boldLabel);
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
                EditorGUILayout.HelpBox("These set the SetHeight brush to that elevation (height only, no tile). " +
                                        "For look+height in one stroke use a Terrain Type above.", MessageType.None);

                EditorGUILayout.Space();
                if (GUILayout.Button("Adopt this map on the OPEN scene (swap terrain + water source)"))
                    AdoptOnOpenScene();
            }

            if (_map != null && _map.HeightTexture == null)
                EditorGUILayout.HelpBox("This map has no texture — create a blank map or export St Peters.",
                    MessageType.Warning);
        }

        private void DrawGroundTilemapRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _groundTilemap = (Tilemap)EditorGUILayout.ObjectField("Ground Tilemap", _groundTilemap, typeof(Tilemap), true);
                if (GUILayout.Button("Find", GUILayout.Width(48f))) AutoFindGroundTilemap();
            }
            if (_groundTilemap == null)
                EditorGUILayout.HelpBox("No ground tilemap assigned. Click Find to auto-locate the scene's " +
                    "TerrainTilemap, or 'Add Ground Tilemap' to create one.", MessageType.None);
            if (GUILayout.Button("Add Ground Tilemap (if the scene has none)"))
                CreateGroundTilemap();
        }

        private void DrawTerrainTypePicker()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Terrain TYPE (one stroke = look + height)", EditorStyles.boldLabel);
            string[] names = new string[_presets.Count];
            for (int i = 0; i < _presets.Count; i++)
            {
                var p = _presets[i];
                bool heightOnly = IsHeightOnly(p.groundTile != null, p.clearTile);
                names[i] = $"{p.name}  ({p.elevation:0.#} m{(heightOnly ? ", height only" : ", " + p.groundTile.name)})";
            }
            _typeIndex = Mathf.Clamp(_typeIndex, 0, _presets.Count - 1);
            _typeIndex = EditorGUILayout.Popup("Type", _typeIndex, names);

            if (AnyPresetMissingTile())
                EditorGUILayout.HelpBox("Some terrain types have no tile yet. Run Hidden Harbours ▸ Art ▸ " +
                    "Build Scene-Painting Toolkit to generate the terrain tiles, then re-open this tool (or " +
                    "assign tiles in the Terrain Types editor below).", MessageType.Warning);
        }

        private void DrawOverlaySection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Height colour overlay (edit-mode aid — never in game)", EditorStyles.boldLabel);
            bool prev = _showOverlay;
            _showOverlay = EditorGUILayout.ToggleLeft(
                "Show height colour overlay in the Scene view", _showOverlay);
            if (_showOverlay != prev)
            {
                if (_showOverlay) _overlayDirty = true;
                SceneView.RepaintAll();
            }
            if (_showOverlay)
                EditorGUILayout.HelpBox("Deep blue → cyan shallows → sand → green → brown/rock. A designer aid " +
                    "drawn ONLY in the Scene view (it never serializes and never renders in Play or a build). " +
                    "The legend marks the WaterSurface preview-tide waterline.", MessageType.None);
        }

        private void DrawPresetEditor()
        {
            EditorGUILayout.Space();
            _showPresetEditor = EditorGUILayout.Foldout(_showPresetEditor, "Terrain Types (tunable — edit names / tiles / heights)", true);
            if (!_showPresetEditor) return;

            using (var scroll = new EditorGUILayout.ScrollViewScope(_presetScroll, GUILayout.MaxHeight(220f)))
            {
                _presetScroll = scroll.scrollPosition;
                int removeAt = -1;
                for (int i = 0; i < _presets.Count; i++)
                {
                    var p = _presets[i];
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            p.name = EditorGUILayout.TextField("Name", p.name);
                            if (GUILayout.Button("X", GUILayout.Width(24f))) removeAt = i;
                        }
                        p.elevation = EditorGUILayout.FloatField("Elevation (m)", p.elevation);
                        p.clearTile = EditorGUILayout.Toggle("Height only (clear tile)", p.clearTile);
                        using (new EditorGUI.DisabledScope(p.clearTile))
                            p.groundTile = (TileBase)EditorGUILayout.ObjectField("Ground tile", p.groundTile, typeof(TileBase), false);
                    }
                }
                if (removeAt >= 0) _presets.RemoveAt(removeAt);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add type")) _presets.Add(new TerrainTypePreset { name = "New", elevation = 0f });
                if (GUILayout.Button("Reset to defaults"))
                {
                    _presets = DefaultPresets();
                    ResolveDefaultPresetTiles();
                }
                if (GUILayout.Button("Re-bind tiles"))
                    ResolveDefaultPresetTiles();
            }
        }

        private void SetStampHeight(float h)
        {
            _brush = Brush.SetHeight;
            _targetHeight = h;
            Repaint();
        }

        // ============================ GROUND TILEMAP RESOLUTION ============================

        /// <summary>Auto-find the scene's ground tilemap (the one "Add Paintable Tilemap" creates).</summary>
        private void AutoFindGroundTilemap()
        {
            _groundTilemap = FindGroundTilemap();
            if (_groundTilemap == null)
                EditorUtility.DisplayDialog("Terrain Paint",
                    "No tilemap found in the open scene. Click 'Add Ground Tilemap' to create one, or open the " +
                    "scene you mean to paint.", "OK");
            else EditorGUIUtility.PingObject(_groundTilemap);
        }

        /// <summary>
        /// Find the ground tilemap to paint: prefer one named "TerrainTilemap" (what Add Paintable Tilemap
        /// makes), else any Tilemap in the open scene. Returns null if none exists.
        /// </summary>
        private static Tilemap FindGroundTilemap()
        {
            var all = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return null;
            foreach (var tm in all)
                if (tm != null && tm.gameObject.name.StartsWith("TerrainTilemap", StringComparison.Ordinal))
                    return tm;
            return all[0];
        }

        /// <summary>
        /// Create a ground tilemap by reusing the existing "Add Paintable Tilemap" path (so the tool doesn't
        /// fork the canvas creation — rule 4 / DRY), then bind to it.
        /// </summary>
        private void CreateGroundTilemap()
        {
            PaintableTilemapMenu.AddPaintableTilemap();
            _groundTilemap = FindGroundTilemap();
            if (_groundTilemap != null) EditorGUIUtility.PingObject(_groundTilemap);
        }

        // ============================ SCENE-VIEW BRUSH ============================

        private void OnSceneGui(SceneView view)
        {
            if (_showOverlay) DrawHeightOverlay();

            if (!_painting || _map == null || _tex == null) return;

            Event e = Event.current;
            Vector2 world = WorldUnderMouse(e);

            // Draw the brush footprint so the owner sees where it'll land.
            Handles.color = _brush == Brush.TerrainType ? new Color(0.3f, 0.9f, 1f, 0.9f)
                                                         : new Color(1f, 0.85f, 0.2f, 0.9f);
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

        /// <summary>
        /// Apply one brush dab centred at a world position. For the height brushes this writes the working
        /// pixel buffer. For the TerrainType brush it ALSO stamps/clears the type's tile on the ground tilemap
        /// at the cells the brush covers — so ONE stroke sets both the height AND the look.
        /// </summary>
        private void Dab(Vector2 worldCenter)
        {
            if (_pixels == null || _tex == null) return;
            int w = _tex.width, h = _tex.height;
            Vector2 size = _map.WorldSize;
            Vector2 min = _map.WorldCenter - size * 0.5f;
            float min0 = _map.MinElevation, max0 = _map.MaxElevation;

            // The terrain TYPE (if that brush is active) fixes the elevation + tile for the whole dab.
            TerrainTypePreset type = (_brush == Brush.TerrainType && _presets != null &&
                                      _typeIndex >= 0 && _typeIndex < _presets.Count) ? _presets[_typeIndex] : null;

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
                float next = type != null
                    ? type.elevation                                  // TYPE: snap the whole footprint to the type height
                    : ApplyBrush(elev, fall, idx, w, h, min0, max0);  // height brushes: shaped by the falloff
                float r01 = PaintedHeightField.EncodeElevation(next, min0, max0);
                _pixels[idx] = new Color(r01, r01, r01, 1f);
            }

            // Push to the working texture + rebuild the decoded field so the live preview updates this frame.
            _tex.SetPixels(_pixels);
            _tex.Apply(false, false);
            _map.Rebuild();
            RefreshOpenWaterSurfaces();
            _overlayDirty = true;

            // TYPE brush: stamp / clear the look on the ground tilemap over the SAME footprint.
            if (type != null) StampTiles(worldCenter, type);
        }

        /// <summary>
        /// Stamp (or clear) the terrain type's tile on the ground tilemap over the brush footprint. Underwater
        /// types (no tile / clearTile) ERASE any tile there so the water shows. Undo-recorded; marks the scene
        /// dirty. Auto-finds/creates the tilemap if the owner hasn't assigned one (rather than failing).
        /// </summary>
        private void StampTiles(Vector2 worldCenter, TerrainTypePreset type)
        {
            if (_groundTilemap == null) _groundTilemap = FindGroundTilemap();
            if (_groundTilemap == null)
            {
                // No canvas yet — make one so the LOOK half never silently no-ops.
                CreateGroundTilemap();
                if (_groundTilemap == null) return;
            }

            bool heightOnly = IsHeightOnly(type.groundTile != null, type.clearTile);
            TileBase tile = heightOnly ? null : type.groundTile;

            Undo.RecordObject(_groundTilemap, "Paint terrain type");

            // Enumerate the tilemap cells whose CENTRE lies within the brush radius (cells are ~1 m; the height
            // texels are ~0.83 m — both read the same world rect, so painting both keeps look==height aligned).
            var grid = _groundTilemap.layoutGrid;
            Vector3 cellSize = grid != null ? grid.cellSize : new Vector3(1f, 1f, 0f);
            float stepX = Mathf.Max(cellSize.x, 1e-3f);
            float stepY = Mathf.Max(cellSize.y, 1e-3f);
            int cellRx = Mathf.CeilToInt(_radius / stepX) + 1;
            int cellRy = Mathf.CeilToInt(_radius / stepY) + 1;
            Vector3Int centerCell = _groundTilemap.WorldToCell(new Vector3(worldCenter.x, worldCenter.y, 0f));

            for (int dy = -cellRy; dy <= cellRy; dy++)
            for (int dx = -cellRx; dx <= cellRx; dx++)
            {
                var cell = new Vector3Int(centerCell.x + dx, centerCell.y + dy, centerCell.z);
                Vector3 cellWorld = _groundTilemap.GetCellCenterWorld(cell);
                if (Vector2.Distance(new Vector2(cellWorld.x, cellWorld.y), worldCenter) > _radius) continue;
                _groundTilemap.SetTile(cell, tile);   // tile == null clears (underwater types)
            }

            EditorUtility.SetDirty(_groundTilemap);
            MarkActiveSceneDirty();
        }

        private static void MarkActiveSceneDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
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
                default: return elev;   // TerrainType handled in Dab (snaps to the type height)
            }
        }

        // ============================ HEIGHT COLOUR OVERLAY (edit-mode only) ============================

        /// <summary>
        /// Draw the height field false-coloured over the painted rect in the Scene view ONLY — a designer aid
        /// the owner can SEE while shaping, that NEVER serializes and NEVER renders in Play or a build (it is
        /// pure GL/Handles editor drawing). Cheap: a small CPU texture rebuilt only when the field changes
        /// (<see cref="_overlayDirty"/>), blitted once onto a world-space quad; no per-frame churn (rule 7).
        /// The legend marks the current preview-tide waterline.
        /// </summary>
        private void DrawHeightOverlay()
        {
            if (_map == null || _map.Field == null) return;
            var field = _map.Field;

            EnsureOverlayResources();
            if (_overlayDirty) RebuildOverlayTexture(field);
            if (_overlayTex == null || _overlayMat == null) return;

            // The painted world rect (the quad we draw the overlay onto).
            Vector2 size = _map.WorldSize;
            Vector2 min = _map.WorldCenter - size * 0.5f;
            Vector3 bl = new Vector3(min.x, min.y, 0f);
            Vector3 br = new Vector3(min.x + size.x, min.y, 0f);
            Vector3 tr = new Vector3(min.x + size.x, min.y + size.y, 0f);
            Vector3 tl = new Vector3(min.x, min.y + size.y, 0f);

            _overlayMat.mainTexture = _overlayTex;
            _overlayMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.QUADS);
            GL.Color(new Color(1f, 1f, 1f, 0.75f));   // semi-transparent so the underlying scene still reads
            GL.TexCoord2(0f, 0f); GL.Vertex(bl);
            GL.TexCoord2(1f, 0f); GL.Vertex(br);
            GL.TexCoord2(1f, 1f); GL.Vertex(tr);
            GL.TexCoord2(0f, 1f); GL.Vertex(tl);
            GL.End();
            GL.PopMatrix();

            DrawOverlayLegend();
        }

        /// <summary>
        /// Rebuild the false-colour overlay texture from the decoded height field via
        /// <see cref="TerrainHeightPalette"/>. Cells SUBMERGED at the current preview tide are tinted slightly
        /// toward blue so the waterline reads. Allocation only on change (not per-frame).
        /// </summary>
        private void RebuildOverlayTexture(PaintedHeightField field)
        {
            int w = field.Width, h = field.Height;
            if (_overlayTex == null || _overlayTex.width != w || _overlayTex.height != h)
            {
                DestroyImmediateSafe(ref _overlayTex);
                _overlayTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
                {
                    name = "TerrainPaint.HeightOverlay",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }

            float waterLevel = ResolvePreviewWaterLevel();
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 world = field.TexelToWorld(x, y);
                float elev = field.ElevationAt(world);
                Color c = TerrainHeightPalette.ColorForElevation(elev);
                // Nudge submerged cells a touch toward deep blue so the waterline is legible.
                if (TerrainHeightPalette.IsSubmerged(elev, waterLevel))
                    c = Color.Lerp(c, new Color(0.05f, 0.12f, 0.35f), 0.18f);
                px[y * w + x] = c;
            }
            _overlayTex.SetPixels(px);
            _overlayTex.Apply(false, false);
            _overlayDirty = false;
        }

        /// <summary>
        /// The preview-tide water level the overlay marks: read from the open scene's WaterSurface preview
        /// (so the overlay and the live water agree) when available, else mean datum (0). Editor-only.
        /// </summary>
        private float ResolvePreviewWaterLevel()
        {
            var surfaces = Object.FindObjectsByType<WaterSurface>(FindObjectsSortMode.None);
            foreach (var s in surfaces)
            {
                if (s == null) continue;
                var so = new SerializedObject(s);
                var prev = so.FindProperty("_previewTideLevel");
                if (prev != null) return prev.floatValue;
            }
            return 0f;
        }

        /// <summary>Draw the legend (colour ≈ elevation) + the preview waterline note as a Scene-view HUD.</summary>
        private void DrawOverlayLegend()
        {
            Handles.BeginGUI();
            var stops = TerrainHeightPalette.LegendStops();
            float waterLevel = ResolvePreviewWaterLevel();

            const float pad = 8f, rowH = 16f, swatch = 18f, width = 168f;
            float height = rowH * (stops.Length + 2) + pad * 2f;
            var box = new Rect(10f, 28f, width, height);
            GUI.Box(box, GUIContent.none);

            var label = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
            float y = box.y + pad;
            GUI.Label(new Rect(box.x + pad, y, width, rowH), "Height overlay (edit aid)", EditorStyles.whiteMiniLabel);
            y += rowH;
            // High → low so the legend reads like a side-on cliff (land at top, deep at bottom).
            for (int i = stops.Length - 1; i >= 0; i--)
            {
                var (elev, color) = stops[i];
                var sw = new Rect(box.x + pad, y + 1f, swatch, swatch - 4f);
                EditorGUI.DrawRect(sw, color);
                GUI.Label(new Rect(sw.xMax + 6f, y, width, rowH), $"{elev:0.#} m", label);
                y += rowH;
            }
            GUI.Label(new Rect(box.x + pad, y, width, rowH), $"waterline ≈ {waterLevel:0.#} m", label);
            Handles.EndGUI();
        }

        private void EnsureOverlayResources()
        {
            if (_overlayMat == null)
            {
                // Unlit/Transparent so the overlay tints over the scene without lighting; hidden + not saved.
                var shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                _overlayMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void DestroyOverlayResources()
        {
            DestroyImmediateSafe(ref _overlayTex);
            if (_overlayMat != null) { Object.DestroyImmediate(_overlayMat); _overlayMat = null; }
        }

        private static void DestroyImmediateSafe(ref Texture2D tex)
        {
            if (tex != null) { Object.DestroyImmediate(tex); tex = null; }
        }

        // ============================ ASSET / TEXTURE PLUMBING ============================

        private void CacheTexture()
        {
            _tex = _map != null ? _map.HeightTexture : null;
            _pixels = (_tex != null && _tex.isReadable) ? _tex.GetPixels() : null;
            if (_tex != null && !_tex.isReadable)
                Debug.LogWarning("[TerrainPaintTool] The map's texture is not readable — re-create it via " +
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
            if (!string.IsNullOrEmpty(pngPath) && pngPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
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
            if (map != null) { _map = map; CacheTexture(); _overlayDirty = true; }
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
                _map = map; CacheTexture(); _overlayDirty = true;
                Debug.Log("[TerrainPaintTool] Exported analytic St Peters → " +
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
                EditorUtility.DisplayDialog("Terrain Paint", "Assign or create a painted height map first.", "OK");
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

            // 2) Sim reads it: disable the scene's analytic terrain(s) — the zone-based TidalTerrain
            //    (St Peters) OR the rect-based RectTidalTerrain (the converged cove/Greywick, ADR 0012
            //    rec. 4) — and add a PaintedTidalTerrain. Disabling (not deleting) keeps it reversible.
            var analytics = new System.Collections.Generic.List<MonoBehaviour>();
            analytics.AddRange(Object.FindObjectsByType<TidalTerrain>(FindObjectsSortMode.None));
            analytics.AddRange(Object.FindObjectsByType<RectTidalTerrain>(FindObjectsSortMode.None));
            GameObject host = null;
            foreach (var analytic in analytics)
            {
                Undo.RecordObject(analytic, "Adopt painted seabed");
                analytic.enabled = false;
                EditorUtility.SetDirty(analytic);
                if (host == null) host = analytic.gameObject;
            }
            if (host == null)
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

            EditorUtility.DisplayDialog("Terrain Paint",
                "Adopted the painted map on the open scene:\n• Every WaterSurface now reads it (render).\n• " +
                "A PaintedTidalTerrain now feeds the sim (walkability / clams / boat-cross).\nThe analytic " +
                "TidalTerrain was DISABLED (not deleted) so this is reversible.\n\nSave the scene to keep it.",
                "Fair winds");
        }
    }
}
#endif
