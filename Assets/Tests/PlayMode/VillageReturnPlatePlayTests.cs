using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The village return, photographed</b> (#898, drop 14 Phase B): the plates and the numbers the
    /// owner rules on. St Peters' houses at noon, in the golden hour and at night, and in rain and in fog,
    /// each framed at the widest view a walker has; every placed room through its door, with its shell cut
    /// away as it is when you step in; and what L2 costs — the texture memory the houses hold, the draw
    /// calls and batches with the houses and without them, and the frame time.
    ///
    /// <para><b>Before and after are one fixture.</b> Which look the scene is in is read off the houses
    /// themselves: no house carrying a <see cref="SpriteLightBinder"/> is <c>before</c> (today's sheets,
    /// drawn as main draws them); every house carrying one is <c>after</c> (the re-baked sheets, lit). A
    /// scene with some houses bound and some not is half-patched, and fails. Plates, captions and numbers
    /// go to <c>temporaryCachePath/VillageReturnPlates/&lt;look&gt;/</c> under the same names both times,
    /// so a before and an after pair up by file name.</para>
    ///
    /// <para><b>The owner's save is never at risk.</b> St Peters boots to the title, and the save service
    /// writes nothing while the shell is there. This fixture never leaves the title — it never continues
    /// or starts a game — and skips, before it moves anything, if the region did not boot to it.</para>
    ///
    /// <para><b>The weather is HELD, not simulated.</b> Rain and fog are art that reads the environment
    /// sample's visibility and sea state (the rain emitter, the sea mist, the day/night weather dim and the
    /// grade all ask <see cref="GameServices.Environment"/> every tick), so every plate wraps the region's
    /// own service in <see cref="HeldMood"/> and answers with the plate's weather: clear air on a light sea,
    /// murk on a rough sea for rain, murk on a calm sea for fog. Wind, tide and seed stay the region's, and
    /// the wrapper comes off in the teardown. Held rather than found, so a before and an after are shot in
    /// the same weather whatever the simulation would have brought that day.</para>
    ///
    /// <para>Needs a GPU: skips loudly as NOT VERIFIED on CI's Null device rather than reading green.</para>
    /// </summary>
    public class VillageReturnPlatePlayTests
    {
        const string PlateDir = "VillageReturnPlates";
        const string CleanupSceneName = "VillageReturnPlateCleanup";
        const string StPeters = "StPeters";
        const string BuildingsContractPath = "_Project/Art/Sprites/Buildings/Village/Buildings.json";
        const string VillageSheetPrefix = "Village_";
        const string RoomSheetPrefix = "Interior_";
        const string HouseRig = "house";
        // Ginny's outbuildings (the net store, the woodshed and the lean-to): wharf-building sheets that did
        // not move, beside a house that did — the one frame with both looks in it.
        const string YardSheetPrefix = "ginny";

        const int PlateHeightPx = CameraFollow.DesignScreenHeightPx;   // 1080: the PPC zoom depends on it
        const int PlateWidthPx = 1920;
        // The follow eases its look-ahead out at 3/s: under half a zoom-4 pixel in ~2.2 s. Three times that.
        const float LeadEaseOutSeconds = 6f;
        // ON the centre: far inside the 1/128 m step of the grid the camera snaps to at zoom 4.
        const float OnTheCentreMetres = 0.001f;
        const float SouthDegrees = 180f;   // her heading: facing the camera
        // How far in from the wall's inner face she stands, on the doorway's own line: over the threshold
        // and clear of it, inside the lane the furniture keeps free (StPetersInteriorsTests' door lane).
        const float PastTheSillMetres = 1f;
        const int InsideWaitFrames = 30;
        const int WarmFrames = 10, TimedFrames = 120;

        const float Noon = 12f, GoldenHour = 19.5f, Night = 23f;
        // The held weathers, against the shipped gates. Rain needs murk (visibility at or under
        // RainConfig.Default.VisFull 0.40 opens it fully) on a sea over SeaOnset 0.30; fog is visibility
        // under the day/night profile's full-dim 0.15 on a sea too calm to rain; clear is clear air on a
        // light sea, under the 0.6 where storm gloom starts.
        const float ClearVisibility = 1f, ClearSea01 = 0.20f;
        const float RainVisibility = 0.30f, RainSea01 = 0.80f;
        const float FogVisibility = 0.10f, FogSea01 = 0.10f;
        // How long the world runs on a framed station before the shot: long enough for the rain to fill the
        // frame around the new centre (a drop lives about a second).
        const float ClearRunSeconds = 1f, WeatherRunSeconds = 3f;
        // The longest a shot waits for the editor to compile the shader variants its frame asked for.
        const float ShaderWaitSeconds = 120f;
        // Two lights closer than this (summed |ΔRGB| of _DayNightTint) are the same light.
        const float SameLight = 1e-3f;

        enum Mood { Clear, Rain, Fog }

        readonly struct Condition
        {
            public readonly float Hours;
            public readonly Mood Mood;

            public Condition(float hours, Mood mood)
            {
                Hours = hours;
                Mood = mood;
            }

            public string Key => $"{Hhmm(Hours)}-{Mood.ToString().ToLowerInvariant()}";
        }

        // A day per weather, always forward: the clock is sought ahead, never wound back.
        static readonly Condition[] VillageSchedule =
        {
            new Condition(Noon, Mood.Clear), new Condition(GoldenHour, Mood.Clear), new Condition(Night, Mood.Clear),
            new Condition(24f + Noon, Mood.Rain), new Condition(24f + Night, Mood.Rain),
            new Condition(48f + Noon, Mood.Fog), new Condition(48f + Night, Mood.Fog),
        };

        static readonly (string slot, int id)[] ChannelSlots =
        {
            ("mask", Shader.PropertyToID(SpriteLightBinding.MaskProperty)),
            ("normal", Shader.PropertyToID(SpriteLightBinding.NormalProperty)),
            ("rimGate", Shader.PropertyToID(SpriteLightBinding.RimGateProperty)),
            ("emit", Shader.PropertyToID(SpriteLightBinding.EmitProperty)),
        };

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>The two fields of the village contract this fixture reads.</summary>
        [Serializable] public sealed class Buildings { public Building[] buildings; }

        [Serializable] public sealed class Building { public string key; public string rig; }

        sealed class Station
        {
            public readonly string Shot, Subject;
            public readonly Vector2 Centre;
            public readonly Renderer[] Subjects;   // each must be in the frame
            public readonly Renderer[] Company;    // at least one must be, where there are any

            public Station(string shot, string subject, Vector2 centre, Renderer[] subjects, Renderer[] company)
            {
                Shot = shot;
                Subject = subject;
                Centre = centre;
                Subjects = subjects;
                Company = company;
            }
        }

        sealed class PlacedRoom
        {
            public string Name, Key;
            public BuildingInterior Interior;
            public SpriteRenderer Room, Shell;
            public Vector2 Inside;
        }

        /// <summary>The region's own environment with the weather HELD: the sample's visibility and sea
        /// state are the plate's; the wind, the current, the tide, the water level and the seed are asked
        /// of the service it wraps.</summary>
        sealed class HeldMood : IEnvironmentService
        {
            public readonly IEnvironmentService Inner;
            readonly float _visibility, _seaState01;

            public HeldMood(IEnvironmentService inner, float visibility, float seaState01)
            {
                Inner = inner;
                _visibility = visibility;
                _seaState01 = seaState01;
            }

            public int WorldSeed => Inner.WorldSeed;

            public TideProfile ActiveTideProfile
            {
                get => Inner.ActiveTideProfile;
                set => Inner.ActiveTideProfile = value;
            }

            public EnvironmentSample Sample()
            {
                EnvironmentSample s = Inner.Sample();
                // The stepped scale is the band the continuous axis sits in (EnvironmentSample.SeaState01).
                var band = (SeaState)Mathf.Clamp(Mathf.FloorToInt(_seaState01 * (int)SeaState.Storm),
                                                 (int)SeaState.Glass, (int)SeaState.Storm);
                return new EnvironmentSample(s.WindVector, s.CurrentVector, s.TideHeight, band, _visibility,
                                             _seaState01);
            }

            public float TideHeightAt(double totalSeconds) => Inner.TideHeightAt(totalSeconds);

            // ⚠ Spelled out, never the interface's default: a region's water level is not always its tide.
            public float WaterLevelAt(double totalSeconds) => Inner.WaterLevelAt(totalSeconds);
        }

        // =============================================================================================
        //  Fixture state — one instance serves every case, so the teardown clears all of it
        // =============================================================================================

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        readonly List<Object> _spawned = new List<Object>();
        DateTime _runStartedUtc;
        bool _loadedAny;
        bool _introOnEntry;
        string _healed = "none";
        string _look = "(not read)";
        string _lookEvidence = "(not read)";
        string _pinned = "(not pinned)";
        string _mood = "(not held)";
        HeldMood _held;

        Camera _cam;
        CameraFollow _follow;
        bool _followCaptured;
        Transform _followTargetOnEntry;
        float _followSmoothOnEntry;
        GameObject _anchor;
        float _askedHeight;
        string _eased = "(not framed)";
        RenderTexture _rt;
        int _w, _h;

        PlayerWalkController _player;
        IsoCharacterSprite _iso;
        Rigidbody2D _body;
        bool _bodyTaken, _bodyWasSimulated;

        [OneTimeSetUp]
        public void MarkTheRun() => _runStartedUtc = DateTime.UtcNow;

        // =============================================================================================
        //  Set-up and teardown: leave nothing loaded (#764), and the region's own weather back
        // =============================================================================================

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;   // ⚠ a STATIC: left at 0 it stops every test that follows
            ReleaseTheMood();      // first: the wrapper holds the region's service, which is about to go

            if (_iso != null) _iso.ReleaseHeading();
            if (_body != null && _bodyTaken) _body.simulated = _bodyWasSimulated;
            if (_follow != null && _followCaptured)
            {
                _follow.Target = _followTargetOnEntry;
                _follow.Smooth = _followSmoothOnEntry;
            }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();

            _follow = null; _cam = null; _followCaptured = false; _followTargetOnEntry = null; _anchor = null;
            _player = null; _iso = null; _body = null; _bodyTaken = false;
            _look = "(not read)"; _lookEvidence = "(not read)"; _pinned = "(not pinned)"; _mood = "(not held)";
            _eased = "(not framed)"; _healed = "none";

            // A case that skipped before loading anything (every case on CI) has nothing to put back.
            if (!_loadedAny) yield break;

            if (GameServices.Clock != null) GameServices.Clock.TimeScale = 1f;
            GameServices.OpeningCinematicRunning = _introOnEntry;
            GameServices.PendingArrivalKey = null;

            // ⚠ Look the cleanup scene up before creating it: CreateScene throws on a name that exists.
            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == StPeters)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            // ⚠ #764: a Single load of a region promotes the persistent core's roots into
            // DontDestroyOnLoad, and unloading the region leaves them there for every test that follows.
            // Destroy exactly the roots this case added, by identity, then clear the service slots.
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            _loadedAny = false;
            yield return null;
        }

        // =============================================================================================
        //  The plates
        // =============================================================================================

        /// <summary>
        /// Every placed house, whole, at the widest view a walker has, and Ginny's yard (her cottage beside
        /// the outbuildings whose sheets did not move): at noon, at 19:30 and at 23:00 in clear weather,
        /// and at noon and 23:00 in rain and in fog. The L2 numbers are taken on the noon clear pass.
        /// </summary>
        [UnityTest]
        public IEnumerator StPeters_TheHouses_AtNoonGoldenHourAndNight_InClearRainAndFog()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            List<(SpriteRenderer sr, string key, string rig)> village = VillageRenderers();
            ReadTheLook(village);
            EnsureTheFollow();
            List<Station> stations = VillageStations(village);
            List<SpriteRenderer> houses = village.Where(v => v.rig == HouseRig).Select(v => v.sr).ToList();
            float widest = WidestWalkingHeight(out string widestSaid);

            string captions = StartFile("captions-village.txt");
            string numbers = StartFile("numbers-village.txt");
            File.AppendAllText(numbers,
                $"look={_look} ({_lookEvidence}); run {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z; Unity " +
                $"{Application.unityVersion}; {SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}\n" +
                Census("houses", houses) +
                Census("other village buildings", village.Where(v => v.rig != HouseRig).Select(v => v.sr)), Utf8);

            var failures = new List<string>();
            var lights = new Dictionary<string, Color>();
            foreach (Condition c in VillageSchedule)
            {
                HoldTheMood(c.Mood);
                yield return PinTheHour(c.Hours, c.Mood == Mood.Clear ? ClearRunSeconds : WeatherRunSeconds);
                lights[c.Key] = Shader.GetGlobalColor("_DayNightTint");

                foreach (Station s in stations)
                {
                    string plate = $"village-{c.Key}-{s.Shot}";
                    yield return FrameOn(s.Centre, widest);
                    yield return RunTheWorld(c.Mood == Mood.Rain ? WeatherRunSeconds : ClearRunSeconds, s.Centre);
                    yield return ShadersReady();

                    string said = CheckTheSubjectIsInFrame(s, plate, failures);
                    if (c.Mood == Mood.Rain) said += CheckItIsRaining(plate, failures);
                    SavePlate(plate + ".png", Capture());
                    File.AppendAllText(captions, Caption(plate, s.Subject, s.Centre, $"framing: {widestSaid}\n{said}"),
                                       Utf8);

                    if (c.Mood == Mood.Clear && c.Hours == Noon)
                    {
                        bool timeIt = s == stations[0] || s == stations[stations.Count - 1];
                        yield return TakeTheNumbers(s, houses, numbers, timeIt);
                    }
                }
            }

            AssertTheLightMoved(lights, failures);
            Assert.IsEmpty(failures, $"[{_look}] the village plates are not evidence as shot:\n  " +
                                     string.Join("\n  ", failures));
        }

        /// <summary>
        /// Every placed village room, entered: she is stood a metre past the sill on the doorway's own line,
        /// the room opens as it does in play (the shell cut away, the room drawn), and the plate is framed
        /// on the room at the standing on-foot view — at noon and at 23:00, in clear weather.
        /// </summary>
        [UnityTest]
        public IEnumerator StPeters_EveryPlacedRoom_ThroughItsDoor_AtNoonAndNight()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            ReadTheLook(VillageRenderers());
            EnsureTheFollow();
            yield return FindHer();
            List<PlacedRoom> rooms = PlacedRooms();
            Assert.IsNotEmpty(rooms, "St Peters places no village room, so there is nothing to photograph.");
            float standing = StandingHeight(out string standingSaid);

            string captions = StartFile("captions-rooms.txt");
            string numbers = StartFile("numbers-rooms.txt");
            File.AppendAllText(numbers,
                $"look={_look} ({_lookEvidence}); run {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z\n" +
                Census("rooms", rooms.SelectMany(RoomRenderers)), Utf8);

            var failures = new List<string>();
            foreach (float hours in new[] { Noon, Night })
            {
                HoldTheMood(Mood.Clear);
                yield return PinTheHour(hours, ClearRunSeconds);

                foreach (PlacedRoom r in rooms)
                {
                    string plate = $"room-{Hhmm(hours)}-{r.Name}";
                    PutHerAt(r.Inside);
                    for (int i = 0; i < InsideWaitFrames && !r.Interior.IsInside; i++) yield return null;
                    if (!r.Interior.IsInside)
                    {
                        failures.Add($"{plate}: she stands at {Metres(r.Inside)}, past the sill on the doorway's " +
                                     $"line, and the room did not open in {InsideWaitFrames} frames. {RoomSaid(r)}");
                        continue;
                    }

                    Vector2 centre = OnTheHalfMetre(r.Room.bounds.center);
                    yield return FrameOn(centre, standing);
                    yield return RunTheWorld(ClearRunSeconds, centre);
                    yield return ShadersReady();

                    string said = RoomSaid(r) + "\n";
                    if (!r.Interior.IsInside)
                        failures.Add($"{plate}: the room closed again while the world ran. {RoomSaid(r)}");
                    else if (!Drawn(r.Room))
                        failures.Add($"{plate}: the room opened but its sprite is not drawn. {RoomSaid(r)}");
                    var s = new Station(r.Name, "", centre, new Renderer[] { r.Room }, new Renderer[0]);
                    said += CheckTheSubjectIsInFrame(s, plate, failures);
                    SavePlate(plate + ".png", Capture());
                    File.AppendAllText(captions, Caption(plate,
                        $"the {r.Key} room of '{r.Interior.gameObject.name}' at " +
                        $"{Metres(r.Interior.transform.position)}, entered", centre,
                        $"framing: {standingSaid}\n{said}"), Utf8);
                }
            }

            Assert.IsEmpty(failures, $"[{_look}] the room plates are not evidence as shot:\n  " +
                                     string.Join("\n  ", failures));
        }

        // =============================================================================================
        //  What is in the scene: the village, the look it is in, the stations and the rooms
        // =============================================================================================

        static List<(SpriteRenderer sr, string key, string rig)> VillageRenderers()
        {
            Dictionary<string, string> rigOf = ReadContract<Buildings>(BuildingsContractPath).buildings
                .ToDictionary(b => b.key, b => b.rig);
            var found = new List<(SpriteRenderer, string, string)>();
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>())
            {
                Texture2D albedo = sr.sprite != null ? sr.sprite.texture : null;
                if (albedo == null || !albedo.name.StartsWith(VillageSheetPrefix, StringComparison.Ordinal)) continue;
                string key = albedo.name.Substring(VillageSheetPrefix.Length);
                if (rigOf.TryGetValue(key, out string rig)) found.Add((sr, key, rig));   // not a channel sheet
            }
            return found;
        }

        /// <summary>Before or after, read off the houses: none bound is today's look, all bound is L2's, and
        /// anything in between is a half-patched scene that is neither.</summary>
        void ReadTheLook(List<(SpriteRenderer sr, string key, string rig)> village)
        {
            var houses = village.Where(v => v.rig == HouseRig).ToList();
            Assert.IsNotEmpty(houses, "St Peters places no village house, so there is nothing to photograph.");
            int bound = houses.Count(h => h.sr.GetComponent<SpriteLightBinder>() != null);
            string materials = string.Join("|", houses
                .Select(h => h.sr.sharedMaterial != null ? h.sr.sharedMaterial.name : "none").Distinct());
            _lookEvidence = $"{bound} of {houses.Count} houses carry a SpriteLightBinder; house material(s) {materials}";
            Assert.IsTrue(bound == 0 || bound == houses.Count,
                $"the scene is half-patched: {_lookEvidence}. A plate of it is neither the before nor the after.");
            _look = bound == 0 ? "before" : "after";
            Debug.Log($"[{PlateDir}] look '{_look}': {_lookEvidence}");
        }

        /// <summary>A station per placed house, centred on the house as drawn; and Ginny's yard, centred on
        /// her outbuildings and the house nearest them together.</summary>
        static List<Station> VillageStations(List<(SpriteRenderer sr, string key, string rig)> village)
        {
            var houses = village.Where(v => v.rig == HouseRig)
                                .OrderBy(v => v.key, StringComparer.Ordinal)
                                .ThenBy(v => v.sr.transform.position.x)
                                .ToList();
            var stations = new List<Station>();
            foreach (var h in houses)
            {
                string name = NameOf(h.sr.transform, h.key, houses.Count(o => o.key == h.key));
                stations.Add(new Station("house-" + name,
                    $"the {h.key} '{h.sr.gameObject.name}' at {Metres(h.sr.transform.position)}, drawn from " +
                    $"'{h.sr.sprite.name}'",
                    OnTheHalfMetre(h.sr.bounds.center), new Renderer[] { h.sr }, new Renderer[0]));
            }

            var yard = village.Where(v => v.key.StartsWith(YardSheetPrefix, StringComparison.Ordinal)).ToList();
            if (yard.Count > 0)
            {
                Bounds b = yard[0].sr.bounds;
                foreach (var y in yard) b.Encapsulate(y.sr.bounds);
                Vector2 middle = b.center;
                var home = houses.OrderBy(h => Vector2.Distance(h.sr.bounds.center, middle)).FirstOrDefault();
                Assert.IsNotNull(home.sr, "Ginny's outbuildings stand in St Peters with no house near them.");
                b.Encapsulate(home.sr.bounds);
                stations.Add(new Station("yard-ginny",
                    $"Ginny's yard: {string.Join(", ", yard.Select(y => y.key))} (sheets that did not move) beside " +
                    $"the {home.key} '{home.sr.gameObject.name}' at {Metres(home.sr.transform.position)} (a house " +
                    "that did)",
                    OnTheHalfMetre(b.center), new Renderer[] { home.sr },
                    yard.Select(y => (Renderer)y.sr).ToArray()));
            }
            return stations;
        }

        /// <summary>Every placed room drawn from a room sheet (a shop is drawn from its own kit and is not
        /// the village return's), with the point a metre past its sill on its doorway's line.</summary>
        static List<PlacedRoom> PlacedRooms()
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo roomField = typeof(BuildingInterior).GetField("_room", Private);
            FieldInfo shellField = typeof(BuildingInterior).GetField("_shell", Private);
            Assert.IsNotNull(roomField, "BuildingInterior no longer serializes _room; re-point this plate");
            Assert.IsNotNull(shellField, "BuildingInterior no longer serializes _shell; re-point this plate");

            var rooms = new List<PlacedRoom>();
            foreach (BuildingInterior interior in Object.FindObjectsByType<BuildingInterior>())
            {
                var room = roomField.GetValue(interior) as SpriteRenderer;
                if (room == null || room.sprite == null ||
                    !room.sprite.texture.name.StartsWith(RoomSheetPrefix, StringComparison.Ordinal))
                    continue;
                InteriorFootprint walls = interior.Footprint;
                float along = walls.LengthMetres * 0.5f - interior.WallThicknessMetres - PastTheSillMetres;
                Vector2 inside = walls.ModelToWorld(new Vector2(walls.DoorAcrossMetres, walls.DoorSign * along));
                Assert.IsTrue(walls.Contains(inside, interior.WallThicknessMetres),
                    $"'{interior.gameObject.name}': a metre past the sill ({Metres(inside)}) is not on its floor.");
                rooms.Add(new PlacedRoom
                {
                    Key = room.sprite.texture.name.Substring(RoomSheetPrefix.Length),
                    Interior = interior,
                    Room = room,
                    Shell = shellField.GetValue(interior) as SpriteRenderer,
                    Inside = inside,
                });
            }
            foreach (PlacedRoom r in rooms)
                r.Name = NameOf(r.Interior.transform, r.Key, rooms.Count(o => o.Key == r.Key));
            return rooms.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
        }

        static IEnumerable<SpriteRenderer> RoomRenderers(PlacedRoom r)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            yield return r.Room;
            FieldInfo upperField = typeof(BuildingInterior).GetField("_upperRoom", Private);
            if (upperField != null && upperField.GetValue(r.Interior) is SpriteRenderer upper && upper != null)
                yield return upper;
        }

        string RoomSaid(PlacedRoom r)
        {
            Transform published = GameServices.PlayerTransform;
            return $"inside {r.Interior.IsInside}; her at {Metres(_player.transform.position)} (the rooms ask " +
                   $"{(published == _player.transform ? "her" : published != null ? $"'{published.name}'" : "no one")}); " +
                   $"the gap in the walls at {Metres(r.Interior.DoorWorld)}; room '{r.Room.sprite.name}' drawn " +
                   $"{Drawn(r.Room)}; shell '{(r.Shell != null && r.Shell.sprite != null ? r.Shell.sprite.name : "none")}' " +
                   $"drawn {r.Shell != null && Drawn(r.Shell)}";
        }

        static bool Drawn(Renderer r) => r != null && r.enabled && r.gameObject.activeInHierarchy;

        /// <summary>The key, and where two of the same stand in one region, where this one does.</summary>
        static string NameOf(Transform t, string key, int sameKey)
        {
            if (sameKey <= 1) return key;
            Vector3 p = t.position;
            return $"{key}-x{Mathf.RoundToInt(p.x)}y{Mathf.RoundToInt(p.y)}";
        }

        /// <summary>The widest view the wheel gives a walker (the shipped <c>FarthestWorldHeightMeters</c>),
        /// landed on the crisp stop it names: the whole of a house fits in it, and the standing view does
        /// not hold one.</summary>
        static float WidestWalkingHeight(out string said)
        {
            Assert.IsNotNull(GameServices.Config, "the region registered no GameConfig, so the walker's zoom is unknown.");
            float farthest = GameServices.Config.PlayerZoom.Sanitized().FarthestWorldHeightMeters;
            int step = CameraZoomPolicy.StepForWorldHeight(farthest, CameraFollow.AssetsPPU,
                                                           CameraFollow.DesignScreenHeightPx);
            float height = CameraZoomPolicy.WorldHeightForStep(step, CameraFollow.AssetsPPU,
                                                               CameraFollow.DesignScreenHeightPx);
            said = $"the walker's widest view, PlayerZoom.FarthestWorldHeightMeters {farthest:0.####} m, on ladder " +
                   $"step {step} = {height:0.####} m";
            return height;
        }

        /// <summary>The walker's standing view (<c>WorldHeightFor(OnFoot)</c>), landed on the crisp stop the
        /// camera lands it on in play: the follow hands the raw request to <c>PixelPerfectCamera</c>, which
        /// re-imposes the nearest ladder step, so 9 m is shown as step 4's 8.4375 m on the 1080 px target.</summary>
        float StandingHeight(out string said)
        {
            float asked = _follow.WorldHeightFor(CameraFraming.OnFoot);
            int step = CameraZoomPolicy.StepForWorldHeight(asked, CameraFollow.AssetsPPU,
                                                           CameraFollow.DesignScreenHeightPx);
            float height = CameraZoomPolicy.WorldHeightForStep(step, CameraFollow.AssetsPPU,
                                                               CameraFollow.DesignScreenHeightPx);
            said = $"the standing on-foot view, WorldHeightFor(OnFoot) {asked:0.####} m, on ladder step {step} = " +
                   $"{height:0.####} m";
            return height;
        }

        // =============================================================================================
        //  The numbers: what the houses hold, what they draw, and what the frame costs
        // =============================================================================================

        /// <summary>Every distinct texture the renderers draw with — the sprite's sheet, and each channel
        /// sheet their property blocks bind — with its size, its format and its bytes: on the GPU, from the
        /// format and the mips; and as the profiler reports it in this editor.</summary>
        static string Census(string what, IEnumerable<SpriteRenderer> renderers)
        {
            var slots = new Dictionary<Texture, string>();
            var materials = new SortedSet<string>(StringComparer.Ordinal);
            var block = new MaterialPropertyBlock();
            int count = 0;
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;
                count++;
                materials.Add(sr.sharedMaterial != null ? sr.sharedMaterial.name : "none");
                if (sr.sprite != null && sr.sprite.texture != null && !slots.ContainsKey(sr.sprite.texture))
                    slots.Add(sr.sprite.texture, "albedo");
                if (!sr.HasPropertyBlock()) continue;
                sr.GetPropertyBlock(block);
                foreach ((string slot, int id) in ChannelSlots)
                {
                    Texture t = block.GetTexture(id);
                    if (t != null && !slots.ContainsKey(t)) slots.Add(t, slot);
                }
            }

            long gpu = 0, runtime = 0;
            var lines = new StringBuilder();
            foreach (KeyValuePair<Texture, string> kv in slots.OrderBy(k => k.Key.name, StringComparer.Ordinal))
            {
                Texture t = kv.Key;
                long g = GpuBytes(t), r = Profiler.GetRuntimeMemorySizeLong(t);
                gpu += g;
                runtime += r;
                lines.AppendLine($"  {what}: texture {t.name} slot={kv.Value} {t.width}x{t.height} {t.graphicsFormat} " +
                                 $"mips={Mips(t)} gpuBytes={g} runtimeBytes={r}");
            }
            return $"{what}: renderers={count} materials={string.Join("|", materials)} textures={slots.Count} " +
                   $"gpuBytes={gpu} ({gpu / 1048576.0:0.00} MB) runtimeBytes={runtime} ({runtime / 1048576.0:0.00} MB)\n" +
                   lines;
        }

        static int Mips(Texture t) => t is Texture2D t2 ? Mathf.Max(1, t2.mipmapCount) : 1;

        static long GpuBytes(Texture t)
        {
            long bytes = 0;
            for (int m = 0; m < Mips(t); m++)
                bytes += GraphicsFormatUtility.ComputeMipmapSize(Mathf.Max(1, t.width >> m), Mathf.Max(1, t.height >> m),
                                                                 t.graphicsFormat);
            return bytes;
        }

        /// <summary>At a framed station, the world stopped: draw calls and batches with the houses and with
        /// every house switched off; and where asked, the frame time both ways.</summary>
        IEnumerator TakeTheNumbers(Station s, List<SpriteRenderer> houses, string numbersPath, bool timeIt)
        {
            long[] on = new long[2], off = new long[2];
            yield return CountDrawCalls(houses, housesOn: true, on);
            yield return CountDrawCalls(houses, housesOn: false, off);
            string line = $"{_look} station={s.Shot} drawCalls={on[0]} batches={on[1]} drawCallsNoHouses={off[0]} " +
                          $"batchesNoHouses={off[1]}";
            if (timeIt)
            {
                double[] tOn = new double[5], tOff = new double[5];
                yield return TimeTheFrame(houses, housesOn: true, tOn);
                yield return TimeTheFrame(houses, housesOn: false, tOff);
                line += $" cpuMsMedian={tOn[0]:0.000} gpuMsMedian={tOn[1]:0.000} mainThreadMsMean={tOn[2]:0.000} " +
                        $"cpuMsMedianNoHouses={tOff[0]:0.000} gpuMsMedianNoHouses={tOff[1]:0.000} " +
                        $"mainThreadMsMeanNoHouses={tOff[2]:0.000} frames={TimedFrames} cpuSamples={tOn[3]:0}/{tOff[3]:0} " +
                        $"gpuSamples={tOn[4]:0}/{tOff[4]:0} (the editor's whole frame, not a player build)";
            }
            File.AppendAllText(numbersPath, line + "\n", Utf8);
            Debug.Log($"[{PlateDir}] numbers: {line}");
        }

        IEnumerator CountDrawCalls(List<SpriteRenderer> houses, bool housesOn, long[] into)
        {
            List<Renderer> off = housesOn ? new List<Renderer>() : SwitchOff(houses);
            var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            try
            {
                for (int i = 0; i < 3; i++) yield return null;   // the world is frozen; the camera still renders
                into[0] = drawCalls.Valid ? drawCalls.LastValue : 0;
                into[1] = batches.Valid ? batches.LastValue : 0;
            }
            finally
            {
                drawCalls.Dispose();
                batches.Dispose();
                foreach (Renderer r in off) if (r != null) r.enabled = true;
            }
        }

        /// <summary>The frame's time over <see cref="TimedFrames"/> frames: the CPU and GPU medians from the
        /// frame-timing manager, and the main thread's mean from the profiler, whichever this editor gives.
        /// Zero where it gives none.</summary>
        IEnumerator TimeTheFrame(List<SpriteRenderer> houses, bool housesOn, double[] into)
        {
            List<Renderer> off = housesOn ? new List<Renderer>() : SwitchOff(houses);
            var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", TimedFrames);
            var cpu = new List<double>(TimedFrames);
            var gpu = new List<double>(TimedFrames);
            var timing = new FrameTiming[1];
            try
            {
                for (int i = 0; i < WarmFrames; i++) yield return null;
                for (int i = 0; i < TimedFrames; i++)
                {
                    yield return null;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timing) == 0) continue;
                    if (timing[0].cpuFrameTime > 0) cpu.Add(timing[0].cpuFrameTime);
                    if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                }
                into[0] = Median(cpu);
                into[1] = Median(gpu);
                into[2] = MeanMilliseconds(mainThread);
                into[3] = cpu.Count;
                into[4] = gpu.Count;
            }
            finally
            {
                mainThread.Dispose();
                foreach (Renderer r in off) if (r != null) r.enabled = true;
            }
        }

        static List<Renderer> SwitchOff(List<SpriteRenderer> renderers)
        {
            var off = new List<Renderer>();
            foreach (SpriteRenderer r in renderers)
                if (r != null && r.enabled) { r.enabled = false; off.Add(r); }
            return off;
        }

        static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            values.Sort();
            int mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) * 0.5;
        }

        static double MeanMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < recorder.Count; i++) sum += recorder.GetSample(i).Value;
            return sum / recorder.Count / 1e6;   // the recorder counts nanoseconds
        }

        // =============================================================================================
        //  The weather and the hour
        // =============================================================================================

        void HoldTheMood(Mood mood)
        {
            ReleaseTheMood();
            IEnvironmentService inner = GameServices.Environment;
            Assert.IsNotNull(inner, "the region registered no environment, so there is no weather to hold.");
            float visibility = mood == Mood.Rain ? RainVisibility : mood == Mood.Fog ? FogVisibility : ClearVisibility;
            float sea01 = mood == Mood.Rain ? RainSea01 : mood == Mood.Fog ? FogSea01 : ClearSea01;
            _held = new HeldMood(inner, visibility, sea01);
            GameServices.Environment = _held;
            _mood = $"{mood.ToString().ToLowerInvariant()}, HELD: visibility {visibility:0.00}, sea state " +
                    $"{sea01:0.00} ({_held.Sample().SeaState}); wind, tide and seed the region's own";
        }

        void ReleaseTheMood()
        {
            if (_held == null) return;
            // Only taken back while it is still ours: a service published over it since is not the plate's.
            if (ReferenceEquals(GameServices.Environment, _held)) GameServices.Environment = _held.Inner;
            _held = null;
        }

        /// <summary>
        /// Stop the clock at <paramref name="hours"/> after day 1's midnight, then let the light catch up —
        /// at least <paramref name="minSeconds"/> of engine time, and until the day/night tint has held
        /// still for four frames. The clock stops only on its own TimeScale, so the light (which ticks on
        /// engine time and reads the clock's hour) gets there while the hour cannot drift.
        /// </summary>
        IEnumerator PinTheHour(float hours, float minSeconds)
        {
            IGameClock clock = GameServices.Clock;
            GameConfig config = GameServices.Config;
            Assert.IsNotNull(clock, "the region registered no clock, so the hour cannot be pinned.");
            Assert.IsNotNull(config, "the region registered no GameConfig, so a day has no length.");

            double spd = config.SecondsPerDay;
            double t = (1.0 + hours / 24.0) * spd;
            Time.timeScale = 1f;
            clock.SeekTo(t);
            clock.TimeScale = 0f;
            Assert.LessOrEqual(Math.Abs(clock.TotalSeconds - t), 1.0,
                $"the clock did not land on {t:0.0} s (it reads {clock.TotalSeconds:0.0} s), so the plate would be " +
                "shot at some other hour.");

            Color tint = Shader.GetGlobalColor("_DayNightTint");
            float waited = 0f;
            int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null;
                frames++;
                waited += Time.deltaTime;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (waited >= minSeconds && still >= 4) break;
            }
            Assert.Less(frames, 900, $"the day/night tint never settled at {Hhmm(hours)}.");

            Vector4 sun = Shader.GetGlobalVector("_SunDir");
            float elevation = Shader.GetGlobalFloat("_SunElevation");
            float hour = clock.HourOfDay;
            int hh = Mathf.FloorToInt(hour), mm = Mathf.FloorToInt((hour - hh) * 60f + 0.5f) % 60;
            _pinned = $"day {clock.DayIndex} {hh:00}:{mm:00} ({hour:0.000} h, TotalSeconds {clock.TotalSeconds:0.0}); " +
                      $"_DayNightTint ({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000}); _SunDir ({sun.x:0.000}, " +
                      $"{sun.y:0.000}); _SunElevation {elevation:0.000}; settled after {frames} frames / {waited:0.00} s";
            Debug.Log($"[{PlateDir}] pinned: {_pinned}; weather {_mood}");
        }

        /// <summary>The light must move with every hour and, at noon, with every weather: a pin or a hold that
        /// did not take would caption two plates with different skies that were shot under one.</summary>
        static void AssertTheLightMoved(Dictionary<string, Color> lights, List<string> failures)
        {
            void Differ(string a, string b)
            {
                if (!lights.TryGetValue(a, out Color x) || !lights.TryGetValue(b, out Color y)) return;
                float d = Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b);
                if (d < SameLight)
                    failures.Add($"{a} and {b} were lit identically ({x}): one of them did not take.");
            }

            string noon = Hhmm(Noon), golden = Hhmm(GoldenHour), night = Hhmm(Night);
            Differ($"{noon}-clear", $"{golden}-clear");
            Differ($"{noon}-clear", $"{night}-clear");
            Differ($"{golden}-clear", $"{night}-clear");
            Differ($"{noon}-clear", $"{noon}-rain");
            Differ($"{noon}-clear", $"{noon}-fog");
            Differ($"{noon}-rain", $"{noon}-fog");
        }

        /// <summary>A rain plate with no rain falling is not a rain plate. The drops are the emitter's own
        /// pooled renderers, each shown by activating it.</summary>
        static string CheckItIsRaining(string plate, List<string> failures)
        {
            FieldInfo pool = typeof(RainEmitter).GetField("_renderers", BindingFlags.Instance | BindingFlags.NonPublic);
            if (pool == null) return "rain: the emitter's drops are unreadable (RainEmitter._renderers moved)\n";
            int emitters = 0, live = 0;
            foreach (RainEmitter e in Resources.FindObjectsOfTypeAll<RainEmitter>())
            {
                if (e == null || !e.isActiveAndEnabled) continue;
                emitters++;
                if (pool.GetValue(e) is SpriteRenderer[] drops)
                    live += drops.Count(d => d != null && d.gameObject.activeSelf);
            }
            if (emitters == 0) failures.Add($"{plate}: no rain emitter is running, so nothing can fall.");
            else if (live == 0)
                failures.Add($"{plate}: a rain plate with no rain falling — the emitter read the held sample and " +
                             "spawned nothing.");
            return $"rain: {live} drops live across {emitters} emitter(s)\n";
        }

        // =============================================================================================
        //  The region, the frame and her
        // =============================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: no graphics device (Null Device), so nothing rendered " +
                              "and nothing was proved. Expected on CI; a plate of the village needs a GPU.");
        }

        /// <summary>
        /// Load a region the way the existing plates do, then wait for the shell to settle at the title.
        /// ⚠ THE SAVE: the save service writes only once the shell has left the title, so a region that
        /// did not boot to it is skipped here, before anything is moved, rather than photographed live.
        /// </summary>
        IEnumerator LoadRegion(string sceneName)
        {
            if (!_loadedAny) _introOnEntry = GameServices.OpeningCinematicRunning;

            GameConfig configBefore = GameServices.Config;
            IGameClock clockBefore = GameServices.Clock;
            IEnvironmentService envBefore = GameServices.Environment;
            IWallet walletBefore = GameServices.Wallet;

            LogAssert.ignoreFailingMessages = true;   // the regions log decor complaints of their own
            _loadedAny = true;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;   // let the self-installing components register

            var healed = new List<string>();
            if (GameServices.Config == null && configBefore != null) { GameServices.Config = configBefore; healed.Add("Config"); }
            if (GameServices.Clock == null && clockBefore != null) { GameServices.Clock = clockBefore; healed.Add("Clock"); }
            if (GameServices.Environment == null && envBefore != null) { GameServices.Environment = envBefore; healed.Add("Environment"); }
            if (GameServices.Wallet == null && walletBefore != null) { GameServices.Wallet = walletBefore; healed.Add("Wallet"); }
            if (healed.Count > 0)
            {
                _healed = $"{sceneName}: re-published {string.Join(", ", healed)} after the region's dev core took them down";
                Debug.Log($"[{PlateDir}] {_healed}.");
            }

            int frames = 0;
            while (frames < 240 && (FindPersistentFollow() == null || !ShellFlow.AtTitle))
            {
                yield return null;
                frames++;
            }
            if (!ShellFlow.AtTitle)
                Assert.Ignore($"SKIPPED: {sceneName} did not settle at the title (the shell is {ShellFlow.Phase}), " +
                              "so the world is live and the save could be written. A plate never risks the save.");
            Assert.IsNotNull(FindPersistentFollow(),
                $"{sceneName}: after 240 frames there is no CameraFollow on the persistent core's camera, so there " +
                "is no play camera to shoot through.");
            Debug.Log($"[{PlateDir}] {sceneName} loaded: region '{GameServices.CurrentRegionId}', shell " +
                      $"{ShellFlow.Phase}, camera up after {frames} frames.");
        }

        void EnsureTheFollow()
        {
            if (_follow != null) return;
            _follow = FindPersistentFollow();
            Assert.IsNotNull(_follow, "no CameraFollow on the persistent core's camera.");
            Assert.IsTrue(_follow.isActiveAndEnabled, "the play camera's CameraFollow is disabled, so it will not follow the anchor.");
            _cam = _follow.GetComponent<Camera>();
            Assert.IsNotNull(_cam, "the play camera's CameraFollow has no Camera.");
            _followTargetOnEntry = _follow.Target;
            _followSmoothOnEntry = _follow.Smooth;
            _followCaptured = true;
        }

        /// <summary>
        /// Hand the play camera's own follow a still anchor, at <paramref name="worldHeight"/>, with the
        /// 1080 px target attached; let the world run until the camera is ON the centre; then stop the world.
        /// ⚠ The hand-over happens with the world STOPPED, so the follow reads the anchor's jump as no motion
        /// (as <c>CliffPxLookPlatePlayTests.FrameOn</c>).
        /// </summary>
        IEnumerator FrameOn(Vector2 at, float worldHeight)
        {
            EnsureTheFollow();
            Time.timeScale = 0f;
            if (_anchor == null)
            {
                _anchor = new GameObject("VillageReturnPlateAnchor");
                _spawned.Add(_anchor);
            }
            _anchor.transform.position = new Vector3(at.x, at.y, 0f);
            _follow.Target = _anchor.transform;
            _follow.Smooth = 1000f;   // any residue arrives in a frame, not over a second of easing
            _cam.transform.position = new Vector3(at.x, at.y, _cam.transform.position.z);
            _askedHeight = worldHeight;
            _follow.SetFraming(worldHeight, 0f);

            if (_rt == null)
            {
                _w = PlateWidthPx;
                _h = PlateHeightPx;
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                _rt.Create();
            }
            _cam.targetTexture = _rt;
            for (int i = 0; i < 2; i++) yield return null;   // the follow reads the anchor while nothing can move

            Time.timeScale = 1f;
            for (int i = 0; i < 8; i++) yield return null;
            yield return SettleCamera(_cam);
            yield return EaseOntoTheCentre(at);

            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;

            Vector3 p = _cam.transform.position;
            float height = _cam.orthographicSize * 2f;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(p.x, p.y), at), OnTheCentreMetres,
                $"the camera sits at ({p.x:0.####}, {p.y:0.####}), not on the station ({at.x:0.###}, {at.y:0.###}), " +
                $"after {_eased}: something else drives it (a region-bounds clamp, a cinematic), so the plate would " +
                "show another stretch of the village and a before and an after would not line up.");
            Assert.AreEqual(worldHeight, height, worldHeight * 0.01f,
                $"the camera frames {height:0.###} m tall, not the {worldHeight:0.###} m asked for.");
        }

        IEnumerator EaseOntoTheCentre(Vector2 at)
        {
            Vector2 stillAt = _cam.transform.position;
            float start = Time.time;
            int frames = 0;
            while (Vector2.Distance(_cam.transform.position, at) > OnTheCentreMetres && Time.time - start < LeadEaseOutSeconds)
            {
                yield return null;
                frames++;
            }
            _eased = $"still at ({stillAt.x:0.####}, {stillAt.y:0.####}), then {frames} frames / {Time.time - start:0.00} s " +
                     "at full time for the look-ahead to ease out";
        }

        /// <summary>Run the world for <paramref name="seconds"/> on a framed station — the clock stays
        /// stopped, so the hour holds while the rain falls and the water moves — then stop it again. The
        /// camera must still be on the station afterwards.</summary>
        IEnumerator RunTheWorld(float seconds, Vector2 at)
        {
            Time.timeScale = 1f;
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
            Vector3 p = _cam.transform.position;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(p.x, p.y), at), OnTheCentreMetres,
                $"the camera drifted off the station to ({p.x:0.####}, {p.y:0.####}) while the world ran.");
        }

        static IEnumerator SettleCamera(Camera cam)
        {
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0, frames = 0;
            while (still < 5 && frames < 400)
            {
                yield return null;
                frames++;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f ||
                             Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            Assert.Less(frames, 400, "the camera never came to rest.");
        }

        static CameraFollow FindPersistentFollow()
        {
            foreach (CameraFollow f in Object.FindObjectsByType<CameraFollow>())
                if (f != null && MoodGradeDirector.IsPersistentCamera(f.GetComponent<Camera>()))
                    return f;
            return null;
        }

        IEnumerator FindHer()
        {
            for (int f = 0; f < 240 && _player == null; f++)
            {
                foreach (PlayerWalkController p in Object.FindObjectsByType<PlayerWalkController>())
                {
                    var iso = p.GetComponent<IsoCharacterSprite>();
                    if (iso == null || iso.Visual == null || iso.Visual.Skin == null) continue;
                    _player = p;
                    _iso = iso;
                    break;
                }
                if (_player == null) yield return null;
            }
            Assert.IsNotNull(_player, "no on-foot player with a character skin after 240 frames, so there is no one " +
                                      "to walk into the rooms.");
            Assert.IsNull(_player.GetComponentInParent<IsoFacetHullRenderer>(),
                "the player is ABOARD a hull; these plates walk her into the houses.");
            Assert.IsTrue(GameServices.PlayerTransform == _player.transform,
                "the player the rooms ask about (GameServices.PlayerTransform) is not the walker this plate moves, " +
                "so moving her would open nothing.");
            _body = _player.GetComponent<Rigidbody2D>();
        }

        void PutHerAt(Vector2 at)
        {
            if (_body != null)
            {
                if (!_bodyTaken) { _bodyWasSimulated = _body.simulated; _bodyTaken = true; }
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
                _body.position = at;
                _body.simulated = false;
            }
            Vector3 was = _player.transform.position;
            _player.transform.position = new Vector3(at.x, at.y, was.z);
            _iso.HoldHeading(SouthDegrees);
        }

        /// <summary>⭐ THE PLATE MUST CONTAIN ITS SUBJECT: each subject's point inside the frame and its
        /// bounds in the camera's view; and of its company, where it has any, at least one in view.</summary>
        string CheckTheSubjectIsInFrame(Station s, string plate, List<string> failures)
        {
            Plane[] view = GeometryUtility.CalculateFrustumPlanes(_cam);
            var said = new StringBuilder();
            foreach (Renderer r in s.Subjects)
            {
                Vector3 vp = _cam.WorldToViewportPoint(r.transform.position);
                bool inView = Drawn(r) && GeometryUtility.TestPlanesAABB(view, r.bounds);
                said.AppendLine($"subject '{r.gameObject.name}' at {Metres(r.transform.position)}: viewport " +
                                $"({vp.x:0.000}, {vp.y:0.000}), drawn and in view {inView}");
                if (vp.x < 0.02f || vp.x > 0.98f || vp.y < 0.02f || vp.y > 0.98f)
                    failures.Add($"{plate}: '{r.gameObject.name}' stands outside the frame (viewport {vp.x:0.000}, {vp.y:0.000}).");
                else if (!inView)
                    failures.Add($"{plate}: '{r.gameObject.name}' is in the frame but not drawn in it.");
            }
            if (s.Company.Length > 0)
            {
                int seen = s.Company.Count(r => Drawn(r) && GeometryUtility.TestPlanesAABB(view, r.bounds));
                said.AppendLine($"company: {seen} of {s.Company.Length} in view " +
                                $"({string.Join(", ", s.Company.Select(r => r.gameObject.name))})");
                if (seen == 0) failures.Add($"{plate}: none of its company ({s.Company.Length}) is in the frame.");
            }
            return said.ToString();
        }

        // =============================================================================================
        //  Shooting, captions and files
        // =============================================================================================

        /// <summary>
        /// A plate is a picture of the shaders, not of their cyan stand-ins. Draw the frame once so every variant it
        /// needs has been asked for, then let the editor's background compiler finish before the shot is taken.
        /// Not <c>ShaderUtil.allowAsyncCompilation = false</c>: compiling in place hung a batch PlayMode run on D3D12
        /// at its first <see cref="Capture"/> (the editor and its shader compiler both idle, the log silent), and no
        /// other plate touches that setting.
        /// </summary>
        IEnumerator ShadersReady()
        {
#if UNITY_EDITOR
            _cam.Render();
            float until = Time.realtimeSinceStartup + ShaderWaitSeconds;
            int frames = 0;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames++;
            }
            if (UnityEditor.ShaderUtil.anythingCompiling)
                Debug.LogWarning($"[{PlateDir}] shaders still compiling after {frames} frame(s) ({ShaderWaitSeconds} s): " +
                                 "this shot may hold a stand-in.");
            else if (frames > 0)
                Debug.Log($"[{PlateDir}] waited {frames} frame(s) for the shaders to compile before the shot.");
#endif
            yield break;
        }

        byte[] Capture()
        {
            _cam.Render();
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prevActive;

            // ⚠ The project renders in LINEAR colour space, so a raw float read-back saves far too dark.
            Color[] px = tex.GetPixels();
            var outBytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                Color c = new Color(Mathf.Clamp01(px[i].r), Mathf.Clamp01(px[i].g), Mathf.Clamp01(px[i].b), 1f).gamma;
                outBytes[i * 4 + 0] = (byte)Mathf.RoundToInt(c.r * 255f);
                outBytes[i * 4 + 1] = (byte)Mathf.RoundToInt(c.g * 255f);
                outBytes[i * 4 + 2] = (byte)Mathf.RoundToInt(c.b * 255f);
                outBytes[i * 4 + 3] = 255;
            }
            Object.DestroyImmediate(tex);
            return outBytes;
        }

        string Caption(string plate, string subject, Vector2 centre, string extra)
        {
            Vector3 p = _cam.transform.position;
            float height = _cam.orthographicSize * 2f;
            Transform her = GameServices.PlayerTransform;
            MoodGradeDirector grade = MoodGradeDirector.Instance;
            var sb = new StringBuilder();
            sb.AppendLine($"== {plate} [{_look}]");
            sb.AppendLine(subject);
            sb.AppendLine($"look: {_lookEvidence}");
            sb.AppendLine($"run: started {_runStartedUtc:yyyy-MM-dd HH:mm:ss}Z, Unity {Application.unityVersion}, " +
                          $"{SystemInfo.graphicsDeviceType} on {SystemInfo.graphicsDeviceName}, box {Application.dataPath}");
            sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}; region '{GameServices.CurrentRegionId}'; shell " +
                          $"{ShellFlow.Phase}; services healed: {_healed}");
            sb.AppendLine($"asked centre {Metres(centre)}; camera at ({p.x:0.####}, {p.y:0.####}); {_eased}");
            sb.AppendLine($"ortho {_cam.orthographicSize:0.#####} -> {height * _cam.aspect:0.###} x {height:0.####} m " +
                          $"(asked {_askedHeight:0.####} m); render texture {_w}x{_h} px; " +
                          $"{_follow.WorldUnitsPerRenderedPixel:0.#####} m per rendered pixel");
            sb.AppendLine($"hour and light: {_pinned}");
            sb.AppendLine($"weather: {_mood}");
            sb.AppendLine(grade != null
                ? $"grade: graded {grade.LastTickGraded}, strength {grade.LastStrength:0.00}"
                : "grade: no MoodGradeDirector");
            sb.AppendLine($"her: {(her != null ? Metres(her.position) : "not published")}");
            // An open room cuts its house's shell away, so a house plate says which rooms were open.
            string open = string.Join(", ", Object.FindObjectsByType<BuildingInterior>()
                .Where(i => i.IsInside).Select(i => $"'{i.gameObject.name}' at {Metres(i.transform.position)}"));
            sb.AppendLine($"rooms open: {(open.Length > 0 ? open : "none")}");
            sb.Append(extra);
            sb.AppendLine();
            return sb.ToString();
        }

        string PlatePath()
        {
            string dir = Path.Combine(Application.temporaryCachePath, PlateDir, _look);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>This case's file, emptied: a caption or a number from an earlier run is never read as
        /// this one's.</summary>
        string StartFile(string name)
        {
            string path = Path.Combine(PlatePath(), name);
            File.WriteAllText(path, "", Utf8);
            return path;
        }

        void SavePlate(string name, byte[] rgbaBottomLeft)
        {
            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgbaBottomLeft);
            tex.Apply();
            string path = Path.Combine(PlatePath(), name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[{PlateDir}] plate written: {path}");
        }

        static string Hhmm(float hours)
        {
            float h = hours % 24f;
            int hh = Mathf.FloorToInt(h), mm = Mathf.RoundToInt((h - hh) * 60f);
            return $"{hh:00}{mm:00}";
        }

        static string Metres(Vector3 v) => $"({v.x:0.##}, {v.y:0.##})";

        static string Metres(Vector2 v) => $"({v.x:0.##}, {v.y:0.##})";

        static Vector2 OnTheHalfMetre(Vector3 v) => new Vector2(Mathf.Round(v.x * 2f) * 0.5f, Mathf.Round(v.y * 2f) * 0.5f);

        static T ReadContract<T>(string relativePath) where T : class
        {
            string path = Path.Combine(Application.dataPath, relativePath);
            Assert.IsTrue(File.Exists(path), $"the contract is not at {path}");
            var contract = JsonUtility.FromJson<T>(File.ReadAllText(path));
            Assert.IsNotNull(contract, $"{path} does not parse");
            return contract;
        }

        // =============================================================================================
        //  DontDestroyOnLoad bookkeeping (#764)
        // =============================================================================================

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
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
