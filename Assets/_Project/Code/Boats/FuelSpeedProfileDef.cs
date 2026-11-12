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
