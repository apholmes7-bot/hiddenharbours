using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Core;   // SortingBands — the one place the sorting-order axis is partitioned

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>THE MEADOW, AS A FIELD RATHER THAN A CROWD.</b> One component per region holds the whole grass
    /// layer: a compact byte plane saying WHAT GROWS WHERE (species + density, as the pass read it off the
    /// ground), and nothing else. Every tuft is derived from that plane at load by
    /// <see cref="GrassFieldScatter"/> and drawn in CHUNKED MESHES — a few dozen renderers instead of tens of
    /// thousands, and zero GameObjects in the saved scene.
    ///
    /// <para><b>⭐ WHAT THIS REPLACED, AND WHY.</b> The grass pass used to emit a GameObject per tuft:
    /// GameObject + Transform + SpriteRenderer + <see cref="YSortSprite"/>, about <b>3.1 KB of scene YAML
    /// each</b>. At the density the owner ratified that is ~27,000 tufts, and St Peters weighed <b>114 MB</b>
    /// against main's 15.8 MB. GitHub's 100 MB file limit is what actually stopped it; rule 7 condemned it
    /// regardless. None of those objects were authored — every one was already a deterministic function of
    /// the painted ground. So the ground's ANSWER is what the scene stores now, and the tufts grow from it.
    /// The field for St Peters is a few tens of kilobytes on ONE line of the scene.</para>
    ///
    /// <para><b>⚠ AND IT CANNOT COME BACK.</b> Every object this component makes carries
    /// <see cref="HideFlags.DontSave"/>. That is not tidiness — it is the structural guarantee: a chunk
    /// cannot be written into a <c>.unity</c> file even if somebody saves the scene with the meadow drawn,
    /// even in edit mode, even by accident. The 114 MB scene is not a mistake this design is capable of
    /// repeating. <c>SceneWeightGuardTests</c> is the tripwire behind it for everything else.</para>
    ///
    /// <para><b>What must survive the change of shape, and does:</b></para>
    /// <list type="bullet">
    /// <item><b>Wind.</b> The tufts still read the shared sim wind through the <c>_WindWorld</c> global
    /// (<see cref="GrassWindBridge"/>) on the same <c>HiddenHarbours/GrassWind</c> material. The shader is
    /// UNCHANGED — not a line — because a batched tuft feeds it exactly the inputs a
    /// <see cref="SpriteRenderer"/> did: the same world-space vertex positions and the same sprite
    /// <c>uv</c>. The bend is therefore identical by construction rather than by resemblance
    /// (<c>GrassFieldWindParityTests</c>).</item>
    /// <item><b>Footstep bend.</b> It needed no per-tuft state to begin with — the shader parts each blade
    /// from the global <c>_GrassTrail</c> using the blade's own world position. A merged mesh carries world
    /// position in exactly the same place, so the trail works with nothing added.</item>
    /// <item><b>The lit-sprite shared path.</b> Untouched. Grass is on its own unlit wind shader and always
    /// was; nothing here forks a third lighting path.</item>
    /// <item><b>Sorting against the player.</b> See <see cref="RowOrderSteps"/> — chunks are ROW-SLICED and
    /// ride ADR 0032's band through <see cref="YSortSprite.OrderFor"/>, so no order is hand-picked.</item>
    /// </list>
    ///
    /// <para><b>Lane &amp; rules.</b> art-pipeline runtime (<c>HiddenHarbours.Art</c>); reads the sim only
    /// through the shader globals the existing bridges publish (rule 4); the field is authored data and the
    /// tufts are recomputed, never saved (rule 5); every knob below is a field (rule 6); the whole point is
    /// rule 7.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class GrassField : MonoBehaviour
    {
        // =====================================================================================
        // THE FIELD — what the scene actually stores
        // =====================================================================================

        [Header("The field (written by the region's grass pass — not by hand)")]
        [Tooltip("World position of the grid's minimum corner: the low-left edge of cell (0,0).")]
        [SerializeField] private Vector2 _originWorld;

        [Tooltip("Grid step in metres — the grain of the field. Site count goes as its INVERSE SQUARE.")]
        [Min(0.01f)] [SerializeField] private float _cellSize = 0.85f;

        [Tooltip("How far a site may wander off its cell centre (m). Held as a ratio of the step by the " +
                 "pass that bakes it: a jitter that does not scale with the step buys holes in the field.")]
        [SerializeField] private float _jitterMetres = 0.35f;

        [Tooltip("Radius (m) of the ring a cell's later sites are offset into, so a cell's tufts read as " +
                 "one clump of growth rather than two stacked sprites.")]
        [SerializeField] private float _spreadMetres = 0.27f;

        [SerializeField] private int _cellsX;
        [SerializeField] private int _cellsY;

        [Tooltip("Sites per cell — the density ceiling.")]
        [Min(1)] [SerializeField] private int _slots = 2;

        [Tooltip("The seed this meadow grows from. Carried HERE, not taken from the player's save, so " +
                 "every machine grows the same meadow. 0 reproduces the shipped scatter exactly.")]
        [SerializeField] private int _seed;

        [Tooltip("The field itself: one byte per site, run-length encoded and Base64'd onto ONE line. " +
                 "See GrassFieldScatter for the bit layout and GrassFieldCodec for the format.")]
        [SerializeField] [TextArea(2, 6)] private string _sitePayload = string.Empty;

        // ---- the straw plane -------------------------------------------------------------------------
        // Tint is a SMOOTH function of exposure, so it is baked as its own COARSE grid and sampled
        // bilinearly rather than stored per site. A smooth signal run-length-encodes badly at full
        // resolution and costs a byte a tuft; at 4 m it is a couple of thousand bytes and the eye cannot
        // tell, because what it drives is a tint multiply over the sprite's own gradient.

        [Header("Straw (the dry-coast bleach, baked coarse and sampled smooth)")]
        [Min(0.5f)] [SerializeField] private float _strawCellSize = 4f;
        [SerializeField] private int _strawCellsX;
        [SerializeField] private int _strawCellsY;
        [SerializeField] [TextArea(2, 4)] private string _strawPayload = string.Empty;

        [Tooltip("The colour the coast bleaches toward, multiplied over each sprite's own dark-to-light " +
                 "gradient (a multiply cannot flatten a gradient, so the drawn shading survives).")]
        [SerializeField] private Color _strawTint = new Color(0.86f, 0.78f, 0.52f, 1f);

        // =====================================================================================
        // THE PALETTE — which baked blade a habitat may wear
        // =====================================================================================
        // ⚠ The SPLIT OF CONCERNS the habitat tag already made, carried across the editor/runtime seam.
        // The scatter knows the GROUND ("this metre is verge, and it wants a broad clump"); the LIBRARY
        // knows the ART. The library is an editor-side manifest, so the pass RESOLVES each habitat's pool
        // once at bake — allies, height classes, broad widths and all — and stores the answer as plain
        // sprite indices. Runtime never needs the manifest, and adding a variant is still a bake.

        [Serializable]
        public sealed class HabitatPool
        {
            [Tooltip("The habitat tag this pool is for — meadow, sward, fringe, dune, headland, verge…")]
            public string Name;

            [Tooltip("Indices into Variants: everything this habitat may wear.")]
            public int[] Pool = Array.Empty<int>();

            [Tooltip("Indices into Variants: the broadest art this habitat carries (two cells or wider). " +
                     "A habitat with no wide bake keeps its normal pool — sparser art beats a bald patch.")]
            public int[] BroadPool = Array.Empty<int>();
        }

        [Header("The palette (resolved from the grass library at bake)")]
        [SerializeField] private Sprite[] _variants = Array.Empty<Sprite>();

        [Tooltip("Index 0 of the field's habitat ids is 'nothing grows here', so this list starts at id 1.")]
        [SerializeField] private HabitatPool[] _habitats = Array.Empty<HabitatPool>();

        [Tooltip("The one material every tuft shares — HiddenHarbours/GrassWind, which is what makes the " +
                 "whole field sway on one global.")]
        [SerializeField] private Material _material;

        // =====================================================================================
        // THE DRAW
        // =====================================================================================

        [Header("The batched draw")]
        [Tooltip("Chunk width in metres along X. Wider = fewer draw calls but coarser culling, so more " +
                 "off-screen geometry rides along with each visible chunk.")]
        [Min(1f)] [SerializeField] private float _chunkWidthMetres = 16f;

        /// <inheritdoc cref="RowOrderSteps"/>
        [Tooltip("How many of ADR 0032's sorting-order steps one chunk ROW collapses into a single draw. " +
                 "1 = bit-identical sorting to the old sprite-per-tuft meadow (and the most draw calls); " +
                 "4 = one metre of world Y per row, the shipped default. This is the ONE knob that trades " +
                 "draw calls against how far the player can be mis-sorted against the grass.")]
        [Min(1)] [SerializeField] private int _rowOrderSteps = 4;

        [Tooltip("Metres of slack added to every chunk's bounds so wind-displaced blades are not culled " +
                 "at the screen edge (the shader moves vertices the CPU-side bounds cannot see).")]
        [Min(0f)] [SerializeField] private float _windBoundsMargin = 1f;

        [Tooltip("Draw the meadow in the Scene view while authoring. The chunks are never saved either " +
                 "way; turn this off if a huge field makes the editor sluggish.")]
        [SerializeField] private bool _previewInEditor = true;

        // =====================================================================================
        // runtime state (none of it serialized)
        // =====================================================================================

        static readonly int IdMainTex = Shader.PropertyToID("_MainTex");

        readonly List<GameObject> _chunkObjects = new List<GameObject>();
        readonly List<Mesh> _chunkMeshes = new List<Mesh>();

        /// <summary>
        /// ⚠ Declared bare and created LAZILY, never as a field initializer. Unity throws
        /// <c>CreateImpl is not allowed to be called from a MonoBehaviour constructor (or instance field
        /// initializer)</c> for anything engine-backed built at construction — so
        /// <c>= new MaterialPropertyBlock()</c> here leaves the field null and every chunk then dies on a
        /// NullReferenceException. The same shape <see cref="TerrainSplatSurface"/> and
        /// <see cref="SpriteLightBinder"/> already use.
        /// </summary>
        MaterialPropertyBlock _mpb;

        int _builtTufts;
        int _builtVertices;

        /// <summary>Chunk renderers currently standing. The draw-call number, and what the measurement
        /// harness reports against the old renderer-per-tuft count.</summary>
        public int ChunkCount => _chunkObjects.Count;

        /// <summary>Tufts actually built into meshes — the meadow's real size.</summary>
        public int TuftCount => _builtTufts;

        /// <summary>Vertices across every chunk. The memory number: this is what the batched path costs
        /// instead of a SpriteRenderer per tuft.</summary>
        public int VertexCount => _builtVertices;

        /// <summary>
        /// How many of ADR 0032's order steps one chunk row collapses into one draw — <b>the one open
        /// design point of this change, made into a knob rather than an argument.</b>
        ///
        /// <para>A renderer has ONE sorting order, so N distinct orders need N renderers. The old meadow
        /// had one renderer per tuft and therefore every order it wanted. A chunk covering a band of world
        /// Y has to pick one, and every tuft in the band takes it — so the player standing in that band
        /// draws in front of, or behind, the whole row.</para>
        ///
        /// <para><b>The row height is exactly <c>this / <see cref="SortingBands.OrdersPerMetre"/></c>
        /// metres</b>, which makes the trade legible: at <b>1</b> the row is 0.25 m, the same quantum
        /// <see cref="YSortSprite"/> already rounded every tuft onto, so sorting is BIT-IDENTICAL to the old
        /// meadow and the only cost is draw calls. At the shipped <b>4</b> the row is one metre: a tuft can
        /// be mis-sorted against the player by at most a metre, which at ankle-to-knee height reads as grass
        /// in front of their boots — and buys a four-fold cut in rows on screen.</para>
        /// </summary>
        public int RowOrderSteps => _rowOrderSteps;

        /// <summary>The height of one chunk row in metres — derived from <see cref="RowOrderSteps"/> and
        /// ADR 0032's band, never picked (rule 6). Also the worst-case sorting error against the player.</summary>
        public float RowHeightMetres => _rowOrderSteps / SortingBands.OrdersPerMetre;

        /// <summary>
        /// Which chunk row a world Y belongs to.
        ///
        /// <para><b>⚠ ROUND, NOT FLOOR — and that is the whole of it.</b> <see cref="YSortSprite.OrderFor"/>
        /// is <c>round(base − y·perUnit)</c>, so the bands of world Y it maps onto a single order are
        /// <c>1/perUnit</c> metres wide and CENTRED on the lattice <c>y = k/perUnit</c> — their edges fall
        /// half a step off the multiples of that step. Bucketing rows with <c>floor</c> puts the row edges
        /// on the multiples instead, i.e. exactly half a row out of phase with the rounding the band
        /// already does. Measured over 100 m of world Y at the shipped band: a floor-bucketed row disagreed
        /// with <c>OrderFor</c> on <b>50%</b> of positions. Rounding here puts the two conventions in
        /// phase, and the disagreement goes to zero.</para>
        ///
        /// <para>This is why the row is anchored at <see cref="RowAnchorY"/> — its lattice point — rather
        /// than at its centre: the lattice point is the Y whose order the whole row shares.</para>
        /// </summary>
        public static int RowOf(float worldY, float rowHeightMetres) =>
            Mathf.RoundToInt(worldY / Mathf.Max(rowHeightMetres, 1e-3f));

        /// <summary>The world Y a chunk row takes its sorting order from — the row's own lattice point.
        /// See <see cref="RowOf"/> for why this is the lattice point and not the centre.</summary>
        public static float RowAnchorY(int row, float rowHeightMetres) => row * rowHeightMetres;

        /// <summary>The field's shape, as the pure scatter wants it.</summary>
        public GrassFieldLayout Layout => new GrassFieldLayout
        {
            OriginX = _originWorld.x,
            OriginY = _originWorld.y,
            CellSize = _cellSize,
            JitterMetres = _jitterMetres,
            SpreadMetres = _spreadMetres,
            CellsX = _cellsX,
            CellsY = _cellsY,
            Slots = _slots,
            Seed = _seed,
        };

        /// <summary>The habitat table, id 1 first (id 0 is "nothing grows here").</summary>
        public IReadOnlyList<HabitatPool> Habitats => _habitats;

        /// <summary>The sprite palette every pool indexes.</summary>
        public IReadOnlyList<Sprite> Variants => _variants;

        /// <summary>Base64 characters the field costs the scene. The number to quote after a bake — and
        /// the one to compare against the 98 MB of GameObjects it replaced.</summary>
        public int PayloadCharacters =>
            GrassFieldCodec.StoredSize(_sitePayload) + GrassFieldCodec.StoredSize(_strawPayload);

        // =====================================================================================
        // authoring entry point (the bake's only door in)
        // =====================================================================================

        /// <summary>
        /// Replace this field wholesale — what a region's grass pass calls instead of making GameObjects.
        ///
        /// <para><b>⚠ IDEMPOTENT BY CONSTRUCTION (ADR 0019).</b> It ASSIGNS the whole field rather than
        /// adding to it, so running the pass twice writes the same bytes twice and the second run is a
        /// no-op. That is the trap that doubled the owner's scene stated as a mechanism: the old brush
        /// APPENDED objects and rejected on min-spacing, so a second stroke over the same ground laid more
        /// grass. A field cannot append — there is one byte per site and writing it again writes the same
        /// value.</para>
        /// </summary>
        public void SetField(
            Vector2 originWorld, float cellSize, float jitterMetres, float spreadMetres,
            int cellsX, int cellsY, int slots, int seed, byte[] sitePlane,
            float strawCellSize, int strawCellsX, int strawCellsY, byte[] strawPlane, Color strawTint,
            Sprite[] variants, HabitatPool[] habitats, Material material)
        {
            if (sitePlane != null && sitePlane.Length != cellsX * cellsY * slots)
                throw new ArgumentException(
                    $"The site plane is {sitePlane.Length} bytes but the grid is {cellsX}x{cellsY} with " +
                    $"{slots} slots ({cellsX * cellsY * slots} bytes). A field that does not match its own " +
                    "grid would draw a meadow shifted off the ground it was gated against.");

            _originWorld = originWorld;
            _cellSize = cellSize;
            _jitterMetres = jitterMetres;
            _spreadMetres = spreadMetres;
            _cellsX = cellsX;
            _cellsY = cellsY;
            _slots = slots;
            _seed = seed;
            _sitePayload = GrassFieldCodec.Encode(sitePlane);

            _strawCellSize = strawCellSize;
            _strawCellsX = strawCellsX;
            _strawCellsY = strawCellsY;
            _strawPayload = GrassFieldCodec.Encode(strawPlane);
            _strawTint = strawTint;

            _variants = variants ?? Array.Empty<Sprite>();
            _habitats = habitats ?? Array.Empty<HabitatPool>();
            if (material != null) _material = material;

            Rebuild();
        }

        // =====================================================================================
        // deriving the tufts
        // =====================================================================================

        /// <summary>One derived tuft: everything the mesh builder needs and nothing that has to be stored.</summary>
        public struct Blade
        {
            public Vector2 Position;
            public int Variant;      // index into Variants
            public float Scale;
            public bool Mirror;
            public Color Tint;
        }

        /// <summary>
        /// Grow the meadow: every tuft this field describes, in cell order.
        ///
        /// <para>Pure in (field bytes, seed) — no <c>Random</c>, no <c>Time</c>, no scene. Called at load,
        /// and by the tests that pin it: <c>GrassFieldScatterTests</c> derives twice and requires the two
        /// runs to be identical tuft for tuft, because "deterministic" that is only true on the machine that
        /// baked it is not determinism.</para>
        /// </summary>
        public List<Blade> DeriveBlades()
        {
            var blades = new List<Blade>();
            if (_cellsX <= 0 || _cellsY <= 0 || _slots <= 0) return blades;

            byte[] sites = GrassFieldCodec.Decode(_sitePayload);
            if (sites.Length == 0) return blades;
            if (sites.Length != _cellsX * _cellsY * _slots)
                throw new InvalidOperationException(
                    $"This grass field's payload decodes to {sites.Length} bytes but its grid asks for " +
                    $"{_cellsX * _cellsY * _slots}. The field and its grid were written by different runs — " +
                    "re-run the region's grass pass.");

            byte[] straw = GrassFieldCodec.Decode(_strawPayload);
            var layout = Layout;

            // ⚠ COLUMN-MAJOR, matching the order the region's pass walks its grid in. Nothing about the
            // picture depends on it — but a derived meadow and a freshly scattered one then line up index
            // for index, which is what lets the migration test compare them tuft by tuft instead of
            // sorting two lists of 27,000 and hoping.
            for (int ix = 0; ix < _cellsX; ix++)
            for (int iy = 0; iy < _cellsY; iy++)
            {
                int cellBase = (iy * _cellsX + ix) * _slots;
                for (int s = 0; s < _slots; s++)
                {
                    byte site = sites[cellBase + s];
                    if (GrassFieldScatter.IsEmpty(site)) continue;

                    int habitatId = GrassFieldScatter.HabitatOf(site);
                    int[] pool = PoolFor(habitatId, GrassFieldScatter.BroadOf(site));
                    if (pool == null || pool.Length == 0) continue;

                    Vector2 p = GrassFieldScatter.SlotPosition(layout, ix, iy, s);
                    float shape = GrassFieldScatter.ShapeRoll(layout, ix, iy, s);
                    int roll = GrassFieldScatter.VariantRoll(layout, ix, iy, s);

                    blades.Add(new Blade
                    {
                        Position = p,
                        Variant = pool[roll % pool.Length],
                        Scale = Mathf.Lerp(ScaleMin, ScaleMax, shape),
                        Mirror = GrassFieldScatter.Mirrored(layout, ix, iy, s),
                        Tint = GrassFieldScatter.TintFor(_strawTint, SampleStraw(straw, p), shape),
                    });
                }
            }
            return blades;
        }

        /// <summary>Tuft scale range, hashed per site — the same 0.85–1.25 the object pass used, so the
        /// derived meadow has the height variety the owner already ratified.</summary>
        public const float ScaleMin = 0.85f;

        /// <inheritdoc cref="ScaleMin"/>
        public const float ScaleMax = 1.25f;

        int[] PoolFor(int habitatId, bool broad)
        {
            int i = habitatId - 1;                       // id 0 is "nothing grows here"
            if (i < 0 || i >= _habitats.Length) return null;
            var h = _habitats[i];
            if (h == null) return null;
            // A habitat with no wide bake keeps its normal pool: sparser art beats a bald patch the owner
            // has to diagnose from a screenshot.
            if (broad && h.BroadPool != null && h.BroadPool.Length > 0) return h.BroadPool;
            return h.Pool;
        }

        /// <summary>
        /// The straw at a world point, bilinear across the coarse plane. Off the plane (or with no plane
        /// baked) the answer is 0 — lush green, the shipped look — so a field baked before the straw plane
        /// existed still draws, just without the coastal bleach.
        /// </summary>
        float SampleStraw(byte[] plane, Vector2 world)
        {
            if (plane == null || plane.Length == 0 || _strawCellsX <= 0 || _strawCellsY <= 0) return 0f;

            float fx = (world.x - _originWorld.x) / Mathf.Max(_strawCellSize, 1e-3f) - 0.5f;
            float fy = (world.y - _originWorld.y) / Mathf.Max(_strawCellSize, 1e-3f) - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;

            float s00 = StrawAt(plane, x0, y0), s10 = StrawAt(plane, x0 + 1, y0);
            float s01 = StrawAt(plane, x0, y0 + 1), s11 = StrawAt(plane, x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(s00, s10, tx), Mathf.Lerp(s01, s11, tx), ty);
        }

        float StrawAt(byte[] plane, int x, int y)
        {
            x = Mathf.Clamp(x, 0, _strawCellsX - 1);     // clamp, not wrap: the coast must not bleach the
            y = Mathf.Clamp(y, 0, _strawCellsY - 1);     // far side of the island
            int i = y * _strawCellsX + x;
            return (uint)i < (uint)plane.Length ? plane[i] / 255f : 0f;
        }

        // =====================================================================================
        // building the chunks
        // =====================================================================================

        void OnEnable()
        {
            if (!Application.isPlaying && !_previewInEditor) { Clear(); return; }
            // Already grown and merely parked by a region toggle — wake it rather than regrow 27,000 tufts.
            if (_chunkObjects.Count > 0)
            {
                for (int i = 0; i < _chunkObjects.Count; i++)
                    if (_chunkObjects[i] != null) _chunkObjects[i].SetActive(true);
                return;
            }
            Rebuild();
        }

        /// <summary>Park the meadow rather than destroy it — a region toggle's <c>SetActive</c> should not
        /// pay to regrow 27,000 tufts. <see cref="OnDestroy"/> is where the objects actually go.</summary>
        void OnDisable()
        {
            for (int i = 0; i < _chunkObjects.Count; i++)
                if (_chunkObjects[i] != null) _chunkObjects[i].SetActive(false);
        }

        void OnDestroy() => Clear();

        /// <summary>
        /// ⚠ <b>No object creation in here.</b> Unity forbids <c>DestroyImmediate</c> during
        /// <c>OnValidate</c>, and rebuilding means destroying the old chunks — so the clamp happens now and
        /// the regrow is deferred one editor tick, where it is legal. The same idiom
        /// <see cref="YSortSprite"/> uses to re-arm itself. Never runs in a build.
        /// </summary>
        void OnValidate()
        {
            _rowOrderSteps = Mathf.Max(1, _rowOrderSteps);
            _chunkWidthMetres = Mathf.Max(1f, _chunkWidthMetres);
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled) Rebuild();
            };
