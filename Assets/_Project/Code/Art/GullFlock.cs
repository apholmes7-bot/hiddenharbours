using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// SELF-INSTALLING gulls — the real birds, drawn from the baked <c>Seagull.png</c> sheet, each one
    /// running the state machine the art director wrote into the rig's gameplay sidecar. They wheel over
    /// the harbour, put down on the flats and the water, walk, preen, peck, float and take off again,
    /// and every one of them has a shadow on the ground that it descends onto when it lands.
    ///
    /// <para><b>This replaced a placeholder.</b> What used to be here drew a hand-built glyph on a
    /// looping ellipse and scaled it "by proximity to camera". Both are gone: the sprite is the baked
    /// 45 px, 1.40 m wingspan at EVERY altitude, and altitude is a SCREEN OFFSET — the transform is
    /// lifted <c>altitude x cos 40</c> up the screen
    /// (<see cref="HiddenHarbours.Core.IsoGround.HeightScale"/>) while the shadow stays down at the
    /// pivot, which is what makes the landing read. There are no giant gulls crossing the camera.</para>
    ///
    /// <para><b>Self-installing (mirrors <see cref="GrassWindBridge"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns ONE hidden persistent host, so the gulls appear
    /// in every scene with no wiring. The flock works the region around the active camera. With no
    /// <see cref="SeagullVisualDef"/> in <c>Resources</c> the host stands itself down and draws nothing
    /// rather than throwing — a project without the sheet simply has no gulls.</para>
    ///
    /// <para><b>Where the numbers come from (rule 6).</b> Frame times, speeds, climb rates, altitude
    /// bands, the LAND clear box, the WATER rock caps and every flock number are the sidecar's, carried
    /// on the def and read through <see cref="SeagullBehaviour"/>. What this component serializes is
    /// STAGING only — how many birds, how big a region they work, how many are down at once, and the
    /// day/night look (<see cref="GullConfig"/>).</para>
    ///
    /// <para><b>Shared signals (cohesion), seam discipline (rule 4) and determinism (rule 5).</b> The
    /// wheel skews downwind on the shared global wind <c>_WindWorld</c>
    /// (<see cref="AmbientGlobals.Wind"/>) and the run-in is flown INTO it; the look dims with
    /// <c>_DayNightTint</c> and the flock roosts when it goes dark — both READ-ONLY. Ground and water
    /// are asked of Core (<see cref="GameServices.TidalTerrain"/>,
    /// <see cref="GameServices.Environment"/>), never of another module's classes. Every random draw is
    /// the rig's own mulberry32 from <see cref="GullConfig.Seed"/>, never
    /// <see cref="System.Random"/>.</para>
    ///
    /// <para><b>Nothing in the sim path reads a clock.</b> This component owns the only clock read:
    /// it accumulates UNSCALED real seconds and hands a millisecond delta down as a parameter.
    /// <see cref="SeagullFlockMath"/> and <see cref="SeagullStateMachine"/> take time as an argument and
    /// contain no <c>Time</c> reference at all, which is what lets a fixture step a bird a thousand
    /// times in one frame and get the same answer twice. Unscaled is deliberate: a 90 ms wing beat is a
    /// wing beat, not something the game's accelerated day should speed up.</para>
    ///
    /// <para><b>Performance (rule 7):</b> a handful of pooled sprite renderers off one sheet (batched),
    /// one pooled shadow each, a throttled tick, no per-frame allocation.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GullFlock : MonoBehaviour
    {
        [Tooltip("Staging for the flock — how many birds, how big a region they work, how many are down " +
                 "at once, and the day/night look. The rig's own numbers (frame times, speeds, altitude " +
                 "bands, the landing box) live on the SeagullVisualDef, not here.")]
        [SerializeField] private GullConfig _config = GullConfig.Default;

        [Tooltip("How often (Hz) the flock steps. Gulls glide; a handful of Hz reads fine and stays cheap.")]
        [Min(5f)] [SerializeField] private float _tickHz = 30f;

        [Tooltip("The baked sheet + state table. Left empty, Resources/SeagullVisualDef is loaded.")]
        [SerializeField] private SeagullVisualDef _visual;

        /// <summary>One bird: its sim state, the renderer that draws it, and the shadow it lands on.</summary>
        private sealed class Bird
        {
            public SeagullSimBird Sim;
            public Transform Node;
            public SpriteRenderer Renderer;
            public SpriteShadow Shadow;
            public double NextDecisionMilliseconds;
            public double AlightDeadlineMilliseconds;
            public double RockPhase;

            /// <summary>The water arrival this bird has already burst for, or −1. One splash publishes
            /// ONE <see cref="GullSplashed"/>: the burst frame is on screen for several ticks and the
            /// fish must not be told four times that the same bird landed on them. It clears itself the
            /// moment the bird is in anything that is not a water arrival, so the next splash fires
            /// again.</summary>
            public int BurstState;

            /// <summary>She came up with a fish and has not taken it away yet.</summary>
            public bool Carrying;

            /// <summary>When to give the carry up if she cannot get airborne — a bird holding an intent
            /// it can never satisfy is a bird that stops behaving.</summary>
            public double CarryDepartByMilliseconds;
        }

        private Bird[] _birds;
        private SeagullFlockMath.SeagullFlockBird[] _wheel;
        private SeagullBehaviour _behaviour;
        private SeagullFlockMath.Mulberry32 _decisions;

        /// <summary>Reused by both shoal reads — the attractor's "are there fish about?" and the burst's
        /// "did she come down ON them?". One list, forever, like every other consumer of this seam.</summary>
        private readonly List<FishSchool> _shoals = new List<FishSchool>(16);

        // The one clock in the whole system, and it is here rather than in the sim.
        private double _simMilliseconds;
        private float _pendingSeconds;
        private float _tickTimer;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("GullFlock") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<GullFlock>();
        }

        private void Awake()
        {
            if (_visual == null) _visual = Resources.Load<SeagullVisualDef>(SeagullVisualDef.ResourcesPath);
            if (_visual == null)
            {
                // No sheet, no gulls. A project mid-import should not throw eight times a frame.
                enabled = false;
                return;
            }

            _behaviour = _visual.Behaviour;
            if (_behaviour == null) { enabled = false; return; }

            BuildFlock();
        }

        private void OnEnable() { _tickTimer = 0f; _pendingSeconds = 0f; }

        private void BuildFlock()
        {
            int n = Mathf.Max(0, _config.Count);
            _birds = new Bird[n];
            _wheel = new SeagullFlockMath.SeagullFlockBird[n];
            _decisions = SeagullFlockMath.Mulberry32.ForFlockSeed(_config.Seed);

            int fly = _behaviour.IndexOf(SeagullStates.Fly);
            double settleMilliseconds = _behaviour.Flock.SettleAfterSeconds * 1000.0;

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("gull");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _visual.Cell(0, SeagullStates.Fly, 0);
                sr.sortingOrder = SortingBands.AboveDecor;

                // The shadow is the SECOND sprite of the pair, and it is what the bird comes down onto.
                // No ground-contact pool: that disc is for a thing with mass overhead (a tree), and a
                // gull three metres up is not standing on the patch it would darken.
                var shadow = go.AddComponent<SpriteShadow>();
                shadow.CastsGroundContact = false;

                _birds[i] = new Bird
                {
                    Node = go.transform,
                    Renderer = sr,
                    Shadow = shadow,
                    // Stagger the first decision across one settle window so the flock does not all
                    // make up its mind on the same tick.
                    BurstState = -1,
                    NextDecisionMilliseconds = _decisions.Next() * settleMilliseconds,
                    RockPhase = _decisions.Next() * (Math.PI * 2.0),
                    Sim = new SeagullSimBird
                    {
                        State = fly,
                        FlockBound = true,
                        IntentState = -1,
                    },
                };
                go.SetActive(false);
            }
        }

        private void Update()
        {
            // The ONLY clock read in the system. Unscaled: a wing beat is a wing beat.
            float dt = Time.unscaledDeltaTime;
            _pendingSeconds += dt;
            _tickTimer -= dt;
            if (_tickTimer > 0f) return;
            _tickTimer = _tickHz > 0f ? 1f / _tickHz : 0.033f;

            double deltaMilliseconds = _pendingSeconds * 1000.0;
            _pendingSeconds = 0f;
            Tick(deltaMilliseconds);
        }

        /// <summary>
        /// One deterministic step of the whole flock by an explicit millisecond delta — the seam a
        /// PlayMode fixture drives instead of the wall clock, and the reason nothing below this line
        /// asks the engine what time it is.
        /// </summary>
        public void Tick(double deltaMilliseconds)
        {
            if (_birds == null || _birds.Length == 0 || _behaviour == null) return;

            Camera cam = AmbientGlobals.ResolveCamera();
            if (cam == null) { HideAll(); return; }

            _simMilliseconds += deltaMilliseconds;

            Vector3 camPos = cam.transform.position;
            var centreWorld = new Vector2(camPos.x, camPos.y);

            Vector2 wind = AmbientGlobals.Wind;
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);
            bool roosting = brightness <= _config.RoostBelowBrightness;

            // The breeze the rest of the coast reads, as a GROUND bearing: a world-space delta is a
            // screen angle until it is un-squashed, and the sheet's eight rows are ground bearings.
            double windHeading = wind.sqrMagnitude > 1e-8f
                ? IsoGround.BearingDegrees(wind) * Mathf.Deg2Rad
                : 0.0;
            // The wheel itself rides downwind, as it always did.
            Vector2 wheelCentre = centreWorld + wind * _config.WindDrift;

            SeagullFlockMath.Flock(_birds.Length, _simMilliseconds, _config.Seed,
                                   _config.WheelRadiusMetres, _behaviour.FlockTuning, _wheel);

            // "Settled" counts birds already DOWN plus birds committed to a descent, so a tick cannot
            // send the whole flock at one patch of shingle.
            int settled = 0;
            for (int i = 0; i < _birds.Length; i++)
                if (_behaviour.SurfaceOf(_birds[i].Sim.State) != SeagullSurface.None
                    || _birds[i].Sim.IntentState >= 0) settled++;
            int wanted = roosting
                ? _birds.Length
                : Mathf.RoundToInt(_birds.Length * Mathf.Clamp01(_config.SettledFraction));

            float seaState = SeaState01();

            for (int i = 0; i < _birds.Length; i++)
            {
                Bird b = _birds[i];

                SeagullStateMachine.Step(ref b.Sim, deltaMilliseconds, _behaviour,
                                         _wheel[i], wheelCentre.x, wheelCentre.y);
                // Arriving turns an intent into a surface; both were already counted.
                SeagullStateMachine.TryAlight(ref b.Sim, _behaviour);

                // The water breaks here, between the step and the decision, so the signal carries the
                // position the bird is drawn at on this very frame.
                PublishSplash(b);

                Decide(b, centreWorld, windHeading, roosting, ref settled, wanted);
                Draw(b, tint, dayOpacity, seaState);
            }
        }

        // ── the splash the fish hear ────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE OWNER'S ASK, FROM THE BIRD'S SIDE (2026-09-09: <i>"fish react to it landing"</i>) —
        /// publishes <see cref="GullSplashed"/> on the frame the water actually breaks.
        ///
        /// <para><b>The publish rule is read from the data, not from a state name.</b> Any arrival anim
        /// whose chain target rests on water bursts: <see cref="SeagullBehaviour.ArrivalSurface"/> says
        /// which, the sidecar's <c>water.splash_burst_frame</c> says when, and
        /// <see cref="SeagullSplashMath.BurstFrame"/> clamps it into the row that is playing. Today that
        /// is <c>splash</c> on frame 2 and a rig test pins it; the day the drop adds a second way down to
        /// the water it fires too, with no edit here.</para>
        ///
        /// <para><b>Kind is the honest distinction.</b> A bird that came down inside a school showing at
        /// the surface made a strike — <see cref="GullSplashKind.Dive"/>; one that hit open water just
        /// put down — <see cref="GullSplashKind.Alight"/>. Both scatter the fish; only the first can come
        /// up with one. That is also why the drop's dive yield is rolled here and nowhere else: a gull
        /// cannot catch a fish where there are no fish.</para>
        ///
        /// <para><b>Nothing about the player is read or written.</b> Bite rate, catch weight and the
        /// species roll are untouched by every line of this (owner's ruling 2026-09-09: fish bite rates
        /// are LEAVE). The signal moves a picture.</para>
        /// </summary>
        private void PublishSplash(Bird b)
        {
            int state = b.Sim.State;
            if (_behaviour.ArrivalSurface(state) != SeagullSurface.Water)
            {
                b.BurstState = -1;
                return;
            }

            if (b.BurstState == state) return;                       // this splash already fired
            if (b.Sim.Frame < SeagullSplashMath.BurstFrame(_behaviour.Water.SplashBurstFrame,
                                                           _behaviour.Row(state).Frames)) return;
            b.BurstState = state;

            var at = new Vector2((float)b.Sim.X, (float)b.Sim.Y);
            bool onShoal = OnSurfaceShoal(at);
            EventBus.Publish(new GullSplashed(at, onShoal ? GullSplashKind.Dive
                                                         : GullSplashKind.Alight));

            if (!onShoal || _visual == null) return;
            if (!SeagullSplashMath.RollsCatch(_config.Seed, b.Sim.X, b.Sim.Y, _simMilliseconds,
                                              _visual.DiveCatchProbability)) return;

            b.Carrying = true;
            b.CarryDepartByMilliseconds =
                _simMilliseconds + _behaviour.Flock.SettleAfterSeconds * 1000.0;
        }

        /// <summary>
        /// Is this patch of water a school the bird can see into? The school owns its own nearness test
        /// (<c>IFishSchools.SchoolsAt</c> returns the schools whose area CONTAINS the point), so there is
        /// no second radius here to drift out of sync with it.
        ///
        /// <para>"Can see into" is the depth the player's own eyes stop at — <c>DepthDrop.ShallowsMaxMeters</c>,
        /// the exact threshold at which the fish are drawn in full rather than dimmed. A gull strikes at
        /// the fish you can see, which is the only version of this a player can read.</para>
        /// </summary>
        private bool OnSurfaceShoal(Vector2 where)
        {
            IFishSchools fish = GameServices.FishSchools;
            if (fish == null) return false;

            var clock = GameServices.Clock;
            int n = fish.SchoolsAt(where, clock != null ? clock.TotalSeconds : 0.0, _shoals);
            float surface = SurfaceDepthMetres();
            for (int i = 0; i < n && i < _shoals.Count; i++)
                if (_shoals[i].DepthMetres <= surface) return true;
            return false;
        }

        /// <summary>How deep the water still counts as "at the surface", in metres.</summary>
        private static float SurfaceDepthMetres()
        {
            GameConfig cfg = GameServices.Config;
            DepthDropSettings d = cfg != null ? cfg.DepthDrop : DepthDropSettings.Default;
            return d.ShallowsMaxMeters;
        }

        // ── deciding what a bird does next ──────────────────────────────────────────────────────────

        private void Decide(Bird b, Vector2 centre, double windHeading, bool roosting,
                            ref int settled, int wanted)
        {
            SeagullSurface on = _behaviour.SurfaceOf(b.Sim.State);

            // A bird the camera has walked away from is off-screen by definition; put it back on the
            // wheel there rather than leaving it standing in a field nobody can see.
            if (on != SeagullSurface.None && OutsideWorkingArea(b, centre))
            {
                b.Sim.FlockBound = true;
                b.Sim.IntentState = -1;
                settled--;
                return;
            }

            // A descent that never arrives (the spot went under the tide, the approach was blocked)
            // gives up after one settle window and rejoins.
            if (b.Sim.IntentState >= 0)
            {
                if (_simMilliseconds >= b.AlightDeadlineMilliseconds)
                {
                    b.Sim.IntentState = -1;
                    b.Sim.FlockBound = true;
                }
                return;
            }

            // SHE CAME UP WITH SOMETHING. A gull that has taken a fish does not sit on the water with
            // it where the rest of the flock would rob her — she carries it off. Checked every tick and
            // not on the settle window, because the picture only reads as a strike while the departure
            // still belongs to the splash; and it is given up at the deadline so a bird that cannot get
            // airborne from where it is never holds the intent forever.
            if (b.Carrying && on != SeagullSurface.None)
            {
                if (SeagullStateMachine.CommandDepart(ref b.Sim, _behaviour))
                {
                    b.Carrying = false;
                    settled--;
                    return;
                }
                if (_simMilliseconds >= b.CarryDepartByMilliseconds) b.Carrying = false;
            }

            if (_simMilliseconds < b.NextDecisionMilliseconds) return;
            b.NextDecisionMilliseconds = _simMilliseconds + _behaviour.Flock.SettleAfterSeconds * 1000.0;

            if (on != SeagullSurface.None)
            {
                if (roosting) { RoostDown(b); return; }
                if (settled > wanted && SeagullStateMachine.CommandDepart(ref b.Sim, _behaviour))
                {
                    settled--;
                    return;
                }
                SurfaceActivity(b);
                return;
            }

            if (settled >= wanted) return;
            if (TryPutDown(b, centre, windHeading)) settled++;
        }

        /// <summary>Night: a gull stands and sleeps. Nothing else on a surface after dark.</summary>
        private void RoostDown(Bird b)
        {
            int stand = _behaviour.IndexOf(SeagullStates.Stand);
            if (b.Sim.State != stand) SeagullStateMachine.TryTransition(ref b.Sim, stand, _behaviour);
        }

        /// <summary>
        /// What a settled bird does with itself: a stander takes a few steps, a walker stops, a floater
        /// preens or pecks and hands itself back to <c>float</c> after the rig's own cycle count.
        /// </summary>
        private void SurfaceActivity(Bird b)
        {
            string id = _behaviour.IdOf(b.Sim.State);
            double roll = _decisions.Next();

            if (id == SeagullStates.Stand)
            {
                if (roll < 0.5)
                {
                    int walk = _behaviour.IndexOf(SeagullStates.Walk);
                    if (SeagullStateMachine.TryTransition(ref b.Sim, walk, _behaviour))
                        // Wander off on a fresh bearing rather than always up the last approach.
                        b.Sim.Heading = _decisions.Next() * (Math.PI * 2.0);
                }
                return;
            }

            if (id == SeagullStates.Walk)
            {
                if (roll < 0.6)
                    SeagullStateMachine.TryTransition(
                        ref b.Sim, _behaviour.IndexOf(SeagullStates.Stand), _behaviour);
                return;
            }

            if (id == SeagullStates.Float)
            {
                int target = roll < 0.35 ? _behaviour.IndexOf(SeagullStates.Preen)
                           : roll < 0.60 ? _behaviour.IndexOf(SeagullStates.Peck)
                           : -1;
                if (target >= 0 && SeagullStateMachine.TryTransition(ref b.Sim, target, _behaviour))
                    SeagullStateMachine.RollCycles(ref b.Sim, _behaviour, ref _decisions);
            }
        }

        /// <summary>
        /// Picks a spot in the working area, asks the world what is there, and commands the descent —
        /// <c>land</c> onto flat exposed ground, <c>splash</c> onto water. One draw, one answer: a bird
        /// that finds nowhere to put down keeps flying and asks again next window.
        /// </summary>
        private bool TryPutDown(Bird b, Vector2 centre, double windHeading)
        {
            double u = _decisions.Next();
            double v = _decisions.Next();
            var spot = new Vector2(
                centre.x + (float)(u * 2.0 - 1.0) * _config.AreaHalfSize.x,
                centre.y + (float)(v * 2.0 - 1.0) * _config.AreaHalfSize.y);

            spot = PulledToShoal(spot, centre);

            SeagullSurface surface = SurfaceAt(spot);
            int arrival = surface switch
            {
                SeagullSurface.Ground => _behaviour.IndexOf(SeagullStates.Land),
                SeagullSurface.Water => _behaviour.IndexOf(SeagullStates.Splash),
                _ => -1,
            };
            if (arrival < 0) return false;

            if (!SeagullStateMachine.CommandAlight(ref b.Sim, arrival, spot.x, spot.y,
                                                   windHeading, _behaviour))
                return false;

            b.AlightDeadlineMilliseconds =
                _simMilliseconds + _behaviour.Flock.SettleAfterSeconds * 1000.0;
            return true;
        }

        /// <summary>
        /// THE ONE ATTRACTOR THIS GAME CAN HONOUR — the drop's <c>shoal_surface</c>, weight 0.5.
        ///
        /// <para>A gull that can see fish showing at the surface comes down AT them rather than at a
        /// random patch of sea, so the drawn spot is pulled toward the nearest surface school inside the
        /// flock's own <c>attract_radius_m</c>. The weight is the pull, straight from the drop.</para>
        ///
        /// <para><b>It spends no randomness.</b> The two draws that choose the spot happen before this
        /// and happen either way, so the decision stream does not fork on whether a shoal exists — two
        /// flocks on one seed still fly the same flight, and a test that says so stays honest.</para>
        ///
        /// <para><b>It does not decide where the bird lands</b>, only where she looks:
        /// <see cref="SurfaceAt"/> still gets the last word, and a pull that lands the spot on a rock is
        /// refused exactly as a random one would be.</para>
        ///
        /// <para>The drop's other five attractors — gutting 1.0, chum 1.0, the open tub 0.8, the
        /// trawler wake 0.7, the bait bucket 0.6 — name things that do not exist in the game today
        /// (no catch handling on deck, no discards over the side, no tub with a lid state, no wake
        /// behind a working trawler, no bait bucket). They are logged in the backlog, not built.</para>
        /// </summary>
        private Vector2 PulledToShoal(Vector2 spot, Vector2 centre)
        {
            float weight = _visual != null ? _visual.ShoalAttractWeight01 : 0f;
            if (!(weight > 0f)) return spot;
            if (!(GameServices.FishSchools is IFishSchoolView view)) return spot;

            float reach = (float)_behaviour.Flock.AttractRadiusMetres;
            if (!(reach > 0f)) return spot;

            var clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            int n = view.SchoolsInView(
                new Rect(centre.x - reach, centre.y - reach, reach * 2f, reach * 2f), now, _shoals);
            if (n <= 0) return spot;

            float surface = SurfaceDepthMetres();
            float best = float.MaxValue;
            Vector2 at = spot;
            for (int i = 0; i < n && i < _shoals.Count; i++)
            {
                FishSchool school = _shoals[i];
                if (school.DepthMetres > surface) continue;    // too deep to show, too deep to draw a bird
                float d2 = (school.Centre - spot).sqrMagnitude;
                if (d2 >= best) continue;
                best = d2;
                at = school.Centre;
            }
            if (best == float.MaxValue) return spot;

            return Vector2.Lerp(spot, at, Mathf.Clamp01(weight));
        }

        private bool OutsideWorkingArea(Bird b, Vector2 centre)
        {
            // Half again the working area: comfortably off-screen, so the re-home is never watched.
            const float Slack = 1.5f;
            double dx = Math.Abs(b.Sim.X - centre.x);
            double dy = Math.Abs(b.Sim.Y - centre.y);
            return dx > _config.AreaHalfSize.x * Slack || dy > _config.AreaHalfSize.y * Slack;
        }

        // ── what is under the bird ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ground, water, or nowhere to put down. Water is anything submerged at the current tide;
        /// ground is exposed AND flat AND clear over the sidecar's landing box; everything else — a
        /// slope, a step, a spot half in the water — is refused. Props and hulls are PR 4.
        /// </summary>
        private SeagullSurface SurfaceAt(Vector2 spot)
        {
            var terrain = GameServices.TidalTerrain;
            if (terrain == null) return SeagullSurface.Water;   // no raster = open water

            var env = GameServices.Environment;
            var clock = GameServices.Clock;
            float waterLevel = env != null ? env.WaterLevelAt(clock != null ? clock.TotalSeconds : 0.0) : 0f;

            // The landing box the sidecar asks for: 1.4 m of wingspan across, 0.6 m of body along,
            // sampled on a 3x3 so a step through the middle cannot hide between the corners.
            var box = new Vector2((float)_behaviour.Land.ClearX, (float)_behaviour.Land.ClearY);
            float lo = float.MaxValue, hi = float.MinValue;
            int exposed = 0, submerged = 0;
            for (int iy = -1; iy <= 1; iy++)
            {
                for (int ix = -1; ix <= 1; ix++)
                {
                    var p = new Vector2(spot.x + ix * box.x * 0.5f, spot.y + iy * box.y * 0.5f);
                    float e = terrain.ElevationAt(p);
                    if (e < lo) lo = e;
                    if (e > hi) hi = e;
                    if (TidalExposure.IsSubmerged(waterLevel, e)) submerged++;
                    else if (TidalExposure.IsExposed(waterLevel, e)) exposed++;
                }
            }

            if (submerged == 9) return SeagullSurface.Water;
            if (exposed != 9) return SeagullSurface.None;       // half in, half out: not a landing
            return FlatEnough(hi - lo, box) ? SeagullSurface.Ground : SeagullSurface.None;
        }

        /// <summary>
        /// Is the box flat enough to stand on? The bar is DERIVED, not picked: the sidecar's
        /// <c>min_flat_m</c> is the shortest run the rig calls flat, and one PIXEL is the smallest step
        /// the player can see — so the steepest ground that still reads flat is one pixel of rise per
        /// <c>min_flat_m</c> of run (about 6 degrees at 32 px/m and 0.3 m). Anything steeper across the
        /// landing box is a slope, and a gull lands into wind on the level.
        /// </summary>
        private bool FlatEnough(float spreadMetres, Vector2 box)
        {
            float pixel = 1f / Mathf.Max(1f, _visual.PixelsPerUnit);
            float minFlat = Mathf.Max(pixel, (float)_behaviour.Land.MinFlatMetres);
            float maxSlope = pixel / minFlat;
            float run = Mathf.Sqrt(box.x * box.x + box.y * box.y);
            return spreadMetres <= maxSlope * run;
        }

        private static float SeaState01()
        {
            var env = GameServices.Environment;
            if (env == null) return 0f;
            var clock = GameServices.Clock;
            return Mathf.Clamp01(env.SeaState01At(clock != null ? clock.TotalSeconds : 0.0));
        }

        // ── drawing ─────────────────────────────────────────────────────────────────────────────────

        private void Draw(Bird b, Color tint, float dayOpacity, float seaState)
        {
            string id = _behaviour.IdOf(b.Sim.State);
            Sprite cell = _visual.Cell(b.Sim.Dir, id, b.Sim.Frame);
            if (cell == null || dayOpacity <= 1e-3f)
            {
                if (b.Renderer.gameObject.activeSelf) b.Renderer.gameObject.SetActive(false);
                return;
            }
            if (b.Renderer.sprite != cell) b.Renderer.sprite = cell;

            // The PIVOT is where the bird is on the ground plane — where its shadow lives, and what it
            // sorts by. World XY is the ground plane (ADR 0042), so the wheel's metres go in 1:1.
            var pivot = new Vector2((float)b.Sim.X, (float)b.Sim.Y);

            // Sea state rocks a FLOATING bird, and only a floating one. Capped by the sidecar at
            // 2.4 degrees of roll and one pixel of heave — a gull rides higher than a hull.
            float roll = 0f, heavePixels = 0f;
            if (id == SeagullStates.Float)
            {
                double angle = SeagullFlockMath.RockAngle(
                    _simMilliseconds, _behaviour.Row(b.Sim.State).DurationMilliseconds, b.RockPhase);
                SeagullFlockMath.RockPose(angle, seaState,
                                          _behaviour.Water.RockRollDegreesMax,
                                          _behaviour.Water.RockHeavePixelsMax,
                                          out double rollDegrees, out double heave);
                roll = (float)rollDegrees;
                heavePixels = (float)heave;
            }

            // ALTITUDE IS A SCREEN OFFSET, NEVER A SCALE. The sprite goes up the screen by
            // altitude x cos 40; the shadow's anchor comes back DOWN by exactly the same number, so it
            // stays at the pivot at the size the bird would cast standing there. Through `land` the
            // offset runs to zero and the two meet — which is the whole trick, and the thing the
            // PlayMode fixture watches.
            float lift = (float)b.Sim.AltitudeMetres * IsoGround.HeightScale
                       + heavePixels / Mathf.Max(1f, _visual.PixelsPerUnit);

            b.Node.SetPositionAndRotation(
                new Vector3(pivot.x, pivot.y + lift, 0f),
                Mathf.Abs(roll) < 1e-4f ? Quaternion.identity : Quaternion.Euler(0f, 0f, roll));
            b.Shadow.FootOffset = lift;

            // Sky or world? Above the wheel's own floor the bird is sky and draws over everything;
            // below it, it is among the harbour and sorts by its pivot like anything else standing there.
            bool sky = b.Sim.AltitudeMetres >= _behaviour.Flock.AltitudeMinMetres;
            b.Renderer.sortingOrder = sky
                ? SortingBands.AboveDecor
                : YSortSprite.OrderFor(pivot.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                       SortingBands.DecorFloor, SortingBands.DecorCeiling);

            Color col = _config.Color * tint;
            col.a = Mathf.Clamp01(_config.MaxAlpha * dayOpacity);
            b.Renderer.color = col;
            if (!b.Renderer.gameObject.activeSelf) b.Renderer.gameObject.SetActive(true);
        }

        private void HideAll()
        {
            if (_birds == null) return;
            for (int i = 0; i < _birds.Length; i++)
            {
                var r = _birds[i]?.Renderer;
                if (r != null && r.gameObject.activeSelf) r.gameObject.SetActive(false);
            }
        }

        // ── the fixture's seam ──────────────────────────────────────────────────────────────────────
        //
        // A PlayMode fixture drives Tick(dt) directly and reads these back. They are an inspection
        // surface, not a control surface: nothing here writes sim state except CommandLanding, which is
        // the one thing a test cannot express any other way (it is a plan, and the planner is private).

        /// <summary>How many birds the flock built. 0 with no sheet.</summary>
        public int BirdCount => _birds == null ? 0 : _birds.Length;

        /// <summary>The state table the flock is running, or null with no sheet.</summary>
        public SeagullBehaviour Behaviour => _behaviour;

        /// <summary>Sim milliseconds since the host woke — the flock's own time base.</summary>
        public double SimMilliseconds => _simMilliseconds;

        /// <summary>One bird's sim state, by value.</summary>
        public SeagullSimBird BirdState(int index) => _birds[index].Sim;

        /// <summary>The transform the bird's SPRITE is on — lifted off the pivot by its altitude.</summary>
        public Transform BirdTransform(int index) => _birds[index].Node;

        /// <summary>The bird's shadow component. Its anchor is the pivot; see the altitude note in Draw.</summary>
        public SpriteShadow BirdShadow(int index) => _birds[index].Shadow;

        /// <summary>
        /// Commands one bird down onto a named spot, into the given wind. Returns false if the state
        /// table cannot get there from where the bird is. The bird stops taking its own decisions until
        /// it is down, so a fixture watching the descent is not raced by the planner.
        /// </summary>
        public bool CommandLanding(int index, Vector2 spot, int arrivalState, double windHeading)
        {
            Bird b = _birds[index];
            if (!SeagullStateMachine.CommandAlight(ref b.Sim, arrivalState, spot.x, spot.y,
                                                   windHeading, _behaviour))
                return false;
            b.AlightDeadlineMilliseconds = double.MaxValue;   // the fixture, not the clock, ends it
            b.NextDecisionMilliseconds = double.MaxValue;
            return true;
        }
    }
}
