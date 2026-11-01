using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    public sealed class PlantField : MonoBehaviour
    {
        public PlantFieldDefinition Definition;
        public MonoBehaviour GroundSource;
        public Vector2 Origin;
        public int Width, Height, Seed = 9109;
        [Min(0.1f)] public float CellSize = 0.5f;
        [Min(0.1f)] public float GroundMetresPerY = 1.5557238f;
        [Min(1)] public float ChunkWidth = 8;
        [Min(0.1f)] public float SlowTickSeconds = 0.5f;
        public float HeightProjection = 0.76604444f, WindMargin = 0.75f;
        [Min(0)] public float SunShadowStrength = 1;
        public int SubmergedOrder = -8;
        public BiomeRecipeDef[] Recipes = Array.Empty<BiomeRecipeDef>();
        public PlantSpeciesDef[] Species = Array.Empty<PlantSpeciesDef>();
        public Material Material;

        public int PlantCount => _sites.Count;
        public int ChunkCount => _chunks.Count;
        public GameObject ShadowRoot => _shadowRoot;
        public int ShadowDrawCount
        {
            get
            {
                if (_shadowRoot == null || !_shadowRoot.activeSelf) return 0;
                int count = 0;
                foreach (var chunk in _shadowChunks.Values) if (chunk.Renderer.enabled) count++;
                return count;
            }
        }
        public int MissingCells { get; private set; }
        public int DormantCells { get; private set; }
        public int AuthoredPlantCount => Definition != null ? Definition.Authored.Length : 0;
        public int CurrentSeason => _season;
        public void CopyGrassRootsTo(List<Vector2> destination)
        {
            foreach (var site in _sites)
                if (site.Def.Family == "grass") destination.Add(new Vector2(site.X, site.Y));
        }
        public string EncodeCurrentField() => PlantFieldCodec.Encode(_plane);
        readonly List<Site> _sites = new List<Site>();
        readonly Dictionary<(int, int, int, int), (GameObject go, Mesh mesh)> _chunks = new Dictionary<(int, int, int, int), (GameObject, Mesh)>();
        readonly PlantSpeciesDef[] _codes = new PlantSpeciesDef[64];
        readonly HashSet<(int, int, int, int)> _dirty = new HashSet<(int, int, int, int)>();
        readonly Dictionary<(int, int, int, int, int), PlantCell> _cells = new Dictionary<(int, int, int, int, int), PlantCell>();
        // The atlas bake uses 32 pixels per metre, as the standing chunks below do.
        const float PlantPixelsPerMetre = 32;
        readonly Dictionary<(int, int, int), ShadowChunk> _shadowChunks = new Dictionary<(int, int, int), ShadowChunk>();
        readonly HashSet<(int, int, int)> _shadowDirty = new HashSet<(int, int, int)>();
        static readonly int SunDirectionId = Shader.PropertyToID("_SunDir"), SunElevationId = Shader.PropertyToID("_SunElevation");
        static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength"), ShadowDirectionId = Shader.PropertyToID("_ShadowDir");
        static readonly int ShadowLengthId = Shader.PropertyToID("_ShadowLen"), ShadowColorId = Shader.PropertyToID("_ShadowColor");
        static readonly int MainTextureId = Shader.PropertyToID("_MainTex"), ShadowUvPerUnitId = Shader.PropertyToID("_ShadowUVPerUnit");
        static readonly int SwayId = Shader.PropertyToID("_Sway");
        GameObject _shadowRoot;
        Material _shadowMaterial;
        bool _shadowMeshes;
        IPlantFieldGround _ground;
        byte[] _plane = Array.Empty<byte>();
        float _nextTick;
        int _season = -1;
        sealed class Site
        {
            public float X, Y, Z;
            public int Stage, Variant, Tide, Order;
            public PlantSpeciesDef Def;
            public PlantCell Cell;
            public bool CastsSun;
        }
        sealed class ShadowChunk
        {
            public readonly List<Site> Sites = new List<Site>();
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<Vector4> Roots = new List<Vector4>(), Cells = new List<Vector4>();
            public readonly List<int> Triangles = new List<int>();
            public Mesh Mesh;
            public MeshRenderer Renderer;
        }

        // Root coverage is deliberately stricter than full-height submerged sorting.
        public static bool CanCastSun(PlantCell cell, float waterOverRoot) =>
            cell != null && !cell.Dormant && waterOverRoot <= 0;

        void Start() { if (GroundSource != null) Rebuild(); }
        void OnEnable() { if (_ground != null) Rebuild(); }
        void OnDisable() { ClearChunks(); ClearShadows(); }
        void OnDestroy() { ClearShadows(); }

        // Called once after a region has made its terrain available, or after an explicit edit.
        public void Rebuild() => Rebuild(true);
        public void Rebuild(bool createMeshes)
        {
            ClearShadows();
            if (Definition != null)
            {
                Origin = Definition.Origin; Width = Definition.Width; Height = Definition.Height; Seed = Definition.Seed;
                CellSize = Definition.CellSize; GroundMetresPerY = Definition.GroundMetresPerY;
                ChunkWidth = Definition.ChunkWidth; SlowTickSeconds = Definition.SlowTickSeconds;
                HeightProjection = Definition.HeightProjection; WindMargin = Definition.WindMargin;
                SunShadowStrength = Definition.SunShadowStrength;
                SubmergedOrder = Definition.SubmergedOrder; Recipes = Definition.Recipes;
                Species = Definition.Species; Material = Definition.Material;
            }
            _ground = GroundSource as IPlantFieldGround;
            if (_ground == null) throw new InvalidOperationException("Plant field needs IPlantFieldGround");
            if (Width < 1 || Height < 1 || (long)Width * Height > PlantFieldCodec.MaxCells || CellSize <= 0 || GroundMetresPerY <= 0 || ChunkWidth < 1)
                throw new InvalidOperationException("Invalid plant field grid");
            Array.Clear(_codes, 0, _codes.Length);
            _cells.Clear();
            foreach (var sp in Species)
            {
                if (sp == null || sp.Code < 1 || sp.Code > 63 || _codes[sp.Code] != null) throw new InvalidOperationException("Invalid plant code table");
                _codes[sp.Code] = sp;
                foreach (var cell in sp.Cells)
                    _cells.Add((sp.Code, cell.Stage, cell.Season, cell.Variant, cell.Tide), cell);
            }
            _sites.Clear(); _plane = new byte[Width * Height];
            _season = _ground.PlantSeason; MissingCells = 0; DormantCells = 0;
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                float px = Origin.x + (x + 0.5f) * CellSize + PlantFieldScatter.Offset(x, y, Seed, false, CellSize);
                float py = Origin.y + (y + 0.5f) * CellSize + PlantFieldScatter.Offset(x, y, Seed, true, CellSize);
                var g = _ground.Sample(px, py);
                if (g.Excluded || g.Recipe < 0) continue;
                if (g.Recipe >= Recipes.Length || Recipes[g.Recipe] == null) throw new InvalidOperationException("Unknown biome recipe");
                var recipe = Recipes[g.Recipe];
                byte code = PlantFieldScatter.Choose(recipe.ForMaterial(g.Material), g.Elevation,
                    CellSize * CellSize * GroundMetresPerY, x, y, Seed, recipe.YoungChance);
                _plane[y * Width + x] = code; if (code == 0) continue;
                var def = _codes[PlantFieldScatter.Species(code)];
                if (def == null) throw new InvalidOperationException("Unbound plant code");
                var s = new Site { X = px, Y = py, Z = g.Elevation, Def = def,
                    Stage = PlantFieldScatter.Stage(code), Variant = PlantFieldScatter.Variant(x, y, Seed) };
                RefreshSite(s); _sites.Add(s);
            }
            if (Definition != null)
            {
                var ids = new HashSet<string>();
                foreach (var row in Definition.Authored)
                {
                    if (row == null || string.IsNullOrEmpty(row.Id) || !ids.Add(row.Id) ||
                        row.Species < 1 || row.Species >= _codes.Length || _codes[row.Species] == null ||
                        row.Stage < 0 || row.Stage > 2 || row.Variant < 0 || row.Variant > 3)
                        throw new InvalidOperationException("Invalid authored plant site");
                    var g = _ground.Sample(row.At.x, row.At.y);
                    var site = new Site { X = row.At.x, Y = row.At.y, Z = g.Elevation,
                        Def = _codes[row.Species], Stage = row.Stage, Variant = row.Variant };
                    RefreshSite(site); _sites.Add(site);
                }
            }
            if (createMeshes) BuildChunks(); else ClearChunks();
            if (createMeshes && SunShadowStrength > 0) BuildShadows();
            _nextTick = Time.unscaledTime + SlowTickSeconds;
        }
        void Update()
        {
            if (_ground == null || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + SlowTickSeconds;
            RefreshAppearance();
        }
        public void RefreshAppearance()
        {
            if (_ground == null) return;
            _season = _ground.PlantSeason;
            _dirty.Clear();
            _shadowDirty.Clear();
            foreach (var s in _sites)
            {
                var old = Key(s);
                var oldShadow = ShadowKey(s);
                var oldCell = s.Cell; bool oldCast = s.CastsSun;
                if (RefreshSite(s)) { _dirty.Add(old); _dirty.Add(Key(s)); }
                if (_shadowMeshes && (oldCell != s.Cell || oldCast != s.CastsSun))
                {
                    var next = ShadowKey(s);
                    if (oldShadow != next && _shadowChunks.TryGetValue(oldShadow, out var previous)) previous.Sites.Remove(s);
                    if (s.CastsSun)
                    {
                        var group = ShadowGroup(next, s);
                        if (!group.Sites.Contains(s)) group.Sites.Add(s);
                    }
                    _shadowDirty.Add(oldShadow); _shadowDirty.Add(next);
                }
            }
            if (_dirty.Count > 0) BuildChunks(_dirty);
            foreach (var key in _shadowDirty)
                if (_shadowChunks.TryGetValue(key, out var chunk)) RebuildShadowChunk(chunk);
            FeedShadowSun();
        }
        bool RefreshSite(Site s)
        {
            float over = ShorePlantTideMath.OverGround(_ground.WaterLevelAt(s.X, s.Y), s.Z);
            int tide = s.Def.TideLadder.Length == 0 ? 0 : ShorePlantTideMath.StateFor(over, s.Def.TideLadder);
            _cells.TryGetValue((s.Def.Code, s.Stage, _season, s.Variant, tide), out var cell);
            bool under = s.Def.TideLadder.Length != 0 && ShorePlantTideMath.IsSubmerged(over, cell != null ? cell.HeightM : s.Def.StandingHeightM);
            int order = under ? SubmergedOrder : YSortSprite.OrderFor(s.Y, SortingBands.DecorBase,
                SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
            bool changed = s.Cell != cell || s.Order != order;
            s.CastsSun = CanCastSun(cell, over);
            s.Cell = cell; s.Tide = tide; s.Order = order; return changed;
        }
        (int, int, int, int) Key(Site s) =>
            (Mathf.FloorToInt(s.X / ChunkWidth), s.Order, s.Def.Code, s.Cell != null ? s.Cell.Page : -1);
        void BuildChunks(HashSet<(int, int, int, int)> dirty = null)
        {
            if (dirty == null) ClearChunks();
            else foreach (var key in dirty) ClearChunk(key);
            MissingCells = 0; DormantCells = 0;
            var groups = new Dictionary<(int, int, int, int), List<Site>>();
            foreach (var s in _sites)
            {
                if (s.Cell == null) { MissingCells++; continue; }
                if (s.Cell.Dormant) { DormantCells++; continue; }
                var k = Key(s);
                if (dirty != null && !dirty.Contains(k)) continue;
                if (!groups.TryGetValue(k, out var list)) groups[k] = list = new List<Site>();
                list.Add(s);
            }
            foreach (var group in groups) BuildChunk(group.Key, group.Value);
        }
        void BuildChunk((int, int, int, int) key, List<Site> sites)
        {
            var first = sites[0]; var page = first.Def.Pages[first.Cell.Page];
            if (Material == null || page.Albedo == null || page.Normal == null || page.Light == null || page.Detail == null)
                throw new InvalidOperationException("Plant bake maps are incomplete");
            var verts = new List<Vector3>(); var uv = new List<Vector2>();
            var roots = new List<Vector4>(); var triangles = new List<int>();
            const int strips = 8;
            foreach (var s in sites)
            {
                var c = s.Cell; int start = verts.Count;
                float rootY = s.Y + s.Z * HeightProjection;
                for (int row = 0; row <= strips; row++) for (int col = 0; col < 2; col++)
                {
                    float cx = col * c.Rect.width, cy = row * c.Rect.height / strips;
                    verts.Add(new Vector3(s.X + (cx - c.Pivot.x) / 32, rootY + (cy - c.Pivot.y) / 32, 0));
                    uv.Add(new Vector2((c.Rect.x + cx) / page.Albedo.width, (c.Rect.y + cy) / page.Albedo.height));
                    roots.Add(new Vector4(s.X, rootY, Mathf.Max(0, (cy - c.Pivot.y) / Math.Max(1, c.Rect.height - c.Pivot.y)), s.Def.Family == "shrub" ? 0 : 1));
                }
                for (int row = 0; row < strips; row++)
                {
                    int a = start + row * 2; triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
                }
            }
            var mesh = new Mesh { name = "PlantChunk", hideFlags = HideFlags.DontSave };
            if (verts.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetUVs(0, uv); mesh.SetUVs(1, roots); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var b = mesh.bounds; b.Expand(WindMargin * 2); mesh.bounds = b;
            var go = new GameObject("PlantChunk") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false); go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Material; mr.sortingOrder = first.Order;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            go.AddComponent<SortingGroup>().sortingOrder = first.Order;
            var props = new MaterialPropertyBlock(); props.SetTexture("_MainTex", page.Albedo);
            props.SetTexture("_NormalTex", page.Normal); props.SetTexture("_LightTex", page.Light); props.SetTexture("_DetailTex", page.Detail);
            mr.SetPropertyBlock(props); _chunks.Add(key, (go, mesh));
        }
        void ClearChunk((int, int, int, int) key)
        {
            if (!_chunks.TryGetValue(key, out var chunk)) return;
            if (chunk.go != null) { chunk.go.SetActive(false); if (Application.isPlaying) Destroy(chunk.go); else DestroyImmediate(chunk.go); }
            if (chunk.mesh != null) { if (Application.isPlaying) Destroy(chunk.mesh); else DestroyImmediate(chunk.mesh); }
            _chunks.Remove(key);
        }
        void ClearChunks()
        {
            foreach (var key in new List<(int, int, int, int)>(_chunks.Keys)) ClearChunk(key);
        }

        (int, int, int) ShadowKey(Site site) =>
            (Mathf.FloorToInt(site.X / ChunkWidth), site.Def.Code, site.Cell != null ? site.Cell.Page : -1);

        void BuildShadows()
        {
            // Merged order groups support the shipped shade arm; legacy needs per-plant sorting.
            if (Material == null || !SpriteShadow.SharedProfile.ScreenSpaceShade) return;
            var asset = Resources.Load<Material>("PlantFieldSunShadow");
            if (asset == null) return;
            _shadowMaterial = new Material(asset) { name = "PlantFieldSunShadow", hideFlags = HideFlags.DontSave };
            _shadowRoot = new GameObject("PlantFieldSunShadows") { hideFlags = HideFlags.DontSave };
            _shadowRoot.transform.SetParent(transform, false);
            _shadowRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _shadowRoot.transform.localScale = Vector3.one;
            _shadowMeshes = true;
            foreach (var site in _sites)
                if (site.CastsSun) ShadowGroup(ShadowKey(site), site).Sites.Add(site);
            foreach (var chunk in _shadowChunks.Values) RebuildShadowChunk(chunk);
            FeedShadowSun();
        }

        ShadowChunk ShadowGroup((int, int, int) key, Site site)
        {
            if (_shadowChunks.TryGetValue(key, out var found)) return found;
            var page = site.Def.Pages[site.Cell.Page];
            var go = new GameObject("PlantSunShadowChunk") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_shadowRoot.transform, false);
            var mesh = new Mesh { name = "PlantSunShadowChunk", hideFlags = HideFlags.DontSave };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _shadowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.sortingOrder = SortingBands.SunShade;
            go.AddComponent<SortingGroup>().sortingOrder = SortingBands.SunShade;
            var properties = new MaterialPropertyBlock(); properties.SetTexture(MainTextureId, page.Albedo);
            properties.SetVector(ShadowUvPerUnitId, new Vector4(PlantPixelsPerMetre / page.Albedo.width, PlantPixelsPerMetre / page.Albedo.height, 0, 0));
            renderer.SetPropertyBlock(properties);
            var chunk = new ShadowChunk { Mesh = mesh, Renderer = renderer };
            _shadowChunks.Add(key, chunk); return chunk;
        }

        void RebuildShadowChunk(ShadowChunk chunk)
        {
            // Reuse memberships, mesh and staging buffers when the slow tide/season tick dirties a key.
            chunk.Vertices.Clear(); chunk.Uv.Clear(); chunk.Roots.Clear(); chunk.Cells.Clear(); chunk.Triangles.Clear();
            float tallest = 0;
            foreach (var site in chunk.Sites)
            {
                if (!site.CastsSun) continue;
                var cell = site.Cell; var page = site.Def.Pages[cell.Page]; int first = chunk.Vertices.Count;
                float rootY = site.Y + site.Z * HeightProjection;
                var rect = new Vector4(cell.Rect.x / page.Albedo.width, cell.Rect.y / page.Albedo.height,
                    cell.Rect.width / page.Albedo.width, cell.Rect.height / page.Albedo.height);
                for (int row = 0; row < 2; row++) for (int col = 0; col < 2; col++)
                {
                    float cx = col * cell.Rect.width, cy = row * cell.Rect.height;
                    float height = (cy - cell.Pivot.y) / PlantPixelsPerMetre;
                    chunk.Vertices.Add(new Vector3(site.X + (cx - cell.Pivot.x) / PlantPixelsPerMetre, rootY + height, 0));
                    chunk.Uv.Add(new Vector2((cell.Rect.x + cx) / page.Albedo.width, (cell.Rect.y + cy) / page.Albedo.height));
                    chunk.Roots.Add(new Vector4(site.X, rootY, Mathf.Max(0, (cy - cell.Pivot.y) / Math.Max(1, cell.Rect.height - cell.Pivot.y)), site.Def.Family == "shrub" ? 0 : 1));
                    chunk.Cells.Add(rect); tallest = Mathf.Max(tallest, Mathf.Abs(height));
                }
                chunk.Triangles.Add(first); chunk.Triangles.Add(first + 2); chunk.Triangles.Add(first + 1);
                chunk.Triangles.Add(first + 1); chunk.Triangles.Add(first + 2); chunk.Triangles.Add(first + 3);
            }
            chunk.Mesh.Clear();
            if (chunk.Vertices.Count > 65535) chunk.Mesh.indexFormat = IndexFormat.UInt32;
            chunk.Mesh.SetVertices(chunk.Vertices); chunk.Mesh.SetUVs(0, chunk.Uv); chunk.Mesh.SetUVs(1, chunk.Roots);
            chunk.Mesh.SetUVs(2, chunk.Cells); chunk.Mesh.SetTriangles(chunk.Triangles, 0); chunk.Mesh.RecalculateBounds();
            var bounds = chunk.Mesh.bounds;
            float margin = tallest * SpriteShadow.SharedProfile.MaxLength + WindMargin;
            bounds.Expand(new Vector3(margin * 2, margin * 2, 0)); chunk.Mesh.bounds = bounds;
            chunk.Renderer.enabled = chunk.Vertices.Count > 0;
        }

        void FeedShadowSun()
        {
            if (_shadowRoot == null || _shadowMaterial == null) return;
            var look = SpriteShadow.SharedProfile;
            float elevation = Shader.GetGlobalFloat(SunElevationId), strength = Shader.GetGlobalFloat(ShadowStrengthId);
            float alpha = DayNightMath.ShadowAlpha(look.MaxAlpha, strength) * Mathf.Max(0, SunShadowStrength);
            bool active = look.ScreenSpaceShade && elevation > 0 && alpha > 0;
            if (_shadowRoot.activeSelf != active) _shadowRoot.SetActive(active);
            if (!active) return;
            Vector4 light = Shader.GetGlobalVector(SunDirectionId);
            Vector2 direction = new Vector2(-light.x, -light.y);
            if (direction.sqrMagnitude > 1e-6f) direction.Normalize(); else direction = Vector2.up;
            _shadowMaterial.SetVector(ShadowDirectionId, new Vector4(direction.x, direction.y, 0, 0));
            _shadowMaterial.SetFloat(ShadowLengthId, DayNightMath.ShadowLength(elevation, look.LengthAtNoon, look.LengthAtHorizon, look.MaxLength));
            Color tint = look.ShadowColor;
            _shadowMaterial.SetColor(ShadowColorId, new Color(tint.r, tint.g, tint.b, alpha));
            _shadowMaterial.SetFloat(SwayId, Material.HasProperty(SwayId) ? Material.GetFloat(SwayId) : 0);
        }

        void ClearShadows()
        {
            _shadowMeshes = false;
            if (_shadowRoot != null) _shadowRoot.SetActive(false);
            foreach (var chunk in _shadowChunks.Values)
                if (chunk.Mesh != null) { if (Application.isPlaying) Destroy(chunk.Mesh); else DestroyImmediate(chunk.Mesh); }
            _shadowChunks.Clear(); _shadowDirty.Clear();
            if (_shadowRoot != null) { if (Application.isPlaying) Destroy(_shadowRoot); else DestroyImmediate(_shadowRoot); }
            if (_shadowMaterial != null) { if (Application.isPlaying) Destroy(_shadowMaterial); else DestroyImmediate(_shadowMaterial); }
            _shadowRoot = null; _shadowMaterial = null;
        }
    }
}
