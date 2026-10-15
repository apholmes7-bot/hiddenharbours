using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE GULLS BELONG TO PLACES, AND THEIR CALLS FOLLOW THEM.</b> The owner's two complaints, as
    /// running scenes: the gulls followed the middle of the screen, and the gull sound never stopped.
    /// Here a flock runs St Peters' own places from the library, under a clock and a sea the fixture
    /// holds still, and the camera only chooses which birds are drawn. It leaves, and the cannery's
    /// birds stay at the cannery; it comes back, and the same birds are there. The hour, the wind and
    /// the tide move them. The director's gull voices are silent over open water and call at the
    /// cannery, on the cannery's side.
    ///
    /// <para><b>The fixture is the clock</b>, as in <see cref="SeagullLandingPlayTests"/>: every flock
    /// measured here is DISABLED before its first frame and stepped through
    /// <see cref="GullFlock.Tick(double)"/>, and the installed flock is stood down for the duration. Only
    /// the plates run the game's own flock, in the game's own region.</para>
    ///
    /// <para><b>Every count is the schedule's, not the pool's.</b> Each count first asserts that the pool
    /// holds every bird near the view, so a smaller number means fewer birds, never a full pool.</para>
    /// </summary>
    public class GullHauntPlayTests
    {
        const string StPetersScene = "StPeters";
        const string PlateDir = "GullHauntPlates";
        const string RegionId = "region.st_peters";

        /// <summary>St Peters' ground, as its RegionDef gives it: WorldCenter ± WorldSizeMeters / 2.</summary>
        static readonly Rect RegionBounds = new Rect(-380f, -260f, 760f, 520f);

        const string Cannery = "birdhaunt.stp_cannery";
        const string Wharf = "birdhaunt.stp_wharf";
        const string EastFlats = "birdhaunt.stp_crossing_east";
        const string Whelps = "birdroost.stp_whelps";
        const string Sisters = "birdroost.stp_sisters";

        /// <summary>Open water south of the island, more than a hundred metres from every place.</summary>
        static readonly Vector2 OpenWater = new Vector2(0f, -200f);

        /// <summary>How far the camera goes when it leaves: the owner's 200 m.</summary>
        const float LeaveMetres = 200f;

        /// <summary>A short look along the coast, with the cannery still in view.</summary>
        const float LookAlongMetres = 30f;

        /// <summary>For the calls, the middle of the view sits this far west of the cannery's box, so
        /// every bird near the view is east of the middle (the rig's wheel is 6 m round its place).</summary>
        const float EastOfTheMiddleMetres = 6f;

        const float OrthoSize = 8f;

        /// <summary>The fixture's own aspect: the view, and so which places are near, must not depend
        /// on the size of the editor's window.</summary>
        const float FixtureAspect = 16f / 9f;

        const int WorldSeed = 7;
        const float MeanWater = 0f, LowWater = -1.9f, HighWater = 1.9f;
        const float Calm = 0f, Gale = 1f;
        const float MorningHour = 9f, DawnHour = 6.5f, NoonHour = 13f, DuskHour = 19.5f, NightHour = 23f;
        static readonly float[] PlateHours = { DawnHour, NoonHour, DuskHour, NightHour };

        const double FrameMilliseconds = 1000.0 / 60.0;

        /// <summary>Two seconds of frames: eight schedule ticks, and every near bird bound.</summary>
        const int SettleFrames = 120;

        const float ListenSeconds = 10f;

        /// <summary>Longer than one call can run (the longest excerpt at the lowest pitch).</summary>
        const float QuietWaitSeconds = 5f;

        /// <summary>Long enough for the 4 Hz schedule and the director's 4 Hz listen to see a change.</summary>
        const float ScheduleSettleSeconds = 0.75f;

        /// <summary>Calls enough to show the side is no accident.</summary>
        const int CallsToHear = 3;

        const int WarmUpTicks = 40;
        const int MeasuredTicks = 100;

        /// <summary>The call player's voice names (<c>GullCallPlayer.VoicePrefix</c>). This assembly does
        /// not reference Audio, so the name is how the voices are found.</summary>
        const string GullVoicePrefix = "GullCall ";

        const int SeasonsPerYear = (int)Season.HardWinter + 1;
        const int DaysPerWeek = (int)Weekday.Sunday + 1;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();

        GullFlock _installed;
        bool _installedWasEnabled;
        Camera _camera;
        StandingClock _clock;
        StandingSea _sea;
        WharfNightStage _stage;
        object _sink;

        Color _tintBefore;
        Vector4 _windBefore;
        float _timeScaleBefore;
        IGameClock _clockBefore;
        IEnvironmentService _environmentBefore;
        IFishSchools _fishBefore;
        IBirdPresence _presenceBefore;
        string _regionIdBefore;
        Rect _regionBoundsBefore;
        bool _introOnEntry;

        /// <summary>A clock that stands where the test puts it, its calendar read from its seconds with
        /// Core's day and season lengths, the way the flock reads the game's.</summary>
        sealed class StandingClock : IGameClock
        {
            public double Seconds;

            static double PerDay => GameServices.SecondsPerDay;

            public double TotalSeconds => Seconds;
            public GameTime Now => new GameTime(Seconds);
            public int DayIndex => (int)Math.Floor(Seconds / PerDay);
            public Season Season => (Season)(DayIndex / GameServices.DaysPerSeason % SeasonsPerYear);
            public int Year => 1 + DayIndex / (GameServices.DaysPerSeason * SeasonsPerYear);
            public int DayOfSeason => DayIndex % GameServices.DaysPerSeason + 1;
            public Weekday Weekday => (Weekday)(DayIndex % DaysPerWeek);
            public bool IsMarketDay => false;
            public float HourOfDay => (float)((Seconds - DayIndex * PerDay) / PerDay * DaySchedule.HoursPerDay);
            public float DayFraction => HourOfDay / DaySchedule.HoursPerDay;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>A sea that stands still: one sea state, one tide and clear air at every time, so the
        /// gale's tell and the tide's look-back read what the test set.</summary>
        sealed class StandingSea : IEnvironmentService
        {
            public float Storm01 = Calm;
            public float Tide = MeanWater;
            public float Visibility = 1f;

            public int WorldSeed => GullHauntPlayTests.WorldSeed;
            public TideProfile ActiveTideProfile { get; set; }

            public EnvironmentSample Sample() => new EnvironmentSample(
                Vector2.zero, Vector2.zero, Tide,
                (SeaState)Mathf.RoundToInt(Storm01 * (float)SeaState.Storm), Visibility, Storm01);

            public float TideHeightAt(double totalSeconds) => Tide;
            public float SeaState01At(double totalSeconds) => Storm01;
        }

        [SetUp]
        public void SetUp()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);

            _tintBefore = Shader.GetGlobalColor("_DayNightTint");
            _windBefore = Shader.GetGlobalVector("_WindWorld");
            _timeScaleBefore = Time.timeScale;
            _clockBefore = GameServices.Clock;
            _environmentBefore = GameServices.Environment;
            _fishBefore = GameServices.FishSchools;
            _presenceBefore = GameServices.BirdPresence;
            _regionIdBefore = GameServices.CurrentRegionId;
            _regionBoundsBefore = GameServices.CurrentRegionBounds;
            _introOnEntry = GameServices.OpeningCinematicRunning;

            // The host is hidden and DontDestroyOnLoad, so it is found among ALL loaded objects.
            _installed = FindInstalledFlock();
            Assert.IsNotNull(_installed, "GullFlock did not install itself: the gulls are wired into no scene");
            // It runs on the wall clock in every PlayMode scene. Stand it down, so the only flock stepped
            // here is the fixture's (the plates stand it up again).
            _installedWasEnabled = _installed.enabled;
            _installed.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Shader.SetGlobalColor("_DayNightTint", _tintBefore);
            Shader.SetGlobalVector("_WindWorld", _windBefore);
            foreach (GameObject go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
            _camera = null;
            _sink = null;

            bool loaded = _stage != null;
            if (loaded)
            {
                // The ashore plates' teardown: the stage unloads the region, then every DontDestroyOnLoad
                // root the region installed is destroyed by IDENTITY against the snapshot, never by name.
                yield return _stage.TearDown();
                _stage = null;
                foreach (GameObject go in PersistentRoots())
                    if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
                GameServices.Reset();
                GameServices.OpeningCinematicRunning = _introOnEntry;
                ShellFlow.Reset();
                InteractionGate.Reset();
            }
            else
            {
                GameServices.Clock = _clockBefore;
                GameServices.Environment = _environmentBefore;
                GameServices.FishSchools = _fishBefore;
                GameServices.CurrentRegionId = _regionIdBefore;
                GameServices.CurrentRegionBounds = _regionBoundsBefore;
            }
            Time.timeScale = _timeScaleBefore;

            if (_installed != null)
            {
                _installed.enabled = false;   // and back: its OnEnable puts it on the Core seam again
                _installed.enabled = _installedWasEnabled;
            }
            if (!loaded) GameServices.BirdPresence = _presenceBefore;
            yield return null;
        }

        // ── the camera leaves, and comes back ──────────────────────────────────────────────────────

        /// <summary>
        /// St Peters at 09:00 in High Summer, calm. The cannery's birds wheel over the cannery with the
        /// camera on it and with the camera 30 m along the coast; with the camera 200 m away, not one
        /// cannery bird is drawn, so none followed it.
        /// </summary>
        [Test]
        public void TheFlockStaysWhenTheCameraLeaves()
        {
            OnTheCoast();
            BirdHauntDef cannery = Place(Cannery);
            GullFlock flock = NewFlock("Gulls_StayPut");
            _clock.Seconds = At(SummerDay, MorningHour);

            Frame(cannery.Centre);
            Settle(flock);
            AssertEveryNearBirdDrawn(flock, "framed on the cannery");
            var keys = new List<int>();
            for (int i = 0; i < flock.BirdCount; i++)
                if (BoundAt(flock, i, Cannery, GullWhere.AtHaunt)) keys.Add(flock.BoundKeyOf(i));
            Assert.Greater(keys.Count, 0, "no birds at the cannery at 09:00 in High Summer: nothing to watch");
            AssertOverItsPlace(flock, cannery, "framed on the cannery");

            Frame(cannery.Centre + new Vector2(-LookAlongMetres, 0f));
            Settle(flock);
            AssertOverItsPlace(flock, cannery, "with the camera " + LookAlongMetres + " m west of the cannery");

            Frame(cannery.Centre + new Vector2(-LeaveMetres, 0f));
            Settle(flock);
            var followed = new List<string>();
            for (int i = 0; i < flock.BirdCount; i++)
            {
                int key = flock.BoundKeyOf(i);
                if (key < 0) continue;
                if (keys.Contains(key) || string.Equals(flock.BoundPlaceIdOf(i), Cannery, StringComparison.Ordinal))
                    followed.Add("bird " + i + " (slot key " + key + ") at " + Where(flock, i));
            }
            if (followed.Count > 0)
                Assert.Fail("cannery birds are still drawn with the camera " + LeaveMetres + " m away:\n  " +
                            string.Join("\n  ", followed));
        }

        /// <summary>
        /// The camera goes 200 m away and comes back: the pool draws the same birds, slot for slot, as a
        /// flock that never left, and as this one did before it went. Every bird's spot is a hash of its
        /// place and slot, so the same slots are the same spots.
        /// </summary>
        [Test]
        public void ComingBackShowsTheSameBirds()
        {
            OnTheCoast();
            BirdHauntDef cannery = Place(Cannery);
            _clock.Seconds = At(SummerDay, MorningHour);

            GullFlock walker = NewFlock("Gulls_LeftAndCameBack");
            Frame(cannery.Centre);
            Settle(walker);
            AssertEveryNearBirdDrawn(walker, "before the camera left");
            Assert.Greater(Drawn(walker, Cannery, GullWhere.AtHaunt), 0, "no birds at the cannery: nothing to compare");
            string before = Binding(walker);

            Frame(cannery.Centre + new Vector2(-LeaveMetres, 0f));
            Settle(walker);
            Assert.AreEqual(0, DrawnFor(walker, Cannery), "the cannery's birds are still drawn 200 m away");

            Frame(cannery.Centre);
            Settle(walker);
            string back = Binding(walker);

            GullFlock stayer = NewFlock("Gulls_NeverLeft");
            Settle(stayer);
            string fresh = Binding(stayer);

            Assert.AreEqual(fresh, back, "the birds that came back are not the birds a flock that never left draws");
            Assert.AreEqual(before, back, "the birds that came back are not the birds that were there before");
        }

        // ── the hour, the wind, the tide ───────────────────────────────────────────────────────────

        /// <summary>
        /// The wharf is busy while the boats come in and quieter at midday: fewer birds at 13:00 than at
        /// 09:00. At 23:00 the cannery is empty and the Whelps hold their night: at least half their room,
        /// and more than at 13:00.
        /// </summary>
        [Test]
        public void TheClockMovesThemOn()
        {
            OnTheCoast();
            GullFlock flock = NewFlock("Gulls_TheHours");

            Frame(Place(Wharf).Centre);
            int morning = CountAt(flock, MorningHour, Wharf, GullWhere.AtHaunt);
            int midday = CountAt(flock, NoonHour, Wharf, GullWhere.AtHaunt);
            Assert.Greater(morning, 0, "no birds at the wharf at 09:00: nothing to thin out");
            Assert.Less(midday, morning, "the wharf at 13:00 is as busy as at 09:00");

            Frame(Place(Cannery).Centre);
            _clock.Seconds = At(SummerDay, NightHour);
            Settle(flock);
            AssertEveryNearBirdDrawn(flock, Cannery + " at " + NightHour + " h");
            Assert.AreEqual(0, DrawnFor(flock, Cannery), "birds at the cannery, or flying in, at 23:00");

            BirdHauntDef whelps = Place(Whelps);
            Assert.IsTrue(whelps.TryGetEntry(GullConfig.Default.SpeciesId, out BirdHauntEntry roost),
                          Whelps + " has no " + GullConfig.Default.SpeciesId + " entry");
            Frame(whelps.Centre);
            int day = CountAt(flock, NoonHour, Whelps, GullWhere.AtRoost);
            int night = CountAt(flock, NightHour, Whelps, GullWhere.AtRoost);
            Assert.GreaterOrEqual(night, roost.Room / 2,
                                  "the Whelps hold " + night + " at 23:00, under half their room of " + roost.Room);
            Assert.Greater(night, day, "the Whelps hold no more birds at night than at midday");
        }

        /// <summary>
        /// A full gale at 09:00, at low water: the exposed flats empty, and the Sisters, their roost, fill.
        /// </summary>
        [Test]
        public void AGaleSendsThemHome()
        {
            OnTheCoast();
            GullFlock flock = NewFlock("Gulls_TheGale");
            _sea.Tide = LowWater;   // the flats open, so the calm side of the comparison has birds on them

            Frame(Place(EastFlats).Centre);
            _sea.Storm01 = Calm;
            int calmFlats = CountAt(flock, MorningHour, EastFlats, GullWhere.AtHaunt);
            _sea.Storm01 = Gale;
            int galeFlats = CountAt(flock, MorningHour, EastFlats, GullWhere.AtHaunt);
            Assert.Greater(calmFlats, 0, "no birds on the flats on a calm low-water morning: nothing to send home");
            Assert.AreEqual(0, galeFlats, "birds stay out on the exposed flats in a full gale");

            Frame(Place(Sisters).Centre);
            _sea.Storm01 = Calm;
            int calmRoost = CountAt(flock, MorningHour, Sisters, GullWhere.AtRoost);
            _sea.Storm01 = Gale;
            int galeRoost = CountAt(flock, MorningHour, Sisters, GullWhere.AtRoost);
            Assert.Greater(galeRoost, calmRoost, "the gale sent no more birds to the Sisters");
        }

        /// <summary>At 09:00 on a calm day the flats hold more birds at low water than at high water.</summary>
        [Test]
        public void LowWaterFillsTheFlats()
        {
            OnTheCoast();
            GullFlock flock = NewFlock("Gulls_TheTide");
            Frame(Place(EastFlats).Centre);

            _sea.Tide = HighWater;
            int high = CountAt(flock, MorningHour, EastFlats, GullWhere.AtHaunt);
            _sea.Tide = LowWater;
            int low = CountAt(flock, MorningHour, EastFlats, GullWhere.AtHaunt);
            Assert.Greater(low, high, "the flats hold no more birds at low water (" + low + ") than at high (" + high + ")");
        }

        // ── the calls ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The director hears this fixture's flock. Over open water, far from every place, no gull voice
        /// sounds for ten seconds. Framed just west of the cannery at 09:00, a gull calls within ten
        /// seconds, and every call pans east: every bird near the view is east of its middle.
        /// </summary>
        [UnityTest]
        public IEnumerator NoGullSoundWithNoGullsNear()
        {
            OnTheCoast();
            List<AudioSource> voices = GullVoices();
            Assert.Greater(voices.Count, 0, "no AudioSource named \"" + GullVoicePrefix + "*\": the director built " +
                                            "no gull voices, or they were renamed");

            GullFlock flock = NewFlock("Gulls_Heard");
            GameServices.BirdPresence = flock;   // the director hears this flock, and only this one
            _clock.Seconds = At(SummerDay, MorningHour);

            Frame(OpenWater);
            Settle(flock);
            Assert.AreEqual(0, flock.NearCount, "birds are near the open-water spot: it is not far from every place");

            // The director listens to the new sky, then any call begun before the fixture began runs out.
            for (float t = 0f; t < ScheduleSettleSeconds; t += Time.unscaledDeltaTime)
            {
                StepAFrame(flock);
                yield return null;
            }
            for (float t = 0f; AnySounding(voices) && t < QuietWaitSeconds; t += Time.unscaledDeltaTime)
            {
                StepAFrame(flock);
                yield return null;
            }
            Assert.IsFalse(AnySounding(voices), "a gull voice still sounds " + QuietWaitSeconds +
                                                " s after every bird was gone");

            for (float t = 0f; t < ListenSeconds; t += Time.unscaledDeltaTime)
            {
                StepAFrame(flock);
                yield return null;
                for (int v = 0; v < voices.Count; v++)
                    if (Sounding(voices[v]))
                        Assert.Fail(voices[v].name + " sounded over open water, " + t.ToString("0.0") +
                                    " s in, with " + flock.NearCount + " birds near");
            }

            BirdHauntDef cannery = Place(Cannery);
            float middle = cannery.Centre.x - cannery.HalfSize.x - EastOfTheMiddleMetres;
            Frame(new Vector2(middle, cannery.Centre.y));
            Settle(flock);
            Assert.Greater(flock.NearCount, 0, "no birds near the cannery at 09:00: nothing to hear");

            var near = new BirdAudible[GullFlock.MaxNearBirds];
            var was = new bool[voices.Count];
            var wrongSide = new List<string>();
            int calls = 0;
            float heardFor = 0f;
            for (; heardFor < ListenSeconds && calls < CallsToHear; heardFor += Time.unscaledDeltaTime)
            {
                StepAFrame(flock);
                AssertEveryBirdEastOf(flock, near, middle);
                yield return null;
                for (int v = 0; v < voices.Count; v++)
                {
                    bool now = Sounding(voices[v]);
                    if (now && !was[v])
                    {
                        calls++;
                        if (!(voices[v].panStereo > 0f))
                            wrongSide.Add(voices[v].name + " pans " + voices[v].panStereo.ToString("0.000"));
                    }
                    was[v] = now;
                }
            }
            Assert.Greater(calls, 0, "no gull called in " + ListenSeconds + " s at the cannery at 09:00, with " +
                                     flock.NearCount + " birds near");
            if (wrongSide.Count > 0)
                Assert.Fail("every bird is east of the middle, but calls pan west or centre:\n  " +
                            string.Join("\n  ", wrongSide));
        }

        // ── rule 7 ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A hundred ticks, each a schedule tick, at the cannery at 09:00 with the clock running at the
        /// game's pace, allocate nothing: the roster, the binding and the drawing all reuse their buffers.
        /// </summary>
        [Test]
        public void TheTickDoesNotAllocate()
        {
            OnTheCoast();
            GullFlock flock = NewFlock("Gulls_Allocation");
            Frame(Place(Cannery).Centre);
            _clock.Seconds = At(SummerDay, MorningHour);
            double tickMilliseconds = 1000.0 / GullConfig.Default.ScheduleTicksPerSecond;   // every tick refreshes

            Ticks(flock, WarmUpTicks, tickMilliseconds);
            Assert.Greater(DrawnFor(flock, Cannery), 0, "nothing at the cannery: the measured ticks would draw no birds");

            // The recorder must see an allocation here, or the negative below would pass on anything.
            Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                        "harness: the GC.Alloc recorder saw no allocation at all");
            Assert.That(() => Ticks(flock, MeasuredTicks, tickMilliseconds), Is.Not.AllocatingGCMemory(),
                        "the flock's tick allocated (rule 7: nothing per tick)");

            // The same ticks on the thread's allocation counter, where this runtime keeps one.
            long before = GC.GetAllocatedBytesForCurrentThread();
            _sink = new object[64];
            if (GC.GetAllocatedBytesForCurrentThread() - before <= 0)
            {
                Debug.Log("[GullHauntPlayTests] GC.GetAllocatedBytesForCurrentThread counts nothing on this " +
                          "runtime, so its arm is skipped. The GC.Alloc recorder above is the measurement.");
                return;
            }
            before = GC.GetAllocatedBytesForCurrentThread();
            Ticks(flock, MeasuredTicks, tickMilliseconds);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before,
                            "the flock's tick allocated bytes on the thread's own counter");
        }

        // ── the plates ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The cannery at dawn, noon, dusk and night through the game's own camera, drawn by the game's
        /// own flock in the region itself: four plates for the owner's eye. The cannery has birds at noon
        /// and none at night.
        /// </summary>
        [UnityTest]
        public IEnumerator DawnNoonDuskNight_Plates()
        {
            WharfNightStage.RequireAGraphicsDevice();   // ⚠ first, before any yield
            BirdHauntDef cannery = Place(Cannery);
            _stage = new WharfNightStage(StPetersScene, PlateDir);
            yield return _stage.Load();
            // St Peters is the start scene: Reset() leaves the title page and publishes nothing, where
            // StartNewGame() would also write the save every worktree shares.
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();

            _installed.enabled = true;   // the game's own flock draws the plates

            var counts = new List<string>();
            int noon = -1, night = -1;
            bool framed = false;
            foreach (float hour in PlateHours)
            {
                yield return _stage.SetNight(hour);
                if (!framed)
                {
                    yield return _stage.FrameOn(cannery.Centre);
                    framed = true;
                }
                else
                {
                    Time.timeScale = 0f;   // SetNight ran engine time to settle the tint: stop the sea again
                    yield return null;
                    yield return null;
                }
                // The flock's schedule runs on unscaled time: let it see the hour before the shot.
                yield return new WaitForSecondsRealtime(ScheduleSettleSeconds);

                int here = DrawnFor(_installed, Cannery);
                string label = hour.ToString("00.0", CultureInfo.InvariantCulture);
                counts.Add(label + " h: " + here + " at the cannery, " + _installed.NearCount + " near");
                if (Mathf.Approximately(hour, NoonHour)) noon = here;
                if (Mathf.Approximately(hour, NightHour)) night = here;
                _stage.SavePlate("gulls-" + label + "h-cannery.png", _stage.Capture());
            }
            Debug.Log("[" + PlateDir + "] " + string.Join("; ", counts));
            Assert.Greater(noon, 0, "no gulls at the cannery at noon in the plates");
            Assert.AreEqual(0, night, "gulls at the cannery at 23:00 in the plates");
        }

        // ── the fixture ────────────────────────────────────────────────────────────────────────────

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>St Peters with no scene: the fixture's camera, clock and sea, the region's id and
        /// ground, and no fish, so every bird here is a place's.</summary>
        void OnTheCoast()
        {
            // ⚠ ONE AUDIO LISTENER: a listener-less scene logs a warning every frame.
            Spawn("AudioListener").AddComponent<AudioListener>();
            GameObject camGo = Spawn("TestCamera");
            camGo.tag = "MainCamera";
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = OrthoSize;
            _camera.aspect = FixtureAspect;
            Assert.AreSame(_camera, Camera.main,
                           "another main camera is live: the flock would frame it, not this fixture's");
            PinGlobals();

            _clock = new StandingClock();
            _sea = new StandingSea();
            GameServices.Clock = _clock;
            GameServices.Environment = _sea;
            GameServices.FishSchools = null;
            GameServices.CurrentRegionId = RegionId;
            GameServices.CurrentRegionBounds = RegionBounds;
        }

        void Frame(Vector2 at) => _camera.transform.position = new Vector3(at.x, at.y, -10f);

        /// <summary>A fresh flock with the shipped defaults and the shipped library, DISABLED before its
        /// first frame: this fixture hands it every millisecond.</summary>
        GullFlock NewFlock(string name)
        {
            GullFlock flock = Spawn(name).AddComponent<GullFlock>();
            flock.enabled = false;
            Assert.Greater(flock.BirdCount, 0, name + " built no birds: no sheet, and every count would be vacuous");
            return flock;
        }

        static void Settle(GullFlock flock)
        {
            for (int i = 0; i < SettleFrames; i++) flock.Tick(FrameMilliseconds);
        }

        static void StepAFrame(GullFlock flock)
        {
            PinGlobals();
            flock.Tick(Time.unscaledDeltaTime * 1000.0);
        }

        void Ticks(GullFlock flock, int count, double milliseconds)
        {
            for (int i = 0; i < count; i++)
            {
                _clock.Seconds += milliseconds / 1000.0;   // the game's pace: a game second each second
                flock.Tick(milliseconds);
            }
        }

        /// <summary>Broad day, no wind: the two shader globals the flock reads, pinned.</summary>
        static void PinGlobals()
        {
            Shader.SetGlobalColor("_DayNightTint", Color.white);
            Shader.SetGlobalVector("_WindWorld", Vector4.zero);
        }

        /// <summary>The third day of High Summer, whatever the season's length.</summary>
        static int SummerDay => GameServices.DaysPerSeason * (int)Season.HighSummer + 2;

        static double At(int day, float hour) => (day + hour / DaySchedule.HoursPerDay) * GameServices.SecondsPerDay;

        /// <summary>The birds drawn for a place in one state, at an hour of the summer day.</summary>
        int CountAt(GullFlock flock, float hour, string placeId, GullWhere where)
        {
            _clock.Seconds = At(SummerDay, hour);
            Settle(flock);
            AssertEveryNearBirdDrawn(flock, placeId + " at " + hour + " h");
            return Drawn(flock, placeId, where);
        }

        static bool BoundAt(GullFlock flock, int bird, string placeId, GullWhere where) =>
            flock.BoundKeyOf(bird) >= 0 && flock.BoundWhereOf(bird) == where &&
            string.Equals(flock.BoundPlaceIdOf(bird), placeId, StringComparison.Ordinal);

        static int Drawn(GullFlock flock, string placeId, GullWhere where)
        {
            int n = 0;
            for (int i = 0; i < flock.BirdCount; i++)
                if (BoundAt(flock, i, placeId, where)) n++;
            return n;
        }

        /// <summary>The birds drawn for a place in any state: there, or flying in.</summary>
        static int DrawnFor(GullFlock flock, string placeId)
        {
            int n = 0;
            for (int i = 0; i < flock.BirdCount; i++)
                if (flock.BoundKeyOf(i) >= 0 && string.Equals(flock.BoundPlaceIdOf(i), placeId, StringComparison.Ordinal))
                    n++;
            return n;
        }

        static void AssertEveryNearBirdDrawn(GullFlock flock, string when)
        {
            Assert.LessOrEqual(flock.NearCount, flock.BirdCount,
                               when + ": more birds are near than the pool draws, so a count would be the pool's");
        }

        /// <summary>The middle of a place's birds is inside its box: they wheel over their place, wherever
        /// the camera is.</summary>
        static void AssertOverItsPlace(GullFlock flock, BirdHauntDef place, string when)
        {
            double sumX = 0.0, sumY = 0.0;
            int n = 0;
            for (int i = 0; i < flock.BirdCount; i++)
            {
                if (!BoundAt(flock, i, place.Id, GullWhere.AtHaunt)) continue;
                SeagullSimBird s = flock.BirdState(i);
                sumX += s.X;
                sumY += s.Y;
                n++;
            }
            Assert.Greater(n, 0, when + ": no bird at " + place.Id);
            var middle = new Vector2((float)(sumX / n), (float)(sumY / n));
            Vector2 lo = place.Centre - place.HalfSize, hi = place.Centre + place.HalfSize;
            Assert.IsTrue(middle.x >= lo.x && middle.x <= hi.x && middle.y >= lo.y && middle.y <= hi.y,
                          when + ": the middle of " + place.Id + "'s birds is " + middle + ", outside its box " +
                          lo + " to " + hi);
        }

        /// <summary>Precondition of the pan check: no bird near the view is west of its middle.</summary>
        static void AssertEveryBirdEastOf(GullFlock flock, BirdAudible[] near, float middle)
        {
            int n = flock.CopyNear(near);
            for (int k = 0; k < n; k++)
                if (near[k].WorldPosition.x <= middle)
                    Assert.Fail("precondition: a bird near the view is at x " + near[k].WorldPosition.x +
                                ", not east of the middle at " + middle + ", so a west pan could be right");
        }

        /// <summary>The pool, bird by bird: the slot each draws, where it is, and its place.</summary>
        static string Binding(GullFlock flock)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < flock.BirdCount; i++)
            {
                int key = flock.BoundKeyOf(i);
                if (key < 0) continue;
                sb.Append(i).Append(':').Append(key).Append(' ').Append(Where(flock, i)).Append('\n');
            }
            return sb.ToString();
        }

        static string Where(GullFlock flock, int bird) =>
            flock.BoundWhereOf(bird) + " " + (flock.BoundPlaceIdOf(bird) ?? "(no place)");

        static BirdHauntDef Place(string id)
        {
            var library = Resources.Load<BirdHauntLibrary>(BirdHauntLibrary.ResourcesPath);
            Assert.IsNotNull(library, "Resources/" + BirdHauntLibrary.ResourcesPath + " did not load: no places");
            foreach (BirdHauntDef def in library.Haunts)
                if (def != null && string.Equals(def.Id, id, StringComparison.Ordinal)) return def;
            Assert.Fail(id + " is not in the library");
            return null;
        }

        static List<AudioSource> GullVoices()
        {
            var voices = new List<AudioSource>();
            foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                if (s != null && s.gameObject.name.StartsWith(GullVoicePrefix, StringComparison.Ordinal)) voices.Add(s);
            return voices;
        }

        /// <summary>A voice sounds while it plays, or while the player holds its level up: the level is
        /// what a machine with no audio device still shows.</summary>
        static bool Sounding(AudioSource voice) => voice != null && (voice.isPlaying || voice.volume > 0f);

        static bool AnySounding(List<AudioSource> voices)
        {
            foreach (AudioSource v in voices) if (Sounding(v)) return true;
            return false;
        }

        static GullFlock FindInstalledFlock()
        {
            foreach (GullFlock f in Resources.FindObjectsOfTypeAll<GullFlock>())
            {
                if (f == null) continue;
                GameObject go = f.gameObject;
                if (go.name == "GullFlock" &&
                    (go.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave)
                    return f;
            }
            return null;
        }

        static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? new List<GameObject>(s.GetRootGameObjects()) : new List<GameObject>();
        }
    }
}
