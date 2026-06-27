#if UNITY_EDITOR
using System.Collections.Generic;
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
    /// <para><b>Lane &amp; rules.</b> art-pipeline authoring aid (Art.Editor); writes only scene decor, nothing
    /// saved at runtime (rule 5); every knob is a field (rule 6); tufts share one material so hundreds batch
    /// (rule 7). Menu: <c>Hidden Harbours ▸ Tools ▸ Grass Paint Tool</c>.</para>
    /// </summary>
    public sealed class GrassPaintTool : EditorWindow
    {
        private const string GrassMaterialPath = "Assets/_Project/Art/Materials/Grass.mat";
        private const string RootName = "PaintedGrass";

        // The three tuft variants, index-aligned to the height toggles (medium / short / tall).
        private static readonly string[] TuftPaths =
        {
            "Assets/_Project/Art/Sprites/GrassTuft.png",
            "Assets/_Project/Art/Sprites/GrassTuft_Short.png",
            "Assets/_Project/Art/Sprites/GrassTuft_Tall.png",
        };
        private static readonly Color StrawTint = new Color(1.0f, 0.92f, 0.55f, 1f);

        private enum Mode { Paint, Erase }

        [SerializeField] private bool _painting = true;
        [SerializeField] private Mode _mode = Mode.Paint;
        [SerializeField] private float _radius = 2f;          // brush radius (m)
        [SerializeField] private float _density = 8f;         // target tufts per square metre
        [SerializeField] private float _flow = 0.3f;          // fraction of the target laid per dab (stroke build-up)

        [SerializeField] private bool _useMedium = true;      // height variants to mix (index-aligned to TuftPaths)
        [SerializeField] private bool _useShort = true;
        [SerializeField] private bool _useTall = true;
        [SerializeField] private float _baseScale = 1f;       // height multiplier
        [SerializeField] private float _scaleJitter = 0.2f;   // randomize height by +/- this fraction

        [SerializeField] private float _straw = 0f;           // 0 = lush green (shipped), 1 = dry straw
        [SerializeField] private float _valueJitter = 0.12f;  // per-tuft brightness variety
        [SerializeField] private float _warmthJitter = 0.05f; // per-tuft warm/cool variety

        [SerializeField] private int _sortingOrder = 2;       // pre-Play default; YSortSprite recomputes from Y

        private Material _material;
        private Sprite[] _tufts;
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
                EditorGUILayout.HelpBox("No grass tuft sprites found in Assets/_Project/Art/Sprites/ " +
                                        "(GrassTuft*.png). Open Unity so they import, then Retry.", MessageType.Warning);
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colour (keeps each tuft's gradient)", EditorStyles.boldLabel);
            _straw = EditorGUILayout.Slider("Green → straw", _straw, 0f, 1f);
            _valueJitter = EditorGUILayout.Slider("Brightness variety", _valueJitter, 0f, 0.4f);
            _warmthJitter = EditorGUILayout.Slider("Warm/cool variety", _warmthJitter, 0f, 0.2f);
            var swatch = Color.Lerp(Color.white, StrawTint, _straw);
            var rect = EditorGUILayout.GetControlRect(false, 16f);
            EditorGUI.DrawRect(rect, swatch);
            EditorGUILayout.LabelField("↑ tint multiplied over the gradient (white = shipped green)", EditorStyles.miniLabel);

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

            int[] variants = EnabledVariants();
            if (variants.Length == 0) return;   // no imported tuft to paint with
            int attempts = Mathf.CeilToInt(_density * Mathf.PI * _radius * _radius * Mathf.Clamp01(_flow));
            for (int i = 0; i < attempts; i++)
            {
                Vector2 p = center + Random.insideUnitCircle * _radius;
                if (TooClose(p, nearby, minSpacing)) continue;
                CreateTuft(root, p, variants);
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

        private void CreateTuft(GameObject root, Vector2 p, int[] variants)
        {
            var go = new GameObject("Tuft");
            Undo.RegisterCreatedObjectUndo(go, "Paint grass");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(p.x, p.y, 0f);
            float s = _baseScale * Random.Range(1f - _scaleJitter, 1f + _scaleJitter);
            go.transform.localScale = new Vector3(s, s, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _tufts[variants[Random.Range(0, variants.Length)]];
            sr.sharedMaterial = _material;
            sr.sortingOrder = _sortingOrder;
            sr.color = TintFor();
            go.AddComponent<YSortSprite>();   // auto-layer by world Y (sorts around the player)
        }

        /// <summary>Per-tuft tint = the green→straw base, with brightness + warmth jitter. Multiplied over the
        /// sprite gradient, so the dark-to-light shading is preserved; at straw 0 the base is white (unchanged).</summary>
        private Color TintFor()
        {
            Color baseTint = Color.Lerp(Color.white, StrawTint, _straw);
            float v = Random.Range(1f - _valueJitter, 1f + _valueJitter);
            float warm = Random.Range(-_warmthJitter, _warmthJitter);
            return new Color(
                Mathf.Clamp01(baseTint.r * v + warm),
                Mathf.Clamp01(baseTint.g * v),
                Mathf.Clamp01(baseTint.b * v - warm * 0.5f),
                1f);
        }

        // ============================ HELPERS ============================

        private void ResolveAssets()
        {
            _material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            // INDEX-ALIGNED to TuftPaths (0 = medium, 1 = short, 2 = tall): keep a null slot for any variant that
            // didn't import, so the height toggles can never map to the wrong sprite if one PNG is missing.
            _tufts = new Sprite[TuftPaths.Length];
            for (int i = 0; i < TuftPaths.Length; i++)
                _tufts[i] = TileAssetBuilder.LoadSpriteAny(TuftPaths[i]);
        }

        private bool HasAnyTuft()
        {
            if (_tufts == null) return false;
            for (int i = 0; i < _tufts.Length; i++) if (_tufts[i] != null) return true;
            return false;
        }

        private bool Variant(int i) => _tufts != null && i < _tufts.Length && _tufts[i] != null;

        /// <summary>Indices into <see cref="_tufts"/> for the enabled, IMPORTED height variants (never empty so
        /// long as any tuft imported — falls back to all imported variants if the owner disabled every one).</summary>
        private int[] EnabledVariants()
        {
            var idx = new List<int>();
            if (_useMedium && Variant(0)) idx.Add(0);
            if (_useShort  && Variant(1)) idx.Add(1);
            if (_useTall   && Variant(2)) idx.Add(2);
            if (idx.Count == 0)
                for (int i = 0; i < _tufts.Length; i++) if (_tufts[i] != null) idx.Add(i);
            return idx.ToArray();
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
