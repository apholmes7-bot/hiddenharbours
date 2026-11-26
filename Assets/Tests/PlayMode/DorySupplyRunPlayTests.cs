using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The dory's supplies, through real conversations over real frames: the shipped people, plus the two
    /// keepers (their own Defs when they ship, a stand-in keyed to the same id while they do not), a real
    /// <see cref="WorldInteractor"/> and <see cref="DialoguePresenter"/>, and the press, the lines and the
    /// rows driven exactly as a press drives them. Then the real mending station mends her from what they
    /// gave. Headless-safe: nothing renders or reads pixels.
    /// </summary>
    public class DorySupplyRunPlayTests
    {
        const string NpcFolder = "Assets/_Project/Data/NPCs/";
        const string RunAsset = "Assets/_Project/Data/Resources/NpcSupplies/SupplyRunStPetersDory.asset";
        const string StationAsset = "Assets/_Project/Data/Regions/StPetersVillage/slipmend.basils_dory.asset";
        const string ConfigAsset = "Assets/_Project/Data/Config/GameConfig.asset";
        const string RegionAsset = "Assets/_Project/Data/Regions/StPeters.asset";
        const int BagCapacity = 20;
        const float Reach = 1.8f;
        const int MaxSteps = 200;

        // ---- fakes ---------------------------------------------------------------------------------

        sealed class MemorySave : ISaveService
        {
            public MemorySave(SaveData data) { Current = data; }
            public SaveData Current { get; set; }
            public bool GetFlag(string key)
            {
                foreach (var f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key) { Current.OnboardingFlags[i] = new SaveFlag(key, value); return; }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() { }
        }

        sealed class BagHands : IShoppingBagHands
        {
            public ShoppingBagRefusal Refuse = ShoppingBagRefusal.None;
            public bool HoldsBag => true;
            public bool BagIsSetDown => false;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag) => Refuse;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId)
            {
                if (Refuse != ShoppingBagRefusal.None) return Refuse;
                return ShoppingBagContents.TryPut(GameServices.Save.Current, supplyId, BagCapacity)
                    ? ShoppingBagRefusal.None : ShoppingBagRefusal.BagFull;
            }
            public bool TryPutAway(string storageId) => ShoppingBagContents.Release(GameServices.Save.Current) >= 0;
        }

        sealed class NoHands : IShoppingBagHands
        {
            public bool HoldsBag => false;
            public bool BagIsSetDown => false;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag) => ShoppingBagRefusal.HandsFull;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId) => ShoppingBagRefusal.HandsFull;
            public bool TryPutAway(string storageId) => false;
        }

        sealed class Clock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => default;
            public Season Season => default;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => 0;
            public float DayFraction => 0;
            public bool IsPaused { get; set; }
            public bool IsHeld { get; set; }
            public float TimeScale { get; set; } = 1;
            public bool FastForwardTo(double target)
            {
                if (IsHeld || IsPaused || target <= TotalSeconds) return false;
                TotalSeconds = target;
                return true;
            }
        }

        sealed class Tide : IEnvironmentService
        {
            public GameConfig Config;
            public int WorldSeed { get; set; }
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double seconds) => TideModel.Height(seconds, ActiveTideProfile, Config);
        }

        // ---- rig -----------------------------------------------------------------------------------

        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        readonly Dictionary<string, Interactable> _people = new Dictionary<string, Interactable>();
        MemorySave _save;
        BagHands _hands;
        SupplyRunDef _run;
        DialoguePresenter _presenter;
        WorldInteractor _interactor;
        Transform _player;

        static readonly FieldInfo OptionsField =
            typeof(DialoguePresenter).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly string[] Cast =
        {
            "BasilSamson", "MargueriteLeBlanc", "JuniorPoirier", "EileenDoiron", "AuntGinny", "RoseMacIsaac",
        };

        // The two keepers live with the lighthouse and the Whelps cottage, which ship in their own change.
        // Keyed by id only: the shipped Def is used when it exists, a stand-in with the same id otherwise.
        static readonly string[] Keepers = { "npc.alban_doucet", "npc.odile_marchand" };

        NpcDef Keeper(string id)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:NpcDef", new[] { NpcFolder.TrimEnd('/') }))
            {
                var shipped = AssetDatabase.LoadAssetAtPath<NpcDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (shipped != null && shipped.Id == id) return shipped;
            }
            var talk = ScriptableObject.CreateInstance<DialogueDef>();
            talk.Id = "dialogue.stand_in";
            talk.FirstLines = new[] { "Afternoon.", "Fine day for it." };
            var npc = ScriptableObject.CreateInstance<NpcDef>();
            npc.Id = id;
            npc.DisplayName = "Keeper";
            npc.Kind = InteractKind.Talk;
            npc.Dialogue = talk;
            _spawned.Add(talk);
            _spawned.Add(npc);
            return npc;
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            InteractActorProbe.Clear();
            ShoppingBagContents.Clear();
            SlipDoryStory.IsHost = null;
            SupplyRunTalk.RunsSource = null;
            SupplyRunTalk.BagLookup = null;

            Spawn("AudioListener").AddComponent<AudioListener>();
            var camGo = Spawn("TestCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            _run = AssetDatabase.LoadAssetAtPath<SupplyRunDef>(RunAsset);
            Assert.IsNotNull(_run, RunAsset);

            _save = new MemorySave(SaveMigration.NewGame());
            SlipDoryStory.BeginForNewGame(_save);
            _hands = new BagHands();
            GameServices.Save = _save;
            GameServices.BagHands = _hands;

            // Everybody far enough apart that only the one stood beside is in reach.
            var talkPoints = new List<Interactable>();
            for (int i = 0; i < Cast.Length + Keepers.Length; i++)
            {
                var npc = i < Cast.Length
                    ? AssetDatabase.LoadAssetAtPath<NpcDef>(NpcFolder + Cast[i] + ".asset")
                    : Keeper(Keepers[i - Cast.Length]);
                Assert.IsNotNull(npc, (i < Cast.Length ? Cast[i] : Keepers[i - Cast.Length]) + " has no NpcDef");
                Assert.IsTrue(npc.HasDialogue, npc.Id + " says nothing");
                var go = Spawn(npc.Id);
                go.transform.position = new Vector3(i * 20f, 0f, 0f);
                var it = go.AddComponent<Interactable>();
                it.Configure(npc);
                talkPoints.Add(it);
                _people[npc.Id] = it;
            }

            _player = Spawn("Player").transform;
            _presenter = Spawn("DialoguePresenter").AddComponent<DialoguePresenter>();
            _interactor = Spawn("WorldInteractor").AddComponent<WorldInteractor>();
            _interactor.Configure(_player, _presenter, talkPoints.ToArray(), Reach);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.Destroy(o);
            _spawned.Clear();
            _people.Clear();
            ShoppingBagContents.Clear();
            SlipDoryStory.IsHost = null;
            SupplyRunTalk.RunsSource = null;
            SupplyRunTalk.BagLookup = null;
            InteractionGate.Reset();
            InteractActorProbe.Clear();
            GameServices.Reset();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        void Own() => _save.Current.OwnedBoats.Add(_run.BoatId);
        bool Flag(string key) => _save.GetFlag(key);
        SupplyGiftDef GiftOf(string giver) => _run.Gifts.Single(g => g.GiverNpcId == giver);

        List<string> RowIds()
        {
            var rows = OptionsField.GetValue(_presenter) as IReadOnlyList<DialogueOption>;
            return rows == null ? new List<string>() : rows.Select(r => r.Id).ToList();
        }

        /// <summary>
        /// Walk up to <paramref name="npcId"/>, press, play the lines through to the rows (or the end),
        /// pick the row <paramref name="optionId"/> (the close row when null), and play the answer out.
        /// Returns the row ids that were offered.
        /// </summary>
        IEnumerator Talk(string npcId, string optionId, List<string> offered)
        {
            Interactable who = _people[npcId];
            _player.position = who.transform.position + new Vector3(1f, 0f, 0f);
            yield return Settle();
            Assert.IsTrue(_interactor.BeginInteract(), "nobody in reach of " + npcId);
            yield return Settle();

            int steps = 0;
            while (_presenter.IsShowing && !_presenter.IsChoosing && steps++ < MaxSteps)
            {
                _presenter.Advance();
                yield return null;
            }
            offered?.AddRange(RowIds());
            if (_presenter.IsChoosing)
            {
                List<string> ids = RowIds();
                int target = ids.IndexOf(optionId ?? DialogueOption.CloseId);
                Assert.GreaterOrEqual(target, 0, npcId + " does not offer " + (optionId ?? "close") + ": " + string.Join(", ", ids));
                while (_presenter.SelectedOption != target && steps++ < MaxSteps)
                {
                    _presenter.MoveSelection(-1f);
                    yield return null;
                    _presenter.MoveSelection(0f);
                    yield return null;
                }
                _presenter.Advance();     // confirm
                yield return null;
                while (_presenter.IsShowing && steps++ < MaxSteps)
                {
                    _presenter.Advance();
                    yield return null;
                }
            }
            else Assert.IsNull(optionId, npcId + " offered no rows at all");
            Assert.IsFalse(_presenter.IsShowing, "the conversation with " + npcId + " never ended");
            yield return Settle();
        }

        // ---- the clue trail -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator NoClueBeforeBasil_ThenBasilNames_ThenFourTalksRoundUpTheClues()
        {
            Own();
            var offered = new List<string>();
            yield return Talk("npc.marguerite_leblanc", null, offered);
            CollectionAssert.DoesNotContain(offered, _run.Topic.OptionId, "no question before Basil has named the makings");
            offered.Clear();
            yield return Talk("npc.rose_macisaac", null, offered);
            CollectionAssert.DoesNotContain(offered, GiftOf("npc.rose_macisaac").OptionId, "nor the asking");

            yield return Talk("npc.basil_samson", null, null);
            Assert.IsTrue(Flag(_run.NamedFlag), "Basil has told him what she wants");

            foreach (string npc in new[] { "npc.marguerite_leblanc", "npc.junior_poirier", "npc.eileen_doiron", "npc.rose_macisaac" })
                yield return Talk(npc, _run.Topic.OptionId, null);

            Assert.IsTrue(Flag("st_peters.dory_clue.oakum.3"), "Rose is named");
            Assert.IsTrue(Flag("st_peters.dory_clue.paint.2"), "the light is pointed at");
            Assert.IsTrue(Flag("st_peters.dory_clue.plank.3"), "the Whelps cottage is named");
            Assert.AreEqual(0, SupplyLocker.Owned(_save.Current, "supply.oakum"), "asking about is not being given");
        }

        [UnityTest]
        public IEnumerator TalkAndGoodbye_GivesNothing_AndAFullHandRefusalChangesNothing()
        {
            Own();
            _save.SetFlag(_run.NamedFlag, true);
            var offered = new List<string>();
            yield return Talk("npc.rose_macisaac", null, offered);
            CollectionAssert.Contains(offered, GiftOf("npc.rose_macisaac").OptionId);
            Assert.AreEqual(0, SupplyLocker.Owned(_save.Current, "supply.oakum"), "goodbye gives nothing");

            GameServices.BagHands = new NoHands();
            string before = JsonUtility.ToJson(_save.Current);
            yield return Talk("npc.rose_macisaac", GiftOf("npc.rose_macisaac").OptionId, null);
            Assert.AreEqual(before, JsonUtility.ToJson(_save.Current), "hands full: nothing changes");
        }

        // ---- an unprompted round, and the mend -------------------------------------------------------

        [UnityTest]
        public IEnumerator AnUnpromptedRound_GetsEverything_AndTheStationMendsHerFromWhatIsPutAway()
        {
            Own();
            _save.SetFlag(_run.NamedFlag, true);
            foreach (var gift in _run.Gifts)
                yield return Talk(gift.GiverNpcId, gift.OptionId, null);

            foreach (var gift in _run.Gifts)
                foreach (string id in gift.ItemIds)
                {
                    Assert.AreEqual(1, SupplyLocker.Owned(_save.Current, id), id + ": exactly one");
                    Assert.AreEqual(1, ShoppingBagContents.InBag(_save.Current, id), id + " is in the bag");
                }

            var offered = new List<string>();
            yield return Talk("npc.rose_macisaac", null, offered);
            CollectionAssert.DoesNotContain(offered, GiftOf("npc.rose_macisaac").OptionId, "once given, not offered again");
            CollectionAssert.DoesNotContain(offered, _run.Topic.OptionId, "all in hand: nothing to ask about");

            // Home, the shopping put away, the bag out of his hand: the slip still has what it needs.
            Assert.IsTrue(_hands.TryPutAway("storage.home"));
            Assert.AreEqual(0, ShoppingBagContents.Total(_save.Current));

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAsset);
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(RegionAsset);
            var stationDef = AssetDatabase.LoadAssetAtPath<DoryMendingStationDef>(StationAsset);
            Assert.IsNotNull(config); Assert.IsNotNull(region); Assert.IsNotNull(stationDef);
            GameServices.Config = config;
            GameServices.Clock = new Clock { TotalSeconds = config.SecondsPerDay * 0.375 };
            GameServices.Environment = new Tide
            {
                Config = config,
                ActiveTideProfile = new TideProfile
                {
                    MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours,
                },
            };
            GameServices.CurrentRegionId = stationDef.Mending.PlaceId;

            var go = Spawn("SlipDory_SupplyRunTest");
            go.SetActive(false);
            var station = go.AddComponent<DoryMendingStation>();
            station.Configure(stationDef);
            go.SetActive(true);
            yield return null;

            var noBag = new NoHands();
            for (int j = 0; j < stationDef.Mending.Jobs.Length; j++)
            {
                Assert.AreEqual(DoryMendingStationResult.Working, station.Press(null, noBag), station.LastReason.TextKey());
                int ticks = 0;
                while (station.IsWorking && ticks++ < 10000) station.TickHeld(true, true, 0.25f);
            }
            CollectionAssert.Contains(_save.Current.RepairedBoats, _run.BoatId, "mended from supplies given by people");
            yield return Settle();

            var after = new List<string>();
            yield return Talk("npc.odile_marchand", null, after);
            CollectionAssert.DoesNotContain(after, GiftOf("npc.odile_marchand").OptionId, "mended: the run is over");
        }
    }
}
