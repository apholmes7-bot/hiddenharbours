using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The visible step of an NPC pot boat's day, on the real Def and the real owner's hull over a flat deep bed:
    /// she lies at her berth with her gear soaking, goes out, lies to each pot with it at her starboard rail,
    /// and the buoy, rope and pot are where the clock says, whenever the game is loaded.
    /// </summary>
    public class NpcPotBoatsPlayTests
    {
        const string DefPath = "Assets/_Project/Data/Traps/NmcDoironYvettePotDay.asset";
        const string OwnersFolder = "Assets/_Project/Data/Boats/Owners";
        const float BedElevation = -6f;
        const double Day = GameConfig.DefaultSecondsPerDay;
        const int WorkedDay = 2;

        sealed class Sea : IEnvironmentService
        {
            public float Level;
            /// <summary>Metres the water rises per clock second around <see cref="Clock"/>'s now.</summary>
            public float RisePerClockSecond;
            public SetClock Clock;
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => WaterLevelAt(totalSeconds);
            public float WaterLevelAt(double totalSeconds)
                => Level + RisePerClockSecond * (float)(totalSeconds - Clock.TotalSeconds);
        }

        sealed class SetClock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public bool IsPaused { get; set; } = true;
            public float TimeScale { get; set; }
            public int DayIndex => (int)System.Math.Floor(T / Day);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => (float)((T / Day) % 1.0);
            public float HourOfDay => DayFraction * 24f;
            public void SeekTo(double totalSeconds) => T = totalSeconds;
        }

        sealed class FlatBed : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => BedElevation;
        }

        readonly List<Object> _spawned = new();
        Sea _sea;
        SetClock _clock;
        NpcPotDayDef _def;
        MooredBoat _hull;
        NpcPotBoats _boats;
        NpcPotDay _today, _yesterday;
        bool _autoBindWas;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _clock = new SetClock { T = WorkedDay * Day + 5.0 };
            _sea = new Sea { Level = 0f, Clock = _clock };
            GameServices.Environment = _sea;
            GameServices.Clock = _clock;
            GameServices.TidalTerrain = new FlatBed();

            _autoBindWas = NpcPotBoats.AutoBind;
            NpcPotBoats.AutoBind = false;

#if UNITY_EDITOR
            _def = AssetDatabase.LoadAssetAtPath<NpcPotDayDef>(DefPath);
            Assert.IsNotNull(_def, DefPath);
            BoatOwnerDef owner = null;
            foreach (string guid in AssetDatabase.FindAssets("t:BoatOwnerDef", new[] { OwnersFolder }))
            {
                var o = AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (o != null && o.Id == _def.OwnerId) owner = o;
            }
            Assert.IsNotNull(owner, _def.OwnerId);

            var go = new GameObject($"Moored_{owner.Id}");
            go.SetActive(false);
            _spawned.Add(go);
            go.transform.position = new Vector3(_def.BerthPosition.x, _def.BerthPosition.y, 0f);
            _hull = go.AddComponent<MooredBoat>();
            _hull.Configure(owner, _def.BerthHeadingDegrees);
            go.SetActive(true);

            // The deck reads the catch off the species registry; a bare test scene has not booted it.
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data/Fish" }))
            {
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (fish != null) FishSpeciesRegistry.Register(fish);
            }
#else
            Assert.Ignore("Needs the AssetDatabase: this drives the real committed Def and owner.");
