using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Allocation-free pull model over the existing glass and the same hull/environment.
    /// No registration, input, presentation, persistence, timers or simulation of its own.
    /// </summary>
    public sealed class WorkingHelmReader
    {
        private readonly IHelmInstruments _glass;
        private readonly IWorkingHullInstruments _hull;
        private readonly IEnvironmentService _environment;
        private readonly IHelmWaypoints _waypoints;
        private readonly IBottomMap _bottomMap;
        private string _selectedWaypointId;

        // No accessory/power switch model exists on main. This transient input is the later switch adapter's seam.
        public WorkingInstrument Powered { get; private set; }
        public KeelDepthReading Depth { get; private set; }
        public MotionInstrumentReading Motion { get; private set; }
        public WindInstrumentReading Wind { get; private set; }
        public PlotterInstrumentReading Plotter { get; private set; }
        public EngineInstrumentReading Engine { get; private set; }
        public AuxiliaryInstrumentReading Bilge { get; private set; }
        public AuxiliaryInstrumentReading Battery { get; private set; }

        /// <summary>Compose with the same Core bottom map used by other gameplay readers.
        /// An absent map leaves the substrate unknown without hiding a valid depth reading.</summary>
        public WorkingHelmReader(IHelmInstruments glass, IWorkingHullInstruments hull,
                                 IEnvironmentService environment, IHelmWaypoints waypoints,
                                 IBottomMap bottomMap = null)
        {
            _glass = glass; _hull = hull; _environment = environment; _waypoints = waypoints;
            _bottomMap = bottomMap;
        }

        public void SetPower(WorkingInstrument instruments, bool on)
        { Powered = on ? Powered | instruments : Powered & ~instruments; }

        public void SelectWaypoint(string id) => _selectedWaypointId = id;

        public void Tick(in WorkingHelmFit fit, in FuelSettings fuelSettings)
        {
            WorkingHullState hull = default;
            bool hasHull = _hull != null && _hull.TryRead(out hull) && hull.Motion.HasBoat
                           && string.Equals(hull.HullId, fit.HullId, StringComparison.Ordinal);
            bool hasGlass = hasHull && _glass != null && string.Equals(_glass.HullId, hull.HullId, StringComparison.Ordinal);
            EnvironmentSample sea = _environment != null ? _environment.Sample() : default;

            var depth = new KeelDepthReading { Status = Status(WorkingInstrument.Depth, fit, hasGlass) };
            if (depth.Status == InstrumentStatus.Ready)
            {
                if (!_glass.TryReadDepth(out float water) || !Finite(water) || water < 0f
                    || !Finite(hull.DraughtMetres) || hull.DraughtMetres < 0f
                    || !Finite(fit.MinimumKeelClearanceMetres) || fit.MinimumKeelClearanceMetres < 0f)
                    depth.Status = InstrumentStatus.Unavailable;
                else
                {
                    SounderPrefs prefs = _glass.SounderPrefs;
                    if (!Finite(prefs.AlarmMetres) || prefs.AlarmMetres < 0f) depth.Status = InstrumentStatus.Unavailable;
                    else
                    {
                        // Preserve the existing surface-depth preference; hull clearance can only raise its threshold.
                        prefs.AlarmMetres = Math.Max(prefs.AlarmMetres, hull.DraughtMetres + fit.MinimumKeelClearanceMetres);
                        depth.WaterDepthMetres = water;
                        depth.UnderKeelMetres = water - hull.DraughtMetres;
                        depth.AlarmWaterDepthMetres = prefs.AlarmMetres;
                        depth.ShallowAlarm = DepthSounder.ShallowAlarm(water, prefs);
                        if (_bottomMap != null && _glass.TryReadPosition(out Vector2 position)
                            && NavMath.IsFinite(position))
                        {
                            BottomType bottom = _bottomMap.BottomAt(position);
                            if (bottom == BottomType.Rock || bottom == BottomType.Mud || bottom == BottomType.Sand)
                                depth.Bottom = bottom;
                        }
                    }
                }
            }
            Depth = depth;

            bool motionValid = hasHull && NavMath.IsFinite(hull.Motion.Velocity)
                               && Finite(hull.Motion.HeadingDegrees) && Finite(hull.Motion.SpeedOverGround)
                               && hull.Motion.SpeedOverGround >= 0f;
            var motion = new MotionInstrumentReading { Status = Status(WorkingInstrument.Motion, fit, motionValid) };
            if (motion.Status == InstrumentStatus.Ready)
            {
                motion.SpeedOverGroundMps = hull.Motion.SpeedOverGround;
                motion.HeadingDegrees = Mathf.Repeat(hull.Motion.HeadingDegrees, 360f);
            }
            Motion = motion;

            var wind = new WindInstrumentReading { Status = Status(WorkingInstrument.Wind, fit,
                motionValid && _environment != null && NavMath.IsFinite(sea.WindVector)) };
            if (wind.Status == InstrumentStatus.Ready)
                wind.Wind = SailDrive.ResolveWind(sea.WindVector,
                    NavMath.DirectionFromBearing(hull.Motion.HeadingDegrees), hull.Motion.Velocity);
            Wind = wind;

            var plotter = new PlotterInstrumentReading { Status = Status(WorkingInstrument.Plotter, fit, hasGlass) };
            if (plotter.Status == InstrumentStatus.Ready)
            {
                if (!_glass.TryReadPosition(out Vector2 position) || !NavMath.IsFinite(position)
                    || string.IsNullOrEmpty(hull.RegionId)) plotter.Status = InstrumentStatus.Unavailable;
                else
                {
                    plotter.OwnPosition = position;
                    if (_waypoints != null && !string.IsNullOrEmpty(_selectedWaypointId))
                    {
                        for (int i = 0; i < _waypoints.Count; i++)
                        {
                            HelmWaypoint point = _waypoints.At(i);
                            if (!point.IsValid || point.RegionId != hull.RegionId || point.Id != _selectedWaypointId) continue;
                            plotter.HasTarget = true;
                            plotter.Target = point;
                            plotter.RangeMetres = NavMath.DistanceMetres(position, point.Position);
                            plotter.HasBearing = plotter.RangeMetres > 0f;
                            plotter.BearingDegrees = NavMath.BearingDegrees(position, point.Position);
                            break;
                        }
                    }
                }
            }
            Plotter = plotter;

            var engine = new EngineInstrumentReading { Status = Status(WorkingInstrument.Engine, fit,
                hasHull && hull.HasEngine && Finite(hull.Drive)) };
            if (engine.Status == InstrumentStatus.Ready)
            {
                engine.Turning = hull.EngineTurning;
                engine.HasEstimatedRpm = Finite(fit.IdleRpm) && Finite(fit.MaximumRpm)
                                         && fit.IdleRpm > 0f && fit.MaximumRpm >= fit.IdleRpm;
                if (engine.HasEstimatedRpm && engine.Turning)
                    engine.EstimatedRpm = Mathf.Lerp(fit.IdleRpm, fit.MaximumRpm, Mathf.Clamp01(Math.Abs(hull.Drive)));
                engine.HasFuel = hull.HasFuel && Finite(hull.FuelLitres) && hull.FuelLitres >= 0f;
                if (engine.HasFuel)
                {
                    engine.FuelLitres = hull.FuelLitres;
                    if (engine.Turning && _environment != null && Finite(sea.SeaState01)
                        && Finite(hull.FullThrottleLitresPerHour) && hull.FullThrottleLitresPerHour > 0f
                        && Finite(hull.HoldFraction))
                    {
                        engine.LitresPerHour = FuelBurn.LitresPerHour(hull.FullThrottleLitresPerHour,
                            hull.Drive, hull.HoldFraction, 0f, sea.SeaState01, fuelSettings);
                        engine.HasRange = Finite(engine.LitresPerHour) && engine.LitresPerHour > 0f;
                        if (engine.HasRange)
                        {
                            float hours = hull.FuelLitres / engine.LitresPerHour;
                            engine.HasRange = Finite(hours);
                            if (engine.HasRange) engine.HoursRemaining = hours;
                        }
                    }
                }
            }
            Engine = engine;
            Bilge = Auxiliary(WorkingInstrument.Bilge, fit, hasHull && hull.HasBilge
                && Finite(hull.BilgeLitres) && hull.BilgeLitres >= 0f, hull.BilgeLitres);
            Battery = Auxiliary(WorkingInstrument.Battery, fit, hasHull && hull.HasBattery
                && Finite(hull.BatteryCharge01) && hull.BatteryCharge01 >= 0f && hull.BatteryCharge01 <= 1f, hull.BatteryCharge01);
        }

        private AuxiliaryInstrumentReading Auxiliary(WorkingInstrument instrument, in WorkingHelmFit fit, bool valid, float value)
        {
            InstrumentStatus status = Status(instrument, fit, valid);
            return new AuxiliaryInstrumentReading { Status = status, Value = status == InstrumentStatus.Ready ? value : 0f };
        }

        private InstrumentStatus Status(WorkingInstrument instrument, in WorkingHelmFit fit, bool available)
        {
            if ((fit.Instruments & instrument) == 0) return InstrumentStatus.NotFitted;
            if ((Powered & instrument) == 0) return InstrumentStatus.Off;
            return available ? InstrumentStatus.Ready : InstrumentStatus.Unavailable;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
