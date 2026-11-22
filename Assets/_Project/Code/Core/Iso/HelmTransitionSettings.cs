using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How long taking and leaving a helm takes on the mesh figure</b>, and how the move is split
    /// between going to the spot and settling into the pose. Read by the player's mesh presenter
    /// (<see cref="HelmTransition"/>); visual only. Control is never held up by it: the helm goes live
    /// on the press, as it always did, and the figure catches up inside these times.
    ///
    /// <para><b>Seat</b> times are for a seated station (a tiller seat, a seated helm, the oars);
    /// <b>wheel</b> times for a standing helm. A time of 0 is the old one-frame snap for that move.</para>
    /// </summary>
    [System.Serializable]
    public struct HelmTransitionSettings
    {
        [Tooltip("Off = every helm hop is the old one-frame snap.")]
        public bool Enabled;

        [Tooltip("Seconds to go from standing on deck to seated at a tiller, a seated helm or the oars.")]
        [Min(0f)] public float SeatTakeSeconds;

        [Tooltip("Seconds to get up from a seated station to standing on deck.")]
        [Min(0f)] public float SeatLeaveSeconds;

        [Tooltip("Seconds to step up to a standing wheel and take hold.")]
        [Min(0f)] public float WheelTakeSeconds;

        [Tooltip("Seconds to let go of a standing wheel and stand free on deck.")]
        [Min(0f)] public float WheelLeaveSeconds;

        [Tooltip("Share of the move spent going to the spot and turning to face the station. Taking, it " +
                 "is the START of the move (walk up, then sit); leaving, the END (stand up, then step off).")]
        [Range(0.05f, 1f)] public float RootShare;

        [Tooltip("Share of the move before the body starts to settle into the station's pose when taking " +
                 "(mirrored when leaving: the body is up by this share before the end).")]
        [Range(0f, 0.95f)] public float BodyStart;

        [Tooltip("A hop whose drawn figure is further than this from where it is going (metres, on the " +
                 "deck) is not a step but a re-seat (a region arrival, a hull swapped under him): it snaps.")]
        [Min(0f)] public float MaxTravelMeters;

        /// <summary>The shipped values: about half a second each way.</summary>
        public static HelmTransitionSettings Default => new HelmTransitionSettings
        {
            Enabled = true,
            SeatTakeSeconds = 0.6f,
            SeatLeaveSeconds = 0.5f,
            WheelTakeSeconds = 0.45f,
            WheelLeaveSeconds = 0.4f,
            RootShare = 0.7f,
            BodyStart = 0.3f,
            MaxTravelMeters = 3f,
        };

        /// <summary>The move's length in seconds; 0 = snap. <paramref name="seated"/> names the seated end
        /// of the hop: where he is going when taking, where he is coming from when leaving.</summary>
        public float SecondsFor(bool taking, bool seated)
        {
            if (!Enabled) return 0f;
            float s = taking ? (seated ? SeatTakeSeconds : WheelTakeSeconds)
                             : (seated ? SeatLeaveSeconds : WheelLeaveSeconds);
            return Mathf.Max(0f, s);
        }
    }
}
