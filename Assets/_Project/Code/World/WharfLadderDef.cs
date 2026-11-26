using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One kind of fixed wharf ladder</b>: the numbers every ladder of that kind is built and checked
    /// against, so a ladder's size and reach are data and not a literal in a builder (rules 2 and 6).
    ///
    /// <para><b>Sized for the person who climbs it.</b> The fisher is 1.75 m; a galvanised wharf ladder
    /// is 0.45 m between rail centres with rungs every 0.30 m, which is the spacing the ladder clip
    /// plants its soles on. <see cref="MinRungPitchMetres"/>..<see cref="MaxRungPitchMetres"/> is the
    /// range of step a person of that height climbs without a stretch or a shuffle; a ladder outside it
    /// reads wrong beside him and would put the clip's feet between rungs.</para>
    ///
    /// <para><b>Its length is not stored here.</b> A ladder runs from the deck it is bolted to down past
    /// the lowest water its region's tide can reach (<see cref="WharfLadderReach"/>), so the length
    /// follows from the deck height and the region's tide profile, both recomputed rather than saved.
    /// This asset only says how far below that lowest water the foot must reach.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/WharfLadderDef", fileName = "WharfLadder")]
    public sealed class WharfLadderDef : ScriptableObject
    {
        [Tooltip("Stable, append-only id (ladder.<kind>).")]
        public string Id = "ladder.galvanised_wharf";

        [Tooltip("Distance between the two rail centres, in metres.")]
        [Min(0.01f)] public float RailSpanMetres = 0.45f;

        [Tooltip("Rung spacing, in metres. The ladder clip's soles were authored against this.")]
        [Min(0.01f)] public float RungMetres = 0.30f;

        [Tooltip("How far the climber's pivot sits off the ladder plane, in metres (the rig's ladder " +
                 "standoff).")]
        [Min(0f)] public float StandoffMetres = 0.275f;

        [Tooltip("The least rung spacing a 1.75 m climber steps comfortably, in metres.")]
        [Min(0.01f)] public float MinRungPitchMetres = 0.25f;

        [Tooltip("The greatest rung spacing a 1.75 m climber steps comfortably, in metres.")]
        [Min(0.01f)] public float MaxRungPitchMetres = 0.35f;

        [Tooltip("How far below the region's LOWEST water (spring low) the foot must reach, in metres, so " +
                 "the bottom rung is wet at every tide and a boat at spring low still finds it.")]
        [Min(0f)] public float BelowLowestWaterMetres = 0.5f;

        [Tooltip("How closely the ladder's top must meet the deck it serves, in metres.")]
        [Min(0f)] public float TopToDeckToleranceMetres = 0.05f;

        [Tooltip("The least plan distance between a ladder and any fender, pile head or other ladder on " +
                 "the same face, in metres. Closer than this and a hull lying at the ladder rubs the " +
                 "fender, or the climber's hands meet the piling.")]
        [Min(0f)] public float FaceClearanceMetres = 1.0f;
    }
}
