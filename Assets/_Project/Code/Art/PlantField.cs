using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    public sealed class PlantField : MonoBehaviour
    {
        public MonoBehaviour GroundSource;
        public Vector2 Origin;
        public int Width, Height, Seed = 9109;
        [Min(0.1f)] public float CellSize = 0.5f;
        [Min(0.1f)] public float GroundMetresPerY = 1.5557238f;
        [Min(1)] public float ChunkWidth = 8;
        [Min(0.1f)] public float SlowTickSeconds = 0.5f;
        public float HeightProjection = 0.76604444f, WindMargin = 0.75f;
        public int SubmergedOrder = -8;
        public BiomeRecipeDef[] Recipes = Array.Empty<BiomeRecipeDef>();
        public PlantSpeciesDef[] Species = Array.Empty<PlantSpeciesDef>();
        public Material Material;

        public int PlantCount => _sites.Count;
        public int ChunkCount => _chunks.Count;
        public int MissingCells { get; private set; }
        public int DormantCells { get; private set; }
        public string EncodeCurrentField() => PlantFieldCodec.Encode(_plane);
        readonly List<Site> _sites = new List<Site>();
        readonly Dictionary<(int, int, int, int), (GameObject go, Mesh mesh)> _chunks = new Dictionary<(int, int, int, int), (GameObject, Mesh)>();
        readonly PlantSpeciesDef[] _codes = new PlantSpeciesDef[64];
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
        }

        void Start() { if (GroundSource != null) Rebuild(); }
        void OnEnable() { if (_ground != null) Rebuild(); }
        void OnDisable() { ClearChunks(); }

        // Called once after a region has made its terrain available, or after an explicit edit.
        public void Rebuild()
        {
            _ground = GroundSource as IPlantFieldGround;
            if (_ground == null) throw new InvalidOperationException("Plant field needs IPlantFieldGround");
            if (Width < 1 || Height < 1 || (long)Width * Height > PlantFieldCodec.MaxCells || CellSize <= 0 || GroundMetresPerY <= 0 || ChunkWidth < 1)
                throw new InvalidOperationException("Invalid plant field grid");
            Array.Clear(_codes, 0, _codes.Length);
            foreach (var sp in Species)
            {
                if (sp == null || sp.Code < 1 || sp.Code > 63 || _codes[sp.Code] != null) throw new InvalidOperationException("Invalid plant code table");
                _codes[sp.Code] = sp;
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
            BuildChunks(); _nextTick = Time.unscaledTime + SlowTickSeconds;
        }
        void Update()
        {
            if (_ground == null || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + SlowTickSeconds;
            _season = _ground.PlantSeason;
            var dirty = new HashSet<(int, int, int, int)>();
            foreach (var s in _sites)
            {
                var old = Key(s);
                if (!RefreshSite(s)) continue;
                dirty.Add(old); dirty.Add(Key(s));
            }
            if (dirty.Count > 0) BuildChunks(dirty);
        }
        bool RefreshSite(Site s)
        {
            float over = ShorePlantTideMath.OverGround(_ground.WaterLevelAt(s.X, s.Y), s.Z);
            int tide = s.Def.TideLadder.Length == 0 ? 0 : ShorePlantTideMath.StateFor(over, s.Def.TideLadder);
            var cell = s.Def.Find(s.Stage, _season, s.Variant, tide);
            bool under = s.Def.TideLadder.Length != 0 && ShorePlantTideMath.IsSubmerged(over, cell != null ? cell.HeightM : s.Def.StandingHeightM);
            int order = under ? SubmergedOrder : YSortSprite.OrderFor(s.Y, SortingBands.DecorBase,
                SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
            bool changed = s.Cell != cell || s.Order != order;
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
    }
}
