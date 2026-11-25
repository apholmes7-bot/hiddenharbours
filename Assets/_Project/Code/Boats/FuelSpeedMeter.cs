using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>How the live fuel meter reads the speed model</b> (the owner's 11-12 ruling: fuel burn rises
    /// with speed; <c>docs/design/fuel-burn-by-speed-logic.md</c>, "Live wiring").
    ///
    /// <para>Pure and static: no Unity object is touched after <see cref="Calibration"/> has copied
    /// its numbers, no clock, no randomness, no state. <see cref="BoatFuelTank"/> is the only live
    /// caller; everything here is EditMode-testable on its own.</para>
    ///
    /// <code>
    /// drivenSpeed  = |throttle| x stillWaterTopSpeed(hull)          // what the engine is driving her at
    /// litresPerHour = curve(drivenSpeed) x loadTerm x seaTerm        // IFuelBurnBySpeed x FuelBurn.DutySurcharge
    /// </code>
    ///
    /// <para><b>The top speed is read off the hull as she is NOW; the curve is calibrated at a FIXED
    /// speed.</b> That pairing is the whole point. Raise a hull's <c>EnginePower</c> and her top speed
    /// rises, so the same throttle asks her curve for a higher speed and she burns more. Had the
    /// calibration speed been derived from the same <c>EnginePower</c>, the two would cancel and a
    /// faster hull would simply go further on a tank.</para>
    /// </summary>
    public static class FuelSpeedMeter
    {
        /// <summary>
        /// The speed (m/s) a hull settles at in still water with the throttle wide open: her thrust
        /// against everything that resists her, which is her own <c>ForwardDrag</c> AND the physics
        /// body's damping. The same balance <see cref="BoatController"/> integrates, read from the
        /// same named constants, so tuning either moves this with it. 0 for a hull with no thrust.
        /// </summary>
        public static float StillWaterTopSpeedMps(float enginePower, float forwardDrag, float massKg)
        {
            float resistance = SailDrive.LinearResistance(forwardDrag, BoatController.BodyMassFor(massKg),
                BoatController.HullLinearDamping, BoatController.ForceFeelScale);
            if (!(enginePower > 0f) || !(resistance > 0f)) return 0f;
            float speed = enginePower / resistance;
            return float.IsNaN(speed) || float.IsInfinity(speed) ? 0f : speed;
        }

        /// <summary>
        /// The speed her engine is driving her at for a throttle of -1..+1. The ABSOLUTE throttle is
        /// used, as the throttle curve always did: backing down burns fuel too. A NaN throttle is 0.
        /// </summary>
        public static float DrivenSpeedMps(float throttle, float stillWaterTopSpeedMps)
        {
            float t = throttle < 0f ? -throttle : throttle;
            if (!(t > 0f) || !(stillWaterTopSpeedMps > 0f)) return 0f;
            if (t > 1f) t = 1f;
            return t * stillWaterTopSpeedMps;
        }

        /// <summary>Litres per REAL hour at one duty: her curve at the driven speed, times the same
        /// load and sea surcharges the throttle curve charged. Zero throttle is the model's idle rate,
        /// which is the old idle rate.</summary>
        public static float LitresPerHour(IFuelBurnBySpeed model, float throttle, float stillWaterTopSpeedMps,
                                          float holdFraction, float seaState01, in FuelSettings cfg)
        {
            if (model == null) return 0f;
            float calm = model.LitresPerHour(DrivenSpeedMps(throttle, stillWaterTopSpeedMps), true);
            double rate = (double)calm * FuelBurn.DutySurcharge(holdFraction, 0f, seaState01, cfg);
            if (!(rate > 0d)) return 0f;
            return rate >= float.MaxValue ? float.MaxValue : (float)rate;
        }

        /// <summary>One fixed step of one duty, in litres.</summary>
        public static float LitresBurnedOverTick(IFuelBurnBySpeed model, float throttle,
            float stillWaterTopSpeedMps, float holdFraction, float seaState01, float deltaSeconds,
            in FuelSettings cfg)
        {
            return FuelBurn.LitresBurned(
                LitresPerHour(model, throttle, stillWaterTopSpeedMps, holdFraction, seaState01, cfg),
                deltaSeconds);
        }

        /// <summary>
        /// Build a hull's model from her copied numbers, or null when she is not calibrated or the
        /// numbers cannot make a model (an owner mid-edit). Null means "keep the throttle curve": the
        /// meter must never throw on the fixed tick over a Def value.
        /// </summary>
        public static IFuelBurnBySpeed TryCreateModel(in Calibration c, in FuelSettings cfg)
        {
            if (!c.Calibrated) return null;
            try
            {
                return FuelBurnBySpeed.FromLegacyReference(c.Curve, c.ReferenceCruiseSpeedMps,
                    c.FullThrottleLitresPerHour, c.ReferenceCruiseThrottle, true, cfg);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>
        /// Every number a hull's model is built from, copied out of her Defs and the fleet settings.
        /// Two equal calibrations build the same model, so the tank rebuilds only when one differs.
        /// </summary>
        public readonly struct Calibration
        {
            public readonly bool Calibrated;
            public readonly FuelSpeedCurve Curve;
            public readonly float ReferenceCruiseSpeedMps;
            public readonly float ReferenceCruiseThrottle;
            public readonly float FullThrottleLitresPerHour;
            private readonly float _idleFraction, _throttleExponent, _burnScale;

            public Calibration(BoatHullDef hull, FuelSpeedProfileDef profile, in FuelSettings cfg)
            {
                bool usable = hull != null && profile != null && profile.Curve != null
                              && profile.ReferenceCruiseSpeedMps > 0f
                              && hull.FuelCapacityLitres > 0f && hull.FullThrottleLitresPerHour > 0f
                              && hull.Propulsion != PropulsionType.Oars;
                Calibrated = usable;
                Curve = usable ? profile.Curve.Curve : default;
                ReferenceCruiseSpeedMps = usable ? profile.ReferenceCruiseSpeedMps : 0f;
                ReferenceCruiseThrottle = usable ? profile.Curve.ReferenceCruiseThrottle : 0f;
                FullThrottleLitresPerHour = usable ? hull.FullThrottleLitresPerHour : 0f;
                _idleFraction = cfg.IdleFraction;
                _throttleExponent = cfg.ThrottleExponent;
                _burnScale = cfg.BurnScale;
            }

            /// <summary>Field-for-field equality, typed so the per-tick compare never boxes.</summary>
            public bool Matches(in Calibration o) =>
                Calibrated == o.Calibrated
                && Curve.HullClass == o.Curve.HullClass
                && Curve.DisplacementExponent == o.Curve.DisplacementExponent
                && Curve.HumpSpeedOverCruise == o.Curve.HumpSpeedOverCruise
                && Curve.PlaningExponent == o.Curve.PlaningExponent
                && ReferenceCruiseSpeedMps == o.ReferenceCruiseSpeedMps
                && ReferenceCruiseThrottle == o.ReferenceCruiseThrottle
                && FullThrottleLitresPerHour == o.FullThrottleLitresPerHour
                && _idleFraction == o._idleFraction
                && _throttleExponent == o._throttleExponent
                && _burnScale == o._burnScale;
        }
    }
}
