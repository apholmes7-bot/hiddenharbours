using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    [Flags]
    public enum WorkingInstrument
    {
        None = 0, Depth = 1, Motion = 2, Wind = 4, Plotter = 8,
        Engine = 16, Bilge = 32, Battery = 64,
        All = Depth | Motion | Wind | Plotter | Engine | Bilge | Battery
    }

    public enum InstrumentStatus { NotFitted, Off, Unavailable, Ready }

    /// <summary>Resolved once from the existing equipment model and a per-hull data profile.</summary>
    public readonly struct WorkingHelmFit
    {
        public readonly string HullId;
        public readonly WorkingInstrument Instruments;
        public readonly float MinimumKeelClearanceMetres;
        public readonly float IdleRpm, MaximumRpm;

        public WorkingHelmFit(string hullId, WorkingInstrument instruments, float clearance, float idleRpm, float maximumRpm)
        {
            HullId = hullId;
            Instruments = instruments;
            MinimumKeelClearanceMetres = clearance;
            IdleRpm = idleRpm;
            MaximumRpm = maximumRpm;
        }
    }

    /// <summary>Read-only hull facts. No new engine, bilge, electrical or fuel simulation.</summary>
    public struct WorkingHullState
    {
        public string HullId, RegionId;
        public BoatKinematics Motion;
        public float DraughtMetres;
        public bool HasEngine, EngineTurning, HasFuel;
        public float Drive, FuelLitres, FullThrottleLitresPerHour, HoldFraction;
        /// <summary>The hull's own fuel meter's rate right now (litres per real hour), when she has
        /// one to report. Set, the gauge shows THIS and not a second estimate, so the helm and the
        /// tank can never disagree. Unset, the reader keeps its throttle-curve estimate.</summary>
        public bool HasMeteredBurn;
        public float MeteredLitresPerHour;
        public bool HasBilge, HasBattery;
        public float BilgeLitres, BatteryCharge01;
    }

    /// <summary>Implemented by the hull's owner; feature consumers depend only on Core.</summary>
    public interface IWorkingHullInstruments
    {
        bool TryRead(out WorkingHullState state);
    }

    public enum HelmWaypointKind { SeamExit, Berth }

    public readonly struct HelmWaypoint
    {
        public readonly string Id, RegionId, Name;
        public readonly Vector2 Position;
        public readonly HelmWaypointKind Kind;

        public HelmWaypoint(string id, string regionId, string name, Vector2 position, HelmWaypointKind kind)
        { Id = id; RegionId = regionId; Name = name; Position = position; Kind = kind; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(RegionId)
                               && NavMath.IsFinite(Position);
    }

    /// <summary>Region-local catalogue, rebuilt outside the tick when region content changes.</summary>
    public interface IHelmWaypoints
    {
        int Count { get; }
        HelmWaypoint At(int index);
    }

    public struct KeelDepthReading
    {
        public InstrumentStatus Status;
        public float WaterDepthMetres, UnderKeelMetres, AlarmWaterDepthMetres;
        public bool ShallowAlarm;
        /// <summary>Unknown when the sounder or substrate query has no current reading.
        /// This is gameplay geology from the shared Core map, never a terrain-art classification.</summary>
        public BottomType Bottom;
    }

    public struct MotionInstrumentReading
    {
        public InstrumentStatus Status;
        public float SpeedOverGroundMps, HeadingDegrees;
    }

    public struct WindInstrumentReading
    {
        public InstrumentStatus Status;
        public SailWind Wind;
    }

    public struct PlotterInstrumentReading
    {
        public InstrumentStatus Status;
        public Vector2 OwnPosition;
        public bool HasTarget, HasBearing;
        public HelmWaypoint Target;
        public float RangeMetres, BearingDegrees;
    }

    public struct EngineInstrumentReading
    {
        public InstrumentStatus Status;
        // Main has throttle, not a mechanical RPM sim. This calibrated indication is explicitly an estimate.
        public bool Turning, HasEstimatedRpm, HasFuel, HasRange;
        public float EstimatedRpm, FuelLitres, LitresPerHour, HoursRemaining;
    }

    public struct AuxiliaryInstrumentReading
    {
        public InstrumentStatus Status;
        public float Value;
    }
}
