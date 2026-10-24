using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// NMC H4: a region's road floor (road / path kit v4), streamed onto its ground. The bake
    /// (<c>tools/nmc-roads/bake-chunks.cjs</c>) writes the region's roads as 8 m chunks of three maps each, under
    /// StreamingAssets; this keeps the chunks the camera sees (a ring wider) in a pool of slots, and binds the pool
    /// to the ground's <see cref="TerrainSplatSurface"/>, whose shader relights each road texel under the ground's own
    /// sky (Include/RoadFloor4.hlsl). The owner's ruling Q1 (a): bake with the kit as is, stream the chunks the camera
    /// sees.
    ///
    /// <para>Pure look: drives no sim, saves nothing, and nothing it makes is serialized (the textures are
    /// HideAndDontSave, the surface's road inputs non-serialized). Without a bake, a refused manifest or a camera, the
    /// ground is exactly the ground it was: the surface's _RoadLoaded stays 0.</para>
    ///
    /// <para>HOW A CHUNK ARRIVES. <see cref="RoadFloorSlots"/> plans each frame the view moves: the baked chunks
    /// nearest the view's centre first, up to <see cref="_capacity"/>, evicting those wanted longest ago (an evicted
    /// chunk leaves the index at once, so the index never names a slot holding another chunk's maps). A started load
    /// reads and decodes its three PNGs on a worker (<see cref="RoadFloorPng"/>, no engine call), at most
    /// <see cref="_decodesInFlight"/> at a time; the main thread uploads at most <see cref="_uploadsPerFrame"/> a frame:
    /// each map into a staging texture and by CopyTexture into its slot of the array, the chunk's row into the ramp,
    /// then the slot into the index. A view wanting more chunks than the pool holds draws its nearest and shows the
    /// ground beyond them (logged once).</para>
    ///
    /// <para>The reader is a seam for the mobile port: StreamingAssets is a folder on desktop, read with
    /// <see cref="File.ReadAllBytes(string)"/>; Android's is inside the package and needs a UnityWebRequest reader
    /// set in <see cref="ReadBytes"/>.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class RoadFloorStreamer : MonoBehaviour
    {
        /// <summary>The bake's manifest, in its folder.</summary>
        public const string ManifestFile = "RoadFloor.json";

        /// <summary>The fields' defaults, the builder's and N1 step 12's settings: Nine Mile Creek's bake, 128 slots
        /// (96 MB of GPU memory), two uploads a frame, four decodes at once and a ring of one chunk.</summary>
        public const string DefaultFolder = "RoadFloor/NineMileCreek";
        public const int DefaultCapacity = 128, DefaultUploadsPerFrame = 2, DefaultDecodesInFlight = 4, DefaultRingChunks = 1;

        [Tooltip("The ground the road floor is drawn on; this GameObject's when left empty.")]
        [SerializeField] private TerrainSplatSurface _surface;
        [Tooltip("The bake's folder under StreamingAssets: RoadFloor.json and its chunks' PNGs.")]
        [SerializeField] private string _folder = DefaultFolder;
        [Tooltip("The pool's slots: chunks resident at once (each 768 KB of GPU memory).")]
        [SerializeField, Min(1)] private int _capacity = DefaultCapacity;
        [Tooltip("Chunks uploaded to the GPU per frame at most.")]
        [SerializeField, Min(1)] private int _uploadsPerFrame = DefaultUploadsPerFrame;
        [Tooltip("Chunks read and decoded on workers at once at most.")]
        [SerializeField, Min(1)] private int _decodesInFlight = DefaultDecodesInFlight;
        [Tooltip("Chunks kept past the view's edge: the relight reads up to 36 px into a neighbour.")]
        [SerializeField, Min(0)] private int _ringChunks = DefaultRingChunks;
        [Tooltip("The view; when left empty, the scene view's in edit mode, else the main camera.")]
        [SerializeField] private Camera _camera;

        /// <summary>Reads a file's bytes, on a worker or the main thread: the mobile port's seam (see the class).</summary>
        public static Func<string, byte[]> ReadBytes = File.ReadAllBytes;

        sealed class Result
        {
            public int Session, Chunk, Slot;
            public byte[][] Maps;
            public string Fault;
        }

        [ThreadStatic] static byte[] t_Scratch;

        RoadFloorManifest _manifest;
        int[] _rowOf;                 // grid chunk -> its manifest row, -1 where nothing is baked
        RoadFloorSlots _slots;
        Texture2DArray[] _arrays;     // normal, light, detail (RoadFloorMaps.MapSuffixes' order)
        Texture2D _ramp, _index;
        Texture2D[] _staging;         // per upload of a frame, per map
        Color[] _rampRow;
        float[] _indexTexels;
        bool _copySlots, _rampDirty, _indexDirty, _arraysDirty, _reopen, _overflowWarned, _failWarned;
        readonly List<RoadFloorSlots.Load> _loads = new List<RoadFloorSlots.Load>();
        readonly List<int> _evicted = new List<int>();
        readonly ConcurrentQueue<Result> _done = new ConcurrentQueue<Result>();
        readonly ConcurrentBag<byte[][]> _buffers = new ConcurrentBag<byte[][]>();
        int _session, _inFlight, _frame;
        RectInt _lastView;
        Vector2Int _lastCentre;
        bool _planned;
        string _root;

        /// <summary>True while a bake is open and bound to the surface.</summary>
        public bool Open => _slots != null;

        /// <summary>The chunks loading or resident.</summary>
        public int Held => _slots?.Held ?? 0;

        /// <summary>The builder's (and the tests') settings: the ground, the bake's folder under StreamingAssets, the
        /// pool's slots, the uploads a frame, the decodes at once, the ring and the view (null: see the field). An
        /// enabled streamer reopens with them on its next tick.</summary>
        public void Configure(TerrainSplatSurface surface, string folder, int capacity, int uploadsPerFrame,
                              int decodesInFlight, int ringChunks, Camera view)
        {
            _surface = surface;
            _folder = folder;
            _capacity = Mathf.Max(1, capacity);
            _uploadsPerFrame = Mathf.Max(1, uploadsPerFrame);
            _decodesInFlight = Mathf.Max(1, decodesInFlight);
            _ringChunks = Mathf.Max(0, ringChunks);
            _camera = view;
            if (_slots != null) _reopen = true;
        }

        // The tests' seams (the Art test assembly sees internals): one frame, the index and ramp as uploaded, the plan.
        internal float IndexTexel(int chunk) => _indexTexels != null ? _indexTexels[chunk] : 0f;
        internal Color RampTexel(int x, int slot) => _ramp != null ? _ramp.GetPixel(x, slot) : default;
        internal RoadFloorSlots Slots => _slots;

        private void OnEnable()
        {
            TryOpen();
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorApplication.update += Tick;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= Tick;
#endif
            Close();
        }

        private void OnValidate()
        {
            // No object creation in OnValidate: the next tick reopens with the new pool.
            if (_slots != null) _reopen = true;
        }

        private void Update()
        {
            if (Application.isPlaying) Tick();   // edit mode ticks on the editor's update
        }

        internal void Tick()
        {
            if (_reopen) { _reopen = false; Close(); TryOpen(); }
            if (_slots == null) return;
            _frame++;
            Plan();
            Drain();
            Flush();
        }

        // ============================ OPEN AND CLOSE ============================

        bool TryOpen()
        {
            _session++;
            if (_surface == null) _surface = GetComponent<TerrainSplatSurface>();
            if (_surface == null) { Warn("no TerrainSplatSurface to draw on; the road floor is off."); return false; }

            _root = Path.Combine(Application.streamingAssetsPath, _folder);
            RoadFloorManifest m;
            try { m = RoadFloorManifest.Parse(Encoding.UTF8.GetString(ReadBytes(Path.Combine(_root, ManifestFile)))); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Warn($"no bake at StreamingAssets/{_folder}/{ManifestFile} ({e.GetType().Name}); the ground has no road floor.");
                return false;
            }
            List<string> problems = m != null ? m.Problems() : new List<string> { "the file is not a manifest." };
            if (problems.Count > 0)
            {
                Warn($"StreamingAssets/{_folder}/{ManifestFile} is refused ({problems.Count} problems; the first: " +
                     string.Join(" ", problems.GetRange(0, Math.Min(3, problems.Count))) + "); the ground has no road floor.");
                return false;
            }

            int w = m.grid.w, h = m.grid.h;
            _rowOf = new int[w * h];
            var baked = new bool[w * h];
            for (int i = 0; i < _rowOf.Length; i++) _rowOf[i] = -1;
            for (int r = 0; r < m.chunks.Length; r++)
            {
                int c = m.GridIndex(m.chunks[r].cx, m.chunks[r].cy);
                _rowOf[c] = r;
                baked[c] = true;
            }
            _manifest = m;
            _slots = new RoadFloorSlots(w, h, baked, _capacity);

            // The pool. Slots arrive by CopyTexture where the GPU copies between texture types (every desktop API),
            // so the arrays hold no CPU copy; elsewhere they stay readable and are applied whole (slow: said once).
            _copySlots = (SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) != 0;
            int maps = RoadFloorMaps.MapSuffixes.Length, side = RoadFloorMaps.ChunkTexels;
            _arrays = new Texture2DArray[maps];
            for (int a = 0; a < maps; a++)
            {
                _arrays[a] = new Texture2DArray(side, side, _capacity, GraphicsFormat.R8G8B8A8_UNorm,
                    TextureCreationFlags.DontInitializePixels)
                {
                    name = "RoadFloor" + RoadFloorMaps.MapSuffixes[a],
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _arrays[a].Apply(false, _copySlots);
            }
            if (!_copySlots)
                Warn("this GPU cannot copy a texture into an array's slice; each upload re-sends the whole pool (slow).");
            _staging = new Texture2D[_copySlots ? _uploadsPerFrame * maps : 0];
            for (int i = 0; i < _staging.Length; i++)
                _staging[i] = new Texture2D(side, side, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.DontInitializePixels)
                {
                    name = "RoadFloorStaging",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };

            _ramp = new Texture2D(RoadFloorMaps.RampWidth, _capacity, TextureFormat.RGBAFloat, false, true)
            {
                name = "RoadFloorRamp",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _ramp.SetPixelData(new float[RoadFloorMaps.RampWidth * _capacity * 4], 0);
            _ramp.Apply(false, false);
            _rampRow = new Color[RoadFloorMaps.RampWidth];

            _index = new Texture2D(w, h, TextureFormat.RFloat, false, true)
            {
                name = "RoadFloorIndex",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _indexTexels = new float[w * h];
            _index.SetPixelData(_indexTexels, 0);
            _index.Apply(false, false);

            _inFlight = 0;
            _planned = false;
            _overflowWarned = false;
            _failWarned = false;
            _surface.ConfigureRoadFloor(_arrays[0], _arrays[1], _arrays[2], _ramp, _index,
                new Vector2(m.origin.x, m.origin.y), RoadFloorMaps.PixelsPerMetre);
            return true;
        }

        void Close()
        {
            _session++;   // loads still on their way are dropped when they land
            if (_surface != null && _slots != null)
                _surface.ConfigureRoadFloor(null, null, null, null, null, Vector2.zero, 0f);
            if (_arrays != null) foreach (Texture2DArray a in _arrays) Kill(a);
            if (_staging != null) foreach (Texture2D s in _staging) Kill(s);
            Kill(_ramp);
            Kill(_index);
            _arrays = null;
            _staging = null;
            _ramp = null;
            _index = null;
            _slots = null;
            _manifest = null;
            _rowOf = null;
            _inFlight = 0;
            _planned = false;
            _rampDirty = _indexDirty = _arraysDirty = false;
            while (_done.TryDequeue(out _)) { }
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        // ============================ EACH FRAME ============================

        void Plan()
        {
            if (!TryView(out Rect view)) return;
            float ox = _manifest.origin.x, oy = _manifest.origin.y, m = RoadFloorMaps.ChunkMetres;
            int x0 = Mathf.FloorToInt((view.xMin - ox) / m) - _ringChunks;
            int x1 = Mathf.FloorToInt((view.xMax - ox) / m) + _ringChunks;
            int y0 = Mathf.FloorToInt((oy - view.yMax) / m) - _ringChunks;   // the grid's rows run south
            int y1 = Mathf.FloorToInt((oy - view.yMin) / m) + _ringChunks;
            float cx = (view.center.x - ox) / m, cy = (oy - view.center.y) / m;
            var rect = new RectInt(x0, y0, x1 - x0, y1 - y0);
            var centre = new Vector2Int(Mathf.FloorToInt(cx * 4f), Mathf.FloorToInt(cy * 4f));   // to a quarter chunk
            if (_planned && rect.Equals(_lastView) && centre == _lastCentre && !_slots.Pending) return;
            _planned = true;
            _lastView = rect;
            _lastCentre = centre;

            _slots.Want(_frame, x0, y0, x1, y1, cx, cy, Mathf.Max(0, _decodesInFlight - _inFlight), _loads, _evicted);
            foreach (int c in _evicted) SetIndex(c, 0f);
            foreach (RoadFloorSlots.Load l in _loads) StartLoad(l);
            if (_slots.Overflow > 0 && !_overflowWarned)
            {
                _overflowWarned = true;
                Warn($"the view wants {_slots.Overflow + _slots.WantedCount} baked chunks and the pool holds {_capacity}: " +
                     "the nearest are drawn and the ground shows beyond them.");
            }
        }

        void StartLoad(RoadFloorSlots.Load l)
        {
            string name = _manifest.chunks[_rowOf[l.Chunk]].name, root = _root;
            int session = _session;
            if (!_buffers.TryTake(out byte[][] maps))
            {
                maps = new byte[RoadFloorMaps.MapSuffixes.Length][];
                for (int a = 0; a < maps.Length; a++) maps[a] = new byte[RoadFloorMaps.MapBytes];
            }
            _inFlight++;
            Task.Run(() =>
            {
                var r = new Result { Session = session, Chunk = l.Chunk, Slot = l.Slot, Maps = maps };
                try
                {
                    byte[] scratch = t_Scratch ??= new byte[RoadFloorPng.ScratchBytes];
                    for (int a = 0; a < maps.Length && r.Fault == null; a++)
                    {
                        string file = RoadFloorMaps.MapFile(name, a);
                        string fault = RoadFloorPng.Decode(ReadBytes(Path.Combine(root, file)), maps[a], scratch);
                        if (fault != null) r.Fault = file + ": " + fault;
                    }
                }
                catch (Exception e) { r.Fault = name + ": " + e.GetType().Name + " " + e.Message; }
                _done.Enqueue(r);
            });
        }

        void Drain()
        {
            int uploads = 0;
            while (uploads < _uploadsPerFrame && _done.TryDequeue(out Result r))
            {
                if (r.Session != _session) continue;   // a closed pool's: its buffers go with it
                _inFlight--;
                if (r.Fault != null)
                {
                    _slots.LoadFailed(r.Chunk);
                    if (!_failWarned) { _failWarned = true; Warn("a chunk is refused and stays the ground: " + r.Fault); }
                }
                else
                {
                    Upload(r, uploads++);
                    _slots.Loaded(r.Chunk);
                    SetIndex(r.Chunk, r.Slot + 1);
                }
                _buffers.Add(r.Maps);
            }
        }

        void Upload(Result r, int k)
        {
            for (int a = 0; a < _arrays.Length; a++)
            {
                if (_copySlots)
                {
                    Texture2D st = _staging[k * _arrays.Length + a];
                    st.SetPixelData(r.Maps[a], 0);
                    st.Apply(false, false);
                    Graphics.CopyTexture(st, 0, 0, _arrays[a], r.Slot, 0);
                }
                else
                {
                    _arrays[a].SetPixelData(r.Maps[a], 0, r.Slot);
                    _arraysDirty = true;
                }
            }
            // The manifest's every row packed when it was opened (Problems), so this one does.
            RoadFloorMaps.PackRampRow(_manifest.chunks[_rowOf[r.Chunk]], _rampRow, 0);
            _ramp.SetPixels(0, r.Slot, RoadFloorMaps.RampWidth, 1, _rampRow);
            _rampDirty = true;
        }

        void SetIndex(int chunk, float value)
        {
            _indexTexels[chunk] = value;   // texel (cx, cy) at cy * w + cx: the shader LOADs the index at (cx, cy)
            _indexDirty = true;
        }

        void Flush()
        {
            bool changed = _rampDirty || _indexDirty || _arraysDirty;
            if (_arraysDirty) { foreach (Texture2DArray a in _arrays) a.Apply(false, false); _arraysDirty = false; }
            if (_rampDirty) { _ramp.Apply(false, false); _rampDirty = false; }
            if (_indexDirty) { _index.SetPixelData(_indexTexels, 0); _index.Apply(false, false); _indexDirty = false; }
#if UNITY_EDITOR
            if (changed && !Application.isPlaying) UnityEditor.SceneView.RepaintAll();
#endif
        }

        // ============================ THE VIEW ============================

        /// <summary>The world rectangle the view shows at z 0.</summary>
        bool TryView(out Rect view)
        {
            view = default;
            Camera cam = _camera;
#if UNITY_EDITOR
            if (cam == null && !Application.isPlaying)
            {
                UnityEditor.SceneView sv = UnityEditor.SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null) cam = sv.camera;
            }
#endif
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;
            if (cam.orthographic)
            {
                float hh = cam.orthographicSize, hw = hh * cam.aspect;
                Vector3 p = cam.transform.position;
                view = new Rect(p.x - hw, p.y - hh, 2f * hw, 2f * hh);
                return true;
            }
            // A perspective view (the scene view in 3D): where its corners' rays meet the ground's plane.
            var ground = new Plane(Vector3.forward, Vector3.zero);
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(i & 1, i >> 1, 0f));
                if (!ground.Raycast(ray, out float d)) return false;
                Vector3 q = ray.GetPoint(d);
                xMin = Mathf.Min(xMin, q.x); xMax = Mathf.Max(xMax, q.x);
                yMin = Mathf.Min(yMin, q.y); yMax = Mathf.Max(yMax, q.y);
            }
            view = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        void Warn(string message) => Debug.LogWarning("[RoadFloorStreamer] " + message, this);
    }
}
