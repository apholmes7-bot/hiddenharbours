#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using KeyScenePlacement = HiddenHarbours.App.Editor.StPetersLayerRefresh.KeyScenePlacement;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE KEY SCENES STAND IN THE COMMITTED SCENE.</b> <c>StPetersKeyScenesTests</c> holds the
    /// placements and the patch that writes them; this file holds the one claim those cannot make: that the
    /// LOADED <c>StPeters.unity</c> carries every piece the key scenes place today, where the step placed it,
    /// showing the cell for its facing, sorted as the step sorted it, with its walls, its waterline and no
    /// light, and nothing else under the root. And that the harbour vane, once the island has booted its
    /// environment service, shows the heading of the wind the sim is blowing.
    ///
    /// <para><b>No graphics needed.</b> Nothing here renders, so it runs on CI. The island's persistent
    /// roots outlive its unload, so the fixture destroys what the load made resident, as
    /// <c>StPetersEastDoorPlayTests</c> does, and <see cref="TheFixtureLeavesNothingResident"/> guards it.</para>
    /// </summary>
    public class StPetersKeyScenesPlayTests
    {
        private const string SceneName = "StPeters";

        /// <summary>How far a loaded position or wall may sit from the step's, in metres: the scene holds the
        /// step's numbers to the bit, so this is only the float round trip.</summary>
        private const float Tolerance = 1e-4f;

        private readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();

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

            var clean = SceneManager.CreateScene("KeyScenesCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go))
                    Object.DestroyImmediate(go);
            GameServices.Reset();
            yield return null;
        }

        private IEnumerator LoadTheIsland()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>What the step places today, from the committed key scenes and the kits' art.</summary>
        private static List<KeyScenePlacement> Expected() =>
            StPetersLayerRefresh.PlaceKeyScenes(StPetersLayerRefresh.LoadStPetersKeyScenes(),
                                                new StPetersLayerRefresh.EditorKeySceneAssets());

        private static GameObject KeyScenesRoot()
        {
            GameObject root = SceneManager.GetSceneByName(SceneName).GetRootGameObjects()
                .FirstOrDefault(g => g.name == StPetersLayerRefresh.KeyScenesRootName);
            Assert.IsNotNull(root, $"the committed {SceneName}.unity has no '{StPetersLayerRefresh.KeyScenesRootName}' root: " +
                                   "run the key scenes step's apply, and commit the scene it writes");
            return root;
        }

        private static (string Guid, long Local) AssetIdOf(Object o)
        {
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long local), $"{o} is no asset");
            return (guid, local);
        }

        // =============================================================================================

        [UnityTest]
        public IEnumerator EveryPlacedPiece_StandsInTheCommittedScene_AsTheStepPlacedIt()
        {
            List<KeyScenePlacement> expected = Expected();
            yield return LoadTheIsland();

            GameObject root = KeyScenesRoot();
            Assert.AreEqual(Vector3.zero, root.transform.position, "the root stands at the origin");
            Assert.IsEmpty(root.GetComponentsInChildren<SceneLight>(true), "a key scene piece carries a light; the step places none");

            var groups = expected.Select(p => p.Scene).Distinct().ToList();
            CollectionAssert.AreEqual(groups, root.transform.Cast<Transform>().Select(t => t.name).ToList(),
                                      "one group per key scene, and nothing else under the root");
            foreach (string scene in groups)
                CollectionAssert.AreEqual(expected.Where(p => p.Scene == scene).Select(p => p.Id).ToList(),
                                          root.transform.Find(scene).Cast<Transform>().Select(t => t.name).ToList(),
                                          $"{scene}'s pieces, and nothing else");

            foreach (KeyScenePlacement p in expected)
            {
                Transform piece = root.transform.Find(p.Scene).Find(p.Id);
                if (p.IsPerch)
                {
                    var perch = piece.GetComponent<HiddenHarbours.World.GullPerch>();
                    Assert.IsNotNull(perch, p.Id);
                    Assert.AreEqual(p.Id, perch.Id);
                    Assert.That(Vector2.Distance(p.At, perch.ScreenPoint), Is.LessThan(Tolerance));
                    Assert.IsNull(piece.GetComponent<SpriteRenderer>(), "the flock supplies the bird");
                    continue;
                }
                Vector3 at = piece.position;
                Assert.AreEqual(p.At.x, at.x, Tolerance, $"{p.Id} stands at {at}");
                Assert.AreEqual(p.At.y, at.y, Tolerance, $"{p.Id} stands at {at}");
                Assert.AreEqual(0f, at.z, Tolerance, $"{p.Id} stands off the ground plane");

                // Its facing is its cell: the frame or iso cell the step chose for CD's dir. The vane may
                // already show the heading of the wind; any of its own headings is its own.
                var sr = piece.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(sr, $"{p.Id} draws nothing");
                Assert.IsNotNull(sr.sprite, $"{p.Id} has no sprite");
                (string guid, long local) = AssetIdOf(sr.sprite);
                if (p.TurnsWithWind)
                {
                    var vane = piece.GetComponent<SetPieceWindVane>();
                    Assert.IsNotNull(vane, $"{p.Id} does not turn with the wind");
                    Assert.IsTrue(guid == p.Sprite.Sprite.Guid && local == p.Sprite.Sprite.FileId ||
                                  vane.Def.Animated.Headings.Any(h => h.Sprite == sr.sprite),
                                  $"{p.Id} shows a picture that is neither its placed frame nor one of its headings");
                }
                else
                {
                    Assert.AreEqual(p.Sprite.Sprite.Guid, guid, $"{p.Id} shows another sheet's cell ({p.Cell} expected)");
                    Assert.AreEqual(p.Sprite.Sprite.FileId, local, $"{p.Id} shows the wrong cell ({p.Cell} expected)");
                    Assert.IsNull(piece.GetComponent<SetPieceWindVane>(), $"{p.Id} turns with the wind");
                }

                Assert.AreEqual(0, sr.sortingLayerID, $"{p.Id} sorts in another layer");
                Assert.AreEqual(p.SortingOrder, sr.sortingOrder, $"{p.Id}'s order");
                var sorter = piece.GetComponent<YSortSprite>();
                if (p.Floor) Assert.IsNull(sorter, $"{p.Id} lies on the floor, and has a sorter");
                else
                {
                    Assert.IsNotNull(sorter, $"{p.Id} stands up, and has no sorter");
                    Assert.IsFalse(sorter.Dynamic, $"{p.Id} does not move, so it sorts once");
                    Assert.AreEqual(p.SortYOffset, sorter.SortPivotYOffset, Tolerance, $"{p.Id}'s sort line");
                }

                var walls = piece.GetComponent<PolygonCollider2D>();
                if (p.Walls.Count == 0) Assert.IsNull(walls, $"{p.Id} has walls its kit does not give it");
                else
                {
                    Assert.IsNotNull(walls, $"{p.Id} has no walls");
                    Assert.IsFalse(walls.isTrigger, $"{p.Id}'s walls do not block the walk");
                    Assert.AreEqual(p.Walls.Count, walls.pathCount, $"{p.Id}'s walls");
                    for (int w = 0; w < p.Walls.Count; w++)
                    {
                        Vector2[] path = walls.GetPath(w);
                        Assert.AreEqual(p.Walls[w].Length, path.Length, $"{p.Id}'s wall {w}");
                        for (int k = 0; k < path.Length; k++)
                            Assert.IsTrue((path[k] - p.Walls[w][k]).sqrMagnitude < Tolerance * Tolerance,
                                          $"{p.Id}'s wall {w} corner {k} is at {path[k]}, not {p.Walls[w][k]}");
                    }
                }

                var face = piece.GetComponent<TidalFaceWaterline>();
                if (!p.RidesTheTide) Assert.IsNull(face, $"{p.Id} is cut by the sea");
                else
                {
                    Assert.IsNotNull(face, $"{p.Id} is not cut by the sea");
                    Assert.IsTrue(face.IsConfigured && face.RidesTheTide, $"{p.Id}'s waterline is not configured to ride the tide");
                    Assert.AreEqual(p.LipWorldY, face.LipWorldY, Tolerance, $"{p.Id}'s lip row");
                    Assert.AreEqual(p.LipElevation, face.LipElevation, Tolerance, $"{p.Id}'s lip height");
                }
            }
        }

        [UnityTest]
        public IEnumerator TheHarbourVane_ShowsTheHeadingOfTheWindTheSimBlows()
        {
            yield return LoadTheIsland();

            SetPieceWindVane vane = KeyScenesRoot().GetComponentsInChildren<SetPieceWindVane>(true).Single();
            IEnvironmentService env = GameServices.Environment;
            Assert.IsNotNull(env, "the island booted no environment service, so the vane has no wind to read");

            int held = vane.ShownHeading;
            vane.Refresh();
            Vector2 wind = env.Sample().WindVector;
            int heading = SetPieceWindVane.HeadingFor(wind, vane.Def.Animated, vane.CalmMetresPerSecond, held);
            Assert.AreEqual(heading, vane.ShownHeading, $"a wind of {wind} m/s shows the wrong heading");
            if (heading >= 0)
                Assert.AreEqual(vane.Def.Animated.Headings[heading].Sprite, vane.GetComponent<SpriteRenderer>().sprite,
                                "the vane does not show its heading's picture");
        }

        [UnityTest]
        public IEnumerator TheFixtureLeavesNothingResident()
        {
            int before = PersistentRoots().Count;

            yield return LoadTheIsland();
            Assert.That(PersistentRoots().Count, Is.GreaterThan(before),
                "if loading the island no longer makes anything resident, this guard has stopped measuring what it was written for");

            var clean = SceneManager.CreateScene("KeyScenesResidencyCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == SceneName)
                    yield return SceneManager.UnloadSceneAsync(s);
            }
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go))
                    Object.DestroyImmediate(go);

            Assert.AreEqual(before, PersistentRoots().Count, "the fixture must leave exactly what it found resident");
        }
    }
}
#endif
