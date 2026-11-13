using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>Explicitly composed read adapter; adds no component and registers no global service.</summary>
    public sealed class WorkingHelmHullProbe : IWorkingHullInstruments
    {
        private readonly BoatController _boat;
        private readonly IFuelVessel _fuel;
        private readonly IHold _hold;
        public string RegionId { get; set; }

        // Cache scene component discovery at setup; never search the scene on a tick.
        public WorkingHelmHullProbe(BoatController boat, IFuelVessel fuel, IHold hold, string regionId)
        { _boat = boat; _fuel = fuel; _hold = hold; RegionId = regionId; }

        public bool TryRead(out WorkingHullState state)
        {
            state = default;
            if (_boat == null || !_boat.isActiveAndEnabled || _boat.Hull == null) return false;
            BoatHullDef hull = _boat.Hull;
            bool hasFuel = _fuel != null && !(_fuel is Object o && o == null) && _fuel.CapacityLitres > 0f;
            float litres = hasFuel ? _fuel.Litres : 0f;
            bool engine = hull.Propulsion == PropulsionType.Engine;
            bool hasHold = _hold != null && !(_hold is Object held && held == null) && _hold.CapacityUnits > 0;
            state = new WorkingHullState
            {
                HullId = hull.Id, RegionId = RegionId,
                Motion = BoatKinematics.FromBow(_boat.transform.up, _boat.Velocity),
                DraughtMetres = hull.DraughtMeters,
                HasEngine = engine, EngineTurning = engine && litres > 0f,
                HasFuel = hasFuel, FuelLitres = litres, Drive = _boat.Throttle,
                FullThrottleLitresPerHour = hull.FullThrottleLitresPerHour,
                HoldFraction = hasHold ? Mathf.Clamp01((float)_hold.UsedUnits / _hold.CapacityUnits) : 0f,
                // Main has neither simulation. Never manufacture healthy/empty values as readings.
                HasBilge = false, HasBattery = false
            };
            return true;
        }
    }
}
