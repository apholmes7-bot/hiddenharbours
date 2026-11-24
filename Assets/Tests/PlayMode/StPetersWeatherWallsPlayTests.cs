#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The four storms round St Peters against a real rowed dory. The self-installed director and the
    /// committed walls are the ones under test; the fixture gives them the island's region id, a held
    /// fair weather and a save whose flags it can set, and rows a dory flat out at each edge in open
    /// water (no bed: the barrier is measured against her drive alone, with nothing to ground on).
    /// </summary>
    public class StPetersWeatherWallsPlayTests
    {
        const float SimSpeed = 8f;
        const string GateFlag = "st_peters.weather_turned";
        const string FirstWeekFlag = "st_peters.first_week";
        const string Region = StPetersLayerRefresh.StPetersRegionId;

        static readonly string[] Edges = { "east", "west", "north", "south" };

        sealed class FlagSave : ISaveService
        {
            // A save made by the first-week opening: the key the walls stand on is already written.
            readonly HashSet<string> _flags = new HashSet<string> { FirstWeekFlag };
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.Contains(key);
            public void SetFlag(string key, bool value) { if (value) _flags.Add(key); else _flags.Remove(key); }
            public void Save() { }
        }

        sealed class FairWeather : IEnvironmentService
        {
            public static readonly EnvironmentSample Fair = new EnvironmentSample(
                new Vector2(1.5f, 0.5f), Vector2.zero, 0f, SeaState.Calm, 1f, 0.05f);
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => Fair;
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        /// <summary>The least she must drift inshore once the oars are shipped.</summary>
        const float SetBackMetres = 1f;

        readonly List<Object> _made = new List<Object>();
        readonly List<string> _lines = new List<string>();
        CampaignBarrierDirector _director;
        FlagSave _save;
        float _timeScale;

        static CampaignBarrierDef Wall(string edge) =>
            Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder)
                .Single(w => w.Id == "campaign_barrier.st_peters_storm_" + edge);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _timeScale = Time.timeScale;
            Time.timeScale = SimSpeed;
            _director = CampaignBarrierDirector.Instance;
            Assert.IsNotNull(_director, "the director installs itself; no scene places it");
            _director.LoadWalls(Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder));
            _save = new FlagSave();
            GameServices.Save = _save;
            GameServices.Environment = new FairWeather();
            GameServices.CurrentRegionId = Region;
            EventBus.Subscribe<AmbientSpeechRequested>(OnSpeech);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AmbientSpeechRequested>(OnSpeech);
            Time.timeScale = _timeScale;
            foreach (var o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            _lines.Clear();
            GameServices.Reset();
        }

        void OnSpeech(AmbientSpeechRequested e)
        {
            if (e.Kind == AmbientSpeechKind.InnerVoice) _lines.Add(e.Line);
        }

        BoatController Boat(string hullName, CampaignBarrierDef wall, float metresInside, bool headingOut = true)
        {
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"Assets/_Project/Data/Boats/{hullName}.asset");
            Assert.IsNotNull(hull, hullName);
            var go = new GameObject("WallBoat_" + hullName);
            _made.Add(go);
            go.transform.position = wall.EdgePoint + wall.PushDirection * metresInside;
            Vector2 heading = headingOut ? -wall.PushDirection : wall.PushDirection;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, heading));
            var boat = go.AddComponent<BoatController>();
            boat.SetHull(hull);
            GameServices.Helm.SetPilotedHull(boat);
            GameServices.Helm.SetPlayersBoat(boat);
            return boat;
        }

        /// <summary>Row (or motor) her flat out for <paramref name="seconds"/> of game time. Reports the
        /// closest she came to the edge, the furthest she moved in one physics step, and the most she
        /// ever turned in one step.</summary>
        IEnumerator Drive(BoatController boat, CampaignBarrierDef wall, float seconds, bool power,
                          System.Action<float, float, float> report)
        {
            float closest = wall.DistanceInside(boat.transform.position), longestStep = 0f, widestTurn = 0f;
            Vector2 last = boat.transform.position;
            float lastAngle = boat.transform.eulerAngles.z;
            int steps = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            var wait = new WaitForFixedUpdate();
            for (int i = 0; i < steps; i++)
            {
                boat.SetControl(power ? 1f : 0f, 0f);
                boat.SetOarInput(power ? 1f : 0f, power ? 1f : 0f, false);
                yield return wait;
                Vector2 now = boat.transform.position;
                closest = Mathf.Min(closest, wall.DistanceInside(now));
                longestStep = Mathf.Max(longestStep, Vector2.Distance(last, now));
                widestTurn = Mathf.Max(widestTurn, Mathf.Abs(Mathf.DeltaAngle(lastAngle, boat.transform.eulerAngles.z)));
                last = now;
                lastAngle = boat.transform.eulerAngles.z;
            }
            report(closest, longestStep, widestTurn);
        }

        void AssertAfloatAndHers(BoatController boat, string what)
        {
            Assert.AreEqual(RigidbodyType2D.Dynamic, boat.GetComponent<Rigidbody2D>().bodyType, what);
            Assert.IsFalse(boat.IsAground, what);
            Assert.AreSame(boat, GameServices.Helm.PilotedHull, what + ": he still has the oars");
        }

        // ---- a rowed dory at each edge is turned back by the weather ---------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator ARowedDory_DrivenAtEachEdge_IsTurnedBackByTheWeather_BeforeTheLimitLine(
            [ValueSource(nameof(Edges))] string edge)
        {
            var wall = Wall(edge);
            var boat = Boat("Dory", wall, wall.StartDistanceMetres + 20f);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(0f, _director.Storm01, "fair water inside the start line");
            Assert.IsFalse(LocalWeather.Active);

            float closest = 0f, longest = 0f, turn = 0f;
            yield return Drive(boat, wall, 150f, true, (c, l, t) => { closest = c; longest = l; turn = t; });

            Debug.Log($"[st-peters-walls] {edge}: a rowed dory flat out for 150 s came within {closest:F1} m " +
                      $"of the edge (start {wall.StartDistanceMetres}, full {wall.FullDistanceMetres}, " +
                      $"limit {wall.LimitDistanceMetres}); storm {_director.Storm01:F2}, push " +
                      $"{_director.PushAcceleration:F2} m/s²; longest step {longest:F3} m, widest turn {turn:F2}°");
            Assert.AreSame(wall, _director.Standing);
            Assert.Less(closest, wall.StartDistanceMetres, $"{edge}: she rowed into the weather ({closest} m)");
            Assert.Greater(closest, wall.LimitDistanceMetres,
                $"{edge}: the weather itself stopped her ({closest} m), before the hard limit line");
            Assert.Less(longest, 0.2f, $"{edge}: nothing threw her ({longest} m in one step)");
            Assert.Less(turn, 5f, $"{edge}: nothing spun her ({turn}° in one step)");
            Assert.IsTrue(LocalWeather.Active, "the storm is over him");
            CollectionAssert.Contains(_lines, wall.Line, "he thinks the wall's one line");
            AssertAfloatAndHers(boat, edge);

            // Oars shipped: the weather sets her back toward the island and leaves her afloat.
            float before = wall.DistanceInside(boat.transform.position);
            yield return Drive(boat, wall, 60f, false, (c, l, t) => { longest = l; });
            // Where the oars stop she may already have fallen back to the storm's outer fringe, where the
            // push is light, so the check is the direction (inshore, never out), not a set distance.
            Assert.Greater(wall.DistanceInside(boat.transform.position), before + SetBackMetres,
                $"{edge}: set back inshore");
            Assert.Less(longest, 0.2f, edge);
            AssertAfloatAndHers(boat, edge);
            Assert.AreEqual(1, _lines.Count(x => x == wall.Line),
                $"{edge}: the line was thought once in all that time in the weather, not over and over");
        }

        // ---- the look: the dark front, the rain, and both gone with the flag ---------------------------

        [UnityTest, Timeout(120000)]
        public IEnumerator TheDarkFrontAndTheRain_AreDrawnInTheWeather_AndGoneTheStepTheFlagIsSet(
            [ValueSource(nameof(Edges))] string edge)
        {
            var wall = Wall(edge);
            var veil = SquallVeil.Instance;
            Assert.IsNotNull(veil, "the veil installs itself; no scene places it");
            var camGo = new GameObject("WallLookCamera") { tag = "MainCamera" };
            _made.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;

            // Well inside the island's water: no front, no rain.
            var boat = Boat("Dory", wall, wall.SightDistanceMetres + 20f, headingOut: false);
            camGo.transform.position = boat.transform.position + Vector3.back * 10f;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsFalse(veil.Showing, $"{edge}: clear water inside the sight line");
            Assert.IsFalse(LocalWeather.SquallActive, edge);

            // Between the sight line and the weather: the front shows, dry.
            boat.transform.position = wall.EdgePoint + wall.PushDirection * (wall.StartDistanceMetres + 15f);
            boat.GetComponent<Rigidbody2D>().position = boat.transform.position;
            camGo.transform.position = boat.transform.position + Vector3.back * 10f;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(veil.Showing, $"{edge}: the dark front shows before the weather");
            Assert.Greater(veil.ShownLoom01, 0f, edge);
            Assert.Less(veil.ShownLoom01, CampaignBarrierDef.LoomShareAtTheStartLine, edge);
            Assert.AreEqual(0f, LocalWeather.Rain(0f), $"{edge}: dry until the weather itself");
            Assert.IsFalse(LocalWeather.Active, edge);

            // In the weather, where a rowed boat is stopped: the whole view dark, and rain.
            boat.transform.position = wall.EdgePoint + wall.PushDirection * (wall.LookFullDistanceMetres - 1f);
            boat.GetComponent<Rigidbody2D>().position = boat.transform.position;
            boat.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(veil.Showing, edge);
            Assert.Greater(veil.ShownLoom01, 0.9f, $"{edge}: the view is under the front");
            Assert.Greater(LocalWeather.Rain(0f), 0.5f, $"{edge}: it rains");
            Assert.IsTrue(LocalWeather.Active, edge);

            // The flag: the same water, the very next step, with him still sitting in it.
            _save.SetFlag(GateFlag, true);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsFalse(LocalWeather.Active, $"{edge}: no storm is drawn or heard");
            Assert.IsFalse(LocalWeather.SquallActive, $"{edge}: no front and no rain of its own");
            Assert.AreEqual(0f, LocalWeather.Rain(0f), edge);
            Assert.IsFalse(veil.Showing, $"{edge}: the dark is gone from the view");
            Assert.AreEqual(0f, _director.Look01, edge);
            Assert.AreEqual(0f, _director.PushAcceleration, edge);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AFastHull_IsHeldInsideTheLimit_NotThrown()
        {
            var wall = Wall("north");
            var boat = Boat("GoFast", wall, wall.StartDistanceMetres + 20f);
            float closest = 0f, longest = 0f, turn = 0f;
            yield return Drive(boat, wall, 120f, true, (c, l, t) => { closest = c; longest = l; turn = t; });
            Debug.Log($"[st-peters-walls] GoFast at the north edge came within {closest:F1} m; longest step {longest:F3} m");
            Assert.Greater(closest, StPetersBuilder.SeaDoorInsetMetres + 10f, "held well clear of the edge");
            Assert.Less(longest, 2f, $"carried, not thrown ({longest} m in one step)");
            AssertAfloatAndHers(boat, "GoFast");
        }

        // ---- open once the gate flag is set ----------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator WithTheGateFlagSet_TheSameDoryRowsOnToTheEdge(
            [ValueSource(nameof(Edges))] string edge)
        {
            _save.SetFlag(GateFlag, true);
            var wall = Wall(edge);
            var boat = Boat("Dory", wall, wall.LimitDistanceMetres + 15f);
            float closest = 0f, longest = 0f, turn = 0f;
            yield return Drive(boat, wall, 60f, true, (c, l, t) => { closest = c; longest = l; turn = t; });
            Assert.Less(closest, wall.LimitDistanceMetres - 30f, $"{edge}: she rowed on past the old limit ({closest} m)");
            Assert.IsNull(_director.Standing);
            Assert.AreEqual(0f, _director.Storm01);
            Assert.AreEqual(0f, _director.PushAcceleration);
            Assert.IsFalse(LocalWeather.Active, "no storm is drawn or heard");
            CollectionAssert.DoesNotContain(_lines, wall.Line);
        }

        // ---- a save that was never in the first week ---------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator WithoutTheFirstWeekKey_NoWallStands_TheDoryRowsOnToTheEdge(
            [ValueSource(nameof(Edges))] string edge)
        {
            _save.SetFlag(FirstWeekFlag, false);
            var wall = Wall(edge);
            Assert.IsFalse(CampaignBarrierDirector.Stands(wall), edge);
            var boat = Boat("Dory", wall, wall.LimitDistanceMetres + 15f);
            float closest = 0f, longest = 0f, turn = 0f;
            yield return Drive(boat, wall, 60f, true, (c, l, t) => { closest = c; longest = l; turn = t; });
            Assert.Less(closest, wall.LimitDistanceMetres - 30f, $"{edge}: she rowed on past the limit line ({closest} m)");
            Assert.IsNull(_director.Standing);
            Assert.AreEqual(0f, _director.Storm01);
            Assert.AreEqual(0f, _director.PushAcceleration);
            Assert.AreEqual(0f, _director.Look01);
            Assert.AreEqual(0f, _director.Loom01);
            Assert.IsFalse(LocalWeather.Active, "no storm is drawn or heard");
            Assert.IsFalse(LocalWeather.SquallActive, "no front and no rain");
            CollectionAssert.DoesNotContain(_lines, wall.Line);
        }

        // ---- the arrival -----------------------------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator TheArrivalBoat_SailsInThroughTheEastStorm_Unpushed()
        {
            var east = Wall("east");
            Vector2[] route = StPetersArrivalOpening.Route();
            GameServices.OpeningCinematicRunning = true;
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/CapeIslander.asset");
            var go = new GameObject("ArrivalBoat");
            _made.Add(go);
            go.transform.position = StPetersBuilder.FromEastWaterArrivalPos;
            Vector2 heading = (route[route.Length - 1] - route[0]).normalized;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, heading));
            var boat = go.AddComponent<BoatController>();
            boat.SetHull(hull);
            GameServices.Helm.SetPilotedHull(boat);
            GameServices.Helm.SetPlayersBoat(boat);
            Assert.Greater(east.Storm01At(go.transform.position), 0f, "she comes in inside the storm's reach");

            var wait = new WaitForFixedUpdate();
            bool cleared = false;
            for (int i = 0; i < Mathf.CeilToInt(60f / Time.fixedDeltaTime) && !cleared; i++)
            {
                boat.SetControl(0.6f, 0f);
                yield return wait;
                Assert.AreEqual(0f, _director.PushAcceleration, $"step {i}: the arrival is never pushed");
                Assert.IsFalse(LocalWeather.Active, $"step {i}");
                cleared = east.DistanceInside(go.transform.position) > east.StartDistanceMetres;
            }
            Assert.IsTrue(cleared, $"she ran in past the storm's start line ({east.DistanceInside(go.transform.position)} m)");
            CollectionAssert.IsEmpty(_lines, "nothing is thought during the arrival");
        }
    }
}
#endif
