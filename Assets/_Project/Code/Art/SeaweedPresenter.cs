using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// DRIFTING SEAWEED — the runtime host of the region weed beds (owner ask 2026-07-08: "seaweed
    /// clumps that can get stuck on things and group together from the waves"; P1 the sea moves
    /// things, P3 a working coast wears its wrack). Clumps of weed ride the REAL sea — the sim's tidal
    /// current, the shared scene wind, and the ONE shared wave field — visibly converge into bigger
    /// clumps where the water gathers them, foul on the player's trap-buoy lines, and strand on ground
    /// a falling tide bares until the flood refloats them.
    ///
    /// <para><b>Decor tier, presentation-only, never saved (rule 5).</b> The weed drives NO gameplay
    /// and nothing about it persists: pieces are re-seeded per session (stable
    /// <see cref="AmbientParticleMath.Hash01(int,int)"/>, never <c>System.Random</c>) and every motion
    /// input is a deterministic shared signal. The frame-to-frame drift track is live/stateful exactly
    /// like the ambient fleet's steering — "reads deterministic samples, isn't bit-deterministic
    /// itself" — and feeds nothing deterministic-consumed.</para>
    ///
    /// <para><b>Self-installing, removable (ADR 0011 — the <c>TrapBuoyPresenter</c> /
    /// <c>AmbientFleetPresenter</c> convention).</b> A <see cref="RuntimeInitializeOnLoadMethod"/> host
    /// boots from the Resources <see cref="SeaweedLibrary"/>, owns its own plain root, and never
    /// touches authored/painted content or the builders. Content is data (ADR 0003): beds come from
    /// <see cref="SeaweedDef"/> assets; a bed activates only while its Def's region scene is active and
    /// the Core services (clock, environment, tidal terrain) are up. Cross-lane reads go through Core
    /// ONLY: <c>GameServices.Clock/Environment/TidalTerrain</c>, the shared <c>_WindWorld</c> /
    /// <c>_DayNightTint</c> globals, and the <see cref="TrapPlaced"/>/<see cref="TrapRemoved"/> signals
    /// for the player's buoy positions (the signals carry positions, so Fishing is never referenced).</para>
    ///
    /// <para><b>One sea, three reads.</b> Drift, bob and wobble all come off the SAME eased wave field
    /// every other floater rides: a per-presenter <see cref="WaveFieldAnimator"/> is ticked with the
    /// game-time delta and live weather (the <c>BoatWaveMotion</c>/<c>BuoyWaveVisual</c> pattern), then
    /// sampled under each piece — height lifts it, the surface SLOPE slides it toward the troughs (the
    /// cheap honest "the waves grouped them"), and the envelope scales the rocking. ⚠ Settings parity
    /// (ADR 0018 §(4)): the field/animator settings are the shared <c>Default</c>s every consumer
    /// starts from — keep them identical until GameConfig unifies them.</para>
    ///
    /// <para><b>Perf (rule 7).</b> Everything is pooled at activation (piece count bounded by the Def,
    /// one shared code-built blob sprite so the bed batches); zero per-frame allocation, no physics.
    /// Per-frame work is one field sample + a few vector ops per live piece; the merge/snag/strand/
    /// respawn logic runs on the slow tick (the ambient fleet's <c>SlowTickSeconds</c> pattern), pure
    /// and EditMode-tested in <see cref="SeaweedMath"/>.</para>
    ///
    /// <para><b>Sorting (the mesh-vs-sprite quirk).</b> Weed floats ON the water plane (a
    /// MeshRenderer, order −5) but under hulls/buoys, so each bed root carries a
    /// <see cref="SortingGroup"/> ("sort as 2D") and every piece takes a small camera-ward z nudge —
    /// the <see cref="SprayEmitter"/> recipe. Defaults slot it at −3: above the sea, under the mist
    /// haze (−2), hulls (0) and buoys (3). Verify in-editor against the water mesh — headless tests
    /// can't see mesh-vs-sprite depth.</para>
    /// </summary>
    public sealed class SeaweedPresenter : MonoBehaviour
    {
        // Cadences (presentation plumbing, not owner feel — feel tunables live on SeaweedDef).
        private const float GateCheckSeconds = 0.5f;   // how often the scene/services gate is re-evaluated
        private const float SlowTickSeconds = 0.5f;    // merge + snag + strand + respawn cadence

        private static SeaweedPresenter _instance;

        private SeaweedLibrary _library;
        private readonly List<BedRuntime> _beds = new();

        // The player's placed buoys (positions off the Core signals) — the snag target set. The exact
        // AmbientFleetPresenter read: dictionary for identity, packed parallel buffers for the math.
        private readonly Dictionary<string, Vector2> _playerBuoys = new();
        private Vector2[] _buoyPositions = new Vector2[8];
        private string[] _buoyIds = new string[8];
        private int _buoyCount;

        private float _nextGateCheck;
        private float _nextSlowTick;
        private bool _hasLastTime;
        private double _lastTimeSeconds;

        // Slow-tick cached terrain read (tide-aware), refreshed like the fleet's depth sampler.
        private ITidalTerrain _tickTerrain;
        private float _tickWaterLevel;

        // The shared eased field (⚠ parity: keep both settings at the shared Defaults — see class doc).
        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();
        private WaveFieldSettings _settings = WaveFieldSettings.Default;
        private WaveFieldAnimatorSettings _animatorSettings = WaveFieldAnimatorSettings.Default;

        private Sprite _greyboxBlob;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var lib = Resources.Load<SeaweedLibrary>(SeaweedLibrary.ResourcesPath);
            if (lib == null || lib.Beds == null || lib.Beds.Length == 0) return;   // no beds authored — stay inert
            var go = new GameObject("[SeaweedPresenter]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SeaweedPresenter>();
            _instance._library = lib;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            EventBus.Subscribe<TrapPlaced>(OnTrapPlaced);
            EventBus.Subscribe<TrapRemoved>(OnTrapRemoved);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TrapPlaced>(OnTrapPlaced);
            EventBus.Unsubscribe<TrapRemoved>(OnTrapRemoved);
            if (_instance == this) _instance = null;
        }

        // ---- buoy tracking (the Core-signal read — positions only, Fishing never referenced) ------

        private void OnTrapPlaced(TrapPlaced e)
        {
            if (string.IsNullOrEmpty(e.InstanceId)) return;
            _playerBuoys[e.InstanceId] = new Vector2(e.PosX, e.PosY);
            RebuildBuoyBuffer();
        }

        private void OnTrapRemoved(TrapRemoved e)
        {
            if (string.IsNullOrEmpty(e.InstanceId) || !_playerBuoys.Remove(e.InstanceId)) return;
            RebuildBuoyBuffer();

            // The haul frees the wrack: anything fouled on that line goes back adrift where it lay.
            for (int b = 0; b < _beds.Count; b++)
            {
                var bed = _beds[b];
                for (int i = 0; i < bed.SnagId.Length; i++)
                {
                    if (bed.SnagId[i] != e.InstanceId) continue;
                    bed.SnagId[i] = null;
                    if (bed.State[i] == SeaweedMath.StateSnagged) bed.State[i] = SeaweedMath.StateDrifting;
                }
            }
        }

        private void RebuildBuoyBuffer()
        {
            if (_buoyPositions.Length < _playerBuoys.Count)
            {
                int n = Mathf.NextPowerOfTwo(_playerBuoys.Count);
                _buoyPositions = new Vector2[n];
                _buoyIds = new string[n];
            }
            _buoyCount = 0;
            foreach (var kv in _playerBuoys)
            {
                _buoyIds[_buoyCount] = kv.Key;
                _buoyPositions[_buoyCount++] = kv.Value;
            }
        }

        // ---- drive ---------------------------------------------------------------------------------

        private void Update()
        {
            if (_library == null) return;

            if (Time.unscaledTime >= _nextGateCheck)
            {
                _nextGateCheck = Time.unscaledTime + GateCheckSeconds;
                EvaluateGate();
            }

            var clock = GameServices.Clock;
            var env = GameServices.Environment;
            if (clock == null || env == null) return;

            bool anyActive = false;
            for (int i = 0; i < _beds.Count; i++) anyActive |= _beds[i].Active;
            if (!anyActive) { _hasLastTime = false; return; }

            // Game-time delta (the BoatWaveMotion pattern): a paused clock freezes the sea and the weed.
            double now = clock.TotalSeconds;
            float dt = _hasLastTime ? Mathf.Max(0f, (float)(now - _lastTimeSeconds)) : 0f;
            _lastTimeSeconds = now;
            _hasLastTime = true;

            bool slowTick = Time.unscaledTime >= _nextSlowTick;
            if (slowTick)
            {
                _nextSlowTick = Time.unscaledTime + SlowTickSeconds;
                _tickTerrain = GameServices.TidalTerrain;
                _tickWaterLevel = env.WaterLevelAt(now);
            }

            EnvironmentSample sample = env.Sample();
            WaveTrains trains = _animator.Tick(dt, sample.WindVector, sample.SeaState01,
                                               in _settings, in _animatorSettings);

            Vector2 flow = sample.CurrentVector;
            Vector2 wind = AmbientGlobals.Wind;
            Color tint = AmbientGlobals.DayNightTint;

            for (int i = 0; i < _beds.Count; i++)
            {
                var bed = _beds[i];
                if (!bed.Active) continue;
                if (slowTick) SlowTickBed(bed, now);
                UpdateBed(bed, dt, now, flow, wind, tint, trains.TotalAmplitude);
            }
        }

        // ---- activation gate (scene + services, the AmbientFleetPresenter shape) --------------------

        private void EvaluateGate()
        {
            string scene = SceneManager.GetActiveScene().name;
            bool servicesUp = GameServices.Ready && GameServices.TidalTerrain != null;

            for (int i = 0; i < _library.Beds.Length; i++)
            {
                var def = _library.Beds[i];
                if (def == null) continue;

                bool shouldRun = servicesUp &&
                                 (string.IsNullOrEmpty(def.RegionSceneName) ||
                                  string.Equals(scene, def.RegionSceneName, System.StringComparison.OrdinalIgnoreCase));

                BedRuntime bed = FindBed(def);
                if (shouldRun && bed == null)
                {
                    bed = BuildBed(def);
                    _beds.Add(bed);
                    ActivateBed(bed);
                }
                else if (shouldRun && !bed.Active)
                {
                    bed.Root.SetActive(true);
                    bed.Active = true;
                    ActivateBed(bed);      // fresh seeded population — never resume stale state (rule 5)
                }
                else if (!shouldRun && bed != null && bed.Active)
                {
                    bed.Root.SetActive(false);
                    bed.Active = false;
                }
            }
        }

        private BedRuntime FindBed(SeaweedDef def)
        {
            for (int i = 0; i < _beds.Count; i++)
                if (_beds[i].Def == def) return _beds[i];
            return null;
        }

        // ---- pool (allocated at activation only) -----------------------------------------------------

        private BedRuntime BuildBed(SeaweedDef def)
        {
            if (_greyboxBlob == null) _greyboxBlob = BuildWeedBlobSprite();

            int n = Mathf.Max(1, def.PieceCount);
            var bed = new BedRuntime
            {
                Def = def,
                Active = true,
                BedRect = new Rect(def.BedCenter - def.BedSize * 0.5f, def.BedSize),
                Pos = new Vector2[n],
                State = new byte[n],
                Tier = new int[n],
                RespawnAt = new double[n],
                BornAt = new double[n],
                SnagUntil = new double[n],
                SnagId = new string[n],
                BaseRotDeg = new float[n],
                Tint = new Color[n],
                Renderers = new SpriteRenderer[n],
                AbsorbedBy = new int[n],
            };

            bed.Root = new GameObject("[SeaweedBed] " + def.Id);
            bed.Root.transform.SetParent(transform, worldPositionStays: true);
            // "Sort as 2D" so the sprite pool sorts by order against the water MeshRenderer (the #134
            // mesh-vs-sprite quirk — the SprayEmitter recipe, paired with the per-piece z nudge).
            var group = bed.Root.AddComponent<SortingGroup>();
            group.sortingOrder = def.SortingOrder;

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("weed_" + i);
                go.transform.SetParent(bed.Root.transform, worldPositionStays: true);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = def.SortingOrder;
                go.SetActive(false);
                bed.Renderers[i] = sr;
                bed.State[i] = SeaweedMath.StateDormant;
            }
            return bed;
        }

        /// <summary>Seed the whole bed fresh (activation / region re-entry): every piece tries for a
        /// deep-enough, buoy-clear seeded spot; failures go dormant and retry on the slow tick.</summary>
        private void ActivateBed(BedRuntime bed)
        {
            var env = GameServices.Environment;
            var clock = GameServices.Clock;
            var terrain = GameServices.TidalTerrain;
            if (env == null || clock == null) return;

            _animator.Reset();          // snap to the live weather on wake — never ease across a gap
            _hasLastTime = false;

            bed.Seed = SeaweedMath.BedSeed(env.WorldSeed, bed.Def.Id);
            double now = clock.TotalSeconds;
            float waterLevel = env.WaterLevelAt(now);

            for (int i = 0; i < bed.Pos.Length; i++)
            {
                bed.SnagId[i] = null;
                bed.Renderers[i].gameObject.SetActive(false);
                if (TrySpawn(bed, i, now, terrain, waterLevel))
                    bed.BornAt[i] = now - bed.Def.FadeInSeconds;   // the standing bed is already visible on arrival
            }
        }

        /// <summary>One respawn attempt run: up to MaxSpawnTries seeded candidates gated on current
        /// depth and buoy clearance. Success wires the piece as a fresh small drifting clump.</summary>
        private bool TrySpawn(BedRuntime bed, int i, double now, ITidalTerrain terrain, float waterLevel)
        {
            var def = bed.Def;
            for (int attempt = 0; attempt < Mathf.Max(1, def.MaxSpawnTries); attempt++)
            {
                Vector2 p = SeaweedMath.SpawnPoint(bed.Seed, i, bed.SpawnCounter++, bed.BedRect);

                float depth = waterLevel - (terrain != null ? terrain.ElevationAt(p) : float.NegativeInfinity);
                if (terrain != null && depth < def.MinSpawnDepthMeters) continue;
                if (SeaweedMath.NearestWithin(p, _buoyPositions, _buoyCount, def.BuoySnagRadiusMeters) >= 0) continue;

                int key = (int)bed.Seed + i * 8191 + bed.SpawnCounter;
                var palette = def.Palette != null && def.Palette.Length > 0 ? def.Palette : null;
                bed.Pos[i] = p;
                bed.State[i] = SeaweedMath.StateDrifting;
                bed.Tier[i] = 0;
                bed.BornAt[i] = now;
                bed.SnagId[i] = null;
                bed.SnagUntil[i] = 0.0;
                bed.BaseRotDeg[i] = AmbientParticleMath.Hash01(key, 11) * 360f;
                bed.Tint[i] = palette != null
                    ? palette[(int)(AmbientParticleMath.Hash01(key, 7) * palette.Length) % palette.Length]
                    : Color.white;
                ApplyLook(bed, i);
                return true;
            }
            bed.State[i] = SeaweedMath.StateDormant;
            bed.RespawnAt[i] = now + bed.Def.RespawnSeconds;
            return false;
        }

        // ---- the slow tick (merge + snag + strand + respawn — the pure SeaweedMath transitions) -------

        private void SlowTickBed(BedRuntime bed, double now)
        {
            var def = bed.Def;
            int n = bed.Pos.Length;

            for (int i = 0; i < n; i++)
            {
                switch (bed.State[i])
                {
                    case SeaweedMath.StateDormant:
                        if (now >= bed.RespawnAt[i])
                            TrySpawn(bed, i, now, _tickTerrain, _tickWaterLevel);
                        break;

                    case SeaweedMath.StateSnagged:
                        // The buoy vanished while the bed was inactive / the hold timed out → the clump
                        // breaks up and washes away (recycled), per the Def's release rule.
                        if (bed.SnagId[i] == null || !_playerBuoys.ContainsKey(bed.SnagId[i]))
                        {
                            bed.SnagId[i] = null;
                            bed.State[i] = SeaweedMath.StateDrifting;
                        }
                        else if (def.SnagReleaseSeconds > 0f && now >= bed.SnagUntil[i])
                        {
                            Recycle(bed, i, now);
                        }
                        break;

                    case SeaweedMath.StateStranded:
                        if (_tickTerrain != null &&
                            !SeaweedMath.NextStranded(true, _tickWaterLevel - _tickTerrain.ElevationAt(bed.Pos[i]),
                                                      def.StrandDepthMeters, def.RefloatDepthMeters))
                            bed.State[i] = SeaweedMath.StateDrifting;   // the flood took it back
                        break;

                    case SeaweedMath.StateDrifting:
                        if (SeaweedMath.OutsideBounds(bed.Pos[i], bed.BedRect, def.DriftBoundsPaddingMeters))
                        {
                            Recycle(bed, i, now);
                            break;
                        }
                        if (_tickTerrain != null &&
                            SeaweedMath.NextStranded(false, _tickWaterLevel - _tickTerrain.ElevationAt(bed.Pos[i]),
                                                     def.StrandDepthMeters, def.RefloatDepthMeters))
                        {
                            bed.State[i] = SeaweedMath.StateStranded;   // beached until a higher tide
                            break;
                        }
                        int hit = SeaweedMath.NearestWithin(bed.Pos[i], _buoyPositions, _buoyCount, def.BuoySnagRadiusMeters);
                        if (hit >= 0)
                        {
                            bed.Pos[i] = SeaweedMath.SnagAnchor(bed.Pos[i], _buoyPositions[hit], def.BuoyRestRadiusMeters);
                            bed.State[i] = SeaweedMath.StateSnagged;
                            bed.SnagId[i] = _buoyIds[hit];
                            bed.SnagUntil[i] = now + def.SnagReleaseSeconds;
                        }
                        break;
                }
            }

            // The waves grouped them: converged pieces merge into bigger clumps (pure, in place).
            if (SeaweedMath.MergePass(bed.Pos, bed.State, bed.Tier, n,
                                      def.MergeRadiusMeters, def.MaxTier, bed.AbsorbedBy) > 0)
            {
                for (int i = 0; i < n; i++)
                    if (bed.AbsorbedBy[i] >= 0)
                    {
                        bed.SnagId[i] = null;
                        bed.RespawnAt[i] = now + def.RespawnSeconds;
                        bed.Renderers[i].gameObject.SetActive(false);
                    }
            }

            // Sync tier looks (scale/sprite) once per slow tick — merges may have grown clumps.
            for (int i = 0; i < n; i++)
                if (bed.State[i] != SeaweedMath.StateDormant) ApplyLook(bed, i);
        }

        private void Recycle(BedRuntime bed, int i, double now)
        {
            bed.State[i] = SeaweedMath.StateDormant;
            bed.SnagId[i] = null;
            bed.RespawnAt[i] = now + bed.Def.RespawnSeconds;
            bed.Renderers[i].gameObject.SetActive(false);
        }

        // ---- per-frame (drift + ride the field + look) ------------------------------------------------

        private void UpdateBed(BedRuntime bed, float dt, double now,
                               Vector2 flow, Vector2 wind, Color tint, float totalAmplitude)
        {
            var def = bed.Def;
            for (int i = 0; i < bed.Pos.Length; i++)
            {
                var sr = bed.Renderers[i];
                if (bed.State[i] == SeaweedMath.StateDormant)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                WaveSample wave = _animator.Sample(bed.Pos[i]);

                if (bed.State[i] == SeaweedMath.StateDrifting && dt > 0f)
                    bed.Pos[i] += SeaweedMath.DriftVelocity(flow, def.FlowResponse, wind, def.WindResponse,
                                                            wave.Slope, def.TroughSeek,
                                                            def.MaxDriftSpeedMetersPerSecond) * dt;

                // A STRANDED clump sits on the hard — bared ground doesn't heave, so no bob/wobble;
                // drifting and snagged pieces are IN the water and ride the field.
                bool afloat = bed.State[i] != SeaweedMath.StateStranded;
                float bob = afloat ? SeaweedMath.BobOffset(wave.Height, def.BobPerMeter, def.MaxBobMeters) : 0f;
                float wobble = afloat ? SeaweedMath.Wobble(wave.Height, totalAmplitude, def.WobbleMaxDegrees) : 0f;

                var t = sr.transform;
                t.position = new Vector3(bed.Pos[i].x, bed.Pos[i].y + bob, -def.CameraZOffset);
                t.localRotation = Quaternion.Euler(0f, 0f, bed.BaseRotDeg[i] + wobble);

                float fade = def.FadeInSeconds > 0f
                    ? Mathf.Clamp01((float)(now - bed.BornAt[i]) / def.FadeInSeconds) : 1f;
                Color col = bed.Tint[i] * tint;      // dim/warm with the shared day/night look
                col.a = def.MaxAlpha * fade;
                sr.color = col;

                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        /// <summary>Set a piece's sprite + world footprint for its size tier: the owner's painted weed
        /// when the Def slots it, else the shared greybox blob; scaled so the sprite's width IS the
        /// Def's tier footprint regardless of the art's PPU.</summary>
        private void ApplyLook(BedRuntime bed, int i)
        {
            var def = bed.Def;
            int tier = Mathf.Clamp(bed.Tier[i], 0, def.MaxTier);

            Sprite sprite = def.TierSprites != null && tier < def.TierSprites.Length && def.TierSprites[tier] != null
                ? def.TierSprites[tier] : _greyboxBlob;
            var sr = bed.Renderers[i];
            if (sr.sprite != sprite) sr.sprite = sprite;

            float footprint = def.TierSizesMeters != null && def.TierSizesMeters.Length > 0
                ? def.TierSizesMeters[Mathf.Clamp(tier, 0, def.TierSizesMeters.Length - 1)] : 1f;
            float width = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1f;
            float scale = footprint / width;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ---- the code-built greybox blob (no asset dependency — replaced by the Def's painted weed) ----

        /// <summary>
        /// A lobed organic blob (32×24 @ 32 PPU ≈ 1×0.75 m at scale 1), white-with-alpha so the Def's
        /// palette tints it — with a few interior gaps and tone speckles so it reads as matted weed,
        /// not a disc. ONE shared sprite → every piece in every bed batches (rule 7). Deterministic
        /// (hash, no RNG), point-filtered pixel-art like the project's other generated sprites.
        /// </summary>
        private static Sprite BuildWeedBlobSprite()
        {
            const int W = 32, H = 24, ppu = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true)
            {
                name = "SeaweedBlobGreybox",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[W * H];
            float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
            float rx = W * 0.5f, ry = H * 0.5f;

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                // A lobed rim: two harmonics wobble the radius so the outline reads organic.
                float rim = 0.78f + 0.14f * Mathf.Sin(3f * ang + 1.3f) + 0.08f * Mathf.Sin(7f * ang + 4.2f);

                byte a = 0;
                if (d <= rim)
                {
                    a = 255;
                    int key = y * W + x;
                    if (d < rim * 0.75f && AmbientParticleMath.Hash01(key, 5) < 0.07f) a = 0;      // frond gaps
                    else if (AmbientParticleMath.Hash01(key, 9) < 0.18f) a = 200;                  // tone speckle
                }
                px[y * W + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), ppu);
        }

        // ---- runtime shape (allocated at activation only) ----------------------------------------------

        private sealed class BedRuntime
        {
            public SeaweedDef Def;
            public GameObject Root;
            public bool Active;
            public uint Seed;
            public Rect BedRect;
            public int SpawnCounter;          // session-local attempt salt (decor — recreated per session)

            public Vector2[] Pos;
            public byte[] State;              // SeaweedMath.State*
            public int[] Tier;
            public double[] RespawnAt;
            public double[] BornAt;
            public double[] SnagUntil;
            public string[] SnagId;
            public float[] BaseRotDeg;
            public Color[] Tint;
            public SpriteRenderer[] Renderers;
            public int[] AbsorbedBy;          // merge scratch, pre-allocated
        }
    }
}
