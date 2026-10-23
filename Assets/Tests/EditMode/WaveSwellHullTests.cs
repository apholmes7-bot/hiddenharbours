using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveSwellHullTests
    {
        sealed class Clock : IGameClock
        {
            public double Seconds;
            public double TotalSeconds => Seconds;
            public GameTime Now => new GameTime(Seconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 12f;
            public float DayFraction => 0.5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        sealed class Weather : IEnvironmentService, IWaveWindSource
        {
            public int WorldSeed => 12345;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double time) => 0f;
            public WaveWindSample WaveWindAt(double time) => new WaveWindSample(Vector2.zero, 0f);
        }

        // Records the common seam driven by both player and moored hulls, without a renderer.
        sealed class Hull : IBoatHullPresenter
        {
            public bool Continuous;
            public float Heave, Phase = DeckRideMath.LevelPhaseDegrees, Roll, Pitch, Scale;
            public BoatHullVariant Variant => Continuous ? BoatHullVariant.Mesh : BoatHullVariant.Sprite;
            public float DrawnHeadingDegrees() => 0f;
            public int FacingCellIndex => 0;
            public int FacingCount => 16;
            public bool FacingsAreCounterClockwise => true;
            public float BakeElevationDegrees => 40f;
            public float WakeSternOffsetMeters => 0f;
            public float WatertightHalfBeamMeters => 1f;
            public float DesignWaterlineMeters => 0f;
            public bool HasRockGrid => true;
            public int RockFrame { get; set; } = -1;
            public bool SupportsContinuousRock => Continuous;
            public void SetRockPhaseDegrees(float phaseDegrees) { Phase = phaseDegrees; }
            public float VisualTiltDegrees { get; set; }
            public void SetDisplacedHeaveMeters(float heaveMeters) { Heave = heaveMeters; }
            public float DrawnRideMeters { get; private set; }
            public float AppliedRollDegrees => Roll;
            public float AppliedPitchDegrees => Pitch;
            public float AppliedHeaveMeters => Heave;
            public void SetDrawnRideMeters(float rideMeters) { DrawnRideMeters = rideMeters; }
            public void SetStormRock(float amplitudeScale, float extraRollDegrees, float extraPitchDegrees)
            { Scale = amplitudeScale; Roll = extraRollDegrees; Pitch = extraPitchDegrees; }
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active) { }
            public float DeckOccluderId => 0f;
            public IDeckOccupantSlots DeckOccupants => NoDeckOccupantSlots.Instance;
            public Transform Visual => null;
            public IBoatHullAnchors Anchors => null;
        }

        struct Frame
        {
            public int RockFrame;
            public float Phase, Heave, Roll, Pitch, Scale, Tilt;
            public Vector3 Position, Size;
            public Vector2 Force;
            public float Torque;
        }

        Frame[] Sail(string regionName, Vector2 position, bool continuous, bool swell)
        {
            GameServices.Reset();
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var root = new GameObject("Swell hull guard");
            var visual = new GameObject("Hull visual");
            visual.transform.SetParent(root.transform, false);
            root.transform.position = position;
            try
            {
                config.WaveField = WaveSwellTests.Field();
                config.WaveFetch = default;
                var clock = new Clock();
                var weather = new Weather();
                var region = AssetDatabase.LoadAssetAtPath<RegionDef>(
                    "Assets/_Project/Data/Regions/" + regionName + ".asset");
                var climate = AssetDatabase.LoadAssetAtPath<SwellClimateDef>(
                    "Assets/_Project/Data/Swell/HomeBaySwell.asset");
                Assert.IsNotNull(region); Assert.IsNotNull(climate);
                GameServices.Config = config; GameServices.Clock = clock; GameServices.Environment = weather;
                GameServices.CurrentRegionId = region.Id;
                if (swell) GameServices.RegisterSwellClimate(region.Id, climate, region.SwellShelter);
                DisplacedSea.Publish(this, new DisplacedSeaState(1f, 0.6f));
                var hull = new Hull { Continuous = continuous };
                var motion = root.AddComponent<BoatWaveMotion>();
                motion.Configure(visual.transform, hull);
                var animator = new WaveFieldAnimator();
                var settings = config.WaveField;
                var policy = SeakeepingSettings.Default;
                var response = new SeakeepingResponse(1f, 0f);
                var frames = new Frame[150];
                for (int i = 0; i < frames.Length; i++)
                {
                    clock.Seconds = 900.0 + i / 30.0;
                    var trains = animator.Tick(clock.Seconds, weather, in settings);
                    SharedWaveField.Publish(this, in trains);
                    motion.Tick();
                    var water = WaveMath.Sample(position, 0.0, in trains, GameServices.FetchAt(position));
                    var sea = SeakeepingForcesMath.Resolve(in water, Vector2.up, 1f, 0f, in response, in policy);
                    frames[i] = new Frame { RockFrame = hull.RockFrame, Phase = motion.RockPhaseDegrees,
                        Heave = hull.Heave, Roll = hull.Roll, Pitch = hull.Pitch, Scale = hull.Scale,
                        Tilt = hull.VisualTiltDegrees, Position = visual.transform.localPosition,
                        Size = visual.transform.localScale, Force = sea.Force, Torque = sea.Torque };
                }
                return frames;
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(config);
                SharedWaveField.Clear(this); DisplacedSea.Clear(this); GameServices.Reset();
            }
        }

        [TestCase("StPeters", 0f, 140f, true)]
        [TestCase("StPeters", 0f, 140f, false)]
        [TestCase("NineMileCreek", 132f, 80f, true)]
        [TestCase("NineMileCreek", 132f, 80f, false)]
        [TestCase("NineMileCreek", 80f, 30f, true)]
        [TestCase("NineMileCreek", 80f, 30f, false)]
        public void ShelteredGlass_HomeBaySwellOnAndOff_DrawExactlyTheSameLevelHull(
            string region, float x, float y, bool continuous)
        {
            var off = Sail(region, new Vector2(x, y), continuous, false);
            var on = Sail(region, new Vector2(x, y), continuous, true);
            for (int i = 0; i < on.Length; i++)
            {
                Assert.AreEqual(-1, on[i].RockFrame, "sheltered hull must hold its level frame");
                Assert.AreEqual(DeckRideMath.LevelPhaseDegrees, on[i].Phase, "shelter must gate canned rock");
                Assert.AreEqual(0f, on[i].Heave);
                Assert.AreEqual(0f, on[i].Roll); Assert.AreEqual(0f, on[i].Pitch);
                Assert.AreEqual(off[i], on[i], "swell hidden by the water must not move a hull or change seakeeping");
            }
        }

        [TestCase("StPeters", 300f, -180f, true)]
        [TestCase("StPeters", 300f, -180f, false)]
        [TestCase("NineMileCreek", 150f, -40f, true)]
        [TestCase("NineMileCreek", 150f, -40f, false)]
        public void ExposedWater_HomeBaySwell_StillMovesTheHull(
            string region, float x, float y, bool continuous)
        {
            var off = Sail(region, new Vector2(x, y), continuous, false);
            var on = Sail(region, new Vector2(x, y), continuous, true);
            int moving = 0;
            for (int i = 0; i < on.Length; i++)
                if (!on[i].Equals(off[i])) moving++;
            Assert.Greater(moving, on.Length / 2, "the exposed control must ride the ocean swell");
        }
    }
}
