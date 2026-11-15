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
    /// ashore runs. Since the owner's 2026-11-15 ruling the press offers a length and the confirmed night
    /// runs fast (<see cref="SleepFlow"/>): she wakes the chosen hours on, one <see cref="DayStarted"/>
    /// for the midnight crossed, one save at the wake recording the bunk aboard.
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
        private const string RegionId = "region.test_bunk_harbour";
        private const double Exact = 1e-9;

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
        private string _regionBefore;
        private bool _responderWasInstalled;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            SleepFlow.Reset();   // a booted core may have left a sleep or a chooser; the test presents its own
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
            _regionBefore = GameServices.CurrentRegionId;
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
            SleepFlow.Reset();
            if (!_responderWasInstalled) RestSaveResponder.Uninstall();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();
            GameServices.Config = null;
            GameServices.Save = _saveBefore;
            GameServices.Clock = _clockBefore;
            GameServices.CurrentRegionId = _regionBefore;

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) UnityEngine.Object.Destroy(_spawned[i]);
            _spawned.Clear();
            BoatInteriorCells.Reset();
        }

        [UnityTest]
        public IEnumerator BelowAtHerBunk_APressOffersTheNight_ConfirmedItRunsFastToTheChosenHour_OneDayStarted_AndOneSaveAtTheWake()
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
            GameServices.CurrentRegionId = RegionId;
            RestSaveResponder.Install();
            SleepFlow.ChooserPresent = true;   // the chooser a real session presents (owner ruling 2026-11-15)

            // Turned in the longest sleep before the wake hour, on the day after the start, so the offer
            // "until the morning" is exactly the longest sleep and crosses one midnight.
            float longest = SleepPlan.MaxHours(_config);
            double hour = SleepPlan.SecondsPerGameHour(_config.SecondsPerDay);
            double bedHour = _config.WakeHour - longest;
            if (bedHour < 0d) bedHour += 24d;
            double bedtime = _config.SecondsPerDay + bedHour * hour;
            clock.SeekTo(bedtime);
            double expectedWake = bedtime + longest * hour;

            // ---- below at the berth, and the press, in one frame ----------------------------------
            int level = cabin.LevelIndexAtHeight(bunk.ReachPoint.z);
            Assert.IsTrue(cabin.TryEnter(level < 0 ? 0 : level), "her cabin takes her in on the bunk's level");
            Assert.IsTrue(bunk.IsAvailable, "below, on the berth's sole, the bunk is offered");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "a cabin sole is walked on deck");
            bunk.Interact(new InteractActor(bunk.WorldPosition, Vector2.up, InteractContext.OnDeck));

            // ---- the press offers a length; nothing moves while she chooses -----------------------
            Assert.AreEqual(SleepPhase.Choosing, SleepFlow.Phase, "the press lies her down and offers a length");
            Assert.AreEqual(longest, SleepFlow.Hours, 1e-4f, "the first offer is until the morning");
            Assert.AreEqual(bedtime, clock.TotalSeconds, Exact, "no time passes while she chooses");
            Assert.AreEqual(0, save.Saves, "and nothing is saved before she sleeps");
            SleepFlow.Adjust(+1);
            Assert.AreEqual(longest, SleepFlow.Hours, 1e-4f, "never longer than the longest sleep");
            SleepFlow.Adjust(-1);
            Assert.AreEqual(longest - SleepPlan.StepHours(_config), SleepFlow.Hours, 1e-4f, "a step shorter");
            SleepFlow.Adjust(+1);
            Assert.AreEqual(longest, SleepFlow.Hours, 1e-4f, "and a step back");

            // ---- confirmed, the night runs fast, a frame at a time --------------------------------
            SleepFlow.Confirm();
            Assert.AreEqual(SleepPhase.Asleep, SleepFlow.Phase, "confirming falls asleep");
            Assert.AreEqual(expectedWake, SleepFlow.WakeSeconds, Exact, "to wake the chosen hours on");

            float frame = Time.fixedDeltaTime;
            int frames = Mathf.CeilToInt(longest / (SleepPlan.GameHoursPerRealSecond(_config) * frame)) + 2;
            SleepFlow.Tick(frame);
            Assert.IsTrue(SleepFlow.InProgress, "one frame does not sleep the whole night: time runs fast, not in a jump");
            Assert.Greater(clock.TotalSeconds, bedtime, "the clock moved forward, never back");
            Assert.Less(clock.TotalSeconds, expectedWake);
            Assert.AreEqual(0, save.Saves, "no save while asleep");
            for (int i = 0; i < frames && SleepFlow.InProgress; i++) SleepFlow.Tick(frame);

            // ---- she wakes on her own at the chosen hour, and the wake is the save ----------------
            Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase, "she wakes on her own when the hours are up");
            Assert.AreEqual(expectedWake, clock.TotalSeconds, Exact, "at the bedtime plus the hours chosen");
            Assert.AreEqual(_config.WakeHour, clock.HourOfDay, 1e-3f, "which is the wake hour");
            Assert.AreEqual(1, _days.Count, "one midnight slept through, one DayStarted");
            Assert.AreEqual(1, save.Saves, "one save, at the wake");
            Assert.AreEqual(1, _saved.Count);
            Assert.IsTrue(_saved[0].Ok, "and the day is kept");
            Assert.AreEqual(SleepFlow.RestedText, _saved[0].Reason, "a whole sleep, not a cancelled one");

            SleepPlace place = RestLocker.Place(save.Current);
            Assert.IsTrue(place.Aboard, "the save records that she slept aboard");
            Assert.AreEqual(bunk.Id, place.BedId, "in this bunk");
            Assert.AreEqual(expectedWake, place.WokeAtSeconds, Exact, "and when she woke");
            Assert.IsFalse(RestQuorum.IsOccupied(bunk.Id), "and she is out of the bunk again");
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
