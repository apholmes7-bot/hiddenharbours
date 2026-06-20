using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Data definition for a hull on the Dory→Dynasty ladder. Content is data, not code
    /// (ADR 0003): make a new boat by creating one of these assets, not a new class.
    /// Create via Assets &gt; Create &gt; Hidden Harbours &gt; Boat Hull, save in Data/Boats.
    /// Stats mirror design/boats-and-navigation.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Boat Hull", fileName = "BoatHull")]
    public class BoatHullDef : ScriptableObject
    {
        [Header("Identity")]
        public string Id = "boat.dory";
        public string DisplayName = "The Dory";

        [Header("Art")]
        [Tooltip("Optional hull sprite. When a boat is granted, the fleet swaps the renderer to this " +
                 "(null-safe). Attached by art-pipeline / wired in the greybox builder.")]
        public Sprite Sprite;

        [Header("Dimensions & mass")]
        public float LengthMeters = 4.5f;
        [Tooltip("How deep the hull sits. Grounds when draught exceeds local water depth (tie to tide).")]
        public float DraughtMeters = 0.3f;
        public float MassKg = 400f;

        [Header("Capacity")]
        public int HoldUnits = 6;      // HU
        public int CrewSlots = 1;

        [Header("Propulsion & handling")]
        [Tooltip("Thrust at full throttle (design units).")]
        public float EnginePower = 1200f;
        [Tooltip("Turning authority (design units). Effective authority scales up with speed.")]
        public float RudderAuthority = 600f;
        [Tooltip("Water drag along the hull (low = glides).")]
        public float ForwardDrag = 40f;
        [Tooltip("Water drag beam-on (high = the boat tracks forward instead of sliding sideways).")]
        public float LateralDrag = 240f;
        [Tooltip("How hard the wind shoves this boat. Small craft are high; big ships low.")]
        public float WindExposure = 1.2f;

        [Header("Seaworthiness")]
        [Tooltip("Worst sea state this boat can work safely. Above it, danger rises (P5).")]
        public SeaState MaxSafeSeaState = SeaState.Lively;
    }
}
