using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Player;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Turning in at the cape islander's bunk brings the morning and keeps the day</b> (owner,
    /// 2026-11-14). The real chain on the real hull: a dory root, boarded on the shipped switcher, the
    /// cape islander swapped on under her (the path a purchase and the dev picker both take), her
    /// installer grows the bunk her rig names, and a press below at the berth runs the same rest the bed
    /// ashore runs — the clock at the wake hour, one <see cref="DayStarted"/>, one save.
    ///
    /// <para><b>What it does NOT walk.</b> Getting down to the cuddy is the cabin walk, which
    /// <see cref="BoatCabinOnASwappedHullPlayTests"/> owns; this enters her cabin on the bunk's level
    /// through the cabin's own <see cref="BoatInterior.TryEnter"/> and presses in the same frame, so no
    /// walker tick can disagree in between. The save is a stand-in that never touches the disk (the
    /// shared savegame is never written), handed in only after boarding so the dory's boarding gate reads
    /// no save at all, exactly as the cabin journeys run.</para>
    /// </summary>
    public class BoatBunkOnTheCapeIslanderPlayTests
    {
        private const string BoatsFolder = "Assets/_Project/Data/Boats";
        private const string DoryName = "Dory";
        private const string SubjectName = "CapeIslander";

        sealed class FakeSave : ISaveService
        {
            public int Saves;
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; LoadedExistingSave = true; }
        }

        private readonly List<UnityEngine.Object> _spawned = new();
        private readonly List<DayStarted> _days = new();
        private readonly List<GameSaved> _saved = new();
        private ISaveService _saveBefore;
        private IGameClock _clockBefore;
        private bool _responderWasInstalled;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            SleepFlow.Reset();   // no chooser: these rests are the instant night (a booted core may have left one)
            Interactables.Clear();

            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();
            BoatInteriorCells.Reset();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.InteriorRockScale = GameConfig.DefaultInteriorRockScale;
            // The rig's boat is adrift; the safety rule has its own EditMode cases (BoatBunkTests).
            _config.BunkRequiresMooredOrAnchored = false;
            _spawned.Add(_config);
            GameServices.Config = _config;

            _saveBefore = GameServices.Save;
            _clockBefore = GameServices.Clock;
            GameServices.Save = null;
            _responderWasInstalled = RestSaveResponder.Installed;

            _days.Clear();
            _saved.Clear();
            EventBus.Subscribe<DayStarted>(_days.Add);
            EventBus.Subscribe<GameSaved>(_saved.Add);

            Spawn("Listener").AddComponent<AudioListener>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<DayStarted>(_days.Add);
            EventBus.Unsubscribe<GameSaved>(_saved.Add);
            if (!_responderWasInstalled) RestSaveResponder.Uninstall();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();
            GameServices.Config = null;
            GameServices.Save = _saveBefore;
            GameServices.Clock = _clockBefore;

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) UnityEngine.Object.Destroy(_spawned[i]);
            _spawned.Clear();
            BoatInteriorCells.Reset();
        }

        [UnityTest]
        public IEnumerator BelowAtHerBunk_APressBringsTheWakeHour_OneDayStarted_AndOneSave()
        {
            BoatHullDef dory = LoadHull(DoryName);
            BoatHullDef subject = LoadHull(SubjectName);
            if (dory == null || subject == null) yield break;   // editor-only; LoadHull Ignored

            var boatGo = Spawn(DoryName);
            var boat = boatGo.AddComponent<BoatController>();     // Awake mounts the interior installer
            var input = boatGo.AddComponent<DevBoatInput>();
            var baseRenderer = boatGo.AddComponent<SpriteRenderer>();
            boat.SetHull(dory);
            var picker = boatGo.AddComponent<DevBoatPicker>();
            picker.Configure(new[] { dory }, boat, null, baseRenderer);
            BoatHullSkinner.ApplyHull(boatGo, baseRenderer, dory, boat);
            var installer = boatGo.GetComponent<BoatInteriorInstaller>();
            Assert.IsNotNull(installer, "BoatController.Awake mounts the installer");

            var playerGo = Spawn("Player", new Vector3(1.2f, 0f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var body = playerGo.GetComponent<SpriteRenderer>();
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;
            var character = playerGo.AddComponent<IsoCharacterSprite>();
            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(playerGo.transform, false);
            var riderSr = riderGo.AddComponent<SpriteRenderer>();
            riderSr.enabled = false;
            var rider = playerGo.AddComponent<DeckRiderVisual>();
            rider.Configure(riderSr, body, character);
            walk.enabled = true; boat.enabled = false; input.enabled = false;

            var sw = Spawn("Switcher").AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, null, zoneRadius: 6f, disembarkPoint: null);
            yield return null;

            // ---- aboard, and the cape islander under her -----------------------------------------
            Assert.IsTrue(sw.BeginInteract(), "E within reach starts the boarding");
            yield return WaitUntil(() => sw.Mode == ControlMode.OnDeck, 5f, "the boarding to land her on deck");
            yield return null;
            picker.Show(subject);
            yield return null;

            Assert.AreSame(subject, boat.Hull, "the picker put the cape islander under her");
            Assert.IsTrue(installer.Built, "the swap grew her cabin");
            Assert.AreEqual(1, installer.Bunks.Count, "…and the one bunk her rig names");
            BoatBunk bunk = installer.Bunks[0];
            BoatInterior cabin = installer.Interior;
            Assert.IsFalse(bunk.IsAvailable, "on deck, outside her cabin, the bunk is not offered");

            // ---- the night, kept without a disk ---------------------------------------------------
            var save = new FakeSave();
            GameServices.Save = save;
            GameClock clock = NewClock();
            GameServices.Clock = clock;
            RestSaveResponder.Install();
            double before = clock.TotalSeconds;

            // ---- below at the berth, and the press, in one frame ----------------------------------
            int level = cabin.LevelIndexAtHeight(bunk.ReachPoint.z);
            Assert.IsTrue(cabin.TryEnter(level < 0 ? 0 : level), "her cabin takes her in on the bunk's level");
            Assert.IsTrue(bunk.IsAvailable, "below, on the berth's sole, the bunk is offered");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "a cabin sole is walked on deck");
            bunk.Interact(new InteractActor(bunk.WorldPosition, Vector2.up, InteractContext.OnDeck));

            Assert.AreEqual(_config.WakeHour, clock.HourOfDay, 1e-3f, "she wakes at the wake hour");
            Assert.Greater(clock.TotalSeconds, before, "the clock moved forward, never back");
            Assert.AreEqual(1, _days.Count, "one midnight slept through, one DayStarted");
            Assert.AreEqual(1, save.Saves, "one save");
            Assert.AreEqual(1, _saved.Count);
            Assert.IsTrue(_saved[0].Ok, "and the day is kept");
            Assert.IsFalse(RestLocker.Anchor(save.Current).IsSet, "no wake spot aboard: a reload wakes at the spawn");
        }

        // ---- rig -----------------------------------------------------------------------------

        /// <summary>A real clock on this config. Built inactive so its Awake reads the config rather than
        /// logging that it has none.</summary>
        private GameClock NewClock()
        {
            var go = new GameObject("BunkClock");
            go.SetActive(false);
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            FieldInfo field = typeof(GameClock).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "GameClock._config");
            field.SetValue(clock, _config);
            go.SetActive(true);
            return clock;
        }

        private GameObject Spawn(string name, Vector3 position = default)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        private static IEnumerator WaitUntil(Func<bool> done, float seconds, string what)
        {
            float deadline = Time.time + seconds;
            while (!done() && Time.time < deadline) yield return null;
            Assert.IsTrue(done(), $"timed out after {seconds:0.0}s waiting for {what}");
        }

        private static BoatHullDef LoadHull(string name)
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{BoatsFolder}/{name}.asset");
            Assert.IsNotNull(asset, $"missing {BoatsFolder}/{name}.asset");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: this swaps onto the REAL committed hull.");
            return null;
#endif
        }
    }
}
