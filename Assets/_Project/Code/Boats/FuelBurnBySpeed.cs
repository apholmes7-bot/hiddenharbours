using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Pure, immutable candidate. Replaces nothing. Calibrate once at a supplied reference cruise
    /// speed, then call through the Core interface. No hull speed is inferred from thrust or mass.
    /// </summary>
    public sealed class FuelBurnBySpeed : IFuelBurnBySpeed
    {
        private readonly FuelSpeedCurve _curve;
        private readonly double _cruiseSpeed;
        private readonly double _idleRate;
        private readonly double _cruiseRate;
        private readonly double _normalizer;
        private readonly double _humpResistance;
        private readonly double _planingScale;
        private readonly double _humpPlaningPower;

        public FuelBurnBySpeed(in FuelSpeedCurve curve, float cruiseSpeedMps,
                               float cruiseLitresPerHour, float idleLitresPerHour)
        {
            RequireFinite(cruiseSpeedMps, nameof(cruiseSpeedMps));
            RequireFinite(cruiseLitresPerHour, nameof(cruiseLitresPerHour));
            RequireFinite(idleLitresPerHour, nameof(idleLitresPerHour));
            if (cruiseSpeedMps <= 0f || idleLitresPerHour < 0f ||
                cruiseLitresPerHour < 0f || idleLitresPerHour > cruiseLitresPerHour ||
                (cruiseLitresPerHour > 0f && idleLitresPerHour == cruiseLitresPerHour))
                throw new ArgumentOutOfRangeException(nameof(cruiseLitresPerHour),
                    "Require positive cruise speed and 0 <= idle < cruise burn, or both burns zero.");
            RequireFinite(curve.DisplacementExponent, nameof(curve.DisplacementExponent));
            RequireFinite(curve.PlaningExponent, nameof(curve.PlaningExponent));
            RequireFinite(curve.HumpSpeedOverCruise, nameof(curve.HumpSpeedOverCruise));
            if (curve.DisplacementExponent <= 1f || curve.PlaningExponent <= 1f ||
                curve.PlaningExponent >= curve.DisplacementExponent ||
                curve.HumpSpeedOverCruise <= 0f || curve.HumpSpeedOverCruise >= 1f ||
                (curve.HullClass != FuelSpeedHullClass.Displacement &&
                 curve.HullClass != FuelSpeedHullClass.Planing))
                throw new ArgumentOutOfRangeException(nameof(curve));

            _curve = curve;
            _cruiseSpeed = cruiseSpeedMps;
            _idleRate = idleLitresPerHour;
            _cruiseRate = cruiseLitresPerHour;
            _humpResistance = Math.Pow(curve.HumpSpeedOverCruise, curve.DisplacementExponent);
            _planingScale = (double)curve.DisplacementExponent / curve.PlaningExponent *
                Math.Pow(curve.HumpSpeedOverCruise, (double)curve.DisplacementExponent - curve.PlaningExponent);
            _humpPlaningPower = Math.Pow(curve.HumpSpeedOverCruise, curve.PlaningExponent);
            _normalizer = Resistance(1d);
            if (!(_normalizer > 0d) || double.IsInfinity(_normalizer))
                throw new ArgumentOutOfRangeException(nameof(curve));
        }

        /// <summary>Calm/light legacy duty; preserves BOTH idle and reference-cruise burn.
        /// No tank or no engine (rowed dory) is explicitly disabled by the caller. An auxiliary can
        /// be calibrated, but this does not enable the current live meter for Sail propulsion.</summary>
        public static FuelBurnBySpeed FromLegacyReference(in FuelSpeedCurve curve, float cruiseSpeedMps,
            float fullThrottleLitresPerHour, float referenceCruiseThrottle, bool hasFuelEngine,
            in FuelSettings legacySettings)
        {
            RequireFinite(fullThrottleLitresPerHour, nameof(fullThrottleLitresPerHour));
            RequireFinite(referenceCruiseThrottle, nameof(referenceCruiseThrottle));
            if (fullThrottleLitresPerHour < 0f || (hasFuelEngine && fullThrottleLitresPerHour == 0f) ||
                referenceCruiseThrottle <= 0f || referenceCruiseThrottle > 1f)
                throw new ArgumentOutOfRangeException(nameof(referenceCruiseThrottle));
            float cruise = hasFuelEngine ? FuelBurn.LitresPerHour(fullThrottleLitresPerHour,
                referenceCruiseThrottle, 0f, 0f, 0f, legacySettings) : 0f;
            float idle = hasFuelEngine ? FuelBurn.LitresPerHour(fullThrottleLitresPerHour,
                0f, 0f, 0f, 0f, legacySettings) : 0f;
            return new FuelBurnBySpeed(curve, cruiseSpeedMps, cruise, idle);
        }

        public float LitresPerHour(float throughWaterSpeedMps, bool engineRunning)
        {
            if (!engineRunning || _cruiseRate == 0d) return 0f;
            RequireFinite(throughWaterSpeedMps, nameof(throughWaterSpeedMps));
            double x = Math.Abs((double)throughWaterSpeedMps) / _cruiseSpeed;
            if (x == 1d) return (float)_cruiseRate;
            double rate = _idleRate + (_cruiseRate - _idleRate) * Resistance(x) / _normalizer;
            // Saturation protects a caller from numeric overflow, without making a faster speed cheaper.
            return rate >= float.MaxValue ? float.MaxValue : (float)rate;
        }

        public float LitresBurnedOverTick(float throughWaterSpeedMps, bool engineRunning, float deltaSeconds)
        {
            RequireFinite(deltaSeconds, nameof(deltaSeconds));
            if (deltaSeconds <= 0f) return 0f;
            double used = (double)LitresPerHour(throughWaterSpeedMps, engineRunning) *
                          deltaSeconds / FuelBurn.SecondsPerHour;
            return used >= float.MaxValue ? float.MaxValue : (float)used;
        }

        private double Resistance(double x)
        {
            double n = _curve.DisplacementExponent;
            double h = _curve.HumpSpeedOverCruise;
            if (_curve.HullClass == FuelSpeedHullClass.Displacement || x <= h)
                return Math.Pow(x, n);
            double p = _curve.PlaningExponent;
            // Positive slope on both sides; value and first derivative meet at the hump.
            // Above the hump the exponent falls, representing a flatter rise in hourly burn.
            return _humpResistance + _planingScale * (Math.Pow(x, p) - _humpPlaningPower);
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
