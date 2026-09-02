using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// SPRAY AT THE LIP (ADR 0040 rev 3 — the plunging ledge as an EVENT): torn puffs thrown shoreward
    /// off a plunging breaker at the moment its crest arrives, and nowhere else.
    ///
    /// <para><b>Where and when.</b> A fixed n×n lattice of probes over the camera frame reads
    /// <see cref="BreakerMath.SurfAt"/> a few times a second — the SAME physics the water draws: the
    /// published field (unpacked from the bridge's globals, so the animator's travel is in it) at the
    /// DRAWN scale (<see cref="FoamInjectionRegistry.DrawnWaveScale"/>), the contour solved once per
    /// probe, the fetch envelope at the point. <see cref="SurfSprayMath.Emission01"/> then keeps only
    /// the cells where the bed plunges, the crest is arriving and the whitewater is live. A spilling
    /// beach throws nothing, and neither does a plunging ledge between bores.</para>
    ///
    /// <para><b>The pattern</b> is <see cref="SprayEmitter"/>'s: a self-installing hidden host, one fixed
    /// sprite pool recycled round-robin (no per-frame allocation, rule 7), hashed salts rather than
    /// <c>System.Random</c>, a <see cref="SortingGroup"/> and a small camera-ward nudge so the puffs
    /// clear the water plane but read around the boats. It drives no sim and enters no save.</para>
    ///
    /// <para><b>⚠ This ships ON</b> (<see cref="SurfSprayConfig.Default"/>, intensity 1) — the one
    /// default in the crashing-washes PR that is not "today's look", because a particle burst cannot be
    /// judged from a plate: it exists only live. The owner's dial is the config's <c>Intensity</c>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfSprayEmitter : MonoBehaviour
    {
        [Tooltip("Every knob of the lip spray — pool, probe lattice, the three bore gates, launch, look.")]
        [SerializeField] private SurfSprayConfig _config = SurfSprayConfig.Default;

        [Tooltip("Sorting order — just above the water, below the rain's, around the boats (the SprayEmitter law).")]
        [SerializeField] private int _sortingOrder = 5;

        [Tooltip("Metres toward the camera (−z) each puff is nudged so it clears the water plane.")]
        [SerializeField] private float _cameraZOffset = 0.25f;

        [Tooltip("How often (Hz) the pool ticks (launch, age, render).")]
        [Min(2f)] [SerializeField] private float _tickHz = 24f;

        private struct Wisp
        {
            public bool Alive;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Age;
            public float Lifetime;
            public float Size;
            public float Weight;
        }

        private Wisp[] _pool;
        private SpriteRenderer[] _renderers;
        private Sprite _sprite;
        private float _tickTimer, _probeTimer;
        private int _spawnCursor, _spawnCounter;

        // The probe lattice's last read: weight, launch heading and depth per cell, and a spawn carry.
        private float[] _cellWeight, _cellDepth, _cellCarry;
        private Vector2[] _cellPos, _cellShoreward;
        private int _cells;

        private const float LaunchDecayPerSecond = 2.6f;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("SurfSprayEmitter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<SurfSprayEmitter>();
        }

        private void Awake()
        {
            _sprite = AmbientGlobals.BuildSoftPuff("SurfSpray.Puff", 16, 48, 0.85f);
            var group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = _sortingOrder;
            BuildPool();
            BuildLattice();
        }

        private void OnEnable() { _tickTimer = 0f; _probeTimer = 0f; }

        private void BuildPool()
        {
            int n = Mathf.Max(1, _config.MaxWisps);
            _pool = new Wisp[n];
            _renderers = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("lip-spray");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                sr.sortingOrder = _sortingOrder;
                go.SetActive(false);
                _renderers[i] = sr;
            }
        }

        private void BuildLattice()
        {
            _cells = Mathf.Clamp(_config.ProbeCells, 2, 32);
            int n = _cells * _cells;
            _cellWeight = new float[n];
            _cellDepth = new float[n];
            _cellCarry = new float[n];
            _cellPos = new Vector2[n];
            _cellShoreward = new Vector2[n];
        }

        private void Update()
        {
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            float step = _tickHz > 0f ? 1f / _tickHz : 0.042f;
            _tickTimer = step;
            Tick(step);
        }

        private void Tick(float dt)
        {
            Camera cam = AmbientGlobals.ResolveCamera();
            if (cam == null || _config.Intensity <= 0f) { HideAll(); return; }

            _probeTimer -= dt;
            if (_probeTimer <= 0f)
            {
                _probeTimer = 1f / Mathf.Max(0.5f, _config.ProbeHz);
                Probe(cam);
            }

            // ---- spawn: each live cell integrates its own rate with a carry ----
            float rate = Mathf.Max(0f, _config.WispsPerSecondPerCell) * Mathf.Clamp01(_config.Intensity);
            for (int c = 0; c < _cellWeight.Length; c++)
            {
                float w = _cellWeight[c];
                if (w <= 0f) { _cellCarry[c] = 0f; continue; }
                _cellCarry[c] += w * rate * dt;
                int n = Mathf.FloorToInt(_cellCarry[c]);
                if (n <= 0) continue;
                _cellCarry[c] -= n;
                for (int k = 0; k < n; k++) Spawn(c, cam);
            }

            // ---- age + render ----
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);
            float moon = AmbientParticleMath.MoonlightCatch(brightness, _config.MoonlightCatch);
            float decay = Mathf.Exp(-LaunchDecayPerSecond * dt);

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Wisp w = ref _pool[i];
                var sr = _renderers[i];
                if (!w.Alive)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }
                w.Pos += w.Vel * dt;
                w.Vel *= decay;
                w.Age += dt;
                if (w.Age >= w.Lifetime) { w.Alive = false; sr.gameObject.SetActive(false); continue; }

                float life = AmbientParticleMath.Life01(w.Age, w.Lifetime);
                float env01 = AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float alpha = Mathf.Clamp01(_config.MaxAlpha * w.Weight * env01 * dayOpacity + moon * env01 * 0.5f);

                var t = sr.transform;
                t.position = new Vector3(w.Pos.x, w.Pos.y, -_cameraZOffset);
                t.localScale = new Vector3(w.Size, w.Size, 1f);
                Color col = _config.Color * tint;
                col.a = alpha;
                sr.color = col;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        /// <summary>Read the bore on the lattice. Everything a null service would need is a "no spray"
        /// rather than an exception: no terrain, no environment, no clock, no published field, a sea
        /// that breaks nowhere — each of them zeroes every cell.</summary>
        private void Probe(Camera cam)
        {
            for (int c = 0; c < _cellWeight.Length; c++) _cellWeight[c] = 0f;

            ITidalTerrain terrain = GameServices.TidalTerrain;
            IEnvironmentService environment = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            if (terrain == null || environment == null || clock == null) return;

            float gravity = GameServices.WaveField.Gravity;
            WaveTrains trains = WaveFieldBridge.UnpackTrains(WaveFieldBridge.ReadPublishedField(), gravity);
            if (trains.Count <= 0 || trains.Dominant.Amplitude <= 0f) return;

            WaveFetchSettings fetch = GameServices.WaveFetch;
            BreakerSettings breakers = GameServices.Breakers;
            WaveTrain dominant = trains.Dominant;
            BreakerContour contour = BreakerMath.ContourFor(in dominant, WaveFetch.Envelope01(0f, in fetch), in breakers);
            if (!contour.Breaks) return;

            float waterLevel = environment.WaterLevelAt(clock.TotalSeconds);
            float drawnScale = FoamInjectionRegistry.DrawnWaveScale;
            Vector2 centre = cam.transform.position;
            float halfH = cam.orthographic ? cam.orthographicSize : 12f;
            var half = new Vector2(halfH * Mathf.Max(cam.aspect, 0.1f), halfH);

            for (int iy = 0; iy < _cells; iy++)
            for (int ix = 0; ix < _cells; ix++)
            {
                int c = iy * _cells + ix;
                Vector2 pos = SurfSprayMath.ProbePoint(centre, half, _cells, ix, iy);
                _cellPos[c] = pos;
                float envelope = GameServices.FetchEnvelopeAt(pos);
                SurfState s = BreakerMath.SurfAt(pos, waterLevel, terrain, in contour, envelope,
                                                 in trains, gravity, in breakers, drawnScale);
                _cellWeight[c] = SurfSprayMath.Emission01(s.PlungingWeight01, s.Bore01, s.Whitewater01,
                                                          _config.PlungingGate, _config.BoreGate, _config.WhitewaterGate);
                _cellShoreward[c] = s.ShorewardDirection;
                _cellDepth[c] = s.DepthMeters;
            }
        }

        private void Spawn(int cell, Camera cam)
        {
            int i = _spawnCursor;
            _spawnCursor = (_spawnCursor + 1) % _pool.Length;
            int salt = _spawnCounter++;
            float hx = AmbientParticleMath.Hash01(salt, 13);
            float hy = AmbientParticleMath.Hash01(salt, 31);
            float hs = AmbientParticleMath.Hash01(salt, 57);
            float hz = AmbientParticleMath.Hash01(salt, 89);
            float hspread = AmbientParticleMath.Hash01(salt, 71);

            float halfH = cam.orthographic ? cam.orthographicSize : 12f;
            var half = new Vector2(halfH * Mathf.Max(cam.aspect, 0.1f), halfH);
            Vector2 pos = SurfSprayMath.SpawnPoint(_cellPos[cell], half, _cells, hx, hy);
            Vector2 vel = SurfSprayMath.Launch(_cellShoreward[cell], _cellDepth[cell], GameServices.WaveField.Gravity,
                                               _config.LaunchSpeedPerBoreSpeed * (0.7f + 0.6f * hs),
                                               _config.SpreadDegrees, hspread);
            _pool[i] = new Wisp
            {
                Alive = true,
                Pos = pos,
                Vel = vel,
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, _config.Lifetime * (0.8f + 0.4f * hz)),
                Size = Mathf.Max(0.01f, _config.Size * (1f + (hz - 0.5f) * 2f * _config.SizeJitter)),
                Weight = _cellWeight[cell],
            };
        }

        private void HideAll()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].gameObject.activeSelf)
                    _renderers[i].gameObject.SetActive(false);
        }
    }
}
