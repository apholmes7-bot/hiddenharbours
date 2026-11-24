using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The broken dory in the loaded St Peters scene: she lies on the slip, a game that has her shows her
    /// and keeps the sailing boat out of the world, the ground under her is dry at the first week's highest
    /// water, a person can stand within reach of her with nothing solid in the way, and the press buys her
    /// once through the live services.
    ///
    /// <para><b>The save is a memory save.</b> The test swaps the loaded save for one held in memory after
    /// the load, so nothing it does is written to any save file on disk. The walk from Ginny's door over
    /// the terrain is the EditMode sibling's (<c>SlipDoryPlacementTests</c>); this one adds the colliders.</para>
    /// </summary>
    public class SlipDoryPlayTests
    {
        private const string SceneName  = "StPeters";
        private const string RegionPath = "Assets/_Project/Data/Regions/StPeters.asset";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        /// <summary>The first week, scanned, and the scan step in game seconds (as the EditMode sibling).</summary>
        private const int WeekDays = 8;
        private const double ScanStepSeconds = 5.0;
        /// <summary>A person's footprint for the "nothing solid in the way" check, metres.</summary>
        private const float PersonRadius = 0.3f;
        /// <summary>How far inside her reach the standing points are taken, metres.</summary>
        private const float ReachInset = 0.4f;
        private const int StandingPoints = 24;

        private readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        /// <summary>Whether the sailing boat stood in the world as the scene loaded, before the test's game.</summary>
        private bool _boatShownAtLoad;

        private sealed class MemorySave : ISaveService
        {
            public MemorySave(SaveData data) { Current = data; }
            public SaveData Current { get; private set; }
            public int Writes;
            public bool GetFlag(string key)
            {
                if (Current?.OnboardingFlags == null) return false;
                foreach (var f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key) { Current.OnboardingFlags[i] = new SaveFlag(key, value); return; }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() => Writes++;
            public void BeginNewGame() => Current = SaveMigration.NewGame();
        }

        private sealed class Purse : IWallet
        {
            public Purse(int money) { Money = money; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        private static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }

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
            GameServices.PendingArrivalKey = null;
            var clean = SceneManager.CreateScene("SlipDoryCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return HiddenHarbours.Tests.Support.SceneSettle.Unload(s);
            }
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go))
                    Object.DestroyImmediate(go);
            GameServices.Reset();
            yield return null;
        }

        /// <summary>Loads the island, then puts a game in memory in place of the loaded save and tells the
        /// scene a game has loaded, as the title page does.</summary>
        private IEnumerator LoadTheIslandWith(MemorySave save, IWallet purse)
        {
            LogAssert.ignoreFailingMessages = true;
            yield return HiddenHarbours.Tests.Support.SceneSettle.Load(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;
            _boatShownAtLoad = PersistentBoat().gameObject.activeSelf;
            GameServices.Save = save;
            GameServices.Wallet = purse;
            EventBus.Publish(new GameLoaded());
            for (int i = 0; i < 2; i++) yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        private static MemorySave NewGameOnTheSlip()
        {
            var save = new MemorySave(SaveMigration.NewGame());
            SlipDoryStory.BeginForNewGame(save);
            return save;
        }

        private static SlipDory TheSlipDory()
        {
            SlipDory[] all = Object.FindObjectsByType<SlipDory>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(d => d.gameObject.scene.name == SceneName).ToArray();
            Assert.AreEqual(1, all.Length, "the St Peters scene must hold exactly one slip dory");
            return all[0];
        }

        private static GameObject BodyOf(SlipDory dory) =>
            dory.GetComponentInChildren<SpriteRenderer>(true).gameObject;

        private static Transform PersistentBoat()
        {
            var coordinator = Object.FindFirstObjectByType<RegionTravelCoordinator>(FindObjectsInactive.Include);
            Assert.IsNotNull(coordinator, "the St Peters scene has no travel coordinator");
            FieldInfo f = typeof(RegionTravelCoordinator).GetField("_boat", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "the coordinator's boat field moved; update this test");
            var boat = f.GetValue(coordinator) as Transform;
            Assert.IsNotNull(boat, "the coordinator has no boat wired in the St Peters scene");
            return boat;
        }

        private static float FirstWeekHighWater()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(RegionPath);
            Assert.IsNotNull(config, ConfigPath + " is missing");
            Assert.IsNotNull(region, RegionPath + " is missing");
            var profile = new TideProfile
            {
                MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours,
            };
            float high = float.MinValue;
            for (double t = 0; t <= config.SecondsPerDay * WeekDays; t += ScanStepSeconds)
                high = Mathf.Max(high, TideModel.Height(t, profile, config));
            return high;
        }

        [UnityTest]
        public IEnumerator ANewGame_ShowsHerOnTheSlip_AndKeepsTheSailingBoatOut()
        {
            yield return LoadTheIslandWith(NewGameOnTheSlip(), new Purse(0));

            SlipDory dory = TheSlipDory();
            Vector2 at = dory.transform.position;
            Assert.That(Mathf.Abs(at.y - StPetersBuilder.BerthTo.y), Is.LessThanOrEqualTo(StPetersBuilder.BerthHalfWidth),
                $"she lies at {at}, off the slip");
            Assert.That(at.x, Is.LessThanOrEqualTo(StPetersBuilder.BerthTo.x), $"she lies at {at}, past the slip's head");

            Assert.IsTrue(dory.IsShown && BodyOf(dory).activeInHierarchy, "a new game shows her on the slip");
            Assert.IsTrue(dory.IsAvailable, "and she can be pressed");
            Assert.IsFalse(PersistentBoat().gameObject.activeSelf,
                "a new game keeps the sailing boat out of the world: he has no usable boat yet");
        }

        [UnityTest]
        public IEnumerator AnOldGame_SeesNoDoryOnTheSlip_AndKeepsItsBoat()
        {
            var save = new MemorySave(SaveMigration.NewGame());   // a game that never set the story
            yield return LoadTheIslandWith(save, new Purse(1000));

            SlipDory dory = TheSlipDory();
            Assert.IsFalse(dory.IsShown || BodyOf(dory).activeSelf, "an old game must see no second dory");
            Assert.IsFalse(dory.IsAvailable, "and no sale");
            Assert.AreEqual(_boatShownAtLoad, PersistentBoat().gameObject.activeSelf,
                "an old game's boat must be left exactly as the scene placed it");
        }

        [UnityTest]
        public IEnumerator SheIsDry_AtTheFirstWeeksHighWater_AndCanBeReachedOnFoot()
        {
            yield return LoadTheIslandWith(NewGameOnTheSlip(), new Purse(0));
            SlipDory dory = TheSlipDory();
            ITidalTerrain terrain = GameServices.TidalTerrain;
            Assert.IsNotNull(terrain, "the loaded island registered no tidal terrain");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            float high = FirstWeekHighWater();

            var box = BodyOf(dory).GetComponent<BoxCollider2D>();
            Assert.IsNotNull(box, "her body has no collider");
            Bounds b = box.bounds;
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                var p = new Vector2(b.center.x + i * b.extents.x, b.center.y + j * b.extents.y);
                Assert.Greater(terrain.ElevationAt(p), high,
                    $"the ground under her at {p} is under the first week's high water ({high:F2} m)");
            }

            // Somewhere within her reach a person can stand at that water with nothing solid in the way.
            Physics2D.SyncTransforms();
            float r = dory.ReachMeters - ReachInset;
            int standable = 0;
            for (int k = 0; k < StandingPoints; k++)
            {
                float a = k * Mathf.PI * 2f / StandingPoints;
                Vector2 p = dory.WorldPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!TidalExposure.IsWalkable(high, terrain.ElevationAt(p), config.WadeDepth)) continue;
                bool blocked = Physics2D.OverlapCircleAll(p, PersonRadius).Any(c => c != null && !c.isTrigger && c.enabled);
                if (!blocked) standable++;
            }
            Assert.Greater(standable, 0,
                $"no point within {dory.ReachMeters} m of her is dry and clear of colliders at {high:F2} m water");
        }

        [UnityTest]
        public IEnumerator ThePress_BuysHer_Once_ThroughTheLiveServices()
        {
            var save = NewGameOnTheSlip();
            SlipBoatSaleDef sale = null;
            var purse = new Purse(0);
            yield return LoadTheIslandWith(save, purse);
            SlipDory dory = TheSlipDory();
            sale = dory.Sale;
            Assert.IsNotNull(sale, "the scene's slip dory carries no sale");

            // The loaded game may hand the purse its starting money, so the checks are relative to it.
            if (purse.Money < sale.Price)
            {
                int short_ = purse.Money;
                Assert.AreEqual(SlipBoatSaleResult.TooDear, dory.Press(), "a short purse is refused");
                Assert.AreEqual(short_, purse.Money, "a refused sale spends nothing");
            }
            Assert.AreEqual(0, save.Current.OwnedBoats.Count(b => b == sale.BoatId));

            purse.Add(sale.Price);
            int before = purse.Money;
            Assert.AreEqual(SlipBoatSaleResult.Bought, dory.Press());
            Assert.AreEqual(before - sale.Price, purse.Money, "the purse drops by exactly her price");
            Assert.AreEqual(1, save.Current.OwnedBoats.Count(b => b == sale.BoatId), "she is his, once");
            Assert.AreEqual(sale.WithPrice(sale.OwnedLabel), dory.VerbLabel);

            int after = purse.Money;
            Assert.AreEqual(SlipBoatSaleResult.AlreadyHis, dory.Press(), "a second press does nothing");
            Assert.AreEqual(after, purse.Money);
            Assert.AreEqual(1, save.Current.OwnedBoats.Count(b => b == sale.BoatId));

            yield return null;
            Assert.IsTrue(dory.IsShown && BodyOf(dory).activeInHierarchy, "bought, she still lies on the slip");
            Assert.IsFalse(PersistentBoat().gameObject.activeSelf, "and she cannot be launched: no boat comes");
        }
    }
}
