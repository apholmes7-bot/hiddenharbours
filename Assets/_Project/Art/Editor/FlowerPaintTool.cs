#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The <b>Flower Paint Tool</b> — a Scene-view brush that scatters the PEI wildflowers, the sibling of
    /// <see cref="GrassPaintTool"/>. Drag over the ground to lay flowers down at a tunable DENSITY; pick the
    /// SPECIES and the TIER (a lone stem, a clump, or a flat patch of ground cover), mix the BLOOM STAGES, and
    /// jitter size and colour — all without leaving the editor. What it paints sways to the real sim wind and
    /// gives underfoot automatically; there is nothing to wire.
    ///
    /// <para><b>Why a sibling window and not a "foliage brush" that does both.</b> The two brushes share a
    /// gesture but almost no settings: grass wants height variants and a green-to-straw slider, flowers want a
    /// species, a tier and bloom stages. Merging them makes one window with two disjoint halves and a mode switch
    /// to get it wrong with. The charter is explicit — build the tool just ahead of the content, not a
    /// speculative generic decor-palette system. If a THIRD foliage brush ever appears, that is the moment to
    /// lift the shared brush loop out; two is not yet a pattern.</para>
    ///
    /// <para><b>What it paints.</b> Real scene GameObjects in the canonical decor shape (a
    /// <see cref="SpriteRenderer"/> on that tier's shared flower material + a <see cref="YSortSprite"/> so they
    /// auto-layer with the player), parented under one <c>PaintedFlowers</c> object, with full Undo. All flowers
    /// of a tier share ONE material so hundreds batch (rule 7). Density is capped by a min-spacing reject, so
    /// dragging back over an area never piles flowers up infinitely.</para>
    ///
    /// <para><b>Jitter is HASHED FROM WORLD POSITION</b>, exactly as the grass tool does it: re-painting or
    /// re-applying colour over the same ground gives the same flowers back, so the knobs are live tuners rather
    /// than a fresh random roll every click.</para>
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Tools ▸ Flower Paint Tool</c>. Scene decor only — nothing saved at
    /// runtime (rule 5), every knob is a field (rule 6).</para>
    /// </summary>
    public sealed class FlowerPaintTool : EditorWindow
    {
        private const string RootName = "PaintedFlowers";

        private enum Mode { Paint, Erase }

        [SerializeField] private bool _painting = true;
        [SerializeField] private Mode _mode = Mode.Paint;
        [SerializeField] private float _radius = 2f;            // brush radius (m)
        [SerializeField] private float _density = 3f;           // target flowers per square metre
        [SerializeField] private float _flow = 0.3f;            // fraction of the target laid per dab

        [SerializeField] private int _speciesIndex;
        [SerializeField] private FlowerCatalog.Tier _tier = FlowerCatalog.Tier.Single;
        [SerializeField] private bool _mixSpecies;              // draw from every species, not just the picked one

        // Bloom stages / variants to mix — index-aligned to the sheet's ROWS. Single has 3, Patch 2, Clump 1.
        [SerializeField] private bool[] _rows = { true, true, true };

        [SerializeField] private float _baseScale = 1f;
        [SerializeField] private float _scaleJitter = 0.12f;
        [SerializeField] private float _valueJitter = 0.1f;     // per-flower brightness variety
        [SerializeField] private float _warmthJitter = 0.04f;   // per-flower warm/cool variety
        [SerializeField] private bool _flipJitter = true;       // mirror half of them, for variety

        [SerializeField] private int _sortingOrder = 3;         // pre-Play default; YSortSprite recomputes from Y

        private List<FlowerCatalog.FlowerSpecies> _species;
        private string[] _speciesLabels;
        private bool _strokeActive;
        private int _undoGroup;

        [MenuItem("Hidden Harbours/Tools/Flower Paint Tool", priority = 42)]
        public static void Open()
        {
            var w = GetWindow<FlowerPaintTool>("Flower Paint");
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
            EditorGUILayout.LabelField("Flower Paint", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag in the Scene view to paint wildflowers. They sway with the real wind and give underfoot " +
                "automatically — press Play to see it. Nothing to wire up.",
                MessageType.Info);

            if (_species == null || _species.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No flower sheets found under {FlowerCatalog.FlowersRoot}. Open Unity so they import, " +
                    "then Retry.", MessageType.Warning);
                if (GUILayout.Button("Retry loading assets")) ResolveAssets();
                return;
            }
            if (FlowerCatalog.MaterialFor(_tier) == null)
            {
                EditorGUILayout.HelpBox(
                    $"The {_tier} flower material is missing ({FlowerCatalog.MaterialPathFor(_tier)}). Open Unity " +
                    "so it imports the flower shader + materials, then Retry.", MessageType.Warning);
                if (GUILayout.Button("Retry loading assets")) ResolveAssets();
                return;
            }

            _painting = EditorGUILayout.ToggleLeft("Paint in Scene view (drag over the ground)", _painting);
            _mode = (Mode)EditorGUILayout.EnumPopup("Mode", _mode);
            _radius = EditorGUILayout.Slider("Brush radius (m)", _radius, 0.25f, 20f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("What to paint", EditorStyles.boldLabel);
            _mixSpecies = EditorGUILayout.ToggleLeft("Mix every species (a wild meadow)", _mixSpecies);
            using (new EditorGUI.DisabledScope(_mixSpecies))
                _speciesIndex = EditorGUILayout.Popup("Species", Mathf.Clamp(_speciesIndex, 0, _species.Count - 1),
                                                      _speciesLabels);

            var newTier = (FlowerCatalog.Tier)EditorGUILayout.EnumPopup("Size", _tier);
            if (newTier != _tier) { _tier = newTier; SyncRowsToTier(); }
            EditorGUILayout.LabelField(TierBlurb(_tier), EditorStyles.miniLabel);

            // The one real hole in the catalog, surfaced rather than hidden: the Lupin colours share ONE patch.
            var picked = CurrentSpecies();
            if (!_mixSpecies && picked != null && !picked.Has(_tier))
                EditorGUILayout.HelpBox($"{picked.Label} has no {_tier} sheet — nothing will be painted. " +
                                        "Pick another size or another species.", MessageType.Warning);
            else if (!_mixSpecies && picked != null && picked.PatchIsShared && _tier == FlowerCatalog.Tier.Patch)
                EditorGUILayout.HelpBox("The lupins share one patch between all four colours (the art director " +
                                        "drew only one), so every lupin patch looks the same. Their Single and " +
                                        "Clump sizes are per-colour.", MessageType.Info);

            EditorGUILayout.Space();
            int rows = FlowerCatalog.GridFor(_tier).Rows;
            if (rows > 1)
            {
                EditorGUILayout.LabelField(RowsLabel(_tier), EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                    for (int r = 0; r < rows; r++)
                        _rows[r] = GUILayout.Toggle(_rows[r], RowName(_tier, r), "Button");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
            _density = EditorGUILayout.Slider("Flowers per m²", _density, 0.2f, 20f);
            _flow = EditorGUILayout.Slider("Flow (build-up per drag)", _flow, 0.05f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variety", EditorStyles.boldLabel);
            _baseScale = EditorGUILayout.Slider("Size", _baseScale, 0.4f, 2f);
            _scaleJitter = EditorGUILayout.Slider("Randomize size (±)", _scaleJitter, 0f, 0.8f);
            _valueJitter = EditorGUILayout.Slider("Brightness variety", _valueJitter, 0f, 0.4f);
            _warmthJitter = EditorGUILayout.Slider("Warm/cool variety", _warmthJitter, 0f, 0.2f);
            _flipJitter = EditorGUILayout.ToggleLeft("Mirror some of them (more variety)", _flipJitter);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select PaintedFlowers")) SelectRoot();
                if (GUILayout.Button("Clear ALL painted flowers")) ClearAll();
            }
        }

        private static string TierBlurb(FlowerCatalog.Tier tier) => tier switch
        {
            FlowerCatalog.Tier.Single => "One slim stem. Bends from its root — the springiest thing out there.",
            FlowerCatalog.Tier.Clump => "A bushy tuft of stems. Bends from its root, but stiffer and calmer.",
            FlowerCatalog.Tier.Patch => "Flat ground cover, seen from above. Shimmers where it lies (no bending).",
            _ => "",
        };

        private static string RowsLabel(FlowerCatalog.Tier tier) =>
            tier == FlowerCatalog.Tier.Single ? "Bloom stages to mix" : "Variants to mix";

        private static string RowName(FlowerCatalog.Tier tier, int row)
        {
            if (tier == FlowerCatalog.Tier.Single)
                return row switch { 0 => "Full bloom", 1 => "Opening", 2 => "Bud", _ => $"Row {row}" };
            return $"Variant {row + 1}";
        }

        // ============================ SCENE-VIEW BRUSH ============================

        private void OnSceneGui(SceneView view)
        {
            if (!_painting || _species == null || _species.Count == 0) return;
            if (FlowerCatalog.MaterialFor(_tier) == null) return;

            Event e = Event.current;
            Vector2 world = WorldUnderMouse(e);

            Handles.color = _mode == Mode.Paint ? new Color(0.9f, 0.7f, 0.95f, 0.9f) : new Color(0.9f, 0.4f, 0.3f, 0.9f);
            Handles.DrawWireDisc(new Vector3(world.x, world.y, 0f), Vector3.forward, _radius);

            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            bool leftButton = e.button == 0 && !e.alt;
            if (leftButton && e.type == EventType.MouseDown)
            {
                _strokeActive = true;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(_mode == Mode.Paint ? "Paint flowers" : "Erase flowers");
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

            var choices = CandidateSprites();
            if (choices.Count == 0) return;   // nothing imported/sliced for this species+tier — say nothing, do nothing

            var material = FlowerCatalog.MaterialFor(_tier);
            var root = GetOrCreateRoot();
            float minSpacing = MinSpacing(_density);
            var nearby = GatherPositions(root, center, _radius + minSpacing);

            int attempts = Mathf.CeilToInt(_density * Mathf.PI * _radius * _radius * Mathf.Clamp01(_flow));
            for (int i = 0; i < attempts; i++)
            {
                Vector2 p = center + Random.insideUnitCircle * _radius;
                if (TooClose(p, nearby, minSpacing)) continue;
                CreateFlower(root, p, choices[Random.Range(0, choices.Count)], material);
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
        /// Every sprite the current settings allow: the picked species (or all of them when mixing) crossed with
        /// the enabled rows, always at column 0 (the neutral drawn pose — the shader picks the pose from there).
        /// A species with no sheet for this tier simply contributes nothing.
        /// </summary>
        private List<Sprite> CandidateSprites()
        {
            var list = new List<Sprite>();
            var pool = _mixSpecies ? _species : new List<FlowerCatalog.FlowerSpecies> { CurrentSpecies() };
            int rows = FlowerCatalog.GridFor(_tier).Rows;

            foreach (var sp in pool)
            {
                if (sp == null || !sp.Has(_tier)) continue;
                string stem = sp.SheetFor(_tier);
                for (int r = 0; r < rows; r++)
                {
                    if (r < _rows.Length && !_rows[r]) continue;
                    var s = FlowerCatalog.LoadNeutral(stem, _tier, r);
                    if (s != null) list.Add(s);
                }
            }

            // If the owner switched every row off, fall back to row 0 rather than silently painting nothing.
            if (list.Count == 0)
            {
                foreach (var sp in pool)
                {
                    if (sp == null || !sp.Has(_tier)) continue;
                    var s = FlowerCatalog.LoadNeutral(sp.SheetFor(_tier), _tier, 0);
                    if (s != null) list.Add(s);
                }
            }

            // When mixing species, LupinPatch would otherwise be added four times (once per colour) and swamp the
            // meadow. Distinct sprites only.
            if (_mixSpecies)
            {
                var seen = new HashSet<Sprite>();
                list.RemoveAll(s => !seen.Add(s));
            }
            return list;
        }

        private void CreateFlower(GameObject root, Vector2 p, Sprite sprite, Material material)
        {
            var go = new GameObject(sprite.name);
            Undo.RegisterCreatedObjectUndo(go, "Paint flowers");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(p.x, p.y, 0f);

            float s = _baseScale * Mathf.Lerp(1f - _scaleJitter, 1f + _scaleJitter, Hash01(p, 3.71f));
            go.transform.localScale = new Vector3(s, s, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = _sortingOrder;
            sr.color = TintFor(p);
            // Mirroring a bottom-centre-pivoted stem about its own root is free variety and costs no draw call
            // (flipX is per-renderer vertex data, not a material change — the batch survives).
            if (_flipJitter) sr.flipX = Hash01(p, 91.3f) > 0.5f;
            go.AddComponent<YSortSprite>();   // auto-layer by world Y (sorts around the player)
        }

        /// <summary>Per-flower tint: brightness + warmth variety, multiplied over the sprite's own colours (the
        /// shader multiplies vertex colour), so the art director's palette is shifted, never flattened. The jitter
        /// is hashed from the flower's POSITION, so it is stable across re-paints.</summary>
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

        /// <summary>A stable 0..1 hash of a world position (plus a salt), so per-flower jitter is reproducible.</summary>
        private static float Hash01(Vector2 p, float salt)
        {
            float h = Mathf.Sin(Vector2.Dot(p, new Vector2(12.9898f, 78.233f)) + salt) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // ============================ HELPERS ============================

        private void ResolveAssets()
        {
            _species = FlowerCatalog.Scan();
            _speciesLabels = new string[_species.Count];
            for (int i = 0; i < _species.Count; i++) _speciesLabels[i] = _species[i].Label;
            SyncRowsToTier();
        }

        private FlowerCatalog.FlowerSpecies CurrentSpecies()
        {
            if (_species == null || _species.Count == 0) return null;
            return _species[Mathf.Clamp(_speciesIndex, 0, _species.Count - 1)];
        }

        /// <summary>Keep the row toggles sized to the tier's row count (Single 3, Patch 2, Clump 1), defaulting
        /// new rows ON so switching size never silently paints less than the owner asked for.</summary>
        private void SyncRowsToTier()
        {
            int rows = FlowerCatalog.GridFor(_tier).Rows;
            var next = new bool[rows];
            for (int i = 0; i < rows; i++) next[i] = _rows != null && i < _rows.Length ? _rows[i] : true;
            bool any = false;
            for (int i = 0; i < rows; i++) any |= next[i];
            if (!any) for (int i = 0; i < rows; i++) next[i] = true;
            _rows = next;
        }

        /// <summary>Target spacing (m) between flowers for the chosen density — caps the per-area count.</summary>
        private static float MinSpacing(float density) => 0.8f / Mathf.Sqrt(Mathf.Max(density, 0.01f));

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
            Undo.RegisterCreatedObjectUndo(root, "Create PaintedFlowers");
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
            if (!EditorUtility.DisplayDialog("Clear painted flowers",
                    $"Delete the '{RootName}' object and ALL flowers painted under it?", "Delete", "Cancel"))
                return;
            var sc = root.scene;
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(sc);
        }
    }
}
#endif
