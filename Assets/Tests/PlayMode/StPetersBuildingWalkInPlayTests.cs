using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Audit slice 1: enter every placed room in the committed scene with the shipped walker.
    /// TestRunSave's assembly prebuild hook redirects BEFORE Play and restores AFTER Play; setup
    /// refuses to load anything unless that redirect and the bootstrapped service agree.
    /// One scene load, no graphics, no real-time waits. Only the outside starting pose of each
    /// independent visit is placed; every threshold crossing and indoor step is held walk input.
    /// </summary>
    public class StPetersBuildingWalkInPlayTests
    {
        const string Region = "StPeters";
        const string Cleanup = "StPetersBuildingWalkCleanup";
        const float Arrive = 0.08f;
        const float GridMetres = 0.2f;
        const float Clearance = 0.025f;
        const float StallSeconds = 1.5f;
        const float TourBudgetSeconds = 120f; // simulated time; one load leaves headroom below 3 minutes
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        enum Gap { BareShop, EmptyHomeWithDeadUpper }

        // Exceptions ONLY, never the discovery list. Change/remove one row when its audit slice lands.
        static readonly Dictionary<string, Gap> KnownGaps = new Dictionary<string, Gap>
        {
            { "creamSaltbox", Gap.EmptyHomeWithDeadUpper }, // Audit slice 2: no furniture or stairs on either floor.
            { "harbourGable", Gap.EmptyHomeWithDeadUpper }, // Audit slice 2: no furniture or stairs on either floor.
            { "whiteGable", Gap.EmptyHomeWithDeadUpper },   // Audit slice 2: no furniture or stairs on either floor.
            { "generalStore", Gap.BareShop },             // Audit slice 7: fixtures still stand outside the shop.
            { "postOffice", Gap.BareShop },               // Audit slice 7: no indoor fixtures have been placed.
        };

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        PlayerWalkController _walker;
        Rigidbody2D _body;
        CircleCollider2D _foot;
        HeldWalkIntents _held;
        SaveService _save;
        ISaveService _previousSave;
        string _previousPath;
        float _timeScale, _tourSeconds, _radius;
        Vector2 _footOffset;
        bool _loaded;

        static T Read<T>(BuildingInterior room, string field) where T : Object
        {
            FieldInfo info = typeof(BuildingInterior).GetField(field, Private);
            Assert.That(info, Is.Not.Null, $"Update the test for renamed interior field {field}");
            return info.GetValue(room) as T;
        }

        static List<GameObject> PersistentRoots()
        {
            var probe = new GameObject("building walk persistent probe");
            Object.DontDestroyOnLoad(probe);
            Scene scene = probe.scene;
            Object.DestroyImmediate(probe);
            return scene.GetRootGameObjects().ToList();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _loaded = false;
            _timeScale = Time.timeScale;
            _residentBefore.Clear();
            foreach (GameObject root in PersistentRoots()) _residentBefore.Add(root);
            string run = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Assert.That(run, Is.Not.Null.And.Not.Empty, "TestRunSave must redirect before entering Play");
            Assert.That(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(run))),
                Is.EqualTo(TestRunSave.RunsRoot), "The save belongs to this checkout's test run");
            Assert.That(SaveStore.ActivePath, Is.EqualTo(run));
            _save = Object.FindAnyObjectByType<SaveService>();
            Assert.That(_save, Is.Not.Null, "The BeforeSceneLoad save bootstrap ran");
            _previousPath = _save.SavePath;
            Assert.That(_previousPath, Is.EqualTo(run), "The shared save must never be opened");
            _previousSave = GameServices.Save;
            _save.Open(Path.Combine(Path.GetDirectoryName(run), "stp-building-walk.json"));
            GameServices.Save = _save;
            GameServices.Save.SetFlag(ArrivalOpening.ArrivedFlagKey, true);
            Time.timeScale = 1f;
            _tourSeconds = 0f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _held?.Release();
            LogAssert.ignoreFailingMessages = false;
            if (_loaded)
            {
                Scene clean = SceneManager.GetSceneByName(Cleanup);
                if (!clean.IsValid()) clean = SceneManager.CreateScene(Cleanup);
                SceneManager.SetActiveScene(clean);
                Scene region = SceneManager.GetSceneByName(Region);
                if (region.IsValid() && region.isLoaded) yield return SceneManager.UnloadSceneAsync(region);
            }
            foreach (GameObject root in PersistentRoots())
                if (root != null && !_residentBefore.Contains(root)) Object.DestroyImmediate(root);
            GameServices.Reset();
            if (_save != null && _previousPath != null) _save.Open(_previousPath);
            GameServices.Save = _previousSave;
            Time.timeScale = _timeScale;
            _held = null;
            _walker = null;
            _save = null;
            _previousPath = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryPlacedBuilding_EntersThroughItsGap_ClimbsItsWorkingStairs_AndClosesAgain()
        {
            // Same real-scene load as VillageReturnScenesPlayTests; only boot's unrelated decor
            // diagnostics are tolerated. All diagnostics during the actual visits remain failures.
            LogAssert.ignoreFailingMessages = true;
            _loaded = true;
            yield return SceneManager.LoadSceneAsync(Region, LoadSceneMode.Single);
            for (int frame = 0; frame < 4; frame++) yield return null;
            LogAssert.ignoreFailingMessages = false;
            ShellFlow.ContinueGame();
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(ShellFlow.WorldInputBlocked, Is.False);
            Assert.That(GameServices.OpeningCinematicRunning, Is.False, "The arrival must not take the walker");
            _walker = Object.FindObjectsByType<PlayerWalkController>(FindObjectsSortMode.None)
                .Single(p => p.isActiveAndEnabled && p.transform == GameServices.PlayerTransform);
            _body = _walker.GetComponent<Rigidbody2D>();
            _foot = _walker.GetComponents<CircleCollider2D>().Single(c => !c.isTrigger && c.enabled);
            _footOffset = _foot.transform.TransformVector(_foot.offset);
            Assert.That(_body.simulated, Is.True);
            _radius = _foot.radius * Mathf.Max(Mathf.Abs(_foot.transform.lossyScale.x), Mathf.Abs(_foot.transform.lossyScale.y));
            _held = new HeldWalkIntents();
            _walker.ConfigureWalkInput(_held);

            BuildingInterior[] buildings = Object.FindObjectsByType<BuildingInterior>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b.gameObject.scene.name == Region)
                .OrderBy(b => b.transform.position.x).ThenBy(b => b.transform.position.y).ToArray();
            Assert.That(buildings.Length, Is.GreaterThanOrEqualTo(10), "The enterable-building census may grow, never shrink");
            var gapsSeen = new HashSet<string>();
            int visited = 0, climbed = 0;
            foreach (BuildingInterior building in buildings)
            {
                Assert.That(building.isActiveAndEnabled, Is.True, building.name);
                InteriorFootprint floor = building.Footprint;
                Vector2 outwards = (floor.ModelToWorld(new Vector2(floor.DoorAcrossMetres,
                    floor.DoorSign * (floor.LengthMetres * 0.5f + 1f))) - building.DoorWorld).normalized;
                Vector2 outside = building.DoorWorld + outwards * (_radius + building.WallThicknessMetres + 0.6f) - _footOffset;
                Vector2 inside = building.DoorWorld - outwards * (_radius + building.WallThicknessMetres + 0.2f) - _footOffset;

                // Set up an independent visit OUTSIDE. No position/velocity writes from here to exit.
                _body.position = outside;
                _walker.transform.position = new Vector3(outside.x, outside.y, _walker.transform.position.z);
                Physics2D.SyncTransforms();
                yield return Settle();
                Assert.That(floor.Contains(_body.position, 0f), Is.False, building.name + " starts outside");
                AssertClosed(building);
                AssertClearFoot(building);
                yield return Walk(inside, building.name + " doorway in");
                AssertFloor(building, 0);

                // A second indoor step proves the fisher can stand AND move on this floor.
                Vector2 further = inside - outwards * 0.3f;
                yield return Walk(further, building.name + " room floor");
                Assert.That(Vector2.Distance(_body.position, inside), Is.GreaterThan(0.2f), building.name + " is not blocked indoors");
                AssertFloor(building, 0);

                InteriorStair[] stairs = building.GetComponentsInChildren<InteriorStair>(true);
                if (KnownGaps.TryGetValue(building.name, out Gap gap))
                {
                    Assert.That(gapsSeen.Add(building.name), Is.True, "A known gap identifies one building only");
                    AssertEmpty(Read<Transform>(building, "_props"), building.name + " audit slice " + (gap == Gap.BareShop ? "7" : "2"));
                    Assert.That(stairs, Is.Empty, building.name + " has no usable stair today (audit slice 2/7)");
                    Assert.That(building.HasUpperLevel, Is.EqualTo(gap == Gap.EmptyHomeWithDeadUpper), building.name);
                    if (gap == Gap.EmptyHomeWithDeadUpper)
                    {
                        AssertEmpty(Read<Transform>(building, "_upperProps"), building.name + " empty upper floor (audit slice 2)");
                        Assert.That(Read<SpriteRenderer>(building, "_upperRoom").sprite, Is.Not.Null);
                        Assert.That(Read<SpriteRenderer>(building, "_upperRoom").enabled, Is.False);
                    }
                }
                else if (building.HasUpperLevel)
                {
                    InteriorStair up = stairs.Single(s => s.FromLevel == 0 && s.ToLevel == 1);
                    InteriorStair down = stairs.Single(s => s.FromLevel == 1 && s.ToLevel == 0);
                    Assert.That(stairs.Length, Is.EqualTo(2), building.name + " has a working pair of stairs");
                    yield return Follow(PathToStair(building, up), building.name + " foot of stairs");
                    Vector2 footOfStair = _body.position;
                    yield return PressStair(up, building);
                    AssertFloor(building, 1);
                    Assert.That(_body.position.y, Is.EqualTo(footOfStair.y + building.UpperLevelY).Within(0.02f));
                    Assert.That(_body.position.x, Is.EqualTo(footOfStair.x).Within(0.02f));
                    Vector2 landing = _body.position;
                    InteriorFootprint upstairs = building.FootprintFor(1);
                    Vector2[] steps = new[] { Vector2.down, Vector2.up, Vector2.right, Vector2.left }
                        .Select(dir => landing + dir * 0.3f)
                        .Where(at => upstairs.Contains(at, building.WallThicknessMetres) &&
                            upstairs.Contains(at + _footOffset, building.WallThicknessMetres) && Clear(landing, at)).ToArray();
                    Assert.That(steps, Is.Not.Empty, building.name + " upper landing is not blocked");
                    yield return Walk(steps[0], building.name + " upper floor");
                    AssertFloor(building, 1);
                    yield return Walk(landing, building.name + " back to stair head", 0.005f);
                    yield return PressStair(down, building);
                    AssertFloor(building, 0);
                    Assert.That(Vector2.Distance(_body.position, footOfStair), Is.LessThan(0.02f));
                    climbed++;
                    // Plan the return against the live ground-floor furniture too.
                    yield return Follow(PathToDoor(building, further), building.name + " return to doorway");
                }

                yield return Walk(inside, building.name + " doorway approach");
                yield return Walk(outside, building.name + " doorway out");
                Assert.That(building.IsInside, Is.False, building.name);
                AssertClosed(building);
                visited++;
                TestContext.WriteLine($"VISITED {building.name}: room on, shell off, floor clear, exit restores shell");
            }
            CollectionAssert.AreEquivalent(KnownGaps.Keys, gapsSeen, "Every documented gap was actually visited");
            Assert.That(visited, Is.EqualTo(buildings.Length));
            Assert.That(climbed, Is.GreaterThanOrEqualTo(1), "At least the existing working upper floor was climbed");
            TestContext.WriteLine($"CENSUS {visited}; CLIMBED {climbed}; simulated walk cost {_tourSeconds:F2}s (one scene load)");
        }

        static void AssertEmpty(Transform props, string why)
        {
            Assert.That(props != null ? props.childCount : 0, Is.Zero, why);
        }

        static void AssertClosed(BuildingInterior building)
        {
            Assert.That(building.IsInside, Is.False, building.name);
            Assert.That(building.Level, Is.Zero, building.name);
            Assert.That(Read<SpriteRenderer>(building, "_shell").enabled, Is.True, building.name);
            Assert.That(Read<SpriteRenderer>(building, "_shell").gameObject.activeInHierarchy, Is.True, building.name);
            Assert.That(Read<SpriteRenderer>(building, "_room").enabled, Is.False, building.name);
            SpriteRenderer upper = Read<SpriteRenderer>(building, "_upperRoom");
            if (upper != null) Assert.That(upper.enabled, Is.False, building.name);
            AssertLevelRoots(building, -1);
        }

        void AssertFloor(BuildingInterior building, int level)
        {
            Assert.That(building.IsInside, Is.True, building.name);
            Assert.That(building.Level, Is.EqualTo(level), building.name);
            Assert.That(Read<SpriteRenderer>(building, "_shell").enabled, Is.False, building.name);
            SpriteRenderer ground = Read<SpriteRenderer>(building, "_room");
            SpriteRenderer upper = Read<SpriteRenderer>(building, "_upperRoom");
            Assert.That(ground.enabled, Is.EqualTo(level == 0), building.name);
            if (upper != null) Assert.That(upper.enabled, Is.EqualTo(level == 1), building.name);
            AssertLevelRoots(building, level);
            SpriteRenderer drawn = level == 0 ? ground : upper;
            Assert.That(drawn.gameObject.activeInHierarchy, Is.True, building.name + " room picture is active");
            Assert.That(drawn.sprite, Is.Not.Null, building.name + " has its room sheet");
            InteriorFootprint floor = building.FootprintFor(level);
            Assert.That(floor.Contains(_body.position, building.WallThicknessMetres), Is.True, building.name + " feet are on this floor");
            Assert.That(floor.Contains(_body.position + _footOffset, building.WallThicknessMetres), Is.True,
                building.name + " actual foot collider stands on this floor too");
            Assert.That(Vector2.Distance(drawn.transform.position, floor.Centre), Is.LessThan(0.001f), building.name + " room is at this floor's height");
            AssertClearFoot(building);
        }

        static void AssertLevelRoots(BuildingInterior building, int level)
        {
            Transform ground = Read<Transform>(building, "_props");
            Transform upper = Read<Transform>(building, "_upperProps");
            Transform walls = Read<Transform>(building, "_upperWalls");
            if (ground != null) Assert.That(ground.gameObject.activeSelf, Is.EqualTo(level == 0), building.name);
            if (upper != null) Assert.That(upper.gameObject.activeSelf, Is.EqualTo(level == 1), building.name);
            if (walls != null) Assert.That(walls.gameObject.activeSelf, Is.EqualTo(level == 1), building.name);
        }

        bool Solid(Collider2D collider) => collider != null && collider.enabled && !collider.isTrigger &&
            !collider.transform.IsChildOf(_walker.transform) &&
            !Physics2D.GetIgnoreLayerCollision(_foot.gameObject.layer, collider.gameObject.layer) &&
            !Physics2D.GetIgnoreCollision(_foot, collider);

        void AssertClearFoot(BuildingInterior building)
        {
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(_body.position + _footOffset, _radius))
                if (Solid(collider)) Assert.That(_foot.Distance(collider).distance, Is.GreaterThanOrEqualTo(-0.01f),
                    building.name + " feet penetrate " + collider.name);
        }

        bool Clear(Vector2 a, Vector2 b)
        {
            if (Physics2D.OverlapCircleAll(b + _footOffset, _radius + Clearance).Any(Solid)) return false;
            Vector2 delta = b - a;
            return !Physics2D.CircleCastAll(a + _footOffset, _radius + Clearance, delta.normalized, delta.magnitude)
                .Any(hit => Solid(hit.collider));
        }

        IEnumerator Settle()
        {
            _held.Release();
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        IEnumerator Walk(Vector2 target, string why, float arrival = Arrive)
        {
            float remaining = Vector2.Distance(_body.position, target) / 1.5f + 3f;
            float stalled = 0f;
            Vector2 checkpoint = _body.position;
            while (Vector2.Distance(_body.position, target) > arrival && remaining > 0f)
            {
                _held.Walk(Vector2.ClampMagnitude((target - _body.position) / 0.4f, 1f));
                yield return null;
                float dt = Time.deltaTime;
                _tourSeconds += dt;
                remaining -= dt;
                stalled += dt;
                Assert.That(_tourSeconds, Is.LessThan(TourBudgetSeconds), "The entire tour must stay short");
                if (stalled < StallSeconds) continue;
                Assert.That(Vector2.Distance(checkpoint, _body.position), Is.GreaterThan(Arrive),
                    $"{why}: blocked at {_body.position}, aiming for {target}");
                checkpoint = _body.position;
                stalled = 0f;
            }
            yield return Settle();
            Assert.That(Vector2.Distance(_body.position, target), Is.LessThanOrEqualTo(arrival * 1.5f), why);
        }

        IEnumerator PressStair(InteriorStair stair, BuildingInterior building)
        {
            Assert.That(stair.IsAvailable, Is.True, stair.Id);
            var actor = new InteractActor(_body.position, Vector2.up, InteractContext.OnFoot);
            Assert.That(InteractResolver.TryResolveNow(actor, InteractResolver.DefaultFacingArcDegrees, out IInteractable best), Is.True);
            Assert.That(best, Is.SameAs(stair), "The ordinary interact press finds this stair");
            best.Interact(actor);
            float budget = GameServices.StairClimbSeconds * 4f + 1f;
            while (building.IsClimbing && budget > 0f)
            {
                yield return null;
                budget -= Time.deltaTime;
                _tourSeconds += Time.deltaTime;
                Assert.That(_tourSeconds, Is.LessThan(TourBudgetSeconds), "The entire tour must stay short");
            }
            Assert.That(building.IsClimbing, Is.False, "The real stair traversal finishes");
            yield return Settle();
        }

        List<Vector2> PathToStair(BuildingInterior building, InteriorStair stair) =>
            FindPath(building, at => Vector2.Distance(at, stair.WorldPosition) <= stair.ReachMeters - Arrive * 1.25f);

        List<Vector2> PathToDoor(BuildingInterior building, Vector2 door) =>
            FindPath(building, at => Vector2.Distance(at, door) < GridMetres && Clear(at, door))
                .Concat(new[] { door }).ToList();

        List<Vector2> FindPath(BuildingInterior building, Func<Vector2, bool> reached)
        {
            // Small local grid of the CURRENT floor, tested against the live physics geometry.
            // Furniture and walls stay in place. This plans inputs; it never moves the player.
            InteriorFootprint floor = building.FootprintFor(building.Level);
            int nx = Mathf.CeilToInt(floor.WidthMetres / GridMetres / 2f);
            int ny = Mathf.CeilToInt(floor.LengthMetres / GridMetres / 2f);
            var points = new Dictionary<Vector2Int, Vector2>();
            for (int x = -nx; x <= nx; x++)
                for (int y = -ny; y <= ny; y++)
                {
                    Vector2 at = floor.ModelToWorld(new Vector2(x * GridMetres, y * GridMetres));
                    if (floor.Contains(at, building.WallThicknessMetres) && Clear(at, at)) points.Add(new Vector2Int(x, y), at);
                }
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var start = new Vector2Int(int.MinValue, int.MinValue);
            foreach (var point in points)
                if (Vector2.Distance(_body.position, point.Value) < GridMetres * 2f && Clear(_body.position, point.Value))
                { queue.Enqueue(point.Key); previous.Add(point.Key, start); }
            while (queue.Count > 0)
            {
                Vector2Int here = queue.Dequeue();
                if (reached(points[here]))
                {
                    var path = new List<Vector2>();
                    for (Vector2Int p = here; p != start; p = previous[p]) path.Add(points[p]);
                    path.Reverse();
                    return path;
                }
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Vector2Int next = here + new Vector2Int(dx, dy);
                        if (previous.ContainsKey(next) || !points.ContainsKey(next) || !Clear(points[here], points[next])) continue;
                        previous.Add(next, here);
                        queue.Enqueue(next);
                    }
            }
            Assert.Fail(building.name + " has no foot-clear route on level " + building.Level);
            return null;
        }

        IEnumerator Follow(List<Vector2> path, string why)
        {
            while (path.Count > 0)
            {
                int next = path.Count - 1;
                while (next > 0 && !Clear(_body.position, path[next])) next--;
                yield return Walk(path[next], why);
                path.RemoveRange(0, next + 1);
            }
        }
    }
}
