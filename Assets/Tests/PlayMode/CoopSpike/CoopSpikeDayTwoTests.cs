#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.CoopSpike;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.CoopSpike
{
    // These are two local gameplay fixtures, not two copies of GameServices. Only the network peers
    // run concurrently. The active local context is selected explicitly for each fishing attempt.
    public sealed partial class CoopSpikeNetworkTests
    {
        private readonly List<GameObject> _dayTwoObjects = new();
        private Scene _waterScene, _previousScene;
        private bool _loadedWater;
        private bool _dayTwoContext;
        private ISaveService _previousSave;

        private CoopBoatReplica Host => m_ServerNetworkManager.LocalClient.PlayerObject.GetComponent<CoopBoatReplica>();
        private CoopBoatReplica HostAtClient => m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects[
            Host.NetworkObjectId].GetComponent<CoopBoatReplica>();

        private sealed class LocalRig
        {
            public GameObject Root;
            public BoatController Boat;
            public ShipHold Hold;
            public PlayerWalkController Player;
            public CarryHands Hands;
            public IsoCharacterSprite Character;
            public DeckRiderVisual Rider;
            public ControlSwitcher Switcher;
            public DevBoatPicker Picker;
            public CarriableTool Rod;
        }

        private IEnumerator EnterEastWater()
        {
            _previousSave = GameServices.Save;
            _dayTwoContext = true;
            GameServices.Reset();
            _previousScene = SceneManager.GetActiveScene();
            _waterScene = SceneManager.GetSceneByName("EastWater");
            if (!_waterScene.IsValid() || !_waterScene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/EastWater.unity", LoadSceneMode.Additive);
                _waterScene = SceneManager.GetSceneByName("EastWater");
                _loadedWater = true;
            }
            Assert.That(_waterScene.isLoaded, Is.True);
            SceneManager.SetActiveScene(_waterScene);
            GameServices.CurrentRegionId = "region.east_water";
            yield return null;
            var terrain = _waterScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTidalTerrain>()).Single();
            GameServices.TidalTerrain = terrain;
            Assert.That(terrain.ElevationAt(Vector2.zero), Is.EqualTo(-4f).Within(0.0001f));
            TestContext.WriteLine("Scene: authored EastWater; no scene builder or save writer started.");
        }

        private IEnumerator TearDownDayTwo()
        {
            foreach (var root in _dayTwoObjects) if (root != null) Object.Destroy(root);
            _dayTwoObjects.Clear();
            yield return null;
            if (_previousScene.IsValid() && _previousScene.isLoaded) SceneManager.SetActiveScene(_previousScene);
            if (_loadedWater && _waterScene.IsValid() && _waterScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_waterScene);
            _loadedWater = false;
            if (_dayTwoContext)
            {
                GameServices.Reset();
                GameServices.Save = _previousSave;
                _dayTwoContext = false;
            }
        }

        private LocalRig MakeLocalRig(CoopSpikeSettings settings, bool client)
        {
            var rig = new LocalRig { Root = new GameObject(client ? "ClientLocalFixture" : "HostLocalFixture") };
            _dayTwoObjects.Add(rig.Root);
            var boatObject = new GameObject("LocalBoat");
            boatObject.SetActive(false);
            boatObject.transform.SetParent(rig.Root.transform);
            rig.Boat = boatObject.AddComponent<BoatController>();
            rig.Boat.SetHull(settings.Roster[0]);
            rig.Hold = boatObject.AddComponent<ShipHold>();
            rig.Hold.SetHull(settings.Roster[0]);
            var hullRenderer = boatObject.AddComponent<SpriteRenderer>();
            BoatHullSkinner.ApplyHull(boatObject, hullRenderer, settings.Roster[0], rig.Boat);
            boatObject.SetActive(true);
            rig.Boat.GetComponent<Rigidbody2D>().gravityScale = 0;

            var playerObject = new GameObject("LocalSkipper");
            playerObject.SetActive(false);
            playerObject.transform.SetParent(rig.Root.transform);
            var bodyRenderer = playerObject.AddComponent<SpriteRenderer>();
            rig.Character = playerObject.AddComponent<IsoCharacterSprite>();
            rig.Character.Configure(settings.Character);
            rig.Player = playerObject.AddComponent<PlayerWalkController>();
            rig.Hands = playerObject.AddComponent<CarryHands>();
            var riderObject = new GameObject("LocalRider");
            riderObject.transform.SetParent(playerObject.transform, false);
            rig.Rider = playerObject.AddComponent<DeckRiderVisual>();
            rig.Rider.Configure(riderObject.AddComponent<SpriteRenderer>(), bodyRenderer, rig.Character);
            playerObject.SetActive(true);

            var controlObject = new GameObject("LocalControl");
            controlObject.SetActive(false);
            controlObject.transform.SetParent(rig.Root.transform);
            rig.Switcher = controlObject.AddComponent<ControlSwitcher>();
            rig.Switcher.Configure(rig.Player, rig.Boat, null, null, 8, null);
            controlObject.SetActive(true);
            rig.Rod = CoopDevStart.PrepareLocal(settings, rig.Boat, rig.Player, rig.Switcher, client);
            rig.Picker = boatObject.GetComponent<DevBoatPicker>();
            // Picking the active process's hands is fixture context, not another static gameplay player.
            GameServices.Hands = rig.Hands;
            GameServices.CatchHands = rig.Hands;
            return rig;
        }

        private static void Bind(CoopBoatReplica replica, LocalRig rig)
            => replica.BindLocal(rig.Boat.transform, rig.Player.transform, () => rig.Boat.Hull.Id, () => string.Empty);

        [UnityTest]
        public IEnumerator EastWaterDevStartBoardsBothPlayersWithSeparateRods()
        {
            yield return EnterEastWater();
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            Assert.That(settings.Scene, Is.EqualTo("EastWater"));
            var host = MakeLocalRig(settings, false);
            var client = MakeLocalRig(settings, true);
            foreach (var rig in new[] { host, client })
            {
                Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.Aboard));
                Assert.That(rig.Picker.IsAtHelm, Is.True);
                Assert.That(CarriedItem.InHand(rig.Hands, settings.Rod.Id), Is.True);
                Assert.That(rig.Rod.DefId, Is.EqualTo(settings.Rod.Id));
            }
            Assert.That(host.Rod, Is.Not.SameAs(client.Rod));
            Assert.That((Vector2)host.Boat.transform.position, Is.EqualTo(settings.HostStart));
            Assert.That((Vector2)client.Boat.transform.position, Is.EqualTo(settings.HostStart + settings.ClientOffset));
            Bind(Host, host);
            Bind(Client, client);
            yield return WaitForConditionOrTimeOut(() => HostAtClient.Boat.Value.HullId.ToString() == host.Boat.Hull.Id
                && ClientAtHost.Boat.Value.HullId.ToString() == client.Boat.Hull.Id);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
        }

        [UnityTest]
        public IEnumerator EachPlayersFVisitsEveryHullAndTheOtherScreenReskins()
        {
            yield return EnterEastWater();
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            Assert.That(settings.Roster.All(hull => hull != null), Is.True);
            Assert.That(settings.Roster.Select(hull => hull.Id).Distinct().Count(), Is.EqualTo(settings.Roster.Length));
            var rigs = new[] { MakeLocalRig(settings, false), MakeLocalRig(settings, true) };
            Bind(Host, rigs[0]); Bind(Client, rigs[1]);
            var remotes = new[] { HostAtClient, ClientAtHost };
            var input = new InputTestFixture();
            input.Setup();
            try
            {
                var keyboard = InputSystem.AddDevice<Keyboard>();
                for (int player = 0; player < rigs.Length; player++)
                {
                    for (int other = 0; other < rigs.Length; other++) rigs[other].Picker.enabled = other == player;
                    Assert.That(rigs[player].Picker.IsAtHelm, Is.True);
                    // Include wrap so entry zero is also reached by a real F edge.
                    for (int step = 1; step <= settings.Roster.Length; step++)
                    {
                        string expected = settings.Roster[step % settings.Roster.Length].Id;
                        input.Press(keyboard.fKey, queueEventOnly: true);
                        yield return null;
                        Assert.That(rigs[player].Boat.Hull.Id, Is.EqualTo(expected), $"player {player}, F {step}");
                        input.Release(keyboard.fKey);
                        yield return WaitForConditionOrTimeOut(() => remotes[player].Boat.Value.HullId.ToString() == expected);
                        Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False, $"player {player}, replicated {expected}");
                        Assert.That(remotes[player].GetComponent<CoopReplicaView>().DrawnHullId, Is.EqualTo(expected));
                        TestContext.WriteLine($"player {player}: F -> {expected}; peer drew {expected}");
                    }
                }
            }
            finally { input.TearDown(); }
        }

        [UnityTest]
        public IEnumerator BothBakedSkippersStandAtTheirOwnersPositionsWithoutRemoteGameplay()
        {
            yield return EnterEastWater();
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            var rigs = new[] { MakeLocalRig(settings, false), MakeLocalRig(settings, true) };
            var owners = new[] { Host, Client };
            var remotes = new[] { HostAtClient, ClientAtHost };
            for (int i = 0; i < rigs.Length; i++)
            {
                rigs[i].Boat.transform.rotation = Quaternion.Euler(0, 0, i == 0 ? 25 : -40);
                rigs[i].Character.HoldSpeed(0);
                Bind(owners[i], rigs[i]);
            }
            for (int step = 0; step < 3; step++)
            {
                for (int i = 0; i < rigs.Length; i++)
                    rigs[i].Player.transform.position = rigs[i].Boat.transform.TransformPoint(new Vector3(0.2f * step, -0.3f * step));
                yield return WaitForConditionOrTimeOut(() => Enumerable.Range(0, remotes.Length).All(i =>
                    Vector3.Distance(remotes[i].Boat.Value.SkipperLocal, new Vector3(0.2f * step, -0.3f * step)) < 0.001f
                    && Mathf.Abs(Mathf.DeltaAngle(remotes[i].Boat.Value.SkipperHeading, rigs[i].Character.HeadingDegrees)) < 0.001f));
                Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
                yield return null;
                for (int i = 0; i < remotes.Length; i++)
                {
                    var view = remotes[i].GetComponent<CoopReplicaView>();
                    Assert.That(view.Character.Visual, Is.SameAs(settings.Character));
                    Assert.That(view.Character.HasArt, Is.True);
                    Assert.That(view.Skipper.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
                    Assert.That(view.Character.HeadingDegrees, Is.EqualTo(rigs[i].Character.HeadingDegrees).Within(0.001f));
                    Assert.That(view.Skipper.GetComponent<SpriteRenderer>().enabled, Is.True);
                    Assert.That(Vector3.Distance(view.Skipper.position,
                        remotes[i].transform.TransformPoint(remotes[i].Boat.Value.SkipperLocal)), Is.LessThan(0.001f));
                    Assert.That(remotes[i].GetComponent<BoatController>(), Is.Null);
                    Assert.That(view.Skipper.GetComponent<PlayerWalkController>(), Is.Null);
                    Assert.That(view.Skipper.GetComponent<CarryHands>(), Is.Null);
                }
            }
            yield return CaptureHostFrame(rigs[0].Boat.gameObject, ClientAtHost.gameObject);
        }

        private sealed class SeaWorld : IFishSchoolWorld, IEnvironmentService
        {
            private readonly GameConfig _config;
            private readonly RegionDef _region;
            private readonly WindProfile _wind;
            public SeaWorld(int seed, GameConfig config, RegionDef region)
            {
                WorldSeed = seed; _config = config; _region = region;
                _wind = WindProfileDef.Resolve(region.WindProfile, region.Id);
                ActiveTideProfile = new TideProfile { MeanLevel = region.TideMeanLevel,
                    Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours };
            }
            public int WorldSeed { get; }
            public string RegionId => _region.Id;
            public double Seconds;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
            {
                WeatherModel.Sample(Seconds, WorldSeed, _config, _wind,
                    out var wind, out var sea, out var visibility);
                return new EnvironmentSample(wind, Vector2.zero, TideHeightAt(Seconds), sea, visibility,
                    WeatherModel.SeaState01(wind.magnitude));
            }
            public float TideHeightAt(double seconds) => TideModel.Height(seconds, ActiveTideProfile, _config);
            public float WaterColumnAt(Vector2 point, double seconds) => TideHeightAt(seconds) -
                GameServices.TidalTerrain.ElevationAt(point);
            public float SeaState01At(double seconds)
                => WeatherModel.SeaState01(WeatherModel.SampleWind(seconds, WorldSeed, _config.SecondsPerHour,
                    _wind).magnitude);
            public Season SeasonAt(double seconds) => Season.EarlySpring;
            public float HourOfDayAt(double seconds) => (float)(seconds % _config.SecondsPerDay / _config.SecondsPerHour);
            public float TideRateMetresPerHourAt(double seconds) => TideModel.Rate(seconds, ActiveTideProfile, _config);
        }

        [UnityTest]
        public IEnumerator HostSeedAndTimeProduceTheSameEastWaterWeatherTideAndNonemptySchools()
        {
            yield return EnterEastWater();
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/EastWater.asset");
            var pool = AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data/Fish" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(fish => fish.RegionIds.Contains(region.Id)).ToArray();
            Assert.That(pool, Is.Not.Empty);
            int schoolsCompared = 0;
            for (int hour = 0; hour < 12; hour++)
            {
                double target = 1234.5 + hour * config.SecondsPerHour;
                _hostClock.SeekTo(target);
                yield return WaitForConditionOrTimeOut(() => Client.ClockAccepted && _clientClock.TotalSeconds == target);
                Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
                var a = new SeaWorld(ClientAtHost.Sea.Value.Seed, config, region) { Seconds = _hostClock.TotalSeconds };
                var b = new SeaWorld(Client.Sea.Value.Seed, config, region) { Seconds = _clientClock.TotalSeconds };
                Assert.That(b.WorldSeed, Is.EqualTo(a.WorldSeed));
                var hostWeather = a.Sample(); var clientWeather = b.Sample();
                Assert.That(clientWeather.TideHeight, Is.EqualTo(hostWeather.TideHeight));
                Assert.That(clientWeather.WindVector, Is.EqualTo(hostWeather.WindVector));
                Assert.That(clientWeather.SeaState, Is.EqualTo(hostWeather.SeaState));
                Assert.That(clientWeather.Visibility, Is.EqualTo(hostWeather.Visibility));
                var tuning = config.FishSchools;
                var depth = config.DepthDrop;
                var hostSchools = new FishSchoolModel(a, pool, in tuning, in depth, config.SecondsPerHour);
                var clientSchools = new FishSchoolModel(b, pool, in tuning, in depth, config.SecondsPerHour);
                var left = new List<FishSchool>(); var right = new List<FishSchool>();
                hostSchools.SchoolsInView(new Rect(-180, -180, 360, 360), a.Seconds, left);
                clientSchools.SchoolsInView(new Rect(-180, -180, 360, 360), b.Seconds, right);
                Assert.That(right.Count, Is.EqualTo(left.Count));
                for (int n = 0; n < left.Count; n++)
                {
                    Assert.That(right[n].Centre, Is.EqualTo(left[n].Centre));
                    Assert.That(right[n].RadiusMetres, Is.EqualTo(left[n].RadiusMetres));
                    Assert.That(right[n].DepthMetres, Is.EqualTo(left[n].DepthMetres));
                    Assert.That(right[n].MarkCount, Is.EqualTo(left[n].MarkCount));
                    Assert.That(right[n].SpeciesIds, Is.EqualTo(left[n].SpeciesIds));
                    Assert.That(right[n].StartSeconds, Is.EqualTo(left[n].StartSeconds));
                    Assert.That(right[n].EndSeconds, Is.EqualTo(left[n].EndSeconds));
                    schoolsCompared++;
                }
            }
            Assert.That(schoolsCompared, Is.GreaterThan(0), "Two empty seas are not school-agreement proof.");
            TestContext.WriteLine($"Compared {schoolsCompared} nonempty school records over 12 transported clock samples.");
        }

        private sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { } // strictly no file writer in either local fishing fixture
        }

        [UnityTest]
        public IEnumerator EachLocalRodCastsBitesFightsAndLandsOnlyInItsOwnHold()
        {
            yield return EnterEastWater();
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            var rigs = new[] { MakeLocalRig(settings, false), MakeLocalRig(settings, true) };
            Bind(Host, rigs[0]); Bind(Client, rigs[1]);
            var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>("Assets/_Project/Data/Fish/Mackerel.asset");
            var bait = AssetDatabase.LoadAssetAtPath<BaitDef>("Assets/_Project/Data/Resources/Catalog/Bait/SeaWorm.asset");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/EastWater.asset");
            Assert.That(fish.Bite, Is.Not.Null); Assert.That(fish.RodFight, Is.Not.Null);
            Assert.That(fish.RegionIds, Does.Contain(region.Id));
            var saves = new[] { new MemorySave(), new MemorySave() };
            for (int player = 0; player < rigs.Length; player++)
            {
                var rig = rigs[player];
                var peerHold = rigs[1 - player].Hold;
                int peerCount = peerHold.UsedUnits;
                GameServices.Save = saves[player];
                GameServices.Clock = player == 0 ? _hostClock : _clientClock;
                GameServices.Hands = rig.Hands; GameServices.CatchHands = rig.Hands;
                GameServices.Environment = new SeaWorld(12345, config, region) { Seconds = GameServices.Clock.TotalSeconds };
                TackleBox.AddBait(saves[player].Current, bait.Id, 5);
                Assert.That(rig.Switcher.TryInteract(), Is.True, "Leave the helm through the real control transition.");
                Assert.That(rig.Switcher.Mode, Is.EqualTo(ControlMode.OnDeck));
                var fishing = rig.Boat.gameObject.AddComponent<FishingController>();
                fishing.Configure(rig.Hold, new[] { fish }, region.Id, Gear.Handline, 4242 + player, null, config);
                fishing.Angler = rig.Player.transform;
                fishing.SetBait(bait);
                fishing.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                var input = rig.Boat.gameObject.AddComponent<DevFishingInput>();
                input.enabled = false; // test feeds the same public input path, not OS events
                input.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                Assert.That(input.RodIsToHand && input.FishingLive, Is.True);
                var phases = new HashSet<FishingPhase>();
                Vector2 anchor = rig.Player.transform.position;
                foreach (float y in new[] { -0.5f, -1f, -1.5f, -0.5f, 0.3f, 1f })
                    input.TickFishing(0.02f, true, anchor + Vector2.up * y, true);
                input.TickFishing(0.02f, false, anchor + Vector2.up, true);
                Assert.That(fishing.Phase, Is.EqualTo(FishingPhase.Cast));
                Vector2 previous = default;
                bool havePrevious = false;
                for (int tick = 0; tick < 15000 && fishing.Phase != FishingPhase.Landed; tick++)
                {
                    var state = fishing.State;
                    phases.Add(state.Phase);
                    Assert.That(state.Phase, Is.Not.EqualTo(FishingPhase.Snapped));
                    Assert.That(state.Phase, Is.Not.EqualTo(FishingPhase.NoBite));
                    Assert.That(state.Phase, Is.Not.EqualTo(FishingPhase.Idle));
                    var offset = new Vector2(state.FishOffsetX, state.FishOffsetY);
                    var delta = havePrevious ? offset - previous : Vector2.zero;
                    previous = offset; havePrevious = state.Phase == FishingPhase.FightSurface;
                    bool pointerValid = state.Phase == FishingPhase.FightSurface && delta.sqrMagnitude > 1e-8f;
                    bool held = state.Phase == FishingPhase.Bite || (state.SlackWindowOpen && state.Tension01 < 0.6f);
                    input.TickFishing(0.02f, held, anchor - delta.normalized * 2f, pointerValid);
                }
                Assert.That(fishing.Phase, Is.EqualTo(FishingPhase.Landed), $"player {player}");
                Assert.That(phases, Does.Contain(FishingPhase.Waiting));
                Assert.That(phases, Does.Contain(FishingPhase.Bite));
                Assert.That(phases, Does.Contain(FishingPhase.FightDeep));
                Assert.That(phases, Does.Contain(FishingPhase.FightSurface));
                Assert.That(rig.Hold.Items.Count, Is.EqualTo(1));
                Assert.That(rig.Hold.Items[0].SpeciesId, Is.EqualTo(fish.Id));
                Assert.That(peerHold.UsedUnits, Is.EqualTo(peerCount), "This screen's catch must not reach the other hold.");
                Assert.That(CarriedItem.InHand(rig.Hands, settings.Rod.Id), Is.True);
                Assert.That(saves[player].Current, Is.Not.SameAs(saves[1 - player].Current));
                TestContext.WriteLine($"player {player}: cast, bite, deep, surface, landed {fish.Id}; peer hold unchanged.");
            }
            Assert.That(rigs[0].Hold.UsedUnits, Is.EqualTo(1));
            Assert.That(rigs[1].Hold.UsedUnits, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ADisconnectedClientLeavesNoGhostSkipperOnTheHost()
        {
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            Client.PublishBoat(new CoopBoatState { HullId = settings.Roster[0].Id, SkipperLocal = new Vector3(0, 0.3f) });
            yield return WaitForConditionOrTimeOut(() => ClientAtHost.Boat.Value.HullId.ToString() == settings.Roster[0].Id);
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            ulong objectId = Client.NetworkObjectId;
            var skipper = ClientAtHost.GetComponent<CoopReplicaView>().Skipper;
            Assert.That(skipper, Is.Not.Null);
            m_ClientNetworkManagers[0].Shutdown();
            yield return WaitForConditionOrTimeOut(() => !m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(objectId));
            Assert.That(s_GlobalTimeoutHelper.TimedOut, Is.False);
            yield return null;
            Assert.That(skipper == null, Is.True, "The remote skipper must die with its network boat.");
            Assert.That(m_ServerNetworkManager.SpawnManager.SpawnedObjects.Count, Is.EqualTo(1));
        }

        private IEnumerator CaptureHostFrame(GameObject localBoat, GameObject remoteBoat)
        {
            var renderers = localBoat.GetComponentsInChildren<Renderer>().Concat(remoteBoat.GetComponentsInChildren<Renderer>()).ToArray();
            var layers = renderers.Select(renderer => renderer.gameObject.layer).ToArray();
            var cameraObject = new GameObject("SpikeHostEvidenceCamera");
            _dayTwoObjects.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 12;
            camera.transform.position = new Vector3(3.5f, -16, -10);
            camera.cullingMask = 1 << 31;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].gameObject.layer = 31;
                camera.targetTexture = target;
                yield return null;
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence~", "coop-spike"));
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "day2-host-two-boats.png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                TestContext.WriteLine("Host view frame: " + path);
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].gameObject.layer = layers[i];
                Object.Destroy(target); Object.Destroy(texture);
            }
        }
    }
}
#endif
