using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tests.Support;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class DockBoardingPlayTests
    {
        public static readonly float[] Waters = { 2.2f, 0f, -2.2f };
        private readonly List<Object> _owned = new();
        private ControlSwitcher _switcher;
        private Transform _player;
        private CharacterClipPlayer _clip;
        private WharfLadder _ladder;

        private sealed class Tide : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double seconds) => Level;
            public float WaterLevelAt(double seconds) => Level;
        }

        private sealed class IsolatedSave : ISaveService
        {
            public SaveData Current { get; } = new SaveData();
            public int Writes;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Writes++;
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            BoardingLadders.Clear();
            StandableSurfaces.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            BoardingLadders.Clear();
            StandableSurfaces.Clear();
            InteractionGate.Reset();
            GameServices.Reset();
        }

        private GameObject Go(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _owned.Add(go);
            return go;
        }

        private void Build(float water, bool ladder = true, bool platform = true)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.MeshCharacter = false;
            config.MeshCharacterAshore = false;
            _owned.Add(config);
            GameServices.Config = config;
            GameServices.Environment = new Tide { Level = water };

            var player = Go("Climber", new Vector3(2.3f, 0f, 0f));
            player.AddComponent<SpriteRenderer>();
            var walk = player.AddComponent<PlayerWalkController>();
            player.AddComponent<DeckWalkController>();
            _clip = player.AddComponent<CharacterClipPlayer>();
            _clip.Configure(AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(
                "Assets/_Project/Data/Characters/FisherIso.asset"));
            _player = player.transform;
            GameServices.PlayerTransform = _player;

            var boat = Go("Hull", Vector3.zero);
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory";
            hull.DraughtMeters = .3f;
            hull.LengthMeters = 4.5f;
            hull.CameraWorldHeightMeters = 14f;
            _owned.Add(hull);
            var controller = boat.AddComponent<BoatController>();
            controller.SetHull(hull);
            controller.enabled = false;
            var input = boat.AddComponent<DevBoatInput>();
            input.enabled = false;
            var deck = ScriptableObject.CreateInstance<BoatDeckDef>();
            deck.Id = "deck.dock_continuity_fixture";
            deck.WalkHalfExtents = new Vector2(1f, 2f);
            deck.Areas = new[] { DeckArea.From("sole", DeckAreaKind.Deck, new[] {
                new Vector3(-1f,-2f,.85f), new Vector3(1f,-2f,.85f),
                new Vector3(1f,2f,.85f), new Vector3(-1f,2f,.85f) }) };
            _owned.Add(deck);
            BoatDeckAreas.Write(boat, deck);

            if (platform)
                Go("Quay", Vector3.zero).AddComponent<StandablePlatform>()
                    .Configure("wharf.fixture", new Rect(1.8f, -3f, 4f, 6f), 3.4f);
            if (ladder)
            {
                _ladder = Go("Ladder", new Vector3(1.8f, 0f, 0f)).AddComponent<WharfLadder>();
                _ladder.Configure("wharf.fixture.ladder", 3.4f, 270f);
            }
            Transform zone = Go("Dock zone", Vector3.zero).transform;
            Transform landing = Go("Shore landing", new Vector3(2.3f, 0f, 0f)).transform;
            _switcher = Go("Switcher", Vector3.zero).AddComponent<ControlSwitcher>();
            _switcher.Configure(walk, controller, input, zone, 4f, landing);
            _switcher.ConfigureHelm(new Vector2(0f, -1.3f), .05f);
            _switcher.ConfigureDeckFacing(() => 90f);
            _switcher.ConfigureBoardingMove(true, 3f, .8f, .55f, .35f);
            _switcher.enabled = false; // the fixture supplies exactly one measured tick per frame
        }

        private IEnumerator ObserveMove(ControlMode destination)
        {
            const float dt = 1f / 60f;
            // The approach is the 3 m/s walk; both the baked ladder's peak and the step are slower.
            const float measuredWalkSpeed = 3f;
            Vector3 previous = _player.position;
            for (int frame = 0; frame < 2400 && _switcher.IsBoardingMove; frame++)
            {
                _switcher.TickBoardingMove(dt);
                Assert.LessOrEqual(Vector3.Distance(previous, _player.position),
                    measuredWalkSpeed * dt + .002f, $"frame {frame}: a hull/dock move jumped");
                previous = _player.position;
                yield return null;
                Assert.LessOrEqual(Vector3.Distance(previous, _player.position), .002f,
                    "another controller moved the player after the boarding sample");
            }
            Assert.IsFalse(_switcher.IsBoardingMove, "the real-time move never finished");
            Assert.AreEqual(destination, _switcher.Mode);
        }

        [UnityTest]
        public IEnumerator NoMoveBetweenHullAndDockJumps([ValueSource(nameof(Waters))] float water)
        {
            Build(water);
            yield return null;
            Vector3 before = _player.position;
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.Less(Vector3.Distance(before, _player.position), .002f,
                "E must start the dock move without placing the player on the hull");
            Assert.IsTrue(_switcher.IsBoardingMove);
            yield return ObserveMove(ControlMode.OnDeck);
            before = _player.position;
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.Less(Vector3.Distance(before, _player.position), .002f);
            yield return ObserveMove(ControlMode.OnFoot);
        }

        [UnityTest]
        public IEnumerator BoardingGoesByTheLadderWhereOneServes([ValueSource(nameof(Waters))] float water)
        {
            Build(water);
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.IsTrue(_switcher.IsLadderBoarding, "the same berth uses its ladder at every tide");
            yield return ObserveMove(ControlMode.OnDeck);
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.IsTrue(_switcher.IsLadderBoarding, "the way ashore is the same ladder");
            yield return ObserveMove(ControlMode.OnFoot);
        }

        [UnityTest]
        public IEnumerator ARealQuayWithoutALadderRefusesTheLargeGap()
        {
            Build(-2.2f, ladder: false);
            Go("Ladder further along the quay", new Vector3(20f, 0f, 0f))
                .AddComponent<WharfLadder>().Configure("wharf.fixture.other_ladder", 3.4f, 270f);
            yield return null;
            Vector3 before = _player.position;
            Assert.IsFalse(_switcher.BeginInteract());
            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode);
            Assert.AreEqual(before, _player.position);
        }

        [UnityTest]
        public IEnumerator TheOnlyLandingInAnUnbuiltSceneDoesNotStrandThePlayer()
        {
            Build(-2.2f, ladder: false, platform: false);
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract());
            yield return ObserveMove(ControlMode.OnDeck);
            Assert.IsTrue(_switcher.BeginInteract());
            yield return ObserveMove(ControlMode.OnFoot);
        }

        [UnityTest]
        public IEnumerator AscentSeeksThePlantedContactDescentBackwards()
        {
            Build(0f);
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract());
            TimedBoarding.Complete(_switcher);
            Assert.IsTrue(_switcher.BeginInteract());
            bool sawRungs = false;
            float previous = float.PositiveInfinity;
            for (int frame = 0; frame < 2400 && _switcher.IsBoardingMove; frame++)
            {
                _switcher.TickBoardingMove(1f / 60f);
                if (_clip.Clip != CharacterClip.LadderDown) continue;
                Assert.IsTrue(_clip.IsDriven, "ascent must seek its contact frames, not replay descent forwards");
                Assert.LessOrEqual(_clip.FramePosition, previous + .001f);
                previous = _clip.FramePosition;
                sawRungs = true;
            }
            Assert.IsTrue(sawRungs);
        }

        [UnityTest]
        public IEnumerator TheArrivalPassengerUsesTheSameLadderWithAnIsolatedSave(
            [ValueSource(nameof(Waters))] float water)
        {
            Build(water);
            yield return null;
            Assert.IsTrue(TimedBoarding.Interact(_switcher));
            var save = new IsolatedSave();
            GameServices.Save = save;
            var arrival = Go("Arrival fixture", Vector3.zero).AddComponent<ArrivalOpening>();
            arrival.enabled = false;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            void Set(string name, object value) => typeof(ArrivalOpening).GetField(name, flags).SetValue(arrival, value);
            Set("_phase", ArrivalOpening.Phase.Moored);
            Set("_mooredTimer", 100f);
            Set("_player", _player);
            Set("_boatRoot", _player.parent);
            Set("_stepAshore", new Vector2(2.3f, 0f));
            var tick = typeof(ArrivalOpening).GetMethod("TickStepAshore", flags);
            var stepping = typeof(ArrivalOpening).GetField("_stepping", flags);
            Assert.IsTrue(arrival.StepAshore());
            bool sawLadder = false;
            float deadline = Time.realtimeSinceStartup + 45f;
            while ((bool)stepping.GetValue(arrival) && Time.realtimeSinceStartup < deadline)
            {
                tick.Invoke(arrival, null);
                sawLadder |= _clip.Clip == CharacterClip.LadderDown;
                yield return null;
            }
            Assert.IsTrue(sawLadder, "arrival StepAshore must use the registered berth ladder");
            Assert.AreEqual(ArrivalOpening.Phase.HandedOver, arrival.Current);
            Assert.AreEqual(1, save.Writes, "the arrival handover used only the isolated save");
        }

        [UnityTest]
        public IEnumerator AVisibleCancellationReturnsByAMoveInsteadOfRewinding()
        {
            Build(-2.2f);
            GameServices.Config.RequireBoatRepairsBeforeBoarding = true;
            var save = new IsolatedSave();
            save.Current.OwnedBoats.Add("boat.dory");
            HiddenHarbours.Economy.RepairLedger.MarkRepaired(save.Current, "boat.dory");
            GameServices.Save = save;
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract());
            for (int frame = 0; frame < 90; frame++) _switcher.TickBoardingMove(1f / 60f);
            Vector3 before = _player.position;
            save.Current.RepairedBoats.Remove("boat.dory");
            Assert.IsFalse(_switcher.BoardableNow());
            _switcher.TickBoardingMove(1f / 60f);
            Assert.IsTrue(_switcher.IsBoardingMove, "a live gate refusal starts a continuous return");
            Assert.Less(Vector3.Distance(before, _player.position), .002f,
                "a live gate refusal must not rewind the player to shore in one frame");
            yield return ObserveMove(ControlMode.OnFoot);
        }

        [UnityTest]
        public IEnumerator ATeardownDuringALiveReturnSettlesAtOnce()
        {
            Build(-2.2f);
            GameServices.Config.RequireBoatRepairsBeforeBoarding = true;
            var save = new IsolatedSave();
            save.Current.OwnedBoats.Add("boat.dory");
            HiddenHarbours.Economy.RepairLedger.MarkRepaired(save.Current, "boat.dory");
            GameServices.Save = save;
            yield return null;
            Vector3 departure = _player.position;
            Assert.IsTrue(_switcher.BeginInteract());
            for (int frame = 0; frame < 90; frame++) _switcher.TickBoardingMove(1f / 60f);
            save.Current.RepairedBoats.Remove("boat.dory");
            Assert.IsFalse(_switcher.BoardableNow());
            _switcher.TickBoardingMove(1f / 60f);
            _switcher.TickBoardingMove(1f / 60f);
            Assert.IsTrue(_switcher.IsBoardingMove, "the live return is still running");
            Assert.Greater(Vector3.Distance(departure, _player.position), .01f);

            _switcher.enabled = true;
            _switcher.enabled = false;

            Assert.IsFalse(_switcher.IsBoardingMove);
            Assert.Less(Vector3.Distance(departure, _player.position), .001f);
            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.IsTrue(_player.GetComponent<PlayerWalkController>().enabled);
        }

        [UnityTest]
        public IEnumerator DisablingTheLegacyMoveOptionCannotRestoreADockJump()
        {
            Build(0f);
            _switcher.ConfigureBoardingMove(false, 3f, .8f, .55f, .35f);
            yield return null;
            Vector3 before = _player.position;
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.IsTrue(_switcher.IsBoardingMove);
            Assert.AreEqual(before, _player.position);
            yield return ObserveMove(ControlMode.OnDeck);
        }

        [UnityTest]
        public IEnumerator MissingDeckWalkCannotRestoreADockJump()
        {
            Build(0f);
            Object.DestroyImmediate(_player.GetComponent<DeckWalkController>());
            yield return null;
            Vector3 before = _player.position;
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.IsTrue(_switcher.IsBoardingMove);
            Assert.AreEqual(before, _player.position);
            yield return ObserveMove(ControlMode.OnDeck);
        }
    }
}
