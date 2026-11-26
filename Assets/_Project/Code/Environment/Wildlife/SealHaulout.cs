using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// ⭐ <b>THE SEALS ON ONE HAUL-OUT</b> — one small host object per reef in its region scene. It reads
    /// <see cref="SealSchedule"/> (who is out: a pure function of seed, time and tide) and steps each seal's
    /// <see cref="SealMind"/> (how it reacts to the player and to hulls) on a slow tick, and draws them
    /// from a fixed pool of sprite renderers made once, when it first runs.
    ///
    /// <para><b>Nothing saved.</b> On load every seal starts where the schedule puts it; only an approach
    /// since then changes anything, and that is forgotten when the region unloads (rule 5).</para>
    ///
    /// <para><b>Talks through Core only</b> (rule 4): the tide, the wind and the sea state from
    /// <see cref="GameServices.Environment"/>, the ground from <see cref="GameServices.TidalTerrain"/>,
    /// the player from <see cref="GameServices.PlayerTransform"/>, hulls from <see cref="HullPresences"/>.
    /// It publishes what it draws through <see cref="WildlifePresences"/> so a camera can ask whether a
    /// seal is in frame.</para>
    ///
    /// <para><b>No per-frame allocation</b> (rule 7): every array is made in <see cref="Build"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SealHaulout : MonoBehaviour, IWildlifePresence
    {
        // Frame choreography inside each row of the sheet (indices into the species' frame arrays). Which
        // frame follows which is the drawing's own grammar, not a balance number.
        static readonly int[] IdleBreath = { 0, 1, 1, 0 };
        static readonly int[] IdleHeadLift = { 0, 2, 2, 2, 2, 0 };
        static readonly int[] IdleYawn = { 2, 3, 3, 3, 2, 0 };
        static readonly int[] IdleScratch = { 4, 5, 4, 5, 4, 0 };
        static readonly int[][] Idles = { IdleBreath, IdleHeadLift, IdleYawn, IdleScratch };
        static readonly int[] SwimLook = { 0, 1, 0, 1, 2, 2, 2, 0, 1, 3, 3, 3 };

        /// <summary>How many points along the way to open water are tried for a wet one (a search resolution).</summary>
        const int SwimProbeSteps = 6;

        const uint SaltIdleWhen = 0x71u, SaltIdleKind = 0x72u, SaltDrift = 0x73u, SaltSwimOffset = 0x74u;

        [SerializeField] SealHauloutDef _haulout;

        /// <summary>The haul-out this host draws.</summary>
        public SealHauloutDef Haulout
        {
            get => _haulout;
            set { _haulout = value; _built = false; }
        }

        bool _built;
        SealTuning _k;
        uint _placeHash;
        int _count;
        float[] _lie;
        uint[] _spotHash;
        SealPlace[] _places;
        SealState[] _minds;
        bool[] _begun;
        Vector2[] _swimAt;
        bool[] _swimWet;
        int[] _coverOrder;
        Vector2[] _drawnAt;
        float[] _sinceTick;
        float[] _nextIdle;
        int[] _idleKind;
        float[] _idleStart;
        int[] _idleCount;
        float[] _idleClock;
        SpriteRenderer[] _renderers;
        float _tickTimer;
        float _tickPending;
        int _seed;
        readonly EnvironmentTide _tide = new EnvironmentTide();

        /// <summary>The schedule's place for each spot at the last tick (tests and plates read it).</summary>
        public SealPlace PlaceOf(int spot) => _built && spot >= 0 && spot < _count ? _places[spot] : SealPlace.Away;

        /// <summary>Each spot's phase at the last tick.</summary>
        public SealPhase PhaseOf(int spot) => _built && spot >= 0 && spot < _count ? _minds[spot].Phase : SealPhase.Away;

        /// <summary>Where spot <paramref name="spot"/>'s seal is drawn now.</summary>
        public Vector2 DrawnAt(int spot) => _built && spot >= 0 && spot < _count ? _drawnAt[spot] : Vector2.zero;

        /// <summary>The spots this host follows.</summary>
        public int SpotCount => _built ? _count : 0;

        void OnEnable()
        {
            _tickTimer = 0f;
            _tickPending = 0f;
            WildlifePresences.Register(this);
        }

        void OnDisable() => WildlifePresences.Unregister(this);

        void Build()
        {
            _built = false;
            if (_haulout == null || _haulout.Species == null || _haulout.Spots == null) return;
            WildlifeSpeciesDef species = _haulout.Species;
            _k = species.ToTuning();
            _placeHash = SealHash.Id(_haulout.Id);
            _count = Mathf.Min(_haulout.Spots.Count, Mathf.Max(1, species.MaxDrawn));
            _lie = new float[_count];
            _spotHash = new uint[_count];
            _places = new SealPlace[_count];
            _minds = new SealState[_count];
            _begun = new bool[_count];
            _swimAt = new Vector2[_count];
            _swimWet = new bool[_count];
            for (int i = 0; i < _count; i++) _swimWet[i] = true;
            _drawnAt = new Vector2[_count];
            _sinceTick = new float[_count];
            _nextIdle = new float[_count];
            _idleKind = new int[_count];
            _idleStart = new float[_count];
            _idleCount = new int[_count];
            _idleClock = new float[_count];
            if (_renderers != null)
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) DestroyRenderer(_renderers[i].gameObject);
            _renderers = new SpriteRenderer[_count];
            for (int i = 0; i < _count; i++)
            {
                SealSpot spot = _haulout.Spots[i];
                _lie[i] = spot.LieMetres;
                _spotHash[i] = SealHash.Id(spot.Id);
                _idleKind[i] = -1;
                _nextIdle[i] = IdleGap(species, i, 0);
                var go = new GameObject("seal " + spot.Id) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = species.CoatFor(spot.Coat);
                sr.flipX = FacesEast(spot.Facing, _spotHash[i]);
                go.SetActive(false);
                _renderers[i] = sr;
            }
            FindCover(species);
            _built = true;
        }

        /// <summary>Once per build (a region load), not per frame: the highest sorting order among the scene's
        /// small sprites whose rect covers each spot, on the seals' own sorting layer. A seal on its rock draws
        /// one over that, so the ledge it lies on (however that ledge is built and sorted) never hides it.</summary>
        void FindCover(WildlifeSpeciesDef species)
        {
            _coverOrder = new int[_count];
            for (int i = 0; i < _count; i++) _coverOrder[i] = int.MinValue;
            if (_count == 0 || species.CoverSpanMetres <= 0f) return;
            int layer = _renderers[0] != null ? _renderers[0].sortingLayerID : 0;
            float span = species.CoverSpanMetres;
            SpriteRenderer[] all = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            for (int r = 0; r < all.Length; r++)
            {
                SpriteRenderer sr = all[r];
                if (sr == null || !sr.enabled || sr.sortingLayerID != layer || sr.transform.IsChildOf(transform)) continue;
                Bounds b = sr.bounds;
                if (b.size.x > span || b.size.y > span) continue;
                for (int i = 0; i < _count; i++)
                {
                    Vector2 at = _haulout.Spots[i].At;
                    if (at.x < b.min.x || at.x > b.max.x || at.y < b.min.y || at.y > b.max.y) continue;
                    if (sr.sortingOrder > _coverOrder[i]) _coverOrder[i] = sr.sortingOrder;
                }
            }
        }

        static void DestroyRenderer(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>The sheet is drawn facing west; a seal lying east-ish is flipped. The facing is the key
        /// scene's dir, COUNTERCLOCKWISE from north (1 NW, 2 W, 3 SW, 4 S, 5 SE, 6 E, 7 NE). Due north or
        /// south picks by its hash.</summary>
        public static bool FacesEast(int facing, uint spotHash)
        {
            int f = ((facing % 8) + 8) % 8;
            if (f >= 5) return true;
            if (f >= 1 && f <= 3) return false;
            return (spotHash & 1u) != 0u;
        }

        void Update()
        {
            if (!_built) Build();
            if (!_built) return;
            float dt = Time.deltaTime;
            _tickPending += dt;
            _tickTimer -= dt;
            for (int i = 0; i < _count; i++) _sinceTick[i] += dt;
            if (_tickTimer <= 0f)
            {
                _tickTimer = Mathf.Max(0.05f, _haulout.Species.ScheduleTickSeconds);
                Tick(_tickPending);
                _tickPending = 0f;
            }
            Draw(dt);
        }

        /// <summary>Runs one slow tick now with <paramref name="dt"/> seconds elapsed (the plates and tests call it).</summary>
        public void TickNow(float dt)
        {
            if (!_built) Build();
            if (!_built) return;
            Tick(dt);
            Draw(0f);
        }

        void Tick(float dt)
        {
            IEnvironmentService env = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            _seed = env != null ? env.WorldSeed : 0;
            double secondsPerHour = config != null ? config.SecondsPerHour : GameConfig.DefaultSecondsPerDay / 24.0;
            double period = (config != null ? config.TidalPeriodHours : 12.4206) * secondsPerHour;
            double perDay = config != null ? config.SecondsPerDay : GameConfig.DefaultSecondsPerDay;

            _tide.Env = env;
            SealTideReading tide = env != null ? SealSchedule.ReadTide(_tide, now, period) : new SealTideReading(-10f, false, -10f, 0L);
            float sea = env != null ? env.SeaState01At(now) : 0f;
            Vector2 wind = env != null ? env.Sample().WindVector : Vector2.zero;
            SealSchedule.Evaluate(_k, _seed, _placeHash, _lie, _spotHash, _count, tide,
                SealSchedule.HourOf(now, perDay), sea, _places);

            ITidalTerrain terrain = GameServices.TidalTerrain;
            Transform player = GameServices.PlayerTransform;
            for (int i = 0; i < _count; i++)
            {
                SealSpot spot = _haulout.Spots[i];
                if (!_begun[i])
                {
                    _minds[i] = SealMind.Begin(_k, _places[i], _seed, _placeHash, _spotHash[i]);
                    _begun[i] = true;
                }
                Vector2 here = _minds[i].Phase == SealPhase.Swimming || _minds[i].Phase == SealPhase.Diving
                    ? _swimAt[i] : spot.At;
                float threat = NearestIntruder(here, player, wind);
                _minds[i] = SealMind.Step(_k, _minds[i], _places[i], threat, dt, _seed, _placeHash, _spotHash[i]);
                _sinceTick[i] = 0f; // the frames run on from the phase time the tick just set
                _swimAt[i] = SwimPoint(spot, i, tide.Level, terrain, out _swimWet[i]);
            }
        }

        /// <summary>The nearest intruder's distance from <paramref name="at"/>, divided by its scare scale.</summary>
        float NearestIntruder(Vector2 at, Transform player, Vector2 wind)
        {
            float best = SealMind.NoIntruder;
            var hulls = HullPresences.Active;
            bool playerAboard = false;
            Vector2 p = player != null ? (Vector2)player.position : Vector2.zero;
            if (hulls != null)
                for (int h = 0; h < hulls.Count; h++)
                {
                    IHullPresence hull = hulls[h];
                    if (hull == null || hull is Object o && o == null) continue;
                    HullFootprint f = hull.Footprint;
                    if (player != null && f.DistanceTo(p) <= 0f) playerAboard = true;
                    float d = f.DistanceTo(at);
                    float scaled = d / Mathf.Max(1e-3f, SealMind.WindScale(_k, at - f.Center, wind));
                    if (scaled < best) best = scaled;
                }
            if (player != null && !playerAboard)
            {
                float d = Vector2.Distance(p, at);
                float scale = SealMind.WindScale(_k, at - p, wind) * _k.OnFootDistanceScale;
                float scaled = d / Mathf.Max(1e-3f, scale);
                if (scaled < best) best = scaled;
            }
            return best;
        }

        /// <summary>Where a seal off its rock puts its head: out from the rock towards open water, in water deep enough.
        /// <paramref name="wet"/> is false when the whole way is dry at this tide (a low water over the flats): the seal
        /// has humped off out of sight, and no head is drawn on sand.</summary>
        Vector2 SwimPoint(SealSpot spot, int i, float level, ITidalTerrain terrain, out bool wet)
        {
            WildlifeSpeciesDef species = _haulout.Species;
            Vector2 open = _haulout.NearestSwimPoint(spot.At);
            Vector2 way = open - spot.At;
            float length = way.magnitude;
            wet = true;
            if (length < 1e-3f) return open;
            Vector2 dir = way / length;
            double u = SealHash.Unit(_seed, _placeHash, _spotHash[i], _minds[i].Surfacings, SaltSwimOffset);
            float lo = Mathf.Min(species.SwimOffsetMinMetres, species.SwimOffsetMaxMetres);
            float hi = Mathf.Max(species.SwimOffsetMinMetres, species.SwimOffsetMaxMetres);
            float want = Mathf.Min(length, lo + (float)u * (hi - lo));
            if (terrain == null) return spot.At + dir * want;
            for (int s = 0; s <= SwimProbeSteps; s++)
            {
                float along = Mathf.Lerp(want, length, s / (float)SwimProbeSteps);
                Vector2 at = spot.At + dir * along;
                if (TidalExposure.WaterDepth(level, terrain.ElevationAt(at)) >= species.MinSwimDepthMetres) return at;
            }
            wet = false;
            return open;
        }

        /// <summary>True while the seal is still on its rock: resting, alert, or the first half of a slide (its body
        /// still over the rock). A seal past that with no water to reach is gone from view.</summary>
        bool OnItsRock(SealState m, int i, WildlifeSpeciesDef species)
        {
            if (m.Phase == SealPhase.Resting || m.Phase == SealPhase.Alert) return true;
            if (m.Phase != SealPhase.SlidingIn && m.Phase != SealPhase.SlidingOut) return false;
            float s = Mathf.Clamp01((m.PhaseSeconds + _sinceTick[i]) / Mathf.Max(0.1f, species.SlideSeconds));
            float into = m.Phase == SealPhase.SlidingIn ? s : 1f - s;
            return into < 0.5f;
        }

        /// <summary>Whether spot <paramref name="i"/>'s seal is in view: begun, in a drawn phase, and not off its rock
        /// with no water to be in.</summary>
        bool Shown(int i, WildlifeSpeciesDef species) =>
            _begun[i] && SealMind.IsDrawn(_minds[i].Phase) && (_swimWet[i] || OnItsRock(_minds[i], i, species));

        /// <summary>Whether spot <paramref name="spot"/>'s way off its rock reaches water deep enough at the last tick.</summary>
        public bool SwimWet(int spot) => !_built || spot < 0 || spot >= _count || _swimWet[spot];

        void Draw(float dt)
        {
            WildlifeSpeciesDef species = _haulout.Species;
            float frameSeconds = Mathf.Max(0.02f, species.FrameSeconds);
            for (int i = 0; i < _count; i++)
            {
                SpriteRenderer sr = _renderers[i];
                if (sr == null) continue;
                SealState m = _minds[i];
                bool drawn = Shown(i, species);
                if (sr.gameObject.activeSelf != drawn) sr.gameObject.SetActive(drawn);
                if (!drawn) continue;

                SealSpot spot = _haulout.Spots[i];
                float t = m.PhaseSeconds + _sinceTick[i];
                Sprite sprite;
                Vector2 at;
                bool onLand;
                switch (m.Phase)
                {
                    case SealPhase.Resting:
                        sprite = RestFrame(species, i, dt, frameSeconds);
                        at = spot.At; onLand = true;
                        break;
                    case SealPhase.Alert:
                        sprite = Pick(species.AlertFrames, (int)(t / (frameSeconds * 4f)));
                        at = spot.At; onLand = true;
                        break;
                    case SealPhase.SlidingIn:
                    case SealPhase.SlidingOut:
                    {
                        float s = Mathf.Clamp01(t / Mathf.Max(0.1f, species.SlideSeconds));
                        float into = m.Phase == SealPhase.SlidingIn ? s : 1f - s;
                        int n = species.SlideFrames != null ? species.SlideFrames.Length : 0;
                        sprite = Pick(species.SlideFrames, Mathf.Min(n - 1, (int)(into * n)));
                        at = Vector2.Lerp(spot.At, _swimAt[i], into * into);
                        onLand = into < 0.5f;
                        break;
                    }
                    case SealPhase.Swimming:
                    {
                        int step = (int)(t / (frameSeconds * 2f));
                        sprite = Pick(species.SwimFrames, SwimLook[step % SwimLook.Length]);
                        at = _swimAt[i] + Drift(species, i, m.Surfacings, t);
                        onLand = false;
                        break;
                    }
                    default:
                    {
                        int n = species.DiveFrames != null ? species.DiveFrames.Length : 0;
                        sprite = Pick(species.DiveFrames, Mathf.Min(n - 1, (int)(t / frameSeconds)));
                        at = _swimAt[i];
                        onLand = false;
                        break;
                    }
                }
                if (sr.sprite != sprite) sr.sprite = sprite;
                _drawnAt[i] = at;
                sr.transform.localPosition = (Vector3)(at - (Vector2)transform.position);
                float sortY = onLand ? at.y - spot.SortBelowMetres : at.y;
                // Over the rock it lies on: a rock takes its foot's order and its upper face the next, so +2.
                int order = YSortOrder(sortY) + (onLand ? 2 : 0);
                // And over whatever scene sprite covers its spot (the ledge, however that ledge sorts).
                if (onLand && _coverOrder[i] != int.MinValue) order = Mathf.Max(order, _coverOrder[i] + 1);
                order = Mathf.Min(order, SortingBands.DecorCeiling - 1);
                if (sr.sortingOrder != order) sr.sortingOrder = order;
            }
        }

        static int YSortOrder(float worldY)
            => Mathf.Clamp(Mathf.RoundToInt(SortingBands.DecorBase - worldY * SortingBands.OrdersPerMetre),
                           SortingBands.DecorFloor + 1, SortingBands.DecorCeiling - 1);

        Vector2 Drift(WildlifeSpeciesDef species, int i, int surfacing, float t)
        {
            double u = SealHash.Unit(_seed, _placeHash, _spotHash[i], surfacing, SaltDrift);
            float phase = (float)(u * Mathf.PI * 2.0);
            float w = Mathf.PI * 2f / Mathf.Max(0.1f, species.DriftPeriodSeconds);
            float r = species.DriftMetres;
            return new Vector2(Mathf.Sin(t * w + phase) * r, Mathf.Sin(t * w * 0.5f + phase) * r * 0.5f);
        }

        /// <summary>The gap before a seal's n-th idle: hashed between the species' bounds.</summary>
        float IdleGap(WildlifeSpeciesDef species, int i, int n)
        {
            double uw = SealHash.Unit(_seed, _placeHash, _spotHash[i], n, SaltIdleWhen);
            float lo = Mathf.Min(species.IdleIntervalMinSeconds, species.IdleIntervalMaxSeconds);
            float hi = Mathf.Max(species.IdleIntervalMinSeconds, species.IdleIntervalMaxSeconds);
            return lo + (float)uw * (hi - lo);
        }

        Sprite RestFrame(WildlifeSpeciesDef species, int i, float dt, float frameSeconds)
        {
            // The idle clock runs only while the seal rests: a hashed gap, then a short fixed run of frames.
            _idleClock[i] += dt;
            float t = _idleClock[i];
            if (_idleKind[i] < 0 && t >= _nextIdle[i])
            {
                double uk = SealHash.Unit(_seed, _placeHash, _spotHash[i], _idleCount[i], SaltIdleKind);
                _idleKind[i] = Mathf.Min(Idles.Length - 1, (int)(uk * Idles.Length));
                _idleStart[i] = t;
            }
            if (_idleKind[i] < 0) return Pick(species.RestFrames, 0);
            int[] run = Idles[_idleKind[i]];
            int f = (int)((t - _idleStart[i]) / frameSeconds);
            if (f < run.Length) return Pick(species.RestFrames, run[f]);
            _idleKind[i] = -1;
            _idleCount[i]++;
            _nextIdle[i] = t + IdleGap(species, i, _idleCount[i]);
            return Pick(species.RestFrames, 0);
        }

        static Sprite Pick(Sprite[] frames, int index)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        // ---- IWildlifePresence ----

        public int Count
        {
            get
            {
                if (!_built) return 0;
                int n = 0;
                for (int i = 0; i < _count; i++) if (Shown(i, _haulout.Species)) n++;
                return n;
            }
        }

        public int CopySightings(WildlifeSighting[] into)
        {
            if (into == null || !_built) return 0;
            string subject = _haulout.Species.Id;
            int n = 0;
            for (int i = 0; i < _count && n < into.Length; i++)
            {
                if (!Shown(i, _haulout.Species)) continue;
                into[n++] = new WildlifeSighting(subject, _drawnAt[i], SealMind.PoseOf(_minds[i].Phase),
                    (int)SealHash.Mix(_placeHash ^ _spotHash[i]));
            }
            return n;
        }

        /// <summary>The environment service seen as the schedule's tide source (made once, no allocation).</summary>
        sealed class EnvironmentTide : ISealTideSource
        {
            public IEnvironmentService Env;
            public float LevelAt(double gameSeconds) => Env != null ? Env.WaterLevelAt(gameSeconds) : 0f;
            public long CycleAt(double gameSeconds) => Env != null ? Env.TideCycleIndexAt(gameSeconds) : 0L;
        }
    }
}
