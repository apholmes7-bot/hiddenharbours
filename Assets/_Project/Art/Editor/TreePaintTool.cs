#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The <b>Tree Paint Tool</b> — a Scene-view brush that plants the Acadian trees, the third
    /// sibling of <see cref="GrassPaintTool"/> and <see cref="FlowerPaintTool"/>. Drag over the
    /// ground to lay down a stand; pick a species or mix every one of them, mix the drawn VARIANTS,
    /// set how close together they may stand, and jitter size and colour. What it plants pivots at
    /// the trunk, carries its own species' trunk anchor, and sways off the real sim wind — there is
    /// nothing to wire.
    ///
    /// <para><b>Three trees are a pattern, so the brush loop is still not lifted out.</b> The
    /// <see cref="FlowerPaintTool"/> doc says two is not yet a pattern and to lift the shared loop
    /// when a third appears. This is the third — but its settings diverge FURTHER than the flowers'
    /// did (spacing in metres rather than a density-derived reject, variants rather than bloom rows,
    /// and no mirroring at all, below), so what the three share is a ~40-line gesture and what they
    /// do not share is every knob. Lifting a base class here would buy indirection, not reuse.
    /// Noted rather than done, so the next person decides on evidence.</para>
    ///
    /// <para>🔴 <b>Trees are never mirrored, unlike the flowers.</b> Both other brushes flip half of
    /// what they paint for free variety. The rig bakes every tree under ONE fixed screen-space key
    /// light (upper-left, <c>Trees.json ▸ light.key</c>), so mirroring a 5 m tree lights it from the
    /// wrong side against every other tree, building and boat in the scene. The art's own answer to
    /// variety is the four drawn VARIANTS per species — mix those instead.</para>
    ///
    /// <para><b>Jitter is HASHED FROM WORLD POSITION</b>, exactly as the other two brushes do it, so
    /// re-painting the same ground gives the same stand back and the knobs are live tuners rather
    /// than a fresh random roll per click.</para>
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Tools ▸ Tree Paint Tool</c>. Scene decor only — nothing saved
    /// at runtime (rule 5), every knob is a field (rule 6).</para>
    /// </summary>
    public sealed class TreePaintTool : EditorWindow
    {
        /// <summary>Everything painted goes under one scene object, so a stand can be selected,
        /// moved or cleared as a unit.</summary>
        public const string RootName = "PaintedTrees";

        private enum Mode { Paint, Erase }

        [SerializeField] private bool _painting = true;
        [SerializeField] private Mode _mode = Mode.Paint;
        [SerializeField] private float _radius = 6f;              // brush radius (m)

        [SerializeField] private int _speciesIndex;
        [SerializeField] private bool _mixSpecies;                // draw from every species

        // Which drawn variants to mix — index-aligned to the sheet's COLUMNS (4 today, read from the
        // contract). Sized by SyncVariantsToSpecies so a species with a different count still works.
        [SerializeField] private bool[] _variants = { true, true, true, true };

        // Trees are metres apart, not centimetres, so spacing is a DIRECT field here rather than the
        // flowers' density-derived reject — "no closer than 2 m" is the thing a person actually means.
        [SerializeField] private float _perHundredSquareMetres = 12f;
        [SerializeField] private float _minSpacing = 1.8f;         // m between trunks

        [SerializeField] private float _baseScale = 1f;
        [SerializeField] private float _scaleJitter = 0.06f;
        [SerializeField] private float _valueJitter = 0.08f;       // per-tree brightness variety
        [SerializeField] private float _warmthJitter = 0.03f;      // per-tree warm/cool variety

        [SerializeField] private int _sortingOrder = AcadianTreeCatalog.SortingOrder;

        private List<AcadianTreeCatalog.Placement> _trees;
        private string[] _labels;
        private bool _strokeActive;
        private int _undoGroup;

        [MenuItem("Hidden Harbours/Tools/Tree Paint Tool", priority = 43)]
        public static void Open()
        {
            var w = GetWindow<TreePaintTool>("Tree Paint");
            w.minSize = new Vector2(340f, 520f);
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
            EditorGUILayout.LabelField("Tree Paint", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag in the Scene view to plant Acadian trees. They plant at the TRUNK (not the " +
                "bottom of the sprite), layer around the player by position, and sway with the real " +
                "wind — press Play to see it. Nothing to wire up.",
                MessageType.Info);

            if (_trees == null || _trees.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No baked trees found. Expected {TreeKitCatalog.ContractPath} and its sheets — " +
                    "run Hidden Harbours ▸ Art ▸ Bake Acadian Trees, then ▸ Slice Acadian Tree " +
                    "Sheets, then Retry.", MessageType.Warning);
                if (GUILayout.Button("Retry loading assets")) ResolveAssets();
                return;
            }
            if (AcadianTreeCatalog.LoadMaterial() == null)
            {
                EditorGUILayout.HelpBox(
                    $"The canopy material is missing ({AcadianTreeCatalog.MaterialPath}). Trees would " +
                    "be planted STATIC. Open Unity so it imports the TreeWind shader + material, " +
                    "then Retry.", MessageType.Warning);
                if (GUILayout.Button("Retry loading assets")) ResolveAssets();
                return;
            }

            _painting = EditorGUILayout.ToggleLeft("Paint in Scene view (drag over the ground)", _painting);
            _mode = (Mode)EditorGUILayout.EnumPopup("Mode", _mode);
            _radius = EditorGUILayout.Slider("Brush radius (m)", _radius, 1f, 40f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("What to plant", EditorStyles.boldLabel);
            _mixSpecies = EditorGUILayout.ToggleLeft("Mix every species (a mixed Acadian stand)", _mixSpecies);
            using (new EditorGUI.DisabledScope(_mixSpecies))
            {
                int next = EditorGUILayout.Popup("Species", Mathf.Clamp(_speciesIndex, 0, _trees.Count - 1), _labels);
                if (next != _speciesIndex) { _speciesIndex = next; SyncVariantsToSpecies(); }
            }

            var picked = CurrentTree();
            if (!_mixSpecies && picked.IsValid)
                EditorGUILayout.LabelField(
                    $"{picked.Entry.latin} · {picked.Entry.form} · draws " +
                    $"{AcadianTreeCatalog.DrawnHeightMetres(picked.Entry, Ppu):0.00} m tall, " +
                    $"trunk anchor {picked.Entry.trunkAnchor:F4}",
                    EditorStyles.miniLabel);

            EditorGUILayout.Space();
            int variantCount = VariantCountForUi();
            if (variantCount > 1)
            {
                EditorGUILayout.LabelField("Drawn variants to mix", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                    for (int v = 0; v < variantCount && v < _variants.Length; v++)
                        _variants[v] = GUILayout.Toggle(_variants[v], $"#{v + 1}", "Button");
                EditorGUILayout.LabelField(
                    "Different individual trees of the same species — this is the art's own variety, " +
                    "and the reason nothing here is mirrored (the key light is baked in).",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("How thick a stand", EditorStyles.boldLabel);
            _perHundredSquareMetres = EditorGUILayout.Slider("Trees per 100 m²", _perHundredSquareMetres, 1f, 80f);
            _minSpacing = EditorGUILayout.Slider("Never closer than (m)", _minSpacing, 0.4f, 10f);
            EditorGUILayout.LabelField(
                $"≈ {TargetPerDab():0} tree(s) attempted per dab of a {_radius:0.#} m brush.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variety", EditorStyles.boldLabel);
            _baseScale = EditorGUILayout.Slider("Size", _baseScale, 0.5f, 1.6f);
            _scaleJitter = EditorGUILayout.Slider("Randomize size (±)", _scaleJitter, 0f, 0.4f);
            _valueJitter = EditorGUILayout.Slider("Brightness variety", _valueJitter, 0f, 0.3f);
            _warmthJitter = EditorGUILayout.Slider("Warm/cool variety", _warmthJitter, 0f, 0.15f);
            EditorGUILayout.LabelField(
                "Size is a real scale on pixel art — keep it small or the trees stop landing on the " +
                "pixel grid and shimmer when the camera moves.", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select PaintedTrees")) SelectRoot();
                if (GUILayout.Button("Clear ALL painted trees")) ClearAll();
            }
        }

        // ============================ SCENE-VIEW BRUSH ============================

        private void OnSceneGui(SceneView view)
        {
            if (!_painting || _trees == null || _trees.Count == 0) return;

            Event e = Event.current;
            Vector2 world = WorldUnderMouse(e);

            Handles.color = _mode == Mode.Paint ? new Color(0.45f, 0.8f, 0.5f, 0.9f)
                                                : new Color(0.9f, 0.4f, 0.3f, 0.9f);
            Handles.DrawWireDisc(new Vector3(world.x, world.y, 0f), Vector3.forward, _radius);

            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            bool leftButton = e.button == 0 && !e.alt;
            if (leftButton && e.type == EventType.MouseDown)
            {
                _strokeActive = true;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(_mode == Mode.Paint ? "Plant trees" : "Clear trees");
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

            var choices = Candidates();
            if (choices.Count == 0) return;   // nothing sliced for this species — say nothing, do nothing

            var material = AcadianTreeCatalog.LoadMaterial();
            var root = GetOrCreateRoot();
            var nearby = GatherPositions(root, center, _radius + _minSpacing);

            int attempts = Mathf.CeilToInt(TargetPerDab());
            for (int i = 0; i < attempts; i++)
            {
                Vector2 p = center + Random.insideUnitCircle * _radius;
                if (TooClose(p, nearby, _minSpacing)) continue;
                var pick = choices[Random.Range(0, choices.Count)];
                PlantTree(root, p, pick.placement, pick.sprite, material);
                nearby.Add(p);
            }
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private void EraseDab(Vector2 center)
        {
            var root = FindRoot();
            if (root == null) return;
            float r2 = _radius * _radius;
            var doomed = new List<GameObject>();
            foreach (Transform child in root.transform)
                if (((Vector2)child.position - center).sqrMagnitude <= r2)
                    doomed.Add(child.gameObject);
            foreach (var go in doomed) Undo.DestroyObjectImmediate(go);
            if (doomed.Count > 0) EditorSceneManager.MarkSceneDirty(root.scene);
        }

        /// <summary>
        /// Plant ONE tree. Goes through <see cref="AcadianTreeCatalog.Configure"/> — the same call the
        /// prefab builder makes — so a painted tree and a dragged-in one are the same object, trunk
        /// anchor included. Deliberately NOT a prefab instance: a painted stand is hundreds of
        /// objects, and instances would tie every one of them to a prefab the owner may rebuild.
        /// </summary>
        private void PlantTree(GameObject root, Vector2 p, AcadianTreeCatalog.Placement placement,
                               Sprite sprite, Material material)
        {
            var go = new GameObject(placement.Species);
            Undo.RegisterCreatedObjectUndo(go, "Plant trees");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(p.x, p.y, 0f);

            var sr = AcadianTreeCatalog.Configure(go, placement, sprite, material, _sortingOrder);

            float s = _baseScale * Mathf.Lerp(1f - _scaleJitter, 1f + _scaleJitter, Hash01(p, 3.71f));
            go.transform.localScale = new Vector3(s, s, 1f);
            sr.color = TintFor(p);
            // No sr.flipX — see the class doc. The key light is baked in.
        }

        /// <summary>
        /// Every (species, variant) sprite the current settings allow: the picked species, or all of
        /// them when mixing, crossed with the enabled variants. A species whose sheet has not been
        /// sliced simply contributes nothing.
        /// </summary>
        private List<(AcadianTreeCatalog.Placement placement, Sprite sprite)> Candidates()
        {
            var list = new List<(AcadianTreeCatalog.Placement, Sprite)>();
            var pool = _mixSpecies ? _trees
                                   : new List<AcadianTreeCatalog.Placement> { CurrentTree() };

            foreach (var placement in pool)
            {
                if (!placement.IsValid) continue;
                int count = AcadianTreeCatalog.VariantCount(placement.Entry);
                for (int v = 0; v < count; v++)
                {
                    if (v < _variants.Length && !_variants[v]) continue;
                    var s = AcadianTreeCatalog.LoadVariant(placement, v);
                    if (s != null) list.Add((placement, s));
                }
            }

            // If every variant was switched off, fall back to variant 0 rather than silently
            // planting nothing and looking broken.
            if (list.Count == 0)
            {
                foreach (var placement in pool)
                {
                    if (!placement.IsValid) continue;
                    var s = AcadianTreeCatalog.LoadVariant(placement, 0);
                    if (s != null) list.Add((placement, s));
                }
            }
            return list;
        }

        /// <summary>Per-tree tint: brightness + warmth variety, multiplied over the sprite's own
        /// colours (the shader multiplies vertex colour), so the art director's palette is shifted,
        /// never flattened. Hashed from POSITION, so it is stable across re-paints.</summary>
        private Color TintFor(Vector2 pos)
        {
            float v = Mathf.Lerp(1f - _valueJitter, 1f + _valueJitter, Hash01(pos, 17.13f));
            float warm = Mathf.Lerp(-_warmthJitter, _warmthJitter, Hash01(pos, 51.71f));
            return new Color(
                Mathf.Clamp01(v + warm),
                Mathf.Clamp01(v),
                Mathf.Clamp01(v - warm * 0.5f),
                1f);
        }

        /// <summary>A stable 0..1 hash of a world position (plus a salt), so per-tree jitter is
        /// reproducible.</summary>
        private static float Hash01(Vector2 p, float salt)
        {
            float h = Mathf.Sin(Vector2.Dot(p, new Vector2(12.9898f, 78.233f)) + salt) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // ============================ HELPERS ============================

        /// <summary>How many trees one dab of the current brush aims at: the per-100 m² rate over the
        /// brush's own area. The min-spacing reject is what actually caps it, so dragging back over
        /// a stand thickens it toward the spacing limit instead of piling trees up.</summary>
        private float TargetPerDab() =>
            _perHundredSquareMetres / 100f * Mathf.PI * _radius * _radius;

        /// <summary>PPU from the bake's own contract, resolved ONCE per Retry — <c>OnGUI</c> repaints
        /// many times a second and <c>TreeKitCatalog.Load</c> re-reads and re-parses the file every
        /// call.</summary>
        private int Ppu => _ppu > 0 ? _ppu : 32;

        [SerializeField] private int _ppu = 32;

        private void ResolveAssets()
        {
            _trees = AcadianTreeCatalog.Scan();
            _labels = new string[_trees.Count];
            for (int i = 0; i < _trees.Count; i++) _labels[i] = _trees[i].Label;

            TreeKitCatalog.Contract contract = TreeKitCatalog.Load();
            _ppu = contract != null && contract.ppu > 0 ? contract.ppu : 32;

            SyncVariantsToSpecies();
        }

        private AcadianTreeCatalog.Placement CurrentTree()
        {
            if (_trees == null || _trees.Count == 0) return default;
            return _trees[Mathf.Clamp(_speciesIndex, 0, _trees.Count - 1)];
        }

        private int VariantCountForUi()
        {
            if (_trees == null || _trees.Count == 0) return 0;
            if (!_mixSpecies) return AcadianTreeCatalog.VariantCount(CurrentTree().Entry);
            int max = 0;
            foreach (var t in _trees) max = Mathf.Max(max, AcadianTreeCatalog.VariantCount(t.Entry));
            return max;
        }

        /// <summary>Keep the variant toggles sized to what the sheets actually hold, defaulting new
        /// ones ON so switching species never silently plants less than the owner asked for.</summary>
        private void SyncVariantsToSpecies()
        {
            int count = Mathf.Max(1, VariantCountForUi());
            var next = new bool[count];
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                next[i] = _variants != null && i < _variants.Length ? _variants[i] : true;
                any |= next[i];
            }
            if (!any) for (int i = 0; i < count; i++) next[i] = true;
            _variants = next;
        }

        private static bool TooClose(Vector2 p, List<Vector2> others, float minSpacing)
        {
            float m2 = minSpacing * minSpacing;
            for (int i = 0; i < others.Count; i++)
                if ((others[i] - p).sqrMagnitude < m2) return true;
            return false;
        }

        private static List<Vector2> GatherPositions(GameObject root, Vector2 center, float range)
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
            Undo.RegisterCreatedObjectUndo(root, "Create PaintedTrees");
            EditorSceneManager.MarkSceneDirty(root.scene);
            return root;
        }

        private static GameObject FindRoot()
        {
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
            if (!EditorUtility.DisplayDialog("Clear painted trees",
                    $"Delete the '{RootName}' object and ALL trees painted under it?", "Delete", "Cancel"))
                return;
            var sc = root.scene;
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(sc);
        }
    }
}
#endif
