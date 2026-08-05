#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The <b>Grass Paint Tool</b> — a Scene-view brush that scatters the wind-swaying living grass (PR #102)
    /// by hand, the way the <see cref="HiddenHarbours.App.Editor.TerrainPaintTool"/> paints the coast. Drag in the
    /// Scene view to lay down tufts at a tunable DENSITY, choose which HEIGHT variants to mix and how much to
    /// RANDOMIZE their height, and tune the colour from lush dark green toward dry STRAW — all without leaving the
    /// editor.
    ///
    /// <para><b>Colour keeps the gradient.</b> Each tuft sprite already carries the cozy dark-to-light gradient;
    /// the colour knob only sets the tuft's <see cref="SpriteRenderer.color"/>, which the grass shader MULTIPLIES
    /// over that gradient. So sliding green→straw shifts the hue while the light/dark shading the owner liked is
    /// preserved (a multiply can't flatten the gradient). At straw = 0 the look is the shipped green, unchanged.</para>
    ///
    /// <para><b>What it paints.</b> Real scene GameObjects (a <c>SpriteRenderer</c> on the shared <c>Grass</c>
    /// material + a <see cref="YSortSprite"/> so they auto-layer with the player), parented under one
    /// <c>PaintedGrass</c> object, with full Undo. They sway off the shared wind and bend underfoot automatically
    /// (the self-installing GrassWindBridge + the player's GrassFootstep — no per-tuft wiring). Density is capped
    /// by a min-spacing reject so dragging back over an area never piles tufts up infinitely.</para>
    ///
    /// <para><b>⭐ WHAT IT PAINTS WITH IS DATA NOW.</b> This tool used to carry three literal sprite paths
    /// index-aligned to its three height toggles; adding a variant meant editing the list and getting the
    /// index right. It now reads <see cref="GrassLibraryCatalog"/> — the manifest the grass rigs bake — so
    /// the brush offers whatever has been baked, filed into the same Short / Medium / Tall mix by MEASURED
    /// climb, and filtered by HABITAT (meadow, sward, fringe, dune, headland, verge, marsh…). The three
    /// shipped tufts are in that library alongside the rest, unchanged and still first in it.</para>
    ///
    /// <para><b>Lane &amp; rules.</b> art-pipeline authoring aid (Art.Editor); writes only scene decor, nothing
    /// saved at runtime (rule 5); every knob is a field (rule 6); tufts share one material so hundreds batch
    /// (rule 7). Menu: <c>Hidden Harbours ▸ Tools ▸ Grass Paint Tool</c>.</para>
    /// </summary>
    public sealed class GrassPaintTool : EditorWindow
    {
        private const string GrassMaterialPath = GrassLibraryCatalog.GrassMaterialPath;
        private const string RootName = "PaintedGrass";
        // The straw multiply-target. The tuft texture is yellow-GREEN (green dominant), and a multiply can only
        // scale channels DOWN — so to actually read as dry straw the tint must drop green and (especially) blue
        // hard, leaving red dominant → a golden/khaki hue. A timid tint (e.g. 1,0.92,0.55) barely shifts and is
        // the reason it "looked like it wasn't working". This keeps the gradient (still a per-pixel multiply).
        private static readonly Color StrawTint = new Color(1.0f, 0.55f, 0.28f, 1f);

        private enum Mode { Paint, Erase }

        [SerializeField] private bool _painting = true;
        [SerializeField] private Mode _mode = Mode.Paint;
        [SerializeField] private float _radius = 2f;          // brush radius (m)
        [SerializeField] private float _density = 8f;         // target tufts per square metre
        [SerializeField] private float _flow = 0.3f;          // fraction of the target laid per dab (stroke build-up)

        [SerializeField] private bool _useMedium = true;      // height classes to mix (GrassLibraryCatalog.HeightClasses)
        [SerializeField] private bool _useShort = true;
        [SerializeField] private bool _useTall = true;
        [SerializeField] private float _baseScale = 1f;       // height multiplier
        [SerializeField] private float _scaleJitter = 0.2f;   // randomize height by +/- this fraction

        /// <summary>Habitat tags the brush draws from. Empty = the whole library, which is the
        /// shipped default so the tool behaves as it always has until the owner narrows it.</summary>
        [SerializeField] private List<string> _habitats = new List<string>();
        [SerializeField] private bool _showHabitats;

        [SerializeField] private float _straw = 0f;           // 0 = lush green (shipped), 1 = dry straw
        [SerializeField] private float _valueJitter = 0.12f;  // per-tuft brightness variety
        [SerializeField] private float _warmthJitter = 0.05f; // per-tuft warm/cool variety

        [SerializeField] private int _sortingOrder = 2;       // pre-Play default; YSortSprite recomputes from Y

        private Material _material;
        private GrassLibraryCatalog.Library _library;
        /// <summary>The library entries whose PNG actually imported — what the brush can draw.</summary>
        private List<GrassLibraryCatalog.Entry> _imported = new List<GrassLibraryCatalog.Entry>();
        private bool _strokeActive;
        private int _undoGroup;

        [MenuItem("Hidden Harbours/Tools/Grass Paint Tool", priority = 41)]
        public static void Open()
        {
            var w = GetWindow<GrassPaintTool>("Grass Paint");
            w.minSize = new Vector2(320f, 460f);
            w.Show();
        }

        private void OnEnable()
        {
            ResolveAssets();
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGui;

        // ============================ WINDOW UI ============================

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Grass Paint", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag in the Scene view to paint grass. It sways with the wind and bends underfoot automatically. " +
                "The colour knob shifts green→straw while keeping each tuft's gradient. Press Play to see the sway.",
                MessageType.Info);

            if (_material == null)
            {
                EditorGUILayout.HelpBox($"Grass material not found at {GrassMaterialPath}. Open Unity so it " +
                                        "imports the grass shader + material, then reopen this tool.", MessageType.Warning);
                if (GUILayout.Button("Retry loading assets")) ResolveAssets();
                return;
            }
            if (!HasAnyTuft())
            {
                EditorGUILayout.HelpBox(
                    _library == null
                        ? $"No grass library at {GrassLibraryCatalog.ManifestPath}. Bake it: " +
                          "Hidden Harbours ▸ Dev ▸ Bake Grass Library."
                        : "The grass library has no imported sprites. Open Unity so they import " +
                          "(and `git lfs pull` if the PNGs are still pointers), then Retry.",
                    MessageType.Warning);
                if (GUILayout.Button("Retry loading assets")) ResolveAssets();
                return;
            }

            _painting = EditorGUILayout.ToggleLeft("Paint in Scene view (drag over the ground)", _painting);
            _mode = (Mode)EditorGUILayout.EnumPopup("Mode", _mode);
            _radius = EditorGUILayout.Slider("Brush radius (m)", _radius, 0.25f, 20f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
            _density = EditorGUILayout.Slider("Tufts per m²", _density, 0.5f, 40f);
            _flow = EditorGUILayout.Slider("Flow (build-up per drag)", _flow, 0.05f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Height", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _useShort = GUILayout.Toggle(_useShort, "Short", "Button");
                _useMedium = GUILayout.Toggle(_useMedium, "Medium", "Button");
                _useTall = GUILayout.Toggle(_useTall, "Tall", "Button");
            }
            _baseScale = EditorGUILayout.Slider("Height scale", _baseScale, 0.4f, 2f);
            _scaleJitter = EditorGUILayout.Slider("Randomize height (±)", _scaleJitter, 0f, 0.8f);

            DrawHabitatSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colour (keeps each tuft's gradient)", EditorStyles.boldLabel);
            _straw = EditorGUILayout.Slider("Green → straw", _straw, 0f, 1f);
            _valueJitter = EditorGUILayout.Slider("Brightness variety", _valueJitter, 0f, 0.4f);
            _warmthJitter = EditorGUILayout.Slider("Warm/cool variety", _warmthJitter, 0f, 0.2f);
            var swatch = Color.Lerp(Color.white, StrawTint, _straw);
            var rect = EditorGUILayout.GetControlRect(false, 16f);
            EditorGUI.DrawRect(rect, swatch);
            EditorGUILayout.LabelField("↑ tint multiplied over the gradient (white = shipped green)", EditorStyles.miniLabel);
            // The colour applies to NEW strokes; click to push it onto grass already painted (a live tuner).
            if (GUILayout.Button("Apply colour to existing painted grass"))
                RecolourExisting();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select PaintedGrass")) SelectRoot();
                if (GUILayout.Button("Clear ALL painted grass")) ClearAll();
            }
        }

        // ============================ SCENE-VIEW BRUSH ============================

        private void OnSceneGui(SceneView view)
        {
            if (!_painting || _material == null || !HasAnyTuft()) return;

            Event e = Event.current;
            Vector2 world = WorldUnderMouse(e);

            Handles.color = _mode == Mode.Paint ? new Color(0.4f, 0.9f, 0.4f, 0.9f) : new Color(0.9f, 0.4f, 0.3f, 0.9f);
            Handles.DrawWireDisc(new Vector3(world.x, world.y, 0f), Vector3.forward, _radius);

            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            bool leftButton = e.button == 0 && !e.alt;
            if (leftButton && e.type == EventType.MouseDown)
            {
                _strokeActive = true;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(_mode == Mode.Paint ? "Paint grass" : "Erase grass");
                _undoGroup = Undo.GetCurrentGroup();
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
                Undo.CollapseUndoOperations(_undoGroup);
                e.Use();
            }

            view.Repaint();
        }

        /// <summary>The world XY under the mouse, projected onto the z=0 plane (the 2D ground).</summary>
        private static Vector2 WorldUnderMouse(Event e)
        {
            Ray r = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            float t = Mathf.Abs(r.direction.z) > 1e-5f ? -r.origin.z / r.direction.z : 0f;
            Vector3 p = r.origin + r.direction * t;
            return new Vector2(p.x, p.y);
        }

        private void Dab(Vector2 center)
        {
            if (_mode == Mode.Erase) { EraseDab(center); return; }

            var root = GetOrCreateRoot();
            float minSpacing = MinSpacing(_density);
            // Gather existing tuft positions near the brush so density is capped (no infinite pile-up on re-drag).
            var nearby = GatherTuftPositions(root, center, _radius + minSpacing);

            var brush = BrushEntries();
            if (brush.Count == 0) return;   // no imported tuft to paint with
            int attempts = Mathf.CeilToInt(_density * Mathf.PI * _radius * _radius * Mathf.Clamp01(_flow));
            for (int i = 0; i < attempts; i++)
            {
                Vector2 p = center + Random.insideUnitCircle * _radius;
                if (TooClose(p, nearby, minSpacing)) continue;
                CreateTuft(root, p, brush);
                nearby.Add(p);
            }
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private void EraseDab(Vector2 center)
        {
            var root = FindRoot();
            if (root == null) return;
            float r2 = _radius * _radius;
            // Collect first (don't mutate the hierarchy while iterating).
            var doomed = new List<GameObject>();
            foreach (Transform child in root.transform)
                if (((Vector2)child.position - center).sqrMagnitude <= r2)
                    doomed.Add(child.gameObject);
            foreach (var go in doomed) Undo.DestroyObjectImmediate(go);
            if (doomed.Count > 0) EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private void CreateTuft(GameObject root, Vector2 p, List<GrassLibraryCatalog.Entry> brush)
        {
            var entry = brush[Random.Range(0, brush.Count)];
            var sprite = GrassLibraryCatalog.LoadSprite(entry);
            if (sprite == null) return;       // dropped between resolve and dab — skip, never null-render

            var go = new GameObject(entry.Name);
            Undo.RegisterCreatedObjectUndo(go, "Paint grass");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(p.x, p.y, 0f);
            float s = _baseScale * Random.Range(1f - _scaleJitter, 1f + _scaleJitter);
            go.transform.localScale = new Vector3(s, s, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = _material;
            sr.sortingOrder = _sortingOrder;
            sr.color = TintFor(p);
            go.AddComponent<YSortSprite>();   // auto-layer by world Y (sorts around the player)
        }

        /// <summary>Per-tuft tint = the green→straw base, with brightness + warmth jitter. Multiplied over the
        /// sprite gradient, so the dark-to-light shading is preserved; at straw 0 the base is white (unchanged).
        /// The jitter is hashed from the tuft's POSITION (deterministic), so re-running the colour over existing
        /// grass is stable — only the green→straw / jitter SETTINGS change the look, not a fresh random roll.</summary>
        private Color TintFor(Vector2 pos)
        {
            Color baseTint = Color.Lerp(Color.white, StrawTint, _straw);
            float v = Mathf.Lerp(1f - _valueJitter, 1f + _valueJitter, Hash01(pos, 17.13f));
            float warm = Mathf.Lerp(-_warmthJitter, _warmthJitter, Hash01(pos, 51.71f));
            return new Color(
                Mathf.Clamp01(baseTint.r * v + warm),
                Mathf.Clamp01(baseTint.g * v),
                Mathf.Clamp01(baseTint.b * v - warm * 0.5f),
                1f);
        }

        /// <summary>A stable 0..1 hash of a world position (plus a salt), so per-tuft jitter is reproducible.</summary>
        private static float Hash01(Vector2 p, float salt)
        {
            float h = Mathf.Sin(Vector2.Dot(p, new Vector2(12.9898f, 78.233f)) + salt) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>
        /// Re-apply the CURRENT colour settings (green→straw + variety) to ALL grass already painted under
        /// <c>PaintedGrass</c> — so the colour knob is a live tuner, not just a setting for the next stroke.
        /// One Undo step; the position-hashed jitter keeps each tuft's variety stable across re-applies.
        /// </summary>
        private void RecolourExisting()
        {
            var root = FindRoot();
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0) return;
            Undo.RecordObjects(renderers, "Recolour painted grass");
            foreach (var sr in renderers)
                sr.color = TintFor(sr.transform.position);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        // ============================ HELPERS ============================

        /// <summary>
        /// The habitat filter, folded away by default. Tags come from the manifest, so this list grows
        /// with the library and never needs editing here. Nothing selected = the whole library, which is
        /// what the brush has always done.
        /// </summary>
        private void DrawHabitatSection()
        {
            if (_library == null || _library.Habitats.Count == 0) return;

            EditorGUILayout.Space();
            string summary = _habitats.Count == 0 ? "all habitats" : string.Join(", ", _habitats);
            _showHabitats = EditorGUILayout.Foldout(
                _showHabitats, $"Habitat — {summary} ({BrushEntries().Count} sprite(s))", true);
            if (!_showHabitats) return;

            EditorGUILayout.HelpBox(
                "Which ground these blades belong on. Nothing ticked paints from the whole library. " +
                "Habitat is a tag on each baked variant, not a name — the scatter pass reads the same tags.",
                MessageType.None);

            foreach (var tag in _library.Habitats.Keys.OrderBy(k => k))
            {
                int have = _imported.Count(e => e.HasHabitat(tag));
                using (new EditorGUI.DisabledScope(have == 0))
                {
                    bool on = _habitats.Contains(tag);
                    bool now = EditorGUILayout.ToggleLeft(
                        new GUIContent($"{tag} ({have})", _library.Habitats[tag]), on);
                    if (now == on) continue;
                    if (now) _habitats.Add(tag); else _habitats.Remove(tag);
                }
            }

            if (GUILayout.Button("Clear habitat filter")) _habitats.Clear();
        }

        private void ResolveAssets()
        {
            _material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            _library = GrassLibraryCatalog.Load();
            _imported = GrassLibraryCatalog.Imported(_library);
            // Drop any habitat the current library doesn't declare, so a stale serialized filter from an
            // older bake can never narrow the brush down to nothing the owner can't see or clear.
            if (_library != null) _habitats.RemoveAll(t => !_library.Habitats.ContainsKey(t));
        }

        private bool HasAnyTuft() => _imported.Count > 0;

        /// <summary>The enabled height classes, in the library's own naming.</summary>
        private List<string> EnabledClasses()
        {
            var classes = new List<string>();
            if (_useShort) classes.Add("short");
            if (_useMedium) classes.Add("medium");
            if (_useTall) classes.Add("tall");
            return classes;
        }

        /// <summary>
        /// What the brush draws from: the imported library narrowed by the height toggles and the habitat
        /// filter. <see cref="GrassLibraryCatalog.Library.Choose"/> falls back to the whole library rather
        /// than returning nothing, so an over-narrow filter (or every toggle off) still paints — silently
        /// painting NOTHING is the failure mode worth avoiding, because it reads as a broken tool.
        /// </summary>
        private List<GrassLibraryCatalog.Entry> BrushEntries()
        {
            if (_imported.Count == 0) return _imported;
            var narrowed = new GrassLibraryCatalog.Library();
            narrowed.Entries.AddRange(_imported);
            return narrowed.Choose(_habitats, EnabledClasses());
        }

        /// <summary>Target spacing (m) between tufts for the chosen density — caps the per-area count.</summary>
        private static float MinSpacing(float density) => 0.8f / Mathf.Sqrt(Mathf.Max(density, 0.01f));

        private static bool TooClose(Vector2 p, List<Vector2> others, float minSpacing)
        {
            float m2 = minSpacing * minSpacing;
            for (int i = 0; i < others.Count; i++)
                if ((others[i] - p).sqrMagnitude < m2) return true;
            return false;
        }

        private static List<Vector2> GatherTuftPositions(GameObject root, Vector2 center, float range)
        {
            var list = new List<Vector2>();
            float r2 = range * range;
            foreach (Transform child in root.transform)
            {
                Vector2 cp = child.position;
                if ((cp - center).sqrMagnitude <= r2) list.Add(cp);
            }
            return list;
        }

        private GameObject GetOrCreateRoot()
        {
            var root = FindRoot();
            if (root != null) return root;
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create PaintedGrass");
            EditorSceneManager.MarkSceneDirty(root.scene);
            return root;
        }

        private static GameObject FindRoot()
        {
            // A root-level object named PaintedGrass in the active scene (do not reach into prefab contents).
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) return go;
            return null;
        }

        private void SelectRoot()
        {
            var root = FindRoot();
            if (root != null) { Selection.activeGameObject = root; EditorGUIUtility.PingObject(root); }
        }

        private void ClearAll()
        {
            var root = FindRoot();
            if (root == null) return;
            if (!EditorUtility.DisplayDialog("Clear painted grass",
                    $"Delete the '{RootName}' object and ALL grass painted under it?", "Delete", "Cancel"))
                return;
            var sc = root.scene;
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(sc);
        }
    }
}
#endif
