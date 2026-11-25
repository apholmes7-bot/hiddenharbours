using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE BEACH AT ST PETERS, ROUND THE YEAR</b> — the evidence run for beach life: the real region,
    /// the real villagers, the real clock. Eight dates (the two liveliest of each season, found by
    /// asking the running village) at four hours each, a wide plate of the whole bar head every time
    /// and a plate through the game's own lens whenever somebody is on the sand; and the frame time
    /// and the garbage of a busy evening beside those of an empty winter noon.
    ///
    /// <para>An explicitly requested GPU run (<c>-beachPlates &lt;folder&gt;</c>): CI has no graphics
    /// device and skips it. Nothing is asserted about pictures; what the plates show is for a person
    /// to look at. Each plate has a text file beside it saying who was on the beach, and where.</para>
    /// </summary>
    public class BeachLifePlatePlayTests
    {
        /// <summary>The hours every date is shot at: morning, early afternoon, evening (late enough that
        /// the last of them has walked out), dusk.</summary>
        static readonly float[] Hours = { 9f, 13f, 20.25f, 21.6f };

        const int DatesPerSeason = 2;
        const int WideReferenceHeight = 900;
        const float GameLensSize = 7.03125f;
        const float BeachMarginMetres = 3f;
        const int TimedFrames = 240;

        WharfNightStage _stage;
        ISaveService _previousSave;
        bool _capturing;
        CameraFollow _pinnedFollow;
        Behaviour _pinnedLens;
        int _previousRefX, _previousRefY;
        object _previousCrop;
        bool _previousLensEnabled;

        sealed class PlateSave : ISaveService
        {
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => key == ArrivalOpening.ArrivedFlagKey;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        struct Scored
        {
            public int Day;
            public Season Season;
            public int Score;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheBeachRoundTheYear_EightDatesAtFourHours()
        {
            WharfNightStage.RequireAGraphicsDevice();
            Assert.AreNotEqual(SaveStore.DefaultPath, SaveStore.ActivePath);
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-beachPlates");
            if (at < 0 || at + 1 >= args.Length)
                Assert.Ignore("Beach plates are an explicitly requested GPU evidence run.");
            string output = args[at + 1];
            Directory.CreateDirectory(output);

            _capturing = true;
            // The world is stopped for every plate, so a line said by the fire would never be read and
            // the two who said it would still be standing there at the next hour. Read each at once.
            EventBus.Subscribe<AmbientSpeechRequested>(ReadAtOnce);
            _previousSave = GameServices.Save;
            GameServices.Save = new PlateSave();
            _stage = new WharfNightStage("StPeters", "BeachLife");
            yield return _stage.Load();
            ShellFlow.ContinueGame();
            foreach (var opening in Object.FindObjectsByType<ArrivalOpening>(FindObjectsSortMode.None))
                opening.enabled = false;

            var clock = GameServices.Clock as HiddenHarbours.Environment.GameClock;
            if (clock == null) clock = Object.FindFirstObjectByType<HiddenHarbours.Environment.GameClock>();
            Assert.IsNotNull(clock);
            GameServices.Clock = clock;
            GameServices.Config = clock.Config;
            clock.IsPaused = true;
            clock.IsHeld = false;
            double perDay = clock.Config.SecondsPerDay;

            BeachLifeDirector director = BeachLifeDirector.Instance;
            Assert.IsNotNull(director);
            Rect beach = BeachBounds(withTheFlats: true);
            // The wide plate is the sand people stand on; the flats run far out past any one frame.
            Vector2 wideCentre = BeachBounds(withTheFlats: false).center;
            var people = new List<VillagerRoutine>();

            // ---- which dates: ask the running village, two liveliest days of each season ----
            Time.timeScale = 1f;
            int daysInYear = clock.Config.DaysPerSeason * 4;
            var scored = new List<Scored>(daysInYear);
            for (int day = 0; day < daysInYear; day++)
            {
                int score = 0;
                Season season = default;
                foreach (float hour in Hours)
                {
                    clock.SeekTo((day + hour / 24d) * perDay);
                    yield return null;
                    yield return null;
                    director.RefreshGatheringsNow();
                    season = clock.Season;
                    score += OnTheBeach(beach, people);
                    // A fire is the thing most worth a plate: a day with one outranks a day without.
                    score += director.MiddlesShown * 10;
                }
                scored.Add(new Scored { Day = day, Season = season, Score = score });
            }

            var dates = new List<Scored>();
            foreach (Season season in new[] { Season.EarlySpring, Season.HighSummer, Season.TheTurn, Season.HardWinter })
            {
                var inSeason = scored.FindAll(s => s.Season == season);
                inSeason.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : a.Day.CompareTo(b.Day));
                for (int i = 0; i < DatesPerSeason && i < inSeason.Count; i++) dates.Add(inSeason[i]);
            }
            dates.Sort((a, b) => a.Day.CompareTo(b.Day));
            Assert.AreEqual(DatesPerSeason * 4, dates.Count, "eight dates: two of each season");

            var index = new StringBuilder();
            index.AppendLine($"worldSeed={GameServices.Environment?.WorldSeed}");
            index.AppendLine($"hours={string.Join(",", Hours)}");
            foreach (Scored s in dates) index.AppendLine($"date day={s.Day} season={s.Season} score={s.Score}");
            int busyDays = scored.FindAll(s => s.Score > 0).Count;
            index.AppendLine($"days of the year with somebody on the sand at one of those hours: {busyDays} of {daysInYear}");

            // ---- the camera: the game's own, parked on the middle of the beach ----
            clock.SeekTo((dates[0].Day + Hours[0] / 24d) * perDay);
            yield return _stage.FrameOn(wideCentre);
            _pinnedFollow = Object.FindAnyObjectByType<CameraFollow>();
            Assert.IsNotNull(_pinnedFollow);
            _pinnedFollow.enabled = false;
            _pinnedLens = _stage.Camera.GetComponent("PixelPerfectCamera") as Behaviour;
            Assert.IsNotNull(_pinnedLens);
            _previousRefX = ReadLens<int>("refResolutionX");
            _previousRefY = ReadLens<int>("refResolutionY");
            _previousCrop = ReadLens<object>("cropFrame");
            _previousLensEnabled = _pinnedLens.enabled;
            WriteLens("cropFrame", System.Enum.Parse(_previousCrop.GetType(), "Letterbox"));
            _pinnedLens.enabled = true;
            int gameReferenceHeight = Mathf.RoundToInt(GameLensSize * 2f * ReadLens<int>("assetsPPU"));
            float cameraZ = _stage.Camera.transform.position.z;

            // ---- the plates ----
            int plates = 0, closePlates = 0;
            Scored busiest = dates[0];
            foreach (Scored date in dates)
            {
                if (date.Score > busiest.Score) busiest = date;
                foreach (float hour in Hours)
                {
                    yield return Settle(clock, director, (date.Day + hour / 24d) * perDay);
                    int there = OnTheBeach(beach, people);
                    string name = $"beach-d{date.Day:000}-{date.Season}-{Mathf.RoundToInt(hour * 60f):0000}";

                    yield return Lens(WideReferenceHeight, wideCentre, cameraZ);
                    Write(output, name + "-wide", _stage.Capture());
                    plates++;

                    Vector2 middle = wideCentre;
                    if (there > 0 || director.MiddlesShown > 0)
                    {
                        middle = director.MiddlesShown > 0
                            ? (Vector2)director.FirstMiddle.transform.position
                            : Centroid(people);
                        yield return Lens(gameReferenceHeight, middle, cameraZ);
                        Write(output, name + "-game", _stage.Capture());
                        closePlates++;
                    }

                    var note = new StringBuilder();
                    var sample = GameServices.Environment != null ? GameServices.Environment.Sample() : default;
                    note.AppendLine($"day={clock.DayIndex} season={clock.Season} dayOfSeason={clock.DayOfSeason} hour={clock.HourOfDay:0.00}");
                    note.AppendLine($"seaState={sample.SeaState01:0.00} visibility={sample.Visibility:0.00} tint={Shader.GetGlobalColor("_DayNightTint")}");
                    note.AppendLine($"onTheBeach={there} firesBurning={director.MiddlesShown} gameLensAt={middle}");
                    foreach (VillagerRoutine v in people)
                        note.AppendLine($"  {v.Routine.Npc.DisplayName} at {(Vector2)v.transform.position} " +
                                        (v.Pose.Walking ? "walking" : "standing"));
                    File.WriteAllText(Path.Combine(output, name + ".txt"), note.ToString());
                }
            }
            index.AppendLine($"plates: {plates} wide, {closePlates} through the game lens");

            // ---- what it costs: a busy evening beside an empty winter noon, same camera ----
            Scored quiet = scored.Find(s => s.Season == Season.HardWinter && s.Score == 0);
            yield return Lens(gameReferenceHeight, wideCentre, cameraZ);
            foreach ((string label, double seconds) in new[]
                     {
                         ("busy evening", (busiest.Day + Hours[2] / 24d) * perDay),
                         ("empty winter noon", (quiet.Day + 12d / 24d) * perDay),
                     })
            {
                yield return Settle(clock, director, seconds);
                Time.timeScale = 1f;
                int there = OnTheBeach(beach, people);
                for (int i = 0; i < 30; i++) yield return null;
                using (ProfilerRecorder garbage = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
                {
                    double sum = 0d, worst = 0d;
                    long bytes = 0, worstBytes = 0;
                    for (int i = 0; i < TimedFrames; i++)
                    {
                        yield return null;
                        double ms = Time.unscaledDeltaTime * 1000d;
                        sum += ms;
                        if (ms > worst) worst = ms;
                        long b = garbage.LastValue;
                        bytes += b;
                        if (b > worstBytes) worstBytes = b;
                    }
                    index.AppendLine($"cost [{label}] day={clock.DayIndex} hour={clock.HourOfDay:0.00} onTheBeach={there} " +
                                     $"fires={director.MiddlesShown}: mean {sum / TimedFrames:0.00} ms, worst {worst:0.00} ms, " +
                                     $"garbage mean {bytes / TimedFrames} B/frame, worst {worstBytes} B (whole editor frame, " +
                                     $"{TimedFrames} frames)");
                }
                Time.timeScale = 0f;
            }

            File.WriteAllText(Path.Combine(output, "index.txt"), index.ToString());
            Debug.Log("[BeachLife] " + index);
        }

        /// <summary>Everything the library's beaches are drawn on, with a margin.</summary>
        static Rect BeachBounds(bool withTheFlats)
        {
            var library = Resources.Load<BeachLifeLibrary>(BeachLifeLibrary.ResourcesPath);
            Assert.IsNotNull(library);
            bool any = false;
            Vector2 min = default, max = default;
            foreach (BeachActivityDef activity in library.Activities)
            {
                BeachDef beach = activity != null ? activity.Beach : null;
                if (beach == null) continue;
                foreach (BeachTransect[] lines in new[] { beach.Spots, beach.Promenade, beach.Flats, beach.Shallows, beach.Gatherings })
                {
                    if (!withTheFlats && (lines == beach.Flats || lines == beach.Promenade)) continue;
                    foreach (BeachTransect line in lines)
                        foreach (Vector2 p in new[] { line.Top, line.Toe })
                        {
                            if (!any) { min = max = p; any = true; continue; }
                            min = Vector2.Min(min, p);
                            max = Vector2.Max(max, p);
                        }
                }
            }
            Assert.IsTrue(any, "the library draws no beach");
            Vector2 margin = Vector2.one * BeachMarginMetres;
            return Rect.MinMaxRect(min.x - margin.x, min.y - margin.y, max.x + margin.x, max.y + margin.y);
        }

        /// <summary>Who is on the beach now because the beach gave them their day. Fills
        /// <paramref name="into"/>.</summary>
        static int OnTheBeach(Rect beach, List<VillagerRoutine> into)
        {
            into.Clear();
            foreach (VillagerRoutine v in Object.FindObjectsByType<VillagerRoutine>(FindObjectsSortMode.None))
                if (v.IsLiving && v.IsVariedToday && v.Routine != null && v.Routine.Npc != null &&
                    beach.Contains((Vector2)v.transform.position))
                    into.Add(v);
            return into.Count;
        }

        static Vector2 Centroid(List<VillagerRoutine> people)
        {
            Vector2 sum = Vector2.zero;
            foreach (VillagerRoutine v in people) sum += (Vector2)v.transform.position;
            return sum / Mathf.Max(1, people.Count);
        }

        /// <summary>Seek, let the world wear the hour, then stop it.</summary>
        static IEnumerator Settle(HiddenHarbours.Environment.GameClock clock, BeachLifeDirector director, double seconds)
        {
            Time.timeScale = 1f;
            clock.SeekTo(seconds);
            yield return null;
            yield return null;
            // The fire first, then the wait: the lamp pools pick a new light up on their own tick.
            director.RefreshGatheringsNow();
            yield return new WaitForSeconds(1f);
            Time.timeScale = 0f;
            foreach (var cycle in Object.FindObjectsByType<DayNightController>(FindObjectsSortMode.None))
                typeof(DayNightController).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(cycle, null);
            FoliageSnowBridge.PublishFromClock();
            if (MoodGradeDirector.Instance != null) MoodGradeDirector.Instance.Tick();
            for (int i = 0; i < 4; i++) yield return null;
        }

        /// <summary>Point the game's camera at a place through a lens of the given reference height.</summary>
        IEnumerator Lens(int referenceHeight, Vector2 at, float z)
        {
            WriteLens("refResolutionY", referenceHeight);
            WriteLens("refResolutionX", Mathf.RoundToInt(referenceHeight * _stage.Camera.aspect));
            _stage.Camera.transform.position = new Vector3(at.x, at.y, z);
            for (int i = 0; i < 3; i++) yield return null;
        }

        void Write(string folder, string name, byte[] rgba)
        {
            var texture = new Texture2D(_stage.Width, _stage.Height, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(rgba);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        // The fixture reads this optional camera component without adding a module reference.
        T ReadLens<T>(string property) => (T)_pinnedLens.GetType().GetProperty(property).GetValue(_pinnedLens);
        void WriteLens(string property, object value) => _pinnedLens.GetType().GetProperty(property).SetValue(_pinnedLens, value);

        void RestoreLens()
        {
            if (_pinnedLens == null) return;
            WriteLens("refResolutionX", _previousRefX);
            WriteLens("refResolutionY", _previousRefY);
            WriteLens("cropFrame", _previousCrop);
            _pinnedLens.enabled = _previousLensEnabled;
            _pinnedLens = null;
        }

        static void ReadAtOnce(AmbientSpeechRequested said) =>
            EventBus.Publish(new AmbientSpeechEnded(said.Id, AmbientSpeechEndReason.Dwelled));

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (!_capturing) yield break;
            EventBus.Unsubscribe<AmbientSpeechRequested>(ReadAtOnce);
            RestoreLens();
            if (_pinnedFollow != null) _pinnedFollow.enabled = true;
            _pinnedFollow = null;
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
            ShellRestart.DestroyPersistentCore();
            ShellFlow.Reset();
            GameServices.Save = _previousSave;
            _capturing = false;
            yield return null;
        }
    }
}
