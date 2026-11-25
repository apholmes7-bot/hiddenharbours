using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>One candidate burn curve per hydrodynamic hull class. No live registration.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fuel Speed Curve", fileName = "FuelSpeedCurve")]
    public sealed class FuelSpeedCurveDef : ScriptableObject
    {
        public string Id;
        public FuelSpeedHullClass HullClass;
        [Min(1f)] public float DisplacementExponent;
        [Range(0f, 1f)] public float HumpSpeedOverCruise;
        [Min(1f)] public float PlaningExponent;
        [Tooltip("Existing fuel design reference duty, not a measured speed or a live throttle command.")]
        [Range(0f, 1f)] public float ReferenceCruiseThrottle;

        public FuelSpeedCurve Curve => new FuelSpeedCurve(HullClass, DisplacementExponent,
            HumpSpeedOverCruise, PlaningExponent);
    }
}
