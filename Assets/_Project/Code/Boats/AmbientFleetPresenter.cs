using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE WORKING COAST — the ambient fisher fleet's runtime host (canon M2-33, P3 "Living Working
    /// Coast"): a handful of NPC punts that sail the deep water, lay alongside their own buoys, set
    /// them, soak them, and haul them — while keeping politely clear of each other, the player, the
    /// player's gear, and any ground that could bare under a falling tide.
    ///
    /// <para><b>Decor tier, deterministic, never saved (rule 5).</b> This drives NO gameplay: NPC
    /// buoys never touch <c>PlacedTrapService</c>, the save, or the player's catch/economy. WHAT the
    /// fleet is doing is a pure function of <c>(worldSeed, gameTime)</c> — spots re-plan per game day
    /// (<see cref="AmbientFleetPlan"/>) and the place→soak→haul beat is closed-form off the clock
    /// (<see cref="AmbientFleetSchedule"/>) — so a session joined at any moment shows the fleet exactly
    /// where the clock says. Only the frame-to-frame steering track is live (it must dodge the
    /// player), exactly the "reads a deterministic sample, isn't bit-deterministic itself" contract
    /// the player's own boat physics follows.</para>
    ///
    /// <para><b>Self-installing, removable (ADR 0011 — the <see cref="TrapBuoyPresenter"/>
    /// convention).</b> A <see cref="RuntimeInitializeOnLoadMethod"/> host subscribes at boot, owns its
    /// own plain root, and never touches authored/painted content or the builders. Content is data:
    /// fleets come from <see cref="AmbientFleetDef"/> assets via the Resources
    /// <see cref="AmbientFleetLibrary"/>; a fleet activates only while its Def's region scene is active
    /// and a tidal terrain is registered. Cross-lane reads go through Core only:
    /// <c>GameServices.Clock/Environment/TidalTerrain</c> and the <see cref="TrapPlaced"/>/<see
    /// cref="TrapRemoved"/> signals (for the polite berth around the player's buoys — the signals carry
    /// positions, so Fishing is never referenced).</para>
    ///
    /// <para><b>Perf (rule 7).</b> Everything is pooled at activation (boats, buoys, sprites) — zero
    /// per-frame allocation and no Instantiate/Destroy in the loop; no rigidbodies (kinematic
    /// transforms + own steering); shoal probes and target refresh run on a slow tick, per-frame work
    /// is a few vector ops per boat. Wave riding reuses <see cref="BoatWaveMotion"/> (hulls) and
    /// <see cref="BuoyWaveVisual"/> (buoys) unchanged, so the fleet rides the same shared field the
    /// player sees.</para>
    /// </summary>
    public sealed class AmbientFleetPresenter : MonoBehaviour
    {
        // Cadences (presentation plumbing, not owner feel — feel tunables live on AmbientFleetDef).
        private const float GateCheckSeconds = 0.5f;   // how often the scene/services gate is re-evaluated
        private const float SlowTickSeconds = 0.25f;   // shoal probes + target refresh (the "plan on the slow tick" rate)
        private const float DepthAvoidWeight = 2f;     // shoal correction outweighs the seek — never argue with the bar

        private static AmbientFleetPresenter _instance;

        private AmbientFleetLibrary _library;
        private readonly List<FleetRuntime> _fleets = new();

        // The player's placed buoys (positions off the Core signals) — the polite-berth obstacle set.
        private readonly Dictionary<string, Vector2> _playerBuoys = new();
        private Vector2[] _playerBuoyPositions = new Vector2[8];
        private int _playerBuoyCount;

        private Transform _playerBoat;          // found in-module (Boats lane); refreshed on the gate check
        private readonly Vector2[] _playerPosBuffer = new Vector2[1];

        private float _nextGateCheck;
        private float _nextSlowTick;
        private bool _hasLastTime;
        private double _lastTimeSeconds;

        // Cached depth sampler (tide-aware) for the shoal probes — allocated once, fed per slow tick.
        private System.Func<Vector2, float> _depthAt;
        private float _tickWaterLevel;
        private ITidalTerrain _tickTerrain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var lib = Resources.Load<AmbientFleetLibrary>(AmbientFleetLibrary.ResourcesPath);
            if (lib == null || lib.Fleets == null || lib.Fleets.Length == 0) return;   // no fleets authored — stay inert
            var go = new GameObject("[AmbientFleetPresenter]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AmbientFleetPresenter>();
            _instance._library = lib;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            EventBus.Subscribe<TrapPlaced>(OnTrapPlaced);
            EventBus.Subscribe<TrapRemoved>(OnTrapRemoved);
            _depthAt = p => _tickWaterLevel - (_tickTerrain != null ? _tickTerrain.ElevationAt(p) : 100f);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TrapPlaced>(OnTrapPlaced);
            EventBus.Unsubscribe<TrapRemoved>(OnTrapRemoved);
            if (_instance == this) _instance = null;
        }

        private void OnTrapPlaced(TrapPlaced e)
        {
            if (string.IsNullOrEmpty(e.InstanceId)) return;
            _playerBuoys[e.InstanceId] = new Vector2(e.PosX, e.PosY);
            RebuildPlayerBuoyBuffer();
        }

        private void OnTrapRemoved(TrapRemoved e)
        {
            if (string.IsNullOrEmpty(e.InstanceId) || !_playerBuoys.Remove(e.InstanceId)) return;
            RebuildPlayerBuoyBuffer();
        }

        private void RebuildPlayerBuoyBuffer()
        {
            if (_playerBuoyPositions.Length < _playerBuoys.Count)
                _playerBuoyPositions = new Vector2[Mathf.NextPowerOfTwo(_playerBuoys.Count)];
            _playerBuoyCount = 0;
            foreach (var kv in _playerBuoys) _playerBuoyPositions[_playerBuoyCount++] = kv.Value;
        }

        private void Update()
        {
            if (_library == null) return;

            if (Time.unscaledTime >= _nextGateCheck)
            {
                _nextGateCheck = Time.unscaledTime + GateCheckSeconds;
                EvaluateGate();
            }

            var clock = GameServices.Clock;
            if (clock == null) return;

            // Game-time delta (the BoatWaveMotion pattern): a paused clock freezes the whole fleet.
            double time = clock.TotalSeconds;
            float dt = _hasLastTime ? Mathf.Max(0f, (float)(time - _lastTimeSeconds)) : 0f;
            _lastTimeSeconds = time;
            _hasLastTime = true;

            bool slowTick = Time.unscaledTime >= _nextSlowTick;
            if (slowTick)
            {
                _nextSlowTick = Time.unscaledTime + SlowTickSeconds;
                _tickTerrain = GameServices.TidalTerrain;
                var env = GameServices.Environment;
                _tickWaterLevel = env != null ? env.WaterLevelAt(time) : 0f;
            }

            for (int i = 0; i < _fleets.Count; i++)
            {
                var fleet = _fleets[i];
                if (!fleet.Active) continue;
                if (clock.DayIndex != fleet.PlannedDayIndex) PlanFleetDay(fleet);   // fishers shift grounds daily
                UpdateFleet(fleet, clock, dt, slowTick);
            }
        }

        // ---- activation gate --------------------------------------------------------------------

        private void EvaluateGate()
        {
            string scene = SceneManager.GetActiveScene().name;
            bool servicesUp = GameServices.Ready && GameServices.TidalTerrain != null;

            // The player's boat lives in this module — a direct find is in-lane (not a cross-module
            // reach); IActiveBoatService carries no position, so this is the polite-berth source.
            if (_playerBoat == null)
            {
                var pb = FindAnyObjectByType<BoatController>();
                _playerBoat = pb != null ? pb.transform : null;
            }

            for (int i = 0; i < _library.Fleets.Length; i++)
            {
                var def = _library.Fleets[i];
                if (def == null) continue;

                bool shouldRun = servicesUp &&
                                 (string.IsNullOrEmpty(def.RegionSceneName) ||
                                  string.Equals(scene, def.RegionSceneName, System.StringComparison.OrdinalIgnoreCase));

                FleetRuntime fleet = FindFleet(def);
                if (shouldRun && fleet == null)
                {
                    fleet = BuildFleet(def);
                    _fleets.Add(fleet);
                    PlanFleetDay(fleet);
                }
                else if (shouldRun && !fleet.Active)
                {
                    fleet.Root.SetActive(true);
                    fleet.Active = true;
                    PlanFleetDay(fleet);      // recompute — never resume stale state (rule 5)
                }
                else if (!shouldRun && fleet != null && fleet.Active)
                {
                    fleet.Root.SetActive(false);
                    fleet.Active = false;
                }
            }
        }

        private FleetRuntime FindFleet(AmbientFleetDef def)
        {
            for (int i = 0; i < _fleets.Count; i++)
                if (_fleets[i].Def == def) return _fleets[i];
            return null;
        }

        // ---- pools ------------------------------------------------------------------------------

        private FleetRuntime BuildFleet(AmbientFleetDef def)
        {
            var fleet = new FleetRuntime { Def = def, Active = true };
            fleet.Root = new GameObject("[AmbientFleet] " + def.Id);
            fleet.Root.transform.SetParent(transform, worldPositionStays: true);

            // The owner's 8-way fishing-boat compass, ALL-OR-NOTHING (AmbientFleetDef.HasFullHullCompass —
            // the player-boat builder's guard): with the full set, each fisher gets the player's exact
            // snap-directional rig; anything less and the pre-compass rendering below stands untouched.
            bool wearsCompass = def.HasFullHullCompass();
            Sprite hull = wearsCompass ? null
                : (def.HullSprite != null ? def.HullSprite : GetGreyboxHullSprite());

            // The fleet keeps its OWN facings field (its data, its asset — decor tier: no rock grid, no
            // oars), so it adapts them into ONE in-memory skin binding for the whole fleet rather than
            // pointing at a BoatVisualDef asset. Built once per fleet, not per boat — every fisher wears
            // the same compass; only the paintwork differs.
            BoatVisualDef fleetSkin = wearsCompass
                ? BoatVisualDef.CreateRuntime(def.HullFacings, def.HullSortingOrder) : null;
            int spotsPerBoat = Mathf.Max(1, def.SpotsPerBoat);

            fleet.Boats = new Fisher[Mathf.Max(1, def.BoatCount)];
            fleet.BoatPositions = new Vector2[fleet.Boats.Length];
            for (int b = 0; b < fleet.Boats.Length; b++)
            {
                var fisher = new Fisher();
                fisher.Root = new GameObject("AmbientFisher_" + b);
                fisher.Root.transform.SetParent(fleet.Root.transform, worldPositionStays: true);

                // Seeded identity: stable across days (only the spots re-plan daily). The fisher's buoys
                // AND hull paintwork share this colour — colour = whose gear it is (ratified owner
                // direction), whose-boat-is-whose at a glance.
                Color tint = def.BuoyPalette != null && def.BuoyPalette.Length > 0
                    ? def.BuoyPalette[b % def.BuoyPalette.Length]
                    : Color.red;

                if (wearsCompass)
                {
                    // The player's rig — now literally the SAME code, not "verbatim" by hand-copy. The ROOT
                    // keeps rotating with the heading exactly as the steering left it (#190); the skinner's
                    // DirectionalBoatSprite swaps the CHILD to the nearest facing and counter-rotates it
                    // back to screen-identity every LateUpdate, so the picture never rotates, it only
                    // changes. BoatWaveMotion comes with it (the roll routes through VisualTiltDegrees —
                    // the child's rotation is stomped, so a direct write would be eaten). The child keeps
                    // the historic name "Visual": nothing looks it up, and converging must not rename it.
                    //
                    // Hull paintwork: ONE colour write, at build time (rule 7 — no per-frame colour churn).
                    // Multiply-tint shifts the whole sprite (teal cabin included), which is why the Def's
                    // default strength is subtle. Alpha stays 1 so the skinner reads it as a real tint.
                    var paint = Color.Lerp(Color.white, tint, Mathf.Clamp01(def.HullTintStrength));
                    paint.a = 1f;

                    BoatHullSkinner.Apply(fisher.Root, fleetSkin, boat: null,
                                          new BoatHullSkinner.Options { ChildName = "Visual", Tint = paint });
                }
                else
                {
                    // Pre-compass behaviour, exactly: one picture on a rotating root, riding the wave field.
                    var visualGo = new GameObject("Visual");
                    visualGo.transform.SetParent(fisher.Root.transform, worldPositionStays: false);
                    var sr = visualGo.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = def.HullSortingOrder;
                    sr.sprite = hull;

                    var wave = fisher.Root.AddComponent<BoatWaveMotion>();
                    wave.Configure(visualGo.transform, null);
                }

                fisher.Heading = Vector2.up;

                fisher.Buoys = new Buoy[spotsPerBoat];
                Sprite buoySprite = BuildBuoySprite(tint);
                for (int j = 0; j < spotsPerBoat; j++)
                    fisher.Buoys[j] = BuildBuoy(fleet.Root.transform, b, j, buoySprite, def.BuoySortingOrder);

                fleet.Boats[b] = fisher;
            }
            return fleet;
        }

        private static Buoy BuildBuoy(Transform parent, int boatIndex, int spotIndex, Sprite sprite, int sortingOrder)
        {
            var buoy = new Buoy();
            buoy.Root = new GameObject($"AmbientBuoy_{boatIndex}_{spotIndex}");
            buoy.Root.transform.SetParent(parent, worldPositionStays: true);

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(buoy.Root.transform, worldPositionStays: false);
            buoy.Visual = visualGo.transform;

            buoy.Renderer = visualGo.AddComponent<SpriteRenderer>();
            buoy.Renderer.sprite = sprite;
            buoy.Renderer.sortingOrder = sortingOrder;

            // The proven wave-rider, reused unchanged (bob + waterline + vanish-under-a-crest).
            buoy.WaveVisual = buoy.Root.AddComponent<BuoyWaveVisual>();
            buoy.WaveVisual.Configure(buoy.Renderer, visualGo.transform);

            buoy.State = BuoyState.Hidden;
            buoy.Root.SetActive(false);
            return buoy;
        }

        // ---- the daily plan (deterministic — see AmbientFleetPlan) -------------------------------

        private void PlanFleetDay(FleetRuntime fleet)
        {
            var clock = GameServices.Clock;
            var env = GameServices.Environment;
            var terrain = GameServices.TidalTerrain;
            if (clock == null || env == null || terrain == null) return;

            var def = fleet.Def;
            fleet.PlannedDayIndex = clock.DayIndex;

            // The tide's all-time floor (spring low): the depth gate that makes routes safe at EVERY phase.
            TideProfile profile = env.ActiveTideProfile;
            float minWaterLevel = profile.MeanLevel - profile.Amplitude;

            var grounds = new Rect(def.GroundsCenter - def.GroundsSize * 0.5f, def.GroundsSize);
            Vector2[][] spots = AmbientFleetPlan.PlanFleet(
                env.WorldSeed, def.Id, fleet.PlannedDayIndex,
                fleet.Boats.Length, Mathf.Max(1, def.SpotsPerBoat), grounds,
                terrain.ElevationAt, minWaterLevel,
                def.MinDepthMeters, def.SpotSpacingMeters, def.LegSampleStepMeters, def.MaxCandidateTries);

            float workEnd = Mathf.Max(def.WorkWindowEndFraction, def.WorkWindowStartFraction + 0.01f);
            float flip = AmbientFleetSchedule.WorkFlipFraction(def.WorkWindowStartFraction, workEnd);

            for (int b = 0; b < fleet.Boats.Length; b++)
            {
                var fisher = fleet.Boats[b];
                uint seed = AmbientFleetPlan.BoatSeed(env.WorldSeed, def.Id, b);
                fisher.PhaseSlots = AmbientFleetPlan.Hash01(seed, 1, 0) * def.SlotsPerDay;
                fisher.CruiseSpeed = Mathf.Lerp(def.MinSpeedMetersPerSecond, def.MaxSpeedMetersPerSecond,
                                                AmbientFleetPlan.Hash01(seed, 2, 0));
                fisher.Spots = spots[b];

                bool hasWork = fisher.Spots.Length > 0;
                fisher.Root.SetActive(hasWork);
                if (!hasWork) { HideBuoys(fisher); continue; }

                // Session join / day flip: put the boat ON STATION for the current schedule instant,
                // and snap buoys to their canonical state with no beat — recompute, don't replay.
                double s = AmbientFleetSchedule.SlotPosition(clock.DayFraction, def.SlotsPerDay, fisher.PhaseSlots);
                int targetIdx = AmbientFleetSchedule.TargetSpot(s, fisher.Spots.Length, workEnd);
                fisher.TargetSpotIndex = targetIdx;   // never leave a stale index pointing past a shorter plan
                fisher.Position = fisher.Spots[targetIdx];
                fisher.Root.transform.position = new Vector3(fisher.Position.x, fisher.Position.y, 0f);

                // She joins ON station, lying-to: way off, no stale corrections from a previous day.
                fisher.Holding = true;
                fisher.SpeedFraction = 0f;
                fisher.DepthCorrection = Vector2.zero;
                fisher.DepthHeading = fisher.Heading;

                for (int j = 0; j < fisher.Buoys.Length; j++)
                {
                    var buoy = fisher.Buoys[j];
                    bool present = j < fisher.Spots.Length &&
                                   AmbientFleetSchedule.BuoyPresent(s, j, fisher.Spots.Length, flip);
                    if (j < fisher.Spots.Length)
                        buoy.Root.transform.position = new Vector3(fisher.Spots[j].x, fisher.Spots[j].y, 0f);
                    SnapBuoy(buoy, present);
                }
            }
        }

        private static void HideBuoys(Fisher fisher)
        {
            for (int j = 0; j < fisher.Buoys.Length; j++) SnapBuoy(fisher.Buoys[j], false);
        }

        // ---- per-frame drive ----------------------------------------------------------------------

        private void UpdateFleet(FleetRuntime fleet, IGameClock clock, float dt, bool slowTick)
        {
            var def = fleet.Def;
            float workStart = def.WorkWindowStartFraction;
            float workEnd = Mathf.Max(def.WorkWindowEndFraction, workStart + 0.01f);
            float flip = AmbientFleetSchedule.WorkFlipFraction(workStart, workEnd);

            // Shared obstacle snapshot for this frame.
            for (int b = 0; b < fleet.Boats.Length; b++) fleet.BoatPositions[b] = fleet.Boats[b].Position;
            int playerCount = 0;
            if (_playerBoat != null)
            {
                _playerPosBuffer[0] = _playerBoat.position;
                playerCount = 1;
            }

            for (int b = 0; b < fleet.Boats.Length; b++)
            {
                var fisher = fleet.Boats[b];
                if (fisher.Spots.Length == 0) continue;

                double s = AmbientFleetSchedule.SlotPosition(clock.DayFraction, def.SlotsPerDay, fisher.PhaseSlots);

                if (slowTick)
                {
                    int nextSpot = AmbientFleetSchedule.TargetSpot(s, fisher.Spots.Length, workEnd);
                    if (nextSpot != fisher.TargetSpotIndex)
                    {
                        fisher.TargetSpotIndex = nextSpot;
                        fisher.Holding = false;   // work's done here — get under way for the next spot
                    }
                    // Probe in the current bow frame and REMEMBER that frame: the correction is kept
                    // bow-relative (re-expressed every frame below), so a bow that swings between
                    // slow ticks can't leave the stored push pointing the wrong way.
                    fisher.DepthCorrection = AmbientFleetSteering.DepthAvoid(
                        fisher.Position, fisher.Heading, def.DepthLookAheadMeters, def.DepthProbeSideDegrees,
                        _depthAt, def.MinDepthMeters, out fisher.DepthSpeedScale);
                    fisher.DepthHeading = fisher.Heading;
                }

                // Social push (gates the settle) and the shoal correction (steers, never wakes her —
                // a planned spot is depth-safe at spring low by construction) stay separate.
                Vector2 social =
                    AmbientFleetSteering.Repulsion(fisher.Position, fleet.BoatPositions, fleet.BoatPositions.Length, def.BoatAvoidRadius) +
                    AmbientFleetSteering.Repulsion(fisher.Position, _playerPosBuffer, playerCount, def.PlayerAvoidRadius) +
                    AmbientFleetSteering.Repulsion(fisher.Position, _playerBuoyPositions, _playerBuoyCount, def.PlayerBuoyAvoidRadius);
                Vector2 depth = AmbientFleetSteering.RotateFromTo(
                    fisher.DepthCorrection, fisher.DepthHeading, fisher.Heading) * DepthAvoidWeight;

                // The whole drive is the shared, EditMode-proven integrator (see AmbientFleetSteering.Step).
                AmbientFleetSteering.Step(ref fisher.Position, ref fisher.Heading, ref fisher.SpeedFraction,
                                          ref fisher.Holding, fisher.Spots[fisher.TargetSpotIndex],
                                          social, depth, fisher.DepthSpeedScale, fisher.CruiseSpeed, def, dt);

                if (!fisher.Holding && dt > 0f)
                {
                    fisher.Root.transform.position = new Vector3(fisher.Position.x, fisher.Position.y, 0f);
                    fisher.Root.transform.up = fisher.Heading;   // bow-up art rides the rotating root
                }

                // Reconcile each buoy's DISPLAY with the canonical schedule; flips play the beat
                // (place = pop up; haul = dip under and vanish — pot up) while the boat lies alongside.
                for (int j = 0; j < fisher.Spots.Length; j++)
                {
                    bool present = AmbientFleetSchedule.BuoyPresent(s, j, fisher.Spots.Length, flip);
                    UpdateBuoy(fisher.Buoys[j], present, def, dt);
                }
            }
        }

        // ---- buoy beats -----------------------------------------------------------------------

        private static void SnapBuoy(Buoy buoy, bool present)
        {
            buoy.Transition = 0f;
            if (present)
            {
                buoy.State = BuoyState.Shown;
                buoy.Root.SetActive(true);
                buoy.WaveVisual.enabled = true;
            }
            else
            {
                buoy.State = BuoyState.Hidden;
                buoy.WaveVisual.enabled = false;
                buoy.Root.SetActive(false);
            }
        }

        private static void UpdateBuoy(Buoy buoy, bool present, AmbientFleetDef def, float dt)
        {
            // Kick a transition when the canonical state flips (retargets mid-beat keep the progress).
            if (present && (buoy.State == BuoyState.Hidden || buoy.State == BuoyState.Sinking))
            {
                buoy.Transition = buoy.State == BuoyState.Sinking ? 1f - buoy.Transition : 0f;
                buoy.State = BuoyState.Rising;
                buoy.Root.SetActive(true);
                buoy.WaveVisual.enabled = false;   // the presenter owns the sprite during the beat
            }
            else if (!present && (buoy.State == BuoyState.Shown || buoy.State == BuoyState.Rising))
            {
                buoy.Transition = buoy.State == BuoyState.Rising ? 1f - buoy.Transition : 0f;
                buoy.State = BuoyState.Sinking;
                buoy.WaveVisual.enabled = false;   // its OnDisable restores the base pose/colour
            }

            if (buoy.State != BuoyState.Rising && buoy.State != BuoyState.Sinking) return;

            buoy.Transition += def.BuoyBeatSeconds > 0f ? dt / def.BuoyBeatSeconds : 1f;
            float t = Mathf.Clamp01(buoy.Transition);
            float shown01 = buoy.State == BuoyState.Rising ? t : 1f - t;   // 0 = under, 1 = riding

            var c = buoy.Renderer.color;
            c.a = shown01;
            buoy.Renderer.color = c;
            buoy.Visual.localPosition = new Vector3(0f, -def.BuoyDipMeters * (1f - shown01), 0f);

            if (t < 1f) return;
            // Beat done: restore the base sprite state and hand back to (or fully drop) the wave rider.
            c.a = 1f;
            buoy.Renderer.color = c;
            buoy.Visual.localPosition = Vector3.zero;
            SnapBuoy(buoy, buoy.State == BuoyState.Rising);
        }

        // ---- code-built greybox sprites (no asset dependency — replaced when art lands) -----------

        private Sprite _greyboxHull;

        private Sprite GetGreyboxHullSprite()
        {
            if (_greyboxHull == null) _greyboxHull = BuildGreyboxHullSprite();
            return _greyboxHull;
        }

        /// <summary>A tiny bow-up punt silhouette (12×28 px @ 32 PPU ≈ 0.4×0.9 m) so the fleet still
        /// reads if the Def's hull sprite is missing. Shared → the hulls batch (rule 7).</summary>
        private static Sprite BuildGreyboxHullSprite()
        {
            const int W = 12, H = 28, ppu = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true)
            {
                name = "AmbientHullGreybox",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            var plank = new Color32(150, 110, 70, 255);
            var wash = new Color32(95, 70, 45, 255);
            int cx = W / 2;
            for (int y = 2; y < H - 1; y++)
            {
                float t = (y - 2) / (float)(H - 4);                      // 0 stern → 1 bow
                float half = Mathf.Lerp(4.5f, 0.8f, t * t);              // taper to the bow
                int h = Mathf.Max(1, Mathf.RoundToInt(half));
                for (int x = cx - h; x < cx + h; x++)
                {
                    bool edge = x == cx - h || x == cx + h - 1 || y == 2;
                    px[y * W + x] = edge ? wash : plank;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), ppu);
        }

        /// <summary>
        /// The ambient buoy: the trap-arc's greybox silhouette (16×32 @ 32 PPU, bottom-centre pivot so
        /// the reused PlayerSubmerge waterline clips from the base up) with the FLOAT in the fisher's
        /// own colour — buoy colour = whose gear it is (ratified owner direction), so an NPC pot can
        /// never read as the player's yellow.
        /// </summary>
        private static Sprite BuildBuoySprite(Color floatTint)
        {
            const int W = 16, H = 32, ppu = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true)
            {
                name = "AmbientBuoyGreybox",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            Color32 floatTop = floatTint;
            var floatBand = new Color32(30, 30, 40, 255);
            var spar = new Color32(120, 95, 70, 255);

            int cx = W / 2;
            for (int y = 14; y <= 30; y++)
            {
                float t = Mathf.InverseLerp(14f, 30f, y);
                int half = Mathf.RoundToInt(Mathf.Lerp(5f, 2f, Mathf.Abs(t - 0.45f) * 1.8f));
                half = Mathf.Clamp(half, 1, 5);
                Color32 c = (y >= 21 && y <= 23) ? floatBand : floatTop;
                for (int x = cx - half; x < cx + half; x++)
                    if (x >= 0 && x < W) px[y * W + x] = c;
            }
            for (int y = 0; y <= 16; y++)
            {
                px[y * W + cx - 1] = spar;
                px[y * W + cx] = spar;
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), ppu);
        }

        // ---- runtime shapes (allocated at activation only) ---------------------------------------

        private enum BuoyState { Hidden, Rising, Shown, Sinking }

        private sealed class Buoy
        {
            public GameObject Root;
            public Transform Visual;
            public SpriteRenderer Renderer;
            public BuoyWaveVisual WaveVisual;
            public BuoyState State;
            public float Transition;
        }

        private sealed class Fisher
        {
            public GameObject Root;
            public Vector2 Position;
            public Vector2 Heading;
            public float CruiseSpeed;
            public float PhaseSlots;
            public Vector2[] Spots = System.Array.Empty<Vector2>();
            public int TargetSpotIndex;
            public Vector2 DepthCorrection;
            public Vector2 DepthHeading = Vector2.up;   // bow frame the correction was probed in
            public float DepthSpeedScale = 1f;
            public float SpeedFraction;                 // way she carries, 0..1 of cruise
            public bool Holding;                        // lying-to alongside the buoy
            public Buoy[] Buoys = System.Array.Empty<Buoy>();
        }

        private sealed class FleetRuntime
        {
            public AmbientFleetDef Def;
            public GameObject Root;
            public bool Active;
            public int PlannedDayIndex = int.MinValue;
            public Fisher[] Boats = System.Array.Empty<Fisher>();
            public Vector2[] BoatPositions = System.Array.Empty<Vector2>();
        }
    }
}
