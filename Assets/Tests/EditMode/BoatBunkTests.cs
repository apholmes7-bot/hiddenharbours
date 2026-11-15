using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A bunk aboard your own boat turns you in</b> (owner, 2026-11-14: "I also want bunks on boats
    /// sleepable to replenish fatigue"). Pins the press rule (<see cref="BoatBunk.Judge"/>): your boat,
    /// below at the berth, no deck work mid-cycle (#193), and — while
    /// <see cref="GameConfig.BunkRequiresMooredOrAnchored"/> is on — made fast, berthed or at anchor. And
    /// pins the save half: a bunk's rest records NO wake spot, so a reload never wakes the player in the
    /// sea, while a bed ashore keeps its anchor exactly as before (ADR 0037, note 2026-11-14).
    ///
    /// <para>Hand-built cabin for the rule (two soles, the cape islander's heights), the committed cape
    /// islander for the installer. No clock, no disk: the save is a stand-in that never writes.</para>
    /// </summary>
    public class BoatBunkTests
    {
        const float HouseSoleZ = 0.72f;
        const float CuddySoleZ = 0.3f;
        const int HouseLevel = 0;
        const int CuddyLevel = 1;
        static readonly Vector3 BunkReach = new Vector3(0f, 2.9f, CuddySoleZ);
        const string Region = "region.bunk_test";
        const string CapeInteriorPath = "Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset";

        sealed class FakeSave : ISaveService
        {
            public int Saves;
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; LoadedExistingSave = true; }
        }

        readonly List<Object> _spawned = new List<Object>();
        readonly List<RestSaveRequested> _requests = new List<RestSaveRequested>();
        readonly List<DevNotice> _notices = new List<DevNotice>();
        System.Action<RestSaveRequested> _onRequest;
        System.Action<DevNotice> _onNotice;

        IGameClock _previousClock;
        GameConfig _previousConfig;
        ISaveService _previousSave;
        string _previousRegion;
        GameConfig _config;

        BoatController _boat;
        BoatInterior _cabin;
        BoatBunk _bunk;

        [SetUp]
        public void SetUp()
        {
            _previousClock = GameServices.Clock;
            _previousConfig = GameServices.Config;
            _previousSave = GameServices.Save;
            _previousRegion = GameServices.CurrentRegionId;

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            GameServices.Clock = null;          // no morning to bring: the rule and the anchor are under test
            GameServices.Config = _config;
            GameServices.Save = null;
            GameServices.CurrentRegionId = null;
            GameServices.Helm.SetPlayersBoat(null);

            _requests.Clear();
            _notices.Clear();
            _onRequest = _requests.Add;
            _onNotice = _notices.Add;
            EventBus.Subscribe(_onRequest);
            EventBus.Subscribe(_onNotice);

            BuildRig();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onRequest);
            EventBus.Unsubscribe(_onNotice);
            GameServices.Helm.SetPlayersBoat(null);
            GameServices.Clock = _previousClock;
            GameServices.Config = _previousConfig;
            GameServices.Save = _previousSave;
            GameServices.CurrentRegionId = _previousRegion;

            // The installer test's cutaway subscribes in Configure, and its OnDestroy never runs in
            // EditMode: clear the channels it joined or the next press of TryEnter (this fixture's, or
            // the next fixture's) publishes into a destroyed hull. Same discipline as
            // EveryInteriorHullReachesItsDoorFromItsStandTests.Dismantle, plus the landing channel.
            EventBus.Clear<CabinEntered>();
            EventBus.Clear<CabinEntryLandingEntered>();
            EventBus.Clear<CabinLeft>();
            EventBus.Clear<ControlModeChanged>();
            Interactables.Clear();

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
            BoatInteriorCells.Reset();
        }

        // =====================================================================================
        //  the press
        // =====================================================================================

        [Test]
        public void OnYourOwnBoat_BelowAtTheBunk_TurnsYouIn_WithNoWakeSpot()
        {
            MakeHersAndSecure();
            Assert.IsTrue(_cabin.TryEnter(CuddyLevel));

            Assert.IsTrue(_bunk.IsAvailable, "below, on the berth's own sole, the bunk is offered");
            Assert.AreEqual(BoatBunk.Verb, _bunk.VerbLabel);
            Assert.AreEqual(InteractContext.OnDeck, _bunk.Contexts, "a cabin sole is walked OnDeck");

            _bunk.Interact(new InteractActor(new Vector2(0.4f, 1.1f), Vector2.up, InteractContext.OnDeck));

            Assert.AreEqual(1, _requests.Count, "one press, one rest");
            Assert.IsTrue(_requests[0].WakesAtSpawn, "a bunk records no wake spot: the save has no boat position");
            Assert.AreEqual(BoatBunk.PlaceName, _requests[0].Place);
            Assert.AreEqual(CuddyLevel, _requests[0].WakeLevel);
            Assert.AreEqual(0, _notices.Count, "no refusal line on a rest");
        }

        [Test]
        public void OnABoatThatIsNotYours_ItIsARefusal_NeverARest()
        {
            SecureHer();
            Assert.IsTrue(_cabin.TryEnter(CuddyLevel));

            Assert.AreEqual(BoatBunk.Refusal.NotYourBoat, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero));
            Assert.AreEqual(0, _requests.Count);
            Assert.AreEqual(1, _notices.Count);
            Assert.AreEqual(BoatBunk.NotYourBoatText, _notices[0].Text);
        }

        [Test]
        public void OutsideTheCabin_OrOnAnotherLevel_ItIsNotOffered_AndAPressIsRefused()
        {
            MakeHersAndSecure();

            Assert.IsFalse(_bunk.IsAvailable, "on deck, outside her cabin, there is no bunk to offer");
            Assert.AreEqual(BoatBunk.Refusal.NotBelow, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero));

            Assert.IsTrue(_cabin.TryEnter(HouseLevel));
            Assert.IsFalse(_bunk.IsAvailable, "in the house above the cuddy, the berth below is not in reach");
            Assert.AreEqual(BoatBunk.Refusal.NotBelow, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero));
            Assert.AreEqual(0, _requests.Count);
        }

        [Test]
        public void MidDeckWork_ItRefuses_TheHash193Guard_AndAllowsOnceTheDeckIsIdle()
        {
            MakeHersAndSecure();
            Assert.IsTrue(_cabin.TryEnter(CuddyLevel));

            _bunk.OnDeckLoopChanged(new DeckLoopStateChanged(
                new DeckLoopState(SternDeckStage.Baiting, 0.5f, 0, true)));
            Assert.AreEqual(BoatBunk.Refusal.DeckWork, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero));
            Assert.AreEqual(BoatBunk.DeckWorkText, _notices[0].Text);

            _bunk.OnDeckLoopChanged(new DeckLoopStateChanged(DeckLoopState.Idle));
            _bunk.OnTrapHaulChanged(new TrapHaulStateChanged(
                new TrapHaulState(TrapHaulPhase.Hauling, 0.3f, 0.4f, false)));
            Assert.AreEqual(BoatBunk.Refusal.DeckWork, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero),
                            "a pot on the way up is deck work too");

            _bunk.OnTrapHaulChanged(new TrapHaulStateChanged(TrapHaulState.Idle));
            Assert.AreEqual(BoatBunk.Refusal.None, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero));
            Assert.AreEqual(1, _requests.Count, "only the idle press rests");
        }

        [Test]
        public void FromAnythingButADeck_ItRefuses()
        {
            MakeHersAndSecure();
            Assert.IsTrue(_cabin.TryEnter(CuddyLevel));

            Assert.AreEqual(BoatBunk.Refusal.NotOnDeck, _bunk.TryTurnIn(InteractContext.OnFoot, Vector2.zero));
            Assert.AreEqual(BoatBunk.Refusal.NotOnDeck, _bunk.TryTurnIn(InteractContext.AtHelm, Vector2.zero));
            Assert.AreEqual(0, _requests.Count);
        }

        [Test]
        public void Adrift_WithTheSafetyRuleOn_ItIsNotSafeToTurnIn()
        {
            GameServices.Helm.SetPlayersBoat(_boat);   // hers, but nothing holding her
            Assert.IsTrue(_cabin.TryEnter(CuddyLevel));
            _config.BunkRequiresMooredOrAnchored = true;

            Assert.IsFalse(_bunk.IsSecured());
            Assert.AreEqual(BoatBunk.Refusal.NotSecured, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero));
            Assert.AreEqual(BoatBunk.NotSecuredText, _notices[0].Text);
            Assert.AreEqual(0, _requests.Count);
        }

        [Test]
        public void Adrift_WithTheSafetyRuleOff_SheLetsYouSleep()
        {
            GameServices.Helm.SetPlayersBoat(_boat);
            Assert.IsTrue(_cabin.TryEnter(CuddyLevel));
            _config.BunkRequiresMooredOrAnchored = false;

            Assert.AreEqual(BoatBunk.Refusal.None, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero));
            Assert.AreEqual(1, _requests.Count);
        }

        [Test]
        public void TheSafetyRule_DefaultsOn_InTheAssetAndWithNoConfig()
        {
            Assert.IsTrue(GameConfig.DefaultBunkRequiresMooredOrAnchored);
            Assert.IsTrue(ScriptableObject.CreateInstance<GameConfig>().BunkRequiresMooredOrAnchored);

            GameServices.Config = null;
            GameServices.Helm.SetPlayersBoat(_boat);
            Assert.IsTrue(_cabin.TryEnter(CuddyLevel));
            Assert.AreEqual(BoatBunk.Refusal.NotSecured, _bunk.TryTurnIn(InteractContext.OnDeck, Vector2.zero));
        }

        // =====================================================================================
        //  the save half
        // =====================================================================================

        [Test]
        public void ABunksRest_ForgetsTheAnchor_SoAReloadWakesAtTheSpawn()
        {
            var save = new FakeSave();
            GameServices.Save = save;
            GameServices.CurrentRegionId = Region;
            RestLocker.Stamp(save.Current, new RestAnchor(Region, new Vector2(3f, 4f), 1));   // last night's bed

            GameSaved outcome = RestSaveResponder.Fulfil(
                new RestSaveRequested(BoatBunk.PlaceName, new Vector2(40f, -12f), CuddyLevel, wakesAtSpawn: true));

            Assert.IsTrue(outcome.Ok, "the day is kept");
            Assert.AreEqual(1, save.Saves);
            Assert.IsFalse(RestLocker.Anchor(save.Current).IsSet,
                           "no anchor: the boat's position is not saved, so a spot aboard is a spot in the sea");
            Assert.IsFalse(RestSaveResponder.AnchorFor(
                               new RestSaveRequested("", Vector2.one, 0, true), Region).IsSet,
                           "…and the anchor such a request names is None, whatever region is live");
        }

        [Test]
        public void ABedsRest_StillStampsItsAnchor_ExactlyAsBefore()
        {
            var save = new FakeSave();
            GameServices.Save = save;
            GameServices.CurrentRegionId = Region;

            var request = new RestSaveRequested("your bed", new Vector2(3f, 4f), 1);
            Assert.IsFalse(request.WakesAtSpawn, "the three-argument bed request keeps its anchor");
            Assert.IsFalse(default(RestSaveRequested).WakesAtSpawn, "and so does a request built bare");

            RestSaveResponder.Fulfil(request);

            RestAnchor anchor = RestLocker.Anchor(save.Current);
            Assert.IsTrue(anchor.IsSet);
            Assert.AreEqual(Region, anchor.Region);
            Assert.AreEqual(new Vector2(3f, 4f), anchor.Position);
            Assert.AreEqual(1, anchor.Level);
        }

        // =====================================================================================
        //  the installer: one bunk per sleep anchor, at its reach point
        // =====================================================================================

        [Test]
        public void TheCapeIslander_GrowsOneBunk_AtHerSleepAnchor()
        {
            var interior = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(CapeInteriorPath);
            Assert.IsNotNull(interior, CapeInteriorPath);
            BoatInteriorAnchor[] sleeps = interior.Anchors.Where(a => a != null && a.Action == BoatBunk.SleepAction).ToArray();
            Assert.AreEqual(1, sleeps.Length, "premise: the cape islander's rig names one berth");

            BoatHullDef hull = AssetDatabase.FindAssets("t:BoatHullDef", new[] { "Assets/_Project/Data/Boats" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BoatHullDef>)
                .FirstOrDefault(h => h != null && h.Visual != null && h.Visual.Interior == interior);
            Assert.IsNotNull(hull, "a committed hull wears the cape islander's interior");

            var root = new GameObject("CapeBunkInstaller");
            _spawned.Add(root);
            var boat = root.AddComponent<BoatController>();
            boat.SetHull(hull);
            boat.enabled = false;
            var installer = root.AddComponent<BoatInteriorInstaller>();
            installer.Build();

            Assert.IsTrue(installer.Built);
            Assert.AreEqual(1, installer.Bunks.Count, "one bunk per sleep anchor, and only those");
            BoatBunk bunk = installer.Bunks[0];
            Assert.AreEqual(sleeps[0].ReachPoint, bunk.ReachPoint, "at the anchor's own reach point");
            Assert.AreSame(installer.Interior, bunk.Cabin, "in her own cabin");
            StringAssert.EndsWith("." + sleeps[0].Id, bunk.Id);
            Assert.IsNotNull(bunk.GetComponent<HullLocalAnchor>(), "hung off the hull frame as her doors are");
        }

        // =====================================================================================
        //  rig
        // =====================================================================================

        void MakeHersAndSecure()
        {
            GameServices.Helm.SetPlayersBoat(_boat);
            SecureHer();
        }

        /// <summary>A declared berth: the plainest "she is moored" the Boats lane has.</summary>
        void SecureHer()
        {
            if (_boat.GetComponent<MooredBoat>() == null) _boat.gameObject.AddComponent<MooredBoat>();
            Assert.IsTrue(_bunk.IsSecured(), "premise: a berthed boat is secured");
        }

        void BuildRig()
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            def.Id = "interior.bunk_test";
            def.PixelsPerMetre = 32;
            def.Levels = new[]
            {
                Sole("house_sole", HouseSoleZ),
                Sole("cuddy_sole", CuddySoleZ),
            };
            _spawned.Add(def);

            var root = new GameObject("BunkTestHull");
            _spawned.Add(root);
            _boat = root.AddComponent<BoatController>();   // + Rigidbody2D, collider, BoatMooring
            _boat.enabled = false;

            var exterior = new GameObject("House").AddComponent<SpriteRenderer>();
            exterior.transform.SetParent(root.transform, false);
            var room = new GameObject("Room").AddComponent<SpriteRenderer>();
            room.transform.SetParent(root.transform, false);

            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var cells = new Sprite[8];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                _spawned.Add(cells[i]);
            }

            _cabin = root.AddComponent<BoatInterior>();
            _cabin.Configure(def, exterior, room, null, room.transform, root.transform, cells, 8, true, 0f,
                             0f, 0f, 0f);

            var bunkGo = new GameObject("BoatBunk.bunk");
            bunkGo.transform.SetParent(root.transform, false);
            _bunk = bunkGo.AddComponent<BoatBunk>();
            _bunk.Configure(_cabin, _boat, "fixture.boat.interior.bunk_test.bunk", BunkReach);

            Assert.AreEqual(CuddyLevel, _cabin.LevelIndexAtHeight(BunkReach.z),
                            "premise: the berth's height reads as the cuddy");
        }

        static BoatInteriorLevel Sole(string id, float z) => new BoatInteriorLevel
        {
            Id = id,
            SoleZMeters = z,
            Outline = new[]
            {
                new Vector2(-1.2f, -2f), new Vector2(1.2f, -2f), new Vector2(1.2f, 3.5f), new Vector2(-1.2f, 3.5f),
            },
        };
    }
}
