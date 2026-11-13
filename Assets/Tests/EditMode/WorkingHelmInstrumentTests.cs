using System;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class WorkingHelmInstrumentTests
    {
        private sealed class Hull : IWorkingHullInstruments
        {
            public bool Available = true;
            public int Reads;
            public WorkingHullState State = new WorkingHullState
            {
                HullId = "boat.test", RegionId = "region.test",
                Motion = new BoatKinematics(true, 0f, new Vector2(3f, 4f), 5f),
                DraughtMetres = 0.3f, HasEngine = true, EngineTurning = true,
                HasFuel = true, FuelLitres = 60f, FullThrottleLitresPerHour = 20f,
                Drive = 0.5f, HoldFraction = 0.25f
            };
            public bool TryRead(out WorkingHullState state) { Reads++; state = State; return Available; }
        }

        private sealed class Glass : IHelmInstruments
        {
            public string HullId { get; set; } = "boat.test";
            public HelmFit Fit => new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.Flush, true, true);
            public float Water = 1.2f;
            public Vector2 Position = new Vector2(100f, 200f);
            public bool Available = true;
            public SounderPrefs SounderPrefs { get; private set; } = new SounderPrefs(0.4f, true, false, false);
            public bool TryReadDepth(out float metres) { metres = Water; return Available; }
            public bool TryReadPosition(out Vector2 position) { position = Position; return Available; }
            public void SetSounderPrefs(in SounderPrefs prefs) => SounderPrefs = prefs;
        }

        private sealed class Sea : IEnvironmentService
        {
            public Vector2 Wind = new Vector2(0f, -6f);
            public float Roughness = 0.5f;
            public int Reads;
            public int WorldSeed => 17;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
            { Reads++; return new EnvironmentSample(Wind, Vector2.zero, 0f, SeaState.Moderate, 1f, Roughness); }
            public float TideHeightAt(double time) => 0f;
        }

        private sealed class Points : IHelmWaypoints
        {
            public HelmWaypoint[] Values = {
                new HelmWaypoint("exit.test", "region.test", "Seam", new Vector2(103f, 204f), HelmWaypointKind.SeamExit),
                new HelmWaypoint("berth.test", "region.test", "Berth", new Vector2(100f, 210f), HelmWaypointKind.Berth),
                new HelmWaypoint("exit.other", "region.other", "Other", new Vector2(103f, 204f), HelmWaypointKind.SeamExit)
            };
            public int Count => Values.Length;
            public HelmWaypoint At(int index) => Values[index];
        }

        private Hull _hull;
        private Glass _glass;
        private Sea _sea;
        private Points _points;
        private WorkingHelmReader _reader;
        private WorkingHelmFit _fit;
        private FuelSettings _fuel;

        [SetUp]
        public void Setup()
        {
            _hull = new Hull(); _glass = new Glass(); _sea = new Sea(); _points = new Points();
            _reader = new WorkingHelmReader(_glass, _hull, _sea, _points);
            _reader.SetPower(WorkingInstrument.All, true);
            _fit = new WorkingHelmFit("boat.test", WorkingInstrument.All, 0.2f, 600f, 2400f);
            _fuel = FuelSettings.Default;
        }

        private void Tick() => _reader.Tick(_fit, _fuel);

        [Test]
        public void Depth_ReadsWaterColumnFromExistingGlass_AndSubtractsActualDraught()
        {
            Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Depth.WaterDepthMetres, Is.EqualTo(1.2f));
            Assert.That(_reader.Depth.UnderKeelMetres, Is.EqualTo(0.9f).Within(1e-6f));
            Assert.That(_reader.Depth.AlarmWaterDepthMetres, Is.EqualTo(0.5f));
            Assert.That(_reader.Depth.ShallowAlarm, Is.False);
        }

        [Test]
        public void ShallowAlarm_SameWaterDifferentHullDraught_ChangesOnlyDeeperHullsAlarm()
        {
            Tick(); Assert.That(_reader.Depth.ShallowAlarm, Is.False);
            _hull.State.DraughtMetres = 1.1f;
            Tick();
            Assert.That(_reader.Depth.UnderKeelMetres, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(_reader.Depth.AlarmWaterDepthMetres, Is.EqualTo(1.3f).Within(1e-6f));
            Assert.That(_reader.Depth.ShallowAlarm, Is.True);
        }

        [Test]
        public void ShallowAlarm_IsInclusive_DisarmWorks_AndLegacyHigherSetpointIsPreserved()
        {
            _glass.Water = 0.5f; Tick(); Assert.That(_reader.Depth.ShallowAlarm, Is.True);
            var prefs = new SounderPrefs(2f, false, false, false);
            _glass.SetSounderPrefs(prefs); Tick(); Assert.That(_reader.Depth.ShallowAlarm, Is.False);
            prefs.Armed = true; _glass.SetSounderPrefs(prefs); _glass.Water = 1.2f; Tick();
            Assert.That(_reader.Depth.AlarmWaterDepthMetres, Is.EqualTo(2f));
            Assert.That(_reader.Depth.ShallowAlarm, Is.True);
        }

        [Test]
        public void Depth_AgroundRetainsNegativeKeelClearance_AndMissingGlassClearsRead()
        {
            _glass.Water = 0f; Tick(); Assert.That(_reader.Depth.UnderKeelMetres, Is.EqualTo(-0.3f));
            Assert.That(_reader.Depth.ShallowAlarm, Is.True);
            _glass.Available = false; Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            Assert.That(_reader.Depth.UnderKeelMetres, Is.Zero);
            Assert.That(_reader.Depth.ShallowAlarm, Is.False);
        }

        [Test]
        public void Motion_ReportsKnownGroundSpeed_AndWrappedCompassHeading()
        {
            _hull.State.Motion = new BoatKinematics(true, -90f, new Vector2(-3f, 4f), 5f); Tick();
            Assert.That(_reader.Motion.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Motion.SpeedOverGroundMps, Is.EqualTo(5f));
            Assert.That(_reader.Motion.HeadingDegrees, Is.EqualTo(270f));
        }

        [Test]
        public void Wind_UsesMainSailingTrueAndApparentWind_WithActualGroundVelocity()
        {
            _hull.State.Motion = new BoatKinematics(true, 0f, new Vector2(0f, 4f), 4f); Tick();
            Assert.That(_reader.Wind.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Wind.Wind.TrueAngleDeg, Is.EqualTo(0f));
            Assert.That(_reader.Wind.Wind.ApparentAngleDeg, Is.EqualTo(0f));
            Assert.That(_reader.Wind.Wind.TrueKn, Is.EqualTo(SailDrive.ToKnots(6f)).Within(1e-6f));
            Assert.That(_reader.Wind.Wind.ApparentKn, Is.EqualTo(SailDrive.ToKnots(10f)).Within(1e-6f));
        }

        [Test]
        public void Wind_CrosswindHasStarboardSource_AndCalmHasZeroSpeed()
        {
            _sea.Wind = new Vector2(-6f, 0f);
            _hull.State.Motion = new BoatKinematics(true, 0f, Vector2.zero, 0f); Tick();
            Assert.That(_reader.Wind.Wind.TrueAngleDeg, Is.EqualTo(90f));
            Assert.That(_reader.Wind.Wind.ApparentAngleDeg, Is.EqualTo(90f));
            _sea.Wind = Vector2.zero; Tick();
            Assert.That(_reader.Wind.Wind.TrueKn, Is.Zero);
            Assert.That(_reader.Wind.Wind.ApparentKn, Is.Zero);
        }

        [Test]
        public void Plotter_ReportsOwnPosition_AndRangeBearingToSelectedSeamExit()
        {
            _reader.SelectWaypoint("exit.test"); Tick();
            Assert.That(_reader.Plotter.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Plotter.OwnPosition, Is.EqualTo(_glass.Position));
            Assert.That(_reader.Plotter.HasTarget, Is.True);
            Assert.That(_reader.Plotter.Target.Kind, Is.EqualTo(HelmWaypointKind.SeamExit));
            Assert.That(_reader.Plotter.RangeMetres, Is.EqualTo(5f));
            Assert.That(_reader.Plotter.BearingDegrees, Is.EqualTo(36.869897f).Within(1e-5f));
        }

        [Test]
        public void Plotter_SelectsBerth_RejectsOtherRegions_AndClearsRemovedTarget()
        {
            _reader.SelectWaypoint("berth.test"); Tick();
            Assert.That(_reader.Plotter.Target.Kind, Is.EqualTo(HelmWaypointKind.Berth));
            Assert.That(_reader.Plotter.RangeMetres, Is.EqualTo(10f));
            Assert.That(_reader.Plotter.BearingDegrees, Is.Zero);
            _reader.SelectWaypoint("exit.other"); Tick(); Assert.That(_reader.Plotter.HasTarget, Is.False);
            _reader.SelectWaypoint("berth.test"); _points.Values = Array.Empty<HelmWaypoint>(); Tick();
            Assert.That(_reader.Plotter.HasTarget, Is.False);
            Assert.That(_reader.Plotter.RangeMetres, Is.Zero);
        }

        [Test]
        public void Plotter_AtDestinationHasZeroRangeWithoutInventingABearing()
        {
            _reader.SelectWaypoint("berth.test"); _glass.Position = new Vector2(100f, 210f); Tick();
            Assert.That(_reader.Plotter.HasTarget, Is.True);
            Assert.That(_reader.Plotter.RangeMetres, Is.Zero);
            Assert.That(_reader.Plotter.HasBearing, Is.False);
        }

        [Test]
        public void Engine_ReportsCalibratedEstimatedRevs_FuelAndHoursAtTheSameDuty()
        {
            _fuel = new FuelSettings { IdleFraction = 0.1f, ThrottleExponent = 2f,
                LoadSurcharge = 0.5f, HoldLoadWeight = 1f, SeaSurcharge = 0.2f, BurnScale = 1f };
            Tick();
            // 20 * (0.1 + 0.9 * 0.5^2) * (1 + 0.5 * 0.25) * (1 + 0.2 * 0.5).
            const float expectedBurn = 8.04375f;
            Assert.That(_reader.Engine.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Engine.HasEstimatedRpm, Is.True);
            Assert.That(_reader.Engine.EstimatedRpm, Is.EqualTo(1500f));
            Assert.That(_reader.Engine.HasFuel, Is.True);
            Assert.That(_reader.Engine.FuelLitres, Is.EqualTo(60f));
            Assert.That(_reader.Engine.HasRange, Is.True);
            Assert.That(_reader.Engine.LitresPerHour, Is.EqualTo(expectedBurn).Within(1e-5f));
            Assert.That(_reader.Engine.HoursRemaining, Is.EqualTo(7.459206f).Within(1e-5f));
        }

        [Test]
        public void Engine_NeutralStillBurnsIdle_AndAsternUsesMagnitude()
        {
            _hull.State.Drive = 0f; Tick();
            Assert.That(_reader.Engine.EstimatedRpm, Is.EqualTo(600f));
            Assert.That(_reader.Engine.LitresPerHour, Is.EqualTo(FuelBurn.LitresPerHour(20f, 0f, 0.25f, 0f, 0.5f, _fuel)));
            _hull.State.Drive = -0.5f; Tick(); Assert.That(_reader.Engine.EstimatedRpm, Is.EqualTo(1500f));
        }

        [Test]
        public void Engine_MeteredBurn_IsShownAsReported_EvenWithNoEnvironment()
        {
            const float metered = 3.25f;
            float estimate = FuelBurn.LitresPerHour(20f, 0.5f, 0.25f, 0f, 0.5f, _fuel);
            Assert.That(metered, Is.Not.EqualTo(estimate), "the fixture must tell the meter from the estimate");
            _hull.State.HasMeteredBurn = true; _hull.State.MeteredLitresPerHour = metered; Tick();
            Assert.That(_reader.Engine.LitresPerHour, Is.EqualTo(metered));
            Assert.That(_reader.Engine.HasRange, Is.True);
            Assert.That(_reader.Engine.HoursRemaining, Is.EqualTo(60f / metered));

            // The tank's meter already carries load and sea, so it needs no environment to be read.
            var noSea = new WorkingHelmReader(_glass, _hull, null, _points);
            noSea.SetPower(WorkingInstrument.All, true);
            noSea.Tick(_fit, _fuel);
            Assert.That(noSea.Engine.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(noSea.Engine.LitresPerHour, Is.EqualTo(metered));
            Assert.That(noSea.Engine.HasRange, Is.True);
            Assert.That(noSea.Engine.HoursRemaining, Is.EqualTo(60f / metered));
        }

        [Test]
        public void Engine_MeteredBurnUnset_KeepsTheThrottleCurveEstimate()
        {
            float estimate = FuelBurn.LitresPerHour(20f, 0.5f, 0.25f, 0f, 0.5f, _fuel);
            // A stale rate with the flag down must be ignored.
            _hull.State.HasMeteredBurn = false; _hull.State.MeteredLitresPerHour = 3.25f; Tick();
            Assert.That(_reader.Engine.LitresPerHour, Is.EqualTo(estimate));
            Assert.That(_reader.Engine.HasRange, Is.True);
            Assert.That(_reader.Engine.HoursRemaining, Is.EqualTo(60f / estimate));
        }

        [Test]
        public void Engine_MeteredBurnNotANumber_FallsBackToTheEstimate()
        {
            float estimate = FuelBurn.LitresPerHour(20f, 0.5f, 0.25f, 0f, 0.5f, _fuel);
            _hull.State.HasMeteredBurn = true; _hull.State.MeteredLitresPerHour = float.NaN; Tick();
            Assert.That(_reader.Engine.LitresPerHour, Is.EqualTo(estimate));
            Assert.That(_reader.Engine.HasRange, Is.True);
            Assert.That(_reader.Engine.HoursRemaining, Is.EqualTo(60f / estimate));

            // With no environment there is no estimate to fall back to: no rate, never a NaN one.
            var noSea = new WorkingHelmReader(_glass, _hull, null, _points);
            noSea.SetPower(WorkingInstrument.All, true);
            noSea.Tick(_fit, _fuel);
            Assert.That(noSea.Engine.LitresPerHour, Is.Zero);
            Assert.That(noSea.Engine.HasRange, Is.False);
        }

        [Test]
        public void Engine_StoppedOrMissingFuelHasNoInventedInfiniteRange()
        {
            _hull.State.EngineTurning = false; Tick();
            Assert.That(_reader.Engine.EstimatedRpm, Is.Zero);
            Assert.That(_reader.Engine.HasRange, Is.False);
            Assert.That(_reader.Engine.HoursRemaining, Is.Zero);
            _hull.State.HasFuel = false; Tick(); Assert.That(_reader.Engine.HasFuel, Is.False);
            _hull.State.HasEngine = false; Tick(); Assert.That(_reader.Engine.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            _hull.State.HasEngine = _hull.State.HasFuel = _hull.State.EngineTurning = true;
            _hull.State.FuelLitres = float.MaxValue;
            _hull.State.FullThrottleLitresPerHour = 1e-20f; Tick();
            Assert.That(_reader.Engine.HasRange, Is.False, "overflow is unknown endurance, never infinity");
            Assert.That(_reader.Engine.HoursRemaining, Is.Zero);
        }

        [Test]
        public void Bilge_AbsentSimulationIsUnavailable_AndInjectedKnownStateIsReadOnly()
        {
            Tick(); Assert.That(_reader.Bilge.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            _hull.State.HasBilge = true; _hull.State.BilgeLitres = 12f; Tick();
            Assert.That(_reader.Bilge.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Bilge.Value, Is.EqualTo(12f));
            Assert.That(_hull.State.BilgeLitres, Is.EqualTo(12f));
        }

        [Test]
        public void Battery_AbsentSimulationIsUnavailable_AndKnownChargeIsNotIntegrated()
        {
            Tick(); Assert.That(_reader.Battery.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            _hull.State.HasBattery = true; _hull.State.BatteryCharge01 = 0.75f; Tick(); Tick();
            Assert.That(_reader.Battery.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Battery.Value, Is.EqualTo(0.75f));
            Assert.That(_hull.State.BatteryCharge01, Is.EqualTo(0.75f));
        }

        [Test]
        public void Power_IsPerInstrument_AndOffClearsOldReadings()
        {
            Tick(); _reader.SetPower(WorkingInstrument.Depth | WorkingInstrument.Engine, false); Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Off));
            Assert.That(_reader.Depth.WaterDepthMetres, Is.Zero);
            Assert.That(_reader.Engine.Status, Is.EqualTo(InstrumentStatus.Off));
            Assert.That(_reader.Engine.FuelLitres, Is.Zero);
            Assert.That(_reader.Motion.Status, Is.EqualTo(InstrumentStatus.Ready));
            _reader.SetPower(WorkingInstrument.Depth, true); Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Engine.Status, Is.EqualTo(InstrumentStatus.Off));
        }

        [TestCase(WorkingInstrument.Depth)]
        [TestCase(WorkingInstrument.Motion)]
        [TestCase(WorkingInstrument.Wind)]
        [TestCase(WorkingInstrument.Plotter)]
        [TestCase(WorkingInstrument.Engine)]
        [TestCase(WorkingInstrument.Bilge)]
        [TestCase(WorkingInstrument.Battery)]
        public void EveryInstrumentHasAnIndependentPowerState(WorkingInstrument instrument)
        {
            _hull.State.HasBilge = true; _hull.State.HasBattery = true;
            Tick(); Assert.That(StatusOf(instrument), Is.EqualTo(InstrumentStatus.Ready));
            _reader.SetPower(instrument, false); Tick();
            Assert.That(StatusOf(instrument), Is.EqualTo(InstrumentStatus.Off));
            _reader.SetPower(instrument, true); Tick();
            Assert.That(StatusOf(instrument), Is.EqualTo(InstrumentStatus.Ready));
        }

        private InstrumentStatus StatusOf(WorkingInstrument instrument)
        {
            switch (instrument)
            {
                case WorkingInstrument.Depth: return _reader.Depth.Status;
                case WorkingInstrument.Motion: return _reader.Motion.Status;
                case WorkingInstrument.Wind: return _reader.Wind.Status;
                case WorkingInstrument.Plotter: return _reader.Plotter.Status;
                case WorkingInstrument.Engine: return _reader.Engine.Status;
                case WorkingInstrument.Bilge: return _reader.Bilge.Status;
                default: return _reader.Battery.Status;
            }
        }

        [Test]
        public void BareFitHasNoInstrument_EvenWhenAllPowerInputsAreOn()
        {
            _fit = new WorkingHelmFit("boat.test", WorkingInstrument.None, 0f, 0f, 0f); Tick();
            AssertStatuses(InstrumentStatus.NotFitted);
        }

        [Test]
        public void FreshReaderStartsWithAllInstrumentsOff()
        {
            _reader = new WorkingHelmReader(_glass, _hull, _sea, _points); Tick();
            AssertStatuses(InstrumentStatus.Off);
        }

        [Test]
        public void MissingOrDifferentHullCannotShowLastBoatsNumbers()
        {
            Tick(); _hull.Available = false; Tick(); AssertStatuses(InstrumentStatus.Unavailable);
            Assert.That(_reader.Motion.SpeedOverGroundMps, Is.Zero);
            Assert.That(_reader.Engine.FuelLitres, Is.Zero);
            _hull.Available = true; _hull.State.HullId = "boat.other"; Tick();
            AssertStatuses(InstrumentStatus.Unavailable);
        }

        [Test]
        public void DifferentGlassHullCannotLeakDepthOrPosition()
        {
            _glass.HullId = "boat.other"; Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            Assert.That(_reader.Plotter.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            Assert.That(_reader.Motion.Status, Is.EqualTo(InstrumentStatus.Ready));
        }

        [Test]
        public void MissingEnvironmentSuppressesWindAndRangeWithoutHidingKnownFuel()
        {
            _reader = new WorkingHelmReader(_glass, _hull, null, _points);
            _reader.SetPower(WorkingInstrument.All, true); Tick();
            Assert.That(_reader.Wind.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            Assert.That(_reader.Engine.HasRange, Is.False);
            Assert.That(_reader.Engine.FuelLitres, Is.EqualTo(60f));
        }

        [Test]
        public void InvalidInputsNeverBecomeHealthyAuxiliaryOrDepthReadings()
        {
            _glass.Water = float.NaN;
            _hull.State.HasBattery = true; _hull.State.BatteryCharge01 = 1.1f;
            _hull.State.HasBilge = true; _hull.State.BilgeLitres = -1f; Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            Assert.That(_reader.Battery.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            Assert.That(_reader.Bilge.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            _glass.Water = 1.2f;
            _glass.SetSounderPrefs(new SounderPrefs(float.NaN, true, false, false)); Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Unavailable));
        }

        [Test]
        public void Tick_SamplesEachAuthorityOnce_AndRepeatedStateIsDeterministic()
        {
            Tick(); var depth = _reader.Depth; var engine = _reader.Engine; var wind = _reader.Wind;
            Tick();
            Assert.That(_hull.Reads, Is.EqualTo(2)); Assert.That(_sea.Reads, Is.EqualTo(2));
            Assert.That(_reader.Depth, Is.EqualTo(depth));
            Assert.That(_reader.Engine, Is.EqualTo(engine));
            Assert.That(_reader.Wind, Is.EqualTo(wind));
        }

        [Test]
        public void Tick_AllocatesExactlyZeroManagedBytes_AfterSetupAcrossTenThousandReads()
        {
            _reader.SelectWaypoint("exit.test");
            for (int i = 0; i < 64; i++) Tick();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) Tick();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
            Assert.That(_reader.Plotter.HasTarget, Is.True);
        }

        private void AssertStatuses(InstrumentStatus expected)
        {
            Assert.That(_reader.Depth.Status, Is.EqualTo(expected));
            Assert.That(_reader.Motion.Status, Is.EqualTo(expected));
            Assert.That(_reader.Wind.Status, Is.EqualTo(expected));
            Assert.That(_reader.Plotter.Status, Is.EqualTo(expected));
            Assert.That(_reader.Engine.Status, Is.EqualTo(expected));
            Assert.That(_reader.Bilge.Status, Is.EqualTo(expected));
            Assert.That(_reader.Battery.Status, Is.EqualTo(expected));
        }
    }
}
