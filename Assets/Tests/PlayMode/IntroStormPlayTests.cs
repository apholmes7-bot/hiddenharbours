#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The intro storm against real hulls. The self-installed director and the committed wall are the
    /// ones under test; the fixture gives them the East Water's region id and own bed, a held fair
    /// weather, a save whose flags it can set, and a boat driven flat out at the south edge.
    /// </summary>
    public class IntroStormPlayTests
    {
        const float SimSpeed = 8f;

        static readonly string[] Hulls = { "Dory", "DoryOutboard", "CapeIslander", "GoFast" };

        sealed class FlagSave : ISaveService
        {
            readonly HashSet<string> _flags = new HashSet<string>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.Contains(key);
            public void SetFlag(string key, bool value) { if (value) _flags.Add(key); else _flags.Remove(key); }
            public void Save() { }
        }

        sealed class FairWeather : IEnvironmentService
        {
            public int Samples;
            public static readonly EnvironmentSample Fair = new EnvironmentSample(
                new Vector2(1.5f, 0.5f), Vector2.zero, 0f, SeaState.Calm, 1f, 0.05f);
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() { Samples++; return Fair; }
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        readonly List<Object> _made = new List<Object>();
        readonly List<string> _lines = new List<string>();
        CampaignBarrierDirector _director;
        CampaignBarrierDef _wall;
        FlagSave _save;
        FairWeather _weather;
        RectTidalTerrain _bed;
        float _timeScale;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _timeScale = Time.timeScale;
            Time.timeScale = SimSpeed;

            _director = CampaignBarrierDirector.Instance;
            Assert.IsNotNull(_director, "the director installs itself; no scene places it");
            _wall = Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder)
                .Single(w => w.RegionId == EastWaterPlan.RegionId);
            _director.LoadWalls(Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder));

            _save = new FlagSave();
            _weather = new FairWeather();
            GameServices.Save = _save;
            GameServices.Environment = _weather;
            GameServices.CurrentRegionId = EastWaterPlan.RegionId;

            var bedGo = new GameObject("EastWaterBed");
            _made.Add(bedGo);
            bedGo.SetActive(false);
            _bed = bedGo.AddComponent<RectTidalTerrain>();
            EastWaterPlan.Plan.ConfigureTerrain(_bed);
            bedGo.SetActive(true);
            GameServices.TidalTerrain = _bed;

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

        BoatController Boat(string hullName, float metresInsideTheEdge, bool headingSouth = true)
        {
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"Assets/_Project/Data/Boats/{hullName}.asset");
            Assert.IsNotNull(hull, hullName);
            var go = new GameObject("StormBoat_" + hullName);
            _made.Add(go);
            go.transform.position = _wall.EdgePoint + _wall.PushDirection * metresInsideTheEdge;
            go.transform.rotation = Quaternion.Euler(0f, 0f, headingSouth ? 180f : 0f);
            var boat = go.AddComponent<BoatController>();
            boat.SetHull(hull);
            GameServices.Helm.SetPilotedHull(boat);
            GameServices.Helm.SetPlayersBoat(boat);
            return boat;
        }

        float Inside(BoatController boat) => _wall.DistanceInside(boat.transform.position);

        /// <summary>Drive her flat out (motor and oars both asked; she uses the one she has) for
        /// <paramref name="seconds"/> of game time. Returns the closest she came to the edge and the
        /// furthest she moved in one physics step.</summary>
        IEnumerator Drive(BoatController boat, float seconds, bool power, System.Action<float, float> report)
        {
            float closest = Inside(boat), longestStep = 0f;
            Vector2 last = boat.transform.position;
            int steps = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            var wait = new WaitForFixedUpdate();
            for (int i = 0; i < steps; i++)
            {
                boat.SetControl(power ? 1f : 0f, 0f);
                boat.SetOarInput(power ? 1f : 0f, power ? 1f : 0f, false);
                yield return wait;
                Vector2 now = boat.transform.position;
                closest = Mathf.Min(closest, Inside(boat));
                longestStep = Mathf.Max(longestStep, Vector2.Distance(last, now));
                last = now;
            }
            report(closest, longestStep);
        }

        void AssertInsideTheEastWaterAfloat(BoatController boat, string hullName)
        {
            Vector2 at = boat.transform.position;
            Assert.IsTrue(EastWaterPlan.Plan.WorldBounds.Contains(at), $"{hullName} at {at}");
            float water = 0f - GameServices.TidalTerrain.ElevationAt(at);
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"Assets/_Project/Data/Boats/{hullName}.asset");
            Assert.Greater(water, hull.DraughtMeters, $"{hullName}: {water} m of water under her");
            Assert.IsFalse(boat.IsAground, hullName);
            Assert.AreEqual(RigidbodyType2D.Dynamic, boat.GetComponent<Rigidbody2D>().bodyType, hullName);
            Assert.AreSame(boat, GameServices.Helm.PilotedHull, $"{hullName}: she still has her helm");
        }

        // ---- blocked before the step ----------------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator BlockedBeforeTheDoryIsRepaired_EveryHullDrivenSouthIsTurnedBack(
            [ValueSource(nameof(Hulls))] string hullName)
        {
            float doorInnerFace = EastWaterPlan.DoorInsetMetres + SouthWaterPlan.PassageBandSize.y * 0.5f;
            var boat = Boat(hullName, _wall.StartDistanceMetres + 30f);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(0f, _director.Storm01, "fair weather north of the start line");
            Assert.IsFalse(LocalWeather.Active);

            float closest = 0f, longest = 0f;
            yield return Drive(boat, 150f, true, (c, l) => { closest = c; longest = l; });

            Assert.AreSame(_wall, _director.Standing);
            Assert.Less(closest, _wall.StartDistanceMetres, $"{hullName} reached the storm ({closest} m)");
            Assert.Greater(closest, doorInnerFace + 10f,
                $"{hullName} was held {closest} m inside the edge, clear of the door at {doorInnerFace} m");
            Assert.Less(longest, 2f, $"{hullName}: nothing threw her ({longest} m in one step)");
            AssertInsideTheEastWaterAfloat(boat, hullName);
            Assert.IsTrue(LocalWeather.Active, "the storm is over her");
            CollectionAssert.Contains(_lines, _wall.Line);

            // Hands off: the storm sets her back north of the limit line and leaves her there afloat.
            yield return Drive(boat, 60f, false, (c, l) => { longest = l; });
            Assert.GreaterOrEqual(Inside(boat), _wall.LimitDistanceMetres - 0.5f, hullName);
            Assert.Less(longest, 2f, hullName);
            AssertInsideTheEastWaterAfloat(boat, hullName);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ABoatFoundPastTheLimit_IsCarriedBackInsideIt_NotThrown()
        {
            var boat = Boat("Dory", 20f);
            float closest = 0f, longest = 0f;
            yield return Drive(boat, 90f, false, (c, l) => { closest = c; longest = l; });
            Assert.GreaterOrEqual(closest, 19.5f, "she never went further out");
            Assert.Less(longest, 2f, $"carried, not thrown ({longest} m in one step)");
            Assert.GreaterOrEqual(Inside(boat), _wall.LimitDistanceMetres - 0.5f);
            AssertInsideTheEastWaterAfloat(boat, "Dory");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SheThinksTheLineOnce_NotEveryStep()
        {
            var boat = Boat("DoryOutboard", _wall.FullDistanceMetres);
            float c = 0f, l = 0f;
            yield return Drive(boat, 20f, true, (a, b) => { c = a; l = b; });
            Assert.AreEqual(1, _lines.Count(x => x == _wall.Line), "one line inside its cooldown");
            Assert.Less(20f, _wall.LineCooldownSeconds);
        }

        // ---- open after -----------------------------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator OpenAfterTheDoryIsRepaired_TheSameHullSailsOnToTheEdge(
            [ValueSource(nameof(Hulls))] string hullName)
        {
            _save.SetFlag(_wall.LiftedByFlag, true);
            float doorInnerFace = EastWaterPlan.DoorInsetMetres + SouthWaterPlan.PassageBandSize.y * 0.5f;
            var boat = Boat(hullName, _wall.LimitDistanceMetres + 15f);
            float closest = 0f, longest = 0f;
            yield return Drive(boat, 150f, true, (c, l) => { closest = c; longest = l; });

            Assert.Less(closest, doorInnerFace, $"{hullName} reached where the door will be ({closest} m)");
            Assert.IsNull(_director.Standing);
            Assert.AreEqual(0f, _director.Storm01);
            Assert.AreEqual(0f, _director.PushAcceleration);
            Assert.IsFalse(LocalWeather.Active, "no storm is drawn or heard");
            CollectionAssert.DoesNotContain(_lines, _wall.Line);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheStormLiftsTheStepTheFlagIsSet_WithHerStillInIt()
        {
            var boat = Boat("Dory", _wall.FullDistanceMetres - 10f, headingSouth: false);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(1f, _director.Storm01);
            Assert.Greater(_director.PushAcceleration, 0f);
            Assert.IsTrue(LocalWeather.Active);

            _save.SetFlag(OnboardingFlags.OnboardedKey, true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(0f, _director.Storm01);
            Assert.AreEqual(0f, _director.PushAcceleration);
            Assert.IsFalse(LocalWeather.Active);
            Assert.IsTrue(EastWaterPlan.Plan.WorldBounds.Contains((Vector2)boat.transform.position));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NoWallStandsInAnotherRegion_OrWithNoSave()
        {
            var boat = Boat("Dory", _wall.FullDistanceMetres - 10f, headingSouth: false);
            GameServices.CurrentRegionId = SouthWaterPlan.RegionId;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsNull(_director.Standing);
            Assert.IsFalse(LocalWeather.Active);

            GameServices.CurrentRegionId = EastWaterPlan.RegionId;
            GameServices.Save = null;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsNull(_director.Standing, "a fixture or the shell, with no save, has no wall");
            Assert.AreEqual(0f, _director.PushAcceleration);
            Assert.IsNotNull(boat);
        }

        // ---- no change to the sim's weather at any time --------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator TheSimsWeatherIsTheSame_BeforeInsideAndAfterTheStorm()
        {
            var boat = Boat("DoryOutboard", _wall.StartDistanceMetres + 30f);
            yield return new WaitForFixedUpdate();
            AssertFair("north of the storm");

            float closest = 0f, longest = 0f;
            var wait = new WaitForFixedUpdate();
            int steps = Mathf.CeilToInt(120f / Time.fixedDeltaTime);
            bool sawStorm = false;
            for (int i = 0; i < steps; i++)
            {
                boat.SetControl(1f, 0f);
                yield return wait;
                AssertFair("step " + i);
                if (LocalWeather.Active)
                {
                    sawStorm = true;
                    var seen = LocalWeather.Shade(GameServices.Environment.Sample());
                    Assert.Greater(seen.WindVector.magnitude, FairWeather.Fair.WindVector.magnitude);
                    Assert.AreEqual(FairWeather.Fair.SeaState, seen.SeaState);
                }
            }
            Assert.IsTrue(sawStorm, "she was in the storm for part of the run");

            _save.SetFlag(_wall.LiftedByFlag, true);
            yield return Drive(boat, 5f, false, (c, l) => { closest = c; longest = l; });
            AssertFair("after it lifted");
            Assert.IsFalse(LocalWeather.Active);
        }

        void AssertFair(string when)
        {
            Assert.AreSame(_weather, GameServices.Environment, when + ": the sim is not replaced or wrapped");
            var s = GameServices.Environment.Sample();
            Assert.AreEqual(FairWeather.Fair.WindVector, s.WindVector, when);
            Assert.AreEqual(FairWeather.Fair.CurrentVector, s.CurrentVector, when);
            Assert.AreEqual(FairWeather.Fair.TideHeight, s.TideHeight, when);
            Assert.AreEqual(FairWeather.Fair.SeaState, s.SeaState, when);
            Assert.AreEqual(FairWeather.Fair.Visibility, s.Visibility, when);
            Assert.AreEqual(FairWeather.Fair.SeaState01, s.SeaState01, when);
        }
    }
}
#endif
