using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>Separate candidate binding: existing hull Defs and their numbers stay untouched.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fuel Speed Profile", fileName = "FuelSpeedProfile")]
    public sealed class FuelSpeedProfileDef : ScriptableObject
    {
        public string Id;
        public BoatHullDef Hull;
        public FuelSpeedCurveDef Curve;

        [Tooltip("The speed through still water (m/s) at which this hull burns her OLD cruise rate " +
                 "(the throttle curve at the curve's reference cruise throttle). Faster than this she " +
                 "burns more per hour along her curve, slower she burns less.\n\n" +
                 "Authored as 0.7 x her still-water top speed on main ce70abd495, BEFORE the 11-12 " +
                 "speed table. It is deliberately a fixed number and NOT derived from EnginePower: " +
                 "give her a bigger engine and she goes faster AND drinks more, instead of the fuel " +
                 "curve quietly stretching to keep her as cheap as she was.\n\n" +
                 "Raise it to make her cheaper to run at every speed; lower it to make her thirstier. " +
                 "0 = not calibrated: the live meter keeps the old throttle curve for this hull.")]
        [Min(0f)] public float ReferenceCruiseSpeedMps;

        /// <summary>True when the live meter can price this hull by speed.</summary>
        public bool IsCalibrated => Hull != null && Curve != null && ReferenceCruiseSpeedMps > 0f;

        /// <summary>Composition must supply a measured calm/light cruise speed through water.
        /// This factory does not sample physics, infer a speed, mutate fuel, or register a service.</summary>
        public IFuelBurnBySpeed CreateModel(float measuredCruiseSpeedMps, in FuelSettings legacySettings)
        {
            if (Hull == null || Curve == null) throw new InvalidOperationException("Incomplete fuel speed profile.");
            bool hasFuelEngine = Hull.FuelCapacityLitres > 0f && Hull.Propulsion != PropulsionType.Oars;
            return FuelBurnBySpeed.FromLegacyReference(Curve.Curve, measuredCruiseSpeedMps,
                Hull.FullThrottleLitresPerHour, Curve.ReferenceCruiseThrottle, hasFuelEngine, legacySettings);
        }
    }
}
