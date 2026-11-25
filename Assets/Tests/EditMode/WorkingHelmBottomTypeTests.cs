using System;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
#if UNITY_5_3_OR_NEWER
using HiddenHarbours.Tests.Support;
#endif

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class WorkingHelmBottomTypeTests
    {
        private sealed class Glass : IHelmInstruments
        {
            public string HullId => "boat.test";
            public HelmFit Fit => new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None, false, false);
            public SounderPrefs SounderPrefs => new SounderPrefs(0.5f, true, false, false);
            public Vector2 Position;
            public bool HasPosition = true, HasDepth = true;
            public bool TryReadDepth(out float metres) { metres = 5f; return HasDepth; }
            public bool TryReadPosition(out Vector2 position) { position = Position; return HasPosition; }
            public void SetSounderPrefs(in SounderPrefs prefs) { }
        }

        private sealed class Hull : IWorkingHullInstruments
        {
            public bool Available = true;
            public bool TryRead(out WorkingHullState state)
            {
                state = new WorkingHullState
                {
                    HullId = "boat.test", RegionId = "region.test", DraughtMetres = 0.3f,
                    Motion = new BoatKinematics(true, 0f, Vector2.zero, 0f)
                };
                return Available;
            }
        }

        private sealed class MapProbe : IBottomMap
        {
            public BottomType Value = BottomType.Rock;
            public int Reads;
            public Vector2 LastPosition;
            public BottomType BottomAt(Vector2 position) { Reads++; LastPosition = position; return Value; }
        }

        private Glass _glass;
        private Hull _hull;
        private WorkingHelmReader _reader;
        private WorkingHelmFit _fit;
        private FuelSettings _fuel;

        private static IBottomMap Geology()
        {
            var settings = new BottomMapSettings
            {
                Origin = Vector2.zero, CellMetres = 10f, Salt = 17,
                RockWeight = 1f, MudWeight = 1f, SandWeight = 1f
            };
            return new DeterministicBottomMap(713, in settings);
        }

        [SetUp]
        public void Setup()
        {
            _glass = new Glass(); _hull = new Hull();
            _fit = new WorkingHelmFit("boat.test", WorkingInstrument.Depth, 0.2f, 0f, 0f);
            _fuel = FuelSettings.Default;
            Compose(Geology());
        }

        private void Compose(IBottomMap map)
        {
            _reader = new WorkingHelmReader(_glass, _hull, null, null, map);
            _reader.SetPower(WorkingInstrument.Depth, true);
        }

        private void Tick() => _reader.Tick(_fit, _fuel);

        [TestCase(0f, BottomType.Rock)]
        [TestCase(50f, BottomType.Mud)]
        [TestCase(10f, BottomType.Sand)]
        public void Bottom_KnownSeedAndPosition_ReadsEachCoreSubstrateWithoutPlotter(float x, BottomType expected)
        {
            _glass.Position = new Vector2(x, 0f);
            Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(expected));
            Assert.That(_reader.Depth.WaterDepthMetres, Is.EqualTo(5f));
            Assert.That(_reader.Depth.UnderKeelMetres, Is.EqualTo(4.7f).Within(1e-6f));
            Assert.That(_reader.Plotter.Status, Is.EqualTo(InstrumentStatus.NotFitted));
            var first = _reader.Depth;
            Compose(Geology()); Tick();
            Assert.That(_reader.Depth, Is.EqualTo(first), "Recreated map and reader must repeat the same reading.");
        }

        [Test]
        public void Bottom_PowerOffClearsPreviousRead_AndDoesNotQueryMap_UntilPoweredAgain()
        {
            var map = new MapProbe(); Compose(map); Tick();
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Rock));
            _reader.SetPower(WorkingInstrument.Depth, false); map.Value = BottomType.Mud; Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Off));
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Unknown));
            Assert.That(map.Reads, Is.EqualTo(1));
            _reader.SetPower(WorkingInstrument.Depth, true); Tick();
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Mud));
            Assert.That(map.Reads, Is.EqualTo(2));
        }

        [Test]
        public void Bottom_NotFittedClearsPreviousRead_AndDoesNotQueryMap()
        {
            var map = new MapProbe(); Compose(map); Tick();
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Rock));
            _fit = new WorkingHelmFit("boat.test", WorkingInstrument.None, 0.2f, 0f, 0f); Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.NotFitted));
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Unknown));
            Assert.That(map.Reads, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Bottom_MissingHullOrDepthClearsPreviousRead_AndDoesNotQueryMap(bool missingHull)
        {
            var map = new MapProbe(); Compose(map); Tick();
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Rock));
            if (missingHull) _hull.Available = false; else _glass.HasDepth = false;
            Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Unavailable));
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Unknown));
            Assert.That(map.Reads, Is.EqualTo(1));
        }

        [Test]
        public void Bottom_MissingMapPreservesValidDepth_WithUnknownSubstrate()
        {
            Compose(null); Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Depth.WaterDepthMetres, Is.EqualTo(5f));
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Unknown));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity)]
        public void Bottom_InvalidPositionClearsPreviousRead_AndDoesNotQueryMap(float x, float y)
        {
            var map = new MapProbe(); Compose(map); Tick();
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Rock));
            _glass.Position = new Vector2(x, y); Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Unknown));
            Assert.That(map.Reads, Is.EqualTo(1));
        }

        [Test]
        public void Bottom_MissingPositionClearsPreviousRead_AndDoesNotQueryMap()
        {
            var map = new MapProbe(); Compose(map); Tick();
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Rock));
            _glass.HasPosition = false; Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Unknown));
            Assert.That(map.Reads, Is.EqualTo(1));
        }

        [TestCase(BottomType.Unknown)]
        [TestCase((BottomType)123)]
        public void Bottom_UnknownOrUnrecognizedMapResultClearsPreviousRead(BottomType value)
        {
            var map = new MapProbe(); Compose(map); Tick();
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Rock));
            map.Value = value; _glass.Position = new Vector2(12f, -34f); Tick();
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Ready));
            Assert.That(_reader.Depth.Bottom, Is.EqualTo(BottomType.Unknown));
            Assert.That(map.LastPosition, Is.EqualTo(_glass.Position));
            Assert.That(map.Reads, Is.EqualTo(2));
        }

        private int ReadTenThousandPositions()
        {
            int checksum = 0;
            for (int i = 0; i < 10000; i++)
            {
                _glass.Position = new Vector2(i % 3 == 0 ? 0f : i % 3 == 1 ? 50f : 10f, 0f);
                Tick(); checksum += (int)_reader.Depth.Bottom;
            }
            return checksum;
        }

        [Test]
        public void Bottom_TickAllocatesNothing_OverTenThousandRealMapReadings_WithCalibratedCounter()
        {
            ReadTenThousandPositions();
            int checksum;
#if UNITY_5_3_OR_NEWER
            using (var probe = new ManagedAllocationProbe())
            {
                probe.Begin(); checksum = ReadTenThousandPositions(); long events = probe.End();
                Assert.That(events, Is.Zero);
            }
#else
            long start = GC.GetAllocatedBytesForCurrentThread(); var known = new byte[1024];
            long observed = GC.GetAllocatedBytesForCurrentThread() - start; GC.KeepAlive(known);
            Assert.That(observed, Is.GreaterThan(0), "The counter must detect a retained allocation.");
            long before = GC.GetAllocatedBytesForCurrentThread();
            checksum = ReadTenThousandPositions();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
#endif
            Assert.That(checksum, Is.EqualTo(19999), "All three substrate readings must contribute.");
            Assert.That(_reader.Depth.Status, Is.EqualTo(InstrumentStatus.Ready));
        }
    }
}
