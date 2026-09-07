using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE EAST DOOR IS IN THE COMMITTED SCENE.</b> <c>StPetersEastDoorTests</c> holds the
    /// arithmetic of where the door belongs; this file holds the only question that arithmetic cannot
    /// answer — whether the door is actually THERE, in <c>StPeters.unity</c>, wired.
    ///
    /// <para><b>Why that is a separate claim.</b> A committed scene here is largely builder output, and
    /// <c>StPetersBuilder.Build()</c> cannot be re-run: it wipes the hand-authored layer and the escape
    /// hatch its own guard names does not exist for this region. So the door was added to the committed
    /// scene BY HAND, with the builder taught the same wiring. That is exactly the shape the
    /// <c>scene-wired is not builder-wired</c> lesson (#323) warns about, and the guard against it is
    /// this: read the door out of the LOADED scene and hold it against the builder's own constants.</para>
    ///
    /// <para><b>⚠⚠ AND THIS FIXTURE HAS TO CLEAN UP AFTER A SCENE THAT OUTLIVES ITS OWN UNLOAD.</b>
    /// Measured on CI run 34081721484, where this file's first version turned four tests RED in three
    /// other classes, every one of them running after this one. <c>StPeters.unity</c> carries <b>eight</b>
    /// <see cref="PersistentObject"/> roots, and <c>PersistentObject.Awake</c> is
    /// <c>DontDestroyOnLoad(gameObject)</c> — which promotes them OUT of the scene. Unloading the scene
    /// therefore does not remove them: GameRoot, the services root, the ControlSwitcher, the
    /// RegionSceneLoader and the <c>RegionTravelCoordinator</c> stay resident for the rest of the run.
    /// A leftover coordinator is still subscribed to <c>activeSceneChanged</c> and eats
    /// <c>GameServices.ConsumePendingArrivalKey()</c> out from under the NEXT fixture's travel, which is
    /// how <c>WestWaterSailPlayTests</c> came to land a boat at its default arrival instead of its named
    /// one. <b>Unloading a region scene is not cleanup.</b> So this fixture records what was resident
    /// before it ran and destroys whatever the load added, and <see cref="TheFixtureLeavesNothingResident"/>
    /// is the guard that it keeps doing so.</para>
    ///
    /// <para><b>No graphics needed.</b> Nothing here renders, so it runs on CI, which has no GPU — and
    /// that matters: the three other classes that load a region this way are all skipped without a
    /// graphics device, so on CI this is the first fixture in the run to do it.</para>
    /// </summary>
    public class StPetersEastDoorPlayTests
    {
        private const string SceneName = "StPeters";
        private const string DoorName = "PassageToEastWater";

        private readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();

        // ---- the persistent (DontDestroyOnLoad) scene, and who is in it -------------------------

        /// <summary>The DontDestroyOnLoad scene, reached the only way Unity offers: put something in it
        /// and ask what scene it landed in.</summary>
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
            // Whatever is already resident is somebody else's and must survive us.
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            GameServices.PendingArrivalKey = null;

            // ⚠️ PUT THE WORLD BACK — and the scene is only half of it.
            var clean = SceneManager.CreateScene("EastDoorCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            // ⭐ …and the other half: everything the load promoted out of that scene. Destroyed by
            // identity against the pre-existing set, so a fixture that ran before us keeps its own.
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go))
                    Object.DestroyImmediate(go);

            // The resident core registered services and a region id while it lived; with it gone those
            // references are stale, and the next fixture builds its own.
            GameServices.Reset();
            yield return null;
        }

        private IEnumerator LoadTheIsland()
        {
            // The island logs unrelated decor complaints; a stray one must not fail a claim about a passage.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;   // let self-installing components register
            LogAssert.ignoreFailingMessages = false;
        }

        private static RegionPassage EastDoor() =>
            Object.FindObjectsByType<RegionPassage>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .FirstOrDefault(p => p.gameObject.name == DoorName);

        // =============================================================================================
        //  the door itself
        // =============================================================================================

        [UnityTest]
        public IEnumerator TheCommittedSceneCarriesTheEastDoor_WiredToTheEastWater()
        {
            yield return LoadTheIsland();

            RegionPassage door = EastDoor();
            Assert.IsNotNull(door,
                $"the committed {SceneName}.unity has no '{DoorName}' — the island's east wall is shut, " +
                "and the opening has nowhere to sail in from");

            Assert.IsNotNull(door.Target, "the east door names no target region");
            Assert.AreEqual(EastWaterPlan.RegionId, door.Target.Id,
                "the east door must lead to the east water and nowhere else");
            Assert.AreEqual(EastWaterPlan.SceneName, door.Target.SceneName,
                "…and the def it points at must promise the scene the loader will ask for");

            Assert.AreEqual(EastWaterPlan.FromStPetersArrivalKey, door.ArrivalKey,
                "the door must name the arrival it lands at, or a second door on the east water would " +
                "one day put this boat at the wrong end of the run she just started");
        }

        [UnityTest]
        public IEnumerator TheDoorInTheSceneStandsWhereTheBuilderSaysItDoes()
        {
            yield return LoadTheIsland();

            RegionPassage door = EastDoor();
            Assert.IsNotNull(door, "no east door in the committed scene");

            // ⭐ THE SCENE-WIRED vs BUILDER-WIRED GUARD. The scene copy was placed by hand because this
            // region cannot be rebuilt; the builder was taught the same numbers so a future rebuild
            // agrees. Nothing but this holds the two together.
            Vector3 expected = StPetersBuilder.ToEastWaterPassagePos;
            Assert.AreEqual(expected.x, door.transform.position.x, 1e-3f,
                "the placed door has drifted from the builder's own constant in X");
            Assert.AreEqual(expected.y, door.transform.position.y, 1e-3f,
                "the placed door has drifted from the builder's own constant in Y");

            var box = door.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(box, "a passage without a trigger collider is a door nothing can open");
            Assert.IsTrue(box.isTrigger, "the band must be a TRIGGER — a solid one would fend the boat off");
            Assert.AreEqual(StPetersBuilder.EastWaterPassageBandSize.x, box.size.x, 1e-3f,
                "the band's depth decides whether a hull can cross it between two physics steps");
            Assert.AreEqual(StPetersBuilder.EastWaterPassageBandSize.y, box.size.y, 1e-3f,
                "the band's height decides whether the crossing can be missed");
            Assert.AreEqual(Vector2.zero, box.offset,
                "the collider must sit on its own transform, or the band is not where the door is");
        }

        // =============================================================================================
        //  the way back in
        // =============================================================================================

        [UnityTest]
        public IEnumerator TheIslandAnswersTheKeyABoatComesHomeWith()
        {
            yield return LoadTheIsland();

            RegionAnchor anchor = Object.FindObjectsByType<RegionAnchor>(FindObjectsInactive.Include,
                                                                        FindObjectsSortMode.None)
                                        .FirstOrDefault(a => a.RegionId == "region.st_peters");
            Assert.IsNotNull(anchor, "St Peters has no region anchor");

            string key = StPetersBuilder.FromEastWaterArrivalKey;
            Assert.IsTrue(anchor.HasArrival(key),
                $"the island authors no arrival named '{key}' — a boat coming home from the east water " +
                "would land at the island's default point, which is the berth alongside the wharf: she " +
                "would appear tied up with the whole passage in unsailed");

            Transform landing = anchor.ArrivalPointFor(key);
            Assert.IsNotNull(landing);
            Assert.AreEqual(StPetersBuilder.FromEastWaterArrivalPos.x, landing.position.x, 1e-3f,
                "the east arrival in the scene has drifted from the builder's constant in X");
            Assert.AreEqual(StPetersBuilder.FromEastWaterArrivalPos.y, landing.position.y, 1e-3f,
                "the east arrival in the scene has drifted from the builder's constant in Y");

            // ⚠ AND THE STEP ASHORE MUST NOT FOLLOW THE WAY YOU CAME IN.
            Assert.AreSame(anchor.DisembarkPoint, anchor.DisembarkPointFor(key),
                "arriving from the east must not move where the player steps ashore — that is a fact " +
                "about the DOCK, not about the way in");
        }

        [UnityTest]
        public IEnumerator TheLoaderKnowsTheEastWater_OrTheDoorCouldNotResolveIt()
        {
            yield return LoadTheIsland();

            RegionSceneLoader loader =
                Object.FindObjectsByType<RegionSceneLoader>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None).FirstOrDefault();
            Assert.IsNotNull(loader, "no region scene loader in the persistent core");
            Assert.IsTrue(loader.Registry.Contains(EastWaterPlan.RegionId),
                "the loader's region list does not include the east water, so nothing could travel " +
                "there by id even with the door wired");

            Assert.IsTrue(loader.Registry.Contains("region.nine_mile_creek"));
            Assert.IsTrue(loader.Registry.Contains("region.st_peters"));
        }

        // =============================================================================================
        //  ⭐ safe to sail into before the region on the far side exists — on a rig of its own
        // =============================================================================================

        [UnityTest]
        public IEnumerator SailingIntoARegionWhoseSceneIsNotBuilt_DeclinesCleanly_AndGivesTheKeyBack()
        {
            // ⭐ THIS TEST EARNED ITS KEEP ON ITS FIRST RUN. It failed — because
            // SceneManager.LoadSceneAsync logs an ENGINE ERROR of its own for a scene that is not in the
            // build profile, and only THEN hands back null. The decline worked; it was just buried under
            // an error the loader could not suppress, so every boat that crossed this seam before the
            // east water existed would have reported a fault. RegionSceneLoader now asks
            // Application.CanStreamedLevelBeLoaded FIRST. LogAssert failing on an unexpected error is
            // what found that and is what keeps it fixed — so there is deliberately no
            // LogAssert.Expect for an error here.
            //
            // ⚠ Built on a SYNTHETIC RIG rather than by loading St Peters. This is a claim about
            // RegionSceneLoader, not about the island, and loading a region to make it would drag the
            // persistent core into the rest of the run for nothing (see the class note).
            var go = new GameObject("EastDoorLoaderRig");
            var loader = go.AddComponent<RegionSceneLoader>();
            var def = ScriptableObject.CreateInstance<RegionDef>();
            try
            {
                def.Id = EastWaterPlan.RegionId;
                def.SceneName = EastWaterPlan.SceneName;

                if (Application.CanStreamedLevelBeLoaded(def.SceneName))
                    Assert.Pass("the east water scene has landed and is in Build Settings — the door " +
                                "now leads somewhere, so the decline path this guards is unreachable");

                GameServices.PendingArrivalKey = EastWaterPlan.FromStPetersArrivalKey;
                LogAssert.Expect(LogType.Warning,
                                 new Regex("Could not load scene '" + EastWaterPlan.SceneName + "'"));

                bool began = loader.Travel(def);
                yield return null;

                Assert.IsFalse(began,
                    "a region whose scene is not in the build profile must be declined, not attempted");

                // The passage's own contract: a declined travel raises no activeSceneChanged, so nobody
                // consumes the key — RegionPassage.Activate takes it back. Mirrored here because the rig
                // calls Travel directly rather than through a passage.
                GameServices.PendingArrivalKey = null;
                Assert.IsNull(GameServices.PendingArrivalKey);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(def);
            }
        }

        // =============================================================================================
        //  ⭐⭐ and the fixture puts the world back — the guard on the bug this file once caused
        // =============================================================================================

        [UnityTest]
        public IEnumerator TheFixtureLeavesNothingResident()
        {
            // Loading a region scene promotes its PersistentObject roots into DontDestroyOnLoad, where
            // UnloadSceneAsync cannot reach them. This asserts the teardown's second half actually works
            // — because the failure mode is silent HERE and loud four classes later, in somebody else's
            // lane, as a boat landing at the wrong arrival.
            int before = PersistentRoots().Count;

            yield return LoadTheIsland();
            Assert.That(PersistentRoots().Count, Is.GreaterThan(before),
                "if loading the island no longer makes anything resident, this guard has stopped " +
                "measuring what it was written for — check PersistentObject before deleting it");

            // the same two halves the teardown does, inline, so the assertion can see the result
            var clean = SceneManager.CreateScene("EastDoorResidencyCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
            Assert.That(PersistentRoots().Count, Is.GreaterThan(before),
                "UNLOADING THE SCENE IS NOT CLEANUP — this is the whole point: the roots outlive it");

            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go))
                    Object.DestroyImmediate(go);

            Assert.AreEqual(before, PersistentRoots().Count,
                "the fixture must leave exactly what it found resident — anything else is handed to " +
                "every class that runs after this one");
        }
    }
}
