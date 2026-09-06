using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>One scheduled trip, as a committed asset</b> — who drives, when they set off, and how fast they
    /// go. The <c>trip.*</c> half of "content is data, not code" (rule 2): a new run on an existing road
    /// is a new asset with a stable id, never a new class and never a hard-coded hour.
    ///
    /// <para><b>What is here and what is NOT.</b> Everything on this asset is a TIME or a SPEED — the
    /// owner's to move, and the two departure hours are the whole timetable (the other six blocks fall
    /// out of how long each leg takes, so a road the owner lengthens simply arrives later). The
    /// GEOMETRY is not here: which bay, which road, where the driver stands are all DERIVED by the
    /// region builder from the region's own constants, the way <c>RoutineStations</c> are, because a
    /// hand-typed route is a second copy of the village and the moment a road moves the copy stops
    /// agreeing (#345).</para>
    ///
    /// <para><b>⚠️ The window between the two hours is a GAMEPLAY ruling, not a feel knob.</b> A driver
    /// who is a shopkeeper is not at his post while he is away, and the fish buyer at Nine Mile Creek is
    /// the first money in the game. The shipped values keep him at his stall for the whole of a playable
    /// day and move his truck at the edges of it; tightening the window to a real landing window is the
    /// owner's call and is one field.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleTripDef", menuName = "Hidden Harbours/Vehicle Trip", order = 61)]
    public class VehicleTripDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): trip.snake_case — 'trip.nmc_fish_buyer_run'.")]
        public string Id = "trip.unnamed";

        public string DisplayName = "Unnamed Run";

        [Tooltip("One line on WHY this trip exists — what the village gains by it. A trip that cannot " +
                 "say what it is for is the one to cut (the dressing table's rule, applied to traffic).")]
        [TextArea(2, 4)] public string Why = "";

        [Header("The timetable (the owner's two hours)")]
        [Tooltip("The hour her driver sets off for her door at the HOME end, 0–24. A departure, not an " +
                 "arrival: she arrives when the drive is done, which is what makes a longer road arrive " +
                 "later instead of teleporting to keep a promise.")]
        [Range(0f, 24f)] public float OutboundDepartureHour = 5f;

        [Tooltip("The hour her driver leaves his FAR post to come home, 0–24.")]
        [Range(0f, 24f)] public float ReturnDepartureHour = 21f;

        [Header("Speeds")]
        [Tooltip("How fast she travels the road, m/s. Well under the machine's own ceiling: this is a " +
                 "village errand on gravel, not a delivery run. 7 m/s is about 25 km/h.")]
        [Min(0.1f)] public float CruiseMetresPerSecond = 7f;

        [Tooltip("How fast her driver walks the last few metres to her door, m/s — the same pace a " +
                 "villager keeps on a lane.")]
        [Min(0.1f)] public float WalkMetresPerSecond = 1.4f;

        /// <summary>True when this asset can actually make a trip. A window that is not a window (the two
        /// hours equal) would have her leave at the instant she arrived, so it is refused rather than
        /// shipped as a truck that flickers between two bays.</summary>
        public bool IsUsable() =>
            !string.IsNullOrEmpty(Id) &&
            CruiseMetresPerSecond > 0f && WalkMetresPerSecond > 0f &&
            !Mathf.Approximately(OutboundDepartureHour, ReturnDepartureHour);
    }
}
