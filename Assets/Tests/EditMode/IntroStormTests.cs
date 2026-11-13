using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The intro storm as data and arithmetic: the committed wall stands on the East Water's south
    /// edge and lifts on the dory's repair; its storm and push are smooth functions of where the boat
    /// is; and Core's local weather shade changes what is drawn and heard, never the sim's own sample.
    /// </summary>
    public class IntroStormTests
    {
        const string WallId = "campaign_barrier.intro_storm_east_water_south";

        sealed class HeldWeather : IEnvironmentService
        {
            public int Samples;
            public EnvironmentSample Held = new EnvironmentSample(new Vector2(2f, 1f), new Vector2(0.1f, 0f),
                0.7f, SeaState.Light, 0.9f, 0.15f);
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() { Samples++; return Held; }
            public float TideHeightAt(double totalSeconds) => Held.TideHeight;
            public float WaterLevelAt(double totalSeconds) => Held.TideHeight;
        }

        sealed class HeldHistory : IWaveWindSource
        {
            public WaveWindSample WaveWindAt(double t) => new WaveWindSample(new Vector2(3f, 0f), 0.2f);
        }

        static CampaignBarrierDef[] Walls() =>
            AssetDatabase.FindAssets("t:CampaignBarrierDef", new[] { "Assets/_Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CampaignBarrierDef>)
                .Where(w => w != null).ToArray();

        static CampaignBarrierDef Wall() => Walls().Single(w => w.Id == WallId);

        [SetUp] public void SetUp() => GameServices.Reset();
        [TearDown] public void TearDown() => GameServices.Reset();

        // ---- the committed wall ---------------------------------------------------------------------

        [Test]
        public void EveryWallIsLoadedFromResourcesAndHasAUniquePrefixedId()
        {
            var walls = Walls();
            Assert.IsNotEmpty(walls);
            var loaded = Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder);
            CollectionAssert.AreEquivalent(walls, loaded, "a wall outside the Resources folder never stands");
            foreach (var w in walls)
                Assert.That(w.Id, Does.Match("^campaign_barrier\\.[a-z0-9_]+$"), AssetDatabase.GetAssetPath(w));
            Assert.AreEqual(walls.Length, walls.Select(w => w.Id).Distinct().Count());
        }

        [Test]
        public void TheIntroStorm_StandsOnTheEastWatersSouthEdge_AndLiftsWhenTheDoryIsRepaired()
        {
            var wall = Wall();
            Assert.AreEqual(EastWaterPlan.RegionId, wall.RegionId);
            float southEdge = EastWaterPlan.RegionWorldCenter.y - EastWaterPlan.RegionWorldSize.y * 0.5f;
            Assert.AreEqual(southEdge, wall.EdgePoint.y, 1e-4f);
            Assert.AreEqual(Vector2.up, wall.PushDirection, "it pushes north, back into the East Water");
            Assert.AreEqual(0f, wall.HalfLengthMetres, "the whole south edge");
            Assert.AreEqual(OnboardingFlags.OnboardedKey, wall.LiftedByFlag);
        }

        [Test]
        public void EveryWallSaysOnePlainLine()
        {
            foreach (var w in Walls())
            {
                Assert.IsNotEmpty(w.Line, w.Id);
                Assert.Less(w.Line.Length, 120, w.Id + ": one bubble's worth");
                Assert.Greater(w.LineCooldownSeconds, 0f, w.Id);
            }
        }

        [Test]
        public void TheLinesRunInwardInOrder_AndTheLimitLiesWellInsideTheSouthWatersDoor()
        {
            var wall = Wall();
            Assert.Greater(wall.StartDistanceMetres, wall.FullDistanceMetres);
            Assert.Greater(wall.FullDistanceMetres, wall.LimitDistanceMetres);
            // The exit a neighbour lane will add: inset 24 m from the edge, 6 m across (region-seam-audit.md).
            float doorInnerFace = EastWaterPlan.DoorInsetMetres + SouthWaterPlan.PassageBandSize.y * 0.5f;
            Assert.Greater(wall.LimitDistanceMetres, doorInnerFace + 20f);
            // Well before the door the push is at its most; IntroStormPlayTests proves that beats every hull.
            Vector2 atDoor = wall.EdgePoint + wall.PushDirection * (doorInnerFace + 20f);
            Assert.AreEqual(wall.MaxPushAcceleration, wall.PushAccelerationAt(atDoor, Vector2.zero), 1e-5f);
            Assert.Greater(wall.MaxPushAcceleration, wall.PushAcceleration);
        }

        [Test]
        public void TheStartLineIsInsideTheRegion_AndTheOpeningLieIsClearOfTheStorm()
        {
            var wall = Wall();
            Assert.Less(wall.StartDistanceMetres, EastWaterPlan.RegionWorldSize.y * 0.5f,
                "the north half of the East Water is never under it");
            Assert.AreEqual(0f, wall.Storm01At(EastWaterPlan.OpeningLiePos));
            Assert.AreEqual(0f, wall.Storm01At(EastWaterPlan.FromStPetersArrivalPos));
        }

        // ---- the arithmetic -------------------------------------------------------------------------

        [Test]
        public void TheStormGrowsSmoothlyFromTheStartLineToTheFullLine_AndHoldsToTheEdge()
        {
            var wall = Wall();
            float last = 0f;
            for (float d = wall.StartDistanceMetres + 20f; d >= -20f; d -= 1f)
            {
                float s = wall.Storm01At(wall.EdgePoint + wall.PushDirection * d);
                Assert.That(s, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(s, last - 1e-6f, $"at {d} m");
                Assert.Less(s - last, 0.05f, $"no step in the sky at {d} m");
                last = s;
            }
            Assert.AreEqual(0f, wall.Storm01At(wall.EdgePoint + wall.PushDirection * wall.StartDistanceMetres));
            Assert.AreEqual(1f, wall.Storm01At(wall.EdgePoint + wall.PushDirection * wall.FullDistanceMetres));
            Assert.AreEqual(1f, wall.Storm01At(wall.EdgePoint - wall.PushDirection * 50f), "and beyond the edge");
        }

        [Test]
        public void ThePushIsNeverNegative_GrowsTowardTheEdge_AndBrakesOnlySpeedTowardIt()
        {
            var wall = Wall();
            float last = 0f;
            for (float d = wall.StartDistanceMetres + 10f; d >= 0f; d -= 1f)
            {
                float a = wall.PushAccelerationAt(wall.EdgePoint + wall.PushDirection * d, Vector2.zero);
                Assert.GreaterOrEqual(a, last - 1e-5f, $"at {d} m");
                last = a;
            }
            Vector2 past = wall.EdgePoint + wall.PushDirection * (wall.LimitDistanceMetres - 1f);
            float still = wall.PushAccelerationAt(past, Vector2.zero);
            Assert.Greater(wall.PushAccelerationAt(past, -wall.PushDirection * 3f), still, "running at the edge");
            Assert.AreEqual(still, wall.PushAccelerationAt(past, wall.PushDirection * 3f), 1e-5f, "coming home");
            Vector2 calm = wall.EdgePoint + wall.PushDirection * (wall.StartDistanceMetres + 1f);
            Assert.AreEqual(0f, wall.PushAccelerationAt(calm, -wall.PushDirection * 10f));
        }

        [Test]
        public void AWallOfAGivenLength_IsNotFeltBeyondItsEnds()
        {
            var wall = Object.Instantiate(Wall());
            try
            {
                wall.HalfLengthMetres = 100f;
                Vector2 deep = wall.EdgePoint + wall.PushDirection * 70f;
                Assert.AreEqual(1f, wall.Storm01At(deep));
                Assert.AreEqual(1f, wall.Storm01At(deep + new Vector2(99f, 0f)));
                Assert.AreEqual(0f, wall.Storm01At(deep + new Vector2(101f, 0f)));
                Assert.AreEqual(0f, wall.PushAccelerationAt(deep + new Vector2(-101f, 0f), Vector2.zero));
            }
            finally { Object.DestroyImmediate(wall); }
        }

        // ---- the look is shaded; the sim is not -----------------------------------------------------

        [Test]
        public void TheShade_ChangesWhatIsDrawnAndHeard_NeverTheSimsOwnSample()
        {
            var wall = Wall();
            var env = new HeldWeather();
            GameServices.Environment = env;
            EnvironmentSample before = GameServices.Environment.Sample();

            LocalWeather.Set(new LocalWeatherShade(1f, wall.StormWind, wall.StormVisibility01, wall.StormSeaState01));
            Assert.AreSame(env, GameServices.Environment, "the sim is not wrapped or replaced");
            EnvironmentSample during = GameServices.Environment.Sample();
            AssertSame(before, during);

            EnvironmentSample seen = LocalWeather.Shade(during);
            Assert.AreEqual(during.WindVector + wall.StormWind, seen.WindVector);
            Assert.AreEqual(wall.StormVisibility01, seen.Visibility, 1e-5f);
            Assert.AreEqual(wall.StormSeaState01, seen.SeaState01, 1e-5f);
            Assert.AreEqual(during.SeaState, seen.SeaState, "the stepped sea state gameplay gates on is the sim's");
            Assert.AreEqual(during.TideHeight, seen.TideHeight);
            Assert.AreEqual(during.CurrentVector, seen.CurrentVector);

            LocalWeather.Clear();
            AssertSame(before, LocalWeather.Shade(GameServices.Environment.Sample()));
        }

        [Test]
        public void TheShade_ComesOnWithItsStrength_AndNeverClearsFogOrCalmsASea()
        {
            var thick = new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, SeaState.Storm, 0.05f, 1f);
            LocalWeather.Set(new LocalWeatherShade(1f, Vector2.up * 20f, 0.2f, 0.9f));
            Assert.AreEqual(0.05f, LocalWeather.Shade(thick).Visibility, 1e-6f);
            Assert.AreEqual(1f, LocalWeather.Shade(thick).SeaState01, 1e-6f);

            var fair = new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, SeaState.Calm, 1f, 0f);
            float lastSea = 0f, lastVisibility = 1f;
            for (int i = 0; i <= 10; i++)
            {
                LocalWeather.Set(new LocalWeatherShade(i / 10f, Vector2.up * 20f, 0.2f, 0.9f));
                var seen = LocalWeather.Shade(fair);
                Assert.GreaterOrEqual(seen.SeaState01, lastSea);
                Assert.LessOrEqual(seen.Visibility, lastVisibility);
                Assert.AreEqual(20f * i / 10f, seen.WindVector.magnitude, 1e-4f);
                lastSea = seen.SeaState01;
                lastVisibility = seen.Visibility;
            }
        }

        [Test]
        public void TheWaveWind_IsTheSimsOwnSourceWhenNoShadeIsOn_AndShadedAtEveryTapWhenOneIs()
        {
            var history = new HeldHistory();
            Assert.AreSame(history, LocalWeather.Shade(history));
            Assert.IsNull(LocalWeather.Shade((IWaveWindSource)null));

            LocalWeather.Set(new LocalWeatherShade(0.5f, new Vector2(0f, 20f), 0.2f, 0.8f));
            IWaveWindSource shaded = LocalWeather.Shade(history);
            Assert.AreNotSame(history, shaded);
            foreach (double t in new[] { -3600.0, 0.0, 900.0 })
            {
                Assert.AreEqual(new Vector2(3f, 10f), shaded.WaveWindAt(t).Wind);
                Assert.AreEqual(0.5f, shaded.WaveWindAt(t).SeaState01, 1e-5f);
            }
            Assert.AreEqual(new Vector2(3f, 0f), history.WaveWindAt(0).Wind, "the source itself is untouched");
        }

        [Test]
        public void ResettingTheServices_ClearsTheShade()
        {
            LocalWeather.Set(new LocalWeatherShade(1f, Vector2.up, 0.2f, 0.9f));
            Assert.IsTrue(LocalWeather.Active);
            GameServices.Reset();
            Assert.IsFalse(LocalWeather.Active);
            Assert.AreEqual(0f, LocalWeather.Current.Strength01);
        }

        static void AssertSame(EnvironmentSample a, EnvironmentSample b)
        {
            Assert.AreEqual(a.WindVector, b.WindVector);
            Assert.AreEqual(a.CurrentVector, b.CurrentVector);
            Assert.AreEqual(a.TideHeight, b.TideHeight);
            Assert.AreEqual(a.SeaState, b.SeaState);
            Assert.AreEqual(a.Visibility, b.Visibility);
            Assert.AreEqual(a.SeaState01, b.SeaState01);
        }
    }
}
