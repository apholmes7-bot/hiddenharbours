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
        [Tooltip("Optional DIRECTIONAL SKIN — how this hull LOOKS, as data (rule 2). Points at a " +
                 "BoatVisualDef binding the hull compass, the wave-coupled rock grid and the baked oar " +
                 "overlays. BoatHullSkinner is the ONE place that installs it, and OwnedFleet re-skins " +
                 "through it on a hull swap, so a bought boat changes its picture as well as its feel. " +
                 "Null (or an incomplete compass) = this hull wears the plain rotating Sprite below, " +
                 "exactly as it did before skins existed.")]
        public BoatVisualDef Visual;

        [Tooltip("Fallback hull sprite: ONE picture that rotates with the hull, for hulls with no " +
                 "directional Visual above (the Punt, the FishingSkiff). When a boat is granted, the " +
                 "fleet swaps the renderer to this (null-safe). Attached by art-pipeline / wired in the " +
                 "greybox builder.")]
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

        [Header("Seakeeping (ADR 0018 B3 — how the sea moves THIS hull)")]
        [Tooltip("Seakeeping mass factor (≥ 0): how much inertia the hull opposes to the sea. Higher = the " +
                 "waves move it LESS (a laden trader shrugs). Combined with liveliness below (response = " +
                 "liveliness / mass factor). Defaults suit a light dory that corks about; leave existing " +
                 "hull assets on the default and they load unchanged.")]
        public float SeakeepingMassFactor = 1f;
        [Tooltip("How readily the hull corks about in a sea (≥ 0). A dory is high (lively, knocked around); a " +
                 "big/heavy hull is low. Higher = the sea shoves and slews it more. Feel-tunable per hull.")]
        public float SeakeepingLiveliness = 1f;
        [Tooltip("Self-damping the hull applies to its wave-driven motion (≥ 0) — a steadier hull settles " +
                 "faster between crests, so it wanders off course less in a beam sea. A light drag against " +
                 "the boat's motion while the sea is working it; 0 = undamped.")]
        public float SeakeepingDamping = 0f;

        [Header("Camera")]
        [Tooltip("World height in metres the camera frames for this hull — bigger boat = more water.")]
        public float CameraWorldHeightMeters = 14f;

        [Header("Deck container (the diegetic catch read — tray now, blue totes up the ladder in M2)")]
        [Tooltip("The catch container this hull carries on deck. Its fill-state sprites ARE the 'how " +
                 "full is my hold' read (owner canon: no HUD counter — you look at the tray). Small " +
                 "boats carry the fish TRAY (container.fish_tray); big hulls take the North Atlantic " +
                 "blue TOTES in M2 — swap the referenced asset, never code. Null = no container shown.")]
        public DeckContainerDef DeckContainer;
        [Tooltip("Where the container sits, in the DRAWN hull's DECK FRAME (x = abeam, +starboard; " +
                 "y = along the keel, +toward the bow; metres). Rotated with the snapped facing so it " +
                 "stays on the same spot of the pictured deck at every heading (the deck-walk clamp " +
                 "convention). NOTE: hull assets serialized before this field existed deserialize it as " +
                 "(0,0) = dead amidships — author it explicitly on any hull that carries a container.")]
        public Vector2 DeckContainerOffset = new Vector2(0.35f, -0.9f);
    }
}