#endif
        }

        /// <summary>
        /// Tear the drawn meadow down and grow it again from the field. Cheap enough to call from a knob;
        /// the expensive half is deriving the tufts, which is a hash per site.
        /// </summary>
        [ContextMenu("Rebuild the meadow")]
        public void Rebuild()
        {
            Clear();
            if (!Application.isPlaying && !_previewInEditor) return;
            if (_material == null || _variants == null || _variants.Length == 0) return;

            // The grid is authored in WORLD space, and the shader reads world position — so a rotated or
            // scaled host would draw a meadow that no longer stands on the ground it was gated against.
            // Say so rather than let it be diagnosed from a screenshot.
            if (transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one)
                Debug.LogWarning(
                    $"[GrassField] {name} is rotated or scaled. The grass field is authored in world space, " +
                    "so the meadow will not line up with the ground the pass gated it against. Reset this " +
                    "object's rotation and scale.", this);

            List<Blade> blades;
            try { blades = DeriveBlades(); }
            catch (Exception e)
            {
                // A corrupt or stale field must name itself, not silently draw a bald island.
                Debug.LogError($"[GrassField] {name}: {e.Message}", this);
                return;
            }
            if (blades.Count == 0) return;

            float rowHeight = Mathf.Max(RowHeightMetres, 1e-3f);
            var byChunk = new Dictionary<(int col, int row), List<Blade>>();
            for (int i = 0; i < blades.Count; i++)
            {
                var b = blades[i];
                // X buckets by floor (columns are a culling convenience and carry no sorting meaning);
                // Y buckets by RowOf, which is in phase with the band's own rounding. See RowOf.
                var key = (Mathf.FloorToInt(b.Position.x / _chunkWidthMetres),
                           RowOf(b.Position.y, rowHeight));
                if (!byChunk.TryGetValue(key, out var list)) byChunk[key] = list = new List<Blade>();
                list.Add(b);
            }

            foreach (var kv in byChunk) BuildChunk(kv.Key.col, kv.Key.row, kv.Value, rowHeight);
            _builtTufts = blades.Count;
        }

        /// <summary>
        /// Split one chunk's blades by TEXTURE, then build a mesh per group.
        ///
        /// <para><b>⚠ THIS IS CORRECTNESS, NOT TUNING.</b> A mesh binds ONE texture, and the grass library
        /// is 29 separate PNGs — merge two variants into one mesh and half the tufts draw with the wrong
        /// blade's pixels. So a chunk is (chunk × texture), and the draw count is
        /// <c>Σ chunks (distinct textures in that chunk)</c>.</para>
        ///
        /// <para><b>⚠ AND NO, NOT A SPRITE ATLAS — the ban is real and it is not about taste.</b> The
        /// obvious fix is to pack the 29 PNGs onto one page so a chunk collapses to a single draw. An atlas
        /// remaps every sprite's uv into a page rectangle, and this shader bends the blade by
        /// <c>IN.uv.y</c> — "how far up this blade" would become "how far up the page", so every tuft would
        /// bend by an arbitrary constant. Same texture-space-uv trap as the shadow-shear defect of #431.
        /// The lever that IS available is <see cref="RowOrderSteps"/>: it cuts the number of BANDS a screen
        /// of grass is chopped into, which is the multiplier that actually hurts.</para>
        ///
        /// <para>All the groups of one chunk share the row's sorting order and therefore TIE — resolved by
        /// draw order, exactly as several textures inside one sorting band tie today. No behaviour
        /// changes.</para>
        /// </summary>
        void BuildChunk(int col, int row, List<Blade> blades, float rowHeight)
        {
            var byTexture = new Dictionary<Texture, List<Blade>>();
            for (int i = 0; i < blades.Count; i++)
            {
                var b = blades[i];
                if ((uint)b.Variant >= (uint)_variants.Length) continue;
                var sprite = _variants[b.Variant];
                if (sprite == null || sprite.texture == null) continue;   // un-imported PNG: goes unplanted
                if (!byTexture.TryGetValue(sprite.texture, out var list))
                    byTexture[sprite.texture] = list = new List<Blade>();
                list.Add(b);
            }

            int textureGroup = 0;
            foreach (var kv in byTexture)
                BuildChunkGroup(col, row, textureGroup++, kv.Key, kv.Value, rowHeight);
        }

        /// <summary>Destroy every chunk this component made. Meshes are destroyed too — an orphaned Mesh
        /// leaks for the lifetime of the process, and this component makes hundreds of them.</summary>
        public void Clear()
        {
            for (int i = 0; i < _chunkObjects.Count; i++) DestroyAnywhere(_chunkObjects[i]);
            for (int i = 0; i < _chunkMeshes.Count; i++) DestroyAnywhere(_chunkMeshes[i]);
            _chunkObjects.Clear();
            _chunkMeshes.Clear();
            _builtTufts = 0;
            _builtVertices = 0;
        }

        static void DestroyAnywhere(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void BuildChunkGroup(int col, int row, int textureGroup, Texture texture, List<Blade> blades,
                             float rowHeight)
        {
            // The chunk's own origin, so vertices are LOCAL and the chunk transform carries the world
            // offset. That is what keeps the mesh's numbers small and, more importantly, what makes
            // GetVertexPositionInputs in the grass shader produce the same world position it produced for
            // a SpriteRenderer — the parity the wind depends on.
            var origin = new Vector3(col * _chunkWidthMetres, RowAnchorY(row, rowHeight), 0f);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color32>();
            var tris = new List<int>();

            for (int i = 0; i < blades.Count; i++)
                AppendSprite(_variants[blades[i].Variant], blades[i], origin, verts, uvs, colors, tris);
            if (verts.Count == 0) return;

            var mesh = new Mesh { name = $"GrassChunk_c{col}_r{row}_t{textureGroup}", hideFlags = HideFlags.DontSave };
            // 16-bit indices top out at 65,535 vertices. A dense chunk at a wide chunk width can pass that,
            // and the failure mode is silent corruption, so widen the format rather than hope.
            if (verts.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            // ⚠ Grow the bounds by the wind's reach. The shader displaces vertices in the VERTEX stage,
            // which the CPU-side bounds cannot see, so a chunk whose blades are leaning on screen can be
            // culled by its un-leaned bounds and pop out at the screen edge.
            var bounds = mesh.bounds;
            bounds.Expand(_windBoundsMargin * 2f);
            mesh.bounds = bounds;

            var go = new GameObject($"GrassChunk_c{col}_r{row}_t{textureGroup}") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, worldPositionStays: false);
            // ⚠ WORLD position, not local. The chunk key comes from the blades' WORLD coordinates (the
            // field's grid is in world space), so parking the chunk at that origin as a LOCAL offset would
            // shift the entire meadow by wherever the owner happened to drag the IslandGrass object — and
            // the grass would silently stop lining up with the ground it was gated against.
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            // ONE shared material for the whole meadow; the texture rides a MaterialPropertyBlock. The
            // alternative — a Material instance per texture — is 29 materials that drift apart the first
            // time somebody tunes a sway value, which is exactly what the shared Grass.mat exists to stop.
            mr.sharedMaterial = _material;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.Clear();
            _mpb.SetTexture(IdMainTex, texture);
            mr.SetPropertyBlock(_mpb);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;

            // THE ROW'S PLACE IN THE BAND. Derived through the very mapping every Y-sorted sprite uses, at
            // the row's own LATTICE POINT — the Y whose order the whole row shares (see RowOf). At one
            // order step per row that reproduces the old sprite-per-tuft meadow's order for every tuft in
            // the row; at four it is the order a tuft standing on the row's lattice line would have had.
            // No hand-picked order anywhere (ADR 0032).
            int order = YSortSprite.OrderFor(
                RowAnchorY(row, rowHeight),
                SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                SortingBands.DecorFloor, SortingBands.DecorCeiling);

            // Mesh renderers do not compete with sprites on sortingOrder alone — they fall back to world z.
            // The SortingGroup ("sort as 2D") is the documented workaround this project already uses for
            // the splat ground, the displaced water and the rain (ADR 0023).
            var sortingGroup = go.AddComponent<SortingGroup>();
            sortingGroup.sortingOrder = order;
            mr.sortingOrder = order;

            _chunkObjects.Add(go);
            _chunkMeshes.Add(mesh);
            _builtVertices += verts.Count;
        }

        /// <summary>
        /// Append one tuft's geometry to a chunk mesh.
        ///
        /// <para><b>⚠ THE SPRITE'S OWN MESH, NOT A QUAD — and this is load-bearing, not tidiness.</b> The
        /// grass shader bends by <c>uv.y²</c> so the base stays rooted and the tip moves most. On a
        /// four-vertex FullRect quad that shaping is evaluated at uv.y 0 and 1 only and interpolates
        /// linearly between them, so the SQUARING DOES NOTHING (0² = 0, 1² = 1) — the exact defect
        /// <c>WindBendTessellationTests</c> pins, which the trees had and the grass did not. The tufts
        /// import Tight, so <see cref="Sprite.vertices"/> is a tessellated silhouette and the shaping
        /// survives. Harvesting the sprite's own mesh is therefore the only way a batched tuft bends like
        /// the sprite it replaces.</para>
        ///
        /// <para><b>⚠ And it is <see cref="Sprite.vertices"/>, deliberately.</b> A <c>SpriteRenderer</c>'s
        /// mesh arrives at the GPU already in WORLD space with an identity object-to-world matrix — harvest
        /// THAT and every tuft lands at the world origin of whatever scene it was captured in.
        /// <c>Sprite.vertices</c> is the sprite's own local geometry in metres about its pivot, which is
        /// what a merged mesh wants.</para>
        ///
        /// <para><b>Mirroring negates local X</b> rather than flipping uv, so the silhouette mirrors and not
        /// just the texture — the same picture <c>SpriteRenderer.flipX</c> drew. It inverts triangle
        /// winding, which is free here: the grass shader is <c>Cull Off</c>.</para>
        /// </summary>
        static void AppendSprite(Sprite sprite, in Blade blade, Vector3 chunkOrigin,
                                 List<Vector3> verts, List<Vector2> uvs, List<Color32> colors,
                                 List<int> tris)
        {
            var sv = sprite.vertices;
            var su = sprite.uv;
            var st = sprite.triangles;
            if (sv == null || su == null || st == null || sv.Length == 0 || sv.Length != su.Length) return;

            int base0 = verts.Count;
            float mirror = blade.Mirror ? -1f : 1f;
            float x0 = blade.Position.x - chunkOrigin.x;
            float y0 = blade.Position.y - chunkOrigin.y;
            Color32 tint = blade.Tint;

            for (int v = 0; v < sv.Length; v++)
            {
                verts.Add(new Vector3(x0 + sv[v].x * blade.Scale * mirror,
                                      y0 + sv[v].y * blade.Scale,
                                      0f));
                uvs.Add(su[v]);
                colors.Add(tint);
            }
            for (int t = 0; t < st.Length; t++) tris.Add(base0 + st[t]);
        }
    }
}
