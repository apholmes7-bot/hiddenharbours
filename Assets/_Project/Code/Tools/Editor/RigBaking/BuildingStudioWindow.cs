#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Building Studio</b> — dial a building on every axis the rig exposes, watch it turn, then bake
    /// it. Menu: <c>Hidden Harbours ▸ Art ▸ Building Studio</c>.
    ///
    /// <para>The wharf-building rig has twenty axes and the house rig nineteen; between them that is
    /// more combinations than anyone can hold in their head, and the seven/five shipped presets are a
    /// thin sample of it. This window makes the rig's whole surface reachable without writing JS, and
    /// makes the bake the last step rather than the only way to see anything.</para>
    ///
    /// <para><b>The dropdowns are read from the rig, not hard-coded.</b> Paint colours, sidings, roofs,
    /// doors, windows, cupolas, rooflines and types all come from the rig's own exported tables at load
    /// time, so a rig drop that adds a colour shows up here with no code change. Only a handful of axes
    /// have no exported table (attic, porch, wainscot, loft) and those are transcribed in
    /// <see cref="BuildingAxes"/> and grepped by a test — because an unknown value is not an error in
    /// these rigs, it silently falls through to a default.</para>
    ///
    /// <para><b>Orientation is shown honestly.</b> The preview names the cell's rig LABEL and the bearing
    /// it actually DEPICTS, which are not the same thing: both building rigs turn counter-clockwise, so
    /// the cell the rig calls 'E' draws a building facing west. The baker corrects this, so the baked
    /// sheet is honest — but the studio is looking at raw rig output, and showing only the label would
    /// be repeating the exact mistake that has shipped five times here.</para>
    /// </summary>
    public sealed class BuildingStudioWindow : EditorWindow
    {
        const int PreviewPad = 8;

        [MenuItem("Hidden Harbours/Art/Building Studio", priority = 38)]
        public static void Open()
        {
            var w = GetWindow<BuildingStudioWindow>("Building Studio");
            w.minSize = new Vector2(760, 520);
            w.Show();
        }

        // ---- state ----------------------------------------------------------------------------

        string _rigKey = "wharfBuilding";
        Dictionary<string, object> _dialled;
        string _buildName = "MyBuild";
        int _facing;                       // the CELL index the studio is showing (0..7)
        double _elev = 40;
        bool _showPivot = true;
        Vector2 _scrollAxes;

        // The live rig host is kept alive between repaints: spinning one up costs far more than a
        // render, and the window is a dialling loop by nature.
        IRigScriptHost _host;
        string _hostRigKey;
        RigGeometry _geo;
        string _globalName;
        AzimuthConvention _convention = AzimuthConvention.CounterClockwise;
        string _conventionNote = "";

        // Chip values pulled from the rig's own tables, per axis key — plus why one came back empty.
        Dictionary<string, string[]> _choices = new Dictionary<string, string[]>();
        Dictionary<string, string> _choiceErrors = new Dictionary<string, string>();
        string[] _presets = Array.Empty<string>();
        string _presetError;

        Texture2D _preview;
        string _previewKey = "";           // the opts+facing the cached texture was rendered for
        string _error;

        // The 8-facing turntable strip, mirroring the demo page's turntable. Refreshed only when
        // nothing is being dragged (see RefreshTurntableIfNeeded) — eight renders of a 1200×1160 cell
        // on every slider frame would make dialling unusable.
        bool _showTurntable = true;
        readonly Texture2D[] _turntable = new Texture2D[8];
        string _turntableKey = "";

        // ---- lifecycle ------------------------------------------------------------------------

        void OnEnable()
        {
            _dialled ??= BuildingAxes.Defaults(_rigKey);
        }

        void OnDisable() => Teardown();

        void Teardown()
        {
            _host?.Dispose();
            _host = null;
            _hostRigKey = null;
            if (_preview != null) DestroyImmediate(_preview);
            _preview = null;
            _previewKey = "";

            for (int i = 0; i < _turntable.Length; i++)
            {
                if (_turntable[i] != null) DestroyImmediate(_turntable[i]);
                _turntable[i] = null;
            }
            _turntableKey = "";
        }

        // ---- the rig host ---------------------------------------------------------------------

        bool EnsureHost()
        {
            if (_host != null && _hostRigKey == _rigKey) return true;

            _host?.Dispose();
            _host = null;

            try
            {
                var entry = RigCatalog.Get(_rigKey);
                _host = RigScriptHostFactory.Create();
                _geo = RigCatalog.Install(_host, entry);
                _globalName = entry.GlobalName;
                _hostRigKey = _rigKey;

                LoadChoicesFromRig();

                // Measure the convention ONCE per rig load, off the rig's defaults, so the preview can
                // report the true bearing. This is the same probe the bake runs; here it is informational
                // and must never block dialling, so a failure degrades to "unknown" instead of throwing.
                try
                {
                    var probe = BuildingRigAzimuthProbe.Measure(
                        _host, _globalName, "{}", _geo.Width, _geo.Height, _geo.PivotX);
                    _convention = probe.Convention;
                    _conventionNote = $"measured: {probe.Convention}";
                }
                catch (Exception e)
                {
                    _convention = entry.DeclaredConvention;
                    _conventionNote = $"probe failed, showing the catalog's declared " +
                                      $"{entry.DeclaredConvention} — {e.Message.Split('\n')[0]}";
                }

                _error = null;
                return true;
            }
            catch (Exception e)
            {
                _error = e.Message;
                _host?.Dispose();
                _host = null;
                return false;
            }
        }

        /// <summary>
        /// Fill every Choice axis's dropdown from the rig's exported table, falling back to the
        /// transcribed inline list where the rig exports none.
        /// </summary>
        void LoadChoicesFromRig()
        {
            _choices = new Dictionary<string, string[]>();
            _choiceErrors = new Dictionary<string, string>();

            foreach (var axis in BuildingAxes.For(_rigKey))
            {
                if (axis.Kind != BuildingAxisKind.Choice) continue;

                if (axis.RigTable == null)
                {
                    _choices[axis.Key] = axis.Inline ?? Array.Empty<string>();
                    continue;
                }

                _choices[axis.Key] = KeysOf(axis.RigTable, out string err);
                if (err != null) _choiceErrors[axis.Key] = $"{axis.RigTable}: {err}";
            }

            _presets = KeysOf("PRESETS", out _presetError);
        }

        /// <summary>
        /// Keys of a rig table, whether it is an object (<c>BODY</c>, <c>TYPES</c>) or an array
        /// (<c>SHAPES</c>, <c>SIDINGS</c>, <c>ROOFS</c> — the wharf rig exports ROOF_KEYS as an array).
        /// Both shapes appear across these two rigs, so asking which it is beats assuming.
        /// </summary>

        string[] KeysOf(string table, out string error)
        {
            error = null;
            string expr = $"(function(){{var t={_globalName}.{table}; if(!t) return ''; " +
                          "return (Array.isArray(t)?t:Object.keys(t)).join('|');})()";
            try
            {
                string joined = _host.EvaluateString(expr);
                if (string.IsNullOrEmpty(joined))
                {
                    error = $"{_globalName}.{table} is absent or empty";
                    return Array.Empty<string>();
                }
                return joined.Split('|').Where(s => s.Length > 0).ToArray();
            }
            catch (Exception e)
            {
                // ⚠️ REPORT, never swallow. A bare `catch { return empty; }` here is what hid the
                // newline bug above: every table threw, every dropdown read "(rig exports no values)",
                // and the window looked like the RIG was missing data rather than like this code was
                // broken. The message now reaches the inspector, so the next failure names itself.
                error = e.Message.Split('\n')[0];
                return Array.Empty<string>();
            }
        }

        // ---- GUI ------------------------------------------------------------------------------

        void OnGUI()
        {
            DrawToolbar();

            if (!EnsureHost())
            {
                EditorGUILayout.HelpBox(
                    $"Could not load the rig.\n\n{_error}", MessageType.Error);
                return;
            }

            DrawPresetRow();

            EditorGUILayout.BeginHorizontal();
            DrawAxesPanel();
            DrawPreviewPanel();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int rigIdx = Mathf.Max(0, Array.IndexOf(BuildingAxes.RigKeys, _rigKey));
            int newRig = EditorGUILayout.Popup(rigIdx, BuildingAxes.RigKeys.Select(Pretty).ToArray(),
                                               EditorStyles.toolbarPopup, GUILayout.Width(150));
            if (newRig != rigIdx)
            {
                _rigKey = BuildingAxes.RigKeys[newRig];
                _dialled = BuildingAxes.Defaults(_rigKey);
                _hostRigKey = null;         // force a reload
                Invalidate();
            }

            if (GUILayout.Button("Reset to rig defaults", EditorStyles.toolbarButton))
            {
                _dialled = BuildingAxes.Defaults(_rigKey);
                Invalidate();
            }

            _showTurntable = GUILayout.Toggle(_showTurntable, "Turntable",
                                              EditorStyles.toolbarButton, GUILayout.Width(72));

            GUILayout.FlexibleSpace();
            GUILayout.Label(_conventionNote, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// The preset ROW — the demo page's "preset wharf row", as buttons rather than a dropdown, so
        /// every starting point is visible at once instead of hidden behind a click. Loading one is a
        /// starting point, not a mode: the axes stay live afterwards.
        /// </summary>
        void DrawPresetRow()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Start from", EditorStyles.miniBoldLabel, GUILayout.Width(64));

            if (_presets.Length == 0)
            {
                GUILayout.Label(_presetError == null
                                    ? "the rig exports no PRESETS"
                                    : $"PRESETS unreadable — {_presetError}",
                                EditorStyles.miniLabel);
            }
            else
            {
                foreach (var p in _presets)
                {
                    bool active = string.Equals(_buildName, p, StringComparison.Ordinal);
                    var style = active ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                    if (GUILayout.Button(active ? $"● {p}" : p, style, GUILayout.MinWidth(72)))
                        LoadPreset(p);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawAxesPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            _scrollAxes = EditorGUILayout.BeginScrollView(_scrollAxes);

            EditorGUI.BeginChangeCheck();

            foreach (var axis in BuildingAxes.For(_rigKey))
            {
                switch (axis.Kind)
                {
                    case BuildingAxisKind.Choice:
                        DrawChips(axis);
                        break;

                    case BuildingAxisKind.Unit:
                        _dialled[axis.Key] = (double)EditorGUILayout.Slider(
                            axis.Label, (float)Convert.ToDouble(_dialled[axis.Key]), 0f, 1f);
                        break;

                    case BuildingAxisKind.Count:
                        _dialled[axis.Key] = EditorGUILayout.IntSlider(
                            axis.Label, Convert.ToInt32(_dialled[axis.Key]), 0, axis.MaxCount);
                        break;

                    case BuildingAxisKind.Flag:
                        _dialled[axis.Key] = EditorGUILayout.Toggle(
                            axis.Label, Convert.ToBoolean(_dialled[axis.Key]));
                        break;
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Camera (preview only)", EditorStyles.boldLabel);
            _elev = EditorGUILayout.Slider("Elevation°", (float)_elev, 10f, 70f);
            EditorGUILayout.HelpBox(
                "Elevation is a PREVIEW dial. The bake always uses the rig's default 40°, which is the " +
                "fleet's turntable elevation — a building baked at another elevation would not sit in " +
                "the same space as the boats.", MessageType.None);

            if (EditorGUI.EndChangeCheck()) Invalidate();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// One Choice axis as a wrapping row of CHIPS — the demo page's control, and the right one
        /// here: every value for an axis is visible at once, so picking a siding is a glance rather
        /// than a click-and-scan. A dropdown hides exactly the thing the owner is trying to compare.
        /// </summary>
        void DrawChips(in BuildingAxis axis)
        {
            var values = _choices.TryGetValue(axis.Key, out var v) ? v : Array.Empty<string>();

            EditorGUILayout.LabelField(axis.Label, EditorStyles.miniBoldLabel);

            if (values.Length == 0)
            {
                // Name the REASON, not just the absence. "(rig exports no values)" is what this window
                // showed when the bug was in this file, and it pointed the finger at the rig.
                EditorGUILayout.HelpBox(
                    _choiceErrors.TryGetValue(axis.Key, out var why)
                        ? $"Could not read this axis — {why}"
                        : "The rig exports no values for this axis.",
                    MessageType.Warning);
                return;
            }

            string cur = _dialled[axis.Key] as string;
            int idx = Array.IndexOf(values, cur);

            // A current value the rig's table does not contain means our transcription drifted. Show it
            // as an extra, clearly-marked chip rather than silently snapping to values[0] — a silent
            // snap would hide precisely the drift the chip is evidence of.
            var shown = values;
            if (idx < 0 && !string.IsNullOrEmpty(cur))
            {
                shown = values.Concat(new[] { $"⚠ {cur}" }).ToArray();
                idx = shown.Length - 1;
            }

            // Chips wrap on the panel width; ~92 px each reads the labels in this kit without truncating.
            int perRow = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth * 0.34f) / 92f));
            int next = GUILayout.SelectionGrid(idx, shown, perRow, EditorStyles.miniButton);

            if (next != idx && next < values.Length)
                _dialled[axis.Key] = values[next];

            EditorGUILayout.Space(2);
        }

        void DrawPreviewPanel()
        {
            EditorGUILayout.BeginVertical();

            // ---- facing control -----------------------------------------------------------------
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ turn", GUILayout.Width(70)))
            {
                _facing = (_facing + 7) % 8;
                Invalidate();
            }
            if (GUILayout.Button("turn ▶", GUILayout.Width(70)))
            {
                _facing = (_facing + 1) % 8;
                Invalidate();
            }

            string label = RigLabelFor(_facing);
            string depicts = DepictedBearing(_facing);
            GUILayout.Label(
                label == depicts
                    ? $"cell {_facing} — labelled '{label}', depicts {depicts}"
                    : $"cell {_facing} — labelled '{label}', DEPICTS {depicts}",
                EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();
            _showPivot = GUILayout.Toggle(_showPivot, "pivot", EditorStyles.miniButton,
                                          GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            if (label != depicts)
                EditorGUILayout.HelpBox(
                    $"This rig turns {_convention}. The cell it labels '{label}' draws a building facing " +
                    $"{depicts}. The BAKE corrects this, so the baked sheet is honest — you are looking " +
                    "at raw rig output here.", MessageType.Info);

            // ---- the picture --------------------------------------------------------------------
            Rect area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true),
                                                 GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(area, new Color(0.16f, 0.17f, 0.19f));

            RefreshPreviewIfNeeded();

            if (_error != null)
            {
                EditorGUI.HelpBox(area, _error, MessageType.Error);
            }
            else if (_preview != null)
            {
                Rect fit = FitRect(_preview.width, _preview.height, area, PreviewPad);
                GUI.DrawTexture(fit, _preview, ScaleMode.StretchToFill, alphaBlend: true);

                if (_showPivot) DrawPivotCross(fit);
            }

            if (_showTurntable) DrawTurntableStrip();

            // ---- bake -----------------------------------------------------------------------------
            EditorGUILayout.BeginHorizontal();
            _buildName = EditorGUILayout.TextField("Build name", _buildName);

            if (GUILayout.Button("Save this cell", GUILayout.Width(100), GUILayout.Height(22)))
                SaveCurrentCell();

            if (GUILayout.Button("Bake 8-facing sheet", GUILayout.Width(160), GUILayout.Height(22)))
                BakeCurrent();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                $"→ {BuildingBakeMenu.BuildingsFolder}/{Prefix()}_" +
                $"{BuildingAxes.SafeStem(_buildName, "Build")}.png",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// All eight facings at a glance — the demo page's turntable. This is the control that makes
        /// orientation checkable: a single facing tells you nothing about whether the set turns the way
        /// you expect, and the mislabel this repo keeps hitting is only visible across the whole row.
        /// Each thumbnail is captioned with its cell index and the bearing it actually DEPICTS.
        /// </summary>
        void DrawTurntableStrip()
        {
            RefreshTurntableIfNeeded();

            EditorGUILayout.BeginHorizontal();
            for (int cell = 0; cell < 8; cell++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(78));

                Rect r = GUILayoutUtility.GetRect(74, 74, GUILayout.Width(74), GUILayout.Height(74));
                EditorGUI.DrawRect(r, cell == _facing
                                          ? new Color(0.25f, 0.32f, 0.40f)
                                          : new Color(0.14f, 0.15f, 0.17f));

                var tex = _turntable[cell];
                if (tex != null)
                    GUI.DrawTexture(FitRect(tex.width, tex.height, r, 3f), tex,
                                    ScaleMode.StretchToFill, alphaBlend: true);

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    _facing = cell;
                    Invalidate();
                }

                GUILayout.Label($"{cell} · {DepictedBearing(cell)}",
                                EditorStyles.miniLabel, GUILayout.Width(74));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Label(
                "Captions are the bearing each cell DEPICTS, not the rig's label for it.",
                EditorStyles.miniLabel);
        }

        void RefreshTurntableIfNeeded()
        {
            // Only rebuild when nothing is being dragged. Eight renders of a 1200×1160 cell per slider
            // FRAME would make the window unusable; waiting for the drag to finish costs nothing,
            // because the big preview is already live and is what the eye is on mid-drag.
            if (GUIUtility.hotControl != 0) return;

            string opts = BuildingAxes.ToOptionsLiteral(_dialled, _elev);
            string key = $"{_rigKey}|{opts}";
            if (key == _turntableKey && _turntable[0] != null) return;

            for (int cell = 0; cell < 8; cell++)
            {
                var tex = RenderCell(cell, opts, out string err);
                if (err != null) { _error = err; break; }

                if (_turntable[cell] != null) DestroyImmediate(_turntable[cell]);
                _turntable[cell] = tex;
            }

            _turntableKey = key;
        }

        void DrawPivotCross(Rect fit)
        {
            // The pivot is the building's GROUND POINT — where it plants when placed. Showing it is the
            // difference between "that looks about right" and knowing where the thing will actually sit.
            float px = fit.x + fit.width * (float)(_geo.PivotX / _geo.Width);
            float py = fit.y + fit.height * (float)(_geo.PivotY / _geo.Height);

            var c = new Color(1f, 0.4f, 0.3f, 0.9f);
            EditorGUI.DrawRect(new Rect(px - 8, py - 0.5f, 16, 1), c);
            EditorGUI.DrawRect(new Rect(px - 0.5f, py - 8, 1, 16), c);
        }

        // ---- rendering -------------------------------------------------------------------------

        void Invalidate() { _previewKey = ""; _turntableKey = ""; Repaint(); }

        void RefreshPreviewIfNeeded()
        {
            string opts = BuildingAxes.ToOptionsLiteral(_dialled, _elev);
            string key = $"{_rigKey}|{_facing}|{opts}";
            if (key == _previewKey && _preview != null) return;

            var tex = RenderCell(_facing, opts, out string err);
            _previewKey = key;        // set either way, so a failing render doesn't spin

            if (err != null) { _error = err; return; }

            if (_preview != null) DestroyImmediate(_preview);
            _preview = tex;
            _error = null;
        }

        /// <summary>
        /// Render one cell to a texture. Shared by the big preview and the turntable strip so the two
        /// can never disagree about what a cell looks like.
        ///
        /// <para>The studio shows RAW rig output at the cell index, deliberately un-corrected — the
        /// banner and the strip captions are what explain the difference. Passing the corrected dir
        /// here would hide the very thing the owner asked to be able to see.</para>
        /// </summary>
        Texture2D RenderCell(int cell, string optsJs, out string error)
        {
            error = null;
            try
            {
                string d = ((double)cell).ToString("R", CultureInfo.InvariantCulture);
                byte[] rgba = _host.EvaluateBytes($"{_globalName}.render({d},{optsJs})");

                int expect = _geo.Width * _geo.Height * 4;
                if (rgba.Length != expect)
                    throw new InvalidOperationException(
                        $"render returned {rgba.Length} bytes, expected {expect}.");

                var tex = new Texture2D(_geo.Width, _geo.Height, TextureFormat.RGBA32, false, false)
                {
                    filterMode = FilterMode.Point,
                };

                // The rig hands back TOP-LEFT-origin rows; Unity textures are bottom-origin.
                var px = new Color32[_geo.Width * _geo.Height];
                for (int y = 0; y < _geo.Height; y++)
                {
                    int src = y * _geo.Width * 4;
                    int dst = (_geo.Height - 1 - y) * _geo.Width;
                    for (int x = 0; x < _geo.Width; x++)
                    {
                        int s = src + x * 4;
                        px[dst + x] = new Color32(rgba[s], rgba[s + 1], rgba[s + 2], rgba[s + 3]);
                    }
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);
                return tex;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        /// <summary>
        /// Write the cell on screen straight to a PNG — the demo page's single-cell download. Useful
        /// for a one-off prop or a reference shot without committing to an 8-facing sheet.
        /// </summary>
        void SaveCurrentCell()
        {
            if (_preview == null) return;

            string stem = $"{Prefix()}_{BuildingAxes.SafeStem(_buildName, "Build")}_cell{_facing}";
            string path = EditorUtility.SaveFilePanel("Save this cell",
                                                      System.IO.Path.Combine(RigCatalog.RepoRoot,
                                                                             BuildingBakeMenu.BuildingsFolder),
                                                      stem, "png");
            if (string.IsNullOrEmpty(path)) return;

            System.IO.File.WriteAllBytes(path, _preview.EncodeToPNG());
            AssetDatabase.Refresh();
            Debug.Log($"[building-studio] Saved cell {_facing} → {path}");
        }

        // ---- baking ----------------------------------------------------------------------------

        void BakeCurrent()
        {
            string stem = $"{Prefix()}_{BuildingAxes.SafeStem(_buildName, "Build")}";

            // The bake gets the dialled axes WITHOUT the preview elevation: 40° is the fleet's turntable
            // and a building baked at another camera would not sit in the same space as the boats.
            string opts = BuildingAxes.ToOptionsLiteral(_dialled);

            try
            {
                var r = BuildingRigBaker.Bake(BuildingBakeRequest.FromOptions(
                    _rigKey, opts, _buildName, BuildingBakeMenu.BuildingsFolder, stem));

                AssetDatabase.Refresh();

                Debug.Log($"[building-studio] Baked '{_buildName}' → {r.AssetPath}\n" +
                          $"  cell {r.CellWidth}×{r.CellHeight} ({r.Columns}×{r.Rows}), " +
                          $"sheet {r.SheetWidth}×{r.SheetHeight}, {r.PngBytes / 1024.0:F0} KB\n" +
                          $"  crop saved {r.CropSaving:P0}, pivot ({r.PivotX:F0},{r.PivotY:F0}), " +
                          $"{r.MeasuredConvention}\n  sidecar: {r.SidecarPath}");

                EditorUtility.DisplayDialog(
                    "Building Studio",
                    $"Baked '{_buildName}'.\n\n" +
                    $"{r.SheetWidth}×{r.SheetHeight}, {r.PngBytes / 1024.0:F0} KB\n" +
                    $"cell {r.CellWidth}×{r.CellHeight}, crop saved {r.CropSaving:P0}\n\n" +
                    $"{r.AssetPath}", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[building-studio] bake FAILED:\n{e}");
                EditorUtility.DisplayDialog("Building Studio — bake failed", e.Message, "OK");
            }
        }

        void LoadPreset(string preset)
        {
            // Read the preset's own fields off the rig and merge onto the defaults, so an axis the
            // preset doesn't mention keeps the rig's default rather than whatever was dialled before.
            var fresh = BuildingAxes.Defaults(_rigKey);

            try
            {
                foreach (var axis in BuildingAxes.For(_rigKey))
                {
                    string has = $"{_globalName}.PRESETS['{preset}']['{axis.Key}']";
                    if (!_host.EvaluateBool($"{has} !== undefined && {has} !== null")) continue;

                    fresh[axis.Key] = axis.Kind switch
                    {
                        BuildingAxisKind.Choice => (object)_host.EvaluateString($"String({has})"),
                        BuildingAxisKind.Flag => _host.EvaluateBool($"!!{has}"),
                        BuildingAxisKind.Count => (int)_host.EvaluateNumber(has),
                        _ => _host.EvaluateNumber(has),
                    };
                }

                _dialled = fresh;
                _buildName = preset;
                Invalidate();
            }
            catch (Exception e)
            {
                _error = $"Could not read preset '{preset}': {e.Message}";
            }
        }

        // ---- helpers ---------------------------------------------------------------------------

        string Prefix() => BuildingBakeMenu.Prefix(_rigKey);

        static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>What the rig's own <c>order</c> array calls this cell.</summary>
        static string RigLabelFor(int cell) => Compass[cell & 7];

        /// <summary>
        /// The bearing the cell actually DRAWS. For a counter-clockwise rig, cell <c>i</c> depicts
        /// <c>−45°·i</c> — so the cell labelled 'E' faces west.
        /// </summary>
        string DepictedBearing(int cell) =>
            _convention == AzimuthConvention.CounterClockwise
                ? Compass[(8 - cell) & 7]
                : Compass[cell & 7];

        static string Pretty(string rigKey) => rigKey switch
        {
            "house" => "Houses",
            "wharfBuilding" => "Wharf buildings",
            _ => rigKey,
        };

        static Rect FitRect(int w, int h, Rect area, float pad)
        {
            float aw = Mathf.Max(1, area.width - pad * 2);
            float ah = Mathf.Max(1, area.height - pad * 2);
            float scale = Mathf.Min(aw / w, ah / h);
            float fw = w * scale, fh = h * scale;
            return new Rect(area.x + (area.width - fw) * 0.5f,
                            area.y + (area.height - fh) * 0.5f, fw, fh);
        }
    }
}
#endif
