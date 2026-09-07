using System.Collections;
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
    /// <para><b>Why that is a separate claim.</b> A committed scene in this project is largely builder
    /// output, and <c>StPetersBuilder.Build()</c> cannot be re-run: it wipes the hand-authored layer and
    /// the guard's own escape hatch ("Refresh St Peters Island Logic") does not exist for this region.
    /// So the door had to be added to the committed scene BY HAND, with the builder taught the same
    /// wiring so a future rebuild agrees. That is exactly the shape the <c>scene-wired is not
    /// builder-wired</c> lesson (#323) warns about, and the guard against it is this: read the door out
    /// of the LOADED scene and hold it against the builder's own constants. If either side moves alone,
    /// this fails.</para>
    ///
    /// <para><b>No graphics needed.</b> Nothing here renders — the scene is loaded and its components
    /// are read — so it runs on CI, which has no GPU.</para>
    /// </summary>
    public class StPetersEastDoorPlayTests
    {
        private const string SceneName = "StPeters";
        private const string DoorName = "PassageToEastWater";

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;
            GameServices.PendingArrivalKey = null;

            // ⚠️ PUT THE WORLD BACK. One player loop is shared across the whole PlayMode run, and a
            // resident St Peters hands every test that follows an island, a tide and a seabed it never
            // asked for — which is how a red lands in somebody else's lane and reads as their bug.
            var clean = SceneManager.CreateScene("EastDoorCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        private IEnumerator LoadTheIsland()
        {
            // The island logs unrelated decor complaints; they are not this file's business and a
            // stray one must not fail a claim about a passage.
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

            // TARGET: the def the builder resolves by path, read back off the placed component.
            Assert.IsNotNull(door.Target, "the east door names no target region");
            Assert.AreEqual(EastWaterPlan.RegionId, door.Target.Id,
                "the east door must lead to the east water and nowhere else");
            Assert.AreEqual(EastWaterPlan.SceneName, door.Target.SceneName,
                "…and the def it points at must promise the scene the loader will ask for");

            // KEY: which way in it lands at on the far side.
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

            // ⚠ AND THE STEP ASHORE MUST NOT FOLLOW THE WAY YOU CAME IN. Naming a disembark point on
            // this arrival would silently re-point every LATER step-off at a spot out in the bay — the
            // one hazard the per-passage arrival seam exists to hold (PerPassageArrivalTests).
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

            // The island's other two ways off must still be there — adding a door may not cost one.
            Assert.IsTrue(loader.Registry.Contains("region.nine_mile_creek"));
            Assert.IsTrue(loader.Registry.Contains("region.st_peters"));
        }

        // =============================================================================================
        //  ⭐ and it is SAFE to sail into before the region on the far side exists
        // =============================================================================================

        [UnityTest]
        public IEnumerator SailingEastBeforeTheEastWaterSceneExists_DeclinesCleanly()
        {
            // This door ships one PR ahead of the scene behind it, so for one merge the east wall leads
            // somewhere that is not built yet. That must be a soft edge and not a broken build (rule 10):
            // the loader declines an unbuilt scene, and the passage takes back the arrival key it had
            // already published — because a key left standing would be read by whatever arrival came
            // next, and the one thing worse than no key is somebody else's.
            //
            // ⚠ Written to stay true AFTER the east water lands: once the scene is in Build Settings the
            // decline path no longer applies and the test says so rather than failing.
            yield return LoadTheIsland();

            RegionPassage door = EastDoor();
            Assert.IsNotNull(door, "no east door in the committed scene");

            if (Application.CanStreamedLevelBeLoaded(EastWaterPlan.SceneName))
                Assert.Pass("the east water scene has landed and is in Build Settings — the door now " +
                            "leads somewhere, so the decline path this guards is no longer reachable");

            GameServices.PendingArrivalKey = null;
            LogAssert.Expect(LogType.Warning,
                             new Regex("Could not load scene '" + EastWaterPlan.SceneName + "'"));

            door.Activate();
            yield return null;

            Assert.IsNull(GameServices.PendingArrivalKey,
                "a declined travel raises no activeSceneChanged, so nobody consumes the key the passage " +
                "published — it has to be taken back, or the next arrival reads it");
            Assert.AreEqual(SceneName, SceneManager.GetActiveScene().name,
                "the player is still on the island — a door to an unbuilt region must not move her");
        }
    }
}
