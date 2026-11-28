using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE BROKEN FOOT TAKES NOTHING</b> (rough-edges pass, owner 11 26). With the jag on, St Peters' cliff blockers
    /// stand on the foot as it is DRAWN — the kit's profile plus the jag, closed over a walker's bite — instead of on
    /// the stations. That moves collision, so this walks every authored thing in the scene (anything with a collider,
    /// a renderer, an NPC stand or a door, the cliffs themselves and the big ground layers aside) and fails on any
    /// whose position the cliff blockers take with the jag on but did not take with it off. It lists them, so a
    /// failure says what would have to move instead of moving it silently.
    /// </summary>
    public class CliffJagClearancePlayTests
    {
        const string StPeters = "StPeters";
        const string CleanupSceneName = "CliffJagCleanup";
        const float BigGroundMetres = 8f;   // a renderer this wide is a ground layer, not a placed thing

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        bool _loadedAny, _introOnEntry;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (!_loadedAny) yield break;
            ShellFlow.Reset();
            InteractionGate.Reset();
            GameServices.OpeningCinematicRunning = _introOnEntry;
            GameServices.PendingArrivalKey = null;
            Scene clean = SceneManager.GetSceneByName(CleanupSceneName);
            if (!clean.IsValid() || !clean.isLoaded) clean = SceneManager.CreateScene(CleanupSceneName);
            if (clean.IsValid()) SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == StPeters)
                    yield return HiddenHarbours.Tests.Support.SceneSettle.Unload(s);
            }
            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go)) Object.DestroyImmediate(go);
            GameServices.Reset();
            ShellFlow.Reset();
            InteractionGate.Reset();
            _loadedAny = false;
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator StPeters_TheJaggedFoot_TakesNoAuthoredThing()
        {
            CliffFaceMixDef.ClearCache();
            CliffFaceMixDef mix = CliffFaceMixDef.ForScene(StPeters);
            Assert.IsNotNull(mix, "St Peters has no cliff mix");
            Assert.IsTrue(CliffJag.From(mix).On, "St Peters' rough edges are off: nothing to prove");

            _introOnEntry = GameServices.OpeningCinematicRunning;
            LogAssert.ignoreFailingMessages = true;
            _loadedAny = true;
            yield return HiddenHarbours.Tests.Support.SceneSettle.Load(StPeters, LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;
            Scene scene = SceneManager.GetSceneByName(StPeters);
            Assert.IsTrue(scene.IsValid() && scene.isLoaded);

            List<(string what, Vector2 at)> things = AuthoredThings(scene);
            Assert.Greater(things.Count, 200, "too few authored things found to prove anything");

            float brow = mix.BrowJagMetres, foot = mix.FootJagMetres;
            HashSet<string> before, after;
            try
            {
                mix.BrowJagMetres = 0f; mix.FootJagMetres = 0f;
                yield return RebuildWalls(scene);
                before = Taken(things);
            }
            finally { mix.BrowJagMetres = brow; mix.FootJagMetres = foot; }
            yield return RebuildWalls(scene);
            after = Taken(things);

            List<string> newly = after.Where(w => !before.Contains(w)).OrderBy(w => w).ToList();
            var report = new StringBuilder();
            report.AppendLine($"[cliff-jag-clearance] {things.Count} authored things; cliff blockers take {before.Count} with the jag off, " +
                              $"{after.Count} with it on; newly taken {newly.Count}:");
            foreach (string w in newly) report.AppendLine("  " + w);
            foreach (string w in before.Where(w => !after.Contains(w)).OrderBy(w => w)) report.AppendLine("  freed: " + w);
            Debug.Log(report.ToString());
            Assert.AreEqual(0, newly.Count, report.ToString());
        }

        static IEnumerator RebuildWalls(Scene scene)
        {
            var walls = new List<CliffWallSurface>();
            foreach (GameObject root in scene.GetRootGameObjects())
                walls.AddRange(root.GetComponentsInChildren<CliffWallSurface>(true));
            foreach (CliffWallSurface w in walls) if (w.enabled) w.enabled = false;
            foreach (CliffWallSurface w in walls) w.enabled = true;
            yield return new WaitForFixedUpdate();
            Physics2D.SyncTransforms();
        }

        static HashSet<string> Taken(List<(string what, Vector2 at)> things)
        {
            var taken = new HashSet<string>();
            var hits = new List<Collider2D>();
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(1 << CliffWallSurface.BlockerLayer);
            foreach ((string what, Vector2 at) in things)
            {
                hits.Clear();
                Physics2D.OverlapPoint(at, filter, hits);
                foreach (Collider2D c in hits)
                {
                    string n = c.gameObject.name;
                    if (n == CliffWallSurface.BlockerChildName || n == CliffWallSurface.FaceBlockerChildName)
                    { taken.Add(what); break; }
                }
            }
            return taken;
        }

        static List<(string, Vector2)> AuthoredThings(Scene scene)
        {
            var found = new List<(string, Vector2)>();
            var seen = new HashSet<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponentInParent<CliffWallSurface>(true) != null) continue;
                    bool placed = t.GetComponent<Collider2D>() != null || t.GetComponent<SpriteRenderer>() != null ||
                                  t.name.Contains("Door") || t.name.Contains("Stand") || t.name.Contains("Path");
                    if (!placed) continue;
                    Renderer r = t.GetComponent<Renderer>();
                    if (r != null && (r.bounds.size.x > BigGroundMetres || r.bounds.size.y > BigGroundMetres)) continue;
                    Collider2D c = t.GetComponent<Collider2D>();
                    if (c != null && (c.bounds.size.x > BigGroundMetres || c.bounds.size.y > BigGroundMetres)) continue;
                    if (!seen.Add(t)) continue;
                    found.Add((Path(t), (Vector2)t.position));
                }
            return found;
        }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (Transform p = t; p != null; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts) + $" @({t.position.x:0.##}, {t.position.y:0.##})";
        }

        static List<GameObject> PersistentRoots()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
