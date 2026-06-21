using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>How a hull is driven — selects the control scheme in <see cref="BoatController"/>.</summary>
    public enum PropulsionType
    {
        /// <summary>Differential hand-rowing (the starting dory): per-oar strokes; turn by pulling one side.</summary>
        Oars,
        /// <summary>Throttle + rudder (boats you buy up the ladder): the existing engine helm.</summary>
        Engine,
    }

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

        [Header("Propulsion")]
        [Tooltip("How this hull is driven. Oars = differential hand-rowing (the dory); Engine = " +
                 "throttle/rudder (boats you buy). Buying an engine boat swaps hand-rowing for a helm (P4).")]
        public PropulsionType Propulsion = PropulsionType.Oars;   // the starting boat is the rowed dory

        [Header("Engine handling (Propulsion = Engine)")]
        [Tooltip("Thrust at full throttle (design units).")]
        public float EnginePower = 1200f;
        [Tooltip("Turning authority (design units). Effective authority scales up with speed.")]
        public float RudderAuthority = 600f;

        [Header("Oar handling (Propulsion = Oars)")]
        [Tooltip("Per-oar pull force at a full stroke (design units). Both oars = 2× ahead; one oar = " +
                 "thrust + a yaw the OTHER way. Feel tunable — playtest and adjust.")]
        public float OarPower = 300f;
        [Tooltip("How far each oar acts off the centreline (m) — the moment arm. Bigger = a sharper turn " +
                 "from a one-sided stroke. Feel tunable.")]
        public float OarLateralOffset = 0.6f;
        [Tooltip("Extra water drag when the oars are braced (Space) to brake/stop. Forgiving — just slows.")]
        public float OarBraceDrag = 400f;

        [Header("Hydrodynamics (shared)")]
        [Tooltip("Water drag along the hull (low = glides).")]
        public float ForwardDrag = 40f;
        [Tooltip("Water drag beam-on (high = the boat tracks forward instead of sliding sideways).")]
        public float LateralDrag = 240f;
        [Tooltip("How hard the wind shoves this boat. Small craft are high; big ships low.")]
        public float WindExposure = 1.2f;

        [Header("Seaworthiness")]
        [Tooltip("Worst sea state this boat can work safely. Above it, danger rises (P5).")]
        public SeaState MaxSafeSeaState = SeaState.Lively;

        [Header("Camera")]
        [Tooltip("World height in metres the camera frames for this hull — bigger boat = more water.")]
        public float CameraWorldHeightMeters = 14f;
    }
}