#endif

            _boats = NpcPotBoats.Instance;
            if (_boats == null)
            {
                var host = new GameObject("[NpcPotBoats] (test)");
                _spawned.Add(host);
                _boats = host.AddComponent<NpcPotBoats>();
            }

            _today = NpcPotDay.Build(_def, _sea.WorldSeed, WorkedDay, Day);
            _yesterday = NpcPotDay.Build(_def, _sea.WorldSeed, WorkedDay - 1, Day);
        }

        [TearDown]
        public void TearDown()
        {
            if (_boats != null && _def != null) _boats.Unbind(_def.Id);
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            NpcPotBoats.AutoBind = _autoBindWas;
            GameServices.Reset();
        }

        /// <summary>Moves the clock and waits past the presenter's slow tick, so tide and gear are re-read.</summary>
        IEnumerator At(double clockSeconds)
        {
            _clock.T = clockSeconds;
            float until = Time.unscaledTime + 0.3f;
            while (Time.unscaledTime < until) yield return null;
            yield return null;
            yield return null;
        }

        Vector2 Hull => _hull.transform.position;
        Vector2 Buoy(int i) => _boats.BuoyOf(_def.Id, i).position;
        bool BuoyShown(int i) => _boats.BuoyOf(_def.Id, i).gameObject.activeInHierarchy;

        Vector2 Rail(NpcPotBoatPose pose) =>
            pose.Position + NpcPotDay.Starboard(pose.HeadingDegrees) * _def.HaulerLocalMeters.x
                          + NpcPotDay.Along(pose.HeadingDegrees) * _def.HaulerLocalMeters.y;

        [UnityTest]
        public IEnumerator BeforeSheCastsOff_SheLiesAtHerBerth_AndHerGearIsSoakingWhereYesterdaySetIt()
        {
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            yield return At(_today.DayStartSeconds + 5.0);

            Assert.That((Hull - _def.BerthPosition).magnitude, Is.LessThan(0.01f));
            Assert.That(_hull.Way, Is.EqualTo(VesselWay.Moored));
            float reach = PotRopeMath.Scope(_def.RopeLengthMeters, -BedElevation);
            for (int i = 0; i < _def.PotCount; i++)
            {
                Assert.IsTrue(BuoyShown(i), $"pot {i}'s buoy is on the water");
                Assert.That((Buoy(i) - _yesterday.SetPositions[i]).magnitude, Is.LessThanOrEqualTo(reach + 0.5f),
                            $"pot {i}'s buoy floats within its rope of where yesterday set the pot");
                Assert.IsTrue(_boats.RopeOf(_def.Id, i).enabled, $"pot {i} has a rope");
                Assert.IsFalse(_boats.PotOf(_def.Id, i).enabled, $"pot {i} is on the bottom in {-BedElevation} m, out of sight");
            }
            Color worn = _boats.BuoyOf(_def.Id, 0).GetComponentInChildren<SpriteRenderer>().color;
            Assert.That(worn.r, Is.GreaterThan(worn.g + 0.2f), "the float wears her red, not the player's yellow");
        }

        [UnityTest]
        public IEnumerator OnPassage_SheIsUnderWay_AlongTheDaysTrack()
        {
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            double t = _today.DepartSeconds + 30.0;
            yield return At(t);

            NpcPotBoatPose pose = _today.PoseAt(t);
            Assert.That(pose.Stage, Is.EqualTo(NpcPotBoatStage.PassageOut));
            Assert.That((Hull - pose.Position).magnitude, Is.LessThan(0.01f), "the hull is where the clock says");
            Assert.That((Hull - _def.BerthPosition).magnitude, Is.GreaterThan(10f), "and that is not her berth");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(-_hull.transform.eulerAngles.z, pose.HeadingDegrees)), Is.LessThan(0.5f),
                        "her bow points down the leg");
            Assert.That(_hull.Way, Is.EqualTo(VesselWay.UnderWay));
        }

        [UnityTest]
        public IEnumerator Hauling_ThePotComesUpTheRope_ToHerStarboardRail_ThenSitsAboard()
        {
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            const int pot = 2;

            double gaffing = _today.GaffStart[pot] + 0.9 * _def.GaffSeconds;
            yield return At(gaffing);
            NpcPotBoatPose pose = _today.PoseAt(gaffing);
            Assert.That((Hull - pose.Position).magnitude, Is.LessThan(0.01f));
            Assert.That(Vector2.Dot(_yesterday.SetPositions[pot] - Hull, NpcPotDay.Starboard(pose.HeadingDegrees)),
                        Is.EqualTo(_def.LayAlongsideMeters).Within(0.05f), "she lies with the pot off her starboard side");
            Assert.IsTrue(BuoyShown(pot));
            Assert.That((Buoy(pot) - Rail(pose)).magnitude, Is.LessThan(0.6f), "the gaff has the buoy almost at the rail");

            double early = _today.HaulStart[pot] + 0.1 * _def.HaulSeconds;
            yield return At(early);
            Assert.IsFalse(BuoyShown(pot), "the buoy is aboard");
            LineRenderer rope = _boats.RopeOf(_def.Id, pot);
            Assert.IsTrue(rope.enabled, "the rope runs from the rail down to the pot");
            Assert.That(((Vector2)rope.GetPosition(0) - Rail(_today.PoseAt(early))).magnitude, Is.LessThan(0.05f));
            float deepY = _boats.PotOf(_def.Id, pot).transform.position.y;

            double late = _today.HaulStart[pot] + 0.95 * _def.HaulSeconds;
            yield return At(late);
            SpriteRenderer potSprite = _boats.PotOf(_def.Id, pot);
            Assert.IsTrue(potSprite.enabled, "near the surface the pot shows through the water");
            Assert.That(potSprite.sprite, Is.EqualTo(_def.Trap.TrapSpriteWet));
            Assert.That(potSprite.transform.position.y, Is.GreaterThan(deepY), "it has come up");
            Assert.That(((Vector2)potSprite.transform.position - Rail(_today.PoseAt(late))).magnitude, Is.LessThan(0.6f));

            double aboard = _today.HaulEnd[pot] + 0.5 * _def.DeckWorkSeconds;
            yield return At(aboard);
            Assert.That(_today.PoseAt(aboard).Stage, Is.EqualTo(NpcPotBoatStage.DeckWork));
            Assert.IsFalse(BuoyShown(pot));
            Assert.IsFalse(rope.enabled);
            Assert.IsTrue(potSprite.enabled, "the pot sits at the rail while it is worked");
            Assert.That(potSprite.sprite, Is.EqualTo(_def.Trap.TrapSprite));
            Assert.That(potSprite.color.a, Is.EqualTo(1f).Within(0.01f));

            for (int i = 0; i < _def.PotCount; i++)
            {
                if (i == pot) continue;
                Vector2 lies = i < pot ? _today.SetPositions[i] : _yesterday.SetPositions[i];
                Assert.IsTrue(BuoyShown(i), $"pot {i} is still fishing");
                Assert.That((Buoy(i) - lies).magnitude, Is.LessThan(1f),
                            i < pot ? $"pot {i} was set again today" : $"pot {i} has not been hauled yet");
            }
        }

        [UnityTest]
        public IEnumerator Setting_ThePotGoesOver_AndTheBuoyFollowsItToTodaysMark()
        {
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            const int pot = 1;

            yield return At(_today.SetStart[pot] + 0.2 * _def.SetSeconds);
            Assert.IsFalse(BuoyShown(pot), "the pot goes first");
            Assert.IsTrue(_boats.RopeOf(_def.Id, pot).enabled, "paying out rope");

            yield return At(_today.SetEnd[pot] + 1.0);
            Assert.IsTrue(BuoyShown(pot));
            Assert.That((Buoy(pot) - _today.SetPositions[pot]).magnitude, Is.LessThan(1f), "the buoy is over today's set");
            Assert.That(_today.SetPositions[pot], Is.Not.EqualTo(_yesterday.SetPositions[pot]), "which is not where it was hauled from");
            Assert.IsFalse(_boats.PotOf(_def.Id, pot).enabled, "the pot is on the bottom again");
        }

        [UnityTest]
        public IEnumerator TheBuoy_StreamsWithTheFlood_AndBackWithTheEbb()
        {
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            double t = _today.DayStartSeconds + 5.0;
            float gameHour = (float)(Day / 24.0);
            Vector2 pot = _yesterday.SetPositions[0];
            Vector2 flood = NpcPotDay.Along(_def.FloodBearingDegrees);

            _sea.RisePerClockSecond = 0f;
            yield return At(t);
            float slack = Vector2.Dot(Buoy(0) - pot, flood);

            _sea.RisePerClockSecond = 2f * _def.FullStreamRiseMetersPerHour / gameHour;
            yield return At(t);
            float onTheFlood = Vector2.Dot(Buoy(0) - pot, flood);

            _sea.RisePerClockSecond = -2f * _def.FullStreamRiseMetersPerHour / gameHour;
            yield return At(t);
            float onTheEbb = Vector2.Dot(Buoy(0) - pot, flood);

            float reach = PotRopeMath.Scope(_def.RopeLengthMeters, -BedElevation) * _def.StreamLean01;
            Assert.That(slack, Is.EqualTo(0f).Within(0.3f), "at slack the buoy sits over its pot");
            Assert.That(onTheFlood, Is.EqualTo(reach).Within(0.3f), "the flood streams it down-tide");
            Assert.That(onTheEbb, Is.EqualTo(-reach).Within(0.3f), "the ebb streams it the other way");

            _sea.RisePerClockSecond = 0f;
            _sea.Level = 2f;
            yield return At(t);
            _sea.RisePerClockSecond = 2f * _def.FullStreamRiseMetersPerHour / gameHour;
            yield return At(t);
            float atHighWater = Vector2.Dot(Buoy(0) - pot, flood);
            Assert.That(atHighWater, Is.LessThan(onTheFlood - 0.3f), "with more water over the pot there is less rope to stream on");
        }

        [UnityTest]
        public IEnumerator LoadedMidDay_BoatAndGearAreWhereTheClockSays()
        {
            double t = _today.HaulEnd[3] + 4.0;

            Assert.IsTrue(_boats.TryBind(_def, _hull));
            yield return At(_today.DayStartSeconds + 5.0);
            yield return At(_today.DepartSeconds + 20.0);
            yield return At(_today.GaffStart[1] + 1.0);
            yield return At(t);
            Vector2 hullPlayedThrough = Hull;
            float headingPlayedThrough = _hull.transform.eulerAngles.z;
            var buoysPlayedThrough = new Vector2[_def.PotCount];
            var shownPlayedThrough = new bool[_def.PotCount];
            for (int i = 0; i < _def.PotCount; i++) { buoysPlayedThrough[i] = Buoy(i); shownPlayedThrough[i] = BuoyShown(i); }

            _boats.Unbind(_def.Id);
            yield return null;
            Assert.That((Hull - _def.BerthPosition).magnitude, Is.LessThan(0.01f), "unbound, she is put back at her berth");
            Assert.IsNull(_boats.BuoyOf(_def.Id, 0), "and her gear is gone with the binding");

            Assert.IsTrue(_boats.TryBind(_def, _hull));
            yield return At(t);
            Assert.That((Hull - hullPlayedThrough).magnitude, Is.LessThan(0.001f), "a fresh load puts the hull where a played day had it");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(_hull.transform.eulerAngles.z, headingPlayedThrough)), Is.LessThan(0.3f));
            for (int i = 0; i < _def.PotCount; i++)
            {
                Assert.That(BuoyShown(i), Is.EqualTo(shownPlayedThrough[i]), $"pot {i}'s buoy");
                if (shownPlayedThrough[i])
                    Assert.That((Buoy(i) - buoysPlayedThrough[i]).magnitude, Is.LessThan(0.3f), $"pot {i}'s buoy (it bobs)");
            }
        }

        [UnityTest]
        public IEnumerator OnDeck_EveryKeeperGoesThroughHerHands_RailToBenchToItsCrate()
        {
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            yield return At(_today.DepartSeconds + 30.0);
            NpcPotDeck deck = _boats.DeckOf(_def.Id);
            NpcPotDayCatch caught = _boats.CatchOf(_def.Id);
            Assert.IsNotNull(deck);
            Assert.IsNotNull(caught);
            Assert.That((deck.StandDeckMeters - deck.HelmDeckMeters).magnitude, Is.LessThan(0.01f), "on passage she is at the helm");
            Assert.IsFalse(deck.InHand.enabled, "with nothing in her hands");
            for (int g = 0; g < deck.GradeCount; g++) Assert.That(deck.ShownInCrate(g), Is.EqualTo(0), "and empty crates");

            int pot = -1, keeper = -1;
            for (int p = 0; p < caught.PotCount && pot < 0; p++)
                for (int k = 0; k < caught.Animals(p).Count && pot < 0; k++)
                    if (caught.Animals(p)[k].Keeper) { pot = p; keeper = k; }
            Assert.That(pot, Is.GreaterThanOrEqualTo(0), "seed 4242 day 2 lands at least one keeper");
            NpcPotAnimal animal = caught.Animals(pot)[keeper];

            yield return At(_today.HaulStart[pot] + 1.0);
            Assert.That((deck.StandDeckMeters - deck.RailDeckMeters).magnitude, Is.LessThan(0.01f), "she hauls from the rail");

            yield return At(animal.PickedAt + 0.9 * _def.PickSeconds);
            Assert.That(deck.Step, Is.EqualTo(NpcDeckStep.Picking));
            Assert.That((deck.StandDeckMeters - deck.RailDeckMeters).magnitude, Is.LessThan(0.01f));
            if (animal.Sprite != null)
            {
                Assert.IsTrue(deck.InHand.enabled, "the animal is in her hand");
                Assert.That(deck.InHand.sprite, Is.EqualTo(animal.Sprite));
            }
            int before = deck.ShownInCrate(animal.Grade);
            Assert.That(before, Is.EqualTo(caught.StowedAt(animal.PickedAt, animal.Grade)));

            yield return At(animal.PickedAt + _def.PickSeconds + 0.8 * _def.BandSeconds);
            Assert.That(deck.Step, Is.EqualTo(NpcDeckStep.Banding));
            Assert.That((deck.StandDeckMeters - deck.BenchDeckMeters).magnitude, Is.LessThan(0.01f), "she bands it at the bench");
            Assert.That(deck.ShownInCrate(animal.Grade), Is.EqualTo(before), "not stowed while it is still in her hand");

            yield return At(animal.PickedAt + _def.PickSeconds + _def.BandSeconds + 0.5 * _def.StowSeconds);
            Assert.That(deck.Step, Is.EqualTo(NpcDeckStep.Stowing));
            Vector2 crate = deck.CrateDeckMeters(animal.Grade);
            Assert.That((deck.StandDeckMeters - (deck.BenchDeckMeters + crate) * 0.5f).magnitude, Is.LessThan(0.05f),
                        "half way through the carry she is half way to the crate");
            if (animal.Sprite != null)
                Assert.That(((Vector2)deck.InHand.transform.localPosition - deck.StandDeckMeters).magnitude, Is.LessThan(0.01f),
                            "and the animal is where her hand is");

            yield return At(animal.DoneAt + 0.05);
            Assert.That(deck.ShownInCrate(animal.Grade), Is.EqualTo(before + 1), "it is in the crate for its grade");

            yield return At(_today.ReturnSeconds + 5.0);
            int shown = 0, expected = 0;
            for (int g = 0; g < deck.GradeCount; g++)
            {
                shown += deck.ShownInCrate(g);
                expected += Mathf.Min(caught.StowedAt(_today.ReturnSeconds, g), _def.CrateShownCapacity);
            }
            Assert.That(shown, Is.EqualTo(expected), "home, her crates hold the day's keepers");
            Assert.That(shown, Is.GreaterThan(0));
            Assert.IsFalse(deck.InHand.enabled);
            Assert.That((deck.StandDeckMeters - deck.HelmDeckMeters).magnitude, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator HomeAgain_SheIsMooredAtHerBerth_AndTomorrowSheHaulsTodaysSet()
        {
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            yield return At(_today.ReturnSeconds + 5.0);
            Assert.That((Hull - _def.BerthPosition).magnitude, Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(-_hull.transform.eulerAngles.z, _def.BerthHeadingDegrees)), Is.LessThan(0.5f));
            Assert.That(_hull.Way, Is.EqualTo(VesselWay.Moored));

            yield return At((WorkedDay + 1) * Day + 5.0);
            for (int i = 0; i < _def.PotCount; i++)
                Assert.That((Buoy(i) - _today.SetPositions[i]).magnitude, Is.LessThan(1f), $"pot {i} lies overnight where today set it");
        }
    }
}
