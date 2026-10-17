using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// SELF-INSTALLING gulls — the real birds, drawn from the baked <c>Seagull.png</c> sheet, each one
    /// running the state machine the art director wrote into the rig's gameplay sidecar. They wheel over
    /// their places, put down on the flats and the water, walk, preen, peck, float and take off again,
    /// and every one of them has a shadow on the ground that it descends onto when it lands.
    ///
    /// <para><b>The birds belong to places, not to the screen.</b> Every gull is a slot at a place: a
    /// <see cref="BirdHauntDef"/> (a fish plant, a wharf, a beach, the flats, a loafing spot or a roost),
    /// or a surface school while it shows. <see cref="GullRoster"/> works out where each slot is at the
    /// game time from the schedule alone (<see cref="GullSchedule"/>: the hour, the season, the wind,
    /// the fog, the tide, the working hours and market day): at its haunt, at its roost, in the air
    /// between the two, or away. The view only chooses WHICH birds are drawn: the ones near it, nearest
    /// first, bound to a fixed pool of <see cref="GullConfig.Count"/> renderers. Pan away and come back
    /// and the same birds are on the same spots, unless the clock has moved them on.</para>
    ///
    /// <para><b>Materialising.</b> A pool bird takes up its slot already in the state the roster gives:
    /// sitting on its spot (or a perch near it, or the water there), wheeling over its place, or on its
    /// flight line at the right point. Nothing flies in from the edge of the screen, and a bird that
    /// appears never raises <see cref="GullSplashed"/>: only a descent you could watch does. A place
    /// keeps <see cref="GullConfig.SettledFraction"/> of its birds down (more in fog and in a gale), and
    /// every bird at a roost sits.</para>
    ///
    /// <para><b>This replaced a placeholder.</b> What used to be here drew a hand-built glyph on a
    /// looping ellipse and scaled it "by proximity to camera". Both are gone: the sprite is the baked
    /// 45 px, 1.40 m wingspan at EVERY altitude, and altitude is a SCREEN OFFSET — the transform is
    /// lifted <c>altitude x cos 40</c> up the screen
    /// (<see cref="HiddenHarbours.Core.IsoGround.HeightScale"/>) while the shadow stays down at the
    /// pivot, which is what makes the landing read. There are no giant gulls crossing the screen.</para>
    ///
    /// <para><b>Self-installing (mirrors <see cref="GrassWindBridge"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns ONE hidden persistent host, so the gulls appear
    /// in every scene with no wiring. The places are the current region's in
    /// <c>Resources/BirdHauntLibrary</c> (<see cref="BirdHauntLibrary"/>); with no clock there is no
    /// schedule and no bird (the title screen). With no <see cref="SeagullVisualDef"/> in
    /// <c>Resources</c> the host stands itself down and draws nothing rather than throwing — a project
    /// without the sheet simply has no gulls.</para>
    ///
    /// <para><b>Where the numbers come from (rule 6).</b> Frame times, speeds, climb rates, altitude
    /// bands, the LAND clear box, the WATER rock caps and every flock number are the sidecar's, carried
    /// on the def and read through <see cref="SeagullBehaviour"/>. How many birds use a place, and when,
    /// is the place's Def and the library's <see cref="GullScheduleTuning"/>. What this component
    /// serializes is STAGING only — the pool, how the birds fly between places, how many sit, and the
    /// day/night look (<see cref="GullConfig"/>).</para>
    ///
    /// <para><b>Shared signals (cohesion), seam discipline (rule 4) and determinism (rule 5).</b> Each
    /// wheel skews downwind on the shared global wind <c>_WindWorld</c>
    /// (<see cref="AmbientGlobals.Wind"/>) and the run-in is flown INTO it; the look dims with
    /// <c>_DayNightTint</c> — both READ-ONLY. Ground and water, the clock, the weather, the tide, the
    /// region and the fish are asked of Core (<see cref="GameServices"/>), never of another module's
    /// classes, and the birds near the view go back out through Core as an <see cref="IBirdPresence"/>
    /// (<see cref="GameServices.BirdPresence"/>) for the calls to follow. Where a bird is, is a hash of
    /// the world seed, its place, its slot and the game time; what it does with itself once drawn is the
    /// rig's own mulberry32 from <see cref="GullConfig.Seed"/>, never <see cref="System.Random"/>.
    /// Nothing is saved.</para>
    ///
    /// <para><b>Nothing in the sim path reads a clock.</b> This component owns the only engine clock
    /// read: it accumulates UNSCALED real seconds and hands a millisecond delta down as a parameter.
    /// The game time the schedule runs on is the game's own clock service, read through Core.
    /// <see cref="SeagullFlockMath"/>, <see cref="SeagullStateMachine"/>, <see cref="GullSchedule"/> and
    /// <see cref="GullRoster"/> take time as an argument and contain no <c>Time</c> reference at all,
    /// which is what lets a fixture step a bird a thousand times in one frame and get the same answer
    /// twice. Unscaled is deliberate: a 90 ms wing beat is a wing beat, not something the game's
    /// accelerated day should speed up.</para>
    ///
    /// <para><b>Performance (rule 7):</b> a fixed pool of sprite renderers off one sheet (batched), one
    /// pooled shadow each, a throttled tick, the schedule a few times a second, no per-frame
    /// allocation.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GullFlock : MonoBehaviour, IBirdPresence
    {
        /// <summary>The most authored places one region may hold (the content test holds every region
        /// to it): the schedule evaluates all of them a few times a second.</summary>
        public const int MaxAuthoredPlaces = 32;

        /// <summary>The most surface schools whose birds are followed at once.</summary>
        public const int MaxSchools = 8;

        /// <summary>The most near birds listed for drawing and for the calls; any more are counted.</summary>
        public const int MaxNearBirds = 128;

        /// <summary>How many other spots, hashed, a bird tries when its own is no place to put down.</summary>
        private const int AlternateSpots = 4;

        /// <summary>How far past its place's box (a multiple of the half-size) a bird on the ground may
        /// wander before it gets up and goes back.</summary>
        private const double OutsideSlack = 1.5;

        /// <summary>The fixture place's id (<see cref="PinFixtureHaunt"/>), for its hashes.</summary>
        private const string FixtureHauntId = "birdhaunt.fixture";

        /// <summary>Salt for a surface school's id hash, so it never collides with an authored id's.</summary>
        private const uint SchoolHashSalt = 0x9E3779B9u;

        /// <summary>A school is told apart by its centre to the centimetre and its start to the hundredth
        /// of a second.</summary>
        private const double HashResolution = 100.0;

        /// <summary>Resources/DayNightProfile.asset, as the day/night controller loads it: the sky's
        /// sunrise and sunset are the schedule's.</summary>
        private const string DayNightProfilePath = "DayNightProfile";

        /// <summary>The schedule's shipped knobs, for a library without its own (built once).</summary>
        private static readonly GullScheduleTuning DefaultTuning = GullScheduleTuning.Default;

        [Tooltip("Staging for the flock — the pool, how the birds fly between places, how many sit, and the " +
                 "day/night look. How many birds use a place, and when, live on its BirdHauntDef and the " +
                 "library's tuning; the rig's own numbers (frame times, speeds, altitude bands, the landing " +
                 "box) live on the SeagullVisualDef.")]
        [SerializeField] private GullConfig _config = GullConfig.Default;

        [Tooltip("How often (Hz) the flock steps. Gulls glide; a handful of Hz reads fine and stays cheap.")]
        [Min(5f)] [SerializeField] private float _tickHz = 30f;

        [Tooltip("The baked sheet + state table. Left empty, Resources/SeagullVisualDef is loaded.")]
        [SerializeField] private SeagullVisualDef _visual;

        [Tooltip("Every bird haunt and roost, and the schedule's knobs. Left empty, Resources/BirdHauntLibrary is loaded.")]
        [SerializeField] private BirdHauntLibrary _library;

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
            public IGullPerch Perch;

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

            /// <summary>The roster slot this pool bird draws, or −1: free, and hidden.</summary>
            public int Key = -1;

            /// <summary>Its slot as the last schedule tick placed it.</summary>
            public GullRosterBird Roster;

            /// <summary>The id hash of the place it is at or flying to, so a real change of place is
            /// told apart from the school list shifting under it.</summary>
            public uint PlaceHash;
        }

        /// <summary>A surface school the flock has seen near the view: its birds' place until a longest
        /// flight after it ends.</summary>
        private struct KnownSchool
        {
            public uint IdHash;
            public double CentreX, CentreY, Radius;
            public double StartSeconds, EndSeconds;
        }

        /// <summary>The schedule's weather at any game time, asked of the environment service (Core).
        /// With none, a calm sea at mean tide.</summary>
        private sealed class EnvironmentWeather : IGullWeather
        {
            public IEnvironmentService Service;

            public double SeaStateAt(double gameSeconds)
                => Service != null ? Mathf.Clamp01(Service.SeaState01At(gameSeconds)) : 0.0;

            public double TideAt(double gameSeconds)
                => Service != null ? Service.TideHeightAt(gameSeconds) : 0.0;
        }

        private Bird[] _birds;
        private SeagullFlockMath.SeagullFlockBird[] _wheel;
        private SeagullBehaviour _behaviour;
        private SeagullFlockMath.Mulberry32 _decisions;

        /// <summary>Reused by every school read — learning the schools near the view, the attractor's
        /// "are there fish about?" and the burst's "did she come down ON them?". One list, forever, like
        /// every other consumer of this seam.</summary>
        private readonly List<FishSchool> _shoals = new List<FishSchool>(16);

        // The places: the region's authored ones first, in the library's order, then the schools.
        private GullRoster _roster;
        private readonly List<BirdHauntDef> _defs = new List<BirdHauntDef>(MaxAuthoredPlaces);
        private readonly GullHauntSpec[] _places = new GullHauntSpec[MaxAuthoredPlaces + MaxSchools];
        private int _authored;
        private int _placeCount;
        private bool _pinned;
        private bool _regionBuilt;
        private bool _warnedFull;
        private string _regionId;
        private Rect _regionBounds;
        private GullRect _region;
        private readonly KnownSchool[] _schools = new KnownSchool[MaxSchools];
        private int _schoolCount;
        private GullTuningSpec _tuning;
        private double _sunriseHour;
        private double _sunsetHour;
        private readonly EnvironmentWeather _weather = new EnvironmentWeather();
        private double _refreshInMilliseconds;
        private bool _refreshed;
        private Rect _viewWorld;
        private int[] _fresh;
        private int _freshCount;

        // Per place, recounted every tick: its bound birds, those down or on the way down, those on a
        // perch, and how many should be down.
        private readonly int[] _placeBirds = new int[MaxAuthoredPlaces + MaxSchools];
        private readonly int[] _placeSettled = new int[MaxAuthoredPlaces + MaxSchools];
        private readonly int[] _placePerched = new int[MaxAuthoredPlaces + MaxSchools];
        private readonly int[] _placeWanted = new int[MaxAuthoredPlaces + MaxSchools];

        // The states the flock puts a bird into itself, looked up once.
        private int _fly, _stand, _float, _land, _splash;

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

            // No library is no places: an empty sky, except for a fixture's pinned place.
            if (_library == null) _library = Resources.Load<BirdHauntLibrary>(BirdHauntLibrary.ResourcesPath);
            ReadSunHours();
            BuildFlock();
        }

        private void OnEnable()
        {
            _tickTimer = 0f;
            _pendingSeconds = 0f;
            _refreshed = false;
            // The calls follow the birds this flock draws: through Core, never by name (rule 4).
            if (_birds != null) GameServices.BirdPresence = this;
        }

        private void OnDisable()
        {
            // A disabled flock is a silent one. Only this flock's own registration is cleared, so a
            // second flock a fixture made keeps its own.
            if (ReferenceEquals(GameServices.BirdPresence, this)) GameServices.BirdPresence = null;
            if (_birds == null) return;
            foreach (Bird bird in _birds) ReleasePerch(bird);
        }

        static void ReleasePerch(Bird bird)
        {
            GullPerches.Release(bird.Perch, bird);
            bird.Perch = null;
        }

        /// <summary>The sky's sunrise and sunset hours, from the shipped profile (or the built-in one).</summary>
        private void ReadSunHours()
        {
            DayNightProfile profile = Resources.Load<DayNightProfile>(DayNightProfilePath);
            bool made = profile == null;
            if (made) profile = DayNightProfile.CreateDefault();
            _sunriseHour = profile.SunriseHour;
            _sunsetHour = profile.SunsetHour;
            if (!made) return;
            if (Application.isPlaying) Destroy(profile);
            else DestroyImmediate(profile);
        }

        private void BuildFlock()
        {
            int n = Mathf.Max(0, _config.Count);
            _birds = new Bird[n];
            _wheel = new SeagullFlockMath.SeagullFlockBird[n];
            _fresh = new int[n];
            _decisions = SeagullFlockMath.Mulberry32.ForFlockSeed(_config.Seed);
            _roster = new GullRoster(_places.Length, MaxNearBirds, n);

            _fly = _behaviour.IndexOf(SeagullStates.Fly);
            _stand = _behaviour.IndexOf(SeagullStates.Stand);
            _float = _behaviour.IndexOf(SeagullStates.Float);
            _land = _behaviour.IndexOf(SeagullStates.Land);
            _splash = _behaviour.IndexOf(SeagullStates.Splash);

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
                    BurstState = -1,
                    RockPhase = _decisions.Next() * (Math.PI * 2.0),
                };
                ResetSim(_birds[i]);
                // Every pool bird starts free and hidden: the schedule decides which are drawn.
                go.SetActive(false);
            }
        }

        private void ResetSim(Bird b)
            => b.Sim = new SeagullSimBird { State = _fly, FlockBound = true, IntentState = -1 };

        private double SettleMilliseconds => _behaviour.Flock.SettleAfterSeconds * 1000.0;

        /// <summary>How far up the screen the highest wheeling bird is drawn, metres: a bird over a place
        /// just below the frame can still be inside it.</summary>
        private float LiftMetres() => (float)_behaviour.Flock.AltitudeMaxMetres * IsoGround.HeightScale;

        private void Update()
        {
            // The ONLY engine clock read in the system. Unscaled: a wing beat is a wing beat.
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

            if (!ViewRectWorld(out Rect view)) { HideAll(); return; }

            _simMilliseconds += deltaMilliseconds;

            IGameClock clock = GameServices.Clock;
            double now = _pinned || clock == null ? 0.0 : clock.TotalSeconds;

            // The schedule a few times a second: where every bird is, and which of them are drawn.
            _refreshInMilliseconds -= deltaMilliseconds;
            if (!_refreshed || _refreshInMilliseconds <= 0.0)
            {
                RefreshSchedule(view, clock, now);
                _refreshed = true;
                double hz = _config.ScheduleTicksPerSecond;
                _refreshInMilliseconds = hz > 0.0 ? 1000.0 / hz : 0.0;
            }
            else
            {
                CountPlaces();
            }

            Vector2 wind = AmbientGlobals.Wind;
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);

            // The breeze the rest of the coast reads, as a GROUND bearing: a world-space delta is a
            // screen angle until it is un-squashed, and the sheet's eight rows are ground bearings.
            double windHeading = wind.sqrMagnitude > 1e-8f
                ? IsoGround.BearingDegrees(wind) * Mathf.Deg2Rad
                : 0.0;
            // Every wheel rides downwind, as the one wheel always did.
            Vector2 drift = wind * _config.WindDrift;

            SeagullFlockMath.SeagullFlockTuning flockTuning = _behaviour.FlockTuning;
            SeagullFlockMath.Flock(_birds.Length, _simMilliseconds, _config.Seed,
                                   _config.WheelRadiusMetres, flockTuning, _wheel);

            float seaState = SeaState01();

            for (int i = 0; i < _birds.Length; i++)
            {
                Bird b = _birds[i];
                if (b.Key < 0) continue;   // free: hidden, and not stepped

                // A placed bird wheels over its place; a commuter rides its point on the flight line.
                SeagullFlockMath.SeagullFlockBird sample = _wheel[i];
                double centreX, centreY;
                if (b.Roster.Where == GullWhere.Commuting)
                {
                    b.Roster.PointAt(now, out centreX, out centreY);
                    sample = OnTheLine(b.Roster, sample, flockTuning);
                }
                else
                {
                    centreX = _places[b.Roster.At].CentreX;
                    centreY = _places[b.Roster.At].CentreY;
                }

                SeagullStateMachine.Step(ref b.Sim, deltaMilliseconds, _behaviour,
                                         sample, centreX + drift.x, centreY + drift.y);
                // Arriving turns an intent into a surface; both were already counted.
                SeagullStateMachine.TryAlight(ref b.Sim, _behaviour);

                // The water breaks here, between the step and the decision, so the signal carries the
                // position the bird is drawn at on this very frame.
                PublishSplash(b);

                Decide(b, windHeading);
                Draw(b, tint, dayOpacity, seaState);
            }
        }

        /// <summary>
        /// A commuter's wheel sample: on its point (no offset), at the wheel's height and wing beat, on
        /// the line's heading. It never swoops in transit: the swoop is a wheel's dive, not a crossing.
        /// </summary>
        private static SeagullFlockMath.SeagullFlockBird OnTheLine(
            in GullRosterBird line, in SeagullFlockMath.SeagullFlockBird wheel,
            in SeagullFlockMath.SeagullFlockTuning tuning)
        {
            double dx = line.ToX - line.FromX;
            double dy = line.ToY - line.FromY;
            double heading = dx * dx + dy * dy > 1e-12 ? Math.Atan2(dx, dy) : wheel.Heading;
            SeagullFlockMath.SeagullFlockAnim anim = wheel.Anim == SeagullFlockMath.SeagullFlockAnim.Swoop
                ? SeagullFlockMath.SeagullFlockAnim.Glide
                : wheel.Anim;
            int frames = Math.Max(1, tuning.FramesOf(anim));
            return new SeagullFlockMath.SeagullFlockBird(0.0, 0.0, wheel.Z, heading,
                                                         SeagullFlockMath.DirOf(heading), anim,
                                                         wheel.Frame % frames, 1.0);
        }

        // ── the schedule ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Places every bird at the game time, lists the ones near the view, and binds the pool to them:
        /// the region's places (rebuilt when the region changes), the surface schools near the view, the
        /// calendar and the weather, all through Core.
        /// </summary>
        private void RefreshSchedule(Rect view, IGameClock clock, double now)
        {
            _viewWorld = view;
            GullScheduleTuning knobs = _library != null && _library.Tuning != null ? _library.Tuning : DefaultTuning;
            _tuning = knobs.ToSpec(_sunriseHour, _sunsetHour);

            GullCalendar calendar;
            int count;
            if (_pinned)
            {
                calendar = StillCalendar();
                count = _authored;
                _weather.Service = null;
            }
            else
            {
                string regionId = GameServices.CurrentRegionId;
                Rect bounds = GameServices.CurrentRegionBounds;
                if (!_regionBuilt || !string.Equals(regionId, _regionId, StringComparison.Ordinal)
                    || bounds != _regionBounds)
                    BuildRegion(regionId, bounds);

                _weather.Service = GameServices.Environment;
                if (clock == null)
                {
                    // No clock, no schedule: an empty sky (the title screen, a bare scene).
                    calendar = StillCalendar();
                    count = 0;
                }
                else
                {
                    calendar = CalendarAt(clock);
                    // A school's birds fly in from the region's edge, so with no region there are none.
                    count = _region.IsEmpty ? _authored : AppendSchools(_authored, view, now);
                }
            }
            _placeCount = count;

            var settings = new GullRosterSettings
            {
                CommuteSpeedMetresPerSecond = _config.CommuteSpeedMetresPerSecond,
                ShowMarginMetres = _config.ShowMarginMetres,
                ShowHysteresisMetres = _config.ShowHysteresisMetres,
                LiftMetres = LiftMetres(),
                Region = _region,
            };
            _roster.Refresh(_places, Math.Min(_authored, count), count, _tuning, settings, calendar, _weather,
                            new GullRect(view.xMin, view.yMin, view.xMax, view.yMax));
            ApplyBinding();
        }

        /// <summary>The calendar at the clock's now, the world seed, and the visibility now.</summary>
        private static GullCalendar CalendarAt(IGameClock clock)
        {
            IEnvironmentService env = GameServices.Environment;
            GameConfig cfg = GameServices.Config;
            int weekday = (int)clock.Weekday;
            int daysPerWeek = cfg != null ? cfg.DaysPerWeek : (int)Weekday.Sunday + 1;
            int marketDay = cfg != null ? cfg.MarketDayIndex : (clock.IsMarketDay ? weekday : -1);
            return new GullCalendar
            {
                Seed = env != null ? env.WorldSeed : 0,
                Seconds = clock.TotalSeconds,
                SecondsPerHour = GameServices.SecondsPerDay / DaySchedule.HoursPerDay,
                Hour = clock.HourOfDay,
                Day = clock.DayIndex,
                Season = (int)clock.Season,
                DayOfSeason = clock.DayOfSeason,
                DaysPerSeason = GameServices.DaysPerSeason,
                Weekday = weekday,
                DaysPerWeek = daysPerWeek,
                MarketDayIndex = marketDay,
                Visibility = env != null ? env.Sample().Visibility : 1.0,
            };
        }

        /// <summary>A calendar that never moves: midnight on day 0, clear air, no market. An always-on
        /// place ignores all of it; the visibility keeps the settled share at its fine-day value.</summary>
        private static GullCalendar StillCalendar() => new GullCalendar
        {
            SecondsPerHour = GameServices.SecondsPerDay / DaySchedule.HoursPerDay,
            DayOfSeason = 1,
            DaysPerSeason = GameServices.DaysPerSeason,
            MarketDayIndex = -1,
            Visibility = 1.0,
        };

        /// <summary>
        /// The region's places, from the library: specs for the herring gull's entry at each, every
        /// haunt's roost found by id, and every place given a way out. Every pool bird is freed, so the
        /// next refresh draws the new region's birds.
        /// </summary>
        private void BuildRegion(string regionId, Rect bounds)
        {
            _regionBuilt = true;
            _regionId = regionId;
            _regionBounds = bounds;
            _region = bounds.width > 0f && bounds.height > 0f
                ? new GullRect(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax)
                : default;

            int n = _library != null ? _library.HauntsIn(regionId, _defs) : 0;
            if (n > MaxAuthoredPlaces)
            {
                if (!_warnedFull)
                {
                    _warnedFull = true;
                    Debug.LogWarning("GullFlock: " + regionId + " has " + n + " bird places; only the first " +
                                     MaxAuthoredPlaces + " are used.");
                }
                n = MaxAuthoredPlaces;
            }

            for (int i = 0; i < n; i++)
            {
                BirdHauntDef def = _defs[i];
                def.TryGetEntry(_config.SpeciesId, out BirdHauntEntry entry);
                _places[i] = def.ToSpec(entry, def.IsRoost ? -1 : RoostIndexOf(def.RoostId, n));
            }

            // The ways out: the roosts first, so a haunt with none of its own can take its roost's.
            for (int i = 0; i < n; i++)
                if (_places[i].IsRoost) WayOutFor(ref _places[i], -1);
            for (int i = 0; i < n; i++)
                if (!_places[i].IsRoost) WayOutFor(ref _places[i], _places[i].RoostIndex);

            _authored = n;
            _placeCount = n;
            _schoolCount = 0;
            _roster.ForgetNear();
            UnbindAll();
        }

        private int RoostIndexOf(string roostId, int count)
        {
            if (string.IsNullOrEmpty(roostId)) return -1;
            for (int i = 0; i < count; i++)
                if (_defs[i].IsRoost && string.Equals(_defs[i].Id, roostId, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>A place with no way out of its own takes its roost's, else straight out from the
        /// region's centre (up the map with no region).</summary>
        private void WayOutFor(ref GullHauntSpec spec, int roost)
        {
            if (spec.AwayX != 0.0 || spec.AwayY != 0.0) return;
            if (roost >= 0 && (_places[roost].AwayX != 0.0 || _places[roost].AwayY != 0.0))
            {
                spec.AwayX = _places[roost].AwayX;
                spec.AwayY = _places[roost].AwayY;
                return;
            }
            double dx = spec.CentreX - _region.CentreX;
            double dy = spec.CentreY - _region.CentreY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (_region.IsEmpty || !(length > 0.0))
            {
                spec.AwayX = 0.0;
                spec.AwayY = 1.0;
                return;
            }
            spec.AwayX = dx / length;
            spec.AwayY = dy / length;
        }

        // ── surface schools ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends the surface schools to the places: the ones still on the list (a school stays until
        /// its last birds are off the map, a longest flight after it ends), plus any new one showing
        /// near the view. Returns the place count.
        /// </summary>
        private int AppendSchools(int count, Rect view, double now)
        {
            double linger = LongestFlightSeconds();
            int kept = 0;
            for (int i = 0; i < _schoolCount; i++)
                if (now < _schools[i].EndSeconds + linger) _schools[kept++] = _schools[i];
            _schoolCount = kept;

            LearnSchools(view, now);

            for (int i = 0; i < _schoolCount && count < _places.Length; i++)
                _places[count++] = SchoolSpec(_schools[i]);
            return count;
        }

        /// <summary>
        /// Notes every school showing at the surface near the view (the same rectangle the roster counts
        /// as near, plus its hysteresis). A school deeper than the surface depth draws no birds. When the
        /// list is full, a new school takes the place of the one farthest from the view, if it is nearer.
        /// </summary>
        private void LearnSchools(Rect view, double now)
        {
            if (!(GameServices.FishSchools is IFishSchoolView fish)) return;

            float grow = Mathf.Max(0f, _config.ShowMarginMetres) + Mathf.Max(0f, _config.ShowHysteresisMetres);
            float lift = LiftMetres();
            var near = new Rect(view.xMin - grow, view.yMin - grow - lift,
                                view.width + 2f * grow, view.height + 2f * grow + lift);
            int n = fish.SchoolsInView(near, now, _shoals);
            float surface = SurfaceDepthMetres();
            double cx = view.center.x, cy = view.center.y;

            for (int i = 0; i < n && i < _shoals.Count; i++)
            {
                FishSchool school = _shoals[i];
                // Too deep to show, too deep to draw a bird.
                if (!GullSchedule.SchoolShows(school.DepthMetres, surface)) continue;
                uint id = SchoolHash(school);
                if (KnowsSchool(id)) continue;

                var known = new KnownSchool
                {
                    IdHash = id,
                    CentreX = school.Centre.x,
                    CentreY = school.Centre.y,
                    Radius = school.RadiusMetres,
                    StartSeconds = school.StartSeconds,
                    EndSeconds = school.EndSeconds,
                };
                if (_schoolCount < _schools.Length)
                {
                    _schools[_schoolCount++] = known;
                    continue;
                }

                int far = 0;
                double farthest = DistanceSq(_schools[0], cx, cy);
                for (int k = 1; k < _schoolCount; k++)
                {
                    double d = DistanceSq(_schools[k], cx, cy);
                    if (d <= farthest) continue;
                    far = k;
                    farthest = d;
                }
                if (DistanceSq(known, cx, cy) < farthest) _schools[far] = known;
            }
        }

        private bool KnowsSchool(uint id)
        {
            for (int i = 0; i < _schoolCount; i++)
                if (_schools[i].IdHash == id) return true;
            return false;
        }

        private static double DistanceSq(in KnownSchool s, double x, double y)
            => (s.CentreX - x) * (s.CentreX - x) + (s.CentreY - y) * (s.CentreY - y);

        /// <summary>A school's id: its centre and its start, hashed. The same school gives the same id on
        /// every read, so its birds keep their keys while it shows.</summary>
        private static uint SchoolHash(in FishSchool s)
        {
            long start = (long)Math.Round(s.StartSeconds * HashResolution);
            uint h = GullHash.Mix(SchoolHashSalt, (int)Math.Round(s.Centre.x * HashResolution));
            h = GullHash.Mix(h, (int)Math.Round(s.Centre.y * HashResolution));
            h = GullHash.Mix(h, unchecked((int)start));
            return GullHash.Mix(h, (int)(start >> 32));
        }

        /// <summary>A school as a place: a disc on open water with no roost, its birds the tuning's
        /// SchoolBirds while it shows, leaving the way the nearest roost's birds leave.</summary>
        private GullHauntSpec SchoolSpec(in KnownSchool s)
        {
            int capacity = (int)Math.Ceiling(_tuning.SchoolBirds * _tuning.SchoolSeason.Max);
            var spec = new GullHauntSpec
            {
                IdHash = s.IdHash,
                Kind = BirdHauntKind.Loafing,   // unread for a school: IsSchool decides
                IsSchool = true,
                CentreX = s.CentreX,
                CentreY = s.CentreY,
                HalfX = s.Radius,
                HalfY = s.Radius,
                RoostIndex = -1,
                Capacity = Math.Max(0, Math.Min(GullRoster.MaxSchoolSlots, capacity)),
                SchoolStartSeconds = s.StartSeconds,
                SchoolEndSeconds = s.EndSeconds,
            };
            WayOutFor(ref spec, NearestRoost(s.CentreX, s.CentreY));
            return spec;
        }

        private int NearestRoost(double x, double y)
        {
            int best = -1;
            double bestD = double.MaxValue;
            for (int i = 0; i < _authored; i++)
            {
                if (!_places[i].IsRoost) continue;
                double dx = _places[i].CentreX - x, dy = _places[i].CentreY - y;
                double d = dx * dx + dy * dy;
                if (d >= bestD) continue;
                bestD = d;
                best = i;
            }
            return best;
        }

        /// <summary>The longest flight in the region, game seconds: its diagonal at the commute speed.</summary>
        private double LongestFlightSeconds()
        {
            double speed = _config.CommuteSpeedMetresPerSecond;
            if (_region.IsEmpty || !(speed > 0.0)) return 0.0;
            double w = _region.MaxX - _region.MinX, h = _region.MaxY - _region.MinY;
            return Math.Sqrt(w * w + h * h) / speed;
        }

        // ── the pool ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Follows the roster's binding. A pool bird whose slot left the near set is freed and hidden; a
        /// pool bird given a new slot materialises in it; one whose slot changed place (it took off for
        /// the roost, it arrived) is sent to its new wheel. A slot that stays, stays as it is.
        /// </summary>
        private void ApplyBinding()
        {
            _freshCount = 0;
            int[] poolKey = _roster.PoolKey;
            int[] poolNear = _roster.PoolNear;
            for (int i = 0; i < _birds.Length; i++)
            {
                Bird b = _birds[i];
                int key = i < poolKey.Length ? poolKey[i] : -1;
                if (key < 0)
                {
                    if (b.Key >= 0) Unbind(b);
                    continue;
                }

                GullRosterBird r = _roster.Near[poolNear[i]];
                uint placeHash = r.At >= 0 ? _places[r.At].IdHash : 0u;
                if (b.Key != key)
                {
                    Unbind(b);
                    b.Key = key;
                    b.Roster = r;
                    b.PlaceHash = placeHash;
                    _fresh[_freshCount++] = i;
                    continue;
                }

                bool moved = r.Where != b.Roster.Where || placeHash != b.PlaceHash;
                b.Roster = r;
                b.PlaceHash = placeHash;
                if (moved) Moved(b);
            }

            CountPlaces();
            for (int k = 0; k < _freshCount; k++) Materialise(_birds[_fresh[k]]);
        }

        private void UnbindAll()
        {
            if (_birds == null) return;
            for (int i = 0; i < _birds.Length; i++)
                if (_birds[i].Key >= 0) Unbind(_birds[i]);
        }

        /// <summary>Frees a pool bird: back on the wheel, nothing claimed, hidden.</summary>
        private void Unbind(Bird b)
        {
            ReleasePerch(b);
            b.Key = -1;
            b.Roster = default;
            b.PlaceHash = 0u;
            b.Carrying = false;
            b.BurstState = -1;
            ResetSim(b);
            if (b.Renderer.gameObject.activeSelf) b.Renderer.gameObject.SetActive(false);
        }

        /// <summary>
        /// Its slot changed place. In the air it gives up any descent and blends onto its new wheel (or
        /// its flight line) over the rig's regroup time. On a surface, <see cref="Decide"/> gets it up.
        /// </summary>
        private void Moved(Bird b)
        {
            if (_behaviour.SurfaceOf(b.Sim.State) != SeagullSurface.None) return;
            if (b.Sim.IntentState >= 0) ReleasePerch(b);
            b.Sim.IntentState = -1;
            b.Sim.FlockBound = false;
            b.Sim.RejoinMilliseconds = 1.0;   // any positive value arms the ramp
        }

        /// <summary>
        /// A pool bird takes up its new slot in the state the roster gives: a commuter on its line and a
        /// bird over its place on the wheel (both at the first step), or, while its place wants more of
        /// its birds down, already sitting — on a perch near its spot, on its spot, or on one of its
        /// alternates. It never publishes a splash: nobody watched it come down.
        /// </summary>
        private void Materialise(Bird b)
        {
            b.NextDecisionMilliseconds = _simMilliseconds + _decisions.Next() * SettleMilliseconds;
            b.Sim.Heading = _decisions.Next() * (Math.PI * 2.0);
            b.Sim.Dir = SeagullFlockMath.DirOf(b.Sim.Heading);
            b.Sim.X = b.Roster.X;
            b.Sim.Y = b.Roster.Y;
            if (b.Roster.Where == GullWhere.Commuting) return;

            int p = b.Roster.At;
            if (p < 0 || _placeSettled[p] >= _placeWanted[p]) return;
            if (TrySitOnPerch(b, p) || TrySitAt(b, b.Roster.X, b.Roster.Y) || TrySitOnAlternate(b, p))
                _placeSettled[p]++;
        }

        /// <summary>Is there room on a perch? About <see cref="GullConfig.PerchShare"/> of a place's
        /// sitting birds stand on one, when one is free near their spot.</summary>
        private bool PerchQuotaOpen(int p) => _placePerched[p] < _config.PerchShare * (_placeSettled[p] + 1);

        private bool TrySitOnPerch(Bird b, int p)
        {
            if (!PerchQuotaOpen(p)) return false;
            var spot = new Vector2((float)b.Roster.X, (float)b.Roster.Y);
            if (!GullPerches.TryClaim(spot, _config.PerchSearchHalfSize, b, out IGullPerch perch)) return false;

            Vector2 at = perch.ScreenPoint;
            SeagullStateMachine.Enter(ref b.Sim, _stand, _behaviour);
            b.Sim.X = at.x;
            b.Sim.Y = at.y;
            b.Perch = perch;
            _placePerched[p]++;
            return true;
        }

        /// <summary>Sits the bird down on a spot as <see cref="SurfaceAt"/> finds it: standing on
        /// ground, floating on water. False where there is no place to put down.</summary>
        private bool TrySitAt(Bird b, double x, double y)
        {
            int state = SurfaceAt(new Vector2((float)x, (float)y)) switch
            {
                SeagullSurface.Ground => _stand,
                SeagullSurface.Water => _float,
                _ => -1,
            };
            if (state < 0) return false;
            SeagullStateMachine.Enter(ref b.Sim, state, _behaviour);
            b.Sim.X = x;
            b.Sim.Y = y;
            return true;
        }

        private bool TrySitOnAlternate(Bird b, int p)
        {
            for (int k = 0; k < AlternateSpots; k++)
            {
                // Keyed by the bird's own key, so two visitors from different haunts sharing a roost
                // never share a fallback.
                _roster.AlternateSpot(p, b.Roster.Key, _roster.NowInputs.Day, k, out double x, out double y);
                if (TrySitAt(b, x, y)) return true;
            }
            return false;
        }

        /// <summary>
        /// Per place: its bound birds (commuters belong to no place until they arrive), how many are
        /// down or on the way down, how many are on a perch, and how many should be down. Every bird at
        /// a roost sits; elsewhere the settled share rises toward the fog's and the gale's.
        /// </summary>
        private void CountPlaces()
        {
            for (int p = 0; p < _placeCount; p++)
            {
                _placeBirds[p] = 0;
                _placeSettled[p] = 0;
                _placePerched[p] = 0;
            }
            for (int i = 0; i < _birds.Length; i++)
            {
                Bird b = _birds[i];
                if (b.Key < 0 || b.Roster.Where == GullWhere.Commuting) continue;
                int p = b.Roster.At;
                if (p < 0 || p >= _placeCount) continue;
                _placeBirds[p]++;
                if (_behaviour.SurfaceOf(b.Sim.State) != SeagullSurface.None || b.Sim.IntentState >= 0)
                    _placeSettled[p]++;
                if (b.Perch != null) _placePerched[p]++;
            }

            double share = SettledShare();
            for (int p = 0; p < _placeCount; p++)
                _placeWanted[p] = _places[p].IsRoost
                    ? _placeBirds[p]
                    : Mathf.RoundToInt((float)(_placeBirds[p] * share));
        }

        /// <summary>The share of a place's birds that sits (<see cref="GullSchedule.SettledShare"/>): the
        /// fine-day share, raised toward the fog's as the air thickens and toward the gale's as the wind
        /// rises.</summary>
        private double SettledShare()
        {
            GullScheduleInputs now = _roster.NowInputs;
            return GullSchedule.SettledShare(_tuning, _config.SettledFraction, _config.FogSettledFraction,
                                             _config.GaleSettledFraction, now.Storm, now.Visibility);
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
            b.CarryDepartByMilliseconds = _simMilliseconds + SettleMilliseconds;
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

        private void Decide(Bird b, double windHeading)
        {
            SeagullSurface on = _behaviour.SurfaceOf(b.Sim.State);
            int p = b.Roster.At;

            if (b.Perch != null && !GullPerches.IsClaimedBy(b.Perch, b))
            {
                // The perch went away under her (its piece was unloaded): back to the wheel.
                if (p >= 0 && b.Roster.Where != GullWhere.Commuting)
                {
                    _placePerched[p]--;
                    if (on != SeagullSurface.None || b.Sim.IntentState >= 0) _placeSettled[p]--;
                }
                b.Perch = null;
                b.Sim.IntentState = -1;
                b.Sim.FlockBound = true;
                return;
            }

            // On her way somewhere else: up off whatever she is on, and onto her line.
            if (b.Roster.Where == GullWhere.Commuting)
            {
                if (on != SeagullSurface.None && SeagullStateMachine.CommandDepart(ref b.Sim, _behaviour))
                {
                    ReleasePerch(b);
                    b.Carrying = false;
                }
                return;
            }
            if (p < 0) return;

            // A bird that has wandered off her place's ground gets up and goes back to its wheel.
            if (on != SeagullSurface.None && b.Perch == null && OutsideItsPlace(b, p))
            {
                if (SeagullStateMachine.CommandDepart(ref b.Sim, _behaviour))
                {
                    b.Carrying = false;
                    _placeSettled[p]--;
                }
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
                    if (b.Perch != null) _placePerched[p]--;
                    ReleasePerch(b);
                    _placeSettled[p]--;
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
                    _placeSettled[p]--;
                    return;
                }
                if (_simMilliseconds >= b.CarryDepartByMilliseconds) b.Carrying = false;
            }

            if (_simMilliseconds < b.NextDecisionMilliseconds) return;
            b.NextDecisionMilliseconds = _simMilliseconds + SettleMilliseconds;

            if (on != SeagullSurface.None)
            {
                // At the roost a gull stands and sleeps, on a perch or off it.
                if (_places[p].IsRoost) { RoostDown(b); return; }
                // A perch is a point, not a walking surface. Leave it after a settle window.
                if (b.Perch != null)
                {
                    if (SeagullStateMachine.CommandDepart(ref b.Sim, _behaviour))
                    {
                        ReleasePerch(b);
                        _placeSettled[p]--;
                        _placePerched[p]--;
                    }
                    return;
                }
                if (_placeSettled[p] > _placeWanted[p] && SeagullStateMachine.CommandDepart(ref b.Sim, _behaviour))
                {
                    _placeSettled[p]--;
                    return;
                }
                SurfaceActivity(b);
                return;
            }

            if (_placeSettled[p] >= _placeWanted[p]) return;
            // Still getting up (the departures above leave her window where it was) or already on her
            // way down: not this window. By the next she is back on her wheel.
            if (!CanBeAskedDown(_behaviour, b.Sim.State)) return;
            if (TryPutDownAtPlace(b, windHeading)) _placeSettled[p]++;
        }

        /// <summary>
        /// Can a bird in the air be asked down from this state? Only from the three the wheel flies —
        /// <c>fly</c>, <c>glide</c>, <c>swoop</c> — the ones a commanded descent can start from.
        /// <c>takeoff</c> has no edge to <c>swoop</c> or <c>land</c>: asked down while still getting
        /// up, a gull keeps the intent with no state that loses height, chains into <c>fly</c> with her
        /// rejoin cancelled, and hangs a metre up until the deadline hands her back to the wheel. One
        /// already coming down (<c>land</c>, <c>splash</c>, <c>dive</c>) is not asked again.
        /// </summary>
        internal static bool CanBeAskedDown(SeagullBehaviour behaviour, int state)
            => behaviour != null && behaviour.IsFlockDriven(state);

        /// <summary>At the roost: a gull stands and sleeps. Nothing else on a surface there.</summary>
        private void RoostDown(Bird b)
        {
            if (b.Sim.State != _stand) SeagullStateMachine.TryTransition(ref b.Sim, _stand, _behaviour);
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
                    SeagullStateMachine.TryTransition(ref b.Sim, _stand, _behaviour);
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
        /// Puts the bird down at its place: on a free perch near its own spot (about
        /// <see cref="GullConfig.PerchShare"/> of the sitters), else on its own spot — <c>land</c> onto
        /// flat exposed ground, <c>splash</c> onto water — else on one of its hashed alternates. A bird
        /// that finds nowhere to put down keeps wheeling and asks again next window.
        /// </summary>
        private bool TryPutDownAtPlace(Bird b, double windHeading)
        {
            int p = b.Roster.At;
            var spot = new Vector2((float)b.Roster.X, (float)b.Roster.Y);

            if (PerchQuotaOpen(p) && GullPerches.TryClaim(spot, _config.PerchSearchHalfSize, b, out IGullPerch perch))
            {
                Vector2 target = perch.ScreenPoint;
                if (SeagullStateMachine.CommandAlight(ref b.Sim, _land, target.x, target.y, windHeading, _behaviour))
                {
                    b.Perch = perch;
                    _placePerched[p]++;
                    b.AlightDeadlineMilliseconds = _simMilliseconds + SettleMilliseconds;
                    return true;
                }
                GullPerches.Release(perch, b);
            }

            if (_places[p].IsSchool) spot = PulledToShoal(spot);
            if (TryDescendTo(b, spot, windHeading)) return true;

            for (int k = 0; k < AlternateSpots; k++)
            {
                _roster.AlternateSpot(p, b.Roster.Key, _roster.NowInputs.Day, k, out double x, out double y);
                if (TryDescendTo(b, new Vector2((float)x, (float)y), windHeading)) return true;
            }
            return false;
        }

        /// <summary>Asks the world what is at the spot and commands the descent onto it. One answer:
        /// false where there is no place to put down.</summary>
        private bool TryDescendTo(Bird b, Vector2 spot, double windHeading)
        {
            int arrival = SurfaceAt(spot) switch
            {
                SeagullSurface.Ground => _land,
                SeagullSurface.Water => _splash,
                _ => -1,
            };
            if (arrival < 0) return false;

            if (!SeagullStateMachine.CommandAlight(ref b.Sim, arrival, spot.x, spot.y,
                                                   windHeading, _behaviour))
                return false;

            b.AlightDeadlineMilliseconds = _simMilliseconds + SettleMilliseconds;
            return true;
        }

        /// <summary>
        /// THE ONE ATTRACTOR THIS GAME CAN HONOUR — the drop's <c>shoal_surface</c>, weight 0.5.
        ///
        /// <para>A gull working a surface school comes down AT the fish showing there rather than at a
        /// patch of empty sea, so her spot is pulled toward the nearest surface school inside the
        /// flock's own <c>attract_radius_m</c>. The weight is the pull, straight from the drop.</para>
        ///
        /// <para><b>It spends no randomness.</b> The spot is a hash of the bird's slot, made the same
        /// way whether there are fish about or not, so the decision stream does not fork on whether a
        /// shoal exists — two flocks on one seed still fly the same flight, and a test that says so
        /// stays honest.</para>
        ///
        /// <para><b>It does not decide where the bird lands</b>, only where she looks:
        /// <see cref="SurfaceAt"/> still gets the last word, and a pull that lands the spot on a rock is
        /// refused exactly as any other spot would be.</para>
        ///
        /// <para>The drop's other five attractors — gutting 1.0, chum 1.0, the open tub 0.8, the
        /// trawler wake 0.7, the bait bucket 0.6 — name things that do not exist in the game today
        /// (no catch handling on deck, no discards over the side, no tub with a lid state, no wake
        /// behind a working trawler, no bait bucket). They are logged in the backlog, not built.</para>
        /// </summary>
        private Vector2 PulledToShoal(Vector2 spot)
        {
            float weight = _visual != null ? _visual.ShoalAttractWeight01 : 0f;
            if (!(weight > 0f)) return spot;
            if (!(GameServices.FishSchools is IFishSchoolView view)) return spot;

            float reach = (float)_behaviour.Flock.AttractRadiusMetres;
            if (!(reach > 0f)) return spot;

            var clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            int n = view.SchoolsInView(
                new Rect(spot.x - reach, spot.y - reach, reach * 2f, reach * 2f), now, _shoals);
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

        /// <summary>Has a bird on the ground wandered off its place's box (by the slack)? A school's box
        /// is its disc's square.</summary>
        private bool OutsideItsPlace(Bird b, int p)
        {
            double halfX = _places[p].HalfX;
            double halfY = _places[p].IsSchool ? halfX : _places[p].HalfY;
            return Math.Abs(b.Sim.X - _places[p].CentreX) > halfX * OutsideSlack
                || Math.Abs(b.Sim.Y - _places[p].CentreY) > halfY * OutsideSlack;
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

        // ── the view ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE ONLY PLACE THE CAMERA IS READ: its view on the ground plane, world metres. It chooses
        /// which birds are drawn and heard, never where any bird is. False with no camera.
        /// </summary>
        private static bool ViewRectWorld(out Rect view)
        {
            Camera cam = AmbientGlobals.ResolveCamera();
            if (cam == null)
            {
                view = default;
                return false;
            }
            Vector3 at = cam.transform.position;
            float halfHeight = cam.orthographic
                ? cam.orthographicSize
                : Mathf.Abs(at.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * cam.aspect;
            view = new Rect(at.x - halfWidth, at.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
            return true;
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
                : YSortSprite.OrderFor(b.Perch?.SortY ?? pivot.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
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

        // ── the birds the calls follow (IBirdPresence) ──────────────────────────────────────────────

        /// <inheritdoc/>
        public Rect ViewWorld => _viewWorld;

        /// <inheritdoc/>
        public int NearCount => _roster != null ? _roster.NearCount : 0;

        /// <inheritdoc/>
        public int CopyNear(BirdAudible[] into)
        {
            if (into == null || _roster == null) return 0;
            int n = Math.Min(into.Length, _roster.StoredCount);
            GullScheduleInputs now = _roster.NowInputs;
            for (int k = 0; k < n; k++)
            {
                GullRosterBird r = _roster.Near[k];
                int pool = _roster.NearPool[k];
                Bird b = pool >= 0 && pool < _birds.Length && _birds[pool].Key == r.Key ? _birds[pool] : null;

                // A drawn bird is heard where she is drawn; one only counted, where the roster put her.
                Vector2 at;
                bool down;
                if (b != null)
                {
                    at = new Vector2((float)b.Sim.X, (float)b.Sim.Y);
                    down = _behaviour.SurfaceOf(b.Sim.State) != SeagullSurface.None;
                }
                else
                {
                    at = new Vector2((float)r.X, (float)r.Y);
                    down = r.Where == GullWhere.AtRoost;
                }
                into[k] = new BirdAudible(at, ActivityOf(r, down, now), r.Key, _config.SpeciesId);
            }
            return n;
        }

        /// <summary>What a near bird is doing, for the calls: working food over a school, or at a fish
        /// plant or a wharf in its working hours; resting when down or at the roost; otherwise aloft.</summary>
        private BirdActivity ActivityOf(in GullRosterBird r, bool down, in GullScheduleInputs now)
        {
            if (r.Where == GullWhere.Commuting) return BirdActivity.Aloft;
            if (r.At >= 0 && r.At < _placeCount)
            {
                if (_places[r.At].IsSchool) return BirdActivity.Feeding;
                BirdHauntKind kind = _places[r.At].Kind;
                if (r.Where == GullWhere.AtHaunt
                    && (kind == BirdHauntKind.FishPlant || kind == BirdHauntKind.Wharf)
                    && GullSchedule.IsBusy(_tuning, _places[r.At], now.Hour, now.Storm, now.Visibility))
                    return BirdActivity.Feeding;
            }
            return down || r.Where == GullWhere.AtRoost ? BirdActivity.Resting : BirdActivity.Aloft;
        }

        // ── the fixture's seam ──────────────────────────────────────────────────────────────────────
        //
        // A PlayMode fixture drives Tick(dt) directly and reads these back. They are an inspection
        // surface, not a control surface: nothing here writes sim state except CommandLanding, which is
        // the one thing a test cannot express any other way (it is a plan, and the planner is private),
        // and PinFixtureHaunt, which gives a fixture with no region and no clock one place to watch.

        /// <summary>How many birds the flock built (the pool). 0 with no sheet.</summary>
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

        /// <summary>How many places counted as near the view at the last schedule tick.</summary>
        public int NearPlaceCount
        {
            get
            {
                if (_roster == null) return 0;
                int n = 0;
                for (int p = 0; p < _placeCount; p++)
                    if (_roster.PlaceNear(p)) n++;
                return n;
            }
        }

        /// <summary>The slot a pool bird draws at its place, or −1 when it is free.</summary>
        public int BoundSlotOf(int bird) => _birds[bird].Key >= 0 ? _birds[bird].Roster.Slot : -1;

        /// <summary>The key of the slot a pool bird draws (stable while it stays near), or −1.</summary>
        public int BoundKeyOf(int bird) => _birds[bird].Key;

        /// <summary>Where a pool bird's slot is, by the last schedule tick; away when it is free.</summary>
        public GullWhere BoundWhereOf(int bird) => _birds[bird].Key >= 0 ? _birds[bird].Roster.Where : GullWhere.Away;

        /// <summary>The id of the place a pool bird is at or flying to: an authored place's id, the
        /// fixture place's, or null for a school, an away line or a free bird.</summary>
        public string BoundPlaceIdOf(int bird)
        {
            Bird b = _birds[bird];
            if (b.Key < 0) return null;
            int p = b.Roster.At;
            if (_pinned) return p == 0 ? FixtureHauntId : null;
            return p >= 0 && p < _authored && p < _defs.Count ? _defs[p].Id : null;
        }

        /// <summary>
        /// Gives a fixture ONE always-on place with no region and no clock: a box on the ground with
        /// <paramref name="birdsOnShow"/> birds at it round the clock, and nothing else. The library's
        /// places are set aside until the flock is made again. Every pool bird is freed, so the next
        /// tick materialises the place's birds.
        /// </summary>
        public void PinFixtureHaunt(Vector2 centre, Vector2 halfSize, int birdsOnShow)
        {
            if (_roster == null) return;
            _pinned = true;
            _places[0] = new GullHauntSpec
            {
                IdHash = GullHash.Id(FixtureHauntId),
                Kind = BirdHauntKind.Loafing,
                AlwaysOn = true,
                CentreX = centre.x,
                CentreY = centre.y,
                HalfX = Mathf.Abs(halfSize.x),
                HalfY = Mathf.Abs(halfSize.y),
                RoostIndex = -1,
                Capacity = Mathf.Clamp(birdsOnShow, 0, GullRoster.MaxSlotsPerPlace),
            };
            _authored = 1;
            _placeCount = 1;
            _schoolCount = 0;
            _regionBuilt = false;
            _region = default;
            _roster.ForgetNear();
            UnbindAll();
            _refreshed = false;
        }

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
