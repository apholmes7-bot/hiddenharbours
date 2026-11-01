using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveFieldAnimatorTests
    {
        private static readonly Vector2 SamplePos = new Vector2(12.3f, -7.7f);

        [Test]
        public void HeldHourKeepsWindMemoryShapeWhileTravelFollowsFlow()
        {
            var saved = GameServices.Clock;
            IGameClock clock = new HeldClock { TotalSeconds = 599d, FlowSeconds = 1200d };
            GameServices.Clock = clock;
            try
            {
                var field = ShippedWaveField.Settings();
                var source = new WaveMemoryTests.StepWind(600d, Vector2.right * 6f, Vector2.up * 12f);
                var targets = WaveMemory.TrainsAt(source, clock.TotalSeconds, in field);
                var shape = GameServices.WithSwell(in targets, clock.TotalSeconds, field.Gravity);
                var trains = new WaveFieldAnimator().Tick(WaveFieldAnimator.HourTimeSeconds,
                    WaveFieldAnimator.GameTimeSeconds, source, in field);
                Assert.AreEqual(shape.Count, trains.Count);
                for (int i = 0; i < trains.Count; i++)
                {
                    Assert.AreEqual(shape[i].Wavelength, trains[i].Wavelength);
                    Assert.AreEqual(shape[i].Amplitude, trains[i].Amplitude);
                    Assert.AreEqual(shape[i].Direction.x, trains[i].Direction.x, 1e-5f);
                    Assert.AreEqual(shape[i].Direction.y, trains[i].Direction.y, 1e-5f);
                    double phase = shape[i].PhaseOffset - 2d * System.Math.PI / shape[i].Wavelength
                        * shape[i].PhaseSpeed * clock.FlowSeconds;
                    phase -= System.Math.Floor(phase / (2d * System.Math.PI)) * (2d * System.Math.PI);
                    Assert.AreEqual((float)phase, trains[i].PhaseOffset, 1e-5f);
                }
            }
            finally { GameServices.Clock = saved; }
        }

        sealed class HeldClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public double FlowSeconds { get; set; }
            public bool IsHeld { get; set; } = true;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 6f;
            public float DayFraction => 0.25f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        [Test]
        public void SharpWindStep_WithTinyDt_HeightIsContinuous()
        {
            var field = ShippedWaveField.Settings();
            var wind = new WaveMemoryTests.StepWind(600.0, new Vector2(6f, 0f), new Vector2(2.5f, 9.5f));
            var animator = new WaveFieldAnimator();
            animator.Tick(600.0 - 1e-5, wind, in field);
            float before = animator.Sample(SamplePos).Height;
            animator.Tick(600.0 + 1e-5, wind, in field);
            Assert.AreEqual(before, animator.Sample(SamplePos).Height, 1e-3f);
        }

        [Test]
        public void SharpWindStep_AtSixtyFps_FrameDeltasStayBounded()
        {
            var field = ShippedWaveField.Settings();
            var wind = new WaveMemoryTests.StepWind(600.0, new Vector2(6f, 0f), new Vector2(2.5f, 9.5f));
            var animator = new WaveFieldAnimator();
            animator.Tick(599.0, wind, in field);
            float previous = animator.Sample(SamplePos).Height;
            for (int i = 1; i <= 37200; i++)
            {
                animator.Tick(599.0 + i / 60.0, wind, in field);
                float height = animator.Sample(SamplePos).Height;
                Assert.Less(Mathf.Abs(height - previous), 0.15f);
                previous = height;
            }
        }

        [Test]
        public void FieldGrowsATrain_TinySpectrumBlend_EntersAtZero()
        {
            // Authoring count changes are immediate data edits. Spectrum growth is continuous
            // in its blend dial, independently of how many frames have run.
            var field = WaveFieldSettings.Default;
            var animator = new WaveFieldAnimator();
            var wind = new WaveMemoryTests.StepWind(0, Vector2.right * 6f, Vector2.right * 6f);
            field.SpectrumBlend = 1e-6f;
            var small = animator.Tick(0, wind, in field);
            Assert.AreEqual(8, small.Count);
            for (int i = 4; i < 8; i++)
            {
                Assert.Less(small[i].Amplitude, 1e-5f);
                Assert.Greater(small[i].Wavelength, 1f);
            }
            field.SpectrumBlend = 0.65f;
            var grown = animator.Tick(0, wind, in field);
            Assert.Greater(grown[5].Amplitude, small[5].Amplitude);
        }

        [Test]
        public void FieldGrowsATrain_TheSampledHeightDoesNotJump()
        {
            var field = ShippedWaveField.Settings();
            var animator = new WaveFieldAnimator();
            var wind = new WaveMemoryTests.StepWind(600, Vector2.zero, Vector2.up * 10f);
            animator.Tick(600 - 1e-6, wind, in field);
            float before = animator.Sample(SamplePos).Height;
            animator.Tick(600 + 1e-6, wind, in field);
            Assert.AreEqual(before, animator.Sample(SamplePos).Height, 1e-5f);
            Assert.Greater(animator.Tick(660, wind, in field).TotalAmplitude, 0.01f);
        }

        [Test]
        public void SeaStateZero_EasedAmplitudes_SnapToExactZero()
        {
            var field = ShippedWaveField.Settings();
            var wind = new WaveMemoryTests.StepWind(600, Vector2.right * 6f, Vector2.zero);
            var animator = new WaveFieldAnimator();
            Assert.Greater(animator.Tick(600, wind, in field).TotalAmplitude, 0f);
            var calm = animator.Tick(1238, wind, in field);
            Assert.AreEqual(0f, calm.TotalAmplitude);
            Assert.AreEqual(0f, animator.Sample(SamplePos).Height);
        }

        [Test]
        public void EasedTrains_MidTransition_PhaseSpeedDerivesFromEasedWavelength()
        {
            var field = ShippedWaveField.Settings();
            var wind = new WaveMemoryTests.StepWind(600, Vector2.right * 4f, Vector2.up * 10f);
            var trains = new WaveFieldAnimator().Tick(660, wind, in field);
            for (int i = 0; i < trains.Count; i++)
                Assert.AreEqual(Mathf.Sqrt(field.Gravity * trains[i].Wavelength / (2f * Mathf.PI)),
                    trains[i].PhaseSpeed, 1e-4f);
        }

        [Test]
        public void SmoothHelper_TwoHalfSteps_EqualOneFullStep()
        {
            float once = WaveFieldAnimator.Smooth(1.2f, 3.7f, 1f / 30f, 0.2f);
            float twice = WaveFieldAnimator.Smooth(1.2f, 3.7f, 1f / 60f, 0.2f);
            twice = WaveFieldAnimator.Smooth(twice, 3.7f, 1f / 60f, 0.2f);
            Assert.AreEqual(once, twice, 1e-5f);
        }

        [Test]
        public void ConstantWeather_SameSpanAtDifferentTickRates_SameSurface()
        {
            var field = ShippedWaveField.Settings();
            var wind = new WaveMemoryTests.StepWind(0, Vector2.right * 6f, Vector2.right * 6f);
            var a = new WaveFieldAnimator();
            var b = new WaveFieldAnimator();
            for (int i = 0; i <= 90; i++) a.Tick(i / 30.0, wind, in field);
            for (int i = 0; i <= 180; i++) b.Tick(i / 60.0, wind, in field);
            WaveMemoryTests.AssertSame(a.Current, b.Current);
        }

        [Test]
        public void BeforeFirstTick_CurrentIsGlass()
        {
            var animator = new WaveFieldAnimator();
            Assert.AreEqual(0, animator.Current.Count);
            Assert.AreEqual(0f, animator.Sample(SamplePos).Height);
        }
    }
}
