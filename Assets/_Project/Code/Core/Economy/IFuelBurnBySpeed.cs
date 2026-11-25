namespace HiddenHarbours.Core
{
    /// <summary>
    /// Prospective engine fuel cost at steady, engine-driven speed THROUGH WATER (m/s).
    /// A caller supplies engine state; sailing or being carried by current must not start an engine.
    /// Rates use real running hours, independent of game-day length. No service is registered here.
    /// </summary>
    public interface IFuelBurnBySpeed
    {
        float LitresPerHour(float throughWaterSpeedMps, bool engineRunning);
        float LitresBurnedOverTick(float throughWaterSpeedMps, bool engineRunning, float deltaSeconds);
    }

    /// <summary>Hydrodynamic families of the candidate curve, not a new hull speed setting.</summary>
    public enum FuelSpeedHullClass { Displacement, Planing }

    /// <summary>Dimensionless class tuning copied from a Def once, before ticking.</summary>
    public readonly struct FuelSpeedCurve
    {
        public readonly FuelSpeedHullClass HullClass;
        public readonly float DisplacementExponent;
        public readonly float HumpSpeedOverCruise;
        public readonly float PlaningExponent;

        public FuelSpeedCurve(FuelSpeedHullClass hullClass, float displacementExponent,
                              float humpSpeedOverCruise, float planingExponent)
        {
            HullClass = hullClass;
            DisplacementExponent = displacementExponent;
            HumpSpeedOverCruise = humpSpeedOverCruise;
            PlaningExponent = planingExponent;
        }
    }
}
